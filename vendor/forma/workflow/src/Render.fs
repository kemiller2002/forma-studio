namespace Forma.Workflow

open System

/// Generated connective text. English by default; hosts localize by supplying
/// their own phrases. Workflow content itself is never translated.
type Phrases =
    { NodeKind: CoreNodeKind -> string
      EdgeKind: CoreEdgeKind -> string
      State: CoreState -> string
      GroupKind: GroupKind -> string
      Status: string
      Reference: string
      Relationships: string
      Membership: string
      Legend: string
      CanvasLabel: string -> string
      /// source, target -> sentence stem
      Leads: string -> string -> string
      LeadsBack: string -> string -> string
      Mutual: string -> string -> string
      Associated: string -> string -> string
      Labelled: string -> string
      ViaPorts: string option -> string option -> string
      Line: LineStyle -> string
      NoMembers: string }

[<RequireQualifiedAccess>]
module Phrases =
    let english =
        { NodeKind =
            function
            | Start -> "Start"
            | End -> "End"
            | Task -> "Task"
            | Decision -> "Decision"
            | Merge -> "Merge"
            | Event -> "Event"
            | Subprocess -> "Subprocess"
            | Data -> "Data"
            | Note -> "Note"
            | External -> "External"
          EdgeKind =
            function
            | Sequence -> "sequence"
            | Conditional -> "conditional"
            | DefaultFlow -> "default path"
            | ExceptionFlow -> "exception path"
            | Message -> "message"
            | Association -> "association"
            | DataFlow -> "data flow"
          State =
            function
            | NoState -> "No status"
            | Pending -> "Pending"
            | Ready -> "Ready"
            | Active -> "Active"
            | Waiting -> "Waiting"
            | Complete -> "Complete"
            | Blocked -> "Blocked"
            | Failed -> "Failed"
            | Skipped -> "Skipped"
            | Cancelled -> "Cancelled"
            | Unknown -> "Unknown"
          GroupKind =
            function
            | PlainGroup -> "Group"
            | Container -> "Container"
            | Swimlane -> "Lane"
            | Phase -> "Phase"
          Status = "Status"
          Reference = "Reference"
          Relationships = "Relationships"
          Membership = "Groups and lanes"
          Legend = "Key"
          CanvasLabel = fun title -> $"{title} diagram, scroll to see all items"
          Leads = fun a b -> $"{a} leads to {b}"
          LeadsBack = fun a b -> $"{b} leads to {a}"
          Mutual = fun a b -> $"{a} and {b} lead to each other"
          Associated = fun a b -> $"{a} is associated with {b}"
          Labelled = fun l -> $"“{l}”"
          ViaPorts =
            fun s t ->
                match s, t with
                | Some a, Some b -> $"from port {a} to port {b}"
                | Some a, None -> $"from port {a}"
                | None, Some b -> $"to port {b}"
                | None, None -> ""
          Line =
            function
            | Solid -> "solid line"
            | Dashed -> "dashed line"
            | Dotted -> "dotted line"
          NoMembers = "no members" }

/// How a workflow is rendered.
type RenderOptions =
    { /// Prefix for every generated element id; defaults to "wf-<workflow id>".
      IdPrefix: string option
      /// Heading level for node labels (2-6).
      HeadingLevel: int
      /// Metadata keys to show visibly on nodes, in this order. Default: none
      /// (metadata stays machine-readable in the workflow file, FMD-META-006/007).
      VisibleMetadata: string list
      /// Where a navigate-to-workflow intent links, if anywhere.
      WorkflowHref: (WorkflowId -> string) option
      Phrases: Phrases }

[<RequireQualifiedAccess>]
module RenderOptions =
    let defaults =
        { IdPrefix = None
          HeadingLevel = 3
          VisibleMetadata = []
          WorkflowHref = None
          Phrases = Phrases.english }

/// A declared external requirement of rendered output.
type Dependency =
    /// A stylesheet from a public package (package name, version, path inside it).
    | Stylesheet of package: string * version: string * path: string
    /// A public ES module needed only for requested interactivity.
    | RuntimeModule of package: string * version: string * path: string

