namespace FormaStudio.Engine

/// The layer that supplied an effective property (FDA-1061, FDA-1067). Editor
/// adorners are deliberately absent: they are never canonical appearance.
type AppearanceLayer =
    | FormaDefaultLayer
    | ProfileDefaultLayer of profile: string * kind: string
    | StyleLayer of StyleId * revision: int
    | MappingLayer of MappingId * field: FieldKey * legend: string
    | OverrideLayer

/// A resolved color keeps its source. `Css` is None when the source cannot be
/// resolved (for example a deleted palette slot); the renderer then falls back to
/// the Forma default and a finding explains why (FDA-963).
type ColorResolution =
    { Source: ColorRef
      Css: string option
      Palette: PaletteSlotId option }

type Effective<'T> = { Value: 'T option; Layer: AppearanceLayer }

type EffectiveAppearance =
    { Fill: Effective<ColorResolution>
      Stroke: Effective<ColorResolution>
      Accent: Effective<ColorResolution>
      Foreground: Effective<ColorResolution>
      ConnectorStroke: Effective<ColorResolution>
      ConnectorWidth: Effective<int>
      Line: Effective<LineStyle>
      Shape: Effective<Shape> }

/// Where the resolved appearance will be seen. Editor scope may use source-only
/// metadata for highlighting (FDA-1266); every output scope obeys disclosure.
type AppearanceScope =
    | EditorScope
    | OutputScope of OutputScope

