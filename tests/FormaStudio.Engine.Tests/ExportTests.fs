module ExportTests

open System.Text.RegularExpressions
open FormaStudio.Engine
open Harness

let private sample () = okOr (Samples.purchaseWorkflow ()) "sample"
let private d = Samples.diagramId
let private project mode p = okOr (Projection.project mode p d) "projection"
let private node (n: string) = NodeRef(d, idOf n)

let private path (json: JsonValue) (names: string list) =
    names |> List.fold (fun (j: JsonValue option) name -> j |> Option.bind (Json.field name)) (Some json)

let private arrayAt json names =
    match path json names with
    | Some(JArray items) -> items
    | other -> fail (sprintf "expected an array at %A, got %A" names other)

/// Source-only metadata must not reach any output text (FDA-823, EPC-META-061).
let private secrets = [ "CC-7731-RESTRICTED"; "Cost center"; "cost-center" ]

let agentExportIsSemantic =
    test "Agent export exposes topology, metadata status, references and color provenance" (fun () ->
        let p = sample ()
        let export = AgentExport.export p
        let text = AgentExport.serialize p
        let diagram = arrayAt export [ "diagrams" ] |> List.head
        equal 7 (arrayAt diagram [ "nodes" ]).Length "all nodes"
        equal 7 (arrayAt diagram [ "edges" ]).Length "all edges"
        equal (Some(JBool true)) (path diagram [ "specificationOnly" ]) "not executable"
        let approve = arrayAt diagram [ "nodes" ] |> List.find (fun n -> Json.field "nodeId" n = Some(JString "approve"))
        let states = arrayAt approve [ "metadata" ] |> List.map (fun m -> Json.field "field" m, Json.field "state" m) |> Map.ofList
        equal (Some(JString "explicit")) states.[Some(JString "status")] "explicit status"
        equal (Some(JString "derived")) states.[Some(JString "owner")] "derived owner"
        expect (states.ContainsKey(Some(JString "ticket"))) "export-scope field present"
        equal (Some(JString "metadata-mapping")) (path approve [ "appearance"; "effective"; "fill"; "layer" ]) "mapped fill layer"
        equal (Some(JString "palette")) (path approve [ "appearance"; "effective"; "fill"; "sourceKind" ]) "fill source kind"
        equal (Some(JString "status")) (path approve [ "appearance"; "effective"; "fill"; "field" ]) "mapping source field"
        let prepare = arrayAt diagram [ "nodes" ] |> List.find (fun n -> Json.field "nodeId" n = Some(JString "prepare"))
        equal (Some(JString "object-override")) (path prepare [ "appearance"; "effective"; "fill"; "layer" ]) "override layer"
        let refs = arrayAt approve [ "references" ] |> List.map (fun r -> Json.field "kind" r)
        equal [ Some(JString "requirement"); Some(JString "issue") ] refs "typed references"
        expect (text.Contains "\"precedence\"") "precedence documented in export"
        for secret in secrets do
            expect (not (text.Contains secret)) (sprintf "agent export leaked %s" secret))

let projectionUsesFormaContract =
    test "Projection emits only public Forma diagram contracts and a text relationship per connector" (fun () ->
        let projection = project Strict (sample ())
        let html = projection.Html
        let classes = Regex.Matches(html, "class=\"([^\"]+)\"") |> Seq.collect (fun m -> m.Groups.[1].Value.Split ' ') |> Set.ofSeq
        expect (classes |> Set.forall (fun c -> c.StartsWith "ef-diagram")) (sprintf "only ef-diagram* classes: %A" classes)
        let connectors = Regex.Matches(html, "<path class=\"ef-diagram-connector\"").Count
        let relations = Regex.Match(html, "<ol class=\"ef-diagram__relations\"[^>]*>(.*?)</ol>", RegexOptions.Singleline).Groups.[1].Value
        equal connectors (Regex.Matches(relations, "<li>").Count) "one relationship item per connector"
        expect (html.Contains "data-ef-shape=\"diamond\"") "decision uses the diamond contract"
        expect (html.Contains "data-ef-group=\"lane\"") "lanes use the group contract"
        expect (html.Contains "data-ef-line=\"dashed\"") "non-color line cue kept"
        expect (html.Contains "data-ef-value-state=\"derived\"") "value state rendered as text")

