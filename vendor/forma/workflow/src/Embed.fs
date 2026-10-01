namespace Forma.Workflow

open System

/// A gesture waiting for its second step (non-drag connect and reconnect).
type Pending =
    | NoPending
    | Connecting of Endpoint
    | Reconnecting of ObjectId * EdgeEnd

/// A DOM event translated by the host element into data. It carries no meaning;
/// the engine decides what, if anything, it does.
type UiEvent =
    { Name: string
      Key: string option
      Arg: string option
      Value: string option
      Fields: (string * string) list
      Toggle: bool
      Shift: bool }

/// Messages from the host (the <forma-workflow> element or a .NET host).
type HostMessage =
    | Init of mode: HostMode * document: string option
    | Load of document: string
    | SetMode of HostMode
    | Execute of EditCommand
    | Undo
    | Redo
    | SelectObjects of ObjectRef list
    | FocusObject of ObjectRef
    | SetRuntimeState of (ObjectId * Status option) list
    | Ui of UiEvent

/// What the engine tells the host happened. These are the public events.
type Emission =
    | Loaded of ValidationReport
    | Changed of Change
    | SelectionChanged of ObjectRef list
    | FocusChanged of ObjectRef option
    | IntentActivated of ObjectRef * Interaction
    | Picked of ObjectRef list
    | Refused of string

type EmbedState =
    { Mode: HostMode
      Workflow: Workflow option
      Report: ValidationReport option
      History: History
      Selection: ObjectRef list
      Focus: ObjectRef option
      Pending: Pending
      /// Percent; view state, never stored in the document.
      Zoom: int
      /// Runtime-visualization overlay; never stored in the document.
      Runtime: Map<ObjectId, Status>
      AddKind: CoreNodeKind
      Announcement: string
      IdPrefix: string }

type EmbedOutput =
    { State: EmbedState
      Html: string
      /// Element id the host should focus after rendering, if any.
      FocusElement: string option
      Emissions: Emission list }

