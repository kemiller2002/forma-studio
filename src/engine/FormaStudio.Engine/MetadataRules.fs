namespace FormaStudio.Engine

open System.Globalization

/// How a field's value resolves on one object, with its origin kept visible
/// (FDA-902, FDA-920..928, FMD-SCHEMA-006).
type ResolvedValue =
    | ResolvedExplicit of MetaValue
    | ResolvedDefault of MetaValue
    | ResolvedDerived of MetaValue * rule: string
    | ResolvedSourceBound of MetaValue * SourceBinding
    | ResolvedUnknown
    | ResolvedUnavailable of reason: string
    | ResolvedInvalid of reason: string
    | ResolvedConflict of authored: MetaValue * derived: MetaValue * rule: string
    | ResolvedMissing of required: bool

[<RequireQualifiedAccess>]
module MetadataRules =
    let private allowedSchemes = set [ "http"; "https"; "mailto" ]

    let disclosureSourceOnly = { Scopes = Set.empty; DerivedPresentation = false }

    let typeName fieldType =
        match fieldType with
        | TextField _ -> "text"
        | NumberField _ -> "number"
        | BooleanField -> "boolean"
        | EnumField _ -> "enum"
        | DateTimeField -> "datetime"
        | UrlField -> "url"
        | TagsField true -> "tags-ordered"
        | TagsField false -> "tags"

    /// Validates a value against its field type and returns the canonical form
    /// (FDA-908, FDA-965): trailing decimal zeros dropped, date/time offsets kept,
    /// set-like tags sorted and de-duplicated, ordered tags kept in authored order.
    let normalize (fieldType: FieldType) (value: MetaValue) : Result<MetaValue, string> =
        match fieldType, value with
        | TextField maxLength, Text text ->
            match maxLength with
            | Some limit when text.Length > limit -> Error(sprintf "text is longer than %d characters" limit)
            | _ -> Ok(Text text)
        | NumberField(minimum, maximum), Number n ->
            let canonical = System.Decimal.Parse(Json.decimalText n, CultureInfo.InvariantCulture)
            match minimum, maximum with
            | Some lo, _ when canonical < lo -> Error(sprintf "%s is below the minimum %s" (Json.decimalText canonical) (Json.decimalText lo))
            | _, Some hi when canonical > hi -> Error(sprintf "%s is above the maximum %s" (Json.decimalText canonical) (Json.decimalText hi))
            | _ -> Ok(Number canonical)
        | BooleanField, Boolean b -> Ok(Boolean b)
        | EnumField options, Enum optionId ->
            if options |> List.exists (fun o -> o.Id = optionId) then Ok(Enum optionId)
            else Error(sprintf "'%s' is not one of the allowed values" optionId)
        | DateTimeField, DateTime text ->
            let dateTimeFormats = [| "yyyy-MM-dd'T'HH:mmK"; "yyyy-MM-dd'T'HH:mm:ssK"; "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK" |]
            match System.DateTimeOffset.TryParseExact(text, dateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None) with
            | true, parsed when text.EndsWith "Z" || text.Length > 16 && (text.Contains "+" || text.Substring(16).Contains "-") ->
                // The authored offset is significant (FDA-908), so it is preserved rather than converted to UTC.
                Ok(DateTime(parsed.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture).Replace("+00:00", "Z")))
            | _ ->
                match System.DateTimeOffset.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal) with
                | true, _ -> Ok(DateTime text)
                | _ -> Error(sprintf "'%s' is not an ISO 8601 date or a date-time with an offset" text)
        | UrlField, Url text ->
            if not (System.Uri.IsWellFormedUriString(text, System.UriKind.Absolute)) then Error(sprintf "'%s' is not an absolute URL" text)
            else
                let scheme = System.Uri(text, System.UriKind.Absolute).Scheme.ToLowerInvariant()
                if allowedSchemes.Contains scheme then Ok(Url text)
                else Error(sprintf "URL scheme '%s' is not allowed (http, https or mailto)" scheme)
        | TagsField ordered, TagList tags ->
            if tags |> List.exists System.String.IsNullOrWhiteSpace then Error "tags cannot be empty"
            elif ordered then
                if List.distinct tags <> tags then Error "ordered tags cannot repeat" else Ok(TagList tags)
            else Ok(TagList(tags |> List.distinct |> List.sortWith (fun a b -> System.String.CompareOrdinal(a, b))))
        | expected, _ -> Error(sprintf "value does not match the field type %s" (typeName expected))

    /// Text used by mappings, search and display. Enum values match on the stable id.
    let matchKeys value =
        match value with
        | Text text -> [ text ]
        | Number n -> [ Json.decimalText n ]
        | Boolean b -> [ (if b then "true" else "false") ]
        | Enum id -> [ id ]
        | DateTime text -> [ text ]
        | Url text -> [ text ]
        | TagList tags -> tags

    /// Human-readable rendering that uses enum labels (localized projections) while
    /// the stored value keeps the stable id.
    let display (definition: FieldDefinition option) value =
        match definition, value with
        | Some { Type = EnumField options }, Enum id ->
            options |> List.tryFind (fun o -> o.Id = id) |> Option.map _.Label |> Option.defaultValue id
        | _, Boolean true -> "Yes"
        | _, Boolean false -> "No"
        | _, TagList tags -> System.String.Join(", ", tags)
        | _, other -> other |> matchKeys |> List.head

    let applies (definition: FieldDefinition) (kind: TargetKind) = definition.AppliesTo.Contains kind

    /// Resolves one field on one object. `derived` is the value produced by the
    /// field's explicit derivation rule, if it has one and it yields a value.
    let resolve (definition: FieldDefinition) (stored: StoredValue option) (derived: (MetaValue * string) option) =
        let checkedValue value = normalize definition.Type value

        match stored, derived with
        | Some(Explicit value), derivedValue ->
            match checkedValue value, derivedValue with
            | Error reason, _ -> ResolvedInvalid reason
            | Ok authored, Some(d, rule) when authored <> d -> ResolvedConflict(authored, d, rule)
            | Ok authored, _ -> ResolvedExplicit authored
        | Some(SourceBound(value, binding)), _ ->
            match checkedValue value with
            | Error reason -> ResolvedInvalid reason
            | Ok v -> ResolvedSourceBound(v, binding)
        | Some UnknownValue, _ -> ResolvedUnknown
        | Some(UnavailableValue reason), _ -> ResolvedUnavailable reason
        | None, Some(value, rule) -> ResolvedDerived(value, rule)
        | None, None ->
            match definition.Default with
            | Some value -> ResolvedDefault value
            | None -> ResolvedMissing definition.Required

    /// The canonical value a resolution represents, when it has one.
    let valueOf resolved =
        match resolved with
        | ResolvedExplicit v
        | ResolvedDefault v
        | ResolvedDerived(v, _)
        | ResolvedSourceBound(v, _) -> Some v
        | ResolvedConflict(authored, _, _) -> Some authored
        | _ -> None

    /// The value-state vocabulary shared with Forma's `data-ef-value-state` and
    /// Folio's rendered metadata (explicit carries no tag).
    let stateName resolved =
        match resolved with
        | ResolvedExplicit _ -> "explicit"
        | ResolvedDefault _ -> "default"
        | ResolvedDerived _ -> "derived"
        | ResolvedSourceBound _ -> "source-bound"
        | ResolvedUnknown -> "unknown"
        | ResolvedUnavailable _ -> "unavailable"
        | ResolvedInvalid _ -> "invalid"
        | ResolvedConflict _ -> "invalid"
        | ResolvedMissing _ -> "missing"

    let allows scope (definition: FieldDefinition) = definition.Disclosure.Scopes.Contains scope