let projectionSanitizes =
    test "Projection escapes untrusted text and never emits executable content" (fun () ->
        let hostile = "<img src=x onerror=alert(1)> & \"quotes\""
        let p =
            (Editor.start (sample ()) |> runAll [ Flow(SetNodeLabel(d, idOf "prepare", hostile)) ]).Project
        let html = (project Strict p).Html
        expect (not (html.Contains "<img")) "no injected element"
        expect (html.Contains "&lt;img src=x onerror=alert(1)&gt; &amp; &quot;quotes&quot;") "escaped label"
        expect (not (Regex.IsMatch(html, "<[^>]*\\son[a-z]+=", RegexOptions.IgnoreCase))) "no event handler attributes"
        expect (not (html.Contains "<script")) "no script"
        expect (not (html.ToLowerInvariant().Contains "javascript:")) "no javascript URLs"
        let styles = Regex.Matches(html, "style=\"([^\"]*)\"") |> Seq.map (fun m -> m.Groups.[1].Value)
        for style in styles do
            expect (Regex.IsMatch(style, "^(--ef-diagram-[a-z-]+: (-?\\d+px|\\d+|#[0-9a-f]{6}|var\\(--ef-color-[a-z-]+\\));\\s?)+$")) (sprintf "style is limited to validated values: %s" style))

let projectionPrivacy =
    test "Source-only metadata and derived presentation from withheld fields never reach the projection" (fun () ->
        let p = sample ()
        let projection = project Strict p
        let manifest = Json.serialize projection.Manifest
        for secret in secrets do
            expect (not (projection.Html.Contains secret)) (sprintf "HTML leaked %s" secret)
            expect (not (manifest.Contains secret)) (sprintf "manifest leaked %s" secret)
        for exportOnly in [ "tickets.example.com"; "audit"; "Ticket"; "Tags" ] do
            expect (not (projection.Html.Contains exportOnly)) (sprintf "export-only field rendered: %s" exportOnly)
        // Withdraw derived-presentation permission for status: the mapped color and
        // its legend category disappear from output, but the editor still sees them.
        let restricted =
            (Editor.start p |> runAll [ MetadataCmd(SetFieldDisclosure(idOf "status", { Scopes = set [ Rendered; AgentExport ]; DerivedPresentation = false })) ]).Project
        let after = project Strict restricted
        expect (not (after.Html.Contains "#fde8d7")) "mapped fill withheld"
        expect (not (after.Html.Contains "Status is Blocked")) "legend category withheld"
        expect (after.Html.Contains "Blocked") "raw status is still rendered because its scope allows it"
        let editorFill = (AppearanceResolution.resolve EditorScope restricted (node "approve") |> fst).Fill
        equal (Some "#fde8d7") (editorFill.Value |> Option.bind _.Css) "editor-only highlight remains")

let projectionManifest =
    test "Manifest carries source identity, bounds, provenance and object linkage" (fun () ->
        let p = sample ()
        let projection = project Strict p
        let m = projection.Manifest
        equal (Some(JString "1.0.0")) (Json.field "projectionVersion" m) "version"
        equal (Some(JString "purchase-request")) (path m [ "source"; "diagramId" ]) "diagram id"
        equal (Some(JString(Codec.diagramRevision p (ProjectOps.tryDiagram d p |> Option.get)))) (path m [ "source"; "revision" ]) "revision"
        equal (Some(JString "2.0.0")) (path m [ "presentation"; "contractVersion" ]) "Forma contract version"
        for o in arrayAt m [ "objects" ] do
            match Json.field "elementId" o with
            | Some(JString id) -> expect (projection.Html.Contains(sprintf "id=\"%s\"" id)) (sprintf "object %s is linked in HTML" id)
            | _ -> fail "object without elementId"
        match path m [ "content"; "sha256" ] with
        | Some(JString digest) -> expect (digest.StartsWith "sha256:" && digest.Length = 71) "content digest"
        | _ -> fail "missing content digest"
        let bounds = [ "x"; "y"; "width"; "height" ] |> List.map (fun k -> path m [ "bounds"; k ])
        equal [ Some(JNumber "0"); Some(JNumber "-24"); Some(JNumber "1060"); Some(JNumber "794") ] bounds "deterministic content bounds include manual routes"
        expect (arrayAt m [ "excluded" ] |> List.contains (JString "selection")) "editor state excluded")

