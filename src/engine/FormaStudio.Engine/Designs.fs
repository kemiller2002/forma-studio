namespace FormaStudio.Engine

/// Reference designs for the HTML export proofs, authored through Studio's own
/// command core (the same commands the Layout surface issues). Each page is a
/// realistic composition of public Forma components.
[<RequireQualifiedAccess>]
module Designs =
    let private page (id: string) (name: string) : PageId = Samples.idOf id
    let private node (id: string) : ComponentNodeId = Samples.idOf id

    /// Commands that add one component with content and properties.
    let private add (pageId: PageId) (parent: string) (index: int) (id: string) (kind: string) (content: (string * string) list) (props: (string * JsonValue) list) =
        Layout(AddComponent(pageId, Some { Parent = node parent; Slot = "children" }, index, node id, kind))
        :: (content |> List.map (fun (k, v) -> Layout(SetComponentContent(pageId, node id, k, v))))
        @ (props |> List.map (fun (k, v) -> Layout(SetComponentProperty(pageId, node id, k, Some v))))

    let private pageCommands (id: string) (name: string) (density: string) (children: (PageId -> Command list) list) =
        let p = page id name
        [ Layout(AddPage(p, name, Some("/" + id)))
          Layout(AddComponent(p, None, 0, node (id + "-stack"), "stack"))
          Layout(SetComponentProperty(p, node (id + "-stack"), "density", Some(JString density))) ]
        @ (children |> List.collect (fun f -> f p))

    let private h (level: int) = [ "level", Json.ofInt level ]

    /// A normal application composition: form, metrics, alert and actions.
    let applicationCommands =
        let root = "application-stack"
        pageCommands
            "application"
            "Crew scheduling"
            "standard"
            [ fun p -> add p root 0 "app-title" "heading" [ "text", "Crew scheduling" ] (h 1)
              fun p -> add p root 1 "app-intro" "text" [ "text", "Assign crew members to upcoming training sessions. Changes take effect after review." ] []
              fun p -> add p root 2 "app-alert" "alert" [ "title", "Two sessions need an instructor"; "text", "Assign instructors before Friday so sessions stay on the schedule." ] []
              fun p -> add p root 3 "app-window" "surface" [ "title", "Next training window" ] []
              fun p -> add p "app-window" 0 "app-window-text" "text" [ "text", "Neutral buoyancy sessions run Monday to Thursday." ] []
              fun p -> add p "app-window" 1 "app-window-metric" "metric-card" [ "label", "Open seats"; "value", "6"; "context", "of 24 this week" ] []
              fun p -> add p root 4 "app-form" "surface" [ "title", "Request a session" ] []
              fun p -> add p "app-form" 0 "app-email" "text-field" [ "label", "Work email"; "description", "We send the confirmation here."; "name", "email" ] [ "inputType", JString "email"; "required", JBool true ]
              fun p -> add p "app-form" 1 "app-date" "text-field" [ "label", "Preferred date"; "name", "date" ] [ "inputType", JString "date" ]
              fun p -> add p "app-form" 2 "app-actions" "actions" [] []
              fun p -> add p "app-actions" 0 "app-submit" "button" [ "label", "Request session" ] [ "type", JString "submit" ]
              fun p -> add p "app-actions" 1 "app-help" "link-button" [ "label", "Read the training policy"; "href", "https://example.org/policy" ] [] ]

    /// A responsive composition: a grid that becomes one column on phones.
    let responsiveCommands =
        let root = "responsive-stack"
        pageCommands
            "responsive"
            "Station status"
            "compact"
            [ fun p -> add p root 0 "rsp-title" "heading" [ "text", "Station status" ] (h 1)
              fun p -> add p root 1 "rsp-grid" "responsive-grid" [ "label", "Station systems" ] []
              fun p -> add p "rsp-grid" 0 "rsp-power" "metric-card" [ "label", "Power"; "value", "92%"; "context", "Solar arrays nominal" ] []
              fun p -> add p "rsp-grid" 1 "rsp-air" "metric-card" [ "label", "Cabin pressure"; "value", "101.3 kPa"; "context", "Within limits" ] []
              fun p -> add p "rsp-grid" 2 "rsp-comms" "metric-card" [ "label", "Communications"; "value", "2 links"; "context", "Next handover 14:20" ] []
              fun p -> add p "rsp-grid" 3 "rsp-notes" "surface" [ "title", "Notes" ] []
              fun p -> add p "rsp-notes" 0 "rsp-notes-text" "text" [ "text", "A long note that must wrap on a 320 pixel screen without making the page scroll sideways, because the grid and its cards reflow." ] []
              fun p -> add p root 2 "rsp-actions" "actions" [] []
              fun p -> add p "rsp-actions" 0 "rsp-refresh" "button" [ "label", "Refresh status" ] [] ]

    /// A page that embeds a portable workflow (rendered by Forma).
    let workflowCommands (workflowId: string) =
        let root = "workflow-stack"
        pageCommands
            "workflow"
            "Launch readiness"
            "standard"
            [ fun p -> add p root 0 "wf-title" "heading" [ "text", "Launch readiness" ] (h 1)
              fun p -> add p root 1 "wf-intro" "text" [ "text", "The poll below is a portable Forma workflow. Every relationship is also listed in text." ] []
              fun p -> add p root 2 "wf-diagram" "workflow" [] [ "workflow", JString workflowId ] ]

    /// The design project: application, responsive and workflow pages.
    let project (workflowId: string) =
        let commands = applicationCommands @ responsiveCommands @ workflowCommands workflowId
        commands
        |> List.fold (fun state command -> state |> Result.bind (Editor.dispatch command)) (Ok(Editor.start (Samples.emptyProject "html-export-designs" "HTML export reference designs")))
        |> Result.map _.Project
