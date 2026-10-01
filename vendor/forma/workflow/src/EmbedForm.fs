namespace Forma.Workflow

open System

/// Form controls and inspector sections for the embeddable component's view,
/// built from public Forma field patterns. Internal to EmbedView.
module internal EmbedForm =
    let el = Markup.el
    let text = Markup.text

    let btn (event: string) (key: string option) (attrs: (string * string) list) (label: string) =
        el "button" ([ "type", "button"; "data-fw-event", event ] @ (key |> Option.map (fun k -> [ "data-fw-key", k ]) |> Option.defaultValue []) @ attrs) [ text label ]

    let kindOptions = Codec.nodeKinds |> List.map (fun (k, v) -> k, Phrases.english.NodeKind v)

    let colorText (c: ColorValue option) = c |> Option.map Codec.colorValueText |> Option.defaultValue ""

    // -- form controls (public Forma field patterns) ------------------------------

    type Ctx = { Prefix: string; Editable: bool; Workflow: Workflow; State: EmbedState }

    let inputId (ctx: Ctx) (name: string) = ctx.Prefix + "-i-" + name

    let textInput (ctx: Ctx) name label (event: string) key (value: string) (extra: (string * string) list) =
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

    let textArea (ctx: Ctx) name label (event: string) key (value: string) =
        let id = inputId ctx name
        el
            "div"
            [ "class", "ef-field" ]
            [ el "label" [ "class", "ef-field__label"; "for", id ] [ text label ]
              el "textarea" ([ "id", id; "rows", "3"; "data-fw-event", event; "data-fw-key", key ] @ (if ctx.Editable then [] else [ "readonly", "" ])) [ text value ] ]

    let selectField (ctx: Ctx) name label (event: string) key (arg: string option) (options: (string * string) list) (current: string) =
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

    let section (title: string) (opened: bool) (children: Markup list) =
        el "details" ([ "class", "ef-disclosure" ] @ (if opened then [ "open", "" ] else [])) [ el "summary" [] [ text title ]; el "div" [ "class", "ef-disclosure__content" ] children ]

    let formOf (event: string) (key: string) (children: Markup list) =
        el "form" [ "class", "ef-workflow-editor__form"; "data-fw-event", event; "data-fw-key", key ] children

    let submit (label: string) = el "div" [ "class", "ef-actions" ] [ el "button" [ "type", "submit" ] [ text label ] ]

    let plainField (ctx: Ctx) (name: string) (label: string) (inputName: string) (attrs: (string * string) list) =
        let id = inputId ctx name
        el "div" [ "class", "ef-field" ] [ el "label" [ "class", "ef-field__label"; "for", id ] [ text label ]; el "input" ([ "id", id; "name", inputName; "type", "text" ] @ attrs) [] ]

    let plainSelect (ctx: Ctx) (name: string) (label: string) (inputName: string) (options: (string * string) list) =
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

    let metadataKind (j: Json) =
        match j with
        | Json.String _ -> "text"
        | Json.Number _ -> "number"
        | Json.Bool _ -> "boolean"
        | _ -> "json"

    let metadataValue (j: Json) =
        match j with
        | Json.String s -> s
        | Json.Number n -> n
        | Json.Bool b -> if b then "yes" else "no"
        | other -> Json.compact other

    let metadataSection (ctx: Ctx) (key: string) (meta: Metadata option) =
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

    let referencesSection (ctx: Ctx) (key: string) (refs: Reference list) =
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

    let interactionSection (ctx: Ctx) (key: string) (i: Interaction option) =
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

    let extensionsSection (ext: Extensions) =
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

    let statusOptions = ("", "No status") :: (Codec.states |> List.map (fun (k, v) -> k, Phrases.english.State v))

    let statusSection (ctx: Ctx) (key: string) (st: Status option) =
        let current = st |> Option.map (fun s -> Codec.stateText s.State) |> Option.defaultValue ""
        let options = if current <> "" && not (statusOptions |> List.exists (fst >> (=) current)) then statusOptions @ [ current, current ] else statusOptions
        [ selectField ctx "status" "Status" "set-status" key None options current
          if st.IsSome then
              textInput ctx "status-label" "Status text" "set-status-label" key (st |> Option.map (Render.statusText Phrases.english) |> Option.defaultValue "") [] ]

    let colorInputs (ctx: Ctx) (key: string) (props: (string * string * ColorValue option) list) =
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
