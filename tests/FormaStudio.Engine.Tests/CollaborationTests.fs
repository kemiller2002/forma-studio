module CollaborationTests

open FormaStudio.Engine
open Harness

let private sample () = okOr (Samples.purchaseWorkflow ()) "sample"
let private wd = Samples.diagramId
let private n (id: string) = NodeRef(wd, idOf id)
let private ep (id: string) : Endpoint = { Node = idOf id; Port = None }
let private apply commands project = (Editor.start project |> runAll commands).Project
let private hex raw = okOr (HexColor.parse raw) raw

// ---------------------------------------------------------------------------
// State and Architecture profiles (Phase 10)
// ---------------------------------------------------------------------------

let stateProfile =
    test "State profile: one initial state, triggers on transitions, unreachable states reported" (fun () ->
        let d: DiagramId = idOf "door"
        let s =
            Editor.start (Samples.emptyProject "p" "p")
            |> runAll
                [ Flow(AddDiagram(d, "Door", { Id = "state"; Version = "1.0.0" }))
                  Flow(AddNode(d, idOf "i", "initial", "Start", 0.0, 0.0, 40.0, 40.0))
                  Flow(AddNode(d, idOf "closed", "state", "Closed", 100.0, 0.0, 120.0, 60.0))
                  Flow(AddNode(d, idOf "open", "state", "Open", 300.0, 0.0, 120.0, 60.0))
                  Flow(AddNode(d, idOf "locked", "state", "Locked", 500.0, 0.0, 120.0, 60.0))
                  Flow(AddNode(d, idOf "gone", "final", "Removed", 700.0, 0.0, 40.0, 40.0))
                  Flow(Connect(d, idOf "t0", "transition", { Node = idOf "i"; Port = None }, { Node = idOf "closed"; Port = None }, None))
                  Flow(Connect(d, idOf "t1", "transition", { Node = idOf "closed"; Port = None }, { Node = idOf "open"; Port = None }, Some "push [unlocked]"))
                  Flow(Connect(d, idOf "t2", "transition", { Node = idOf "open"; Port = None }, { Node = idOf "closed"; Port = None }, None)) ]
        errorOf (Editor.dispatch (Flow(Connect(d, idOf "x", "transition", { Node = idOf "gone"; Port = None }, { Node = idOf "open"; Port = None }, None))) s) "final has no outgoing" |> ignore
        errorOf (Editor.dispatch (Flow(Connect(d, idOf "x", "transition", { Node = idOf "open"; Port = None }, { Node = idOf "i"; Port = None }, None))) s) "nothing enters initial" |> ignore
        let codes = Validation.run s.Project |> List.map (fun f -> f.Code, f.Target)
        expect (codes |> List.exists (fun (c, t) -> c = "state.unreachable" && t.EndsWith "node:locked")) "unreachable state found"
        expect (codes |> List.exists (fun (c, t) -> c = "state.transition.trigger" && t.EndsWith "edge:t2")) "missing trigger advised"
        expect (not (codes |> List.exists (fun (c, _) -> c = "state.initial.missing"))) "one initial state")

let architectureProfile =
    test "Architecture profile: typed relationships, explicit boundaries, no inferred security" (fun () ->
        let d: DiagramId = idOf "arch"
        let trust =
            { Key = idOf "trust-zone"; Name = "Trust zone"; Help = None; Type = EnumField [ { Id = "internal"; Label = "Internal" }; { Id = "partner"; Label = "Partner" } ]
              AppliesTo = set [ GroupTarget ]; Disclosure = { Scopes = set [ Rendered; AgentExport ]; DerivedPresentation = true }; Default = None; Required = false; Derivation = None; Origin = ProjectLocal }
        let s =
            Editor.start (Samples.emptyProject "p" "p")
            |> runAll
                [ Flow(AddDiagram(d, "Orders", { Id = "architecture"; Version = "1.0.0" }))
                  Flow(AddNode(d, idOf "web", "service", "Web", 0.0, 0.0, 120.0, 60.0))
                  Flow(AddNode(d, idOf "db", "storage", "Orders DB", 200.0, 0.0, 120.0, 60.0))
                  Flow(AddNode(d, idOf "billing", "external", "Billing", 400.0, 0.0, 120.0, 60.0))
                  Flow(Connect(d, idOf "w-db", "data", { Node = idOf "web"; Port = None }, { Node = idOf "db"; Port = None }, Some "writes orders"))
                  Flow(Connect(d, idOf "w-b", "message", { Node = idOf "web"; Port = None }, { Node = idOf "billing"; Port = None }, Some "invoice request"))
                  Flow(AddGroup(d, idOf "dc", Group, "Data center", -20.0, -20.0, 360.0, 120.0))
                  Flow(SetGroupMembers(d, idOf "dc", [ idOf "web"; idOf "db" ]))
                  MetadataCmd(DefineField trust)
                  MetadataCmd(SetValue([ GroupRef(d, idOf "dc") ], idOf "trust-zone", Explicit(Enum "internal"))) ]
        errorOf (Editor.dispatch (Flow(Connect(d, idOf "x", "control", { Node = idOf "db"; Port = None }, { Node = idOf "web"; Port = None }, None))) s) "storage cannot control" |> ignore
        let findings = Validation.run s.Project
        expect (findings |> List.exists (fun f -> f.Code = "architecture.message.queue")) "message without queue advised"
        expect (findings |> List.forall (fun f -> not (f.Message.ToLowerInvariant().Contains "secure"))) "no security claim is inferred"
        let export = AgentExport.serialize s.Project
        expect (export.Contains "Boundary trust is authored metadata") "profile states its limits in the export"
        expect (export.Contains "\"trust-zone\"") "boundary meaning is explicit metadata")

