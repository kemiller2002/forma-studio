namespace Forma.Workflow

open System

/// Deterministic default layout. The same document always produces the same
/// geometry: ordering derives only from document order, requested rank/order
/// and the graph, never from hashing or time.
[<RequireQualifiedAccess>]
module Layout =
    let margin = 24.0
    let private baseRankGap = 96.0
    let private itemGap = 32.0
    let private groupPad = 16.0
    let private groupHeader = 32.0

    let clearance = LayoutSizing.clearance
    let labelTextStyle = LayoutSizing.labelTextStyle
    let contentHeight = LayoutSizing.contentHeight
    let contentWidth = LayoutSizing.contentWidth
    let estimateHeight = LayoutSizing.estimateHeight
    let shapeOf = LayoutSizing.shapeOf
    let requiredHeight = LayoutSizing.requiredHeight
    let labelMaxWidth = LayoutSizing.labelMaxWidth
    let labelSize = LayoutSizing.labelSize

    let isForward (e: Edge) = e.Direction <> Some Backward

    /// Ranks by longest path over forward edges. Cycles are broken by ignoring
    /// edges that close a cycle in document-order depth-first traversal.
    let private ranks (w: Workflow) =
        let ids = w.Nodes |> List.map _.Id
        let known = Set.ofList ids
        let edges =
            w.Edges
            |> List.filter (fun e -> known.Contains e.Source.Node && known.Contains e.Target.Node && e.Source.Node <> e.Target.Node)
            |> List.map (fun e -> if isForward e then e.Source.Node, e.Target.Node else e.Target.Node, e.Source.Node)
        let succ = edges |> List.groupBy fst |> List.map (fun (k, v) -> k, v |> List.map snd) |> Map.ofList
        // Depth-first in document order marks back edges.
        let rec visit (node: ObjectId) (state: Map<ObjectId, int>, back: Set<ObjectId * ObjectId>) =
            let state = state.Add(node, 1)
            let state, back =
                succ
                |> Map.tryFind node
                |> Option.defaultValue []
                |> List.fold
                    (fun (st: Map<ObjectId, int>, bk: Set<ObjectId * ObjectId>) next ->
                        match Map.tryFind next st with
                        | Some 1 -> st, bk.Add(node, next)
                        | Some _ -> st, bk
                        | None -> visit next (st, bk))
                    (state, back)
            state.Add(node, 2), back
        let _, back = ids |> List.fold (fun (st: Map<ObjectId, int>, bk) id -> if st.ContainsKey id then st, bk else visit id (st, bk)) (Map.empty, Set.empty)
        let dag = edges |> List.filter (fun pair -> not (back.Contains pair)) |> List.distinct
        let preds = dag |> List.groupBy snd |> List.map (fun (k, v) -> k, v |> List.map fst) |> Map.ofList
        let requested = w.Nodes |> List.choose (fun n -> n.Layout.Rank |> Option.map (fun r -> n.Id, r)) |> Map.ofList
        // Longest path, memoised, in document order.
        let rec rankOf (memo: Map<ObjectId, int>) (id: ObjectId) =
            match Map.tryFind id memo with
            | Some r -> r, memo
            | None ->
                let ps = Map.tryFind id preds |> Option.defaultValue []
                let best, memo = ps |> List.fold (fun (b, m) p -> let r, m = rankOf m p in max b (r + 1), m) (0, memo)
                let r = max best (Map.tryFind id requested |> Option.defaultValue 0)
                r, memo.Add(id, r)
        ids |> List.fold (fun (m: Map<ObjectId, int>) id -> snd (rankOf m id)) Map.empty, dag

    /// Swimlanes (and phases when there are no swimlanes) in document order, with their members.
    let lanes (w: Workflow) =
        let lanes = w.Groups |> List.filter (fun g -> g.Kind = Swimlane)
        lanes

    let private laneOf (w: Workflow) (id: ObjectId) =
        lanes w |> List.tryFindIndex (fun g -> List.contains id g.Members)

    /// Computes geometry for every node, group, port and edge.
    let resolve (w: Workflow) : ResolvedLayout =
        let direction = w.Layout.Direction |> Option.defaultValue FlowRight
        let horizontal = direction = FlowRight
        let rankMap, dag = ranks w
        // A connector label sits in the gap between ranks, so the gap is wide
        // enough for the widest label plus a stub on each side (LCE-001).
        let labelExtent =
            w.Edges
            |> List.choose (fun e -> e.Label)
            |> List.map (fun l -> if horizontal then labelSize l |> fst else labelSize l |> snd)
            |> List.fold max 0.0
        let rankGap = max baseRankGap (Math.Ceiling(labelExtent + 48.0))
        let docIndex = w.Nodes |> List.mapi (fun i n -> n.Id, i) |> Map.ofList
        let groupById = w.Groups |> List.map (fun g -> g.Id, g) |> Map.ofList
        let groupIndex = w.Groups |> List.mapi (fun i g -> g.Id, i) |> Map.ofList
        // Bands: swimlanes, groups and containers become nested bands across the
        // flow axis, so members stay together and boundaries never overlap.
        // Phases cut across bands and are drawn around their members.
        let banded (g: Group) = g.Kind <> Phase
        let rec ancestry (seen: Set<ObjectId>) (g: Group) : ObjectId list =
            match g.Parent |> Option.bind (fun p -> Map.tryFind p groupById) with
            | Some parent when banded parent && not (seen.Contains parent.Id) -> ancestry (seen.Add parent.Id) parent @ [ g.Id ]
            | _ -> [ g.Id ]
        let bandPath (n: Node) : ObjectId list =
            let containing = w.Groups |> List.filter (fun g -> banded g && List.contains n.Id g.Members)
            match containing with
            | [] -> []
            | _ ->
                let paths = containing |> List.map (ancestry Set.empty)
                // The deepest chain wins; ties go to the earlier group in the document.
                let chain = paths |> List.sortBy (fun p -> -p.Length, groupIndex[List.last p]) |> List.head
                let lane = containing |> List.tryFind (fun g -> g.Kind = Swimlane)
                match lane with
                | Some l when not (List.contains l.Id chain) -> l.Id :: chain
                | _ -> chain
        let paths = w.Nodes |> List.map (fun n -> n.Id, bandPath n) |> Map.ofList
        // Order within a cell (band, rank): requested order, barycenter of predecessors, document order.
        let preds = dag |> List.groupBy snd |> List.map (fun (k, v) -> k, v |> List.map fst) |> Map.ofList
        let cells = w.Nodes |> List.groupBy (fun n -> paths[n.Id], Map.find n.Id rankMap)
        let firstPass =
            cells
            |> List.collect (fun (_, ns) -> ns |> List.sortBy (fun n -> n.Layout.Order |> Option.defaultValue Int32.MaxValue, docIndex[n.Id]) |> List.mapi (fun i n -> n.Id, float i))
            |> Map.ofList
        let ordered =
            cells
            |> List.map (fun (key, ns) ->
                let bary (n: Node) =
                    match Map.tryFind n.Id preds with
                    | Some ps when not ps.IsEmpty -> ps |> List.averageBy (fun p -> Map.find p firstPass)
                    | _ -> Map.find n.Id firstPass
                key, ns |> List.sortBy (fun n -> n.Layout.Order |> Option.defaultValue Int32.MaxValue, bary n, docIndex[n.Id]))
            |> Map.ofList
        let sizes = w.Nodes |> List.map (fun n -> n.Id, LayoutSizing.sizeOf n) |> Map.ofList
        let maxRank = if rankMap.IsEmpty then 0 else rankMap |> Map.toList |> List.map snd |> List.max
        let alongOf (id: ObjectId) = let (nw, nh) = sizes[id] in if horizontal then nw else nh
        let acrossOf (id: ObjectId) = let (nw, nh) = sizes[id] in if horizontal then nh else nw
        // Extent of each rank along the flow axis.
        let rankExtent r =
            w.Nodes |> List.filter (fun n -> rankMap[n.Id] = r) |> List.map (fun n -> alongOf n.Id) |> function [] -> 0.0 | xs -> List.max xs
        let rankStart =
            [ 0..maxRank ]
            |> List.fold (fun (acc: float list) r -> match acc with [] -> [ 0.0 ] | last :: _ -> (last + rankExtent (r - 1) + rankGap) :: acc) []
            |> List.rev
            |> List.toArray
        // The band tree, children in group document order.
        let allPaths = paths |> Map.toList |> List.map snd |> List.distinct
        let childBands (path: ObjectId list) =
            allPaths
            |> List.filter (fun p -> p.Length > path.Length && List.take path.Length p = path)
            |> List.map (fun p -> List.take (path.Length + 1) p)
            |> List.distinct
            |> List.sortBy (fun p -> groupIndex[List.last p])
        // Across the flow, a group band reserves its header (horizontal flow) and padding.
        let bandLead (path: ObjectId list) = if path.IsEmpty then 0.0 elif horizontal then groupHeader + groupPad else groupPad
        let bandTrail (path: ObjectId list) = if path.IsEmpty then 0.0 else groupPad
        let looseExtent (path: ObjectId list) =
            [ 0..maxRank ]
            |> List.map (fun r ->
                match Map.tryFind (path, r) ordered with
                | Some ns -> (ns |> List.sumBy (fun n -> acrossOf n.Id + itemGap)) - itemGap
                | None -> 0.0)
            |> List.max
        let rec extent (path: ObjectId list) : float =
            let loose = looseExtent path
            let children = childBands path |> List.map extent
            let sections = (if loose > 0.0 then [ loose ] else []) @ children
            let body = if sections.IsEmpty then 48.0 else List.sum sections + itemGap * float (sections.Length - 1)
            bandLead path + body + bandTrail path
        // Across-axis start of every band, depth first.
        let rec placeBand (path: ObjectId list) (start: float) : (ObjectId list * float) list =
            let loose = looseExtent path
            let first = start + bandLead path + (if loose > 0.0 then loose + itemGap else 0.0)
            let _, placed =
                childBands path
                |> List.fold (fun (cursor, acc) child -> cursor + extent child + itemGap, acc @ placeBand child cursor) (first, [])
            (path, start) :: placed
        let bandStart = placeBand [] 0.0 |> Map.ofList
        // Down flow puts group headers along the axis; reserve room before the first rank.
        let alongLead = if horizontal then 0.0 else (if w.Groups |> List.exists banded then groupHeader + groupPad else 0.0)
        let place (n: Node) =
            let path = paths[n.Id]
            let rank = rankMap[n.Id]
            let siblings = ordered[(path, rank)]
            let before = siblings |> List.takeWhile (fun s -> s.Id <> n.Id)
            let across = bandStart[path] + bandLead path + (before |> List.sumBy (fun s -> acrossOf s.Id + itemGap))
            let along = alongLead + rankStart[rank] + (rankExtent rank - alongOf n.Id) / 2.0
            let (nw, nh) = sizes[n.Id]
            let x, y = if horizontal then along, across else across, along
            { X = Math.Round x; Y = Math.Round y; W = nw; H = nh }
        let placedNodes =
            w.Nodes
            |> List.map (fun n ->
                match n.Layout.Position with
                | Some p -> n.Id, ({ X = p.X; Y = p.Y; W = fst sizes[n.Id]; H = snd sizes[n.Id] }, true)
                | None -> n.Id, (place n, false))
        let bounds (rects: Rect list) =
            match rects with
            | [] -> None
            | _ ->
                let x = rects |> List.map _.X |> List.min
                let y = rects |> List.map _.Y |> List.min
                Some { X = x; Y = y; W = (rects |> List.map _.Right |> List.max) - x; H = (rects |> List.map _.Bottom |> List.max) - y }
        let computed = placedNodes |> List.filter (snd >> snd >> not) |> List.map (snd >> fst)
        let alongSpan =
            match bounds computed with
            | Some b -> if horizontal then b.X, b.Right else b.Y, b.Bottom
            | None -> 0.0, 200.0
        let rec groupRect (seen: Set<ObjectId>) (g: Group) : Rect option =
            match g.Layout.Position, g.Layout.Width, g.Layout.Height with
            | Some p, Some gw, Some gh -> Some { X = p.X; Y = p.Y; W = gw; H = gh }
            | _ ->
                let path = allPaths |> List.tryPick (fun p -> List.tryFindIndex ((=) g.Id) p |> Option.map (fun i -> List.take (i + 1) p))
                let memberRects = g.Members |> List.choose (fun m -> placedNodes |> List.tryFind (fst >> (=) m) |> Option.map (snd >> fst))
                let childRects =
                    w.Groups
                    |> List.filter (fun c -> c.Parent = Some g.Id && not (seen.Contains c.Id))
                    |> List.choose (fun c -> groupRect (seen.Add c.Id) c)
                match path with
                | Some p when banded g && bandStart.ContainsKey p ->
                    let a0 = bandStart[p]
                    let a1 = a0 + extent p
                    let l0, l1 =
                        if g.Kind = Swimlane then fst alongSpan - groupPad, snd alongSpan + groupPad
                        else
                            match bounds (memberRects @ childRects) with
                            | Some b -> (if horizontal then b.X, b.Right else b.Y, b.Bottom) |> fun (s, e) -> s - groupPad, e + groupPad
                            | None -> fst alongSpan, snd alongSpan
                    let l0 = if horizontal then l0 else l0 - groupHeader
                    Some(if horizontal then { X = l0; Y = a0; W = l1 - l0; H = a1 - a0 } else { X = a0; Y = l0; W = a1 - a0; H = l1 - l0 })
                | _ ->
                    bounds (memberRects @ childRects)
                    |> Option.map (fun b -> { X = b.X - groupPad; Y = b.Y - groupPad - groupHeader; W = b.W + 2.0 * groupPad; H = b.H + 2.0 * groupPad + groupHeader })
        let rawGroups = w.Groups |> List.choose (fun g -> groupRect (Set.singleton g.Id) g |> Option.map (fun r -> g.Id, r))
        // Translate computed geometry so it starts at the margin. Authored
        // geometry is never moved.
        let shiftX, shiftY =
            let computedGroups = rawGroups |> List.filter (fun (id, _) -> (groupById[id]).Layout.Position.IsNone) |> List.map snd
            match bounds (computed @ computedGroups) with
            | Some b -> margin - b.X, margin - b.Y
            | None -> 0.0, 0.0
        let shift (r: Rect) = { r with X = r.X + shiftX; Y = r.Y + shiftY }
        let nodes = placedNodes |> List.map (fun (id, (r, authored)) -> id, (if authored then r else shift r)) |> Map.ofList
        let groups = rawGroups |> List.map (fun (id, r) -> id, (if (groupById[id]).Layout.Position.IsSome then r else shift r)) |> Map.ofList
        // Ports: evenly spaced along their side.
        let defaultSide (p: Port) =
            match p.Direction, horizontal with
            | Some In, true -> Left
            | Some In, false -> Top
            | Some Out, true -> Right
            | Some Out, false -> Bottom
            | _, _ -> Bottom
        let ports =
            w.Nodes
            |> List.collect (fun n ->
                match Map.tryFind n.Id nodes with
                | None -> []
                | Some r ->
                    n.Ports
                    |> List.groupBy (fun p -> p.Side |> Option.defaultValue (defaultSide p))
                    |> List.collect (fun (side, ps) ->
                        let count = float ps.Length
                        ps
                        |> List.mapi (fun i p ->
                            let t = (float i + 1.0) / (count + 1.0)
                            let pt =
                                match side with
                                | Top -> { X = r.X + r.W * t; Y = r.Y }
                                | Bottom -> { X = r.X + r.W * t; Y = r.Bottom }
                                | Left -> { X = r.X; Y = r.Y + r.H * t }
                                | Right -> { X = r.Right; Y = r.Y + r.H * t }
                            (n.Id, p.Id), (pt, side))))
            |> Map.ofList
        let edges, labelBoxes = LayoutRouting.routes horizontal nodes ports w
        let everything =
            (nodes |> Map.toList |> List.map snd) @ (groups |> Map.toList |> List.map snd)
            @ (edges |> Map.toList |> List.collect (fun (_, r) -> r.Points |> List.map (fun p -> { X = p.X; Y = p.Y; W = 1.0; H = 1.0 })))
            @ labelBoxes
        // A label near the first rank can extend before the margin; an
        // automatic layout moves right/down so nothing sits at a negative
        // canvas coordinate, where the viewport could never scroll to it.
        let anyAuthored = placedNodes |> List.exists (snd >> snd) || w.Groups |> List.exists (fun g -> g.Layout.Position.IsSome)
        let dx = if anyAuthored then 0.0 else max 0.0 (margin - (labelBoxes |> List.map _.X |> List.fold min margin))
        let dy = if anyAuthored then 0.0 else max 0.0 (margin - (labelBoxes |> List.map _.Y |> List.fold min margin))
        let moveP (p: Point) = { X = p.X + dx; Y = p.Y + dy }
        let moveR (r: Rect) = { r with X = r.X + dx; Y = r.Y + dy }
        let nodes, groups, ports, edges, everything =
            if dx = 0.0 && dy = 0.0 then nodes, groups, ports, edges, everything
            else
                nodes |> Map.map (fun _ r -> moveR r),
                groups |> Map.map (fun _ r -> moveR r),
                ports |> Map.map (fun _ (p, side) -> moveP p, side),
                edges |> Map.map (fun _ r -> { Points = r.Points |> List.map moveP; LabelAt = moveP r.LabelAt }),
                everything |> List.map moveR
        let canvas =
            match w.Layout.Canvas with
            | Some c -> c
            | None ->
                match bounds everything with
                | Some b -> Math.Ceiling(max 0.0 b.Right + margin), Math.Ceiling(max 0.0 b.Bottom + margin)
                | None -> 240.0, 120.0
        { Nodes = nodes; Groups = groups; Ports = ports; Edges = edges; Canvas = canvas; Direction = direction }

    /// Writes resolved geometry into layout fields only. Ids, semantics,
    /// metadata, references and extensions are untouched by construction: only
    /// `Layout` record fields are replaced.
    let apply (request: LayoutRequest) (w: Workflow) : Workflow =
        let source =
            match request with
            | FillMissing -> w
            | Recompute ->
                { w with
                    Nodes = w.Nodes |> List.map (fun n -> { n with Layout = { n.Layout with Position = None; Width = None; Height = None } })
                    Groups = w.Groups |> List.map (fun g -> { g with Layout = { g.Layout with Position = None; Width = None; Height = None } })
                    Edges = w.Edges |> List.map (fun e -> { e with Layout = EdgeLayout.empty })
                    Layout = { w.Layout with Canvas = None } }
        let r = resolve source
        { source with
            Nodes =
                source.Nodes
                |> List.map (fun n ->
                    match Map.tryFind n.Id r.Nodes with
                    | Some rect -> { n with Layout = { n.Layout with Position = Some { X = rect.X; Y = rect.Y }; Width = Some rect.W; Height = Some rect.H } }
                    | None -> n)
            Groups =
                source.Groups
                |> List.map (fun g ->
                    match Map.tryFind g.Id r.Groups with
                    | Some rect -> { g with Layout = { g.Layout with Position = Some { X = rect.X; Y = rect.Y }; Width = Some rect.W; Height = Some rect.H } }
                    | None -> g)
            Layout =
                { source.Layout with
                    Mode = Some Authored
                    Direction = Some r.Direction
                    Canvas = Some r.Canvas } }

    /// The workflow with every layout field removed: its pure semantics.
    let strip (w: Workflow) : Workflow =
        { w with
            Nodes = w.Nodes |> List.map (fun n -> { n with Layout = BoxLayout.empty })
            Edges = w.Edges |> List.map (fun e -> { e with Layout = EdgeLayout.empty })
            Groups = w.Groups |> List.map (fun g -> { g with Layout = BoxLayout.empty })
            Layout = WorkflowLayout.empty }
