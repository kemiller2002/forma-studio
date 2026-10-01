namespace Forma.Workflow

open System

/// Decoding from schema-valid JSON into the typed model. Internal to Codec.
module internal CodecDecode =
    open CodecRead

    let objectColor where (json: Json option) : D<ObjectColor> =
        match json with
        | None -> Ok ObjectColor.none
        | Some j ->
            obj where j
            |> Result.bind (fun p ->
                let c name = optional (colorValue $"{where}.{name}") (get name p)
                match c "fill", c "stroke", c "accent", c "foreground" with
                | Ok f, Ok s, Ok a, Ok fg -> Ok { Fill = f; Stroke = s; Accent = a; Foreground = fg }
                | Error e, _, _, _ | _, Error e, _, _ | _, _, Error e, _ | _, _, _, Error e -> Error e)

    let metadata where (json: Json option) : D<Metadata option> = optional (obj where) json

    let extensions where (json: Json option) : D<Extensions> =
        match json with
        | None -> Ok []
        | Some j ->
            obj where j
            |> Result.bind (traverse (fun (k, v) ->
                match Namespace.tryCreate k, v with
                | Some n, Json.Object members -> Ok(n, members)
                | None, _ -> Error $"{where}: \"{k}\" is not a namespace"
                | _, _ -> Error $"{where}.{k}: an extension value is an object"))

    let accessibility where (json: Json option) : D<Accessibility> =
        match json with
        | None -> Ok Accessibility.none
        | Some j ->
            obj where j
            |> Result.bind (fun p ->
                match optStr where "name" p, optStr where "description" p with
                | Ok n, Ok d -> Ok { Name = n; Description = d }
                | Error e, _ | _, Error e -> Error e)

    let state where (p: (string * Json) list) : D<State> =
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

    let status where (json: Json option) : D<Status option> =
        json
        |> optional (fun j ->
            result {
                let! p = obj where j
                let! st = state where p
                let! detail = optStr where "detail" p
                let! updated = optStr where "updatedAt" p
                return { State = st; Detail = detail; UpdatedAt = updated }
            })

    let reference where (json: Json) : D<Reference> =
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

    let listOf (where: string) (name: string) (f: string -> Json -> D<'a>) (p: (string * Json) list) : D<'a list> =
        match get name p with
        | None -> Ok []
        | Some j -> arr where j |> Result.bind (List.mapi (fun i x -> i, x) >> traverse (fun (i, x) -> f $"{where}.{name}[{i}]" x))

    let references where (p: (string * Json) list) = listOf where "references" reference p

    let target where (json: Json) : D<InteractionTarget> =
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

    let interaction where (json: Json option) : D<Interaction option> =
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

    let point where (json: Json) : D<Point> =
        result {
            let! p = obj where json
            let! x = required where "x" p |> Result.bind (num $"{where}.x")
            let! y = required where "y" p |> Result.bind (num $"{where}.y")
            return { X = x; Y = y }
        }

    let boxLayout where (json: Json option) : D<BoxLayout> =
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

    let edgeLayout where (json: Json option) : D<EdgeLayout> =
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

    let port where (json: Json) : D<Port> =
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

    let nodeKind where (p: (string * Json) list) : D<NodeKind> =
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

    let edgeKind where (p: (string * Json) list) : D<EdgeKind option> =
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

    let node (where: string) (json: Json) : D<Node> =
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

    let endpoint where (json: Json) : D<Endpoint> =
        result {
            let! p = obj where json
            let! n = required where "node" p |> Result.bind (id' $"{where}.node")
            let! port = optional (localId $"{where}.port") (get "port" p)
            return { Node = n; Port = port }
        }

    let edge (where: string) (json: Json) : D<Edge> =
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

    let group (where: string) (json: Json) : D<Group> =
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

    let formaTarget (json: Json) : D<FormaTarget> =
        result {
            let! p = obj "forma" json
            let! v = required "forma" "version" p |> Result.bind (str "forma.version")
            let! version = SemanticVersion.tryParse v |> Option.map Ok |> Option.defaultValue (Error "forma.version: invalid version")
            let! requires = listOf "forma" "requires" str p
            return { Version = version; Requires = requires }
        }

    let workflowLayout (json: Json option) : D<WorkflowLayout> =
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

    let legendEntry (where: string) (json: Json) : D<LegendEntry> =
        result {
            let! p = obj where json
            let! lid = required where "id" p |> Result.bind (localId $"{where}.id")
            let! label = required where "label" p |> Result.bind (str $"{where}.label")
            let! desc = optStr where "description" p
            let! color = objectColor $"{where}.color" (get "color" p)
            let! line = optional (choose $"{where}.line" lines) (get "line" p)
            return { Id = lid; Label = label; Description = desc; Color = color; Line = line }
        }

    let presentation (json: Json option) : D<Presentation> =
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

    let provenance (json: Json option) : D<Provenance option> =
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

    let palette (json: Json option) : D<(SlotName * ColorSource) list> =
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
