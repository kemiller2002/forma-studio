namespace FormaStudio.Engine

/// The Flow editor as a Limen engine: browser messages in, a complete view plus
/// effect requests out. Everything here is noEffects; the WebAssembly glue holds the
/// single current-state cell. Every canonical change goes through Editor.dispatch,
/// so pointer, keyboard, Structure view and inspector input share one command path
/// (FDA-069, FDA-221).
type EdgeEnd =
    | SourceEnd
    | TargetEnd

type PendingGesture =
    | NoPending
    | Connecting of NodeId
    /// Waiting for the node that one end of a connector should move to.
    | Reconnecting of EdgeId * EdgeEnd

/// Where a new project-local field may appear (FDA-1181, FDA-820).
type FieldScopeChoice =
    | PrintedAndExported
    | ExportedOnly
    | EditorOnly

/// Unsubmitted form input. Drafts are view state: they never enter the project
/// until a create command succeeds.
type Drafts =
    { FieldName: string
      FieldType: string
      FieldOptions: string
      FieldScope: FieldScopeChoice
      SlotName: string
      SlotColor: string
      MappingValue: string option
      MappingSlot: string option }

type EditorState =
    { Session: Session
      Diagram: DiagramId
      Pending: PendingGesture
      AddKind: string
      Field: FieldKey option
      Status: string
      Drafts: Drafts
      /// The Layout page being edited, if any. Layout and Flow share this session,
      /// its history and its command path; only the visible surface differs.
      Page: PageId option
      /// Canvas zoom in percent and grid snapping: view preferences that never
      /// enter the project document or its history (FDA-027).
      Zoom: int
      Snap: bool
      /// Copied nodes with their dependency closure; view state until inserted.
      Clipboard: DiagramFragment option
      /// The template (built-in key or "clipboard") chosen for review.
      Template: string option
      /// The project as last saved or opened; the review compares against it.
      Baseline: Project
      /// The project a pending save wrote, adopted as the baseline on success.
      Saving: Project option
      /// A saved copy that changed since the baseline, under merge review.
      Incoming: Project option
      /// Item conflicts the reviewer resolved in favour of the saved copy.
      TakeSaved: Set<string> }

