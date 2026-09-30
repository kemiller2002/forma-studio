module ModelTests

open FormaStudio.Engine
open Harness

let private empty () = Samples.emptyProject "test-project" "Test project"
let private d: DiagramId = idOf "d1"
let private nodeRef (n: string) = NodeRef(d, idOf n)
let private endpoint (n: string) : Endpoint = { Node = idOf n; Port = None }
let private general = { Id = "general"; Version = "1.0.0" }
let private workflow = { Id = "workflow"; Version = "1.0.0" }

let private hex raw = okOr (HexColor.parse raw) raw
let private token raw = okOr (TokenRef.parse raw) raw

let private reload project =
    okOr (Codec.load (Codec.serialize project) |> Result.mapError Codec.describeLoadError) "reload"

let private sample () = okOr (Samples.purchaseWorkflow ()) "purchase workflow sample"

let private statusField disclosure =
    { Key = idOf "status"; Name = "Status"; Help = None
      Type = EnumField [ { Id = "blocked"; Label = "Blocked" }; { Id = "approved"; Label = "Approved" }; { Id = "open"; Label = "Open" } ]
      AppliesTo = set [ NodeTarget; EdgeTarget ]; Disclosure = disclosure; Default = Some(Enum "open"); Required = false; Derivation = None; Origin = ProjectLocal }

let private visible = { Scopes = set [ Rendered; AgentExport ]; DerivedPresentation = true }

let private twoNodes () =
    Editor.start (empty ())
    |> runAll
        [ Flow(AddDiagram(d, "Flow", general))
          Flow(AddNode(d, idOf "a", "box", "Alpha", 0.0, 0.0, 120.0, 60.0))
          Flow(AddNode(d, idOf "b", "box", "Beta", 200.0, 0.0, 120.0, 60.0)) ]

// ---------------------------------------------------------------------------
// FDA-220..222: the two vertical slices through one shared session
// ---------------------------------------------------------------------------

let layoutProof =
    test "Layout proof: stack, heading, edit, reorder, spacing, undo, redo, save, reload" (fun () ->
        let page: PageId = idOf "home"
        let stack: ComponentNodeId = idOf "stack-1"
        let h1: ComponentNodeId = idOf "heading-1"
        let h2: ComponentNodeId = idOf "heading-2"
        let slot = Some { Parent = stack; Slot = "children" }
        let session =
            Editor.start (empty ())
            |> runAll
                [ Layout(AddPage(page, "Home", Some "/"))
                  Layout(AddComponent(page, None, 0, stack, "stack"))
                  Layout(AddComponent(page, slot, 0, h1, "heading"))
                  Layout(SetComponentContent(page, h1, "text", "Welcome"))
                  Layout(SetComponentProperty(page, h1, "level", Some(Json.ofInt 1)))
                  Layout(AddComponent(page, slot, 1, h2, "heading"))
                  Layout(SetComponentContent(page, h2, "text", "Details"))
                  Layout(MoveComponent(page, h2, slot, 0))
                  Layout(SetComponentProperty(page, stack, "density", Some(JString "compact"))) ]
        let children p = (ProjectOps.tryComponent page stack p |> Option.get).Slots |> Map.find "children" |> List.map _.Id
        equal [ h2; h1 ] (children session.Project) "reordered children"
        equal (Some(JString "compact")) ((ProjectOps.tryComponent page stack session.Project |> Option.get).Properties |> Map.tryFind "density") "density"
        let undone = Editor.undo session
        equal None ((ProjectOps.tryComponent page stack undone.Project |> Option.get).Properties |> Map.tryFind "density") "undo spacing"
        let undone2 = Editor.undo undone
        equal [ h1; h2 ] (children undone2.Project) "undo reorder"
        let redone = undone2 |> Editor.redo |> Editor.redo
        equal session.Project redone.Project "redo restores the same canonical state"
        equal redone.Project (reload redone.Project) "save and reload"
        equal (Codec.revision session.Project) (Codec.revision (reload redone.Project)) "revision is content-derived")

let layoutRejectsInventedProperties =
    test "Layout rejects properties, slots and components absent from the Forma contract" (fun () ->
        let page: PageId = idOf "home"
        let s = Editor.start (empty ()) |> runAll [ Layout(AddPage(page, "Home", None)); Layout(AddComponent(page, None, 0, idOf "h", "heading")) ]
        errorOf (Editor.dispatch (Layout(SetComponentProperty(page, idOf "h", "x", Some(Json.ofInt 10)))) s) "arbitrary x" |> ignore
        errorOf (Editor.dispatch (Layout(SetComponentProperty(page, idOf "h", "level", Some(Json.ofInt 9)))) s) "level 9" |> ignore
        errorOf (Editor.dispatch (Layout(AddComponent(page, Some { Parent = idOf "h"; Slot = "children" }, 0, idOf "c", "heading"))) s) "heading has no slot" |> ignore
        errorOf (Editor.dispatch (Layout(AddComponent(page, None, 0, idOf "z", "freeform-div"))) s) "unknown component" |> ignore)