// ---------------------------------------------------------------------------
// Semantic diff (FDA-202, FDA-894, FDA-1068)
// ---------------------------------------------------------------------------

let private codes (changes: Change list) = changes |> List.map (fun c -> c.Code, c.Category)

let semanticDiff =
    test "Semantic diff separates movement, semantics, metadata, color, definitions, reconnection, topology and routing" (fun () ->
        let p = sample ()
        let diff commands = Diff.between p (apply commands p) |> codes
        equal [ "node.moved", Geometry ] (diff [ Flow(MoveNodes(wd, [ idOf "approve", 10.0, 0.0 ])) ]) "movement only"
        equal [ "node.kind-changed", Semantic ] (diff [ Flow(SetNodeKind(wd, idOf "approve", "decision")); Flow(Connect(wd, idOf "extra", "flow", ep "approve", ep "placed", Some "fast track")) ] |> List.filter (fun (c, _) -> c = "node.kind-changed")) "semantic type change"
        equal [ "metadata.changed", MetadataChange ] (diff [ MetadataCmd(SetValue([ n "approve" ], idOf "status", Explicit(Enum "done"))) ]) "metadata change"
        equal [ "appearance.override-changed", Presentation ] (diff [ AppearanceCmd(SetOverride([ n "approve" ], { Appearance.empty with Fill = Some(LiteralColor(hex "#abcdef")) })) ]) "manual color change"
        equal [ "style.definition-changed", PresentationDefinition ] (diff [ AppearanceCmd(UpdateStyle(idOf "decision-emphasis", Appearance.empty)) ]) "one style change, not per-object changes"
        equal [ "palette.definition-changed", PresentationDefinition ] (diff [ AppearanceCmd(SetPaletteValue(idOf "highlight", PaletteLiteral(hex "#dddddd"))) ]) "palette change"
        let mapping = (ProjectOps.tryMapping (idOf "status-color") p |> Option.get)
        equal [ "mapping.definition-changed", PresentationDefinition ] (diff [ AppearanceCmd(UpdateMapping { mapping with Rules = [] }) ]) "mapping rule change"
        equal [ "edge.reconnected", Topology ] (diff [ Flow(Reconnect(wd, idOf "e-placed", ep "approve", ep "placed")) ]) "reconnection"
        equal [ "edge.routing-changed", Routing ] (diff [ Flow(SetEdgeRouting(wd, idOf "e-yes", Straight)) ]) "routing only"
        equal [ "edge.added", Topology ] (diff [ Flow(Connect(wd, idOf "extra", "flow", ep "prepare", ep "approve", None)) ]) "topology change"
        equal [ "group.membership-changed", Membership; "group.membership-changed", Membership ] (diff [ Flow(AssignLane(wd, idOf "approve", Some(idOf "lane-procurement"))) ]) "membership change on both lanes")

// ---------------------------------------------------------------------------
// Graph-aware merge (Phase 18)
// ---------------------------------------------------------------------------

