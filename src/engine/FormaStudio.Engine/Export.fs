namespace FormaStudio.Engine

/// Scope-aware views shared by the agent export and the Folio projection.
/// Everything that leaves the editor passes through here, so disclosure policy is
/// enforced in one place (FDA-823, FDA-825, FDA-1260..1268).
[<RequireQualifiedAccess>]
module DisclosurePolicy =
    /// Fields whose values may appear in an output scope.
    let fields scope (project: Project) = project.Fields |> List.filter (MetadataRules.allows scope)

    /// Fields that must not appear anywhere in the given scope, including their
    /// names, keys and values.
    let withheld scope (project: Project) = project.Fields |> List.filter (MetadataRules.allows scope >> not)

    /// Text fragments that must never appear in an output for `scope`: names and
    /// stored values of withheld fields, plus stored values of undefined fields.
    let secrets scope (project: Project) =
        let hidden = withheld scope project |> List.map _.Key |> Set.ofList
        let definedKeys = project.Fields |> List.map _.Key |> Set.ofList
        let valuesOf (stored: StoredValue) =
            match stored with
            | Explicit v
            | SourceBound(v, _) -> MetadataRules.matchKeys v
            | UnavailableValue reason -> [ reason ]
            | UnknownValue -> []
        let values =
            ProjectOps.allObjects project
            |> List.collect (fun r ->
                ProjectOps.metadataOf r project
                |> Option.defaultValue Map.empty
                |> Map.toList
                |> List.filter (fun (k, _) -> hidden.Contains k || not (definedKeys.Contains k))
                |> List.collect (snd >> valuesOf))
        let names = withheld scope project |> List.map _.Name
        values @ names |> List.filter (fun s -> s.Length >= 3) |> List.distinct

/// A metadata value prepared for output: stable key, display label, rendered
/// text and value state.
type OutputValue =
    { Key: FieldKey
      Label: string
      Value: MetaValue option
      Text: string option
      State: string
      Note: string option }

[<RequireQualifiedAccess>]
module OutputValues =
    let private derivedNote (rule: string) =
        if rule.StartsWith "membership:lane" then "derived from lane"
        elif rule.StartsWith "membership:phase" then "derived from phase"
        elif rule.StartsWith "membership:group" then "derived from group"
        else "derived"

    let ofResolved (field: FieldDefinition) (resolved: ResolvedValue) =
        let text v = Some(MetadataRules.display (Some field) v)
        let make value textValue state note =
            { Key = field.Key; Label = field.Name; Value = value; Text = textValue; State = state; Note = note }
        match resolved with
        | ResolvedExplicit v -> make (Some v) (text v) "explicit" None
        | ResolvedDefault v -> make (Some v) (text v) "default" (Some "default")
        | ResolvedDerived(v, rule) -> make (Some v) (text v) "derived" (Some(derivedNote rule))
        | ResolvedSourceBound(v, _) -> make (Some v) (text v) "source-bound" (Some "from source")
        | ResolvedUnknown -> make None None "unknown" (Some "unknown")
        | ResolvedUnavailable _ -> make None None "unavailable" (Some "unavailable")
        | ResolvedInvalid _ -> make None None "invalid" (Some "invalid")
        | ResolvedConflict(authored, _, _) -> make (Some authored) (text authored) "invalid" (Some "conflicts with its derived value")
        | ResolvedMissing _ -> make None None "missing" (Some "not set")

    /// Values of every field that applies to the object and is permitted in `scope`.
    let forObject scope (project: Project) reference =
        DisclosurePolicy.fields scope project
        |> List.filter (fun f -> MetadataRules.applies f (ObjectRef.kind reference))
        |> List.map (fun f -> ofResolved f (ProjectOps.resolveField f reference project))

    let toJson (v: OutputValue) =
        Json.objOpt
            [ "field", Some(JString(Id.value v.Key))
              "label", Some(JString v.Label)
              "state", Some(JString v.State)
              "value", v.Value |> Option.map (Codec.encodeMetaValue >> JObject)
              "text", v.Text |> Option.map JString
              "note", v.Note |> Option.map JString ]

