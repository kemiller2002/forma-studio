namespace Forma.Workflow

open System

[<RequireQualifiedAccess>]
module Validation =
    open ValidationRules

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

    /// http, https and mailto URLs and relative references are allowed. Anything
    /// with another scheme (javascript:, data:, vbscript: ...), control
    /// characters or surrounding whitespace is rejected.
    let isSafeUrl (url: string) = ValidationRules.isSafeUrl url

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

    /// Forma's light surface-primary (--forma-color-surface-primary, the
    /// components.css fallback): the effective fill of a node without an
    /// authored fill, and the canvas behind groups and edges.
    let defaultSurface = HexColor.create "#fafafa"

    /// The default diagram surfaces a literal colour may land on: the compiled
    /// light theme and the dark theme (prefers-color-scheme: dark and
    /// data-forma-theme="dark"), both --forma-color-surface-primary in dist/tokens.css.
    let defaultSurfaces = [ "light", defaultSurface; "dark", HexColor.create "#161616" ]

    /// The default surfaces a document is judged against for a host theme: the
    /// pinned theme's surface only, or light and dark when the host follows
    /// prefers-color-scheme (no theme, or one Forma.Workflow does not pin).
    let surfacesFor (options: ValidationOptions) =
        match options.Theme with
        | Some theme when defaultSurfaces |> List.exists (fst >> (=) theme) -> defaultSurfaces |> List.filter (fst >> (=) theme)
        | _ -> defaultSurfaces

    /// WCAG 1.4.11 minimum for non-text indicators (outlines, accents, connectors).
    let nonTextMinimum = 3.0

    let private ratioText (ratio: float) = ratio.ToString("0.00", Globalization.CultureInfo.InvariantCulture)

    let private nonTextFinding ptr subject message =
        finding "A11Y-NONTEXT-CONTRAST" Severity.Error FindingCategory.Accessibility ptr subject
            $"{message}; non-text indicators need at least 3:1 (WCAG 1.4.11)."

    /// A literal drawn directly on the default surfaces, judged in the theme where it is weakest.
    let private onSurfaces surfaces ptr subject (label: string) (colour: HexColor) =
        let theme, surface = surfaces |> List.minBy (fun (_, s) -> HexColor.contrast colour s)
        let ratio = HexColor.contrast colour surface
        if ratio < nonTextMinimum then
            [ nonTextFinding ptr subject $"{label} colour {colour.Value} on the {theme} surface {surface.Value} has contrast {ratioText ratio}:1" ]
        else []

    /// A11Y-NONTEXT-CONTRAST for a node, group or legend swatch.
    /// - No authored fill: the shape is drawn on the default surface, so a
    ///   literal stroke and accent must reach 3:1 against the light and the dark
    ///   surface.
    /// - Literal fill: the accent is an indicator and must reach 3:1 against the
    ///   fill. The stroke is a boundary: it is reported when it is below 3:1
    ///   against the fill and the boundary is not otherwise perceivable, that is
    ///   when neither the stroke nor the fill reaches 3:1 against a default
    ///   surface (a borderless filled shape stays valid where its fill stands out).
    /// Token values (and token-backed palette slots), token fills and token/literal
    /// mixes are not judged: the theme decides them. The token pairs are covered by
    /// tests/nontext-contrast.test.mjs.
    let private nonTextContrast surfaces (w: Workflow) ptr subject (c: ObjectColor) =
        let stroke, accent = resolveLiteral w c.Stroke, resolveLiteral w c.Accent
        let field name = ptr + "/color/" + name
        let onSurface name label value = value |> Option.map (onSurfaces surfaces (field name) subject label) |> Option.defaultValue []
        match c.Fill with
        | None -> onSurface "stroke" "Stroke" stroke @ onSurface "accent" "Accent" accent
        | Some _ ->
            match resolveLiteral w c.Fill with
            | None -> []
            | Some fill ->
                let below a b = HexColor.contrast a b < nonTextMinimum
                let strokeFindings =
                    match stroke with
                    | Some s when below s fill ->
                        surfaces
                        |> List.tryFind (fun (_, surface) -> below s surface && below fill surface)
                        |> Option.map (fun (theme, surface) ->
                            nonTextFinding (field "stroke") subject
                                $"Stroke colour {s.Value} on fill {fill.Value} has contrast {ratioText (HexColor.contrast s fill)}:1, and neither reaches 3:1 against the {theme} surface {surface.Value}, so the shape has no perceivable boundary")
                        |> Option.toList
                    | _ -> []
                let accentFindings =
                    match accent with
                    | Some a when below a fill ->
                        [ nonTextFinding (field "accent") subject $"Accent colour {a.Value} on fill {fill.Value} has contrast {ratioText (HexColor.contrast a fill)}:1" ]
                    | _ -> []
                strokeFindings @ accentFindings

    /// A11Y-NONTEXT-CONTRAST for an edge: a literal stroke against the default surfaces.
    let private edgeContrast surfaces (w: Workflow) ptr subject (stroke: ColorValue option) =
        resolveLiteral w stroke |> Option.map (onSurfaces surfaces (ptr + "/color/stroke") subject "Edge") |> Option.defaultValue []

    /// Accessibility rules that can be decided from the document alone.
    let private accessibility (options: ValidationOptions) (w: Workflow) =
        let surfaces = surfacesFor options
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
            | None, Some fill when HexColor.contrast (HexColor.create "#161616") fill < 4.5 && HexColor.contrast (HexColor.create "#ffffff") fill < 4.5 ->
                [ finding "A11Y-CONTRAST" Severity.Warning FindingCategory.Accessibility (ptr + "/color") subject
                      $"Fill {fill.Value} has low contrast with both dark and light text; set an explicit foreground." ]
            | _ -> []
        let contrasts =
            (w.Nodes |> List.mapi (fun i n -> contrast (nodePtr i) (Some n.Id.Value) n.Color) |> List.concat)
            @ (w.Groups |> List.mapi (fun i g -> contrast (groupPtr i) (Some g.Id.Value) g.Color) |> List.concat)
        let nonText =
            (w.Nodes |> List.mapi (fun i n -> nonTextContrast surfaces w (nodePtr i) (Some n.Id.Value) n.Color) |> List.concat)
            @ (w.Groups |> List.mapi (fun i g -> nonTextContrast surfaces w (groupPtr i) (Some g.Id.Value) g.Color) |> List.concat)
            @ (w.Edges |> List.mapi (fun i e -> edgeContrast surfaces w (edgePtr i) (Some e.Id.Value) e.Stroke) |> List.concat)
            @ (w.Presentation.Legend |> List.mapi (fun i l -> nonTextContrast surfaces w $"/presentation/legend/{i}" (Some l.Id.Value) l.Color) |> List.concat)
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
        title @ names @ groupNames @ contrasts @ nonText @ branches @ customColorWithoutLegend @ bidi

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
    let semanticWith (options: ValidationOptions) (w: Workflow) : Finding list =
        identity w @ connections w @ membership w @ interactionFindings w @ paletteFindings w @ extensionFindings w
        @ semanticWarnings w @ accessibility options w @ layoutFindings w

    /// Every rule that needs a decoded workflow, for a host that follows prefers-color-scheme.
    let semantic (w: Workflow) : Finding list = semanticWith ValidationOptions.defaults w

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

    /// Classifies and decodes a parsed document for the given host options.
    let ofJsonWith (options: ValidationOptions) (json: Json) : ValidationReport =
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
                        let semanticFindings = semanticWith options workflow
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

    /// Classifies and decodes a parsed document (no pinned theme).
    let ofJson (json: Json) : ValidationReport = ofJsonWith ValidationOptions.defaults json

    /// Parses, classifies and decodes untrusted text for the given host options.
    let loadWith (options: ValidationOptions) (text: string) : ValidationReport =
        match Json.parse text with
        | Result.Error e -> invalid [ finding "JSON" Severity.Error FindingCategory.Structure "/" None e ]
        | Ok json -> ofJsonWith options json

    /// Parses, classifies and decodes untrusted text (no pinned theme).
    let load (text: string) : ValidationReport = loadWith ValidationOptions.defaults text

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
