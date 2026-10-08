namespace FormaStudio.Engine

open Interaction

/// The pinned Forma icon collection and the icon picker. The collection is
/// fetched through Limen from the files the app ships from the pinned Forma
/// package, verified (IconCatalog), and offered through native buttons. Setting
/// and clearing an icon is one canonical command (AppearanceCommand.SetIcon), so
/// it shares history, review and persistence with every other edit.
[<RequireQualifiedAccess>]
module internal IconInteraction =
    let private registryCorrelation = "icon-registry"
    let private clipboardCorrelation = "icon-clipboard"

    let private assetCorrelation kind (name: IconName) =
        (match kind with StaticSvg -> "icon-svg:" | InlineSnippet -> "icon-html:") + IconName.value name

    let private assetOf (correlation: string) =
        let named (prefix: string) kind =
            if correlation.StartsWith prefix then IconName.parse (correlation.Substring prefix.Length) |> Result.toOption |> Option.map (fun n -> n, kind) else None
        named "icon-svg:" StaticSvg |> Option.orElse (named "icon-html:" InlineSnippet)

    let private fetch correlation (path: string) =
        JObject
            [ "kind", JString "Http"; "correlationId", JString correlation; "method", JString "GET"; "url", JString(IconSession.assetBase + path)
              "timeoutMs", Json.ofInt 15000; "response", JString "text" ]

    let private withIcons (state: EditorState) (icons: IconSession) = { state with Icons = icons }

    /// Requests the pinned registry once, when the browser kernel starts the engine.
    let initialize (state: EditorState) : EditorState * JsonValue list =
        match state.Icons.Availability, state.Icons.Loading with
        | IconsNotLoaded, None -> state, [ fetch registryCorrelation "registry.json" ]
        | _ -> state, []

    /// The text body of a successful (HTTP 200) Limen result, or why there is none.
    let private body (result: JsonValue) =
        let outcome = Json.field "outcome" result
        match outcome |> Option.bind (Json.field "kind"), outcome |> Option.bind (Json.field "status"), outcome |> Option.bind (Json.field "body") with
        | Some(JString "Success"), Some(JNumber "200"), Some(JString text) -> Ok text
        | Some(JString "Success"), Some(JNumber status), _ -> Error(sprintf "the host answered HTTP %s" status)
        | _ -> Error "the host could not fetch it"

    let private settle availability (state: EditorState) = withIcons state { state.Icons with Availability = availability; Loading = None }

    let private onRegistry (fetched: Result<string, string>) (state: EditorState) =
        match fetched |> Result.map IconRegistry.parse with
        | Error _ -> settle (IconsUnavailable IconCatalog.notInRelease) state, []
        | Ok(Error why) -> settle (IconsUnavailable(sprintf "The pinned Forma icon registry was refused (%s), so icons are unavailable." why)) state, []
        | Ok(Ok registry) when List.isEmpty registry.Entries -> settle (IconCatalog.finish (IconCatalog.start registry)) state, []
        | Ok(Ok registry) ->
            withIcons state { state.Icons with Loading = Some(IconCatalog.start registry) },
            registry.Entries
            |> List.collect (fun e -> [ StaticSvg; InlineSnippet ] |> List.map (fun kind -> fetch (assetCorrelation kind e.Name) (IconRegistry.assetPath kind e.Name)))

    let private onAsset name kind fetched (state: EditorState) =
        match state.Icons.Loading |> Option.map (IconCatalog.receive name kind fetched) with
        | Some load when IconCatalog.isComplete load -> settle (IconCatalog.finish load) state
        | Some load -> withIcons state { state.Icons with Loading = Some load }
        | None -> state

    /// Handles the Limen result of an icon request; None when the result is not one.
    let onResult (result: JsonValue) (state: EditorState) : (EditorState * JsonValue list) option =
        match Json.field "correlationId" result with
        | Some(JString id) when id = registryCorrelation -> Some(onRegistry (body result) state)
        | Some(JString id) when id = clipboardCorrelation ->
            let copied = match Json.field "outcome" result |> Option.bind (Json.field "kind") with Some(JString "Success") -> true | _ -> false
            Some(status (if copied then "Copied the icon's inline HTML. It is decorative: give the control it decorates its own name." else "The browser could not copy the icon.") state, [])
        | Some(JString id) -> assetOf id |> Option.map (fun (name, kind) -> onAsset name kind (body result) state, [])
        | _ -> None

    /// The object a pick event names: a Layout item by key, otherwise the selected diagram node.
    let private targetOf (state: EditorState) (key: string) =
        if key = "" then EditorState.selectedRef state |> Option.filter (function NodeRef _ -> true | _ -> false)
        else
            match state.Page, Id.create<ComponentKind> (key.Split('|').[0]) with
            | Some page, Ok id when (ProjectOps.tryComponent page id (EditorState.project state)).IsSome -> Some(ComponentRef(page, id))
            | _ -> None

    let private unavailable (state: EditorState) =
        match state.Icons.Availability with
        | IconsUnavailable why -> why
        | IconsNotLoaded -> "The pinned Forma icons are still loading."
        | IconsAvailable _ -> ""

    let private copy (state: EditorState) =
        let current = state.Icons.Target |> Option.bind (fun r -> ProjectOps.iconOf r (EditorState.project state)) |> Option.bind IconRef.tryName
        match current, IconSession.catalog state.Icons with
        | Some name, Some catalog ->
            match IconCatalog.artwork name catalog with
            | Some markup ->
                status "Copying the icon's inline HTML…" state,
                [ JObject [ "kind", JString "Clipboard"; "correlationId", JString clipboardCorrelation; "operation", JString "writeText"; "text", JString(Forma.Workflow.Markup.toCompactHtml markup) ] ]
            | None -> status "This icon is not in the pinned Forma release, so there is nothing to copy." state, []
        | _ -> status "Choose an icon from the pinned Forma release first." state, []

    let update (event: IconEvent) (args: EventArgs) (state: EditorState) : EditorState * JsonValue list =
        let icons = state.Icons
        let only s = s, []
        match event with
        | IconEvent.IconPick ->
            match targetOf state args.Key with
            | Some target ->
                let note = match unavailable state with "" -> "Choose an icon below." | why -> why
                only { withIcons state { icons with Target = Some target; Query = "" } with Status = sprintf "Icon picker opened. %s" note }
            | None -> only (status "Select a diagram item, or choose a Layout item, to give it an icon." state)
        | IconEvent.IconSearch -> only (withIcons state { icons with Query = args.Value })
        | IconEvent.IconChoose ->
            match icons.Target, IconName.parse args.Key, IconSession.catalog icons with
            | None, _, _ -> only (status "Open the icon picker for an item first." state)
            | Some _, _, None -> only (status (unavailable state) state)
            | Some target, Ok name, Some catalog ->
                match IconCatalog.tryEntry name catalog with
                | Some entry -> only (run (AppearanceCmd(SetIcon(target, Some name))) (sprintf "Icon set to %s." entry.Label) state)
                | None -> only (status "That icon is not in the pinned Forma release." state)
            | Some _, Error _, Some _ -> only (status "That is not a Forma icon name." state)
        | IconEvent.IconClear ->
            match icons.Target with
            | Some target -> only (run (AppearanceCmd(SetIcon(target, None))) "Icon removed." state)
            | None -> only (status "Open the icon picker for an item first." state)
        | IconEvent.IconCopy -> copy state
        | IconEvent.IconClose -> only { withIcons state { icons with Target = None; Query = "" } with Status = "Icon picker closed." }
