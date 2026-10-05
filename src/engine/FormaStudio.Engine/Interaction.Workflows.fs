namespace FormaStudio.Engine

open Interaction

/// Portable Forma workflows, edited in the public <forma-workflow> component:
/// Studio adopts each validated change it reports and persists the library.
[<RequireQualifiedAccess>]
module internal WorkflowInteraction =
    let update (event: WorkflowEvent) (args: EventArgs) (state: EditorState) : EditorState * JsonValue list =
        let key, value = args.Key, args.Value
        let only s = s, []
        match event with
        | WorkflowEvent.OpenWorkflows -> only { state with WorkflowVisible = true; Status = "Workflows opened." }
        | WorkflowEvent.WorkflowNew ->
            match WorkflowLibrary.blank "New workflow" state.Workflows with
            | Ok(lib, entry) -> only { state with Workflows = lib; WorkflowVisible = true; Status = $"Created {WorkflowLibrary.fileName entry}." }
            | Error message -> only (status message state)
        | WorkflowEvent.WorkflowOpened ->
            match WorkflowLibrary.openText value state.Workflows with
            | Ok(lib, entry) ->
                only { state with Workflows = lib; WorkflowVisible = true; Status = $"Opened {entry.Title} ({entry.Class}); unknown metadata and extensions are kept as they are." }
            | Error message -> only (status message state)
        | WorkflowEvent.WorkflowChanged ->
            match WorkflowLibrary.adoptChange value state.Workflows with
            | Ok(lib, entry) -> only { state with Workflows = lib; Status = $"{entry.Title}: change kept ({entry.Class})." }
            | Error message -> only (status message state)
        | WorkflowEvent.WorkflowSelect ->
            match WorkflowLibrary.select key state.Workflows with
            | Some lib -> only { state with Workflows = lib; WorkflowVisible = true; Status = "Workflow opened." }
            | None -> only (status "That workflow is not open." state)
        | WorkflowEvent.WorkflowClose -> only { state with Workflows = WorkflowLibrary.close key state.Workflows; Status = "Workflow closed." }
        | WorkflowEvent.WorkflowSave -> status "Saving workflows…" state, [ HostEffects.saveWorkflows state.Workflows ]
        | WorkflowEvent.WorkflowLoad -> status "Opening saved workflows…" state, [ HostEffects.loadWorkflows ]