let flowProof =
    test "Flow proof: nodes, connect, move, label, metadata, color, undo, redo, save, reload" (fun () ->
        let session =
            twoNodes ()
            |> runAll
                [ Flow(Connect(d, idOf "ab", "flow", endpoint "a", endpoint "b", None))
                  Flow(MoveNodes(d, [ idOf "b", 40.4, 100.6 ]))
                  Flow(SetEdgeLabel(d, idOf "ab", Some "hands off to"))
                  MetadataCmd(DefineField(statusField visible))
                  MetadataCmd(SetValue([ nodeRef "a" ], idOf "status", Explicit(Enum "blocked")))
                  AppearanceCmd(SetOverride([ nodeRef "a" ], { Appearance.empty with Fill = Some(LiteralColor(hex "#7B3FA0")) })) ]
        let p = session.Project
        let edge = ProjectOps.tryEdge d (idOf "ab") p |> Option.get
        equal (endpoint "a") edge.Source "edge source keeps node identity after move"
        equal (endpoint "b") edge.Target "edge target keeps node identity after move"
        equal { X = 240; Y = 101 } (ProjectOps.tryNode d (idOf "b") p |> Option.get).Box.Position "geometry normalized on commit"
        equal (Some "hands off to") edge.Label "label"
        equal (Some(Explicit(Enum "blocked"))) ((ProjectOps.tryNode d (idOf "a") p |> Option.get).Metadata |> Map.tryFind (idOf "status")) "metadata"
        equal (Some(LiteralColor(hex "#7b3fa0"))) (ProjectOps.tryNode d (idOf "a") p |> Option.get).Appearance.Overrides.Fill "literal normalized"
        let rewound = [ 1..6 ] |> List.fold (fun s _ -> Editor.undo s) session
        equal (twoNodes ()).Project rewound.Project "six undos return to two unconnected nodes"
        let replayed = [ 1..6 ] |> List.fold (fun s _ -> Editor.redo s) rewound
        equal p replayed.Project "six redos restore the final state"
        equal p (reload p) "save and reload is lossless")

let sharedInfrastructure =
    test "Layout and Flow share one dispatcher, one history and one selection model" (fun () ->
        let page: PageId = idOf "home"
        let session =
            twoNodes ()
            |> runAll [ Layout(AddPage(page, "Home", None)); Flow(MoveNodes(d, [ idOf "a", 10.0, 0.0 ])); Layout(AddComponent(page, None, 0, idOf "s", "stack")) ]
            |> Editor.select [ nodeRef "a"; ComponentRef(page, idOf "s") ]
        equal 6 session.Undo.Length "one history across surfaces"
        let once = Editor.undo session
        equal None (ProjectOps.tryComponent page (idOf "s") once.Project) "undo removes the latest Layout edit"
        equal [ nodeRef "a" ] once.Selection "selection pruned to existing objects"
        let twice = Editor.undo once
        equal 0 (ProjectOps.tryNode d (idOf "a") twice.Project |> Option.get).Box.Position.X "second undo reverts the Flow move"
        equal twice.Project (Editor.select [ nodeRef "b" ] twice).Project "selection never changes the project")

let rejectedCommandsDoNotMutate =
    test "Rejected commands leave the session unchanged" (fun () ->
        let s = twoNodes ()
        let findings = errorOf (Editor.dispatch (Flow(Connect(d, idOf "x", "flow", endpoint "a", endpoint "missing", None))) s) "dangling connect"
        expect (findings |> List.forall Finding.isBlocker) "rejection reports blockers"
        errorOf (Editor.dispatch (Flow(AddNode(d, idOf "a", "box", "Dup", 0.0, 0.0, 50.0, 50.0))) s) "duplicate id" |> ignore
        errorOf (Editor.dispatch (Flow(AddNode(d, idOf "c", "box", "Bad", nan, 0.0, 50.0, 50.0))) s) "NaN geometry" |> ignore
        errorOf (Editor.dispatch (Flow(AddNode(d, idOf "c", "box", "Bad", 0.0, 0.0, 0.0, 50.0))) s) "zero width" |> ignore
        errorOf (Editor.dispatch (Flow(AddNode(d, idOf "c", "gizmo", "Bad", 0.0, 0.0, 50.0, 50.0))) s) "unknown kind" |> ignore
        errorOf (Editor.dispatch (Batch("atomic", [ Flow(AddNode(d, idOf "c", "box", "C", 0.0, 0.0, 50.0, 50.0)); Flow(RemoveEdge(d, idOf "nope")) ])) s) "failing batch" |> ignore
        equal None (ProjectOps.tryNode d (idOf "c") s.Project) "failed batch applied nothing")

