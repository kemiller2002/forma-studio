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
