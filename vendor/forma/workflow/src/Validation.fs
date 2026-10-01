namespace Forma.Workflow

open System


[<RequireQualifiedAccess>]
type Severity =
    | Error
    | Warning
    | Info

[<RequireQualifiedAccess>]
type FindingCategory =
    | Structure
    | Identity
    | Reference
    | Ports
    | Membership
    | ExtensionData
    | Compatibility
    | Accessibility
    | Security
    | Semantics

/// One validation result, located by JSON Pointer and, where it concerns an
/// object, by that object's stable id.
type Finding =
    { Code: string
      Severity: Severity
      Category: FindingCategory
      Pointer: string
      Subject: string option
      Message: string }

/// The six compatibility classes required by PORTABLE-WORKFLOW-INTERCHANGE.
type CompatibilityClass =
    /// Valid and every construct is understood.
    | FullySupported
    /// Valid; carries namespaced extensions that are preserved but not interpreted.
    | SupportedWithPreservedExtensions of Namespace list
    /// Declares another format version. `Automatic` means the reader migrated it.
    | MigrationRequired of from: SemanticVersion * target: SemanticVersion * automatic: bool * reason: string
    /// Not well-formed JSON, wrong format, or violates the published schema.
    | StructurallyInvalid
    /// Schema-valid, but references, identity, ports or membership are broken.
    | SemanticallyInvalid
    /// Valid format that needs Forma capabilities the target release lacks.
    | UnavailableCapabilities of string list

type ValidationReport =
    { Class: CompatibilityClass
      Findings: Finding list
      /// The decoded workflow whenever the document is structurally valid, so an
      /// editor can open a semantically invalid file and show findings without
      /// deleting anyone's data.
      Workflow: Workflow option
      /// Capabilities the document uses or requires.
      Capabilities: string list
      /// Extension namespaces present anywhere in the document.
      Extensions: Namespace list }

[<RequireQualifiedAccess>]
module Capabilities =
    type Capability = { Id: string; Since: SemanticVersion; Description: string }

    /// The Forma release this library ships in.
    let formaVersion = SemanticVersion.create "0.4.0"

    let private load () =
        use stream = typeof<Finding>.Assembly.GetManifestResourceStream("Forma.Workflow.workflow-capabilities.json")
        use reader = new IO.StreamReader(stream)
        match Json.parse (reader.ReadToEnd()) with
        | Ok json ->
            match Json.field "capabilities" json with
            | Some(Json.Object caps) ->
                caps
                |> List.map (fun (id, c) ->
                    let text name = match Json.field name c with Some(Json.String s) -> s | _ -> ""
                    { Id = id; Since = SemanticVersion.create (text "since"); Description = text "description" })
            | _ -> failwith "capability contract has no capabilities"
        | Result.Error e -> failwith e

    /// The published capability manifest (contracts/workflow-capabilities.json).
    let all = lazy (load ())

    let tryFind id = all.Value |> List.tryFind (fun c -> c.Id = id)

    /// Capabilities a document actually uses, inferred from its content.
    let used (w: Workflow) =
        let groupKinds = w.Groups |> List.map _.Kind
        let anyColor (c: ObjectColor) = c.Fill.IsSome || c.Stroke.IsSome || c.Accent.IsSome || c.Foreground.IsSome
        let colors =
            (w.Nodes |> List.map _.Color) @ (w.Groups |> List.map _.Color) @ (w.Presentation.Legend |> List.map _.Color)
        let colorValues =
            (colors |> List.collect (fun c -> [ c.Fill; c.Stroke; c.Accent; c.Foreground ]) |> List.choose id)
            @ (w.Edges |> List.choose _.Stroke)
        let statuses = (w.Nodes |> List.choose _.Status) @ (w.Edges |> List.choose _.Status) @ (w.Groups |> List.choose _.Status)
        let interactions = (w.Nodes |> List.choose _.Interaction) @ (w.Edges |> List.choose _.Interaction) @ (w.Groups |> List.choose _.Interaction)
        let refs = (w.Nodes |> List.collect _.References) @ (w.Edges |> List.collect _.References) @ (w.Groups |> List.collect _.References)
        let layout =
            (w.Nodes |> List.exists (fun n -> n.Layout <> BoxLayout.empty))
            || (w.Edges |> List.exists (fun e -> e.Layout <> EdgeLayout.empty))
            || w.Layout <> WorkflowLayout.empty
        [ "workflow.core", true
          "workflow.shapes", w.Nodes |> List.exists (fun n -> n.Variant.IsSome)
          "workflow.color", colors |> List.exists anyColor || (w.Edges |> List.exists (fun e -> e.Stroke.IsSome))
          "workflow.palette", (not w.Palette.IsEmpty) || (colorValues |> List.exists (function PaletteColor _ -> true | _ -> false))
          "workflow.groups", groupKinds |> List.exists (fun k -> k = PlainGroup || k = Container)
          "workflow.swimlanes", groupKinds |> List.exists (fun k -> k = Swimlane || k = Phase)
          "workflow.ports", w.Nodes |> List.exists (fun n -> not n.Ports.IsEmpty)
          "workflow.status", not statuses.IsEmpty
          "workflow.references", not refs.IsEmpty
          "workflow.interactions", not interactions.IsEmpty
          "workflow.legend", not w.Presentation.Legend.IsEmpty
          "workflow.layout", layout ]
        |> List.filter snd
        |> List.map fst