let geometryNormalization =
    test "Geometry: whole-unit rounding, legal negatives, invalid values rejected" (fun () ->
        equal (Ok { Position = { X = -10; Y = 11 }; Size = { Width = 8; Height = 100001 - 1 } }) (Geometry.box -10.4 10.5 8.0 100000.0) "normalized"
        expect (Geometry.box 0.0 0.0 7.9 10.0 |> Result.isError) "below minimum size"
        expect (Geometry.box infinity 0.0 10.0 10.0 |> Result.isError) "infinite"
        expect (Geometry.box 2_000_000.0 0.0 10.0 10.0 |> Result.isError) "out of range")

let deleteNodeSurfacesEdges =
    test "Deleting a connected node surfaces incident connectors (FDA-025)" (fun () ->
        let s = twoNodes () |> runAll [ Flow(Connect(d, idOf "ab", "flow", endpoint "a", endpoint "b", None)) ]
        let findings = errorOf (Editor.dispatch (Flow(RemoveNode(d, idOf "a", RejectIfConnected))) s) "remove connected"
        expect (findings |> List.exists (fun f -> f.Target.EndsWith "edge:ab")) "the incident edge is named"
        let removed = okOr (Editor.dispatch (Flow(RemoveNode(d, idOf "a", RemoveIncidentEdges))) s) "remove with edges"
        equal [ "edge.removed-with-node" ] (removed.LastObligations |> List.map _.Code) "obligation reports the removed edge"
        equal [] (ProjectOps.tryDiagram d removed.Project |> Option.get).Edges "no dangling edge remains")

let workflowRules =
    test "Workflow profile: illegal connections rejected, decisions and lanes validated" (fun () ->
        let s =
            Editor.start (empty ())
            |> runAll
                [ Flow(AddDiagram(d, "WF", workflow))
                  Flow(AddNode(d, idOf "s", "start", "Start", 0.0, 0.0, 100.0, 60.0))
                  Flow(AddNode(d, idOf "q", "decision", "OK?", 200.0, 0.0, 100.0, 100.0))
                  Flow(AddNode(d, idOf "e", "end", "End", 400.0, 0.0, 100.0, 60.0))
                  Flow(Connect(d, idOf "sq", "flow", endpoint "s", endpoint "q", None))
                  Flow(Connect(d, idOf "qe", "flow", endpoint "q", endpoint "e", Some "yes"))
                  Flow(AddGroup(d, idOf "l1", Lane, "Sales", 0.0, 0.0, 600.0, 100.0))
                  Flow(AddGroup(d, idOf "l2", Lane, "Legal", 0.0, 110.0, 600.0, 100.0))
                  Flow(AssignLane(d, idOf "q", Some(idOf "l1"))) ]
        errorOf (Editor.dispatch (Flow(Connect(d, idOf "x", "flow", endpoint "e", endpoint "q", None))) s) "end cannot flow out" |> ignore
        errorOf (Editor.dispatch (Flow(Connect(d, idOf "x", "flow", endpoint "q", endpoint "s", None))) s) "start cannot receive" |> ignore
        let findings = Validation.run s.Project |> List.map _.Code
        expect (List.contains "workflow.decision.outcomes" findings) "a decision with one outcome is reported"
        expect (not (findings |> List.exists (fun c -> c.StartsWith "id."))) "no integrity problems"
        let moved = okOr (Editor.dispatch (Flow(AssignLane(d, idOf "q", Some(idOf "l2")))) s) "move lane"
        equal [ "lane.responsibility-changed" ] (moved.LastObligations |> List.map _.Code) "responsibility change surfaced (FDA-102)"
        let lanes = (ProjectOps.tryDiagram d moved.Project |> Option.get).Groups |> List.filter (fun g -> List.contains (idOf "q") g.Members)
        equal [ "Legal" ] (lanes |> List.map _.Label) "lanes are exclusive")

// ---------------------------------------------------------------------------
// Metadata (FDA-800..928)
// ---------------------------------------------------------------------------

