namespace FormaStudio.Engine

/// The icon picker and icon previews, projected from the editor state for the
/// Limen view (one panel of EditorView). Pure. Previews use the pinned
/// release's static SVG files, decorative (`alt=""`): a control's accessible
/// name always comes from its text, never from an icon (Forma ICON-008).
[<RequireQualifiedAccess>]
module IconView =
    let private str (s: string) = JString s

    /// The pinned release's static SVG for a verified icon, or "" when there is none to show.
    let private source (session: IconSession) (icon: IconRef option) =
        match IconSession.catalog session, icon with
        | Some catalog, Some(NamedIcon name) when catalog.Artwork.ContainsKey name -> IconSession.assetBase + IconRegistry.assetPath StaticSvg name
        | _ -> ""

    /// `iconShown` drives a `data-if`, so no image (and no request) exists without an icon to show.
    let private preview (session: IconSession) (icon: IconRef option) =
        let src = source session icon
        [ "iconSrc", str src; "iconShown", JBool(src <> "") ]

    let private describe (session: IconSession) icon = IconCatalog.describe (IconCatalog.status session.Availability icon)

    /// Fields for one Layout item: its icon preview, the state of its icon, and
    /// a specific name for its "Choose icon" button.
    let layoutItem (state: EditorState) (node: ComponentNode) =
        let label = sprintf "%s %s" node.Component (Id.value node.Id)
        let icon = ProjectOps.componentIcon node
        preview state.Icons icon
        @ [ "iconText", str (sprintf "Icon: %s" (describe state.Icons icon))
            "iconPickLabel", str (sprintf "Choose icon for %s" label)
            "iconPickHidden", JBool(not (Components.supportsIcon node.Component))
            // A component with no place for an icon shows the line only to explain a kept icon.
            "iconLineHidden", JBool(not (Components.supportsIcon node.Component) && icon.IsNone) ]

    /// Fields for one canvas node: its icon preview.
    let canvasNode (state: EditorState) (node: DiagramNode) = preview state.Icons node.Icon

    let private targetLabel (project: Project) (target: ObjectRef) =
        match target with
        | ComponentRef(page, id) -> ProjectOps.tryComponent page id project |> Option.map (fun n -> sprintf "%s %s" n.Component (Id.value n.Id))
        | NodeRef(diagram, id) -> ProjectOps.tryNode diagram id project |> Option.map _.Label
        | _ -> None

    let private availability (session: IconSession) (shown: int) =
        match session.Availability with
        | IconsNotLoaded -> "Loading the pinned Forma icons…"
        | IconsUnavailable why -> why
        | IconsAvailable c ->
            let refused = if List.isEmpty c.Rejected then "" else sprintf " %d icon(s) failed verification and are not offered." c.Rejected.Length
            sprintf "%d of %d icons from Forma %s match.%s" shown c.Entries.Length c.FormaVersion refused

    /// The picker panel and the selected node's icon line.
    let fields (state: EditorState) : (string * JsonValue) list =
        let project = EditorState.project state
        let session = state.Icons
        // The picker shows beside the object it edits, only while that object exists.
        let target = session.Target |> Option.filter (fun t -> ProjectOps.exists t project)
        let current = target |> Option.bind (fun t -> ProjectOps.iconOf t project)
        let onLayout = match target with Some(ComponentRef(page, _)) -> state.Page = Some page && not state.WorkflowVisible | _ -> false
        let onNode = match target with Some(NodeRef _ as r) -> EditorState.selectedRef state = Some r && state.Page.IsNone && not state.WorkflowVisible | _ -> false
        let choices = IconSession.catalog session |> Option.map (IconCatalog.search session.Query) |> Option.defaultValue []
        let currentName = current |> Option.bind IconRef.tryName
        let selectedNodeIcon = match EditorState.selectedNode state with Some n -> n.Icon | None -> None
        [ "iconPickerLayout", JBool onLayout
          "iconPickerNode", JBool onNode
          "iconTargetLabel", str (target |> Option.bind (targetLabel project) |> Option.defaultValue "")
          "iconCurrent", str (describe session current)
          "iconAvailability", str (availability session choices.Length)
          "iconQuery", str session.Query
          "iconSearchDisabled", JBool(IconSession.catalog session |> Option.isNone)
          "iconChoicesLabel", str (IconSession.catalog session |> Option.map (fun c -> sprintf "Icons in Forma %s" c.FormaVersion) |> Option.defaultValue "Icons")
          "iconChoices",
          choices
          |> List.map (fun e ->
              JObject
                  [ "key", str (IconName.value e.Name)
                    "label", str e.Label
                    "src", str (source session (Some(NamedIcon e.Name)))
                    "pressed", str (if currentName = Some e.Name then "true" else "false") ])
          |> JArray
          "iconClearDisabled", JBool current.IsNone
          "iconCopyDisabled", JBool(source session current = "")
          "nodeIconText", str (sprintf "Icon: %s" (describe session selectedNodeIcon)) ]
