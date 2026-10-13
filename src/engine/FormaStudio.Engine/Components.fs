namespace FormaStudio.Engine

/// The Layout component contracts Studio can author. Each mirrors a public Forma
/// contract; Studio never invents properties the contract lacks (DOCUMENT-MODEL
/// "Component node"). Components outside this list are preserved but not editable.
type ComponentContract =
    { Id: string
      Source: string
      Slots: string list
      Properties: (string * (JsonValue -> Result<unit, string>)) list
      Content: string list }

[<RequireQualifiedAccess>]
module Components =
    let private enumProperty (allowed: string list) value =
        match value with
        | JString s when List.contains s allowed -> Ok()
        | _ -> Error(sprintf "must be one of %s" (System.String.Join(", ", allowed)))

    let private intRange lo hi value =
        match value with
        | JNumber text ->
            match System.Int32.TryParse text with
            | true, n when n >= lo && n <= hi -> Ok()
            | _ -> Error(sprintf "must be a whole number from %d to %d" lo hi)
        | _ -> Error(sprintf "must be a whole number from %d to %d" lo hi)

    let densities = [ "relaxed"; "standard"; "compact"; "analytical" ]
    let buttonTypes = [ "button"; "submit" ]
    let inputTypes = [ "text"; "email"; "number"; "date"; "tel"; "url"; "search" ]

    /// Forma `ef-stack`: vertical composition. `density` is the public spacing hook
    /// (relaxed, standard, compact, analytical); Layout never stores x/y.
    let stack =
        { Id = "stack"
          Source = "forma:patterns/stack.html"
          Slots = [ "children" ]
          Properties = [ "density", enumProperty densities ]
          Content = [] }

    /// A native HTML heading styled by Forma foundations.
    let heading =
        { Id = "heading"
          Source = "html:h1-h6 with forma foundations"
          Slots = []
          Properties = [ "level", intRange 1 6 ]
          Content = [ "text" ] }

    let private boolProperty value =
        match value with
        | JBool _ -> Ok()
        | _ -> Error "must be true or false"

    let private idProperty value =
        match value with
        | JString s when System.Text.RegularExpressions.Regex.IsMatch(s, "^[a-z0-9](?:[a-z0-9-]{0,126}[a-z0-9])?$") -> Ok()
        | _ -> Error "must be a workflow id (lowercase words joined by hyphens)"

    /// A paragraph styled by Forma foundations.
    let text = { Id = "text"; Source = "html:p with forma foundations"; Slots = []; Properties = []; Content = [ "text" ] }

    /// A semantic icon identifier is document data, not arbitrary SVG markup.
    /// Unknown future Forma IDs remain valid inert data through persistence and
    /// are resolved against the application's pinned registry at render time.
    let private iconIdProperty value =
        match value with
        | JString id when IconName.parse id |> Result.isOk -> Ok()
        | _ -> Error "icon must be a stable lowercase Forma identifier (not SVG markup or a URL)"

    /// Native button (forma:patterns/button.html).
    let button =
        { Id = "button"; Source = "forma:patterns/button.html"; Slots = []; Properties = [ "type", enumProperty buttonTypes; "icon", iconIdProperty ]; Content = [ "label" ] }

    /// A link that looks like a button (`a.ef-button`, forma:patterns/button.html).
    let linkButton = { Id = "link-button"; Source = "forma:patterns/button.html"; Slots = []; Properties = [ "icon", iconIdProperty ]; Content = [ "label"; "href" ] }

    /// Labelled native input (forma:patterns/text-field.html).
    let textField =
        { Id = "text-field"
          Source = "forma:patterns/text-field.html"
          Slots = []
          Properties = [ "inputType", enumProperty inputTypes; "required", boolProperty ]
          Content = [ "label"; "description"; "name" ] }

    /// The edges a container may declare its direct children share (Visual Engineering
    /// UI-LAYOUT-INTERACTION@1.0.0 ULI-L01/ULI-L10). A declaration, not a layout
    /// capability: geometry stays Forma's, and the export carries the declaration
    /// (`data-uli-align-*`) so a browser check can measure it instead of guessing peers.
    let alignmentEdges = [ "top"; "bottom"; "center" ]

    /// Row of actions (`.ef-actions`, forma:patterns/button.html).
    let actions = { Id = "actions"; Source = "forma:patterns/button.html"; Slots = [ "children" ]; Properties = [ "alignment", enumProperty alignmentEdges ]; Content = [] }

    /// Titled surface section (forma:patterns/surface.html).
    let surface = { Id = "surface"; Source = "forma:patterns/surface.html"; Slots = [ "children" ]; Properties = []; Content = [ "title" ] }

    /// Responsive grid that reflows to one column (forma:patterns/responsive-grid.html).
    /// With `alignment` it is a declared form row: its direct children are peers.
    let responsiveGrid =
        { Id = "responsive-grid"; Source = "forma:patterns/responsive-grid.html"; Slots = [ "children" ]; Properties = [ "alignment", enumProperty alignmentEdges ]; Content = [ "label" ] }

    /// Status alert (forma:patterns/alert.html).
    let alert = { Id = "alert"; Source = "forma:patterns/alert.html"; Slots = []; Properties = [ "icon", iconIdProperty ]; Content = [ "title"; "text" ] }

    /// Metric with label and context (forma:patterns/metric-card.html).
    let metricCard = { Id = "metric-card"; Source = "forma:patterns/metric-card.html"; Slots = []; Properties = [ "icon", iconIdProperty ]; Content = [ "label"; "value"; "context" ] }

    /// A portable workflow rendered by Forma (forma:workflow, .ef-diagram contract 2.1.0).
    let workflow = { Id = "workflow"; Source = "forma:workflow"; Slots = []; Properties = [ "workflow", idProperty; "icon", iconIdProperty ]; Content = [] }

    let catalog = [ stack; heading; text; button; linkButton; textField; actions; surface; responsiveGrid; alert; metricCard; workflow ]

    let tryFind id = catalog |> List.tryFind (fun c -> c.Id = id)

    /// Components whose Forma contract takes an `icon` property (#33). An icon
    /// stored on any other component is kept but never exported.
    /// The edge a container declares its children share, if it declares one.
    let alignmentOf (node: ComponentNode) =
        match node.Properties |> Map.tryFind "alignment" with
        | Some(JString edge) when List.contains edge alignmentEdges && (tryFind node.Component |> Option.exists (fun c -> c.Properties |> List.exists (fst >> (=) "alignment"))) -> Some edge
        | _ -> None

    /// The values an author can choose for a property, by name (free-form and
    /// picker-owned properties such as `icon` and `workflow` have none here).
    let propertyChoices (property: string) =
        match property with
        | "density" -> densities
        | "type" -> buttonTypes
        | "inputType" -> inputTypes
        | "required" -> [ "true"; "false" ]
        | "level" -> [ "1"; "2"; "3"; "4"; "5"; "6" ]
        | "alignment" -> alignmentEdges
        | _ -> []

    let supportsIcon (componentId: string) =
        tryFind componentId |> Option.exists (fun c -> c.Properties |> List.exists (fst >> (=) "icon"))
