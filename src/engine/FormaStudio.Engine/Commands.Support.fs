namespace FormaStudio.Engine

/// Rejection and list helpers shared by the command-family modules.
module internal CommandSupport =
    let reject code target message =
        Error [ Finding.blocker code CommandRule target message ]

    let lift target (result: Result<'T, string>) =
        result |> Result.mapError (fun message -> [ Finding.blocker "command.rejected" CommandRule target message ])

    let ok project = Ok { Project = project; Obligations = [] }

    let insertAt index item list =
        let i = max 0 (min index (List.length list))
        List.take i list @ [ item ] @ List.skip i list

    let diagramTarget (d: DiagramId) = sprintf "diagram:%s" (Id.value d)

    let requireProfile (project: Project) diagramId =
        match ProjectOps.tryDiagram diagramId project with
        | None -> reject "diagram.missing" (diagramTarget diagramId) "The diagram does not exist."
        | Some diagram ->
            match Profiles.tryFind diagram.Profile with
            | None ->
                reject "profile.unavailable" (diagramTarget diagramId)
                    (sprintf "Profile %s@%s is not available, so the diagram is read-only here." diagram.Profile.Id diagram.Profile.Version)
            | Some profile -> Ok(diagram, profile)
