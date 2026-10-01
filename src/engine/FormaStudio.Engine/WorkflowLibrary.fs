namespace FormaStudio.Engine

/// One portable workflow document held by Studio. The text is the canonical
/// .forma-workflow.json produced by Forma's codec, so Studio never keeps a
/// private copy of the format (REQUIREMENTS.md "Portable workflows").
type WorkflowEntry =
    { Id: string
      Title: string
      /// Canonical document text.
      Text: string
      Class: string
      Valid: bool
      FindingCount: int }

/// Studio's set of open portable workflows. Editing happens in the public
/// Forma workflow component; Studio adopts each validated change it reports.
type WorkflowLibrary =
    { Entries: WorkflowEntry list
      Current: string option
      /// Increases when Studio asks the embedded component to load a document
      /// (open, new, switch, restore). Changes reported by the component do not
      /// increase it, so the component is never reloaded with its own edit.
      Revision: int
      /// Ids changed since the library was last saved or loaded.
      Unsaved: Set<string> }

[<RequireQualifiedAccess>]
module WorkflowLibrary =
    open Forma.Workflow

    let empty = { Entries = []; Current = None; Revision = 0; Unsaved = Set.empty }

    let current (lib: WorkflowLibrary) =
        lib.Current |> Option.bind (fun id -> lib.Entries |> List.tryFind (fun e -> e.Id = id))

    /// Validates a document with Forma and returns the entry to keep. A
    /// document that does not decode is refused with Forma's findings; a
    /// semantically invalid one is kept (with its findings) so nobody's data is lost.
    let entryOf (text: string) : Result<WorkflowEntry, string> =
        let report = Validation.load text
        match report.Workflow with
        | Some w ->
            Ok
                { Id = w.Id.Value
                  Title = w.Title
                  Text = Codec.serialize w
                  Class = Validation.classText report.Class
                  Valid = Validation.isValid report
                  FindingCount = report.Findings.Length }
        | None ->
            let reasons = report.Findings |> List.truncate 3 |> List.map (fun f -> f.Message) |> String.concat " "
            Error $"Not a usable Forma workflow ({Validation.classText report.Class}): {reasons}"

    let private upsert (entry: WorkflowEntry) (lib: WorkflowLibrary) =
        let entries =
            if lib.Entries |> List.exists (fun e -> e.Id = entry.Id) then lib.Entries |> List.map (fun e -> if e.Id = entry.Id then entry else e)
            else lib.Entries @ [ entry ]
        { lib with Entries = entries }

    /// Opens a document (from a file, another system or a template): it becomes
    /// current and the component is asked to load it.
    let openText (text: string) (lib: WorkflowLibrary) =
        entryOf text
        |> Result.map (fun entry ->
            let lib = upsert entry lib
            { lib with Current = Some entry.Id; Revision = lib.Revision + 1; Unsaved = lib.Unsaved.Add entry.Id }, entry)

    /// Adopts a validated change reported by the component. The component
    /// already shows it, so the revision does not change.
    let adoptChange (text: string) (lib: WorkflowLibrary) =
        entryOf text
        |> Result.map (fun entry ->
            let lib = upsert entry lib
            { lib with Current = Some entry.Id; Unsaved = lib.Unsaved.Add entry.Id }, entry)

    let select (id: string) (lib: WorkflowLibrary) =
        if lib.Entries |> List.exists (fun e -> e.Id = id) then Some { lib with Current = Some id; Revision = lib.Revision + 1 } else None

    let close (id: string) (lib: WorkflowLibrary) =
        let entries = lib.Entries |> List.filter (fun e -> e.Id <> id)
        { lib with
            Entries = entries
            Current = (if lib.Current = Some id then entries |> List.tryHead |> Option.map _.Id else lib.Current)
            Revision = lib.Revision + 1
            Unsaved = lib.Unsaved.Remove id }

    /// A new, valid workflow with a fresh id.
    let blank (title: string) (lib: WorkflowLibrary) =
        let used = lib.Entries |> List.map _.Id |> Set.ofList
        let id = Seq.initInfinite (fun i -> $"workflow-{i + 1}") |> Seq.find (used.Contains >> not)
        let doc =
            Json.Object
                [ "$schema", Json.String Format.schemaUri
                  "format", Json.String Format.identifier
                  "formatVersion", Json.String(Format.current.ToString())
                  "id", Json.String id
                  "title", Json.String title
                  "forma", Json.Object [ "version", Json.String(Capabilities.formaVersion.ToString()) ]
                  "nodes", Json.Array [ Json.Object [ "id", Json.String "start"; "kind", Json.String "start"; "label", Json.String "Start" ] ]
                  "provenance", Json.Object [ "producer", Json.Object [ "name", Json.String "Forma Studio" ] ] ]
        openText (Json.serialize doc) lib

    /// The persisted form: the documents themselves, keyed by id.
    let toStorage (lib: WorkflowLibrary) =
        Json.compact (
            Json.Object
                [ "version", Json.Number "1"
                  "current", (lib.Current |> Option.map Json.String |> Option.defaultValue Json.Null)
                  "workflows", Json.Array(lib.Entries |> List.map (fun e -> Json.String e.Text)) ]
        )

    let ofStorage (text: string) (lib: WorkflowLibrary) : Result<WorkflowLibrary, string> =
        match Json.parse text with
        | Error e -> Error e
        | Ok json ->
            match Json.field "workflows" json with
            | Some(Json.Array docs) ->
                let entries = docs |> List.choose (function Json.String t -> entryOf t |> Result.toOption | _ -> None)
                let current =
                    match Json.field "current" json with
                    | Some(Json.String c) when entries |> List.exists (fun e -> e.Id = c) -> Some c
                    | _ -> entries |> List.tryHead |> Option.map _.Id
                Ok { Entries = entries; Current = current; Revision = lib.Revision + 1; Unsaved = Set.empty }
            | _ -> Error "The saved workflows are not in the expected form."

    /// File name under the Forma repository convention.
    let fileName (entry: WorkflowEntry) = entry.Id + Format.suffix

    /// HTML for the current workflow, rendered by Forma.
    let html (document: bool) (opts: DocumentOptions) (entry: WorkflowEntry) =
        match (Validation.load entry.Text).Workflow with
        | Some w -> Some(if document then WorkflowDocument.document opts w else WorkflowDocument.fragment opts w)
        | None -> None