let projectionIsDeterministicAndFresh =
    test "Projection is deterministic, and a changed diagram makes an old projection stale" (fun () ->
        let p = sample ()
        let first = project Strict p
        let reloaded = okOr (Codec.load (Codec.serialize p) |> Result.mapError Codec.describeLoadError) "reload"
        let second = project Strict reloaded
        equal first.Html second.Html "same HTML after reload"
        equal (Json.serialize first.Manifest) (Json.serialize second.Manifest) "same manifest after reload"
        equal (Ok()) (Projection.checkFresh first.Manifest p) "fresh"
        let moved = (Editor.start p |> runAll [ Flow(MoveNodes(d, [ idOf "approve", 10.0, 0.0 ])) ])
        expect (Projection.checkFresh first.Manifest moved.Project |> Result.isError) "stale after a move"
        equal (Ok()) (Projection.checkFresh first.Manifest (Editor.undo moved).Project) "fresh again after undo"
        let restyled = (Editor.start p |> runAll [ AppearanceCmd(UpdateStyle(idOf "decision-emphasis", Appearance.empty)) ]).Project
        expect (Projection.checkFresh first.Manifest restyled |> Result.isError) "a style change also makes it stale")

let strictProjectionFailsSafely =
    test "Strict projection refuses unavailable profiles; diagnostic output is labelled invalid" (fun () ->
        let p = sample ()
        let text = (Codec.serialize p).Replace("\"id\": \"workflow\"", "\"id\": \"workflow-next\"")
        let unknown = okOr (Codec.load text |> Result.mapError Codec.describeLoadError) "load"
        let findings = errorOf (Projection.project Strict unknown d) "strict with unknown profile"
        expect (findings |> List.exists (fun f -> f.Code = "profile.unavailable")) "unavailable profile named"
        let diagnostic = okOr (Projection.project Diagnostic unknown d) "diagnostic"
        expect (diagnostic.Html.Contains "Invalid or incomplete diagram") "diagnostic label"
        equal (Some(JString "invalid-diagnostic")) (Json.field "status" diagnostic.Manifest) "status")

let colorIndependentMeaning =
    test "Every node states its kind in text and every colored category has a text legend entry" (fun () ->
        let projection = project Strict (sample ())
        let kinds = Regex.Matches(projection.Html, "<p class=\"ef-diagram-node__kind\">([^<]+)</p>") |> Seq.map (fun m -> m.Groups.[1].Value) |> List.ofSeq
        equal 7 kinds.Length "kind text on every node"
        expect (kinds |> List.forall (fun k -> k.Length > 0)) "non-empty kinds"
        let legend = arrayAt projection.Manifest [ "legend" ] |> List.choose (Json.field "label")
        expect (List.contains (JString "Status is Blocked") legend) "mapping explained"
        expect (List.contains (JString "Author highlight") legend) "author color explained as carrying no status")

let all =
    [ agentExportIsSemantic; projectionUsesFormaContract; projectionSanitizes; projectionPrivacy; projectionManifest
      projectionIsDeterministicAndFresh; strictProjectionFailsSafely; colorIndependentMeaning ]

let committedFixturesAreCurrent =
    test "Committed purchase-request fixtures match what the engine generates today" (fun () ->
        let dir = System.IO.Path.Combine(__SOURCE_DIRECTORY__, "../../examples/purchase-request")
        let read (name: string) = System.IO.File.ReadAllText(System.IO.Path.Combine(dir, name))
        let p = sample ()
        equal (Codec.serialize p) (read "project.json") "project.json (regenerate with the CLI 'sample' command)"
        equal (AgentExport.serialize p) (read "agent-export.json") "agent-export.json"
        let projection = project Strict p
        equal (Json.serialize projection.Manifest) (read "folio-projection/projection.json") "projection.json"
        equal projection.Html (read "folio-projection/diagram.html") "diagram.html")

let allWithFixtures = all @ [ committedFixturesAreCurrent ]
