namespace FormaStudio.Engine

open Interaction

/// Project definitions from forms, without JSON: new metadata fields
/// (FDA-1180..1191) and palette slots (FDA-1200..1209). The forms are drafts
/// (view state) until a create command succeeds.
[<RequireQualifiedAccess>]
module internal DefinitionInteraction =
    let private draftOf (d: Drafts) : FieldDraft = { Name = d.FieldName; Type = d.FieldType; Options = d.FieldOptions; Scope = d.FieldScope }

    let private describe =
        function
        | FieldNameMissing -> "Name the field first."
        | FieldTypeInvalid message -> sprintf "Not changed: %s" message

    let private createField (state: EditorState) =
        let draft = draftOf state.Drafts
        match EditorState.diagram state with
        | Some diagram ->
            match EditorIntents.defineField (EditorState.project state) diagram draft with
            | Ok(definition, command) ->
                let created = run command (sprintf "Field %s added." definition.Name) state
                { created with Field = Some definition.Key; Drafts = { created.Drafts with FieldName = ""; FieldOptions = "" } }
            | Error error -> status (describe error) state
        | None ->
            match EditorIntents.checkFieldDraft draft with
            | Error error -> status (describe error) state
            | Ok _ -> state

    let private createSlot (state: EditorState) =
        match HexColor.parse state.Drafts.SlotColor with
        | _ when System.String.IsNullOrWhiteSpace state.Drafts.SlotName -> status "Name the palette slot first." state
        | Error message -> status (sprintf "Not changed: %s" message) state
        | Ok hex ->
            let slot = EditorIntents.paletteSlot (EditorState.project state) state.Drafts.SlotName hex
            let created = run (AppearanceCmd(AddPaletteSlot slot)) (sprintf "Palette slot %s added." slot.Name) state
            { created with Drafts = { created.Drafts with SlotName = ""; SlotColor = "" } }

    let private removeSlot (state: EditorState) key policy message =
        match Id.create<PaletteKind> key with
        | Ok slot -> run (AppearanceCmd(RemovePaletteSlot(slot, policy))) message state
        | Error _ -> state

    let update (event: DefinitionEvent) (args: EventArgs) (state: EditorState) : EditorState =
        let key, value = args.Key, args.Value
        let draft f = { state with Drafts = f state.Drafts }
        match event with
        | DefinitionEvent.DraftFieldName -> draft (fun d -> { d with FieldName = value })
        | DefinitionEvent.DraftFieldType -> draft (fun d -> { d with FieldType = key })
        | DefinitionEvent.DraftFieldOptions -> draft (fun d -> { d with FieldOptions = value })
        | DefinitionEvent.DraftFieldScope ->
            let scope = EditorIntents.scopeChoices |> List.tryFind (fun (k, _, _) -> k = key) |> Option.map (fun (_, v, _) -> v) |> Option.defaultValue state.Drafts.FieldScope
            draft (fun d -> { d with FieldScope = scope })
        | DefinitionEvent.CreateField -> createField state
        | DefinitionEvent.DraftSlotName -> draft (fun d -> { d with SlotName = value })
        | DefinitionEvent.DraftSlotColor -> draft (fun d -> { d with SlotColor = value })
        | DefinitionEvent.CreateSlot -> createSlot state
        | DefinitionEvent.DeleteSlot -> removeSlot state key BlockPaletteIfUsed "Palette slot deleted."
        | DefinitionEvent.MaterializeSlot -> removeSlot state key MaterializePalette "Palette slot deleted; its color is now set directly where it was used."
