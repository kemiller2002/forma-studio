namespace FormaStudio.Engine

/// Pure lookups and immutable updates over the project tree. Every update returns
/// a new project or an error; nothing is mutated in place.
[<RequireQualifiedAccess>]
module ProjectOps =
    let private missing reference =
        Error(sprintf "%s does not exist" (ObjectRef.describe reference))

    // -- component trees ---------------------------------------------------

    let rec private tryFindComponent (id: ComponentNodeId) (nodes: ComponentNode list) =
        nodes
        |> List.tryPick (fun node ->
            if node.Id = id then Some node
            else node.Slots |> Map.toList |> List.tryPick (snd >> tryFindComponent id))

    let rec private mapComponent (id: ComponentNodeId) (f: ComponentNode -> ComponentNode) (nodes: ComponentNode list) =
        nodes
        |> List.map (fun node ->
            if node.Id = id then f node
            else { node with Slots = node.Slots |> Map.map (fun _ children -> mapComponent id f children) })

    let rec componentIds (nodes: ComponentNode list) =
        nodes |> List.collect (fun n -> n.Id :: (n.Slots |> Map.toList |> List.collect (snd >> componentIds)))

    let tryPage (id: PageId) (project: Project) = project.Pages |> List.tryFind (fun p -> p.Id = id)
    let tryDiagram (id: DiagramId) (project: Project) = project.Diagrams |> List.tryFind (fun d -> d.Id = id)

    let tryComponent (page: PageId) (id: ComponentNodeId) (project: Project) =
        tryPage page project |> Option.bind (fun p -> tryFindComponent id p.Nodes)

    let tryNode (diagram: DiagramId) (id: NodeId) (project: Project) =
        tryDiagram diagram project |> Option.bind (fun d -> d.Nodes |> List.tryFind (fun n -> n.Id = id))

    let tryEdge (diagram: DiagramId) (id: EdgeId) (project: Project) =
        tryDiagram diagram project |> Option.bind (fun d -> d.Edges |> List.tryFind (fun e -> e.Id = id))

    let tryGroup (diagram: DiagramId) (id: GroupId) (project: Project) =
        tryDiagram diagram project |> Option.bind (fun d -> d.Groups |> List.tryFind (fun g -> g.Id = id))

    let exists reference project =
        match reference with
        | PageRef p -> (tryPage p project).IsSome
        | ComponentRef(p, c) -> (tryComponent p c project).IsSome
        | DiagramRef d -> (tryDiagram d project).IsSome
        | NodeRef(d, n) -> (tryNode d n project).IsSome
        | EdgeRef(d, e) -> (tryEdge d e project).IsSome
        | GroupRef(d, g) -> (tryGroup d g project).IsSome

    /// A Layout component's icon: its `icon` property (#33), read without rejection.
    let componentIcon (node: ComponentNode) = node.Properties |> Map.tryFind "icon" |> Option.map IconRef.ofJson

    /// The icon stored on a component or diagram node (None for objects that carry none).
    let iconOf reference project : IconRef option =
        match reference with
        | ComponentRef(p, c) -> tryComponent p c project |> Option.bind componentIcon
        | NodeRef(d, n) -> tryNode d n project |> Option.bind _.Icon
        | _ -> None

    let updatePage (id: PageId) (f: Page -> Result<Page, string>) (project: Project) =
        match tryPage id project with
        | None -> missing (PageRef id)
        | Some page ->
            f page |> Result.map (fun updated -> { project with Pages = project.Pages |> List.map (fun p -> if p.Id = id then updated else p) })

    let updateDiagram (id: DiagramId) (f: Diagram -> Result<Diagram, string>) (project: Project) =
        match tryDiagram id project with
        | None -> missing (DiagramRef id)
        | Some diagram ->
            f diagram
            |> Result.map (fun updated -> { project with Diagrams = project.Diagrams |> List.map (fun d -> if d.Id = id then updated else d) })

    let updateComponent (page: PageId) (id: ComponentNodeId) (f: ComponentNode -> Result<ComponentNode, string>) (project: Project) =
        match tryComponent page id project with
        | None -> missing (ComponentRef(page, id))
        | Some node -> f node |> Result.bind (fun updated -> updatePage page (fun p -> Ok { p with Nodes = mapComponent id (fun _ -> updated) p.Nodes }) project)

    let updateNode (diagram: DiagramId) (id: NodeId) (f: DiagramNode -> Result<DiagramNode, string>) (project: Project) =
        match tryNode diagram id project with
        | None -> missing (NodeRef(diagram, id))
        | Some node ->
            f node |> Result.bind (fun updated -> updateDiagram diagram (fun d -> Ok { d with Nodes = d.Nodes |> List.map (fun n -> if n.Id = id then updated else n) }) project)

    let updateEdge (diagram: DiagramId) (id: EdgeId) (f: DiagramEdge -> Result<DiagramEdge, string>) (project: Project) =
        match tryEdge diagram id project with
        | None -> missing (EdgeRef(diagram, id))
        | Some edge ->
            f edge |> Result.bind (fun updated -> updateDiagram diagram (fun d -> Ok { d with Edges = d.Edges |> List.map (fun e -> if e.Id = id then updated else e) }) project)

    let updateGroup (diagram: DiagramId) (id: GroupId) (f: DiagramGroup -> Result<DiagramGroup, string>) (project: Project) =
        match tryGroup diagram id project with
        | None -> missing (GroupRef(diagram, id))
        | Some group ->
            f group |> Result.bind (fun updated -> updateDiagram diagram (fun d -> Ok { d with Groups = d.Groups |> List.map (fun g -> if g.Id = id then updated else g) }) project)

    // -- metadata and appearance, addressed uniformly -----------------------

    let metadataOf reference project : Metadata option =
        match reference with
        | PageRef p -> tryPage p project |> Option.map _.Metadata
        | ComponentRef(p, c) -> tryComponent p c project |> Option.map _.Metadata
        | DiagramRef d -> tryDiagram d project |> Option.map _.Metadata
        | NodeRef(d, n) -> tryNode d n project |> Option.map _.Metadata
        | EdgeRef(d, e) -> tryEdge d e project |> Option.map _.Metadata
        | GroupRef(d, g) -> tryGroup d g project |> Option.map _.Metadata

    let updateMetadata reference (f: Metadata -> Metadata) project =
        match reference with
        | PageRef p -> updatePage p (fun x -> Ok { x with Metadata = f x.Metadata }) project
        | ComponentRef(p, c) -> updateComponent p c (fun x -> Ok { x with Metadata = f x.Metadata }) project
        | DiagramRef d -> updateDiagram d (fun x -> Ok { x with Metadata = f x.Metadata }) project
        | NodeRef(d, n) -> updateNode d n (fun x -> Ok { x with Metadata = f x.Metadata }) project
        | EdgeRef(d, e) -> updateEdge d e (fun x -> Ok { x with Metadata = f x.Metadata }) project
        | GroupRef(d, g) -> updateGroup d g (fun x -> Ok { x with Metadata = f x.Metadata }) project

    let appearanceOf reference project : ObjectAppearance option =
        match reference with
        | NodeRef(d, n) -> tryNode d n project |> Option.map _.Appearance
        | EdgeRef(d, e) -> tryEdge d e project |> Option.map _.Appearance
        | GroupRef(d, g) -> tryGroup d g project |> Option.map _.Appearance
        | _ -> None

    let updateAppearance reference (f: ObjectAppearance -> ObjectAppearance) project =
        match reference with
        | NodeRef(d, n) -> updateNode d n (fun x -> Ok { x with Appearance = f x.Appearance }) project
        | EdgeRef(d, e) -> updateEdge d e (fun x -> Ok { x with Appearance = f x.Appearance }) project
        | GroupRef(d, g) -> updateGroup d g (fun x -> Ok { x with Appearance = f x.Appearance }) project
        | other -> Error(sprintf "%s cannot carry diagram appearance" (ObjectRef.describe other))

    /// Every addressable object in canonical order: pages (and their component
    /// trees), then diagrams with their groups, nodes and edges.
    let allObjects (project: Project) =
        let pages =
            project.Pages
            |> List.collect (fun p -> PageRef p.Id :: (componentIds p.Nodes |> List.map (fun c -> ComponentRef(p.Id, c))))
        let diagrams =
            project.Diagrams
            |> List.collect (fun d ->
                DiagramRef d.Id
                :: (d.Groups |> List.map (fun g -> GroupRef(d.Id, g.Id)))
                @ (d.Nodes |> List.map (fun n -> NodeRef(d.Id, n.Id)))
                @ (d.Edges |> List.map (fun e -> EdgeRef(d.Id, e.Id))))
        pages @ diagrams

    let tryField (key: FieldKey) (project: Project) = project.Fields |> List.tryFind (fun f -> f.Key = key)
    let tryPalette (id: PaletteSlotId) (project: Project) = project.Palette |> List.tryFind (fun p -> p.Id = id)
    let tryStyle (id: StyleId) (project: Project) = project.Styles |> List.tryFind (fun s -> s.Id = id)
    let tryMapping (id: MappingId) (project: Project) = project.Mappings |> List.tryFind (fun m -> m.Id = id)

    // -- derived metadata ---------------------------------------------------

    /// The value an explicit derivation rule produces for an object, if any.
    /// Membership derivation reads the label of the containing group of a kind,
    /// so lane ownership is not duplicated into every node (FDA-921..924).
    let derivedValue (definition: FieldDefinition) reference (project: Project) =
        match definition.Derivation, reference with
        | Some(FromMembership kind), NodeRef(diagramId, nodeId) ->
            tryDiagram diagramId project
            |> Option.bind (fun d -> d.Groups |> List.tryFind (fun g -> g.Kind = kind && List.contains nodeId g.Members))
            |> Option.bind (fun group ->
                let rule = sprintf "membership:%s:%s" (match kind with Group -> "group" | Lane -> "lane" | Phase -> "phase") (Id.value group.Id)
                match definition.Type with
                | EnumField options ->
                    options
                    |> List.tryFind (fun o -> System.String.Equals(o.Label, group.Label, System.StringComparison.OrdinalIgnoreCase) || o.Id = group.Label)
                    |> Option.map (fun o -> Enum o.Id, rule)
                | TextField _ -> Some(Text group.Label, rule)
                | _ -> None)
        | _ -> None

    let resolveField (definition: FieldDefinition) reference project =
        let stored = metadataOf reference project |> Option.bind (Map.tryFind definition.Key)
        MetadataRules.resolve definition stored (derivedValue definition reference project)

    // -- references ---------------------------------------------------------

    let referencesOf reference project =
        match reference with
        | NodeRef(d, n) -> tryNode d n project |> Option.map _.References |> Option.defaultValue []
        | EdgeRef(d, e) -> tryEdge d e project |> Option.map _.References |> Option.defaultValue []
        | DiagramRef d -> tryDiagram d project |> Option.map _.References |> Option.defaultValue []
        | _ -> []

    let private pointsAt (target: ObjectRef) (reference: TypedReference) =
        match target, reference.Target with
        | PageRef p, PageTargetRef q -> p = q
        | DiagramRef d, DiagramTargetRef e -> d = e
        | DiagramRef d, DiagramElementTargetRef(e, _) -> d = e
        | NodeRef(d, n), DiagramElementTargetRef(e, element) -> d = e && Id.value n = element
        | EdgeRef(d, x), DiagramElementTargetRef(e, element) -> d = e && Id.value x = element
        | GroupRef(d, g), DiagramElementTargetRef(e, element) -> d = e && Id.value g = element
        | _ -> false

    /// Inbound typed references to an object, excluding references held by the
    /// object's own subtree when `within` says so (FDA-234, FDA-1035).
    let inboundReferences (target: ObjectRef) (within: ObjectRef -> bool) project =
        allObjects project
        |> List.filter (within >> not)
        |> List.collect (fun holder -> referencesOf holder project |> List.filter (pointsAt target) |> List.map (fun r -> holder, r))
