namespace Forma.Workflow

open System
open System.Text

/// A tiny typed HTML tree. Every text node and attribute value is escaped when
/// rendered, so untrusted labels, metadata and extension values can never become
/// markup. There is deliberately no "raw HTML" case.
type Markup =
    | Element of name: string * attributes: (string * string) list * children: Markup list
    | Text of string
    /// An HTML comment. "--" and ">" sequences are neutralised.
    | Comment of string

[<RequireQualifiedAccess>]
module Markup =
    let el name attrs children = Element(name, attrs, children)
    let text s = Text s

    let private voidElements = set [ "area"; "base"; "br"; "col"; "embed"; "hr"; "img"; "input"; "link"; "meta"; "source"; "track"; "wbr" ]
    let private svgLeaves = set [ "path"; "circle"; "rect"; "line"; "polyline"; "polygon"; "ellipse"; "use" ]
    let private inlineElements =
        set [ "a"; "abbr"; "b"; "bdi"; "bdo"; "cite"; "code"; "data"; "dfn"; "em"; "i"; "kbd"; "mark"; "q"; "s"; "samp"
              "small"; "span"; "strong"; "sub"; "sup"; "time"; "u"; "var"; "button"; "label"; "output" ]
    let private validName (n: string) =
        not (String.IsNullOrEmpty n) && n |> Seq.forall (fun c -> Char.IsAsciiLetterOrDigit c || c = '-' || c = ':' || c = '_')

    let escapeText (s: string) =
        let sb = StringBuilder(s.Length)
        for c in s do
            match c with
            | '&' -> sb.Append("&amp;") |> ignore
            | '<' -> sb.Append("&lt;") |> ignore
            | '>' -> sb.Append("&gt;") |> ignore
            | c when Char.IsControl c && c <> '\n' && c <> '\t' -> sb.Append(' ') |> ignore
            | c -> sb.Append(c) |> ignore
        sb.ToString()

    let escapeAttribute (s: string) =
        let sb = StringBuilder(s.Length)
        for c in s do
            match c with
            | '&' -> sb.Append("&amp;") |> ignore
            | '<' -> sb.Append("&lt;") |> ignore
            | '>' -> sb.Append("&gt;") |> ignore
            | '"' -> sb.Append("&quot;") |> ignore
            | '\'' -> sb.Append("&#39;") |> ignore
            | c when Char.IsControl c -> sb.Append(' ') |> ignore
            | c -> sb.Append(c) |> ignore
        sb.ToString()

    let private isInline (m: Markup) =
        match m with
        | Text _ -> true
        | Comment _ -> false
        | Element(name, _, _) -> inlineElements.Contains name

    let private openTag (name: string) (attrs: (string * string) list) =
        if not (validName name) then invalidArg (nameof name) $"invalid element name {name}"
        let sb = StringBuilder()
        sb.Append('<').Append(name) |> ignore
        for (k, v) in attrs do
            if not (validName k) then invalidArg (nameof attrs) $"invalid attribute name {k}"
            // Event-handler attributes are never emitted, whatever the caller asks.
            if k.StartsWith("on", StringComparison.OrdinalIgnoreCase) then invalidArg (nameof attrs) $"event handler attribute {k} is not allowed"
            if v = "" && (k = "hidden" || k = "disabled" || k = "open" || k = "checked" || k = "required" || k = "inert") then sb.Append(' ').Append(k) |> ignore
            else sb.Append(' ').Append(k).Append("=\"").Append(escapeAttribute v).Append('"') |> ignore
        sb.ToString()

    let rec private renderInline (sb: StringBuilder) (m: Markup) =
        match m with
        | Text s -> sb.Append(escapeText s) |> ignore
        | Comment c -> sb.Append("<!-- ").Append(c.Replace("--", "- -").Replace(">", "&gt;")).Append(" -->") |> ignore
        | Element(name, attrs, children) ->
            if name = "script" || name = "style" then invalidArg (nameof m) $"<{name}> content is written with Markup.scriptData/Markup.styleText only"
            sb.Append(openTag name attrs) |> ignore
            if voidElements.Contains name then sb.Append('>') |> ignore
            elif svgLeaves.Contains name && children.IsEmpty then sb.Append(" />") |> ignore
            else
                sb.Append('>') |> ignore
                children |> List.iter (renderInline sb)
                sb.Append("</").Append(name).Append('>') |> ignore

    /// A JSON data block. The JSON is written with <, >, & escaped so the text can
    /// never close the element or start markup.
    let scriptData (attrs: (string * string) list) (json: Json) =
        Element("script", ("type", "application/json") :: attrs, [ Text(Json.htmlSafe json) ])

    /// A module script reference (no inline code). Only used for declared public runtime modules.
    let moduleScript (src: string) = Element("script", [ "type", "module"; "src", src ], [])

    let rec private render (sb: StringBuilder) (depth: int) (m: Markup) =
        let pad = String(' ', depth * 2)
        match m with
        | Element("script", attrs, children) ->
            // Only JSON data blocks or external module references reach here.
            sb.Append(pad).Append(openTag "script" attrs).Append('>') |> ignore
            for c in children do
                match c with
                | Text t -> sb.Append(t.Replace("</", "<\\/")) |> ignore
                | _ -> invalidArg (nameof m) "script content must be text"
            sb.Append("</script>\n") |> ignore
        | Element(name, attrs, children) when voidElements.Contains name -> sb.Append(pad).Append(openTag name attrs).Append(">\n") |> ignore
        | Element(name, attrs, []) when svgLeaves.Contains name -> sb.Append(pad).Append(openTag name attrs).Append(" />\n") |> ignore
        | Element(name, attrs, children) when children |> List.forall isInline ->
            sb.Append(pad) |> ignore
            renderInline sb (Element(name, attrs, children))
            sb.Append('\n') |> ignore
        | Element(name, attrs, children) ->
            sb.Append(pad).Append(openTag name attrs).Append(">\n") |> ignore
            children |> List.iter (render sb (depth + 1))
            sb.Append(pad).Append("</").Append(name).Append(">\n") |> ignore
        | Text s -> sb.Append(pad).Append(escapeText s).Append('\n') |> ignore
        | Comment _ ->
            sb.Append(pad) |> ignore
            renderInline sb m
            sb.Append('\n') |> ignore

    /// Deterministic, indented HTML for a list of top-level nodes.
    let renderAll (nodes: Markup list) =
        let sb = StringBuilder()
        nodes |> List.iter (render sb 0)
        sb.ToString()

    let toHtml (m: Markup) = renderAll [ m ]

    /// Compact single-line HTML (used by the live editor view).
    let toCompactHtml (m: Markup) =
        let sb = StringBuilder()
        let rec go (m: Markup) =
            match m with
            | Element("script", attrs, children) ->
                sb.Append(openTag "script" attrs).Append('>') |> ignore
                for c in children do
                    match c with
                    | Text t -> sb.Append(t.Replace("</", "<\\/")) |> ignore
                    | _ -> ()
                sb.Append("</script>") |> ignore
            | Element(name, attrs, _) when voidElements.Contains name -> sb.Append(openTag name attrs).Append('>') |> ignore
            | Element(name, attrs, []) when svgLeaves.Contains name -> sb.Append(openTag name attrs).Append(" />") |> ignore
            | Element(name, attrs, children) ->
                sb.Append(openTag name attrs).Append('>') |> ignore
                children |> List.iter go
                sb.Append("</").Append(name).Append('>') |> ignore
            | other -> renderInline sb other
        go m
        sb.ToString()

    /// Formats a CSS length in px with invariant culture and no trailing zeros.
    let px (v: float) =
        let r = Math.Round(v, 1)
        r.ToString("0.#", Globalization.CultureInfo.InvariantCulture) + "px"

    let number (v: float) =
        Math.Round(v, 1).ToString("0.#", Globalization.CultureInfo.InvariantCulture)

    /// Builds a style attribute from custom properties whose values the caller
    /// constructs from typed data (numbers, validated colours, token names).
    let style (props: (string * string) list) =
        props |> List.map (fun (k, v) -> k + ": " + v + ";") |> String.concat " "