let independentEditsMerge =
    test "Merge: independent edits on both sides combine without conflict" (fun () ->
        let b = sample ()
        let ours = apply [ Flow(MoveNodes(wd, [ idOf "approve", 20.0, 0.0 ])) ] b
        let theirs = apply [ Flow(SetNodeLabel(wd, idOf "order", "Issue PO")) ] b
        let result = Merge.three b ours theirs
        equal [] result.Conflicts "no conflicts"
        equal 620 (ProjectOps.tryNode wd (idOf "approve") result.Project |> Option.get).Box.Position.X "our move kept"
        equal "Issue PO" (ProjectOps.tryNode wd (idOf "order") result.Project |> Option.get).Label "their label kept")

let deletedNodeVsNewEdge =
    test "Merge: a node deleted on one side and an edge added to it on the other is a conflict" (fun () ->
        let b = sample ()
        let ours = apply [ Flow(RemoveNode(wd, idOf "revise", RemoveIncidentEdges)) ] b
        let theirs = apply [ Flow(Connect(wd, idOf "late", "flow", ep "approve", ep "revise", Some "send back")) ] b
        let result = Merge.three b ours theirs
        expect (result.Conflicts |> List.exists (fun c -> c.Code = "merge.edge.endpoint.dangling" && c.Target.EndsWith "edge:late")) (sprintf "dangling edge reported: %A" result.Conflicts))

let reconnectVsDeletedTarget =
    test "Merge: reconnecting to a node the other side deleted is a conflict" (fun () ->
        let b = sample ()
        let ours = apply [ Flow(Reconnect(wd, idOf "e-placed", ep "order", ep "revise")) ] b
        let theirs = apply [ Flow(RemoveNode(wd, idOf "revise", RemoveIncidentEdges)) ] b
        let result = Merge.three b ours theirs
        expect (result.Conflicts |> List.exists (fun c -> c.Target.EndsWith "edge:e-placed")) (sprintf "reconnection conflict reported: %A" result.Conflicts))

let concurrentStyleEdits =
    test "Merge: concurrent different edits to one style conflict; identical edits agree" (fun () ->
        let b = sample ()
        let styleA = { Appearance.empty with Fill = Some(LiteralColor(hex "#111111")) }
        let styleB = { Appearance.empty with Fill = Some(LiteralColor(hex "#222222")) }
        let conflicting = Merge.three b (apply [ AppearanceCmd(UpdateStyle(idOf "decision-emphasis", styleA)) ] b) (apply [ AppearanceCmd(UpdateStyle(idOf "decision-emphasis", styleB)) ] b)
        equal [ "merge.both-changed", "style:decision-emphasis" ] (conflicting.Conflicts |> List.map (fun c -> c.Code, c.Target)) "style conflict"
        let same = apply [ AppearanceCmd(UpdateStyle(idOf "decision-emphasis", styleA)) ] b
        equal [] (Merge.three b same same).Conflicts "identical edits agree")

let paletteVsOverride =
    test "Merge: a palette edit and an object override compose; the override still wins" (fun () ->
        let b = sample ()
        let ours = apply [ AppearanceCmd(SetOverride([ n "order" ], { Appearance.empty with Fill = Some(LiteralColor(hex "#fafafa")) })) ] b
        let theirs = apply [ AppearanceCmd(SetPaletteValue(idOf "highlight", PaletteLiteral(hex "#e0e0ff"))) ] b
        let result = Merge.three b ours theirs
        equal [] result.Conflicts "no conflict"
        let fill r = ((AppearanceResolution.resolve EditorScope result.Project r |> fst).Fill.Value |> Option.bind _.Css)
        equal (Some "#fafafa") (fill (n "order")) "override wins"
        equal (Some "#e0e0ff") (fill (n "prepare")) "palette edit reaches other users of the slot")

let profileMigrationVsEdit =
    test "Merge: a profile migration against a profile-specific edit surfaces the illegal combination" (fun () ->
        let b = sample ()
        let migrate (p: Project) =
            { p with
                Diagrams =
                    p.Diagrams
                    |> List.map (fun d ->
                        { d with
                            Profile = { Id = "general"; Version = "1.0.0" }
                            Nodes = d.Nodes |> List.map (fun node -> { node with Kind = "box" }) }) }
        let theirs = migrate b
        let ours = apply [ Flow(AddNode(wd, idOf "second-check", "decision", "Second check?", 900.0, 300.0, 120.0, 120.0)) ] b
        let result = Merge.three b ours theirs
        expect (result.Conflicts |> List.exists (fun c -> c.Code = "merge.profile.node-kind" && c.Target.EndsWith "node:second-check")) (sprintf "migration conflict: %A" result.Conflicts))

