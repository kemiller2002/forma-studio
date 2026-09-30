namespace FormaStudio.Engine

/// A reusable diagram fragment (template, copy buffer) with its dependency
/// closure: the fields, palette slots, styles and mappings its objects need,
/// plus the profile it was authored under (FDA-1250..1257, FDA-1110).
type DiagramFragment =
    { Profile: ProfileRef
      Nodes: DiagramNode list
      Edges: DiagramEdge list
      Fields: FieldDefinition list
      Palette: PaletteSlot list
      Styles: AppearanceStyle list
      Mappings: PresentationMapping list }

/// What to do when a dependency with the same stable id already exists in the
/// target project but its definition differs.
type ConflictPolicy =
    | RemapConflicting
    | RejectConflicting

type DependencyAction =
    | Reused
    | Imported
    | Remapped of newId: string

type DependencyDecision =
    { Kind: string
      SourceId: string
      Action: DependencyAction }

[<RequireQualifiedAccess>]
module Fragment =
    let private colorPalettes (a: Appearance) = Appearance.colors a |> List.choose (function PaletteColor p -> Some p | _ -> None)

    let private outcomes (m: PresentationMapping) =
        (m.Rules |> List.map _.Outcome)
        @ ([ m.Fallbacks.Missing; m.Fallbacks.Unknown; m.Fallbacks.Unavailable; m.Fallbacks.Invalid; m.Fallbacks.Unmapped ]
           |> List.choose (function Apply(o, _) -> Some o | NoMapping -> None))

    /// Extracts selected nodes with the connectors between them. Connectors to
    /// unselected nodes are omitted (FDA-1115). Dependencies are closed over.
    let extract (project: Project) (diagramId: DiagramId) (nodeIds: NodeId list) : Result<DiagramFragment, string> =
        match ProjectOps.tryDiagram diagramId project with
        | None -> Error "The diagram does not exist."
        | Some diagram ->
            let selected = Set.ofList nodeIds
            let nodes = diagram.Nodes |> List.filter (fun n -> selected.Contains n.Id)
            let edges = diagram.Edges |> List.filter (fun e -> selected.Contains e.Source.Node && selected.Contains e.Target.Node)
            let objectMetadata = (nodes |> List.map _.Metadata) @ (edges |> List.map _.Metadata)
            let objectAppearance = (nodes |> List.map _.Appearance) @ (edges |> List.map _.Appearance)
            let fieldKeys = objectMetadata |> List.collect (Map.keys >> List.ofSeq) |> Set.ofList
            let fields = project.Fields |> List.filter (fun f -> fieldKeys.Contains f.Key)
            let mappings = project.Mappings |> List.filter (fun m -> fieldKeys.Contains m.Field)
            let styleIds =
                (objectAppearance |> List.choose _.Style)
                @ (mappings |> List.collect outcomes |> List.choose (function UseStyle s -> Some s | _ -> None))
                |> Set.ofList
            let styles = project.Styles |> List.filter (fun s -> styleIds.Contains s.Id)
            let paletteIds =
                (objectAppearance |> List.collect (fun a -> colorPalettes a.Overrides))
                @ (styles |> List.collect (fun s -> colorPalettes s.Appearance))
                @ (mappings |> List.collect outcomes |> List.collect (function UseAppearance a -> colorPalettes a | _ -> []))
                |> Set.ofList
            let palette = project.Palette |> List.filter (fun p -> paletteIds.Contains p.Id)
            Ok { Profile = diagram.Profile; Nodes = nodes; Edges = edges; Fields = fields; Palette = palette; Styles = styles; Mappings = mappings }

    let private freshId (used: Set<string>) (candidate: string) =
        if not (used.Contains candidate) then candidate
        else Seq.initInfinite (fun i -> sprintf "%s-%d" candidate (i + 2)) |> Seq.find (used.Contains >> not)

    /// Decides, per dependency, whether to reuse, import or remap it. Identity is
    /// the stable id plus the full definition; display names and resolved colors
    /// never decide a match (FDA-1251..1253).
    let private reconcile kind (idOf: 'T -> string) (existing: 'T list) (incoming: 'T list) policy =
        let used = existing |> List.map idOf |> Set.ofList
        incoming
        |> List.fold
            (fun state item ->
                state
                |> Result.bind (fun (decisions, reserved: Set<string>) ->
                    let id = idOf item
                    match existing |> List.tryFind (fun e -> idOf e = id) with
                    | None -> Ok(decisions @ [ { Kind = kind; SourceId = id; Action = Imported } ], reserved.Add id)
                    | Some e when e = item -> Ok(decisions @ [ { Kind = kind; SourceId = id; Action = Reused } ], reserved)
                    | Some _ ->
                        match policy with
                        | RejectConflicting -> Error(sprintf "%s '%s' already exists in this project with a different definition." kind id)
                        | RemapConflicting ->
                            let newId = freshId reserved id
                            Ok(decisions @ [ { Kind = kind; SourceId = id; Action = Remapped newId } ], reserved.Add newId)))
            (Ok([], used))
        |> Result.map fst

    let private remapTable (decisions: DependencyDecision list) =
        decisions |> List.choose (fun d -> match d.Action with Remapped n -> Some(d.SourceId, n) | _ -> None) |> Map.ofList

    let private rename (table: Map<string, string>) (id: Id<'K>) : Id<'K> =
        match Map.tryFind (Id.value id) table with
        | Some n -> Samples.idOf n
        | None -> id

    /// Plans an application without changing anything: dependency decisions for
    /// review before commit (FDA-1253, FDA-1257).
    let plan (fragment: DiagramFragment) (project: Project) policy =
        let fields = reconcile "field" (fun (f: FieldDefinition) -> Id.value f.Key) project.Fields fragment.Fields policy
        let palette = reconcile "palette" (fun (p: PaletteSlot) -> Id.value p.Id) project.Palette fragment.Palette policy
        let styles = reconcile "style" (fun (s: AppearanceStyle) -> Id.value s.Id) project.Styles fragment.Styles policy
        let mappings = reconcile "mapping" (fun (m: PresentationMapping) -> Id.value m.Id) project.Mappings fragment.Mappings policy
        match fields, palette, styles, mappings with
        | Ok f, Ok p, Ok s, Ok m -> Ok(f @ p @ s @ m)
        | Error e, _, _, _
        | _, Error e, _, _
        | _, _, Error e, _
        | _, _, _, Error e -> Error e

    /// Builds the single batch command that applies a fragment at an offset. Node
    /// and connector ids are always fresh; dependency references are rewritten
    /// consistently with the plan (FDA-1254). One undo removes everything.
    let applyCommand (fragment: DiagramFragment) (project: Project) (diagramId: DiagramId) (dx: float) (dy: float) policy : Result<Command * DependencyDecision list, string> =
        match ProjectOps.tryDiagram diagramId project with
        | None -> Error "The target diagram does not exist."
        | Some diagram when diagram.Profile <> fragment.Profile ->
            Error(sprintf "The fragment was authored for %s@%s; this diagram uses %s@%s. Migrate one of them first." fragment.Profile.Id fragment.Profile.Version diagram.Profile.Id diagram.Profile.Version)
        | Some diagram ->
            plan fragment project policy
            |> Result.map (fun decisions ->
                let byKind kind = decisions |> List.filter (fun d -> d.Kind = kind) |> remapTable
                let fieldMap, paletteMap, styleMap, mappingMap = byKind "field", byKind "palette", byKind "style", byKind "mapping"
                let action kind (id: string) = decisions |> List.tryFind (fun d -> d.Kind = kind && d.SourceId = id) |> Option.map _.Action
                let color c = match c with PaletteColor p -> PaletteColor(rename paletteMap p) | other -> other
                let appearance (a: Appearance) =
                    { a with Fill = a.Fill |> Option.map color; Stroke = a.Stroke |> Option.map color; Accent = a.Accent |> Option.map color
                             Foreground = a.Foreground |> Option.map color; ConnectorStroke = a.ConnectorStroke |> Option.map color }
                let outcome o = match o with UseStyle s -> UseStyle(rename styleMap s) | UseAppearance a -> UseAppearance(appearance a)
                let fallback f = match f with Apply(o, l) -> Apply(outcome o, l) | NoMapping -> NoMapping
                let used =
                    (diagram.Nodes |> List.map (fun n -> Id.value n.Id)) @ (diagram.Edges |> List.map (fun e -> Id.value e.Id)) @ (diagram.Groups |> List.map (fun g -> Id.value g.Id))
                    |> Set.ofList
                let nodeIds =
                    fragment.Nodes
                    |> List.fold (fun (table: Map<string, string>, reserved: Set<string>) n ->
                        let fresh = freshId reserved (Id.value n.Id)
                        table.Add(Id.value n.Id, fresh), reserved.Add fresh) (Map.empty, used)
                    |> fst
                let edgeIds =
                    fragment.Edges
                    |> List.fold (fun (table: Map<string, string>, reserved: Set<string>) e ->
                        let fresh = freshId reserved (Id.value e.Id)
                        table.Add(Id.value e.Id, fresh), reserved.Add fresh) (Map.empty, used |> Set.union (nodeIds |> Map.values |> Set.ofSeq))
                    |> fst
                let definitions =
                    (fragment.Fields |> List.choose (fun f ->
                        match action "field" (Id.value f.Key) with
                        | Some Imported -> Some(MetadataCmd(DefineField f))
                        | Some(Remapped n) -> Some(MetadataCmd(DefineField { f with Key = Samples.idOf n }))
                        | _ -> None))
                    @ (fragment.Palette |> List.choose (fun p ->
                        match action "palette" (Id.value p.Id) with
                        | Some Imported -> Some(AppearanceCmd(AddPaletteSlot p))
                        | Some(Remapped n) -> Some(AppearanceCmd(AddPaletteSlot { p with Id = Samples.idOf n }))
                        | _ -> None))
                    @ (fragment.Styles |> List.choose (fun s ->
                        let remapped = { s with Appearance = appearance s.Appearance }
                        match action "style" (Id.value s.Id) with
                        | Some Imported -> Some(AppearanceCmd(DefineStyle remapped))
                        | Some(Remapped n) -> Some(AppearanceCmd(DefineStyle { remapped with Id = Samples.idOf n }))
                        | _ -> None))
                    @ (fragment.Mappings |> List.choose (fun m ->
                        let remapped =
                            { m with
                                Field = rename fieldMap m.Field
                                Rules = m.Rules |> List.map (fun r -> { r with Outcome = outcome r.Outcome })
                                Fallbacks =
                                    { Missing = fallback m.Fallbacks.Missing; Unknown = fallback m.Fallbacks.Unknown; Unavailable = fallback m.Fallbacks.Unavailable
                                      Invalid = fallback m.Fallbacks.Invalid; Unmapped = fallback m.Fallbacks.Unmapped } }
                        match action "mapping" (Id.value m.Id) with
                        | Some Imported -> Some(AppearanceCmd(DefineMapping remapped))
                        | Some(Remapped n) -> Some(AppearanceCmd(DefineMapping { remapped with Id = Samples.idOf n }))
                        | _ -> None))
                let nodeRef (n: DiagramNode) = NodeRef(diagramId, Samples.idOf nodeIds.[Id.value n.Id])
                let nodeCommands =
                    fragment.Nodes
                    |> List.collect (fun n ->
                        let r = nodeRef n
                        let newId: NodeId = Samples.idOf nodeIds.[Id.value n.Id]
                        [ Flow(AddNode(diagramId, newId, n.Kind, n.Label, float n.Box.Position.X + dx, float n.Box.Position.Y + dy, float n.Box.Size.Width, float n.Box.Size.Height)) ]
                        @ (n.Metadata |> Map.toList |> List.map (fun (k, v) -> MetadataCmd(SetValue([ r ], rename fieldMap k, v))))
                        @ (if Appearance.isEmpty n.Appearance.Overrides then [] else [ AppearanceCmd(SetOverride([ r ], appearance n.Appearance.Overrides)) ])
                        @ (n.Appearance.Style |> Option.map (fun s -> AppearanceCmd(ApplyStyle([ r ], Some(rename styleMap s)))) |> Option.toList)
                        @ (n.References |> List.map (fun ref -> Flow(AddReference(r, ref)))))
                let edgeCommands =
                    fragment.Edges
                    |> List.collect (fun e ->
                        let newId: EdgeId = Samples.idOf edgeIds.[Id.value e.Id]
                        let r = EdgeRef(diagramId, newId)
                        let endpoint (p: Endpoint) = { p with Node = Samples.idOf nodeIds.[Id.value p.Node] }
                        let shift (pt: Point) = { X = pt.X + int dx; Y = pt.Y + int dy }
                        [ Flow(Connect(diagramId, newId, e.Kind, endpoint e.Source, endpoint e.Target, e.Label))
                          Flow(SetEdgeRouting(diagramId, newId, (match e.Routing with Manual ps -> Manual(ps |> List.map shift) | other -> other))) ]
                        @ (e.Metadata |> Map.toList |> List.map (fun (k, v) -> MetadataCmd(SetValue([ r ], rename fieldMap k, v))))
                        @ (if Appearance.isEmpty e.Appearance.Overrides then [] else [ AppearanceCmd(SetOverride([ r ], appearance e.Appearance.Overrides)) ]))
                Batch("apply fragment", definitions @ nodeCommands @ edgeCommands), decisions)