[<RequireQualifiedAccess>]
module EffectiveJson =
    /// Effective appearance with its source layer (FDA-1024, FDA-1067). A mapping
    /// layer names its source field only when that field may appear in `scope`.
    let encode scope (project: Project) (effective: EffectiveAppearance) =
        let layer l =
            let fieldVisible key = ProjectOps.tryField key project |> Option.exists (MetadataRules.allows scope)
            match l with
            | FormaDefaultLayer -> [ "layer", Some(JString "forma-default") ]
            | ProfileDefaultLayer(profile, kind) -> [ "layer", Some(JString "profile-default"); "profile", Some(JString profile); "kind", Some(JString kind) ]
            | StyleLayer(id, revision) -> [ "layer", Some(JString "named-style"); "style", Some(JString(Id.value id)); "styleRevision", Some(Json.ofInt revision) ]
            | MappingLayer(id, key, legend) ->
                [ "layer", Some(JString "metadata-mapping"); "mapping", Some(JString(Id.value id))
                  "field", (if fieldVisible key then Some(JString(Id.value key)) else None); "legend", Some(JString legend) ]
            | OverrideLayer -> [ "layer", Some(JString "object-override") ]
        let colorSource (c: ColorResolution) =
            match c.Source with
            | TokenColor t -> "token", JString(TokenRef.value t)
            | PaletteColor p -> "palette", JString(Id.value p)
            | LiteralColor h -> "literal", JString(HexColor.value h)
        let color (e: Effective<ColorResolution>) =
            match e.Value with
            | None -> Json.objOpt (layer e.Layer)
            | Some c ->
                let kind, source = colorSource c
                Json.objOpt (("css", c.Css |> Option.map JString) :: ("sourceKind", Some(JString kind)) :: ("source", Some source) :: layer e.Layer)
        let value (e: Effective<'T>) (toJson: 'T -> JsonValue) =
            Json.objOpt (("value", e.Value |> Option.map toJson) :: layer e.Layer)
        let shapeName s = match s with Rectangle -> "rectangle" | Rounded -> "rounded" | Pill -> "pill" | Ellipse -> "ellipse" | Diamond -> "diamond"
        let lineName l = match l with Solid -> "solid" | Dashed -> "dashed" | Dotted -> "dotted"
        JObject
            [ "fill", color effective.Fill
              "stroke", color effective.Stroke
              "accent", color effective.Accent
              "foreground", color effective.Foreground
              "connectorStroke", color effective.ConnectorStroke
              "connectorWidth", value effective.ConnectorWidth Json.ofInt
              "line", value effective.Line (lineName >> JString)
              "shape", value effective.Shape (shapeName >> JString) ]

