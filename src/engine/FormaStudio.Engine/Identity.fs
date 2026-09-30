namespace FormaStudio.Engine

open System.Text.RegularExpressions

// Marker types that keep identifier kinds apart at compile time. A PageId can
// never be passed where a NodeId is expected, although both serialize as strings.
type ProjectKind = private | ProjectKind
type PageKind = private | PageKind
type ComponentKind = private | ComponentKind
type DiagramKind = private | DiagramKind
type NodeKind = private | NodeKind
type EdgeKind = private | EdgeKind
type GroupKind = private | GroupKind
type PortKind = private | PortKind
type FieldKind = private | FieldKind
type PaletteKind = private | PaletteKind
type StyleKind = private | StyleKind
type MappingKind = private | MappingKind
type ReferenceKind = private | ReferenceKind

/// A stable identifier. It is independent of labels, routes, names and position,
/// and can only be created through `Id.create`, which enforces the project-format
/// pattern shared with schema v1.
type Id<'Kind> = private Id of string

type ProjectId = Id<ProjectKind>
type PageId = Id<PageKind>
type ComponentNodeId = Id<ComponentKind>
type DiagramId = Id<DiagramKind>
type NodeId = Id<NodeKind>
type EdgeId = Id<EdgeKind>
type GroupId = Id<GroupKind>
type PortId = Id<PortKind>
type FieldKey = Id<FieldKind>
type PaletteSlotId = Id<PaletteKind>
type StyleId = Id<StyleKind>
type MappingId = Id<MappingKind>
type ReferenceId = Id<ReferenceKind>

[<RequireQualifiedAccess>]
module Id =
    let private pattern = Regex("^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}$", RegexOptions.CultureInvariant)

    /// Validates a raw identifier. The pattern matches schema v1 so migrated ids stay valid.
    let create<'Kind> (raw: string) : Result<Id<'Kind>, string> =
        if pattern.IsMatch raw then Ok(Id raw)
        else Error(sprintf "'%s' is not a valid identifier (letters, digits, '.', '_', ':' or '-', starting with a letter or digit, at most 128 characters)" raw)

    let value (Id raw: Id<'Kind>) = raw

    /// Converts between identifier kinds when a reference is re-typed on purpose,
    /// for example when a typed reference names a diagram element.
    let retag<'From, 'To> (Id raw: Id<'From>) : Id<'To> = Id raw
