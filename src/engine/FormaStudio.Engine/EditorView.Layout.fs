namespace FormaStudio.Engine

/// The Layout panel, projected from the editor state for the Limen view (one
/// panel of EditorView). Pure. Every component on the open page is listed, at any
/// depth, with every content value and every choosable contract property (such
/// as a declared alignment group), so nothing on the page is reachable only by
/// adding it; and the pre-export checks (LayoutChecks) are shown before export.
[<RequireQualifiedAccess>]
module LayoutView =
    let private str (s: string) = JString s
    let private item (fields: (string * JsonValue) list) = JObject fields

    let private rootOf (page: Page) = page.Nodes |> List.tryFind (fun n -> n.Component = "stack")

    /// Every component under the root stack with the container that holds it, in document order.
    let private entries (page: Page) =
        let rec walk (parent: ComponentNode option) (nodes: ComponentNode list) =
            nodes |> List.collect (fun n -> (parent, n) :: (n.Slots |> Map.toList |> List.collect (snd >> walk (Some n))))
        match rootOf page with
        | Some root -> root.Slots |> Map.tryFind "children" |> Option.defaultValue [] |> walk None
        | None -> []

    let private name (n: ComponentNode) = sprintf "%s %s" n.Component (Id.value n.Id)

    let private contentText key (n: ComponentNode) =
        n.Content |> Map.tryFind key |> Option.map (function JString t -> t | _ -> "") |> Option.defaultValue ""

    let private propertyText key (n: ComponentNode) =
        match n.Properties |> Map.tryFind key with
        | Some(JString s) -> s
        | Some(JBool b) -> (if b then "true" else "false")
        | Some(JNumber n) -> n
        | _ -> ""

    let private layoutItem (state: EditorState) (parent: ComponentNode option, c: ComponentNode) =
        let contract = Components.tryFind c.Component
        let content = contract |> Option.map _.Content |> Option.defaultValue []
        let primary = content |> List.tryHead |> Option.defaultValue "text"
        let label = if c.Component = "heading" then sprintf "Heading text for %s" (Id.value c.Id) else sprintf "%s %s for %s" c.Component primary (Id.value c.Id)
        let more =
            (match content with _ :: rest -> rest | [] -> []) |> List.map (fun k -> item [ "key", str (sprintf "%s|%s" (Id.value c.Id) k); "label", str (sprintf "%s %s for %s" c.Component k (Id.value c.Id)); "text", str (contentText k c) ])
        let choices =
            contract |> Option.map _.Properties |> Option.defaultValue []
            |> List.collect (fun (property, _) ->
                let current = propertyText property c
                let options = Components.propertyChoices property
                if options.IsEmpty then []
                else
                    (options |> List.map (fun v -> v, sprintf "%s: %s" property v)) @ [ "", sprintf "%s: %s" property (if property = "alignment" then "none" else "default") ]
                    |> List.map (fun (v, text) ->
                        item [ "key", str (sprintf "%s|%s|%s" (Id.value c.Id) property v); "label", str text; "pressed", str (if current = v then "true" else "false") ]))
        let place = match parent with Some p -> sprintf "Inside %s" (name p) | None -> "On the page"
        let alignment = Components.alignmentOf c |> Option.map (fun e -> sprintf " Declares an alignment group: its items share a %s edge." e) |> Option.defaultValue ""
        item (
            [ "key", str (Id.value c.Id + "|" + primary); "text", str (contentText primary c); "label", str label; "kind", str c.Component
              "isHeading", JBool(c.Component = "heading"); "notHeading", JBool(c.Component <> "heading")
              "title", str (name c); "place", str (place + "." + alignment)
              "contentFields", JArray more; "hasChoices", JBool(not choices.IsEmpty); "propertyChoices", JArray choices
              "choicesLabel", str (sprintf "Properties of %s" (name c)); "removeLabel", str (sprintf "Remove: %s" (name c))
              "moveEarlierLabel", str (sprintf "Move earlier: %s" (name c)); "moveLaterLabel", str (sprintf "Move later: %s" (name c))
              "exportLabel", str (sprintf "Export as HTML: %s" (name c)) ]
            @ IconView.layoutItem state c)

    let private checks (page: Page option) =
        page |> Option.map LayoutChecks.page |> Option.defaultValue []
        |> List.mapi (fun i f -> item [ "key", str (sprintf "%d:%s:%s" i f.Code f.Target); "text", str (LayoutChecks.describe f) ])

    let private checkCount (items: JsonValue list) =
        match items.Length with
        | 0 -> "No problems found before export."
        | 1 -> "1 problem to review before export."
        | n -> sprintf "%d problems to review before export." n

    let fields (state: EditorState) : (string * JsonValue) list =
        let p = EditorState.project state
        let page = EditorState.openPage state
        let exported = page |> Option.orElse (p.Pages |> List.tryHead)
        let layoutChecks, exportChecks = checks page, checks exported
        let containers =
            page |> Option.map entries |> Option.defaultValue []
            |> List.map snd
            |> List.filter (fun n -> n.Component <> "stack" && not (List.isEmpty (Components.tryFind n.Component |> Option.map _.Slots |> Option.defaultValue [])))
        [ "pageName", str (page |> Option.map _.Name |> Option.defaultValue "")
          "density",
          str (page |> Option.bind rootOf |> Option.bind (fun root -> Map.tryFind "density" root.Properties) |> Option.map (function JString d -> d | _ -> "standard") |> Option.defaultValue "standard")
          "layoutItems", page |> Option.map entries |> Option.defaultValue [] |> List.map (layoutItem state) |> JArray
          "componentChoices",
          Components.catalog |> List.filter (fun c -> c.Id <> "stack" && c.Id <> "heading") |> List.map (fun c -> item [ "key", str c.Id; "label", str ("Add " + c.Id) ]) |> JArray
          "layoutTargets",
          ("root", "page") :: (containers |> List.map (fun n -> Id.value n.Id, name n))
          |> List.map (fun (k, label) -> item [ "key", str k; "label", str ("Add into " + label); "pressed", str (if (state.LayoutTarget |> Option.defaultValue "root") = k then "true" else "false") ])
          |> JArray
          "densities", Components.densities |> List.map (fun d -> item [ "key", str d; "label", str d ]) |> JArray
          "layoutChecks", JArray layoutChecks
          "layoutCheckSummary", str (checkCount layoutChecks)
          "exportChecks", JArray exportChecks
          "exportCheckSummary", str (match exported with Some pg -> sprintf "%s: %s" pg.Name (checkCount exportChecks) | None -> "No Layout page to check.") ]
