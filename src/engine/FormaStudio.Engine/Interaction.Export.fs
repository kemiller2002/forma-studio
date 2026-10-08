namespace FormaStudio.Engine

open Interaction

/// Standards-based HTML export of a Layout page, one component tree or the
/// current workflow, and copying the result.
[<RequireQualifiedAccess>]
module internal ExportInteraction =
    let private options (state: EditorState) =
        { HtmlExport.defaults with Target = state.Export.Target; Brand = state.Export.Brand; InteractiveWorkflow = state.Export.InteractiveWorkflow; Icons = IconSession.catalog state.Icons }

    let private show (state: EditorState) (name: string) (result: HtmlExportResult) =
        let kind = match state.Export.Target with HtmlFragment -> "fragment" | HtmlDocument -> "document"
        let deps =
            result.Dependencies
            |> List.map (function Forma.Workflow.Stylesheet(pkg, v, path) | Forma.Workflow.RuntimeModule(pkg, v, path) -> $"{pkg}@{v}/{path}")
        { state with
            Export =
                { state.Export with
                    Text = result.Html
                    FileName = name + (if state.Export.Target = HtmlDocument then ".html" else ".fragment.html")
                    Omitted = result.Omitted
                    Summary = $"HTML {kind} for {name}. Needs: " + String.concat ", " deps + "." }
            Status = (if result.Omitted.IsEmpty then $"Exported {name} as an HTML {kind}." else $"Exported {name}; {result.Omitted.Length} item(s) were left out (listed below).") }

    let update (event: ExportEvent) (args: EventArgs) (state: EditorState) : EditorState * JsonValue list =
        let key = args.Key
        let only s = s, []
        let p = EditorState.project state
        match event with
        | ExportEvent.ExportTarget -> only { state with Export = { state.Export with Target = (if key = "document" then HtmlDocument else HtmlFragment) } }
        | ExportEvent.ExportInteractive ->
            let on = not state.Export.InteractiveWorkflow
            only { state with Export = { state.Export with InteractiveWorkflow = on }; Status = (if on then "Workflow exports will add the public forma-workflow runtime." else "Workflow exports are static HTML and CSS.") }
        | ExportEvent.ExportBrand -> only { state with Export = { state.Export with Brand = (if key = "" || key = "none" then None else Some key) } }
        | ExportEvent.ExportPage ->
            match EditorState.openPage state |> Option.orElse (p.Pages |> List.tryHead) with
            | Some page -> only (show state (Id.value page.Id) (HtmlExport.page (options state) state.Workflows page))
            | None -> only (status "Add a Layout page to export it." state)
        | ExportEvent.ExportComponent ->
            let componentKey = key.Split('|').[0]
            match EditorState.openPage state, Id.create<ComponentKind> componentKey with
            | Some page, Ok id ->
                match HtmlExport.componentTree (options state) state.Workflows page id with
                | Some result -> only (show state componentKey result)
                | None -> only (status "That component is no longer on the page." state)
            | _ -> only (status "Open a Layout page to export a component." state)
        | ExportEvent.ExportWorkflow ->
            match WorkflowLibrary.current state.Workflows with
            | Some entry ->
                match HtmlExport.workflow (options state) entry with
                | Ok result -> only (show state entry.Id result)
                | Error message -> only (status message state)
            | None -> only (status "Open a workflow to export it." state)
        | ExportEvent.CopyExport when state.Export.Text <> "" -> status "Copying the exported HTML…" state, [ HostEffects.copyText state.Export.Text ]
        // Retained quirk (pinned by the characterization golden): with nothing
        // exported the original handler fell through to "unrecognized".
        | ExportEvent.CopyExport -> only (unrecognized (EditorEvent.wireName (EditorEvent.Export ExportEvent.CopyExport)) state)
