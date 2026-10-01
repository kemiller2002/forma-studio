namespace Forma.Workflow

open System

/// Identity, connection, membership, interaction and URL rules. Internal to
/// Validation, which re-exports isSafeUrl.
module internal ValidationRules =
    let finding code severity category pointer (subject: string option) message =
        { Code = code; Severity = severity; Category = category; Pointer = pointer; Subject = subject; Message = message }

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
    let hasBidiControl (s: string) =
        s |> Seq.exists (fun c -> (c >= '‪' && c <= '‮') || (c >= '⁦' && c <= '⁩'))

    // -- semantic rules --------------------------------------------------------

    let nodePtr i = $"/nodes/{i}"
    let edgePtr i = $"/edges/{i}"
    let groupPtr i = $"/groups/{i}"

    let duplicates (items: (string * string) list) =
        items
        |> List.groupBy fst
        |> List.filter (fun (_, xs) -> xs.Length > 1)
        |> List.collect (fun (id, xs) -> xs |> List.skip 1 |> List.map (fun (_, ptr) -> id, ptr))

    let identity (w: Workflow) =
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

    let connections (w: Workflow) =
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

    let membership (w: Workflow) =
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

    let interactionFindings (w: Workflow) =
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
