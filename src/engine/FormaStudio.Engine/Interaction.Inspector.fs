namespace FormaStudio.Engine

open Interaction

/// Inspector interaction on the selection: metadata values, fill, palette,
/// named styles, color rules and lanes.
[<RequireQualifiedAccess>]
module internal InspectorInteraction =
    /// Metadata edits apply to the whole selection as one atomic command; if any
    /// selected object cannot take the value, nothing changes (FDA-983).
    let private withField (state: EditorState) f =
        match state.Session.Selection, state.Field |> Option.bind (fun k -> ProjectOps.tryField k (EditorState.project state)) with
        | (_ :: _ as targets), Some field -> f targets field
        | [], _ -> status "Select an item first." state
        | _, None -> status "Choose a metadata field first." state

    let private createMappingRule (state: EditorState) =
        let p = EditorState.project state
        match state.Field |> Option.bind (fun k -> ProjectOps.tryField k p), state.Drafts.MappingValue, state.Drafts.MappingSlot with
        | Some({ Type = EnumField options } as field), Some value, Some slotKey ->
            match options |> List.tryFind (fun o -> o.Id = value), Id.create<PaletteKind> slotKey with
            | Some option, Ok slot -> run (EditorIntents.mappingRule p field option slot) (sprintf "Items whose %s is %s now take this fill." field.Name option.Label) state
            | _ -> status "Choose a value and a palette slot." state
        | Some _, _, _ -> status "Mappings work on choice fields: choose a value and a palette slot." state
        | None, _, _ -> status "Choose a metadata field first." state

    let update (event: InspectorEvent) (args: EventArgs) (state: EditorState) : EditorState =
        let key, value = args.Key, args.Value
        match event with
        | InspectorEvent.ChooseField -> { state with Field = Id.create<FieldKind> key |> Result.toOption }
        | InspectorEvent.SetFieldValue ->
            withField state (fun target field ->
                match EditorIntents.parseValue field value with
                | Ok v -> run (MetadataCmd(SetValue(target, field.Key, Explicit v))) (sprintf "%s set." field.Name) state
                | Error message -> status (sprintf "Not changed: %s" message) state)
        | InspectorEvent.SetFieldOption ->
            withField state (fun target field -> run (MetadataCmd(SetValue(target, field.Key, Explicit(Enum key)))) (sprintf "%s set." field.Name) state)
        | InspectorEvent.SetFieldUnknown ->
            withField state (fun target field -> run (MetadataCmd(SetValue(target, field.Key, UnknownValue))) (sprintf "%s marked unknown." field.Name) state)
        | InspectorEvent.ClearField ->
            withField state (fun target field -> run (MetadataCmd(ClearValue(target, field.Key))) (sprintf "%s cleared; the default or derived value shows through." field.Name) state)
        | InspectorEvent.SetFill ->
            match state.Session.Selection, HexColor.parse value with
            | (_ :: _ as targets), Ok hex -> run (AppearanceCmd(SetOverride(targets, { Appearance.empty with Fill = Some(LiteralColor hex) }))) "Fill set." state
            | _ :: _, Error message -> status (sprintf "Not changed: %s" message) state
            | [], _ -> status "Select an item first." state
        | InspectorEvent.ChooseFillPalette ->
            match state.Session.Selection, Id.create<PaletteKind> key with
            | (_ :: _ as targets), Ok slot -> run (AppearanceCmd(SetOverride(targets, { Appearance.empty with Fill = Some(PaletteColor slot) }))) "Fill set from the palette." state
            | _ -> status "Select an item and a palette slot." state
        | InspectorEvent.ResetFill ->
            match state.Session.Selection with
            | _ :: _ as targets -> run (AppearanceCmd(ResetOverride(targets, [ FillProperty ]))) "Fill override removed; the next layer shows through." state
            | [] -> status "Select an item first." state
        | InspectorEvent.ChooseStyle ->
            match EditorState.selectedRef state with
            | Some target ->
                let style = if key = "" then None else Id.create<StyleKind> key |> Result.toOption
                run (AppearanceCmd(ApplyStyle([ target ], style))) (if style.IsSome then "Style applied." else "Style removed.") state
            | None -> status "Select an item first." state
        | InspectorEvent.MappingValue -> { state with Drafts = { state.Drafts with MappingValue = Some key } }
        | InspectorEvent.MappingSlot -> { state with Drafts = { state.Drafts with MappingSlot = Some key } }
        | InspectorEvent.CreateMappingRule -> createMappingRule state
        | InspectorEvent.AssignLane ->
            match EditorState.selectedNode state with
            | Some node ->
                let lane = if key = "" then None else Id.create<GroupKind> key |> Result.toOption
                run (Flow(AssignLane(state.Diagram, node.Id, lane))) "Lane changed." state
            | None -> status "Select an item to change its lane." state