[<RequireQualifiedAccess>]
module AppearanceResolution =
    let private defaultForegroundHex = "#171a18"

    let colorCss (project: Project) (color: ColorRef) =
        match color with
        | TokenColor token -> { Source = color; Css = Some(sprintf "var(%s)" (TokenRef.value token)); Palette = None }
        | LiteralColor hex -> { Source = color; Css = Some(HexColor.value hex); Palette = None }
        | PaletteColor id ->
            match ProjectOps.tryPalette id project with
            | Some { Value = PaletteToken token } -> { Source = color; Css = Some(sprintf "var(%s)" (TokenRef.value token)); Palette = Some id }
            | Some { Value = PaletteLiteral hex } -> { Source = color; Css = Some(HexColor.value hex); Palette = Some id }
            | None -> { Source = color; Css = None; Palette = Some id }

    let private kindOf reference = ObjectRef.kind reference

    let private profileLayer (project: Project) reference =
        let spec =
            match reference with
            | NodeRef(d, n) ->
                match ProjectOps.tryDiagram d project, ProjectOps.tryNode d n project with
                | Some diagram, Some node ->
                    Profiles.tryFind diagram.Profile
                    |> Option.bind (fun p -> Profiles.nodeKind p node.Kind |> Option.map (fun k -> p.Id, k.Kind, { Appearance.empty with Shape = Some k.DefaultShape }))
                | _ -> None
            | EdgeRef(d, e) ->
                match ProjectOps.tryDiagram d project, ProjectOps.tryEdge d e project with
                | Some diagram, Some edge ->
                    Profiles.tryFind diagram.Profile
                    |> Option.bind (fun p -> Profiles.edgeKind p edge.Kind |> Option.map (fun k -> p.Id, k.Kind, { Appearance.empty with Line = Some k.DefaultLine }))
                | _ -> None
            | _ -> None
        spec |> Option.map (fun (profile, kind, appearance) -> ProfileDefaultLayer(profile, kind), appearance) |> Option.toList

    let private styleAppearance (project: Project) target styleId =
        match ProjectOps.tryStyle styleId project with
        | Some style -> Ok style
        | None ->
            Error(Finding.create "appearance.style.missing" Warning AppearanceRule target
                    (sprintf "Style '%s' is unavailable; its reference is kept and the next layer is shown." (Id.value styleId)))

    /// The mapping outcome for one resolved value, with the legend text that explains it.
    let mappingOutcome (mapping: PresentationMapping) (resolved: ResolvedValue) =
        let fallback f =
            match f with
            | NoMapping -> None
            | Apply(outcome, legend) -> Some(outcome, legend)

        match resolved with
        | ResolvedMissing _ -> fallback mapping.Fallbacks.Missing
        | ResolvedUnknown -> fallback mapping.Fallbacks.Unknown
        | ResolvedUnavailable _ -> fallback mapping.Fallbacks.Unavailable
        | ResolvedInvalid _
        | ResolvedConflict _ -> fallback mapping.Fallbacks.Invalid
        | ResolvedExplicit value
        | ResolvedDefault value
        | ResolvedDerived(value, _)
        | ResolvedSourceBound(value, _) ->
            let keys = MetadataRules.matchKeys value
            mapping.Rules
            |> List.tryFind (fun rule ->
                match rule.Match with
                | Equals expected -> List.contains expected keys
                | AnyOf expected -> expected |> List.exists (fun e -> List.contains e keys))
            |> Option.map (fun rule -> rule.Outcome, rule.Legend)
            |> Option.orElse (fallback mapping.Fallbacks.Unmapped)

    /// May a mapping from this field influence appearance seen in `scope`?
    let derivationAllowed scope (field: FieldDefinition) =
        match scope with
        | EditorScope -> true
        | OutputScope _ -> field.Disclosure.DerivedPresentation

    let private mappingLayers scope (project: Project) reference target =
        project.Mappings
        |> List.filter (fun m -> m.Enabled && m.Targets.Contains(kindOf reference))
        |> List.map (fun mapping ->
            match ProjectOps.tryField mapping.Field project with
            | None ->
                [], [ Finding.create "appearance.mapping.field-missing" Warning AppearanceRule target
                          (sprintf "Mapping '%s' reads field '%s', which is not defined." mapping.Name (Id.value mapping.Field)) ]
            | Some field when not (derivationAllowed scope field) -> [], []
            | Some field ->
                match mappingOutcome mapping (ProjectOps.resolveField field reference project) with
                | None -> [], []
                | Some(UseAppearance appearance, legend) -> [ MappingLayer(mapping.Id, mapping.Field, legend), appearance ], []
                | Some(UseStyle styleId, legend) ->
                    match styleAppearance project target styleId with
                    | Ok style -> [ MappingLayer(mapping.Id, mapping.Field, legend), style.Appearance ], []
                    | Error finding -> [], [ finding ])
        // Earlier mappings take precedence, so they are applied last.
        |> List.rev
        |> fun results -> List.collect fst results, List.collect snd results

    let private pick (get: Appearance -> 'T option) (layers: (AppearanceLayer * Appearance) list) =
        layers
        |> List.fold
            (fun current (layer, appearance) ->
                match get appearance with
                | Some value -> { Value = Some value; Layer = layer }
                | None -> current)
            { Value = None; Layer = FormaDefaultLayer }

    let private mapColor project (effective: Effective<ColorRef>) =
        { Value = effective.Value |> Option.map (colorCss project); Layer = effective.Layer }

    /// Contributing layers from lowest to highest precedence.
    let layers scope (project: Project) reference =
        let target = ObjectRef.describe reference
        match ProjectOps.appearanceOf reference project with
        | None -> [], []
        | Some objectAppearance ->
            let style, styleFindings =
                match objectAppearance.Style with
                | None -> [], []
                | Some id ->
                    match styleAppearance project target id with
                    | Ok s -> [ StyleLayer(s.Id, s.Revision), s.Appearance ], []
                    | Error finding -> [], [ finding ]
            let mapped, mappingFindings = mappingLayers scope project reference target
            profileLayer project reference @ style @ mapped @ [ OverrideLayer, objectAppearance.Overrides ],
            styleFindings @ mappingFindings

    let resolve scope (project: Project) reference : EffectiveAppearance * Finding list =
        let contributing, findings = layers scope project reference
        let target = ObjectRef.describe reference
        let color get = pick get contributing |> mapColor project
        let effective =
            { Fill = color _.Fill
              Stroke = color _.Stroke
              Accent = color _.Accent
              Foreground = color _.Foreground
              ConnectorStroke = color _.ConnectorStroke
              ConnectorWidth = pick _.ConnectorWidth contributing
              Line = pick _.Line contributing
              Shape = pick _.Shape contributing }

        let unresolved =
            [ effective.Fill; effective.Stroke; effective.Accent; effective.Foreground; effective.ConnectorStroke ]
            |> List.choose _.Value
            |> List.filter (fun c -> c.Css.IsNone)
            |> List.map (fun c ->
                Finding.create "appearance.palette.missing" Warning AppearanceRule target
                    (sprintf "Palette slot '%s' is missing; the Forma default is shown instead of guessing a color." (c.Palette |> Option.map Id.value |> Option.defaultValue "?")))

        let hexOf (effective: Effective<ColorResolution>) =
            effective.Value |> Option.bind _.Css |> Option.filter (fun css -> css.StartsWith "#") |> Option.bind (HexColor.parse >> Result.toOption)

        let contrast =
            match hexOf effective.Fill with
            | None -> []
            | Some fill ->
                let foreground =
                    match effective.Foreground.Value with
                    | None -> HexColor.parse defaultForegroundHex |> Result.toOption
                    | Some _ -> hexOf effective.Foreground
                match foreground with
                | Some fg when HexColor.contrast fill fg < 4.5 ->
                    [ Finding.create "appearance.contrast" Warning AppearanceRule target
                          (sprintf "Text contrast against fill %s is %.2f:1, below 4.5:1." (HexColor.value fill) (HexColor.contrast fill fg)) ]
                | _ -> []

        effective, findings @ unresolved @ contrast

    let layerName layer =
        match layer with
        | FormaDefaultLayer -> "forma-default"
        | ProfileDefaultLayer _ -> "profile-default"
        | StyleLayer _ -> "named-style"
        | MappingLayer _ -> "metadata-mapping"
        | OverrideLayer -> "object-override"
