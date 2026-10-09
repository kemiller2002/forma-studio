/// Forma icons in Studio (forma-studio#27): a typed optional icon name on Layout
/// components and diagram nodes, preserved through persistence even when the
/// pinned Forma release does not know it, verified against the pinned release's
/// compiled assets, and inlined into portable HTML only from those assets.
///
/// Positive cases use the committed test fixture under
/// tests/fixtures/forma-icons-test-fixture (synthetic shapes, not Forma artwork).
/// The "no icons" cases use an icon-less release (no registry, as Forma 0.4.1 and
/// earlier) or no catalog at all; the pinned package (Forma 0.5.0) is verified
/// in full by "pinnedPackage".
module IconTests

open System
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.RegularExpressions
open FormaStudio.Engine
open Harness

let private repo =
    let rec up (d: DirectoryInfo | null) =
        match d with
        | null -> failwith "repository root not found"
        | d when File.Exists(Path.Combine(d.FullName, "REQUIREMENTS.md")) -> d.FullName
        | d -> up d.Parent
    up (DirectoryInfo(AppContext.BaseDirectory))

let private fixtureDir = Path.Combine(repo, "tests", "fixtures", "forma-icons-test-fixture")
let private fixture (relative: string) = File.ReadAllText(Path.Combine(fixtureDir, relative))
let private readFixture (relative: string) = Ok(fixture relative)

let private name raw = okOr (IconName.parse raw) raw
let private sha (text: string) = SHA256.HashData(Encoding.UTF8.GetBytes text) |> Convert.ToHexString |> _.ToLowerInvariant()

/// The fixture collection, with optional replacements for individual files.
let private catalogWith (overrides: Map<string, string>) =
    IconCatalog.fromFiles (fixture "registry.json") (fun relative -> Ok(overrides |> Map.tryFind relative |> Option.defaultWith (fun () -> fixture relative)))

let private fixtureCatalog () =
    match catalogWith Map.empty with
    | IconsAvailable catalog -> catalog
    | other -> fail (sprintf "the test fixture should verify, got %A" other)

let private refusedNames (availability: IconAvailability) =
    match availability with
    | IconsAvailable c -> c.Rejected |> List.map fst
    | IconsUnavailable _ -> [ "test-line"; "test-box"; "test-dot" ]
    | IconsNotLoaded -> []

// -- a small project with one Layout page and one diagram ----------------------

let private page: PageId = idOf "home"
let private stack: ComponentNodeId = idOf "stack-1"
let private heading: ComponentNodeId = idOf "heading-1"
let private metric: ComponentNodeId = idOf "metric-1"
let private button: ComponentNodeId = idOf "button-1"
let private field: ComponentNodeId = idOf "field-1"
let private d: DiagramId = idOf "d1"
let private nodeA: NodeId = idOf "a"
let private general = { Id = "general"; Version = "1.0.0" }

let private layoutSession () =
    let slot = Some { Parent = stack; Slot = "children" }
    Editor.start (Samples.emptyProject "icons" "Icons")
    |> runAll
        [ Layout(AddPage(page, "Home", Some "/"))
          Layout(AddComponent(page, None, 0, stack, "stack"))
          Layout(AddComponent(page, slot, 0, heading, "heading"))
          Layout(SetComponentContent(page, heading, "text", "Crew schedule"))
          Layout(AddComponent(page, slot, 1, metric, "metric-card"))
          Layout(SetComponentContent(page, metric, "label", "Open requests"))
          Layout(AddComponent(page, slot, 2, button, "button"))
          Layout(SetComponentContent(page, button, "label", "Search crews"))
          Layout(AddComponent(page, slot, 3, field, "text-field"))
          Layout(SetComponentContent(page, field, "label", "Name"))
          Flow(AddDiagram(d, "Flow", general))
          Flow(AddNode(d, nodeA, "box", "Alpha", 0.0, 0.0, 120.0, 60.0)) ]

let private setIcon target raw = AppearanceCmd(SetIcon(target, Some(name raw)))
let private metricRef = ComponentRef(page, metric)
let private buttonRef = ComponentRef(page, button)
let private nodeRef = NodeRef(d, nodeA)

let private reload project = okOr (Codec.load (Codec.serialize project) |> Result.mapError Codec.describeLoadError) "reload"

let private pageOf (p: Project) = p.Pages |> List.find (fun x -> x.Id = page)

/// Rewrites the "icon" value of every object that has one, as a hand-edited file would.
let private withRawIcons (raw: string) (project: Project) =
    let text = Codec.serialize project
    Regex.Replace(text, "\"icon\": \"[a-z0-9-]+\"", "\"icon\": " + raw)

