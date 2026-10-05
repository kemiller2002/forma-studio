namespace FormaStudio.Engine

open Interaction

/// Layout pages: adding pages and components, editing text, reordering,
/// density, and switching between the Flow, Layout and Workflows surfaces.
[<RequireQualifiedAccess>]
module internal LayoutInteraction =
    /// Runs a Layout command against the open page's root stack.
    let private onRoot (state: EditorState) (build: Page -> ComponentNode -> Command * string) =
        match EditorState.openPage state with
        | Some page ->
            match page.Nodes |> List.tryFind (fun n -> n.Component = "stack") with
            | Some root -> let command, message = build page root in run command message state
            | None -> status "This page has no stack to edit." state
        | None -> status "Open a Layout page first." state

    let private reorder (state: EditorState) (key: string) earlier =
        onRoot state (fun page root ->
            match EditorIntents.reorder page root (key.Split('|').[0]) earlier with
            | Some command -> command, "Reordered."
            | None -> EditorIntents.nothing, "Nothing to reorder.")

    let update (event: LayoutEvent) (args: EventArgs) (state: EditorState) : EditorState =
        let key, value = args.Key, args.Value
        match event with
        | LayoutEvent.AddPage ->
            let number, pageId, command = EditorIntents.addPage (EditorState.project state)
            let created = run command (sprintf "Page %d added with an empty stack." number) state
            { created with Page = (if created.Session.Project <> state.Session.Project then Some pageId else state.Page) }
        | LayoutEvent.OpenPage -> { state with Page = Id.create<PageKind> key |> Result.toOption; WorkflowVisible = false; Status = "Layout page opened." }
        | LayoutEvent.OpenDiagram -> { state with Page = None; WorkflowVisible = false; Status = "Diagram opened." }
        | LayoutEvent.LayoutAddHeading -> onRoot state (fun page root -> EditorIntents.addHeading page root, "Heading added.")
        | LayoutEvent.LayoutSetText ->
            // key is "<component id>" (heading text) or "<component id>|<content key>".
            let componentKey, contentKey = match key.Split('|') with [| c; k |] -> c, k | _ -> key, "text"
            match state.Page, Id.create<ComponentKind> componentKey with
            | Some page, Ok id -> run (Layout(SetComponentContent(page, id, contentKey, value))) "Text changed." state
            | _ -> state
        | LayoutEvent.LayoutTarget -> { state with LayoutTarget = (if key = "" || key = "root" then None else Some key) }
        | LayoutEvent.LayoutAddComponent ->
            match Components.tryFind key with
            | None -> status "That component is not in Studio's Forma catalog." state
            | Some contract ->
                let workflow = WorkflowLibrary.current state.Workflows |> Option.map _.Id
                onRoot state (fun page root -> EditorIntents.addComponent page root state.LayoutTarget workflow contract, sprintf "Added %s." contract.Id)
        | LayoutEvent.LayoutMoveUp -> reorder state key true
        | LayoutEvent.LayoutMoveDown -> reorder state key false
        | LayoutEvent.LayoutDensity ->
            onRoot state (fun page root -> Layout(SetComponentProperty(page.Id, root.Id, "density", Some(JString key))), "Spacing changed.")
