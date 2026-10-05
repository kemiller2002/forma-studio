namespace FormaStudio.Engine

/// Flow commands: diagrams, nodes, connectors, groups, lanes and references.
/// Internal: `Commands.execute` is the only public execution authority.
module internal FlowCommands =
    open CommandSupport

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

    let rec apply (command: FlowCommand) (project: Project) =
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
                    apply (MoveNodes(diagramId, group.Members |> List.map (fun m -> m, dx, dy))) p)
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
