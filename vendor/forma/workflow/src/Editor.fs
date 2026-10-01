namespace Forma.Workflow

open System

/// An addressable workflow object.
type ObjectRef =
    | NodeRef of ObjectId
    | EdgeRef of ObjectId
    | GroupRef of ObjectId

[<RequireQualifiedAccess>]
module ObjectRef =
    let key (r: ObjectRef) =
        match r with
        | NodeRef id -> "node:" + id.Value
        | EdgeRef id -> "edge:" + id.Value
        | GroupRef id -> "group:" + id.Value

    let tryParse (s: string) =
        if isNull s then None
        else
            match s.IndexOf ':' with
            | -1 -> None
            | i ->
                let kind, raw = s.Substring(0, i), s.Substring(i + 1)
                ObjectId.tryCreate raw
                |> Option.bind (fun id ->
                    match kind with
                    | "node" -> Some(NodeRef id)
                    | "edge" -> Some(EdgeRef id)
                    | "group" -> Some(GroupRef id)
                    | _ -> None)

    let id (r: ObjectRef) = match r with NodeRef id | EdgeRef id | GroupRef id -> id

    let exists (w: Workflow) (r: ObjectRef) =
        match r with
        | NodeRef id -> w.Nodes |> List.exists (fun n -> n.Id = id)
        | EdgeRef id -> w.Edges |> List.exists (fun e -> e.Id = id)
        | GroupRef id -> w.Groups |> List.exists (fun g -> g.Id = id)

type TextField =
    | LabelText
    | DescriptionText
    | AccessibleNameText
    | AccessibleDescriptionText

type ColorProperty =
    | FillColor
    | StrokeColor
    | AccentColor
    | ForegroundColor

type EdgeEnd =
    | SourceEnd
    | TargetEnd

/// Every change to a workflow is one of these. Pointer, keyboard, touch,
/// inspector and host API input are adapters onto this one command surface.
type EditCommand =
    | AddNode of kind: NodeKind * label: string * at: Point option
    | Delete of ObjectRef list
    | Move of ObjectId list * dx: float * dy: float
    | MoveTo of ObjectId * Point
    | Resize of ObjectId * width: float * height: float
    | SetText of ObjectRef * TextField * string option
    | SetNodeKind of ObjectId * NodeKind
    | SetShape of ObjectId * Shape option
    | SetColor of ObjectRef * ColorProperty * ColorValue option
    | SetStatus of ObjectRef * Status option
    | Connect of Endpoint * Endpoint * EdgeKind option
    | Reconnect of ObjectId * EdgeEnd * Endpoint
    | SetEdgeKind of ObjectId * EdgeKind option
    | SetEdgeLine of ObjectId * LineStyle option
    | SetEdgeDirection of ObjectId * EdgeDirection option
    | AddPort of ObjectId * Port
    | RemovePort of ObjectId * LocalId
    | AddGroup of GroupKind * label: string * members: ObjectId list
    | SetMembership of group: ObjectId * node: ObjectId * isMember: bool
    | SetMetadata of ObjectRef * key: string * value: Json option
    | AddReference of ObjectRef * Reference
    | RemoveReference of ObjectRef * LocalId
    | SetInteraction of ObjectRef * Interaction option
    | SetTitle of string
    | AutoLayout
    /// The host replaces the document (for example after its own validated change).
    | ReplaceDocument of Workflow

/// A legal change that was applied.
type Change =
    { Command: EditCommand
      Description: string
      Before: Workflow
      After: Workflow
      Report: ValidationReport }

/// Why a command was refused. Nothing changes when a command is refused.
type Refusal =
    | NotPermittedInMode of HostMode
    | UnknownObject of string
    | WouldBreakWorkflow of Finding list
    | InvalidInput of string

