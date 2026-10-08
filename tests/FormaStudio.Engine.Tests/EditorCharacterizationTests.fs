/// Characterization of the editor's Limen contract (forma-studio#18, FST-F3).
///
/// Every event the engine handles is replayed from `EditorApp.start ()` through
/// `EditorApp.handle`, in at least one scenario, and the observable outcome is
/// pinned in a checked-in golden file: status text, history depth, the canonical
/// project hash, the full view hash, the effect requests and the view-state
/// fields that drive the page. A refactor that changes any of these fails here
/// with the scenario name and the first differing line.
///
/// Regenerate deliberately (and review the diff) with:
///   FORMA_STUDIO_UPDATE_GOLDEN=1 dotnet run --project tests/FormaStudio.Engine.Tests -c Release
module EditorCharacterizationTests

open System
open System.IO
open System.Security.Cryptography
open System.Text
open FormaStudio.Engine
open Harness

let private repo =
    let rec up (d: DirectoryInfo | null) =
        match d with
        | null -> failwith "repository root not found"
        | d when File.Exists(Path.Combine(d.FullName, "REQUIREMENTS.md")) -> d.FullName
        | d -> up d.Parent
    up (DirectoryInfo(AppContext.BaseDirectory))

let goldenPath = Path.Combine(repo, "tests", "FormaStudio.Engine.Tests", "golden", "editor-events.golden.txt")

/// One Limen message as the browser would send it.
type Msg =
    | Ev of name: string * key: string option * value: string option
    | Fx of correlation: string * outcome: string * value: string option
    | RawMessage of JsonValue

let ev name = Ev(name, None, None)
let evk name key = Ev(name, Some key, None)
let evv name value = Ev(name, None, Some value)
let evkv name key value = Ev(name, Some key, Some value)

let toJson msg =
    match msg with
    | Ev(name, key, value) ->
        Json.obj
            [ "kind", JString "Event"
              "event", Json.objOpt [ "kind", Some(JString "Event"); "name", Some(JString name); "key", key |> Option.map JString; "value", value |> Option.map JString ] ]
    | Fx(correlation, outcome, value) ->
        Json.obj
            [ "kind", JString "EffectResult"
              "result",
              Json.obj
                  [ "kind", JString "StorageResult"
                    "correlationId", JString correlation
                    "outcome", Json.objOpt [ "kind", Some(JString outcome); "value", value |> Option.map JString ] ] ]
    | RawMessage json -> json

type Scenario = { Name: string; Setup: Msg list; Act: Msg }

let private scenario name setup act = { Name = name; Setup = setup; Act = act }

let private sha (text: string) =
    SHA256.HashData(Encoding.UTF8.GetBytes text) |> Convert.ToHexString |> fun h -> h.Substring(0, 16).ToLowerInvariant()

/// Long strings (serialized projects, exported HTML) are pinned by hash and length.
let rec private compact (json: JsonValue) =
    match json with
    | JString s when s.Length > 120 -> JString(sprintf "<sha:%s len:%d>" (sha s) s.Length)
    | JArray items -> JArray(items |> List.map compact)
    | JObject members -> JObject(members |> List.map (fun (k, v) -> k, compact v))
    | other -> other

let private refText =
    function
    | NodeRef(_, n) -> "node:" + Id.value n
    | EdgeRef(_, e) -> "edge:" + Id.value e
    | GroupRef(_, g) -> "group:" + Id.value g
    | other -> sprintf "%A" other

let private pendingText =
    function
    | NoPending -> "none"
    | Connecting n -> "connecting " + Id.value n
    | Reconnecting(e, SourceEnd) -> "reconnecting " + Id.value e + " source"
    | Reconnecting(e, TargetEnd) -> "reconnecting " + Id.value e + " target"

let private opt f = Option.map f >> Option.defaultValue "-"

