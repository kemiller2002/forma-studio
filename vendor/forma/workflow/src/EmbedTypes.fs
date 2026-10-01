namespace Forma.Workflow

open System

/// A gesture waiting for its second step (non-drag connect and reconnect).
type Pending =
    | NoPending
    | Connecting of Endpoint
    | Reconnecting of ObjectId * EdgeEnd

/// A DOM event translated by the host element into data. It carries no meaning;
/// the engine decides what, if anything, it does.
type UiEvent =
    { Name: string
      Key: string option
      Arg: string option
      Value: string option
      Fields: (string * string) list
      Toggle: bool
      Shift: bool }

/// Messages from the host (the <forma-workflow> element or a .NET host).
type HostMessage =
    | Init of mode: HostMode * document: string option
    | Load of document: string
    | SetMode of HostMode
    | Execute of EditCommand
    | Undo
    | Redo
    | SelectObjects of ObjectRef list
    | FocusObject of ObjectRef
    | SetRuntimeState of (ObjectId * Status option) list
    | Ui of UiEvent

/// What the engine tells the host happened. These are the public events.
type Emission =
    | Loaded of ValidationReport
    | Changed of Change
    | SelectionChanged of ObjectRef list
    | FocusChanged of ObjectRef option
    | IntentActivated of ObjectRef * Interaction
    | Picked of ObjectRef list
    | Refused of string

type EmbedState =
    { Mode: HostMode
      Workflow: Workflow option
      Report: ValidationReport option
      History: History
      Selection: ObjectRef list
      Focus: ObjectRef option
      Pending: Pending
      /// Percent; view state, never stored in the document.
      Zoom: int
      /// Runtime-visualization overlay; never stored in the document.
      Runtime: Map<ObjectId, Status>
      AddKind: CoreNodeKind
      Announcement: string
      IdPrefix: string }

type EmbedOutput =
    { State: EmbedState
      Html: string
      /// Element id the host should focus after rendering, if any.
      FocusElement: string option
      Emissions: Emission list }
