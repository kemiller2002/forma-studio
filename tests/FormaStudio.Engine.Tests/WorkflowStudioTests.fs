/// Portable workflows in Studio (REQUIREMENTS.md "Portable workflows and
/// embeddable designer") and standards-based HTML export.
module WorkflowStudioTests

open System.IO
open System.Text.RegularExpressions
open Harness
open FormaStudio.Engine

module FW = Forma.Workflow.Validation

let private repo =
    let rec up (d: DirectoryInfo | null) =
        match d with
        | null -> failwith "repository root not found"
        | d when File.Exists(Path.Combine(d.FullName, "REQUIREMENTS.md")) -> d.FullName
        | d -> up d.Parent
    up (DirectoryInfo(System.AppContext.BaseDirectory))

let private fixture name = File.ReadAllText(Path.Combine(repo, "vendor", "forma", "workflow", "fixtures", name + ".forma-workflow.json"))
let private parse (t: string) = match Forma.Workflow.Json.parse t with Ok j -> j | Error e -> fail e
let private field name j = Forma.Workflow.Json.field name j

/// Sends one host-protocol message to the public Forma component engine, as
/// Studio's transport (StudioWorkflowInterop) does.
let private send (sessions: Forma.Workflow.Sessions) (fields: (string * Forma.Workflow.Json) list) =
    let message =
        Forma.Workflow.Json.compact (
            Forma.Workflow.Json.Object(
                [ "protocol", Forma.Workflow.Json.String Forma.Workflow.Embed.protocol; "instance", Forma.Workflow.Json.String "studio" ] @ fields
            )
        )
    let next, reply = Forma.Workflow.EmbedHost.dispatch Forma.Workflow.RenderOptions.defaults message sessions
    next, parse reply

let private changedDocument (reply: Forma.Workflow.Json) =
    match field "events" reply with
    | Some(Forma.Workflow.Json.Array events) ->
        events
        |> List.tryPick (fun e ->
            match field "type" e, field "workflow" e with
            | Some(Forma.Workflow.Json.String "change"), Some w -> Some(Forma.Workflow.Json.serialize w)
            | _ -> None)
    | _ -> None

let private str s = Forma.Workflow.Json.String s

