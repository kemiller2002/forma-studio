/// Enforcement for the editor decomposition (forma-studio#18).
///
///  1. No event is named by a string literal outside the EditorEvent mapping:
///     kernel scripts use the generated editor-events.js, engine and WebAssembly
///     sources use EditorEvent cases.
///  2. Dependency direction between the editor modules.
///  3. A source-size ratchet over the editor and command modules: the checked-in
///     baseline (tests/FormaStudio.Engine.Tests/source-size-baseline.json) may
///     only decrease, unless an explicit, owned, expiring exception allows more.
///
/// Lower the baseline after shrinking a file with:
///   FORMA_STUDIO_UPDATE_GENERATED=1 dotnet run --project tests/FormaStudio.Engine.Tests -c Release
/// (the update never raises a limit).
module EditorConformanceTests

open System
open System.Diagnostics
open System.IO
open System.Text.Json
open System.Text.Json.Nodes
open System.Text.RegularExpressions
open FormaStudio.Engine
open Harness

let private repo =
    let rec up (d: DirectoryInfo | null) =
        match d with
        | null -> failwith "repository root not found"
        | d when File.Exists(Path.Combine(d.FullName, "REQUIREMENTS.md")) -> d.FullName
        | d -> up d.Parent
    up (DirectoryInfo(AppContext.BaseDirectory))

let private fileName (f: string) = match Path.GetFileName f with null -> f | n -> n
let private env (name: string) = match Environment.GetEnvironmentVariable name with null -> "" | v -> v

let private path (parts: string list) = Path.Combine(repo :: parts |> Array.ofList)
let private engineDir = path [ "src"; "engine"; "FormaStudio.Engine" ]
let private engineFile name = File.ReadAllText(Path.Combine(engineDir, name))
let private known = EditorEvent.catalog |> List.map snd |> Set.ofList

/// Code lines: whole-line comments removed.
let private codeLines (text: string) =
    text.Split('\n') |> Array.filter (fun l -> not (l.TrimStart().StartsWith "//")) |> List.ofArray

/// Lower-case string literals in source text, skipping whole-line comments.
let private literals (text: string) =
    codeLines text |> List.collect (fun l -> Regex.Matches(l, "\"([a-z][a-z-]*)\"") |> Seq.map (fun m -> m.Groups.[1].Value) |> List.ofSeq)

let private filesIn dir pattern = Directory.GetFiles(dir, pattern) |> Array.sort |> List.ofArray

let private eventLiterals files =
    files |> List.collect (fun f -> literals (File.ReadAllText f) |> List.filter known.Contains |> List.map (fun n -> fileName f, n))

// -- 1. no event literals ------------------------------------------------------

let noStringLiteralEvents =
    test "Conformance: no kernel script, engine or WebAssembly source names an event by string literal" (fun () ->
        let scripts = filesIn (path [ "src"; "kernel" ]) "*.js" |> List.filter (fun f -> fileName f <> "editor-events.js")
        let inScripts = eventLiterals scripts
        expect (List.isEmpty inScripts) (sprintf "use editorEvents.<name> from ./editor-events.js instead of literals: %A" inScripts)
        let inEngine = eventLiterals (filesIn engineDir "*.fs")
        expect (List.isEmpty inEngine) (sprintf "engine sources must use EditorEvent cases, not wire-name literals: %A" inEngine)
        let inWasm = eventLiterals (filesIn (path [ "src"; "wasm"; "FormaStudio.Wasm" ]) "*.cs")
        expect (List.isEmpty inWasm) (sprintf "the WebAssembly glue must not interpret events: %A" inWasm))

let noStringMatchOnEventNames =
    test "Conformance: no engine source matches on a raw event name instead of the parsed union" (fun () ->
        let offenders =
            filesIn engineDir "*.fs"
            |> List.collect (fun f ->
                codeLines (File.ReadAllText f)
                |> List.filter (fun l -> Regex.IsMatch(l, @"\.Name\s*=\s*""|match\s+\w+\.Name\s+with|parseEvent"))
                |> List.map (fun l -> fileName f, l.Trim()))
        expect (List.isEmpty offenders) (sprintf "dispatch on EditorEvent cases (EditorEvent.tryParse), not on name strings: %A" offenders))

// -- 2. dependency direction ---------------------------------------------------

let private areaModules =
    [ "CanvasInteraction"; "TemplateInteraction"; "HistoryInteraction"; "InspectorInteraction"; "DefinitionInteraction"
      "LayoutInteraction"; "WorkflowInteraction"; "ExportInteraction"; "ReviewInteraction" ]

