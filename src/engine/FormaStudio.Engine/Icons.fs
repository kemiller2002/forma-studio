namespace FormaStudio.Engine

open System.Text.RegularExpressions

/// A Forma icon name in the registry grammar (lower-kebab-case, for example
/// `search` or `arrow-left`). It names an icon; it never carries geometry,
/// labels, state or behaviour, which stay with Forma and the application
/// (Forma ICON-006, ICON-014). Created only through `IconName.parse`.
type IconName = private IconName of string

/// The optional icon an authored object stores. Studio stores the name only.
/// Whether the pinned Forma release provides that name is decided when the
/// document is shown or exported, never when it is read, so a name from a
/// newer Forma release survives a round trip through an older Studio.
type IconRef =
    /// A well-formed icon name, known to the pinned release or not.
    | NamedIcon of IconName
    /// A stored value that is not a well-formed name, kept exactly as read.
    /// It is inert: never rendered, never exported as markup, never rewritten.
    | MalformedIcon of raw: JsonValue

[<RequireQualifiedAccess>]
module IconName =
    /// Registry names are short; anything longer is not a name.
    let maxLength = 64

    // \z, not $: in .NET `$` also matches before a trailing newline.
    let private pattern = Regex(@"\A[a-z][a-z0-9]*(?:-[a-z0-9]+)*\z", RegexOptions.CultureInvariant)

    let parse (raw: string) : Result<IconName, string> =
        if raw.Length <= maxLength && pattern.IsMatch raw then Ok(IconName raw)
        else Error "an icon name is lowercase letters and digits joined by single hyphens, starting with a letter, at most 64 characters"

    let value (IconName name) = name

[<RequireQualifiedAccess>]
module IconRef =
    /// Reads a stored icon value without ever rejecting or dropping it.
    let ofJson (json: JsonValue) =
        match json with
        | JString raw ->
            match IconName.parse raw with
            | Ok name -> NamedIcon name
            | Error _ -> MalformedIcon json
        | other -> MalformedIcon other

    /// The stored form: a name is written as its string, anything else exactly as it was read.
    let toJson (icon: IconRef) =
        match icon with
        | NamedIcon name -> JString(IconName.value name)
        | MalformedIcon raw -> raw

    let tryName (icon: IconRef) =
        match icon with
        | NamedIcon name -> Some name
        | MalformedIcon _ -> None