/// The embeddable workflow component as a pure engine. A host element feeds it
/// messages and renders the HTML it returns; every decision happens here.
[<RequireQualifiedAccess>]
module Embed =
    /// The versioned host boundary identifier.
    let protocol = "forma-workflow-host/1"

    let zoomLevels = [ 50; 75; 100; 125; 150; 200 ]

    let initial (mode: HostMode) (instance: string) =
        { Mode = mode; Workflow = None; Report = None; History = History.empty; Selection = []; Focus = None; Pending = NoPending
          Zoom = 100; Runtime = Map.empty; AddKind = Task; Announcement = ""; IdPrefix = instance }

    let canEdit (s: EmbedState) = s.Mode = EditMode
    let canSelect (s: EmbedState) = s.Mode <> ViewMode && s.Mode <> RuntimeMode

    // -- transitions -------------------------------------------------------------

    let private announce text (s: EmbedState) = { s with Announcement = text }

    let private loadText (text: string) (s: EmbedState) =
        let report = Validation.load text
        match report.Workflow with
        | Some w ->
            let s =
                { s with
                    Workflow = Some w
                    Report = Some report
                    History = History.empty
                    Selection = []
                    Focus = None
                    Pending = NoPending
                    Runtime = Map.empty }
            announce $"Opened {w.Title}." s, [ Loaded report ]
        | None ->
            let first = report.Findings |> List.tryHead |> Option.map _.Message |> Option.defaultValue "The workflow could not be read."
            announce first { s with Workflow = None; Report = Some report }, [ Loaded report ]

    let private applyEdit (cmd: EditCommand) (s: EmbedState) =
        match s.Workflow with
        | None -> announce "No workflow is loaded." s, [ Refused "No workflow is loaded." ]
        | Some _ when not (canEdit s) ->
            let text = Editing.refusalText (NotPermittedInMode s.Mode)
            announce text s, [ Refused text ]
        | Some w ->
            match Editing.apply cmd w with
            | Ok change ->
                let alive = s.Selection |> List.filter (ObjectRef.exists change.After)
                let selection =
                    match cmd with
                    | AddNode _ ->
                        change.After.Nodes |> List.tryFind (fun n -> not (w.Nodes |> List.exists (fun o -> o.Id = n.Id))) |> Option.map (fun n -> [ NodeRef n.Id ]) |> Option.defaultValue alive
                    | Connect _ ->
                        change.After.Edges |> List.tryFind (fun e -> not (w.Edges |> List.exists (fun o -> o.Id = e.Id))) |> Option.map (fun e -> [ EdgeRef e.Id ]) |> Option.defaultValue alive
                    | AddGroup _ ->
                        change.After.Groups |> List.tryFind (fun g -> not (w.Groups |> List.exists (fun o -> o.Id = g.Id))) |> Option.map (fun g -> [ GroupRef g.Id ]) |> Option.defaultValue alive
                    | _ -> alive
                let selectionEvents = if selection <> s.Selection then [ SelectionChanged selection ] else []
                let s =
                    { s with
                        Workflow = Some change.After
                        Report = Some change.Report
                        History = History.record w s.History
                        Selection = selection
                        Focus = s.Focus |> Option.filter (ObjectRef.exists change.After)
                        Pending = NoPending }
                announce (change.Description + ".") s, Changed change :: selectionEvents
            | Error refusal ->
                let text = Editing.refusalText refusal
                announce text s, [ Refused text ]

    let private history (forward: bool) (s: EmbedState) =
        match s.Workflow with
        | Some w when canEdit s ->
            match (if forward then History.redo w s.History else History.undo w s.History) with
            | Some(restored, h) ->
                let report = Validation.check restored
                let change = { Command = ReplaceDocument restored; Description = (if forward then "Redo" else "Undo"); Before = w; After = restored; Report = report }
                let s = { s with Workflow = Some restored; Report = Some report; History = h; Selection = s.Selection |> List.filter (ObjectRef.exists restored); Pending = NoPending }
                announce (if forward then "Redone." else "Undone.") s, [ Changed change ]
            | None -> announce (if forward then "Nothing to redo." else "Nothing to undo.") s, []
        | _ -> s, []

    /// The readable name of an object, for announcements.
    let nameOf (w: Workflow) (r: ObjectRef) =
        match r with
        | NodeRef id -> w.Nodes |> List.tryFind (fun n -> n.Id = id) |> Option.map Render.nodeName |> Option.defaultValue id.Value
        | EdgeRef id -> w.Edges |> List.tryFind (fun e -> e.Id = id) |> Option.map (Render.relationText w Phrases.english) |> Option.defaultValue id.Value
        | GroupRef id -> w.Groups |> List.tryFind (fun g -> g.Id = id) |> Option.map _.Label |> Option.defaultValue id.Value

    let private select (refs: ObjectRef list) (s: EmbedState) =
        match s.Workflow with
        | Some w ->
            let refs = refs |> List.filter (ObjectRef.exists w) |> List.distinct
            if refs = s.Selection then s, []
            else
                let text =
                    match refs with
                    | [] -> "Nothing selected."
                    | [ EdgeRef _ as one ] -> "Selected connection: " + nameOf w one
                    | [ one ] -> "Selected " + nameOf w one + "."
                    | many -> string many.Length + " items selected."
                announce text { s with Selection = refs }, [ SelectionChanged refs ]
        | None -> s, []

    let private toggle (r: ObjectRef) (s: EmbedState) =
        if List.contains r s.Selection then select (s.Selection |> List.filter ((<>) r)) s else select (s.Selection @ [ r ]) s

    let private selectedNodes (s: EmbedState) = s.Selection |> List.choose (function NodeRef id -> Some id | _ -> None)

    // -- UI event translation ----------------------------------------------------

    let private parseFloat (s: string) =
        match Double.TryParse(s, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture) with
        | true, v when not (Double.IsNaN v || Double.IsInfinity v) -> Some v
        | _ -> None

    let private pair (s: string option) =
        match s |> Option.map (fun v -> v.Split([| ' '; ',' |], StringSplitOptions.RemoveEmptyEntries)) with
        | Some [| a; b |] -> Option.map2 (fun x y -> x, y) (parseFloat a) (parseFloat b)
        | _ -> None

    let private field (name: string) (e: UiEvent) = e.Fields |> List.tryFind (fst >> (=) name) |> Option.map snd |> Option.map (fun v -> v.Trim())

    let private nonEmpty (s: string option) = s |> Option.filter (String.IsNullOrWhiteSpace >> not)

    let parseColor (text: string) : Result<ColorValue option, string> =
        let t = text.Trim().ToLowerInvariant()
        if t = "" || t = "default" then Ok None
        else
            match Codec.colorValue "colour" (Json.String(if t.StartsWith "#" || t.StartsWith "token:" || t.StartsWith "palette:" then t else "token:" + t)) with
            | Ok c -> Ok(Some c)
            | Error _ -> Error $"\"{text}\" is not a colour: use #rrggbb, a Forma token or a palette slot."

    /// Parses a metadata value as the chosen type. Unknown JSON stays JSON.
    let parseMetadata (kind: string) (text: string) : Result<Json, string> =
        match kind with
        | "number" ->
            match Json.parse (text.Trim()) with
            | Ok(Json.Number _ as n) -> Ok n
            | _ -> Error $"\"{text}\" is not a number."
        | "boolean" ->
            match text.Trim().ToLowerInvariant() with
            | "true" | "yes" -> Ok(Json.Bool true)
            | "false" | "no" -> Ok(Json.Bool false)
            | _ -> Error "Use yes or no."
        | "json" -> Json.parse text |> Result.mapError (fun e -> "Not valid JSON: " + e)
        | _ -> Ok(Json.String text)

    let private refOf (e: UiEvent) = e.Key |> Option.bind ObjectRef.tryParse

    let private nodeOf (e: UiEvent) = match refOf e with Some(NodeRef id) -> Some id | _ -> None

    let private stateOf (value: string) (label: string option) : Status option =
        if value = "" then None
        else
            match Codec.states |> List.tryFind (fst >> (=) value) with
            | Some(_, c) -> Some { State = CoreStatus(c, label); Detail = None; UpdatedAt = None }
            | None ->
                QualifiedName.tryParse value
                |> Option.map (fun q -> { State = CustomStatus(q, label |> Option.defaultValue q.Name); Detail = None; UpdatedAt = None })

    let rec private onUi (e: UiEvent) (s: EmbedState) : EmbedState * Emission list =
        let w = s.Workflow
        let edit cmd = applyEdit cmd s
        let withRef f = match refOf e with Some r -> f r | None -> s, []
        let fail text = announce text s, [ Refused text ]
        match e.Name with
        | "select" ->
            withRef (fun r ->
                match s.Pending, r with
                | Connecting source, NodeRef target when canEdit s -> edit (Connect(source, { Node = target; Port = None }, None))
                | Reconnecting(edge, which), NodeRef target when canEdit s -> edit (Reconnect(edge, which, { Node = target; Port = None }))
                | _ when not (canSelect s || canEdit s) -> s, []
                | _ when e.Toggle || s.Mode = PickMode -> toggle r s
                | _ -> select [ r ] s)
        | "clear-selection" -> select [] { s with Pending = NoPending }
        | "select-all" -> w |> Option.map (fun w -> select (w.Nodes |> List.map (fun n -> NodeRef n.Id)) s) |> Option.defaultValue (s, [])
        | "focus-finding" -> withRef (fun r -> let s, ev = select [ r ] s in { s with Focus = Some r }, ev @ [ FocusChanged(Some r) ])
        | "intent" ->
            withRef (fun r ->
                let interaction =
                    w
                    |> Option.bind (fun w ->
                        match r with
                        | NodeRef id -> w.Nodes |> List.tryFind (fun n -> n.Id = id) |> Option.bind _.Interaction
                        | EdgeRef id -> w.Edges |> List.tryFind (fun x -> x.Id = id) |> Option.bind _.Interaction
                        | GroupRef id -> w.Groups |> List.tryFind (fun g -> g.Id = id) |> Option.bind _.Interaction)
                match interaction with
                | Some i -> announce "Sent to the application." s, [ IntentActivated(r, i) ]
                | None -> s, [])
        | "pick-confirm" when s.Mode = PickMode -> announce $"{s.Selection.Length} item(s) chosen." s, [ Picked s.Selection ]
        | "zoom-in" -> { s with Zoom = zoomLevels |> List.tryFind (fun z -> z > s.Zoom) |> Option.defaultValue s.Zoom } |> fun s -> announce ("Zoom " + string s.Zoom + "%.") s, []
        | "zoom-out" -> { s with Zoom = zoomLevels |> List.rev |> List.tryFind (fun z -> z < s.Zoom) |> Option.defaultValue s.Zoom } |> fun s -> announce ("Zoom " + string s.Zoom + "%.") s, []
        | "zoom-reset" -> announce "Zoom 100%." { s with Zoom = 100 }, []
        | "undo" -> history false s
        | "redo" -> history true s
        | "add-kind" ->
            match e.Value |> Option.bind (fun v -> Codec.nodeKinds |> List.tryFind (fst >> (=) v)) with
            | Some(_, k) -> { s with AddKind = k }, []
            | None -> s, []
        | "add-node" ->
            let label = nonEmpty (field "label" e) |> Option.defaultValue ("New " + (Phrases.english.NodeKind s.AddKind).ToLowerInvariant())
            edit (AddNode(CoreNode(s.AddKind, None), label, None))
        | "delete" -> if s.Selection.IsEmpty then fail "Select something to delete." else edit (Delete s.Selection)
        | "auto-layout" -> edit AutoLayout
        | "connect-start" ->
            match selectedNodes s with
            | [ id ] when canEdit s -> announce "Choose the step to connect to." { s with Pending = Connecting { Node = id; Port = None } }, []
            | _ -> fail "Select one step to connect from."
        | "reconnect-start" ->
            match s.Selection, e.Arg with
            | [ EdgeRef id ], Some which when canEdit s ->
                let which = if which = "source" then SourceEnd else TargetEnd
                announce "Choose the step this end should connect to." { s with Pending = Reconnecting(id, which) }, []
            | _ -> fail "Select one connection to reconnect."
        | "cancel" ->
            match s.Pending with
            | NoPending -> select [] s
            | _ -> announce "Cancelled." { s with Pending = NoPending }, []
        | "connect-to" ->
            match selectedNodes s, e.Value |> Option.bind ObjectId.tryCreate with
            | [ source ], Some target -> edit (Connect({ Node = source; Port = nonEmpty (field "sourcePort" e) |> Option.bind LocalId.tryCreate }, { Node = target; Port = nonEmpty (field "targetPort" e) |> Option.bind LocalId.tryCreate }, None))
            | _ -> fail "Select one step and choose where to connect it."
        | "connect-form" ->
            match selectedNodes s, field "target" e |> Option.bind ObjectId.tryCreate with
            | [ source ], Some target ->
                edit (Connect({ Node = source; Port = nonEmpty (field "sourcePort" e) |> Option.bind LocalId.tryCreate }, { Node = target; Port = nonEmpty (field "targetPort" e) |> Option.bind LocalId.tryCreate }, None))
            | _ -> fail "Choose the step to connect to."
        | "gesture-move" ->
            match nodeOf e, pair e.Value with
            | Some id, Some(dx, dy) ->
                let scale = float s.Zoom / 100.0
                let ids = if List.contains (NodeRef id) s.Selection then selectedNodes s else [ id ]
                edit (Move(ids, dx / scale, dy / scale))
            | _ -> s, []
        | "gesture-resize" ->
            match nodeOf e, pair e.Value, w with
            | Some id, Some(dw, dh), Some w ->
                let r = Layout.resolve w
                match Map.tryFind id r.Nodes with
                | Some rect ->
                    let scale = float s.Zoom / 100.0
                    edit (Resize(id, rect.W + dw / scale, rect.H + dh / scale))
                | None -> s, []
            | _ -> s, []
        | "gesture-connect" ->
            match nodeOf e, e.Value |> Option.bind ObjectRef.tryParse with
            | Some source, Some(NodeRef target) when source <> target ->
                edit (Connect({ Node = source; Port = e.Arg |> Option.bind LocalId.tryCreate }, { Node = target; Port = None }, None))
            | _ -> s, []
        | "key" ->
            let step = if e.Shift then 32.0 else 8.0
            match e.Value, selectedNodes s with
            | Some("ArrowLeft" | "ArrowRight" | "ArrowUp" | "ArrowDown" as k), (_ :: _ as ids) when canEdit s ->
                let dx, dy =
                    match k with
                    | "ArrowLeft" -> -step, 0.0
                    | "ArrowRight" -> step, 0.0
                    | "ArrowUp" -> 0.0, -step
                    | _ -> 0.0, step
                edit (Move(ids, dx, dy))
            | Some("Delete" | "Backspace"), _ when canEdit s && not s.Selection.IsEmpty -> edit (Delete s.Selection)
            | Some "Escape", _ -> onUi { e with Name = "cancel" } s
            | _ -> s, []
        | "nudge" ->
            match e.Value, selectedNodes s with
            | Some dir, (_ :: _ as ids) ->
                let dx, dy = match dir with "left" -> -8.0, 0.0 | "right" -> 8.0, 0.0 | "up" -> 0.0, -8.0 | _ -> 0.0, 8.0
                edit (Move(ids, dx, dy))
            | _ -> s, []
        | "set-title" -> edit (SetTitle(e.Value |> Option.defaultValue ""))
        | "set-label" -> withRef (fun r -> edit (SetText(r, LabelText, e.Value)))
        | "set-description" -> withRef (fun r -> edit (SetText(r, DescriptionText, e.Value)))
        | "set-a11y-name" -> withRef (fun r -> edit (SetText(r, AccessibleNameText, e.Value)))
        | "set-a11y-description" -> withRef (fun r -> edit (SetText(r, AccessibleDescriptionText, e.Value)))
        | "set-kind" ->
            match nodeOf e, e.Value |> Option.bind (fun v -> Codec.nodeKinds |> List.tryFind (fst >> (=) v)) with
            | Some id, Some(_, k) -> edit (SetNodeKind(id, CoreNode(k, None)))
            | _ -> s, []
        | "set-shape" ->
            match nodeOf e with
            | Some id -> edit (SetShape(id, e.Value |> Option.bind (fun v -> Codec.shapes |> List.tryFind (fst >> (=) v)) |> Option.map snd))
            | None -> s, []
        | "set-color" ->
            withRef (fun r ->
                let prop =
                    match e.Arg with
                    | Some "stroke" -> StrokeColor
                    | Some "accent" -> AccentColor
                    | Some "foreground" -> ForegroundColor
                    | _ -> FillColor
                match parseColor (e.Value |> Option.defaultValue "") with
                | Ok c -> edit (SetColor(r, prop, c))
                | Error message -> fail message)
        | "set-status" ->
            withRef (fun r ->
                let current =
                    w
                    |> Option.bind (fun w ->
                        match r with
                        | NodeRef id -> w.Nodes |> List.tryFind (fun n -> n.Id = id) |> Option.bind _.Status
                        | EdgeRef id -> w.Edges |> List.tryFind (fun x -> x.Id = id) |> Option.bind _.Status
                        | GroupRef id -> w.Groups |> List.tryFind (fun g -> g.Id = id) |> Option.bind _.Status)
                let label = current |> Option.bind (fun st -> match st.State with CoreStatus(_, l) -> l | CustomStatus(_, l) -> Some l)
                edit (SetStatus(r, stateOf (e.Value |> Option.defaultValue "") label)))
        | "set-status-label" ->
            withRef (fun r ->
                let current =
                    w
                    |> Option.bind (fun w ->
                        match r with
                        | NodeRef id -> w.Nodes |> List.tryFind (fun n -> n.Id = id) |> Option.bind _.Status
                        | EdgeRef id -> w.Edges |> List.tryFind (fun x -> x.Id = id) |> Option.bind _.Status
                        | GroupRef id -> w.Groups |> List.tryFind (fun g -> g.Id = id) |> Option.bind _.Status)
                match current with
                | Some st ->
                    let label = nonEmpty e.Value
                    let state =
                        match st.State, label with
                        | CoreStatus(c, _), l -> CoreStatus(c, l)
                        | CustomStatus(q, _), Some l -> CustomStatus(q, l)
                        | CustomStatus(q, old), None -> CustomStatus(q, old)
                    edit (SetStatus(r, Some { st with State = state }))
                | None -> fail "Choose a status first.")
        | "set-geometry" ->
            match nodeOf e, e.Arg, e.Value |> Option.bind parseFloat, w with
            | Some id, Some axis, Some v, Some w ->
                let r = Layout.resolve w
                match Map.tryFind id r.Nodes with
                | Some rect ->
                    match axis with
                    | "x" -> edit (MoveTo(id, { X = v; Y = rect.Y }))
                    | "y" -> edit (MoveTo(id, { X = rect.X; Y = v }))
                    | "width" -> edit (Resize(id, v, rect.H))
                    | _ -> edit (Resize(id, rect.W, v))
                | None -> s, []
            | _ -> fail "Enter a number."
        | "edge-kind" ->
            match refOf e with
            | Some(EdgeRef id) -> edit (SetEdgeKind(id, e.Value |> Option.bind (fun v -> Codec.edgeKinds |> List.tryFind (fst >> (=) v)) |> Option.map (fun (_, k) -> CoreEdge(k, None))))
            | _ -> s, []
        | "edge-line" ->
            match refOf e with
            | Some(EdgeRef id) -> edit (SetEdgeLine(id, e.Value |> Option.bind (fun v -> Codec.lines |> List.tryFind (fst >> (=) v)) |> Option.map snd))
            | _ -> s, []
        | "edge-direction" ->
            match refOf e with
            | Some(EdgeRef id) ->
                let dir = match e.Value with Some "backward" -> Some Backward | Some "both" -> Some Both | Some "none" -> Some Undirected | Some "forward" -> Some Forward | _ -> None
                edit (SetEdgeDirection(id, dir))
            | _ -> s, []
        | "add-port" ->
            match nodeOf e, w with
            | Some id, Some w ->
                let node = w.Nodes |> List.find (fun n -> n.Id = id)
                let side = match field "side" e with Some "top" -> Some Top | Some "bottom" -> Some Bottom | Some "left" -> Some Left | Some "right" -> Some Right | _ -> None
                let dir = match field "direction" e with Some "in" -> Some In | Some "out" -> Some Out | Some "inout" -> Some InOut | _ -> None
                let label = nonEmpty (field "label" e)
                let stem = label |> Option.map (fun l -> String(l.ToLowerInvariant() |> Seq.map (fun c -> if Char.IsAsciiLetterOrDigit c then c else '-') |> Array.ofSeq).Trim('-')) |> Option.filter ((<>) "") |> Option.defaultValue "port"
                let port = { Id = Editing.freshLocalId stem (node.Ports |> List.map _.Id); Side = side; Direction = dir; Label = label; MaxConnections = None; Metadata = None; Extensions = [] }
                edit (AddPort(id, port))
            | _ -> s, []
        | "remove-port" ->
            match nodeOf e, e.Arg |> Option.bind LocalId.tryCreate with
            | Some id, Some p -> edit (RemovePort(id, p))
            | _ -> s, []
        | "add-group" ->
            let kind = match field "kind" e with Some "swimlane" -> Swimlane | Some "phase" -> Phase | Some "container" -> Container | _ -> PlainGroup
            match nonEmpty (field "label" e) with
            | Some label -> edit (AddGroup(kind, label, selectedNodes s))
            | None -> fail "Name the group."
        | "membership" ->
            match refOf e, e.Arg |> Option.bind ObjectId.tryCreate with
            | Some(GroupRef g), Some n -> edit (SetMembership(g, n, e.Value = Some "true"))
            | _ -> s, []
        | "add-metadata" ->
            withRef (fun r ->
                match nonEmpty (field "key" e) with
                | None -> fail "A metadata field needs a key."
                | Some key ->
                    match parseMetadata (field "type" e |> Option.defaultValue "text") (field "value" e |> Option.defaultValue "") with
                    | Ok v -> edit (SetMetadata(r, key, Some v))
                    | Error message -> fail message)
        | "set-metadata" ->
            withRef (fun r ->
                match e.Arg with
                | Some key ->
                    let kind = field "type" e |> Option.defaultValue "text"
                    match parseMetadata kind (e.Value |> Option.defaultValue "") with
                    | Ok v -> edit (SetMetadata(r, key, Some v))
                    | Error message -> fail message
                | None -> s, [])
        | "remove-metadata" -> withRef (fun r -> match e.Arg with Some key -> edit (SetMetadata(r, key, None)) | None -> s, [])
        | "add-reference" ->
            withRef (fun r ->
                match nonEmpty (field "system" e) |> Option.bind Namespace.tryCreate, nonEmpty (field "key" e) with
                | Some system, Some key ->
                    let existing =
                        w
                        |> Option.map (fun w ->
                            match r with
                            | NodeRef id -> w.Nodes |> List.tryFind (fun n -> n.Id = id) |> Option.map _.References
                            | EdgeRef id -> w.Edges |> List.tryFind (fun x -> x.Id = id) |> Option.map _.References
                            | GroupRef id -> w.Groups |> List.tryFind (fun g -> g.Id = id) |> Option.map _.References)
                        |> Option.flatten
                        |> Option.defaultValue []
                    let href = nonEmpty (field "href" e)
                    if href |> Option.exists (Validation.isSafeUrl >> not) then fail "Links must be http, https, mailto or relative."
                    else
                        let reference =
                            { Id = Editing.freshLocalId "ref" (existing |> List.map _.Id); System = system; Type = nonEmpty (field "type" e); Key = key
                              Label = nonEmpty (field "label" e); Href = href; Metadata = None }
                        edit (AddReference(r, reference))
                | None, _ -> fail "The system must be a namespace such as com.example.erp."
                | _, None -> fail "A reference needs a key.")
        | "remove-reference" -> withRef (fun r -> match e.Arg |> Option.bind LocalId.tryCreate with Some id -> edit (RemoveReference(r, id)) | None -> s, [])
        | "set-interaction" ->
            withRef (fun r ->
                let label = nonEmpty (field "label" e)
                let target () =
                    match field "targetKind" e, nonEmpty (field "target" e) with
                    | Some "url", Some u when Validation.isSafeUrl u -> Ok(ToUrl u)
                    | Some "url", Some _ -> Error "Links must be http, https, mailto or relative."
                    | Some "node", Some t -> ObjectId.tryCreate t |> Option.map ToNode |> Option.map Ok |> Option.defaultValue (Error "Unknown step.")
                    | Some "workflow", Some t -> WorkflowId.tryCreate t |> Option.map ToWorkflow |> Option.map Ok |> Option.defaultValue (Error "Workflow ids are lowercase words joined by hyphens.")
                    | Some "reference", Some t -> LocalId.tryCreate t |> Option.map ToReference |> Option.map Ok |> Option.defaultValue (Error "Unknown reference.")
                    | _ -> Error "Choose where it goes."
                let interaction =
                    match field "action" e with
                    | Some "" | None -> Ok None
                    | Some "select" -> Ok(Some(SelectIntent label))
                    | Some "inspect" -> Ok(Some(InspectIntent label))
                    | Some "navigate" -> target () |> Result.map (fun t -> Some(Navigate(t, label)))
                    | Some "open" -> target () |> Result.map (fun t -> Some(Open(t, label)))
                    | Some "command" ->
                        match nonEmpty (field "command" e) |> Option.bind QualifiedName.tryParse, label with
                        | Some q, Some l -> Ok(Some(Command(q, l, None)))
                        | None, _ -> Error "Commands are named <namespace>:<name>, such as com.example.app:approve."
                        | _, None -> Error "A command needs the text people see."
                    | Some _ -> Ok None
                match interaction with
                | Ok i -> edit (SetInteraction(r, i))
                | Error message -> fail message)
        | _ -> s, []

    // -- host messages -------------------------------------------------------------

    let update (msg: HostMessage) (s: EmbedState) : EmbedState * Emission list =
        match msg with
        | Init(mode, doc) ->
            let s = { s with Mode = mode }
            match doc with
            | Some text -> loadText text s
            | None -> s, []
        | Load text -> loadText text s
        | SetMode m -> announce $"{HostMode.text m} mode." { s with Mode = m; Pending = NoPending; Selection = (if m = ViewMode || m = RuntimeMode then [] else s.Selection) }, []
        | Execute cmd -> applyEdit cmd s
        | Undo -> history false s
        | Redo -> history true s
        | SelectObjects refs -> select refs s
        | FocusObject r -> { s with Focus = Some r }, [ FocusChanged(Some r) ]
        | SetRuntimeState states ->
            let runtime =
                states |> List.fold (fun (m: Map<ObjectId, Status>) (id, st) -> match st with Some x -> m.Add(id, x) | None -> m.Remove id) s.Runtime
            { s with Runtime = runtime }, []
        | Ui e -> onUi e s
