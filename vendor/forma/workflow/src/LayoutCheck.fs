namespace Forma.Workflow

open System

/// A layout constraint the resolved geometry does not satisfy.
type LayoutIssue =
    { Code: string
      Severity: Severity
      Subject: string
      Message: string }

/// Static layout validation (LCE-001): what can be known before a browser
/// renders. It works on the resolved layout and the text estimate, so it
/// finds authored geometry that cannot hold its content, overlapping nodes,
/// connectors through nodes and colliding labels. It does not replace the
/// rendered-geometry check (tests/layout): fonts and user styles decide the
/// final text size, and only a browser measures it.
[<RequireQualifiedAccess>]
module LayoutCheck =
    let private issue code severity (subject: string) message =
        { Code = code; Severity = severity; Subject = subject; Message = message }

    let private rect (r: Rect) = $"{r.X:F0},{r.Y:F0} {r.W:F0}x{r.H:F0}"

    /// Authored sizes too small for the content in the node's shape.
    let private sizes (w: Workflow) =
        w.Nodes
        |> List.filter (fun n -> n.Layout.Width.IsSome || n.Layout.Height.IsSome)
        |> List.choose (fun n ->
            let shape = Layout.shapeOf n
            let width = n.Layout.Width |> Option.defaultValue 200.0
            let required = Layout.requiredHeight n width
            let id = n.Id.Value
            if Layout.contentWidth shape width < 16.0 then
                Some(issue "layout.node.too-narrow" Severity.Error id $"Node {id} is {width:F0} wide; its {Codec.shapeText shape} shape leaves no room for text.")
            else
                match shape, n.Layout.Height with
                | Diamond, _ when required > width + 0.5 ->
                    Some(issue "layout.node.too-small" Severity.Error id $"Node {id}: a {width:F0}px diamond holds its content only at {required:F0}px or larger (content sits in the inscribed square).")
                | Diamond, _ -> None
                | _, Some h when h < required - 0.5 ->
                    Some(issue "layout.node.too-small" Severity.Error id $"Node {id}: its content needs about {required:F0}px of height at {width:F0}px wide in a {Codec.shapeText shape} shape; it is given {h:F0}px, so it would render taller than its place in the layout.")
                | _ -> None)

    let private pairs xs = [ for i, a in List.indexed xs do for b in List.skip (i + 1) xs -> a, b ]

    /// Every static layout issue for a workflow, deterministic in document order.
    let check (w: Workflow) : LayoutIssue list =
        let layout = Layout.resolve w
        let nodes = w.Nodes |> List.choose (fun n -> Map.tryFind n.Id layout.Nodes |> Option.map (fun r -> n.Id, r))
        let overlaps =
            pairs nodes
            |> List.filter (fun ((_, a), (_, b)) -> LayoutGeometry.overlapArea a b > 1.0)
            |> List.map (fun ((ia, a), (ib, b)) -> issue "layout.node.overlap" Severity.Error ia.Value $"Nodes {ia.Value} ({rect a}) and {ib.Value} ({rect b}) overlap.")
        let crossings =
            w.Edges
            |> List.collect (fun e ->
                match Map.tryFind e.Id layout.Edges with
                | None -> []
                | Some route ->
                    nodes
                    |> List.filter (fun (id, _) -> id <> e.Source.Node && id <> e.Target.Node)
                    |> List.filter (fun (_, r) -> route.Points |> List.pairwise |> List.exists (fun (p, q) -> LayoutGeometry.segmentCrosses (LayoutGeometry.insetRect (LayoutGeometry.even 2.0) r) p q))
                    |> List.map (fun (id, _) -> issue "layout.connector.crosses-node" Severity.Warning e.Id.Value $"Connector {e.Id.Value} passes through node {id.Value}."))
        let labels =
            w.Edges
            |> List.choose (fun e ->
                match e.Label, Map.tryFind e.Id layout.Edges with
                | Some l, Some r -> let lw, lh = Layout.labelSize l in Some(e.Id, { X = r.LabelAt.X; Y = r.LabelAt.Y; W = lw; H = lh })
                | _ -> None)
        let labelNode =
            labels
            |> List.collect (fun (eid, lr) ->
                nodes
                |> List.filter (fun (_, r) -> LayoutGeometry.intersects lr r)
                |> List.map (fun (nid, _) -> issue "layout.label.overlap" Severity.Warning eid.Value $"The label of {eid.Value} overlaps node {nid.Value}."))
        let labelLabel =
            pairs labels
            |> List.filter (fun ((_, a), (_, b)) -> LayoutGeometry.intersects a b)
            |> List.map (fun ((a, _), (b, _)) -> issue "layout.label.overlap" Severity.Warning a.Value $"The labels of {a.Value} and {b.Value} overlap.")
        let canvas =
            match w.Layout.Canvas with
            | Some(cw, ch) ->
                let right = layout.Nodes |> Map.toList |> List.map (snd >> _.Right) |> List.fold max 0.0
                let bottom = layout.Nodes |> Map.toList |> List.map (snd >> _.Bottom) |> List.fold max 0.0
                if right > cw || bottom > ch then [ issue "layout.canvas.too-small" Severity.Warning w.Id.Value $"The authored canvas {cw:F0}x{ch:F0} is smaller than its nodes' extent {right:F0}x{bottom:F0}." ] else []
            | None -> []
        sizes w @ overlaps @ crossings @ labelNode @ labelLabel @ canvas
