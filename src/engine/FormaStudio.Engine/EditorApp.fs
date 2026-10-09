namespace FormaStudio.Engine

/// The Flow editor as a Limen engine: browser messages in, a complete view plus
/// effect requests out. This module is only the transport entry point and a thin
/// dispatcher: events are parsed into the typed `EditorEvent` vocabulary and
/// routed to one per-area update function (Interaction.*.fs); command
/// construction policy lives in `EditorIntents`, effects in `HostEffects` and the
/// projection in `EditorView` (forma-studio#18). Every canonical change still
/// goes through Editor.dispatch (FDA-069, FDA-221).
[<RequireQualifiedAccess>]
module EditorApp =
    let view (state: EditorState) : JsonValue = EditorView.view state

    let private noEffects (state: EditorState) = state, ([]: JsonValue list)

    /// Routes one typed event to the update function of its area.
    let update (event: EditorEvent) (args: EventArgs) (state: EditorState) : EditorState * JsonValue list =
        match event with
        | EditorEvent.Canvas e -> CanvasInteraction.update e args state |> noEffects
        | EditorEvent.Templates e -> TemplateInteraction.update e args state |> noEffects
        | EditorEvent.History e -> HistoryInteraction.update e args state |> noEffects
        | EditorEvent.Inspector e -> InspectorInteraction.update e args state |> noEffects
        | EditorEvent.Definitions e -> DefinitionInteraction.update e args state |> noEffects
        | EditorEvent.Layout e -> LayoutInteraction.update e args state |> noEffects
        | EditorEvent.Workflows e -> WorkflowInteraction.update e args state
        | EditorEvent.Export e -> ExportInteraction.update e args state
        | EditorEvent.Review e -> ReviewInteraction.update e args state
        | EditorEvent.Icons e -> IconInteraction.update e args state

    /// The event payload of a Limen `Event` message: wire name and arguments.
    let private eventOf (message: JsonValue) =
        Json.field "event" message
        |> Option.map (fun e ->
            let text name = match Json.field name e with Some(JString s) -> s | _ -> ""
            text "name", { Key = text "key"; Value = text "value" })

    /// Handles one Limen `BrowserToEngineMessage` and returns the new state plus
    /// the complete `EngineToBrowserMessage` (view, effects, cancellations).
    let handle (message: JsonValue) (state: EditorState) : EditorState * JsonValue =
        let next, effects =
            match Json.field "kind" message with
            | Some(JString "Event") ->
                match eventOf message with
                | Some(name, args) ->
                    match EditorEvent.tryParse name with
                    | Some event -> update event args state
                    | None -> noEffects (Interaction.unrecognized name state)
                | None -> noEffects state
            | Some(JString "Initialize") -> IconInteraction.initialize state
            | Some(JString "EffectResult") ->
                match Json.field "result" message with
                | Some result -> IconInteraction.onResult result state |> Option.defaultWith (fun () -> noEffects (HostEffects.onResult result state))
                | None -> noEffects state
            | _ -> noEffects state
        next, JObject [ "view", view next; "effects", JArray effects; "cancellations", JArray [] ]

    /// Initial state for the hosted editor: the canonical Workflow sample.
    let start () =
        match Samples.purchaseWorkflow () with
        | Ok project -> EditorState.initial project Samples.diagramId
        | Error findings -> { EditorState.initial (Samples.emptyProject "empty" "Empty project") Samples.diagramId with Status = Interaction.describeFindings findings }

    /// Text entry point for the WebAssembly glue: JSON message in, JSON response out.
    /// Malformed input leaves the state unchanged and reports it in the view.
    let dispatchText (messageJson: string) (state: EditorState) : EditorState * string =
        match Json.parse messageJson with
        | Ok message ->
            let next, response = handle message state
            next, Json.serialize response
        | Error error ->
            let next = { state with Status = sprintf "Ignored a malformed message: %s" error }
            next, Json.serialize (JObject [ "view", view next; "effects", JArray []; "cancellations", JArray [] ])
