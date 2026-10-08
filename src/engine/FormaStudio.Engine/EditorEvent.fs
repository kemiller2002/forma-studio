namespace FormaStudio.Engine

open Microsoft.FSharp.Reflection

// The editor's semantic event vocabulary: the single source of truth for every
// event the browser may send (forma-studio#18, FST-F2). Wire names are derived
// from the case names (PascalCase -> kebab-case) and never written by hand on
// the engine side; `index.html` and the gesture/workflow adapters bind to those
// names, and EditorEventContractTests proves every name they dispatch maps to a
// case here and every case round-trips to its wire name.

/// Canvas: selection, structure edits, gestures, view preferences, arrangement.
[<RequireQualifiedAccess>]
type CanvasEvent =
    | Select
    | ClearSelection
    | ToggleSelect
    | ConnectStart
    | Cancel
    | ChooseKind
    | AddNode
    | SetLabel
    | Delete
    | MoveLeft
    | MoveRight
    | MoveUp
    | MoveDown
    | GestureMove
    | GestureResize
    | SetWidth
    | SetHeight
    | ReconnectEnd
    | GestureReconnect
    | ZoomIn
    | ZoomOut
    | ZoomReset
    | ToggleSnap
    | AlignLeft
    | AlignTop
    | DistributeHorizontally

/// Templates and the copy clipboard.
[<RequireQualifiedAccess>]
type TemplateEvent =
    | CopySelection
    | ChooseTemplate
    | InsertTemplate

/// Undo and redo over the one shared session history.
[<RequireQualifiedAccess>]
type HistoryEvent =
    | Undo
    | Redo

/// Inspector: metadata values, appearance, color rules and lanes of the selection.
[<RequireQualifiedAccess>]
type InspectorEvent =
    | ChooseField
    | SetFieldValue
    | SetFieldOption
    | SetFieldUnknown
    | ClearField
    | SetFill
    | ChooseFillPalette
    | ResetFill
    | ChooseStyle
    | MappingValue
    | MappingSlot
    | CreateMappingRule
    | AssignLane

/// Project definitions: new fields and palette slots.
[<RequireQualifiedAccess>]
type DefinitionEvent =
    | DraftFieldName
    | DraftFieldType
    | DraftFieldOptions
    | DraftFieldScope
    | CreateField
    | DraftSlotName
    | DraftSlotColor
    | CreateSlot
    | DeleteSlot
    | MaterializeSlot

/// Layout pages and surface navigation.
[<RequireQualifiedAccess>]
type LayoutEvent =
    | AddPage
    | OpenPage
    | OpenDiagram
    | LayoutAddHeading
    | LayoutSetText
    | LayoutTarget
    | LayoutAddComponent
    | LayoutMoveUp
    | LayoutMoveDown
    | LayoutDensity

/// Portable Forma workflows.
[<RequireQualifiedAccess>]
type WorkflowEvent =
    | OpenWorkflows
    | WorkflowNew
    | WorkflowOpened
    | WorkflowChanged
    | WorkflowSelect
    | WorkflowClose
    | WorkflowSave
    | WorkflowLoad

/// Standards-based HTML export.
[<RequireQualifiedAccess>]
type ExportEvent =
    | ExportTarget
    | ExportInteractive
    | ExportBrand
    | ExportPage
    | ExportComponent
    | ExportWorkflow
    | CopyExport

/// Persistence and merge review against the saved copy.
[<RequireQualifiedAccess>]
type ReviewEvent =
    | Save
    | Load
    | CheckSaved
    | MergeTakeSaved
    | MergeKeepMine
    | MergeCancel
    | MergeApply

/// Every semantic event the editor handles, grouped by the area that updates it.
[<RequireQualifiedAccess>]
type EditorEvent =
    | Canvas of CanvasEvent
    | Templates of TemplateEvent
    | History of HistoryEvent
    | Inspector of InspectorEvent
    | Definitions of DefinitionEvent
    | Layout of LayoutEvent
    | Workflows of WorkflowEvent
    | Export of ExportEvent
    | Review of ReviewEvent
    | Icons of IconEvent

/// The untyped arguments of an event, as the Limen binding sends them. Absent
/// key or value arrive as "".
type EventArgs = { Key: string; Value: string }

[<RequireQualifiedAccess>]
module EditorEvent =
    /// Every event with its wire name, in declaration order. Built once from
    /// the union definitions above.
    let catalog: (EditorEvent * string) list =
        FSharpType.GetUnionCases(typeof<EditorEvent>, true)
        |> Array.toList
        |> List.collect (fun area ->
            let inner = (area.GetFields() |> Array.exactlyOne).PropertyType
            FSharpType.GetUnionCases(inner, true)
            |> Array.toList
            |> List.map (fun case ->
                match FSharpValue.MakeUnion(area, [| FSharpValue.MakeUnion(case, [||], true) |], true) with
                | :? EditorEvent as value -> value, WireName.ofCaseName case.Name
                | _ -> invalidOp (sprintf "%s.%s is not an editor event" area.Name case.Name)))

    let all = catalog |> List.map fst

    let private names = Map.ofList catalog
    let private byName = catalog |> List.map (fun (e, n) -> n, e) |> Map.ofList

    /// The wire name the browser sends for this event.
    let wireName (event: EditorEvent) = names.[event]

    /// The event for a wire name, or None for a name the editor does not handle.
    let tryParse (name: string) = byName.TryFind name

    /// The area an event belongs to (the outer case name).
    let area (event: EditorEvent) = (fst (FSharpValue.GetUnionFields(event, typeof<EditorEvent>, true))).Name
