namespace FormaStudio.Engine

open System
open System.Text.RegularExpressions

/// One icon as the pinned Forma release's `icons/registry.json` describes it.
/// Labels and keywords are Forma's suggested metadata for finding an icon;
/// they are never an accessible name for a control (Forma ICON-008).
type IconEntry =
    { Name: IconName
      Category: string
      Label: string
      Keywords: string list
      /// SHA-256 of the compiled static SVG, as the release recorded it.
      SvgSha256: string }

type IconRegistry =
    { FormaVersion: string
      Entries: IconEntry list }

/// The pinned release's icon collection, after Studio verified each icon's
/// compiled artwork against the registry digest. Only verified icons are offered
/// or rendered; the rest are listed with the reason they were refused.
type IconCatalog =
    { FormaVersion: string
      Entries: IconEntry list
      /// The release's decorative inline snippet, parsed into Forma's escaping markup tree.
      Artwork: Map<IconName, Forma.Workflow.Markup>
      Rejected: (string * string) list }

type IconAvailability =
    | IconsNotLoaded
    | IconsUnavailable of reason: string
    | IconsAvailable of IconCatalog

/// The two compiled files each icon has in the release.
type IconAssetKind =
    /// `icons/<name>.svg`, the static SVG the registry digest covers.
    | StaticSvg
    /// `icons/html/<name>.html`, the decorative inline snippet.
    | InlineSnippet

/// Assets received so far for a registry, while the host fetches them.
type IconLoad =
    { Registry: IconRegistry
      Received: Map<IconName * IconAssetKind, Result<string, string>> }