// -- names ------------------------------------------------------------------------

let names =
    test "Icon names follow the registry grammar; everything else is not a name" (fun () ->
        for ok in [ "search"; "arrow-left"; "more-horizontal"; "a"; "a1"; "chevron-2-up"; String('a', 80) ] do
            expect (IconName.parse ok |> Result.isOk) (sprintf "%s is a name" ok)
        for bad in
            [ ""; "Search"; "search icon"; "../evil"; "a--b"; "-a"; "a-"; "1abc"; "search\n"; "search\r\n"; String('a', 81); "<script>alert(1)</script>"
              "javascript:alert(1)"; "a_b"; "séarch"; "ｓearch"; "icons/search.svg"; "search.svg"; " search"; "search "; "\u0000search" ] do
            expect (IconName.parse bad |> Result.isError) (sprintf "%A is not a name" bad))

let storedValues =
    test "A stored icon is read without rejection: names as names, anything else kept verbatim" (fun () ->
        equal (NamedIcon(name "future-glyph")) (IconRef.ofJson (JString "future-glyph")) "a well-formed unknown name is a name"
        for raw in [ JString "<script>alert(1)</script>"; JString "javascript:alert(1)"; JString "https://evil.example/x.svg"; JNumber "42"; JBool true
                     JObject [ "href", JString "x" ]; JArray [ JString "search" ] ] do
            match IconRef.ofJson raw with
            | MalformedIcon kept -> equal raw kept "kept verbatim"
            | other -> fail (sprintf "%A should be malformed, got %A" raw other)
            equal raw (IconRef.toJson (IconRef.ofJson raw)) "written back exactly")

// -- persistence ----------------------------------------------------------------

let persistence =
    test "Icons round-trip through save and reload; node icons are schema 3, Layout icons the schema 2 icon property" (fun () ->
        let plain = (layoutSession ()).Project
        expect ((Codec.serialize plain).Contains "\"schemaVersion\": 2") "no icon: still schema 2, byte-compatible with older Studio builds"
        let layoutOnly = (layoutSession () |> runAll [ setIcon metricRef "test-line" ]).Project
        expect ((Codec.serialize layoutOnly).Contains "\"schemaVersion\": 2") "a Layout icon is the component's icon property (#33), which schema 2 readers keep"
        equal (Some(JString "test-line")) ((ProjectOps.tryComponent page metric layoutOnly |> Option.get).Properties |> Map.tryFind "icon") "stored as properties.icon"
        equal layoutOnly (reload layoutOnly) "and round-trips"
        let withIcons = layoutSession () |> runAll [ setIcon metricRef "test-line"; setIcon nodeRef "future-glyph" ]
        let text = Codec.serialize withIcons.Project
        expect (text.Contains "\"schemaVersion\": 3") "node icons: schema 3, so a schema 2 reader refuses instead of dropping them"
        let loaded = reload withIcons.Project
        equal withIcons.Project loaded "the reloaded document is identical, including an icon no release has yet"
        equal text (Codec.serialize loaded) "serialization is idempotent"
        equal (Some(NamedIcon(name "future-glyph"))) (ProjectOps.iconOf nodeRef loaded) "unknown names are kept"
        let cleared = Editor.start loaded |> runAll [ AppearanceCmd(SetIcon(metricRef, None)); AppearanceCmd(SetIcon(nodeRef, None)) ]
        expect ((Codec.serialize cleared.Project).Contains "\"schemaVersion\": 2") "removing every icon returns to schema 2"
        // A schema 2 file that already names icons (hand-edited, or from another tool) is read, not dropped.
        let v2 = text.Replace("\"schemaVersion\": 3", "\"schemaVersion\": 2")
        equal withIcons.Project (okOr (Codec.load v2 |> Result.mapError Codec.describeLoadError) "schema 2 with icons") "schema 2 icons are kept")

