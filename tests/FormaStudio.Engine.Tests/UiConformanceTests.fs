/// Universal UI layout and interaction conformance in the engine (Visual
/// Engineering UI-LAYOUT-INTERACTION@1.0.0; forma-studio#35): declared alignment
/// groups survive save, open, edit and HTML export; pre-export checks name the
/// rule they serve; and new components carry no placeholder action.
module UiConformanceTests

open System.Text.RegularExpressions
open FormaStudio.Engine
open Harness

let private pageId: PageId = idOf "form"
let private root: ComponentNodeId = idOf "form-stack"
let private row: ComponentNodeId = idOf "contact-row"
let private at parent = Some { Parent = parent; Slot = "children" }

let private add parent index (id: string) kind = Layout(AddComponent(pageId, at parent, index, idOf id, kind))
let private content (id: string) key text = Layout(SetComponentContent(pageId, idOf id, key, text))
let private property (id: string) key value = Layout(SetComponentProperty(pageId, idOf id, key, value))

/// A page with a declared top-aligned form row of two fields.
let private formCommands =
    [ Layout(AddPage(pageId, "Contact form", Some "/contact"))
      Layout(AddComponent(pageId, None, 0, root, "stack"))
      add root 0 "contact-row" "responsive-grid"
      content "contact-row" "label" "Contact details"
      property "contact-row" "alignment" (Some(JString "top"))
      add row 0 "first-name" "text-field"
      content "first-name" "label" "First name"
      add row 1 "email" "text-field"
      content "email" "label" "Email" ]

let private session commands = runAll commands (Editor.start (Samples.emptyProject "conformance" "Conformance"))
let private formSession () = session formCommands
let private pageOf (p: Project) = (ProjectOps.tryPage pageId p).Value
let private codes (findings: Finding list) = findings |> List.map _.Code

let private export (p: Project) = HtmlExport.page HtmlExport.defaults WorkflowLibrary.empty (pageOf p)

let alignmentRoundTrips =
    test "An alignment declaration serializes, loads unchanged, and is edited and cleared through one command each" (fun () ->
        let s = formSession ()
        let text = Codec.serialize s.Project
        expect (text.Contains "\"alignment\"") "the declaration is stored as a component property"
        let loaded = okOr (Codec.load text) "load"
        equal (Codec.serialize s.Project) (Codec.serialize loaded) "round trip is byte-identical"
        let grid = (ProjectOps.tryComponent pageId row loaded).Value
        equal (Some "top") (Components.alignmentOf grid) "loaded declaration"
        let edited = runAll [ property "contact-row" "alignment" (Some(JString "bottom")) ] (Editor.start loaded)
        equal (Some "bottom") (Components.alignmentOf (ProjectOps.tryComponent pageId row edited.Project).Value) "edited edge"
        let cleared = runAll [ property "contact-row" "alignment" None ] (Editor.start edited.Project)
        equal None (Components.alignmentOf (ProjectOps.tryComponent pageId row cleared.Project).Value) "cleared"
        let undone = Editor.undo cleared
        equal (Some "bottom") (Components.alignmentOf (ProjectOps.tryComponent pageId row undone.Project).Value) "undo restores it")

let alignmentRejectsUnknownEdges =
    test "Only declared edges are accepted, and only on containers whose contract has an alignment" (fun () ->
        let s = formSession ()
        let bad = errorOf (Editor.dispatch (property "contact-row" "alignment" (Some(JString "diagonal"))) s) "unknown edge"
        expect (bad |> List.exists (fun f -> f.Message.Contains "top, bottom, center")) (sprintf "names the edges: %A" bad)
        errorOf (Editor.dispatch (property "first-name" "alignment" (Some(JString "top"))) s) "alignment on a field" |> ignore)

let exportCarriesDeclaration =
    test "HTML export carries the declared group on the row and a peer mark on each field's control" (fun () ->
        let html = (export (formSession ()).Project).Html
        expect (html.Contains "data-uli-align-group=\"contact-row\"") "group declared on the row"
        expect (html.Contains "data-uli-align-edge=\"top\"") "edge declared"
        let peers = Regex.Matches(html, "<input [^>]*data-uli-align-peer=\"contact-row\"").Count
        equal 2 peers "each field's input, not its label, is a peer"
        expect (not (Regex.IsMatch(html, "<label [^>]*data-uli-align-peer"))) "labels are not peers"
        expect (not (Regex.IsMatch(html, "<script|\\son[a-z]+=|style="))) "still static, no inline style"
        let undeclared = (export (runAll [ property "contact-row" "alignment" None ] (formSession ())).Project).Html
        expect (not (undeclared.Contains "data-uli-")) "no declaration, no attributes")

let exportIsDeterministic =
    test "Exporting a declared row twice gives identical HTML" (fun () ->
        let p = (formSession ()).Project
        equal (export p).Html (export p).Html "deterministic")

