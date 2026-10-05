namespace FormaStudio.Engine

/// Where a new project-local field may appear (FDA-1181, FDA-820).
type FieldScopeChoice =
    | PrintedAndExported
    | ExportedOnly
    | EditorOnly

/// A field definition as an author drafts it: plain text and choices, before
/// the domain rules below turn it into a typed definition.
type FieldDraft =
    { Name: string
      Type: string
      Options: string
      Scope: FieldScopeChoice }

/// Why a drafted field cannot be defined. Rendered as status text by the editor.
type FieldDraftError =
    | FieldNameMissing
    | FieldTypeInvalid of string

/// Derived node moves: align and distribute (FDA-125).
type Arrangement =
    | AlignLeft
    | AlignTop
    | DistributeHorizontally

/// Intent -> command builders: the domain policy behind editor gestures and
/// forms (identifier allocation, field types, disclosure scopes, slug keys,
/// default placement, composite Batch construction). Pure functions of the
/// project; they never see editor view state, so any caller that is not the
/// browser editor (an agent, a CLI, a test) builds the same commands for the
/// same intent. Commands are still executed only by `Commands.execute` through
/// `Editor.dispatch` (forma-studio#18, FST-F4).
[<RequireQualifiedAccess>]
module EditorIntents =
    // -- identifiers ------------------------------------------------------------

    /// Deterministic identifier allocation: the smallest unused `prefix-n`.
    let freshId prefix (used: Set<string>) =
        Seq.initInfinite (fun i -> sprintf "%s-%d" prefix (i + 1)) |> Seq.find (used.Contains >> not)

    /// A stable, readable key from a display name; display names can change later
    /// without touching the key (FDA-1184).
    let slug (text: string) =
        let lowered = text.Trim().ToLowerInvariant()
        let chars = lowered |> Seq.map (fun c -> if System.Char.IsLetterOrDigit c && c < '\u0080' then c else '-') |> Seq.toArray |> System.String
        let collapsed = System.Text.RegularExpressions.Regex.Replace(chars, "-+", "-").Trim('-')
        if collapsed = "" then "field" else collapsed

    /// The candidate itself, or the first free `candidate-n` from 2.
    let unique (used: Set<string>) (candidate: string) =
        if not (used.Contains candidate) then candidate
        else Seq.initInfinite (fun i -> sprintf "%s-%d" candidate (i + 2)) |> Seq.find (used.Contains >> not)

    /// Ids of every node, edge and group in a diagram (one namespace per diagram).
    let diagramIds (diagram: Diagram) =
        (diagram.Nodes |> List.map (fun n -> Id.value n.Id)) @ (diagram.Edges |> List.map (fun e -> Id.value e.Id)) @ (diagram.Groups |> List.map (fun g -> Id.value g.Id))
        |> Set.ofList

    // -- grid -------------------------------------------------------------------

    /// The logical grid unit for snapping and keyboard nudges.
    let gridStep = 8.0

    /// Adjusts a delta so the moved coordinate lands on the grid.
    let snapDelta enabled (origin: int) (delta: float) =
        if enabled then System.Math.Round((float origin + delta) / gridStep) * gridStep - float origin else delta

    /// Rounds a size to the grid, never below one grid unit.
    let snapSize enabled (size: float) =
        if enabled then max gridStep (System.Math.Round(size / gridStep) * gridStep) else size

    // -- Flow -------------------------------------------------------------------

    /// A new node of the chosen kind, cascaded from the top-left so successive
    /// additions do not stack exactly. Returns the new id, the command and the
    /// kind label (the profile's, or "Item").
    let addNode (profile: DiagramProfile option) (diagram: Diagram option) (diagramId: DiagramId) (kind: string) =
        let used = diagram |> Option.map diagramIds |> Option.defaultValue Set.empty
        let count = diagram |> Option.map (fun d -> d.Nodes.Length) |> Option.defaultValue 0
        let label = profile |> Option.bind (fun p -> Profiles.nodeKind p kind) |> Option.map _.Label |> Option.defaultValue "Item"
        let nodeId: NodeId = Samples.idOf (freshId "node" used)
        let offset = 40.0 + float (count % 5) * 40.0
        nodeId, Flow(AddNode(diagramId, nodeId, kind, sprintf "New %s" (label.ToLowerInvariant()), offset, offset, 200.0, 120.0)), label

    /// A connector of the profile's first edge kind (or "flow") with a fresh id.
    let connect (profile: DiagramProfile option) (diagram: Diagram option) (diagramId: DiagramId) (source: NodeId) (target: NodeId) =
        let kind = profile |> Option.bind (fun p -> List.tryHead p.EdgeKinds) |> Option.map _.Kind |> Option.defaultValue "flow"
        let edgeId: EdgeId = Samples.idOf (freshId "edge" (diagram |> Option.map diagramIds |> Option.defaultValue Set.empty))
        edgeId, Flow(Connect(diagramId, edgeId, kind, { Node = source; Port = None }, { Node = target; Port = None }, None))

    /// Moves one end of a connector to another node, keeping the other end.
    let reconnect (diagramId: DiagramId) (edge: DiagramEdge) (sourceEnd: bool) (node: NodeId) =
        let endpoint = { Node = node; Port = None }
        let source, target = if sourceEnd then endpoint, edge.Target else edge.Source, endpoint
        Flow(Reconnect(diagramId, edge.Id, source, target))

    /// One delta per node; nodes already in place are left out. An empty result
    /// with too few nodes means "select more", otherwise "already arranged".
    let arrange (arrangement: Arrangement) (nodes: DiagramNode list) =
        let moves =
            match arrangement, nodes with
            | AlignLeft, (_ :: _ :: _) ->
                let left = nodes |> List.map (fun n -> n.Box.Position.X) |> List.min
                nodes |> List.map (fun n -> n.Id, float (left - n.Box.Position.X), 0.0)
            | AlignTop, (_ :: _ :: _) ->
                let top = nodes |> List.map (fun n -> n.Box.Position.Y) |> List.min
                nodes |> List.map (fun n -> n.Id, 0.0, float (top - n.Box.Position.Y))
            | DistributeHorizontally, (_ :: _ :: _ :: _) ->
                let ordered = nodes |> List.sortBy (fun n -> n.Box.Position.X, Id.value n.Id)
                let first, last = List.head ordered, List.last ordered
                let span = float (last.Box.Position.X - first.Box.Position.X)
                let gap = span / float (ordered.Length - 1)
                ordered |> List.mapi (fun i n -> n.Id, float first.Box.Position.X + gap * float i - float n.Box.Position.X, 0.0)
            | _ -> []
        moves, moves |> List.filter (fun (_, dx, dy) -> dx <> 0.0 || dy <> 0.0)

    /// Places a fragment below the current diagram so it never lands on top of
    /// existing items.
    let insertionOffset (diagram: Diagram option) (fragment: DiagramFragment) =
        match diagram, fragment.Nodes with
        | Some d, (_ :: _ as nodes) ->
            let b = if List.isEmpty d.Nodes && List.isEmpty d.Groups then { Position = { X = 0; Y = 0 }; Size = { Width = 1; Height = 1 } } else Projection.bounds d
            let left = nodes |> List.map (fun n -> n.Box.Position.X) |> List.min
            let top = nodes |> List.map (fun n -> n.Box.Position.Y) |> List.min
            float (b.Position.X - left), float (b.Position.Y + b.Size.Height + 40 - top)
        | _ -> 0.0, 0.0

    // -- metadata ---------------------------------------------------------------

    /// Parses an author's text into a value of the field's type.
    let parseValue (field: FieldDefinition) (text: string) =
        let trimmed = text.Trim()
        match field.Type with
        | TextField _ -> Ok(Text text)
        | NumberField _ ->
            match System.Decimal.TryParse(trimmed, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture) with
            | true, n -> Ok(Number n)
            | _ -> Error(sprintf "'%s' is not a number." trimmed)
        | BooleanField ->
            match trimmed.ToLowerInvariant() with
            | "true" | "yes" -> Ok(Boolean true)
            | "false" | "no" -> Ok(Boolean false)
            | _ -> Error "Enter yes or no."
        | EnumField options ->
            options
            |> List.tryFind (fun o -> o.Id = trimmed || System.String.Equals(o.Label, trimmed, System.StringComparison.OrdinalIgnoreCase))
            |> Option.map (fun o -> Ok(Enum o.Id))
            |> Option.defaultValue (Error(sprintf "Choose one of: %s." (options |> List.map _.Label |> String.concat ", ")))
        | DateTimeField -> Ok(DateTime trimmed)
        | UrlField -> Ok(Url trimmed)
        | TagsField _ -> Ok(TagList(trimmed.Split(',') |> Array.map (fun s -> s.Trim()) |> Array.filter ((<>) "") |> List.ofArray))

    /// The field types an author can choose, keyed as the form submits them.
    let fieldTypes =
        [ "text", "Text"; "number", "Number"; "boolean", "Yes or no"; "choice", "Choice list"; "date", "Date or time"; "url", "Link"; "tags", "Tags" ]

    /// The disclosure scopes an author can choose, keyed as the form submits them.
    let scopeChoices = [ "printed", PrintedAndExported, "Printed and exported"; "export", ExportedOnly, "Exported only"; "editor", EditorOnly, "Editor only" ]

    /// Comma-separated choices: trimmed, blanks and duplicates dropped, slug ids.
    let choiceOptions (text: string) =
        text.Split(',') |> Array.map (fun o -> o.Trim()) |> Array.filter ((<>) "") |> Array.distinct |> List.ofArray
        |> List.map (fun label -> { Id = slug label; Label = label })

    /// The typed field type for a drafted type key; unknown keys are text.
    let fieldType (typeKey: string) (options: EnumOption list) =
        match typeKey with
        | "number" -> Ok(NumberField(None, None))
        | "boolean" -> Ok BooleanField
        | "choice" when List.isEmpty options -> Error "List the choices, separated by commas."
        | "choice" -> Ok(EnumField options)
        | "date" -> Ok DateTimeField
        | "url" -> Ok UrlField
        | "tags" -> Ok(TagsField false)
        | _ -> Ok(TextField None)

    /// Disclosure semantics of each authoring scope.
    let disclosureOf scope =
        match scope with
        | PrintedAndExported -> { Scopes = set [ Rendered; AgentExport; ProvenanceExport ]; DerivedPresentation = true }
        | ExportedOnly -> { Scopes = set [ AgentExport; ProvenanceExport ]; DerivedPresentation = false }
        | EditorOnly -> MetadataRules.disclosureSourceOnly

    /// Validates a draft before any diagram is consulted: a name, then a type.
    let checkFieldDraft (draft: FieldDraft) =
        match System.String.IsNullOrWhiteSpace draft.Name, fieldType draft.Type (choiceOptions draft.Options) with
        | true, _ -> Error FieldNameMissing
        | _, Error message -> Error(FieldTypeInvalid message)
        | false, Ok t -> Ok t

    /// Defines a project-local field from a draft (FDA-1180..1191). Printed
    /// fields are also shown on the canvas; the whole change is one batch, so one
    /// undo removes it.
    let defineField (project: Project) (diagram: Diagram) (draft: FieldDraft) =
        checkFieldDraft draft
        |> Result.map (fun fieldType ->
            let key = unique (project.Fields |> List.map (fun f -> Id.value f.Key) |> Set.ofList) (slug draft.Name)
            let fieldKey: FieldKey = Samples.idOf key
            let definition =
                { Key = fieldKey; Name = draft.Name.Trim(); Help = None; Type = fieldType; AppliesTo = set [ NodeTarget; EdgeTarget; GroupTarget ]
                  Disclosure = disclosureOf draft.Scope; Default = None; Required = false; Derivation = None; Origin = ProjectLocal }
            let display =
                if draft.Scope = PrintedAndExported then
                    [ Flow(SetDisplay(diagram.Id, { diagram.Display with NodeFields = diagram.Display.NodeFields @ [ fieldKey ] })) ]
                else []
            definition, Batch("define field", MetadataCmd(DefineField definition) :: display))

    // -- appearance -------------------------------------------------------------

    /// A palette slot with a fresh key derived from its name.
    let paletteSlot (project: Project) (name: string) (hex: HexColor) =
        let key = unique (project.Palette |> List.map (fun p -> Id.value p.Id) |> Set.ofList) (slug name)
        { Id = Samples.idOf key; Name = name.Trim(); Value = PaletteLiteral hex; Description = None }

    /// Adds or replaces one exact-match rule on a choice field's mapping:
    /// value -> palette slot fill (FDA-1210..1220). Mapping ids are stable per field.
    let mappingRule (project: Project) (field: FieldDefinition) (option: EnumOption) (slot: PaletteSlotId) =
        let rule = { Match = Equals option.Id; Outcome = UseAppearance { Appearance.empty with Fill = Some(PaletteColor slot) }; Legend = sprintf "%s is %s" field.Name option.Label }
        let mappingId: MappingId = Samples.idOf (sprintf "map-%s" (Id.value field.Key))
        match ProjectOps.tryMapping mappingId project with
        | Some existing ->
            let rules = (existing.Rules |> List.filter (fun r -> r.Match <> Equals option.Id)) @ [ rule ]
            AppearanceCmd(UpdateMapping { existing with Rules = rules; Enabled = true })
        | None ->
            AppearanceCmd(
                DefineMapping
                    { Id = mappingId; Name = sprintf "%s color" field.Name; Field = field.Key; Targets = set [ NodeTarget ]; Rules = [ rule ]; Enabled = true
                      Fallbacks = { Missing = NoMapping; Unknown = NoMapping; Unavailable = NoMapping; Invalid = NoMapping; Unmapped = NoMapping } }
            )

    // -- Layout -----------------------------------------------------------------

    /// The next free `page-n` with an empty root stack, as one batch.
    let addPage (project: Project) =
        let used = project.Pages |> List.map (fun pg -> Id.value pg.Id) |> Set.ofList
        let number = Seq.initInfinite (fun i -> i + 1) |> Seq.find (fun i -> not (used.Contains(sprintf "page-%d" i)))
        let pageId: PageId = Samples.idOf (sprintf "page-%d" number)
        let rootId: ComponentNodeId = Samples.idOf (sprintf "page-%d-stack" number)
        number, pageId, Batch("add page", [ Layout(AddPage(pageId, sprintf "Page %d" number, Some(sprintf "/page-%d" number))); Layout(AddComponent(pageId, None, 0, rootId, "stack")) ])

    let private childCount (node: ComponentNode) = node.Slots |> Map.tryFind "children" |> Option.map List.length |> Option.defaultValue 0

    /// A level-2 heading appended to the page's root stack.
    let addHeading (page: Page) (root: ComponentNode) =
        let used = ProjectOps.componentIds page.Nodes |> List.map Id.value |> Set.ofList
        let headingId: ComponentNodeId = Samples.idOf (unique used (sprintf "%s-heading" (Id.value page.Id)))
        Batch("add heading", [ Layout(AddComponent(page.Id, Some { Parent = root.Id; Slot = "children" }, childCount root, headingId, "heading"))
                               Layout(SetComponentContent(page.Id, headingId, "text", "New heading"))
                               Layout(SetComponentProperty(page.Id, headingId, "level", Some(Json.ofInt 2))) ])

    /// Starter content so a new component is visible and labelled.
    let componentDefaults (contractId: string) =
        match contractId with
        | "heading" -> [ "text", "New heading" ]
        | "text" -> [ "text", "New paragraph." ]
        | "button" | "link-button" -> [ "label", "Continue" ] @ (if contractId = "link-button" then [ "href", "#" ] else [])
        | "text-field" -> [ "label", "New field" ]
        | "surface" -> [ "title", "New section" ]
        | "responsive-grid" -> [ "label", "Summary" ]
        | "alert" -> [ "title", "Notice"; "text", "Describe what changed." ]
        | "metric-card" -> [ "label", "Metric"; "value", "0" ]
        | _ -> []

    /// A catalog component appended to a container on the page (or the root
    /// stack when the target is not on it), with its defaults, as one batch.
    let addComponent (page: Page) (root: ComponentNode) (target: string option) (currentWorkflow: string option) (contract: ComponentContract) =
        let used = ProjectOps.componentIds page.Nodes |> List.map Id.value |> Set.ofList
        let id: ComponentNodeId = Samples.idOf (unique used (sprintf "%s-%s" (Id.value page.Id) contract.Id))
        let containers = ProjectOps.componentIds page.Nodes
        let parent = target |> Option.bind (fun t -> containers |> List.tryFind (fun c -> Id.value c = t)) |> Option.defaultValue root.Id
        let siblings =
            let rec find (nodes: ComponentNode list) =
                nodes |> List.tryPick (fun n -> if n.Id = parent then Some n else n.Slots |> Map.toList |> List.collect snd |> find)
            find page.Nodes |> Option.map childCount |> Option.defaultValue 0
        let props =
            match contract.Id, currentWorkflow with
            | "heading", _ -> [ "level", Json.ofInt 2 ]
            | "workflow", Some workflowId -> [ "workflow", JString workflowId ]
            | _ -> []
        Batch(
            "add " + contract.Id,
            Layout(AddComponent(page.Id, Some { Parent = parent; Slot = "children" }, siblings, id, contract.Id))
            :: (componentDefaults contract.Id |> List.map (fun (k, v) -> Layout(SetComponentContent(page.Id, id, k, v))))
            @ (props |> List.map (fun (k, v) -> Layout(SetComponentProperty(page.Id, id, k, Some v))))
        )

    /// An empty batch: dispatched when an intent resolves to no change, so the
    /// editor reports it through the same command path.
    let nothing = Batch("nothing", [])

    /// Moves a root-stack child one place earlier or later (clamped), or None
    /// when the component is not a direct child of the root stack.
    let reorder (page: Page) (root: ComponentNode) (componentKey: string) (earlier: bool) =
        let children = root.Slots |> Map.tryFind "children" |> Option.defaultValue []
        children
        |> List.tryFindIndex (fun c -> Id.value c.Id = componentKey)
        |> Option.map (fun index ->
            let target = if earlier then max 0 (index - 1) else min (children.Length - 1) (index + 1)
            Layout(MoveComponent(page.Id, children.[index].Id, Some { Parent = root.Id; Slot = "children" }, target)))
