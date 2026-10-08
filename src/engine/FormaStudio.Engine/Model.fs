namespace FormaStudio.Engine

// ---------------------------------------------------------------------------
// Addressable objects
// ---------------------------------------------------------------------------

/// The kinds of addressable object a metadata field, style or mapping may target.
type TargetKind =
    | PageTarget
    | ComponentTarget
    | DiagramTarget
    | NodeTarget
    | EdgeTarget
    | GroupTarget

/// A stable address for one authored object. Selection, commands, findings and
/// exports all refer to objects this way, never to rendered elements.
type ObjectRef =
    | PageRef of PageId
    | ComponentRef of PageId * ComponentNodeId
    | DiagramRef of DiagramId
    | NodeRef of DiagramId * NodeId
    | EdgeRef of DiagramId * EdgeId
    | GroupRef of DiagramId * GroupId

// ---------------------------------------------------------------------------
// Metadata (DOCUMENT-MODEL "Object metadata", FDA-800..928, FDA-1180..1193)
// ---------------------------------------------------------------------------

/// Output scopes a metadata field may reach beyond the editor. The editor itself
/// may always show a value to an authorized author.
type OutputScope =
    | Rendered
    | AgentExport
    | ProvenanceExport

/// Disclosure policy. `DerivedPresentation` separately authorizes appearance
/// derived from the value (color, style, legend category), because a mapped color
/// discloses the value even when the raw text is hidden (FDA-1260..1268).
type Disclosure =
    { Scopes: Set<OutputScope>
      DerivedPresentation: bool }

type EnumOption = { Id: string; Label: string }

type FieldType =
    | TextField of maxLength: int option
    | NumberField of minimum: decimal option * maximum: decimal option
    | BooleanField
    | EnumField of EnumOption list
    | DateTimeField
    | UrlField
    | TagsField of ordered: bool

/// A typed metadata value. Enum values store the stable option id, never the label.
type MetaValue =
    | Text of string
    | Number of decimal
    | Boolean of bool
    | Enum of string
    | DateTime of string
    | Url of string
    | TagList of string list

type SourceBinding =
    { Source: string
      RetrievedAt: string option }

/// What the author stored. Defaults and derived values are never stored: they are
/// computed, so a later default change cannot silently rewrite authored data.
type StoredValue =
    | Explicit of MetaValue
    | SourceBound of MetaValue * SourceBinding
    | UnknownValue
    | UnavailableValue of reason: string

/// An explicit, deterministic derivation from canonical structure.
type Derivation = FromMembership of GroupKindName

and GroupKindName =
    | Group
    | Lane
    | Phase

type FieldOrigin =
    | ProjectLocal
    | ProfileField of profileId: string

type FieldDefinition =
    { Key: FieldKey
      Name: string
      Help: string option
      Type: FieldType
      AppliesTo: Set<TargetKind>
      Disclosure: Disclosure
      Default: MetaValue option
      Required: bool
      Derivation: Derivation option
      Origin: FieldOrigin }

/// Values keyed by stable field key. Keys without a definition are preserved as
/// unknown fields and treated as source-only.
type Metadata = Map<FieldKey, StoredValue>

// ---------------------------------------------------------------------------
// Typed references (FDA-1030..1039)
// ---------------------------------------------------------------------------

type ReferenceTarget =
    | RequirementTarget of requirementId: string
    | PageTargetRef of PageId
    | DiagramTargetRef of DiagramId
    | DiagramElementTargetRef of DiagramId * elementId: string
    | RepositoryTarget of ownerAndName: string
    | IssueTarget of repository: string * number: int
    | ExternalUrlTarget of url: string

type Availability =
    | Unverified
    | Available
    | Unavailable of reason: string

type TypedReference =
    { Id: ReferenceId
      Target: ReferenceTarget
      Label: string option
      Availability: Availability }

// ---------------------------------------------------------------------------
// Appearance (FDA-830..976, FDA-1060..1075, FDA-1200..1220)
// ---------------------------------------------------------------------------

/// A literal color normalized to lowercase `#rrggbb` (solid only, FDA-1070/1071).
type HexColor = private HexColor of string

/// A public Forma color token such as `--ef-color-accent-primary`.
type TokenRef = private TokenRef of string

type ColorRef =
    | TokenColor of TokenRef
    | PaletteColor of PaletteSlotId
    | LiteralColor of HexColor

