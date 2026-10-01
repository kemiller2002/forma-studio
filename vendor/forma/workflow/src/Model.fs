namespace Forma.Workflow

open System
open System.Text.RegularExpressions

/// Validated identifiers and names. Constructors are private so a value of these
/// types is always well formed (the schema patterns, enforced in F#).
[<AutoOpen>]
module Identifiers =
    let private matches (pattern: string) (s: string) = not (isNull s) && Regex.IsMatch(s, pattern, RegexOptions.CultureInvariant)

    [<Struct; CustomEquality; CustomComparison>]
    type WorkflowId =
        private | WorkflowId of string
        member this.Value = let (WorkflowId v) = this in v
        override this.ToString() = this.Value
        override this.Equals(o) = match o with :? WorkflowId as w -> w.Value = this.Value | _ -> false
        override this.GetHashCode() = this.Value.GetHashCode()
        interface IComparable with
            member this.CompareTo(o) = match o with :? WorkflowId as w -> String.CompareOrdinal(this.Value, w.Value) | _ -> 1

    [<Struct; CustomEquality; CustomComparison>]
    type ObjectId =
        private | ObjectId of string
        member this.Value = let (ObjectId v) = this in v
        override this.ToString() = this.Value
        override this.Equals(o) = match o with :? ObjectId as w -> w.Value = this.Value | _ -> false
        override this.GetHashCode() = this.Value.GetHashCode()
        interface IComparable with
            member this.CompareTo(o) = match o with :? ObjectId as w -> String.CompareOrdinal(this.Value, w.Value) | _ -> 1

    /// Unique within an owner: a port within its node, a reference within its object.
    [<Struct; CustomEquality; CustomComparison>]
    type LocalId =
        private | LocalId of string
        member this.Value = let (LocalId v) = this in v
        override this.ToString() = this.Value
        override this.Equals(o) = match o with :? LocalId as w -> w.Value = this.Value | _ -> false
        override this.GetHashCode() = this.Value.GetHashCode()
        interface IComparable with
            member this.CompareTo(o) = match o with :? LocalId as w -> String.CompareOrdinal(this.Value, w.Value) | _ -> 1

    /// Reverse-DNS namespace such as com.example.erp.
    [<Struct; CustomEquality; CustomComparison>]
    type Namespace =
        private | Namespace of string
        member this.Value = let (Namespace v) = this in v
        override this.ToString() = this.Value
        override this.Equals(o) = match o with :? Namespace as w -> w.Value = this.Value | _ -> false
        override this.GetHashCode() = this.Value.GetHashCode()
        interface IComparable with
            member this.CompareTo(o) = match o with :? Namespace as w -> String.CompareOrdinal(this.Value, w.Value) | _ -> 1

    /// <namespace>:<name>, used for producer kinds, states and commands.
    type QualifiedName =
        { Namespace: Namespace
          Name: string }
        member this.Value = this.Namespace.Value + ":" + this.Name

    let workflowIdPattern = "^[a-z0-9](?:[a-z0-9-]{0,126}[a-z0-9])?$"
    let objectIdPattern = "^[A-Za-z0-9][A-Za-z0-9_.:-]{0,127}$"
    let namespacePattern = "^[a-z][a-z0-9-]*(?:\\.[a-z][a-z0-9-]*)+$"
    let private qualifiedPattern = "^([a-z][a-z0-9-]*(?:\\.[a-z][a-z0-9-]*)+):([a-z][a-z0-9-]{0,63})$"

    [<RequireQualifiedAccess>]
    module WorkflowId =
        let tryCreate (s: string) = if matches workflowIdPattern s then Some(WorkflowId s) else None
        let create s = tryCreate s |> Option.defaultWith (fun () -> invalidArg "s" $"invalid workflow id \"{s}\"")

    [<RequireQualifiedAccess>]
    module ObjectId =
        let tryCreate (s: string) = if matches objectIdPattern s then Some(ObjectId s) else None
        let create s = tryCreate s |> Option.defaultWith (fun () -> invalidArg "s" $"invalid object id \"{s}\"")

    [<RequireQualifiedAccess>]
    module LocalId =
        let tryCreate (s: string) = if matches objectIdPattern s then Some(LocalId s) else None
        let create s = tryCreate s |> Option.defaultWith (fun () -> invalidArg "s" $"invalid local id \"{s}\"")

    [<RequireQualifiedAccess>]
    module Namespace =
        let tryCreate (s: string) = if matches namespacePattern s && s.Length <= 128 then Some(Namespace s) else None
        let create s = tryCreate s |> Option.defaultWith (fun () -> invalidArg "s" $"invalid namespace \"{s}\"")

    [<RequireQualifiedAccess>]
    module QualifiedName =
        let tryParse (s: string) =
            if isNull s then None
            else
                let m = Regex.Match(s, qualifiedPattern, RegexOptions.CultureInvariant)
                if m.Success then Namespace.tryCreate m.Groups[1].Value |> Option.map (fun ns -> { Namespace = ns; Name = m.Groups[2].Value })
                else None

