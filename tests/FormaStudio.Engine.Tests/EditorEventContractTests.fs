/// The editor's machine-operable event contract (forma-studio#18, FST-F2).
///
/// `EditorEvent` (src/engine/FormaStudio.Engine/EditorEvent.fs) is the single
/// source of event names. These tests prove that:
///  - every case round-trips to a unique, well-formed wire name;
///  - the generated browser catalog (src/kernel/editor-events.js) is current;
///  - every name the browser layer dispatches or selects on maps to a case;
///  - every case is emitted by the page or is a documented agent-only event.
///
/// Regenerate the catalog with:
///   FORMA_STUDIO_UPDATE_GENERATED=1 dotnet run --project tests/FormaStudio.Engine.Tests -c Release
module EditorEventContractTests

open System
open System.IO
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

let private path (parts: string list) = Path.Combine(repo :: parts |> Array.ofList)
let private read parts = File.ReadAllText(path parts)
let private names = EditorEvent.catalog |> List.map snd
let private known = Set.ofList names

/// Handled by the engine but not bound on the page: operable by agents and
/// tests through the same Limen message. Each needs a reason.
let agentOnly =
    [ "clear-selection", "Clears the selection without choosing another item; the page clears by selecting."
      "workflow-close", "Closes a portable workflow; the page has no close control yet." ]

let private camel (wire: string) =
    let parts = wire.Split('-')
    parts.[0] + (parts.[1..] |> Array.map (fun p -> string (Char.ToUpperInvariant p.[0]) + p.Substring 1) |> String.concat "")

/// The browser-side catalog, generated from the union.
let generatedCatalog () =
    let entries =
        EditorEvent.catalog
        |> List.map (fun (e, wire) -> sprintf "  %s: \"%s\", // %s" (camel wire) wire (EditorEvent.area e))
        |> String.concat "\n"
    "// GENERATED from the F# EditorEvent union (src/engine/FormaStudio.Engine/EditorEvent.fs).\n"
    + "// Do not edit. Regenerate with:\n"
    + "//   FORMA_STUDIO_UPDATE_GENERATED=1 dotnet run --project tests/FormaStudio.Engine.Tests -c Release\n"
    + "// Every semantic event the Studio engine handles, by wire name (forma-studio#18).\n"
    + "export const editorEvents = Object.freeze({\n"
    + entries
    + "\n});\n"

let private catalogPath = [ "src"; "kernel"; "editor-events.js" ]

let private matches (pattern: string) (text: string) =
    Regex.Matches(text, pattern) |> Seq.map (fun m -> m.Groups.[1].Value) |> List.ofSeq

/// Names the page binds with `data-event`, and the browser tests select on.
let private pageNames () =
    let html = read [ "src"; "kernel"; "index.html" ]
    matches "data-event=\"([^\"]*)\"" html

let private testSelectorNames () =
    Directory.GetFiles(path [ "tests"; "browser" ], "*.mjs")
    |> Seq.collect (fun f -> matches "data-event=\"?([a-z-]+)" (File.ReadAllText f))
    |> List.ofSeq

let private kernelScripts () =
    Directory.GetFiles(path [ "src"; "kernel" ], "*.js")
    |> Array.filter (fun f -> Path.GetFileName f <> "editor-events.js")
    |> Array.sort
    |> List.ofArray

/// Catalog members the kernel scripts reference: `editorEvents.gestureMove`.
let private scriptNames () =
    let byCamel = names |> List.map (fun n -> camel n, n) |> Map.ofList
    kernelScripts ()
    |> List.collect (fun f -> matches @"editorEvents\.([A-Za-z]+)" (File.ReadAllText f) |> List.map (fun c -> Path.GetFileName f, c, byCamel.TryFind c))

let roundTrip =
    test "Event contract: every EditorEvent case round-trips to a unique kebab-case wire name" (fun () ->
        // 86 wire names were characterized before the typed contract; GH-27 added the six icon-picker events.
        equal (86 + 6) (List.length EditorEvent.all) "event count (86 wire names before the typed contract, plus 6 icon events)"
        equal (List.length names) (names |> List.distinct |> List.length) "wire names are unique"
        for e, wire in EditorEvent.catalog do
            expect (Regex.IsMatch(wire, "^[a-z]+(-[a-z]+)*$")) (sprintf "%s is kebab-case" wire)
            equal (Some e) (EditorEvent.tryParse wire) (sprintf "%s parses back to its case" wire)
            equal wire (EditorEvent.wireName e) (sprintf "%A names itself %s" e wire)
        equal None (EditorEvent.tryParse "bogus-name") "an unknown name is not an event"
        equal None (EditorEvent.tryParse "") "the empty name is not an event")

let preservesCharacterizedNames =
    test "Event contract: the union keeps every wire name characterized before the extraction" (fun () ->
        let lost = EditorCharacterizationTests.handledNames |> List.filter (known.Contains >> not)
        expect (List.isEmpty lost) (sprintf "names the page or agents relied on that no case handles any more: %A" lost))

let catalogIsCurrent =
    test "Event contract: src/kernel/editor-events.js is generated from the union and current" (fun () ->
        let expected = generatedCatalog ()
        if Environment.GetEnvironmentVariable "FORMA_STUDIO_UPDATE_GENERATED" = "1" then File.WriteAllText(path catalogPath, expected)
        equal expected (read catalogPath |> fun t -> t.Replace("\r\n", "\n")) "editor-events.js (regenerate with FORMA_STUDIO_UPDATE_GENERATED=1)")

let browserNamesAreEvents =
    test "Event contract: every event name the page binds, the scripts emit and the browser tests select is a case" (fun () ->
        let unknownPage = pageNames () |> List.filter (known.Contains >> not) |> List.distinct
        expect (List.isEmpty unknownPage) (sprintf "index.html binds data-event names no case handles: %A" unknownPage)
        let unknownTests = testSelectorNames () |> List.filter (known.Contains >> not) |> List.distinct
        expect (List.isEmpty unknownTests) (sprintf "browser tests select data-event names no case handles: %A" unknownTests)
        let unknownScript = scriptNames () |> List.filter (fun (_, _, n) -> n.IsNone)
        expect (List.isEmpty unknownScript) (sprintf "kernel scripts reference catalog members that do not exist: %A" unknownScript))

let everyEventIsOperable =
    test "Event contract: every case is bound on the page or documented as agent-only" (fun () ->
        let emitted = Set.ofList (pageNames () @ (scriptNames () |> List.choose (fun (_, _, n) -> n)))
        let agent = agentOnly |> List.map fst |> Set.ofList
        let unbound = names |> List.filter (fun n -> not (emitted.Contains n) && not (agent.Contains n))
        expect (List.isEmpty unbound) (sprintf "events nothing emits (bind them, or document them as agent-only): %A" unbound)
        let stale = agent |> Set.filter (fun n -> emitted.Contains n || not (known.Contains n))
        expect (Set.isEmpty stale) (sprintf "agent-only entries that are bound on the page or no longer exist: %A" stale)
        for name, reason in agentOnly do
            expect (reason.Trim() <> "") (sprintf "%s needs a reason" name))

let all = [ roundTrip; preservesCharacterizedNames; catalogIsCurrent; browserNamesAreEvents; everyEventIsOperable ]