/// The observable outcome of one handled message.
let snapshot (state: EditorState) (response: JsonValue) =
    let effects = Json.field "effects" response |> Option.defaultValue (JArray [])
    let view = Json.field "view" response |> Option.defaultValue JNull
    let d = state.Drafts
    let wf = state.Workflows
    [ "status: " + state.Status
      sprintf "history: undo=%d redo=%d" state.Session.Undo.Length state.Session.Redo.Length
      "selection: " + (state.Session.Selection |> List.map refText |> String.concat ",")
      "pending: " + pendingText state.Pending
      sprintf "surface: page=%s workflows=%b layoutTarget=%s" (opt Id.value state.Page) state.WorkflowVisible (opt id state.LayoutTarget)
      sprintf "canvas: zoom=%d snap=%b addKind=%s field=%s template=%s clipboard=%s"
          state.Zoom state.Snap state.AddKind (opt Id.value state.Field) (opt id state.Template) (opt (fun (f: DiagramFragment) -> sprintf "%d/%d" f.Nodes.Length f.Edges.Length) state.Clipboard)
      sprintf "drafts: name=%s type=%s options=%s scope=%A slot=%s color=%s mapValue=%s mapSlot=%s"
          d.FieldName d.FieldType d.FieldOptions d.FieldScope d.SlotName d.SlotColor (opt id d.MappingValue) (opt id d.MappingSlot)
      sprintf "workflows: count=%d current=%s revision=%d unsaved=%s"
          wf.Entries.Length (opt id wf.Current) wf.Revision (wf.Unsaved |> Set.toList |> String.concat ",")
      sprintf "export: target=%A brand=%s interactive=%b file=%s text=%s omitted=%d summary=%s"
          state.Export.Target (opt id state.Export.Brand) state.Export.InteractiveWorkflow state.Export.FileName (sha state.Export.Text) state.Export.Omitted.Length state.Export.Summary
      sprintf "review: baseline=%s saving=%s incoming=%s takeSaved=%s"
          (sha (Codec.serialize state.Baseline)) (opt (Codec.serialize >> sha) state.Saving) (opt (Codec.serialize >> sha) state.Incoming) (state.TakeSaved |> Set.toList |> String.concat ",")
      sprintf "icons: availability=%s loading=%s target=%s query=%s"
          (match state.Icons.Availability with
           | IconsNotLoaded -> "not-loaded"
           | IconsUnavailable why -> "unavailable " + sha why
           | IconsAvailable c -> sprintf "forma %s, %d verified, %d refused" c.FormaVersion c.Entries.Length c.Rejected.Length)
          (opt (fun (l: IconLoad) -> sprintf "%d/%d" l.Received.Count (2 * l.Registry.Entries.Length)) state.Icons.Loading)
          (opt ObjectRef.describe state.Icons.Target) state.Icons.Query
      "project: " + sha (Codec.serialize state.Session.Project)
      "view: " + sha (Json.serialize view)
      "effects: " + Json.serialize (compact effects) ]

/// Replays a scenario from the hosted start state; returns the final snapshot.
let replay (s: Scenario) =
    let setupState = s.Setup |> List.fold (fun st m -> EditorApp.handle (toJson m) st |> fst) (EditorApp.start ())
    let next, response = EditorApp.handle (toJson s.Act) setupState
    snapshot next response

// -- fixtures --------------------------------------------------------------

let private workflowFixture name = File.ReadAllText(Path.Combine(repo, "vendor", "forma", "workflow", "fixtures", name + ".forma-workflow.json"))

let private startText () = Codec.serialize (EditorApp.start ()).Session.Project

/// The saved copy as another tab would leave it (mirrors the browser merge test).
let private savedByOtherTab () =
    startText().Replace("Revise request", "Revise and resubmit").Replace("Issue purchase order", "Raise purchase order")

/// Limen messages that load the committed test-fixture icon collection
/// (tests/fixtures/forma-icons-test-fixture; synthetic, not Forma artwork).
let private initialize = RawMessage(Json.obj [ "kind", JString "Initialize" ])

let private http correlation (status: int) (body: string) =
    RawMessage(
        Json.obj
            [ "kind", JString "EffectResult"
              "result",
              Json.obj
                  [ "kind", JString "HttpResult"
                    "correlationId", JString correlation
                    "outcome", Json.obj [ "kind", JString "Success"; "status", Json.ofInt status; "body", JString body ] ] ]
    )

let private iconFixture (relative: string) = File.ReadAllText(Path.Combine(repo, "tests", "fixtures", "forma-icons-test-fixture", relative))

let private iconsLoaded =
    [ initialize; http "icon-registry" 200 (iconFixture "registry.json") ]
    @ ([ "test-line"; "test-box"; "test-dot" ]
       |> List.collect (fun n -> [ http ("icon-svg:" + n) 200 (iconFixture (n + ".svg")); http ("icon-html:" + n) 200 (iconFixture ("html/" + n + ".html")) ]))

let private sel = evk "select" "node:prepare"
let private selEdge = evk "select" "edge:e-yes"
let private page1 = [ ev "add-page" ]
let private withHeading = page1 @ [ ev "layout-add-heading" ]

/// Every wire name the engine handled before the event contract was made typed
/// (86 names in 81 match arms of the original string `onEvent`). Frozen: the
/// typed EditorEvent union must keep all of them (EditorEventContractTests).
let handledNames =
    [ "select"; "clear-selection"; "choose-kind"; "add-node"; "set-label"; "move-left"; "move-right"; "move-up"; "move-down"
      "gesture-move"; "gesture-resize"; "set-width"; "set-height"; "reconnect-end"; "gesture-reconnect"; "zoom-in"; "zoom-out"
      "zoom-reset"; "toggle-snap"; "copy-selection"; "choose-template"; "insert-template"; "connect-start"; "cancel"; "delete"
      "undo"; "redo"; "choose-field"; "set-field-value"; "set-field-option"; "set-field-unknown"; "clear-field"; "set-fill"
      "choose-fill-palette"; "reset-fill"; "toggle-select"; "align-left"; "align-top"; "distribute-horizontally"; "choose-style"
      "draft-field-name"; "draft-field-type"; "draft-field-options"; "draft-field-scope"; "create-field"; "draft-slot-name"
      "draft-slot-color"; "create-slot"; "delete-slot"; "materialize-slot"; "mapping-value"; "mapping-slot"; "create-mapping-rule"
      "assign-lane"; "add-page"; "open-page"; "open-diagram"; "layout-add-heading"; "layout-set-text"; "layout-target"
      "layout-add-component"; "layout-move-up"; "layout-move-down"; "layout-density"; "save"; "load"; "check-saved"
      "open-workflows"; "workflow-new"; "workflow-opened"; "workflow-changed"; "workflow-select"; "workflow-close"; "workflow-save"
      "workflow-load"; "export-target"; "export-interactive"; "export-brand"; "export-page"; "export-component"; "export-workflow"
      "copy-export"; "merge-take-saved"; "merge-keep-mine"; "merge-cancel"; "merge-apply" ]