/// A semantic version (format version or Forma release).
[<CustomEquality; CustomComparison>]
type SemanticVersion =
    { Major: int
      Minor: int
      Patch: int
      Pre: string option }
    override this.ToString() =
        $"{this.Major}.{this.Minor}.{this.Patch}" + (this.Pre |> Option.map ((+) "-") |> Option.defaultValue "")
    override this.Equals(o) = match o with :? SemanticVersion as v -> v.ToString() = this.ToString() | _ -> false
    override this.GetHashCode() = this.ToString().GetHashCode()
    interface IComparable with
        member this.CompareTo(o) =
            match o with
            | :? SemanticVersion as v ->
                let core = compare (this.Major, this.Minor, this.Patch) (v.Major, v.Minor, v.Patch)
                if core <> 0 then core
                else
                    match this.Pre, v.Pre with
                    | None, None -> 0
                    | None, Some _ -> 1 // a release sorts after its pre-releases
                    | Some _, None -> -1
                    | Some a, Some b -> String.CompareOrdinal(a, b)
            | _ -> 1

[<RequireQualifiedAccess>]
module SemanticVersion =
    let tryParse (s: string) =
        if isNull s then None
        else
            let m = Regex.Match(s, "^(0|[1-9][0-9]{0,8})\\.(0|[1-9][0-9]{0,8})\\.(0|[1-9][0-9]{0,8})(?:-([0-9A-Za-z.-]{1,64}))?$")
            if not m.Success then None
            else
                Some
                    { Major = int m.Groups[1].Value
                      Minor = int m.Groups[2].Value
                      Patch = int m.Groups[3].Value
                      Pre = if m.Groups[4].Success then Some m.Groups[4].Value else None }

    let create s = tryParse s |> Option.defaultWith (fun () -> invalidArg "s" $"invalid version \"{s}\"")

// ---------------------------------------------------------------------------
// Appearance
// ---------------------------------------------------------------------------

/// The Forma colour tokens a workflow may reference (contracts/diagram-presentation.json).
type ColorToken =
    | AccentPrimary
    | AccentSecondary
    | BorderFunctional
    | BorderSubtle
    | SurfacePrimary
    | SurfaceSecondary
    | SurfaceInverse
    | TextPrimary
    | TextSecondary

/// Lowercase #rrggbb.
[<Struct>]
type HexColor =
    private | HexColor of string
    member this.Value = let (HexColor v) = this in v

[<RequireQualifiedAccess>]
module HexColor =
    let tryCreate (s: string) = if not (isNull s) && Regex.IsMatch(s, "^#[0-9a-f]{6}$") then Some(HexColor s) else None
    let create s = tryCreate s |> Option.defaultWith (fun () -> invalidArg "s" $"invalid colour \"{s}\"")

    /// WCAG relative luminance.
    let luminance (HexColor s) =
        let channel (i: int) =
            let c = float (Convert.ToInt32(s.Substring(i, 2), 16)) / 255.0
            if c <= 0.04045 then c / 12.92 else Math.Pow((c + 0.055) / 1.055, 2.4)
        0.2126 * channel 1 + 0.7152 * channel 3 + 0.0722 * channel 5

    let contrast a b =
        let la, lb = luminance a, luminance b
        (max la lb + 0.05) / (min la lb + 0.05)

/// A palette slot name (palette:<slot>).
[<Struct>]
type SlotName =
    private | SlotName of string
    member this.Value = let (SlotName v) = this in v

[<RequireQualifiedAccess>]
module SlotName =
    let tryCreate (s: string) = if not (isNull s) && Regex.IsMatch(s, "^[a-z][a-z0-9-]{0,63}$") then Some(SlotName s) else None
    let create s = tryCreate s |> Option.defaultWith (fun () -> invalidArg "s" $"invalid palette slot \"{s}\"")

