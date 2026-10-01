/// Developer command line over the engine. File access lives here, outside the
/// engine's Limen boundary; the engine itself stays pure.
module Program

open System.IO
open FormaStudio.Engine

let private usage () =
    eprintfn "usage:"
    eprintfn "  forma-studio sample <project.json>                       write the purchase-request Workflow sample"
    eprintfn "  forma-studio validate <project.json>                     print findings; exit 3 on blockers"
    eprintfn "  forma-studio export <project.json> <export.json>          write the agent/developer export"
    eprintfn "  forma-studio project <project.json> <diagram-id> <dir> [--diagnostic]"
    eprintfn "                                                            write projection.json and diagram.html for Folio"
    eprintfn "  forma-studio workflow-validate <file.forma-workflow.json>...  Forma validation report (JSON); exit 1 if any is invalid"
    eprintfn "  forma-studio designs <project.json> <workflow-id>         write the HTML export reference designs"
    eprintfn "  forma-studio html <project.json> <page-id> <out.html> [--document] [--brand ID] [--forma-base URL] [--workflows DIR]"
    eprintfn "  forma-studio workflow-html <file.forma-workflow.json> <out.html> [--document] [--brand ID] [--forma-base URL]"
    2

let private flag (name: string) (args: string list) =
    args |> List.pairwise |> List.tryPick (fun (a, b) -> if a = name then Some b else None)

let private exportOptions (args: string list) =
    { HtmlExport.defaults with
        Target = (if List.contains "--document" args then HtmlDocument else HtmlFragment)
        Brand = flag "--brand" args
        FormaBase = flag "--forma-base" args |> Option.defaultValue HtmlExport.defaults.FormaBase }

/// Opens every .forma-workflow.json in a directory as Studio would.
let private library (dir: string option) =
    match dir with
    | None -> WorkflowLibrary.empty
    | Some d ->
        Directory.GetFiles(d, "*.forma-workflow.json")
        |> Array.sort
        |> Array.fold (fun lib file -> match WorkflowLibrary.openText (File.ReadAllText file) lib with Ok(next, _) -> next | Error _ -> lib) WorkflowLibrary.empty

let private load path =
    Codec.load (File.ReadAllText path) |> Result.mapError Codec.describeLoadError

let private write (path: string) (text: string) =
    match Path.GetDirectoryName(Path.GetFullPath path) with
    | null -> ()
    | dir -> Directory.CreateDirectory dir |> ignore
    File.WriteAllText(path, text)
    printfn "wrote %s" path

let private report (findings: Finding list) =
    findings |> List.iter (fun f -> printfn "%-8A %-32s %s  %s" f.Severity f.Code f.Target f.Message)

[<EntryPoint>]
let main argv =
    match List.ofArray argv with
    | [ "sample"; out ] ->
        match Samples.purchaseWorkflow () with
        | Ok project -> write out (Codec.serialize project); 0
        | Error findings -> report findings; 1
    | [ "validate"; path ] ->
        match load path with
        | Error message -> eprintfn "%s" message; 4
        | Ok project ->
            let findings = Validation.run project
            report findings
            if findings |> List.exists Finding.isBlocker then 3 else 0
    | [ "export"; path; out ] ->
        match load path with
        | Error message -> eprintfn "%s" message; 4
        | Ok project -> write out (AgentExport.serialize project); 0
    | "project" :: path :: diagram :: dir :: rest ->
        let mode = if List.contains "--diagnostic" rest then Diagnostic else Strict
        match load path, Id.create<DiagramKind> diagram with
        | Error message, _ -> eprintfn "%s" message; 4
        | _, Error message -> eprintfn "%s" message; 2
        | Ok project, Ok diagramId ->
            match Projection.project mode project diagramId with
            | Error findings -> report findings; 3
            | Ok projection ->
                write (Path.Combine(dir, "projection.json")) (Json.serialize projection.Manifest)
                write (Path.Combine(dir, "diagram.html")) projection.Html
                0
    | "workflow-validate" :: files when not files.IsEmpty ->
        let reports = files |> List.map (fun f -> f, Forma.Workflow.Validation.load (File.ReadAllText f))
        let json =
            Forma.Workflow.Json.Array(
                reports |> List.map (fun (f, r) -> Forma.Workflow.Json.Object [ "file", Forma.Workflow.Json.String f; "report", Forma.Workflow.Validation.reportJson r ])
            )
        printf "%s" (Forma.Workflow.Json.serialize json)
        if reports |> List.forall (snd >> Forma.Workflow.Validation.isValid) then 0 else 1
    | [ "designs"; out; workflowId ] ->
        match Designs.project workflowId with
        | Ok project -> write out (Codec.serialize project); 0
        | Error findings -> report findings; 1
    | "html" :: path :: pageId :: out :: rest ->
        match load path with
        | Error message -> eprintfn "%s" message; 4
        | Ok project ->
            match project.Pages |> List.tryFind (fun p -> Id.value p.Id = pageId) with
            | None -> eprintfn "no page %s" pageId; 2
            | Some page ->
                let result = HtmlExport.page (exportOptions rest) (library (flag "--workflows" rest)) page
                write out result.Html
                for o in result.Omitted do eprintfn "omitted: %s" o
                0
    | "workflow-html" :: file :: out :: rest ->
        match WorkflowLibrary.entryOf (File.ReadAllText file) with
        | Error message -> eprintfn "%s" message; 1
        | Ok entry ->
            match HtmlExport.workflow (exportOptions rest) entry with
            | Ok result -> write out result.Html; 0
            | Error message -> eprintfn "%s" message; 1
    | _ -> usage ()
