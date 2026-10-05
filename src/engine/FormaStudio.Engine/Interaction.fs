namespace FormaStudio.Engine

/// Helpers shared by the per-area update functions (Interaction.*.fs). Every
/// canonical change goes through `run`, i.e. Editor.dispatch -> Commands.execute.
module internal Interaction =
    let describeFindings (findings: Finding list) =
        findings |> List.map _.Message |> List.truncate 2 |> String.concat " "

    /// Runs a command; a rejection leaves the project unchanged and says why.
    let run (command: Command) (success: string) (state: EditorState) =
        match Editor.dispatch command state.Session with
        | Ok session ->
            let obligations = session.LastObligations |> List.map _.Message
            { state with Session = session; Status = String.concat " " (success :: obligations) }
        | Error findings -> { state with Status = sprintf "Not changed: %s" (describeFindings findings) }

    let status message (state: EditorState) = { state with Status = message }

    let select refs (state: EditorState) = { state with Session = Editor.select refs state.Session }

    /// The status for a wire name that no event handles.
    let unrecognized (name: string) (state: EditorState) = status (sprintf "Unrecognized action '%s'." name) state

    /// Element keys in the view are "node:ID", "edge:ID" or "group:ID".
    let refOfKey (state: EditorState) (key: string) =
        match key.Split(':', 2) with
        | [| "node"; id |] -> Id.create<NodeKind> id |> Result.toOption |> Option.map (fun n -> NodeRef(state.Diagram, n))
        | [| "edge"; id |] -> Id.create<EdgeKind> id |> Result.toOption |> Option.map (fun e -> EdgeRef(state.Diagram, e))
        | [| "group"; id |] -> Id.create<GroupKind> id |> Result.toOption |> Option.map (fun g -> GroupRef(state.Diagram, g))
        | _ -> None

    let parseNumber (text: string) =
        match System.Double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture) with
        | true, v -> Some v
        | _ -> None

    /// "id|a|b" from the gesture adapter: a node and two numbers.
    let parseDelta (text: string) =
        match text.Split('|') with
        | [| id; a; b |] ->
            match parseNumber a, parseNumber b with
            | Some x, Some y -> Id.create<NodeKind> id |> Result.toOption |> Option.map (fun n -> n, x, y)
            | _ -> None
        | _ -> None
