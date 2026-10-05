namespace FormaStudio.Engine

/// Layout commands: pages and Forma component trees.
/// Internal: `Commands.execute` is the only public execution authority.
module internal LayoutCommands =
    open CommandSupport

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

    let apply (command: LayoutCommand) (project: Project) =
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
