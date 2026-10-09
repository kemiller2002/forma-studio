namespace FormaStudio.Engine

/// The optional Forma icon name of a Layout component (its `icon` property, #33)
/// or a diagram node. The
/// command checks the name's form (by its type) and that the target can hold an
/// icon. It does not check that the pinned Forma release has the icon: a
/// document may name an icon from a newer release, which is kept and simply not
/// shown (Forma ICON-014). Internal: `Commands.execute` is the only public
/// execution authority.
module internal IconCommands =
    open CommandSupport

    let setIcon (target: ObjectRef) (name: IconName option) (project: Project) =
        let address = ObjectRef.describe target
        match target with
        | ComponentRef(page, id) ->
            ProjectOps.updateComponent page id (fun node ->
                match name with
                | Some _ when not (Components.supportsIcon node.Component) -> Error(sprintf "A %s has no place for an icon." node.Component)
                | Some n -> Ok { node with Properties = node.Properties |> Map.add "icon" (JString(IconName.value n)) }
                | None -> Ok { node with Properties = node.Properties |> Map.remove "icon" }) project
            |> lift address
            |> Result.bind ok
        | NodeRef(diagram, id) -> ProjectOps.updateNode diagram id (fun node -> Ok { node with Icon = name |> Option.map NamedIcon }) project |> lift address |> Result.bind ok
        | _ -> reject "icon.target" address "Only Layout components and diagram nodes carry an icon."