/// What a palette slot holds.
type ColorSource =
    | TokenSource of ColorToken
    | LiteralSource of HexColor

/// What an object refers to.
type ColorValue =
    | TokenColor of ColorToken
    | LiteralColor of HexColor
    | PaletteColor of SlotName

type ObjectColor =
    { Fill: ColorValue option
      Stroke: ColorValue option
      Accent: ColorValue option
      Foreground: ColorValue option }

[<RequireQualifiedAccess>]
module ObjectColor =
    let none = { Fill = None; Stroke = None; Accent = None; Foreground = None }

/// Forma shape vocabulary. Presentation only.
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

// ---------------------------------------------------------------------------
// Semantics
// ---------------------------------------------------------------------------

type CoreNodeKind =
    | Start
    | End
    | Task
    | Decision
    | Merge
    | Event
    | Subprocess
    | Data
    | Note
    | External

/// A core kind may carry a display override; a producer kind must carry its label.
type NodeKind =
    | CoreNode of CoreNodeKind * label: string option
    | CustomNode of QualifiedName * label: string

type CoreEdgeKind =
    | Sequence
    | Conditional
    | DefaultFlow
    | ExceptionFlow
    | Message
    | Association
    | DataFlow

type EdgeKind =
    | CoreEdge of CoreEdgeKind * label: string option
    | CustomEdge of QualifiedName * label: string

type GroupKind =
    | PlainGroup
    | Container
    | Swimlane
    | Phase

type CoreState =
    | NoState
    | Pending
    | Ready
    | Active
    | Waiting
    | Complete
    | Blocked
    | Failed
    | Skipped
    | Cancelled
    | Unknown

type State =
    | CoreStatus of CoreState * label: string option
    | CustomStatus of QualifiedName * label: string

type Status =
    { State: State
      Detail: string option
      UpdatedAt: string option }

/// Ordered JSON object members that Forma preserves and never interprets.
type Metadata = (string * Json) list

/// Namespaced producer data. Each value is a JSON object.
type Extensions = (Namespace * (string * Json) list) list

type Accessibility =
    { Name: string option
      Description: string option }

[<RequireQualifiedAccess>]
module Accessibility =
    let none = { Name = None; Description = None }

type PortSide =
    | Top
    | Right
    | Bottom
    | Left

type PortDirection =
    | In
    | Out
    | InOut

type Port =
    { Id: LocalId
      Side: PortSide option
      Direction: PortDirection option
      Label: string option
      MaxConnections: int option
      Metadata: Metadata option
      Extensions: Extensions }

/// A typed pointer to a host/domain object. Displayed, never dereferenced.
type Reference =
    { Id: LocalId
      System: Namespace
      Type: string option
      Key: string
      Label: string option
      Href: string option
      Metadata: Metadata option }

type InteractionTarget =
    | ToWorkflow of WorkflowId
    | ToNode of ObjectId
    | ToEdge of ObjectId
    | ToGroup of ObjectId
    | ToReference of LocalId
    | ToUrl of string

/// Declarative intents. The host supplies behavior; nothing here executes.
type Interaction =
    | NoInteraction
    | SelectIntent of label: string option
    | InspectIntent of label: string option
    | Navigate of InteractionTarget * label: string option
    | Open of InteractionTarget * label: string option
    | Command of QualifiedName * label: string * parameters: (string * Json) list option

type Point = { X: float; Y: float }

type SizeHint =
    | CompactSize
    | StandardSize
    | WideSize

/// Presentation geometry and layout intent. A position is both coordinates or none.
type BoxLayout =
    { Position: Point option
      Width: float option
      Height: float option
      Rank: int option
      Order: int option
      Size: SizeHint option }

[<RequireQualifiedAccess>]
module BoxLayout =
    let empty = { Position = None; Width = None; Height = None; Rank = None; Order = None; Size = None }

type Routing =
    | Straight
    | Orthogonal

type EdgeLayout =
    { Routing: Routing option
      Waypoints: Point list
      LabelAt: Point option }

[<RequireQualifiedAccess>]
module EdgeLayout =
    let empty = { Routing = None; Waypoints = []; LabelAt = None }

type Node =
    { Id: ObjectId
      Kind: NodeKind
      Label: string
      Description: string option
      Variant: Shape option
      Color: ObjectColor
      Status: Status option
      Ports: Port list
      References: Reference list
      Metadata: Metadata option
      Accessibility: Accessibility
      Interaction: Interaction option
      Layout: BoxLayout
      Extensions: Extensions }

