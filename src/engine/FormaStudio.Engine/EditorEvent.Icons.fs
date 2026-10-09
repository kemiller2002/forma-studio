namespace FormaStudio.Engine

/// The icon picker: the Forma icon of a Layout item or a diagram node, chosen
/// from the pinned release through native buttons and a search field. Part of
/// the editor's event vocabulary (EditorEvent.fs); routed like every other area.
[<RequireQualifiedAccess>]
type IconEvent =
    /// Opens the picker for a Layout item (key "<component id>|...") or, with no key, the selected node.
    | IconPick
    /// Filters the offered icons by name, category, label or keyword.
    | IconSearch
    /// Sets the icon named by the key on the picker's object.
    | IconChoose
    /// Removes the picker object's icon.
    | IconClear
    /// Copies the current icon's verified inline HTML to the clipboard.
    | IconCopy
    /// Closes the picker.
    | IconClose