let malformedPreserved =
    test "Malformed and injected icon values are kept as inert data: never rejected, dropped, rewritten or blocking" (fun () ->
        let named = (layoutSession () |> runAll [ setIcon metricRef "test-line"; setIcon nodeRef "test-dot" ]).Project
        for raw in [ "\"<script>alert(1)</script>\""; "\"javascript:alert(1)\""; "\"Search\""; "42"; "{\"href\": \"https://evil.example/x.svg\"}"; "[\"search\"]"; "\"search\\n\"" ] do
            let text = withRawIcons raw named
            let loaded = okOr (Codec.load text |> Result.mapError Codec.describeLoadError) raw
            match ProjectOps.iconOf metricRef loaded, ProjectOps.iconOf nodeRef loaded with
            | Some(MalformedIcon _), Some(MalformedIcon _) -> ()
            | other -> fail (sprintf "%s should load as malformed, got %A" raw other)
            let expected = okOr (Json.parse raw) raw
            equal (Some expected) (ProjectOps.iconOf metricRef loaded |> Option.map IconRef.toJson) (sprintf "%s is kept as the same JSON value" raw)
            let once = Codec.serialize loaded
            equal once (Codec.serialize (reload loaded)) (sprintf "%s is written back unchanged on every save" raw)
            let findings = Validation.run loaded |> List.filter (fun f -> f.Code = "icon.malformed")
            equal 2 findings.Length (sprintf "%s is reported once per object" raw)
            expect (findings |> List.forall (fun f -> f.Severity = Warning)) "a malformed icon warns; it never blocks"
            expect (findings |> List.forall (fun f -> not (f.Message.Contains "script") && not (f.Message.Contains "evil"))) "findings never echo the value"
            // Other edits still work on a document that holds them.
            let edited = okOr (Commands.execute (Layout(SetComponentContent(page, metric, "label", "Edited"))) loaded) "edit beside a malformed icon"
            equal (ProjectOps.iconOf metricRef loaded) (ProjectOps.iconOf metricRef edited.Project) (sprintf "%s survives an unrelated edit" raw))

// -- commands -----------------------------------------------------------------------

let commands =
    test "SetIcon is one undoable command on supported components and nodes, and refused elsewhere" (fun () ->
        let s = layoutSession ()
        let set = s |> runAll [ setIcon metricRef "test-line" ]
        equal (Some(NamedIcon(name "test-line"))) (ProjectOps.iconOf metricRef set.Project) "set"
        equal s.Project (Editor.undo set).Project "undo restores the document"
        equal set.Project (Editor.redo (Editor.undo set)).Project "redo"
        for target, why in
            [ ComponentRef(page, stack), "a stack has no place for an icon"
              ComponentRef(page, heading), "a heading's Forma contract has no icon property"
              ComponentRef(page, field), "a text field has no place for an icon"
              ComponentRef(page, idOf "missing"), "a missing component"
              NodeRef(d, idOf "missing"), "a missing node"
              DiagramRef d, "a diagram"
              PageRef page, "a page" ] do
            let rejected = errorOf (Editor.dispatch (setIcon target "test-line") s) why
            expect (not (List.isEmpty rejected) && rejected |> List.forall Finding.isBlocker) why
        // SetIcon and #33's SetComponentProperty "icon" are the same document data.
        let viaProperty = s |> runAll [ Layout(SetComponentProperty(page, metric, "icon", Some(JString "test-line"))) ]
        equal set.Project viaProperty.Project "one representation for Layout icons"
        let unknown = s |> runAll [ setIcon nodeRef "future-glyph" ]
        equal (Some(NamedIcon(name "future-glyph"))) (ProjectOps.iconOf nodeRef unknown.Project) "the command does not consult a release: a newer name is allowed"
        let cleared = okOr (Editor.dispatch (AppearanceCmd(SetIcon(ComponentRef(page, stack), None))) s) "clearing is always allowed"
        equal s.Project cleared.Project "clearing an absent icon changes nothing")

let review =
    test "Icon changes appear in review and merge like other presentation edits" (fun () ->
        let before = (layoutSession ()).Project
        let ours = (Editor.start before |> runAll [ setIcon nodeRef "test-dot" ]).Project
        let theirs = (Editor.start before |> runAll [ setIcon nodeRef "test-box" ]).Project
        expect (Diff.between before ours |> List.exists (fun c -> c.Code = "node.icon-changed")) "the diff names the icon change"
        expect (Diff.between before ours |> List.exists (fun c -> c.Code = "page.changed") |> not) "a node icon is not a page change"
        let headed = (Editor.start before |> runAll [ setIcon metricRef "test-line" ]).Project
        expect (Diff.between before headed |> List.exists (fun c -> c.Code = "page.changed")) "a Layout icon is a page change"
        let merged = Merge.three before ours theirs
        expect (merged.Conflicts |> List.exists (fun c -> c.Code = "merge.both-changed" && c.Target.EndsWith "/node:a")) "different icons on both sides conflict"
        equal (Some(NamedIcon(name "test-dot"))) (ProjectOps.iconOf nodeRef merged.Project) "ours is kept until the reviewer chooses")

// -- the pinned collection ------------------------------------------------------------

