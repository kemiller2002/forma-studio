namespace Forma.Workflow

open System

/// Decoding from schema-valid JSON into the typed model, and canonical
/// encoding back. Metadata, extension values and parameters are carried as JSON
/// and never interpreted, so they round-trip unchanged.
[<RequireQualifiedAccess>]
module Codec =
    open CodecRead

    // -- tables and decoding (CodecRead, CodecDecode) ---------------------------

    let tokens = CodecRead.tokens
    let shapes = CodecRead.shapes
    let lines = CodecRead.lines
    let nodeKinds = CodecRead.nodeKinds
    let edgeKinds = CodecRead.edgeKinds
    let groupKinds = CodecRead.groupKinds
    let states = CodecRead.states
    let colorSource where json = CodecRead.colorSource where json
    let colorValue where json = CodecRead.colorValue where json

    /// Decodes a schema-valid document. Callers normally go through
    /// `Validation.load`, which runs the schema first so these errors are rare.
    let decode (json: Json) : Result<Workflow, string> = CodecDecode.decode json

    // -- encoding --------------------------------------------------------------

    let private props (members: (string * Json option) list) =
        Json.Object(members |> List.choose (fun (k, v) -> v |> Option.map (fun x -> k, x)))

    let private s (v: string option) = v |> Option.map Json.String
    let private some (v: Json) = Some v
    let private whenAny (members: (string * Json option) list) =
        if members |> List.exists (snd >> Option.isSome) then Some(props members) else None

    let colorSourceText (c: ColorSource) =
        match c with
        | TokenSource t -> reverse tokens t
        | LiteralSource h -> h.Value

    let colorValueText (c: ColorValue) =
        match c with
        | TokenColor t -> reverse tokens t
        | LiteralColor h -> h.Value
        | PaletteColor slot -> "palette:" + slot.Value

    let private encodeColor (c: ObjectColor) =
        let v = Option.map (colorValueText >> Json.String)
        whenAny [ "fill", v c.Fill; "stroke", v c.Stroke; "accent", v c.Accent; "foreground", v c.Foreground ]

    let private encodeMeta (m: Metadata option) = m |> Option.map Json.Object

    let private encodeExtensions (e: Extensions) =
        if e.IsEmpty then None else Some(Json.Object(e |> List.map (fun (n, members) -> n.Value, Json.Object members)))

    let private encodeA11y (a: Accessibility) = whenAny [ "name", s a.Name; "description", s a.Description ]

    let stateText (st: State) =
        match st with
        | CoreStatus(c, _) -> reverse states c
        | CustomStatus(q, _) -> q.Value

    let private encodeStatus (st: Status option) =
        st
        |> Option.map (fun st ->
            let label = match st.State with CoreStatus(_, l) -> l | CustomStatus(_, l) -> Some l
            props [ "state", some (Json.String(stateText st.State)); "label", s label; "detail", s st.Detail; "updatedAt", s st.UpdatedAt ])

    let private encodeReferences (refs: Reference list) =
        if refs.IsEmpty then None
        else
            refs
            |> List.map (fun r ->
                props
                    [ "id", some (Json.String r.Id.Value); "system", some (Json.String r.System.Value); "type", s r.Type
                      "key", some (Json.String r.Key); "label", s r.Label; "href", s r.Href; "metadata", encodeMeta r.Metadata ])
            |> Json.Array
            |> Some

    let private encodeTarget (t: InteractionTarget) =
        match t with
        | ToWorkflow w -> "workflow", Json.String w.Value
        | ToNode n -> "node", Json.String n.Value
        | ToEdge e -> "edge", Json.String e.Value
        | ToGroup g -> "group", Json.String g.Value
        | ToReference r -> "reference", Json.String r.Value
        | ToUrl u -> "url", Json.String u
        |> fun (k, v) -> Json.Object [ k, v ]

    let private encodeInteraction (i: Interaction option) =
        i
        |> Option.map (fun i ->
            let action, target, command, label, parameters =
                match i with
                | NoInteraction -> "none", None, None, None, None
                | SelectIntent l -> "select", None, None, l, None
                | InspectIntent l -> "inspect", None, None, l, None
                | Navigate(t, l) -> "navigate", Some(encodeTarget t), None, l, None
                | Open(t, l) -> "open", Some(encodeTarget t), None, l, None
                | Command(q, l, p) -> "command", None, Some(Json.String q.Value), Some l, p |> Option.map Json.Object
            props [ "action", some (Json.String action); "target", target; "command", command; "label", s label; "parameters", parameters ])

    let private encodePoint (p: Point) = Json.Object [ "x", Json.number p.X; "y", Json.number p.Y ]

    let private encodeBox (l: BoxLayout) =
        whenAny
            [ "x", l.Position |> Option.map (fun p -> Json.number p.X)
              "y", l.Position |> Option.map (fun p -> Json.number p.Y)
              "width", l.Width |> Option.map Json.number
              "height", l.Height |> Option.map Json.number
              "rank", l.Rank |> Option.map (float >> Json.number)
              "order", l.Order |> Option.map (float >> Json.number)
              "size", l.Size |> Option.map (reverse sizes >> Json.String) ]

    let private encodeEdgeLayout (l: EdgeLayout) =
        whenAny
            [ "routing", l.Routing |> Option.map (reverse routings >> Json.String)
              "waypoints", (if l.Waypoints.IsEmpty then None else Some(Json.Array(l.Waypoints |> List.map encodePoint)))
              "label", l.LabelAt |> Option.map encodePoint ]

    let private encodePort (p: Port) =
        props
            [ "id", some (Json.String p.Id.Value)
              "side", p.Side |> Option.map (reverse sides >> Json.String)
              "direction", p.Direction |> Option.map (reverse portDirections >> Json.String)
              "label", s p.Label
              "maxConnections", p.MaxConnections |> Option.map (float >> Json.number)
              "metadata", encodeMeta p.Metadata
              "extensions", encodeExtensions p.Extensions ]

    let nodeKindText (k: NodeKind) =
        match k with
        | CoreNode(c, _) -> reverse nodeKinds c
        | CustomNode(q, _) -> q.Value

    let edgeKindText (k: EdgeKind) =
        match k with
        | CoreEdge(c, _) -> reverse edgeKinds c
        | CustomEdge(q, _) -> q.Value

    let groupKindText (k: GroupKind) = reverse groupKinds k
    let shapeText (s: Shape) = reverse shapes s
    let lineText (l: LineStyle) = reverse lines l
    let directionText (d: EdgeDirection) = reverse edgeDirections d
    let sideText (s: PortSide) = reverse sides s
    let portDirectionText (d: PortDirection) = reverse portDirections d

    let private encodeNode (n: Node) =
        let kindLabel = match n.Kind with CoreNode(_, l) -> l | CustomNode(_, l) -> Some l
        props
            [ "id", some (Json.String n.Id.Value)
              "kind", some (Json.String(nodeKindText n.Kind))
              "kindLabel", s kindLabel
              "label", some (Json.String n.Label)
              "description", s n.Description
              "variant", n.Variant |> Option.map (shapeText >> Json.String)
              "color", encodeColor n.Color
              "status", encodeStatus n.Status
              "ports", (if n.Ports.IsEmpty then None else Some(Json.Array(n.Ports |> List.map encodePort)))
              "references", encodeReferences n.References
              "metadata", encodeMeta n.Metadata
              "accessibility", encodeA11y n.Accessibility
              "interaction", encodeInteraction n.Interaction
              "layout", encodeBox n.Layout
              "extensions", encodeExtensions n.Extensions ]

    let private encodeEndpoint (e: Endpoint) =
        props [ "node", some (Json.String e.Node.Value); "port", e.Port |> Option.map (fun p -> Json.String p.Value) ]

    let private encodeEdge (e: Edge) =
        let kindLabel = e.Kind |> Option.bind (function CoreEdge(_, l) -> l | CustomEdge(_, l) -> Some l)
        props
            [ "id", some (Json.String e.Id.Value)
              "source", some (encodeEndpoint e.Source)
              "target", some (encodeEndpoint e.Target)
              "direction", e.Direction |> Option.map (directionText >> Json.String)
              "kind", e.Kind |> Option.map (edgeKindText >> Json.String)
              "kindLabel", s kindLabel
              "label", s e.Label
              "description", s e.Description
              "line", e.Line |> Option.map (lineText >> Json.String)
              "width", e.Width |> Option.map (float >> Json.number)
              "color", e.Stroke |> Option.map (fun c -> Json.Object [ "stroke", Json.String(colorValueText c) ])
              "status", encodeStatus e.Status
              "references", encodeReferences e.References
              "metadata", encodeMeta e.Metadata
              "accessibility", encodeA11y e.Accessibility
              "interaction", encodeInteraction e.Interaction
              "layout", encodeEdgeLayout e.Layout
              "extensions", encodeExtensions e.Extensions ]

    let private encodeGroup (g: Group) =
        props
            [ "id", some (Json.String g.Id.Value)
              "kind", some (Json.String(groupKindText g.Kind))
              "label", some (Json.String g.Label)
              "description", s g.Description
              "members", (if g.Members.IsEmpty then None else Some(Json.Array(g.Members |> List.map (fun m -> Json.String m.Value))))
              "parent", g.Parent |> Option.map (fun p -> Json.String p.Value)
              "line", g.Line |> Option.map (lineText >> Json.String)
              "color", encodeColor g.Color
              "status", encodeStatus g.Status
              "references", encodeReferences g.References
              "metadata", encodeMeta g.Metadata
              "accessibility", encodeA11y g.Accessibility
              "interaction", encodeInteraction g.Interaction
              "layout", encodeBox g.Layout
              "extensions", encodeExtensions g.Extensions ]

    /// Canonical encoding: fixed property order, empty optional collections omitted.
    let encode (w: Workflow) : Json =
        props
            [ "$schema", s w.Schema
              "format", some (Json.String Format.identifier)
              "formatVersion", some (Json.String(w.FormatVersion.ToString()))
              "id", some (Json.String w.Id.Value)
              "title", some (Json.String w.Title)
              "description", s w.Description
              "language", s w.Language
              "direction", w.TextDirection |> Option.map (reverse textDirections >> Json.String)
              "forma",
              some (
                  props
                      [ "version", some (Json.String(w.Forma.Version.ToString()))
                        "requires", (if w.Forma.Requires.IsEmpty then None else Some(Json.Array(w.Forma.Requires |> List.map Json.String))) ]
              )
              "metadata", encodeMeta w.Metadata
              "palette", (if w.Palette.IsEmpty then None else Some(Json.Object(w.Palette |> List.map (fun (k, v) -> k.Value, Json.String(colorSourceText v)))))
              "nodes", some (Json.Array(w.Nodes |> List.map encodeNode))
              "edges", (if w.Edges.IsEmpty then None else Some(Json.Array(w.Edges |> List.map encodeEdge)))
              "groups", (if w.Groups.IsEmpty then None else Some(Json.Array(w.Groups |> List.map encodeGroup)))
              "layout",
              whenAny
                  [ "direction", w.Layout.Direction |> Option.map (reverse flows >> Json.String)
                    "mode", w.Layout.Mode |> Option.map (reverse modes >> Json.String)
                    "canvas", w.Layout.Canvas |> Option.map (fun (cw, ch) -> Json.Object [ "width", Json.number cw; "height", Json.number ch ]) ]
              "presentation",
              whenAny
                  [ "density", w.Presentation.Density |> Option.map (reverse densities >> Json.String)
                    "legendTitle", s w.Presentation.LegendTitle
                    "legend",
                    (if w.Presentation.Legend.IsEmpty then None
                     else
                         Some(
                             Json.Array(
                                 w.Presentation.Legend
                                 |> List.map (fun l ->
                                     props
                                         [ "id", some (Json.String l.Id.Value); "label", some (Json.String l.Label); "description", s l.Description
                                           "color", encodeColor l.Color; "line", l.Line |> Option.map (lineText >> Json.String) ])
                             )
                         )) ]
              "extensions", encodeExtensions w.Extensions
              "provenance",
              w.Provenance
              |> Option.bind (fun p ->
                  whenAny
                      [ "producer", p.Producer |> Option.map (fun pr -> props [ "name", some (Json.String pr.Name); "version", s pr.Version; "uri", s pr.Uri ])
                        "createdAt", s p.CreatedAt
                        "modifiedAt", s p.ModifiedAt
                        "source", s p.Source
                        "modifiedBy", p.ModifiedBy |> Option.map (fun (n, v) -> props [ "name", some (Json.String n); "version", s v ]) ]) ]

    /// Canonical text of a workflow document.
    let serialize (w: Workflow) = Json.serialize (encode w)
