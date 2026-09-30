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
    2

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
    | _ -> usage ()