let metadataStates =
    test "Metadata distinguishes explicit, default, derived, conflict, unknown, unavailable and invalid" (fun () ->
        let owner =
            { Key = idOf "owner"; Name = "Owner"; Help = None; Type = TextField None; AppliesTo = set [ NodeTarget ]; Disclosure = visible
              Default = None; Required = true; Derivation = Some(FromMembership Lane); Origin = ProjectLocal }
        let s =
            twoNodes ()
            |> runAll
                [ MetadataCmd(DefineField(statusField visible))
                  MetadataCmd(DefineField owner)
                  Flow(AddGroup(d, idOf "lane", Lane, "Finance", 0.0, 0.0, 400.0, 100.0))
                  Flow(AssignLane(d, idOf "a", Some(idOf "lane"))) ]
        let resolve key n p = ProjectOps.resolveField (ProjectOps.tryField (idOf key) p |> Option.get) (nodeRef n) p
        equal (ResolvedDefault(Enum "open")) (resolve "status" "a" s.Project) "default is not explicit"
        equal (ResolvedDerived(Text "Finance", "membership:lane:lane")) (resolve "owner" "a" s.Project) "derived from lane"
        equal (ResolvedMissing true) (resolve "owner" "b" s.Project) "required and missing"
        let conflicted = s |> runAll [ MetadataCmd(SetValue([ nodeRef "a" ], idOf "owner", Explicit(Text "Legal"))) ]
        equal (ResolvedConflict(Text "Legal", Text "Finance", "membership:lane:lane")) (resolve "owner" "a" conflicted.Project) "authored vs derived conflict"
        expect (Validation.run conflicted.Project |> List.exists (fun f -> f.Code = "metadata.value.conflict")) "conflict finding"
        let unknown = s |> runAll [ MetadataCmd(SetValue([ nodeRef "b" ], idOf "status", UnknownValue)) ]
        equal ResolvedUnknown (resolve "status" "b" unknown.Project) "unknown is not empty"
        let materialized = s |> runAll [ MetadataCmd(MaterializeValue([ nodeRef "a" ], idOf "owner")) ]
        equal (Some(Explicit(Text "Finance"))) ((ProjectOps.tryNode d (idOf "a") materialized.Project |> Option.get).Metadata |> Map.tryFind (idOf "owner")) "materialize derived"
        errorOf (Editor.dispatch (MetadataCmd(SetValue([ nodeRef "a" ], idOf "status", Explicit(Enum "purple")))) s) "invalid enum" |> ignore
        errorOf (Editor.dispatch (MetadataCmd(SetValue([ nodeRef "a"; DiagramRef d ], idOf "status", Explicit(Enum "blocked")))) s) "bulk edit is atomic" |> ignore
        equal (ResolvedDefault(Enum "open")) (resolve "status" "a" s.Project) "nothing applied by the failed bulk edit"
        let cleared = s |> runAll [ MetadataCmd(SetValue([ nodeRef "a" ], idOf "status", Explicit(Enum "blocked"))); MetadataCmd(ClearValue([ nodeRef "a" ], idOf "status")) ]
        equal (ResolvedDefault(Enum "open")) (resolve "status" "a" cleared.Project) "clear reveals the default")

let metadataFieldLifecycle =
    test "Field rename keeps its key; removal of a used field is blocked" (fun () ->
        let s =
            twoNodes ()
            |> runAll [ MetadataCmd(DefineField(statusField visible)); MetadataCmd(SetValue([ nodeRef "a" ], idOf "status", Explicit(Enum "blocked"))) ]
        let renamed = s |> runAll [ MetadataCmd(RenameField(idOf "status", "State")) ]
        equal (Some(Explicit(Enum "blocked"))) ((ProjectOps.tryNode d (idOf "a") renamed.Project |> Option.get).Metadata |> Map.tryFind (idOf "status")) "value survives rename"
        errorOf (Editor.dispatch (MetadataCmd(RemoveField(idOf "status", BlockFieldIfUsed))) s) "used field" |> ignore
        let removed = okOr (Editor.dispatch (MetadataCmd(RemoveField(idOf "status", RemoveFieldValues))) s) "explicit removal"
        equal [ "field.value-removed" ] (removed.LastObligations |> List.map _.Code) "affected values reported")

let metadataValueNormalization =
    test "Metadata values normalize deterministically and unsafe URLs are rejected" (fun () ->
        equal (Ok(Number 1.5m)) (MetadataRules.normalize (NumberField(None, None)) (Number 1.500m)) "trailing zeros"
        equal (Ok(TagList [ "a"; "b" ])) (MetadataRules.normalize (TagsField false) (TagList [ "b"; "a"; "b" ])) "set-like tags"
        equal (Ok(TagList [ "b"; "a" ])) (MetadataRules.normalize (TagsField true) (TagList [ "b"; "a" ])) "ordered tags keep order"
        equal (Ok(DateTime "2026-09-30T10:00:00-05:00")) (MetadataRules.normalize DateTimeField (DateTime "2026-09-30T10:00-05:00")) "offset preserved"
        expect (MetadataRules.normalize DateTimeField (DateTime "2026-09-30T10:00") |> Result.isError) "date-time without offset"
        expect (MetadataRules.normalize UrlField (Url "javascript:alert(1)") |> Result.isError) "javascript URL"
        equal (Ok(Url "https://example.com/x")) (MetadataRules.normalize UrlField (Url "https://example.com/x")) "https URL"
        equal "مرحبا ‏עברית" (MetadataRules.display None (Text "مرحبا ‏עברית")) "bidirectional text preserved")

