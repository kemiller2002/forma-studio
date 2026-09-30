namespace FormaStudio.Engine

type IncidentEdgePolicy =
    | RejectIfConnected
    | RemoveIncidentEdges

type PaletteRemoval =
    | BlockPaletteIfUsed
    | ReassignPalette of PaletteSlotId
    | MaterializePalette

type StyleRemoval =
    | BlockStyleIfUsed
    | DetachAndMaterialize

type FieldRemoval =
    | BlockFieldIfUsed
    | RemoveFieldValues

type SlotPosition = { Parent: ComponentNodeId; Slot: string }

type LayoutCommand =
    | AddPage of PageId * name: string * route: string option
    | AddComponent of PageId * parent: SlotPosition option * index: int * ComponentNodeId * componentId: string
    | RemoveComponent of PageId * ComponentNodeId
    | MoveComponent of PageId * ComponentNodeId * parent: SlotPosition option * index: int
    | SetComponentProperty of PageId * ComponentNodeId * name: string * value: JsonValue option
    | SetComponentContent of PageId * ComponentNodeId * key: string * text: string

type FlowCommand =
    | AddDiagram of DiagramId * name: string * ProfileRef
    | RenameDiagram of DiagramId * name: string
    | RemoveDiagram of DiagramId
    | AddNode of DiagramId * NodeId * kind: string * label: string * x: float * y: float * width: float * height: float
    /// Moves several nodes by one delta each as one history entry (FDA-063).
    | MoveNodes of DiagramId * (NodeId * float * float) list
    | ResizeNode of DiagramId * NodeId * width: float * height: float
    | SetNodeLabel of DiagramId * NodeId * string
    | SetNodeKind of DiagramId * NodeId * string
    | RemoveNode of DiagramId * NodeId * IncidentEdgePolicy
    | Connect of DiagramId * EdgeId * kind: string * source: Endpoint * target: Endpoint * label: string option
    | Reconnect of DiagramId * EdgeId * source: Endpoint * target: Endpoint
    | SetEdgeLabel of DiagramId * EdgeId * string option
    | SetEdgeRouting of DiagramId * EdgeId * Routing
    | RemoveEdge of DiagramId * EdgeId
    | AddGroup of DiagramId * GroupId * GroupKindName * label: string * x: float * y: float * width: float * height: float
    /// Moves a container and its explicit members together (FDA-103).
    | MoveGroup of DiagramId * GroupId * dx: float * dy: float
    /// Places a node in one lane (or none); lanes are exclusive (FDA-029, FDA-102).
    | AssignLane of DiagramId * NodeId * GroupId option
    | SetGroupMembers of DiagramId * GroupId * NodeId list
    | SetDisplay of DiagramId * MetadataDisplay
    | AddReference of ObjectRef * TypedReference
    | RemoveReference of ObjectRef * ReferenceId

type MetadataCommand =
    | DefineField of FieldDefinition
    | RenameField of FieldKey * name: string
    | SetFieldDisclosure of FieldKey * Disclosure
    | RemoveField of FieldKey * FieldRemoval
    /// Sets one field on several objects atomically (FDA-983).
    | SetValue of ObjectRef list * FieldKey * StoredValue
    /// Removes the stored value, revealing the default or derived value (FDA-984).
    | ClearValue of ObjectRef list * FieldKey
    /// Turns a default, derived or source-bound value into an explicit one (FDA-928).
    | MaterializeValue of ObjectRef list * FieldKey

type AppearanceCommand =
    | AddPaletteSlot of PaletteSlot
    | RenamePaletteSlot of PaletteSlotId * name: string
    | SetPaletteValue of PaletteSlotId * PaletteValue
    | MovePaletteSlot of PaletteSlotId * index: int
    | RemovePaletteSlot of PaletteSlotId * PaletteRemoval
    | DefineStyle of AppearanceStyle
    | UpdateStyle of StyleId * Appearance
    | RenameStyle of StyleId * name: string
    | RemoveStyle of StyleId * StyleRemoval
    | ApplyStyle of ObjectRef list * StyleId option
    /// Sets the properties that are Some in the given appearance as overrides.
    | SetOverride of ObjectRef list * Appearance
    /// Removes overrides so the next layer shows through (FDA-1064).
    | ResetOverride of ObjectRef list * AppearanceProperty list
    | DetachStyle of ObjectRef list
    | DefineMapping of PresentationMapping
    | UpdateMapping of PresentationMapping
    | RemoveMapping of MappingId
    | MoveMapping of MappingId * index: int

/// One typed command surface for Layout, Flow, metadata and appearance. Pointer,
/// keyboard, inspector, Structure view and agent input all produce these values
/// (FDA-069, FDA-221, FDA-1020).
type Command =
    | Layout of LayoutCommand
    | Flow of FlowCommand
    | MetadataCmd of MetadataCommand
    | AppearanceCmd of AppearanceCommand
    | Batch of label: string * Command list

type Applied =
    { Project: Project
      Obligations: Obligation list }

