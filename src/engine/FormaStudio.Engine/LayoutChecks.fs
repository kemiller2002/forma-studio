namespace FormaStudio.Engine

/// Pre-export checks for a Layout page against the universal UI layout and
/// interaction contract (Visual Engineering UI-LAYOUT-INTERACTION@1.0.0). Each
/// finding names the rule it serves. They are avoidable authoring problems that
/// the model can see before any browser runs: a placeholder link, a control with
/// no accessible name, an unbreakable string that can overflow at 320 CSS px, and
/// a declared alignment group whose peers cannot share the declared edge. They
/// never block an export, and they do not replace the browser geometry check of
/// the exported markup (a model cannot prove rendered geometry).
[<RequireQualifiedAccess>]
module LayoutChecks =
    /// The longest unbroken run of visible text treated as safe at 320 CSS px.
    let unbreakableLimit = 32

    /// A label this long may wrap beside a shorter peer and stagger the controls.
    let peerLabelLimit = 40

    /// The contract rule each check code serves.
    let ruleOf (code: string) =
        match code with
        | "layout.link.placeholder" | "layout.button.unowned" -> "ULI-A01"
        | "layout.name.missing" -> "ULI-A06"
        | "layout.text.unbreakable" -> "ULI-L05"
        | "layout.alignment.control-types" -> "ULI-L02"
        | _ -> "ULI-L01"

    let private text key (node: ComponentNode) =
        match node.Content |> Map.tryFind key with
        | Some(JString s) -> s
        | _ -> ""

    let private children (node: ComponentNode) = node.Slots |> Map.tryFind "children" |> Option.defaultValue []

    let rec private flatten (nodes: ComponentNode list) =
        nodes |> List.collect (fun n -> n :: (n.Slots |> Map.toList |> List.collect (snd >> flatten)))

    /// Content shown to people (an href or a field name is not).
    let private visibleKeys = set [ "text"; "label"; "description"; "title"; "value"; "context" ]

    let private longestRun (s: string) =
        s.Split([| ' '; '\t'; '\n'; '\r' |], System.StringSplitOptions.RemoveEmptyEntries) |> Array.fold (fun acc w -> max acc w.Length) 0

    let private placeholderHref (href: string) =
        let h = href.Trim()
        h = "" || h = "#" || h.StartsWith("javascript:", System.StringComparison.OrdinalIgnoreCase) || not (Forma.Workflow.Validation.isSafeUrl h)

    let private nodeChecks (finding: string -> Severity -> ComponentNode -> string -> Finding) (node: ComponentNode) =
        let name = Id.value node.Id
        let action =
            match node.Component with
            | "link-button" when placeholderHref (text "href" node) ->
                [ finding "layout.link.placeholder" Warning node (sprintf "Link button %s has no real destination (empty, \"#\" or unsafe), so it would be an inert action. Give it a page, http(s) or mailto address." name) ]
            | "button" ->
                [ finding "layout.button.unowned" Advisory node (sprintf "Button %s exports as a native button with no behaviour (static HTML has no script). The consuming application must wire it, or use a link button with a destination." name) ]
            | _ -> []
        let named =
            match node.Component with
            | "button" | "link-button" | "text-field" when (text "label" node).Trim() = "" ->
                [ finding "layout.name.missing" Warning node (sprintf "%s %s has no visible label, so it has no accessible name." node.Component name) ]
            | _ -> []
        let overflow =
            node.Content
            |> Map.toList
            |> List.choose (fun (key, value) -> match value with JString s when visibleKeys.Contains key && longestRun s > unbreakableLimit -> Some key | _ -> None)
            |> List.map (fun key ->
                finding "layout.text.unbreakable" Warning node
                    (sprintf "%s %s: its %s has an unbroken run longer than %d characters, which can overflow its container at 320 CSS px. Add spaces or shorten it." node.Component name key unbreakableLimit))
        action @ named @ overflow

    let private alignmentChecks (finding: string -> Severity -> ComponentNode -> string -> Finding) (node: ComponentNode) =
        match Components.alignmentOf node with
        | None -> []
        | Some edge ->
            let name = Id.value node.Id
            let peers = children node
            let fields = peers |> List.filter (fun p -> p.Component = "text-field")
            let few =
                if peers.Length < 2 then
                    [ finding "layout.alignment.peers" Advisory node (sprintf "%s %s declares a %s-aligned group but has fewer than two peers." node.Component name edge) ]
                else []
            let described = fields |> List.map (fun f -> (text "description" f).Trim() <> "") |> List.distinct
            let structure =
                if described.Length > 1 then
                    [ finding "layout.alignment.structure" Warning node
                          (sprintf "In aligned group %s some fields have a description and some do not; the description sits above the input, so the inputs cannot share a %s edge. Give every peer a description or none." name edge) ]
                else []
            let mixed =
                if not fields.IsEmpty && fields.Length < peers.Length then
                    [ finding "layout.alignment.mixed" Warning node
                          (sprintf "Aligned group %s mixes labelled fields with other components; a field's label sits above its input, so the peers cannot share a %s edge. Keep fields and actions in separate groups." name edge) ]
                else []
            let long =
                fields
                |> List.filter (fun f -> (text "label" f).Length > peerLabelLimit)
                |> List.map (fun f ->
                    finding "layout.alignment.label-length" Advisory node
                        (sprintf "In aligned group %s the label of %s is longer than %d characters and may wrap beside its peers, staggering the inputs. Shorten it or move detail into its description." name (Id.value f.Id) peerLabelLimit))
            let inputType (f: ComponentNode) = match f.Properties |> Map.tryFind "inputType" with Some(JString t) -> t | _ -> "text"
            let types = fields |> List.map inputType |> List.distinct
            let controlTypes =
                if types.Length > 1 then
                    [ finding "layout.alignment.control-types" Advisory node
                          (sprintf "Aligned group %s mixes input types (%s). Forma does not yet give every native input one intrinsic height (a date input is taller), so the peers can differ by about a pixel; this is Forma's to fix, not a page offset." name (String.concat ", " types)) ]
                else []
            few @ structure @ mixed @ long @ controlTypes

    /// Every pre-export finding for one page, in document order.
    let page (page: Page) : Finding list =
        let finding code severity (node: ComponentNode) message =
            Finding.create code severity LayoutRule (ObjectRef.describe (ComponentRef(page.Id, node.Id))) message
        flatten page.Nodes |> List.collect (fun n -> nodeChecks finding n @ alignmentChecks finding n)

    /// "ULI-L01: message", the text shown to the author.
    let describe (f: Finding) = sprintf "%s: %s" (ruleOf f.Code) f.Message