/// A deliberately small, closed reader for the compiled icon files. It accepts
/// only the element and attribute vocabulary the Forma icon compiler emits and
/// refuses everything else (scripts, event attributes, foreignObject, external
/// references, entities, text, comments), so nothing executable can reach an
/// export even if a file was tampered with (Forma ICON-007, ICON-013).
module internal IconMarkup =
    type Tag = { Name: string; Attributes: (string * string) list; Children: Tag list }

    let maxBytes = 16384

    let private paint = [ "fill"; "stroke"; "stroke-width"; "stroke-linecap"; "stroke-linejoin" ]
    let private geometry = [ "path"; "circle"; "ellipse"; "rect"; "line"; "polyline"; "polygon" ]

    let private allowed =
        Map.ofList
            [ "ef-icon", ([ "class" ], [ "span" ])
              "span", ([ "class"; "data-ef-icon" ], [ "svg" ])
              "svg", ([ "xmlns"; "class"; "viewBox"; "aria-hidden"; "focusable" ] @ paint, "g" :: geometry)
              "g", (paint, geometry)
              "path", ("d" :: paint, [])
              "circle", ([ "cx"; "cy"; "r" ] @ paint, [])
              "ellipse", ([ "cx"; "cy"; "rx"; "ry" ] @ paint, [])
              "rect", ([ "x"; "y"; "width"; "height"; "rx"; "ry" ] @ paint, [])
              "line", ([ "x1"; "y1"; "x2"; "y2" ] @ paint, [])
              "polyline", ("points" :: paint, [])
              "polygon", ("points" :: paint, []) ]

    let svgNamespace = "http://www.w3.org/2000/svg"
    let private safeValue = Regex(@"\A[A-Za-z0-9 .,#%_-]{0,4096}\z", RegexOptions.CultureInvariant)
    let private nameChar (c: char) = Char.IsAsciiLetterOrDigit c || c = '-'

    let private valueOk (attribute: string) (value: string) =
        if attribute = "xmlns" then value = svgNamespace
        elif attribute = "viewBox" then value = "0 0 24 24"
        else safeValue.IsMatch value

    /// Parses one element (and its children) starting at `i`; returns the tag and the next index.
    let rec private element (text: string) (i: int) (parent: string option) : Result<Tag * int, string> =
        let rec skip j = if j < text.Length && Char.IsWhiteSpace text.[j] then skip (j + 1) else j
        let rec nameEnd j = if j < text.Length && nameChar text.[j] then nameEnd (j + 1) else j
        let name j = let k = nameEnd j in text.Substring(j, k - j), k
        if i >= text.Length || text.[i] <> '<' then Error "expected an element"
        else
            let tag, afterName = name (i + 1)
            match Map.tryFind tag allowed with
            | None -> Error(sprintf "element <%s> is not part of the Forma icon vocabulary" tag)
            | Some(_, _) when parent |> Option.exists (fun p -> not (List.contains tag (snd allowed.[p]))) ->
                Error(sprintf "<%s> may not appear inside <%s>" tag parent.Value)
            | Some(attributeNames, _) ->
                let rec attributes j acc =
                    let k = skip j
                    if k >= text.Length then Error "unterminated element"
                    elif text.[k] = '/' && k + 1 < text.Length && text.[k + 1] = '>' then Ok(List.rev acc, k + 2, true)
                    elif text.[k] = '>' then Ok(List.rev acc, k + 1, false)
                    elif k = j then Error "attributes must be separated by whitespace"
                    else
                        let attribute, afterAttribute = name k
                        if attribute = "" then Error "malformed attribute"
                        elif not (List.contains attribute attributeNames) then Error(sprintf "attribute %s is not allowed on <%s>" attribute tag)
                        elif acc |> List.exists (fst >> (=) attribute) then Error(sprintf "attribute %s is repeated" attribute)
                        elif afterAttribute + 1 >= text.Length || text.[afterAttribute] <> '=' || text.[afterAttribute + 1] <> '"' then Error "attribute values must be double-quoted"
                        else
                            match text.IndexOf('"', afterAttribute + 2) with
                            | -1 -> Error "unterminated attribute value"
                            | close ->
                                let value = text.Substring(afterAttribute + 2, close - afterAttribute - 2)
                                if valueOk attribute value then attributes (close + 1) ((attribute, value) :: acc)
                                else Error(sprintf "attribute %s has a value outside the icon vocabulary" attribute)
                let rec children j acc =
                    let k = skip j
                    let closing = "</" + tag
                    if text.Length - k >= closing.Length && String.CompareOrdinal(text, k, closing, 0, closing.Length) = 0 then
                        let e = skip (k + closing.Length)
                        if e < text.Length && text.[e] = '>' then Ok(List.rev acc, e + 1) else Error(sprintf "malformed </%s>" tag)
                    elif k < text.Length && text.[k] = '<' then
                        element text k (Some tag) |> Result.bind (fun (child, next) -> children next (child :: acc))
                    else Error "text, comments and entities are not allowed in icon markup"
                attributes afterName []
                |> Result.bind (fun (attrs, next, selfClosing) ->
                    if selfClosing then Ok({ Name = tag; Attributes = attrs; Children = [] }, next)
                    else children next [] |> Result.map (fun (kids, after) -> { Name = tag; Attributes = attrs; Children = kids }, after))

    /// Parses a whole file whose single root element must be `root`.
    let parse (root: string) (text: string) : Result<Tag, string> =
        if Text.Encoding.UTF8.GetByteCount text > maxBytes then Error "the file is missing or larger than an icon can be"
        else
            let trimmed = text.Trim()
            element trimmed 0 None
            |> Result.bind (fun (tag, next) ->
                if next <> trimmed.Length then Error "content follows the root element"
                elif tag.Name <> root then Error(sprintf "the root element is <%s>, not <%s>" tag.Name root)
                else Ok tag)

    let attribute name (tag: Tag) = tag.Attributes |> List.tryFind (fst >> (=) name) |> Option.map snd

    /// Structural equality that ignores attribute order.
    let rec same (a: Tag) (b: Tag) =
        a.Name = b.Name && Set.ofList a.Attributes = Set.ofList b.Attributes
        && a.Children.Length = b.Children.Length && List.forall2 same a.Children b.Children

    let rec toMarkup (tag: Tag) : Forma.Workflow.Markup = Forma.Workflow.Markup.el tag.Name tag.Attributes (tag.Children |> List.map toMarkup)

