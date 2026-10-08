namespace FormaStudio.Engine

open System.Globalization

/// A small Result-based decoder toolkit. Errors carry the JSON path.
module Decode =
    type Decoder<'T> = JsonValue -> Result<'T, string>

    type ResultBuilder() =
        member _.Bind(r, f) = Result.bind f r
        member _.Return v = Ok v
        member _.ReturnFrom r = r

    let result = ResultBuilder()

    let private at path (r: Result<'T, string>) = r |> Result.mapError (fun e -> sprintf "%s: %s" path e)

    let str: Decoder<string> = function JString s -> Ok s | _ -> Error "expected a string"
    let bool: Decoder<bool> = function JBool b -> Ok b | _ -> Error "expected true or false"

    let int: Decoder<int> =
        function
        | JNumber n ->
            match System.Int32.TryParse(n, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture) with
            | true, v -> Ok v
            | _ -> Error "expected a whole number"
        | _ -> Error "expected a number"

    let number: Decoder<decimal> =
        function
        | JNumber n ->
            match System.Decimal.TryParse(n, NumberStyles.Float, CultureInfo.InvariantCulture) with
            | true, v -> Ok v
            | _ -> Error "expected a number"
        | _ -> Error "expected a number"

    let id<'K> : Decoder<Id<'K>> = fun json -> str json |> Result.bind Id.create<'K>

    let list (decoder: Decoder<'T>) : Decoder<'T list> =
        function
        | JArray items ->
            items
            |> List.mapi (fun i item -> decoder item |> at (sprintf "[%d]" i))
            |> List.fold (fun acc r -> match acc, r with Ok xs, Ok x -> Ok(x :: xs) | Error e, _ -> Error e | _, Error e -> Error e) (Ok [])
            |> Result.map List.rev
        | _ -> Error "expected an array"

    let field name (decoder: Decoder<'T>) (json: JsonValue) =
        match Json.field name json with
        | Some value -> decoder value |> at name
        | None -> Error(sprintf "missing '%s'" name)

    let optional name (decoder: Decoder<'T>) (json: JsonValue) =
        match Json.field name json with
        | None
        | Some JNull -> Ok None
        | Some value -> decoder value |> at name |> Result.map Some

    let withDefault name fallback (decoder: Decoder<'T>) json =
        optional name decoder json |> Result.map (Option.defaultValue fallback)

    let entries (decoder: Decoder<'T>) : Decoder<(string * 'T) list> =
        function
        | JObject members ->
            members
            |> List.map (fun (k, v) -> decoder v |> at k |> Result.map (fun d -> k, d))
            |> List.fold (fun acc r -> match acc, r with Ok xs, Ok x -> Ok(x :: xs) | Error e, _ -> Error e | _, Error e -> Error e) (Ok [])
            |> Result.map List.rev
        | _ -> Error "expected an object"

    let oneOf (name: string) (cases: (string * 'T) list) : Decoder<'T> =
        fun json ->
            str json
            |> Result.bind (fun s ->
                match cases |> List.tryFind (fst >> (=) s) with
                | Some(_, v) -> Ok v
                | None -> Error(sprintf "unknown %s '%s'" name s))

/// Deterministic persistence for schema v2 and migration from v1
/// (DOCUMENT-MODEL "Migration", FDA-200..202).
[<RequireQualifiedAccess>]
module Codec =
    open Decode

    /// The newest schema this build reads and writes. Version 3 adds optional icon
    /// names on components and diagram nodes; a document is written as version 3
    /// only when it holds an icon, so a version 2 Studio refuses it instead of
    /// silently dropping the icons (DOCUMENT-MODEL "Migration").
    let currentSchemaVersion = 3

    // -- shared vocabularies -------------------------------------------------

    let private targetKinds =
        [ "page", PageTarget; "component", ComponentTarget; "diagram", DiagramTarget; "node", NodeTarget; "edge", EdgeTarget; "group", GroupTarget ]

    let private scopes = [ "rendered", Rendered; "agent-export", AgentExport; "provenance-export", ProvenanceExport ]
    let private groupKinds = [ "group", Group; "lane", Lane; "phase", Phase ]
    let private shapes = [ "rectangle", Rectangle; "rounded", Rounded; "pill", Pill; "ellipse", Ellipse; "diamond", Diamond ]
    let private lines = [ "solid", Solid; "dashed", Dashed; "dotted", Dotted ]
    let private sides = [ "top", Top; "right", Right; "bottom", Bottom; "left", Left ]

    let private nameOf (table: (string * 'T) list) (value: 'T) = table |> List.find (snd >> (=) value) |> fst

    /// Sets serialize in the vocabulary's canonical order, not insertion order.
    let private encodeSet table (values: Set<'T>) =
        table |> List.filter (fun (_, v) -> values.Contains v) |> List.map (fst >> JString) |> JArray

    let private decodeSet name table = list (oneOf name table) >> Result.map Set.ofList

    let private omitEmpty name (value: JsonValue) =
        match value with
        | JArray []
        | JObject [] -> name, None
        | v -> name, Some v

    // -- metadata ------------------------------------------------------------

    let encodeMetaValue value =
        let typed kind v = [ "type", JString kind; "value", v ]
        match value with
        | Text s -> typed "text" (JString s)
        | Number n -> typed "number" (Json.ofDecimal n)
        | Boolean b -> typed "boolean" (JBool b)
        | Enum id -> typed "enum" (JString id)
        | DateTime s -> typed "datetime" (JString s)
        | Url s -> typed "url" (JString s)
        | TagList tags -> typed "tags" (tags |> List.map JString |> JArray)

    let decodeMetaValue json =
        result {
            let! kind = field "type" str json
            match kind with
            | "text" -> return! field "value" str json |> Result.map Text
            | "number" -> return! field "value" number json |> Result.map Number
            | "boolean" -> return! field "value" bool json |> Result.map Boolean
            | "enum" -> return! field "value" str json |> Result.map Enum
            | "datetime" -> return! field "value" str json |> Result.map DateTime
            | "url" -> return! field "value" str json |> Result.map Url
            | "tags" -> return! field "value" (list str) json |> Result.map TagList
            | other -> return! Error(sprintf "unknown metadata value type '%s'" other)
        }

    let encodeStored stored =
        match stored with
        | Explicit v -> JObject((("state", JString "explicit") :: encodeMetaValue v))
        | SourceBound(v, binding) ->
            Json.objOpt
                ([ "state", Some(JString "source-bound") ]
                 @ (encodeMetaValue v |> List.map (fun (k, x) -> k, Some x))
                 @ [ "source", Some(JString binding.Source); "retrievedAt", binding.RetrievedAt |> Option.map JString ])
        | UnknownValue -> JObject [ "state", JString "unknown" ]
        | UnavailableValue reason -> JObject [ "state", JString "unavailable"; "reason", JString reason ]

    let decodeStored json =
        result {
            let! state = field "state" str json
            match state with
            | "explicit" -> return! decodeMetaValue json |> Result.map Explicit
            | "source-bound" ->
                let! v = decodeMetaValue json
                let! source = field "source" str json
                let! retrieved = optional "retrievedAt" str json
                return SourceBound(v, { Source = source; RetrievedAt = retrieved })
            | "unknown" -> return UnknownValue
            | "unavailable" -> return! field "reason" str json |> Result.map UnavailableValue
            | other -> return! Error(sprintf "unknown metadata state '%s'" other)
        }

    /// Metadata serializes sorted by stable field key.
    let encodeMetadata (metadata: Metadata) =
        metadata |> Map.toList |> List.sortWith (fun (a, _) (b, _) -> System.String.CompareOrdinal(Id.value a, Id.value b)) |> List.map (fun (k, v) -> Id.value k, encodeStored v) |> JObject

    let decodeMetadata json : Result<Metadata, string> =
        entries decodeStored json
        |> Result.bind (fun pairs ->
            pairs
            |> List.fold (fun acc (k, v) -> acc |> Result.bind (fun m -> Id.create<FieldKind> k |> Result.map (fun key -> Map.add key v m))) (Ok Map.empty))

    let private encodeFieldType fieldType =
        match fieldType with
        | TextField maxLength -> Json.objOpt [ "kind", Some(JString "text"); "maxLength", maxLength |> Option.map Json.ofInt ]
        | NumberField(lo, hi) -> Json.objOpt [ "kind", Some(JString "number"); "minimum", lo |> Option.map Json.ofDecimal; "maximum", hi |> Option.map Json.ofDecimal ]
        | BooleanField -> JObject [ "kind", JString "boolean" ]
        | EnumField options ->
            JObject [ "kind", JString "enum"; "options", options |> List.map (fun o -> JObject [ "id", JString o.Id; "label", JString o.Label ]) |> JArray ]
        | DateTimeField -> JObject [ "kind", JString "datetime" ]
        | UrlField -> JObject [ "kind", JString "url" ]
        | TagsField ordered -> JObject [ "kind", JString "tags"; "ordered", JBool ordered ]

    let private decodeFieldType json =
        result {
            let! kind = field "kind" str json
            match kind with
            | "text" -> return! optional "maxLength" int json |> Result.map TextField
            | "number" ->
                let! lo = optional "minimum" number json
                let! hi = optional "maximum" number json
                return NumberField(lo, hi)
            | "boolean" -> return BooleanField
            | "enum" ->
                return!
                    field "options" (list (fun o -> result { let! i = field "id" str o in let! l = field "label" str o in return { Id = i; Label = l } })) json
                    |> Result.map EnumField
            | "datetime" -> return DateTimeField
            | "url" -> return UrlField
            | "tags" -> return! withDefault "ordered" false bool json |> Result.map TagsField
            | other -> return! Error(sprintf "unknown field type '%s'" other)
        }

    let private encodeField (f: FieldDefinition) =
        Json.objOpt
            [ "key", Some(JString(Id.value f.Key))
              "name", Some(JString f.Name)
              "help", f.Help |> Option.map JString
              "type", Some(encodeFieldType f.Type)
              "appliesTo", Some(encodeSet targetKinds f.AppliesTo)
              "disclosure", Some(JObject [ "scopes", encodeSet scopes f.Disclosure.Scopes; "derivedPresentation", JBool f.Disclosure.DerivedPresentation ])
              "default", f.Default |> Option.map (encodeMetaValue >> JObject)
              "required", Some(JBool f.Required)
              "derivation", f.Derivation |> Option.map (fun (FromMembership kind) -> JObject [ "kind", JString "membership"; "group", JString(nameOf groupKinds kind) ])
              "origin", Some(match f.Origin with ProjectLocal -> JString "project" | ProfileField p -> JObject [ "profile", JString p ]) ]

    let private decodeField json =
        result {
            let! key = field "key" id<FieldKind> json
            let! name = field "name" str json
            let! help = optional "help" str json
            let! fieldType = field "type" decodeFieldType json
            let! applies = field "appliesTo" (decodeSet "target kind" targetKinds) json
            let! disclosureScopes = field "disclosure" (field "scopes" (decodeSet "scope" scopes)) json
            let! derived = field "disclosure" (field "derivedPresentation" bool) json
            let! defaultValue = optional "default" decodeMetaValue json
            let! required = withDefault "required" false bool json
            let! derivation = optional "derivation" (fun d -> field "group" (oneOf "group kind" groupKinds) d |> Result.map FromMembership) json
            let! origin =
                match Json.field "origin" json with
                | None
                | Some(JString "project") -> Ok ProjectLocal
                | Some o -> field "profile" str o |> Result.map ProfileField
            return
                { Key = key; Name = name; Help = help; Type = fieldType; AppliesTo = applies
                  Disclosure = { Scopes = disclosureScopes; DerivedPresentation = derived }
                  Default = defaultValue; Required = required; Derivation = derivation; Origin = origin }
        }

    // -- appearance ----------------------------------------------------------

    let private encodeColor color =
        match color with
        | TokenColor t -> JObject [ "token", JString(TokenRef.value t) ]
        | PaletteColor p -> JObject [ "palette", JString(Id.value p) ]
        | LiteralColor h -> JObject [ "literal", JString(HexColor.value h) ]

    let private decodeColor json =
        match Json.field "token" json, Json.field "palette" json, Json.field "literal" json with
        | Some(JString t), None, None -> TokenRef.parse t |> Result.map TokenColor
        | None, Some(JString p), None -> Id.create<PaletteKind> p |> Result.map PaletteColor
        | None, None, Some(JString l) -> HexColor.parse l |> Result.map LiteralColor
        | _ -> Error "a color is exactly one of token, palette or literal"

    let encodeAppearance (a: Appearance) =
        Json.objOpt
            [ "fill", a.Fill |> Option.map encodeColor
              "stroke", a.Stroke |> Option.map encodeColor
              "accent", a.Accent |> Option.map encodeColor
              "foreground", a.Foreground |> Option.map encodeColor
              "connectorStroke", a.ConnectorStroke |> Option.map encodeColor
              "connectorWidth", a.ConnectorWidth |> Option.map Json.ofInt
              "line", a.Line |> Option.map (nameOf lines >> JString)
              "shape", a.Shape |> Option.map (nameOf shapes >> JString) ]

    let private decodeAppearance json =
        result {
            let! fill = optional "fill" decodeColor json
            let! stroke = optional "stroke" decodeColor json
            let! accent = optional "accent" decodeColor json
            let! foreground = optional "foreground" decodeColor json
            let! connectorStroke = optional "connectorStroke" decodeColor json
            let! width = optional "connectorWidth" int json
            let! line = optional "line" (oneOf "line style" lines) json
            let! shape = optional "shape" (oneOf "shape" shapes) json
            return ({ Fill = fill; Stroke = stroke; Accent = accent; Foreground = foreground; ConnectorStroke = connectorStroke; ConnectorWidth = width; Line = line; Shape = shape }: Appearance)
        }

    let private encodeObjectAppearance (a: ObjectAppearance) =
        match a.Style, Appearance.isEmpty a.Overrides with
        | None, true -> None
        | style, empty ->
            Some(Json.objOpt [ "style", style |> Option.map (Id.value >> JString); "overrides", (if empty then None else Some(encodeAppearance a.Overrides)) ])

    let private decodeObjectAppearance json =
        result {
            let! style = optional "style" id<StyleKind> json
            let! overrides = withDefault "overrides" Appearance.empty decodeAppearance json
            return { Style = style; Overrides = overrides }
        }

    let private encodeOutcome outcome =
        match outcome with
        | UseStyle s -> JObject [ "style", JString(Id.value s) ]
        | UseAppearance a -> JObject [ "appearance", encodeAppearance a ]

    let private decodeOutcome json =
        match Json.field "style" json, Json.field "appearance" json with
        | Some _, None -> field "style" id<StyleKind> json |> Result.map UseStyle
        | None, Some _ -> field "appearance" decodeAppearance json |> Result.map UseAppearance
        | _ -> Error "an outcome is exactly one of style or appearance"

    let private encodeFallback fallback =
        match fallback with
        | NoMapping -> JString "none"
        | Apply(outcome, legend) -> JObject [ "outcome", encodeOutcome outcome; "legend", JString legend ]

    let private decodeFallback json =
        match json with
        | JString "none" -> Ok NoMapping
        | _ -> result { let! o = field "outcome" decodeOutcome json in let! l = field "legend" str json in return Apply(o, l) }

    let private encodeMapping (m: PresentationMapping) =
        JObject
            [ "id", JString(Id.value m.Id)
              "name", JString m.Name
              "field", JString(Id.value m.Field)
              "targets", encodeSet targetKinds m.Targets
              "rules",
              m.Rules
              |> List.map (fun r ->
                  JObject
                      [ "match", (match r.Match with Equals v -> JObject [ "equals", JString v ] | AnyOf vs -> JObject [ "anyOf", vs |> List.map JString |> JArray ])
                        "outcome", encodeOutcome r.Outcome
                        "legend", JString r.Legend ])
              |> JArray
              "fallbacks",
              JObject
                  [ "missing", encodeFallback m.Fallbacks.Missing
                    "unknown", encodeFallback m.Fallbacks.Unknown
                    "unavailable", encodeFallback m.Fallbacks.Unavailable
                    "invalid", encodeFallback m.Fallbacks.Invalid
                    "unmapped", encodeFallback m.Fallbacks.Unmapped ]
              "enabled", JBool m.Enabled ]

    let private decodeMapping json =
        let decodeMatch m =
            match Json.field "equals" m, Json.field "anyOf" m with
            | Some _, None -> field "equals" str m |> Result.map Equals
            | None, Some _ -> field "anyOf" (list str) m |> Result.map AnyOf
            | _ -> Error "a match is exactly one of equals or anyOf"
        let decodeRule r =
            result {
                let! m = field "match" decodeMatch r
                let! o = field "outcome" decodeOutcome r
                let! l = field "legend" str r
                return { Match = m; Outcome = o; Legend = l }
            }
        result {
            let! mid = field "id" id<MappingKind> json
            let! name = field "name" str json
            let! f = field "field" id<FieldKind> json
            let! targets = field "targets" (decodeSet "target kind" targetKinds) json
            let! rules = field "rules" (list decodeRule) json
            let! fb = field "fallbacks" Ok json
            let! missing = field "missing" decodeFallback fb
            let! unknown = field "unknown" decodeFallback fb
            let! unavailable = field "unavailable" decodeFallback fb
            let! invalid = field "invalid" decodeFallback fb
            let! unmapped = field "unmapped" decodeFallback fb
            let! enabled = withDefault "enabled" true bool json
            return
                { Id = mid; Name = name; Field = f; Targets = targets; Rules = rules; Enabled = enabled
                  Fallbacks = { Missing = missing; Unknown = unknown; Unavailable = unavailable; Invalid = invalid; Unmapped = unmapped } }
        }

    // -- references ----------------------------------------------------------

    let private encodeReference (r: TypedReference) =
        let target =
            match r.Target with
            | RequirementTarget id -> JObject [ "kind", JString "requirement"; "id", JString id ]
            | PageTargetRef p -> JObject [ "kind", JString "page"; "pageId", JString(Id.value p) ]
            | DiagramTargetRef d -> JObject [ "kind", JString "diagram"; "diagramId", JString(Id.value d) ]
            | DiagramElementTargetRef(d, e) -> JObject [ "kind", JString "diagram-element"; "diagramId", JString(Id.value d); "elementId", JString e ]
            | RepositoryTarget repo -> JObject [ "kind", JString "repository"; "repository", JString repo ]
            | IssueTarget(repo, n) -> JObject [ "kind", JString "issue"; "repository", JString repo; "number", Json.ofInt n ]
            | ExternalUrlTarget url -> JObject [ "kind", JString "url"; "url", JString url ]
        Json.objOpt
            [ "id", Some(JString(Id.value r.Id))
              "target", Some target
              "label", r.Label |> Option.map JString
              "availability",
              Some(
                  match r.Availability with
                  | Unverified -> JString "unverified"
                  | Available -> JString "available"
                  | Unavailable reason -> JObject [ "unavailable", JString reason ]
              ) ]

    let private decodeReference json =
        let decodeTarget t =
            result {
                let! kind = field "kind" str t
                match kind with
                | "requirement" -> return! field "id" str t |> Result.map RequirementTarget
                | "page" -> return! field "pageId" id<PageKind> t |> Result.map PageTargetRef
                | "diagram" -> return! field "diagramId" id<DiagramKind> t |> Result.map DiagramTargetRef
                | "diagram-element" ->
                    let! d = field "diagramId" id<DiagramKind> t
                    let! e = field "elementId" str t
                    return DiagramElementTargetRef(d, e)
                | "repository" -> return! field "repository" str t |> Result.map RepositoryTarget
                | "issue" ->
                    let! repo = field "repository" str t
                    let! n = field "number" int t
                    return IssueTarget(repo, n)
                | "url" -> return! field "url" str t |> Result.map ExternalUrlTarget
                | other -> return! Error(sprintf "unknown reference kind '%s'" other)
            }
        result {
            let! rid = field "id" id<ReferenceKind> json
            let! target = field "target" decodeTarget json
            let! label = optional "label" str json
            let! availability =
                match Json.field "availability" json with
                | None
                | Some(JString "unverified") -> Ok Unverified
                | Some(JString "available") -> Ok Available
                | Some a -> field "unavailable" str a |> Result.map Unavailable
            return { Id = rid; Target = target; Label = label; Availability = availability }
        }

    // -- layout --------------------------------------------------------------

    let private encodeAnnotation (a: Annotation) =
        Json.objOpt [ "annotationId", Some(JString a.Id); "text", Some(JString a.Text); "kind", a.Kind |> Option.map JString; "reference", a.Reference |> Option.map JString ]

    let private decodeAnnotation json =
        result {
            let! aid = field "annotationId" str json
            let! text = field "text" str json
            let! kind = optional "kind" str json
            let! reference = optional "reference" str json
            return { Id = aid; Text = text; Kind = kind; Reference = reference }
        }

    let private encodeMap (m: Map<string, JsonValue>) = m |> Map.toList |> List.sortWith (fun (a, _) (b, _) -> System.String.CompareOrdinal(a, b)) |> JObject

    let rec private encodeComponent (n: ComponentNode) =
        Json.objOpt
            [ "nodeId", Some(JString(Id.value n.Id))
              "componentId", Some(JString n.Component)
              omitEmpty "properties" (encodeMap n.Properties)
              omitEmpty "tokenBindings" (n.TokenBindings |> Map.map (fun _ v -> JString v) |> encodeMap)
              omitEmpty "content" (encodeMap n.Content)
              omitEmpty "slots" (n.Slots |> Map.map (fun _ children -> children |> List.map encodeComponent |> JArray) |> encodeMap)
              omitEmpty "navigation" (JArray n.Navigation)
              omitEmpty "annotations" (n.Annotations |> List.map encodeAnnotation |> JArray)
              omitEmpty "metadata" (encodeMetadata n.Metadata)
              "icon", n.Icon |> Option.map IconRef.toJson ]

    /// Any stored icon value is kept: a well-formed name as a name, anything else verbatim.
    let private decodeIcon json = optional "icon" (IconRef.ofJson >> Ok) json

    let private mapOf (json: JsonValue) = match json with JObject members -> Ok(Map.ofList members) | _ -> Error "expected an object"

    let rec private decodeComponent json =
        result {
            let! nid = field "nodeId" id<ComponentKind> json
            let! componentId = field "componentId" str json
            let! properties = withDefault "properties" Map.empty mapOf json
            let! tokens = withDefault "tokenBindings" [] (entries str) json
            let! content = withDefault "content" Map.empty mapOf json
            let! slots = withDefault "slots" [] (entries (list decodeComponent)) json
            let! navigation = withDefault "navigation" [] (list Ok) json
            let! annotations = withDefault "annotations" [] (list decodeAnnotation) json
            let! metadata = withDefault "metadata" Map.empty decodeMetadata json
            let! icon = decodeIcon json
            return
                { Id = nid; Component = componentId; Properties = properties; TokenBindings = Map.ofList tokens; Content = content
                  Slots = Map.ofList slots; Navigation = navigation; Annotations = annotations; Metadata = metadata; Icon = icon }
        }

    let private encodePage (p: Page) =
        Json.objOpt
            [ "pageId", Some(JString(Id.value p.Id))
              "name", Some(JString p.Name)
              "route", p.Route |> Option.map JString
              "title", p.Title |> Option.map JString
              "description", p.Description |> Option.map JString
              "nodes", Some(p.Nodes |> List.map encodeComponent |> JArray)
              omitEmpty "annotations" (p.Annotations |> List.map encodeAnnotation |> JArray)
              omitEmpty "metadata" (encodeMetadata p.Metadata) ]

    let private decodePage json =
        result {
            let! pid = field "pageId" id<PageKind> json
            let! name = field "name" str json
            let! route = optional "route" str json
            let! title = optional "title" str json
            let! description = optional "description" str json
            let! nodes = field "nodes" (list decodeComponent) json
            let! annotations = withDefault "annotations" [] (list decodeAnnotation) json
            let! metadata = withDefault "metadata" Map.empty decodeMetadata json
            return { Id = pid; Name = name; Route = route; Title = title; Description = description; Nodes = nodes; Annotations = annotations; Metadata = metadata }
        }

    // -- flow ----------------------------------------------------------------

    let private boxMembers (b: Box) =
        [ "x", Some(Json.ofInt b.Position.X); "y", Some(Json.ofInt b.Position.Y); "width", Some(Json.ofInt b.Size.Width); "height", Some(Json.ofInt b.Size.Height) ]

    let private decodeBox json =
        result {
            let! x = field "x" number json
            let! y = field "y" number json
            let! w = field "width" number json
            let! h = field "height" number json
            return! Geometry.box (float x) (float y) (float w) (float h) |> Result.mapError Geometry.describe
        }

    let private encodeEndpoint (e: Endpoint) =
        Json.objOpt [ "nodeId", Some(JString(Id.value e.Node)); "portId", e.Port |> Option.map (Id.value >> JString) ]

    let private decodeEndpoint json =
        result {
            let! n = field "nodeId" id<NodeKind> json
            let! p = optional "portId" id<PortKind> json
            return { Node = n; Port = p }
        }

    let private encodeNode (n: DiagramNode) =
        Json.objOpt
            ([ "nodeId", Some(JString(Id.value n.Id)); "kind", Some(JString n.Kind); "label", Some(JString n.Label) ]
             @ boxMembers n.Box
             @ [ omitEmpty "ports" (n.Ports |> List.map (fun p -> Json.objOpt [ "portId", Some(JString(Id.value p.Id)); "side", Some(JString(nameOf sides p.Side)); "label", p.Label |> Option.map JString ]) |> JArray)
                 "locked", (if n.Locked then Some(JBool true) else None)
                 omitEmpty "metadata" (encodeMetadata n.Metadata)
                 "appearance", encodeObjectAppearance n.Appearance
                 omitEmpty "references" (n.References |> List.map encodeReference |> JArray)
                 "icon", n.Icon |> Option.map IconRef.toJson ])

    let private decodeNode json =
        let decodePort p =
            result {
                let! pid = field "portId" id<PortKind> p
                let! side = field "side" (oneOf "port side" sides) p
                let! label = optional "label" str p
                return { Id = pid; Side = side; Label = label }
            }
        result {
            let! nid = field "nodeId" id<NodeKind> json
            let! kind = field "kind" str json
            let! label = field "label" str json
            let! b = decodeBox json
            let! ports = withDefault "ports" [] (list decodePort) json
            let! locked = withDefault "locked" false bool json
            let! metadata = withDefault "metadata" Map.empty decodeMetadata json
            let! appearance = withDefault "appearance" Appearance.none decodeObjectAppearance json
            let! references = withDefault "references" [] (list decodeReference) json
            let! icon = decodeIcon json
            return { Id = nid; Kind = kind; Label = label; Box = b; Ports = ports; Locked = locked; Metadata = metadata; Appearance = appearance; References = references; Icon = icon }
        }

    let private encodeRouting routing =
        match routing with
        | Straight -> JString "straight"
        | Orthogonal -> JString "orthogonal"
        | Manual points -> JObject [ "manual", points |> List.map (fun p -> JObject [ "x", Json.ofInt p.X; "y", Json.ofInt p.Y ]) |> JArray ]

    let private decodeRouting json =
        match json with
        | JString "straight" -> Ok Straight
        | JString "orthogonal" -> Ok Orthogonal
        | _ ->
            field "manual" (list (fun p -> result {
                let! x = field "x" number p
                let! y = field "y" number p
                return! Geometry.point (float x) (float y) |> Result.mapError Geometry.describe })) json
            |> Result.map Manual

    let private encodeEdge (e: DiagramEdge) =
        Json.objOpt
            [ "edgeId", Some(JString(Id.value e.Id))
              "kind", Some(JString e.Kind)
              "source", Some(encodeEndpoint e.Source)
              "target", Some(encodeEndpoint e.Target)
              "label", e.Label |> Option.map JString
              "routing", Some(encodeRouting e.Routing)
              omitEmpty "metadata" (encodeMetadata e.Metadata)
              "appearance", encodeObjectAppearance e.Appearance
              omitEmpty "references" (e.References |> List.map encodeReference |> JArray) ]

    let private decodeEdge json =
        result {
            let! eid = field "edgeId" id<EdgeKind> json
            let! kind = field "kind" str json
            let! source = field "source" decodeEndpoint json
            let! target = field "target" decodeEndpoint json
            let! label = optional "label" str json
            let! routing = withDefault "routing" Orthogonal decodeRouting json
            let! metadata = withDefault "metadata" Map.empty decodeMetadata json
            let! appearance = withDefault "appearance" Appearance.none decodeObjectAppearance json
            let! references = withDefault "references" [] (list decodeReference) json
            return { Id = eid; Kind = kind; Source = source; Target = target; Label = label; Routing = routing; Metadata = metadata; Appearance = appearance; References = references }
        }

    let private encodeGroup (g: DiagramGroup) =
        Json.objOpt
            ([ "groupId", Some(JString(Id.value g.Id)); "kind", Some(JString(nameOf groupKinds g.Kind)); "label", Some(JString g.Label) ]
             @ boxMembers g.Box
             @ [ "members", Some(g.Members |> List.map (Id.value >> JString) |> JArray)
                 "semantic", Some(JBool g.Semantic)
                 omitEmpty "metadata" (encodeMetadata g.Metadata)
                 "appearance", encodeObjectAppearance g.Appearance ])

    let private decodeGroup json =
        result {
            let! gid = field "groupId" id<GroupKind> json
            let! kind = field "kind" (oneOf "group kind" groupKinds) json
            let! label = field "label" str json
            let! b = decodeBox json
            let! members = withDefault "members" [] (list id<NodeKind>) json
            let! semantic = withDefault "semantic" false bool json
            let! metadata = withDefault "metadata" Map.empty decodeMetadata json
            let! appearance = withDefault "appearance" Appearance.none decodeObjectAppearance json
            return { Id = gid; Kind = kind; Label = label; Box = b; Members = members; Semantic = semantic; Metadata = metadata; Appearance = appearance }
        }

    let encodeDiagram (d: Diagram) =
        Json.objOpt
            [ "diagramId", Some(JString(Id.value d.Id))
              "name", Some(JString d.Name)
              "profile", Some(JObject [ "id", JString d.Profile.Id; "version", JString d.Profile.Version ])
              "groups", Some(d.Groups |> List.map encodeGroup |> JArray)
              "nodes", Some(d.Nodes |> List.map encodeNode |> JArray)
              "edges", Some(d.Edges |> List.map encodeEdge |> JArray)
              "display",
              Some(
                  JObject
                      [ "nodeFields", d.Display.NodeFields |> List.map (Id.value >> JString) |> JArray
                        "kindFields", d.Display.KindFields |> Map.toList |> List.map (fun (kind, keys) -> kind, keys |> List.map (Id.value >> JString) |> JArray) |> JObject
                        "edgeFields", d.Display.EdgeFields |> List.map (Id.value >> JString) |> JArray
                        "missing", JString(match d.Display.Missing with OmitMissing -> "omit" | ShowValueState -> "show-state") ]
              )
              omitEmpty "metadata" (encodeMetadata d.Metadata)
              omitEmpty "references" (d.References |> List.map encodeReference |> JArray) ]

    let private decodeDiagram json =
        result {
            let! did = field "diagramId" id<DiagramKind> json
            let! name = field "name" str json
            let! profileId = field "profile" (field "id" str) json
            let! profileVersion = field "profile" (field "version" str) json
            let! groups = withDefault "groups" [] (list decodeGroup) json
            let! nodes = withDefault "nodes" [] (list decodeNode) json
            let! edges = withDefault "edges" [] (list decodeEdge) json
            let! nodeFields = withDefault "display" [] (fun d -> withDefault "nodeFields" [] (list id<FieldKind>) d) json
            let! edgeFields = withDefault "display" [] (fun d -> withDefault "edgeFields" [] (list id<FieldKind>) d) json
            let! kindFields = withDefault "display" [] (fun d -> withDefault "kindFields" [] (entries (list id<FieldKind>)) d) json
            let! missing = withDefault "display" OmitMissing (fun d -> withDefault "missing" OmitMissing (oneOf "missing display" [ "omit", OmitMissing; "show-state", ShowValueState ]) d) json
            let! metadata = withDefault "metadata" Map.empty decodeMetadata json
            let! references = withDefault "references" [] (list decodeReference) json
            return
                { Id = did; Name = name; Profile = { Id = profileId; Version = profileVersion }; Nodes = nodes; Edges = edges; Groups = groups
                  Display = { NodeFields = nodeFields; KindFields = Map.ofList kindFields; EdgeFields = edgeFields; Missing = missing }; Metadata = metadata; References = references }
        }

    // -- project -------------------------------------------------------------

    let private encodePalette (p: PaletteSlot) =
        Json.objOpt
            [ "id", Some(JString(Id.value p.Id))
              "name", Some(JString p.Name)
              "value", Some(match p.Value with PaletteToken t -> JObject [ "token", JString(TokenRef.value t) ] | PaletteLiteral h -> JObject [ "literal", JString(HexColor.value h) ])
              "description", p.Description |> Option.map JString ]

    let private decodePalette json =
        result {
            let! pid = field "id" id<PaletteKind> json
            let! name = field "name" str json
            let! value =
                field "value" (fun v ->
                    match decodeColor v with
                    | Ok(TokenColor t) -> Ok(PaletteToken t)
                    | Ok(LiteralColor h) -> Ok(PaletteLiteral h)
                    | Ok(PaletteColor _) -> Error "a palette slot cannot reference another palette slot"
                    | Error e -> Error e) json
            let! description = optional "description" str json
            return { Id = pid; Name = name; Value = value; Description = description }
        }

    let private encodeStyle (s: AppearanceStyle) =
        JObject
            [ "id", JString(Id.value s.Id)
              "name", JString s.Name
              "revision", Json.ofInt s.Revision
              "targets", encodeSet targetKinds s.Targets
              "appearance", encodeAppearance s.Appearance ]

    let private decodeStyle json =
        result {
            let! sid = field "id" id<StyleKind> json
            let! name = field "name" str json
            let! revision = withDefault "revision" 1 int json
            let! targets = field "targets" (decodeSet "target kind" targetKinds) json
            let! appearance = field "appearance" decodeAppearance json
            return { Id = sid; Name = name; Revision = revision; Targets = targets; Appearance = appearance }
        }

    let private hasIcon (p: Project) =
        let rec inComponents (nodes: ComponentNode list) = nodes |> List.exists (fun n -> n.Icon.IsSome || n.Slots |> Map.exists (fun _ c -> inComponents c))
        p.Pages |> List.exists (fun page -> inComponents page.Nodes) || p.Diagrams |> List.exists (fun d -> d.Nodes |> List.exists (fun n -> n.Icon.IsSome))

    /// The schema version a project is written as: the oldest that holds all of it.
    let schemaVersionOf (p: Project) = if hasIcon p then currentSchemaVersion else 2

    let toJson (p: Project) =
        Json.objOpt
            [ "schemaVersion", Some(Json.ofInt (schemaVersionOf p))
              "projectId", Some(JString(Id.value p.Id))
              "name", Some(JString p.Name)
              "description", p.Description |> Option.map JString
              "formaVersion", Some(JString p.FormaVersion)
              "startPageId", p.StartPage |> Option.map (Id.value >> JString)
              "pages", Some(p.Pages |> List.map encodePage |> JArray)
              "diagrams", Some(p.Diagrams |> List.map encodeDiagram |> JArray)
              "metadataFields", Some(p.Fields |> List.map encodeField |> JArray)
              "palette", Some(p.Palette |> List.map encodePalette |> JArray)
              "appearanceStyles", Some(p.Styles |> List.map encodeStyle |> JArray)
              "presentationMappings", Some(p.Mappings |> List.map encodeMapping |> JArray)
              "scenarios", Some(JArray p.Scenarios)
              "assets", Some(JArray p.Assets)
              omitEmpty "metadata" (encodeMetadata p.Metadata)
              "legacyMetadata", p.LegacyMetadata ]

    let serialize project = Json.serialize (toJson project)

    let private decodeCommon (schemaVersion: int) json =
        result {
            let! pid = field "projectId" id<ProjectKind> json
            let! name = field "name" str json
            let! description = optional "description" str json
            let! forma = field "formaVersion" str json
            let! start = optional "startPageId" id<PageKind> json
            let! pages = withDefault "pages" [] (list decodePage) json
            let! scenarios = withDefault "scenarios" [] (list Ok) json
            let! assets = withDefault "assets" [] (list Ok) json
            let baseProject =
                { Id = pid; Name = name; Description = description; FormaVersion = forma; StartPage = start; Pages = pages; Diagrams = []
                  Fields = []; Palette = []; Styles = []; Mappings = []; Scenarios = scenarios; Assets = assets; Metadata = Map.empty; LegacyMetadata = None }
            if schemaVersion = 1 then
                // v1 `metadata` was free-form JSON. It is preserved verbatim as legacy
                // data, never rendered or exported, and never turned into typed fields.
                return { baseProject with LegacyMetadata = Json.field "metadata" json |> Option.filter ((<>) (JObject [])) }
            else
                let! diagrams = withDefault "diagrams" [] (list decodeDiagram) json
                let! fields = withDefault "metadataFields" [] (list decodeField) json
                let! palette = withDefault "palette" [] (list decodePalette) json
                let! styles = withDefault "appearanceStyles" [] (list decodeStyle) json
                let! mappings = withDefault "presentationMappings" [] (list decodeMapping) json
                let! metadata = withDefault "metadata" Map.empty decodeMetadata json
                return
                    { baseProject with
                        Diagrams = diagrams; Fields = fields; Palette = palette; Styles = styles; Mappings = mappings
                        Metadata = metadata; LegacyMetadata = Json.field "legacyMetadata" json }
        }

    type LoadError =
        | InvalidJson of string
        | NewerSchema of found: int * supported: int
        | UnsupportedSchema of string
        | InvalidDocument of string

    let describeLoadError error =
        match error with
        | InvalidJson e -> sprintf "The file is not valid JSON (%s)." e
        | NewerSchema(found, supported) ->
            sprintf "This project uses schema version %d, but this Studio build reads versions up to %d. Open it in a newer Studio; nothing was changed or dropped." found supported
        | UnsupportedSchema e -> sprintf "Unsupported schema version: %s." e
        | InvalidDocument e -> sprintf "The project document is invalid: %s." e

    /// Loads v1 or v2. v1 is migrated in memory (page-only projects gain no diagrams);
    /// newer versions fail safely instead of silently dropping unknown content.
    let load (text: string) : Result<Project, LoadError> =
        match Json.parse text with
        | Error e -> Error(InvalidJson e)
        | Ok json ->
            match Json.field "schemaVersion" json with
            | Some(JNumber "1") -> decodeCommon 1 json |> Result.mapError InvalidDocument
            | Some(JNumber "2") -> decodeCommon 2 json |> Result.mapError InvalidDocument
            | Some(JNumber "3") -> decodeCommon 3 json |> Result.mapError InvalidDocument
            | Some(JNumber n) ->
                match System.Int32.TryParse n with
                | true, v when v > currentSchemaVersion -> Error(NewerSchema(v, currentSchemaVersion))
                | _ -> Error(UnsupportedSchema n)
            | _ -> Error(UnsupportedSchema "missing schemaVersion")

    let private sha256 (text: string) =
        let bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes text)
        "sha256:" + System.Convert.ToHexString(bytes).ToLowerInvariant()

    /// Content revision of the whole project: a digest of its canonical form.
    /// Undo, redo and reload of identical content yield the identical revision.
    let revision project = sha256 (serialize project)

    /// Revision of one diagram plus the project definitions that shape its output
    /// (fields, palette, styles, mappings). Used to reject stale projections.
    let diagramRevision (project: Project) (diagram: Diagram) =
        JObject
            [ "diagram", encodeDiagram diagram
              "fields", project.Fields |> List.map encodeField |> JArray
              "palette", project.Palette |> List.map encodePalette |> JArray
              "styles", project.Styles |> List.map encodeStyle |> JArray
              "mappings", project.Mappings |> List.map encodeMapping |> JArray
              "formaVersion", JString project.FormaVersion ]
        |> Json.serialize
        |> sha256
