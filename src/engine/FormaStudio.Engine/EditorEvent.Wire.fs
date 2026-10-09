namespace FormaStudio.Engine

open Microsoft.FSharp.Reflection

/// Wire names derived from union case names: `ZoomIn` <-> "zoom-in".
[<RequireQualifiedAccess>]
module WireName =
    let ofCaseName (name: string) =
        name |> Seq.mapi (fun i c -> if i > 0 && System.Char.IsUpper c then sprintf "-%c" (System.Char.ToLowerInvariant c) else string (System.Char.ToLowerInvariant c)) |> String.concat ""

    /// Every case of a union whose cases carry no fields, with its wire name.
    let table<'T> () : ('T * string) list =
        FSharpType.GetUnionCases(typeof<'T>, true)
        |> Array.map (fun case ->
            match FSharpValue.MakeUnion(case, [||], true) with
            | :? 'T as value -> value, ofCaseName case.Name
            | _ -> invalidOp (sprintf "%s is not a case of %s" case.Name typeof<'T>.Name))
        |> List.ofArray