// ---------------------------------------------------------------------------
// Appearance (FDA-830..976, FDA-1060..1069, FDA-1200..1220)
// ---------------------------------------------------------------------------

let private colored () =
    twoNodes ()
    |> runAll
        [ MetadataCmd(DefineField(statusField visible))
          AppearanceCmd(AddPaletteSlot { Id = idOf "blocked"; Name = "Blocked"; Value = PaletteLiteral(hex "#fde8d7"); Description = None })
          AppearanceCmd(AddPaletteSlot { Id = idOf "purple"; Name = "Purple"; Value = PaletteLiteral(hex "#efe6fb"); Description = None })
          AppearanceCmd(DefineStyle { Id = idOf "calm"; Name = "Calm"; Revision = 1; Targets = set [ NodeTarget ]
                                      Appearance = { Appearance.empty with Fill = Some(TokenColor(token "--ef-color-surface-secondary")); Stroke = Some(TokenColor(token "--ef-color-accent-primary")) } })
          AppearanceCmd(DefineMapping
                            { Id = idOf "status-map"; Name = "Status"; Field = idOf "status"; Targets = set [ NodeTarget ]; Enabled = true
                              Rules = [ { Match = Equals "blocked"; Outcome = UseAppearance { Appearance.empty with Fill = Some(PaletteColor(idOf "blocked")) }; Legend = "Blocked" } ]
                              Fallbacks = { Missing = NoMapping; Unknown = Apply(UseAppearance { Appearance.empty with Line = Some Dotted }, "Status unknown"); Unavailable = NoMapping; Invalid = NoMapping; Unmapped = NoMapping } })
          AppearanceCmd(ApplyStyle([ nodeRef "a"; nodeRef "b" ], Some(idOf "calm")))
          MetadataCmd(SetValue([ nodeRef "a" ], idOf "status", Explicit(Enum "blocked"))) ]

let private fillOf scope n (p: Project) = (AppearanceResolution.resolve scope p (nodeRef n) |> fst).Fill

let appearanceCascade =
    test "Appearance cascade: default < profile < style < mapping < override, and reset reveals the next layer" (fun () ->
        let s = colored ()
        let fill = fillOf EditorScope "a" s.Project
        equal (Some "#fde8d7") (fill.Value |> Option.bind _.Css) "mapping beats style"
        expect (match fill.Layer with MappingLayer(_, _, "Blocked") -> true | _ -> false) "fill comes from the mapping layer"
        equal (Some "var(--ef-color-surface-secondary)") ((fillOf EditorScope "b" s.Project).Value |> Option.bind _.Css) "style applies without mapping"
        let shape = (AppearanceResolution.resolve EditorScope s.Project (nodeRef "b") |> fst).Shape
        equal (Some Rectangle, ProfileDefaultLayer("general", "box")) (shape.Value, shape.Layer) "profile default shape"
        let overridden = s |> runAll [ AppearanceCmd(SetOverride([ nodeRef "a" ], { Appearance.empty with Fill = Some(PaletteColor(idOf "purple")) })) ]
        equal (Some "#efe6fb", OverrideLayer) ((fillOf EditorScope "a" overridden.Project).Value |> Option.bind _.Css, (fillOf EditorScope "a" overridden.Project).Layer) "override beats mapping"
        let reset = overridden |> runAll [ AppearanceCmd(ResetOverride([ nodeRef "a" ], [ FillProperty ])) ]
        equal (fillOf EditorScope "a" s.Project) (fillOf EditorScope "a" reset.Project) "reset reveals the mapping, not a copied value"
        let disabled = s.Project.Mappings |> List.map (fun m -> { m with Enabled = false }) |> List.head
        let noMapping = s |> runAll [ AppearanceCmd(UpdateMapping disabled) ]
        equal (Some "var(--ef-color-surface-secondary)") ((fillOf EditorScope "a" noMapping.Project).Value |> Option.bind _.Css) "disabling the mapping reveals the style"
        equal ((ProjectOps.tryNode d (idOf "a") s.Project |> Option.get).Metadata) ((ProjectOps.tryNode d (idOf "a") noMapping.Project |> Option.get).Metadata) "mapping never mutates metadata"
        let unknown = s |> runAll [ MetadataCmd(SetValue([ nodeRef "b" ], idOf "status", UnknownValue)) ]
        equal (Some Dotted) ((AppearanceResolution.resolve EditorScope unknown.Project (nodeRef "b") |> fst).Line.Value) "unknown fallback is explicit")