let actionsRowDeclaresButtons =
    test "A declared actions row marks each button as a peer (ULI-L10)" (fun () ->
        let actions: ComponentNodeId = idOf "form-actions"
        let s =
            runAll
                [ add root 1 "form-actions" "actions"
                  property "form-actions" "alignment" (Some(JString "center"))
                  add actions 0 "save" "button"
                  content "save" "label" "Save"
                  add actions 1 "help" "link-button"
                  content "help" "label" "Help"
                  content "help" "href" "/help" ]
                (formSession ())
        let html = (export s.Project).Html
        expect (Regex.IsMatch(html, "<button [^>]*data-uli-align-peer=\"form-actions\"")) "button is a peer"
        expect (Regex.IsMatch(html, "<a [^>]*data-uli-align-peer=\"form-actions\"")) "link button is a peer")

let cleanFormHasNoAlignmentFindings =
    test "A well-formed declared row has no layout findings" (fun () ->
        equal [] (LayoutChecks.page (pageOf (formSession ()).Project)) "no findings")

let checksFindAvoidableViolations =
    test "Pre-export checks name the rule: staggered peers, mixed rows, long labels, placeholder links, missing names, overflow" (fun () ->
        let s =
            runAll
                [ content "email" "description" "We reply within a day."
                  add row 2 "send" "button"
                  content "send" "label" "Send"
                  content "first-name" "label" (String.replicate 6 "Given name ")
                  add root 1 "docs" "link-button"
                  content "docs" "label" "Read the docs"
                  content "docs" "href" "#"
                  add root 2 "nameless" "text-field"
                  add root 3 "url" "text"
                  content "url" "text" "https://example.com/an-extremely-long-unbroken-address-that-overflows" ]
                (formSession ())
        let findings = LayoutChecks.page (pageOf s.Project)
        let found = codes findings |> Set.ofList
        for code in [ "layout.alignment.structure"; "layout.alignment.mixed"; "layout.alignment.label-length"; "layout.link.placeholder"
                      "layout.name.missing"; "layout.text.unbreakable"; "layout.button.unowned" ] do
            expect (found.Contains code) (sprintf "%s expected in %A" code (codes findings))
        equal "ULI-L01" (LayoutChecks.ruleOf "layout.alignment.structure") "alignment rule"
        equal "ULI-A01" (LayoutChecks.ruleOf "layout.link.placeholder") "action rule"
        equal "ULI-A06" (LayoutChecks.ruleOf "layout.name.missing") "name rule"
        equal "ULI-L05" (LayoutChecks.ruleOf "layout.text.unbreakable") "overflow rule"
        expect (findings |> List.forall (fun f -> f.Category = LayoutRule && not (Finding.isBlocker f))) "checks never block"
        expect (findings |> List.forall (fun f -> f.Target.StartsWith "page:form/component:")) "each finding names its component"
        expect ((LayoutChecks.describe (findings |> List.find (fun f -> f.Code = "layout.alignment.structure"))).StartsWith "ULI-L01: ") "shown with its rule")

let mixedInputTypesAreAdvised =
    test "A declared row that mixes input types is advised under ULI-L02 (Forma's intrinsic control heights differ)" (fun () ->
        let s = runAll [ property "email" "inputType" (Some(JString "date")) ] (formSession ())
        let findings = LayoutChecks.page (pageOf s.Project)
        equal [ "layout.alignment.control-types" ] (codes findings) "one advisory"
        equal "ULI-L02" (LayoutChecks.ruleOf "layout.alignment.control-types") "rule"
        equal Advisory findings.Head.Severity "advisory, never blocking")

let lonelyGroupIsReported =
    test "A declared group with fewer than two peers is reported, not silently passed" (fun () ->
        let s = runAll [ Layout(RemoveComponent(pageId, idOf "email")) ] (formSession ())
        equal [ "layout.alignment.peers" ] (codes (LayoutChecks.page (pageOf s.Project))) "one peer")

let newLinkButtonHasNoPlaceholder =
    test "A new link button gets no placeholder href, so it exports as text and is flagged until it has a destination (ULI-A01)" (fun () ->
        let contract = (Components.tryFind "link-button").Value
        let p = (formSession ()).Project
        let command = EditorIntents.addComponent (pageOf p) (ProjectOps.tryComponent pageId root p).Value None None contract
        let s = runAll [ command ] (Editor.start p)
        let html = (export s.Project).Html
        expect (not (html.Contains "href=\"#\"")) "no placeholder link exported"
        expect (codes (LayoutChecks.page (pageOf s.Project)) |> List.contains "layout.link.placeholder") "flagged before export")

let nestedReorder =
    test "Components inside a container reorder among their siblings (no root-only editing)" (fun () ->
        let s = formSession ()
        let page = pageOf s.Project
        let command = match EditorIntents.reorder page "email" true with Some c -> c | None -> fail "reorder found no container"
        let moved = runAll [ command ] s
        let kids = (ProjectOps.tryComponent pageId row moved.Project).Value.Slots.["children"] |> List.map (fun c -> Id.value c.Id)
        equal [ "email"; "first-name" ] kids "email moved earlier inside the row")

let all =
    [ alignmentRoundTrips; alignmentRejectsUnknownEdges; exportCarriesDeclaration; exportIsDeterministic; actionsRowDeclaresButtons
      cleanFormHasNoAlignmentFindings; checksFindAvoidableViolations; mixedInputTypesAreAdvised; lonelyGroupIsReported; newLinkButtonHasNoPlaceholder; nestedReorder ]
