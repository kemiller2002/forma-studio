namespace Forma.Workflow

open System

/// Connector routing and label placement (LCE-001), split from Layout.resolve
/// along its seam: routes avoid nodes they do not connect, and labels are
/// placed where they collide with nothing.
[<RequireQualifiedAccess>]
module internal LayoutRouting =

    /// Routes every edge between placed nodes and ports; returns the routes and the label boxes.
    let routes (horizontal: bool) (nodes: Map<ObjectId, Rect>) (ports: Map<ObjectId * LocalId, Point * PortSide>) (w: Workflow) =
        let anchor (r: Rect) (other: Rect) (outgoing: bool) =
            // Leave/enter on the side facing the other node.
            let dx, dy = other.CenterX - r.CenterX, other.CenterY - r.CenterY
            if horizontal then
                if abs dx >= abs dy * 0.5 && abs dx > 1.0 then (if dx > 0.0 then { X = r.Right; Y = r.CenterY }, Right else { X = r.X; Y = r.CenterY }, Left)
                elif dy > 0.0 then { X = r.CenterX; Y = r.Bottom }, Bottom
                else { X = r.CenterX; Y = r.Y }, Top
            else if abs dy >= abs dx * 0.5 && abs dy > 1.0 then (if dy > 0.0 then { X = r.CenterX; Y = r.Bottom }, Bottom else { X = r.CenterX; Y = r.Y }, Top)
            elif dx > 0.0 then { X = r.Right; Y = r.CenterY }, Right
            else { X = r.X; Y = r.CenterY }, Left
            |> fun (p, side) -> ignore outgoing; p, side
        let stub (p: Point) side =
            match side with
            | Top -> { X = p.X; Y = p.Y - 12.0 }
            | Bottom -> { X = p.X; Y = p.Y + 12.0 }
            | Left -> { X = p.X - 12.0; Y = p.Y }
            | Right -> { X = p.X + 12.0; Y = p.Y }
        let horizontalSide side = side = Left || side = Right
        /// Interior crossings of a polyline with nodes other than its endpoints.
        let crossings (exclude: Set<ObjectId>) (pts: Point list) =
            nodes
            |> Map.toList
            |> List.filter (fun (id, _) -> not (exclude.Contains id))
            |> List.sumBy (fun (_, r) ->
                let inner = LayoutGeometry.insetRect (LayoutGeometry.even 2.0) r
                pts |> List.pairwise |> List.filter (fun (p, q) -> LayoutGeometry.segmentCrosses inner p q) |> List.length)
        /// Elbows between two stubs. A path never runs back against the side it
        /// leaves from; backward edges detour around both boxes. A forward
        /// connector picks the first channel that passes through no other node:
        /// the midpoint, beside the source, beside the target, then a lane
        /// outside every node between them (LCE-001).
        let orthogonal (exclude: Set<ObjectId>) (s: Rect) (t: Rect) (a: Point) sside (b: Point) tside =
            let forwardX side (p: Point) (q: Point) = (side = Right && q.X >= p.X) || (side = Left && q.X <= p.X)
            let forwardY side (p: Point) (q: Point) = (side = Bottom && q.Y >= p.Y) || (side = Top && q.Y <= p.Y)
            let choose (candidates: Point list list) =
                let full = candidates |> List.map (fun c -> c, crossings exclude ([ a ] @ c @ [ b ]))
                match full |> List.tryFind (fun (_, n) -> n = 0) with
                | Some(c, _) -> c
                | None -> full |> List.minBy snd |> fst
            let between lo hi (along: Rect -> float * float) =
                nodes |> Map.toList |> List.filter (fun (id, r) -> not (exclude.Contains id) && (let s0, s1 = along r in s1 > lo && s0 < hi)) |> List.map snd
            match horizontalSide sside, horizontalSide tside with
            | true, true ->
                if forwardX sside a b then
                    let mx = (a.X + b.X) / 2.0
                    let near = if b.X >= a.X then 12.0 else -12.0
                    let via x = [ { X = x; Y = a.Y }; { X = x; Y = b.Y } ]
                    let lanes =
                        let lo, hi = min a.X b.X, max a.X b.X
                        match between lo hi (fun r -> r.X, r.Right) with
                        | [] -> []
                        | obstacles ->
                            [ (obstacles |> List.map _.Y |> List.min) - 16.0; (obstacles |> List.map _.Bottom |> List.max) + 16.0 ]
                            |> List.map (fun y -> [ { X = a.X + near; Y = a.Y }; { X = a.X + near; Y = y }; { X = b.X - near; Y = y }; { X = b.X - near; Y = b.Y } ])
                    choose ([ via mx; via (a.X + near); via (b.X - near) ] @ lanes)
                else
                    let detour = (max s.Bottom t.Bottom) + 24.0
                    [ { X = a.X; Y = detour }; { X = b.X; Y = detour } ]
            | false, false ->
                if forwardY sside a b then
                    let my = (a.Y + b.Y) / 2.0
                    let near = if b.Y >= a.Y then 12.0 else -12.0
                    let via y = [ { X = a.X; Y = y }; { X = b.X; Y = y } ]
                    let lanes =
                        let lo, hi = min a.Y b.Y, max a.Y b.Y
                        match between lo hi (fun r -> r.Y, r.Bottom) with
                        | [] -> []
                        | obstacles ->
                            [ (obstacles |> List.map _.X |> List.min) - 16.0; (obstacles |> List.map _.Right |> List.max) + 16.0 ]
                            |> List.map (fun x -> [ { X = a.X; Y = a.Y + near }; { X = x; Y = a.Y + near }; { X = x; Y = b.Y - near }; { X = b.X; Y = b.Y - near } ])
                    choose ([ via my; via (a.Y + near); via (b.Y - near) ] @ lanes)
                else
                    let detour = (max s.Right t.Right) + 24.0
                    [ { X = detour; Y = a.Y }; { X = detour; Y = b.Y } ]
            | false, true ->
                // Leave vertically, arrive horizontally.
                if forwardY sside a b then [ { X = a.X; Y = b.Y } ] else [ { X = b.X; Y = a.Y } ]
            | true, false ->
                if forwardX sside a b then [ { X = b.X; Y = a.Y } ] else [ { X = a.X; Y = b.Y } ]
        let route (e: Edge) =
            match Map.tryFind e.Source.Node nodes, Map.tryFind e.Target.Node nodes with
            | Some s, Some t ->
                let endpoint (ep: Endpoint) (r: Rect) (other: Rect) outgoing =
                    match ep.Port |> Option.bind (fun p -> Map.tryFind (ep.Node, p) ports) with
                    | Some(pt, side) -> pt, side
                    | None -> anchor r other outgoing
                let sp, sside = endpoint e.Source s t true
                let tp, tside = endpoint e.Target t s false
                // A connector that runs against the flow (a loop back to an earlier
                // step) travels outside every node instead of through them.
                let againstFlow =
                    e.Target.Port.IsNone && e.Layout.Waypoints.IsEmpty && e.Source.Node <> e.Target.Node
                    && (if horizontal then t.Right < s.X else t.Bottom < s.Y)
                let points =
                    if againstFlow then
                        let all = nodes |> Map.toList |> List.map snd
                        let start = if e.Source.Port.IsSome then stub sp sside else sp
                        if horizontal then
                            let goAbove = e.Source.Port.IsSome && sside = Top
                            let lane = if goAbove then (all |> List.map _.Y |> List.min) - 24.0 else (all |> List.map _.Bottom |> List.max) + 24.0
                            let from = if e.Source.Port.IsSome then sp else (if goAbove then { X = s.CenterX; Y = s.Y } else { X = s.CenterX; Y = s.Bottom })
                            let enter = if goAbove then { X = t.CenterX; Y = t.Y } else { X = t.CenterX; Y = t.Bottom }
                            [ from; start; { X = start.X; Y = lane }; { X = enter.X; Y = lane }; enter ]
                        else
                            let goLeft = e.Source.Port.IsSome && sside = Left
                            let lane = if goLeft then (all |> List.map _.X |> List.min) - 24.0 else (all |> List.map _.Right |> List.max) + 24.0
                            let from = if e.Source.Port.IsSome then sp else (if goLeft then { X = s.X; Y = s.CenterY } else { X = s.Right; Y = s.CenterY })
                            let enter = if goLeft then { X = t.X; Y = t.CenterY } else { X = t.Right; Y = t.CenterY }
                            [ from; start; { X = lane; Y = start.Y }; { X = lane; Y = enter.Y }; enter ]
                    elif e.Source.Node = e.Target.Node then
                        // Self-loop: out the top-right corner and back.
                        [ { X = s.Right; Y = s.CenterY }; { X = s.Right + 24.0; Y = s.CenterY }; { X = s.Right + 24.0; Y = s.Y - 24.0 }
                          { X = s.CenterX; Y = s.Y - 24.0 }; { X = s.CenterX; Y = s.Y } ]
                    elif not e.Layout.Waypoints.IsEmpty then [ sp ] @ e.Layout.Waypoints @ [ tp ]
                    else
                        match e.Layout.Routing with
                        | Some Straight -> [ sp; tp ]
                        | _ ->
                            let a, b = stub sp sside, stub tp tside
                            [ sp; a ] @ orthogonal (set [ e.Source.Node; e.Target.Node ]) s t a sside b tside @ [ b; tp ]
                let points = points |> List.map (fun p -> { X = Math.Round(p.X, 1); Y = Math.Round(p.Y, 1) })
                // Drop consecutive duplicates and collinear interior points.
                let dedup =
                    points
                    |> List.fold (fun acc p -> match acc with last :: _ when last = p -> acc | _ -> p :: acc) []
                    |> List.rev
                let simplified =
                    match dedup with
                    | first :: rest when rest.Length >= 2 ->
                        let arr = List.toArray dedup
                        [ yield first
                          for i in 1 .. arr.Length - 2 do
                              let a, b, c = arr[i - 1], arr[i], arr[i + 1]
                              let collinear = (a.X = b.X && b.X = c.X) || (a.Y = b.Y && b.Y = c.Y)
                              if not collinear then yield b
                          yield arr[arr.Length - 1] ]
                    | _ -> dedup
                let labelAt =
                    match e.Layout.LabelAt with
                    | Some p -> p
                    | None ->
                        let arr = List.toArray simplified
                        let segs = [ for i in 0 .. arr.Length - 2 -> arr[i], arr[i + 1] ]
                        let len (a: Point, b: Point) = abs (b.X - a.X) + abs (b.Y - a.Y)
                        let longest = segs |> List.maxBy len
                        let a, b = longest
                        { X = Math.Round((a.X + b.X) / 2.0); Y = Math.Round((a.Y + b.Y) / 2.0) }
                Some(e.Id, { Points = simplified; LabelAt = labelAt })
            | _ -> None
        let routed = w.Edges |> List.choose route
        // Labels are placed one at a time, in document order, at the first
        // point along their connector (segment midpoints, longest first, then
        // quarter points) whose box overlaps no node and no label already
        // placed: iterative collision resolution (LCE-001). The label's
        // top-left corner is stored, so a label is centred on its point.
        let nodeBoxes = nodes |> Map.toList |> List.map (snd >> LayoutGeometry.insetRect (LayoutGeometry.even -4.0))
        let placeLabels (placed: Rect list, acc: (ObjectId * Route) list) (e: Edge, (id: ObjectId, r: Route)) =
            match e.Label, e.Layout.LabelAt with
            | Some label, None ->
                let lw, lh = LayoutSizing.labelSize label
                let segs = r.Points |> List.pairwise |> List.sortByDescending (fun (a, b) -> abs (b.X - a.X) + abs (b.Y - a.Y))
                let at (a: Point, b: Point) t = { X = a.X + (b.X - a.X) * t; Y = a.Y + (b.Y - a.Y) * t }
                let candidates = [ for t in [ 0.5; 0.25; 0.75 ] do for seg in segs -> at seg t ]
                let box (c: Point) = { X = Math.Round(c.X - lw / 2.0); Y = Math.Round(c.Y - lh / 2.0); W = lw; H = lh }
                let free (b: Rect) = not (nodeBoxes @ placed |> List.exists (LayoutGeometry.intersects b))
                let chosen = candidates |> List.map box |> List.tryFind free |> Option.defaultWith (fun () -> box candidates.Head)
                chosen :: placed, (id, { r with LabelAt = { X = chosen.X; Y = chosen.Y } }) :: acc
            | _ -> placed, (id, r) :: acc
        let labelled = w.Edges |> List.filter (fun e -> routed |> List.exists (fst >> (=) e.Id))
        let edges = List.zip labelled routed |> List.fold placeLabels ([], []) |> snd |> Map.ofList
        let labelBoxes =
            w.Edges
            |> List.choose (fun e ->
                match e.Label, Map.tryFind e.Id edges with
                | Some l, Some r -> Some { X = r.LabelAt.X; Y = r.LabelAt.Y; W = fst (LayoutSizing.labelSize l); H = snd (LayoutSizing.labelSize l) }
                | _ -> None)
        edges, labelBoxes
