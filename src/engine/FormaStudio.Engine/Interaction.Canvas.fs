namespace FormaStudio.Engine

open Interaction

/// Canvas interaction: selection, connecting, adding, labelling, deleting,
/// movement and resize gestures, reconnection, zoom, snapping and arrangement.
[<RequireQualifiedAccess>]
module internal CanvasInteraction =
    /// One resize intent (a finished handle drag or one inspector edit) is one
    /// ResizeNode command; the engine validates and rounds the size.
    let private resize (state: EditorState) (node: NodeId) (size: Size -> float * float) =
        match ProjectOps.tryNode state.Diagram node (EditorState.project state) with
        | Some n ->
            let w, h = size n.Box.Size
            run (Flow(ResizeNode(state.Diagram, node, w, h))) (sprintf "Resized %s." n.Label) state |> select [ NodeRef(state.Diagram, node) ]
        | None -> status "Select an item to resize it." state

    let private edgeEndOf =
        function
        | "source" -> Some SourceEnd
        | "target" -> Some TargetEnd
        | _ -> None

    /// Moves one end of a connector to another node: one Reconnect command,
    /// whether it came from an endpoint drag or the keyboard path (FDA-067).
    let private reconnect (state: EditorState) (edgeId: EdgeId) (edgeEnd: EdgeEnd) (node: NodeId) =
        match EditorState.diagram state |> Option.bind (fun d -> d.Edges |> List.tryFind (fun e -> e.Id = edgeId)) with
        | Some edge ->
            run (EditorIntents.reconnect state.Diagram edge (edgeEnd = SourceEnd) node) "Reconnected." { state with Pending = NoPending }
            |> select [ EdgeRef(state.Diagram, edgeId) ]
        | None -> { state with Pending = NoPending; Status = "Select a connector to reconnect it." }

    let private move (state: EditorState) (dx: float) (dy: float) =
        match EditorState.selectedNode state with
        | Some node -> run (Flow(MoveNodes(state.Diagram, [ node.Id, dx, dy ]))) (sprintf "Moved %s." node.Label) state
        | None -> status "Select an item to move it." state

    /// Align and distribute are derived edits: one MoveNodes command, so they
    /// undo as one step (FDA-125).
    let private arrange (state: EditorState) arrangement =
        let nodes = EditorState.selectedNodes state
        match EditorIntents.arrange arrangement nodes with
        | [], _ -> status "Select more items to arrange them." state
        | _, [] -> status "Already arranged." state
        | _, changed -> run (Flow(MoveNodes(state.Diagram, changed))) (sprintf "Arranged %d items." (List.length nodes)) state

    let private zoom (state: EditorState) inward =
        let next =
            if inward then EditorState.zoomLevels |> List.tryFind (fun z -> z > state.Zoom)
            else EditorState.zoomLevels |> List.rev |> List.tryFind (fun z -> z < state.Zoom)
        match next with
        | Some z -> { state with Zoom = z; Status = sprintf "Zoom %d%%." z }
        | None -> status (sprintf "Zoom is already %d%%." state.Zoom) state

    let private setSize (state: EditorState) (value: string) width =
        match EditorState.selectedNode state, parseNumber value with
        | Some node, Some v -> resize state node.Id (fun size -> if width then v, float size.Height else float size.Width, v)
        | Some _, None -> status "Not changed: enter a number." state
        | None, _ -> status "Select an item to resize it." state

    let private selectKey (state: EditorState) (key: string) =
        match refOfKey state key, state.Pending with
        | Some(NodeRef(_, target)), Connecting source ->
            let edgeId, command = EditorIntents.connect (EditorState.profile state) (EditorState.diagram state) state.Diagram source target
            run command "Connected." { state with Pending = NoPending } |> select [ EdgeRef(state.Diagram, edgeId) ]
        | Some(NodeRef(_, target)), Reconnecting(edge, edgeEnd) -> reconnect state edge edgeEnd target
        | Some reference, _ -> { state with Session = Editor.select [ reference ] state.Session; Pending = NoPending; Status = "Selected." }
        | None, _ -> status "Nothing to select." state

    let update (event: CanvasEvent) (args: EventArgs) (state: EditorState) : EditorState =
        let key, value = args.Key, args.Value
        match event with
        | CanvasEvent.Select -> selectKey state key
        | CanvasEvent.ClearSelection -> { state with Session = Editor.select [] state.Session; Pending = NoPending; Status = "Selection cleared." }
        | CanvasEvent.ToggleSelect ->
            match refOfKey state key with
            | Some reference ->
                let current = state.Session.Selection
                let next = if List.contains reference current then current |> List.filter ((<>) reference) else current @ [ reference ]
                { state with Session = Editor.select next state.Session; Pending = NoPending; Status = sprintf "%d selected." next.Length }
            | None -> state
        | CanvasEvent.ConnectStart ->
            match EditorState.selectedNode state with
            | Some node -> { state with Pending = Connecting node.Id; Status = sprintf "Choose the item that %s connects to." node.Label }
            | None -> status "Select the item to connect from." state
        | CanvasEvent.Cancel -> { state with Pending = NoPending; Status = "Cancelled." }
        | CanvasEvent.ChooseKind -> { state with AddKind = key }
        | CanvasEvent.AddNode ->
            let nodeId, command, label = EditorIntents.addNode (EditorState.profile state) (EditorState.diagram state) state.Diagram state.AddKind
            run command (sprintf "Added %s." label) state |> select [ NodeRef(state.Diagram, nodeId) ]
        | CanvasEvent.SetLabel ->
            match EditorState.selectedRef state with
            | Some(NodeRef(_, n)) -> run (Flow(SetNodeLabel(state.Diagram, n, value))) "Label changed." state
            | Some(EdgeRef(_, edge)) -> run (Flow(SetEdgeLabel(state.Diagram, edge, Some value))) "Label changed." state
            | _ -> status "Select an item to label it." state
        | CanvasEvent.Delete ->
            match EditorState.selectedRef state with
            | Some(NodeRef(_, n)) -> run (Flow(RemoveNode(state.Diagram, n, RemoveIncidentEdges))) "Deleted." state
            | Some(EdgeRef(_, edge)) -> run (Flow(RemoveEdge(state.Diagram, edge))) "Deleted." state
            | _ -> status "Select an item to delete it." state
        | CanvasEvent.MoveLeft -> move state -EditorIntents.gridStep 0.0
        | CanvasEvent.MoveRight -> move state EditorIntents.gridStep 0.0
        | CanvasEvent.MoveUp -> move state 0.0 -EditorIntents.gridStep
        | CanvasEvent.MoveDown -> move state 0.0 EditorIntents.gridStep
        | CanvasEvent.GestureMove ->
            // One completed drag (or one arrow-key nudge from the gesture adapter)
            // becomes exactly one command and one history entry.
            match parseDelta value with
            | Some(node, dx, dy) ->
                let existing = ProjectOps.tryNode state.Diagram node (EditorState.project state)
                let label = existing |> Option.map _.Label |> Option.defaultValue "item"
                let dx, dy =
                    existing
                    |> Option.map (fun n -> EditorIntents.snapDelta state.Snap n.Box.Position.X dx, EditorIntents.snapDelta state.Snap n.Box.Position.Y dy)
                    |> Option.defaultValue (dx, dy)
                run (Flow(MoveNodes(state.Diagram, [ node, dx, dy ]))) (sprintf "Moved %s." label) state |> select [ NodeRef(state.Diagram, node) ]
            | None -> status "Ignored an unreadable move." state
        | CanvasEvent.GestureResize ->
            match parseDelta value with
            | Some(node, w, h) -> resize state node (fun _ -> EditorIntents.snapSize state.Snap w, EditorIntents.snapSize state.Snap h)
            | None -> status "Ignored an unreadable resize." state
        | CanvasEvent.SetWidth -> setSize state value true
        | CanvasEvent.SetHeight -> setSize state value false
        | CanvasEvent.ReconnectEnd ->
            match EditorState.selectedRef state, edgeEndOf key with
            | Some(EdgeRef(_, edge)), Some edgeEnd ->
                let which = match edgeEnd with SourceEnd -> "start" | TargetEnd -> "end"
                { state with Pending = Reconnecting(edge, edgeEnd); Status = sprintf "Choose the item this connector should %s at." which }
            | _ -> status "Select a connector to reconnect it." state
        | CanvasEvent.GestureReconnect ->
            match EditorState.selectedRef state, value.Split('|') with
            | Some(EdgeRef(_, edge)), [| endKey; nodeKey |] ->
                match edgeEndOf endKey, Id.create<NodeKind> nodeKey with
                | Some edgeEnd, Ok node -> reconnect state edge edgeEnd node
                | _ -> status "Ignored an unreadable reconnect." state
            | _ -> status "Select a connector to reconnect it." state
        | CanvasEvent.ZoomIn -> zoom state true
        | CanvasEvent.ZoomOut -> zoom state false
        | CanvasEvent.ZoomReset -> { state with Zoom = 100; Status = "Zoom 100%." }
        | CanvasEvent.ToggleSnap ->
            let on = not state.Snap
            { state with Snap = on; Status = (if on then "Snapping to the 8-unit grid." else "Snapping off.") }
        | CanvasEvent.AlignLeft -> arrange state AlignLeft
        | CanvasEvent.AlignTop -> arrange state AlignTop
        | CanvasEvent.DistributeHorizontally -> arrange state DistributeHorizontally
