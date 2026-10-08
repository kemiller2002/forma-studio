namespace FormaStudio.Engine

/// The templates the editor offers: the diagram profile's built-in templates and
/// copied items. View-facing reads over the editor state (no project change).
[<RequireQualifiedAccess>]
module EditorTemplates =
    /// Built-in templates for the diagram's profile, after copied items if any:
    /// (key, name, description, fragment).
    let templates (state: EditorState) =
        let builtIn =
            EditorState.diagram state |> Option.map (fun d -> Templates.forProfile d.Profile) |> Option.defaultValue []
            |> List.map (fun t -> t.Key, t.Name, t.Description, t.Fragment)
        let copied =
            state.Clipboard
            |> Option.map (fun f -> "clipboard", "Copied items", sprintf "%d item(s) and %d connector(s) copied from this project." f.Nodes.Length f.Edges.Length, f)
            |> Option.toList
        copied @ builtIn

    let chosenTemplate (state: EditorState) =
        state.Template |> Option.bind (fun key -> templates state |> List.tryFind (fun (k, _, _, _) -> k = key))
