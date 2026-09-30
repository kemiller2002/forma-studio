namespace FormaStudio.Engine

/// Canonical example projects, built only through the public command surface so
/// the example itself exercises the shared dispatcher (FDA-890..892, FDA-223).
[<RequireQualifiedAccess>]
module Samples =
    /// Identifiers in samples are compile-time constants; an invalid one is a
    /// programming error, not user input.
    let idOf<'K> (raw: string) : Id<'K> =
        match Id.create<'K> raw with
        | Ok id -> id
        | Error e -> invalidArg "raw" e

    let private hex raw =
        match HexColor.parse raw with
        | Ok h -> h
        | Error e -> invalidArg "raw" e

    let private token raw =
        match TokenRef.parse raw with
        | Ok t -> t
        | Error e -> invalidArg "raw" e

    let emptyProject id name =
        { Id = idOf id; Name = name; Description = None; FormaVersion = "0.3.0"; StartPage = None; Pages = []; Diagrams = []
          Fields = []; Palette = []; Styles = []; Mappings = []; Scenarios = []; Assets = []; Metadata = Map.empty; LegacyMetadata = None }

    let private visible = { Scopes = set [ Rendered; AgentExport; ProvenanceExport ]; DerivedPresentation = true }
    let private exportOnly = { Scopes = set [ AgentExport; ProvenanceExport ]; DerivedPresentation = false }

    let private field key name fieldType applies disclosure defaultValue derivation help =
        { Key = idOf key; Name = name; Help = Some help; Type = fieldType; AppliesTo = set applies; Disclosure = disclosure
          Default = defaultValue; Required = false; Derivation = derivation; Origin = ProjectLocal }

    let diagramId: DiagramId = idOf "purchase-request"

    let private node id = NodeRef(diagramId, idOf id)

    /// The purchase-request Workflow: seven nodes in three semantic lanes, rich
    /// metadata (status, owner derived from lane, phase, tags, ticket URL and a
    /// source-only cost center), authored colors that deliberately do not track
    /// status, an explicit status-to-palette mapping, a named style and typed
    /// references.
    let purchaseWorkflowCommands: Command list =
        let d = diagramId
        let nodeId (s: string) : NodeId = idOf s
        let flowEdge id source target label = Flow(Connect(d, idOf id, "flow", { Node = nodeId source; Port = None }, { Node = nodeId target; Port = None }, label))
        let statusField =
            field "status" "Status" (EnumField [ { Id = "not-started"; Label = "Not started" }; { Id = "in-progress"; Label = "In progress" }
                                                 { Id = "blocked"; Label = "Blocked" }; { Id = "done"; Label = "Done" } ])
                [ NodeTarget ] visible (Some(Enum "not-started")) None "Progress of the activity as recorded by its owner."
        [ Flow(AddDiagram(d, "Purchase request", { Id = "workflow"; Version = "1.0.0" }))
          Flow(AddGroup(d, idOf "lane-requester", Lane, "Requester", 0.0, 0.0, 1060.0, 240.0))
          Flow(AddGroup(d, idOf "lane-finance", Lane, "Finance", 0.0, 250.0, 1060.0, 240.0))
          Flow(AddGroup(d, idOf "lane-procurement", Lane, "Procurement", 0.0, 500.0, 1060.0, 270.0))
          Flow(AddNode(d, idOf "submitted", "start", "Request submitted", 40.0, 76.0, 150.0, 88.0))
          Flow(AddNode(d, idOf "prepare", "activity", "Prepare request", 250.0, 40.0, 210.0, 168.0))
          Flow(AddNode(d, idOf "revise", "activity", "Revise request", 600.0, 40.0, 210.0, 168.0))
          Flow(AddNode(d, idOf "budget-check", "decision", "Within budget?", 280.0, 295.0, 150.0, 150.0))
          Flow(AddNode(d, idOf "approve", "activity", "Approve spend", 600.0, 286.0, 210.0, 168.0))
          Flow(AddNode(d, idOf "order", "activity", "Issue purchase order", 600.0, 526.0, 210.0, 190.0))
          Flow(AddNode(d, idOf "placed", "end", "Order placed", 880.0, 577.0, 150.0, 88.0))
          Flow(AssignLane(d, nodeId "submitted", Some(idOf "lane-requester")))
          Flow(AssignLane(d, nodeId "prepare", Some(idOf "lane-requester")))
          Flow(AssignLane(d, nodeId "revise", Some(idOf "lane-requester")))
          Flow(AssignLane(d, nodeId "budget-check", Some(idOf "lane-finance")))
          Flow(AssignLane(d, nodeId "approve", Some(idOf "lane-finance")))
          Flow(AssignLane(d, nodeId "order", Some(idOf "lane-procurement")))
          Flow(AssignLane(d, nodeId "placed", Some(idOf "lane-procurement")))
          flowEdge "e-submit" "submitted" "prepare" None
          flowEdge "e-check" "prepare" "budget-check" None
          flowEdge "e-yes" "budget-check" "approve" (Some "yes, within budget")
          flowEdge "e-no" "budget-check" "revise" (Some "no, over budget")
          flowEdge "e-resubmit" "revise" "prepare" (Some "resubmit")
          flowEdge "e-approved" "approve" "order" None
          flowEdge "e-placed" "order" "placed" None
          Flow(SetEdgeRouting(d, idOf "e-resubmit", Manual [ { X = 705; Y = -24 }; { X = 355; Y = -24 } ]))
          MetadataCmd(DefineField statusField)
          MetadataCmd(DefineField(field "owner" "Owner" (TextField(Some 80)) [ NodeTarget ] visible None (Some(FromMembership Lane)) "Team responsible, derived from the lane."))
          MetadataCmd(DefineField(field "phase" "Phase" (EnumField [ { Id = "intake"; Label = "Intake" }; { Id = "review"; Label = "Review" }; { Id = "fulfilment"; Label = "Fulfilment" } ]) [ NodeTarget ] visible None None "Stage of the purchase lifecycle."))
          MetadataCmd(DefineField(field "tags" "Tags" (TagsField false) [ NodeTarget; EdgeTarget ] exportOnly None None "Free classification for search and export."))
          MetadataCmd(DefineField(field "ticket" "Ticket" UrlField [ NodeTarget ] exportOnly None None "Tracking ticket for the activity."))
          MetadataCmd(DefineField(field "cost-center" "Cost center" (TextField(Some 40)) [ NodeTarget ] MetadataRules.disclosureSourceOnly None None "Internal accounting code. Source-only: never rendered or exported."))
          MetadataCmd(SetValue([ node "prepare" ], idOf "status", Explicit(Enum "in-progress")))
          MetadataCmd(SetValue([ node "revise"; node "approve" ], idOf "status", Explicit(Enum "blocked")))
          MetadataCmd(SetValue([ node "submitted"; node "prepare"; node "revise" ], idOf "phase", Explicit(Enum "intake")))
          MetadataCmd(SetValue([ node "budget-check"; node "approve" ], idOf "phase", Explicit(Enum "review")))
          MetadataCmd(SetValue([ node "order"; node "placed" ], idOf "phase", Explicit(Enum "fulfilment")))
          MetadataCmd(SetValue([ node "approve"; node "order" ], idOf "tags", Explicit(TagList [ "spend"; "audit" ])))
          MetadataCmd(SetValue([ node "approve" ], idOf "ticket", Explicit(Url "https://tickets.example.com/PR-1042")))
          MetadataCmd(SetValue([ node "approve"; node "order" ], idOf "cost-center", Explicit(Text "CC-7731-RESTRICTED")))
          AppearanceCmd(AddPaletteSlot { Id = idOf "workflow-blocked"; Name = "Workflow blocked"; Value = PaletteLiteral(hex "#fde8d7"); Description = Some "Fill for items whose status is Blocked." })
          AppearanceCmd(AddPaletteSlot { Id = idOf "highlight"; Name = "Author highlight"; Value = PaletteLiteral(hex "#efe6fb"); Description = Some "A highlight authors choose by hand. It carries no status." })
          AppearanceCmd(DefineStyle { Id = idOf "decision-emphasis"; Name = "Decision emphasis"; Revision = 1; Targets = set [ NodeTarget ]
                                      Appearance = { Appearance.empty with Stroke = Some(TokenColor(token "--ef-color-accent-primary")); Accent = Some(TokenColor(token "--ef-color-accent-primary")) } })
          AppearanceCmd(DefineMapping
                            { Id = idOf "status-color"; Name = "Status color"; Field = idOf "status"; Targets = set [ NodeTarget ]; Enabled = true
                              Rules = [ { Match = Equals "blocked"; Legend = "Status is Blocked"
                                          Outcome = UseAppearance { Appearance.empty with Fill = Some(PaletteColor(idOf "workflow-blocked")); Accent = Some(LiteralColor(hex "#9a3b00")) } } ]
                              Fallbacks = { Missing = NoMapping; Unknown = NoMapping; Unavailable = NoMapping; Invalid = NoMapping; Unmapped = NoMapping } })
          AppearanceCmd(ApplyStyle([ node "budget-check" ], Some(idOf "decision-emphasis")))
          // Author-chosen colors that deliberately do not follow status:
          // two different statuses share the purple highlight, and a blocked item is green.
          AppearanceCmd(SetOverride([ node "prepare"; node "order" ], { Appearance.empty with Fill = Some(PaletteColor(idOf "highlight")); Accent = Some(LiteralColor(hex "#6b3fa0")) }))
          AppearanceCmd(SetOverride([ node "revise" ], { Appearance.empty with Fill = Some(LiteralColor(hex "#e3f4e1")); Accent = Some(LiteralColor(hex "#2e6b2a")) }))
          AppearanceCmd(SetOverride([ EdgeRef(d, idOf "e-no") ], { Appearance.empty with Line = Some Dashed }))
          AppearanceCmd(SetOverride([ EdgeRef(d, idOf "e-resubmit") ], { Appearance.empty with Line = Some Dotted }))
          Flow(AddReference(node "approve", { Id = idOf "req-42"; Target = RequirementTarget "REQ-42"; Label = Some "Spend approval requirement"; Availability = Unverified }))
          Flow(AddReference(node "approve", { Id = idOf "forma-53"; Target = IssueTarget("kemiller2002/forma", 53); Label = Some "Workflow color contract"; Availability = Unverified }))
          Flow(SetDisplay(d,
                            { NodeFields = [ idOf "status"; idOf "owner"; idOf "phase"; idOf "cost-center" ]
                              KindFields = Map.ofList [ "start", []; "end", []; "decision", [] ]
                              EdgeFields = []
                              Missing = ShowValueState })) ]

    /// Builds the sample through one editor session.
    let purchaseWorkflow () =
        purchaseWorkflowCommands
        |> List.fold (fun state command -> state |> Result.bind (Editor.dispatch command)) (Ok(Editor.start (emptyProject "purchase-request-example" "Purchase request example")))
        |> Result.map _.Project
