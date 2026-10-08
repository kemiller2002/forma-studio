namespace FormaStudio.Engine

/// The optional Forma icon name of a Layout component or a diagram node. The
/// command checks the name's form (by its type) and that the target can hold an
/// icon. It does not check that the pinned Forma release has the icon: a
/// document may name an icon from a newer release, which is kept and simply not
/// shown (Forma ICON-014). Internal: `Commands.execute` is the only public
/// execution authority.
module internal IconCommands =
    open CommandSupport

    let setIcon (target: ObjectRef) (name: IconName option) (project: Project) =
        let address = ObjectRef.describe target
        let icon = name |> Option.map NamedIcon
        match target with
        | ComponentRef(page, id) ->
            ProjectOps.updateComponent page id (fun node ->
                if name.IsSome && not (Components.supportsIcon node.Component) then Error(sprintf "A %s has no place for an icon." node.Component)
                else Ok { node with Icon = icon }) project
            |> lift address
            |> Result.bind ok
        | NodeRef(diagram, id) -> ProjectOps.updateNode diagram id (fun node -> Ok { node with Icon = icon }) project |> lift address |> Result.bind ok
        | _ -> reject "icon.target" address "Only Layout components and diagram nodes carry an icon."