let scenarios () : Scenario list =
    let field name fieldType = [ evv "draft-field-name" name; evk "draft-field-type" fieldType; ev "create-field" ]
    [ // selection and connection
      scenario "select node" [] sel
      scenario "select edge" [] selEdge
      scenario "select group" [] (evk "select" "group:lane-finance")
      scenario "select unreadable key" [] (evk "select" "bogus")
      scenario "select completes a connection" [ sel; ev "connect-start" ] (evk "select" "node:approve")
      scenario "select refuses an illegal connection" [ evk "select" "node:placed"; ev "connect-start" ] sel
      scenario "select completes a reconnect" [ selEdge; evk "reconnect-end" "target" ] (evk "select" "node:revise")
      scenario "clear-selection" [ sel; ev "connect-start" ] (ev "clear-selection")
      scenario "toggle-select adds" [ sel ] (evk "toggle-select" "node:approve")
      scenario "toggle-select removes" [ sel; evk "toggle-select" "node:approve" ] (evk "toggle-select" "node:prepare")
      scenario "toggle-select unreadable key" [ sel ] (evk "toggle-select" "bogus")
      scenario "connect-start" [ sel ] (ev "connect-start")
      scenario "connect-start without selection" [] (ev "connect-start")
      scenario "cancel" [ sel; ev "connect-start" ] (ev "cancel")
      // adding, labelling, deleting
      scenario "choose-kind" [] (evk "choose-kind" "decision")
      scenario "add-node" [] (ev "add-node")
      scenario "add-node of chosen kind" [ evk "choose-kind" "decision" ] (ev "add-node")
      scenario "add-node of unknown kind" [ evk "choose-kind" "bogus" ] (ev "add-node")
      scenario "add-node twice" [ ev "add-node" ] (ev "add-node")
      scenario "set-label node" [ sel ] (evv "set-label" "Prepared")
      scenario "set-label edge" [ selEdge ] (evv "set-label" "yes")
      scenario "set-label without selection" [] (evv "set-label" "Prepared")
      scenario "delete node" [ sel ] (ev "delete")
      scenario "delete edge" [ selEdge ] (ev "delete")
      scenario "delete group" [ evk "select" "group:lane-finance" ] (ev "delete")
      scenario "delete without selection" [] (ev "delete")
      // movement, size, reconnection
      scenario "move-left" [ sel ] (ev "move-left")
      scenario "move-right" [ sel ] (ev "move-right")
      scenario "move-up" [ sel ] (ev "move-up")
      scenario "move-down" [ sel ] (ev "move-down")
      scenario "move-left without selection" [] (ev "move-left")
      scenario "gesture-move" [ sel ] (evv "gesture-move" "prepare|37.4|-12.6")
      scenario "gesture-move snapped" [ ev "toggle-snap" ] (evv "gesture-move" "prepare|13|5")
      scenario "gesture-move unreadable" [] (evv "gesture-move" "x")
      scenario "gesture-move unknown node" [] (evv "gesture-move" "ghost|10|10")
      scenario "gesture-resize" [] (evv "gesture-resize" "prepare|250|170")
      scenario "gesture-resize snapped" [ ev "toggle-snap" ] (evv "gesture-resize" "prepare|251|3")
      scenario "gesture-resize unreadable" [] (evv "gesture-resize" "nope")
      scenario "gesture-resize unknown node" [] (evv "gesture-resize" "ghost|10|10")
      scenario "set-width" [ sel ] (evv "set-width" "300")
      scenario "set-width not a number" [ sel ] (evv "set-width" "abc")
      scenario "set-width without selection" [] (evv "set-width" "300")
      scenario "set-height" [ sel ] (evv "set-height" "200")
      scenario "reconnect-end source" [ selEdge ] (evk "reconnect-end" "source")
      scenario "reconnect-end on a node" [ sel ] (evk "reconnect-end" "target")
      scenario "reconnect-end unreadable end" [ selEdge ] (evk "reconnect-end" "middle")
      scenario "gesture-reconnect" [ selEdge ] (evv "gesture-reconnect" "target|revise")
      scenario "gesture-reconnect illegal" [ selEdge ] (evv "gesture-reconnect" "source|placed")
      scenario "gesture-reconnect unreadable" [ selEdge ] (evv "gesture-reconnect" "bad")
      scenario "gesture-reconnect unreadable end" [ selEdge ] (evv "gesture-reconnect" "middle|revise")
      scenario "gesture-reconnect without connector" [] (evv "gesture-reconnect" "target|revise")
      // view preferences
      scenario "zoom-in" [] (ev "zoom-in")
      scenario "zoom-in at maximum" [ ev "zoom-in"; ev "zoom-in"; ev "zoom-in" ] (ev "zoom-in")
      scenario "zoom-out" [] (ev "zoom-out")
      scenario "zoom-out at minimum" [ ev "zoom-out"; ev "zoom-out" ] (ev "zoom-out")
      scenario "zoom-reset" [ ev "zoom-in" ] (ev "zoom-reset")
      scenario "toggle-snap on" [] (ev "toggle-snap")
      scenario "toggle-snap off" [ ev "toggle-snap" ] (ev "toggle-snap")
      // arrange
      scenario "align-left" [ sel; evk "toggle-select" "node:approve" ] (ev "align-left")
      scenario "align-left already arranged" [ evk "select" "node:revise"; evk "toggle-select" "node:approve" ] (ev "align-left")
      scenario "align-left needs two" [ sel ] (ev "align-left")
      scenario "align-top" [ sel; evk "toggle-select" "node:budget-check" ] (ev "align-top")
      scenario "distribute-horizontally" [ evk "select" "node:submitted"; evk "toggle-select" "node:prepare"; evk "toggle-select" "node:placed" ] (ev "distribute-horizontally")
      scenario "distribute-horizontally needs three" [ sel; evk "toggle-select" "node:approve" ] (ev "distribute-horizontally")
      // templates and clipboard
      scenario "copy-selection" [ sel; evk "toggle-select" "node:approve" ] (ev "copy-selection")
      scenario "copy-selection without nodes" [ selEdge ] (ev "copy-selection")
      scenario "choose-template" [] (evk "choose-template" "approval-decision")
      scenario "insert-template" [ evk "choose-template" "approval-decision" ] (ev "insert-template")
      scenario "insert-template unknown" [ evk "choose-template" "bogus" ] (ev "insert-template")
      scenario "insert-template without choice" [] (ev "insert-template")
      scenario "insert-template from clipboard" [ sel; evk "toggle-select" "node:approve"; ev "copy-selection" ] (ev "insert-template")
      // history
      scenario "undo" [ sel; evv "set-label" "Prepared" ] (ev "undo")
      scenario "undo with empty history" [] (ev "undo")
      scenario "redo" [ sel; evv "set-label" "Prepared"; ev "undo" ] (ev "redo")
      scenario "redo with nothing undone" [] (ev "redo")
      // metadata values
      scenario "choose-field" [] (evk "choose-field" "status")
      scenario "choose-field unreadable" [ evk "choose-field" "status" ] (evk "choose-field" "Not An Id!")
      scenario "set-field-value enum by label" [ sel; evk "choose-field" "status" ] (evv "set-field-value" "Done")
      scenario "set-field-value enum invalid" [ sel; evk "choose-field" "status" ] (evv "set-field-value" "nonsense")
      scenario "set-field-value url" [ sel; evk "choose-field" "ticket" ] (evv "set-field-value" " https://tickets.example.com/9 ")
      scenario "set-field-value text" [ sel; evk "choose-field" "owner" ] (evv "set-field-value" "Ops team")
      scenario "set-field-value tags" [ sel; evk "choose-field" "tags" ] (evv "set-field-value" "a, b,,c ")
      scenario "set-field-value number" (field "Cost" "number" @ [ sel ]) (evv "set-field-value" "12.50")
      scenario "set-field-value number invalid" (field "Cost" "number" @ [ sel ]) (evv "set-field-value" "twelve")
      scenario "set-field-value boolean" (field "Urgent" "boolean" @ [ sel ]) (evv "set-field-value" "Yes")
      scenario "set-field-value boolean invalid" (field "Urgent" "boolean" @ [ sel ]) (evv "set-field-value" "maybe")
      scenario "set-field-value date" (field "Due" "date" @ [ sel ]) (evv "set-field-value" "2026-10-05")
      scenario "set-field-value multi-selection" [ sel; evk "toggle-select" "node:approve"; evk "choose-field" "status" ] (evv "set-field-value" "Done")
      scenario "set-field-value without selection" [ evk "choose-field" "status" ] (evv "set-field-value" "Done")
      scenario "set-field-value without field" [ sel ] (evv "set-field-value" "Done")
      scenario "set-field-option" [ sel; evk "choose-field" "status" ] (evk "set-field-option" "done")
      scenario "set-field-unknown" [ sel; evk "choose-field" "status" ] (ev "set-field-unknown")
      scenario "clear-field" [ sel; evk "choose-field" "status" ] (ev "clear-field")
      // appearance
      scenario "set-fill" [ sel ] (evv "set-fill" "#112233")
      scenario "set-fill invalid" [ sel ] (evv "set-fill" "red")
      scenario "set-fill without selection" [] (evv "set-fill" "#112233")
      scenario "choose-fill-palette" [ sel ] (evk "choose-fill-palette" "workflow-blocked")
      scenario "choose-fill-palette without selection" [] (evk "choose-fill-palette" "highlight")
      scenario "reset-fill" [ sel ] (ev "reset-fill")
      scenario "reset-fill without selection" [] (ev "reset-fill")
      scenario "choose-style" [ sel ] (evk "choose-style" "decision-emphasis")
      scenario "choose-style none" [ evk "select" "node:budget-check" ] (evk "choose-style" "")
      scenario "choose-style without selection" [] (evk "choose-style" "decision-emphasis")
      // field definition
      scenario "draft-field-name" [] (evv "draft-field-name" "Risk")
      scenario "draft-field-type" [] (evk "draft-field-type" "number")
      scenario "draft-field-options" [] (evv "draft-field-options" "Low, High")
      scenario "draft-field-scope" [] (evk "draft-field-scope" "export")
      scenario "draft-field-scope unknown" [ evk "draft-field-scope" "editor" ] (evk "draft-field-scope" "bogus")
      scenario "create-field choice" [ evv "draft-field-name" "Risk"; evv "draft-field-options" "Low, High, Low, " ] (ev "create-field")
      scenario "create-field choice without options" [ evv "draft-field-name" "Risk" ] (ev "create-field")
      scenario "create-field without name" [ evv "draft-field-name" "  " ] (ev "create-field")
      scenario "create-field text editor-only" [ evv "draft-field-name" "Notes"; evk "draft-field-type" "text"; evk "draft-field-scope" "editor" ] (ev "create-field")
      scenario "create-field export-only clashing key" [ evv "draft-field-name" " Status "; evk "draft-field-type" "text"; evk "draft-field-scope" "export" ] (ev "create-field")
      scenario "create-field url" [ evv "draft-field-name" "Link!"; evk "draft-field-type" "url" ] (ev "create-field")
      scenario "create-field tags" [ evv "draft-field-name" "Labels"; evk "draft-field-type" "tags" ] (ev "create-field")
      scenario "create-field unknown type is text" [ evv "draft-field-name" "Élan"; evk "draft-field-type" "bogus" ] (ev "create-field")
      // palette
      scenario "draft-slot-name" [] (evv "draft-slot-name" "Accent")
      scenario "draft-slot-color" [] (evv "draft-slot-color" "#123456")
      scenario "create-slot" [ evv "draft-slot-name" "Accent"; evv "draft-slot-color" "#123456" ] (ev "create-slot")
      scenario "create-slot clashing key" [ evv "draft-slot-name" "Highlight"; evv "draft-slot-color" "#123456" ] (ev "create-slot")
      scenario "create-slot without name" [ evv "draft-slot-color" "#123456" ] (ev "create-slot")
      scenario "create-slot invalid color" [ evv "draft-slot-name" "Accent"; evv "draft-slot-color" "blue" ] (ev "create-slot")
      scenario "delete-slot in use" [] (evk "delete-slot" "highlight")
      scenario "delete-slot unused" [ evv "draft-slot-name" "Accent"; evv "draft-slot-color" "#123456"; ev "create-slot" ] (evk "delete-slot" "accent")
      scenario "delete-slot unreadable" [] (evk "delete-slot" "Not An Id!")
      scenario "materialize-slot" [] (evk "materialize-slot" "highlight")
      scenario "materialize-slot unreadable" [] (evk "materialize-slot" "Not An Id!")
      // color rules
      scenario "mapping-value" [] (evk "mapping-value" "done")
      scenario "mapping-slot" [] (evk "mapping-slot" "highlight")
      scenario "create-mapping-rule defines" [ evk "choose-field" "status"; evk "mapping-value" "done"; evk "mapping-slot" "highlight" ] (ev "create-mapping-rule")
      scenario "create-mapping-rule updates"
          [ evk "choose-field" "status"; evk "mapping-value" "done"; evk "mapping-slot" "highlight"; ev "create-mapping-rule"; evk "mapping-value" "blocked" ]
          (ev "create-mapping-rule")
      scenario "create-mapping-rule unknown value" [ evk "choose-field" "status"; evk "mapping-value" "bogus"; evk "mapping-slot" "highlight" ] (ev "create-mapping-rule")
      scenario "create-mapping-rule incomplete" [ evk "choose-field" "status" ] (ev "create-mapping-rule")
      scenario "create-mapping-rule non-choice field" [ evk "choose-field" "owner"; evk "mapping-value" "x"; evk "mapping-slot" "highlight" ] (ev "create-mapping-rule")
      scenario "create-mapping-rule without field" [] (ev "create-mapping-rule")
      // lanes
      scenario "assign-lane" [ sel ] (evk "assign-lane" "lane-finance")
      scenario "assign-lane none" [ sel ] (evk "assign-lane" "")
      scenario "assign-lane without selection" [] (evk "assign-lane" "lane-finance")
      // Layout pages
      scenario "add-page" [] (ev "add-page")
      scenario "add-page twice" page1 (ev "add-page")
      scenario "open-page" (page1 @ [ ev "open-diagram" ]) (evk "open-page" "page-1")
      scenario "open-page unreadable" [] (evk "open-page" "Not An Id!")
      scenario "open-diagram" (page1 @ [ ev "open-workflows" ]) (ev "open-diagram")
      scenario "layout-add-heading" page1 (ev "layout-add-heading")
      scenario "layout-add-heading twice" withHeading (ev "layout-add-heading")
      scenario "layout-add-heading without page" [] (ev "layout-add-heading")
      scenario "layout-set-text heading" withHeading (evkv "layout-set-text" "page-1-heading" "Hello")
      scenario "layout-set-text content key" withHeading (evkv "layout-set-text" "page-1-heading|text" "Hello again")
      scenario "layout-set-text without page" [] (evkv "layout-set-text" "page-1-heading" "Hello")
      scenario "layout-target container" page1 (evk "layout-target" "page-1-surface")
      scenario "layout-target root" [ evk "layout-target" "x" ] (evk "layout-target" "root")
      scenario "layout-target empty" [ evk "layout-target" "x" ] (evk "layout-target" "")
      yield!
          Components.catalog
          |> List.map (fun c -> scenario (sprintf "layout-add-component %s" c.Id) page1 (evk "layout-add-component" c.Id))
      scenario "layout-add-component workflow with a current workflow" (ev "workflow-new" :: page1) (evk "layout-add-component" "workflow")
      scenario "layout-add-component unknown" page1 (evk "layout-add-component" "bogus")
      scenario "layout-add-component without page" [] (evk "layout-add-component" "text")
      scenario "layout-add-component into a container"
          (page1 @ [ evk "layout-add-component" "surface"; evk "layout-target" "page-1-surface" ])
          (evk "layout-add-component" "text")
      scenario "layout-add-component missing target falls back to root"
          (page1 @ [ evk "layout-target" "page-1-nothing" ])
          (evk "layout-add-component" "text")
      scenario "layout-move-up" (withHeading @ [ evk "layout-add-component" "text" ]) (evk "layout-move-up" "page-1-text|text")
      scenario "layout-move-up at top" withHeading (evk "layout-move-up" "page-1-heading|text")
      scenario "layout-move-down" (withHeading @ [ evk "layout-add-component" "text" ]) (evk "layout-move-down" "page-1-heading|text")
      scenario "layout-move-down unknown" withHeading (evk "layout-move-down" "nothing|text")
      scenario "layout-density" page1 (evk "layout-density" "compact")
      scenario "layout-density without page" [] (evk "layout-density" "compact")
      // persistence and review
      scenario "save" [ sel; evv "set-label" "Prepared" ] (ev "save")
      scenario "load" [] (ev "load")
      scenario "check-saved" [] (ev "check-saved")
      scenario "effect save success" [ sel; evv "set-label" "Prepared"; ev "save" ] (Fx("save", "Success", None))
      scenario "effect save failure" [ ev "save" ] (Fx("save", "Failure", None))
      scenario "effect load success" [ sel; evv "set-label" "Prepared"; ev "zoom-in" ] (Fx("load", "Success", Some(startText ())))
      scenario "effect load nothing saved" [] (Fx("load", "Success", None))
      scenario "effect load unreadable" [] (Fx("load", "Success", Some "[]"))
      scenario "effect compare unchanged" [] (Fx("compare", "Success", Some(startText ())))
      scenario "effect compare changed" [] (Fx("compare", "Success", Some(savedByOtherTab ())))
      scenario "effect compare nothing saved" [] (Fx("compare", "Success", None))
      scenario "effect compare unreadable" [] (Fx("compare", "Success", Some "[]"))
      scenario "effect unknown correlation" [] (Fx("mystery", "Success", None))
      scenario "effect copy-export" [] (Fx("copy-export", "Success", None))
      scenario "merge-take-saved" [ Fx("compare", "Success", Some(savedByOtherTab ())) ] (evk "merge-take-saved" "node:order")
      scenario "merge-keep-mine" [ Fx("compare", "Success", Some(savedByOtherTab ())); evk "merge-take-saved" "node:order" ] (evk "merge-keep-mine" "node:order")
      scenario "merge-cancel" [ Fx("compare", "Success", Some(savedByOtherTab ())) ] (ev "merge-cancel")
      scenario "merge-apply" [ Fx("compare", "Success", Some(savedByOtherTab ())) ] (ev "merge-apply")
      scenario "merge-apply with a conflict taken from saved"
          [ evk "select" "node:order"; evv "set-label" "Send purchase order"; Fx("compare", "Success", Some(savedByOtherTab ())); evk "merge-take-saved" "node:order" ]
          (ev "merge-apply")
      scenario "merge-apply keeping mine"
          [ evk "select" "node:order"; evv "set-label" "Send purchase order"; Fx("compare", "Success", Some(savedByOtherTab ())) ]
          (ev "merge-apply")
      scenario "merge-apply without incoming" [] (ev "merge-apply")
      // portable workflows
      scenario "open-workflows" [] (ev "open-workflows")
      scenario "workflow-new" [] (ev "workflow-new")
      scenario "workflow-new twice" [ ev "workflow-new" ] (ev "workflow-new")
      scenario "workflow-opened" [] (evv "workflow-opened" (workflowFixture "branching"))
      scenario "workflow-opened unreadable" [] (evv "workflow-opened" "[]")
      scenario "workflow-changed" [ evv "workflow-opened" (workflowFixture "minimal") ] (evv "workflow-changed" (workflowFixture "branching"))
      scenario "workflow-changed unreadable" [] (evv "workflow-changed" "{}")
      scenario "workflow-select" [ ev "workflow-new"; ev "workflow-new" ] (evk "workflow-select" "workflow-1")
      scenario "workflow-select unknown" [] (evk "workflow-select" "workflow-9")
      scenario "workflow-close" [ ev "workflow-new"; ev "workflow-new" ] (evk "workflow-close" "workflow-2")
      scenario "workflow-save" [ ev "workflow-new" ] (ev "workflow-save")
      scenario "workflow-load" [] (ev "workflow-load")
      scenario "effect workflows-save" [ ev "workflow-new"; ev "workflow-save" ] (Fx("workflows-save", "Success", None))
      scenario "effect workflows-load"
          []
          (Fx("workflows-load", "Success", Some(WorkflowLibrary.toStorage (WorkflowLibrary.blank "Saved" WorkflowLibrary.empty |> Result.map fst |> Result.defaultValue WorkflowLibrary.empty))))
      scenario "effect workflows-load nothing saved" [] (Fx("workflows-load", "Success", None))
      scenario "effect workflows-load unreadable" [] (Fx("workflows-load", "Success", Some "{}"))
      // HTML export
      scenario "export-target document" [] (evk "export-target" "document")
      scenario "export-target other" [ evk "export-target" "document" ] (evk "export-target" "fragment")
      scenario "export-interactive on" [] (ev "export-interactive")
      scenario "export-interactive off" [ ev "export-interactive" ] (ev "export-interactive")
      scenario "export-brand" [] (evk "export-brand" "echelon")
      scenario "export-brand none" [ evk "export-brand" "echelon" ] (evk "export-brand" "none")
      scenario "export-brand empty" [ evk "export-brand" "echelon" ] (evk "export-brand" "")
      scenario "export-page" withHeading (ev "export-page")
      scenario "export-page document with brand" (withHeading @ [ evk "export-target" "document"; evk "export-brand" "echelon" ]) (ev "export-page")
      scenario "export-page falls back to first page" (withHeading @ [ ev "open-diagram" ]) (ev "export-page")
      scenario "export-page without pages" [] (ev "export-page")
      scenario "export-component" withHeading (evk "export-component" "page-1-heading|text")
      scenario "export-component gone" withHeading (evk "export-component" "page-1-nothing|text")
      scenario "export-component without page" [] (evk "export-component" "page-1-heading|text")
      scenario "export-workflow" [ ev "workflow-new" ] (ev "export-workflow")
      scenario "export-workflow interactive" [ ev "workflow-new"; ev "export-interactive" ] (ev "export-workflow")
      scenario "export-workflow without workflow" [] (ev "export-workflow")
      scenario "copy-export" (withHeading @ [ ev "export-page" ]) (ev "copy-export")
      // Pinned quirk: with nothing exported, copy-export falls through to the
      // unrecognized-action branch of the original handler.
      scenario "copy-export with nothing exported" [] (ev "copy-export")
      // Forma icons (GH-27): the pinned collection loads through Limen; the picker sets one canonical command
      scenario "initialize requests the pinned icon registry" [] initialize
      scenario "effect icon registry missing (pinned Forma 0.4.1)" [ initialize ] (http "icon-registry" 404 "Not found")
      scenario "effect icon registry refused" [ initialize ] (http "icon-registry" 200 "{\"schemaVersion\": 1, \"grid\": 16}")
      scenario "effect icon assets complete" (List.take (iconsLoaded.Length - 1) iconsLoaded) (List.last iconsLoaded)
      scenario "effect icon asset with a tampered digest" (List.take (iconsLoaded.Length - 1) iconsLoaded) (http "icon-html:test-dot" 200 "<ef-icon></ef-icon>")
      scenario "icon-pick selected node" (iconsLoaded @ [ sel ]) (ev "icon-pick")
      scenario "icon-pick without selection" [] (ev "icon-pick")
      scenario "icon-pick layout item" withHeading (evk "icon-pick" "page-1-heading|text")
      scenario "icon-pick unknown layout item" withHeading (evk "icon-pick" "page-1-nothing|text")
      scenario "icon-pick icons unavailable" [ initialize; http "icon-registry" 404 "Not found"; sel ] (ev "icon-pick")
      scenario "icon-search" (iconsLoaded @ [ sel; ev "icon-pick" ]) (evv "icon-search" "SQUARE")
      scenario "icon-choose" (iconsLoaded @ [ sel; ev "icon-pick" ]) (evk "icon-choose" "test-dot")
      scenario "icon-choose layout heading" (iconsLoaded @ withHeading @ [ evk "icon-pick" "page-1-heading|text" ]) (evk "icon-choose" "test-line")
      scenario "icon-choose not in the release" (iconsLoaded @ [ sel; ev "icon-pick" ]) (evk "icon-choose" "future-glyph")
      scenario "icon-choose injected key" (iconsLoaded @ [ sel; ev "icon-pick" ]) (evk "icon-choose" "<script>alert(1)</script>")
      scenario "icon-choose icons unavailable" [ initialize; http "icon-registry" 404 "Not found"; sel; ev "icon-pick" ] (evk "icon-choose" "test-dot")
      scenario "icon-choose without picker" iconsLoaded (evk "icon-choose" "test-dot")
      scenario "icon-clear" (iconsLoaded @ [ sel; ev "icon-pick"; evk "icon-choose" "test-dot" ]) (ev "icon-clear")
      scenario "icon-clear without picker" [] (ev "icon-clear")
      scenario "icon-copy" (iconsLoaded @ [ sel; ev "icon-pick"; evk "icon-choose" "test-dot" ]) (ev "icon-copy")
      scenario "icon-copy without icon" (iconsLoaded @ [ sel; ev "icon-pick" ]) (ev "icon-copy")
      scenario "effect icon-clipboard"
          []
          (RawMessage(Json.obj [ "kind", JString "EffectResult"; "result", Json.obj [ "kind", JString "ClipboardResult"; "correlationId", JString "icon-clipboard"; "outcome", Json.obj [ "kind", JString "Success" ] ] ]))
      scenario "icon-close" (iconsLoaded @ [ sel; ev "icon-pick" ]) (ev "icon-close")
      scenario "undo icon-choose" (iconsLoaded @ [ sel; ev "icon-pick"; evk "icon-choose" "test-dot" ]) (ev "undo")
      scenario "export-page with an icon" (iconsLoaded @ withHeading @ [ evk "icon-pick" "page-1-heading|text"; evk "icon-choose" "test-line" ]) (ev "export-page")
      // transport edges
      scenario "unknown event name" [] (ev "bogus-name")
      scenario "empty event name" [] (ev "")
      scenario "event message without event" [] (RawMessage(Json.obj [ "kind", JString "Event" ]))
      scenario "unknown message kind" [ sel ] (RawMessage(Json.obj [ "kind", JString "Ping" ]))
      scenario "effect message without result" [] (RawMessage(Json.obj [ "kind", JString "EffectResult" ])) ]