[<RequireQualifiedAccess>]
module EditorApp =
    let storageKey = "forma-studio.project"
    let private step = 8.0
    let private zoomLevels = [ 50; 75; 100; 125; 150; 200 ]

    /// Adjusts a delta so the moved coordinate lands on the step grid.
    let private snapped enabled (origin: int) (delta: float) =
        if enabled then System.Math.Round((float origin + delta) / step) * step - float origin else delta

    let private snapSize enabled (size: float) =
        if enabled then max step (System.Math.Round(size / step) * step) else size

    let initial (project: Project) (diagram: DiagramId) =
        { Session = Editor.start project
          Diagram = diagram
          Pending = NoPending
          AddKind = "activity"
          Field = None
          Status = "Ready."
          Drafts =
            { FieldName = ""; FieldType = "choice"; FieldOptions = ""; FieldScope = PrintedAndExported
              SlotName = ""; SlotColor = ""; MappingValue = None; MappingSlot = None }
          Page = None
          Zoom = 100
          Snap = false
          Clipboard = None
          Template = None
          Baseline = project
          Saving = None
          Incoming = None
          TakeSaved = Set.empty }

    let fieldTypes =
        [ "text", "Text"; "number", "Number"; "boolean", "Yes or no"; "choice", "Choice list"; "date", "Date or time"; "url", "Link"; "tags", "Tags" ]

    let private scopeChoices = [ "printed", PrintedAndExported, "Printed and exported"; "export", ExportedOnly, "Exported only"; "editor", EditorOnly, "Editor only" ]

    /// A stable, readable key from a display name; display names can change later
    /// without touching the key (FDA-1184).
    let slug (text: string) =
        let lowered = text.Trim().ToLowerInvariant()
        let chars = lowered |> Seq.map (fun c -> if System.Char.IsLetterOrDigit c && c < '\u0080' then c else '-') |> Seq.toArray |> System.String
        let collapsed = System.Text.RegularExpressions.Regex.Replace(chars, "-+", "-").Trim('-')
        if collapsed = "" then "field" else collapsed

    let private unique (used: Set<string>) (candidate: string) =
        if not (used.Contains candidate) then candidate
        else Seq.initInfinite (fun i -> sprintf "%s-%d" candidate (i + 2)) |> Seq.find (used.Contains >> not)

    // -- events ---------------------------------------------------------------

    type private Event = { Name: string; Key: string option; Value: string option }

    let private parseEvent (json: JsonValue) =
        match Json.field "event" json with
        | Some e ->
            let text name = match Json.field name e with Some(JString s) -> Some s | _ -> None
            Some { Name = defaultArg (text "name") ""; Key = text "key"; Value = text "value" }
        | None -> None

    let private project state = state.Session.Project
    let private diagramOf state = ProjectOps.tryDiagram state.Diagram (project state)
    let private profileOf state = diagramOf state |> Option.bind (fun d -> Profiles.tryFind d.Profile)

    let private selectedRef state = state.Session.Selection |> List.tryHead

    let private selectedNode state =
        match selectedRef state with
        | Some(NodeRef(_, n)) -> ProjectOps.tryNode state.Diagram n (project state)
        | _ -> None

    /// Element keys in the view are "node:ID", "edge:ID" or "group:ID".
    let private refOfKey state (key: string) =
        match key.Split(':', 2) with
        | [| "node"; id |] -> Id.create<NodeKind> id |> Result.toOption |> Option.map (fun n -> NodeRef(state.Diagram, n))
        | [| "edge"; id |] -> Id.create<EdgeKind> id |> Result.toOption |> Option.map (fun e -> EdgeRef(state.Diagram, e))
        | [| "group"; id |] -> Id.create<GroupKind> id |> Result.toOption |> Option.map (fun g -> GroupRef(state.Diagram, g))
        | _ -> None

    /// Deterministic identifier allocation: the smallest unused `prefix-n`.
    let private freshId prefix (used: Set<string>) =
        Seq.initInfinite (fun i -> sprintf "%s-%d" prefix (i + 1)) |> Seq.find (used.Contains >> not)

    let private usedIds state =
        diagramOf state
        |> Option.map (fun d -> (d.Nodes |> List.map (fun n -> Id.value n.Id)) @ (d.Edges |> List.map (fun e -> Id.value e.Id)) @ (d.Groups |> List.map (fun g -> Id.value g.Id)))
        |> Option.defaultValue []
        |> Set.ofList

    let private describeFindings (findings: Finding list) =
        findings |> List.map _.Message |> List.truncate 2 |> String.concat " "

    /// Runs a command; a rejection leaves the session noEffects and says why.
    let private run (command: Command) (success: string) state =
        match Editor.dispatch command state.Session with
        | Ok session ->
            let obligations = session.LastObligations |> List.map _.Message
            { state with Session = session; Status = String.concat " " (success :: obligations) }
        | Error findings -> { state with Status = sprintf "Not changed: %s" (describeFindings findings) }

    let private parseDelta (text: string) =
        match text.Split('|') with
        | [| id; dx; dy |] ->
            match System.Double.TryParse(dx, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture),
                  System.Double.TryParse(dy, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture) with
            | (true, x), (true, y) -> Id.create<NodeKind> id |> Result.toOption |> Option.map (fun n -> n, x, y)
            | _ -> None
        | _ -> None

    let private parseNumber (text: string) =
        match System.Double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture) with
        | true, v -> Some v
        | _ -> None

    /// One resize intent (a finished handle drag or one inspector edit) is one
    /// ResizeNode command; the engine validates and rounds the size.
    let private resize state (node: NodeId) (size: Size -> float * float) =
        match ProjectOps.tryNode state.Diagram node (project state) with
        | Some n ->
            let w, h = size n.Box.Size
            let resized = run (Flow(ResizeNode(state.Diagram, node, w, h))) (sprintf "Resized %s." n.Label) state
            { resized with Session = Editor.select [ NodeRef(state.Diagram, node) ] resized.Session }
        | None -> { state with Status = "Select an item to resize it." }

    let private edgeEndOf =
        function
        | "source" -> Some SourceEnd
        | "target" -> Some TargetEnd
        | _ -> None

    /// Moves one end of a connector to another node: one Reconnect command,
    /// whether it came from an endpoint drag or the keyboard path (FDA-067).
    let private reconnect state (edgeId: EdgeId) (edgeEnd: EdgeEnd) (node: NodeId) =
        match diagramOf state |> Option.bind (fun d -> d.Edges |> List.tryFind (fun e -> e.Id = edgeId)) with
        | Some edge ->
            let endpoint = { Node = node; Port = None }
            let source, target = match edgeEnd with SourceEnd -> endpoint, edge.Target | TargetEnd -> edge.Source, endpoint
            let moved = run (Flow(Reconnect(state.Diagram, edgeId, source, target))) "Reconnected." { state with Pending = NoPending }
            { moved with Session = Editor.select [ EdgeRef(state.Diagram, edgeId) ] moved.Session }
        | None -> { state with Pending = NoPending; Status = "Select a connector to reconnect it." }

    let private templatesOf state =
        let builtIn =
            diagramOf state |> Option.map (fun d -> Templates.forProfile d.Profile) |> Option.defaultValue []
            |> List.map (fun t -> t.Key, t.Name, t.Description, t.Fragment)
        let copied =
            state.Clipboard
            |> Option.map (fun f -> "clipboard", "Copied items", sprintf "%d item(s) and %d connector(s) copied from this project." f.Nodes.Length f.Edges.Length, f)
            |> Option.toList
        copied @ builtIn

    let private chosenTemplate state =
        state.Template |> Option.bind (fun key -> templatesOf state |> List.tryFind (fun (k, _, _, _) -> k = key))

    /// Places a fragment below the current diagram so it never lands on top of
    /// existing items.
    let private insertionOffset state (fragment: DiagramFragment) =
        match diagramOf state, fragment.Nodes with
        | Some d, (_ :: _ as nodes) ->
            let b = if List.isEmpty d.Nodes && List.isEmpty d.Groups then { Position = { X = 0; Y = 0 }; Size = { Width = 1; Height = 1 } } else Projection.bounds d
            let left = nodes |> List.map (fun n -> n.Box.Position.X) |> List.min
            let top = nodes |> List.map (fun n -> n.Box.Position.Y) |> List.min
            float (b.Position.X - left), float (b.Position.Y + b.Size.Height + 40 - top)
        | _ -> 0.0, 0.0

    let private describeDecision (d: DependencyDecision) =
        let kind = match d.Kind with "field" -> "Field" | "palette" -> "Palette color" | "style" -> "Style" | "mapping" -> "Color rule" | other -> other
        match d.Action with
        | Reused -> sprintf "%s %s: reuses the identical one already in this project." kind d.SourceId
        | Imported -> sprintf "%s %s: will be added to this project." kind d.SourceId
        | Remapped n -> sprintf "%s %s: differs from this project's; it will be added as %s." kind d.SourceId n

    let private insertTemplate state =
        match chosenTemplate state with
        | Some(_, name, _, fragment) ->
            let dx, dy = insertionOffset state fragment
            match Fragment.applyCommand fragment (project state) state.Diagram dx dy RemapConflicting with
            | Ok(command, _) ->
                let added = match command with Batch(_, steps) -> steps |> List.choose (function Flow(AddNode(_, id, _, _, _, _, _, _)) -> Some(NodeRef(state.Diagram, id)) | _ -> None) | _ -> []
                let inserted = run command (sprintf "Inserted %s." name) state
                if inserted.Session.Project = state.Session.Project then inserted
                else { inserted with Session = Editor.select added inserted.Session; Template = None }
            | Error message -> { state with Status = sprintf "Not inserted: %s" message }
        | None -> { state with Status = "Choose a template to insert." }

    let private move state (dx: float) (dy: float) =
        match selectedNode state with
        | Some node -> run (Flow(MoveNodes(state.Diagram, [ node.Id, dx, dy ]))) (sprintf "Moved %s." node.Label) state
        | None -> { state with Status = "Select an item to move it." }

    let private parseValue (field: FieldDefinition) (text: string) =
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

    /// Metadata edits apply to the whole selection as one atomic command; if any
    /// selected object cannot take the value, nothing changes (FDA-983).
    let private withField state f =
        match state.Session.Selection, state.Field |> Option.bind (fun k -> ProjectOps.tryField k (project state)) with
        | (_ :: _ as targets), Some field -> f targets field
        | [], _ -> { state with Status = "Select an item first." }
        | _, None -> { state with Status = "Choose a metadata field first." }

    /// Nodes in the selection, in selection order.
    let private selectedNodes state =
        state.Session.Selection
        |> List.choose (function NodeRef(_, n) -> ProjectOps.tryNode state.Diagram n (project state) | _ -> None)

    /// Align and distribute are derived edits: they compute one delta per node and
    /// commit a single MoveNodes command, so they undo as one step (FDA-125).
    let private arrange state name =
        let nodes = selectedNodes state
        let moves =
            match name, nodes with
            | "align-left", (_ :: _ :: _) ->
                let left = nodes |> List.map (fun n -> n.Box.Position.X) |> List.min
                nodes |> List.map (fun n -> n.Id, float (left - n.Box.Position.X), 0.0)
            | "align-top", (_ :: _ :: _) ->
                let top = nodes |> List.map (fun n -> n.Box.Position.Y) |> List.min
                nodes |> List.map (fun n -> n.Id, 0.0, float (top - n.Box.Position.Y))
            | "distribute-horizontally", (_ :: _ :: _ :: _) ->
                let ordered = nodes |> List.sortBy (fun n -> n.Box.Position.X, Id.value n.Id)
                let first, last = List.head ordered, List.last ordered
                let span = float (last.Box.Position.X - first.Box.Position.X)
                let gap = span / float (ordered.Length - 1)
                ordered |> List.mapi (fun i n -> n.Id, float first.Box.Position.X + gap * float i - float n.Box.Position.X, 0.0)
            | _ -> []
        match moves |> List.filter (fun (_, dx, dy) -> dx <> 0.0 || dy <> 0.0) with
        | [] when List.isEmpty moves -> { state with Status = "Select more items to arrange them." }
        | [] -> { state with Status = "Already arranged." }
        | changed -> run (Flow(MoveNodes(state.Diagram, changed))) (sprintf "Arranged %d items." (List.length nodes)) state

    let private storage correlation operation extra =
        JObject([ "kind", JString "Storage"; "correlationId", JString correlation; "operation", JString operation; "key", JString storageKey ] @ extra)

    /// Creates a project-local field from the form, without JSON (FDA-1180..1191).
    /// Printed fields are also shown on the canvas; the whole change is one batch,
    /// so one undo removes it.
    let private createField state =
        let d = state.Drafts
        let options =
            d.FieldOptions.Split(',') |> Array.map (fun o -> o.Trim()) |> Array.filter ((<>) "") |> Array.distinct |> List.ofArray
            |> List.map (fun label -> { Id = slug label; Label = label })
        let fieldType =
            match d.FieldType with
            | "number" -> Ok(NumberField(None, None))
            | "boolean" -> Ok BooleanField
            | "choice" when List.isEmpty options -> Error "List the choices, separated by commas."
            | "choice" -> Ok(EnumField options)
            | "date" -> Ok DateTimeField
            | "url" -> Ok UrlField
            | "tags" -> Ok(TagsField false)
            | _ -> Ok(TextField None)
        let disclosure =
            match d.FieldScope with
            | PrintedAndExported -> { Scopes = set [ Rendered; AgentExport; ProvenanceExport ]; DerivedPresentation = true }
            | ExportedOnly -> { Scopes = set [ AgentExport; ProvenanceExport ]; DerivedPresentation = false }
            | EditorOnly -> MetadataRules.disclosureSourceOnly
        match System.String.IsNullOrWhiteSpace d.FieldName, fieldType, diagramOf state with
        | true, _, _ -> { state with Status = "Name the field first." }
        | _, Error message, _ -> { state with Status = sprintf "Not changed: %s" message }
        | _, _, None -> state
        | false, Ok fieldType, Some diagram ->
            let key = unique ((project state).Fields |> List.map (fun f -> Id.value f.Key) |> Set.ofList) (slug d.FieldName)
            let fieldKey: FieldKey = Samples.idOf key
            let definition =
                { Key = fieldKey; Name = d.FieldName.Trim(); Help = None; Type = fieldType; AppliesTo = set [ NodeTarget; EdgeTarget; GroupTarget ]
                  Disclosure = disclosure; Default = None; Required = false; Derivation = None; Origin = ProjectLocal }
            let display =
                if d.FieldScope = PrintedAndExported then
                    [ Flow(SetDisplay(state.Diagram, { diagram.Display with NodeFields = diagram.Display.NodeFields @ [ fieldKey ] })) ]
                else []
            let created = run (Batch("define field", MetadataCmd(DefineField definition) :: display)) (sprintf "Field %s added." definition.Name) state
            { created with Field = Some fieldKey; Drafts = { created.Drafts with FieldName = ""; FieldOptions = "" } }

    /// Adds or replaces one exact-match rule on the chosen choice field's mapping:
    /// value -> palette slot fill (FDA-1210..1220). Mapping ids are stable per field.
    let private createMappingRule state =
        let p = project state
        match state.Field |> Option.bind (fun k -> ProjectOps.tryField k p), state.Drafts.MappingValue, state.Drafts.MappingSlot with
        | Some({ Type = EnumField options } as field), Some value, Some slotKey ->
            match options |> List.tryFind (fun o -> o.Id = value), Id.create<PaletteKind> slotKey with
            | Some option, Ok slot ->
                let rule = { Match = Equals option.Id; Outcome = UseAppearance { Appearance.empty with Fill = Some(PaletteColor slot) }; Legend = sprintf "%s is %s" field.Name option.Label }
                let mappingId: MappingId = Samples.idOf (sprintf "map-%s" (Id.value field.Key))
                let command =
                    match ProjectOps.tryMapping mappingId p with
                    | Some existing ->
                        let rules = (existing.Rules |> List.filter (fun r -> r.Match <> Equals option.Id)) @ [ rule ]
                        AppearanceCmd(UpdateMapping { existing with Rules = rules; Enabled = true })
                    | None ->
                        AppearanceCmd(
                            DefineMapping
                                { Id = mappingId; Name = sprintf "%s color" field.Name; Field = field.Key; Targets = set [ NodeTarget ]; Rules = [ rule ]; Enabled = true
                                  Fallbacks = { Missing = NoMapping; Unknown = NoMapping; Unavailable = NoMapping; Invalid = NoMapping; Unmapped = NoMapping } }
                        )
                run command (sprintf "Items whose %s is %s now take this fill." field.Name option.Label) state
            | _ -> { state with Status = "Choose a value and a palette slot." }
        | Some _, _, _ -> { state with Status = "Mappings work on choice fields: choose a value and a palette slot." }
        | None, _, _ -> { state with Status = "Choose a metadata field first." }

    /// Runs a Layout command against the open page's root stack.
    let private layoutOnRoot state (build: Page -> ComponentNode -> Command * string) =
        match state.Page |> Option.bind (fun id -> ProjectOps.tryPage id (project state)) with
        | Some page ->
            match page.Nodes |> List.tryFind (fun n -> n.Component = "stack") with
            | Some root -> let command, message = build page root in run command message state
            | None -> { state with Status = "This page has no stack to edit." }
        | None -> { state with Status = "Open a Layout page first." }

    /// Applies one semantic event. Returns the new state and any effect requests.
    let private onEvent (e: Event) state : EditorState * JsonValue list =
        let value = defaultArg e.Value ""
        let key = defaultArg e.Key ""
        let noEffects s = s, []
        match e.Name with
        | "select" ->
            match refOfKey state key, state.Pending with
            | Some(NodeRef(_, target)), Connecting source ->
                let kind = profileOf state |> Option.bind (fun p -> List.tryHead p.EdgeKinds) |> Option.map _.Kind |> Option.defaultValue "flow"
                let edgeId: EdgeId = Samples.idOf (freshId "edge" (usedIds state))
                let connected = run (Flow(Connect(state.Diagram, edgeId, kind, { Node = source; Port = None }, { Node = target; Port = None }, None))) "Connected." { state with Pending = NoPending }
                noEffects { connected with Session = Editor.select [ EdgeRef(state.Diagram, edgeId) ] connected.Session }
            | Some(NodeRef(_, target)), Reconnecting(edge, edgeEnd) -> noEffects (reconnect state edge edgeEnd target)
            | Some reference, _ -> noEffects { state with Session = Editor.select [ reference ] state.Session; Pending = NoPending; Status = "Selected." }
            | None, _ -> noEffects { state with Status = "Nothing to select." }
        | "clear-selection" -> noEffects { state with Session = Editor.select [] state.Session; Pending = NoPending; Status = "Selection cleared." }
        | "choose-kind" -> noEffects { state with AddKind = key }
        | "add-node" ->
            let id = freshId "node" (usedIds state)
            let count = diagramOf state |> Option.map (fun d -> d.Nodes.Length) |> Option.defaultValue 0
            let label = profileOf state |> Option.bind (fun p -> Profiles.nodeKind p state.AddKind) |> Option.map _.Label |> Option.defaultValue "Item"
            let nodeId: NodeId = Samples.idOf id
            let added = run (Flow(AddNode(state.Diagram, nodeId, state.AddKind, sprintf "New %s" (label.ToLowerInvariant()), 40.0 + float (count % 5) * 40.0, 40.0 + float (count % 5) * 40.0, 200.0, 120.0))) (sprintf "Added %s." label) state
            noEffects { added with Session = Editor.select [ NodeRef(state.Diagram, nodeId) ] added.Session }
        | "set-label" ->
            match selectedRef state with
            | Some(NodeRef(_, n)) -> noEffects (run (Flow(SetNodeLabel(state.Diagram, n, value))) "Label changed." state)
            | Some(EdgeRef(_, edge)) -> noEffects (run (Flow(SetEdgeLabel(state.Diagram, edge, Some value))) "Label changed." state)
            | _ -> noEffects { state with Status = "Select an item to label it." }
        | "move-left" -> noEffects (move state -step 0.0)
        | "move-right" -> noEffects (move state step 0.0)
        | "move-up" -> noEffects (move state 0.0 -step)
        | "move-down" -> noEffects (move state 0.0 step)
        | "gesture-move" ->
            // One completed drag (or one arrow-key nudge from the gesture adapter)
            // becomes exactly one command and one history entry.
            match parseDelta value with
            | Some(node, dx, dy) ->
                let label = ProjectOps.tryNode state.Diagram node (project state) |> Option.map _.Label |> Option.defaultValue "item"
                let origin = ProjectOps.tryNode state.Diagram node (project state) |> Option.map _.Box.Position
                let dx, dy = origin |> Option.map (fun o -> snapped state.Snap o.X dx, snapped state.Snap o.Y dy) |> Option.defaultValue (dx, dy)
                let moved = run (Flow(MoveNodes(state.Diagram, [ node, dx, dy ]))) (sprintf "Moved %s." label) state
                noEffects { moved with Session = Editor.select [ NodeRef(state.Diagram, node) ] moved.Session }
            | None -> noEffects { state with Status = "Ignored an unreadable move." }
        | "gesture-resize" ->
            match parseDelta value with
            | Some(node, w, h) -> noEffects (resize state node (fun _ -> snapSize state.Snap w, snapSize state.Snap h))
            | None -> noEffects { state with Status = "Ignored an unreadable resize." }
        | "set-width" | "set-height" ->
            match selectedNode state, parseNumber value with
            | Some node, Some v ->
                noEffects (resize state node.Id (fun size -> if e.Name = "set-width" then v, float size.Height else float size.Width, v))
            | Some _, None -> noEffects { state with Status = "Not changed: enter a number." }
            | None, _ -> noEffects { state with Status = "Select an item to resize it." }
        | "reconnect-end" ->
            match selectedRef state, edgeEndOf key with
            | Some(EdgeRef(_, edge)), Some edgeEnd ->
                let which = match edgeEnd with SourceEnd -> "start" | TargetEnd -> "end"
                noEffects { state with Pending = Reconnecting(edge, edgeEnd); Status = sprintf "Choose the item this connector should %s at." which }
            | _ -> noEffects { state with Status = "Select a connector to reconnect it." }
        | "gesture-reconnect" ->
            match selectedRef state, value.Split('|') with
            | Some(EdgeRef(_, edge)), [| endKey; nodeKey |] ->
                match edgeEndOf endKey, Id.create<NodeKind> nodeKey with
                | Some edgeEnd, Ok node -> noEffects (reconnect state edge edgeEnd node)
                | _ -> noEffects { state with Status = "Ignored an unreadable reconnect." }
            | _ -> noEffects { state with Status = "Select a connector to reconnect it." }
        | "zoom-in" | "zoom-out" ->
            let next =
                if e.Name = "zoom-in" then zoomLevels |> List.tryFind (fun z -> z > state.Zoom)
                else zoomLevels |> List.rev |> List.tryFind (fun z -> z < state.Zoom)
            match next with
            | Some z -> noEffects { state with Zoom = z; Status = sprintf "Zoom %d%%." z }
            | None -> noEffects { state with Status = sprintf "Zoom is already %d%%." state.Zoom }
        | "zoom-reset" -> noEffects { state with Zoom = 100; Status = "Zoom 100%." }
        | "toggle-snap" ->
            let on = not state.Snap
            noEffects { state with Snap = on; Status = (if on then "Snapping to the 8-unit grid." else "Snapping off.") }
        | "copy-selection" ->
            let nodes = state.Session.Selection |> List.choose (function NodeRef(d, n) when d = state.Diagram -> Some n | _ -> None)
            if List.isEmpty nodes then noEffects { state with Status = "Select items to copy." }
            else
                match Fragment.extract (project state) state.Diagram nodes with
                | Ok fragment ->
                    let dependencies = fragment.Fields.Length + fragment.Palette.Length + fragment.Styles.Length + fragment.Mappings.Length
                    noEffects { state with Clipboard = Some fragment; Template = Some "clipboard"; Status = sprintf "Copied %d item(s) with %d dependency definition(s)." fragment.Nodes.Length dependencies }
                | Error message -> noEffects { state with Status = sprintf "Not copied: %s" message }
        | "choose-template" -> noEffects { state with Template = Some key }
        | "insert-template" -> noEffects (insertTemplate state)
        | "connect-start" ->
            match selectedNode state with
            | Some node -> noEffects { state with Pending = Connecting node.Id; Status = sprintf "Choose the item that %s connects to." node.Label }
            | None -> noEffects { state with Status = "Select the item to connect from." }
        | "cancel" -> noEffects { state with Pending = NoPending; Status = "Cancelled." }
        | "delete" ->
            match selectedRef state with
            | Some(NodeRef(_, n)) -> noEffects (run (Flow(RemoveNode(state.Diagram, n, RemoveIncidentEdges))) "Deleted." state)
            | Some(EdgeRef(_, edge)) -> noEffects (run (Flow(RemoveEdge(state.Diagram, edge))) "Deleted." state)
            | _ -> noEffects { state with Status = "Select an item to delete it." }
        | "undo" ->
            if Editor.canUndo state.Session then noEffects { state with Session = Editor.undo state.Session; Pending = NoPending; Status = "Undone." }
            else noEffects { state with Status = "Nothing to undo." }
        | "redo" ->
            if Editor.canRedo state.Session then noEffects { state with Session = Editor.redo state.Session; Pending = NoPending; Status = "Redone." }
            else noEffects { state with Status = "Nothing to redo." }
        | "choose-field" -> noEffects { state with Field = Id.create<FieldKind> key |> Result.toOption }
        | "set-field-value" ->
            noEffects (withField state (fun target field ->
                match parseValue field value with
                | Ok v -> run (MetadataCmd(SetValue(target, field.Key, Explicit v))) (sprintf "%s set." field.Name) state
                | Error message -> { state with Status = sprintf "Not changed: %s" message }))
        | "set-field-option" ->
            noEffects (withField state (fun target field -> run (MetadataCmd(SetValue(target, field.Key, Explicit(Enum key)))) (sprintf "%s set." field.Name) state))
        | "set-field-unknown" -> noEffects (withField state (fun target field -> run (MetadataCmd(SetValue(target, field.Key, UnknownValue))) (sprintf "%s marked unknown." field.Name) state))
        | "clear-field" -> noEffects (withField state (fun target field -> run (MetadataCmd(ClearValue(target, field.Key))) (sprintf "%s cleared; the default or derived value shows through." field.Name) state))
        | "set-fill" ->
            match state.Session.Selection, HexColor.parse value with
            | (_ :: _ as targets), Ok hex -> noEffects (run (AppearanceCmd(SetOverride(targets, { Appearance.empty with Fill = Some(LiteralColor hex) }))) "Fill set." state)
            | _ :: _, Error message -> noEffects { state with Status = sprintf "Not changed: %s" message }
            | [], _ -> noEffects { state with Status = "Select an item first." }
        | "choose-fill-palette" ->
            match state.Session.Selection, Id.create<PaletteKind> key with
            | (_ :: _ as targets), Ok slot -> noEffects (run (AppearanceCmd(SetOverride(targets, { Appearance.empty with Fill = Some(PaletteColor slot) }))) "Fill set from the palette." state)
            | _ -> noEffects { state with Status = "Select an item and a palette slot." }
        | "reset-fill" ->
            match state.Session.Selection with
            | _ :: _ as targets -> noEffects (run (AppearanceCmd(ResetOverride(targets, [ FillProperty ]))) "Fill override removed; the next layer shows through." state)
            | [] -> noEffects { state with Status = "Select an item first." }
        | "toggle-select" ->
            match refOfKey state key with
            | Some reference ->
                let current = state.Session.Selection
                let next = if List.contains reference current then current |> List.filter ((<>) reference) else current @ [ reference ]
                noEffects { state with Session = Editor.select next state.Session; Pending = NoPending; Status = sprintf "%d selected." next.Length }
            | None -> noEffects state
        | "align-left" | "align-top" | "distribute-horizontally" -> noEffects (arrange state e.Name)
        | "choose-style" ->
            match selectedRef state with
            | Some target ->
                let style = if key = "" then None else Id.create<StyleKind> key |> Result.toOption
                noEffects (run (AppearanceCmd(ApplyStyle([ target ], style))) (if style.IsSome then "Style applied." else "Style removed.") state)
            | None -> noEffects { state with Status = "Select an item first." }
        | "draft-field-name" -> noEffects { state with Drafts = { state.Drafts with FieldName = value } }
        | "draft-field-type" -> noEffects { state with Drafts = { state.Drafts with FieldType = key } }
        | "draft-field-options" -> noEffects { state with Drafts = { state.Drafts with FieldOptions = value } }
        | "draft-field-scope" ->
            let scope = scopeChoices |> List.tryFind (fun (k, _, _) -> k = key) |> Option.map (fun (_, v, _) -> v) |> Option.defaultValue state.Drafts.FieldScope
            noEffects { state with Drafts = { state.Drafts with FieldScope = scope } }
        | "create-field" -> noEffects (createField state)
        | "draft-slot-name" -> noEffects { state with Drafts = { state.Drafts with SlotName = value } }
        | "draft-slot-color" -> noEffects { state with Drafts = { state.Drafts with SlotColor = value } }
        | "create-slot" ->
            match HexColor.parse state.Drafts.SlotColor with
            | _ when System.String.IsNullOrWhiteSpace state.Drafts.SlotName -> noEffects { state with Status = "Name the palette slot first." }
            | Error message -> noEffects { state with Status = sprintf "Not changed: %s" message }
            | Ok hex ->
                let key = unique ((project state).Palette |> List.map (fun p -> Id.value p.Id) |> Set.ofList) (slug state.Drafts.SlotName)
                let slot = { Id = Samples.idOf key; Name = state.Drafts.SlotName.Trim(); Value = PaletteLiteral hex; Description = None }
                let created = run (AppearanceCmd(AddPaletteSlot slot)) (sprintf "Palette slot %s added." slot.Name) state
                noEffects { created with Drafts = { created.Drafts with SlotName = ""; SlotColor = "" } }
        | "delete-slot" ->
            match Id.create<PaletteKind> key with
            | Ok slot -> noEffects (run (AppearanceCmd(RemovePaletteSlot(slot, BlockPaletteIfUsed))) "Palette slot deleted." state)
            | Error _ -> noEffects state
        | "materialize-slot" ->
            match Id.create<PaletteKind> key with
            | Ok slot -> noEffects (run (AppearanceCmd(RemovePaletteSlot(slot, MaterializePalette))) "Palette slot deleted; its color is now set directly where it was used." state)
            | Error _ -> noEffects state
        | "mapping-value" -> noEffects { state with Drafts = { state.Drafts with MappingValue = Some key } }
        | "mapping-slot" -> noEffects { state with Drafts = { state.Drafts with MappingSlot = Some key } }
        | "create-mapping-rule" -> noEffects (createMappingRule state)
        | "assign-lane" ->
            match selectedNode state with
            | Some node ->
                let lane = if key = "" then None else Id.create<GroupKind> key |> Result.toOption
                noEffects (run (Flow(AssignLane(state.Diagram, node.Id, lane))) "Lane changed." state)
            | None -> noEffects { state with Status = "Select an item to change its lane." }
        | "add-page" ->
            let used = (project state).Pages |> List.map (fun pg -> Id.value pg.Id) |> Set.ofList
            let number = Seq.initInfinite (fun i -> i + 1) |> Seq.find (fun i -> not (used.Contains(sprintf "page-%d" i)))
            let pageId: PageId = Samples.idOf (sprintf "page-%d" number)
            let rootId: ComponentNodeId = Samples.idOf (sprintf "page-%d-stack" number)
            let created =
                run (Batch("add page", [ Layout(AddPage(pageId, sprintf "Page %d" number, Some(sprintf "/page-%d" number))); Layout(AddComponent(pageId, None, 0, rootId, "stack")) ]))
                    (sprintf "Page %d added with an empty stack." number) state
            noEffects { created with Page = (if created.Session.Project <> state.Session.Project then Some pageId else state.Page) }
        | "open-page" -> noEffects { state with Page = Id.create<PageKind> key |> Result.toOption; Status = "Layout page opened." }
        | "open-diagram" -> noEffects { state with Page = None; Status = "Diagram opened." }
        | "layout-add-heading" -> noEffects (layoutOnRoot state (fun page root ->
            let used = ProjectOps.componentIds page.Nodes |> List.map Id.value |> Set.ofList
            let headingId: ComponentNodeId = Samples.idOf (unique used (sprintf "%s-heading" (Id.value page.Id)))
            let index = root.Slots |> Map.tryFind "children" |> Option.map List.length |> Option.defaultValue 0
            Batch("add heading", [ Layout(AddComponent(page.Id, Some { Parent = root.Id; Slot = "children" }, index, headingId, "heading"))
                                   Layout(SetComponentContent(page.Id, headingId, "text", "New heading"))
                                   Layout(SetComponentProperty(page.Id, headingId, "level", Some(Json.ofInt 2))) ]), "Heading added."))
        | "layout-set-text" ->
            match state.Page, Id.create<ComponentKind> key with
            | Some page, Ok heading -> noEffects (run (Layout(SetComponentContent(page, heading, "text", value))) "Heading text changed." state)
            | _ -> noEffects state
        | "layout-move-up" | "layout-move-down" ->
            noEffects (layoutOnRoot state (fun page root ->
                let children = root.Slots |> Map.tryFind "children" |> Option.defaultValue []
                match children |> List.tryFindIndex (fun c -> Id.value c.Id = key) with
                | Some index ->
                    let target = if e.Name = "layout-move-up" then max 0 (index - 1) else min (children.Length - 1) (index + 1)
                    Layout(MoveComponent(page.Id, children.[index].Id, Some { Parent = root.Id; Slot = "children" }, target)), "Reordered."
                | None -> Batch("nothing", []), "Nothing to reorder."))
        | "layout-density" ->
            noEffects (layoutOnRoot state (fun page root -> Layout(SetComponentProperty(page.Id, root.Id, "density", Some(JString key))), "Spacing changed."))
        | "save" -> { state with Status = "Saving…"; Saving = Some(project state) }, [ storage "save" "set" [ "value", JString(Codec.serialize (project state)) ] ]
        | "load" -> { state with Status = "Loading…" }, [ storage "load" "get" [] ]
        | "check-saved" -> { state with Status = "Checking the saved copy…" }, [ storage "compare" "get" [] ]
        | "merge-take-saved" -> noEffects { state with TakeSaved = state.TakeSaved.Add key }
        | "merge-keep-mine" -> noEffects { state with TakeSaved = state.TakeSaved.Remove key }
        | "merge-cancel" -> noEffects { state with Incoming = None; TakeSaved = Set.empty; Status = "Merge cancelled; nothing changed." }
        | "merge-apply" ->
            match state.Incoming with
            | Some saved ->
                let result = Merge.resolve state.Baseline (project state) saved state.TakeSaved
                match result.Conflicts |> List.filter (Merge.isItemConflict >> not) with
                | [] ->
                    match Editor.adopt result.Project state.Session with
                    | Ok session ->
                        // The saved copy is now the common ancestor for the next comparison.
                        noEffects { state with Session = session; Baseline = saved; Incoming = None; TakeSaved = Set.empty; Status = "Merged the saved changes. One undo reverts the merge." }
                    | Error findings -> noEffects { state with Status = sprintf "Not merged: %s" (describeFindings findings) }
                | blocking -> noEffects { state with Status = sprintf "Not merged: %s" (blocking |> List.map _.Message |> String.concat " ") }
            | None -> noEffects { state with Status = "There is no saved change to merge." }
        | other -> noEffects { state with Status = sprintf "Unrecognized action '%s'." other }

    let private onEffect (result: JsonValue) state =
        let field name json = Json.field name json
        match field "correlationId" result, field "outcome" result |> Option.bind (field "kind") with
        | Some(JString "save"), Some(JString "Success") ->
            { state with Status = "Saved."; Baseline = defaultArg state.Saving state.Baseline; Saving = None }
        | Some(JString "load"), Some(JString "Success") ->
            match field "outcome" result |> Option.bind (field "value") with
            | Some(JString text) ->
                match Codec.load text with
                | Ok loaded ->
                    // Reopening restores the canonical project, not the undo stack (FDA-1051).
                    let diagram = loaded.Diagrams |> List.tryHead |> Option.map _.Id |> Option.defaultValue state.Diagram
                    { initial loaded diagram with Status = "Loaded the saved project. Undo history starts fresh." }
                | Error error -> { state with Status = Codec.describeLoadError error }
            | _ -> { state with Status = "Nothing has been saved yet." }
        | Some(JString "compare"), Some(JString "Success") ->
            match field "outcome" result |> Option.bind (field "value") with
            | Some(JString text) ->
                match Codec.load text with
                | Ok saved when saved = state.Baseline -> { state with Incoming = None; Status = "The saved copy has not changed since you opened or saved it." }
                | Ok saved -> { state with Incoming = Some saved; TakeSaved = Set.empty; Status = "The saved copy has changed. Review the merge below." }
                | Error error -> { state with Status = Codec.describeLoadError error }
            | _ -> { state with Status = "Nothing has been saved yet." }
        | Some(JString _), Some(JString "Failure") -> { state with Status = "The browser could not complete the storage request."; Saving = None }
        | _ -> state

    // -- view ------------------------------------------------------------------

    let private str (s: string) = JString s
    let private item (fields: (string * JsonValue) list) = JObject fields

    let private metaSlots (project: Project) (diagram: Diagram) reference kind =
        let fields = diagram.Display.KindFields |> Map.tryFind kind |> Option.defaultValue diagram.Display.NodeFields
        let values =
            fields
            |> List.choose (fun k -> ProjectOps.tryField k project)
            // The canvas previews rendered output, so it shows only fields allowed there;
            // the inspector lists every field, marking the ones that are not printed.
            |> List.filter (fun f -> MetadataRules.applies f (ObjectRef.kind reference) && MetadataRules.allows Rendered f)
            |> List.map (fun f -> OutputValues.ofResolved f (ProjectOps.resolveField f reference project))
            |> List.filter (fun v -> v.State <> "missing")
        [ 1..4 ]
        |> List.collect (fun i ->
            match List.tryItem (i - 1) values with
            | Some v ->
                [ sprintf "m%dLabel" i, str v.Label
                  sprintf "m%dValue" i, str (defaultArg v.Text "")
                  sprintf "m%dNote" i, str (if v.State = "explicit" then "" else defaultArg v.Note v.State)
                  sprintf "m%dState" i, str v.State
                  sprintf "m%dHidden" i, JBool false
                  sprintf "m%dNoteHidden" i, JBool(v.State = "explicit") ]
            | None ->
                [ sprintf "m%dLabel" i, str ""; sprintf "m%dValue" i, str ""; sprintf "m%dNote" i, str ""; sprintf "m%dState" i, str ""
                  sprintf "m%dHidden" i, JBool true; sprintf "m%dNoteHidden" i, JBool true ])

    let private shapeName shape =
        match shape with
        | Some Rounded -> "rounded"
        | Some Pill -> "pill"
        | Some Ellipse -> "ellipse"
        | Some Diamond -> "diamond"
        | _ -> "rectangle"

    let private lineName line =
        match line with
        | Some Dashed -> "dashed"
        | Some Dotted -> "dotted"
        | _ -> "solid"

    let private layerText (project: Project) (e: Effective<ColorResolution>) =
        let source =
            match e.Value with
            | None -> "Forma default"
            | Some c ->
                match c.Source with
                | TokenColor t -> sprintf "Forma token %s" (TokenRef.value t)
                | PaletteColor p -> sprintf "palette slot %s" (ProjectOps.tryPalette p project |> Option.map _.Name |> Option.defaultValue (Id.value p))
                | LiteralColor h -> sprintf "literal %s" (HexColor.value h)
        match e.Layer with
        | FormaDefaultLayer -> "Forma default"
        | ProfileDefaultLayer(profile, kind) -> sprintf "%s (profile default for %s %s)" source profile kind
        | StyleLayer(id, revision) -> sprintf "%s (named style %s, revision %d)" source (ProjectOps.tryStyle id project |> Option.map _.Name |> Option.defaultValue (Id.value id)) revision
        | MappingLayer(id, _, legend) -> sprintf "%s (mapping %s: %s)" source (ProjectOps.tryMapping id project |> Option.map _.Name |> Option.defaultValue (Id.value id)) legend
        | OverrideLayer -> sprintf "%s (set on this item)" source

    /// References to a palette slot across object overrides, styles and mappings (FDA-1207).
    let private paletteUseCount (p: Project) (slot: PaletteSlotId) =
        let usesSlot (a: Appearance) = Appearance.colors a |> List.exists (function PaletteColor id -> id = slot | _ -> false)
        let objects = ProjectOps.allObjects p |> List.filter (fun r -> ProjectOps.appearanceOf r p |> Option.exists (fun a -> usesSlot a.Overrides)) |> List.length
        let styles = p.Styles |> List.filter (fun st -> usesSlot st.Appearance) |> List.length
        let mappings = p.Mappings |> List.filter (fun m -> m.Rules |> List.exists (fun r -> match r.Outcome with UseAppearance a -> usesSlot a | UseStyle _ -> false)) |> List.length
        objects + styles + mappings

    let view (state: EditorState) : JsonValue =
        let p = project state
        match diagramOf state with
        | None -> JObject [ "status", str "The diagram is not available." ]
        | Some diagram ->
            let profile = Profiles.tryFind diagram.Profile
            let selected = selectedRef state
            let changes = Diff.between state.Baseline p
            let isSelected r = List.contains r state.Session.Selection
            let connectingFrom = match state.Pending with Connecting n -> Some n | _ -> None
            let kindLabel kind = profile |> Option.bind (fun pr -> Profiles.nodeKind pr kind) |> Option.map _.Label |> Option.defaultValue kind
            let b = Projection.bounds diagram
            let dx, dy = Projection.padding - b.Position.X, Projection.padding - b.Position.Y
            let width, height = b.Size.Width + 2 * Projection.padding + 240, b.Size.Height + 2 * Projection.padding + 200
            let colorDecls (e: EffectiveAppearance) =
                [ "--ef-diagram-fill", e.Fill; "--ef-diagram-stroke", e.Stroke; "--ef-diagram-accent", e.Accent; "--ef-diagram-foreground", e.Foreground ]
                |> List.choose (fun (name, v) -> v.Value |> Option.bind _.Css |> Option.map (sprintf "%s: %s;" name))
            let geometry (bx: Box) =
                [ sprintf "--ef-diagram-x: %dpx;" (bx.Position.X + dx); sprintf "--ef-diagram-y: %dpx;" (bx.Position.Y + dy)
                  sprintf "--ef-diagram-w: %dpx;" bx.Size.Width; sprintf "--ef-diagram-h: %dpx;" bx.Size.Height ]
            let effect r = AppearanceResolution.resolve EditorScope p r |> fst

            let groups =
                diagram.Groups
                |> List.map (fun g ->
                    let r = GroupRef(diagram.Id, g.Id)
                    item
                        [ "key", str (sprintf "group:%s" (Id.value g.Id))
                          "style", str (String.concat " " (geometry g.Box @ colorDecls (effect r)))
                          "variant", str (match g.Kind with Group -> "group" | Lane -> "lane" | Phase -> "phase")
                          "kind", str (match g.Kind with Group -> "Group" | Lane -> "Lane" | Phase -> "Phase")
                          "label", str g.Label ])

            let nodes =
                diagram.Nodes
                |> List.map (fun n ->
                    let r = NodeRef(diagram.Id, n.Id)
                    let e = effect r
                    let adorners =
                        [ if isSelected r then "studio-selected"
                          if connectingFrom = Some n.Id then "studio-connect-source" ]
                    item
                        ([ "key", str (sprintf "node:%s" (Id.value n.Id))
                           "id", str (Id.value n.Id)
                           "classes", str (String.concat " " ("ef-diagram-node" :: adorners))
                           "shape", str (shapeName e.Shape.Value)
                           "style", str (String.concat " " (geometry n.Box @ colorDecls e))
                           "kind", str (kindLabel n.Kind)
                           "label", str n.Label
                           "name", str (sprintf "%s: %s" (kindLabel n.Kind) n.Label)
                           "pressed", str (if isSelected r then "true" else "false") ]
                         @ metaSlots p diagram r n.Kind))

            let nodeById id = diagram.Nodes |> List.tryFind (fun n -> n.Id = id)
            let shift (bx: Box) = { bx with Position = { X = bx.Position.X + dx; Y = bx.Position.Y + dy } }
            let routed =
                diagram.Edges
                |> List.choose (fun edge ->
                    match nodeById edge.Source.Node, nodeById edge.Target.Node with
                    | Some s, Some t ->
                        let routing = match edge.Routing with Manual ps -> Manual(ps |> List.map (fun pt -> { X = pt.X + dx; Y = pt.Y + dy })) | other -> other
                        let points, labelAt = Projection.route routing (shift s.Box) (shift t.Box)
                        Some(edge, s, t, points, labelAt)
                    | _ -> None)
            let edges =
                routed
                |> List.map (fun (edge, _, _, points, _) ->
                    let r = EdgeRef(diagram.Id, edge.Id)
                    let e = effect r
                    let d = points |> List.mapi (fun i pt -> sprintf "%s%d %d" (if i = 0 then "M" else " L") pt.X pt.Y) |> String.concat ""
                    item
                        [ "key", str (sprintf "edge:%s" (Id.value edge.Id))
                          "d", str d
                          "line", str (lineName e.Line.Value)
                          "classes", str (if isSelected r then "ef-diagram-connector studio-selected-wire" else "ef-diagram-connector")
                          "style", str (e.ConnectorStroke.Value |> Option.bind _.Css |> Option.map (sprintf "--ef-diagram-connector-stroke: %s;") |> Option.defaultValue "") ])
            // Endpoint handles are editor adorners for the one selected connector.
            let endpoints =
                routed
                |> List.filter (fun (edge, _, _, _, _) -> selected = Some(EdgeRef(diagram.Id, edge.Id)))
                |> List.collect (fun (edge, s, t, points, _) ->
                    let place (pt: Point) = sprintf "--studio-x: %dpx; --studio-y: %dpx;" pt.X pt.Y
                    let pressed which = str (if state.Pending = Reconnecting(edge.Id, which) then "true" else "false")
                    match points, List.tryLast points with
                    | first :: _, Some last ->
                        [ item [ "key", str "source"; "style", str (place first); "pressed", pressed SourceEnd; "label", str (sprintf "Move the start of this connector (now %s)" s.Label) ]
                          item [ "key", str "target"; "style", str (place last); "pressed", pressed TargetEnd; "label", str (sprintf "Move the end of this connector (now %s)" t.Label) ] ]
                    | _ -> [])
            let labels =
                routed
                |> List.choose (fun (edge, _, _, _, at) ->
                    edge.Label |> Option.map (fun l -> item [ "key", str (sprintf "edge:%s" (Id.value edge.Id)); "text", str l; "style", str (sprintf "--ef-diagram-x: %dpx; --ef-diagram-y: %dpx;" at.X at.Y) ]))

            let outline =
                (diagram.Nodes
                 |> List.map (fun n ->
                     let r = NodeRef(diagram.Id, n.Id)
                     let lane = diagram.Groups |> List.tryFind (fun g -> g.Kind = Lane && List.contains n.Id g.Members) |> Option.map (fun g -> sprintf ", lane %s" g.Label) |> Option.defaultValue ""
                     item [ "key", str (sprintf "node:%s" (Id.value n.Id)); "text", str (sprintf "%s: %s%s" (kindLabel n.Kind) n.Label lane); "ref", str (Id.value n.Id); "current", str (if isSelected r then "true" else "false")
                            "toggleLabel", str (sprintf "%s %s %s selection" (if List.contains r state.Session.Selection then "Remove" else "Add") n.Label (if List.contains r state.Session.Selection then "from" else "to"))
                            "inSelection", str (if List.contains r state.Session.Selection then "true" else "false") ]))
                @ (routed
                   |> List.map (fun (edge, s, t, _, _) ->
                       let r = EdgeRef(diagram.Id, edge.Id)
                       let label = edge.Label |> Option.map (sprintf " (%s)") |> Option.defaultValue ""
                       item [ "key", str (sprintf "edge:%s" (Id.value edge.Id)); "text", str (sprintf "Connector: %s to %s%s" s.Label t.Label label); "ref", str (Id.value edge.Id); "current", str (if isSelected r then "true" else "false")
                              "toggleLabel", str (sprintf "%s connector %s to %s %s selection" (if List.contains r state.Session.Selection then "Remove" else "Add") s.Label t.Label (if List.contains r state.Session.Selection then "from" else "to"))
                              "inSelection", str (if List.contains r state.Session.Selection then "true" else "false") ]))

            let selectedLabel, selectedKind, selectedValue =
                match selected with
                | Some(NodeRef(_, n)) -> nodeById n |> Option.map (fun node -> node.Label, kindLabel node.Kind, node.Label) |> Option.defaultValue ("", "", "")
                | Some(EdgeRef(_, e)) ->
                    diagram.Edges |> List.tryFind (fun x -> x.Id = e) |> Option.map (fun x -> defaultArg x.Label "Unlabelled connector", "Connector", defaultArg x.Label "") |> Option.defaultValue ("", "", "")
                | _ -> "", "", ""

            let applicable =
                selected
                |> Option.map (fun r -> p.Fields |> List.filter (fun f -> MetadataRules.applies f (ObjectRef.kind r)))
                |> Option.defaultValue []
            let selection = state.Session.Selection
            // With several objects selected, a field whose values differ shows as
            // mixed instead of pretending one value applies to all (FDA-973).
            let metadataRows =
                applicable
                |> List.filter (fun f -> selection |> List.forall (fun r -> MetadataRules.applies f (ObjectRef.kind r)))
                |> List.map (fun f ->
                    let values = selection |> List.map (fun r -> OutputValues.ofResolved f (ProjectOps.resolveField f r p))
                    let scope = if f.Disclosure.Scopes.Contains Rendered then "" else " · not printed"
                    match values |> List.distinctBy (fun v -> v.Value, v.State) with
                    | [ v ] -> item [ "key", str (Id.value f.Key); "label", str f.Name; "text", str (defaultArg v.Text "—"); "state", str (sprintf "%s%s" (defaultArg v.Note v.State) scope) ]
                    | _ -> item [ "key", str (Id.value f.Key); "label", str f.Name; "text", str "Mixed"; "state", str (sprintf "%d different values%s" (values |> List.distinctBy (fun v -> v.Value, v.State) |> List.length) scope) ])
            let chosenField = state.Field |> Option.bind (fun k -> applicable |> List.tryFind (fun f -> f.Key = k))
            let fieldChoices =
                match chosenField with
                | Some { Type = EnumField options } -> options |> List.map (fun o -> item [ "key", str o.Id; "label", str o.Label ])
                | _ -> []
            let effective = selected |> Option.filter (function NodeRef _ | EdgeRef _ | GroupRef _ -> true | _ -> false) |> Option.map effect
            let fillHex =
                match selected |> Option.bind (fun r -> ProjectOps.appearanceOf r p) |> Option.bind (fun a -> a.Overrides.Fill) with
                | Some(LiteralColor h) -> HexColor.value h
                | _ -> ""
            let styleName =
                selected |> Option.bind (fun r -> ProjectOps.appearanceOf r p) |> Option.bind _.Style |> Option.bind (fun s -> ProjectOps.tryStyle s p) |> Option.map _.Name |> Option.defaultValue "None"
            let blockers = Validation.run p |> List.filter (fun f -> f.Target.StartsWith(sprintf "diagram:%s" (Id.value diagram.Id)))

            JObject
                [ "pages", p.Pages |> List.map (fun pg -> item [ "key", str (Id.value pg.Id); "label", str (sprintf "Layout: %s" pg.Name); "current", str (if state.Page = Some pg.Id then "page" else "false") ]) |> JArray
                  "flowLabel", str (sprintf "Flow: %s" diagram.Name)
                  "flowCurrent", str (if state.Page.IsNone then "page" else "false")
                  "flowVisible", JBool state.Page.IsNone
                  "layoutVisible", JBool state.Page.IsSome
                  "pageName", str (state.Page |> Option.bind (fun id -> ProjectOps.tryPage id p) |> Option.map _.Name |> Option.defaultValue "")
                  "density",
                  str (state.Page |> Option.bind (fun id -> ProjectOps.tryPage id p) |> Option.bind (fun pg -> pg.Nodes |> List.tryFind (fun n -> n.Component = "stack"))
                       |> Option.bind (fun root -> Map.tryFind "density" root.Properties) |> Option.map (function JString d -> d | _ -> "standard") |> Option.defaultValue "standard")
                  "layoutItems",
                  (state.Page |> Option.bind (fun id -> ProjectOps.tryPage id p) |> Option.bind (fun pg -> pg.Nodes |> List.tryFind (fun n -> n.Component = "stack"))
                   |> Option.map (fun root -> root.Slots |> Map.tryFind "children" |> Option.defaultValue [])
                   |> Option.defaultValue []
                   |> List.map (fun c ->
                       let text = c.Content |> Map.tryFind "text" |> Option.map (function JString t -> t | _ -> "") |> Option.defaultValue ""
                       item [ "key", str (Id.value c.Id); "text", str text; "label", str (sprintf "Heading text for %s" (Id.value c.Id)) ]))
                  |> JArray
                  "densities", [ "relaxed"; "standard"; "compact"; "analytical" ] |> List.map (fun d -> item [ "key", str d; "label", str d ]) |> JArray
                  "diagramName", str diagram.Name
                  "profileName", str (profile |> Option.map (fun pr -> sprintf "%s profile %s" pr.Name pr.Version) |> Option.defaultValue "Unavailable profile")
                  "status", str state.Status
                  "undoDisabled", JBool(not (Editor.canUndo state.Session))
                  "redoDisabled", JBool(not (Editor.canRedo state.Session))
                  "historyCount", Json.ofInt state.Session.Undo.Length
                  "canvasStyle", str (sprintf "--ef-diagram-canvas-w: %dpx; --ef-diagram-canvas-h: %dpx; --studio-zoom: %s;" width height (string (float state.Zoom / 100.0)))
                  "zoomFactor", str (string (float state.Zoom / 100.0))
                  "zoomLabel", str (sprintf "%d%%" state.Zoom)
                  "zoomOutDisabled", JBool(state.Zoom <= List.head zoomLevels)
                  "zoomInDisabled", JBool(state.Zoom >= List.last zoomLevels)
                  "snapPressed", str (if state.Snap then "true" else "false")
                  "changes",
                  changes
                  |> List.mapi (fun i c ->
                      let category =
                          match c.Category with
                          | Topology -> "Structure" | Geometry -> "Position or size" | Semantic -> "Meaning" | Label -> "Label"
                          | MetadataChange -> "Metadata" | Presentation -> "Appearance" | PresentationDefinition -> "Palette, style or rule"
                          | Routing -> "Routing" | Membership -> "Lane or group" | ReferenceChange -> "Reference" | Schema -> "Schema" | LayoutChange -> "Layout"
                      item [ "key", str (sprintf "%d:%s:%s" i c.Code c.Target); "category", str category; "summary", str c.Summary ])
                  |> JArray
                  "changeCount", str (match List.length changes with 0 -> "No changes since the last save or open." | 1 -> "1 change since the last save or open." | n -> sprintf "%d changes since the last save or open." n)
                  "mergeOpen", JBool state.Incoming.IsSome
                  "mergeIncoming",
                  (match state.Incoming with
                   | Some saved -> Diff.between state.Baseline saved |> List.mapi (fun i c -> item [ "key", str (sprintf "%d:%s:%s" i c.Code c.Target); "text", str c.Summary ])
                   | None -> [])
                  |> JArray
                  "mergeConflicts",
                  (match state.Incoming with
                   | Some saved ->
                       Merge.resolve state.Baseline p saved state.TakeSaved
                       |> fun r -> r.Conflicts
                       |> List.map (fun c ->
                           let choosable = Merge.isItemConflict c
                           let saved = state.TakeSaved.Contains c.Target
                           item [ "key", str c.Target; "text", str (sprintf "%s — %s" c.Target c.Message)
                                  "choosable", JBool choosable; "blocking", JBool(not choosable)
                                  "minePressed", str (if saved then "false" else "true"); "savedPressed", str (if saved then "true" else "false") ])
                   | None -> [])
                  |> JArray
                  "templates",
                  templatesOf state
                  |> List.map (fun (k, name, _, _) -> item [ "key", str k; "label", str name; "pressed", str (if state.Template = Some k then "true" else "false") ])
                  |> JArray
                  "templateChosen", JBool (chosenTemplate state |> Option.isSome)
                  "templateDescription", str (chosenTemplate state |> Option.map (fun (_, _, d, _) -> d) |> Option.defaultValue "")
                  "templatePlan",
                  (match chosenTemplate state with
                   | Some(_, _, _, fragment) ->
                       match Fragment.plan fragment p RemapConflicting with
                       | Ok [] -> [ item [ "key", str "none"; "text", str "No fields, colors or rules to bring along." ] ]
                       | Ok decisions -> decisions |> List.map (fun d -> item [ "key", str (sprintf "%s:%s" d.Kind d.SourceId); "text", str (describeDecision d) ])
                       | Error message -> [ item [ "key", str "error"; "text", str message ] ]
                   | None -> [])
                  |> JArray
                  "viewBox", str (sprintf "0 0 %d %d" width height)
                  "groups", JArray groups
                  "nodes", JArray nodes
                  "edges", JArray edges
                  "edgeLabels", JArray labels
                  "outline", JArray outline
                  "kinds", profile |> Option.map (fun pr -> pr.NodeKinds |> List.map (fun k -> item [ "key", str k.Kind; "label", str k.Label; "pressed", str (if k.Kind = state.AddKind then "true" else "false") ])) |> Option.defaultValue [] |> JArray
                  "hasSelection", JBool selected.IsSome
                  "selectionCount", Json.ofInt selection.Length
                  "multiSelection", JBool(selection.Length > 1)
                  "noSelection", JBool selected.IsNone
                  "selectionIsNode", JBool(match selected with Some(NodeRef _) -> true | _ -> false)
                  "selectedLabel", str selectedLabel
                  "selectedKind", str selectedKind
                  "labelValue", str selectedValue
                  "widthValue", str (selectedNode state |> Option.map (fun n -> string n.Box.Size.Width) |> Option.defaultValue "")
                  "heightValue", str (selectedNode state |> Option.map (fun n -> string n.Box.Size.Height) |> Option.defaultValue "")
                  "connecting", JBool(connectingFrom.IsSome)
                  "reconnecting", JBool(match state.Pending with Reconnecting _ -> true | _ -> false)
                  "endpoints", JArray endpoints
                  "connectDisabled", JBool(match selected with Some(NodeRef _) -> false | _ -> true)
                  "fields", applicable |> List.map (fun f -> item [ "key", str (Id.value f.Key); "label", str f.Name; "pressed", str (if Some f.Key = state.Field then "true" else "false") ]) |> JArray
                  "metadataRows", JArray metadataRows
                  "fieldChosen", JBool chosenField.IsSome
                  "fieldName", str (chosenField |> Option.map _.Name |> Option.defaultValue "")
                  "fieldHelp", str (chosenField |> Option.bind _.Help |> Option.defaultValue "")
                  "fieldIsEnum", JBool(not (List.isEmpty fieldChoices))
                  "fieldChoices", JArray fieldChoices
                  "fillValue", str fillHex
                  "fillSource", str (effective |> Option.map (fun e -> layerText p e.Fill) |> Option.defaultValue "")
                  "strokeSource", str (effective |> Option.map (fun e -> layerText p e.Stroke) |> Option.defaultValue "")
                  "styleName", str styleName
                  "palette", p.Palette |> List.map (fun s -> item [ "key", str (Id.value s.Id); "label", str s.Name ]) |> JArray
                  "styles", p.Styles |> List.map (fun s -> item [ "key", str (Id.value s.Id); "label", str s.Name ]) |> JArray
                  "draftFieldName", str state.Drafts.FieldName
                  "draftFieldOptions", str state.Drafts.FieldOptions
                  "draftFieldIsChoice", JBool(state.Drafts.FieldType = "choice")
                  "fieldTypes", fieldTypes |> List.map (fun (k, label) -> item [ "key", str k; "label", str label; "pressed", str (if k = state.Drafts.FieldType then "true" else "false") ]) |> JArray
                  "fieldScopes", scopeChoices |> List.map (fun (k, v, label) -> item [ "key", str k; "label", str label; "pressed", str (if v = state.Drafts.FieldScope then "true" else "false") ]) |> JArray
                  "draftSlotName", str state.Drafts.SlotName
                  "draftSlotColor", str state.Drafts.SlotColor
                  "paletteRows",
                  p.Palette
                  |> List.map (fun slot ->
                      let uses = paletteUseCount p slot.Id
                      let value = match slot.Value with PaletteLiteral h -> HexColor.value h | PaletteToken t -> TokenRef.value t
                      item [ "key", str (Id.value slot.Id); "label", str slot.Name; "value", str value; "uses", str (sprintf "%d %s" uses (if uses = 1 then "use" else "uses")) ])
                  |> JArray
                  "mappingAvailable", JBool(match chosenField with Some { Type = EnumField _; Disclosure = d } -> d.DerivedPresentation | _ -> false)
                  "mappingValues",
                  (match chosenField with
                   | Some { Type = EnumField options } -> options |> List.map (fun o -> item [ "key", str o.Id; "label", str o.Label; "pressed", str (if Some o.Id = state.Drafts.MappingValue then "true" else "false") ])
                   | _ -> [])
                  |> JArray
                  "mappingSlots", p.Palette |> List.map (fun slot -> item [ "key", str (Id.value slot.Id); "label", str slot.Name; "pressed", str (if Some(Id.value slot.Id) = state.Drafts.MappingSlot then "true" else "false") ]) |> JArray
                  "mappings",
                  p.Mappings
                  |> List.map (fun m ->
                      let affected =
                          diagram.Nodes
                          |> List.filter (fun n ->
                              match (effect (NodeRef(diagram.Id, n.Id))).Fill.Layer with
                              | MappingLayer(id, _, _) -> id = m.Id
                              | _ -> false)
                          |> List.length
                      let rules = m.Rules |> List.map _.Legend |> String.concat "; "
                      item [ "key", str (Id.value m.Id); "text", str (sprintf "%s: %s. Currently colors %d %s." m.Name rules affected (if affected = 1 then "item" else "items")) ])
                  |> JArray
                  "lanes",
                  diagram.Groups
                  |> List.filter (fun g -> g.Kind = Lane)
                  |> List.map (fun g ->
                      let inLane = match selected with Some(NodeRef(_, n)) -> List.contains n g.Members | _ -> false
                      item [ "key", str (Id.value g.Id); "label", str g.Label; "pressed", str (if inLane then "true" else "false") ])
                  |> JArray
                  "findingCount", Json.ofInt blockers.Length
                  "findings", blockers |> List.mapi (fun i f -> item [ "key", str (sprintf "f%d" i); "text", str f.Message ]) |> JArray ]

    /// Handles one Limen `BrowserToEngineMessage` and returns the new state plus
    /// the complete `EngineToBrowserMessage` (view, effects, cancellations).
    let handle (message: JsonValue) (state: EditorState) : EditorState * JsonValue =
        let next, effects =
            match Json.field "kind" message with
            | Some(JString "Event") ->
                match parseEvent message with
                | Some e -> onEvent e state
                | None -> state, []
            | Some(JString "EffectResult") ->
                match Json.field "result" message with
                | Some result -> onEffect result state, []
                | None -> state, []
            | _ -> state, []
        next, JObject [ "view", view next; "effects", JArray effects; "cancellations", JArray [] ]

    /// Initial state for the hosted editor: the canonical Workflow sample.
    let start () =
        match Samples.purchaseWorkflow () with
        | Ok project -> initial project Samples.diagramId
        | Error findings -> { initial (Samples.emptyProject "empty" "Empty project") Samples.diagramId with Status = describeFindings findings }

    /// Text entry point for the WebAssembly glue: JSON message in, JSON response out.
    /// Malformed input leaves the state unchanged and reports it in the view.
    let dispatchText (messageJson: string) (state: EditorState) : EditorState * string =
        match Json.parse messageJson with
        | Ok message ->
            let next, response = handle message state
            next, Json.serialize response
        | Error error ->
            let next = { state with Status = sprintf "Ignored a malformed message: %s" error }
            next, Json.serialize (JObject [ "view", view next; "effects", JArray []; "cancellations", JArray [] ])