type PaletteValue =
    | PaletteToken of TokenRef
    | PaletteLiteral of HexColor

type PaletteSlot =
    { Id: PaletteSlotId
      Name: string
      Value: PaletteValue
      Description: string option }

type Shape =
    | Rectangle
    | Rounded
    | Pill
    | Ellipse
    | Diamond

type LineStyle =
    | Solid
    | Dashed
    | Dotted

/// Every appearance property is optional; absence means "inherit from the next
/// lower layer", never "copy the resolved value".
type Appearance =
    { Fill: ColorRef option
      Stroke: ColorRef option
      Accent: ColorRef option
      Foreground: ColorRef option
      ConnectorStroke: ColorRef option
      ConnectorWidth: int option
      Line: LineStyle option
      Shape: Shape option }

type AppearanceProperty =
    | FillProperty
    | StrokeProperty
    | AccentProperty
    | ForegroundProperty
    | ConnectorStrokeProperty
    | ConnectorWidthProperty
    | LineProperty
    | ShapeProperty

type AppearanceStyle =
    { Id: StyleId
      Name: string
      Revision: int
      Targets: Set<TargetKind>
      Appearance: Appearance }

type MatchRule =
    | Equals of string
    | AnyOf of string list

type MappingOutcome =
    | UseStyle of StyleId
    | UseAppearance of Appearance

type MappingRule =
    { Match: MatchRule
      Outcome: MappingOutcome
      Legend: string }

type MappingFallback =
    | NoMapping
    | Apply of MappingOutcome * legend: string

type MappingFallbacks =
    { Missing: MappingFallback
      Unknown: MappingFallback
      Unavailable: MappingFallback
      Invalid: MappingFallback
      Unmapped: MappingFallback }

/// A declarative categorical mapping. Mappings are ordered: an earlier mapping has
/// higher precedence than a later one for the same property (FDA-1214).
type PresentationMapping =
    { Id: MappingId
      Name: string
      Field: FieldKey
      Targets: Set<TargetKind>
      Rules: MappingRule list
      Fallbacks: MappingFallbacks
      Enabled: bool }

type ObjectAppearance =
    { Style: StyleId option
      Overrides: Appearance }

// ---------------------------------------------------------------------------
// Layout (component trees, schema v1 compatible)
// ---------------------------------------------------------------------------

type Annotation =
    { Id: string
      Text: string
      Kind: string option
      Reference: string option }

type ComponentNode =
    { Id: ComponentNodeId
      Component: string
      Properties: Map<string, JsonValue>
      TokenBindings: Map<string, string>
      Content: Map<string, JsonValue>
      Slots: Map<string, ComponentNode list>
      Navigation: JsonValue list
      Annotations: Annotation list
      Metadata: Metadata
      /// Optional Forma icon, by name only (supported components: see `Components`).
      Icon: IconRef option }

type Page =
    { Id: PageId
      Name: string
      Route: string option
      Title: string option
      Description: string option
      Nodes: ComponentNode list
      Annotations: Annotation list
      Metadata: Metadata }

// ---------------------------------------------------------------------------
// Flow (DOCUMENT-MODEL "Diagram", FDA-020..050)
// ---------------------------------------------------------------------------

type PortSide =
    | Top
    | Right
    | Bottom
    | Left

type Port =
    { Id: PortId
      Side: PortSide
      Label: string option }

type Endpoint = { Node: NodeId; Port: PortId option }

/// Automatic routing is recomputed from canonical state; only manual points persist.
type Routing =
    | Straight
    | Orthogonal
    | Manual of Point list

type DiagramNode =
    { Id: NodeId
      Kind: string
      Label: string
      Box: Box
      Ports: Port list
      Locked: bool
      Metadata: Metadata
      Appearance: ObjectAppearance
      References: TypedReference list
      /// Optional Forma icon, by name only; shown and exported only when the pinned release has it.
      Icon: IconRef option }

type DiagramEdge =
    { Id: EdgeId
      Kind: string
      Source: Endpoint
      Target: Endpoint
      Label: string option
      Routing: Routing
      Metadata: Metadata
      Appearance: ObjectAppearance
      References: TypedReference list }

/// Membership is explicit and ordered. Visual overlap never implies membership.
type DiagramGroup =
    { Id: GroupId
      Kind: GroupKindName
      Label: string
      Box: Box
      Members: NodeId list
      Semantic: bool
      Metadata: Metadata
      Appearance: ObjectAppearance }