let styleAndPaletteLifecycle =
    test "Styles and palettes propagate by reference and never silently break" (fun () ->
        let s = colored ()
        let updated = s |> runAll [ AppearanceCmd(UpdateStyle(idOf "calm", { Appearance.empty with Fill = Some(LiteralColor(hex "#ffffff")) })) ]
        equal (Some "#ffffff") ((fillOf EditorScope "b" updated.Project).Value |> Option.bind _.Css) "style change propagates"
        equal 2 (ProjectOps.tryStyle (idOf "calm") updated.Project |> Option.get).Revision "style revision increments"
        equal (ProjectOps.tryNode d (idOf "b") s.Project |> Option.get).Appearance (ProjectOps.tryNode d (idOf "b") updated.Project |> Option.get).Appearance "objects are not rewritten"
        let renamed = s |> runAll [ AppearanceCmd(RenamePaletteSlot(idOf "blocked", "Needs attention")) ]
        equal (fillOf EditorScope "a" s.Project).Value (fillOf EditorScope "a" renamed.Project).Value "rename keeps references"
        errorOf (Editor.dispatch (AppearanceCmd(RemovePaletteSlot(idOf "blocked", BlockPaletteIfUsed))) s) "in-use palette" |> ignore
        let reassigned = okOr (Editor.dispatch (AppearanceCmd(RemovePaletteSlot(idOf "blocked", ReassignPalette(idOf "purple")))) s) "reassign"
        equal (Some "#efe6fb") ((fillOf EditorScope "a" reassigned.Project).Value |> Option.bind _.Css) "reassigned palette"
        let materialized = okOr (Editor.dispatch (AppearanceCmd(RemovePaletteSlot(idOf "blocked", MaterializePalette))) s) "materialize"
        equal (Some "#fde8d7") ((fillOf EditorScope "a" materialized.Project).Value |> Option.bind _.Css) "materialized value kept"
        errorOf (Editor.dispatch (AppearanceCmd(RemoveStyle(idOf "calm", BlockStyleIfUsed))) s) "in-use style" |> ignore
        let detached = okOr (Editor.dispatch (AppearanceCmd(RemoveStyle(idOf "calm", DetachAndMaterialize))) s) "detach style"
        equal (Some "var(--ef-color-surface-secondary)") ((fillOf EditorScope "b" detached.Project).Value |> Option.bind _.Css) "detached appearance equivalent"
        equal OverrideLayer (fillOf EditorScope "b" detached.Project).Layer "detached value is now local")

let colorIsNotSemantics =
    test "Color never implies status: same status may differ in color and different statuses may share one" (fun () ->
        let s =
            colored ()
            |> runAll
                [ MetadataCmd(SetValue([ nodeRef "b" ], idOf "status", Explicit(Enum "approved")))
                  AppearanceCmd(SetOverride([ nodeRef "a"; nodeRef "b" ], { Appearance.empty with Fill = Some(PaletteColor(idOf "purple")) })) ]
        equal (fillOf EditorScope "a" s.Project) (fillOf EditorScope "b" s.Project) "Blocked and Approved share purple"
        expect (Validation.run s.Project |> List.forall (fun f -> f.Category <> AppearanceRule && f.Category <> MetadataRule)) "no finding infers meaning from color"
        let greenBlocked = s |> runAll [ AppearanceCmd(SetOverride([ nodeRef "a" ], { Appearance.empty with Fill = Some(LiteralColor(hex "#2e8b57")); Foreground = Some(LiteralColor(hex "#ffffff")) })) ]
        equal (Some(Explicit(Enum "blocked"))) ((ProjectOps.tryNode d (idOf "a") greenBlocked.Project |> Option.get).Metadata |> Map.tryFind (idOf "status")) "green does not approve")

let disclosureControl =
    test "Mappings may not derive appearance from source-only fields (FDA-1260..1268)" (fun () ->
        let secret = { statusField { Scopes = Set.empty; DerivedPresentation = false } with Key = idOf "risk" }
        let s = twoNodes () |> runAll [ MetadataCmd(DefineField secret) ]
        let mapping =
            { Id = idOf "risk-map"; Name = "Risk"; Field = idOf "risk"; Targets = set [ NodeTarget ]; Enabled = true
              Rules = [ { Match = Equals "blocked"; Outcome = UseAppearance { Appearance.empty with Fill = Some(LiteralColor(hex "#ff0000")) }; Legend = "High risk" } ]
              Fallbacks = { Missing = NoMapping; Unknown = NoMapping; Unavailable = NoMapping; Invalid = NoMapping; Unmapped = NoMapping } }
        let findings = errorOf (Editor.dispatch (AppearanceCmd(DefineMapping mapping)) s) "mapping from source-only field"
        equal [ "disclosure.mapping.prohibited" ] (findings |> List.map _.Code) "disclosure path named"
        // A permitted field later restricted: output scopes stop using the mapping,
        // the editor may still highlight with it, and the change reports obligations.
        let c = colored ()
        let restricted = okOr (Editor.dispatch (MetadataCmd(SetFieldDisclosure(idOf "status", { Scopes = Set.empty; DerivedPresentation = false }))) c) "restrict"
        equal [ "disclosure.mapping.withheld" ] (restricted.LastObligations |> List.map _.Code) "dependent mapping revalidated"
        equal (Some "var(--ef-color-surface-secondary)") ((fillOf (OutputScope Rendered) "a" restricted.Project).Value |> Option.bind _.Css) "rendered output ignores the mapping"
        equal (Some "#fde8d7") ((fillOf EditorScope "a" restricted.Project).Value |> Option.bind _.Css) "editor-only highlight still allowed")