/// The workflow editing core: legal transitions over a workflow with undo and
/// redo. Pure: every function returns a new value.
[<RequireQualifiedAccess>]
module Editing =
    let private errors (r: ValidationReport) = r.Findings |> List.filter (fun f -> f.Severity = Severity.Error)

    let private updateNode (id: ObjectId) (f: Node -> Node) (w: Workflow) =
        { w with Nodes = w.Nodes |> List.map (fun n -> if n.Id = id then f n else n) }

    let private updateEdge (id: ObjectId) (f: Edge -> Edge) (w: Workflow) =
        { w with Edges = w.Edges |> List.map (fun e -> if e.Id = id then f e else e) }

    let private updateGroup (id: ObjectId) (f: Group -> Group) (w: Workflow) =
        { w with Groups = w.Groups |> List.map (fun g -> if g.Id = id then f g else g) }

    let private allIds (w: Workflow) =
        (w.Nodes |> List.map _.Id) @ (w.Edges |> List.map _.Id) @ (w.Groups |> List.map _.Id) |> List.map _.Value |> Set.ofList

    /// The first unused "<stem>-<n>" id, deterministic for a given document.
    let freshId (stem: string) (w: Workflow) =
        let used = allIds w
        let clean = String(stem.ToLowerInvariant() |> Seq.map (fun c -> if Char.IsAsciiLetterOrDigit c then c else '-') |> Array.ofSeq).Trim('-')
        let stem = if clean = "" then "item" else clean
        Seq.initInfinite (fun i -> $"{stem}-{i + 1}") |> Seq.find (used.Contains >> not) |> ObjectId.create

    let freshLocalId (stem: string) (existing: LocalId list) =
        let used = existing |> List.map _.Value |> Set.ofList
        Seq.initInfinite (fun i -> if i = 0 then stem else $"{stem}-{i + 1}") |> Seq.find (used.Contains >> not) |> LocalId.create

    let private metadataOf (r: ObjectRef) (w: Workflow) =
        match r with
        | NodeRef id -> w.Nodes |> List.tryFind (fun n -> n.Id = id) |> Option.bind _.Metadata
        | EdgeRef id -> w.Edges |> List.tryFind (fun e -> e.Id = id) |> Option.bind _.Metadata
        | GroupRef id -> w.Groups |> List.tryFind (fun g -> g.Id = id) |> Option.bind _.Metadata

    let private setMeta (key: string) (value: Json option) (existing: Metadata option) : Metadata option =
        let current = existing |> Option.defaultValue []
        let updated =
            match value with
            | None -> current |> List.filter (fst >> (<>) key)
            | Some v when current |> List.exists (fst >> (=) key) -> current |> List.map (fun (k, old) -> if k = key then k, v else k, old)
            | Some v -> current @ [ key, v ]
        // Absence and an empty object are distinct in the file; keep what was there.
        match existing, updated with
        | None, [] -> None
        | _ -> Some updated

    let private positioned (w: Workflow) =
        if w.Nodes |> List.forall (fun n -> n.Layout.Position.IsSome) then w else Layout.apply FillMissing w

    let private round (v: float) = Math.Round(v)

    let nodeName (w: Workflow) (id: ObjectId) =
        w.Nodes |> List.tryFind (fun n -> n.Id = id) |> Option.map (fun n -> n.Label) |> Option.defaultValue id.Value

    let describe (w: Workflow) (cmd: EditCommand) =
        let name (r: ObjectRef) =
            match r with
            | NodeRef id -> nodeName w id
            | EdgeRef id -> w.Edges |> List.tryFind (fun e -> e.Id = id) |> Option.bind _.Label |> Option.defaultValue $"connection {id.Value}"
            | GroupRef id -> w.Groups |> List.tryFind (fun g -> g.Id = id) |> Option.map _.Label |> Option.defaultValue id.Value
        match cmd with
        | AddNode(_, label, _) -> $"Added {label}"
        | Delete refs -> "Deleted " + String.Join(", ", refs |> List.map name)
        | Move(ids, _, _) -> "Moved " + String.Join(", ", ids |> List.map (nodeName w))
        | MoveTo(id, _) -> $"Moved {nodeName w id}"
        | Resize(id, _, _) -> $"Resized {nodeName w id}"
        | SetText(r, LabelText, _) -> $"Renamed {name r}"
        | SetText(r, _, _) -> $"Changed text of {name r}"
        | SetNodeKind(id, _) -> $"Changed kind of {nodeName w id}"
        | SetShape(id, _) -> $"Changed shape of {nodeName w id}"
        | SetColor(r, _, _) -> $"Changed colour of {name r}"
        | SetStatus(r, _) -> $"Changed status of {name r}"
        | Connect(a, b, _) -> $"Connected {nodeName w a.Node} to {nodeName w b.Node}"
        | Reconnect(id, _, ep) -> $"Reconnected {name (EdgeRef id)} to {nodeName w ep.Node}"
        | SetEdgeKind(id, _) | SetEdgeLine(id, _) | SetEdgeDirection(id, _) -> $"Changed {name (EdgeRef id)}"
        | AddPort(id, _) | RemovePort(id, _) -> $"Changed ports of {nodeName w id}"
        | AddGroup(_, label, _) -> $"Added {label}"
        | SetMembership(g, n, true) -> $"Added {nodeName w n} to {name (GroupRef g)}"
        | SetMembership(g, n, false) -> $"Removed {nodeName w n} from {name (GroupRef g)}"
        | SetMetadata(r, key, Some _) -> $"Set {key} on {name r}"
        | SetMetadata(r, key, None) -> $"Removed {key} from {name r}"
        | AddReference(r, _) | RemoveReference(r, _) -> $"Changed references of {name r}"
        | SetInteraction(r, _) -> $"Changed interaction of {name r}"
        | SetTitle t -> $"Renamed the workflow to {t}"
        | AutoLayout -> "Arranged the workflow automatically"
        | ReplaceDocument _ -> "Replaced the workflow"

    /// Applies a command without validation. Unknown targets are refused here.
    let rec private transform (cmd: EditCommand) (w: Workflow) : Result<Workflow, Refusal> =
        let nodeExists id = w.Nodes |> List.exists (fun n -> n.Id = id)
        let edgeExists id = w.Edges |> List.exists (fun e -> e.Id = id)
        let groupExists id = w.Groups |> List.exists (fun g -> g.Id = id)
        let needNode (id: ObjectId) f = if nodeExists id then Ok(f ()) else Error(UnknownObject id.Value)
        let needEdge (id: ObjectId) f = if edgeExists id then Ok(f ()) else Error(UnknownObject id.Value)
        let needRef (r: ObjectRef) f = if ObjectRef.exists w r then Ok(f ()) else Error(UnknownObject(ObjectRef.key r))
        match cmd with
        | AddNode(kind, label, at) ->
            if String.IsNullOrWhiteSpace label then Error(InvalidInput "A step needs a name.")
            else
                let stem = match kind with CoreNode(k, _) -> (Codec.nodeKindText (CoreNode(k, None))) | CustomNode(q, _) -> q.Name
                let id = freshId stem w
                let placed = positioned w
                let position =
                    match at with
                    | Some p -> p
                    | None ->
                        let r = Layout.resolve placed
                        let bottom = if r.Nodes.IsEmpty then Layout.margin else (r.Nodes |> Map.toList |> List.map (fun (_, x) -> x.Bottom) |> List.max) + 32.0
                        { X = Layout.margin; Y = bottom }
                let node =
                    { Id = id; Kind = kind; Label = label.Trim(); Description = None; Variant = None; Color = ObjectColor.none; Status = None
                      Ports = []; References = []; Metadata = None; Accessibility = Accessibility.none; Interaction = None
                      Layout = { BoxLayout.empty with Position = Some { X = round position.X; Y = round position.Y } }; Extensions = [] }
                Ok { placed with Nodes = placed.Nodes @ [ node ]; Layout = { placed.Layout with Canvas = None } }
        | Delete refs ->
            match refs |> List.tryFind (ObjectRef.exists w >> not) with
            | Some missing -> Error(UnknownObject(ObjectRef.key missing))
            | None ->
                let nodes = refs |> List.choose (function NodeRef id -> Some id | _ -> None) |> Set.ofList
                let edges = refs |> List.choose (function EdgeRef id -> Some id | _ -> None) |> Set.ofList
                let groups = refs |> List.choose (function GroupRef id -> Some id | _ -> None) |> Set.ofList
                Ok
                    { w with
                        Nodes = w.Nodes |> List.filter (fun n -> not (nodes.Contains n.Id))
                        // Removing a node removes the connections that would otherwise dangle.
                        Edges = w.Edges |> List.filter (fun e -> not (edges.Contains e.Id) && not (nodes.Contains e.Source.Node) && not (nodes.Contains e.Target.Node))
                        Groups =
                            w.Groups
                            |> List.filter (fun g -> not (groups.Contains g.Id))
                            |> List.map (fun g ->
                                { g with
                                    Members = g.Members |> List.filter (nodes.Contains >> not)
                                    Parent = g.Parent |> Option.filter (groups.Contains >> not) }) }
        | Move(ids, dx, dy) ->
            match ids |> List.tryFind (nodeExists >> not) with
            | Some missing -> Error(UnknownObject missing.Value)
            | None ->
                let placed = positioned w
                let set = Set.ofList ids
                Ok
                    { placed with
                        Nodes =
                            placed.Nodes
                            |> List.map (fun n ->
                                if set.Contains n.Id then
                                    let p = n.Layout.Position |> Option.defaultValue { X = 0.0; Y = 0.0 }
                                    { n with Layout = { n.Layout with Position = Some { X = round (p.X + dx); Y = round (p.Y + dy) } } }
                                else n)
                        Layout = { placed.Layout with Mode = Some Authored; Canvas = None } }
        | MoveTo(id, p) ->
            needNode id (fun () ->
                let placed = positioned w
                updateNode id (fun n -> { n with Layout = { n.Layout with Position = Some { X = round p.X; Y = round p.Y } } }) placed
                |> fun x -> { x with Layout = { x.Layout with Mode = Some Authored; Canvas = None } })
        | Resize(id, width, height) ->
            if width < 40.0 || height < 32.0 || width > 4000.0 || height > 4000.0 then Error(InvalidInput "Width must be 40 to 4000 and height 32 to 4000.")
            else needNode id (fun () -> updateNode id (fun n -> { n with Layout = { n.Layout with Width = Some(round width); Height = Some(round height) } }) (positioned w) |> fun x -> { x with Layout = { x.Layout with Canvas = None } })
        | SetText(r, field, value) ->
            let value = value |> Option.map (fun s -> s.Trim()) |> Option.filter (String.IsNullOrEmpty >> not)
            match field, value, r with
            | LabelText, None, (NodeRef _ | GroupRef _) -> Error(InvalidInput "A name cannot be empty.")
            | _ ->
                needRef r (fun () ->
                    let a11y (a: Accessibility) =
                        match field with
                        | AccessibleNameText -> { a with Name = value }
                        | AccessibleDescriptionText -> { a with Description = value }
                        | _ -> a
                    match r with
                    | NodeRef id ->
                        updateNode id (fun n ->
                            match field with
                            | LabelText -> { n with Label = value.Value }
                            | DescriptionText -> { n with Description = value }
                            | _ -> { n with Accessibility = a11y n.Accessibility }) w
                    | EdgeRef id ->
                        updateEdge id (fun e ->
                            match field with
                            | LabelText -> { e with Label = value }
                            | DescriptionText -> { e with Description = value }
                            | _ -> { e with Accessibility = a11y e.Accessibility }) w
                    | GroupRef id ->
                        updateGroup id (fun g ->
                            match field with
                            | LabelText -> { g with Label = value.Value }
                            | DescriptionText -> { g with Description = value }
                            | _ -> { g with Accessibility = a11y g.Accessibility }) w)
        | SetNodeKind(id, kind) -> needNode id (fun () -> updateNode id (fun n -> { n with Kind = kind }) w)
        | SetShape(id, shape) -> needNode id (fun () -> updateNode id (fun n -> { n with Variant = shape }) w)
        | SetColor(r, prop, value) ->
            let apply (c: ObjectColor) =
                match prop with
                | FillColor -> { c with Fill = value }
                | StrokeColor -> { c with Stroke = value }
                | AccentColor -> { c with Accent = value }
                | ForegroundColor -> { c with Foreground = value }
            needRef r (fun () ->
                match r with
                | NodeRef id -> updateNode id (fun n -> { n with Color = apply n.Color }) w
                | GroupRef id -> updateGroup id (fun g -> { g with Color = apply g.Color }) w
                | EdgeRef id -> updateEdge id (fun e -> { e with Stroke = value }) w)
        | SetStatus(r, status) ->
            needRef r (fun () ->
                match r with
                | NodeRef id -> updateNode id (fun n -> { n with Status = status }) w
                | EdgeRef id -> updateEdge id (fun e -> { e with Status = status }) w
                | GroupRef id -> updateGroup id (fun g -> { g with Status = status }) w)
        | Connect(source, target, kind) ->
            if not (nodeExists source.Node) then Error(UnknownObject source.Node.Value)
            elif not (nodeExists target.Node) then Error(UnknownObject target.Node.Value)
            else
                let id = freshId "edge" w
                let edge =
                    { Id = id; Source = source; Target = target; Direction = None; Kind = kind; Label = None; Description = None; Line = None
                      Width = None; Stroke = None; Status = None; References = []; Metadata = None; Accessibility = Accessibility.none
                      Interaction = None; Layout = EdgeLayout.empty; Extensions = [] }
                Ok { w with Edges = w.Edges @ [ edge ] }
        | Reconnect(id, which, ep) ->
            if not (nodeExists ep.Node) then Error(UnknownObject ep.Node.Value)
            else
                needEdge id (fun () ->
                    updateEdge id (fun e ->
                        let e = { e with Layout = { e.Layout with Waypoints = []; LabelAt = None } }
                        match which with
                        | SourceEnd -> { e with Source = ep }
                        | TargetEnd -> { e with Target = ep }) w)
        | SetEdgeKind(id, kind) -> needEdge id (fun () -> updateEdge id (fun e -> { e with Kind = kind }) w)
        | SetEdgeLine(id, line) -> needEdge id (fun () -> updateEdge id (fun e -> { e with Line = line }) w)
        | SetEdgeDirection(id, dir) -> needEdge id (fun () -> updateEdge id (fun e -> { e with Direction = dir }) w)
        | AddPort(id, port) -> needNode id (fun () -> updateNode id (fun n -> { n with Ports = n.Ports @ [ port ] }) w)
        | RemovePort(id, portId) ->
            if w.Edges |> List.exists (fun e -> (e.Source.Node = id && e.Source.Port = Some portId) || (e.Target.Node = id && e.Target.Port = Some portId)) then
                Error(InvalidInput "Disconnect the connections that use this port first.")
            else needNode id (fun () -> updateNode id (fun n -> { n with Ports = n.Ports |> List.filter (fun p -> p.Id <> portId) }) w)
        | AddGroup(kind, label, members) ->
            if String.IsNullOrWhiteSpace label then Error(InvalidInput "A group needs a name.")
            else
                match members |> List.tryFind (nodeExists >> not) with
                | Some missing -> Error(UnknownObject missing.Value)
                | None ->
                    let stem = match kind with Swimlane -> "lane" | Phase -> "phase" | Container -> "container" | PlainGroup -> "group"
                    let group =
                        { Id = freshId stem w; Kind = kind; Label = label.Trim(); Description = None; Members = members; Parent = None; Line = None
                          Color = ObjectColor.none; Status = None; References = []; Metadata = None; Accessibility = Accessibility.none
                          Interaction = None; Layout = BoxLayout.empty; Extensions = [] }
                    Ok { w with Groups = w.Groups @ [ group ] }
        | SetMembership(groupId, nodeId, isMember) ->
            if not (groupExists groupId) then Error(UnknownObject groupId.Value)
            elif not (nodeExists nodeId) then Error(UnknownObject nodeId.Value)
            else
                Ok(
                    updateGroup
                        groupId
                        (fun g ->
                            let without = g.Members |> List.filter ((<>) nodeId)
                            { g with Members = (if isMember then without @ [ nodeId ] else without); Layout = { g.Layout with Position = None; Width = None; Height = None } })
                        w
                )
        | SetMetadata(r, key, value) ->
            if String.IsNullOrWhiteSpace key then Error(InvalidInput "A metadata field needs a key.")
            else
                needRef r (fun () ->
                    match r with
                    | NodeRef id -> updateNode id (fun n -> { n with Metadata = setMeta key value n.Metadata }) w
                    | EdgeRef id -> updateEdge id (fun e -> { e with Metadata = setMeta key value e.Metadata }) w
                    | GroupRef id -> updateGroup id (fun g -> { g with Metadata = setMeta key value g.Metadata }) w)
        | AddReference(r, reference) ->
            needRef r (fun () ->
                match r with
                | NodeRef id -> updateNode id (fun n -> { n with References = n.References @ [ reference ] }) w
                | EdgeRef id -> updateEdge id (fun e -> { e with References = e.References @ [ reference ] }) w
                | GroupRef id -> updateGroup id (fun g -> { g with References = g.References @ [ reference ] }) w)
        | RemoveReference(r, refId) ->
            needRef r (fun () ->
                let drop (rs: Reference list) = rs |> List.filter (fun x -> x.Id <> refId)
                match r with
                | NodeRef id -> updateNode id (fun n -> { n with References = drop n.References }) w
                | EdgeRef id -> updateEdge id (fun e -> { e with References = drop e.References }) w
                | GroupRef id -> updateGroup id (fun g -> { g with References = drop g.References }) w)
        | SetInteraction(r, interaction) ->
            needRef r (fun () ->
                match r with
                | NodeRef id -> updateNode id (fun n -> { n with Interaction = interaction }) w
                | EdgeRef id -> updateEdge id (fun e -> { e with Interaction = interaction }) w
                | GroupRef id -> updateGroup id (fun g -> { g with Interaction = interaction }) w)
        | SetTitle title ->
            if String.IsNullOrWhiteSpace title then Error(InvalidInput "The workflow needs a title.") else Ok { w with Title = title.Trim() }
        | AutoLayout -> Ok(Layout.apply Recompute w)
        | ReplaceDocument replacement -> Ok replacement

    /// Applies a command if it is legal: the target exists and the result has
    /// no validation error the document did not already have. A refused
    /// command changes nothing.
    let apply (cmd: EditCommand) (w: Workflow) : Result<Change, Refusal> =
        transform cmd w
        |> Result.bind (fun after ->
            let before = Validation.check w
            let report = Validation.check after
            let key (f: Finding) = f.Code, f.Subject, f.Message
            let existing = errors before |> List.map key |> Set.ofList
            let introduced = errors report |> List.filter (fun f -> not (existing.Contains(key f)))
            match cmd, introduced with
            | ReplaceDocument _, _ when not (Validation.isValid report) && report.Workflow.IsNone -> Error(WouldBreakWorkflow(errors report))
            | ReplaceDocument _, _ -> Ok { Command = cmd; Description = describe w cmd; Before = w; After = after; Report = report }
            | _, [] -> Ok { Command = cmd; Description = describe w cmd; Before = w; After = after; Report = report }
            | _, found -> Error(WouldBreakWorkflow found))

    let refusalText (r: Refusal) =
        match r with
        | NotPermittedInMode m -> $"Editing is not available in {HostMode.text m} mode."
        | UnknownObject id -> $"\"{id}\" no longer exists."
        | WouldBreakWorkflow findings -> "Not changed: " + String.Join(" ", findings |> List.map _.Message)
        | InvalidInput message -> message

/// Undo/redo history of workflow states. Each entry is one logical change.
type History =
    { Past: Workflow list
      Future: Workflow list
      Limit: int }

[<RequireQualifiedAccess>]
module History =
    let empty = { Past = []; Future = []; Limit = 200 }

    let record (before: Workflow) (h: History) =
        { h with Past = before :: h.Past |> List.truncate h.Limit; Future = [] }

    let undo (current: Workflow) (h: History) =
        match h.Past with
        | previous :: rest -> Some(previous, { h with Past = rest; Future = current :: h.Future })
        | [] -> None

    let redo (current: Workflow) (h: History) =
        match h.Future with
        | next :: rest -> Some(next, { h with Past = current :: h.Past; Future = rest })
        | [] -> None
