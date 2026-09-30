namespace FormaStudio.Engine

/// Editor session state shared by Layout and Flow: one project, one history and
/// one selection model (FDA-221, FDA-224). Selection, pan, zoom and previews are
/// view state and never enter the project document (FDA-027).
type Session =
    { Project: Project
      Undo: Project list
      Redo: Project list
      Selection: ObjectRef list
      LastObligations: Obligation list }

[<RequireQualifiedAccess>]
module Editor =
    let start (project: Project) =
        { Project = project; Undo = []; Redo = []; Selection = []; LastObligations = [] }

    let private prune (project: Project) (selection: ObjectRef list) =
        selection |> List.filter (fun r -> ProjectOps.exists r project)

    /// Runs a command through the shared dispatcher. A rejected command leaves the
    /// session unchanged; an applied one becomes exactly one history entry.
    let dispatch (command: Command) (session: Session) : Result<Session, Finding list> =
        Commands.execute command session.Project
        |> Result.map (fun applied ->
            if applied.Project = session.Project then
                // A command that changes nothing is not a history entry.
                { session with LastObligations = applied.Obligations }
            else
                { Project = applied.Project
                  Undo = session.Project :: session.Undo
                  Redo = []
                  Selection = prune applied.Project session.Selection
                  LastObligations = applied.Obligations })

    /// Adopts a whole project produced by a reviewed operation outside the command
    /// surface (a three-way merge) as exactly one history entry. A project with
    /// integrity blockers is rejected and the session is unchanged.
    let adopt (project: Project) (session: Session) : Result<Session, Finding list> =
        match Validation.blockers project with
        | [] when project = session.Project -> Ok session
        | [] ->
            Ok { Project = project; Undo = session.Project :: session.Undo; Redo = []; Selection = prune project session.Selection; LastObligations = [] }
        | blockers -> Error blockers

    /// Computes the result of a command without committing it, for previews and
    /// agent change plans (FDA-1023). Nothing in the session changes.
    let preview (command: Command) (session: Session) = Commands.execute command session.Project

    let canUndo session = not (List.isEmpty session.Undo)
    let canRedo session = not (List.isEmpty session.Redo)

    let undo (session: Session) =
        match session.Undo with
        | [] -> session
        | previous :: rest ->
            { session with Project = previous; Undo = rest; Redo = session.Project :: session.Redo; Selection = prune previous session.Selection; LastObligations = [] }

    let redo (session: Session) =
        match session.Redo with
        | [] -> session
        | next :: rest ->
            { session with Project = next; Redo = rest; Undo = session.Project :: session.Undo; Selection = prune next session.Selection; LastObligations = [] }

    /// Selection changes are view state: no history entry, no project change.
    let select (items: ObjectRef list) (session: Session) =
        { session with Selection = prune session.Project (List.distinct items) }
