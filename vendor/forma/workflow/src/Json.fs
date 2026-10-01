namespace Forma.Workflow

open System
open System.Text

/// An order-preserving JSON value. Numbers keep their source text so metadata
/// round-trips exactly; objects keep property order so output is deterministic.
[<RequireQualifiedAccess>]
type Json =
    | Null
    | Bool of bool
    | Number of string
    | String of string
    | Array of Json list
    | Object of (string * Json) list

/// Limits applied to untrusted input before anything else reads it.
type JsonLimits = { MaxBytes: int; MaxDepth: int }

[<RequireQualifiedAccess>]
module Json =
    let defaultLimits = { MaxBytes = 16 * 1024 * 1024; MaxDepth = 64 }

    let field name (json: Json) =
        match json with
        | Json.Object props -> props |> List.tryFind (fst >> (=) name) |> Option.map snd
        | _ -> None

    let number (value: float) =
        if Double.IsNaN value || Double.IsInfinity value then invalidArg (nameof value) "JSON numbers are finite"
        elif value = Math.Floor value && abs value < 1e15 then Json.Number((int64 value).ToString(Globalization.CultureInfo.InvariantCulture))
        else Json.Number(value.ToString("R", Globalization.CultureInfo.InvariantCulture))

    let tryFloat (json: Json) =
        match json with
        | Json.Number text ->
            match Double.TryParse(text, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture) with
            | true, v -> Some v
            | _ -> None
        | _ -> None

    let tryInt (json: Json) =
        match json with
        | Json.Number text ->
            match Int32.TryParse(text, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture) with
            | true, v -> Some v
            | _ -> tryFloat json |> Option.filter (fun f -> f = Math.Floor f && abs f < 2e9) |> Option.map int
        | _ -> None

    // -- parsing ---------------------------------------------------------------

    let rec private convert (element: System.Text.Json.JsonElement) : Result<Json, string> =
        match element.ValueKind with
        | System.Text.Json.JsonValueKind.Null -> Ok Json.Null
        | System.Text.Json.JsonValueKind.True -> Ok(Json.Bool true)
        | System.Text.Json.JsonValueKind.False -> Ok(Json.Bool false)
        | System.Text.Json.JsonValueKind.Number -> Ok(Json.Number(element.GetRawText()))
        | System.Text.Json.JsonValueKind.String -> Ok(Json.String(element.GetString()))
        | System.Text.Json.JsonValueKind.Array ->
            element.EnumerateArray()
            |> Seq.fold
                (fun acc item ->
                    match acc, convert item with
                    | Ok items, Ok value -> Ok(value :: items)
                    | (Error _ as e), _ -> e
                    | _, Error e -> Error e)
                (Ok [])
            |> Result.map (List.rev >> Json.Array)
        | System.Text.Json.JsonValueKind.Object ->
            element.EnumerateObject()
            |> Seq.fold
                (fun acc prop ->
                    match acc with
                    | Error _ -> acc
                    | Ok(seen: Set<string>, props) when seen.Contains prop.Name ->
                        Error $"duplicate property \"{prop.Name}\""
                    | Ok(seen, props) ->
                        convert prop.Value |> Result.map (fun v -> seen.Add prop.Name, (prop.Name, v) :: props))
                (Ok(Set.empty, []))
            |> Result.map (snd >> List.rev >> Json.Object)
        | kind -> Error $"unsupported JSON token {kind}"

    /// Parses untrusted text. Comments, trailing commas, duplicate keys, excess
    /// depth and oversize input are errors rather than silently tolerated.
    let parseWith (limits: JsonLimits) (text: string) : Result<Json, string> =
        if isNull text then Error "no input"
        elif Encoding.UTF8.GetByteCount text > limits.MaxBytes then Error $"input exceeds {limits.MaxBytes} bytes"
        else
            try
                let options =
                    System.Text.Json.JsonDocumentOptions(
                        MaxDepth = limits.MaxDepth,
                        AllowTrailingCommas = false,
                        CommentHandling = System.Text.Json.JsonCommentHandling.Disallow
                    )
                use doc = System.Text.Json.JsonDocument.Parse(text, options)
                convert doc.RootElement
            with :? System.Text.Json.JsonException as ex ->
                Error $"not valid JSON: {ex.Message}"

    let parse text = parseWith defaultLimits text

    // -- writing ---------------------------------------------------------------

    /// JSON string escaping. `htmlSafe` also escapes <, >, & and ' so the text
    /// can sit inside an HTML script data block without ending it.
    let escape (htmlSafe: bool) (s: string) =
        let sb = StringBuilder(s.Length + 2)
        sb.Append('"') |> ignore
        for c in s do
            match c with
            | '"' -> sb.Append("\\\"") |> ignore
            | '\\' -> sb.Append("\\\\") |> ignore
            | '\n' -> sb.Append("\\n") |> ignore
            | '\r' -> sb.Append("\\r") |> ignore
            | '\t' -> sb.Append("\\t") |> ignore
            | '\b' -> sb.Append("\\b") |> ignore
            | '\f' -> sb.Append("\\f") |> ignore
            | c when c < ' ' || c = ' ' || c = ' ' -> sb.AppendFormat("\\u{0:x4}", int c) |> ignore
            | '<' | '>' | '&' | '\'' when htmlSafe -> sb.AppendFormat("\\u{0:x4}", int c) |> ignore
            | c -> sb.Append(c) |> ignore
        sb.Append('"').ToString()

    let rec private write (htmlSafe: bool) (indent: int option) (depth: int) (sb: StringBuilder) (json: Json) =
        let newline (d: int) =
            match indent with
            | Some n -> sb.Append('\n').Append(' ', n * d) |> ignore
            | None -> ()
        let sep = if indent.IsSome then ": " else ":"
        match json with
        | Json.Null -> sb.Append("null") |> ignore
        | Json.Bool true -> sb.Append("true") |> ignore
        | Json.Bool false -> sb.Append("false") |> ignore
        | Json.Number n -> sb.Append(n) |> ignore
        | Json.String s -> sb.Append(escape htmlSafe s) |> ignore
        | Json.Array [] -> sb.Append("[]") |> ignore
        | Json.Object [] -> sb.Append("{}") |> ignore
        | Json.Array items ->
            sb.Append('[') |> ignore
            items
            |> List.iteri (fun i item ->
                if i > 0 then sb.Append(',') |> ignore
                newline (depth + 1)
                write htmlSafe indent (depth + 1) sb item)
            newline depth
            sb.Append(']') |> ignore
        | Json.Object props ->
            sb.Append('{') |> ignore
            props
            |> List.iteri (fun i (k, v) ->
                if i > 0 then sb.Append(',') |> ignore
                newline (depth + 1)
                sb.Append(escape htmlSafe k).Append(sep) |> ignore
                write htmlSafe indent (depth + 1) sb v)
            newline depth
            sb.Append('}') |> ignore

    /// Canonical, deterministic, two-space indented output with a final newline.
    let serialize (json: Json) =
        let sb = StringBuilder()
        write false (Some 2) 0 sb json
        sb.Append('\n').ToString()

    /// Single-line output.
    let compact (json: Json) =
        let sb = StringBuilder()
        write false None 0 sb json
        sb.ToString()

    /// Single-line output safe to place inside an HTML `<script type="application/json">`.
    let htmlSafe (json: Json) =
        let sb = StringBuilder()
        write true None 0 sb json
        sb.ToString()

    /// Structural equality that ignores object property order and numeric spelling.
    let rec equivalent (a: Json) (b: Json) =
        match a, b with
        | Json.Number x, Json.Number y -> x = y || (tryFloat a = tryFloat b && (tryFloat a).IsSome)
        | Json.Array xs, Json.Array ys -> xs.Length = ys.Length && List.forall2 equivalent xs ys
        | Json.Object xs, Json.Object ys ->
            xs.Length = ys.Length
            && xs |> List.forall (fun (k, v) -> ys |> List.tryFind (fst >> (=) k) |> Option.exists (snd >> equivalent v))
        | _ -> a = b