let all =
    [ test "An externally produced workflow opens as a first-class document and keeps everything Studio does not understand" (fun () ->
          let text = fixture "extensions"
          let lib, entry = match WorkflowLibrary.openText text WorkflowLibrary.empty with Ok x -> x | Error e -> fail e
          equal "extensions" entry.Id "id"
          equal "supported-with-preserved-extensions" entry.Class "class"
          expect (Forma.Workflow.Json.equivalent (parse text) (parse entry.Text)) "nothing lost on open"
          equal (Some "extensions") lib.Current "current")

      test "A semantically invalid workflow still opens with its findings, without deleting data" (fun () ->
          let broken = (fixture "multi-step").Replace("\"node\": \"ship\"", "\"node\": \"ghost\"")
          let _, entry = match WorkflowLibrary.openText broken WorkflowLibrary.empty with Ok x -> x | Error e -> fail e
          equal "semantically-invalid" entry.Class "class"
          expect (entry.Text.Contains "ghost") "dangling reference kept for repair")

      test "Non-workflow input is refused with Forma's reason" (fun () ->
          match WorkflowLibrary.openText "{\"format\":\"bpmn\"}" WorkflowLibrary.empty with
          | Error message -> expect (message.Contains "structurally-invalid") message
          | Ok _ -> fail "accepted")

      test "Lifecycle: external producer, Forma validation, Studio open, Studio edit, save, external consumer" (fun () ->
          // External producer: a document written from the schema alone.
          let produced = fixture "node-metadata"
          equal true (FW.isValid (FW.load produced)) "Forma validation"
          // Studio opens it and shows it in the public component (Studio's transport path).
          let lib, opened = match WorkflowLibrary.openText produced WorkflowLibrary.empty with Ok x -> x | Error e -> fail e
          let sessions, _ = send Forma.Workflow.EmbedHost.empty [ "type", str "init"; "mode", str "edit"; "document", str opened.Text ]
          // Studio edit: rename a step, add a step, connect it, set metadata, arrange.
          let commands =
              [ Forma.Workflow.Json.Object [ "name", str "setLabel"; "object", str "node:qualify"; "value", str "Qualification test (rev B)" ]
                Forma.Workflow.Json.Object [ "name", str "addNode"; "kind", str "task"; "label", str "Safety sign-off" ]
                Forma.Workflow.Json.Object [ "name", str "connect"; "source", str "qualify"; "target", str "task-1" ]
                Forma.Workflow.Json.Object [ "name", str "setMetadata"; "object", str "node:release"; "key", str "approvedBy"; "value", str "Flight director" ]
                Forma.Workflow.Json.Object [ "name", str "autoLayout" ] ]
          let lib, _ =
              commands
              |> List.fold
                  (fun (lib, sessions) cmd ->
                      let sessions, reply = send sessions [ "type", str "command"; "command", cmd ]
                      match changedDocument reply with
                      | Some doc -> (match WorkflowLibrary.adoptChange doc lib with Ok(l, _) -> l | Error e -> fail e), sessions
                      | None -> fail (Forma.Workflow.Json.compact reply))
                  (lib, sessions)
          // Save and reopen (Studio's persisted form), then hand the file to a consumer.
          let restored = match WorkflowLibrary.ofStorage (WorkflowLibrary.toStorage lib) WorkflowLibrary.empty with Ok l -> l | Error e -> fail e
          let saved = parse (WorkflowLibrary.current restored).Value.Text
          let original = parse produced
          // External consumer: ids, semantics, metadata, references and extensions survived.
          let nodes j = match field "nodes" j with Some(Forma.Workflow.Json.Array ns) -> ns | _ -> []
          let byId j = nodes j |> List.map (fun n -> (match field "id" n with Some(Forma.Workflow.Json.String s) -> s | _ -> ""), n) |> Map.ofList
          let before, after = byId original, byId saved
          for KeyValue(id, n) in before do
              expect (after.ContainsKey id) $"node {id} kept"
              for key in [ "kind"; "references" ] do
                  equal (field key n) (field key after[id]) $"{id}.{key}"
          equal (field "metadata" before["build"]) (field "metadata" after["build"]) "unknown metadata kept exactly"
          equal (field "metadata" original) (field "metadata" saved) "workflow metadata kept"
          expect (after["release"] |> field "metadata" |> Option.exists (fun m -> field "approvedBy" m = Some(str "Flight director"))) "edit kept"
          equal (Some(str "Qualification test (rev B)")) (field "label" after["qualify"]) "rename kept"
          expect (after.ContainsKey "task-1") "added step kept")

      test "Extensions survive a Studio edit session unchanged" (fun () ->
          let text = fixture "extensions"
          let sessions, _ = send Forma.Workflow.EmbedHost.empty [ "type", str "init"; "mode", str "edit"; "document", str text ]
          let _, reply = send sessions [ "type", str "command"; "command", Forma.Workflow.Json.Object [ "name", str "setLabel"; "object", str "node:approve"; "value", str "Approved by range" ] ]
          let doc = parse (changedDocument reply).Value
          equal (field "extensions" (parse text)) (field "extensions" doc) "workflow extensions"
          let nodeExt j = match field "nodes" j with Some(Forma.Workflow.Json.Array ns) -> ns |> List.map (field "extensions") | _ -> []
          equal (nodeExt (parse text)) (nodeExt doc) "node extensions")

      test "The workflow library persists documents themselves under the Forma file convention" (fun () ->
          let lib = [ "minimal"; "swimlanes" ] |> List.fold (fun l n -> match WorkflowLibrary.openText (fixture n) l with Ok(x, _) -> x | Error e -> fail e) WorkflowLibrary.empty
          let restored = match WorkflowLibrary.ofStorage (WorkflowLibrary.toStorage lib) WorkflowLibrary.empty with Ok l -> l | Error e -> fail e
          equal (lib.Entries |> List.map _.Text) (restored.Entries |> List.map _.Text) "documents"
          equal "swimlanes.forma-workflow.json" (WorkflowLibrary.fileName restored.Entries[1]) "file name")

      test "Opening and switching documents asks the component to load; adopting its change does not" (fun () ->
          let lib, _ = match WorkflowLibrary.openText (fixture "minimal") WorkflowLibrary.empty with Ok x -> x | Error e -> fail e
          let changed, _ = match WorkflowLibrary.adoptChange ((fixture "minimal").Replace("\"Start\"", "\"Begin\"")) lib with Ok x -> x | Error e -> fail e
          equal lib.Revision changed.Revision "no reload for the component's own change"
          let switched = (WorkflowLibrary.select "minimal" changed).Value
          expect (switched.Revision > changed.Revision) "reload when switching") ]