let presenceIsNotProjectState =
    test "Collaboration readiness: selection and other session state never enter the project or its revision" (fun () ->
        let p = sample ()
        let s = Editor.start p |> Editor.select [ n "approve"; n "order" ]
        equal (Codec.revision p) (Codec.revision s.Project) "selection does not change the revision"
        expect (not ((Codec.serialize s.Project).Contains "selection")) "no presence or selection in the document")

// ---------------------------------------------------------------------------
// Fragments and template dependency closure (FDA-1250..1257)
// ---------------------------------------------------------------------------

let private wfDiagram (d: DiagramId) = Flow(AddDiagram(d, "Target", { Id = "workflow"; Version = "1.0.0" }))

let fragmentClosure =
    test "Fragments carry their dependency closure and reuse or import by stable id" (fun () ->
        let p = sample ()
        let fragment = okOr (Fragment.extract p wd [ idOf "approve"; idOf "order" ]) "extract"
        equal [ "e-approved" ] (fragment.Edges |> List.map (fun e -> Id.value e.Id)) "only internal connectors"
        let fieldKeys = fragment.Fields |> List.map (fun f -> Id.value f.Key) |> Set.ofList
        expect (Set.isSubset (set [ "status"; "tags"; "ticket"; "cost-center"; "phase" ]) fieldKeys) (sprintf "fields closed over: %A" fieldKeys)
        equal [ "workflow-blocked"; "highlight" ] (fragment.Palette |> List.map (fun s -> Id.value s.Id)) "palette slots from overrides and mappings"
        equal [ "status-color" ] (fragment.Mappings |> List.map (fun m -> Id.value m.Id)) "mapping over an included field"
        // Into a fresh project: everything is imported and appearance is equivalent.
        let target: DiagramId = idOf "t"
        let fresh = apply [ wfDiagram target ] (Samples.emptyProject "fresh" "Fresh")
        let command, decisions = okOr (Fragment.applyCommand fragment fresh target 0.0 0.0 RemapConflicting) "plan"
        expect (decisions |> List.forall (fun d -> d.Action = Imported)) "all imported"
        let session = okOr (Editor.dispatch command (Editor.start fresh)) "apply"
        equal 1 session.Undo.Length "one undo entry for the whole fragment"
        let fill (proj: Project) r = ((AppearanceResolution.resolve (OutputScope Rendered) proj r |> fst).Fill.Value |> Option.bind _.Css)
        equal (fill p (n "approve")) (fill session.Project (NodeRef(target, idOf "approve"))) "mapped color survives"
        equal (fill p (n "order")) (fill session.Project (NodeRef(target, idOf "order"))) "override color survives"
        // Back into the source project: identical definitions are reused; ids are fresh.
        let again, reuse = okOr (Fragment.applyCommand fragment p wd 0.0 300.0 RemapConflicting) "plan into source"
        expect (reuse |> List.forall (fun d -> d.Action = Reused)) "identical definitions reused"
        let pasted = okOr (Editor.dispatch again (Editor.start p)) "paste"
        expect (ProjectOps.tryNode wd (idOf "approve-2") pasted.Project |> Option.isSome) "fresh node id allocated"
        equal [] (Validation.blockers pasted.Project) "no dangling references after paste")

let fragmentConflicts =
    test "Conflicting dependencies are remapped or rejected, never matched by name" (fun () ->
        let p = sample ()
        let fragment = okOr (Fragment.extract p wd [ idOf "approve" ]) "extract"
        let target: DiagramId = idOf "t"
        let differentStatus =
            { (ProjectOps.tryField (idOf "status") p |> Option.get) with Type = EnumField [ { Id = "blocked"; Label = "Blocked" }; { Id = "open"; Label = "Open" } ]; Default = None }
        let other = apply [ wfDiagram target; MetadataCmd(DefineField differentStatus) ] (Samples.emptyProject "other" "Other")
        let rejected = Fragment.applyCommand fragment other target 0.0 0.0 RejectConflicting
        expect (match rejected with Error e -> e.Contains "field 'status'" | Ok _ -> false) "reject policy explains"
        let command, decisions = okOr (Fragment.applyCommand fragment other target 0.0 0.0 RemapConflicting) "remap plan"
        expect (decisions |> List.exists (fun d -> d.Kind = "field" && d.SourceId = "status" && d.Action = Remapped "status-2")) "status remapped"
        let session = okOr (Editor.dispatch command (Editor.start other)) "apply remapped"
        let copied = ProjectOps.tryNode target (idOf "approve") session.Project |> Option.get
        expect (copied.Metadata.ContainsKey(idOf "status-2")) "metadata re-keyed to the imported field"
        expect (not (copied.Metadata.ContainsKey(idOf "status"))) "never written into the unrelated same-named field"
        let mapping = session.Project.Mappings |> List.exactlyOne
        equal "status-2" (Id.value mapping.Field) "mapping follows the remap"
        let general: DiagramId = idOf "g"
        let mismatch = apply [ Flow(AddDiagram(general, "General", { Id = "general"; Version = "1.0.0" })) ] (Samples.emptyProject "x" "x")
        expect (Fragment.applyCommand fragment mismatch general 0.0 0.0 RemapConflicting |> Result.isError) "profile mismatch refused")