let private references (token: string) (file: string) =
    codeLines (engineFile file) |> List.exists (fun l -> Regex.IsMatch(l, @"(?<![\w.])" + Regex.Escape token + @"\b"))

let dependencyDirection =
    test "Conformance: editor modules depend in one direction (intents <- state <- interaction <- app)" (fun () ->
        let areaFiles = filesIn engineDir "Interaction.*.fs" |> List.map fileName
        // Area update functions are composed only by the EditorApp dispatcher.
        for file in areaFiles do
            for m in areaModules do
                expect (not (references (m + ".") file)) (sprintf "%s must not call %s; EditorApp composes the areas" file m)
        // Effects are requested only where the browser capability is the point.
        let effectful = set [ "EditorApp.fs"; "Interaction.Review.fs"; "Interaction.Workflows.fs"; "Interaction.Export.fs"; "HostEffects.fs" ]
        for file in filesIn engineDir "*.fs" |> List.map fileName do
            if not (effectful.Contains file) then
                expect (not (references "HostEffects." file)) (sprintf "%s must not request host effects" file)
        // The view is a pure projection: no command execution, no effects.
        for token in [ "Editor.dispatch"; "Interaction.run"; "HostEffects."; "Commands.execute" ] do
            expect (not (references token "EditorView.fs")) (sprintf "EditorView.fs must not use %s" token)
        // Domain policy does not see editor view state or the browser vocabulary.
        for token in [ "EditorState"; "EditorEvent"; "Interaction"; "HostEffects" ] do
            expect (not (references token "EditorIntents.fs")) (sprintf "EditorIntents.fs must not depend on %s" token)
        // Command families stay behind the one execution authority.
        for family in [ "LayoutCommands"; "FlowCommands"; "MetadataCommands"; "AppearanceCommands"; "CommandSupport" ] do
            for file in filesIn engineDir "*.fs" |> List.map fileName |> List.filter (fun f -> not (f.StartsWith "Commands")) do
                expect (not (references family file)) (sprintf "%s must use Commands.execute, not %s" file family))

let compositeCommandsInIntents =
    test "Conformance: composite (Batch) commands are built in the intent and command layers, not in UI orchestration" (fun () ->
        let builders = set [ "EditorIntents.fs"; "Fragment.fs"; "Samples.fs"; "Templates.fs"; "Designs.fs" ]
        let offenders =
            filesIn engineDir "*.fs"
            |> List.map fileName
            |> List.filter (fun f -> not (builders.Contains f) && not (f.StartsWith "Commands"))
            |> List.filter (fun f -> codeLines (engineFile f) |> List.exists (fun l -> l.Contains "Batch(\""))
        expect (List.isEmpty offenders) (sprintf "move Batch construction into EditorIntents: %A" offenders))

// -- 3. source-size ratchet ----------------------------------------------------

let baselinePath = path [ "tests"; "FormaStudio.Engine.Tests"; "source-size-baseline.json" ]
let private baselineRelative = "tests/FormaStudio.Engine.Tests/source-size-baseline.json"

type SizeException = { File: string; MaxLines: int; Owner: string; Reason: string; Revisit: string; Expires: DateOnly option }

type SizeBaseline = { Ceiling: int; Limits: Map<string, int>; Exceptions: SizeException list }

let private parseBaseline (text: string) =
    let root = JsonNode.Parse(text)
    let str (n: JsonNode | null) name = match n with null -> "" | n -> match n.[name: string] with null -> "" | v -> v.GetValue<string>()
    let int (n: JsonNode | null) name = match n with null -> 0 | n -> match n.[name: string] with null -> 0 | v -> v.GetValue<int>()
    let limits =
        match root with
        | null -> Map.empty
        | r ->
            match r.["limits"] with
            | :? JsonObject as o -> o |> Seq.map (fun kv -> kv.Key, (match kv.Value with null -> 0 | v -> v.GetValue<int>())) |> Map.ofSeq
            | _ -> Map.empty
    let exceptions =
        match root with
        | null -> []
        | r ->
            match r.["exceptions"] with
            | :? JsonArray as a ->
                a
                |> Seq.map (fun e ->
                    { File = str e "file"; MaxLines = int e "maxLines"; Owner = str e "owner"; Reason = str e "reason"; Revisit = str e "revisit"
                      Expires = match DateOnly.TryParseExact(str e "expires", "yyyy-MM-dd") with | true, d -> Some d | _ -> None })
                |> List.ofSeq
            | _ -> []
    { Ceiling = int root "ceiling"; Limits = limits; Exceptions = exceptions }