[<RequireQualifiedAccess>]
module IconRegistry =
    let private sha = Regex(@"\A[0-9a-f]{64}\z", RegexOptions.CultureInvariant)
    let private version = Regex(@"\A[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?\z", RegexOptions.CultureInvariant)
    let maxIcons = 2000

    let private text name (json: JsonValue) =
        match Json.field name json with
        | Some(JString s) when not (String.IsNullOrWhiteSpace s) && s.Length <= 200 -> Ok s
        | _ -> Error(sprintf "'%s' must be a non-empty string" name)

    let private entry (json: JsonValue) : Result<IconEntry, string> =
        match Json.field "name" json with
        | Some(JString raw) ->
            match IconName.parse raw with
            | Error e -> Error(sprintf "icon name is invalid: %s" e)
            | Ok name ->
                let keywords =
                    match Json.field "keywords" json with
                    | Some(JArray items) when items |> List.forall (function JString _ -> true | _ -> false) -> Ok(items |> List.choose (function JString s -> Some s | _ -> None))
                    | _ -> Error "keywords must be a list of strings"
                match text "category" json, text "label" json, keywords, Json.field "svg" json, Json.field "html" json, Json.field "svgSha256" json with
                | Error e, _, _, _, _, _ | _, Error e, _, _, _, _ | _, _, Error e, _, _, _ -> Error(sprintf "%s: %s" raw e)
                | Ok category, Ok label, Ok words, Some(JString svg), Some(JString html), Some(JString digest) ->
                    if svg <> sprintf "icons/%s.svg" raw || html <> sprintf "icons/html/%s.html" raw then Error(sprintf "%s: asset paths must be icons/%s.svg and icons/html/%s.html" raw raw raw)
                    elif not (sha.IsMatch digest) then Error(sprintf "%s: svgSha256 must be 64 lowercase hex digits" raw)
                    else Ok { Name = name; Category = category; Label = label; Keywords = words; SvgSha256 = digest }
                | _ -> Error(sprintf "%s: svg, html and svgSha256 are required strings" raw)
        | _ -> Error "every icon needs a string name"

    /// Reads `icons/registry.json` (schema 1, 24-unit grid). The whole registry is
    /// refused if any entry is malformed or a name repeats: a partial registry would
    /// silently change which names count as known.
    let parse (registryText: string) : Result<IconRegistry, string> =
        match Json.parse registryText with
        | Error e -> Error(sprintf "the icon registry is not valid JSON (%s)" e)
        | Ok json ->
            match Json.field "schemaVersion" json, Json.field "grid" json, Json.field "formaVersion" json, Json.field "icons" json with
            | Some(JNumber "1"), Some(JNumber "24"), Some(JString v), Some(JArray icons) when version.IsMatch v ->
                if icons.Length > maxIcons then Error "the icon registry lists more icons than Studio accepts"
                else
                    let folded =
                        icons |> List.fold (fun acc item -> acc |> Result.bind (fun xs -> entry item |> Result.map (fun e -> e :: xs))) (Ok [])
                    folded
                    |> Result.bind (fun reversed ->
                        let entries = List.rev reversed
                        if (entries |> List.distinctBy _.Name).Length <> entries.Length then Error "the icon registry repeats a name"
                        else Ok { FormaVersion = v; Entries = entries })
            | _ -> Error "the icon registry is not schema 1 on the 24-unit grid with a Forma version"

    /// Relative paths of an entry's compiled files, as the registry records them.
    let assetPath (kind: IconAssetKind) (name: IconName) =
        match kind with
        | StaticSvg -> sprintf "%s.svg" (IconName.value name)
        | InlineSnippet -> sprintf "html/%s.html" (IconName.value name)