// ---------------------------------------------------------------------------
// Large graph (Phase 16)
// ---------------------------------------------------------------------------

let largeGraph =
    test "Large graph: 2,000 nodes and 3,000 edges stay lossless and responsive" (fun () ->
        let d: DiagramId = idOf "big"
        let nodeCount, edgeCount = 2000, 3000
        let nodes =
            [ 0 .. nodeCount - 1 ]
            |> List.map (fun i ->
                { Id = idOf (sprintf "n%d" i); Kind = "box"; Label = sprintf "Item %d" i
                  Box = okOr (Geometry.box (float (i % 50 * 220)) (float (i / 50 * 140)) 180.0 100.0) "box"
                  Ports = []; Locked = false; Metadata = Map.empty; Appearance = Appearance.none; References = [] })
        let edges =
            [ 0 .. edgeCount - 1 ]
            |> List.map (fun i ->
                let s, t = i % nodeCount, (i * 7 + 1) % nodeCount
                { Id = idOf (sprintf "e%d" i); Kind = "flow"; Source = { Node = idOf (sprintf "n%d" s); Port = None }; Target = { Node = idOf (sprintf "n%d" t); Port = None }
                  Label = None; Routing = Orthogonal; Metadata = Map.empty; Appearance = Appearance.none; References = [] })
        let diagram =
            { Id = d; Name = "Big"; Profile = { Id = "general"; Version = "1.0.0" }; Nodes = nodes; Edges = edges; Groups = []
              Display = { NodeFields = []; KindFields = Map.empty; EdgeFields = []; Missing = OmitMissing }; Metadata = Map.empty; References = [] }
        let project = { Samples.emptyProject "big" "Big" with Diagrams = [ diagram ] }
        let timed f =
            let watch = System.Diagnostics.Stopwatch.StartNew()
            let result = f ()
            result, watch.Elapsed.TotalSeconds
        let findings, validationSeconds = timed (fun () -> Validation.run project)
        equal [] (findings |> List.filter Finding.isBlocker) "valid"
        let reloaded, roundTripSeconds = timed (fun () -> okOr (Codec.load (Codec.serialize project) |> Result.mapError Codec.describeLoadError) "round trip")
        equal project reloaded "lossless round trip"
        let moved, commandSeconds = timed (fun () -> okOr (Commands.execute (Flow(MoveNodes(d, [ idOf "n10", 5.0, 5.0 ]))) project) "move")
        equal nodeCount (ProjectOps.tryDiagram d moved.Project |> Option.get).Nodes.Length "no node dropped"
        equal edgeCount (ProjectOps.tryDiagram d moved.Project |> Option.get).Edges.Length "no edge dropped"
        let projection, projectionSeconds = timed (fun () -> okOr (Projection.project Strict project d) "projection")
        equal edgeCount (System.Text.RegularExpressions.Regex.Matches(projection.Html, "<path class=\"ef-diagram-connector\"").Count) "every connector projected"
        printfn "       large graph: validate %.2fs, round trip %.2fs, command %.2fs, projection %.2fs" validationSeconds roundTripSeconds commandSeconds projectionSeconds
        expect (validationSeconds < 20.0 && roundTripSeconds < 20.0 && commandSeconds < 20.0 && projectionSeconds < 60.0) "within generous bounds")

let all =
    [ stateProfile; architectureProfile; semanticDiff; independentEditsMerge; deletedNodeVsNewEdge; reconnectVsDeletedTarget
      concurrentStyleEdits; paletteVsOverride; profileMigrationVsEdit; presenceIsNotProjectState; fragmentClosure; fragmentConflicts; largeGraph ]