let colorSyntax =
    test "Literal colors normalize to #rrggbb; alpha and non-public tokens are rejected" (fun () ->
        equal "#aabbcc" (HexColor.value (hex "#ABC")) "short hex"
        expect (HexColor.parse "#aabbccdd" |> Result.isError) "alpha rejected"
        expect (HexColor.parse "red" |> Result.isError) "named colors rejected"
        expect (TokenRef.parse "--ef-private-thing" |> Result.isError) "private token rejected"
        let _, contrast = AppearanceResolution.resolve EditorScope (colored () |> runAll [ AppearanceCmd(SetOverride([ nodeRef "b" ], { Appearance.empty with Fill = Some(LiteralColor(hex "#222222")) })) ]).Project (nodeRef "b")
        expect (contrast |> List.exists (fun f -> f.Code = "appearance.contrast")) "illegible text on a dark fill is reported")

// ---------------------------------------------------------------------------
// References (FDA-1030..1039)
// ---------------------------------------------------------------------------

let typedReferences =
    test "Typed references block deletion of their targets and reject unsafe URLs" (fun () ->
        let other: DiagramId = idOf "d2"
        let s =
            twoNodes ()
            |> runAll
                [ Flow(AddDiagram(other, "Sub-flow", general))
                  Flow(AddReference(nodeRef "a", { Id = idOf "sub"; Target = DiagramTargetRef other; Label = Some "Details"; Availability = Available }))
                  Flow(AddReference(DiagramRef other, { Id = idOf "back"; Target = DiagramElementTargetRef(d, "b"); Label = None; Availability = Available })) ]
        let findings = errorOf (Editor.dispatch (Flow(RemoveDiagram other)) s) "delete referenced diagram"
        expect (findings |> List.exists (fun f -> f.Code = "reference.inbound" && f.Target.EndsWith "node:a")) "inbound reference named"
        errorOf (Editor.dispatch (Flow(RemoveNode(d, idOf "b", RemoveIncidentEdges))) s) "delete referenced node" |> ignore
        errorOf (Editor.dispatch (Flow(AddReference(nodeRef "b", { Id = idOf "x"; Target = ExternalUrlTarget "javascript:alert(1)"; Label = None; Availability = Unverified }))) s) "unsafe URL" |> ignore)

// ---------------------------------------------------------------------------
// Persistence and migration (DOCUMENT-MODEL "Migration")
// ---------------------------------------------------------------------------

let v1Migration =
    test "A v1 page-only project migrates without gaining diagrams and keeps legacy metadata" (fun () ->
        let text = System.IO.File.ReadAllText(System.IO.Path.Combine(__SOURCE_DIRECTORY__, "../../examples/two-page-project.json"))
        let project = okOr (Codec.load text |> Result.mapError Codec.describeLoadError) "load v1"
        equal [] project.Diagrams "no dummy diagrams"
        equal 2 project.Pages.Length "pages kept"
        expect project.LegacyMetadata.IsSome "free-form v1 metadata preserved"
        let home = project.Pages.Head.Nodes.Head
        equal 1 home.Navigation.Length "navigation preserved"
        equal [] (Validation.blockers project) "migrated project is valid"
        expect ((Codec.serialize project).Contains "\"schemaVersion\": 2") "saved as schema 2"
        equal project (reload project) "migrated project round-trips")

let diagramOnly =
    test "A diagram-only project needs no page" (fun () ->
        let p = (twoNodes ()).Project
        equal None p.StartPage "no start page"
        equal [] p.Pages "no dummy page"
        equal [] (Validation.blockers p) "valid"
        equal p (reload p) "round trip")

let newerSchemaFailsSafely =
    test "A newer schema version is refused with an explanation, not partially read" (fun () ->
        let text = (Codec.serialize (twoNodes ()).Project).Replace("\"schemaVersion\": 2", "\"schemaVersion\": 3")
        match Codec.load text with
        | Error(Codec.NewerSchema(3, 2)) -> ()
        | other -> fail (sprintf "expected NewerSchema, got %A" other)
        expect (Codec.load "{\"schemaVersion\": 2, \"projectId\": \"x\"," |> Result.isError) "invalid JSON rejected"
        expect (Codec.load "{\"schemaVersion\": 2, \"projectId\": \"x\", \"projectId\": \"y\"}" |> Result.isError) "duplicate keys rejected")