[<RequireQualifiedAccess>]
module IconCatalog =
    let private sha256 (text: string) =
        Security.Cryptography.SHA256.HashData(Text.Encoding.UTF8.GetBytes text) |> Convert.ToHexString |> _.ToLowerInvariant()

    /// Verifies one icon's compiled files: the static SVG must match the registry
    /// digest, and the inline snippet must be the same SVG marked decorative
    /// (`aria-hidden="true"`), wrapped for exactly this name. Returns the snippet.
    let verify (entry: IconEntry) (svgText: string) (snippetText: string) : Result<Forma.Workflow.Markup, string> =
        let name = IconName.value entry.Name
        if sha256 svgText <> entry.SvgSha256 then Error "its SVG does not match the registry digest"
        else
            match IconMarkup.parse "svg" svgText, IconMarkup.parse "ef-icon" snippetText with
            | Error e, _ -> Error(sprintf "its SVG was refused: %s" e)
            | _, Error e -> Error(sprintf "its inline snippet was refused: %s" e)
            | Ok svg, Ok snippet ->
                match snippet.Children with
                | [ { Name = "span"; Children = [ inner ] } as span ] when IconMarkup.attribute "data-ef-icon" span = Some name ->
                    let decorative = IconMarkup.attribute "aria-hidden" inner = Some "true"
                    let stripped = { inner with Attributes = inner.Attributes |> List.filter (fun (k, _) -> k <> "aria-hidden" && k <> "focusable") }
                    if not decorative then Error "its inline snippet is not marked decorative"
                    elif not (IconMarkup.same stripped svg) then Error "its inline snippet differs from the verified SVG"
                    else Ok(IconMarkup.toMarkup snippet)
                | _ -> Error "its inline snippet is not wrapped for this icon name"

    let private expected (registry: IconRegistry) =
        registry.Entries |> List.collect (fun e -> [ e.Name, StaticSvg; e.Name, InlineSnippet ])

    let start (registry: IconRegistry) = { Registry = registry; Received = Map.empty }

    /// Records one fetched file (or why it could not be fetched).
    let receive (name: IconName) (kind: IconAssetKind) (content: Result<string, string>) (load: IconLoad) =
        if load.Registry.Entries |> List.exists (fun e -> e.Name = name) then { load with Received = load.Received.Add((name, kind), content) } else load

    let isComplete (load: IconLoad) = expected load.Registry |> List.forall load.Received.ContainsKey

    /// The catalog once every file has arrived. Icons whose files are missing or
    /// fail verification are refused one by one; the collection is unavailable
    /// only when none survives.
    let finish (load: IconLoad) : IconAvailability =
        let file name kind = load.Received |> Map.tryFind (name, kind) |> Option.defaultValue (Error "it was not fetched")
        let results =
            load.Registry.Entries
            |> List.map (fun e ->
                match file e.Name StaticSvg, file e.Name InlineSnippet with
                | Ok svg, Ok html -> e, verify e svg html
                | Error why, _ | _, Error why -> e, Error why)
        let verified = results |> List.choose (fun (e, r) -> r |> Result.toOption |> Option.map (fun m -> e, m))
        let rejected = results |> List.choose (fun (e, r) -> match r with Error why -> Some(IconName.value e.Name, why) | Ok _ -> None)
        if List.isEmpty verified then
            IconsUnavailable(sprintf "No icon in Forma %s's icon collection passed verification, so icons are unavailable." load.Registry.FormaVersion)
        else
            IconsAvailable
                { FormaVersion = load.Registry.FormaVersion
                  Entries = verified |> List.map fst
                  Artwork = verified |> List.map (fun (e, m) -> e.Name, m) |> Map.ofList
                  Rejected = rejected }

    /// Builds a catalog from a registry and a reader for its files (the CLI and
    /// tests read a directory; the editor fetches them through Limen).
    let fromFiles (registryText: string) (read: string -> Result<string, string>) : IconAvailability =
        match IconRegistry.parse registryText with
        | Error e -> IconsUnavailable(sprintf "The pinned Forma icon registry was refused: %s." e)
        | Ok registry ->
            expected registry
            |> List.fold (fun load (name, kind) -> receive name kind (read (IconRegistry.assetPath kind name)) load) (start registry)
            |> finish

    /// Why icons are unavailable when the pinned release has no registry at all.
    let notInRelease =
        "The pinned Forma release does not include the icon collection, so icons are unavailable. Icon names already in documents are kept and not shown."

    let tryEntry (name: IconName) (catalog: IconCatalog) = catalog.Entries |> List.tryFind (fun e -> e.Name = name)

    /// Verified inline artwork for a name, stamped with the release it came from.
    let artwork (name: IconName) (catalog: IconCatalog) : Forma.Workflow.Markup option =
        match catalog.Artwork |> Map.tryFind name with
        | Some(Forma.Workflow.Element(tag, attributes, children)) -> Some(Forma.Workflow.Element(tag, attributes @ [ "data-forma-version", catalog.FormaVersion ], children))
        | _ -> None

    /// Entries whose name, category, label or keyword contains the query (case-insensitive).
    let search (query: string) (catalog: IconCatalog) =
        let q = query.Trim().ToLowerInvariant()
        if q = "" then catalog.Entries
        else
            catalog.Entries
            |> List.filter (fun e -> IconName.value e.Name :: e.Category :: e.Label :: e.Keywords |> List.exists (fun w -> w.ToLowerInvariant().Contains q))

    /// How a stored icon stands against the pinned release.
    type Status =
        | NoIcon
        | Shown of IconEntry
        | NotInRelease of name: string * formaVersion: string
        | CollectionUnavailable of name: string
        | Unrecognized

    let status (availability: IconAvailability) (icon: IconRef option) =
        match icon, availability with
        | None, _ -> NoIcon
        | Some(MalformedIcon _), _ -> Unrecognized
        | Some(NamedIcon n), IconsAvailable c -> tryEntry n c |> Option.map Shown |> Option.defaultValue (NotInRelease(IconName.value n, c.FormaVersion))
        | Some(NamedIcon n), _ -> CollectionUnavailable(IconName.value n)

    let describe (status: Status) =
        match status with
        | NoIcon -> "No icon."
        | Shown e -> sprintf "%s (%s)." e.Label (IconName.value e.Name)
        | NotInRelease(n, v) -> sprintf "\"%s\" is not in Forma %s; the name is kept and not shown." n v
        | CollectionUnavailable n -> sprintf "\"%s\" is kept; the pinned Forma release has no icons to show it with." n
        | Unrecognized -> "The stored icon value is not a valid icon name; it is kept as data and never shown."