/// The agent/developer export: machine-readable topology, semantics, metadata,
/// references and presentation without image interpretation (FDA-203, FDA-893,
/// FDA-1024, FDA-1039). Only fields permitted in the agent-export scope appear.
[<RequireQualifiedAccess>]
module AgentExport =
    let exportVersion = "1.0.0"
    let private scope = AgentExport

    let private metadata project reference =
        OutputValues.forObject scope project reference |> List.filter (fun v -> v.State <> "missing") |> List.map OutputValues.toJson |> JArray

    let private boxJson (b: Box) =
        JObject [ "x", Json.ofInt b.Position.X; "y", Json.ofInt b.Position.Y; "width", Json.ofInt b.Size.Width; "height", Json.ofInt b.Size.Height ]

    let private reference (r: TypedReference) =
        let kind, target =
            match r.Target with
            | RequirementTarget id -> "requirement", [ "requirement", JString id ]
            | PageTargetRef p -> "page", [ "pageId", JString(Id.value p) ]
            | DiagramTargetRef d -> "diagram", [ "diagramId", JString(Id.value d) ]
            | DiagramElementTargetRef(d, e) -> "diagram-element", [ "diagramId", JString(Id.value d); "elementId", JString e ]
            | RepositoryTarget repo -> "repository", [ "repository", JString repo ]
            | IssueTarget(repo, n) -> "issue", [ "repository", JString repo; "number", Json.ofInt n ]
            | ExternalUrlTarget url -> "url", [ "url", JString url ]
        let availability =
            match r.Availability with
            | Unverified -> JString "unverified"
            | Available -> JString "available"
            | Unavailable reason -> JObject [ "unavailable", JString reason ]
        Json.objOpt ([ "id", Some(JString(Id.value r.Id)); "kind", Some(JString kind) ] @ (target |> List.map (fun (k, v) -> k, Some v)) @ [ "label", r.Label |> Option.map JString; "availability", Some availability ])

    let private appearance (project: Project) objectRef =
        match ProjectOps.appearanceOf objectRef project with
        | None -> JNull
        | Some a ->
            let effective, _ = AppearanceResolution.resolve (OutputScope scope) project objectRef
            Json.objOpt
                [ "style", a.Style |> Option.map (Id.value >> JString)
                  "overrides", (if Appearance.isEmpty a.Overrides then None else Some(Codec.encodeAppearance a.Overrides))
                  "effective", Some(EffectiveJson.encode scope project effective) ]

    let private diagram (project: Project) (d: Diagram) =
        let profile = Profiles.tryFind d.Profile
        let lane nodeId = d.Groups |> List.tryFind (fun g -> g.Kind = Lane && List.contains nodeId g.Members) |> Option.map (fun g -> Id.value g.Id)
        let kindLabel kind = profile |> Option.bind (fun p -> Profiles.nodeKind p kind) |> Option.map _.Label |> Option.defaultValue kind
        JObject
            [ "diagramId", JString(Id.value d.Id)
              "name", JString d.Name
              "revision", JString(Codec.diagramRevision project d)
              "profile", JObject [ "id", JString d.Profile.Id; "version", JString d.Profile.Version; "available", JBool profile.IsSome ]
              "specificationOnly", JBool true
              "groups",
              d.Groups
              |> List.map (fun g ->
                  JObject
                      [ "groupId", JString(Id.value g.Id)
                        "kind", JString(match g.Kind with Group -> "group" | Lane -> "lane" | Phase -> "phase")
                        "label", JString g.Label
                        "semantic", JBool g.Semantic
                        "members", g.Members |> List.map (Id.value >> JString) |> JArray
                        "geometry", boxJson g.Box
                        "metadata", metadata project (GroupRef(d.Id, g.Id))
                        "appearance", appearance project (GroupRef(d.Id, g.Id)) ])
              |> JArray
              "nodes",
              d.Nodes
              |> List.map (fun n ->
                  Json.objOpt
                      [ "nodeId", Some(JString(Id.value n.Id))
                        "kind", Some(JString n.Kind)
                        "kindLabel", Some(JString(kindLabel n.Kind))
                        "label", Some(JString n.Label)
                        "lane", lane n.Id |> Option.map JString
                        "geometry", Some(boxJson n.Box)
                        "locked", Some(JBool n.Locked)
                        "metadata", Some(metadata project (NodeRef(d.Id, n.Id)))
                        "references", Some(n.References |> List.map reference |> JArray)
                        "appearance", Some(appearance project (NodeRef(d.Id, n.Id))) ])
              |> JArray
              "edges",
              d.Edges
              |> List.map (fun e ->
                  let directed = profile |> Option.bind (fun p -> Profiles.edgeKind p e.Kind) |> Option.map _.Directed |> Option.defaultValue true
                  Json.objOpt
                      [ "edgeId", Some(JString(Id.value e.Id))
                        "kind", Some(JString e.Kind)
                        "directed", Some(JBool directed)
                        "source", Some(JString(Id.value e.Source.Node))
                        "target", Some(JString(Id.value e.Target.Node))
                        "label", e.Label |> Option.map JString
                        "routing", Some(match e.Routing with Straight -> JString "straight" | Orthogonal -> JString "orthogonal" | Manual _ -> JString "manual")
                        "metadata", Some(metadata project (EdgeRef(d.Id, e.Id)))
                        "references", Some(e.References |> List.map reference |> JArray)
                        "appearance", Some(appearance project (EdgeRef(d.Id, e.Id))) ])
              |> JArray
              "metadata", metadata project (DiagramRef d.Id)
              "references", d.References |> List.map reference |> JArray ]

    let private fieldJson (f: FieldDefinition) =
        let codec = Codec.toJson { Samples.emptyProject "x" "x" with Fields = [ f ] }
        match Json.field "metadataFields" codec with
        | Some(JArray [ single ]) -> single
        | _ -> JNull

    /// Findings are included unless their text would reveal a withheld field.
    let private findings (project: Project) =
        let secrets = DisclosurePolicy.secrets scope project
        Validation.run project
        |> List.filter (fun f -> secrets |> List.forall (fun s -> not (f.Message.Contains s)))
        |> List.map (fun f ->
            JObject
                [ "code", JString f.Code
                  "severity", JString(match f.Severity with Blocker -> "blocker" | Warning -> "warning" | Advisory -> "advisory")
                  "category", JString(sprintf "%A" f.Category |> fun s -> s.ToLowerInvariant())
                  "target", JString f.Target
                  "message", JString f.Message ])
        |> JArray

    let export (project: Project) =
        let visibleFields = DisclosurePolicy.fields scope project |> List.map _.Key |> Set.ofList
        let pageTree (page: Page) =
            let rec node (n: ComponentNode) =
                JObject
                    [ "nodeId", JString(Id.value n.Id)
                      "componentId", JString n.Component
                      "properties", n.Properties |> Map.toList |> JObject
                      "content", n.Content |> Map.toList |> JObject
                      "slots", n.Slots |> Map.toList |> List.map (fun (slot, children) -> slot, children |> List.map node |> JArray) |> JObject
                      "metadata", metadata project (ComponentRef(page.Id, n.Id)) ]
            Json.objOpt
                [ "pageId", Some(JString(Id.value page.Id))
                  "name", Some(JString page.Name)
                  "route", page.Route |> Option.map JString
                  "tree", Some(page.Nodes |> List.map node |> JArray)
                  "metadata", Some(metadata project (PageRef page.Id)) ]
        let profilesUsed =
            project.Diagrams
            |> List.map _.Profile
            |> List.distinct
            |> List.map (fun r ->
                match Profiles.tryFind r with
                | Some p ->
                    JObject
                        [ "id", JString p.Id; "version", JString p.Version; "name", JString p.Name; "description", JString p.Description
                          "nodeKinds", p.NodeKinds |> List.map (fun k -> JObject [ "kind", JString k.Kind; "label", JString k.Label ]) |> JArray
                          "edgeKinds", p.EdgeKinds |> List.map (fun k -> JObject [ "kind", JString k.Kind; "label", JString k.Label; "directed", JBool k.Directed ]) |> JArray ]
                | None -> JObject [ "id", JString r.Id; "version", JString r.Version; "available", JBool false ])
        let exportedMappings =
            project.Mappings
            |> List.filter (fun m -> visibleFields.Contains m.Field)
            |> List.map (fun m -> Json.field "presentationMappings" (Codec.toJson { Samples.emptyProject "x" "x" with Mappings = [ m ] }) |> function Some(JArray [ one ]) -> one | _ -> JNull)
        JObject
            [ "exportVersion", JString exportVersion
              "kind", JString "forma-studio.agent-export"
              "scope", JString "agent-export"
              "project",
              Json.objOpt
                  [ "projectId", Some(JString(Id.value project.Id))
                    "name", Some(JString project.Name)
                    "schemaVersion", Some(Json.ofInt Codec.currentSchemaVersion)
                    "formaVersion", Some(JString project.FormaVersion)
                    "revision", Some(JString(Codec.revision project))
                    "startPageId", project.StartPage |> Option.map (Id.value >> JString) ]
              "formaContract", JObject [ "contract", JString "forma.diagram-presentation"; "contractVersion", JString "2.0.0" ]
              "profiles", JArray profilesUsed
              "pages", project.Pages |> List.map pageTree |> JArray
              "diagrams", project.Diagrams |> List.map (diagram project) |> JArray
              "metadataFields", DisclosurePolicy.fields scope project |> List.map fieldJson |> JArray
              "palette", (match Json.field "palette" (Codec.toJson project) with Some p -> p | None -> JArray [])
              "appearanceStyles", (match Json.field "appearanceStyles" (Codec.toJson project) with Some p -> p | None -> JArray [])
              "presentationMappings", JArray exportedMappings
              "precedence", [ "forma-default"; "profile-default"; "named-style"; "metadata-mapping"; "object-override" ] |> List.map JString |> JArray
              "validation", findings project
              "provenance", JObject [ "generator", JString "forma-studio-engine"; "exportScope", JString "agent-export" ] ]

    let serialize project = Json.serialize (export project)
