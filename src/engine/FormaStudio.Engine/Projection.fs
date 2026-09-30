namespace FormaStudio.Engine

open System.Text

/// A Folio-consumable projection of one diagram (FDA-1120..1127, EPC-DIAG-HANDOFF).
/// It is generated from canonical state for the Rendered scope only: public Forma
/// diagram markup plus a manifest. Editor state is never part of it.
type DiagramProjection =
    { Manifest: JsonValue
      Html: string
      Findings: Finding list }

type ProjectionMode =
    /// Fails when the diagram has blockers, an unavailable profile or unresolved
    /// presentation, rather than inventing a substitute (FDA-1125).
    | Strict
    /// Produces output labelled invalid/incomplete (FDA-164).
    | Diagnostic

[<RequireQualifiedAccess>]
module Projection =
    let projectionVersion = "1.0.0"
    let padding = 24
    let private scope = Rendered

    let private escape (text: string) =
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;")

    let private sha256 (text: string) =
        "sha256:" + System.Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes text)).ToLowerInvariant()

    // -- geometry and routing ------------------------------------------------

    let private anchors (b: Box) =
        {| Top = ({ X = Geometry.centerX b; Y = b.Position.Y }: Point)
           Bottom = ({ X = Geometry.centerX b; Y = Geometry.bottom b }: Point)
           Left = ({ X = b.Position.X; Y = Geometry.centerY b }: Point)
           Right = ({ X = Geometry.right b; Y = Geometry.centerY b }: Point) |}

    let private toward (b: Box) (p: Point) =
        let a = anchors b
        if p.Y < b.Position.Y then a.Top
        elif p.Y > Geometry.bottom b then a.Bottom
        elif p.X < b.Position.X then a.Left
        else a.Right

    /// Deterministic routing from canonical geometry (FDA-030, FDA-048). Returns the
    /// path points and the label position.
    let route (routing: Routing) (s: Box) (t: Box) : Point list * Point =
        let pt (a: Point) = a
        let sa, ta = anchors s, anchors t
        let points =
            match routing with
            | Manual waypoints when not (List.isEmpty waypoints) ->
                [ pt (toward s (List.head waypoints)) ] @ waypoints @ [ pt (toward t (List.last waypoints)) ]
            | Straight ->
                if t.Position.X >= Geometry.right s then [ pt sa.Right; pt ta.Left ]
                elif Geometry.right t <= s.Position.X then [ pt sa.Left; pt ta.Right ]
                elif t.Position.Y >= Geometry.bottom s then [ pt sa.Bottom; pt ta.Top ]
                else [ pt sa.Top; pt ta.Bottom ]
            | _ ->
                if t.Position.X >= Geometry.right s + 16 then
                    let mx = (Geometry.right s + t.Position.X) / 2
                    [ pt sa.Right; { X = mx; Y = sa.Right.Y }; { X = mx; Y = ta.Left.Y }; pt ta.Left ]
                elif Geometry.right t <= s.Position.X - 16 then
                    let mx = (s.Position.X + Geometry.right t) / 2
                    [ pt sa.Left; { X = mx; Y = sa.Left.Y }; { X = mx; Y = ta.Right.Y }; pt ta.Right ]
                elif t.Position.Y >= Geometry.bottom s then
                    let my = (Geometry.bottom s + t.Position.Y) / 2
                    [ pt sa.Bottom; { X = sa.Bottom.X; Y = my }; { X = ta.Top.X; Y = my }; pt ta.Top ]
                else
                    let my = (s.Position.Y + Geometry.bottom t) / 2
                    [ pt sa.Top; { X = sa.Top.X; Y = my }; { X = ta.Bottom.X; Y = my }; pt ta.Bottom ]
            |> List.pairwise
            |> List.filter (fun (a, b) -> a <> b)
            |> fun pairs -> (pairs |> List.map fst) @ (pairs |> List.tryLast |> Option.map snd |> Option.toList)
        let segments = List.pairwise points
        let longest =
            segments |> List.maxBy (fun (a, b) -> abs (a.X - b.X) + abs (a.Y - b.Y))
        let a, b = longest
        // Horizontal segments carry the label just below the line; vertical ones to its right.
        let label =
            if a.Y = b.Y then { X = (min a.X b.X) + 8; Y = a.Y + 4 }
            else { X = a.X + 6; Y = (a.Y + b.Y) / 2 - 10 }
        points, label

    let private pathData (points: Point list) =
        points
        |> List.mapi (fun i p -> sprintf "%s%d %d" (if i = 0 then "M" else " L") p.X p.Y)
        |> String.concat ""

    /// Deterministic content bounds independent of editor pan and zoom (EPC-DIAG-020).
    let bounds (diagram: Diagram) =
        let routePoints =
            diagram.Edges
            |> List.collect (fun e -> match e.Routing with Manual ps -> ps | _ -> [])
            |> List.map (fun p -> { Position = p; Size = { Width = 0; Height = 0 } })
        (diagram.Nodes |> List.map _.Box) @ (diagram.Groups |> List.map _.Box) @ routePoints
        |> Geometry.bounds
        |> Option.defaultValue { Position = { X = 0; Y = 0 }; Size = { Width = 0; Height = 0 } }

    // -- presentation helpers -----------------------------------------------

    let private shapeAttr shape =
        match shape with
        | Some Rounded -> " data-ef-shape=\"rounded\""
        | Some Pill -> " data-ef-shape=\"pill\""
        | Some Ellipse -> " data-ef-shape=\"ellipse\""
        | Some Diamond -> " data-ef-shape=\"diamond\""
        | _ -> ""

    let private lineAttr line =
        match line with
        | Some Dashed -> " data-ef-line=\"dashed\""
        | Some Dotted -> " data-ef-line=\"dotted\""
        | _ -> ""

    let private lineWord line =
        match line with
        | Some Dashed -> "dashed"
        | Some Dotted -> "dotted"
        | _ -> "solid"

    let private css (e: Effective<ColorResolution>) = e.Value |> Option.bind _.Css

    /// Only validated values ever reach a style attribute: `#rrggbb` or
    /// `var(--ef-color-*)` from the pinned token list.
    let private colorDeclarations (effective: EffectiveAppearance) =
        [ "--ef-diagram-fill", css effective.Fill
          "--ef-diagram-stroke", css effective.Stroke
          "--ef-diagram-accent", css effective.Accent
          "--ef-diagram-foreground", css effective.Foreground ]
        |> List.choose (fun (name, value) -> value |> Option.map (sprintf "%s: %s;" name))

    let private geometryDeclarations (dx: int) (dy: int) (b: Box) =
        [ sprintf "--ef-diagram-x: %dpx;" (b.Position.X + dx)
          sprintf "--ef-diagram-y: %dpx;" (b.Position.Y + dy)
          sprintf "--ef-diagram-w: %dpx;" b.Size.Width
          sprintf "--ef-diagram-h: %dpx;" b.Size.Height ]

    let elementId (diagram: Diagram) (id: string) = sprintf "fs-%s-%s" (Id.value diagram.Id) id

    // -- legend ---------------------------------------------------------------

    type private LegendEntry =
        { Label: string
          Description: string
          Fill: string option
          Stroke: string option
          Accent: string option }

    let private legend (project: Project) (effects: (ObjectRef * EffectiveAppearance) list) =
        let fromMappings =
            effects
            |> List.choose (fun (_, e) ->
                match e.Fill.Layer with
                | MappingLayer(mapping, _, legendText) ->
                    let name = ProjectOps.tryMapping mapping project |> Option.map _.Name |> Option.defaultValue (Id.value mapping)
                    Some { Label = legendText; Description = sprintf "fill from the %s mapping" (name.ToLowerInvariant()); Fill = css e.Fill; Stroke = None; Accent = css e.Accent }
                | _ -> None)
        let fromStyles =
            effects
            |> List.choose (fun (_, e) ->
                match e.Stroke.Layer, e.Fill.Layer with
                | StyleLayer(id, _), _
                | _, StyleLayer(id, _) ->
                    ProjectOps.tryStyle id project
                    |> Option.map (fun s -> { Label = s.Name; Description = "named style, presentation only"; Fill = css e.Fill; Stroke = css e.Stroke; Accent = css e.Accent })
                | _ -> None)
        let fromOverrides =
            effects
            |> List.choose (fun (_, e) ->
                match e.Fill.Layer, e.Fill.Value with
                | OverrideLayer, Some { Palette = Some slot } ->
                    ProjectOps.tryPalette slot project
                    |> Option.map (fun p -> { Label = p.Name; Description = "chosen by the author; it carries no status"; Fill = css e.Fill; Stroke = None; Accent = css e.Accent })
                | OverrideLayer, Some _ -> Some { Label = "Other author colors"; Description = "chosen by the author; they carry no status"; Fill = None; Stroke = None; Accent = None }
                | _ -> None)
        let plain = { Label = "No mapping or author color"; Description = "Forma default"; Fill = None; Stroke = None; Accent = None }
        let usesDefault = effects |> List.exists (fun (r, e) -> (match r with NodeRef _ -> true | _ -> false) && e.Fill.Value.IsNone)
        (fromMappings @ fromStyles @ fromOverrides @ (if usesDefault then [ plain ] else []))
        |> List.distinctBy _.Label

    // -- main -----------------------------------------------------------------

    let private kindLabel (profile: DiagramProfile option) kind =
        profile |> Option.bind (fun p -> Profiles.nodeKind p kind) |> Option.map _.Label |> Option.defaultValue kind

    let private renderedMetadata (project: Project) (diagram: Diagram) reference (fields: FieldKey list) =
        fields
        |> List.choose (fun key -> ProjectOps.tryField key project)
        |> List.filter (fun f -> MetadataRules.allows scope f && MetadataRules.applies f (ObjectRef.kind reference))
        |> List.map (fun f -> OutputValues.ofResolved f (ProjectOps.resolveField f reference project))
        |> List.filter (fun v -> v.State <> "missing" || diagram.Display.Missing = ShowValueState)

    let private valueState (v: OutputValue) =
        match v.State with
        | "explicit" -> None
        | "missing" -> Some("unknown", "not set")
        | state -> Some(state, defaultArg v.Note state)

    let private metaHtml (values: OutputValue list) =
        match values with
        | [] -> ""
        | _ ->
            values
            |> List.map (fun v ->
                let text = v.Text |> Option.map escape |> Option.defaultValue ""
                let tag =
                    valueState v
                    |> Option.map (fun (state, note) -> sprintf "%s<span class=\"ef-diagram-value-state\" data-ef-value-state=\"%s\">%s</span>" (if text = "" then "" else " ") state (escape note))
                    |> Option.defaultValue ""
                sprintf "<dt>%s</dt><dd>%s%s</dd>" (escape v.Label) text tag)
            |> String.concat ""
            |> sprintf "\n        <dl class=\"ef-diagram-node__meta\">%s</dl>"

    let project (mode: ProjectionMode) (project: Project) (diagramId: DiagramId) : Result<DiagramProjection, Finding list> =
        match ProjectOps.tryDiagram diagramId project with
        | None -> Error [ Finding.blocker "diagram.missing" CommandRule (sprintf "diagram:%s" (Id.value diagramId)) "The diagram does not exist." ]
        | Some diagram ->
            let target = sprintf "diagram:%s" (Id.value diagram.Id)
            let profile = Profiles.tryFind diagram.Profile
            let refsOf (d: Diagram) =
                (d.Groups |> List.map (fun g -> GroupRef(d.Id, g.Id))) @ (d.Nodes |> List.map (fun n -> NodeRef(d.Id, n.Id))) @ (d.Edges |> List.map (fun e -> EdgeRef(d.Id, e.Id)))
            let resolved = refsOf diagram |> List.map (fun r -> r, AppearanceResolution.resolve (OutputScope scope) project r)
            let effects = resolved |> List.map (fun (r, (e, _)) -> r, e)
            let effectOf r = effects |> List.find (fst >> (=) r) |> snd
            let diagramFindings =
                (resolved |> List.collect (snd >> snd))
                @ (Validation.run project |> List.filter (fun f -> f.Target = target || f.Target.StartsWith(target + "/")))
            let blockers =
                (diagramFindings |> List.filter Finding.isBlocker)
                @ (if profile.IsNone then [ Finding.blocker "profile.unavailable" ProfileRule target "The diagram's profile is unavailable, so its presentation mapping cannot be proven." ] else [])
                @ (diagramFindings |> List.filter (fun f -> f.Code = "appearance.palette.missing" || f.Code = "appearance.style.missing") |> List.map (fun f -> { f with Severity = Blocker }))
            match mode, blockers with
            | Strict, _ :: _ -> Error blockers
            | _ ->
                let b = bounds diagram
                let dx, dy = padding - b.Position.X, padding - b.Position.Y
                let width, height = b.Size.Width + 2 * padding, b.Size.Height + 2 * padding
                let prefix = elementId diagram ""
                let nodeById id = diagram.Nodes |> List.tryFind (fun n -> n.Id = id)
                let displayFields kind = diagram.Display.KindFields |> Map.tryFind kind |> Option.defaultValue diagram.Display.NodeFields
                let shift (p: Point) = { X = p.X + dx; Y = p.Y + dy }
                let shiftBox (bx: Box) = { bx with Position = shift bx.Position }

                let groupsHtml =
                    diagram.Groups
                    |> List.map (fun g ->
                        let e = effectOf (GroupRef(diagram.Id, g.Id))
                        let kindName = match g.Kind with Group -> "Group" | Lane -> "Lane" | Phase -> "Phase"
                        let variant = match g.Kind with Group -> "" | Lane -> " data-ef-group=\"lane\"" | Phase -> " data-ef-group=\"phase\""
                        sprintf "      <div class=\"ef-diagram-group\" id=\"%s\"%s%s style=\"%s\">\n        <p class=\"ef-diagram-group__header\"><span class=\"ef-diagram-group__kind\">%s</span> <span class=\"ef-diagram-group__label\">%s</span></p>\n      </div>"
                            (elementId diagram (Id.value g.Id)) variant (lineAttr e.Line.Value)
                            (String.concat " " (geometryDeclarations dx dy g.Box @ colorDeclarations e)) kindName (escape g.Label))

                let routed =
                    diagram.Edges
                    |> List.choose (fun edge ->
                        match nodeById edge.Source.Node, nodeById edge.Target.Node with
                        | Some s, Some t ->
                            let routing = match edge.Routing with Manual ps -> Manual(ps |> List.map shift) | other -> other
                            let points, labelAt = route routing (shiftBox s.Box) (shiftBox t.Box)
                            Some(edge, s, t, points, labelAt)
                        | _ -> None)

                let directed (edge: DiagramEdge) =
                    profile |> Option.bind (fun p -> Profiles.edgeKind p edge.Kind) |> Option.map _.Directed |> Option.defaultValue true

                let wiresHtml =
                    routed
                    |> List.map (fun (edge, _, _, points, _) ->
                        let e = effectOf (EdgeRef(diagram.Id, edge.Id))
                        let style =
                            [ css e.ConnectorStroke |> Option.map (sprintf "--ef-diagram-connector-stroke: %s;")
                              e.ConnectorWidth.Value |> Option.map (sprintf "--ef-diagram-connector-width: %d;") ]
                            |> List.choose id
                        sprintf "        <path class=\"ef-diagram-connector\" id=\"%s\"%s%s d=\"%s\"%s/>"
                            (elementId diagram (Id.value edge.Id)) (lineAttr e.Line.Value)
                            (if List.isEmpty style then "" else sprintf " style=\"%s\"" (String.concat " " style))
                            (pathData points) (if directed edge then sprintf " marker-end=\"url(#%sarrow)\"" prefix else ""))

                let nodesHtml =
                    diagram.Nodes
                    |> List.map (fun n ->
                        let reference = NodeRef(diagram.Id, n.Id)
                        let e = effectOf reference
                        let id = elementId diagram (Id.value n.Id)
                        sprintf "      <article class=\"ef-diagram-node\" id=\"%s\"%s aria-labelledby=\"%s-label\" style=\"%s\">\n        <p class=\"ef-diagram-node__kind\">%s</p>\n        <h3 class=\"ef-diagram-node__label\" id=\"%s-label\">%s</h3>%s\n      </article>"
                            id (shapeAttr e.Shape.Value) id (String.concat " " (geometryDeclarations dx dy n.Box @ colorDeclarations e))
                            (escape (kindLabel profile n.Kind)) id (escape n.Label)
                            (metaHtml (renderedMetadata project diagram reference (displayFields n.Kind))))

                let labelsHtml =
                    routed
                    |> List.choose (fun (edge, _, _, _, at) ->
                        edge.Label |> Option.map (fun label -> sprintf "      <span class=\"ef-diagram-connector__label\" style=\"--ef-diagram-x: %dpx; --ef-diagram-y: %dpx;\">%s</span>" at.X at.Y (escape label)))

                let relationText (edge: DiagramEdge, s: DiagramNode, t: DiagramNode, _, _) =
                    let e = effectOf (EdgeRef(diagram.Id, edge.Id))
                    let verb = if directed edge then "leads to" else "is associated with"
                    let condition = edge.Label |> Option.map (sprintf ": %s") |> Option.defaultValue ""
                    sprintf "%s %s %s%s (%s line)." s.Label verb t.Label condition (lineWord e.Line.Value)
                let relations = routed |> List.map relationText

                let laneSummary =
                    diagram.Groups
                    |> List.filter (fun g -> g.Kind = Lane)
                    |> List.map (fun g ->
                        let members = g.Members |> List.choose nodeById |> List.map _.Label
                        sprintf "%s: %s" g.Label (if List.isEmpty members then "no items" else String.concat ", " members))

                let legendEntries = legend project effects
                let legendHtml =
                    match legendEntries with
                    | [] -> ""
                    | entries ->
                        entries
                        |> List.map (fun l ->
                            let declarations =
                                [ l.Fill |> Option.map (sprintf "--ef-diagram-fill: %s;"); l.Stroke |> Option.map (sprintf "--ef-diagram-stroke: %s;"); l.Accent |> Option.map (sprintf "--ef-diagram-accent: %s;") ]
                                |> List.choose id
                            let style = if List.isEmpty declarations then "" else sprintf " style=\"%s\"" (String.concat " " declarations)
                            sprintf "    <div><dt><span class=\"ef-diagram-legend__swatch\"%s></span> %s</dt><dd>%s</dd></div>" style (escape l.Label) (escape l.Description))
                        |> String.concat "\n"
                        |> sprintf "\n  <dl class=\"ef-diagram-legend\" aria-label=\"Key: what color means in this diagram\">\n%s\n  </dl>"

                let invalidNote =
                    match mode, blockers with
                    | Diagnostic, _ :: _ -> "\n  <p><strong>Invalid or incomplete diagram.</strong> This diagnostic projection has unresolved validation blockers.</p>"
                    | _ -> ""

                let title = escape diagram.Name
                let html =
                    [ sprintf "<figure class=\"ef-diagram\" id=\"%sfigure\" aria-labelledby=\"%stitle\">" prefix prefix
                      sprintf "  <figcaption id=\"%stitle\">%s</figcaption>%s" prefix title invalidNote
                      "  <p>Every item states its kind in text. Colors are explained in the key; they never carry status on their own.</p>"
                      sprintf "  <div class=\"ef-diagram__viewport\" tabindex=\"0\" role=\"group\" aria-label=\"%s canvas, scroll to see all items\">" title
                      sprintf "    <div class=\"ef-diagram__canvas\" style=\"--ef-diagram-canvas-w: %dpx; --ef-diagram-canvas-h: %dpx;\">" width height ]
                    @ groupsHtml
                    @ [ sprintf "      <svg class=\"ef-diagram__wires\" viewBox=\"0 0 %d %d\" aria-hidden=\"true\" focusable=\"false\">" width height
                        sprintf "        <defs><marker id=\"%sarrow\" class=\"ef-diagram-marker\" viewBox=\"0 0 10 10\" refX=\"9\" refY=\"5\" markerWidth=\"8\" markerHeight=\"8\" orient=\"auto-start-reverse\"><path d=\"M0 0 L10 5 L0 10 z\"/></marker></defs>" prefix ]
                    @ wiresHtml
                    @ [ "      </svg>" ]
                    @ nodesHtml
                    @ labelsHtml
                    @ [ "    </div>"; "  </div>"; "  <ol class=\"ef-diagram__relations\" aria-label=\"Relationships\">" ]
                    @ (relations |> List.map (escape >> sprintf "    <li>%s</li>"))
                    @ [ "  </ol>" ]
                    @ (if List.isEmpty laneSummary then [] else [ "  <ul aria-label=\"Lanes\">" ] @ (laneSummary |> List.map (escape >> sprintf "    <li>%s</li>")) @ [ "  </ul>" ])
                    |> String.concat "\n"
                    |> fun body -> body + legendHtml + "\n</figure>\n"

                let objectJson =
                    let appearance r = EffectiveJson.encode ProvenanceExport project (effectOf r)
                    let metadata values = values |> List.map OutputValues.toJson |> JArray
                    (diagram.Groups |> List.map (fun g ->
                        JObject
                            [ "id", JString(Id.value g.Id); "role", JString "group"; "kind", JString(match g.Kind with Group -> "group" | Lane -> "lane" | Phase -> "phase")
                              "label", JString g.Label; "elementId", JString(elementId diagram (Id.value g.Id))
                              "members", g.Members |> List.map (Id.value >> JString) |> JArray; "appearance", appearance (GroupRef(diagram.Id, g.Id)) ]))
                    @ (diagram.Nodes |> List.map (fun n ->
                        let reference = NodeRef(diagram.Id, n.Id)
                        JObject
                            [ "id", JString(Id.value n.Id); "role", JString "node"; "kind", JString n.Kind; "kindLabel", JString(kindLabel profile n.Kind)
                              "label", JString n.Label; "elementId", JString(elementId diagram (Id.value n.Id))
                              "renderedMetadata", metadata (renderedMetadata project diagram reference (displayFields n.Kind))
                              "appearance", appearance reference ]))
                    @ (routed |> List.map (fun (edge, s, t, _, _) ->
                        Json.objOpt
                            [ "id", Some(JString(Id.value edge.Id)); "role", Some(JString "edge"); "kind", Some(JString edge.Kind)
                              "source", Some(JString(Id.value s.Id)); "target", Some(JString(Id.value t.Id)); "label", edge.Label |> Option.map JString
                              "elementId", Some(JString(elementId diagram (Id.value edge.Id))); "appearance", Some(appearance (EdgeRef(diagram.Id, edge.Id))) ]))

                let manifest =
                    Json.objOpt
                        [ "projectionVersion", Some(JString projectionVersion)
                          "kind", Some(JString "forma-studio.diagram-projection")
                          "scope", Some(JString "rendered")
                          "status", Some(JString(if List.isEmpty blockers then "valid" else "invalid-diagnostic"))
                          "source",
                          Some(
                              JObject
                                  [ "projectId", JString(Id.value project.Id)
                                    "diagramId", JString(Id.value diagram.Id)
                                    "diagramName", JString diagram.Name
                                    "revision", JString(Codec.diagramRevision project diagram)
                                    "schemaVersion", Json.ofInt Codec.currentSchemaVersion
                                    "profile", JObject [ "id", JString diagram.Profile.Id; "version", JString diagram.Profile.Version ] ]
                          )
                          "presentation",
                          Some(JObject [ "formaVersion", JString project.FormaVersion; "contract", JString "forma.diagram-presentation"; "contractVersion", JString "2.0.0"; "paint", JString "solid" ])
                          "content", Some(JObject [ "html", JString "diagram.html"; "sha256", JString(sha256 html) ])
                          "bounds",
                          Some(JObject [ "x", Json.ofInt b.Position.X; "y", Json.ofInt b.Position.Y; "width", Json.ofInt b.Size.Width; "height", Json.ofInt b.Size.Height
                                         "padding", Json.ofInt padding; "canvasWidth", Json.ofInt width; "canvasHeight", Json.ofInt height; "unit", JString "logical-px" ])
                          "objects", Some(JArray objectJson)
                          "relationships", Some(relations |> List.map JString |> JArray)
                          "lanes", Some(laneSummary |> List.map JString |> JArray)
                          "legend", Some(legendEntries |> List.map (fun l -> JObject [ "label", JString l.Label; "description", JString l.Description ]) |> JArray)
                          "accessibility",
                          Some(JObject [ "title", JString diagram.Name
                                         "summary", JString(sprintf "%s: %d items and %d relationships. Every relationship is listed in text." diagram.Name diagram.Nodes.Length relations.Length) ])
                          "outputHints",
                          Some(JObject [ "preferredOrientation", JString(if width > height then "landscape" else "portrait"); "minimumTextPt", Json.ofInt 7; "baseTextPx", Json.ofInt 13 ])
                          "excluded", Some([ "selection"; "hover"; "handles"; "guides"; "minimap"; "routing-preview"; "source-only-metadata" ] |> List.map JString |> JArray)
                          "provenance", Some(JObject [ "generator", JString "forma-studio-engine"; "projectRevision", JString(Codec.revision project) ]) ]

                Ok { Manifest = manifest; Html = html; Findings = diagramFindings }

    /// Rejects a projection whose source revision no longer matches the diagram
    /// (FDA-1124). A caller may still label and show a stale projection explicitly.
    let checkFresh (manifest: JsonValue) (project: Project) =
        let field path = path |> List.fold (fun (json: JsonValue option) name -> json |> Option.bind (Json.field name)) (Some manifest)
        match field [ "source"; "diagramId" ], field [ "source"; "revision" ] with
        | Some(JString diagram), Some(JString revision) ->
            match Id.create<DiagramKind> diagram |> Result.toOption |> Option.bind (fun d -> ProjectOps.tryDiagram d project) with
            | None -> Error "The projection's diagram no longer exists."
            | Some d when Codec.diagramRevision project d = revision -> Ok()
            | Some _ -> Error "The projection is stale: the diagram has changed since it was generated."
        | _ -> Error "The manifest has no source revision."
