namespace FormaStudio.Engine

/// A node kind a profile permits. `Presentation` is the default Forma shape; the
/// semantic kind and the shape stay separate (FDA-950..954).
type NodeKindSpec =
    { Kind: string
      Label: string
      DefaultShape: Shape
      AllowedShapes: Shape list option
      MaxIncoming: int option
      MaxOutgoing: int option }

type EdgeKindSpec =
    { Kind: string
      Label: string
      Directed: bool
      DefaultLine: LineStyle }

/// A diagram profile: explicit element kinds, connection legality, validation,
/// presentation defaults and derived metadata (DOCUMENT-MODEL "Diagram profile").
/// Profiles are data plus pure functions; they never fork the editor core.
type DiagramProfile =
    { Id: string
      Version: string
      Name: string
      NodeKinds: NodeKindSpec list
      EdgeKinds: EdgeKindSpec list
      /// Rejects an illegal connection before canonical mutation (FDA-043).
      CanConnect: NodeKindSpec -> EdgeKindSpec -> NodeKindSpec -> Result<unit, string>
      /// Profile rules over a whole diagram (FDA-160..163, FDA-448..474).
      Validate: Diagram -> Finding list
      /// Lanes carry ownership semantics in this profile (FDA-102).
      SemanticLanes: bool
      /// Claims the profile makes about itself; never "executable" (FDA-084).
      Description: string }

