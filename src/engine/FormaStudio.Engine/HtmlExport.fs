namespace FormaStudio.Engine

/// What to produce.
type HtmlTarget =
    /// Markup to insert into an existing page, with a leading comment declaring its assets.
    | HtmlFragment
    /// A complete document that opens as ordinary HTML with the declared public assets.
    | HtmlDocument

type HtmlExportOptions =
    { Target: HtmlTarget
      /// Where the consuming project serves Forma's dist files.
      FormaBase: string
      /// Optional public Forma brand (brands/<id>.css and data-ef-brand).
      Brand: string option
      Theme: string option
      Language: string
      /// For workflows only: wrap the static figure in the public <forma-workflow>
      /// element (read-only view) and declare its runtime. Off by default: static stays static.
      InteractiveWorkflow: bool
      /// Where the consuming project serves @echelon-foundry/forma-workflow's dist files.
      WorkflowRuntimeBase: string }

type HtmlExportResult =
    { Html: string
      Dependencies: Forma.Workflow.Dependency list
      /// Design content that has no public Forma contract and was left out.
      Omitted: string list }

/// Standards-based HTML export (REQUIREMENTS.md "Standards-based HTML design
/// export", EHA-080..084). Each Studio component maps to its public Forma
/// markup; workflows are rendered by Forma itself. Output is deterministic,
/// contains no script and no Studio class, id or runtime, and escapes every
/// authored value (it is built with Forma's escaping Markup tree).
[<RequireQualifiedAccess>]
module HtmlExport =
    open Forma.Workflow

    let defaults =
        { Target = HtmlFragment
          FormaBase = "node_modules/@echelon-foundry/design-system/dist/"
          Brand = None
          Theme = None
          Language = "en"
          InteractiveWorkflow = false
          WorkflowRuntimeBase = "node_modules/@echelon-foundry/forma-workflow/dist/" }

    let private el = Markup.el
    let private text = Markup.text

    let private content (key: string) (node: ComponentNode) =
        match node.Content |> Map.tryFind key with
        | Some(JString s) -> Some s
        | _ -> None

    let private property (key: string) (node: ComponentNode) = node.Properties |> Map.tryFind key

    let private children (node: ComponentNode) = node.Slots |> Map.tryFind "children" |> Option.defaultValue []

    let private nodeId (node: ComponentNode) = Id.value node.Id

    let rec private exportNode (workflows: WorkflowLibrary) (node: ComponentNode) : Markup list * string list =
        let kids () =
            let parts = children node |> List.map (exportNode workflows)
            parts |> List.collect fst, parts |> List.collect snd
        let one m = [ m ], []
        match node.Component with
        | "stack" ->
            let markup, omitted = kids ()
            let density = match property "density" node with Some(JString d) -> [ "data-density", d ] | _ -> []
            [ el "div" ([ "class", "ef-stack" ] @ density) markup ], omitted
        | "heading" ->
            let level = match property "level" node with Some(JNumber n) -> (match System.Int32.TryParse n with | true, v when v >= 1 && v <= 6 -> v | _ -> 2) | _ -> 2
            one (el ("h" + string level) [] [ text (content "text" node |> Option.defaultValue "") ])
        | "text" -> one (el "p" [] [ text (content "text" node |> Option.defaultValue "") ])
        | "button" ->
            let kind = match property "type" node with Some(JString "submit") -> "submit" | _ -> "button"
            one (el "button" [ "type", kind ] [ text (content "label" node |> Option.defaultValue "") ])
        | "link-button" ->
            let label = content "label" node |> Option.defaultValue ""
            match content "href" node |> Option.filter Validation.isSafeUrl with
            | Some href -> one (el "a" [ "class", "ef-button"; "href", href ] [ text label ])
            | None -> [ el "span" [] [ text label ] ], [ $"{nodeId node}: link target is missing or not http, https, mailto or relative, so it was exported as text" ]
        | "text-field" ->
            let id = "field-" + nodeId node
            let description = content "description" node
            let required = match property "required" node with Some(JBool true) -> [ "required", "" ] | _ -> []
            let inputType = match property "inputType" node with Some(JString t) -> t | _ -> "text"
            let name = content "name" node |> Option.defaultValue (nodeId node)
            one (
                el
                    "div"
                    [ "class", "ef-field" ]
                    [ yield el "label" [ "class", "ef-field__label"; "for", id ] [ text (content "label" node |> Option.defaultValue "") ]
                      match description with
                      | Some d -> yield el "span" [ "class", "ef-field__description"; "id", id + "-description" ] [ text d ]
                      | None -> ()
                      yield
                          el
                              "input"
                              ([ "id", id; "name", name; "type", inputType ]
                               @ (description |> Option.map (fun _ -> [ "aria-describedby", id + "-description" ]) |> Option.defaultValue [])
                               @ required)
                              [] ]
            )
        | "actions" ->
            let markup, omitted = kids ()
            [ el "div" [ "class", "ef-actions" ] markup ], omitted
        | "surface" ->
            let markup, omitted = kids ()
            let titleId = "surface-" + nodeId node
            match content "title" node with
            | Some title -> [ el "section" [ "class", "ef-surface"; "aria-labelledby", titleId ] (el "h2" [ "id", titleId ] [ text title ] :: markup) ], omitted
            | None -> [ el "div" [ "class", "ef-surface" ] markup ], omitted
        | "responsive-grid" ->
            let markup, omitted = kids ()
            match content "label" node with
            | Some label -> [ el "section" [ "class", "ef-responsive-grid"; "aria-label", label ] markup ], omitted
            | None -> [ el "div" [ "class", "ef-responsive-grid" ] markup ], omitted
        | "alert" ->
            one (
                el
                    "aside"
                    [ "class", "ef-alert"; "role", "status" ]
                    [ el "div" [ "class", "ef-alert__icon"; "aria-hidden", "true" ] [ text "!" ]
                      el
                          "div"
                          []
                          [ el "h3" [ "class", "ef-alert__title" ] [ text (content "title" node |> Option.defaultValue "") ]
                            el "p" [] [ text (content "text" node |> Option.defaultValue "") ] ] ]
            )
        | "metric-card" ->
            one (
                el
                    "article"
                    [ "class", "ef-metric-card" ]
                    [ yield el "div" [ "class", "ef-metric-card__label" ] [ text (content "label" node |> Option.defaultValue "") ]
                      yield el "div" [ "class", "ef-metric-card__value" ] [ text (content "value" node |> Option.defaultValue "") ]
                      match content "context" node with
                      | Some c -> yield el "div" [ "class", "ef-metric-card__context" ] [ text c ]
                      | None -> () ]
            )
        | "workflow" ->
            let id = match property "workflow" node with Some(JString s) -> Some s | _ -> None
            match id |> Option.bind (fun i -> workflows.Entries |> List.tryFind (fun e -> e.Id = i)) |> Option.bind (fun e -> (Validation.load e.Text).Workflow) with
            | Some w -> one (Render.figure RenderOptions.defaults w)
            | None ->
                let missing = defaultArg id "(none)"
                [], [ $"{nodeId node}: workflow \"{missing}\" is not open in this project" ]
        | other -> [], [ $"{nodeId node}: \"{other}\" has no public Forma contract in Studio's catalog and was not exported" ]

    let private brandOk (b: string option) = b |> Option.filter (fun v -> System.Text.RegularExpressions.Regex.IsMatch(v, "^[a-z][a-z0-9-]{0,63}$"))
    let private themeOk (t: string option) = t |> Option.filter (fun v -> v = "light" || v = "dark")

    let private dependencies (opts: HtmlExportOptions) =
        Render.staticDependencies
        @ (brandOk opts.Brand |> Option.map (fun b -> Stylesheet(Render.formaPackage, Capabilities.formaVersion.ToString(), $"brands/{b}.css")) |> Option.toList)

    let private wrap (opts: HtmlExportOptions) (title: string) (description: string option) (body: Markup list) (omitted: string list) =
        let deps = dependencies opts
        match opts.Target with
        | HtmlFragment ->
            { Html = Markup.renderAll (Render.dependencyComment deps :: body); Dependencies = deps; Omitted = omitted }
        | HtmlDocument ->
            let join (path: string) = if opts.FormaBase.EndsWith "/" then opts.FormaBase + path else opts.FormaBase + "/" + path
            let links =
                deps |> List.choose (function Stylesheet(_, _, path) -> Some(el "link" [ "rel", "stylesheet"; "href", join path ] []) | RuntimeModule _ -> None)
            let htmlAttrs =
                [ yield "lang", opts.Language
                  match brandOk opts.Brand with Some b -> yield "data-ef-brand", b | None -> ()
                  match themeOk opts.Theme with Some t -> yield "data-ef-theme", t | None -> () ]
            let head =
                el
                    "head"
                    []
                    ([ el "meta" [ "charset", "utf-8" ] []
                       el "meta" [ "name", "viewport"; "content", "width=device-width, initial-scale=1" ] []
                       el "title" [] [ text title ] ]
                     @ (description |> Option.map (fun d -> el "meta" [ "name", "description"; "content", d ] []) |> Option.toList)
                     @ links)
            let html = el "html" htmlAttrs [ head; el "body" [] [ el "main" [] body ] ]
            { Html = "<!doctype html>\n" + Markup.renderAll [ html ]; Dependencies = deps; Omitted = omitted }

    /// Exports one Layout page.
    let page (opts: HtmlExportOptions) (workflows: WorkflowLibrary) (page: Page) =
        let parts = page.Nodes |> List.map (exportNode workflows)
        wrap opts (page.Title |> Option.defaultValue page.Name) page.Description (parts |> List.collect fst) (parts |> List.collect snd)

    /// Exports one component and everything inside it (a component hierarchy).
    let componentTree (opts: HtmlExportOptions) (workflows: WorkflowLibrary) (page: Page) (id: ComponentNodeId) =
        let rec find (nodes: ComponentNode list) =
            nodes |> List.tryPick (fun n -> if n.Id = id then Some n else n.Slots |> Map.toList |> List.collect snd |> find)
        find page.Nodes
        |> Option.map (fun node ->
            let markup, omitted = exportNode workflows node
            wrap opts (page.Title |> Option.defaultValue page.Name) page.Description markup omitted)

    /// Exports one portable workflow (rendered by Forma). Interactive output is
    /// Forma's own interactive document or fragment, with the public runtime declared.
    let workflow (opts: HtmlExportOptions) (entry: WorkflowEntry) =
        match (Validation.load entry.Text).Workflow with
        | Some w when opts.InteractiveWorkflow ->
            let formaOpts =
                { DocumentOptions.defaults with
                    FormaBase = opts.FormaBase
                    Brand = brandOk opts.Brand
                    Theme = themeOk opts.Theme
                    Interactivity = InteractiveOutput(ViewMode, opts.WorkflowRuntimeBase) }
            let rendered = if opts.Target = HtmlDocument then WorkflowDocument.document formaOpts w else WorkflowDocument.fragment formaOpts w
            Ok { Html = rendered.Html; Dependencies = rendered.Dependencies; Omitted = [] }
        | Some w ->
            let figure = Render.figure RenderOptions.defaults w
            Ok(wrap { opts with Language = w.Language |> Option.defaultValue opts.Language } w.Title w.Description [ figure ] [])
        | None -> Error "The workflow could not be read."

    /// A machine-readable dependency list for the export.
    let dependencyText (result: HtmlExportResult) = Forma.Workflow.Json.serialize (WorkflowDocument.dependencyJson result.Dependencies)
