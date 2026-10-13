namespace FormaStudio.Engine

open Interaction

/// Edits to one component anywhere on the open Layout page: content, contract
/// properties (including a declared alignment group), order among its siblings
/// and removal. Each is one command through the shared session; keys are
/// "<component id>|..." as the Layout view projects them. A helper of
/// LayoutInteraction, not an area of its own.
module internal LayoutComponentEdits =
    let private parts (key: string) = key.Split('|') |> List.ofArray

    let private componentOf (key: string) = parts key |> List.tryHead |> Option.bind (Id.create<ComponentKind> >> Result.toOption)

    let private onPage (state: EditorState) (key: string) (build: Page -> ComponentNodeId -> EditorState) =
        match EditorState.openPage state, componentOf key with
        | Some page, Some id when ProjectOps.componentIds page.Nodes |> List.contains id -> build page id
        | None, _ -> status "Open a Layout page first." state
        | _ -> status "That component is no longer on the page." state

    /// A property value as its contract checks it: true/false, a whole number, or text.
    let private propertyValue (text: string) =
        match text, System.Int32.TryParse text with
        | "", _ -> None
        | ("true" | "false"), _ -> Some(JBool(text = "true"))
        | _, (true, n) -> Some(Json.ofInt n)
        | _ -> Some(JString text)

    let setText (key: string) (value: string) (state: EditorState) =
        let contentKey = match parts key with [ _; k ] -> k | _ -> "text"
        onPage state key (fun page id -> run (Layout(SetComponentContent(page.Id, id, contentKey, value))) "Text changed." state)

    let setProperty (key: string) (state: EditorState) =
        match parts key with
        | [ _; property; value ] ->
            let message = if value = "" then sprintf "%s cleared." property else sprintf "%s set to %s." property value
            onPage state key (fun page id -> run (Layout(SetComponentProperty(page.Id, id, property, propertyValue value))) message state)
        | _ -> status "Choose a property value." state

    let remove (key: string) (state: EditorState) =
        onPage state key (fun page id ->
            if page.Nodes |> List.exists (fun n -> n.Id = id) then status "The page's root stack cannot be removed." state
            else
                let removed = run (Layout(RemoveComponent(page.Id, id))) (sprintf "Removed %s." (Id.value id)) state
                // A removed container can no longer receive new components.
                let onPageNow t = EditorState.openPage removed |> Option.exists (fun p -> ProjectOps.componentIds p.Nodes |> List.exists (fun c -> Id.value c = t))
                { removed with LayoutTarget = removed.LayoutTarget |> Option.filter onPageNow })

    let reorder (key: string) (earlier: bool) (state: EditorState) =
        onPage state key (fun page _ ->
            match EditorIntents.reorder page (parts key |> List.head) earlier with
            | Some command -> run command "Reordered." state
            | None -> status "Nothing to reorder." state)