let private governed (name: string) =
    name.StartsWith "Editor" || name.StartsWith "Interaction" || name.StartsWith "Commands" || name = "HostEffects.fs"

let private lineCount (file: string) =
    let text = File.ReadAllText(Path.Combine(engineDir, file))
    text.Split('\n').Length - (if text.EndsWith "\n" then 1 else 0)

let actualSizes () =
    filesIn engineDir "*.fs" |> List.map fileName |> List.filter governed |> List.map (fun f -> f, lineCount f) |> Map.ofList

/// The baseline at the comparison ref (FORMA_STUDIO_RATCHET_BASE, else
/// origin/main), when git and that ref are available.
let private previousBaseline () =
    let refs = [ env "FORMA_STUDIO_RATCHET_BASE"; "origin/main" ] |> List.filter (String.IsNullOrWhiteSpace >> not)
    refs
    |> List.tryPick (fun r ->
        try
            let info = ProcessStartInfo("git", sprintf "-C \"%s\" show %s:%s" repo r baselineRelative, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)
            match Process.Start info with
            | null -> None
            | p ->
                use p = p
                let output = p.StandardOutput.ReadToEnd()
                p.WaitForExit()
                if p.ExitCode = 0 then Some(r, parseBaseline output) else None
        with _ -> None)

/// Violations of the ratchet; empty when it holds.
let ratchetViolations (today: DateOnly) (baseline: SizeBaseline) (previous: SizeBaseline option) (actual: Map<string, int>) =
    let active file lines =
        baseline.Exceptions |> List.exists (fun e -> e.File = file && e.MaxLines >= lines && e.Expires |> Option.exists (fun d -> d >= today))
    let raised file limit =
        previous |> Option.bind (fun p -> p.Limits.TryFind file) |> Option.exists (fun before -> limit > before)
    [ for KeyValue(file, lines) in actual do
          match baseline.Limits.TryFind file with
          | None -> yield sprintf "%s (%d lines) is governed but has no limit; add it to the baseline" file lines
          | Some limit when lines > limit && not (active file lines) ->
              yield sprintf "%s grew to %d lines (limit %d); shrink it, split it, or record an owned, expiring exception" file lines limit
          | Some limit when lines < limit -> yield sprintf "%s shrank to %d lines; ratchet its limit down from %d (FORMA_STUDIO_UPDATE_GENERATED=1)" file lines limit
          | Some limit when limit > baseline.Ceiling && not (active file limit) ->
              yield sprintf "%s has limit %d above the %d-line ceiling without an active exception" file limit baseline.Ceiling
          | Some _ -> ()
      for KeyValue(file, _) in baseline.Limits do
          if not (actual.ContainsKey file) then yield sprintf "%s has a limit but no longer exists; remove it" file
      for e in baseline.Exceptions do
          if String.IsNullOrWhiteSpace e.Owner || String.IsNullOrWhiteSpace e.Reason || String.IsNullOrWhiteSpace e.Revisit then
              yield sprintf "exception for %s needs an owner, a reason and a revisit trigger" e.File
          match e.Expires with
          | None -> yield sprintf "exception for %s needs an expiry date (yyyy-MM-dd)" e.File
          | Some d when d < today -> yield sprintf "exception for %s expired on %O; resolve it or renew it explicitly" e.File d
          | Some _ -> ()
          match actual.TryFind e.File, baseline.Limits.TryFind e.File with
          | Some lines, Some limit when lines <= limit && limit <= baseline.Ceiling && not (raised e.File limit) ->
              yield sprintf "exception for %s is no longer needed; remove it" e.File
          | None, _ -> yield sprintf "exception for %s names a file that does not exist" e.File
          | _ -> ()
      match previous with
      | Some prev ->
          if baseline.Ceiling > prev.Ceiling then yield sprintf "the ceiling rose from %d to %d; it may only decrease" prev.Ceiling baseline.Ceiling
          for KeyValue(file, limit) in baseline.Limits do
              match prev.Limits.TryFind file with
              | Some before when limit > before && not (active file limit) ->
                  yield sprintf "%s limit rose from %d to %d without an active exception" file before limit
              | None when limit > baseline.Ceiling && not (active file limit) -> yield sprintf "new file %s starts above the ceiling" file
              | _ -> ()
      | None -> () ]

let private render (limits: Map<string, int>) (original: string) =
    let root = JsonNode.Parse(original) |> Option.ofObj |> Option.defaultWith (fun () -> JsonObject() :> JsonNode)
    let o = JsonObject()
    for KeyValue(file, limit) in limits do
        o.[file] <- JsonValue.Create limit
    root.["limits"] <- o
    root.ToJsonString(JsonSerializerOptions(WriteIndented = true)) + "\n"

