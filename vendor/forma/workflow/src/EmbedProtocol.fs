namespace Forma.Workflow

open System

/// The JSON wire format of the host boundary "forma-workflow-host/1". The
/// browser element, the iframe bridge and .NET hosts all speak it. It moves
/// data only: documents, events, HTML and emissions.
[<RequireQualifiedAccess>]
module EmbedProtocol =
    let private str (j: Json option) = match j with Some(Json.String s) -> Some s | _ -> None
    let private flt (j: Json option) = j |> Option.bind Json.tryFloat
    let private bool' (j: Json option) = match j with Some(Json.Bool b) -> b | _ -> false

    let private objectRef (j: Json) =
        match j with
        | Json.String key -> ObjectRef.tryParse key
        | Json.Object _ ->
            match str (Json.field "type" j), str (Json.field "id" j) with
            | Some t, Some id -> ObjectRef.tryParse (t + ":" + id)
            | _ -> None
        | _ -> None

    let private refJson (r: ObjectRef) =
        let t, id = match r with NodeRef id -> "node", id | EdgeRef id -> "edge", id | GroupRef id -> "group", id
        Json.Object [ "type", Json.String t; "id", Json.String id.Value ]

    let private endpoint (j: Json option) =
        j
        |> Option.bind (fun e ->
            match e with
            | Json.String node -> ObjectId.tryCreate node |> Option.map (fun n -> { Node = n; Port = None })
            | _ ->
                str (Json.field "node" e)
                |> Option.bind ObjectId.tryCreate
                |> Option.map (fun n -> { Node = n; Port = str (Json.field "port" e) |> Option.bind LocalId.tryCreate }))

    let private documentText (j: Json option) =
        match j with
        | Some(Json.String text) -> Some text
        | Some(Json.Object _ as o) -> Some(Json.compact o)
        | _ -> None

    /// Host API commands. Every one becomes a validated EditCommand.
    let command (j: Json) : Result<EditCommand, string> =
        let get name = Json.field name j
        let objectOf name = get name |> Option.bind objectRef
        let need name = Error $"command needs \"{name}\""
        match str (get "name") with
        | Some "addNode" ->
            let kind =
                match str (get "kind") with
                | Some k ->
                    match Codec.nodeKinds |> List.tryFind (fst >> (=) k) with
                    | Some(_, c) -> Some(CoreNode(c, None))
                    | None -> QualifiedName.tryParse k |> Option.map (fun q -> CustomNode(q, str (get "kindLabel") |> Option.defaultValue q.Name))
                | None -> Some(CoreNode(Task, None))
            match kind, str (get "label") with
            | Some k, Some label ->
                let at = match flt (get "x"), flt (get "y") with Some x, Some y -> Some { X = x; Y = y } | _ -> None
                Ok(AddNode(k, label, at))
            | None, _ -> Error "unknown kind"
            | _, None -> need "label"
        | Some "delete" ->
            match get "objects" with
            | Some(Json.Array items) -> Ok(Delete(items |> List.choose objectRef))
            | _ -> need "objects"
        | Some "move" ->
            match get "nodes", flt (get "dx"), flt (get "dy") with
            | Some(Json.Array ids), Some dx, Some dy -> Ok(Move(ids |> List.choose (function Json.String s -> ObjectId.tryCreate s | _ -> None), dx, dy))
            | _ -> need "nodes, dx and dy"
        | Some "setLabel" ->
            match objectOf "object" with
            | Some r -> Ok(SetText(r, LabelText, str (get "value")))
            | None -> need "object"
        | Some "setDescription" ->
            match objectOf "object" with
            | Some r -> Ok(SetText(r, DescriptionText, str (get "value")))
            | None -> need "object"
        | Some "connect" ->
            match endpoint (get "source"), endpoint (get "target") with
            | Some s, Some t ->
                let kind = str (get "kind") |> Option.bind (fun k -> Codec.edgeKinds |> List.tryFind (fst >> (=) k)) |> Option.map (fun (_, c) -> CoreEdge(c, None))
                Ok(Connect(s, t, kind))
            | _ -> need "source and target"
        | Some "setMetadata" ->
            match objectOf "object", str (get "key") with
            | Some r, Some key -> Ok(SetMetadata(r, key, get "value" |> Option.filter (fun v -> v <> Json.Null)))
            | _ -> need "object and key"
        | Some "setStatus" ->
            match objectOf "object" with
            | Some r ->
                match get "status" with
                | None | Some Json.Null -> Ok(SetStatus(r, None))
                | Some st ->
                        let state = str (Json.field "state" st) |> Option.defaultValue ""
                        let label = str (Json.field "label" st)
                        match Codec.states |> List.tryFind (fst >> (=) state), QualifiedName.tryParse state with
                        | Some(_, c), _ -> Ok(SetStatus(r, Some { State = CoreStatus(c, label); Detail = str (Json.field "detail" st); UpdatedAt = None }))
                        | None, Some q -> Ok(SetStatus(r, Some { State = CustomStatus(q, label |> Option.defaultValue q.Name); Detail = str (Json.field "detail" st); UpdatedAt = None }))
                        | _ -> Error $"unknown state \"{state}\""
            | None -> need "object"
        | Some "setColor" ->
            match objectOf "object", str (get "property") with
            | Some r, Some p ->
                let prop = match p with "stroke" -> StrokeColor | "accent" -> AccentColor | "foreground" -> ForegroundColor | _ -> FillColor
                Embed.parseColor (str (get "value") |> Option.defaultValue "") |> Result.map (fun c -> SetColor(r, prop, c))
            | _ -> need "object and property"
        | Some "autoLayout" -> Ok AutoLayout
        | Some other -> Error $"unknown command \"{other}\""
        | None -> need "name"

    let private uiEvent (j: Json) =
        { Name = str (Json.field "name" j) |> Option.defaultValue ""
          Key = str (Json.field "key" j)
          Arg = str (Json.field "arg" j)
          Value = str (Json.field "value" j)
          Fields =
            match Json.field "fields" j with
            | Some(Json.Object props) -> props |> List.choose (fun (k, v) -> match v with Json.String s -> Some(k, s) | _ -> None)
            | _ -> []
          Toggle = bool' (Json.field "toggle" j)
          Shift = bool' (Json.field "shift" j) }

    /// Decodes one host message. The instance id addresses one embedded component.
    let decode (j: Json) : Result<string * HostMessage, string> =
        let get name = Json.field name j
        match str (get "protocol"), str (get "instance") with
        | Some p, _ when p <> Embed.protocol -> Error $"unsupported protocol \"{p}\"; this engine speaks {Embed.protocol}"
        | None, _ -> Error "missing protocol"
        | _, None -> Error "missing instance"
        | _, Some instance when not (Text.RegularExpressions.Regex.IsMatch(instance, "^[A-Za-z][A-Za-z0-9_-]{0,63}$")) -> Error "instance ids are letters, digits, - and _"
        | _, Some instance ->
            let mode () = str (get "mode") |> Option.bind HostMode.tryParse |> Option.defaultValue ViewMode
            match str (get "type") with
            | Some "init" -> Ok(instance, Init(mode (), documentText (get "document")))
            | Some "load" ->
                match documentText (get "document") with
                | Some d -> Ok(instance, Load d)
                | None -> Error "load needs a document"
            | Some "mode" -> Ok(instance, SetMode(mode ()))
            | Some "command" ->
                match get "command" with
                | Some c -> command c |> Result.map (fun cmd -> instance, Execute cmd)
                | None -> Error "command needs a command"
            | Some "undo" -> Ok(instance, Undo)
            | Some "redo" -> Ok(instance, Redo)
            | Some "select" ->
                match get "objects" with
                | Some(Json.Array items) -> Ok(instance, SelectObjects(items |> List.choose objectRef))
                | _ -> Error "select needs objects"
            | Some "focus" ->
                match get "object" |> Option.bind objectRef with
                | Some r -> Ok(instance, FocusObject r)
                | None -> Error "focus needs an object"
            | Some "runtime" ->
                match get "states" with
                | Some(Json.Object states) ->
                    let parsed =
                        states
                        |> List.choose (fun (id, st) ->
                            ObjectId.tryCreate id
                            |> Option.map (fun oid ->
                                match st with
                                | Json.Object _ ->
                                    let state = str (Json.field "state" st) |> Option.defaultValue "unknown"
                                    let label = str (Json.field "label" st)
                                    let status =
                                        match Codec.states |> List.tryFind (fst >> (=) state), QualifiedName.tryParse state with
                                        | Some(_, c), _ -> Some { State = CoreStatus(c, label); Detail = str (Json.field "detail" st); UpdatedAt = str (Json.field "updatedAt" st) }
                                        | None, Some q -> Some { State = CustomStatus(q, label |> Option.defaultValue q.Name); Detail = None; UpdatedAt = None }
                                        | _ -> None
                                    oid, status
                                | _ -> oid, None))
                    Ok(instance, SetRuntimeState parsed)
                | _ -> Error "runtime needs states"
            | Some "event" ->
                match get "event" with
                | Some e -> Ok(instance, Ui(uiEvent e))
                | None -> Error "event needs an event"
            | Some other -> Error $"unknown message type \"{other}\""
            | None -> Error "missing type"

    let private reportFields (r: ValidationReport) =
        [ "valid", Json.Bool(Validation.isValid r)
          "class", Json.String(Validation.classText r.Class)
          "findings", Json.Array(r.Findings |> List.map Validation.findingJson) ]

    let private interactionJson (i: Interaction) =
        let target (t: InteractionTarget) =
            match t with
            | ToWorkflow w -> "workflow", w.Value
            | ToNode n -> "node", n.Value
            | ToEdge e -> "edge", e.Value
            | ToGroup g -> "group", g.Value
            | ToReference r -> "reference", r.Value
            | ToUrl u -> "url", u
            |> fun (k, v) -> Json.Object [ k, Json.String v ]
        match i with
        | NoInteraction -> [ "action", Json.String "none" ]
        | SelectIntent l -> [ yield "action", Json.String "select"; match l with Some x -> yield "label", Json.String x | None -> () ]
        | InspectIntent l -> [ yield "action", Json.String "inspect"; match l with Some x -> yield "label", Json.String x | None -> () ]
        | Navigate(t, l) -> [ yield "action", Json.String "navigate"; yield "target", target t; match l with Some x -> yield "label", Json.String x | None -> () ]
        | Open(t, l) -> [ yield "action", Json.String "open"; yield "target", target t; match l with Some x -> yield "label", Json.String x | None -> () ]
        | Command(q, l, p) ->
            [ yield "action", Json.String "command"
              yield "command", Json.String q.Value
              yield "label", Json.String l
              match p with Some ps -> yield "parameters", Json.Object ps | None -> () ]

    let emissionJson (e: Emission) =
        match e with
        | Loaded r ->
            Json.Object(
                [ "type", Json.String "load" ] @ reportFields r
                @ (r.Workflow |> Option.map (fun w -> [ "workflow", Codec.encode w ]) |> Option.defaultValue [])
            )
        | Changed c ->
            Json.Object([ "type", Json.String "change"; "description", Json.String c.Description; "workflow", Codec.encode c.After ] @ reportFields c.Report)
        | SelectionChanged refs -> Json.Object [ "type", Json.String "selection"; "objects", Json.Array(refs |> List.map refJson) ]
        | FocusChanged r -> Json.Object [ "type", Json.String "focus"; "object", (r |> Option.map refJson |> Option.defaultValue Json.Null) ]
        | IntentActivated(r, i) -> Json.Object([ "type", Json.String "intent"; "object", refJson r ] @ interactionJson i)
        | Picked refs -> Json.Object [ "type", Json.String "pick"; "objects", Json.Array(refs |> List.map refJson) ]
        | Refused message -> Json.Object [ "type", Json.String "refused"; "message", Json.String message ]

    let outputJson (instance: string) (out: EmbedOutput) =
        Json.Object
            [ yield "protocol", Json.String Embed.protocol
              yield "instance", Json.String instance
              yield "html", Json.String out.Html
              match out.FocusElement with Some f -> yield "focus", Json.String f | None -> ()
              yield "announce", Json.String out.State.Announcement
              yield "events", Json.Array(out.Emissions |> List.map emissionJson) ]

/// Several embedded components behind one engine instance (one WebAssembly
/// runtime per page). The host glue holds this value; this module stays pure.
type Sessions = Map<string, EmbedState>

[<RequireQualifiedAccess>]
module EmbedHost =
    let empty: Sessions = Map.empty

    let render (opts: RenderOptions) (s: EmbedState) (emissions: Emission list) =
        let focus =
            emissions
            |> List.tryPick (function FocusChanged(Some r) -> Some r | _ -> None)
            |> Option.map (fun r ->
                match r with
                | NodeRef id -> Render.nodeElementId s.IdPrefix id
                | EdgeRef id -> Render.edgeElementId s.IdPrefix id
                | GroupRef id -> Render.groupElementId s.IdPrefix id)
        { State = s; Html = Markup.toCompactHtml (EmbedView.view opts s); FocusElement = focus; Emissions = emissions }

    /// Handles one JSON message and returns the next sessions and the JSON reply.
    let dispatch (opts: RenderOptions) (text: string) (sessions: Sessions) : Sessions * string =
        let error (instance: string) (message: string) =
            Json.compact (
                Json.Object
                    [ "protocol", Json.String Embed.protocol; "instance", Json.String instance
                      "events", Json.Array [ Json.Object [ "type", Json.String "refused"; "message", Json.String message ] ] ]
            )
        match Json.parse text with
        | Error e -> sessions, error "" e
        | Ok json ->
            match EmbedProtocol.decode json with
            | Error e -> sessions, error (match Json.field "instance" json with Some(Json.String i) -> i | _ -> "") e
            | Ok(instance, msg) ->
                let state =
                    match Map.tryFind instance sessions, msg with
                    | Some s, _ -> s
                    | None, Init(mode, _) -> Embed.initial mode instance
                    | None, _ -> Embed.initial ViewMode instance
                let next, emissions = Embed.update msg state
                let out = render opts next emissions
                sessions.Add(instance, next), Json.compact (EmbedProtocol.outputJson instance out)

    /// Forgets an instance when its element is removed.
    let dispose (instance: string) (sessions: Sessions) = sessions.Remove instance

    /// The current document of an instance, canonical JSON text.
    let document (instance: string) (sessions: Sessions) =
        Map.tryFind instance sessions |> Option.bind _.Workflow |> Option.map Codec.serialize
