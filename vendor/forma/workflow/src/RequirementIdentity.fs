namespace Forma.Workflow

open System

/// Optional, application-owned requirement identity (FMO-019; locator tier 3 of FMO-012,
/// "documented application-owned stable object identity"). An application that verifies its
/// requirements field by field puts `data-requirement-id="<ID>"` on the exact interactive or
/// display element that implements a requirement. Forma never requires it (FMO-013) and never
/// uses it for styling, behaviour or as a substitute for labels and roles: those stay the primary
/// identity (FMO-012 tiers 1-2).
///
/// The rules this module enforces, so that the attribute can be trusted by a verifier:
///  * the ID goes on the element of the component's kind, never on a wrapper, label or an
///    unrelated element (a `text-input` ID on a `div` is refused);
///  * one element carries a given ID per active scope; repeated controls add a distinct
///    `data-requirement-instance` key;
///  * the attribute never changes the element's accessible name, role, state or other attributes,
///    and rendering the same markup twice gives the same identity (it is data, not generated).
type RequirementComponent =
    | TextInput
    | TextArea
    | Select
    | Checkbox
    | Button
    | Link
    /// Read-only content that shows a value (a `dd`, `time`, `output`, `p`, `span`...).
    | Display
    /// A landmark or grouping region (`section`, `form`, `fieldset`...).
    | Region

type RequirementIdentityError =
    | InvalidRequirementId of string
    | InvalidInstanceKey of string
    /// No element of the component's kind was found inside the markup.
    | NoMatchingElement of requirementId: string * comp: RequirementComponent
    /// More than one element of the component's kind: which one implements the requirement is ambiguous.
    | AmbiguousElement of requirementId: string * count: int
    | AlreadyIdentified of existing: string
    /// Two elements in one scope carry the ID without distinct instance keys.
    | DuplicateRequirementId of requirementId: string * count: int

[<RequireQualifiedAccess>]
module RequirementIdentity =
    [<Literal>]
    let Attribute = "data-requirement-id"

    [<Literal>]
    let InstanceAttribute = "data-requirement-instance"

    let private validKey (value: string) =
        not (String.IsNullOrWhiteSpace value)
        && value.Length <= 64
        && value |> Seq.forall (fun c -> Char.IsAsciiLetterOrDigit c || c = '-' || c = '_' || c = '.')

    let private attr (name: string) (attributes: (string * string) list) =
        attributes |> List.tryFind (fun (k, _) -> k = name) |> Option.map snd

    let private textTypes = set [ "text"; "search"; "email"; "url"; "tel"; "number"; "date"; "datetime-local"; "password" ]

    /// Is this element the kind of element the component is rendered as?
    let accepts (comp: RequirementComponent) (markup: Markup) =
        match markup with
        | Element(name, attributes, _) ->
            let inputType = attr "type" attributes |> Option.defaultValue "text"

            match comp with
            | TextInput -> name = "input" && textTypes.Contains inputType
            | TextArea -> name = "textarea"
            | Select -> name = "select"
            | Checkbox -> name = "input" && inputType = "checkbox"
            | Button -> name = "button" || (name = "input" && (inputType = "submit" || inputType = "button"))
            | Link -> name = "a" && (attr "href" attributes).IsSome
            | Display -> List.contains name [ "dd"; "dt"; "time"; "output"; "p"; "span"; "strong"; "data"; "li"; "td"; "h1"; "h2"; "h3"; "h4" ]
            | Region -> List.contains name [ "section"; "form"; "fieldset"; "main"; "nav"; "aside"; "dialog" ]
        | Text _
        | Comment _ -> false

    let rec private matching (comp: RequirementComponent) (markup: Markup) : int =
        match markup with
        | Element(_, _, children) -> (if accepts comp markup then 1 else 0) + (children |> List.sumBy (matching comp))
        | _ -> 0

    /// Put the ID on the single element of the component's kind inside `markup` (the markup itself or a
    /// descendant, e.g. the input inside a field wrapper). Refuses anything that would make the identity
    /// untrustworthy: an invalid ID, no or several candidate elements, or an element already identified.
    let identifyInstance
        (comp: RequirementComponent)
        (requirementId: string)
        (instance: string option)
        (markup: Markup)
        : Result<Markup, RequirementIdentityError> =
        let extra = [ Attribute, requirementId ] @ (instance |> Option.map (fun key -> InstanceAttribute, key) |> Option.toList)

        let rec place (m: Markup) =
            match m with
            | Element(name, attributes, children) when accepts comp m ->
                match attr Attribute attributes with
                | Some existing -> Error(AlreadyIdentified existing)
                | None -> Ok(Element(name, attributes @ extra, children))
            | Element(name, attributes, children) ->
                children
                |> List.fold
                    (fun acc child -> acc |> Result.bind (fun placed -> place child |> Result.map (fun c -> placed @ [ c ])))
                    (Ok [])
                |> Result.map (fun placed -> Element(name, attributes, placed))
            | other -> Ok other

        if not (validKey requirementId) then Error(InvalidRequirementId requirementId)
        else
            match instance with
            | Some key when not (validKey key) -> Error(InvalidInstanceKey key)
            | _ ->
                match matching comp markup with
                | 0 -> Error(NoMatchingElement(requirementId, comp))
                | 1 -> place markup
                | n -> Error(AmbiguousElement(requirementId, n))

    /// `identifyInstance` for a requirement rendered once per scope.
    let identify comp requirementId markup = identifyInstance comp requirementId None markup

    /// Every (requirement ID, instance key, element name) in the markup, in document order.
    let bindings (nodes: Markup list) : (string * string option * string) list =
        let rec walk (m: Markup) =
            match m with
            | Element(name, attributes, children) ->
                let own =
                    match attr Attribute attributes with
                    | Some id -> [ id, attr InstanceAttribute attributes, name ]
                    | None -> []

                own @ (children |> List.collect walk)
            | _ -> []

        nodes |> List.collect walk

    /// Duplicate identities in one active scope: an ID carried by several elements whose instance keys
    /// are missing or not distinct.
    let duplicates (nodes: Markup list) : RequirementIdentityError list =
        bindings nodes
        |> List.groupBy (fun (id, _, _) -> id)
        |> List.choose (fun (id, found) ->
            let keys = found |> List.map (fun (_, key, _) -> key)

            if found.Length > 1 && (keys |> List.exists Option.isNone || (List.distinct keys).Length <> keys.Length) then
                Some(DuplicateRequirementId(id, found.Length))
            else
                None)
