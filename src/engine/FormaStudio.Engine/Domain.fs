namespace FormaStudio.Engine

/// Stable project identity. Persistence formats use the string representation.
type ProjectId = private ProjectId of string

/// Stable page identity. Internal navigation targets page identity, not route text.
type PageId = private PageId of string

/// Stable component-node identity within a project.
type NodeId = private NodeId of string

/// A route is a projection of page identity and may change without breaking internal links.
type Route = private Route of string

type Page =
    { Id: PageId
      Name: string
      Route: Route option }

type Project =
    { Id: ProjectId
      Name: string
      StartPage: PageId
      Pages: Page list }

/// Placeholder for the initial executable domain boundary.
/// Requirements and state transitions are specified before implementation is expanded.
module Domain =
    let pageIds (project: Project) =
        project.Pages |> List.map _.Id |> Set.ofList

    let hasStartPage (project: Project) =
        pageIds project |> Set.contains project.StartPage
