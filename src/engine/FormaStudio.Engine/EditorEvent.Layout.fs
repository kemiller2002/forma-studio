namespace FormaStudio.Engine

/// Layout pages and surface navigation. Part of the editor's event vocabulary
/// (EditorEvent.fs); routed like every other area. Component keys are
/// "<component id>|..." as the Layout view projects them.
[<RequireQualifiedAccess>]
type LayoutEvent =
    | AddPage
    | OpenPage
    | OpenDiagram
    | LayoutAddHeading
    /// Sets one content value (key "<component id>" or "<component id>|<content key>").
    | LayoutSetText
    | LayoutTarget
    | LayoutAddComponent
    | LayoutMoveUp
    | LayoutMoveDown
    | LayoutDensity
    /// Sets or clears one contract property (key "<component id>|<property>|<value>"; an empty value clears it).
    | LayoutSetProperty
    /// Removes a component and everything inside it (key "<component id>|...").
    | LayoutRemove