type Endpoint = { Node: ObjectId; Port: LocalId option }

type EdgeDirection =
    | Forward
    | Backward
    | Both
    | Undirected

type Edge =
    { Id: ObjectId
      Source: Endpoint
      Target: Endpoint
      Direction: EdgeDirection option
      Kind: EdgeKind option
      Label: string option
      Description: string option
      Line: LineStyle option
      Width: int option
      Stroke: ColorValue option
      Status: Status option
      References: Reference list
      Metadata: Metadata option
      Accessibility: Accessibility
      Interaction: Interaction option
      Layout: EdgeLayout
      Extensions: Extensions }

type Group =
    { Id: ObjectId
      Kind: GroupKind
      Label: string
      Description: string option
      Members: ObjectId list
      Parent: ObjectId option
      Line: LineStyle option
      Color: ObjectColor
      Status: Status option
      References: Reference list
      Metadata: Metadata option
      Accessibility: Accessibility
      Interaction: Interaction option
      Layout: BoxLayout
      Extensions: Extensions }

type FormaTarget =
    { Version: SemanticVersion
      Requires: string list }

type FlowDirection =
    | FlowRight
    | FlowDown

type LayoutMode =
    | Authored
    | Auto

type WorkflowLayout =
    { Direction: FlowDirection option
      Mode: LayoutMode option
      Canvas: (float * float) option }

[<RequireQualifiedAccess>]
module WorkflowLayout =
    let empty = { Direction = None; Mode = None; Canvas = None }

type Density =
    | Comfortable
    | Compact

type LegendEntry =
    { Id: LocalId
      Label: string
      Description: string option
      Color: ObjectColor
      Line: LineStyle option }

type Presentation =
    { Density: Density option
      Legend: LegendEntry list
      LegendTitle: string option }

[<RequireQualifiedAccess>]
module Presentation =
    let empty = { Density = None; Legend = []; LegendTitle = None }

type Producer =
    { Name: string
      Version: string option
      Uri: string option }

type Provenance =
    { Producer: Producer option
      CreatedAt: string option
      ModifiedAt: string option
      Source: string option
      ModifiedBy: (string * string option) option }

type TextDirection =
    | Ltr
    | Rtl
    | AutoDirection

/// A portable workflow document (format 1.x).
type Workflow =
    { Schema: string option
      FormatVersion: SemanticVersion
      Id: WorkflowId
      Title: string
      Description: string option
      Language: string option
      TextDirection: TextDirection option
      Forma: FormaTarget
      Metadata: Metadata option
      Palette: (SlotName * ColorSource) list
      Nodes: Node list
      Edges: Edge list
      Groups: Group list
      Layout: WorkflowLayout
      Presentation: Presentation
      Extensions: Extensions
      Provenance: Provenance option }

[<RequireQualifiedAccess>]
module Format =
    /// The format identifier every document declares.
    let identifier = "forma-workflow"
    /// The format version this library reads and writes.
    let current = SemanticVersion.create "1.0.0"
    /// The canonical file suffix.
    let suffix = ".forma-workflow.json"
    /// Stable public URL of the schema for this format version.
    let schemaUri = "https://forma.echelonfoundry.com/schemas/workflow/1.0/forma-workflow.schema.json"
    /// The repository convention for workflow files.
    let repositoryPath (id: WorkflowId) = "forma/workflows/" + id.Value + suffix

/// Host modes of the embeddable renderer/editor (PORTABLE-WORKFLOW-INTERCHANGE).
type HostMode =
    /// Read-only display.
    | ViewMode
    /// Read-only with selection and an inspector for metadata, references and status.
    | InspectMode
    /// Full editing through validated commands.
    | EditMode
    /// The user picks objects; the host receives the selection.
    | PickMode
    /// Read-only; the host projects runtime state (status) onto the workflow.
    | RuntimeMode

[<RequireQualifiedAccess>]
module HostMode =
    let all = [ "view", ViewMode; "inspect", InspectMode; "edit", EditMode; "pick", PickMode; "runtime", RuntimeMode ]
    let text (m: HostMode) = all |> List.find (snd >> (=) m) |> fst
    let tryParse (s: string) = all |> List.tryFind (fst >> (=) s) |> Option.map snd
