namespace Forma.Workflow

open System

/// Decoding from schema-valid JSON into the typed model, and canonical
/// encoding back. Metadata, extension values and parameters are carried as JSON
/// and never interpreted, so they round-trip unchanged.
[<RequireQualifiedAccess>]
module Codec =
    // -- small result combinators ---------------------------------------------

    type private D<'a> = Result<'a, string>

    type private ResultBuilder() =
        member _.Bind(r: Result<'a, 'e>, f: 'a -> Result<'b, 'e>) = Result.bind f r
        member _.Return(x: 'a) : Result<'a, 'e> = Ok x
        member _.ReturnFrom(r: Result<'a, 'e>) = r
        member _.Zero() : Result<unit, 'e> = Ok()

    let private result = ResultBuilder()

    let private traverse (f: 'a -> D<'b>) (items: 'a list) : D<'b list> =
        List.foldBack (fun x acc -> match f x, acc with Ok v, Ok vs -> Ok(v :: vs) | Error e, _ | _, Error e -> Error e) items (Ok [])

    let private optional (f: Json -> D<'b>) (value: Json option) : D<'b option> =
        match value with
        | None -> Ok None
        | Some v -> f v |> Result.map Some


    let private str (where: string) (json: Json) : D<string> =
        match json with
        | Json.String s -> Ok s
        | _ -> Error $"{where}: expected a string"

    let private obj (where: string) (json: Json) : D<(string * Json) list> =
        match json with
        | Json.Object props -> Ok props
        | _ -> Error $"{where}: expected an object"

    let private arr (where: string) (json: Json) : D<Json list> =
        match json with
        | Json.Array items -> Ok items
        | _ -> Error $"{where}: expected an array"

    let private num (where: string) (json: Json) : D<float> =
        Json.tryFloat json |> Option.map Ok |> Option.defaultValue (Error $"{where}: expected a number")

    let private int' (where: string) (json: Json) : D<int> =
        Json.tryInt json |> Option.map Ok |> Option.defaultValue (Error $"{where}: expected an integer")

    let private get name (props: (string * Json) list) = props |> List.tryFind (fst >> (=) name) |> Option.map snd

    let private required where name props =
        get name props |> Option.map Ok |> Option.defaultValue (Error $"{where}: missing \"{name}\"")

    let private optStr where name props = optional (str $"{where}.{name}") (get name props)

    let private choose (where: string) (table: (string * 'a) list) (json: Json) : D<'a> =
        match json with
        | Json.String s ->
            table |> List.tryFind (fst >> (=) s) |> Option.map (snd >> Ok) |> Option.defaultValue (Error $"{where}: \"{s}\" is not allowed")
        | _ -> Error $"{where}: expected a string"

    let private reverse (table: (string * 'a) list) (value: 'a) =
        table |> List.find (snd >> (=) value) |> fst

    // -- vocabularies ----------------------------------------------------------

    let tokens =
        [ "token:accent-primary", AccentPrimary; "token:accent-secondary", AccentSecondary
          "token:border-functional", BorderFunctional; "token:border-subtle", BorderSubtle
          "token:surface-primary", SurfacePrimary; "token:surface-secondary", SurfaceSecondary
          "token:surface-inverse", SurfaceInverse; "token:text-primary", TextPrimary
          "token:text-secondary", TextSecondary ]

    let shapes = [ "rectangle", Rectangle; "rounded", Rounded; "pill", Pill; "ellipse", Ellipse; "diamond", Diamond ]
    let lines = [ "solid", Solid; "dashed", Dashed; "dotted", Dotted ]

    let nodeKinds =
        [ "start", Start; "end", End; "task", Task; "decision", Decision; "merge", Merge; "event", Event
          "subprocess", Subprocess; "data", Data; "note", Note; "external", External ]

    let edgeKinds =
        [ "sequence", Sequence; "conditional", Conditional; "default", DefaultFlow; "exception", ExceptionFlow
          "message", Message; "association", Association; "data", DataFlow ]

    let groupKinds = [ "group", PlainGroup; "container", Container; "swimlane", Swimlane; "phase", Phase ]

    let states =
        [ "none", NoState; "pending", Pending; "ready", Ready; "active", Active; "waiting", Waiting
          "complete", Complete; "blocked", Blocked; "failed", Failed; "skipped", Skipped
          "cancelled", Cancelled; "unknown", Unknown ]

    let private sides = [ "top", Top; "right", Right; "bottom", Bottom; "left", Left ]
    let private portDirections = [ "in", In; "out", Out; "inout", InOut ]
    let private edgeDirections = [ "forward", Forward; "backward", Backward; "both", Both; "none", Undirected ]
    let private sizes = [ "compact", CompactSize; "standard", StandardSize; "wide", WideSize ]
    let private routings = [ "straight", Straight; "orthogonal", Orthogonal ]
    let private flows = [ "right", FlowRight; "down", FlowDown ]
    let private modes = [ "authored", Authored; "auto", Auto ]
    let private densities = [ "comfortable", Comfortable; "compact", Compact ]
    let private textDirections = [ "ltr", Ltr; "rtl", Rtl; "auto", AutoDirection ]

    // -- decoding --------------------------------------------------------------

    let private id' where json =
        str where json |> Result.bind (fun s -> ObjectId.tryCreate s |> Option.map Ok |> Option.defaultValue (Error $"{where}: invalid id"))

    let private localId where json =
        str where json |> Result.bind (fun s -> LocalId.tryCreate s |> Option.map Ok |> Option.defaultValue (Error $"{where}: invalid id"))

    let private ns where json =
        str where json |> Result.bind (fun s -> Namespace.tryCreate s |> Option.map Ok |> Option.defaultValue (Error $"{where}: invalid namespace"))

    let private qualified where (s: string) =
        QualifiedName.tryParse s |> Option.map Ok |> Option.defaultValue (Error $"{where}: \"{s}\" is not a core value or <namespace>:<name>")

    let colorSource where json : D<ColorSource> =
        match json with
        | Json.String s when s.StartsWith "#" -> HexColor.tryCreate s |> Option.map (LiteralSource >> Ok) |> Option.defaultValue (Error $"{where}: invalid colour")
        | Json.String _ -> choose where tokens json |> Result.map TokenSource
        | _ -> Error $"{where}: expected a colour"

    let colorValue where json : D<ColorValue> =
        match json with
        | Json.String s when s.StartsWith "palette:" ->
            SlotName.tryCreate (s.Substring 8) |> Option.map (PaletteColor >> Ok) |> Option.defaultValue (Error $"{where}: invalid palette slot")
        | _ ->
            colorSource where json
            |> Result.map (function TokenSource t -> TokenColor t | LiteralSource h -> LiteralColor h)

    let private objectColor where (json: Json option) : D<ObjectColor> =
        match json with
        | None -> Ok ObjectColor.none
        | Some j ->
            obj where j
            |> Result.bind (fun p ->
                let c name = optional (colorValue $"{where}.{name}") (get name p)
                match c "fill", c "stroke", c "accent", c "foreground" with
                | Ok f, Ok s, Ok a, Ok fg -> Ok { Fill = f; Stroke = s; Accent = a; Foreground = fg }
                | Error e, _, _, _ | _, Error e, _, _ | _, _, Error e, _ | _, _, _, Error e -> Error e)

    let private metadata where (json: Json option) : D<Metadata option> = optional (obj where) json

    let private extensions where (json: Json option) : D<Extensions> =
        match json with
        | None -> Ok []
        | Some j ->
            obj where j
            |> Result.bind (traverse (fun (k, v) ->
                match Namespace.tryCreate k, v with
                | Some n, Json.Object members -> Ok(n, members)
                | None, _ -> Error $"{where}: \"{k}\" is not a namespace"
                | _, _ -> Error $"{where}.{k}: an extension value is an object"))

    let private accessibility where (json: Json option) : D<Accessibility> =
        match json with
        | None -> Ok Accessibility.none
        | Some j ->
            obj where j
            |> Result.bind (fun p ->
                match optStr where "name" p, optStr where "description" p with
                | Ok n, Ok d -> Ok { Name = n; Description = d }
                | Error e, _ | _, Error e -> Error e)

    let private state where (p: (string * Json) list) : D<State> =
        result {
            let! s = required where "state" p |> Result.bind (str $"{where}.state")
            let! label = optStr where "label" p
            match List.tryFind (fst >> (=) s) states with
            | Some(_, core) -> return CoreStatus(core, label)
            | None ->
                let! q = qualified $"{where}.state" s
                match label with
                | Some l -> return CustomStatus(q, l)
                | None -> return! Error $"{where}: a namespaced state needs a label"
        }

    let private status where (json: Json option) : D<Status option> =
        json
        |> optional (fun j ->
            result {
                let! p = obj where j
                let! st = state where p
                let! detail = optStr where "detail" p
                let! updated = optStr where "updatedAt" p
                return { State = st; Detail = detail; UpdatedAt = updated }
            })

    let private reference where (json: Json) : D<Reference> =
        result {
            let! p = obj where json
            let! rid = required where "id" p |> Result.bind (localId $"{where}.id")
            let! system = required where "system" p |> Result.bind (ns $"{where}.system")
            let! key = required where "key" p |> Result.bind (str $"{where}.key")
            let! ty = optStr where "type" p
            let! label = optStr where "label" p
            let! href = optStr where "href" p
            let! meta = metadata $"{where}.metadata" (get "metadata" p)
            return { Id = rid; System = system; Type = ty; Key = key; Label = label; Href = href; Metadata = meta }
        }

    let private listOf (where: string) (name: string) (f: string -> Json -> D<'a>) (p: (string * Json) list) : D<'a list> =
        match get name p with
        | None -> Ok []
        | Some j -> arr where j |> Result.bind (List.mapi (fun i x -> i, x) >> traverse (fun (i, x) -> f $"{where}.{name}[{i}]" x))

    let private references where (p: (string * Json) list) = listOf where "references" reference p

    let private target where (json: Json) : D<InteractionTarget> =
        result {
            let! p = obj where json
            match p with
            | [ "workflow", v ] ->
                let! s = str where v
                return! WorkflowId.tryCreate s |> Option.map (ToWorkflow >> Ok) |> Option.defaultValue (Error $"{where}: invalid workflow id")
            | [ "node", v ] -> return! id' where v |> Result.map ToNode
            | [ "edge", v ] -> return! id' where v |> Result.map ToEdge
            | [ "group", v ] -> return! id' where v |> Result.map ToGroup
            | [ "reference", v ] -> return! localId where v |> Result.map ToReference
            | [ "url", v ] -> return! str where v |> Result.map ToUrl
            | _ -> return! Error $"{where}: a target names exactly one destination"
        }

    let private interaction where (json: Json option) : D<Interaction option> =
        json
        |> optional (fun j ->
            result {
                let! p = obj where j
                let! label = optStr where "label" p
                let tgt () = required where "target" p |> Result.bind (target $"{where}.target")
                match get "action" p with
                | Some(Json.String "none") -> return NoInteraction
                | Some(Json.String "select") -> return SelectIntent label
                | Some(Json.String "inspect") -> return InspectIntent label
                | Some(Json.String "navigate") ->
                    let! t = tgt ()
                    return Navigate(t, label)
                | Some(Json.String "open") ->
                    let! t = tgt ()
                    return Open(t, label)
                | Some(Json.String "command") ->
                    let! cmd = required where "command" p |> Result.bind (str $"{where}.command") |> Result.bind (qualified $"{where}.command")
                    let! parameters = optional (obj $"{where}.parameters") (get "parameters" p)
                    match label with
                    | Some l -> return Command(cmd, l, parameters)
                    | None -> return! Error $"{where}: a command needs a label"
                | _ -> return! Error $"{where}: unknown action"
            })

    let private point where (json: Json) : D<Point> =
        result {
            let! p = obj where json
            let! x = required where "x" p |> Result.bind (num $"{where}.x")
            let! y = required where "y" p |> Result.bind (num $"{where}.y")
            return { X = x; Y = y }
        }

    let private boxLayout where (json: Json option) : D<BoxLayout> =
        match json with
        | None -> Ok BoxLayout.empty
        | Some j ->
            result {
                let! p = obj where j
                let! pos =
                    match get "x" p, get "y" p with
                    | Some x, Some y -> result { let! xv = num $"{where}.x" x in let! yv = num $"{where}.y" y in return Some { X = xv; Y = yv } }
                    | None, None -> Ok None
                    | _ -> Error $"{where}: x and y come together"
                let! w = optional (num $"{where}.width") (get "width" p)
                let! h = optional (num $"{where}.height") (get "height" p)
                let! rank = optional (int' $"{where}.rank") (get "rank" p)
                let! order = optional (int' $"{where}.order") (get "order" p)
                let! size = optional (choose $"{where}.size" sizes) (get "size" p)
                return { Position = pos; Width = w; Height = h; Rank = rank; Order = order; Size = size }
            }

    let private edgeLayout where (json: Json option) : D<EdgeLayout> =
        match json with
        | None -> Ok EdgeLayout.empty
        | Some j ->
            result {
                let! p = obj where j
                let! routing = optional (choose $"{where}.routing" routings) (get "routing" p)
                let! wps = listOf where "waypoints" point p
                let! label = optional (point $"{where}.label") (get "label" p)
                return { Routing = routing; Waypoints = wps; LabelAt = label }
            }

    let private port where (json: Json) : D<Port> =
        result {
            let! p = obj where json
            let! pid = required where "id" p |> Result.bind (localId $"{where}.id")
            let! side = optional (choose $"{where}.side" sides) (get "side" p)
            let! dir = optional (choose $"{where}.direction" portDirections) (get "direction" p)
            let! label = optStr where "label" p
            let! maxc = optional (int' $"{where}.maxConnections") (get "maxConnections" p)
            let! meta = metadata $"{where}.metadata" (get "metadata" p)
            let! ext = extensions $"{where}.extensions" (get "extensions" p)
            return { Id = pid; Side = side; Direction = dir; Label = label; MaxConnections = maxc; Metadata = meta; Extensions = ext }
        }

    let private nodeKind where (p: (string * Json) list) : D<NodeKind> =
        result {
            let! k = required where "kind" p |> Result.bind (str $"{where}.kind")
            let! label = optStr where "kindLabel" p
            match List.tryFind (fst >> (=) k) nodeKinds with
            | Some(_, core) -> return CoreNode(core, label)
            | None ->
                let! q = qualified $"{where}.kind" k
                match label with
                | Some l -> return CustomNode(q, l)
                | None -> return! Error $"{where}: a namespaced kind needs kindLabel"
        }

    let private edgeKind where (p: (string * Json) list) : D<EdgeKind option> =
        match get "kind" p with
        | None -> Ok None
        | Some kj ->
            result {
                let! k = str $"{where}.kind" kj
                let! label = optStr where "kindLabel" p
                match List.tryFind (fst >> (=) k) edgeKinds with
                | Some(_, core) -> return Some(CoreEdge(core, label))
                | None ->
                    let! q = qualified $"{where}.kind" k
                    match label with
                    | Some l -> return Some(CustomEdge(q, l))
                    | None -> return! Error $"{where}: a namespaced kind needs kindLabel"
            }

    let private node (where: string) (json: Json) : D<Node> =
        result {
            let! p = obj where json
            let! nid = required where "id" p |> Result.bind (id' $"{where}.id")
            let! kind = nodeKind where p
            let! label = required where "label" p |> Result.bind (str $"{where}.label")
            let! desc = optStr where "description" p
            let! variant = optional (choose $"{where}.variant" shapes) (get "variant" p)
            let! color = objectColor $"{where}.color" (get "color" p)
            let! st = status $"{where}.status" (get "status" p)
            let! ports = listOf where "ports" port p
            let! refs = references where p
            let! meta = metadata $"{where}.metadata" (get "metadata" p)
            let! a11y = accessibility $"{where}.accessibility" (get "accessibility" p)
            let! inter = interaction $"{where}.interaction" (get "interaction" p)
            let! layout = boxLayout $"{where}.layout" (get "layout" p)
            let! ext = extensions $"{where}.extensions" (get "extensions" p)
            return
                { Id = nid; Kind = kind; Label = label; Description = desc; Variant = variant
                  Color = color; Status = st; Ports = ports; References = refs; Metadata = meta
                  Accessibility = a11y; Interaction = inter; Layout = layout; Extensions = ext }
        }

    let private endpoint where (json: Json) : D<Endpoint> =
        result {
            let! p = obj where json
            let! n = required where "node" p |> Result.bind (id' $"{where}.node")
            let! port = optional (localId $"{where}.port") (get "port" p)
            return { Node = n; Port = port }
        }

    let private edge (where: string) (json: Json) : D<Edge> =
        result {
            let! p = obj where json
            let! eid = required where "id" p |> Result.bind (id' $"{where}.id")
            let! src = required where "source" p |> Result.bind (endpoint $"{where}.source")
            let! tgt = required where "target" p |> Result.bind (endpoint $"{where}.target")
            let! dir = optional (choose $"{where}.direction" edgeDirections) (get "direction" p)
            let! kind = edgeKind where p
            let! label = optStr where "label" p
            let! desc = optStr where "description" p
            let! line = optional (choose $"{where}.line" lines) (get "line" p)
            let! width = optional (int' $"{where}.width") (get "width" p)
            let! stroke =
                match get "color" p with
                | None -> Ok None
                | Some c -> obj $"{where}.color" c |> Result.bind (fun cp -> optional (colorValue $"{where}.color.stroke") (get "stroke" cp))
            let! st = status $"{where}.status" (get "status" p)
            let! refs = references where p
            let! meta = metadata $"{where}.metadata" (get "metadata" p)
            let! a11y = accessibility $"{where}.accessibility" (get "accessibility" p)
            let! inter = interaction $"{where}.interaction" (get "interaction" p)
            let! layout = edgeLayout $"{where}.layout" (get "layout" p)
            let! ext = extensions $"{where}.extensions" (get "extensions" p)
            return
                { Id = eid; Source = src; Target = tgt; Direction = dir; Kind = kind; Label = label
                  Description = desc; Line = line; Width = width; Stroke = stroke; Status = st
                  References = refs; Metadata = meta; Accessibility = a11y; Interaction = inter
                  Layout = layout; Extensions = ext }
        }

    let private group (where: string) (json: Json) : D<Group> =
        result {
            let! p = obj where json
            let! gid = required where "id" p |> Result.bind (id' $"{where}.id")
            let! kind = required where "kind" p |> Result.bind (choose $"{where}.kind" groupKinds)
            let! label = required where "label" p |> Result.bind (str $"{where}.label")
            let! desc = optStr where "description" p
            let! members = listOf where "members" id' p
            let! parent = optional (id' $"{where}.parent") (get "parent" p)
            let! line = optional (choose $"{where}.line" lines) (get "line" p)
            let! color = objectColor $"{where}.color" (get "color" p)
            let! st = status $"{where}.status" (get "status" p)
            let! refs = references where p
            let! meta = metadata $"{where}.metadata" (get "metadata" p)
            let! a11y = accessibility $"{where}.accessibility" (get "accessibility" p)
            let! inter = interaction $"{where}.interaction" (get "interaction" p)
            let! layout = boxLayout $"{where}.layout" (get "layout" p)
            let! ext = extensions $"{where}.extensions" (get "extensions" p)
            return
                { Id = gid; Kind = kind; Label = label; Description = desc; Members = members
                  Parent = parent; Line = line; Color = color; Status = st; References = refs
                  Metadata = meta; Accessibility = a11y; Interaction = inter; Layout = layout
                  Extensions = ext }
        }

    let private formaTarget (json: Json) : D<FormaTarget> =
        result {
            let! p = obj "forma" json
            let! v = required "forma" "version" p |> Result.bind (str "forma.version")
            let! version = SemanticVersion.tryParse v |> Option.map Ok |> Option.defaultValue (Error "forma.version: invalid version")
            let! requires = listOf "forma" "requires" str p
            return { Version = version; Requires = requires }
        }

    let private workflowLayout (json: Json option) : D<WorkflowLayout> =
        match json with
        | None -> Ok WorkflowLayout.empty
        | Some j ->
            result {
                let! p = obj "layout" j
                let! dir = optional (choose "layout.direction" flows) (get "direction" p)
                let! mode = optional (choose "layout.mode" modes) (get "mode" p)
                let! canvas =
                    get "canvas" p
                    |> optional (fun c ->
                        result {
                            let! cp = obj "layout.canvas" c
                            let! w = required "layout.canvas" "width" cp |> Result.bind (num "layout.canvas.width")
                            let! h = required "layout.canvas" "height" cp |> Result.bind (num "layout.canvas.height")
                            return w, h
                        })
                return { Direction = dir; Mode = mode; Canvas = canvas }
            }

    let private legendEntry (where: string) (json: Json) : D<LegendEntry> =
        result {
            let! p = obj where json
            let! lid = required where "id" p |> Result.bind (localId $"{where}.id")
            let! label = required where "label" p |> Result.bind (str $"{where}.label")
            let! desc = optStr where "description" p
            let! color = objectColor $"{where}.color" (get "color" p)
            let! line = optional (choose $"{where}.line" lines) (get "line" p)
            return { Id = lid; Label = label; Description = desc; Color = color; Line = line }
        }

    let private presentation (json: Json option) : D<Presentation> =
        match json with
        | None -> Ok Presentation.empty
        | Some j ->
            result {
                let! p = obj "presentation" j
                let! density = optional (choose "presentation.density" densities) (get "density" p)
                let! legend = listOf "presentation" "legend" legendEntry p
                let! title = optStr "presentation" "legendTitle" p
                return { Density = density; Legend = legend; LegendTitle = title }
            }

    let private provenance (json: Json option) : D<Provenance option> =
        json
        |> optional (fun j ->
            result {
                let! p = obj "provenance" j
                let! producer =
                    get "producer" p
                    |> optional (fun pj ->
                        result {
                            let! pp = obj "provenance.producer" pj
                            let! name = required "provenance.producer" "name" pp |> Result.bind (str "provenance.producer.name")
                            let! version = optStr "provenance.producer" "version" pp
                            let! uri = optStr "provenance.producer" "uri" pp
                            return { Name = name; Version = version; Uri = uri }
                        })
                let! modifiedBy =
                    get "modifiedBy" p
                    |> optional (fun mj ->
                        result {
                            let! mp = obj "provenance.modifiedBy" mj
                            let! name = required "provenance.modifiedBy" "name" mp |> Result.bind (str "provenance.modifiedBy.name")
                            let! version = optStr "provenance.modifiedBy" "version" mp
                            return name, version
                        })
                let! created = optStr "provenance" "createdAt" p
                let! modified = optStr "provenance" "modifiedAt" p
                let! source = optStr "provenance" "source" p
                return { Producer = producer; CreatedAt = created; ModifiedAt = modified; Source = source; ModifiedBy = modifiedBy }
            })

    let private palette (json: Json option) : D<(SlotName * ColorSource) list> =
        match json with
        | None -> Ok []
        | Some j ->
            obj "palette" j
            |> Result.bind (traverse (fun (k, v) ->
                match SlotName.tryCreate k with
                | Some slot -> colorSource $"palette.{k}" v |> Result.map (fun c -> slot, c)
                | None -> Error $"palette: \"{k}\" is not a slot name"))

    /// Decodes a schema-valid document. Callers normally go through
    /// `Validation.load`, which runs the schema first so these errors are rare.
    let decode (json: Json) : Result<Workflow, string> =
        result {
            let! p = obj "document" json
            let! fv = required "document" "formatVersion" p |> Result.bind (str "formatVersion")
            let! formatVersion = SemanticVersion.tryParse fv |> Option.map Ok |> Option.defaultValue (Error "formatVersion: invalid version")
            let! wid = required "document" "id" p |> Result.bind (str "id")
            let! workflowId = WorkflowId.tryCreate wid |> Option.map Ok |> Option.defaultValue (Error "id: invalid workflow id")
            let! title = required "document" "title" p |> Result.bind (str "title")
            let! forma = required "document" "forma" p |> Result.bind formaTarget
            let! schema = optStr "document" "$schema" p
            let! desc = optStr "document" "description" p
            let! lang = optStr "document" "language" p
            let! dir = optional (choose "direction" textDirections) (get "direction" p)
            let! meta = metadata "metadata" (get "metadata" p)
            let! pal = palette (get "palette" p)
            let! nodes = listOf "document" "nodes" node p
            let! edges = listOf "document" "edges" edge p
            let! groups = listOf "document" "groups" group p
            let! layout = workflowLayout (get "layout" p)
            let! pres = presentation (get "presentation" p)
            let! ext = extensions "extensions" (get "extensions" p)
            let! prov = provenance (get "provenance" p)
            return
                { Schema = schema; FormatVersion = formatVersion; Id = workflowId; Title = title
                  Description = desc; Language = lang; TextDirection = dir; Forma = forma
                  Metadata = meta; Palette = pal; Nodes = nodes; Edges = edges; Groups = groups
                  Layout = layout; Presentation = pres; Extensions = ext; Provenance = prov }
        }

    // -- encoding --------------------------------------------------------------

    let private props (members: (string * Json option) list) =
        Json.Object(members |> List.choose (fun (k, v) -> v |> Option.map (fun x -> k, x)))

    let private s (v: string option) = v |> Option.map Json.String
    let private some (v: Json) = Some v
    let private whenAny (members: (string * Json option) list) =
        if members |> List.exists (snd >> Option.isSome) then Some(props members) else None

    let colorSourceText (c: ColorSource) =
        match c with
        | TokenSource t -> reverse tokens t
        | LiteralSource h -> h.Value

    let colorValueText (c: ColorValue) =
        match c with
        | TokenColor t -> reverse tokens t
        | LiteralColor h -> h.Value
        | PaletteColor slot -> "palette:" + slot.Value

    let private encodeColor (c: ObjectColor) =
        let v = Option.map (colorValueText >> Json.String)
        whenAny [ "fill", v c.Fill; "stroke", v c.Stroke; "accent", v c.Accent; "foreground", v c.Foreground ]

    let private encodeMeta (m: Metadata option) = m |> Option.map Json.Object

    let private encodeExtensions (e: Extensions) =
        if e.IsEmpty then None else Some(Json.Object(e |> List.map (fun (n, members) -> n.Value, Json.Object members)))

    let private encodeA11y (a: Accessibility) = whenAny [ "name", s a.Name; "description", s a.Description ]

    let stateText (st: State) =
        match st with
        | CoreStatus(c, _) -> reverse states c
        | CustomStatus(q, _) -> q.Value

    let private encodeStatus (st: Status option) =
        st
        |> Option.map (fun st ->
            let label = match st.State with CoreStatus(_, l) -> l | CustomStatus(_, l) -> Some l
            props [ "state", some (Json.String(stateText st.State)); "label", s label; "detail", s st.Detail; "updatedAt", s st.UpdatedAt ])

    let private encodeReferences (refs: Reference list) =
        if refs.IsEmpty then None
        else
            refs
            |> List.map (fun r ->
                props
                    [ "id", some (Json.String r.Id.Value); "system", some (Json.String r.System.Value); "type", s r.Type
                      "key", some (Json.String r.Key); "label", s r.Label; "href", s r.Href; "metadata", encodeMeta r.Metadata ])
            |> Json.Array
            |> Some

    let private encodeTarget (t: InteractionTarget) =
        match t with
        | ToWorkflow w -> "workflow", Json.String w.Value
        | ToNode n -> "node", Json.String n.Value
        | ToEdge e -> "edge", Json.String e.Value
        | ToGroup g -> "group", Json.String g.Value
        | ToReference r -> "reference", Json.String r.Value
        | ToUrl u -> "url", Json.String u
        |> fun (k, v) -> Json.Object [ k, v ]

    let private encodeInteraction (i: Interaction option) =
        i
        |> Option.map (fun i ->
            let action, target, command, label, parameters =
                match i with
                | NoInteraction -> "none", None, None, None, None
                | SelectIntent l -> "select", None, None, l, None
                | InspectIntent l -> "inspect", None, None, l, None
                | Navigate(t, l) -> "navigate", Some(encodeTarget t), None, l, None
                | Open(t, l) -> "open", Some(encodeTarget t), None, l, None
                | Command(q, l, p) -> "command", None, Some(Json.String q.Value), Some l, p |> Option.map Json.Object
            props [ "action", some (Json.String action); "target", target; "command", command; "label", s label; "parameters", parameters ])

    let private encodePoint (p: Point) = Json.Object [ "x", Json.number p.X; "y", Json.number p.Y ]

    let private encodeBox (l: BoxLayout) =
        whenAny
            [ "x", l.Position |> Option.map (fun p -> Json.number p.X)
              "y", l.Position |> Option.map (fun p -> Json.number p.Y)
              "width", l.Width |> Option.map Json.number
              "height", l.Height |> Option.map Json.number
              "rank", l.Rank |> Option.map (float >> Json.number)
              "order", l.Order |> Option.map (float >> Json.number)
              "size", l.Size |> Option.map (reverse sizes >> Json.String) ]

    let private encodeEdgeLayout (l: EdgeLayout) =
        whenAny
            [ "routing", l.Routing |> Option.map (reverse routings >> Json.String)
              "waypoints", (if l.Waypoints.IsEmpty then None else Some(Json.Array(l.Waypoints |> List.map encodePoint)))
              "label", l.LabelAt |> Option.map encodePoint ]

    let private encodePort (p: Port) =
        props
            [ "id", some (Json.String p.Id.Value)
              "side", p.Side |> Option.map (reverse sides >> Json.String)
              "direction", p.Direction |> Option.map (reverse portDirections >> Json.String)
              "label", s p.Label
              "maxConnections", p.MaxConnections |> Option.map (float >> Json.number)
              "metadata", encodeMeta p.Metadata
              "extensions", encodeExtensions p.Extensions ]

    let nodeKindText (k: NodeKind) =
        match k with
        | CoreNode(c, _) -> reverse nodeKinds c
        | CustomNode(q, _) -> q.Value

    let edgeKindText (k: EdgeKind) =
        match k with
        | CoreEdge(c, _) -> reverse edgeKinds c
        | CustomEdge(q, _) -> q.Value

    let groupKindText (k: GroupKind) = reverse groupKinds k
    let shapeText (s: Shape) = reverse shapes s
    let lineText (l: LineStyle) = reverse lines l
    let directionText (d: EdgeDirection) = reverse edgeDirections d
    let sideText (s: PortSide) = reverse sides s
    let portDirectionText (d: PortDirection) = reverse portDirections d

    let private encodeNode (n: Node) =
        let kindLabel = match n.Kind with CoreNode(_, l) -> l | CustomNode(_, l) -> Some l
        props
            [ "id", some (Json.String n.Id.Value)
              "kind", some (Json.String(nodeKindText n.Kind))
              "kindLabel", s kindLabel
              "label", some (Json.String n.Label)
              "description", s n.Description
              "variant", n.Variant |> Option.map (shapeText >> Json.String)
              "color", encodeColor n.Color
              "status", encodeStatus n.Status
              "ports", (if n.Ports.IsEmpty then None else Some(Json.Array(n.Ports |> List.map encodePort)))
              "references", encodeReferences n.References
              "metadata", encodeMeta n.Metadata
              "accessibility", encodeA11y n.Accessibility
              "interaction", encodeInteraction n.Interaction
              "layout", encodeBox n.Layout
              "extensions", encodeExtensions n.Extensions ]

    let private encodeEndpoint (e: Endpoint) =
        props [ "node", some (Json.String e.Node.Value); "port", e.Port |> Option.map (fun p -> Json.String p.Value) ]

    let private encodeEdge (e: Edge) =
        let kindLabel = e.Kind |> Option.bind (function CoreEdge(_, l) -> l | CustomEdge(_, l) -> Some l)
        props
            [ "id", some (Json.String e.Id.Value)
              "source", some (encodeEndpoint e.Source)
              "target", some (encodeEndpoint e.Target)
              "direction", e.Direction |> Option.map (directionText >> Json.String)
              "kind", e.Kind |> Option.map (edgeKindText >> Json.String)
              "kindLabel", s kindLabel
              "label", s e.Label
              "description", s e.Description
              "line", e.Line |> Option.map (lineText >> Json.String)
              "width", e.Width |> Option.map (float >> Json.number)
              "color", e.Stroke |> Option.map (fun c -> Json.Object [ "stroke", Json.String(colorValueText c) ])
              "status", encodeStatus e.Status
              "references", encodeReferences e.References
              "metadata", encodeMeta e.Metadata
              "accessibility", encodeA11y e.Accessibility
              "interaction", encodeInteraction e.Interaction
              "layout", encodeEdgeLayout e.Layout
              "extensions", encodeExtensions e.Extensions ]

    let private encodeGroup (g: Group) =
        props
            [ "id", some (Json.String g.Id.Value)
              "kind", some (Json.String(groupKindText g.Kind))
              "label", some (Json.String g.Label)
              "description", s g.Description
              "members", (if g.Members.IsEmpty then None else Some(Json.Array(g.Members |> List.map (fun m -> Json.String m.Value))))
              "parent", g.Parent |> Option.map (fun p -> Json.String p.Value)
              "line", g.Line |> Option.map (lineText >> Json.String)
              "color", encodeColor g.Color
              "status", encodeStatus g.Status
              "references", encodeReferences g.References
              "metadata", encodeMeta g.Metadata
              "accessibility", encodeA11y g.Accessibility
              "interaction", encodeInteraction g.Interaction
              "layout", encodeBox g.Layout
              "extensions", encodeExtensions g.Extensions ]

    /// Canonical encoding: fixed property order, empty optional collections omitted.
    let encode (w: Workflow) : Json =
        props
            [ "$schema", s w.Schema
              "format", some (Json.String Format.identifier)
              "formatVersion", some (Json.String(w.FormatVersion.ToString()))
              "id", some (Json.String w.Id.Value)
              "title", some (Json.String w.Title)
              "description", s w.Description
              "language", s w.Language
              "direction", w.TextDirection |> Option.map (reverse textDirections >> Json.String)
              "forma",
              some (
                  props
                      [ "version", some (Json.String(w.Forma.Version.ToString()))
                        "requires", (if w.Forma.Requires.IsEmpty then None else Some(Json.Array(w.Forma.Requires |> List.map Json.String))) ]
              )
              "metadata", encodeMeta w.Metadata
              "palette", (if w.Palette.IsEmpty then None else Some(Json.Object(w.Palette |> List.map (fun (k, v) -> k.Value, Json.String(colorSourceText v)))))
              "nodes", some (Json.Array(w.Nodes |> List.map encodeNode))
              "edges", (if w.Edges.IsEmpty then None else Some(Json.Array(w.Edges |> List.map encodeEdge)))
              "groups", (if w.Groups.IsEmpty then None else Some(Json.Array(w.Groups |> List.map encodeGroup)))
              "layout",
              whenAny
                  [ "direction", w.Layout.Direction |> Option.map (reverse flows >> Json.String)
                    "mode", w.Layout.Mode |> Option.map (reverse modes >> Json.String)
                    "canvas", w.Layout.Canvas |> Option.map (fun (cw, ch) -> Json.Object [ "width", Json.number cw; "height", Json.number ch ]) ]
              "presentation",
              whenAny
                  [ "density", w.Presentation.Density |> Option.map (reverse densities >> Json.String)
                    "legendTitle", s w.Presentation.LegendTitle
                    "legend",
                    (if w.Presentation.Legend.IsEmpty then None
                     else
                         Some(
                             Json.Array(
                                 w.Presentation.Legend
                                 |> List.map (fun l ->
                                     props
                                         [ "id", some (Json.String l.Id.Value); "label", some (Json.String l.Label); "description", s l.Description
                                           "color", encodeColor l.Color; "line", l.Line |> Option.map (lineText >> Json.String) ])
                             )
                         )) ]
              "extensions", encodeExtensions w.Extensions
              "provenance",
              w.Provenance
              |> Option.bind (fun p ->
                  whenAny
                      [ "producer", p.Producer |> Option.map (fun pr -> props [ "name", some (Json.String pr.Name); "version", s pr.Version; "uri", s pr.Uri ])
                        "createdAt", s p.CreatedAt
                        "modifiedAt", s p.ModifiedAt
                        "source", s p.Source
                        "modifiedBy", p.ModifiedBy |> Option.map (fun (n, v) -> props [ "name", some (Json.String n); "version", s v ]) ]) ]

    /// Canonical text of a workflow document.
    let serialize (w: Workflow) = Json.serialize (encode w)
