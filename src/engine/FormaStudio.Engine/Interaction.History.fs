namespace FormaStudio.Engine

open Interaction

/// Undo and redo over the one session history.
[<RequireQualifiedAccess>]
module internal HistoryInteraction =
    let update (event: HistoryEvent) (_: EventArgs) (state: EditorState) : EditorState =
        match event with
        | HistoryEvent.Undo ->
            if Editor.canUndo state.Session then { state with Session = Editor.undo state.Session; Pending = NoPending; Status = "Undone." }
            else status "Nothing to undo." state
        | HistoryEvent.Redo ->
            if Editor.canRedo state.Session then { state with Session = Editor.redo state.Session; Pending = NoPending; Status = "Redone." }
            else status "Nothing to redo." state
