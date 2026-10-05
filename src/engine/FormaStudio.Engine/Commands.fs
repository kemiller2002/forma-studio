namespace FormaStudio.Engine

[<RequireQualifiedAccess>]
module Commands =
    let rec private apply (command: Command) (project: Project) : Result<Applied, Finding list> =
        match command with
        | Layout c -> LayoutCommands.apply c project
        | Flow c -> FlowCommands.apply c project
        | MetadataCmd c -> MetadataCommands.apply c project
        | AppearanceCmd c -> AppearanceCommands.apply c project
        | Batch(_, commands) ->
            commands
            |> List.fold
                (fun state c -> state |> Result.bind (fun (a: Applied) -> apply c a.Project |> Result.map (fun next -> { next with Obligations = a.Obligations @ next.Obligations })))
                (Ok { Project = project; Obligations = [] })

    /// Executes a command against the current state. On success the successor state
    /// introduces no new integrity blockers; on failure the prior state is unchanged
    /// because nothing is mutated (DOCUMENT-MODEL "Shared editor commands").
    let execute (command: Command) (project: Project) : Result<Applied, Finding list> =
        let before = Validation.blockers project |> Set.ofList
        apply command project
        |> Result.bind (fun applied ->
            let introduced = Validation.blockers applied.Project |> List.filter (fun f -> not (before.Contains f))
            if List.isEmpty introduced then Ok applied else Error introduced)
