namespace FormaStudio.Engine

/// Appearance commands: palette, named styles, mappings and overrides,
/// with the palette/style usage and rewrite helpers they need.
/// Internal: `Commands.execute` is the only public execution authority.
module internal AppearanceCommands =
    open CommandSupport

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

    let apply (command: AppearanceCommand) (project: Project) =
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