let private designs () =
    match Designs.project "branching" with
    | Ok p -> p
    | Error findings -> fail (sprintf "%A" findings)

let private workflowLibrary () =
    match WorkflowLibrary.openText (fixture "branching") WorkflowLibrary.empty with
    | Ok(l, _) -> l
    | Error e -> fail e

let private publicClasses =
    lazy
        ([ "tokens.css"; "foundations.css"; "components.css" ] |> List.map (fun f -> File.ReadAllText(Path.Combine(repo, "vendor", "forma", f))) |> String.concat "\n")

let private pageOf (p: Project) id = p.Pages |> List.find (fun pg -> Id.value pg.Id = id)

let exportTests =
    [ test "HTML export is deterministic for the same design and options" (fun () ->
          let p, lib = designs (), workflowLibrary ()
          for id in [ "application"; "responsive"; "workflow" ] do
              for opts in [ HtmlExport.defaults; { HtmlExport.defaults with Target = HtmlDocument; Brand = Some "example-harbor" } ] do
                  equal (HtmlExport.page opts lib (pageOf p id)).Html (HtmlExport.page opts lib (pageOf (designs ()) id)).Html id)

      test "Static designs export as HTML and CSS only, with public Forma classes and nothing from Studio" (fun () ->
          let p, lib = designs (), workflowLibrary ()
          for id in [ "application"; "responsive"; "workflow" ] do
              let html = (HtmlExport.page { HtmlExport.defaults with Target = HtmlDocument } lib (pageOf p id)).Html
              expect (not (html.Contains "<script")) $"{id}: script"
              expect (not (Regex.IsMatch(html, "\\son[a-z]+=", RegexOptions.IgnoreCase))) $"{id}: handler"
              expect (not (html.ToLowerInvariant().Contains "studio")) $"{id}: Studio reference"
              for m in Regex.Matches(html, "class=\"([^\"]+)\"") do
                  for c in m.Groups[1].Value.Split ' ' do
                      expect (c.StartsWith "ef-") $"{id}: non-Forma class {c}"
                      // Public pattern markup may name a part Forma does not style (forma:patterns/metric-card.html).
                      let patternOnly = set [ "ef-metric-card__context" ]
                      expect (patternOnly.Contains c || Regex.IsMatch(publicClasses.Value, "\\." + Regex.Escape c + "(?![a-zA-Z0-9_-])")) $"{id}: {c} is not public Forma CSS")

      test "Fragments declare their assets; documents are complete and link exactly the declared assets" (fun () ->
          let p, lib = designs (), workflowLibrary ()
          let fragment = HtmlExport.page HtmlExport.defaults lib (pageOf p "application")
          expect (fragment.Html.StartsWith "<!-- Requires @echelon-foundry/design-system@0.4.0/tokens.css") fragment.Html
          let doc = HtmlExport.page { HtmlExport.defaults with Target = HtmlDocument; FormaBase = "/assets/forma/"; Brand = Some "echelon" } lib (pageOf p "application")
          for part in [ "<!doctype html>"; "<html lang=\"en\" data-ef-brand=\"echelon\">"; "<meta charset=\"utf-8\">"; "<meta name=\"viewport\""; "<title>Crew scheduling</title>"; "<main>"
                        "href=\"/assets/forma/components.css\""; "href=\"/assets/forma/brands/echelon.css\"" ] do
              expect (doc.Html.Contains part) part
          equal 4 doc.Dependencies.Length "dependencies")

      test "Accessibility semantics survive export: labels, descriptions, headings, landmarks and names" (fun () ->
          let p, lib = designs (), workflowLibrary ()
          let html = (HtmlExport.page HtmlExport.defaults lib (pageOf p "application")).Html
          expect (html.Contains "<label class=\"ef-field__label\" for=\"field-app-email\">Work email</label>") "label"
          expect (html.Contains "aria-describedby=\"field-app-email-description\"") "description"
          expect (html.Contains "<h1>Crew scheduling</h1>") "heading"
          expect (html.Contains "aria-labelledby=\"surface-app-form\"") "named section"
          let wf = (HtmlExport.page HtmlExport.defaults lib (pageOf p "workflow")).Html
          expect (wf.Contains "class=\"ef-diagram__relations\"") "workflow relationships in text")

      test "Authored values are escaped and unsafe links are not emitted" (fun () ->
          let p = designs ()
          let pageId = (pageOf p "application").Id
          let evil = "<script>alert(1)</script>"
          let commands =
              [ Layout(SetComponentContent(pageId, Samples.idOf "app-title", "text", evil))
                Layout(SetComponentContent(pageId, Samples.idOf "app-help", "href", "javascript:alert(1)")) ]
          let session = commands |> List.fold (fun s c -> s |> Result.bind (Editor.dispatch c)) (Ok(Editor.start p))
          let edited = match session with Ok s -> s.Project | Error f -> fail (sprintf "%A" f)
          let result = HtmlExport.page HtmlExport.defaults WorkflowLibrary.empty (pageOf edited "application")
          expect (not (result.Html.Contains "<script>")) "escaped"
          expect (result.Html.Contains "&lt;script&gt;") "text kept"
          expect (not (result.Html.Contains "javascript:")) "link dropped"
          expect (not result.Omitted.IsEmpty) "reported")

      test "A workflow exports through Forma's renderer, identical to Forma's own output" (fun () ->
          let lib = workflowLibrary ()
          let entry = (WorkflowLibrary.current lib).Value
          let studio = match HtmlExport.workflow HtmlExport.defaults entry with Ok r -> r.Html | Error e -> fail e
          let w = (FW.load entry.Text).Workflow.Value
          equal (Forma.Workflow.WorkflowDocument.fragment Forma.Workflow.DocumentOptions.defaults w).Html studio "same markup")

      test "Content without a public Forma contract is reported, never faked" (fun () ->
          let p = designs ()
          let page = pageOf p "workflow"
          let result = HtmlExport.page HtmlExport.defaults WorkflowLibrary.empty page
          expect (result.Omitted |> List.exists (fun o -> o.Contains "not open")) "missing workflow reported") ]

let interactiveTests =
    [ test "Interactive workflow export is opt-in, uses only the public runtime and declares it" (fun () ->
          let lib = workflowLibrary ()
          let entry = (WorkflowLibrary.current lib).Value
          let staticHtml = match HtmlExport.workflow HtmlExport.defaults entry with Ok r -> r | Error e -> fail e
          expect (not (staticHtml.Html.Contains "<script")) "static by default"
          let interactive = match HtmlExport.workflow { HtmlExport.defaults with InteractiveWorkflow = true; Target = HtmlDocument } entry with Ok r -> r | Error e -> fail e
          expect (interactive.Html.Contains "<forma-workflow mode=\"view\"") "public element"
          expect (interactive.Dependencies |> List.exists (function Forma.Workflow.RuntimeModule(p, _, _) -> p = "@echelon-foundry/forma-workflow" | _ -> false)) "declared runtime"
          expect (not (System.Text.RegularExpressions.Regex.IsMatch(interactive.Html, "studio-|studio\\.css|forma-studio/", System.Text.RegularExpressions.RegexOptions.IgnoreCase))) "no Studio asset or class") ]