[<RequireQualifiedAccess>]
module Validation =
    let private finding code severity category pointer (subject: string option) message =
        { Code = code; Severity = severity; Category = category; Pointer = pointer; Subject = subject; Message = message }

    // -- schema ----------------------------------------------------------------

    let private schemaText =
        lazy
            (use stream = typeof<Finding>.Assembly.GetManifestResourceStream("Forma.Workflow.forma-workflow.schema.json")
             use reader = new IO.StreamReader(stream)
             reader.ReadToEnd())

    /// The published schema text (schemas/workflow/1.0/forma-workflow.schema.json).
    let schemaJson () = schemaText.Value

    let private schema =
        lazy
            (match Json.parse schemaText.Value |> Result.bind JsonSchema.load with
             | Ok s -> s
             | Result.Error e -> failwith e)

    let schemaErrors (json: Json) =
        JsonSchema.validate schema.Value json
        |> List.map (fun e -> finding "SCHEMA" Severity.Error FindingCategory.Structure e.Pointer None e.Message)

    // -- URL safety ------------------------------------------------------------

    /// http, https and mailto URLs and relative references are allowed. Anything
    /// with another scheme (javascript:, data:, vbscript: ...), control
    /// characters or surrounding whitespace is rejected.
    let isSafeUrl (url: string) =
        not (String.IsNullOrEmpty url)
        && url = url.Trim()
        && not (url |> Seq.exists (fun c -> Char.IsControl c || c = ' ' || c = ' '))
        && (let m = System.Text.RegularExpressions.Regex.Match(url, "^([A-Za-z][A-Za-z0-9+.-]*):")
            not m.Success || List.contains (m.Groups[1].Value.ToLowerInvariant()) [ "http"; "https"; "mailto" ])
        && not (url.StartsWith "\\\\")

    /// Bidirectional override/isolate controls can disguise text (CVE-2021-42574 class).
    let private hasBidiControl (s: string) =
        s |> Seq.exists (fun c -> (c >= '‪' && c <= '‮') || (c >= '⁦' && c <= '⁩'))

    // -- semantic rules --------------------------------------------------------

    let private nodePtr i = $"/nodes/{i}"
    let private edgePtr i = $"/edges/{i}"
    let private groupPtr i = $"/groups/{i}"

    let private duplicates (items: (string * string) list) =
        items
        |> List.groupBy fst
        |> List.filter (fun (_, xs) -> xs.Length > 1)
        |> List.collect (fun (id, xs) -> xs |> List.skip 1 |> List.map (fun (_, ptr) -> id, ptr))

    let private identity (w: Workflow) =
        let objects =
            (w.Nodes |> List.mapi (fun i n -> n.Id.Value, nodePtr i))
            @ (w.Edges |> List.mapi (fun i e -> e.Id.Value, edgePtr i))
            @ (w.Groups |> List.mapi (fun i g -> g.Id.Value, groupPtr i))
        let dupObjects =
            duplicates objects
            |> List.map (fun (id, ptr) -> finding "ID-DUPLICATE" Severity.Error FindingCategory.Identity (ptr + "/id") (Some id) $"The id \"{id}\" is used by more than one node, edge or group.")
        let dupPorts =
            w.Nodes
            |> List.mapi (fun i n ->
                duplicates (n.Ports |> List.mapi (fun pi p -> p.Id.Value, $"{nodePtr i}/ports/{pi}"))
                |> List.map (fun (id, ptr) -> finding "ID-DUPLICATE-PORT" Severity.Error FindingCategory.Identity (ptr + "/id") (Some n.Id.Value) $"Node \"{n.Id.Value}\" has more than one port \"{id}\"."))
            |> List.concat
        let dupRefs owner basePtr (refs: Reference list) =
            duplicates (refs |> List.mapi (fun ri r -> r.Id.Value, $"{basePtr}/references/{ri}"))
            |> List.map (fun (id, ptr) -> finding "ID-DUPLICATE-REFERENCE" Severity.Error FindingCategory.Identity (ptr + "/id") (Some owner) $"\"{owner}\" has more than one reference \"{id}\".")
        let refs =
            (w.Nodes |> List.mapi (fun i n -> dupRefs n.Id.Value (nodePtr i) n.References) |> List.concat)
            @ (w.Edges |> List.mapi (fun i e -> dupRefs e.Id.Value (edgePtr i) e.References) |> List.concat)
            @ (w.Groups |> List.mapi (fun i g -> dupRefs g.Id.Value (groupPtr i) g.References) |> List.concat)
        let legend =
            duplicates (w.Presentation.Legend |> List.mapi (fun i l -> l.Id.Value, $"/presentation/legend/{i}"))
            |> List.map (fun (id, ptr) -> finding "ID-DUPLICATE-LEGEND" Severity.Error FindingCategory.Identity (ptr + "/id") None $"The legend has more than one entry \"{id}\".")
        dupObjects @ dupPorts @ refs @ legend

    let private connections (w: Workflow) =
        let nodes = w.Nodes |> List.map (fun n -> n.Id, n) |> Map.ofList
        let endpointFindings i (e: Edge) (role: string) (ep: Endpoint) =
            let ptr = $"{edgePtr i}/{role}"
            match Map.tryFind ep.Node nodes with
            | None ->
                [ finding "REF-DANGLING-EDGE" Severity.Error FindingCategory.Reference (ptr + "/node") (Some e.Id.Value)
                      $"Edge \"{e.Id.Value}\" {role} refers to node \"{ep.Node.Value}\", which does not exist." ]
            | Some n ->
                match ep.Port with
                | None -> []
                | Some portId ->
                    match n.Ports |> List.tryFind (fun p -> p.Id = portId) with
                    | None ->
                        [ finding "REF-MISSING-PORT" Severity.Error FindingCategory.Ports (ptr + "/port") (Some e.Id.Value)
                              $"Edge \"{e.Id.Value}\" uses port \"{portId.Value}\", which node \"{n.Id.Value}\" does not declare." ]
                    | Some port ->
                        match role, port.Direction with
                        | "source", Some In ->
                            [ finding "PORT-DIRECTION" Severity.Error FindingCategory.Ports (ptr + "/port") (Some e.Id.Value)
                                  $"Edge \"{e.Id.Value}\" leaves through input-only port \"{portId.Value}\" of \"{n.Id.Value}\"." ]
                        | "target", Some Out ->
                            [ finding "PORT-DIRECTION" Severity.Error FindingCategory.Ports (ptr + "/port") (Some e.Id.Value)
                                  $"Edge \"{e.Id.Value}\" enters through output-only port \"{portId.Value}\" of \"{n.Id.Value}\"." ]
                        | _ -> []
        let endpoints = w.Edges |> List.mapi (fun i e -> endpointFindings i e "source" e.Source @ endpointFindings i e "target" e.Target) |> List.concat
        let cardinality =
            w.Nodes
            |> List.mapi (fun i n ->
                n.Ports
                |> List.mapi (fun pi p ->
                    let count =
                        w.Edges
                        |> List.sumBy (fun e ->
                            (if e.Source.Node = n.Id && e.Source.Port = Some p.Id then 1 else 0)
                            + (if e.Target.Node = n.Id && e.Target.Port = Some p.Id then 1 else 0))
                    match p.MaxConnections with
                    | Some m when count > m ->
                        [ finding "PORT-CARDINALITY" Severity.Error FindingCategory.Ports $"{nodePtr i}/ports/{pi}" (Some n.Id.Value)
                              $"Port \"{p.Id.Value}\" of \"{n.Id.Value}\" allows {m} connection(s) but has {count}." ]
                    | _ -> [])
                |> List.concat)
            |> List.concat
        endpoints @ cardinality

    let private membership (w: Workflow) =
        let nodeIds = w.Nodes |> List.map _.Id |> Set.ofList
        let groups = w.Groups |> List.map (fun g -> g.Id, g) |> Map.ofList
        let members =
            w.Groups
            |> List.mapi (fun i g ->
                g.Members
                |> List.mapi (fun mi m ->
                    if nodeIds.Contains m then []
                    else
                        [ finding "GROUP-MISSING-MEMBER" Severity.Error FindingCategory.Membership $"{groupPtr i}/members/{mi}" (Some g.Id.Value)
                              $"Group \"{g.Id.Value}\" lists member \"{m.Value}\", which is not a node." ])
                |> List.concat)
            |> List.concat
        let rec ancestors (seen: Set<ObjectId>) (g: Group) =
            match g.Parent with
            | None -> Ok()
            | Some p when seen.Contains p -> Result.Error p
            | Some p ->
                match Map.tryFind p groups with
                | Some parent -> ancestors (seen.Add p) parent
                | None -> Ok()
        let parents =
            w.Groups
            |> List.mapi (fun i g ->
                match g.Parent with
                | None -> []
                | Some p when not (groups.ContainsKey p) ->
                    [ finding "GROUP-MISSING-PARENT" Severity.Error FindingCategory.Membership $"{groupPtr i}/parent" (Some g.Id.Value)
                          $"Group \"{g.Id.Value}\" has parent \"{p.Value}\", which is not a group." ]
                | Some _ ->
                    match ancestors (Set.singleton g.Id) g with
                    | Ok() -> []
                    | Result.Error p ->
                        [ finding "GROUP-CYCLE" Severity.Error FindingCategory.Membership $"{groupPtr i}/parent" (Some g.Id.Value)
                              $"Group \"{g.Id.Value}\" is its own ancestor through \"{p.Value}\"." ])
            |> List.concat
        let lanes =
            w.Nodes
            |> List.mapi (fun i n ->
                let owning = w.Groups |> List.filter (fun g -> g.Kind = Swimlane && List.contains n.Id g.Members)
                if owning.Length > 1 then
                    let names = String.Join(", ", owning |> List.map (fun g -> "\"" + g.Id.Value + "\""))
                    [ finding "GROUP-LANE-CONFLICT" Severity.Error FindingCategory.Membership (nodePtr i) (Some n.Id.Value)
                          $"Node \"{n.Id.Value}\" belongs to more than one swimlane ({names})." ]
                else [])
            |> List.concat
        members @ parents @ lanes

    let private interactionFindings (w: Workflow) =
        let objectIds kind =
            match kind with
            | "node" -> w.Nodes |> List.map _.Id |> Set.ofList
            | "edge" -> w.Edges |> List.map _.Id |> Set.ofList
            | _ -> w.Groups |> List.map _.Id |> Set.ofList
        let check ptr (owner: ObjectId) (refs: Reference list) (interaction: Interaction option) =
            let target t =
                match t with
                | ToNode id when not ((objectIds "node").Contains id) -> [ "node", id.Value ]
                | ToEdge id when not ((objectIds "edge").Contains id) -> [ "edge", id.Value ]
                | ToGroup id when not ((objectIds "group").Contains id) -> [ "group", id.Value ]
                | ToReference id when not (refs |> List.exists (fun r -> r.Id = id)) -> [ "reference", id.Value ]
                | _ -> []
            let missing, url =
                match interaction with
                | Some(Navigate(t, _))
                | Some(Open(t, _)) -> target t, (match t with ToUrl u -> Some u | _ -> None)
                | _ -> [], None
            (missing
             |> List.map (fun (kind, id) ->
                 finding "REF-INTERACTION-TARGET" Severity.Error FindingCategory.Reference (ptr + "/interaction/target") (Some owner.Value)
                     $"The interaction on \"{owner.Value}\" targets {kind} \"{id}\", which does not exist."))
            @ (match url with
               | Some u when not (isSafeUrl u) ->
                   [ finding "SEC-UNSAFE-URL" Severity.Error FindingCategory.Security (ptr + "/interaction/target/url") (Some owner.Value)
                         $"The interaction on \"{owner.Value}\" uses a URL that is not http, https, mailto or relative." ]
               | _ -> [])
            @ (refs
               |> List.mapi (fun ri r ->
                   match r.Href with
                   | Some h when not (isSafeUrl h) ->
                       [ finding "SEC-UNSAFE-URL" Severity.Error FindingCategory.Security $"{ptr}/references/{ri}/href" (Some owner.Value)
                             $"Reference \"{r.Id.Value}\" on \"{owner.Value}\" uses a URL that is not http, https, mailto or relative." ]
                   | _ -> [])
               |> List.concat)
        (w.Nodes |> List.mapi (fun i n -> check (nodePtr i) n.Id n.References n.Interaction) |> List.concat)
        @ (w.Edges |> List.mapi (fun i e -> check (edgePtr i) e.Id e.References e.Interaction) |> List.concat)
        @ (w.Groups |> List.mapi (fun i g -> check (groupPtr i) g.Id g.References g.Interaction) |> List.concat)

    /// Resolves a colour to a literal where that is statically knowable.
    let resolveLiteral (w: Workflow) (c: ColorValue option) =
        match c with
        | Some(LiteralColor h) -> Some h
        | Some(PaletteColor slot) ->
            w.Palette |> List.tryFind (fun (s, _) -> s = slot) |> Option.bind (function _, LiteralSource h -> Some h | _ -> None)
        | _ -> None

    let private paletteFindings (w: Workflow) =
        let defined = w.Palette |> List.map fst |> Set.ofList
        let check ptr subject (values: ColorValue option list) =
            values
            |> List.choose id
            |> List.choose (function
                | PaletteColor slot when not (defined.Contains slot) ->
                    Some(finding "REF-PALETTE" Severity.Error FindingCategory.Reference (ptr + "/color") subject $"Palette slot \"{slot.Value}\" is not defined.")
                | _ -> None)
        let colorList (c: ObjectColor) = [ c.Fill; c.Stroke; c.Accent; c.Foreground ]
        (w.Nodes |> List.mapi (fun i n -> check (nodePtr i) (Some n.Id.Value) (colorList n.Color)) |> List.concat)
        @ (w.Edges |> List.mapi (fun i e -> check (edgePtr i) (Some e.Id.Value) [ e.Stroke ]) |> List.concat)
        @ (w.Groups |> List.mapi (fun i g -> check (groupPtr i) (Some g.Id.Value) (colorList g.Color)) |> List.concat)
        @ (w.Presentation.Legend |> List.mapi (fun i l -> check $"/presentation/legend/{i}" None (colorList l.Color)) |> List.concat)

    let rec private allExtensions (w: Workflow) =
        w.Extensions
        @ (w.Nodes |> List.collect (fun n -> n.Extensions @ (n.Ports |> List.collect _.Extensions)))
        @ (w.Edges |> List.collect _.Extensions)
        @ (w.Groups |> List.collect _.Extensions)

    /// Extension namespaces present anywhere in the document, in first-seen order.
    let extensionNamespaces (w: Workflow) = allExtensions w |> List.map fst |> List.distinct

    let private extensionFindings (w: Workflow) =
        extensionNamespaces w
        |> List.filter (fun n -> n.Value = "forma" || n.Value.StartsWith "forma.")
        |> List.map (fun n ->
            finding "EXT-RESERVED" Severity.Error FindingCategory.ExtensionData "/extensions" None
                $"The namespace \"{n.Value}\" is reserved for Forma; producers use their own reverse-DNS namespace.")

    let private outgoing (w: Workflow) (id: ObjectId) =
        w.Edges |> List.filter (fun e -> e.Source.Node = id && e.Direction <> Some Backward && e.Direction <> Some Undirected)

    let private incoming (w: Workflow) (id: ObjectId) =
        w.Edges |> List.filter (fun e -> e.Target.Node = id && e.Direction <> Some Backward && e.Direction <> Some Undirected)

    let private semanticWarnings (w: Workflow) =
        w.Nodes
        |> List.mapi (fun i n ->
            let out = outgoing w n.Id
            match n.Kind with
            | CoreNode(Decision, _) when out.Length < 2 ->
                [ finding "SEM-DECISION-BRANCHES" Severity.Warning FindingCategory.Semantics (nodePtr i) (Some n.Id.Value)
                      $"Decision \"{n.Label}\" has {out.Length} outgoing edge(s); a decision normally has at least two." ]
            | CoreNode(Start, _) when not (incoming w n.Id).IsEmpty ->
                [ finding "SEM-START-INCOMING" Severity.Warning FindingCategory.Semantics (nodePtr i) (Some n.Id.Value) $"Start \"{n.Label}\" has incoming edges." ]
            | CoreNode(End, _) when not out.IsEmpty ->
                [ finding "SEM-END-OUTGOING" Severity.Warning FindingCategory.Semantics (nodePtr i) (Some n.Id.Value) $"End \"{n.Label}\" has outgoing edges." ]
            | _ -> [])
        |> List.concat

    let private blank (s: string) = String.IsNullOrWhiteSpace s

    /// Accessibility rules that can be decided from the document alone.
    let private accessibility (w: Workflow) =
        let title =
            if blank w.Title then [ finding "A11Y-TITLE" Severity.Error FindingCategory.Accessibility "/title" None "The workflow title is blank; it names the diagram for assistive technology." ]
            else []
        let names =
            w.Nodes
            |> List.mapi (fun i n ->
                [ if blank n.Label && n.Accessibility.Name |> Option.forall blank then
                      finding "A11Y-NAME" Severity.Error FindingCategory.Accessibility (nodePtr i + "/label") (Some n.Id.Value) $"Node \"{n.Id.Value}\" has no readable name."
                  if n.Accessibility.Name |> Option.exists blank then
                      finding "A11Y-NAME" Severity.Error FindingCategory.Accessibility (nodePtr i + "/accessibility/name") (Some n.Id.Value) $"Node \"{n.Id.Value}\" has a blank accessible name." ])
            |> List.concat
        let groupNames =
            w.Groups
            |> List.mapi (fun i g ->
                if blank g.Label then [ finding "A11Y-NAME" Severity.Error FindingCategory.Accessibility (groupPtr i + "/label") (Some g.Id.Value) $"Group \"{g.Id.Value}\" has no readable name." ]
                else [])
            |> List.concat
        let contrast ptr subject (c: ObjectColor) =
            match resolveLiteral w c.Foreground, resolveLiteral w c.Fill with
            | Some fg, Some fill when HexColor.contrast fg fill < 4.5 ->
                let ratio = HexColor.contrast fg fill
                let shown = ratio.ToString("0.00", Globalization.CultureInfo.InvariantCulture)
                [ finding "A11Y-CONTRAST" Severity.Error FindingCategory.Accessibility (ptr + "/color") subject
                      $"Text colour {fg.Value} on fill {fill.Value} has contrast {shown}:1; at least 4.5:1 is required." ]
            | None, Some fill when HexColor.contrast (HexColor.create "#171a18") fill < 4.5 && HexColor.contrast (HexColor.create "#ffffff") fill < 4.5 ->
                [ finding "A11Y-CONTRAST" Severity.Warning FindingCategory.Accessibility (ptr + "/color") subject
                      $"Fill {fill.Value} has low contrast with both dark and light text; set an explicit foreground." ]
            | _ -> []
        let contrasts =
            (w.Nodes |> List.mapi (fun i n -> contrast (nodePtr i) (Some n.Id.Value) n.Color) |> List.concat)
            @ (w.Groups |> List.mapi (fun i g -> contrast (groupPtr i) (Some g.Id.Value) g.Color) |> List.concat)
        let branches =
            w.Nodes
            |> List.mapi (fun i n ->
                let out = outgoing w n.Id
                let unlabeled = out |> List.filter (fun e -> e.Label.IsNone && e.Accessibility.Name.IsNone)
                if out.Length > 1 && not unlabeled.IsEmpty then
                    let ids = String.Join(", ", unlabeled |> List.map (fun e -> "\"" + e.Id.Value + "\""))
                    [ finding "A11Y-BRANCH-LABEL" Severity.Warning FindingCategory.Accessibility (nodePtr i) (Some n.Id.Value)
                          $"\"{n.Label}\" branches, but edges {ids} have no label, so the difference between branches is only visual." ]
                else [])
            |> List.concat
        let customColorWithoutLegend =
            let colored =
                w.Nodes
                |> List.filter (fun n -> n.Color.Fill.IsSome || n.Color.Accent.IsSome)
                |> List.choose (fun n -> n.Color.Fill |> Option.orElse n.Color.Accent)
                |> List.distinct
            if colored.Length > 1 && w.Presentation.Legend.IsEmpty then
                [ finding "A11Y-LEGEND" Severity.Info FindingCategory.Accessibility "/presentation" None
                      "Several authored colours are used without a legend. If the colours carry meaning, add a legend so it is not conveyed by colour alone." ]
            else []
        let bidi =
            let texts =
                [ yield "/title", None, w.Title
                  for i, n in List.indexed w.Nodes do
                      yield nodePtr i + "/label", Some n.Id.Value, n.Label
                  for i, e in List.indexed w.Edges do
                      match e.Label with Some l -> yield edgePtr i + "/label", Some e.Id.Value, l | None -> ()
                  for i, g in List.indexed w.Groups do
                      yield groupPtr i + "/label", Some g.Id.Value, g.Label ]
            texts
            |> List.filter (fun (_, _, t) -> hasBidiControl t)
            |> List.map (fun (ptr, subject, _) ->
                finding "SEC-BIDI-CONTROL" Severity.Warning FindingCategory.Security ptr subject "Text contains bidirectional override characters that can make it read differently than it is stored.")
        title @ names @ groupNames @ contrasts @ branches @ customColorWithoutLegend @ bidi

    let private layoutFindings (w: Workflow) =
        match w.Layout.Mode with
        | Some Authored ->
            let unplaced = w.Nodes |> List.filter (fun n -> n.Layout.Position.IsNone)
            if unplaced.IsEmpty then []
            else
                [ finding "LAYOUT-PARTIAL" Severity.Info FindingCategory.Semantics "/layout" None
                      $"{unplaced.Length} node(s) have no authored position; the default layout places them." ]
        | _ -> []

    /// Every rule that needs a decoded workflow.
    let semantic (w: Workflow) : Finding list =
        identity w @ connections w @ membership w @ interactionFindings w @ paletteFindings w @ extensionFindings w
        @ semanticWarnings w @ accessibility w @ layoutFindings w

    let private compatibilityFindings (w: Workflow) =
        let used = Capabilities.used w
        let required = w.Forma.Requires
        let all = (used @ required) |> List.distinct
        let unavailable =
            all
            |> List.filter (fun id ->
                match Capabilities.tryFind id with
                | None -> true
                | Some c -> compare c.Since w.Forma.Version > 0)
        let findings =
            unavailable
            |> List.map (fun id ->
                match Capabilities.tryFind id with
                | None ->
                    finding "COMPAT-UNKNOWN-CAPABILITY" Severity.Warning FindingCategory.Compatibility "/forma/requires" None
                        $"The workflow requires \"{id}\", which Forma {Capabilities.formaVersion} does not provide."
                | Some c ->
                    finding "COMPAT-UNAVAILABLE" Severity.Warning FindingCategory.Compatibility "/forma/version" None
                        $"\"{id}\" needs Forma {c.Since}, but the workflow targets Forma {w.Forma.Version}.")
        all, unavailable, findings

    // -- migration -------------------------------------------------------------

    /// Pre-releases of the current format version upgrade automatically: the
    /// version is rewritten and the document must then pass the current schema.
    let private migrate (version: SemanticVersion) (json: Json) : (Json * bool * string) =
        let current = Format.current
        if version.Major = current.Major && version.Minor = current.Minor && version.Patch = current.Patch && version.Pre.IsSome then
            let rewritten =
                match json with
                | Json.Object props -> Json.Object(props |> List.map (fun (k, v) -> if k = "formatVersion" then k, Json.String(current.ToString()) else k, v))
                | other -> other
            rewritten, true, $"pre-release {version} upgraded to {current}"
        elif compare version current < 0 then json, false, $"format {version} predates {current} and no migration is registered"
        else json, false, $"format {version} is newer than this reader ({current}); upgrade Forma to read it"

    // -- the pipeline -----------------------------------------------------------

    let private invalid findings =
        { Class = StructurallyInvalid; Findings = findings; Workflow = None; Capabilities = []; Extensions = [] }

    /// Classifies and decodes a parsed document.
    let ofJson (json: Json) : ValidationReport =
        let format = Json.field "format" json
        let version = Json.field "formatVersion" json
        match json, format, version with
        | Json.Object _, Some(Json.String f), _ when f <> Format.identifier ->
            invalid [ finding "FORMAT" Severity.Error FindingCategory.Structure "/format" None $"This is not a Forma workflow (format \"{f}\")." ]
        | Json.Object _, Some(Json.String _), Some(Json.String v) when (SemanticVersion.tryParse v).IsSome ->
            let declared = SemanticVersion.create v
            let json, migrated, migrationReason =
                if declared = Format.current then json, false, ""
                else migrate declared json
            let migrationNeeded = declared <> Format.current
            if migrationNeeded && not migrated then
                { Class = MigrationRequired(declared, Format.current, false, migrationReason)
                  Findings = [ finding "FORMAT-VERSION" Severity.Error FindingCategory.Compatibility "/formatVersion" None migrationReason ]
                  Workflow = None
                  Capabilities = []
                  Extensions = [] }
            else
                match schemaErrors json with
                | _ :: _ as errors -> invalid errors
                | [] ->
                    match Codec.decode json with
                    | Result.Error e -> invalid [ finding "DECODE" Severity.Error FindingCategory.Structure "/" None e ]
                    | Ok workflow ->
                        let semanticFindings = semantic workflow
                        let caps, unavailable, compat = compatibilityFindings workflow
                        let namespaces = extensionNamespaces workflow
                        let migrationFindings =
                            if migrated then [ finding "FORMAT-MIGRATED" Severity.Info FindingCategory.Compatibility "/formatVersion" None migrationReason ] else []
                        let findings = migrationFindings @ semanticFindings @ compat
                        let cls =
                            if semanticFindings |> List.exists (fun f -> f.Severity = Severity.Error) then SemanticallyInvalid
                            elif migrated then MigrationRequired(declared, Format.current, true, migrationReason)
                            elif not unavailable.IsEmpty then UnavailableCapabilities unavailable
                            elif not namespaces.IsEmpty then SupportedWithPreservedExtensions namespaces
                            else FullySupported
                        { Class = cls; Findings = findings; Workflow = Some workflow; Capabilities = caps; Extensions = namespaces }
        | Json.Object _, _, _ ->
            match schemaErrors json with
            | [] -> invalid [ finding "FORMAT" Severity.Error FindingCategory.Structure "/" None "The document has no format identifier." ]
            | errors -> invalid errors
        | _ -> invalid [ finding "FORMAT" Severity.Error FindingCategory.Structure "/" None "A workflow document is a JSON object." ]

    /// Parses, classifies and decodes untrusted text.
    let load (text: string) : ValidationReport =
        match Json.parse text with
        | Result.Error e -> invalid [ finding "JSON" Severity.Error FindingCategory.Structure "/" None e ]
        | Ok json -> ofJson json

    /// Re-validates an in-memory workflow (for example after an edit).
    let check (w: Workflow) : ValidationReport = ofJson (Codec.encode w)

    let isValid (report: ValidationReport) =
        match report.Class with
        | FullySupported
        | SupportedWithPreservedExtensions _
        | UnavailableCapabilities _ -> true
        | MigrationRequired(_, _, automatic, _) -> automatic
        | StructurallyInvalid
        | SemanticallyInvalid -> false

    // -- report encoding ---------------------------------------------------------

    let severityText s = match s with Severity.Error -> "error" | Severity.Warning -> "warning" | Severity.Info -> "info"

    let categoryText c =
        match c with
        | FindingCategory.Structure -> "structure"
        | FindingCategory.Identity -> "identity"
        | FindingCategory.Reference -> "reference"
        | FindingCategory.Ports -> "ports"
        | FindingCategory.Membership -> "membership"
        | FindingCategory.ExtensionData -> "extensions"
        | FindingCategory.Compatibility -> "compatibility"
        | FindingCategory.Accessibility -> "accessibility"
        | FindingCategory.Security -> "security"
        | FindingCategory.Semantics -> "semantics"

    let classText c =
        match c with
        | FullySupported -> "fully-supported"
        | SupportedWithPreservedExtensions _ -> "supported-with-preserved-extensions"
        | MigrationRequired _ -> "migration-required"
        | StructurallyInvalid -> "structurally-invalid"
        | SemanticallyInvalid -> "semantically-invalid"
        | UnavailableCapabilities _ -> "unavailable-capabilities"

    let findingJson (f: Finding) =
        Json.Object
            [ yield "code", Json.String f.Code
              yield "severity", Json.String(severityText f.Severity)
              yield "category", Json.String(categoryText f.Category)
              yield "pointer", Json.String f.Pointer
              match f.Subject with Some s -> yield "subject", Json.String s | None -> ()
              yield "message", Json.String f.Message ]

    /// The machine-readable report used by the CLI, CI, agents and hosts.
    let reportJson (r: ValidationReport) =
        Json.Object
            [ yield "class", Json.String(classText r.Class)
              yield "valid", Json.Bool(isValid r)
              match r.Class with
              | MigrationRequired(f, t, a, reason) ->
                  yield "migration", Json.Object [ "from", Json.String(f.ToString()); "to", Json.String(t.ToString()); "automatic", Json.Bool a; "reason", Json.String reason ]
              | UnavailableCapabilities caps -> yield "unavailable", Json.Array(caps |> List.map Json.String)
              | _ -> ()
              match r.Workflow with Some w -> yield "workflow", Json.String w.Id.Value | None -> ()
              yield "formatVersion", Json.String(Format.current.ToString())
              yield "capabilities", Json.Array(r.Capabilities |> List.map Json.String)
              yield "extensions", Json.Array(r.Extensions |> List.map (fun n -> Json.String n.Value))
              yield "findings", Json.Array(r.Findings |> List.map findingJson) ]