type ProfileRef = { Id: string; Version: string }

type MissingDisplay =
    | OmitMissing
    | ShowValueState

/// Which metadata fields appear on the canvas, by stable key (FDA-1230..1240).
/// `KindFields` overrides `NodeFields` for specific node kinds, for example to
/// keep start, end and decision shapes compact. Hiding a field never clears it.
type MetadataDisplay =
    { NodeFields: FieldKey list
      KindFields: Map<string, FieldKey list>
      EdgeFields: FieldKey list
      Missing: MissingDisplay }

type Diagram =
    { Id: DiagramId
      Name: string
      Profile: ProfileRef
      Nodes: DiagramNode list
      Edges: DiagramEdge list
      Groups: DiagramGroup list
      Display: MetadataDisplay
      Metadata: Metadata
      References: TypedReference list }

// ---------------------------------------------------------------------------
// Project
// ---------------------------------------------------------------------------

/// The authoritative project document. Pages and diagrams are both optional:
/// page-only, diagram-only and mixed projects are all legal.
type Project =
    { Id: ProjectId
      Name: string
      Description: string option
      FormaVersion: string
      StartPage: PageId option
      Pages: Page list
      Diagrams: Diagram list
      Fields: FieldDefinition list
      Palette: PaletteSlot list
      Styles: AppearanceStyle list
      Mappings: PresentationMapping list
      Scenarios: JsonValue list
      Assets: JsonValue list
      Metadata: Metadata
      LegacyMetadata: JsonValue option }

// ---------------------------------------------------------------------------
// Findings and outcomes
// ---------------------------------------------------------------------------

type Severity =
    | Blocker
    | Warning
    | Advisory

type FindingCategory =
    | Integrity
    | ProfileRule
    | MetadataRule
    | AppearanceRule
    | DisclosureRule
    | ReferenceRule
    | CommandRule

/// A validation finding tied to a stable object address (FDA-160..163, FDA-878).
type Finding =
    { Code: string
      Severity: Severity
      Category: FindingCategory
      Target: string
      Message: string }

/// A consequence the command caller must see, for example a responsibility change
/// when an activity moves between semantic lanes (FDA-102).
type Obligation =
    { Code: string
      Target: string
      Message: string }

// ---------------------------------------------------------------------------
// Smart constructors for the private color types
// ---------------------------------------------------------------------------

[<RequireQualifiedAccess>]
module HexColor =
    let private isHex (c: char) = System.Uri.IsHexDigit c

    /// Accepts `#rgb` or `#rrggbb` in any case and normalizes to lowercase `#rrggbb`.
    /// Alpha, named colors and functional syntax are rejected: the initial contract
    /// is solid color only (FMD-PAINT-001, FDA-1070..1072).
    let parse (raw: string) : Result<HexColor, string> =
        let text = raw.Trim()
        let digits = if text.StartsWith "#" then text.Substring 1 else ""
        if digits.Length = 3 && Seq.forall isHex digits then
            digits |> Seq.collect (fun c -> [ c; c ]) |> Seq.toArray |> System.String |> fun d -> Ok(HexColor("#" + d.ToLowerInvariant()))
        elif digits.Length = 6 && Seq.forall isHex digits then
            Ok(HexColor("#" + digits.ToLowerInvariant()))
        else
            Error(sprintf "'%s' is not a solid color; use #rrggbb" text)

    let value (HexColor text) = text

    /// Relative luminance (WCAG 2.x) for contrast findings.
    let luminance (HexColor text) =
        let channel (offset: int) =
            let c = float (System.Convert.ToInt32(text.Substring(offset, 2), 16)) / 255.0
            if c <= 0.03928 then c / 12.92 else ((c + 0.055) / 1.055) ** 2.4
        0.2126 * channel 1 + 0.7152 * channel 3 + 0.0722 * channel 5

    let contrast a b =
        let la, lb = luminance a, luminance b
        (max la lb + 0.05) / (min la lb + 0.05)

