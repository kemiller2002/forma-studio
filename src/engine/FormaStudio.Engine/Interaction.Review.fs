namespace FormaStudio.Engine

open Interaction

/// Persistence through Limen storage effects and graph-aware merge review
/// against the saved copy.
[<RequireQualifiedAccess>]
module internal ReviewInteraction =
    let private mergeApply (state: EditorState) =
        match state.Incoming with
        | Some saved ->
            let result = Merge.resolve state.Baseline (EditorState.project state) saved state.TakeSaved
            match result.Conflicts |> List.filter (Merge.isItemConflict >> not) with
            | [] ->
                match Editor.adopt result.Project state.Session with
                | Ok session ->
                    // The saved copy is now the common ancestor for the next comparison.
                    { state with Session = session; Baseline = saved; Incoming = None; TakeSaved = Set.empty; Status = "Merged the saved changes. One undo reverts the merge." }
                | Error findings -> status (sprintf "Not merged: %s" (describeFindings findings)) state
            | blocking -> status (sprintf "Not merged: %s" (blocking |> List.map _.Message |> String.concat " ")) state
        | None -> status "There is no saved change to merge." state

    let update (event: ReviewEvent) (args: EventArgs) (state: EditorState) : EditorState * JsonValue list =
        let key = args.Key
        let only s = s, []
        match event with
        | ReviewEvent.Save ->
            let project = EditorState.project state
            { state with Status = "Saving…"; Saving = Some project }, [ HostEffects.saveProject project ]
        | ReviewEvent.Load -> status "Loading…" state, [ HostEffects.loadProject ]
        | ReviewEvent.CheckSaved -> status "Checking the saved copy…" state, [ HostEffects.compareSaved ]
        | ReviewEvent.MergeTakeSaved -> only { state with TakeSaved = state.TakeSaved.Add key }
        | ReviewEvent.MergeKeepMine -> only { state with TakeSaved = state.TakeSaved.Remove key }
        | ReviewEvent.MergeCancel -> only { state with Incoming = None; TakeSaved = Set.empty; Status = "Merge cancelled; nothing changed." }
        | ReviewEvent.MergeApply -> only (mergeApply state)