let fixtureVerifies =
    test "The committed test fixture verifies: every SVG matches its registry digest and its snippet" (fun () ->
        let c = fixtureCatalog ()
        equal "0.0.0-test-fixture" c.FormaVersion "release stamp comes from the registry"
        equal [ "test-line"; "test-box"; "test-dot" ] (c.Entries |> List.map (_.Name >> IconName.value)) "registry order"
        equal [] c.Rejected "nothing refused"
        equal [ "test-box" ] (IconCatalog.search "  SQUARE " c |> List.map (_.Name >> IconName.value)) "search covers keywords, case-insensitive"
        equal 3 (IconCatalog.search "TEST" c).Length "and category"
        equal 3 (IconCatalog.search "" c).Length "an empty search offers everything")

let tamperedAssets =
    test "Tampered, unsafe or mismatched icon files are refused one by one; nothing executable passes" (fun () ->
        let svg = fixture "test-line.svg"
        let html = fixture "html/test-line.html"
        let cases =
            [ "test-line.svg", svg.Replace("M4 12H20", "M4 12H21"), "geometry changed after the digest"
              "html/test-line.html", html.Replace("M4 12H20", "M4 12H21"), "snippet geometry differs from the verified SVG"
              "html/test-line.html", html.Replace(" aria-hidden=\"true\"", ""), "snippet not decorative"
              "html/test-line.html", html.Replace("data-ef-icon=\"test-line\"", "data-ef-icon=\"test-box\""), "snippet wrapped for another name"
              "html/test-line.html", html.Replace("<path d=\"M4 12H20\"/>", "<path d=\"M4 12H20\"/><script>alert(1)</script>"), "script element"
              "html/test-line.html", html.Replace("<path d=", "<path onload=\"alert(1)\" d="), "event attribute"
              "html/test-line.html", html.Replace("<path d=\"M4 12H20\"/>", "<foreignObject><div>x</div></foreignObject>"), "foreignObject"
              "html/test-line.html", html.Replace("<path d=\"M4 12H20\"/>", "<use href=\"https://evil.example/s.svg#x\"/>"), "external reference"
              "html/test-line.html", html.Replace("<path d=", "<path style=\"fill:red\" d="), "style attribute"
              "html/test-line.html", html.Replace("fill=\"none\"", "fill=\"url(#paint)\""), "url() paint"
              "html/test-line.html", html.Replace("M4 12H20", "javascript:alert(1)"), "script URL in geometry"
              "html/test-line.html", html.Replace("<path d=\"M4 12H20\"/>", "<!-- x --><path d=\"M4 12H20\"/>"), "comment"
              "html/test-line.html", html.Replace("<path d=\"M4 12H20\"/>", "&lt;<path d=\"M4 12H20\"/>"), "entity text"
              "html/test-line.html", html.Replace("<path d=", "<path d='M4 12H20' x="), "single-quoted attribute"
              "html/test-line.html", html + "<img src=x onerror=alert(1)>", "content after the root"
              "html/test-line.html", html.Replace("<svg xmlns=\"http://www.w3.org/2000/svg\"", "<svg xmlns=\"http://evil.example/ns\""), "foreign namespace"
              "html/test-line.html", html.Replace("viewBox=\"0 0 24 24\"", "viewBox=\"0 0 48 48\""), "not the 24-unit grid"
              "test-line.svg", String('x', 20000), "oversized file" ]
        for file, content, why in cases do
            let availability = catalogWith (Map.ofList [ file, content ])
            equal [ "test-line" ] (refusedNames availability) why
            match availability with
            | IconsAvailable c ->
                expect (c.Entries |> List.forall (fun e -> IconName.value e.Name <> "test-line")) (sprintf "%s: the refused icon is not offered" why)
                expect (not (c.Artwork.ContainsKey(name "test-line"))) (sprintf "%s: and has no artwork" why)
            | other -> fail (sprintf "%s: the other icons should still verify, got %A" why other))

