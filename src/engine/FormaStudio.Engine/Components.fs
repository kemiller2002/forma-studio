namespace FormaStudio.Engine

/// The Layout component contracts Studio can author. Each mirrors a public Forma
/// contract; Studio never invents properties the contract lacks (DOCUMENT-MODEL
/// "Component node"). Components outside this list are preserved but not editable.
type ComponentContract =
    { Id: string
      Source: string
      Slots: string list
      Properties: (string * (JsonValue -> Result<unit, string>)) list
      Content: string list }

[<RequireQualifiedAccess>]
module Components =
    let private enumProperty (allowed: string list) value =
        match value with
        | JString s when List.contains s allowed -> Ok()
        | _ -> Error(sprintf "must be one of %s" (System.String.Join(", ", allowed)))

    let private intRange lo hi value =
        match value with
        | JNumber text ->
            match System.Int32.TryParse text with
            | true, n when n >= lo && n <= hi -> Ok()
            | _ -> Error(sprintf "must be a whole number from %d to %d" lo hi)
        | _ -> Error(sprintf "must be a whole number from %d to %d" lo hi)

    /// Forma `ef-stack`: vertical composition. `density` is the public spacing hook
    /// (relaxed, standard, compact, analytical); Layout never stores x/y.
    let stack =
        { Id = "stack"
          Source = "forma:patterns/stack.html"
          Slots = [ "children" ]
          Properties = [ "density", enumProperty [ "relaxed"; "standard"; "compact"; "analytical" ] ]
          Content = [] }

    /// A native HTML heading styled by Forma foundations.
    let heading =
        { Id = "heading"
          Source = "html:h1-h6 with forma foundations"
          Slots = []
          Properties = [ "level", intRange 1 6 ]
          Content = [ "text" ] }

    let catalog = [ stack; heading ]

    let tryFind id = catalog |> List.tryFind (fun c -> c.Id = id)
