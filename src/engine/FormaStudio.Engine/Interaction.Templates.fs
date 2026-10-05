namespace FormaStudio.Engine

open Interaction

/// Templates and the copy clipboard: copying nodes with their dependency
/// closure, choosing a template and inserting it as one batch.
[<RequireQualifiedAccess>]
module internal TemplateInteraction =
    let private insertTemplate (state: EditorState) =
        match EditorState.chosenTemplate state with
        | Some(_, name, _, fragment) ->
            let dx, dy = EditorIntents.insertionOffset (EditorState.diagram state) fragment
            match Fragment.applyCommand fragment (EditorState.project state) state.Diagram dx dy RemapConflicting with
            | Ok(command, _) ->
                let added = match command with Batch(_, steps) -> steps |> List.choose (function Flow(AddNode(_, id, _, _, _, _, _, _)) -> Some(NodeRef(state.Diagram, id)) | _ -> None) | _ -> []
                let inserted = run command (sprintf "Inserted %s." name) state
                if inserted.Session.Project = state.Session.Project then inserted
                else { inserted with Session = Editor.select added inserted.Session; Template = None }
            | Error message -> status (sprintf "Not inserted: %s" message) state
        | None -> status "Choose a template to insert." state

    let update (event: TemplateEvent) (args: EventArgs) (state: EditorState) : EditorState =
        match event with
        | TemplateEvent.CopySelection ->
            let nodes = state.Session.Selection |> List.choose (function NodeRef(d, n) when d = state.Diagram -> Some n | _ -> None)
            if List.isEmpty nodes then status "Select items to copy." state
            else
                match Fragment.extract (EditorState.project state) state.Diagram nodes with
                | Ok fragment ->
                    let dependencies = fragment.Fields.Length + fragment.Palette.Length + fragment.Styles.Length + fragment.Mappings.Length
                    { state with Clipboard = Some fragment; Template = Some "clipboard"; Status = sprintf "Copied %d item(s) with %d dependency definition(s)." fragment.Nodes.Length dependencies }
                | Error message -> status (sprintf "Not copied: %s" message) state
        | TemplateEvent.ChooseTemplate -> { state with Template = Some args.Key }
        | TemplateEvent.InsertTemplate -> insertTemplate state
