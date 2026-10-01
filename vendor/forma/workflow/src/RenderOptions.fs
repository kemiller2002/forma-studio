namespace Forma.Workflow

open System

/// Generated connective text. English by default; hosts localize by supplying
/// their own phrases. Workflow content itself is never translated.
type Phrases =
    { NodeKind: CoreNodeKind -> string
      EdgeKind: CoreEdgeKind -> string
      State: CoreState -> string
      GroupKind: GroupKind -> string
      Status: string
      Reference: string
      Relationships: string
      Membership: string
      Legend: string
      CanvasLabel: string -> string
      /// source, target -> sentence stem
      Leads: string -> string -> string
      LeadsBack: string -> string -> string
      Mutual: string -> string -> string
      Associated: string -> string -> string
      Labelled: string -> string
      ViaPorts: string option -> string option -> string
      Line: LineStyle -> string
      NoMembers: string }

[<RequireQualifiedAccess>]
module Phrases =
    let english =
        { NodeKind =
            function
            | Start -> "Start"
            | End -> "End"
            | Task -> "Task"
            | Decision -> "Decision"
            | Merge -> "Merge"
            | Event -> "Event"
            | Subprocess -> "Subprocess"
            | Data -> "Data"
            | Note -> "Note"
            | External -> "External"
          EdgeKind =
            function
            | Sequence -> "sequence"
            | Conditional -> "conditional"
            | DefaultFlow -> "default path"
            | ExceptionFlow -> "exception path"
            | Message -> "message"
            | Association -> "association"
            | DataFlow -> "data flow"
          State =
            function
            | NoState -> "No status"
            | Pending -> "Pending"
            | Ready -> "Ready"
            | Active -> "Active"
            | Waiting -> "Waiting"
            | Complete -> "Complete"
            | Blocked -> "Blocked"
            | Failed -> "Failed"
            | Skipped -> "Skipped"
            | Cancelled -> "Cancelled"
            | Unknown -> "Unknown"
          GroupKind =
            function
            | PlainGroup -> "Group"
            | Container -> "Container"
            | Swimlane -> "Lane"
            | Phase -> "Phase"
          Status = "Status"
          Reference = "Reference"
          Relationships = "Relationships"
          Membership = "Groups and lanes"
          Legend = "Key"
          CanvasLabel = fun title -> $"{title} diagram, scroll to see all items"
          Leads = fun a b -> $"{a} leads to {b}"
          LeadsBack = fun a b -> $"{b} leads to {a}"
          Mutual = fun a b -> $"{a} and {b} lead to each other"
          Associated = fun a b -> $"{a} is associated with {b}"
          Labelled = fun l -> $"“{l}”"
          ViaPorts =
            fun s t ->
                match s, t with
                | Some a, Some b -> $"from port {a} to port {b}"
                | Some a, None -> $"from port {a}"
                | None, Some b -> $"to port {b}"
                | None, None -> ""
          Line =
            function
            | Solid -> "solid line"
            | Dashed -> "dashed line"
            | Dotted -> "dotted line"
          NoMembers = "no members" }

/// How a workflow is rendered.
type RenderOptions =
    { /// Prefix for every generated element id; defaults to "wf-<workflow id>".
      IdPrefix: string option
      /// Heading level for node labels (2-6).
      HeadingLevel: int
      /// Metadata keys to show visibly on nodes, in this order. Default: none
      /// (metadata stays machine-readable in the workflow file, FMD-META-006/007).
      VisibleMetadata: string list
      /// Where a navigate-to-workflow intent links, if anywhere.
      WorkflowHref: (WorkflowId -> string) option
      Phrases: Phrases }

[<RequireQualifiedAccess>]
module RenderOptions =
    let defaults =
        { IdPrefix = None
          HeadingLevel = 3
          VisibleMetadata = []
          WorkflowHref = None
          Phrases = Phrases.english }

/// A declared external requirement of rendered output.
type Dependency =
    /// A stylesheet from a public package (package name, version, path inside it).
    | Stylesheet of package: string * version: string * path: string
    /// A public ES module needed only for requested interactivity.
    | RuntimeModule of package: string * version: string * path: string

type RenderedHtml =
    { Html: string
      Dependencies: Dependency list }
