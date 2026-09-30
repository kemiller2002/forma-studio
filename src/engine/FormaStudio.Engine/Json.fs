namespace FormaStudio.Engine

open System
open System.Globalization
open System.Text
open System.Text.Json

/// A small immutable JSON tree. Object members keep the order they were built in,
/// which is how the codec produces deterministic output. Numbers keep their source
/// text so unknown data round-trips without precision changes.
type JsonValue =
    | JNull
    | JBool of bool
    | JNumber of string
    | JString of string
    | JArray of JsonValue list
    | JObject of (string * JsonValue) list

[<RequireQualifiedAccess>]
module Json =
    let ofInt (value: int) = JNumber(value.ToString(CultureInfo.InvariantCulture))

    /// Canonical decimal text: invariant culture, no trailing fractional zeros.
    let decimalText (value: decimal) =
        let text = value.ToString("0.############################", CultureInfo.InvariantCulture)
        if text = "-0" then "0" else text

    let ofDecimal (value: decimal) = JNumber(decimalText value)

    let str (value: string) = JString value
    let arr (values: JsonValue list) = JArray values
    let obj (members: (string * JsonValue) list) = JObject members

    /// Object members whose value is absent are omitted rather than written as null.
    let objOpt (members: (string * JsonValue option) list) =
        members |> List.choose (fun (name, value) -> value |> Option.map (fun v -> name, v)) |> JObject

    let field (name: string) (value: JsonValue) =
        match value with
        | JObject members -> members |> List.tryFind (fst >> (=) name) |> Option.map snd
        | _ -> None

    let private escape (builder: StringBuilder) (text: string) =
        builder.Append('"') |> ignore
        text
        |> Seq.iter (fun c ->
            match c with
            | '"' -> builder.Append("\\\"") |> ignore
            | '\\' -> builder.Append("\\\\") |> ignore
            | '\n' -> builder.Append("\\n") |> ignore
            | '\r' -> builder.Append("\\r") |> ignore
            | '\t' -> builder.Append("\\t") |> ignore
            | '\b' -> builder.Append("\\b") |> ignore
            | '\f' -> builder.Append("\\f") |> ignore
            | c when c < ' ' || c = '\u2028' || c = '\u2029' -> builder.Append(sprintf "\\u%04x" (Checked.int c)) |> ignore
            | c -> builder.Append(c) |> ignore)
        builder.Append('"') |> ignore

    /// Serializes with two-space indentation, LF line endings and a trailing newline.
    let serialize (value: JsonValue) =
        let builder = StringBuilder()
        let indent depth = builder.Append(String(' ', depth * 2)) |> ignore

        let rec write depth value =
            match value with
            | JNull -> builder.Append("null") |> ignore
            | JBool b -> builder.Append(if b then "true" else "false") |> ignore
            | JNumber n -> builder.Append(n) |> ignore
            | JString s -> escape builder s
            | JArray [] -> builder.Append("[]") |> ignore
            | JObject [] -> builder.Append("{}") |> ignore
            | JArray items ->
                builder.Append("[\n") |> ignore
                items
                |> List.iteri (fun index item ->
                    indent (depth + 1)
                    write (depth + 1) item
                    builder.Append(if index < items.Length - 1 then ",\n" else "\n") |> ignore)
                indent depth
                builder.Append(']') |> ignore
            | JObject members ->
                builder.Append("{\n") |> ignore
                members
                |> List.iteri (fun index (name, item) ->
                    indent (depth + 1)
                    escape builder name
                    builder.Append(": ") |> ignore
                    write (depth + 1) item
                    builder.Append(if index < members.Length - 1 then ",\n" else "\n") |> ignore)
                indent depth
                builder.Append('}') |> ignore

        write 0 value
        builder.Append('\n').ToString()

    let rec private ofElement (element: JsonElement) =
        match element.ValueKind with
        | JsonValueKind.Null -> JNull
        | JsonValueKind.True -> JBool true
        | JsonValueKind.False -> JBool false
        | JsonValueKind.Number -> JNumber(element.GetRawText())
        | JsonValueKind.String ->
            match element.GetString() with
            | null -> JNull
            | text -> JString text
        | JsonValueKind.Array -> element.EnumerateArray() |> Seq.map ofElement |> List.ofSeq |> JArray
        | JsonValueKind.Object ->
            element.EnumerateObject() |> Seq.map (fun p -> p.Name, ofElement p.Value) |> List.ofSeq |> JObject
        | _ -> JNull

    /// Parses JSON text. Duplicate object keys are rejected because they make
    /// canonical data ambiguous.
    let parse (text: string) : Result<JsonValue, string> =
        try
            use document = JsonDocument.Parse(text, JsonDocumentOptions(AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow))
            let value = ofElement document.RootElement

            let rec duplicates value =
                match value with
                | JObject members ->
                    let names = members |> List.map fst
                    (if List.length (List.distinct names) <> List.length names then [ "duplicate object key" ] else [])
                    @ (members |> List.collect (snd >> duplicates))
                | JArray items -> items |> List.collect duplicates
                | _ -> []

            match duplicates value with
            | [] -> Ok value
            | problem :: _ -> Error problem
        with :? JsonException as error ->
            Error(sprintf "invalid JSON: %s" error.Message)