type RenderedHtml =
    { Html: string
      Dependencies: Dependency list }

/// Static HTML rendering of a portable workflow using public Forma diagram
/// contracts. The result needs no script: relationships, membership, status and
/// references are all present as text.
[<RequireQualifiedAccess>]
module Render =
    let formaPackage = "@echelon-foundry/design-system"
    let workflowPackage = "@echelon-foundry/forma-workflow"
    let workflowPackageVersion = "1.0.0"

    let private el = Markup.el
    let private text = Markup.text

    let prefixOf (opts: RenderOptions) (w: Workflow) =
        opts.IdPrefix |> Option.defaultValue ("wf-" + w.Id.Value)

    let nodeElementId prefix (id: ObjectId) = prefix + "-n-" + id.Value
    let edgeElementId prefix (id: ObjectId) = prefix + "-e-" + id.Value
    let groupElementId prefix (id: ObjectId) = prefix + "-g-" + id.Value

    let tokenVar (t: ColorToken) =
        match t with
        | AccentPrimary -> "var(--ef-color-accent-primary)"
        | AccentSecondary -> "var(--ef-color-accent-secondary)"
        | BorderFunctional -> "var(--ef-color-border-functional)"
        | BorderSubtle -> "var(--ef-color-border-subtle)"
        | SurfacePrimary -> "var(--ef-color-surface-primary)"
        | SurfaceSecondary -> "var(--ef-color-surface-secondary)"
        | SurfaceInverse -> "var(--ef-color-surface-inverse)"
        | TextPrimary -> "var(--ef-color-text-primary)"
        | TextSecondary -> "var(--ef-color-text-secondary)"

    /// The CSS value for a colour. Palette slots resolve through
    /// --ef-workflow-palette-<slot> on the figure, so a brand can restyle token
    /// slots without touching objects.
    let cssColor (c: ColorValue) =
        match c with
        | TokenColor t -> tokenVar t
        | LiteralColor h -> h.Value
        | PaletteColor slot -> $"var(--ef-workflow-palette-{slot.Value})"

    let cssColorSource (c: ColorSource) =
        match c with
        | TokenSource t -> tokenVar t
        | LiteralSource h -> h.Value

    let colorProps (c: ObjectColor) =
        [ c.Fill |> Option.map (fun v -> "--ef-diagram-fill", cssColor v)
          c.Stroke |> Option.map (fun v -> "--ef-diagram-stroke", cssColor v)
          c.Accent |> Option.map (fun v -> "--ef-diagram-accent", cssColor v)
          c.Foreground |> Option.map (fun v -> "--ef-diagram-foreground", cssColor v) ]
        |> List.choose id

    let kindText (p: Phrases) (k: NodeKind) =
        match k with
        | CoreNode(_, Some label) -> label
        | CoreNode(c, None) -> p.NodeKind c
        | CustomNode(_, label) -> label

    let edgeKindText (p: Phrases) (k: EdgeKind) =
        match k with
        | CoreEdge(_, Some label) -> label
        | CoreEdge(c, None) -> p.EdgeKind c
        | CustomEdge(_, label) -> label

    let statusText (p: Phrases) (s: Status) =
        match s.State with
        | CoreStatus(_, Some label) -> label
        | CoreStatus(c, None) -> p.State c
        | CustomStatus(_, label) -> label

    /// The data-ef-status hook value (core states only; producer states get no cue).
    let statusHook (s: Status) =
        match s.State with
        | CoreStatus(c, _) -> Some(Codec.stateText (CoreStatus(c, None)))
        | CustomStatus _ -> None

    let nodeName (n: Node) = n.Accessibility.Name |> Option.defaultValue n.Label

    /// Visible text for a metadata value; nested objects are not rendered.
    let rec metadataText (j: Json) =
        match j with
        | Json.String s -> Some s
        | Json.Number n -> Some n
        | Json.Bool b -> Some(if b then "yes" else "no")
        | Json.Null -> None
        | Json.Array items ->
            let parts = items |> List.choose metadataText
            if parts.IsEmpty then None else Some(String.Join(", ", parts))
        | Json.Object _ -> None

    let private safeHref (url: string) = if Validation.isSafeUrl url then Some url else None

    let private referenceItem (r: Reference) =
        let label = r.Label |> Option.defaultValue r.Key
        match r.Href |> Option.bind safeHref with
        | Some href -> el "a" [ "href", href ] [ text label ]
        | None -> text label

    /// The reading order: along the flow, then across it. Independent of paint
    /// order and of the order nodes appear in the file.
    let readingOrder (w: Workflow) (layout: ResolvedLayout) =
        let docIndex = w.Nodes |> List.mapi (fun i n -> n.Id, i) |> Map.ofList
        w.Nodes
        |> List.sortBy (fun n ->
            match Map.tryFind n.Id layout.Nodes with
            | Some r ->
                let laneIndex = Layout.lanes w |> List.tryFindIndex (fun l -> List.contains n.Id l.Members) |> Option.defaultValue Int32.MaxValue
                if layout.Direction = FlowRight then (Math.Round(r.X), float laneIndex, Math.Round(r.Y), docIndex[n.Id])
                else (Math.Round(r.Y), float laneIndex, Math.Round(r.X), docIndex[n.Id])
            | None -> (Double.MaxValue, 0.0, 0.0, docIndex[n.Id]))

    let private headingTag (opts: RenderOptions) = "h" + string (max 2 (min 6 opts.HeadingLevel))

    let private interactionLabel (w: Workflow) (opts: RenderOptions) prefix (n: Node) (label: Markup) =
        let href =
            match n.Interaction with
            | Some(Navigate(t, _))
            | Some(Open(t, _)) ->
                match t with
                | ToUrl u -> safeHref u
                | ToNode id when w.Nodes |> List.exists (fun x -> x.Id = id) -> Some("#" + nodeElementId prefix id)
                | ToGroup id when w.Groups |> List.exists (fun x -> x.Id = id) -> Some("#" + groupElementId prefix id)
                | ToWorkflow id -> opts.WorkflowHref |> Option.map (fun f -> f id) |> Option.bind safeHref
                | ToReference rid -> n.References |> List.tryFind (fun r -> r.Id = rid) |> Option.bind _.Href |> Option.bind safeHref
                | ToEdge _ | ToNode _ | ToGroup _ -> None
            | _ -> None
        match href with
        | Some h -> el "a" [ "href", h ] [ label ]
        | None -> label

    let private portMarkup (layout: ResolvedLayout) (n: Node) (rect: Rect) =
        n.Ports
        |> List.choose (fun p ->
            Map.tryFind (n.Id, p.Id) layout.Ports
            |> Option.map (fun (pt, side) ->
                let offset =
                    match side with
                    | Top | Bottom -> if rect.W > 0.0 then (pt.X - rect.X) / rect.W * 100.0 else 50.0
                    | Left | Right -> if rect.H > 0.0 then (pt.Y - rect.Y) / rect.H * 100.0 else 50.0
                let attrs =
                    [ yield "class", "ef-diagram-port"
                      yield "data-ef-side", Codec.sideText side
                      match p.Direction with Some d -> yield "data-ef-direction", Codec.portDirectionText d | None -> ()
                      yield "style", Markup.style [ "--ef-diagram-port-offset", Markup.number offset + "%" ] ]
                match p.Label with
                | Some l -> el "span" attrs [ el "span" [ "class", "ef-diagram-port__label" ] [ text l ] ]
                | None -> el "span" (attrs @ [ "aria-hidden", "true" ]) []))

    let private nodeMarkup (w: Workflow) (opts: RenderOptions) prefix (layout: ResolvedLayout) (n: Node) =
        let p = opts.Phrases
        let rect = Map.find n.Id layout.Nodes
        let labelId = nodeElementId prefix n.Id + "-label"
        let shape = Layout.shapeOf n
        let metaRows =
            [ match n.Status with
              | Some s -> yield [ el "dt" [] [ text p.Status ]; el "dd" [] [ text (statusText p s) ] ]
              | None -> ()
              for r in n.References do
                  yield [ el "dt" [] [ text (r.Type |> Option.defaultValue p.Reference) ]; el "dd" [] [ referenceItem r ] ]
              for key in opts.VisibleMetadata do
                  match n.Metadata |> Option.bind (List.tryFind (fst >> (=) key)) |> Option.bind (snd >> metadataText) with
                  | Some v -> yield [ el "dt" [] [ text key ]; el "dd" [] [ text v ] ]
                  | None -> () ]
            |> List.concat
        let describedBy =
            match n.Accessibility.Description with
            | Some _ -> [ "aria-describedby", nodeElementId prefix n.Id + "-a11y" ]
            | None -> []
        let naming =
            match n.Accessibility.Name with
            | Some name -> [ "aria-label", name ]
            | None -> [ "aria-labelledby", labelId ]
        let attrs =
            [ yield "class", "ef-diagram-node"
              yield "id", nodeElementId prefix n.Id
              if shape <> Rectangle then yield "data-ef-shape", Codec.shapeText shape
              match n.Status |> Option.bind statusHook with
              | Some hook when hook <> "none" -> yield "data-ef-status", hook
              | _ -> ()
              yield! naming
              yield! describedBy
              yield
                  "style",
                  Markup.style (
                      [ "--ef-diagram-x", Markup.px rect.X; "--ef-diagram-y", Markup.px rect.Y; "--ef-diagram-w", Markup.px rect.W
                        "--ef-diagram-h", Markup.px rect.H ]
                      @ colorProps n.Color
                  ) ]
        el
            "article"
            attrs
            [ yield el "p" [ "class", "ef-diagram-node__kind" ] [ text (kindText p n.Kind) ]
              yield el (headingTag opts) [ "class", "ef-diagram-node__label"; "id", labelId ] [ interactionLabel w opts prefix n (text n.Label) ]
              match n.Description with
              | Some d -> yield el "p" [ "class", "ef-diagram-node__description" ] [ text d ]
              | None -> ()
              match n.Accessibility.Description with
              | Some d -> yield el "p" [ "class", "ef-visually-hidden"; "id", nodeElementId prefix n.Id + "-a11y" ] [ text d ]
              | None -> ()
              if not metaRows.IsEmpty then yield el "dl" [ "class", "ef-diagram-node__meta" ] metaRows
              yield! portMarkup layout n rect ]

    let private groupMarkup (opts: RenderOptions) prefix (layout: ResolvedLayout) (g: Group) =
        Map.tryFind g.Id layout.Groups
        |> Option.map (fun rect ->
            let p = opts.Phrases
            let variant =
                match g.Kind with
                | Swimlane -> Some "lane"
                | Phase -> Some "phase"
                | PlainGroup | Container -> None
            let labelId = groupElementId prefix g.Id + "-label"
            el
                "div"
                [ yield "class", "ef-diagram-group"
                  yield "id", groupElementId prefix g.Id
                  match variant with Some v -> yield "data-ef-group", v | None -> ()
                  match g.Line with Some l when l <> Solid -> yield "data-ef-line", Codec.lineText l | _ -> ()
                  match g.Status |> Option.bind statusHook with
                  | Some hook when hook <> "none" -> yield "data-ef-status", hook
                  | _ -> ()
                  yield
                      "style",
                      Markup.style (
                          [ "--ef-diagram-x", Markup.px rect.X; "--ef-diagram-y", Markup.px rect.Y; "--ef-diagram-w", Markup.px rect.W
                            "--ef-diagram-h", Markup.px rect.H ]
                          @ colorProps g.Color
                      ) ]
                [ el
                      "p"
                      [ "class", "ef-diagram-group__header" ]
                      [ yield el "span" [ "class", "ef-diagram-group__kind" ] [ text (p.GroupKind g.Kind) ]
                        yield text " "
                        yield el "span" [ "class", "ef-diagram-group__label"; "id", labelId ] [ text g.Label ]
                        match g.Status with
                        | Some s -> yield text (" (" + statusText p s + ")")
                        | None -> () ] ])

    let private svgPath (points: Point list) =
        points
        |> List.mapi (fun i pt -> (if i = 0 then "M" else "L") + Markup.number pt.X + " " + Markup.number pt.Y)
        |> String.concat " "

    let private edgeMarkup prefix (layout: ResolvedLayout) (e: Edge) =
        Map.tryFind e.Id layout.Edges
        |> Option.map (fun route ->
            let arrow = "url(#" + prefix + "-arrow)"
            let direction = e.Direction |> Option.defaultValue Forward
            el
                "path"
                [ yield "class", "ef-diagram-connector"
                  yield "id", edgeElementId prefix e.Id
                  match e.Line with Some l when l <> Solid -> yield "data-ef-line", Codec.lineText l | _ -> ()
                  yield "d", svgPath route.Points
                  if direction = Backward || direction = Both then yield "marker-start", arrow
                  if direction = Forward || direction = Both then yield "marker-end", arrow
                  let styleProps =
                      [ yield! e.Stroke |> Option.map (fun c -> "--ef-diagram-connector-stroke", cssColor c) |> Option.toList
                        yield! e.Width |> Option.map (fun w -> "--ef-diagram-connector-width", string w) |> Option.toList ]
                  if not styleProps.IsEmpty then yield "style", Markup.style styleProps ]
                [])

    let private edgeLabelMarkup (layout: ResolvedLayout) (e: Edge) =
        match e.Label, Map.tryFind e.Id layout.Edges with
        | Some label, Some route ->
            Some(
                el
                    "span"
                    [ "class", "ef-diagram-connector__label"; "aria-hidden", "true"
                      "style", Markup.style [ "--ef-diagram-x", Markup.px route.LabelAt.X; "--ef-diagram-y", Markup.px route.LabelAt.Y ] ]
                    [ text label ]
            )
        | _ -> None

    /// One sentence per connector: endpoints, label, kind, ports, status and line
    /// style. This is the non-visual equivalent of the drawn connectors.
    let relationText (w: Workflow) (p: Phrases) (e: Edge) =
        let name (id: ObjectId) =
            w.Nodes |> List.tryFind (fun n -> n.Id = id) |> Option.map nodeName |> Option.defaultValue id.Value
        let a, b = name e.Source.Node, name e.Target.Node
        let stem =
            match e.Direction |> Option.defaultValue Forward with
            | Forward -> p.Leads a b
            | Backward -> p.LeadsBack a b
            | Both -> p.Mutual a b
            | Undirected -> p.Associated a b
        let details =
            [ e.Accessibility.Name |> Option.orElse e.Label |> Option.map p.Labelled
              e.Kind |> Option.bind (fun k -> match k with CoreEdge(Sequence, None) -> None | _ -> Some(edgeKindText p k))
              (let s = p.ViaPorts (e.Source.Port |> Option.map _.Value) (e.Target.Port |> Option.map _.Value) in if s = "" then None else Some s)
              e.Status |> Option.map (fun s -> p.Status.ToLowerInvariant() + " " + statusText p s)
              e.Line |> Option.bind (fun l -> if l = Solid then None else Some(p.Line l)) ]
            |> List.choose id
        let sentence = if details.IsEmpty then stem else stem + ": " + String.Join("; ", details)
        let sentence = match e.Accessibility.Description with Some d -> sentence + ". " + d | None -> sentence
        if sentence.EndsWith "." || sentence.EndsWith "?" || sentence.EndsWith "!" then sentence else sentence + "."

    let private legendMarkup (opts: RenderOptions) (w: Workflow) =
        if w.Presentation.Legend.IsEmpty then None
        else
            let title = w.Presentation.LegendTitle |> Option.defaultValue opts.Phrases.Legend
            Some(
                el
                    "dl"
                    [ "class", "ef-diagram-legend"; "aria-label", title ]
                    (w.Presentation.Legend
                     |> List.map (fun l ->
                         let swatchProps = colorProps l.Color
                         el
                             "div"
                             []
                             [ el
                                   "dt"
                                   []
                                   [ el "span" [ yield "class", "ef-diagram-legend__swatch"; if not swatchProps.IsEmpty then yield "style", Markup.style swatchProps ] []
                                     text (" " + l.Label) ]
                               el "dd" [] [ text (l.Description |> Option.defaultValue (l.Line |> Option.map opts.Phrases.Line |> Option.defaultValue "")) ] ]))
            )

    let private membershipMarkup (opts: RenderOptions) (w: Workflow) (order: Node list) =
        if w.Groups.IsEmpty then None
        else
            let position = order |> List.mapi (fun i n -> n.Id, i) |> Map.ofList
            Some(
                el
                    "dl"
                    [ "class", "ef-diagram__membership"; "aria-label", opts.Phrases.Membership ]
                    (w.Groups
                     |> List.collect (fun g ->
                         let members =
                             g.Members
                             |> List.choose (fun m -> w.Nodes |> List.tryFind (fun n -> n.Id = m))
                             |> List.sortBy (fun n -> Map.tryFind n.Id position |> Option.defaultValue Int32.MaxValue)
                             |> List.map nodeName
                         let parent =
                             g.Parent |> Option.bind (fun pid -> w.Groups |> List.tryFind (fun x -> x.Id = pid)) |> Option.map (fun pg -> " (" + pg.Label + ")")
                         [ el "dt" [] [ text (opts.Phrases.GroupKind g.Kind + ": " + g.Label + (parent |> Option.defaultValue "")) ]
                           el "dd" [] [ text (if members.IsEmpty then opts.Phrases.NoMembers else String.Join(", ", members)) ] ]))
            )

    /// The workflow figure as a Markup tree (used by fragment and document output
    /// and by hosts that compose it into larger pages).
    let figure (opts: RenderOptions) (w: Workflow) : Markup =
        let prefix = prefixOf opts w
        let layout = Layout.resolve w
        let order = readingOrder w layout
        let canvasW, canvasH = layout.Canvas
        let titleId = prefix + "-title"
        let paletteProps = w.Palette |> List.map (fun (slot, c) -> "--ef-workflow-palette-" + slot.Value, cssColorSource c)
        let relations = w.Edges
        el
            "figure"
            [ yield "class", "ef-diagram"
              yield "id", prefix
              yield "aria-labelledby", titleId
              match w.Language with Some l -> yield "lang", l | None -> ()
              match w.TextDirection with
              | Some Ltr -> yield "dir", "ltr"
              | Some Rtl -> yield "dir", "rtl"
              | Some AutoDirection -> yield "dir", "auto"
              | None -> ()
              if not paletteProps.IsEmpty then yield "style", Markup.style paletteProps ]
            [ yield el "figcaption" [ "id", titleId ] [ text w.Title ]
              match w.Description with
              | Some d -> yield el "p" [] [ text d ]
              | None -> ()
              yield
                  el
                      "div"
                      [ "class", "ef-diagram__viewport"; "tabindex", "0"; "role", "group"; "aria-label", opts.Phrases.CanvasLabel w.Title ]
                      [ el
                            "div"
                            [ "class", "ef-diagram__canvas"; "style", Markup.style [ "--ef-diagram-canvas-w", Markup.px canvasW; "--ef-diagram-canvas-h", Markup.px canvasH ] ]
                            [ yield! w.Groups |> List.choose (groupMarkup opts prefix layout)
                              yield
                                  el
                                      "svg"
                                      [ "class", "ef-diagram__wires"; "viewBox", $"0 0 {Markup.number canvasW} {Markup.number canvasH}"; "aria-hidden", "true"; "focusable", "false" ]
                                      [ yield
                                            el
                                                "defs"
                                                []
                                                [ el
                                                      "marker"
                                                      [ "id", prefix + "-arrow"; "class", "ef-diagram-marker"; "viewBox", "0 0 10 10"; "refX", "9"; "refY", "5"
                                                        "markerWidth", "8"; "markerHeight", "8"; "orient", "auto-start-reverse" ]
                                                      [ el "path" [ "d", "M0 0 L10 5 L0 10 z" ] [] ] ]
                                        yield! w.Edges |> List.choose (edgeMarkup prefix layout) ]
                              yield! order |> List.map (nodeMarkup w opts prefix layout)
                              yield! w.Edges |> List.choose (edgeLabelMarkup layout) ] ]
              if not relations.IsEmpty then
                  yield
                      el
                          "ol"
                          [ "class", "ef-diagram__relations"; "aria-label", opts.Phrases.Relationships ]
                          (relations |> List.map (fun e -> el "li" [] [ text (relationText w opts.Phrases e) ]))
              yield! membershipMarkup opts w order |> Option.toList
              yield! legendMarkup opts w |> Option.toList ]

    /// Public Forma stylesheets a static workflow needs.
    let staticDependencies =
        [ Stylesheet(formaPackage, Capabilities.formaVersion.ToString(), "tokens.css")
          Stylesheet(formaPackage, Capabilities.formaVersion.ToString(), "foundations.css")
          Stylesheet(formaPackage, Capabilities.formaVersion.ToString(), "components.css") ]

    let dependencyComment (deps: Dependency list) =
        let describe d =
            match d with
            | Stylesheet(pkg, v, path) -> $"{pkg}@{v}/{path}"
            | RuntimeModule(pkg, v, path) -> $"{pkg}@{v}/{path} (runtime)"
        Comment("Requires " + String.Join(", ", deps |> List.map describe))

    /// A static HTML fragment for insertion into an existing page. A leading
    /// comment declares the public Forma assets it needs.
    let fragment (opts: RenderOptions) (w: Workflow) : RenderedHtml =
        { Html = Markup.renderAll [ dependencyComment staticDependencies; figure opts w ]
          Dependencies = staticDependencies }