let deterministicSerialization =
    test "Serialization is deterministic and independent of edit order" (fun () ->
        let p = sample ()
        let text = Codec.serialize p
        equal text (Codec.serialize (reload p)) "stable across reload"
        expect (not (text.Contains "\r")) "LF only"
        let a = twoNodes () |> runAll [ MetadataCmd(DefineField(statusField visible)); MetadataCmd(SetValue([ nodeRef "a" ], idOf "status", Explicit(Enum "open")))
                                        MetadataCmd(DefineField({ statusField visible with Key = idOf "alpha" })); MetadataCmd(SetValue([ nodeRef "a" ], idOf "alpha", Explicit(Enum "open"))) ]
        let text2 = Codec.serialize a.Project
        expect (text2.IndexOf "\"alpha\": {" < text2.IndexOf "\"status\": {") "metadata keys sorted")

let unknownDataPreserved =
    test "Unknown profiles and undefined metadata fields are preserved, never flattened or rendered" (fun () ->
        let p = (twoNodes ()).Project
        let json = (Codec.serialize p).Replace("\"id\": \"general\"", "\"id\": \"sequence\"")
        let loaded = okOr (Codec.load json |> Result.mapError Codec.describeLoadError) "load unknown profile"
        equal "sequence" (ProjectOps.tryDiagram d loaded |> Option.get).Profile.Id "profile kept"
        expect (Validation.run loaded |> List.exists (fun f -> f.Code = "profile.unavailable")) "unavailable profile reported"
        errorOf (Commands.execute (Flow(AddNode(d, idOf "c", "box", "C", 0.0, 0.0, 20.0, 20.0))) loaded) "typed editing disabled" |> ignore
        let withUnknown = { p with Diagrams = p.Diagrams |> List.map (fun dg -> { dg with Nodes = dg.Nodes |> List.map (fun n -> { n with Metadata = Map.ofList [ idOf "vendor.x", Explicit(Text "kept") ] }) }) }
        equal withUnknown (reload withUnknown) "unknown field round-trips"
        expect (Validation.run withUnknown |> List.exists (fun f -> f.Code = "metadata.field.unknown")) "unknown field reported")

let sampleWorkflow =
    test "The canonical Workflow sample meets FDA-890..892 and validates" (fun () ->
        let p = sample ()
        let diagram = ProjectOps.tryDiagram Samples.diagramId p |> Option.get
        expect (diagram.Nodes.Length >= 5) "at least five nodes"
        equal [] (Validation.blockers p) "no blockers"
        let codes = Validation.run p |> List.map _.Code
        expect (not (List.contains "workflow.decision.outcomes" codes)) "decision has two outcomes"
        let fill n = (AppearanceResolution.resolve (OutputScope Rendered) p (NodeRef(Samples.diagramId, idOf n)) |> fst).Fill
        equal (fill "prepare").Value (fill "order").Value "different statuses share an authored color"
        expect ((fill "revise").Value <> (fill "approve").Value) "same status (Blocked) with different colors"
        expect (match (fill "approve").Layer with MappingLayer _ -> true | _ -> false) "explicit mapping drives Approve spend"
        expect (match (fill "revise").Layer with OverrideLayer -> true | _ -> false) "manual override beats the mapping")

let pinnedFormaContract =
    test "Studio's accepted Forma tokens and shapes match the pinned Forma diagram contract" (fun () ->
        let text = System.IO.File.ReadAllText(System.IO.Path.Combine(__SOURCE_DIRECTORY__, "../../contracts/forma/diagram-presentation.json"))
        let contract = okOr (Json.parse text) "contract"
        let strings path =
            path
            |> List.fold (fun (json: JsonValue option) name -> json |> Option.bind (Json.field name)) (Some contract)
            |> function
                | Some(JArray items) -> items |> List.choose (function JString s -> Some s | _ -> None)
                | _ -> []
        equal TokenRef.allowed (set (strings [ "color"; "tokenReferences" ])) "token references"
        equal [ "rectangle"; "rounded"; "pill"; "ellipse"; "diamond" ] (strings [ "elements"; "node"; "shapes" ]) "shapes"
        equal (Some(JString "2.0.0")) (Json.field "contractVersion" contract) "contract version")

let all =
    [ layoutProof; layoutRejectsInventedProperties; flowProof; sharedInfrastructure; rejectedCommandsDoNotMutate; geometryNormalization
      deleteNodeSurfacesEdges; workflowRules; metadataStates; metadataFieldLifecycle; metadataValueNormalization; appearanceCascade
      styleAndPaletteLifecycle; colorIsNotSemantics; disclosureControl; colorSyntax; typedReferences; v1Migration; diagramOnly
      newerSchemaFailsSafely; deterministicSerialization; unknownDataPreserved; sampleWorkflow; pinnedFormaContract ]