[<RequireQualifiedAccess>]
module TokenRef =
    /// Public Forma color tokens accepted as diagram color sources. Mirrors
    /// `color.tokenReferences` in Forma's `contracts/diagram-presentation.json`
    /// (contract 2.0.0); a test compares this list with the pinned contract copy.
    let allowed =
        set [ "--ef-color-accent-primary"
              "--ef-color-accent-secondary"
              "--ef-color-border-functional"
              "--ef-color-border-subtle"
              "--ef-color-surface-primary"
              "--ef-color-surface-secondary"
              "--ef-color-surface-inverse"
              "--ef-color-text-primary"
              "--ef-color-text-secondary" ]

    let parse (raw: string) : Result<TokenRef, string> =
        if allowed.Contains raw then Ok(TokenRef raw)
        else Error(sprintf "'%s' is not a public Forma diagram color token" raw)

    let value (TokenRef name) = name

[<RequireQualifiedAccess>]
module Appearance =
    let empty =
        { Fill = None
          Stroke = None
          Accent = None
          Foreground = None
          ConnectorStroke = None
          ConnectorWidth = None
          Line = None
          Shape = None }

    let isEmpty appearance = appearance = empty

    let none = { Style = None; Overrides = empty }

    let properties =
        [ FillProperty; StrokeProperty; AccentProperty; ForegroundProperty
          ConnectorStrokeProperty; ConnectorWidthProperty; LineProperty; ShapeProperty ]

    let propertyName property =
        match property with
        | FillProperty -> "fill"
        | StrokeProperty -> "stroke"
        | AccentProperty -> "accent"
        | ForegroundProperty -> "foreground"
        | ConnectorStrokeProperty -> "connectorStroke"
        | ConnectorWidthProperty -> "connectorWidth"
        | LineProperty -> "line"
        | ShapeProperty -> "shape"

    /// Copies one property from `source` into `target`, leaving the rest untouched.
    let copyProperty property (source: Appearance) (target: Appearance) =
        match property with
        | FillProperty -> { target with Fill = source.Fill }
        | StrokeProperty -> { target with Stroke = source.Stroke }
        | AccentProperty -> { target with Accent = source.Accent }
        | ForegroundProperty -> { target with Foreground = source.Foreground }
        | ConnectorStrokeProperty -> { target with ConnectorStroke = source.ConnectorStroke }
        | ConnectorWidthProperty -> { target with ConnectorWidth = source.ConnectorWidth }
        | LineProperty -> { target with Line = source.Line }
        | ShapeProperty -> { target with Shape = source.Shape }

    let isSet property (appearance: Appearance) =
        match property with
        | FillProperty -> appearance.Fill.IsSome
        | StrokeProperty -> appearance.Stroke.IsSome
        | AccentProperty -> appearance.Accent.IsSome
        | ForegroundProperty -> appearance.Foreground.IsSome
        | ConnectorStrokeProperty -> appearance.ConnectorStroke.IsSome
        | ConnectorWidthProperty -> appearance.ConnectorWidth.IsSome
        | LineProperty -> appearance.Line.IsSome
        | ShapeProperty -> appearance.Shape.IsSome

    let colors (appearance: Appearance) =
        [ appearance.Fill; appearance.Stroke; appearance.Accent; appearance.Foreground; appearance.ConnectorStroke ]
        |> List.choose id

[<RequireQualifiedAccess>]
module ObjectRef =
    let describe reference =
        match reference with
        | PageRef page -> sprintf "page:%s" (Id.value page)
        | ComponentRef(page, node) -> sprintf "page:%s/component:%s" (Id.value page) (Id.value node)
        | DiagramRef diagram -> sprintf "diagram:%s" (Id.value diagram)
        | NodeRef(diagram, node) -> sprintf "diagram:%s/node:%s" (Id.value diagram) (Id.value node)
        | EdgeRef(diagram, edge) -> sprintf "diagram:%s/edge:%s" (Id.value diagram) (Id.value edge)
        | GroupRef(diagram, group) -> sprintf "diagram:%s/group:%s" (Id.value diagram) (Id.value group)

    let kind reference =
        match reference with
        | PageRef _ -> PageTarget
        | ComponentRef _ -> ComponentTarget
        | DiagramRef _ -> DiagramTarget
        | NodeRef _ -> NodeTarget
        | EdgeRef _ -> EdgeTarget
        | GroupRef _ -> GroupTarget

[<RequireQualifiedAccess>]
module Finding =
    let create code severity category target message =
        { Code = code; Severity = severity; Category = category; Target = target; Message = message }

    let blocker code category target message = create code Blocker category target message

    let isBlocker (finding: Finding) = finding.Severity = Blocker
