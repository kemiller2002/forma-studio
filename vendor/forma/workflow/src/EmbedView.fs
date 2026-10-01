namespace Forma.Workflow

open System

/// The embeddable component's view. The canvas is the same public figure the
/// static renderer produces; editor-only affordances use ef-workflow-editor*
/// classes and data-fw-* hooks that never appear in exported output.
[<RequireQualifiedAccess>]
module EmbedView =
    let private el = Markup.el
    let private text = Markup.text

    let private btn (event: string) (key: string option) (attrs: (string * string) list) (label: string) =
        el "button" ([ "type", "button"; "data-fw-event", event ] @ (key |> Option.map (fun k -> [ "data-fw-key", k ]) |> Option.defaultValue []) @ attrs) [ text label ]

    let private kindOptions = Codec.nodeKinds |> List.map (fun (k, v) -> k, Phrases.english.NodeKind v)

    let private colorText (c: ColorValue option) = c |> Option.map Codec.colorValueText |> Option.defaultValue ""

    // -- form controls (public Forma field patterns) ------------------------------

    type private Ctx = { Prefix: string; Editable: bool; Workflow: Workflow; State: EmbedState }

    let private inputId (ctx: Ctx) (name: string) = ctx.Prefix + "-i-" + name

    let private textInput (ctx: Ctx) name label (event: string) key (value: string) (extra: (string * string) list) =
        let id = inputId ctx name
        el
            "div"
            [ "class", "ef-field" ]
            [ el "label" [ "class", "ef-field__label"; "for", id ] [ text label ]
              el
                  "input"
                  ([ "id", id; "type", "text"; "value", value; "data-fw-event", event; "data-fw-key", key ]
                   @ extra
                   @ (if ctx.Editable then [] else [ "readonly", "" ]))
                  [] ]

    let private textArea (ctx: Ctx) name label (event: string) key (value: string) =
        let id = inputId ctx name
        el
            "div"
            [ "class", "ef-field" ]
            [ el "label" [ "class", "ef-field__label"; "for", id ] [ text label ]
              el "textarea" ([ "id", id; "rows", "3"; "data-fw-event", event; "data-fw-key", key ] @ (if ctx.Editable then [] else [ "readonly", "" ])) [ text value ] ]

    let private selectField (ctx: Ctx) name label (event: string) key (arg: string option) (options: (string * string) list) (current: string) =
        let id = inputId ctx name
        el
            "label"
            [ "class", "ef-select-field"; "for", id ]
            [ el "span" [ "class", "ef-field__label" ] [ text label ]
              el
                  "span"
                  [ "class", "ef-select" ]
                  [ el
                        "select"
                        ([ "class", "ef-select__input"; "id", id; "data-fw-event", event; "data-fw-key", key ]
                         @ (arg |> Option.map (fun a -> [ "data-fw-arg", a ]) |> Option.defaultValue [])
                         @ (if ctx.Editable then [] else [ "disabled", "" ]))
                        (options |> List.map (fun (v, l) -> el "option" ([ "value", v ] @ (if v = current then [ "selected", "selected" ] else [])) [ text l ]))
                    el "span" [ "class", "ef-select__indicator"; "aria-hidden", "true" ] [] ] ]

    let private section (title: string) (opened: bool) (children: Markup list) =
        el "details" ([ "class", "ef-disclosure" ] @ (if opened then [ "open", "" ] else [])) [ el "summary" [] [ text title ]; el "div" [ "class", "ef-disclosure__content" ] children ]

    let private formOf (event: string) (key: string) (children: Markup list) =
        el "form" [ "class", "ef-workflow-editor__form"; "data-fw-event", event; "data-fw-key", key ] children

    let private submit (label: string) = el "div" [ "class", "ef-actions" ] [ el "button" [ "type", "submit" ] [ text label ] ]

    let private plainField (ctx: Ctx) (name: string) (label: string) (inputName: string) (attrs: (string * string) list) =
        let id = inputId ctx name
        el "div" [ "class", "ef-field" ] [ el "label" [ "class", "ef-field__label"; "for", id ] [ text label ]; el "input" ([ "id", id; "name", inputName; "type", "text" ] @ attrs) [] ]

    let private plainSelect (ctx: Ctx) (name: string) (label: string) (inputName: string) (options: (string * string) list) =
        let id = inputId ctx name
        el
            "label"
            [ "class", "ef-select-field"; "for", id ]
            [ el "span" [ "class", "ef-field__label" ] [ text label ]
              el
                  "span"
                  [ "class", "ef-select" ]
                  [ el "select" [ "class", "ef-select__input"; "id", id; "name", inputName ] (options |> List.map (fun (v, l) -> el "option" [ "value", v ] [ text l ]))
                    el "span" [ "class", "ef-select__indicator"; "aria-hidden", "true" ] [] ] ]

    // -- shared inspector sections -------------------------------------------------

    let private metadataKind (j: Json) =
        match j with
        | Json.String _ -> "text"
        | Json.Number _ -> "number"
        | Json.Bool _ -> "boolean"
        | _ -> "json"

    let private metadataValue (j: Json) =
        match j with
        | Json.String s -> s
        | Json.Number n -> n
        | Json.Bool b -> if b then "yes" else "no"
        | other -> Json.compact other

    let private metadataSection (ctx: Ctx) (key: string) (meta: Metadata option) =
        let rows = meta |> Option.defaultValue []
        let row i (k, v) =
            let id = inputId ctx $"meta-{i}"
            el
                "li"
                []
                [ el "label" [ "for", id ] [ text k ]
                  el
                      "input"
                      ([ "id", id; "type", "text"; "value", metadataValue v; "data-fw-event", "set-metadata"; "data-fw-key", key; "data-fw-arg", k
                         "data-fw-type", metadataKind v ]
                       @ (if ctx.Editable then [] else [ "readonly", "" ]))
                      []
                  if ctx.Editable then btn "remove-metadata" (Some key) [ "data-fw-arg", k; "aria-label", $"Remove {k}" ] "Remove" else text "" ]
        section
            (if rows.IsEmpty then "Metadata" else $"Metadata ({rows.Length})")
            (not rows.IsEmpty)
            [ if rows.IsEmpty then el "p" [] [ text "No metadata." ] else el "ul" [ "class", "ef-workflow-editor__rows" ] (rows |> List.mapi row)
              if ctx.Editable then
                  formOf
                      "add-metadata"
                      key
                      [ plainField ctx "meta-key" "Field key" "key" [ "required", "" ]
                        plainSelect ctx "meta-type" "Field type" "type" [ "text", "Text"; "number", "Number"; "boolean", "Yes or no"; "json", "JSON" ]
                        plainField ctx "meta-value" "Field value" "value" []
                        submit "Add field" ] ]

    let private referencesSection (ctx: Ctx) (key: string) (refs: Reference list) =
        section
            (if refs.IsEmpty then "References" else $"References ({refs.Length})")
            (not refs.IsEmpty)
            [ if refs.IsEmpty then el "p" [] [ text "No references." ]
              else
                  el
                      "ul"
                      [ "class", "ef-workflow-editor__rows" ]
                      (refs
                       |> List.map (fun r ->
                           el
                               "li"
                               []
                               [ text ((r.Type |> Option.map (fun t -> t + ": ") |> Option.defaultValue "") + (r.Label |> Option.defaultValue r.Key) + $" ({r.System.Value} {r.Key})")
                                 if ctx.Editable then btn "remove-reference" (Some key) [ "data-fw-arg", r.Id.Value; "aria-label", $"Remove reference {r.Key}" ] "Remove" else text "" ]))
              if ctx.Editable then
                  formOf
                      "add-reference"
                      key
                      [ plainField ctx "ref-system" "System (namespace)" "system" [ "required", ""; "placeholder", "com.example.erp" ]
                        plainField ctx "ref-type" "Reference type" "type" []
                        plainField ctx "ref-key" "Reference key" "key" [ "required", "" ]
                        plainField ctx "ref-label" "Reference label" "label" []
                        plainField ctx "ref-href" "Reference link" "href" [ "type", "url" ]
                        submit "Add reference" ] ]

    let private interactionSection (ctx: Ctx) (key: string) (i: Interaction option) =
        let describe =
            match i with
            | None -> "None"
            | Some NoInteraction -> "None"
            | Some(SelectIntent _) -> "Select"
            | Some(InspectIntent _) -> "Inspect"
            | Some(Navigate(t, l)) | Some(Open(t, l)) ->
                let where =
                    match t with
                    | ToUrl u -> u
                    | ToNode n -> "step " + n.Value
                    | ToEdge e -> "connection " + e.Value
                    | ToGroup g -> "group " + g.Value
                    | ToReference r -> "reference " + r.Value
                    | ToWorkflow w -> "workflow " + w.Value
                (match i with Some(Navigate _) -> "Navigate to " | _ -> "Open ") + where + (l |> Option.map (fun x -> $" (\"{x}\")") |> Option.defaultValue "")
            | Some(Command(q, l, _)) -> $"Command {q.Value} (\"{l}\")"
        section
            "Interaction"
            false
            [ el "p" [] [ text ("Current: " + describe + ". The application decides what an interaction does.") ]
              match i with
              | Some(Command _ | Navigate _ | Open _) -> btn "intent" (Some key) [] "Try it"
              | _ -> text ""
              if ctx.Editable then
                  formOf
                      "set-interaction"
                      key
                      [ plainSelect ctx "int-action" "Action" "action" [ "", "None"; "select", "Select"; "inspect", "Inspect"; "navigate", "Navigate"; "open", "Open"; "command", "Command" ]
                        plainSelect ctx "int-target-kind" "Destination" "targetKind" [ "url", "Link"; "node", "Step"; "workflow", "Workflow"; "reference", "Reference" ]
                        plainField ctx "int-target" "Destination value" "target" []
                        plainField ctx "int-command" "Command (namespace:name)" "command" []
                        plainField ctx "int-label" "Text people see" "label" []
                        submit "Set interaction" ] ]

    let private extensionsSection (ext: Extensions) =
        if ext.IsEmpty then text ""
        else
            section
                $"Extensions ({ext.Length})"
                false
                [ el "p" [] [ text "Preserved exactly as the producer wrote them. Forma does not interpret them." ]
                  el
                      "dl"
                      [ "class", "ef-workflow-editor__extensions" ]
                      (ext |> List.collect (fun (n, members) -> [ el "dt" [] [ text n.Value ]; el "dd" [] [ el "code" [] [ text (Json.compact (Json.Object members)) ] ] ])) ]

    let private statusOptions = ("", "No status") :: (Codec.states |> List.map (fun (k, v) -> k, Phrases.english.State v))

    let private statusSection (ctx: Ctx) (key: string) (st: Status option) =
        let current = st |> Option.map (fun s -> Codec.stateText s.State) |> Option.defaultValue ""
        let options = if current <> "" && not (statusOptions |> List.exists (fst >> (=) current)) then statusOptions @ [ current, current ] else statusOptions
        [ selectField ctx "status" "Status" "set-status" key None options current
          if st.IsSome then
              textInput ctx "status-label" "Status text" "set-status-label" key (st |> Option.map (Render.statusText Phrases.english) |> Option.defaultValue "") [] ]

    let private colorInputs (ctx: Ctx) (key: string) (props: (string * string * ColorValue option) list) =
        let listId = ctx.Prefix + "-colors"
        el
            "div"
            []
            ((props
              |> List.map (fun (arg, label, value) ->
                  let id = inputId ctx ("color-" + arg)
                  el
                      "div"
                      [ "class", "ef-field" ]
                      [ el "label" [ "class", "ef-field__label"; "for", id ] [ text label ]
                        el "span" [ "class", "ef-field__description"; "id", id + "-d" ] [ text "#rrggbb, a token such as accent-primary, or palette:slot. Empty uses the Forma default." ]
                        el
                            "input"
                            ([ "id", id; "type", "text"; "list", listId; "value", colorText value; "data-fw-event", "set-color"; "data-fw-key", key; "data-fw-arg", arg
                               "aria-describedby", id + "-d" ]
                             @ (if ctx.Editable then [] else [ "readonly", "" ]))
                            [] ]))
             @ [ el
                     "datalist"
                     [ "id", listId ]
                     ((Codec.tokens |> List.map fst) @ (ctx.Workflow.Palette |> List.map (fun (slot, _) -> "palette:" + slot.Value))
                      |> List.map (fun v -> el "option" [ "value", v ] [])) ])

    // -- inspector per selection ---------------------------------------------------

    let private nodeInspector (ctx: Ctx) (n: Node) (rect: Rect option) =
        let key = ObjectRef.key (NodeRef n.Id)
        let kind = match n.Kind with CoreNode(k, _) -> Codec.nodeKindText (CoreNode(k, None)) | CustomNode(q, _) -> q.Value
        let kinds = match n.Kind with CustomNode(q, l) -> kindOptions @ [ q.Value, l ] | _ -> kindOptions
        let groups = ctx.Workflow.Groups
        let others = ctx.Workflow.Nodes |> List.filter (fun x -> x.Id <> n.Id)
        let num (v: float) = Markup.number v
        [ yield textInput ctx "label" "Name" "set-label" key n.Label [ "required", "" ]
          yield selectField ctx "kind" "Kind" "set-kind" key None kinds kind
          yield textArea ctx "description" "Description" "set-description" key (n.Description |> Option.defaultValue "")
          yield! statusSection ctx key n.Status
          yield
              section
                  "Appearance"
                  false
                  [ selectField ctx "shape" "Shape" "set-shape" key None (("", "Default for kind") :: (Codec.shapes |> List.map (fun (k, _) -> k, k))) (n.Variant |> Option.map Codec.shapeText |> Option.defaultValue "")
                    colorInputs ctx key [ "fill", "Fill", n.Color.Fill; "stroke", "Border", n.Color.Stroke; "accent", "Accent", n.Color.Accent; "foreground", "Text colour", n.Color.Foreground ] ]
          match rect with
          | Some r ->
              yield
                  section
                      "Position and size"
                      false
                      [ el
                            "div"
                            [ "class", "ef-workflow-editor__geometry" ]
                            [ textInput ctx "x" "X" "set-geometry" key (num r.X) [ "data-fw-arg", "x"; "inputmode", "decimal" ]
                              textInput ctx "y" "Y" "set-geometry" key (num r.Y) [ "data-fw-arg", "y"; "inputmode", "decimal" ]
                              textInput ctx "width" "Width" "set-geometry" key (num r.W) [ "data-fw-arg", "width"; "inputmode", "decimal" ]
                              textInput ctx "height" "Height" "set-geometry" key (num r.H) [ "data-fw-arg", "height"; "inputmode", "decimal" ] ]
                        if ctx.Editable then
                            el
                                "div"
                                [ "class", "ef-cluster"; "role", "group"; "aria-label", "Move" ]
                                [ btn "nudge" None [ "value", "left" ] "Move left"; btn "nudge" None [ "value", "right" ] "Move right"
                                  btn "nudge" None [ "value", "up" ] "Move up"; btn "nudge" None [ "value", "down" ] "Move down" ]
                        else text "" ]
          | None -> ()
          if ctx.Editable && not others.IsEmpty then
              yield
                  section
                      "Connect"
                      false
                      [ formOf
                            "connect-form"
                            key
                            [ plainSelect ctx "connect-target" "Connect to" "target" (others |> List.map (fun x -> x.Id.Value, x.Label))
                              if not n.Ports.IsEmpty then plainSelect ctx "connect-source-port" "From port" "sourcePort" (("", "No port") :: (n.Ports |> List.map (fun p -> p.Id.Value, p.Label |> Option.defaultValue p.Id.Value)))
                              else text ""
                              plainField ctx "connect-target-port" "To port (optional id)" "targetPort" []
                              submit "Connect" ] ]
          yield
              section
                  (if n.Ports.IsEmpty then "Ports" else $"Ports ({n.Ports.Length})")
                  false
                  [ if n.Ports.IsEmpty then el "p" [] [ text "No ports. Connections attach to the step's sides." ]
                    else
                        el
                            "ul"
                            [ "class", "ef-workflow-editor__rows" ]
                            (n.Ports
                             |> List.map (fun p ->
                                 let detail =
                                     [ p.Side |> Option.map Codec.sideText; p.Direction |> Option.map Codec.portDirectionText ] |> List.choose id |> String.concat ", "
                                 el
                                     "li"
                                     []
                                     [ text ((p.Label |> Option.defaultValue p.Id.Value) + (if detail = "" then "" else $" ({detail})"))
                                       if ctx.Editable then btn "remove-port" (Some key) [ "data-fw-arg", p.Id.Value; "aria-label", $"Remove port {p.Id.Value}" ] "Remove" else text "" ]))
                    if ctx.Editable then
                        formOf
                            "add-port"
                            key
                            [ plainField ctx "port-label" "Port name" "label" []
                              plainSelect ctx "port-side" "Side" "side" [ "", "Automatic"; "top", "Top"; "right", "Right"; "bottom", "Bottom"; "left", "Left" ]
                              plainSelect ctx "port-direction" "Direction" "direction" [ "", "Any"; "in", "In"; "out", "Out"; "inout", "In and out" ]
                              submit "Add port" ] ]
          if not groups.IsEmpty then
              yield
                  section
                      "Groups and lanes"
                      false
                      [ el
                            "ul"
                            [ "class", "ef-workflow-editor__rows" ]
                            (groups
                             |> List.map (fun g ->
                                 let id = inputId ctx ("member-" + g.Id.Value)
                                 el
                                     "li"
                                     [ "class", "ef-checkbox" ]
                                     [ el
                                           "input"
                                           ([ "id", id; "type", "checkbox"; "data-fw-event", "membership"; "data-fw-key", ObjectRef.key (GroupRef g.Id); "data-fw-arg", n.Id.Value ]
                                            @ (if List.contains n.Id g.Members then [ "checked", "" ] else [])
                                            @ (if ctx.Editable then [] else [ "disabled", "" ]))
                                           []
                                       el "label" [ "for", id ] [ text (Phrases.english.GroupKind g.Kind + ": " + g.Label) ] ])) ]
          yield referencesSection ctx key n.References
          yield metadataSection ctx key n.Metadata
          yield interactionSection ctx key n.Interaction
          yield
              section
                  "Accessibility"
                  false
                  [ textInput ctx "a11y-name" "Accessible name (if different from the name)" "set-a11y-name" key (n.Accessibility.Name |> Option.defaultValue "") []
                    textInput ctx "a11y-description" "Accessible description" "set-a11y-description" key (n.Accessibility.Description |> Option.defaultValue "") [] ]
          yield extensionsSection n.Extensions ]

    let private edgeInspector (ctx: Ctx) (e: Edge) =
        let key = ObjectRef.key (EdgeRef e.Id)
        let kind = e.Kind |> Option.map Codec.edgeKindText |> Option.defaultValue ""
        [ el "p" [] [ text (Render.relationText ctx.Workflow Phrases.english e) ]
          textInput ctx "label" "Label" "set-label" key (e.Label |> Option.defaultValue "") []
          selectField ctx "edge-kind" "Kind" "edge-kind" key None (("", "Sequence (default)") :: (Codec.edgeKinds |> List.map (fun (k, v) -> k, Phrases.english.EdgeKind v))) kind
          selectField ctx "edge-line" "Line" "edge-line" key None (("", "Solid (default)") :: (Codec.lines |> List.map (fun (k, _) -> k, k))) (e.Line |> Option.map Codec.lineText |> Option.defaultValue "")
          selectField
              ctx
              "edge-direction"
              "Direction"
              "edge-direction"
              key
              None
              [ "", "Forward (default)"; "backward", "Backward"; "both", "Both ways"; "none", "No direction" ]
              (e.Direction |> Option.map Codec.directionText |> Option.defaultValue "")
          yield! statusSection ctx key e.Status
          section "Appearance" false [ colorInputs ctx key [ "stroke", "Line colour", e.Stroke ] ]
          if ctx.Editable then
              el "div" [ "class", "ef-cluster"; "role", "group"; "aria-label", "Reconnect" ] [ btn "reconnect-start" None [ "data-fw-arg", "source" ] "Change start"; btn "reconnect-start" None [ "data-fw-arg", "target" ] "Change end" ]
          else text ""
          referencesSection ctx key e.References
          metadataSection ctx key e.Metadata
          interactionSection ctx key e.Interaction
          section
              "Accessibility"
              false
              [ textInput ctx "a11y-name" "Accessible name" "set-a11y-name" key (e.Accessibility.Name |> Option.defaultValue "") []
                textInput ctx "a11y-description" "Accessible description" "set-a11y-description" key (e.Accessibility.Description |> Option.defaultValue "") [] ]
          extensionsSection e.Extensions ]

    let private groupInspector (ctx: Ctx) (g: Group) =
        let key = ObjectRef.key (GroupRef g.Id)
        [ textInput ctx "label" "Name" "set-label" key g.Label [ "required", "" ]
          el "p" [] [ text ("Kind: " + Phrases.english.GroupKind g.Kind) ]
          textArea ctx "description" "Description" "set-description" key (g.Description |> Option.defaultValue "")
          yield! statusSection ctx key g.Status
          section
              "Members"
              true
              [ el
                    "ul"
                    [ "class", "ef-workflow-editor__rows" ]
                    (ctx.Workflow.Nodes
                     |> List.map (fun n ->
                         let id = inputId ctx ("gm-" + n.Id.Value)
                         el
                             "li"
                             [ "class", "ef-checkbox" ]
                             [ el
                                   "input"
                                   ([ "id", id; "type", "checkbox"; "data-fw-event", "membership"; "data-fw-key", key; "data-fw-arg", n.Id.Value ]
                                    @ (if List.contains n.Id g.Members then [ "checked", "" ] else [])
                                    @ (if ctx.Editable then [] else [ "disabled", "" ]))
                                   []
                               el "label" [ "for", id ] [ text n.Label ] ])) ]
          section "Appearance" false [ colorInputs ctx key [ "fill", "Fill", g.Color.Fill; "stroke", "Border", g.Color.Stroke; "accent", "Accent", g.Color.Accent ] ]
          referencesSection ctx key g.References
          metadataSection ctx key g.Metadata
          extensionsSection g.Extensions ]

    let private workflowInspector (ctx: Ctx) =
        let w = ctx.Workflow
        [ textInput ctx "title" "Workflow title" "set-title" "workflow" w.Title [ "required", "" ]
          el
              "dl"
              [ "class", "ef-workflow-editor__facts" ]
              [ el "dt" [] [ text "Id" ]
                el "dd" [] [ el "code" [] [ text w.Id.Value ] ]
                el "dt" [] [ text "File" ]
                el "dd" [] [ el "code" [] [ text (Format.repositoryPath w.Id) ] ]
                el "dt" [] [ text "Contents" ]
                el "dd" [] [ text $"{w.Nodes.Length} steps, {w.Edges.Length} connections, {w.Groups.Length} groups" ] ]
          if ctx.Editable then
              section
                  "Add a group or lane"
                  false
                  [ el "p" [] [ text "Selected steps become its members." ]
                    formOf
                        "add-group"
                        "workflow"
                        [ plainField ctx "group-label" "Name" "label" [ "required", "" ]
                          plainSelect ctx "group-kind" "Kind" "kind" [ "group", "Group"; "container", "Container"; "swimlane", "Lane"; "phase", "Phase" ]
                          submit "Add group" ] ]
          else text ""
          section
              "Workflow metadata"
              false
              [ el "p" [] [ text "Edit workflow-level metadata in the file; it is preserved here." ]
                el "code" [] [ text (w.Metadata |> Option.map (Json.Object >> Json.compact) |> Option.defaultValue "{}") ] ]
          extensionsSection w.Extensions ]

    // -- canvas -----------------------------------------------------------------

    let rec private augment (ctx: Ctx) (display: Workflow) (m: Markup) : Markup =
        let prefix = ctx.Prefix
        let selected r = List.contains r ctx.State.Selection
        let interactive = ctx.State.Mode <> ViewMode && ctx.State.Mode <> RuntimeMode
        let edit = ctx.Editable
        match m with
        | Element("article", attrs, children) ->
            let id = attrs |> List.tryFind (fst >> (=) "id") |> Option.map snd |> Option.defaultValue ""
            match display.Nodes |> List.tryFind (fun n -> Render.nodeElementId prefix n.Id = id) with
            | Some n ->
                let r = NodeRef n.Id
                let isSelected = selected r
                let cls = [ "ef-workflow-editor__object"; (if isSelected then "ef-workflow-editor__selected" else "") ] |> List.filter ((<>) "")
                let attrs =
                    attrs
                    |> List.map (fun (k, v) -> if k = "class" then k, v + " " + String.Join(" ", cls) else k, v)
                    |> fun a -> a @ (if interactive || edit then [ "data-fw-event", "select"; "data-fw-key", ObjectRef.key r; "tabindex", "-1" ] else [])
                    |> fun a -> a @ (if edit && isSelected then [ "data-fw-drag", "move" ] else [])
                let intent =
                    match n.Interaction with
                    | Some(Command(_, label, _)) -> [ btn "intent" (Some(ObjectRef.key r)) [ "class", "ef-workflow-editor__intent" ] label ]
                    | Some(Open(t, l)) | Some(Navigate(t, l)) when (match t with ToUrl _ -> false | _ -> true) ->
                        [ btn "intent" (Some(ObjectRef.key r)) [ "class", "ef-workflow-editor__intent" ] (l |> Option.defaultValue "Open") ]
                    | _ -> []
                let handles =
                    if edit && isSelected then
                        [ el "span" [ "class", "ef-workflow-editor__resize"; "data-fw-drag", "resize"; "data-fw-key", ObjectRef.key r; "aria-hidden", "true" ] []
                          el "span" [ "class", "ef-workflow-editor__connect"; "data-fw-drag", "connect"; "data-fw-key", ObjectRef.key r; "aria-hidden", "true" ] [] ]
                    else []
                Element("article", attrs, children @ intent @ handles)
            | None -> Element("article", attrs, children |> List.map (augment ctx display))
        | Element("div", attrs, children) when attrs |> List.exists (fun (k, v) -> k = "class" && v.StartsWith "ef-diagram-group") ->
            let id = attrs |> List.tryFind (fst >> (=) "id") |> Option.map snd |> Option.defaultValue ""
            match display.Groups |> List.tryFind (fun g -> Render.groupElementId prefix g.Id = id) with
            | Some g when interactive || edit ->
                let r = GroupRef g.Id
                let attrs =
                    attrs
                    |> List.map (fun (k, v) -> if k = "class" && selected r then k, v + " ef-workflow-editor__selected" else k, v)
                Element("div", attrs @ [ "data-fw-event", "select"; "data-fw-key", ObjectRef.key r ], children)
            | _ -> Element("div", attrs, children)
        | Element("ol", attrs, items) when attrs |> List.contains ("class", "ef-diagram__relations") && (interactive || edit) ->
            let rows =
                List.zip (display.Edges |> List.truncate items.Length) items
                |> List.map (fun (e, item) ->
                    match item with
                    | Element("li", _, content) ->
                        let r = EdgeRef e.Id
                        el
                            "li"
                            []
                            [ el
                                  "button"
                                  [ "type", "button"; "class", "ef-workflow-editor__relation"; "data-fw-event", "select"; "data-fw-key", ObjectRef.key r
                                    "aria-pressed", (if selected r then "true" else "false") ]
                                  content ]
                    | other -> other)
            Element("ol", attrs, rows)
        | Element(name, attrs, children) -> Element(name, attrs, children |> List.map (augment ctx display))
        | other -> other

    /// The workflow as displayed: runtime overlay applied, positions filled.
    let displayed (s: EmbedState) (w: Workflow) =
        let w =
            if s.Runtime.IsEmpty then w
            else
                { w with Nodes = w.Nodes |> List.map (fun n -> match Map.tryFind n.Id s.Runtime with Some st -> { n with Status = Some st } | None -> n) }
        w

    // -- whole view -------------------------------------------------------------

    let private structure (ctx: Ctx) =
        let w = ctx.Workflow
        let item (r: ObjectRef) (label: string) =
            el "li" [] [ btn "select" (Some(ObjectRef.key r)) [ "aria-pressed", (if List.contains r ctx.State.Selection then "true" else "false") ] label ]
        let total = w.Nodes.Length + w.Edges.Length + w.Groups.Length
        el
            "details"
            [ "class", "ef-disclosure ef-workflow-editor__structure"; "open", "" ]
            [ el "summary" [] [ text $"Objects ({total})" ]
              el
                  "div"
                  [ "class", "ef-disclosure__content" ]
                  [ el "p" [ "class", "ef-workflow-editor__hint" ] [ text (if ctx.Editable then "Select an item, then use the arrow keys to move it (Shift for larger steps) or Delete to remove it." else "Select an item to inspect it.") ]
                    el
                        "ul"
                        [ "class", "ef-workflow-editor__list"; "aria-label", "Steps" ]
                        (w.Nodes |> List.map (fun n -> item (NodeRef n.Id) (Render.kindText Phrases.english n.Kind + ": " + n.Label)))
                    if w.Edges.IsEmpty then text ""
                    else
                        el "ul" [ "class", "ef-workflow-editor__list"; "aria-label", "Connections" ] (w.Edges |> List.map (fun e -> item (EdgeRef e.Id) (Render.relationText w Phrases.english e)))
                    if w.Groups.IsEmpty then text ""
                    else
                        el "ul" [ "class", "ef-workflow-editor__list"; "aria-label", "Groups" ] (w.Groups |> List.map (fun g -> item (GroupRef g.Id) (Phrases.english.GroupKind g.Kind + ": " + g.Label))) ] ]

    let private toolbar (ctx: Ctx) =
        let s = ctx.State
        let canUndo = not s.History.Past.IsEmpty
        let canRedo = not s.History.Future.IsEmpty
        let oneNode = match s.Selection with [ NodeRef _ ] -> true | _ -> false
        let disabled cond = if cond then [] else [ "disabled", "" ]
        el
            "div"
            [ "class", "ef-workflow-editor__toolbar"; "role", "toolbar"; "aria-label", "Workflow tools" ]
            [ if ctx.Editable then
                  el
                      "div"
                      [ "class", "ef-cluster"; "role", "group"; "aria-label", "Create" ]
                      [ selectField ctx "add-kind" "New step kind" "add-kind" "workflow" None kindOptions (Codec.nodeKindText (CoreNode(s.AddKind, None)))
                        btn "add-node" None [] "Add step"
                        btn "connect-start" None (disabled oneNode) "Connect"
                        btn "delete" None (disabled (not s.Selection.IsEmpty)) "Delete"
                        btn "auto-layout" None [] "Arrange" ]
              else text ""
              if ctx.Editable then
                  el "div" [ "class", "ef-cluster"; "role", "group"; "aria-label", "History" ] [ btn "undo" None (disabled canUndo) "Undo"; btn "redo" None (disabled canRedo) "Redo" ]
              else text ""
              el
                  "div"
                  [ "class", "ef-cluster"; "role", "group"; "aria-label", "Zoom" ]
                  [ btn "zoom-out" None (disabled (s.Zoom > List.head Embed.zoomLevels)) "Zoom out"
                    el "span" [ "class", "ef-workflow-editor__zoom-level" ] [ text (string s.Zoom + "%") ]
                    btn "zoom-in" None (disabled (s.Zoom < List.last Embed.zoomLevels)) "Zoom in"
                    btn "zoom-reset" None [] "Actual size" ] ]

    let private findings (ctx: Ctx) =
        match ctx.State.Report with
        | Some r when not r.Findings.IsEmpty ->
            let visible = r.Findings |> List.filter (fun f -> f.Severity <> Severity.Info || ctx.Editable)
            if visible.IsEmpty then text ""
            else
                el
                    "section"
                    [ "class", "ef-workflow-editor__findings"; "aria-label", "Validation" ]
                    [ el "p" [] [ text ($"Validation: {Validation.classText r.Class}.") ]
                      el
                          "ul"
                          [ "class", "ef-validation-summary__list" ]
                          (visible
                           |> List.map (fun f ->
                               let label = (match f.Severity with Severity.Error -> "Error: " | Severity.Warning -> "Warning: " | Severity.Info -> "Note: ") + f.Message
                               let target =
                                   f.Subject
                                   |> Option.bind ObjectId.tryCreate
                                   |> Option.bind (fun id ->
                                       if ctx.Workflow.Nodes |> List.exists (fun n -> n.Id = id) then Some(NodeRef id)
                                       elif ctx.Workflow.Edges |> List.exists (fun e -> e.Id = id) then Some(EdgeRef id)
                                       elif ctx.Workflow.Groups |> List.exists (fun g -> g.Id = id) then Some(GroupRef id)
                                       else None)
                               match target with
                               | Some t -> el "li" [] [ btn "focus-finding" (Some(ObjectRef.key t)) [] label ]
                               | None -> el "li" [] [ text label ])) ]
        | _ -> text ""

    let private inspector (ctx: Ctx) (layout: ResolvedLayout) =
        let w = ctx.Workflow
        let body, title =
            match ctx.State.Selection with
            | [ NodeRef id ] ->
                match w.Nodes |> List.tryFind (fun n -> n.Id = id) with
                | Some n -> nodeInspector ctx n (Map.tryFind id layout.Nodes), n.Label
                | None -> [], ""
            | [ EdgeRef id ] ->
                match w.Edges |> List.tryFind (fun e -> e.Id = id) with
                | Some e -> edgeInspector ctx e, "Connection"
                | None -> [], ""
            | [ GroupRef id ] ->
                match w.Groups |> List.tryFind (fun g -> g.Id = id) with
                | Some g -> groupInspector ctx g, g.Label
                | None -> [], ""
            | [] -> workflowInspector ctx, "Workflow"
            | many ->
                [ el "p" [] [ text $"{many.Length} items selected." ]
                  if ctx.Editable then
                      formOf
                          "add-group"
                          "workflow"
                          [ plainField ctx "group-label" "Group the selected steps" "label" [ "required", ""; "placeholder", "Group name" ]
                            plainSelect ctx "group-kind" "Kind" "kind" [ "group", "Group"; "container", "Container"; "swimlane", "Lane"; "phase", "Phase" ]
                            submit "Add group" ]
                  else text "" ],
                "Selection"
        let headingId = ctx.Prefix + "-inspector"
        el "section" [ "class", "ef-workflow-editor__inspector"; "aria-labelledby", headingId ] (el "h2" [ "id", headingId ] [ text title ] :: body)

    /// Renders the component for its current state.
    let view (opts: RenderOptions) (s: EmbedState) : Markup =
        match s.Workflow with
        | None ->
            el
                "div"
                [ "class", "ef-workflow-editor"; "data-mode", HostMode.text s.Mode ]
                [ el "p" [ "role", "status" ] [ text (if s.Announcement = "" then "No workflow loaded." else s.Announcement) ] ]
        | Some w ->
            let editable = Embed.canEdit s
            let display = displayed s w |> fun d -> if d.Nodes |> List.forall (fun n -> n.Layout.Position.IsSome) then d else Layout.apply FillMissing d
            let ctx = { Prefix = s.IdPrefix; Editable = editable; Workflow = w; State = s }
            let opts = { opts with IdPrefix = Some s.IdPrefix }
            let figure = Render.figure opts display |> augment ctx display
            let layout = Layout.resolve display
            let withPanels = s.Mode = InspectMode || s.Mode = EditMode
            let pending =
                match s.Pending with
                | NoPending -> text ""
                | Connecting src ->
                    el "p" [ "class", "ef-workflow-editor__pending"; "role", "status" ] [ text $"Connecting from {Editing.nodeName w src.Node}: choose the step to connect to. "; btn "cancel" None [] "Cancel" ]
                | Reconnecting _ -> el "p" [ "class", "ef-workflow-editor__pending"; "role", "status" ] [ text "Choose the step this end should connect to. "; btn "cancel" None [] "Cancel" ]
            el
                "div"
                [ "class", "ef-workflow-editor"; "data-mode", HostMode.text s.Mode; "data-fw-root", s.IdPrefix ]
                [ toolbar ctx
                  el "p" [ "class", "ef-visually-hidden"; "role", "status"; "aria-live", "polite" ] [ text s.Announcement ]
                  pending
                  el
                      "div"
                      [ "class", (if withPanels || s.Mode = PickMode then "ef-workflow-editor__layout" else "ef-workflow-editor__layout ef-workflow-editor__layout--canvas") ]
                      [ if withPanels || s.Mode = PickMode then structure ctx else text ""
                        el "div" [ "class", "ef-workflow-editor__canvas"; "style", Markup.style [ "--ef-workflow-zoom", Markup.number (float s.Zoom / 100.0) ] ] [ figure ]
                        if withPanels then inspector ctx layout else text "" ]
                  if s.Mode = PickMode then
                      el "div" [ "class", "ef-actions" ] [ btn "pick-confirm" None (if s.Selection.IsEmpty then [ "disabled", "" ] else []) $"Use selection ({s.Selection.Length})" ]
                  else text ""
                  if withPanels then findings ctx else text "" ]
