module EditorAppTests

open FormaStudio.Engine
open Harness

let private event name key value =
    Json.obj
        [ "kind", JString "Event"
          "event", Json.objOpt [ "kind", Some(JString "Event"); "name", Some(JString name); "key", key |> Option.map JString; "value", value |> Option.map JString ] ]

let private send message state = EditorApp.handle message state |> fst
let private viewOf state = EditorApp.view state
let private field name state = Json.field name (viewOf state)

let gestureIsOneCommand =
    test "Editor: selection is view state; one gesture event is one command and one history entry" (fun () ->
        let start = EditorApp.start ()
        let selected = send (event "select" (Some "node:prepare") None) start
        equal 0 selected.Session.Undo.Length "selection adds no history"
        equal start.Session.Project selected.Session.Project "selection does not change the project"
        let moved = send (event "gesture-move" None (Some "prepare|37.4|-12.6")) selected
        equal 1 moved.Session.Undo.Length "one drag, one entry"
        let node = ProjectOps.tryNode Samples.diagramId (idOf "prepare") moved.Session.Project |> Option.get
        equal { X = 287; Y = 13 } node.Box.Position "normalized on commit")

let rejectedEventsExplainAndKeepState =
    test "Editor: illegal and malformed input leave the project unchanged with a visible reason" (fun () ->
        let start = EditorApp.start ()
        let fromEnd = start |> send (event "select" (Some "node:placed") None) |> send (event "connect-start" None None) |> send (event "select" (Some "node:prepare") None)
        equal start.Session.Project fromEnd.Session.Project "no connection from an end node"
        expect (fromEnd.Status.Contains "end node") "reason shown"
        let next, response = EditorApp.dispatchText "{not json" start
        equal start.Session.Project next.Session.Project "malformed message ignored"
        expect (response.Contains "Ignored a malformed message") "malformed input reported")

let saveAndLoadUseEffects =
    test "Editor: save and load are Limen storage effects; loading resets undo history" (fun () ->
        let start = EditorApp.start () |> send (event "select" (Some "node:prepare") None) |> send (event "set-label" None (Some "Prepared"))
        let _, response = EditorApp.handle (event "save" None None) start
        match Json.field "effects" response with
        | Some(JArray [ effect ]) ->
            equal (Some(JString "set")) (Json.field "operation" effect) "storage set"
            match Json.field "value" effect with
            | Some(JString saved) ->
                let loadResult = Json.obj [ "kind", JString "EffectResult"; "result", Json.obj [ "kind", JString "StorageResult"; "correlationId", JString "load"; "outcome", Json.obj [ "kind", JString "Success"; "value", JString saved ] ] ]
                let reloaded = send loadResult (EditorApp.start ())
                equal start.Session.Project reloaded.Session.Project "project restored"
                equal 0 reloaded.Session.Undo.Length "history is session-local (FDA-1051)"
            | _ -> fail "missing saved value"
        | other -> fail (sprintf "expected one effect, got %A" other))

let canvasPreviewsRenderedScope =
    test "Editor: the canvas shows only rendered-scope metadata; the inspector marks the rest" (fun () ->
        let selected = EditorApp.start () |> send (event "select" (Some "node:approve") None)
        let text = Json.serialize (viewOf selected)
        let nodes = match field "nodes" selected with Some(JArray items) -> Json.serialize (JArray items) | _ -> ""
        expect (not (nodes.Contains "CC-7731")) "source-only value not on canvas"
        expect (text.Contains "not printed") "inspector marks unprinted fields")

let all = [ gestureIsOneCommand; rejectedEventsExplainAndKeepState; saveAndLoadUseEffects; canvasPreviewsRenderedScope ]
