namespace FormaStudio.Engine

/// The browser capabilities the editor asks Limen for, identified by a typed
/// correlation whose wire id is derived from the case name.
[<RequireQualifiedAccess>]
type Correlation =
    | Save
    | Load
    | Compare
    | WorkflowsSave
    | WorkflowsLoad
    | CopyExport

/// Host-effect requests and the handling of their results. Effect requests are
/// plain Limen JSON; nothing here touches the project except through the state
/// the results produce.
[<RequireQualifiedAccess>]
module HostEffects =
    let storageKey = "forma-studio.project"

    /// Portable workflows persist under their own key, as their own documents.
    let workflowStorageKey = "forma-studio.workflows"

    let private correlations = WireName.table<Correlation> ()
    let correlationId (c: Correlation) = correlations |> List.find (fst >> (=) c) |> snd
    let tryCorrelation (id: string) = correlations |> List.tryFind (snd >> (=) id) |> Option.map fst

    let private storageAt key correlation operation extra =
        JObject([ "kind", JString "Storage"; "correlationId", JString(correlationId correlation); "operation", JString operation; "key", JString key ] @ extra)

    let saveProject (project: Project) = storageAt storageKey Correlation.Save "set" [ "value", JString(Codec.serialize project) ]
    let loadProject = storageAt storageKey Correlation.Load "get" []
    let compareSaved = storageAt storageKey Correlation.Compare "get" []
    let saveWorkflows (lib: WorkflowLibrary) = storageAt workflowStorageKey Correlation.WorkflowsSave "set" [ "value", JString(WorkflowLibrary.toStorage lib) ]
    let loadWorkflows = storageAt workflowStorageKey Correlation.WorkflowsLoad "get" []

    let copyText (text: string) =
        JObject [ "kind", JString "Clipboard"; "correlationId", JString(correlationId Correlation.CopyExport); "operation", JString "writeText"; "text", JString text ]

    /// Applies one Limen effect result. Unknown correlations leave the state as is;
    /// any failure reports that the browser could not complete the request.
    let onResult (result: JsonValue) (state: EditorState) =
        let field name json = Json.field name json
        let value () = field "outcome" result |> Option.bind (field "value")
        match field "correlationId" result, field "outcome" result |> Option.bind (field "kind") with
        | Some(JString id), Some(JString "Success") ->
            match tryCorrelation id with
            | Some Correlation.Save -> { state with Status = "Saved."; Baseline = defaultArg state.Saving state.Baseline; Saving = None }
            | Some Correlation.Load ->
                match value () with
                | Some(JString text) ->
                    match Codec.load text with
                    | Ok loaded ->
                        // Reopening restores the canonical project, not the undo stack (FDA-1051).
                        let diagram = loaded.Diagrams |> List.tryHead |> Option.map _.Id |> Option.defaultValue state.Diagram
                        { EditorState.initial loaded diagram with Status = "Loaded the saved project. Undo history starts fresh."; Icons = { state.Icons with Target = None; Query = "" } }
                    | Error error -> { state with Status = Codec.describeLoadError error }
                | _ -> { state with Status = "Nothing has been saved yet." }
            | Some Correlation.Compare ->
                match value () with
                | Some(JString text) ->
                    match Codec.load text with
                    | Ok saved when saved = state.Baseline -> { state with Incoming = None; Status = "The saved copy has not changed since you opened or saved it." }
                    | Ok saved -> { state with Incoming = Some saved; TakeSaved = Set.empty; Status = "The saved copy has changed. Review the merge below." }
                    | Error error -> { state with Status = Codec.describeLoadError error }
                | _ -> { state with Status = "Nothing has been saved yet." }
            | Some Correlation.WorkflowsSave ->
                { state with Status = $"Saved {state.Workflows.Entries.Length} workflow(s) in this browser."; Workflows = { state.Workflows with Unsaved = Set.empty } }
            | Some Correlation.WorkflowsLoad ->
                match value () with
                | Some(JString text) ->
                    match WorkflowLibrary.ofStorage text state.Workflows with
                    | Ok lib -> { state with Workflows = lib; WorkflowVisible = true; Status = $"Opened {lib.Entries.Length} saved workflow(s)." }
                    | Error message -> { state with Status = message }
                | _ -> { state with Status = "No workflows have been saved in this browser yet." }
            | Some Correlation.CopyExport -> { state with Status = "Copied the exported HTML." }
            | None -> state
        | Some(JString _), Some(JString "Failure") -> { state with Status = "The browser could not complete the storage request."; Saving = None }
        | _ -> state
