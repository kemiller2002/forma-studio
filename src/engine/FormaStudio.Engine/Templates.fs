namespace FormaStudio.Engine

/// A named, reusable diagram fragment offered by the editor (FDA-1250..1257).
type DiagramTemplate =
    { Key: string
      Name: string
      Description: string
      Fragment: DiagramFragment }

/// Built-in templates. Each is authored through the public command surface on a
/// scratch project and then extracted, so a template is valid by construction
/// and carries its own dependency closure.
[<RequireQualifiedAccess>]
module Templates =
    let private scratch: DiagramId = Samples.idOf "template"

    let private build (profile: ProfileRef) key name description (commands: Command list) =
        let steps = Flow(AddDiagram(scratch, name, profile)) :: commands
        let project =
            steps
            |> List.fold
                (fun (p: Project) c ->
                    match Commands.execute c p with
                    | Ok applied -> applied.Project
                    | Error findings -> invalidOp (sprintf "Template %s is invalid: %s" key (findings |> List.map _.Message |> String.concat " ")))
                (Samples.emptyProject "template" "Template")
        let nodes = ProjectOps.tryDiagram scratch project |> Option.map (fun d -> d.Nodes |> List.map _.Id) |> Option.defaultValue []
        match Fragment.extract project scratch nodes with
        | Ok fragment -> { Key = key; Name = name; Description = description; Fragment = fragment }
        | Error e -> invalidOp e

    let private node id kind label x y w h = Flow(AddNode(scratch, Samples.idOf id, kind, label, x, y, w, h))
    let private edge id kind source target label =
        Flow(Connect(scratch, Samples.idOf id, kind, { Node = Samples.idOf source; Port = None }, { Node = Samples.idOf target; Port = None }, label))

    let private workflow =
        [ build { Id = "workflow"; Version = "1.0.0" } "approval-decision" "Approval decision"
              "A decision with two labelled outcomes: proceed or return for changes."
              [ node "approved" "decision" "Approved?" 0.0 60.0 150.0 150.0
                node "proceed" "activity" "Proceed" 240.0 0.0 210.0 120.0
                node "return" "activity" "Return for changes" 240.0 150.0 210.0 120.0
                edge "yes" "flow" "approved" "proceed" (Some "Yes")
                edge "no" "flow" "approved" "return" (Some "No") ] ]

    let private general =
        [ build { Id = "general"; Version = "1.0.0" } "box-note" "Box with note"
              "A box and an explanatory note joined by an association."
              [ node "box" "box" "Box" 0.0 0.0 200.0 120.0
                node "note" "note" "Note" 260.0 0.0 200.0 120.0
                edge "about" "association" "note" "box" None ] ]

    let private state =
        [ build { Id = "state"; Version = "1.0.0" } "state-pair" "Two states"
              "Two states joined by a named transition."
              [ node "from" "state" "State A" 0.0 0.0 180.0 100.0
                node "to" "state" "State B" 260.0 0.0 180.0 100.0
                edge "go" "transition" "from" "to" (Some "trigger") ] ]

    let private architecture =
        [ build { Id = "architecture"; Version = "1.0.0" } "service-storage" "Service with storage"
              "A service and the storage it reads and writes."
              [ node "service" "service" "Service" 0.0 0.0 200.0 120.0
                node "store" "storage" "Storage" 280.0 0.0 200.0 120.0
                edge "data" "data" "service" "store" (Some "reads and writes") ] ]

    let private all = lazy (workflow @ general @ state @ architecture)

    /// Templates authored for exactly this profile id and version.
    let forProfile (profile: ProfileRef) =
        all.Force() |> List.filter (fun t -> t.Fragment.Profile = profile)