let registryRefusals =
    test "A malformed or unsafe registry makes icons unavailable instead of half-loading" (fun () ->
        let registry = fixture "registry.json"
        let unavailable (text: string) why =
            match IconCatalog.fromFiles text readFixture with
            | IconsUnavailable _ -> ()
            | other -> fail (sprintf "%s should be refused, got %A" why other)
        unavailable "not json" "invalid JSON"
        unavailable (registry.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2")) "another schema version"
        unavailable (registry.Replace("\"grid\": 24", "\"grid\": 16")) "another grid"
        unavailable (registry.Replace("\"formaVersion\": \"0.0.0-test-fixture\"", "\"formaVersion\": \"latest\"")) "no release version"
        unavailable (registry.Replace("\"name\": \"test-box\"", "\"name\": \"test-line\"").Replace("icons/test-box.svg", "icons/test-line.svg").Replace("icons/html/test-box.html", "icons/html/test-line.html")) "a repeated name"
        unavailable (registry.Replace("\"name\": \"test-box\"", "\"name\": \"../evil\"")) "a path-traversal name"
        unavailable (registry.Replace("\"icons/test-box.svg\"", "\"https://other.example/a.svg\"")) "a remote asset path"
        unavailable (registry.Replace("\"icons/html/test-box.html\"", "\"icons/html/../../x.html\"")) "a traversing snippet path"
        unavailable (Regex.Replace(registry, "\"svgSha256\": \"[0-9a-f]{64}\"", "\"svgSha256\": \"abc\"")) "a malformed digest"
        unavailable (registry.Replace("\"label\": \"Test box\"", "\"label\": \"\"")) "an empty label"
        unavailable "{\"schemaVersion\": 1, \"formaVersion\": \"0.5.0\", \"grid\": 24, \"icons\": []}" "an empty collection has nothing to offer"
        // Every file fails verification: unavailable, with the reason.
        match IconCatalog.fromFiles registry (fun _ -> Error "missing") with
        | IconsUnavailable why -> expect (why.Contains "0.0.0-test-fixture") "the reason names the release"
        | other -> fail (sprintf "no verifiable files should be unavailable, got %A" other))

let pinnedPackage =
    test "The pinned Forma package: icons are unavailable without a registry, and every packaged icon verifies when present" (fun () ->
        let packageDir = Path.Combine(repo, "node_modules", "@echelon-foundry", "design-system")
        let registry = Path.Combine(packageDir, "dist", "icons", "registry.json")
        if not (Directory.Exists packageDir) then
            printfn "       pinned package: node_modules/@echelon-foundry/design-system is not installed here (npm install); CI installs it before these tests"
        elif not (File.Exists registry) then
            // A release without icons (Forma 0.4.1 and earlier): Studio must say so and fabricate nothing.
            printfn "       pinned package: no dist/icons (icons unavailable)"
            expect (not (File.Exists(Path.Combine(packageDir, "dist", "icons", "search.svg")))) "no stray icon files"
        else
            let dir = Path.Combine(packageDir, "dist", "icons")
            let version = Regex.Match(File.ReadAllText(Path.Combine(packageDir, "package.json")), "\"version\":\\s*\"([^\"]+)\"").Groups.[1].Value
            match IconCatalog.fromFiles (File.ReadAllText registry) (fun rel -> Ok(File.ReadAllText(Path.Combine(dir, rel)))) with
            | IconsAvailable c ->
                equal version c.FormaVersion "the registry belongs to the pinned package version"
                equal [] c.Rejected "every packaged icon verifies"
                printfn "       pinned package: Forma %s, %d icons verified" c.FormaVersion c.Entries.Length
            | other -> fail (sprintf "the pinned package's icons should verify, got %A" other))

// -- portable HTML -----------------------------------------------------------------------

/// (icon name, Forma version) for every inlined icon, read back from exported HTML.
let private iconsIn (html: string) =
    Regex.Matches(html, "<ef-icon class=\"ef-component-tag\" data-forma-version=\"([^\"]+)\">\\s*<span class=\"ef-icon\" data-ef-icon=\"([a-z0-9-]+)\">")
    |> Seq.map (fun m -> m.Groups.[2].Value, m.Groups.[1].Value)
    |> List.ofSeq

let private export catalog (project: Project) =
    HtmlExport.page { HtmlExport.defaults with Icons = catalog } WorkflowLibrary.empty (pageOf project)

let exportInlinesPinnedIcons =
    test "HTML export inlines the release's own decorative SVG, stamped with its Forma version, and round-trips the names" (fun () ->
        let c = fixtureCatalog ()
        let project = (layoutSession () |> runAll [ setIcon metricRef "test-line"; setIcon buttonRef "test-dot" ]).Project
        let result = export (Some c) project
        equal [ "test-line", "0.0.0-test-fixture"; "test-dot", "0.0.0-test-fixture" ] (iconsIn result.Html) "exported icons read back as the document's names and the release"
        equal [] result.Omitted "nothing left out"
        expect (result.Html.Contains "aria-hidden=\"true\"" && result.Html.Contains "focusable=\"false\"") "inline SVG is decorative"
        expect (result.Html.Contains "<path d=\"M4 12H20\" />" && result.Html.Contains "<circle cx=\"12\" cy=\"12\" r=\"3\" />") "geometry comes from the release files"
        expect (Regex.IsMatch(result.Html, "<button type=\"button\">[\\s\\S]*</ef-icon>\\s*Search crews\\s*</button>")) "the button's name is still its text"
        expect (not (Regex.IsMatch(result.Html, "<script|\\son[a-z]+=|javascript:|<foreignObject|href=\"http", RegexOptions.IgnoreCase))) "no script, handler, foreignObject or remote reference"
        expect (result.Html.StartsWith "<!-- Requires @echelon-foundry/design-system@0.0.0-test-fixture/tokens.css") "stylesheets are declared at the icons' release"
        equal result.Html (export (Some c) project).Html "deterministic"
        let doc = HtmlExport.page { HtmlExport.defaults with Icons = Some c; Target = HtmlDocument } WorkflowLibrary.empty (pageOf project)
        expect (doc.Html.Contains "href=\"node_modules/@echelon-foundry/design-system/dist/components.css\"") "a document links the stylesheets"
        equal (iconsIn result.Html) (iconsIn doc.Html) "fragment and document carry the same icons")

let exportWithoutIcons =
    test "Without an icon collection (an icon-less release) the export fabricates no SVG, keeps the static output and says why" (fun () ->
        let plain = (layoutSession ()).Project
        let named = (Editor.start plain |> runAll [ setIcon metricRef "test-line" ]).Project
        let result = export None named
        equal (export None plain).Html result.Html "byte-identical to the same design without an icon"
        expect (not (result.Html.Contains "<svg") && not (result.Html.Contains "ef-icon")) "no SVG and no icon markup"
        equal 1 result.Omitted.Length "one note"
        expect (result.Omitted.Head.Contains "test-line" && result.Omitted.Head.Contains "no icon collection" && result.Omitted.Head.Contains "keeps it") "the note says the document keeps the icon")

let exportUnknownAndMalformed =
    test "Unknown, malformed and misplaced icons are never exported as markup, and their values are never echoed" (fun () ->
        let c = fixtureCatalog ()
        let unknown = (layoutSession () |> runAll [ setIcon metricRef "future-glyph" ]).Project
        let r1 = export (Some c) unknown
        expect (not (r1.Html.Contains "<svg")) "an icon the release lacks is not drawn"
        expect (r1.Omitted |> List.exists (fun o -> o.Contains "future-glyph" && o.Contains "0.0.0-test-fixture")) "and the note names the release"
        let injected =
            okOr (Codec.load (withRawIcons "\"<script>alert(1)</script>\"" (layoutSession () |> runAll [ setIcon metricRef "test-line" ]).Project) |> Result.mapError Codec.describeLoadError) "load"
        let r2 = export (Some c) injected
        expect (not (r2.Html.Contains "<svg") && not (r2.Html.Contains "script")) "a malformed value is inert"
        expect (r2.Omitted |> List.forall (fun o -> not (o.Contains "script"))) "and is never echoed"
        let misplaced = okOr (Codec.load ((Codec.serialize (layoutSession ()).Project).Replace("\"componentId\": \"text-field\",", "\"componentId\": \"text-field\",\n            \"properties\": { \"icon\": \"test-line\" },")) |> Result.mapError Codec.describeLoadError) "load misplaced"
        let r3 = export (Some c) misplaced
        expect (not (r3.Html.Contains "<svg")) "a component with no place for an icon draws none"
        expect (r3.Omitted |> List.exists (fun o -> o.Contains "no place for an icon")) "and says so")

// -- the editor (Limen messages) ---------------------------------------------------------------

let private send (message: JsonValue) (state: EditorState) = EditorApp.handle message state

let private event name key value =
    Json.obj [ "kind", JString "Event"; "event", Json.objOpt [ "kind", Some(JString "Event"); "name", Some(JString name); "key", key |> Option.map JString; "value", value |> Option.map JString ] ]

let private httpResult correlation (status: int) (body: string) =
    Json.obj
        [ "kind", JString "EffectResult"
          "result", Json.obj [ "kind", JString "HttpResult"; "correlationId", JString correlation; "outcome", Json.obj [ "kind", JString "Success"; "status", Json.ofInt status; "body", JString body ] ] ]

let private apply (messages: JsonValue list) (state: EditorState) = messages |> List.fold (fun s m -> send m s |> fst) state

/// Answers every Http effect from the fixture directory, as the browser kernel would.
let rec private serve (effects: JsonValue list) (state: EditorState) =
    effects
    |> List.fold
        (fun s effect ->
            match Json.field "kind" effect, Json.field "correlationId" effect, Json.field "url" effect with
            | Some(JString "Http"), Some(JString correlation), Some(JString url) when url.StartsWith IconSession.assetBase ->
                let next, response = send (httpResult correlation 200 (fixture (url.Substring IconSession.assetBase.Length))) s
                serve (match Json.field "effects" response with Some(JArray more) -> more | _ -> []) next
            | _ -> s)
        state

let private started () =
    let state, response = send (Json.obj [ "kind", JString "Initialize" ]) (EditorApp.start ())
    state, (match Json.field "effects" response with Some(JArray effects) -> effects | _ -> [])

let private viewField (key: string) (state: EditorState) = Json.field key (EditorView.view state) |> Option.defaultValue JNull

let private effectsOf (response: JsonValue) = match Json.field "effects" response with Some(JArray effects) -> effects | _ -> []

let editorLoadsThePinnedCollection =
    test "The editor fetches the pinned collection through Limen Http effects and verifies it before offering it" (fun () ->
        let state, effects = started ()
        match effects with
        | [ request ] ->
            equal (Some(JString "Http")) (Json.field "kind" request) "a Limen Http effect"
            equal (Some(JString "./forma/icons/registry.json")) (Json.field "url" request) "the pinned registry, same origin"
            equal (Some(JString "text")) (Json.field "response" request) "read as text"
            equal (Some(JString "GET")) (Json.field "method" request) "GET"
        | other -> fail (sprintf "expected one registry request, got %A" other)
        let loaded = serve effects state
        match loaded.Icons.Availability with
        | IconsAvailable c -> equal 3 c.Entries.Length "all three fixture icons verified"
        | other -> fail (sprintf "expected the fixture collection, got %A" other)
        let again, more = send (Json.obj [ "kind", JString "Initialize" ]) loaded
        equal (JArray []) (Json.field "effects" more |> Option.defaultValue JNull) "a loaded collection is not fetched again"
        equal loaded.Icons again.Icons "unchanged")

let editorWithoutIcons =
    test "With the icon-less pin the picker reports icons unavailable and changes nothing" (fun () ->
        let state, _ = started ()
        let unavailable = apply [ httpResult "icon-registry" 404 "Not found" ] state
        match unavailable.Icons.Availability with
        | IconsUnavailable why -> expect (why.Contains "does not include the icon collection") "the reason"
        | other -> fail (sprintf "expected unavailable, got %A" other)
        let picked = apply [ event "select" (Some "node:prepare") None; event "icon-pick" None None ] unavailable
        expect (picked.Status.Contains "does not include the icon collection") "opening the picker says so"
        let tried = apply [ event "icon-choose" (Some "search") None ] picked
        equal picked.Session.Project tried.Session.Project "nothing set"
        equal (JBool true) (viewField "iconSearchDisabled" tried) "search disabled"
        equal (JArray []) (viewField "iconChoices" tried) "no choices"
        equal (JBool true) (viewField "iconPickerNode" tried) "the picker still opens, to explain"
        // A network failure is the same: unavailable, never a guess.
        let failed = apply [ Json.obj [ "kind", JString "EffectResult"; "result", Json.obj [ "kind", JString "HttpResult"; "correlationId", JString "icon-registry"; "outcome", Json.obj [ "kind", JString "Failure"; "reason", JString "network" ] ] ] ] state
        match failed.Icons.Availability with
        | IconsUnavailable _ -> ()
        | other -> fail (sprintf "a failed fetch should be unavailable, got %A" other))

let editorPicker =
    test "The picker sets and clears a node icon through one command, and the view offers native, named choices" (fun () ->
        let state, effects = started ()
        let loaded = serve effects state
        let picking = apply [ event "select" (Some "node:prepare") None; event "icon-pick" None None ] loaded
        equal (Some(NodeRef(Samples.diagramId, idOf "prepare"))) picking.Icons.Target "the picker edits the selected node"
        equal (JBool true) (viewField "iconPickerNode" picking) "shown in the inspector"
        equal (JBool false) (viewField "iconPickerLayout" picking) "not beside the Layout list"
        match viewField "iconChoices" picking with
        | JArray choices ->
            equal 3 choices.Length "three choices"
            expect (choices |> List.contains (JObject [ "key", JString "test-box"; "label", JString "Test box"; "src", JString "./forma/icons/test-box.svg"; "pressed", JString "false" ]))
                "each choice: key, text label, pinned static SVG, pressed state"
        | other -> fail (sprintf "choices: %A" other)
        let searched = apply [ event "icon-search" None (Some "dot") ] picking
        equal (JString "1 of 3 icons from Forma 0.0.0-test-fixture match.") (viewField "iconAvailability" searched) "search narrows the choices"
        let chosen = apply [ event "icon-choose" (Some "test-dot") None ] searched
        equal (Some(NamedIcon(name "test-dot"))) (ProjectOps.iconOf (NodeRef(Samples.diagramId, idOf "prepare")) chosen.Session.Project) "set"
        equal 1 chosen.Session.Undo.Length "one history entry"
        equal (JString "Icon: Test dot (test-dot).") (viewField "nodeIconText" chosen) "the inspector states the icon in text"
        let _, copyResponse = send (event "icon-copy" None None) chosen
        match effectsOf copyResponse with
        | [ effect ] ->
            equal (Some(JString "Clipboard")) (Json.field "kind" effect) "a Limen clipboard effect"
            match Json.field "text" effect with
            | Some(JString text) -> expect (text.Contains "data-ef-icon=\"test-dot\"" && text.Contains "data-forma-version=\"0.0.0-test-fixture\"" && text.Contains "aria-hidden=\"true\"") "copy uses the verified, decorative snippet"
            | other -> fail (sprintf "clipboard text: %A" other)
        | other -> fail (sprintf "expected one clipboard effect, got %A" other)
        for bad in [ "future-glyph"; "<script>alert(1)</script>"; "../test-dot"; "" ] do
            let refused = apply [ event "icon-choose" (Some bad) None ] chosen
            equal chosen.Session.Project refused.Session.Project (sprintf "%A refused" bad)
        let cleared = apply [ event "icon-clear" None None ] chosen
        equal None (ProjectOps.iconOf (NodeRef(Samples.diagramId, idOf "prepare")) cleared.Session.Project) "cleared"
        let undone = apply [ event "undo" None None ] cleared
        equal chosen.Session.Project undone.Session.Project "undo brings it back"
        let closed = apply [ event "icon-close" None None ] undone
        equal (JBool false) (viewField "iconPickerNode" closed) "closed")

let editorLayoutAndSave =
    test "A Layout item's icon is chosen by keyboard-reachable buttons, saved through Limen storage and reopened" (fun () ->
        let state, effects = started ()
        let loaded = serve effects state
        let s = apply [ event "add-page" None None; event "layout-add-component" (Some "button") None; event "icon-pick" (Some "page-1-button|label") None; event "icon-choose" (Some "test-line") None ] loaded
        let buttonRef = ComponentRef(idOf "page-1", idOf "page-1-button")
        equal (Some(NamedIcon(name "test-line"))) (ProjectOps.iconOf buttonRef s.Session.Project) "set on the button"
        equal (JBool true) (viewField "iconPickerLayout" s) "picker beside the Layout list"
        match viewField "layoutItems" s with
        | JArray [ item ] ->
            equal (Some(JString "Choose icon for button page-1-button")) (Json.field "iconPickLabel" item) "a specific button name"
            equal (Some(JString "./forma/icons/test-line.svg")) (Json.field "iconSrc" item) "the preview uses the pinned static SVG"
            equal (Some(JBool true)) (Json.field "iconShown" item) "and is shown"
        | other -> fail (sprintf "layout items: %A" other)
        let saving, response = send (event "save" None None) s
        let saved =
            match effectsOf response with
            | [ effect ] -> (match Json.field "value" effect with Some(JString text) -> text | _ -> fail "no saved text")
            | other -> fail (sprintf "expected one storage effect, got %A" other)
        expect (saved.Contains "\"icon\": \"test-line\"" && saved.Contains "\"schemaVersion\": 2") "the saved document holds the icon property"
        let reopened = apply [ Json.obj [ "kind", JString "EffectResult"; "result", Json.obj [ "kind", JString "StorageResult"; "correlationId", JString "load"; "outcome", Json.obj [ "kind", JString "Success"; "value", JString saved ] ] ] ] saving
        equal s.Session.Project reopened.Session.Project "reopened identically"
        let exported = apply [ event "open-page" (Some "page-1") None; event "export-page" None None ] reopened
        equal [ "test-line", "0.0.0-test-fixture" ] (iconsIn exported.Export.Text) "the editor's export inlines the icon")

let all =
    [ names; storedValues; persistence; malformedPreserved; commands; review; fixtureVerifies; tamperedAssets; registryRefusals; pinnedPackage
      exportInlinesPinnedIcons; exportWithoutIcons; exportUnknownAndMalformed; editorLoadsThePinnedCollection; editorWithoutIcons; editorPicker; editorLayoutAndSave ]
