namespace Forma.Workflow

open System

/// JSON reading primitives and the value tables shared by decoding and encoding.
/// Internal to Codec, which re-exports the public tables.
module internal CodecRead =
    // -- small result combinators ---------------------------------------------

    type D<'a> = Result<'a, string>

    type ResultBuilder() =
        member _.Bind(r: Result<'a, 'e>, f: 'a -> Result<'b, 'e>) = Result.bind f r
        member _.Return(x: 'a) : Result<'a, 'e> = Ok x
        member _.ReturnFrom(r: Result<'a, 'e>) = r
        member _.Zero() : Result<unit, 'e> = Ok()

    let result = ResultBuilder()

    let traverse (f: 'a -> D<'b>) (items: 'a list) : D<'b list> =
        List.foldBack (fun x acc -> match f x, acc with Ok v, Ok vs -> Ok(v :: vs) | Error e, _ | _, Error e -> Error e) items (Ok [])

    let optional (f: Json -> D<'b>) (value: Json option) : D<'b option> =
        match value with
        | None -> Ok None
        | Some v -> f v |> Result.map Some


    let str (where: string) (json: Json) : D<string> =
        match json with
        | Json.String s -> Ok s
        | _ -> Error $"{where}: expected a string"

    let obj (where: string) (json: Json) : D<(string * Json) list> =
        match json with
        | Json.Object props -> Ok props
        | _ -> Error $"{where}: expected an object"

    let arr (where: string) (json: Json) : D<Json list> =
        match json with
        | Json.Array items -> Ok items
        | _ -> Error $"{where}: expected an array"

    let num (where: string) (json: Json) : D<float> =
        Json.tryFloat json |> Option.map Ok |> Option.defaultValue (Error $"{where}: expected a number")

    let int' (where: string) (json: Json) : D<int> =
        Json.tryInt json |> Option.map Ok |> Option.defaultValue (Error $"{where}: expected an integer")

    let get name (props: (string * Json) list) = props |> List.tryFind (fst >> (=) name) |> Option.map snd

    let required where name props =
        get name props |> Option.map Ok |> Option.defaultValue (Error $"{where}: missing \"{name}\"")

    let optStr where name props = optional (str $"{where}.{name}") (get name props)

    let choose (where: string) (table: (string * 'a) list) (json: Json) : D<'a> =
        match json with
        | Json.String s ->
            table |> List.tryFind (fst >> (=) s) |> Option.map (snd >> Ok) |> Option.defaultValue (Error $"{where}: \"{s}\" is not allowed")
        | _ -> Error $"{where}: expected a string"

    let reverse (table: (string * 'a) list) (value: 'a) =
        table |> List.find (snd >> (=) value) |> fst

    // -- vocabularies ----------------------------------------------------------

    let tokens =
        [ "token:accent-primary", AccentPrimary; "token:accent-secondary", AccentSecondary
          "token:border-functional", BorderFunctional; "token:border-subtle", BorderSubtle
          "token:surface-primary", SurfacePrimary; "token:surface-secondary", SurfaceSecondary
          "token:surface-inverse", SurfaceInverse; "token:text-primary", TextPrimary
          "token:text-secondary", TextSecondary ]

    let shapes = [ "rectangle", Rectangle; "rounded", Rounded; "pill", Pill; "ellipse", Ellipse; "diamond", Diamond ]
    let lines = [ "solid", Solid; "dashed", Dashed; "dotted", Dotted ]

    let nodeKinds =
        [ "start", Start; "end", End; "task", Task; "decision", Decision; "merge", Merge; "event", Event
          "subprocess", Subprocess; "data", Data; "note", Note; "external", External ]

    let edgeKinds =
        [ "sequence", Sequence; "conditional", Conditional; "default", DefaultFlow; "exception", ExceptionFlow
          "message", Message; "association", Association; "data", DataFlow ]

    let groupKinds = [ "group", PlainGroup; "container", Container; "swimlane", Swimlane; "phase", Phase ]

    let states =
        [ "none", NoState; "pending", Pending; "ready", Ready; "active", Active; "waiting", Waiting
          "complete", Complete; "blocked", Blocked; "failed", Failed; "skipped", Skipped
          "cancelled", Cancelled; "unknown", Unknown ]

    let sides = [ "top", Top; "right", Right; "bottom", Bottom; "left", Left ]
    let portDirections = [ "in", In; "out", Out; "inout", InOut ]
    let edgeDirections = [ "forward", Forward; "backward", Backward; "both", Both; "none", Undirected ]
    let sizes = [ "compact", CompactSize; "standard", StandardSize; "wide", WideSize ]
    let routings = [ "straight", Straight; "orthogonal", Orthogonal ]
    let flows = [ "right", FlowRight; "down", FlowDown ]
    let modes = [ "authored", Authored; "auto", Auto ]
    let densities = [ "comfortable", Comfortable; "compact", Compact ]
    let textDirections = [ "ltr", Ltr; "rtl", Rtl; "auto", AutoDirection ]

    // -- decoding --------------------------------------------------------------

    let id' where json =
        str where json |> Result.bind (fun s -> ObjectId.tryCreate s |> Option.map Ok |> Option.defaultValue (Error $"{where}: invalid id"))

    let localId where json =
        str where json |> Result.bind (fun s -> LocalId.tryCreate s |> Option.map Ok |> Option.defaultValue (Error $"{where}: invalid id"))

    let ns where json =
        str where json |> Result.bind (fun s -> Namespace.tryCreate s |> Option.map Ok |> Option.defaultValue (Error $"{where}: invalid namespace"))

    let qualified where (s: string) =
        QualifiedName.tryParse s |> Option.map Ok |> Option.defaultValue (Error $"{where}: \"{s}\" is not a core value or <namespace>:<name>")

    let colorSource where json : D<ColorSource> =
        match json with
        | Json.String s when s.StartsWith "#" -> HexColor.tryCreate s |> Option.map (LiteralSource >> Ok) |> Option.defaultValue (Error $"{where}: invalid colour")
        | Json.String _ -> choose where tokens json |> Result.map TokenSource
        | _ -> Error $"{where}: expected a colour"

    let colorValue where json : D<ColorValue> =
        match json with
        | Json.String s when s.StartsWith "palette:" ->
            SlotName.tryCreate (s.Substring 8) |> Option.map (PaletteColor >> Ok) |> Option.defaultValue (Error $"{where}: invalid palette slot")
        | _ ->
            colorSource where json
            |> Result.map (function TokenSource t -> TokenColor t | LiteralSource h -> LiteralColor h)
