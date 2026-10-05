namespace FormaStudio.Engine

/// Editor session and view state. Immutable; the WebAssembly glue holds the
/// single current-state cell. Every canonical change goes through
/// Editor.dispatch, so pointer, keyboard, Structure view and inspector input
/// share one command path (FDA-069, FDA-221).
type EdgeEnd =
    | SourceEnd
    | TargetEnd

type PendingGesture =
    | NoPending
    | Connecting of NodeId
    /// Waiting for the node that one end of a connector should move to.
    | Reconnecting of EdgeId * EdgeEnd

/// Unsubmitted form input. Drafts are view state: they never enter the project
/// until a create command succeeds.
type Drafts =
    { FieldName: string
      FieldType: string
      FieldOptions: string
      FieldScope: FieldScopeChoice
      SlotName: string
      SlotColor: string
      MappingValue: string option
      MappingSlot: string option }

type EditorState =
    { Session: Session
      Diagram: DiagramId
      Pending: PendingGesture
      AddKind: string
      Field: FieldKey option
      Status: string
      Drafts: Drafts
      /// The Layout page being edited, if any. Layout and Flow share this session,
      /// its history and its command path; only the visible surface differs.
      Page: PageId option
      /// Canvas zoom in percent and grid snapping: view preferences that never
      /// enter the project document or its history (FDA-027).
      Zoom: int
      Snap: bool
      /// Copied nodes with their dependency closure; view state until inserted.
      Clipboard: DiagramFragment option
      /// The template (built-in key or "clipboard") chosen for review.
      Template: string option
      /// The project as last saved or opened; the review compares against it.
      Baseline: Project
      /// The project a pending save wrote, adopted as the baseline on success.
      Saving: Project option
      /// A saved copy that changed since the baseline, under merge review.
      Incoming: Project option
      /// Item conflicts the reviewer resolved in favour of the saved copy.
      TakeSaved: Set<string>
      /// Portable workflow documents, edited in the public Forma workflow component.
      Workflows: WorkflowLibrary
      /// The Workflows surface is showing (instead of the Flow diagram or a Layout page).
      WorkflowVisible: bool
      /// HTML export settings and the last result (view state, never in the project).
      Export: ExportView
      /// Where new Layout components go: a container on the open page, or its root stack.
      LayoutTarget: string option }

/// The HTML export panel: what was exported last and how.
and ExportView =
    { Target: HtmlTarget
      Brand: string option
      InteractiveWorkflow: bool
      Text: string
      FileName: string
      Omitted: string list
      Summary: string }

[<RequireQualifiedAccess>]
module EditorState =
    /// Canvas zoom steps in percent.
    let zoomLevels = [ 50; 75; 100; 125; 150; 200 ]

    let initial (project: Project) (diagram: DiagramId) =
        { Session = Editor.start project
          Diagram = diagram
          Pending = NoPending
          AddKind = "activity"
          Field = None
          Status = "Ready."
          Drafts =
            { FieldName = ""; FieldType = "choice"; FieldOptions = ""; FieldScope = PrintedAndExported
              SlotName = ""; SlotColor = ""; MappingValue = None; MappingSlot = None }
          Page = None
          Zoom = 100
          Snap = false
          Clipboard = None
          Template = None
          Baseline = project
          Saving = None
          Incoming = None
          TakeSaved = Set.empty
          Workflows = WorkflowLibrary.empty
          WorkflowVisible = false
          Export = { Target = HtmlFragment; Brand = None; InteractiveWorkflow = false; Text = ""; FileName = "export.html"; Omitted = []; Summary = "Nothing exported yet." }
          LayoutTarget = None }

    let project (state: EditorState) = state.Session.Project
    let diagram (state: EditorState) = ProjectOps.tryDiagram state.Diagram (project state)
    let profile (state: EditorState) = diagram state |> Option.bind (fun d -> Profiles.tryFind d.Profile)
    let selectedRef (state: EditorState) = state.Session.Selection |> List.tryHead

    let selectedNode (state: EditorState) =
        match selectedRef state with
        | Some(NodeRef(_, n)) -> ProjectOps.tryNode state.Diagram n (project state)
        | _ -> None

    /// Nodes in the selection, in selection order.
    let selectedNodes (state: EditorState) =
        state.Session.Selection
        |> List.choose (function NodeRef(_, n) -> ProjectOps.tryNode state.Diagram n (project state) | _ -> None)

    let openPage (state: EditorState) = state.Page |> Option.bind (fun id -> ProjectOps.tryPage id (project state))

    /// Built-in templates for the diagram's profile, after copied items if any:
    /// (key, name, description, fragment).
    let templates (state: EditorState) =
        let builtIn =
            diagram state |> Option.map (fun d -> Templates.forProfile d.Profile) |> Option.defaultValue []
            |> List.map (fun t -> t.Key, t.Name, t.Description, t.Fragment)
        let copied =
            state.Clipboard
            |> Option.map (fun f -> "clipboard", "Copied items", sprintf "%d item(s) and %d connector(s) copied from this project." f.Nodes.Length f.Edges.Length, f)
            |> Option.toList
        copied @ builtIn

    let chosenTemplate (state: EditorState) =
        state.Template |> Option.bind (fun key -> templates state |> List.tryFind (fun (k, _, _, _) -> k = key))
