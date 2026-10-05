namespace FormaStudio.Engine

/// The whole editor page as one Limen view model (JSON), projected from the
/// editor state. Pure; the browser binds it through `data-*` attributes.
[<RequireQualifiedAccess>]
module EditorView =
    let private describeDecision (d: DependencyDecision) =
        let kind = match d.Kind with "field" -> "Field" | "palette" -> "Palette color" | "style" -> "Style" | "mapping" -> "Color rule" | other -> other
        match d.Action with
        | Reused -> sprintf "%s %s: reuses the identical one already in this project." kind d.SourceId
        | Imported -> sprintf "%s %s: will be added to this project." kind d.SourceId
        | Remapped n -> sprintf "%s %s: differs from this project's; it will be added as %s." kind d.SourceId n

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
        let p = EditorState.project state
        match EditorState.diagram state with
        | None -> JObject [ "status", str "The diagram is not available." ]
        | Some diagram ->
            let profile = Profiles.tryFind diagram.Profile
            let selected = EditorState.selectedRef state
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
                  "flowVisible", JBool(state.Page.IsNone && not state.WorkflowVisible)
                  "layoutVisible", JBool(state.Page.IsSome && not state.WorkflowVisible)
                  "flowHidden", JBool(state.Page.IsSome || state.WorkflowVisible)
                  "layoutHidden", JBool(state.Page.IsNone || state.WorkflowVisible)
                  "workflowHidden", JBool(not state.WorkflowVisible)
                  "workflowsCurrent", str (if state.WorkflowVisible then "page" else "false")
                  "workflows",
                  state.Workflows.Entries
                  |> List.map (fun e ->
                      item
                          [ "key", str e.Id
                            "label", str (e.Title + (if state.Workflows.Unsaved.Contains e.Id then " (unsaved)" else ""))
                            "file", str (WorkflowLibrary.fileName e)
                            "validity", str e.Class
                            "current", str (if state.Workflows.Current = Some e.Id then "page" else "false") ])
                  |> JArray
                  "workflowCount", str (string state.Workflows.Entries.Length)
                  "workflowDocument", str (WorkflowLibrary.current state.Workflows |> Option.map _.Text |> Option.defaultValue "")
                  "workflowRevision", str (string state.Workflows.Revision)
                  "workflowFile", str (WorkflowLibrary.current state.Workflows |> Option.map WorkflowLibrary.fileName |> Option.defaultValue "workflow.forma-workflow.json")
                  "workflowNone", JBool((WorkflowLibrary.current state.Workflows).IsSome)
                  "exportTargets",
                  [ "fragment", "HTML fragment", HtmlFragment; "document", "Complete HTML document", HtmlDocument ]
                  |> List.map (fun (k, label, t) -> item [ "key", str k; "label", str label; "pressed", str (if state.Export.Target = t then "true" else "false") ])
                  |> JArray
                  "exportBrands",
                  [ "none", "No brand"; "echelon", "Echelon"; "example-harbor", "Example Harbor" ]
                  |> List.map (fun (k, label) -> item [ "key", str k; "label", str label; "pressed", str (if (state.Export.Brand |> Option.defaultValue "none") = k then "true" else "false") ])
                  |> JArray
                  "exportText", str state.Export.Text
                  "exportFile", str state.Export.FileName
                  "exportSummary", str state.Export.Summary
                  "exportOmitted", state.Export.Omitted |> List.mapi (fun i o -> item [ "key", str (string i); "text", str o ]) |> JArray
                  "exportEmpty", JBool(state.Export.Text = "")
                  "exportInteractive", str (if state.Export.InteractiveWorkflow then "true" else "false")
                  "pageName", str (state.Page |> Option.bind (fun id -> ProjectOps.tryPage id p) |> Option.map _.Name |> Option.defaultValue "")
                  "density",
                  str (state.Page |> Option.bind (fun id -> ProjectOps.tryPage id p) |> Option.bind (fun pg -> pg.Nodes |> List.tryFind (fun n -> n.Component = "stack"))
                       |> Option.bind (fun root -> Map.tryFind "density" root.Properties) |> Option.map (function JString d -> d | _ -> "standard") |> Option.defaultValue "standard")
                  "layoutItems",
                  (state.Page |> Option.bind (fun id -> ProjectOps.tryPage id p) |> Option.bind (fun pg -> pg.Nodes |> List.tryFind (fun n -> n.Component = "stack"))
                   |> Option.map (fun root -> root.Slots |> Map.tryFind "children" |> Option.defaultValue [])
                   |> Option.defaultValue []
                   |> List.map (fun c ->
                       let primary = Components.tryFind c.Component |> Option.bind (fun ct -> List.tryHead ct.Content) |> Option.defaultValue "text"
                       let text = c.Content |> Map.tryFind primary |> Option.map (function JString t -> t | _ -> "") |> Option.defaultValue ""
                       let label = if c.Component = "heading" then sprintf "Heading text for %s" (Id.value c.Id) else sprintf "%s %s for %s" c.Component primary (Id.value c.Id)
                       item [ "key", str (Id.value c.Id + "|" + primary); "text", str text; "label", str label; "kind", str c.Component ]))
                  |> JArray
                  "componentChoices",
                  Components.catalog |> List.filter (fun c -> c.Id <> "stack" && c.Id <> "heading") |> List.map (fun c -> item [ "key", str c.Id; "label", str ("Add " + c.Id) ]) |> JArray
                  "layoutTargets",
                  (state.Page |> Option.bind (fun id -> ProjectOps.tryPage id p)
                   |> Option.map (fun pg ->
                       let rec containers (nodes: ComponentNode list) =
                           nodes |> List.collect (fun n -> (if n.Component <> "stack" && not (List.isEmpty (Components.tryFind n.Component |> Option.map _.Slots |> Option.defaultValue [])) then [ n ] else []) @ (n.Slots |> Map.toList |> List.collect snd |> containers))
                       containers pg.Nodes)
                   |> Option.defaultValue []
                   |> List.map (fun n -> n.Id, n.Component)
                   |> fun cs -> ("root", "page") :: (cs |> List.map (fun (id, kind) -> Id.value id, kind + " " + Id.value id))
                   |> List.map (fun (k, label) -> item [ "key", str k; "label", str ("Add into " + label); "pressed", str (if (state.LayoutTarget |> Option.defaultValue "root") = k then "true" else "false") ]))
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
                  "zoomOutDisabled", JBool(state.Zoom <= List.head EditorState.zoomLevels)
                  "zoomInDisabled", JBool(state.Zoom >= List.last EditorState.zoomLevels)
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
                  EditorState.templates state
                  |> List.map (fun (k, name, _, _) -> item [ "key", str k; "label", str name; "pressed", str (if state.Template = Some k then "true" else "false") ])
                  |> JArray
                  "templateChosen", JBool (EditorState.chosenTemplate state |> Option.isSome)
                  "templateDescription", str (EditorState.chosenTemplate state |> Option.map (fun (_, _, d, _) -> d) |> Option.defaultValue "")
                  "templatePlan",
                  (match EditorState.chosenTemplate state with
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
                  "widthValue", str (EditorState.selectedNode state |> Option.map (fun n -> string n.Box.Size.Width) |> Option.defaultValue "")
                  "heightValue", str (EditorState.selectedNode state |> Option.map (fun n -> string n.Box.Size.Height) |> Option.defaultValue "")
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
                  "fieldTypes", EditorIntents.fieldTypes |> List.map (fun (k, label) -> item [ "key", str k; "label", str label; "pressed", str (if k = state.Drafts.FieldType then "true" else "false") ]) |> JArray
                  "fieldScopes", EditorIntents.scopeChoices |> List.map (fun (k, v, label) -> item [ "key", str k; "label", str label; "pressed", str (if v = state.Drafts.FieldScope then "true" else "false") ]) |> JArray
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
