/// A deliberately tiny test harness: named test functions, assertions that raise,
/// and a runner that reports every result. No external test framework is needed
/// (dependency policy: prefer the platform).
module Harness

open FormaStudio.Engine

exception AssertionFailed of string

type Test = { Name: string; Body: unit -> unit }

let test name body = { Name = name; Body = body }

let fail message = raise (AssertionFailed message)

let expect condition message = if not condition then fail message

let equal (expected: 'T) (actual: 'T) label =
    if expected <> actual then fail (sprintf "%s\n  expected: %A\n  actual:   %A" label expected actual)

let okOr (result: Result<'T, 'E>) label =
    match result with
    | Ok value -> value
    | Error e -> fail (sprintf "%s failed: %A" label e)

let errorOf (result: Result<'T, 'E>) label =
    match result with
    | Ok value -> fail (sprintf "%s should have been rejected but produced %A" label value)
    | Error e -> e

let idOf<'K> raw : Id<'K> = Samples.idOf<'K> raw

/// Dispatches a sequence of commands through one editor session.
let runAll (commands: Command list) (session: Session) =
    commands |> List.fold (fun s c -> okOr (Editor.dispatch c s) (sprintf "%A" c)) session

let run (tests: Test list) =
    let results =
        tests
        |> List.map (fun t ->
            try
                t.Body()
                printfn "ok     %s" t.Name
                true
            with
            | AssertionFailed message ->
                printfn "FAILED %s\n  %s" t.Name message
                false
            | error ->
                printfn "ERROR  %s\n  %s" t.Name (error.ToString())
                false)
    let failed = results |> List.filter not |> List.length
    printfn "\n%d passed, %d failed, %d total" (List.length tests - failed) failed (List.length tests)
    if failed = 0 then 0 else 1
