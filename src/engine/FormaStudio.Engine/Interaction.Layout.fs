namespace FormaStudio.Engine

open Interaction

/// Layout pages: adding pages and components, editing content and properties,
/// reordering, removal, density, and switching between the Flow, Layout and
/// Workflows surfaces. Per-component edits are in LayoutComponentEdits.
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

    let update (event: LayoutEvent) (args: EventArgs) (state: EditorState) : EditorState =
        let key, value = args.Key, args.Value
        match event with
        | LayoutEvent.AddPage ->
            let number, pageId, command = EditorIntents.addPage (EditorState.project state)
            let created = run command (sprintf "Page %d added with an empty stack." number) state
            { created with Page = (if created.Session.Project <> state.Session.Project then Some pageId else state.Page) }
        | LayoutEvent.OpenPage -> { state with Page = Id.create<PageKind> key |> Result.toOption; WorkflowVisible = false; LayoutTarget = None; Status = "Layout page opened." }
        | LayoutEvent.OpenDiagram -> { state with Page = None; WorkflowVisible = false; Status = "Diagram opened." }
        | LayoutEvent.LayoutAddHeading -> onRoot state (fun page root -> EditorIntents.addHeading page root, "Heading added.")
        | LayoutEvent.LayoutSetText -> LayoutComponentEdits.setText key value state
        | LayoutEvent.LayoutTarget -> { state with LayoutTarget = (if key = "" || key = "root" then None else Some key) }
        | LayoutEvent.LayoutAddComponent ->
            match Components.tryFind key with
            | None -> status "That component is not in Studio's Forma catalog." state
            | Some contract ->
                let workflow = WorkflowLibrary.current state.Workflows |> Option.map _.Id
                onRoot state (fun page root -> EditorIntents.addComponent page root state.LayoutTarget workflow contract, sprintf "Added %s." contract.Id)
        | LayoutEvent.LayoutMoveUp -> LayoutComponentEdits.reorder key true state
        | LayoutEvent.LayoutMoveDown -> LayoutComponentEdits.reorder key false state
        | LayoutEvent.LayoutSetProperty -> LayoutComponentEdits.setProperty key state
        | LayoutEvent.LayoutRemove -> LayoutComponentEdits.remove key state
        | LayoutEvent.LayoutDensity ->
            onRoot state (fun page root -> Layout(SetComponentProperty(page.Id, root.Id, "density", Some(JString key))), "Spacing changed.")