[<RequireQualifiedAccess>]
module Profiles =
    let private node kind label shape =
        { Kind = kind; Label = label; DefaultShape = shape; AllowedShapes = None; MaxIncoming = None; MaxOutgoing = None }

    let private target (diagram: Diagram) (id: NodeId) =
        sprintf "diagram:%s/node:%s" (Id.value diagram.Id) (Id.value id)

    let general =
        { Id = "general"
          Version = "1.0.0"
          Name = "General"
          NodeKinds = [ node "box" "Box" Rectangle; node "note" "Note" Rounded; node "terminal" "Terminal" Pill ]
          EdgeKinds =
            [ { Kind = "flow"; Label = "Flow"; Directed = true; DefaultLine = Solid }
              { Kind = "association"; Label = "Association"; Directed = false; DefaultLine = Dashed } ]
          CanConnect = fun _ _ _ -> Ok()
          Validate = fun _ -> []
          SemanticLanes = false
          Description = "Minimally typed boxes, notes and connectors. No semantic constraints." }

    let private workflowKinds =
        [ { node "start" "Start" Ellipse with MaxIncoming = Some 0 }
          { node "end" "End" Ellipse with MaxOutgoing = Some 0 }
          node "activity" "Activity" Rounded
          { node "decision" "Decision" Diamond with AllowedShapes = Some [ Diamond ] } ]

    /// Workflow specification semantics. Decisions need at least two labelled
    /// outcomes; start and end nodes bound the flow; unreachable activities are
    /// reported. This is a specification, not an executable process (FDA-084).
    let private validateWorkflow (diagram: Diagram) =
        let outgoing id = diagram.Edges |> List.filter (fun e -> e.Source.Node = id)
        let incoming id = diagram.Edges |> List.filter (fun e -> e.Target.Node = id)
        let starts = diagram.Nodes |> List.filter (fun n -> n.Kind = "start")

        let decisions =
            diagram.Nodes
            |> List.filter (fun n -> n.Kind = "decision")
            |> List.collect (fun n ->
                let outs = outgoing n.Id
                [ if outs.Length < 2 then
                      Finding.create "workflow.decision.outcomes" Warning ProfileRule (target diagram n.Id)
                          (sprintf "Decision '%s' has %d outgoing outcome(s); a decision needs at least two." n.Label outs.Length)
                  if outs |> List.exists (fun e -> e.Label |> Option.forall System.String.IsNullOrWhiteSpace) then
                      Finding.create "workflow.decision.unlabelled" Warning ProfileRule (target diagram n.Id)
                          (sprintf "Every outcome of decision '%s' needs a label that states its condition." n.Label) ])

        let missingStart =
            if List.isEmpty starts && not (List.isEmpty diagram.Nodes) then
                [ Finding.create "workflow.start.missing" Warning ProfileRule (sprintf "diagram:%s" (Id.value diagram.Id)) "The workflow has no start node." ]
            else []

        let reachable =
            let rec walk visited frontier =
                match frontier with
                | [] -> visited
                | id :: rest when Set.contains id visited -> walk visited rest
                | id :: rest -> walk (Set.add id visited) ((outgoing id |> List.map (fun e -> e.Target.Node)) @ rest)
            walk Set.empty (starts |> List.map _.Id)

        let unreachable =
            if List.isEmpty starts then []
            else
                diagram.Nodes
                |> List.filter (fun n -> not (reachable.Contains n.Id))
                |> List.map (fun n ->
                    Finding.create "workflow.unreachable" Advisory ProfileRule (target diagram n.Id)
                        (sprintf "'%s' cannot be reached from a start node." n.Label))

        let deadEnds =
            diagram.Nodes
            |> List.filter (fun n -> n.Kind <> "end" && List.isEmpty (outgoing n.Id) && not (List.isEmpty (incoming n.Id)))
            |> List.map (fun n ->
                Finding.create "workflow.dead-end" Advisory ProfileRule (target diagram n.Id)
                    (sprintf "'%s' has no outgoing flow and is not an end node." n.Label))

        missingStart @ decisions @ unreachable @ deadEnds

    let workflow =
        { Id = "workflow"
          Version = "1.0.0"
          Name = "Workflow"
          NodeKinds = workflowKinds
          EdgeKinds = [ { Kind = "flow"; Label = "Flow"; Directed = true; DefaultLine = Solid } ]
          CanConnect =
            fun source _ targetKind ->
                if source.Kind = "end" then Error "An end node cannot have outgoing flow."
                elif targetKind.Kind = "start" then Error "A start node cannot have incoming flow."
                else Ok()
          Validate = validateWorkflow
          SemanticLanes = true
          Description = "Start, end, activity and decision with labelled outcomes and semantic lanes. A specification only; it does not execute." }

    let private nodeTarget (diagram: Diagram) (id: NodeId) = target diagram id

    /// State machine documentation. Transitions carry "trigger [guard] / action"
    /// as authored label text. The profile documents a state model; it is not the
    /// authoritative domain state machine (FDA-085, FDA-086).
    let private validateState (diagram: Diagram) =
        let initials = diagram.Nodes |> List.filter (fun n -> n.Kind = "initial")
        let outgoing id = diagram.Edges |> List.filter (fun e -> e.Source.Node = id)
        let initialFindings =
            match initials with
            | [ _ ] -> []
            | [] when not (List.isEmpty diagram.Nodes) ->
                [ Finding.create "state.initial.missing" Warning ProfileRule (sprintf "diagram:%s" (Id.value diagram.Id)) "The state diagram has no initial state." ]
            | [] -> []
            | many ->
                many |> List.map (fun n -> Finding.create "state.initial.multiple" Warning ProfileRule (nodeTarget diagram n.Id) "A state diagram has exactly one initial state.")
        let reachable =
            let rec walk visited frontier =
                match frontier with
                | [] -> visited
                | id :: rest when Set.contains id visited -> walk visited rest
                | id :: rest -> walk (Set.add id visited) ((outgoing id |> List.map (fun e -> e.Target.Node)) @ rest)
            walk Set.empty (initials |> List.map _.Id)
        let unreachable =
            if List.isEmpty initials then []
            else
                diagram.Nodes
                |> List.filter (fun n -> not (reachable.Contains n.Id))
                |> List.map (fun n -> Finding.create "state.unreachable" Warning ProfileRule (nodeTarget diagram n.Id) (sprintf "State '%s' cannot be reached from the initial state." n.Label))
        let unlabelled =
            diagram.Edges
            |> List.filter (fun e -> e.Label |> Option.forall System.String.IsNullOrWhiteSpace)
            |> List.filter (fun e -> diagram.Nodes |> List.exists (fun n -> n.Id = e.Source.Node && n.Kind = "state"))
            |> List.map (fun e -> Finding.create "state.transition.trigger" Advisory ProfileRule (sprintf "diagram:%s/edge:%s" (Id.value diagram.Id) (Id.value e.Id)) "Name the trigger that causes this transition.")
        initialFindings @ unreachable @ unlabelled

    let state =
        { Id = "state"
          Version = "1.0.0"
          Name = "State"
          NodeKinds =
            [ { node "initial" "Initial state" Ellipse with MaxIncoming = Some 0 }
              node "state" "State" Rounded
              { node "final" "Final state" Ellipse with MaxOutgoing = Some 0 } ]
          EdgeKinds = [ { Kind = "transition"; Label = "Transition"; Directed = true; DefaultLine = Solid } ]
          CanConnect =
            fun source _ targetKind ->
                if source.Kind = "final" then Error "A final state has no outgoing transitions."
                elif targetKind.Kind = "initial" then Error "Nothing transitions into the initial state."
                else Ok()
          Validate = validateState
          SemanticLanes = false
          Description = "States and triggered transitions that document a state model. Not the authoritative application state machine." }

    /// Architecture overview. Boundaries are explicit groups; whether a boundary is
    /// a trust or deployment boundary is authored metadata, and the profile never
    /// infers security from notation (FDA-087..089).
    let private validateArchitecture (diagram: Diagram) =
        let kindOf id = diagram.Nodes |> List.tryFind (fun n -> n.Id = id) |> Option.map _.Kind
        diagram.Edges
        |> List.choose (fun e ->
            let edgeTarget = sprintf "diagram:%s/edge:%s" (Id.value diagram.Id) (Id.value e.Id)
            match e.Kind, kindOf e.Source.Node, kindOf e.Target.Node with
            | "message", Some s, Some t when s <> "queue" && t <> "queue" ->
                Some(Finding.create "architecture.message.queue" Advisory ProfileRule edgeTarget "A message relationship usually passes through a queue; name the channel if it does not.")
            | _ -> None)

    let architecture =
        { Id = "architecture"
          Version = "1.0.0"
          Name = "Architecture"
          NodeKinds =
            [ node "actor" "Actor" Pill
              node "service" "Service" Rounded
              node "storage" "Storage" Rectangle
              node "queue" "Queue" Pill
              node "external" "External system" Rectangle ]
          EdgeKinds =
            [ { Kind = "dependency"; Label = "Depends on"; Directed = true; DefaultLine = Solid }
              { Kind = "data"; Label = "Data flow"; Directed = true; DefaultLine = Dashed }
              { Kind = "message"; Label = "Message"; Directed = true; DefaultLine = Dotted }
              { Kind = "control"; Label = "Control"; Directed = true; DefaultLine = Solid } ]
          CanConnect =
            fun source edge targetKind ->
                if source.Kind = "storage" && edge.Kind = "control" then Error "Storage does not control other components; use a data relationship."
                elif targetKind.Kind = "actor" && edge.Kind = "control" then Error "Systems do not control actors; use a message or data relationship."
                else Ok()
          Validate = validateArchitecture
          SemanticLanes = false
          Description = "Systems, storage, queues and explicit boundaries with typed relationships. Boundary trust is authored metadata, never implied by notation." }

    let registry = [ general; workflow; state; architecture ]

    /// Profiles are identified by id and version; an unavailable profile is never
    /// replaced by General (DOCUMENT-MODEL "Diagram profile").
    let tryFind (reference: ProfileRef) =
        registry |> List.tryFind (fun p -> p.Id = reference.Id && p.Version = reference.Version)

    let nodeKind (profile: DiagramProfile) kind = profile.NodeKinds |> List.tryFind (fun k -> k.Kind = kind)
    let edgeKind (profile: DiagramProfile) kind = profile.EdgeKinds |> List.tryFind (fun k -> k.Kind = kind)