/// Requested interactivity for exported HTML. Static is the default and adds no script.
type Interactivity =
    | StaticOutput
    /// Wraps the static figure in the public <forma-workflow> element, which
    /// upgrades it through the @echelon-foundry/forma-workflow runtime. Without
    /// script the static figure still renders.
    | InteractiveOutput of mode: HostMode * runtimeBase: string

type DocumentOptions =
    { Render: RenderOptions
      /// URL prefix where the Forma package's dist files are served,
      /// e.g. "node_modules/@echelon-foundry/design-system/dist/" or "/assets/forma/".
      FormaBase: string
      /// Optional public Forma brand id (loads brands/<id>.css and sets data-ef-brand).
      Brand: string option
      /// Optional explicit theme ("light" or "dark").
      Theme: string option
      Interactivity: Interactivity }

[<RequireQualifiedAccess>]
module DocumentOptions =
    let defaults =
        { Render = RenderOptions.defaults
          FormaBase = "node_modules/@echelon-foundry/design-system/dist/"
          Brand = None
          Theme = None
          Interactivity = StaticOutput }

/// Complete HTML documents and interactive wrappers.
[<RequireQualifiedAccess>]
module WorkflowDocument =
    let private el = Markup.el

    let private brandPattern = System.Text.RegularExpressions.Regex("^[a-z][a-z0-9-]{0,63}$")

    let private validBrand (brand: string option) = brand |> Option.filter brandPattern.IsMatch

    let private validTheme (theme: string option) = theme |> Option.filter (fun t -> t = "light" || t = "dark")

    let private joinUrl (baseUrl: string) (path: string) =
        if baseUrl = "" then path elif baseUrl.EndsWith "/" then baseUrl + path else baseUrl + "/" + path

    /// The dependencies for given output options.
    let dependencies (opts: DocumentOptions) =
        let brand = validBrand opts.Brand |> Option.map (fun b -> Stylesheet(Render.formaPackage, Capabilities.formaVersion.ToString(), $"brands/{b}.css")) |> Option.toList
        let runtime =
            match opts.Interactivity with
            | StaticOutput -> []
            | InteractiveOutput _ ->
                [ Stylesheet(Render.workflowPackage, Render.workflowPackageVersion, "forma-workflow.css")
                  RuntimeModule(Render.workflowPackage, Render.workflowPackageVersion, "forma-workflow.js") ]
        Render.staticDependencies @ brand @ runtime

    /// The workflow wrapped for interactive output: the public custom element,
    /// its portable JSON source as an inert data block, and the static figure as
    /// fallback content.
    let interactiveElement (opts: RenderOptions) (mode: HostMode) (w: Workflow) =
        el
            "forma-workflow"
            [ "mode", HostMode.text mode; "workflow", w.Id.Value ]
            [ Markup.scriptData [ "class", "forma-workflow-source" ] (Codec.encode w); Render.figure opts w ]

    /// The body content for given options (a fragment when static).
    let content (opts: DocumentOptions) (w: Workflow) =
        match opts.Interactivity with
        | StaticOutput -> Render.figure opts.Render w
        | InteractiveOutput(mode, _) -> interactiveElement opts.Render mode w

    /// A fragment with its dependency comment, honouring interactivity.
    let fragment (opts: DocumentOptions) (w: Workflow) : RenderedHtml =
        let deps = dependencies opts
        { Html = Markup.renderAll [ Render.dependencyComment deps; content opts w ]; Dependencies = deps }

    /// A complete, valid HTML document that opens as ordinary HTML with the
    /// declared public assets.
    let document (opts: DocumentOptions) (w: Workflow) : RenderedHtml =
        let deps = dependencies opts
        let brand = validBrand opts.Brand
        let links =
            deps
            |> List.choose (function
                | Stylesheet(pkg, _, path) when pkg = Render.formaPackage -> Some(el "link" [ "rel", "stylesheet"; "href", joinUrl opts.FormaBase path ] [])
                | Stylesheet(_, _, path) ->
                    match opts.Interactivity with
                    | InteractiveOutput(_, runtimeBase) -> Some(el "link" [ "rel", "stylesheet"; "href", joinUrl runtimeBase path ] [])
                    | StaticOutput -> None
                | RuntimeModule _ -> None)
        let scripts =
            match opts.Interactivity with
            | InteractiveOutput(_, runtimeBase) -> [ Markup.moduleScript (joinUrl runtimeBase "forma-workflow.js") ]
            | StaticOutput -> []
        let htmlAttrs =
            [ yield "lang", w.Language |> Option.defaultValue "en"
              match w.TextDirection with
              | Some Rtl -> yield "dir", "rtl"
              | Some Ltr -> yield "dir", "ltr"
              | _ -> ()
              match brand with Some b -> yield "data-ef-brand", b | None -> ()
              match validTheme opts.Theme with Some t -> yield "data-ef-theme", t | None -> () ]
        let head =
            el
                "head"
                []
                ([ el "meta" [ "charset", "utf-8" ] []
                   el "meta" [ "name", "viewport"; "content", "width=device-width, initial-scale=1" ] []
                   el "title" [] [ Text w.Title ] ]
                 @ (w.Description |> Option.map (fun d -> el "meta" [ "name", "description"; "content", d ] []) |> Option.toList)
                 @ [ el "meta" [ "name", "generator"; "content", $"Forma workflow renderer {Format.current}" ] [] ]
                 @ links
                 @ scripts)
        let body = el "body" [] [ el "main" [] [ content opts w ] ]
        { Html = "<!doctype html>\n" + Markup.renderAll [ el "html" htmlAttrs [ head; body ] ]; Dependencies = deps }

    /// A machine-readable dependency manifest for an export.
    let dependencyJson (deps: Dependency list) =
        Json.Array(
            deps
            |> List.map (function
                | Stylesheet(pkg, v, path) -> Json.Object [ "kind", Json.String "stylesheet"; "package", Json.String pkg; "version", Json.String v; "path", Json.String path ]
                | RuntimeModule(pkg, v, path) -> Json.Object [ "kind", Json.String "module"; "package", Json.String pkg; "version", Json.String v; "path", Json.String path ])
        )