let sourceSizeRatchet =
    test "Conformance: editor and command module sizes stay within the checked-in ratchet" (fun () ->
        let text = File.ReadAllText baselinePath
        let baseline = parseBaseline text
        let actual = actualSizes ()
        if env "FORMA_STUDIO_UPDATE_GENERATED" = "1" then
            // Only ever lowers a limit or adds a new file at its size within the ceiling.
            let lowered =
                actual
                |> Map.fold (fun (acc: Map<string, int>) file lines ->
                    match acc.TryFind file with
                    | Some limit when lines < limit -> acc.Add(file, lines)
                    | None when lines <= baseline.Ceiling -> acc.Add(file, lines)
                    | _ -> acc) baseline.Limits
                |> Map.filter (fun file _ -> actual.ContainsKey file)
            File.WriteAllText(baselinePath, render lowered text)
        let baseline = parseBaseline (File.ReadAllText baselinePath)
        let previous = previousBaseline ()
        match previous with
        | Some(r, _) -> printfn "       ratchet: compared limits with %s" r
        | None -> printfn "       ratchet: no earlier baseline reachable (FORMA_STUDIO_RATCHET_BASE / origin/main); limit increases not compared"
        let violations = ratchetViolations (DateOnly.FromDateTime DateTime.UtcNow) baseline (previous |> Option.map snd) actual
        expect (List.isEmpty violations) (String.concat "\n  " ("source-size ratchet:" :: violations)))

let ratchetRules =
    test "Conformance: the ratchet rejects growth, loosening, stale entries and lapsed exceptions" (fun () ->
        let today = DateOnly(2026, 10, 5)
        let ex expires = { File = "A.fs"; MaxLines = 120; Owner = "owner"; Reason = "reason"; Revisit = "trigger"; Expires = expires }
        let b limits exceptions = { Ceiling = 100; Limits = Map.ofList limits; Exceptions = exceptions }
        let check baseline previous actual = ratchetViolations today baseline previous (Map.ofList actual)
        equal [] (check (b [ "A.fs", 50 ] []) None [ "A.fs", 50 ]) "tight limit holds"
        expect (not (List.isEmpty (check (b [ "A.fs", 50 ] []) None [ "A.fs", 51 ]))) "growth fails"
        expect (not (List.isEmpty (check (b [ "A.fs", 50 ] []) None [ "A.fs", 49 ]))) "shrinking demands a lower limit"
        expect (not (List.isEmpty (check (b [ "A.fs", 50 ] []) None [ "A.fs", 50; "B.fs", 10 ]))) "a new governed file needs a limit"
        expect (not (List.isEmpty (check (b [ "A.fs", 50; "Gone.fs", 5 ] []) None [ "A.fs", 50 ]))) "a stale limit fails"
        expect (not (List.isEmpty (check (b [ "A.fs", 60 ] []) (Some(b [ "A.fs", 50 ] [])) [ "A.fs", 60 ]))) "raising a limit fails"
        equal [] (check (b [ "A.fs", 60 ] [ ex (Some(DateOnly(2026, 12, 31))) ]) (Some(b [ "A.fs", 50 ] [])) [ "A.fs", 60 ]) "an owned, unexpired exception allows it"
        expect (not (List.isEmpty (check (b [ "A.fs", 110 ] [ ex (Some(DateOnly(2026, 10, 4))) ]) None [ "A.fs", 110 ]))) "an expired exception fails"
        expect (not (List.isEmpty (check (b [ "A.fs", 110 ] [ { ex (Some(DateOnly(2026, 12, 31))) with Owner = "" } ]) None [ "A.fs", 110 ]))) "an unowned exception fails"
        expect (not (List.isEmpty (check (b [ "A.fs", 110 ] []) None [ "A.fs", 110 ]))) "above the ceiling needs an exception"
        expect (not (List.isEmpty (check (b [ "A.fs", 50 ] [ ex (Some(DateOnly(2026, 12, 31))) ]) None [ "A.fs", 50 ]))) "an unneeded exception fails"
        expect (not (List.isEmpty (check { b [ "A.fs", 50 ] [] with Ceiling = 200 } (Some(b [ "A.fs", 50 ] [])) [ "A.fs", 50 ]))) "raising the ceiling fails")

let all = [ noStringLiteralEvents; noStringMatchOnEventNames; dependencyDirection; compositeCommandsInIntents; ratchetRules; sourceSizeRatchet ]