[<RequireQualifiedAccess>]
module Commands =
    let private reject code target message =
        Error [ Finding.blocker code CommandRule target message ]

    let private lift target (result: Result<'T, string>) =
        result |> Result.mapError (fun message -> [ Finding.blocker "command.rejected" CommandRule target message ])

    let private ok project = Ok { Project = project; Obligations = [] }

    let private insertAt index item list =
        let i = max 0 (min index (List.length list))
        List.take i list @ [ item ] @ List.skip i list

    let private diagramTarget (d: DiagramId) = sprintf "diagram:%s" (Id.value d)

    let private requireProfile (project: Project) diagramId =
        match ProjectOps.tryDiagram diagramId project with
        | None -> reject "diagram.missing" (diagramTarget diagramId) "The diagram does not exist."
        | Some diagram ->
            match Profiles.tryFind diagram.Profile with
            | None ->
                reject "profile.unavailable" (diagramTarget diagramId)
                    (sprintf "Profile %s@%s is not available, so the diagram is read-only here." diagram.Profile.Id diagram.Profile.Version)
            | Some profile -> Ok(diagram, profile)

    // -- Layout --------------------------------------------------------------

    let rec private detach (id: ComponentNodeId) (nodes: ComponentNode list) : ComponentNode option * ComponentNode list =
        match nodes |> List.tryFind (fun n -> n.Id = id) with
        | Some found -> Some found, nodes |> List.filter (fun n -> n.Id <> id)
        | None ->
            nodes
            |> List.fold
                (fun (found, acc) node ->
                    match found with
                    | Some _ -> found, acc @ [ node ]
                    | None ->
                        let slotResults = node.Slots |> Map.map (fun _ children -> detach id children)
                        let inner = slotResults |> Map.toList |> List.tryPick (fun (_, (f, _)) -> f)
                        inner, acc @ [ { node with Slots = slotResults |> Map.map (fun _ (_, children) -> children) } ])
                (None, [])

    let rec private insertInto (position: SlotPosition option) index (item: ComponentNode) (nodes: ComponentNode list) =
        match position with
        | None -> Ok(insertAt index item nodes)
        | Some { Parent = parent; Slot = slot } ->
            let rec go (nodes: ComponentNode list) =
                nodes
                |> List.map (fun node ->
                    if node.Id = parent then
                        let children = node.Slots |> Map.tryFind slot |> Option.defaultValue []
                        { node with Slots = node.Slots |> Map.add slot (insertAt index item children) }
                    else { node with Slots = node.Slots |> Map.map (fun _ c -> go c) })
            Ok(go nodes)

    let private checkSlot (page: Page) (position: SlotPosition option) =
        match position with
        | None -> Ok()
        | Some { Parent = parent; Slot = slot } ->
            let rec find (nodes: ComponentNode list) =
                nodes |> List.tryPick (fun n -> if n.Id = parent then Some n else n.Slots |> Map.toList |> List.tryPick (snd >> find))
            match find page.Nodes with
            | None -> Error(sprintf "Parent component '%s' does not exist." (Id.value parent))
            | Some p ->
                match Components.tryFind p.Component with
                | Some contract when List.contains slot contract.Slots -> Ok()
                | Some _ -> Error(sprintf "'%s' has no slot named '%s'." p.Component slot)
                | None -> Error(sprintf "'%s' is not an editable Forma component." p.Component)

    let private layout (command: LayoutCommand) (project: Project) =
        match command with
        | AddPage(id, name, route) ->
            if (ProjectOps.tryPage id project).IsSome then reject "id.duplicate" (sprintf "page:%s" (Id.value id)) "A page with this id already exists."
            elif System.String.IsNullOrWhiteSpace name then reject "page.name" (sprintf "page:%s" (Id.value id)) "A page needs a name."
            else
                let page = { Id = id; Name = name; Route = route; Title = None; Description = None; Nodes = []; Annotations = []; Metadata = Map.empty }
                ok { project with Pages = project.Pages @ [ page ]; StartPage = project.StartPage |> Option.orElse (Some id) }
        | AddComponent(pageId, position, index, id, componentId) ->
            let target = sprintf "page:%s/component:%s" (Id.value pageId) (Id.value id)
            match Components.tryFind componentId, ProjectOps.tryPage pageId project with
            | None, _ -> reject "component.unknown" target (sprintf "'%s' is not an authorable Forma component." componentId)
            | _, None -> reject "page.missing" target "The page does not exist."
            | Some _, Some page ->
                if ProjectOps.componentIds page.Nodes |> List.contains id then reject "id.duplicate" target "A component with this id already exists."
                else
                    checkSlot page position
                    |> lift target
                    |> Result.bind (fun () ->
                        let node =
                            { Id = id; Component = componentId; Properties = Map.empty; TokenBindings = Map.empty; Content = Map.empty
                              Slots = Map.empty; Navigation = []; Annotations = []; Metadata = Map.empty }
                        ProjectOps.updatePage pageId (fun p -> insertInto position index node p.Nodes |> Result.map (fun nodes -> { p with Nodes = nodes })) project
                        |> lift target)
                    |> Result.bind ok
        | RemoveComponent(pageId, id) ->
            let target = sprintf "page:%s/component:%s" (Id.value pageId) (Id.value id)
            ProjectOps.updatePage pageId (fun p ->
                match detach id p.Nodes with
                | None, _ -> Error "The component does not exist."
                | Some _, rest -> Ok { p with Nodes = rest }) project
            |> lift target |> Result.bind ok
        | MoveComponent(pageId, id, position, index) ->
            let target = sprintf "page:%s/component:%s" (Id.value pageId) (Id.value id)
            ProjectOps.updatePage pageId (fun p ->
                match detach id p.Nodes with
                | None, _ -> Error "The component does not exist."
                | Some moving, rest ->
                    let intoOwnSubtree =
                        match position with
                        | Some { Parent = parent } -> parent = id || ProjectOps.componentIds [ moving ] |> List.contains parent
                        | None -> false
                    if intoOwnSubtree then Error "A component cannot move inside itself."
                    else checkSlot { p with Nodes = rest } position |> Result.bind (fun () -> insertInto position index moving rest) |> Result.map (fun nodes -> { p with Nodes = nodes })) project
            |> lift target |> Result.bind ok
        | SetComponentProperty(pageId, id, name, value) ->
            let target = sprintf "page:%s/component:%s" (Id.value pageId) (Id.value id)
            ProjectOps.updateComponent pageId id (fun node ->
                match Components.tryFind node.Component with
                | None -> Error(sprintf "'%s' is not an editable Forma component; its data is preserved as-is." node.Component)
                | Some contract ->
                    match contract.Properties |> List.tryFind (fst >> (=) name), value with
                    | None, _ -> Error(sprintf "'%s' has no property '%s' in its Forma contract." node.Component name)
                    | Some _, None -> Ok { node with Properties = node.Properties |> Map.remove name }
                    | Some(_, check), Some v -> check v |> Result.map (fun () -> { node with Properties = node.Properties |> Map.add name v }) |> Result.mapError (sprintf "%s %s" name)) project
            |> lift target |> Result.bind ok
        | SetComponentContent(pageId, id, key, text) ->
            let target = sprintf "page:%s/component:%s" (Id.value pageId) (Id.value id)
            ProjectOps.updateComponent pageId id (fun node ->
                match Components.tryFind node.Component with
                | Some contract when List.contains key contract.Content -> Ok { node with Content = node.Content |> Map.add key (JString text) }
                | Some _ -> Error(sprintf "'%s' has no content slot '%s'." node.Component key)
                | None -> Error(sprintf "'%s' is not an editable Forma component." node.Component)) project
            |> lift target |> Result.bind ok

    // -- Flow ----------------------------------------------------------------

    let private box target x y w h = Geometry.box x y w h |> Result.mapError (fun e -> [ Finding.blocker "geometry.invalid" Integrity target (Geometry.describe e) ])

    let private endpointValid (diagram: Diagram) (endpoint: Endpoint) =
        match diagram.Nodes |> List.tryFind (fun n -> n.Id = endpoint.Node) with
        | None -> Error(sprintf "Node '%s' does not exist." (Id.value endpoint.Node))
        | Some node ->
            match endpoint.Port with
            | Some port when not (node.Ports |> List.exists (fun p -> p.Id = port)) -> Error(sprintf "Node '%s' has no port '%s'." node.Label (Id.value port))
            | _ -> Ok node

    let private connectionLegal (profile: DiagramProfile) (diagram: Diagram) kind (source: Endpoint) (target: Endpoint) =
        match Profiles.edgeKind profile kind with
        | None -> Error(sprintf "Connector kind '%s' is not defined by the %s profile." kind profile.Name)
        | Some edgeKind ->
            match endpointValid diagram source, endpointValid diagram target with
            | Error e, _
            | _, Error e -> Error e
            | Ok s, Ok t ->
                match Profiles.nodeKind profile s.Kind, Profiles.nodeKind profile t.Kind with
                | Some sk, Some tk -> profile.CanConnect sk edgeKind tk
                | _ -> Error "An endpoint has a kind the profile does not define."

    let private laneLabel (diagram: Diagram) nodeId =
        diagram.Groups |> List.tryFind (fun g -> g.Kind = Lane && List.contains nodeId g.Members) |> Option.map _.Label

    let rec private flow (command: FlowCommand) (project: Project) =
        match command with
        | AddDiagram(id, name, profileRef) ->
            let target = diagramTarget id
            if (ProjectOps.tryDiagram id project).IsSome then reject "id.duplicate" target "A diagram with this id already exists."
            elif (Profiles.tryFind profileRef).IsNone then reject "profile.unavailable" target (sprintf "Profile %s@%s is not available." profileRef.Id profileRef.Version)
            elif System.String.IsNullOrWhiteSpace name then reject "diagram.name" target "A diagram needs a name."
            else
                let diagram =
                    { Id = id; Name = name; Profile = profileRef; Nodes = []; Edges = []; Groups = []
                      Display = { NodeFields = []; KindFields = Map.empty; EdgeFields = []; Missing = OmitMissing }; Metadata = Map.empty; References = [] }
                ok { project with Diagrams = project.Diagrams @ [ diagram ] }
        | RenameDiagram(id, name) ->
            if System.String.IsNullOrWhiteSpace name then reject "diagram.name" (diagramTarget id) "A diagram needs a name."
            else ProjectOps.updateDiagram id (fun d -> Ok { d with Name = name }) project |> lift (diagramTarget id) |> Result.bind ok
        | RemoveDiagram id ->
            let inbound = ProjectOps.inboundReferences (DiagramRef id) (function DiagramRef d | NodeRef(d, _) | EdgeRef(d, _) | GroupRef(d, _) -> d = id | _ -> false) project
            match ProjectOps.tryDiagram id project, inbound with
            | None, _ -> reject "diagram.missing" (diagramTarget id) "The diagram does not exist."
            | Some _, [] -> ok { project with Diagrams = project.Diagrams |> List.filter (fun d -> d.Id <> id) }
            | Some _, refs ->
                Error(refs |> List.map (fun (holder, r) ->
                    Finding.blocker "reference.inbound" ReferenceRule (ObjectRef.describe holder) (sprintf "Reference '%s' points to this diagram; remove or retarget it first." (Id.value r.Id))))
        | AddNode(diagramId, id, kind, label, x, y, w, h) ->
            let target = sprintf "%s/node:%s" (diagramTarget diagramId) (Id.value id)
            requireProfile project diagramId
            |> Result.bind (fun (diagram, profile) ->
                if diagram.Nodes |> List.exists (fun n -> n.Id = id) || diagram.Edges |> List.exists (fun e -> Id.value e.Id = Id.value id) || diagram.Groups |> List.exists (fun g -> Id.value g.Id = Id.value id) then
                    reject "id.duplicate" target "An element with this id already exists in the diagram."
                elif (Profiles.nodeKind profile kind).IsNone then reject "profile.node-kind" target (sprintf "Kind '%s' is not defined by the %s profile." kind profile.Name)
                else
                    box target x y w h
                    |> Result.bind (fun b ->
                        let node = { Id = id; Kind = kind; Label = label; Box = b; Ports = []; Locked = false; Metadata = Map.empty; Appearance = Appearance.none; References = [] }
                        ProjectOps.updateDiagram diagramId (fun d -> Ok { d with Nodes = d.Nodes @ [ node ] }) project |> lift target)
                    |> Result.bind ok)
        | MoveNodes(diagramId, moves) ->
            let target = diagramTarget diagramId
            moves
            |> List.fold
                (fun state (nodeId, dx, dy) ->
                    state
                    |> Result.bind (fun p ->
                        let nodeTarget = sprintf "%s/node:%s" target (Id.value nodeId)
                        match ProjectOps.tryNode diagramId nodeId p with
                        | None -> reject "node.missing" nodeTarget "The node does not exist."
                        | Some node when node.Locked -> reject "node.locked" nodeTarget (sprintf "'%s' is locked." node.Label)
                        | Some node ->
                            Geometry.translate dx dy node.Box
                            |> Result.mapError (fun e -> [ Finding.blocker "geometry.invalid" Integrity nodeTarget (Geometry.describe e) ])
                            |> Result.bind (fun b -> ProjectOps.updateNode diagramId nodeId (fun n -> Ok { n with Box = b }) p |> lift nodeTarget)))
                (Ok project)
            |> Result.bind ok
        | ResizeNode(diagramId, nodeId, w, h) ->
            let target = sprintf "%s/node:%s" (diagramTarget diagramId) (Id.value nodeId)
            match ProjectOps.tryNode diagramId nodeId project with
            | None -> reject "node.missing" target "The node does not exist."
            | Some node when node.Locked -> reject "node.locked" target (sprintf "'%s' is locked." node.Label)
            | Some node ->
                box target (float node.Box.Position.X) (float node.Box.Position.Y) w h
                |> Result.bind (fun b -> ProjectOps.updateNode diagramId nodeId (fun n -> Ok { n with Box = b }) project |> lift target)
                |> Result.bind ok
        | SetNodeLabel(diagramId, nodeId, label) ->
            let target = sprintf "%s/node:%s" (diagramTarget diagramId) (Id.value nodeId)
            ProjectOps.updateNode diagramId nodeId (fun n -> Ok { n with Label = label }) project |> lift target |> Result.bind ok
        | SetNodeKind(diagramId, nodeId, kind) ->
            let target = sprintf "%s/node:%s" (diagramTarget diagramId) (Id.value nodeId)
            requireProfile project diagramId
            |> Result.bind (fun (_, profile) ->
                match Profiles.nodeKind profile kind with
                | None -> reject "profile.node-kind" target (sprintf "Kind '%s' is not defined by the %s profile." kind profile.Name)
                | Some _ -> ProjectOps.updateNode diagramId nodeId (fun n -> Ok { n with Kind = kind }) project |> lift target |> Result.bind ok)
        | RemoveNode(diagramId, nodeId, policy) ->
            let target = sprintf "%s/node:%s" (diagramTarget diagramId) (Id.value nodeId)
            match ProjectOps.tryDiagram diagramId project, ProjectOps.tryNode diagramId nodeId project with
            | Some diagram, Some node ->
                let incident = diagram.Edges |> List.filter (fun e -> e.Source.Node = nodeId || e.Target.Node = nodeId)
                let inbound = ProjectOps.inboundReferences (NodeRef(diagramId, nodeId)) ((=) (NodeRef(diagramId, nodeId))) project
                match policy, incident, inbound with
                | _, _, (_ :: _ as refs) ->
                    Error(refs |> List.map (fun (holder, r) ->
                        Finding.blocker "reference.inbound" ReferenceRule (ObjectRef.describe holder) (sprintf "Reference '%s' points to '%s'; remove or retarget it first." (Id.value r.Id) node.Label)))
                | RejectIfConnected, (_ :: _), [] ->
                    Error(incident |> List.map (fun e ->
                        Finding.blocker "node.incident-edge" CommandRule (sprintf "%s/edge:%s" (diagramTarget diagramId) (Id.value e.Id))
                            (sprintf "Connector '%s' is attached to '%s'. Remove it, or delete the node together with its connectors." (Id.value e.Id) node.Label)))
                | _, _, [] ->
                    let removedIds = incident |> List.map _.Id |> Set.ofList
                    let updated =
                        { diagram with
                            Nodes = diagram.Nodes |> List.filter (fun n -> n.Id <> nodeId)
                            Edges = diagram.Edges |> List.filter (fun e -> not (removedIds.Contains e.Id))
                            Groups = diagram.Groups |> List.map (fun g -> { g with Members = g.Members |> List.filter ((<>) nodeId) }) }
                    ProjectOps.updateDiagram diagramId (fun _ -> Ok updated) project
                    |> lift target
                    |> Result.map (fun p ->
                        { Project = p
                          Obligations =
                            incident |> List.map (fun e ->
                                { Code = "edge.removed-with-node"; Target = sprintf "%s/edge:%s" (diagramTarget diagramId) (Id.value e.Id)
                                  Message = sprintf "Connector '%s' was removed with '%s'." (Id.value e.Id) node.Label }) })
            | _ -> reject "node.missing" target "The node does not exist."
        | Connect(diagramId, edgeId, kind, source, targetEndpoint, label) ->
            let target = sprintf "%s/edge:%s" (diagramTarget diagramId) (Id.value edgeId)
            requireProfile project diagramId
            |> Result.bind (fun (diagram, profile) ->
                if diagram.Edges |> List.exists (fun e -> e.Id = edgeId) || diagram.Nodes |> List.exists (fun n -> Id.value n.Id = Id.value edgeId) then
                    reject "id.duplicate" target "An element with this id already exists in the diagram."
                else
                    connectionLegal profile diagram kind source targetEndpoint
                    |> lift target
                    |> Result.bind (fun () ->
                        let edge =
                            { Id = edgeId; Kind = kind; Source = source; Target = targetEndpoint; Label = label; Routing = Orthogonal
                              Metadata = Map.empty; Appearance = Appearance.none; References = [] }
                        ProjectOps.updateDiagram diagramId (fun d -> Ok { d with Edges = d.Edges @ [ edge ] }) project |> lift target)
                    |> Result.bind ok)
        | Reconnect(diagramId, edgeId, source, targetEndpoint) ->
            let target = sprintf "%s/edge:%s" (diagramTarget diagramId) (Id.value edgeId)
            requireProfile project diagramId
            |> Result.bind (fun (diagram, profile) ->
                match ProjectOps.tryEdge diagramId edgeId project with
                | None -> reject "edge.missing" target "The connector does not exist."
                | Some edge ->
                    connectionLegal profile diagram edge.Kind source targetEndpoint
                    |> lift target
                    |> Result.bind (fun () -> ProjectOps.updateEdge diagramId edgeId (fun e -> Ok { e with Source = source; Target = targetEndpoint }) project |> lift target)
                    |> Result.bind ok)
        | SetEdgeLabel(diagramId, edgeId, label) ->
            let target = sprintf "%s/edge:%s" (diagramTarget diagramId) (Id.value edgeId)
            ProjectOps.updateEdge diagramId edgeId (fun e -> Ok { e with Label = label |> Option.filter (System.String.IsNullOrWhiteSpace >> not) }) project
            |> lift target |> Result.bind ok
        | SetEdgeRouting(diagramId, edgeId, routing) ->
            let target = sprintf "%s/edge:%s" (diagramTarget diagramId) (Id.value edgeId)
            let pointsValid =
                match routing with
                | Manual points -> points |> List.forall (fun p -> abs p.X <= int Geometry.coordinateLimit && abs p.Y <= int Geometry.coordinateLimit)
                | _ -> true
            if not pointsValid then reject "geometry.invalid" target "A routing point is outside the legal coordinate range."
            else ProjectOps.updateEdge diagramId edgeId (fun e -> Ok { e with Routing = routing }) project |> lift target |> Result.bind ok
        | RemoveEdge(diagramId, edgeId) ->
            let target = sprintf "%s/edge:%s" (diagramTarget diagramId) (Id.value edgeId)
            match ProjectOps.tryEdge diagramId edgeId project with
            | None -> reject "edge.missing" target "The connector does not exist."
            | Some _ -> ProjectOps.updateDiagram diagramId (fun d -> Ok { d with Edges = d.Edges |> List.filter (fun e -> e.Id <> edgeId) }) project |> lift target |> Result.bind ok
        | AddGroup(diagramId, groupId, kind, label, x, y, w, h) ->
            let target = sprintf "%s/group:%s" (diagramTarget diagramId) (Id.value groupId)
            requireProfile project diagramId
            |> Result.bind (fun (diagram, profile) ->
                if diagram.Groups |> List.exists (fun g -> g.Id = groupId) || diagram.Nodes |> List.exists (fun n -> Id.value n.Id = Id.value groupId) then
                    reject "id.duplicate" target "An element with this id already exists in the diagram."
                else
                    box target x y w h
                    |> Result.bind (fun b ->
                        let group =
                            { Id = groupId; Kind = kind; Label = label; Box = b; Members = []; Semantic = (kind = Lane && profile.SemanticLanes)
                              Metadata = Map.empty; Appearance = Appearance.none }
                        ProjectOps.updateDiagram diagramId (fun d -> Ok { d with Groups = d.Groups @ [ group ] }) project |> lift target)
                    |> Result.bind ok)
        | MoveGroup(diagramId, groupId, dx, dy) ->
            let target = sprintf "%s/group:%s" (diagramTarget diagramId) (Id.value groupId)
            match ProjectOps.tryGroup diagramId groupId project with
            | None -> reject "group.missing" target "The group does not exist."
            | Some group ->
                Geometry.translate dx dy group.Box
                |> Result.mapError (fun e -> [ Finding.blocker "geometry.invalid" Integrity target (Geometry.describe e) ])
                |> Result.bind (fun b ->
                    ProjectOps.updateGroup diagramId groupId (fun g -> Ok { g with Box = b }) project |> lift target)
                |> Result.bind (fun p ->
                    // Members move with their container in the same command.
                    flow (MoveNodes(diagramId, group.Members |> List.map (fun m -> m, dx, dy))) p)
        | AssignLane(diagramId, nodeId, laneId) ->
            let target = sprintf "%s/node:%s" (diagramTarget diagramId) (Id.value nodeId)
            match ProjectOps.tryDiagram diagramId project, ProjectOps.tryNode diagramId nodeId project with
            | Some diagram, Some node ->
                let lane = laneId |> Option.bind (fun l -> diagram.Groups |> List.tryFind (fun g -> g.Id = l))
                match laneId, lane with
                | Some l, None -> reject "group.missing" target (sprintf "Lane '%s' does not exist." (Id.value l))
                | _, Some g when g.Kind <> Lane -> reject "group.not-lane" target (sprintf "'%s' is not a lane." g.Label)
                | _ ->
                    let before = laneLabel diagram nodeId
                    let groups =
                        diagram.Groups
                        |> List.map (fun g ->
                            if g.Kind <> Lane then g
                            elif Some g.Id = laneId then { g with Members = (g.Members |> List.filter ((<>) nodeId)) @ [ nodeId ] }
                            else { g with Members = g.Members |> List.filter ((<>) nodeId) })
                    let after = lane |> Option.map _.Label
                    let semantic = lane |> Option.map _.Semantic |> Option.defaultValue false || diagram.Groups |> List.exists (fun g -> g.Kind = Lane && g.Semantic && Some g.Label = before)
                    ProjectOps.updateDiagram diagramId (fun d -> Ok { d with Groups = groups }) project
                    |> lift target
                    |> Result.map (fun p ->
                        { Project = p
                          Obligations =
                            if semantic && before <> after then
                                [ { Code = "lane.responsibility-changed"; Target = target
                                    Message = sprintf "Responsibility for '%s' changes from %s to %s." node.Label (defaultArg before "no lane") (defaultArg after "no lane") } ]
                            else [] })
            | _ -> reject "node.missing" target "The node does not exist."
        | SetGroupMembers(diagramId, groupId, members) ->
            let target = sprintf "%s/group:%s" (diagramTarget diagramId) (Id.value groupId)
            ProjectOps.updateGroup diagramId groupId (fun g -> Ok { g with Members = List.distinct members }) project |> lift target |> Result.bind ok
        | SetDisplay(diagramId, display) ->
            let target = diagramTarget diagramId
            let allFields = display.NodeFields @ display.EdgeFields @ (display.KindFields |> Map.toList |> List.collect snd)
            let unknown = allFields |> List.filter (fun k -> (ProjectOps.tryField k project).IsNone)
            match unknown with
            | key :: _ -> reject "metadata.field.unknown" target (sprintf "Field '%s' is not defined." (Id.value key))
            | [] ->
                let hidden =
                    allFields
                    |> List.distinct
                    |> List.choose (fun k -> ProjectOps.tryField k project)
                    |> List.filter (MetadataRules.allows Rendered >> not)
                ProjectOps.updateDiagram diagramId (fun d -> Ok { d with Display = display }) project
                |> lift target
                |> Result.map (fun p ->
                    { Project = p
                      Obligations =
                        hidden |> List.map (fun f ->
                            { Code = "disclosure.display.withheld"; Target = target
                              Message = sprintf "'%s' is not permitted in rendered output, so it shows only in the editor." f.Name }) })
        | AddReference(holder, reference) ->
            let target = ObjectRef.describe holder
            let add (refs: TypedReference list) =
                if refs |> List.exists (fun r -> r.Id = reference.Id) then Error "A reference with this id already exists." else Ok(refs @ [ reference ])
            let external =
                match reference.Target with
                | ExternalUrlTarget url -> MetadataRules.normalize UrlField (Url url) |> Result.map ignore
                | _ -> Ok()
            external
            |> Result.bind (fun () ->
                match holder with
                | NodeRef(d, n) -> ProjectOps.updateNode d n (fun x -> add x.References |> Result.map (fun r -> { x with References = r })) project
                | EdgeRef(d, e) -> ProjectOps.updateEdge d e (fun x -> add x.References |> Result.map (fun r -> { x with References = r })) project
                | DiagramRef d -> ProjectOps.updateDiagram d (fun x -> add x.References |> Result.map (fun r -> { x with References = r })) project
                | other -> Error(sprintf "%s cannot hold typed references yet." (ObjectRef.describe other)))
            |> lift target |> Result.bind ok
        | RemoveReference(holder, referenceId) ->
            let target = ObjectRef.describe holder
            let remove (refs: TypedReference list) = refs |> List.filter (fun r -> r.Id <> referenceId)
            (match holder with
             | NodeRef(d, n) -> ProjectOps.updateNode d n (fun x -> Ok { x with References = remove x.References }) project
             | EdgeRef(d, e) -> ProjectOps.updateEdge d e (fun x -> Ok { x with References = remove x.References }) project
             | DiagramRef d -> ProjectOps.updateDiagram d (fun x -> Ok { x with References = remove x.References }) project
             | other -> Error(sprintf "%s cannot hold typed references." (ObjectRef.describe other)))
            |> lift target |> Result.bind ok

    // -- Metadata ------------------------------------------------------------

    let private fieldTarget (key: FieldKey) = sprintf "field:%s" (Id.value key)

    let private fieldUses (key: FieldKey) (project: Project) =
        let values =
            ProjectOps.allObjects project
            |> List.filter (fun r -> ProjectOps.metadataOf r project |> Option.exists (Map.containsKey key))
            |> List.map (fun r -> ObjectRef.describe r, "has a value")
        let mappings = project.Mappings |> List.filter (fun m -> m.Field = key) |> List.map (fun m -> sprintf "mapping:%s" (Id.value m.Id), "reads this field")
        let displays =
            project.Diagrams
            |> List.filter (fun d -> List.contains key d.Display.NodeFields || List.contains key d.Display.EdgeFields || d.Display.KindFields |> Map.exists (fun _ keys -> List.contains key keys))
            |> List.map (fun d -> diagramTarget d.Id, "displays this field")
        values, mappings, displays

    let private metadata (command: MetadataCommand) (project: Project) =
        match command with
        | DefineField definition ->
            let target = fieldTarget definition.Key
            if (ProjectOps.tryField definition.Key project).IsSome then reject "id.duplicate" target "A field with this key already exists."
            elif System.String.IsNullOrWhiteSpace definition.Name then reject "field.name" target "A field needs a display name."
            else
                match definition.Default |> Option.map (MetadataRules.normalize definition.Type) with
                | Some(Error reason) -> reject "field.default" target (sprintf "Default value: %s." reason)
                | normalized ->
                    let definition = { definition with Default = normalized |> Option.bind Result.toOption }
                    ok { project with Fields = project.Fields @ [ definition ] }
        | RenameField(key, name) ->
            // Display names are projections; the stable key and every use are unchanged (FDA-1184).
            if System.String.IsNullOrWhiteSpace name then reject "field.name" (fieldTarget key) "A field needs a display name."
            else
                match ProjectOps.tryField key project with
                | None -> reject "field.missing" (fieldTarget key) "The field does not exist."
                | Some _ -> ok { project with Fields = project.Fields |> List.map (fun f -> if f.Key = key then { f with Name = name } else f) }
        | SetFieldDisclosure(key, disclosure) ->
            match ProjectOps.tryField key project with
            | None -> reject "field.missing" (fieldTarget key) "The field does not exist."
            | Some field ->
                let updated = { project with Fields = project.Fields |> List.map (fun f -> if f.Key = key then { f with Disclosure = disclosure } else f) }
                // FDA-1263: revalidate every dependent mapping and display configuration.
                let mappingObligations =
                    if field.Disclosure.DerivedPresentation && not disclosure.DerivedPresentation then
                        project.Mappings
                        |> List.filter (fun m -> m.Field = key)
                        |> List.map (fun m ->
                            { Code = "disclosure.mapping.withheld"; Target = sprintf "mapping:%s" (Id.value m.Id)
                              Message = sprintf "Mapping '%s' no longer affects rendered or exported output; '%s' does not allow derived presentation." m.Name field.Name })
                    else []
                let displayObligations =
                    if field.Disclosure.Scopes.Contains Rendered && not (disclosure.Scopes.Contains Rendered) then
                        project.Diagrams
                        |> List.filter (fun d -> List.contains key d.Display.NodeFields || List.contains key d.Display.EdgeFields)
                        |> List.map (fun d ->
                            { Code = "disclosure.display.withheld"; Target = diagramTarget d.Id
                              Message = sprintf "'%s' is no longer rendered in '%s'." field.Name d.Name })
                    else []
                Ok { Project = updated; Obligations = mappingObligations @ displayObligations }
        | RemoveField(key, policy) ->
            let target = fieldTarget key
            match ProjectOps.tryField key project with
            | None -> reject "field.missing" target "The field does not exist."
            | Some _ ->
                let values, mappings, displays = fieldUses key project
                let blocking = mappings @ (if policy = BlockFieldIfUsed then values @ displays else [])
                match blocking with
                | _ :: _ -> Error(blocking |> List.map (fun (t, why) -> Finding.blocker "field.in-use" MetadataRule t (sprintf "This object %s; the field cannot be removed until it is migrated." why)))
                | [] ->
                    let withoutValues =
                        ProjectOps.allObjects project
                        |> List.fold (fun p r -> ProjectOps.updateMetadata r (Map.remove key) p |> Result.defaultValue p) project
                    let withoutDisplay =
                        { withoutValues with
                            Diagrams =
                                withoutValues.Diagrams
                                |> List.map (fun d ->
                                    { d with
                                        Display =
                                            { d.Display with
                                                NodeFields = d.Display.NodeFields |> List.filter ((<>) key)
                                                KindFields = d.Display.KindFields |> Map.map (fun _ keys -> keys |> List.filter ((<>) key))
                                                EdgeFields = d.Display.EdgeFields |> List.filter ((<>) key) } })
                            Fields = withoutValues.Fields |> List.filter (fun f -> f.Key <> key) }
                    Ok
                        { Project = withoutDisplay
                          Obligations = values @ displays |> List.map (fun (t, why) -> { Code = "field.value-removed"; Target = t; Message = sprintf "Removed with the field (it %s)." why }) }
        | SetValue(targets, key, stored) ->
            match ProjectOps.tryField key project with
            | None -> reject "field.missing" (fieldTarget key) "The field does not exist."
            | Some field ->
                let normalized =
                    match stored with
                    | Explicit v -> MetadataRules.normalize field.Type v |> Result.map Explicit
                    | SourceBound(v, binding) -> MetadataRules.normalize field.Type v |> Result.map (fun n -> SourceBound(n, binding))
                    | other -> Ok other
                match normalized with
                | Error reason -> reject "metadata.value.invalid" (fieldTarget key) (sprintf "%s: %s." field.Name reason)
                | Ok value ->
                    // Validate every target before mutating any (atomic bulk edit).
                    let problems =
                        targets
                        |> List.choose (fun r ->
                            if not (ProjectOps.exists r project) then Some(Finding.blocker "object.missing" CommandRule (ObjectRef.describe r) "The object does not exist.")
                            elif not (MetadataRules.applies field (ObjectRef.kind r)) then
                                Some(Finding.blocker "metadata.field.not-applicable" MetadataRule (ObjectRef.describe r) (sprintf "'%s' does not apply to this kind of object." field.Name))
                            else None)
                    if not (List.isEmpty problems) then Error problems
                    else
                        targets
                        |> List.fold (fun state r -> state |> Result.bind (ProjectOps.updateMetadata r (Map.add key value))) (Ok project)
                        |> lift (fieldTarget key)
                        |> Result.bind ok
        | ClearValue(targets, key) ->
            targets
            |> List.fold (fun state r -> state |> Result.bind (ProjectOps.updateMetadata r (Map.remove key))) (Ok project)
            |> lift (fieldTarget key)
            |> Result.bind ok
        | MaterializeValue(targets, key) ->
            match ProjectOps.tryField key project with
            | None -> reject "field.missing" (fieldTarget key) "The field does not exist."
            | Some field ->
                targets
                |> List.fold
                    (fun state r ->
                        state
                        |> Result.bind (fun p ->
                            match ProjectOps.resolveField field r p with
                            | ResolvedDefault v
                            | ResolvedDerived(v, _)
                            | ResolvedSourceBound(v, _) -> ProjectOps.updateMetadata r (Map.add key (Explicit v)) p
                            | ResolvedExplicit _ -> Ok p
                            | _ -> Error(sprintf "%s has no default, derived or source-bound value to materialize on %s." field.Name (ObjectRef.describe r))))
                    (Ok project)
                |> lift (fieldTarget key)
                |> Result.bind ok

    // -- Appearance ------------------------------------------------------------

    let private colorUsesPalette (id: PaletteSlotId) color =
        match color with
        | Some(PaletteColor p) when p = id -> true
        | _ -> false

    let private mapPaletteColor (id: PaletteSlotId) (replacement: ColorRef) (appearance: Appearance) =
        let swap color = if colorUsesPalette id color then Some replacement else color
        { appearance with
            Fill = swap appearance.Fill
            Stroke = swap appearance.Stroke
            Accent = swap appearance.Accent
            Foreground = swap appearance.Foreground
            ConnectorStroke = swap appearance.ConnectorStroke }

    let private appearanceUsesPalette id (appearance: Appearance) =
        Appearance.colors appearance |> List.exists (fun c -> colorUsesPalette id (Some c))

    let private outcomeAppearances (mapping: PresentationMapping) =
        (mapping.Rules |> List.map _.Outcome)
        @ ([ mapping.Fallbacks.Missing; mapping.Fallbacks.Unknown; mapping.Fallbacks.Unavailable; mapping.Fallbacks.Invalid; mapping.Fallbacks.Unmapped ]
           |> List.choose (function Apply(o, _) -> Some o | NoMapping -> None))

    let private paletteUses id (project: Project) =
        let objects =
            ProjectOps.allObjects project
            |> List.filter (fun r -> ProjectOps.appearanceOf r project |> Option.exists (fun a -> appearanceUsesPalette id a.Overrides))
            |> List.map ObjectRef.describe
        let styles = project.Styles |> List.filter (fun s -> appearanceUsesPalette id s.Appearance) |> List.map (fun s -> sprintf "style:%s" (Id.value s.Id))
        let mappings =
            project.Mappings
            |> List.filter (fun m -> outcomeAppearances m |> List.exists (function UseAppearance a -> appearanceUsesPalette id a | _ -> false))
            |> List.map (fun m -> sprintf "mapping:%s" (Id.value m.Id))
        objects @ styles @ mappings

    /// Replaces a palette reference everywhere it appears.
    let private rewritePalette id replacement (project: Project) =
        let rewriteOutcome outcome =
            match outcome with
            | UseAppearance a -> UseAppearance(mapPaletteColor id replacement a)
            | other -> other
        let rewriteFallback f =
            match f with
            | Apply(o, legend) -> Apply(rewriteOutcome o, legend)
            | NoMapping -> NoMapping
        let objectsRewritten =
            ProjectOps.allObjects project
            |> List.fold
                (fun p r ->
                    match ProjectOps.appearanceOf r p with
                    | Some _ -> ProjectOps.updateAppearance r (fun a -> { a with Overrides = mapPaletteColor id replacement a.Overrides }) p |> Result.defaultValue p
                    | None -> p)
                project
        { objectsRewritten with
            Styles = objectsRewritten.Styles |> List.map (fun s -> { s with Appearance = mapPaletteColor id replacement s.Appearance })
            Mappings =
                objectsRewritten.Mappings
                |> List.map (fun m ->
                    { m with
                        Rules = m.Rules |> List.map (fun r -> { r with Outcome = rewriteOutcome r.Outcome })
                        Fallbacks =
                            { Missing = rewriteFallback m.Fallbacks.Missing
                              Unknown = rewriteFallback m.Fallbacks.Unknown
                              Unavailable = rewriteFallback m.Fallbacks.Unavailable
                              Invalid = rewriteFallback m.Fallbacks.Invalid
                              Unmapped = rewriteFallback m.Fallbacks.Unmapped } }) }

    let private styleUses (id: StyleId) (project: Project) =
        let objects =
            ProjectOps.allObjects project
            |> List.filter (fun r -> ProjectOps.appearanceOf r project |> Option.exists (fun a -> a.Style = Some id))
        let mappings =
            project.Mappings |> List.filter (fun m -> outcomeAppearances m |> List.contains (UseStyle id))
        objects, mappings

    /// Materializes a style's properties under the object's own overrides.
    let private detachFrom (style: AppearanceStyle option) (objectAppearance: ObjectAppearance) =
        let merged =
            match style with
            | None -> objectAppearance.Overrides
            | Some s ->
                Appearance.properties
                |> List.fold (fun acc property -> if Appearance.isSet property acc then acc else Appearance.copyProperty property s.Appearance acc) objectAppearance.Overrides
        { Style = None; Overrides = merged }

    let private checkColors (project: Project) target (appearance: Appearance) =
        Appearance.colors appearance
        |> List.tryPick (function PaletteColor id when (ProjectOps.tryPalette id project).IsNone -> Some id | _ -> None)
        |> function
            | Some id -> reject "palette.missing" target (sprintf "Palette slot '%s' does not exist." (Id.value id))
            | None ->
                match appearance.ConnectorWidth with
                | Some w when w < 1 || w > 8 -> reject "appearance.connector-width" target "Connector width must be from 1 to 8."
                | _ -> Ok()

    let private checkMapping (project: Project) (mapping: PresentationMapping) =
        let target = sprintf "mapping:%s" (Id.value mapping.Id)
        match ProjectOps.tryField mapping.Field project with
        | None -> reject "mapping.field.missing" target (sprintf "Field '%s' does not exist." (Id.value mapping.Field))
        | Some field when not field.Disclosure.DerivedPresentation ->
            // FDA-1262: a mapping may only read a field that permits derived presentation.
            reject "disclosure.mapping.prohibited" target
                (sprintf "'%s' does not allow derived presentation, so it cannot drive appearance (field -> mapping -> rendered object would disclose it)." field.Name)
        | Some _ ->
            outcomeAppearances mapping
            |> List.fold
                (fun state outcome ->
                    state
                    |> Result.bind (fun () ->
                        match outcome with
                        | UseStyle s when (ProjectOps.tryStyle s project).IsNone -> reject "mapping.style.missing" target (sprintf "Style '%s' does not exist." (Id.value s))
                        | UseStyle _ -> Ok()
                        | UseAppearance a -> checkColors project target a))
                (Ok())

    let private appearance (command: AppearanceCommand) (project: Project) =
        match command with
        | AddPaletteSlot slot ->
            let target = sprintf "palette:%s" (Id.value slot.Id)
            if (ProjectOps.tryPalette slot.Id project).IsSome then reject "id.duplicate" target "A palette slot with this id already exists."
            elif System.String.IsNullOrWhiteSpace slot.Name then reject "palette.name" target "A palette slot needs a name."
            else ok { project with Palette = project.Palette @ [ slot ] }
        | RenamePaletteSlot(id, name) ->
            match ProjectOps.tryPalette id project with
            | None -> reject "palette.missing" (sprintf "palette:%s" (Id.value id)) "The palette slot does not exist."
            | Some _ when System.String.IsNullOrWhiteSpace name -> reject "palette.name" (sprintf "palette:%s" (Id.value id)) "A palette slot needs a name."
            | Some _ -> ok { project with Palette = project.Palette |> List.map (fun p -> if p.Id = id then { p with Name = name } else p) }
        | SetPaletteValue(id, value) ->
            match ProjectOps.tryPalette id project with
            | None -> reject "palette.missing" (sprintf "palette:%s" (Id.value id)) "The palette slot does not exist."
            | Some _ -> ok { project with Palette = project.Palette |> List.map (fun p -> if p.Id = id then { p with Value = value } else p) }
        | MovePaletteSlot(id, index) ->
            match ProjectOps.tryPalette id project with
            | None -> reject "palette.missing" (sprintf "palette:%s" (Id.value id)) "The palette slot does not exist."
            | Some slot -> ok { project with Palette = project.Palette |> List.filter (fun p -> p.Id <> id) |> insertAt index slot }
        | RemovePaletteSlot(id, policy) ->
            let target = sprintf "palette:%s" (Id.value id)
            match ProjectOps.tryPalette id project with
            | None -> reject "palette.missing" target "The palette slot does not exist."
            | Some slot ->
                let uses = paletteUses id project
                let without (p: Project) = { p with Palette = p.Palette |> List.filter (fun s -> s.Id <> id) }
                match policy, uses with
                | _, [] -> ok (without project)
                | BlockPaletteIfUsed, _ ->
                    Error(uses |> List.map (fun u -> Finding.blocker "palette.in-use" AppearanceRule u (sprintf "Uses palette slot '%s'; reassign or materialize it first (%d uses)." slot.Name uses.Length)))
                | ReassignPalette other, _ when other = id || (ProjectOps.tryPalette other project).IsNone ->
                    reject "palette.missing" target "Reassign to a different, existing palette slot."
                | ReassignPalette other, _ ->
                    Ok { Project = project |> rewritePalette id (PaletteColor other) |> without
                         Obligations = uses |> List.map (fun u -> { Code = "palette.reassigned"; Target = u; Message = sprintf "Now uses palette slot '%s'." (Id.value other) }) }
                | MaterializePalette, _ ->
                    let literal = match slot.Value with PaletteToken t -> TokenColor t | PaletteLiteral h -> LiteralColor h
                    Ok { Project = project |> rewritePalette id literal |> without
                         Obligations = uses |> List.map (fun u -> { Code = "palette.materialized"; Target = u; Message = sprintf "Now uses the value of '%s' directly." slot.Name }) }
        | DefineStyle style ->
            let target = sprintf "style:%s" (Id.value style.Id)
            if (ProjectOps.tryStyle style.Id project).IsSome then reject "id.duplicate" target "A style with this id already exists."
            else checkColors project target style.Appearance |> Result.bind (fun () -> ok { project with Styles = project.Styles @ [ { style with Revision = max 1 style.Revision } ] })
        | UpdateStyle(id, appearance) ->
            let target = sprintf "style:%s" (Id.value id)
            match ProjectOps.tryStyle id project with
            | None -> reject "style.missing" target "The style does not exist."
            | Some _ ->
                checkColors project target appearance
                |> Result.bind (fun () ->
                    // One definition change; referencing objects are not rewritten (FDA-935/936).
                    ok { project with Styles = project.Styles |> List.map (fun s -> if s.Id = id then { s with Appearance = appearance; Revision = s.Revision + 1 } else s) })
        | RenameStyle(id, name) ->
            match ProjectOps.tryStyle id project with
            | None -> reject "style.missing" (sprintf "style:%s" (Id.value id)) "The style does not exist."
            | Some _ -> ok { project with Styles = project.Styles |> List.map (fun s -> if s.Id = id then { s with Name = name } else s) }
        | RemoveStyle(id, policy) ->
            let target = sprintf "style:%s" (Id.value id)
            match ProjectOps.tryStyle id project with
            | None -> reject "style.missing" target "The style does not exist."
            | Some style ->
                let objects, mappings = styleUses id project
                let without (p: Project) = { p with Styles = p.Styles |> List.filter (fun s -> s.Id <> id) }
                match mappings, policy, objects with
                | _ :: _, _, _ ->
                    Error(mappings |> List.map (fun m -> Finding.blocker "style.in-use" AppearanceRule (sprintf "mapping:%s" (Id.value m.Id)) (sprintf "Mapping '%s' applies this style." m.Name)))
                | [], _, [] -> ok (without project)
                | [], BlockStyleIfUsed, _ ->
                    Error(objects |> List.map (fun r -> Finding.blocker "style.in-use" AppearanceRule (ObjectRef.describe r) (sprintf "Uses style '%s'; detach or reassign it first." style.Name)))
                | [], DetachAndMaterialize, _ ->
                    objects
                    |> List.fold (fun state r -> state |> Result.bind (ProjectOps.updateAppearance r (detachFrom (Some style)))) (Ok project)
                    |> lift target
                    |> Result.map (fun p ->
                        { Project = without p
                          Obligations = objects |> List.map (fun r -> { Code = "style.detached"; Target = ObjectRef.describe r; Message = sprintf "Detached from '%s'; its appearance is now local." style.Name }) })
        | ApplyStyle(targets, styleId) ->
            let problems =
                targets
                |> List.choose (fun r ->
                    match styleId |> Option.map (fun s -> s, ProjectOps.tryStyle s project) with
                    | Some(s, None) -> Some(Finding.blocker "style.missing" AppearanceRule (ObjectRef.describe r) (sprintf "Style '%s' does not exist." (Id.value s)))
                    | Some(_, Some style) when not (style.Targets.Contains(ObjectRef.kind r)) ->
                        Some(Finding.blocker "style.target" AppearanceRule (ObjectRef.describe r) (sprintf "Style '%s' does not apply to this kind of object." style.Name))
                    | _ -> if ProjectOps.appearanceOf r project |> Option.isNone then Some(Finding.blocker "object.missing" CommandRule (ObjectRef.describe r) "The object cannot carry a style.") else None)
            if not (List.isEmpty problems) then Error problems
            else
                targets
                |> List.fold (fun state r -> state |> Result.bind (ProjectOps.updateAppearance r (fun a -> { a with Style = styleId }))) (Ok project)
                |> lift "style" |> Result.bind ok
        | SetOverride(targets, overrides) ->
            checkColors project "appearance" overrides
            |> Result.bind (fun () ->
                targets
                |> List.fold
                    (fun state r ->
                        state
                        |> Result.bind (
                            ProjectOps.updateAppearance r (fun a ->
                                { a with
                                    Overrides =
                                        Appearance.properties
                                        |> List.fold (fun acc property -> if Appearance.isSet property overrides then Appearance.copyProperty property overrides acc else acc) a.Overrides })))
                    (Ok project)
                |> lift "appearance")
            |> Result.bind ok
        | ResetOverride(targets, properties) ->
            targets
            |> List.fold
                (fun state r ->
                    state
                    |> Result.bind (
                        ProjectOps.updateAppearance r (fun a ->
                            { a with Overrides = properties |> List.fold (fun acc property -> Appearance.copyProperty property Appearance.empty acc) a.Overrides })))
                (Ok project)
            |> lift "appearance" |> Result.bind ok
        | DetachStyle targets ->
            targets
            |> List.fold
                (fun state r ->
                    state
                    |> Result.bind (fun p ->
                        match ProjectOps.appearanceOf r p with
                        | None -> Error(sprintf "%s has no appearance." (ObjectRef.describe r))
                        | Some a -> ProjectOps.updateAppearance r (detachFrom (a.Style |> Option.bind (fun s -> ProjectOps.tryStyle s p))) p))
                (Ok project)
            |> lift "appearance" |> Result.bind ok
        | DefineMapping mapping ->
            if (ProjectOps.tryMapping mapping.Id project).IsSome then reject "id.duplicate" (sprintf "mapping:%s" (Id.value mapping.Id)) "A mapping with this id already exists."
            else checkMapping project mapping |> Result.bind (fun () -> ok { project with Mappings = project.Mappings @ [ mapping ] })
        | UpdateMapping mapping ->
            match ProjectOps.tryMapping mapping.Id project with
            | None -> reject "mapping.missing" (sprintf "mapping:%s" (Id.value mapping.Id)) "The mapping does not exist."
            | Some _ -> checkMapping project mapping |> Result.bind (fun () -> ok { project with Mappings = project.Mappings |> List.map (fun m -> if m.Id = mapping.Id then mapping else m) })
        | RemoveMapping id ->
            match ProjectOps.tryMapping id project with
            | None -> reject "mapping.missing" (sprintf "mapping:%s" (Id.value id)) "The mapping does not exist."
            | Some _ -> ok { project with Mappings = project.Mappings |> List.filter (fun m -> m.Id <> id) }
        | MoveMapping(id, index) ->
            match ProjectOps.tryMapping id project with
            | None -> reject "mapping.missing" (sprintf "mapping:%s" (Id.value id)) "The mapping does not exist."
            | Some m -> ok { project with Mappings = project.Mappings |> List.filter (fun x -> x.Id <> id) |> insertAt index m }

    let rec private apply (command: Command) (project: Project) : Result<Applied, Finding list> =
        match command with
        | Layout c -> layout c project
        | Flow c -> flow c project
        | MetadataCmd c -> metadata c project
        | AppearanceCmd c -> appearance c project
        | Batch(_, commands) ->
            commands
            |> List.fold
                (fun state c -> state |> Result.bind (fun (a: Applied) -> apply c a.Project |> Result.map (fun next -> { next with Obligations = a.Obligations @ next.Obligations })))
                (Ok { Project = project; Obligations = [] })

    /// Executes a command against the current state. On success the successor state
    /// introduces no new integrity blockers; on failure the prior state is unchanged
    /// because nothing is mutated (DOCUMENT-MODEL "Shared editor commands").
    let execute (command: Command) (project: Project) : Result<Applied, Finding list> =
        let before = Validation.blockers project |> Set.ofList
        apply command project
        |> Result.bind (fun applied ->
            let introduced = Validation.blockers applied.Project |> List.filter (fun f -> not (before.Contains f))
            if List.isEmpty introduced then Ok applied else Error introduced)
