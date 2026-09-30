namespace FormaStudio.Engine

/// What kind of change a semantic diff entry reports (FDA-202, FDA-894, FDA-1068).
/// Presentation-definition changes are reported once at the definition, never
/// expanded into per-object semantic changes.
type ChangeCategory =
    | Topology
    | Geometry
    | Semantic
    | Label
    | MetadataChange
    | Presentation
    | PresentationDefinition
    | Routing
    | Membership
    | ReferenceChange
    | Schema
    | LayoutChange

type Change =
    { Code: string
      Category: ChangeCategory
      Target: string
      Summary: string }

[<RequireQualifiedAccess>]
module Diff =
    let private change code category target summary = { Code = code; Category = category; Target = target; Summary = summary }

    /// Pairs keyed items: (key, before, after) in before-then-added order.
    let private pairs (key: 'T -> 'K) (before: 'T list) (after: 'T list) =
        let afterMap = after |> List.map (fun x -> key x, x) |> Map.ofList
        let beforeMap = before |> List.map (fun x -> key x, x) |> Map.ofList
        (before |> List.map (fun b -> key b, Some b, Map.tryFind (key b) afterMap))
        @ (after |> List.filter (fun a -> not (beforeMap.ContainsKey(key a))) |> List.map (fun a -> key a, None, Some a))

    let private metadataChanges target (before: Metadata) (after: Metadata) =
        let keys = Set.union (before |> Map.keys |> Set.ofSeq) (after |> Map.keys |> Set.ofSeq)
        keys
        |> Set.toList
        |> List.filter (fun k -> Map.tryFind k before <> Map.tryFind k after)
        |> List.map (fun k -> change "metadata.changed" MetadataChange target (sprintf "Metadata field '%s' changed." (Id.value k)))

    let private nodeChanges (d: Diagram) (before: DiagramNode option) (after: DiagramNode option) =
        let target (id: NodeId) = sprintf "diagram:%s/node:%s" (Id.value d.Id) (Id.value id)
        match before, after with
        | None, Some n -> [ change "node.added" Topology (target n.Id) (sprintf "Added '%s'." n.Label) ]
        | Some n, None -> [ change "node.removed" Topology (target n.Id) (sprintf "Removed '%s'." n.Label) ]
        | Some b, Some a ->
            let t = target a.Id
            [ if b.Box.Position <> a.Box.Position then change "node.moved" Geometry t (sprintf "Moved '%s'." a.Label)
              if b.Box.Size <> a.Box.Size then change "node.resized" Geometry t (sprintf "Resized '%s'." a.Label)
              if b.Kind <> a.Kind then change "node.kind-changed" Semantic t (sprintf "'%s' changed from %s to %s." a.Label b.Kind a.Kind)
              if b.Label <> a.Label then change "node.label-changed" Label t (sprintf "Renamed '%s' to '%s'." b.Label a.Label)
              if b.Appearance.Overrides <> a.Appearance.Overrides then change "appearance.override-changed" Presentation t (sprintf "Changed the appearance set on '%s'." a.Label)
              if b.Appearance.Style <> a.Appearance.Style then change "appearance.style-reference-changed" Presentation t (sprintf "Changed the named style of '%s'." a.Label)
              if b.References <> a.References then change "references.changed" ReferenceChange t (sprintf "Changed references of '%s'." a.Label)
              if b.Ports <> a.Ports || b.Locked <> a.Locked then change "node.structure-changed" Semantic t (sprintf "Changed ports or lock of '%s'." a.Label) ]
            @ metadataChanges t b.Metadata a.Metadata
        | None, None -> []

    let private edgeChanges (d: Diagram) (before: DiagramEdge option) (after: DiagramEdge option) =
        let target (id: EdgeId) = sprintf "diagram:%s/edge:%s" (Id.value d.Id) (Id.value id)
        match before, after with
        | None, Some e -> [ change "edge.added" Topology (target e.Id) "Added a connector." ]
        | Some e, None -> [ change "edge.removed" Topology (target e.Id) "Removed a connector." ]
        | Some b, Some a ->
            let t = target a.Id
            [ if b.Source <> a.Source || b.Target <> a.Target then change "edge.reconnected" Topology t "Reconnected a connector."
              if b.Kind <> a.Kind then change "edge.kind-changed" Semantic t (sprintf "Connector kind changed from %s to %s." b.Kind a.Kind)
              if b.Routing <> a.Routing then change "edge.routing-changed" Routing t "Changed only the connector route."
              if b.Label <> a.Label then change "edge.label-changed" Label t "Changed a connector label."
              if b.Appearance <> a.Appearance then change "appearance.override-changed" Presentation t "Changed a connector's appearance."
              if b.References <> a.References then change "references.changed" ReferenceChange t "Changed connector references." ]
            @ metadataChanges t b.Metadata a.Metadata
        | None, None -> []

    let private groupChanges (d: Diagram) (before: DiagramGroup option) (after: DiagramGroup option) =
        let target (id: GroupId) = sprintf "diagram:%s/group:%s" (Id.value d.Id) (Id.value id)
        match before, after with
        | None, Some g -> [ change "group.added" Topology (target g.Id) (sprintf "Added %s." g.Label) ]
        | Some g, None -> [ change "group.removed" Topology (target g.Id) (sprintf "Removed %s." g.Label) ]
        | Some b, Some a ->
            let t = target a.Id
            [ if b.Members <> a.Members then change "group.membership-changed" Membership t (sprintf "Changed who belongs to %s." a.Label)
              if b.Box <> a.Box then change "group.geometry-changed" Geometry t (sprintf "Moved or resized %s." a.Label)
              if b.Label <> a.Label || b.Kind <> a.Kind || b.Semantic <> a.Semantic then change "group.changed" Semantic t (sprintf "Changed %s." a.Label)
              if b.Appearance <> a.Appearance then change "appearance.override-changed" Presentation t (sprintf "Changed the appearance of %s." a.Label) ]
            @ metadataChanges t b.Metadata a.Metadata
        | None, None -> []

    let private diagramChanges (before: Diagram option) (after: Diagram option) =
        match before, after with
        | None, Some d -> [ change "diagram.added" Topology (sprintf "diagram:%s" (Id.value d.Id)) (sprintf "Added diagram '%s'." d.Name) ]
        | Some d, None -> [ change "diagram.removed" Topology (sprintf "diagram:%s" (Id.value d.Id)) (sprintf "Removed diagram '%s'." d.Name) ]
        | Some b, Some a ->
            let t = sprintf "diagram:%s" (Id.value a.Id)
            [ if b.Name <> a.Name then change "diagram.renamed" Label t (sprintf "Renamed diagram to '%s'." a.Name)
              if b.Profile <> a.Profile then change "diagram.profile-changed" Semantic t (sprintf "Profile changed from %s@%s to %s@%s." b.Profile.Id b.Profile.Version a.Profile.Id a.Profile.Version)
              if b.Display <> a.Display then change "diagram.display-changed" Presentation t "Changed which metadata is shown on the canvas." ]
            @ metadataChanges t b.Metadata a.Metadata
            @ (pairs (fun (x: DiagramGroup) -> x.Id) b.Groups a.Groups |> List.collect (fun (_, x, y) -> groupChanges a x y))
            @ (pairs (fun (x: DiagramNode) -> x.Id) b.Nodes a.Nodes |> List.collect (fun (_, x, y) -> nodeChanges a x y))
            @ (pairs (fun (x: DiagramEdge) -> x.Id) b.Edges a.Edges |> List.collect (fun (_, x, y) -> edgeChanges a x y))
        | None, None -> []

    let private definitionChanges code category label (key: 'T -> string) (before: 'T list) (after: 'T list) =
        pairs key before after
        |> List.choose (fun (k, b, a) ->
            let target = sprintf "%s:%s" label k
            match b, a with
            | None, Some _ -> Some(change (sprintf "%s.added" code) category target (sprintf "Added %s '%s'." label k))
            | Some _, None -> Some(change (sprintf "%s.removed" code) category target (sprintf "Removed %s '%s'." label k))
            | Some x, Some y when x <> y -> Some(change (sprintf "%s.definition-changed" code) category target (sprintf "Changed the definition of %s '%s'." label k))
            | _ -> None)

    /// A semantic diff between two project states, in deterministic order.
    let between (before: Project) (after: Project) : Change list =
        let pages =
            pairs (fun (x: Page) -> x.Id) before.Pages after.Pages
            |> List.choose (fun (k, b, a) ->
                let t = sprintf "page:%s" (Id.value k)
                match b, a with
                | None, Some _ -> Some(change "page.added" LayoutChange t "Added a page.")
                | Some _, None -> Some(change "page.removed" LayoutChange t "Removed a page.")
                | Some x, Some y when x <> y -> Some(change "page.changed" LayoutChange t "Changed a page's layout or content.")
                | _ -> None)
        pages
        @ (pairs (fun (x: Diagram) -> x.Id) before.Diagrams after.Diagrams |> List.collect (fun (_, b, a) -> diagramChanges b a))
        @ definitionChanges "field" Schema "field" (fun (f: FieldDefinition) -> Id.value f.Key) before.Fields after.Fields
        @ definitionChanges "palette" PresentationDefinition "palette" (fun (p: PaletteSlot) -> Id.value p.Id) before.Palette after.Palette
        @ definitionChanges "style" PresentationDefinition "style" (fun (s: AppearanceStyle) -> Id.value s.Id) before.Styles after.Styles
        @ definitionChanges "mapping" PresentationDefinition "mapping" (fun (m: PresentationMapping) -> Id.value m.Id) before.Mappings after.Mappings

/// A merge conflict names the object and why both sides cannot be combined.
type MergeConflict =
    { Code: string
      Target: string
      Message: string }

type MergeResult =
    { Project: Project
      Conflicts: MergeConflict list }

/// Graph-aware three-way merge. Items merge by stable identity; a change on only
/// one side is taken; identical changes agree; different changes to the same item
/// conflict and keep "ours". Integrity is then re-validated so that combinations
/// such as "node deleted" and "edge added to it" surface as conflicts even though
/// each side was individually valid (Phase 18). Real-time presence is never merged:
/// it is not project data (FDA-1000).
[<RequireQualifiedAccess>]
module Merge =
    let private merge3 target (key: 'T -> 'K) (baseItems: 'T list) (ours: 'T list) (theirs: 'T list) =
        let toMap items = items |> List.map (fun x -> key x, x) |> Map.ofList
        let b, o, t = toMap baseItems, toMap ours, toMap theirs
        let order = (ours |> List.map key) @ (theirs |> List.map key |> List.filter (fun k -> not (o.ContainsKey k)))
        let decide k =
            match Map.tryFind k b, Map.tryFind k o, Map.tryFind k t with
            | _, oi, ti when oi = ti -> oi, None
            | bi, oi, ti when oi = bi -> ti, None
            | bi, oi, ti when ti = bi -> oi, None
            | _, Some oi, None -> Some oi, Some { Code = "merge.changed-vs-deleted"; Target = target k; Message = "Changed on our side and deleted on theirs; our version is kept." }
            | _, None, Some _ -> None, Some { Code = "merge.deleted-vs-changed"; Target = target k; Message = "Deleted on our side and changed on theirs; it stays deleted." }
            | _, oi, _ -> oi, Some { Code = "merge.both-changed"; Target = target k; Message = "Changed differently on both sides; our version is kept." }
        let decided = order |> List.distinct |> List.map decide
        decided |> List.choose fst, decided |> List.choose snd

    let private scalar target (b: 'T) (o: 'T) (t: 'T) =
        if o = t || t = b then o, []
        elif o = b then t, []
        else o, [ { Code = "merge.both-changed"; Target = target; Message = "Changed differently on both sides; our version is kept." } ]

    let private mergeDiagram (b: Diagram option) (o: Diagram) (t: Diagram) =
        let target = sprintf "diagram:%s" (Id.value o.Id)
        let empty = { o with Nodes = []; Edges = []; Groups = [] }
        let bd = defaultArg b empty
        let nodes, nc = merge3 (fun (k: NodeId) -> sprintf "%s/node:%s" target (Id.value k)) (fun (x: DiagramNode) -> x.Id) bd.Nodes o.Nodes t.Nodes
        let edges, ec = merge3 (fun (k: EdgeId) -> sprintf "%s/edge:%s" target (Id.value k)) (fun (x: DiagramEdge) -> x.Id) bd.Edges o.Edges t.Edges
        let groups, gc = merge3 (fun (k: GroupId) -> sprintf "%s/group:%s" target (Id.value k)) (fun (x: DiagramGroup) -> x.Id) bd.Groups o.Groups t.Groups
        let name, c1 = scalar target bd.Name o.Name t.Name
        let profile, c2 = scalar (target + "/profile") bd.Profile o.Profile t.Profile
        let display, c3 = scalar (target + "/display") bd.Display o.Display t.Display
        let metadata, c4 = scalar (target + "/metadata") bd.Metadata o.Metadata t.Metadata
        let references, c5 = scalar (target + "/references") bd.References o.References t.References
        { o with Nodes = nodes; Edges = edges; Groups = groups; Name = name; Profile = profile; Display = display; Metadata = metadata; References = references },
        nc @ ec @ gc @ c1 @ c2 @ c3 @ c4 @ c5

    let three (baseProject: Project) (ours: Project) (theirs: Project) : MergeResult =
        let diagrams, diagramConflicts =
            let find (items: Diagram list) id = items |> List.tryFind (fun (d: Diagram) -> d.Id = id)
            let ids = (ours.Diagrams @ theirs.Diagrams) |> List.map (fun (d: Diagram) -> d.Id) |> List.distinct
            let results =
                ids
                |> List.map (fun id ->
                    match find baseProject.Diagrams id, find ours.Diagrams id, find theirs.Diagrams id with
                    // Both sides kept the diagram: merge inside it, element by element.
                    | b, Some o, Some t -> let d, c = mergeDiagram b o t in Some d, c
                    | b, o, t ->
                        let kept, conflicts = merge3 (fun (k: DiagramId) -> sprintf "diagram:%s" (Id.value k)) (fun (x: Diagram) -> x.Id) (Option.toList b) (Option.toList o) (Option.toList t)
                        List.tryHead kept, conflicts)
            results |> List.choose fst, results |> List.collect snd
        let pages, pc = merge3 (fun (k: PageId) -> sprintf "page:%s" (Id.value k)) (fun (x: Page) -> x.Id) baseProject.Pages ours.Pages theirs.Pages
        let fields, fc = merge3 (fun (k: FieldKey) -> sprintf "field:%s" (Id.value k)) (fun (x: FieldDefinition) -> x.Key) baseProject.Fields ours.Fields theirs.Fields
        let palette, plc = merge3 (fun (k: PaletteSlotId) -> sprintf "palette:%s" (Id.value k)) (fun (x: PaletteSlot) -> x.Id) baseProject.Palette ours.Palette theirs.Palette
        let styles, sc = merge3 (fun (k: StyleId) -> sprintf "style:%s" (Id.value k)) (fun (x: AppearanceStyle) -> x.Id) baseProject.Styles ours.Styles theirs.Styles
        let mappings, mc = merge3 (fun (k: MappingId) -> sprintf "mapping:%s" (Id.value k)) (fun (x: PresentationMapping) -> x.Id) baseProject.Mappings ours.Mappings theirs.Mappings
        let merged =
            { ours with Diagrams = diagrams; Pages = pages; Fields = fields; Palette = palette; Styles = styles; Mappings = mappings }
        // Graph-aware pass: blockers that neither side had are merge conflicts.
        let known = (Validation.blockers ours @ Validation.blockers theirs) |> Set.ofList
        let integrity =
            Validation.blockers merged
            |> List.filter (fun f -> not (known.Contains f))
            |> List.map (fun f -> { Code = sprintf "merge.%s" f.Code; Target = f.Target; Message = f.Message })
        { Project = merged; Conflicts = diagramConflicts @ pc @ fc @ plc @ sc @ mc @ integrity }
