namespace FormaStudio.Engine

/// Whole-project validation. Integrity blockers make a command illegal; profile,
/// metadata, appearance and disclosure findings are reported with the affected
/// stable object and never inferred from visual neatness (FDA-160..164).
[<RequireQualifiedAccess>]
module Validation =
    let private duplicates (ids: string list) =
        ids |> List.countBy id |> List.filter (fun (_, n) -> n > 1) |> List.map fst

    let private integrity (project: Project) =
        let projectTarget = sprintf "project:%s" (Id.value project.Id)
        let pageIds = project.Pages |> List.map (fun p -> Id.value p.Id)
        let diagramIds = project.Diagrams |> List.map (fun d -> Id.value d.Id)

        let startPage =
            match project.StartPage, project.Pages with
            | None, [] -> []
            | None, _ -> [ Finding.blocker "project.start-page.missing" Integrity projectTarget "A project with pages needs a start page." ]
            | Some start, _ when not (List.contains (Id.value start) pageIds) ->
                [ Finding.blocker "project.start-page.dangling" Integrity projectTarget (sprintf "Start page '%s' does not exist." (Id.value start)) ]
            | Some _, _ -> []

        let routes =
            project.Pages
            |> List.choose (fun p -> p.Route |> Option.map (fun r -> r.TrimEnd('/').ToLowerInvariant()))
            |> List.map (fun r -> if r = "" then "/" else r)
            |> duplicates
            |> List.map (fun r -> Finding.blocker "page.route.duplicate" Integrity projectTarget (sprintf "Route '%s' is used by more than one page." r))

        let pageDuplicates =
            duplicates pageIds |> List.map (fun id -> Finding.blocker "id.duplicate" Integrity projectTarget (sprintf "Page id '%s' is not unique." id))

        let componentDuplicates =
            project.Pages
            |> List.collect (fun p -> ProjectOps.componentIds p.Nodes |> List.map Id.value)
            |> duplicates
            |> List.map (fun id -> Finding.blocker "id.duplicate" Integrity projectTarget (sprintf "Component id '%s' is not unique." id))

        let diagramDuplicates =
            duplicates diagramIds |> List.map (fun id -> Finding.blocker "id.duplicate" Integrity projectTarget (sprintf "Diagram id '%s' is not unique." id))

        let perDiagram =
            project.Diagrams
            |> List.collect (fun diagram ->
                let target = sprintf "diagram:%s" (Id.value diagram.Id)
                let nodeIds = diagram.Nodes |> List.map (fun n -> Id.value n.Id)
                let elementIds = nodeIds @ (diagram.Edges |> List.map (fun e -> Id.value e.Id)) @ (diagram.Groups |> List.map (fun g -> Id.value g.Id))
                let dupes =
                    duplicates elementIds |> List.map (fun id -> Finding.blocker "id.duplicate" Integrity target (sprintf "Element id '%s' is not unique in the diagram." id))
                let endpoint (edge: DiagramEdge) side (e: Endpoint) =
                    let edgeTarget = sprintf "%s/edge:%s" target (Id.value edge.Id)
                    match diagram.Nodes |> List.tryFind (fun n -> n.Id = e.Node) with
                    | None -> [ Finding.blocker "edge.endpoint.dangling" Integrity edgeTarget (sprintf "The %s node '%s' does not exist." side (Id.value e.Node)) ]
                    | Some node ->
                        match e.Port with
                        | Some port when not (node.Ports |> List.exists (fun p -> p.Id = port)) ->
                            [ Finding.blocker "edge.port.invalid" Integrity edgeTarget (sprintf "Node '%s' has no port '%s'." node.Label (Id.value port)) ]
                        | _ -> []
                let edges = diagram.Edges |> List.collect (fun e -> endpoint e "source" e.Source @ endpoint e "target" e.Target)
                let membership =
                    diagram.Groups
                    |> List.collect (fun g ->
                        g.Members
                        |> List.filter (fun m -> not (List.contains (Id.value m) nodeIds))
                        |> List.map (fun m ->
                            Finding.blocker "group.member.dangling" Integrity (sprintf "%s/group:%s" target (Id.value g.Id)) (sprintf "Member '%s' does not exist." (Id.value m))))
                let laneConflicts =
                    diagram.Nodes
                    |> List.filter (fun n -> (diagram.Groups |> List.filter (fun g -> g.Kind = Lane && List.contains n.Id g.Members)).Length > 1)
                    |> List.map (fun n ->
                        Finding.blocker "lane.membership.multiple" Integrity (sprintf "%s/node:%s" target (Id.value n.Id)) (sprintf "'%s' belongs to more than one lane." n.Label))
                dupes @ edges @ membership @ laneConflicts)

        let definitionDuplicates =
            [ project.Fields |> List.map (fun f -> Id.value f.Key), "Metadata field"
              project.Palette |> List.map (fun p -> Id.value p.Id), "Palette slot"
              project.Styles |> List.map (fun s -> Id.value s.Id), "Style"
              project.Mappings |> List.map (fun m -> Id.value m.Id), "Mapping" ]
            |> List.collect (fun (ids, label) ->
                duplicates ids |> List.map (fun id -> Finding.blocker "id.duplicate" Integrity projectTarget (sprintf "%s id '%s' is not unique." label id)))

        startPage @ routes @ pageDuplicates @ componentDuplicates @ diagramDuplicates @ perDiagram @ definitionDuplicates

    let private profile (project: Project) =
        project.Diagrams
        |> List.collect (fun diagram ->
            let target = sprintf "diagram:%s" (Id.value diagram.Id)
            match Profiles.tryFind diagram.Profile with
            | None ->
                [ Finding.create "profile.unavailable" Warning ProfileRule target
                      (sprintf "Profile %s@%s is not available in this Studio build; the diagram is preserved unchanged and typed editing is disabled." diagram.Profile.Id diagram.Profile.Version) ]
            | Some p ->
                let kinds =
                    diagram.Nodes
                    |> List.choose (fun n ->
                        match Profiles.nodeKind p n.Kind with
                        | None -> Some(Finding.create "profile.node-kind" Blocker ProfileRule (sprintf "%s/node:%s" target (Id.value n.Id)) (sprintf "Kind '%s' is not defined by the %s profile." n.Kind p.Name))
                        | Some spec ->
                            match spec.AllowedShapes, n.Appearance.Overrides.Shape with
                            | Some allowed, Some shape when not (List.contains shape allowed) ->
                                Some(Finding.create "profile.shape" Warning ProfileRule (sprintf "%s/node:%s" target (Id.value n.Id)) (sprintf "The %s profile limits '%s' nodes to specific shapes." p.Name n.Kind))
                            | _ -> None)
                let edges =
                    diagram.Edges
                    |> List.choose (fun e ->
                        let edgeTarget = sprintf "%s/edge:%s" target (Id.value e.Id)
                        match Profiles.edgeKind p e.Kind, diagram.Nodes |> List.tryFind (fun n -> n.Id = e.Source.Node), diagram.Nodes |> List.tryFind (fun n -> n.Id = e.Target.Node) with
                        | None, _, _ -> Some(Finding.create "profile.edge-kind" Blocker ProfileRule edgeTarget (sprintf "Connector kind '%s' is not defined by the %s profile." e.Kind p.Name))
                        | Some edgeKind, Some source, Some targetNode ->
                            match Profiles.nodeKind p source.Kind, Profiles.nodeKind p targetNode.Kind with
                            | Some s, Some t ->
                                match p.CanConnect s edgeKind t with
                                | Ok() -> None
                                | Error reason -> Some(Finding.create "profile.connection" Blocker ProfileRule edgeTarget reason)
                            | _ -> None
                        | _ -> None)
                kinds @ edges @ p.Validate diagram)

    let private metadata (project: Project) =
        ProjectOps.allObjects project
        |> List.collect (fun reference ->
            let target = ObjectRef.describe reference
            let stored = ProjectOps.metadataOf reference project |> Option.defaultValue Map.empty
            let unknown =
                stored
                |> Map.toList
                |> List.filter (fun (key, _) -> (ProjectOps.tryField key project).IsNone)
                |> List.map (fun (key, _) ->
                    Finding.create "metadata.field.unknown" Advisory MetadataRule target
                        (sprintf "Field '%s' is not defined in this project. Its value is preserved and treated as source-only." (Id.value key)))
            let notApplicable =
                stored
                |> Map.toList
                |> List.choose (fun (key, _) -> ProjectOps.tryField key project |> Option.filter (fun f -> not (MetadataRules.applies f (ObjectRef.kind reference))))
                |> List.map (fun f ->
                    Finding.create "metadata.field.not-applicable" Warning MetadataRule target (sprintf "Field '%s' does not apply to this kind of object." f.Name))
            let perField =
                project.Fields
                |> List.filter (fun f -> MetadataRules.applies f (ObjectRef.kind reference))
                |> List.collect (fun field ->
                    match ProjectOps.resolveField field reference project with
                    | ResolvedInvalid reason ->
                        [ Finding.create "metadata.value.invalid" Blocker MetadataRule target (sprintf "%s: %s." field.Name reason) ]
                    | ResolvedConflict(authored, derived, rule) ->
                        [ Finding.create "metadata.value.conflict" Warning MetadataRule target
                              (sprintf "%s is '%s' but %s derives '%s'." field.Name (MetadataRules.display (Some field) authored) rule (MetadataRules.display (Some field) derived)) ]
                    | ResolvedMissing true -> [ Finding.create "metadata.value.required" Warning MetadataRule target (sprintf "%s is required." field.Name) ]
                    | _ -> [])
            unknown @ notApplicable @ perField)

    let private references (project: Project) =
        ProjectOps.allObjects project
        |> List.collect (fun holder ->
            ProjectOps.referencesOf holder project
            |> List.choose (fun r ->
                let dangling =
                    match r.Target with
                    | PageTargetRef p -> (ProjectOps.tryPage p project).IsNone
                    | DiagramTargetRef d -> (ProjectOps.tryDiagram d project).IsNone
                    | DiagramElementTargetRef(d, element) ->
                        match ProjectOps.tryDiagram d project with
                        | None -> true
                        | Some diagram ->
                            not (diagram.Nodes |> List.exists (fun n -> Id.value n.Id = element)
                                 || diagram.Edges |> List.exists (fun e -> Id.value e.Id = element)
                                 || diagram.Groups |> List.exists (fun g -> Id.value g.Id = element))
                    | _ -> false
                if dangling then
                    Some(Finding.blocker "reference.dangling" ReferenceRule (ObjectRef.describe holder) (sprintf "Reference '%s' points to an internal target that does not exist." (Id.value r.Id)))
                else None))

    let private appearance (project: Project) =
        let objects =
            ProjectOps.allObjects project
            |> List.collect (fun reference ->
                match ProjectOps.appearanceOf reference project with
                | None -> []
                | Some a ->
                    let target = ObjectRef.describe reference
                    let style =
                        match a.Style with
                        | Some s when (ProjectOps.tryStyle s project).IsNone ->
                            [ Finding.create "appearance.style.missing" Warning AppearanceRule target (sprintf "Style '%s' is unavailable; the reference is kept." (Id.value s)) ]
                        | _ -> []
                    let palette =
                        Appearance.colors a.Overrides
                        |> List.choose (function PaletteColor id when (ProjectOps.tryPalette id project).IsNone -> Some id | _ -> None)
                        |> List.map (fun id -> Finding.create "appearance.palette.missing" Warning AppearanceRule target (sprintf "Palette slot '%s' is missing." (Id.value id)))
                    style @ palette)
        let mappings =
            project.Mappings
            |> List.collect (fun m ->
                let target = sprintf "mapping:%s" (Id.value m.Id)
                match ProjectOps.tryField m.Field project with
                | None -> [ Finding.create "mapping.field.missing" Blocker AppearanceRule target (sprintf "Mapping '%s' reads undefined field '%s'." m.Name (Id.value m.Field)) ]
                | Some field ->
                    let disclosure =
                        if field.Disclosure.DerivedPresentation then []
                        else
                            // FDA-1262/1265: name the disclosure path.
                            [ Finding.create "disclosure.mapping.prohibited" Warning DisclosureRule target
                                  (sprintf "Field '%s' does not allow derived presentation, so mapping '%s' affects only editor-only highlighting (field -> mapping -> rendered object is blocked)." field.Name m.Name) ]
                    let styles =
                        (m.Rules |> List.map _.Outcome)
                        @ ([ m.Fallbacks.Missing; m.Fallbacks.Unknown; m.Fallbacks.Unavailable; m.Fallbacks.Invalid; m.Fallbacks.Unmapped ]
                           |> List.choose (function Apply(o, _) -> Some o | NoMapping -> None))
                        |> List.choose (function UseStyle s when (ProjectOps.tryStyle s project).IsNone -> Some s | _ -> None)
                        |> List.map (fun s -> Finding.create "mapping.style.missing" Blocker AppearanceRule target (sprintf "Mapping '%s' uses missing style '%s'." m.Name (Id.value s)))
                    disclosure @ styles)
        objects @ mappings

    /// Stored icon values that are not names, and icons on components with no
    /// place for one. Both are kept as data; neither ever blocks (Forma ICON-014).
    let private icons (project: Project) =
        let note target icon placeable =
            match icon with
            | Some(MalformedIcon _) -> [ Finding.create "icon.malformed" Warning Integrity target "The stored icon is not a valid Forma icon name; it is kept as data and never shown." ]
            | Some(NamedIcon _) when not placeable -> [ Finding.create "icon.unplaced" Advisory Integrity target "This component has no place for an icon; the icon is kept but not exported." ]
            | _ -> []
        let rec components page (nodes: ComponentNode list) =
            nodes
            |> List.collect (fun n ->
                note (ObjectRef.describe (ComponentRef(page, n.Id))) (ProjectOps.componentIcon n) (Components.supportsIcon n.Component)
                @ (n.Slots |> Map.toList |> List.collect (snd >> components page)))
        (project.Pages |> List.collect (fun p -> components p.Id p.Nodes))
        @ (project.Diagrams |> List.collect (fun d -> d.Nodes |> List.collect (fun n -> note (ObjectRef.describe (NodeRef(d.Id, n.Id))) n.Icon true)))

    /// All findings in deterministic order.
    let run (project: Project) =
        integrity project @ profile project @ metadata project @ references project @ appearance project @ icons project

    let blockers project = run project |> List.filter Finding.isBlocker
