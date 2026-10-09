namespace FormaStudio.Engine

/// The editor's view of the pinned Forma icon collection and of the icon picker.
/// It is view state: it never enters the project document or its history.
type IconSession =
    { /// The verified collection, or why it is unavailable (not loaded yet at startup).
      Availability: IconAvailability
      /// Files received so far while the host fetches the collection.
      Loading: IconLoad option
      /// The object whose icon the picker is choosing; None when the picker is closed.
      Target: ObjectRef option
      /// The picker's search text.
      Query: string }

[<RequireQualifiedAccess>]
module IconSession =
    /// Where the editor's page serves the pinned release's compiled icons
    /// (scripts/build-app.mjs copies them from the pinned Forma package).
    let assetBase = "./forma/icons/"

    let initial = { Availability = IconsNotLoaded; Loading = None; Target = None; Query = "" }

    let catalog (session: IconSession) =
        match session.Availability with
        | IconsAvailable catalog -> Some catalog
        | _ -> None
