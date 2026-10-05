namespace FormaStudio.Engine

/// Metadata commands: field definitions and values.
/// Internal: `Commands.execute` is the only public execution authority.
module internal MetadataCommands =
    open CommandSupport

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

    let apply (command: MetadataCommand) (project: Project) =
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