let private render (pairs: (string * string list) list) =
    let sb = StringBuilder()
    sb.Append("# Forma Studio editor event characterization (forma-studio#18).\n")
      .Append("# Generated by tests/FormaStudio.Engine.Tests/EditorCharacterizationTests.fs; do not edit by hand.\n")
      .Append("# Regenerate with FORMA_STUDIO_UPDATE_GOLDEN=1 and review every changed line.\n")
    |> ignore
    for name, lines in pairs do
        sb.Append("\n## ").Append(name).Append('\n') |> ignore
        for l in lines do
            sb.Append(l.Replace("\n", "\\n")).Append('\n') |> ignore
    sb.ToString()

let private blocks (text: string) =
    text.Replace("\r\n", "\n").Split("\n## ")
    |> Array.skip 1
    |> Array.map (fun b -> let lines = b.TrimEnd('\n').Split('\n') in lines.[0], List.ofArray lines.[1..])
    |> List.ofArray

let goldenMatches =
    test "Editor characterization: every scenario's outcome matches the checked-in golden" (fun () ->
        let actual = scenarios () |> List.map (fun s -> s.Name, replay s)
        let text = render actual
        if Environment.GetEnvironmentVariable "FORMA_STUDIO_UPDATE_GOLDEN" = "1" then
            Directory.CreateDirectory(Path.Combine(repo, "tests", "FormaStudio.Engine.Tests", "golden")) |> ignore
            File.WriteAllText(goldenPath, text)
        expect (File.Exists goldenPath) (sprintf "golden missing: %s (run with FORMA_STUDIO_UPDATE_GOLDEN=1)" goldenPath)
        let expected = blocks (File.ReadAllText goldenPath) |> Map.ofList
        let differences =
            actual
            |> List.choose (fun (name, lines) ->
                match Map.tryFind name expected with
                | None -> Some(sprintf "%s: no golden entry" name)
                | Some golden when golden = (lines |> List.map (fun l -> l.Replace("\n", "\\n"))) -> None
                | Some golden ->
                    let pairs = List.zip (List.truncate (min golden.Length lines.Length) golden) (List.truncate (min golden.Length lines.Length) lines)
                    match pairs |> List.tryFind (fun (g, a) -> g <> a.Replace("\n", "\\n")) with
                    | Some(g, a) -> Some(sprintf "%s\n    golden: %s\n    actual: %s" name g a)
                    | None -> Some(sprintf "%s: line count differs" name))
        let stale = expected |> Map.toList |> List.map fst |> List.filter (fun n -> not (actual |> List.exists (fst >> (=) n)))
        expect (List.isEmpty stale) (sprintf "golden has scenarios that no longer exist: %A" stale)
        expect (List.isEmpty differences) (sprintf "%d scenario(s) changed:\n  %s" differences.Length (differences |> List.truncate 8 |> String.concat "\n  ")))

