namespace Forma.Workflow

open System
open System.Text.RegularExpressions

/// One structural violation, located by JSON Pointer.
type SchemaError = { Pointer: string; Message: string }

/// A dependency-free JSON Schema 2020-12 validator for the keyword subset the
/// published workflow schema uses. The schema file stays the single structural
/// authority: Forma validates documents against that file, not a parallel copy.
/// Unsupported keywords are rejected when the schema is loaded so the subset
/// cannot silently drift.
[<RequireQualifiedAccess>]
module JsonSchema =
    let private supported =
        set [ "$schema"; "$id"; "$ref"; "$defs"; "title"; "description"; "type"; "properties"; "required"
              "additionalProperties"; "patternProperties"; "propertyNames"; "items"; "minItems"; "maxItems"
              "uniqueItems"; "minProperties"; "maxProperties"; "enum"; "const"; "pattern"; "minLength"
              "maxLength"; "minimum"; "maximum"; "exclusiveMinimum"; "exclusiveMaximum"; "anyOf"; "oneOf"
              "allOf"; "not"; "if"; "then"; "else"; "dependentRequired"; "format"; "default"; "examples" ]

    type Schema = private { Root: Json; Patterns: Collections.Concurrent.ConcurrentDictionary<string, Regex> }

    let rec private keywords (path: string) (json: Json) : string list =
        match json with
        | Json.Object props ->
            props
            |> List.collect (fun (k, v) ->
                let here = if supported.Contains k then [] else [ $"{path}/{k}" ]
                let nested =
                    match k, v with
                    | ("properties" | "$defs" | "patternProperties" | "dependentRequired"), Json.Object children ->
                        children |> List.collect (fun (name, child) -> if k = "dependentRequired" then [] else keywords $"{path}/{k}/{name}" child)
                    | ("anyOf" | "oneOf" | "allOf"), Json.Array items -> items |> List.mapi (fun i s -> keywords $"{path}/{k}/{i}" s) |> List.concat
                    | ("items" | "additionalProperties" | "propertyNames" | "not" | "if" | "then" | "else"), (Json.Object _ as s) -> keywords $"{path}/{k}" s
                    | _ -> []
                here @ nested)
        | _ -> []

    let load (schema: Json) : Result<Schema, string> =
        match keywords "#" schema with
        | [] -> Ok { Root = schema; Patterns = Collections.Concurrent.ConcurrentDictionary() }
        | unsupported -> Error("schema uses unsupported keywords: " + String.Join(", ", unsupported))

    let private regex (schema: Schema) (pattern: string) =
        schema.Patterns.GetOrAdd(pattern, fun p -> Regex(p, RegexOptions.CultureInvariant, TimeSpan.FromSeconds 1.0))

    let private escapePointer (s: string) = s.Replace("~", "~0").Replace("/", "~1")

    let private resolve (schema: Schema) (reference: string) =
        if not (reference.StartsWith "#/") then None
        else
            reference.Substring(2).Split('/')
            |> Array.fold (fun node part -> node |> Option.bind (Json.field (part.Replace("~1", "/").Replace("~0", "~")))) (Some schema.Root)

    let private typeName (json: Json) =
        match json with
        | Json.Null -> "null"
        | Json.Bool _ -> "boolean"
        | Json.Number _ -> "number"
        | Json.String _ -> "string"
        | Json.Array _ -> "array"
        | Json.Object _ -> "object"

    let private isType (name: string) (json: Json) =
        match name, json with
        | "integer", Json.Number _ -> Json.tryFloat json |> Option.exists (fun f -> f = Math.Floor f)
        | n, _ -> typeName json = n

    let private textLength (s: string) = Globalization.StringInfo(s).LengthInTextElements

    let rec private check (schema: Schema) (ptr: string) (node: Json) (value: Json) : SchemaError list =
        let err message = [ { Pointer = (if ptr = "" then "/" else ptr); Message = message } ]
        match node with
        | Json.Bool true -> []
        | Json.Bool false -> err "no value is allowed here"
        | Json.Object props ->
            let get k = props |> List.tryFind (fst >> (=) k) |> Option.map snd
            let num k = get k |> Option.bind Json.tryFloat
            let intOf k = get k |> Option.bind Json.tryInt
            let refErrors =
                match get "$ref" with
                | Some(Json.String r) ->
                    match resolve schema r with
                    | Some target -> check schema ptr target value
                    | None -> err $"schema reference {r} does not resolve"
                | _ -> []
            let typeErrors =
                match get "type" with
                | Some(Json.String t) when not (isType t value) -> err $"expected {t}, found {typeName value}"
                | Some(Json.Array ts) when not (ts |> List.exists (function Json.String t -> isType t value | _ -> false)) ->
                    err $"value has type {typeName value}, which is not allowed"
                | _ -> []
            let enumErrors =
                match get "enum" with
                | Some(Json.Array allowed) when not (allowed |> List.exists (Json.equivalent value)) ->
                    let shown = allowed |> List.map Json.compact
                    err ("must be one of " + String.Join(", ", shown))
                | _ -> []
            let constErrors =
                match get "const" with
                | Some c when not (Json.equivalent c value) -> err $"must equal {Json.compact c}"
                | _ -> []
            let stringErrors =
                match value with
                | Json.String s ->
                    [ match intOf "minLength" with
                      | Some n when textLength s < n -> yield! err $"must be at least {n} characters"
                      | _ -> ()
                      match intOf "maxLength" with
                      | Some n when textLength s > n -> yield! err $"must be at most {n} characters"
                      | _ -> ()
                      match get "pattern" with
                      | Some(Json.String p) ->
                          let ok =
                              try (regex schema p).IsMatch s
                              with :? RegexMatchTimeoutException -> false
                          if not ok then yield! err $"does not match the required pattern {p}"
                      | _ -> () ]
                | _ -> []
            let numberErrors =
                match Json.tryFloat value with
                | Some f ->
                    [ match num "minimum" with Some m when f < m -> yield! err $"must be at least {m}" | _ -> ()
                      match num "maximum" with Some m when f > m -> yield! err $"must be at most {m}" | _ -> ()
                      match num "exclusiveMinimum" with Some m when f <= m -> yield! err $"must be greater than {m}" | _ -> ()
                      match num "exclusiveMaximum" with Some m when f >= m -> yield! err $"must be less than {m}" | _ -> () ]
                | None -> []
            let arrayErrors =
                match value with
                | Json.Array items ->
                    [ match intOf "minItems" with Some n when items.Length < n -> yield! err $"must have at least {n} items" | _ -> ()
                      match intOf "maxItems" with Some n when items.Length > n -> yield! err $"must have at most {n} items" | _ -> ()
                      match get "uniqueItems" with
                      | Some(Json.Bool true) ->
                          let dup =
                              items
                              |> List.mapi (fun i x -> i, x)
                              |> List.tryFind (fun (i, x) -> items |> List.take i |> List.exists (Json.equivalent x))
                          match dup with
                          | Some(i, _) -> yield! err $"item {i} repeats an earlier item"
                          | None -> ()
                      | _ -> ()
                      match get "items" with
                      | Some itemSchema -> yield! items |> List.mapi (fun i item -> check schema $"{ptr}/{i}" itemSchema item) |> List.concat
                      | None -> () ]
                | _ -> []
            let objectErrors =
                match value with
                | Json.Object members ->
                    let properties = match get "properties" with Some(Json.Object p) -> p | _ -> []
                    let patternProps = match get "patternProperties" with Some(Json.Object p) -> p | _ -> []
                    [ match get "required" with
                      | Some(Json.Array req) ->
                          for r in req do
                              match r with
                              | Json.String name when not (members |> List.exists (fst >> (=) name)) -> yield! err $"missing required property \"{name}\""
                              | _ -> ()
                      | _ -> ()
                      match intOf "minProperties" with Some n when members.Length < n -> yield! err $"must have at least {n} properties" | _ -> ()
                      match intOf "maxProperties" with Some n when members.Length > n -> yield! err $"must have at most {n} properties" | _ -> ()
                      match get "dependentRequired" with
                      | Some(Json.Object deps) ->
                          for (name, needs) in deps do
                              if members |> List.exists (fst >> (=) name) then
                                  match needs with
                                  | Json.Array ns ->
                                      for n in ns do
                                          match n with
                                          | Json.String other when not (members |> List.exists (fst >> (=) other)) ->
                                              yield! err $"\"{name}\" requires \"{other}\""
                                          | _ -> ()
                                  | _ -> ()
                      | _ -> ()
                      for (key, v) in members do
                          let here = ptr + "/" + escapePointer key
                          match get "propertyNames" with
                          | Some nameSchema ->
                              for e in check schema here nameSchema (Json.String key) do
                                  yield { e with Message = $"property name \"{key}\" is not allowed: {e.Message}" }
                          | None -> ()
                          let declared = properties |> List.tryFind (fst >> (=) key)
                          let matching =
                              patternProps |> List.filter (fun (p, _) -> try (regex schema p).IsMatch key with _ -> false)
                          match declared with
                          | Some(_, s) -> yield! check schema here s v
                          | None -> ()
                          for (_, s) in matching do
                              yield! check schema here s v
                          if declared.IsNone && matching.IsEmpty then
                              match get "additionalProperties" with
                              | Some(Json.Bool false) -> yield! [ { Pointer = here; Message = $"unknown property \"{key}\"" } ]
                              | Some(Json.Object _ as s) -> yield! check schema here s v
                              | _ -> () ]
                | _ -> []
            let combinators =
                [ match get "allOf" with
                  | Some(Json.Array schemas) -> yield! schemas |> List.collect (fun s -> check schema ptr s value)
                  | _ -> ()
                  match get "anyOf" with
                  | Some(Json.Array schemas) ->
                      let results = schemas |> List.map (fun s -> check schema ptr s value)
                      if not (results |> List.exists List.isEmpty) then
                          // Report the closest alternative so the message stays actionable.
                          yield! results |> List.minBy List.length
                  | _ -> ()
                  match get "oneOf" with
                  | Some(Json.Array schemas) ->
                      let passing = schemas |> List.filter (fun s -> check schema ptr s value |> List.isEmpty) |> List.length
                      if passing <> 1 then yield! err $"must match exactly one alternative (matched {passing})"
                  | _ -> ()
                  match get "not" with
                  | Some s when check schema ptr s value |> List.isEmpty -> yield! err "matches a disallowed form"
                  | _ -> ()
                  match get "if" with
                  | Some condition ->
                      if check schema ptr condition value |> List.isEmpty then
                          match get "then" with Some s -> yield! check schema ptr s value | None -> ()
                      else
                          match get "else" with Some s -> yield! check schema ptr s value | None -> ()
                  | None -> () ]
            // Type mismatches make the remaining keyword messages noise.
            if not typeErrors.IsEmpty then refErrors @ typeErrors
            else refErrors @ enumErrors @ constErrors @ stringErrors @ numberErrors @ arrayErrors @ objectErrors @ combinators
        | _ -> err "invalid schema node"

    /// Validates a value against the schema root. An empty list means valid.
    let validate (schema: Schema) (value: Json) : SchemaError list =
        check schema "" schema.Root value |> List.distinct