let everyHandledEventIsCharacterized =
    test "Editor characterization: every event has at least one scenario" (fun () ->
        let exercised = scenarios () |> List.choose (fun s -> match s.Act with Ev(name, _, _) -> Some name | _ -> None) |> Set.ofList
        let missing = EditorEvent.catalog |> List.map snd |> List.filter (exercised.Contains >> not)
        equal 86 (List.length handledNames) "handled wire names"
        equal 86 (handledNames |> List.distinct |> List.length) "distinct wire names"
        expect (List.isEmpty missing) (sprintf "events without a scenario (add one and regenerate the golden): %A" missing))

let everyHandledEventIsRecognized =
    test "Editor characterization: every handled event name is recognized by the engine" (fun () ->
        let start = EditorApp.start ()
        let unrecognized =
            handledNames
            // Pinned quirk (see the golden): copy-export with nothing exported falls through.
            |> List.filter ((<>) "copy-export")
            |> List.filter (fun name -> (EditorApp.handle (toJson (ev name)) start |> fst).Status.StartsWith "Unrecognized action")
        expect (List.isEmpty unrecognized) (sprintf "names the engine does not handle: %A" unrecognized)
        let bogus = EditorApp.handle (toJson (ev "bogus-name")) start |> fst
        equal "Unrecognized action 'bogus-name'." bogus.Status "an unknown name is reported, never silently ignored")

let all = [ everyHandledEventIsCharacterized; everyHandledEventIsRecognized; goldenMatches ]
