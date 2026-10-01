namespace Forma.Workflow

open System
open System.Text.RegularExpressions

/// Validated identifiers and names. Constructors are private so a value of these
/// types is always well formed (the schema patterns, enforced in F#).
[<AutoOpen>]
module Identifiers =
    let private matches (pattern: string) (s: string) = not (isNull s) && Regex.IsMatch(s, pattern, RegexOptions.CultureInvariant)

    [<Struct; CustomEquality; CustomComparison>]
    type WorkflowId =
        private | WorkflowId of string
        member this.Value = let (WorkflowId v) = this in v
        override this.ToString() = this.Value
        override this.Equals(o) = match o with :? WorkflowId as w -> w.Value = this.Value | _ -> false
        override this.GetHashCode() = this.Value.GetHashCode()
        interface IComparable with
            member this.CompareTo(o) = match o with :? WorkflowId as w -> String.CompareOrdinal(this.Value, w.Value) | _ -> 1

    [<Struct; CustomEquality; CustomComparison>]
    type ObjectId =
        private | ObjectId of string
        member this.Value = let (ObjectId v) = this in v
        override this.ToString() = this.Value
        override this.Equals(o) = match o with :? ObjectId as w -> w.Value = this.Value | _ -> false
        override this.GetHashCode() = this.Value.GetHashCode()
        interface IComparable with
            member this.CompareTo(o) = match o with :? ObjectId as w -> String.CompareOrdinal(this.Value, w.Value) | _ -> 1

    /// Unique within an owner: a port within its node, a reference within its object.
    [<Struct; CustomEquality; CustomComparison>]
    type LocalId =
        private | LocalId of string
        member this.Value = let (LocalId v) = this in v
        override this.ToString() = this.Value
        override this.Equals(o) = match o with :? LocalId as w -> w.Value = this.Value | _ -> false
        override this.GetHashCode() = this.Value.GetHashCode()
        interface IComparable with
            member this.CompareTo(o) = match o with :? LocalId as w -> String.CompareOrdinal(this.Value, w.Value) | _ -> 1

    /// Reverse-DNS namespace such as com.example.erp.
    [<Struct; CustomEquality; CustomComparison>]
    type Namespace =
        private | Namespace of string
        member this.Value = let (Namespace v) = this in v
        override this.ToString() = this.Value
        override this.Equals(o) = match o with :? Namespace as w -> w.Value = this.Value | _ -> false
        override this.GetHashCode() = this.Value.GetHashCode()
        interface IComparable with
            member this.CompareTo(o) = match o with :? Namespace as w -> String.CompareOrdinal(this.Value, w.Value) | _ -> 1

    /// <namespace>:<name>, used for producer kinds, states and commands.
    type QualifiedName =
        { Namespace: Namespace
          Name: string }
        member this.Value = this.Namespace.Value + ":" + this.Name

    let workflowIdPattern = "^[a-z0-9](?:[a-z0-9-]{0,126}[a-z0-9])?$"
    let objectIdPattern = "^[A-Za-z0-9][A-Za-z0-9_.:-]{0,127}$"
    let namespacePattern = "^[a-z][a-z0-9-]*(?:\\.[a-z][a-z0-9-]*)+$"
    let private qualifiedPattern = "^([a-z][a-z0-9-]*(?:\\.[a-z][a-z0-9-]*)+):([a-z][a-z0-9-]{0,63})$"

    [<RequireQualifiedAccess>]
    module WorkflowId =
        let tryCreate (s: string) = if matches workflowIdPattern s then Some(WorkflowId s) else None
        let create s = tryCreate s |> Option.defaultWith (fun () -> invalidArg "s" $"invalid workflow id \"{s}\"")

    [<RequireQualifiedAccess>]
    module ObjectId =
        let tryCreate (s: string) = if matches objectIdPattern s then Some(ObjectId s) else None
        let create s = tryCreate s |> Option.defaultWith (fun () -> invalidArg "s" $"invalid object id \"{s}\"")

    [<RequireQualifiedAccess>]
    module LocalId =
        let tryCreate (s: string) = if matches objectIdPattern s then Some(LocalId s) else None
        let create s = tryCreate s |> Option.defaultWith (fun () -> invalidArg "s" $"invalid local id \"{s}\"")

    [<RequireQualifiedAccess>]
    module Namespace =
        let tryCreate (s: string) = if matches namespacePattern s && s.Length <= 128 then Some(Namespace s) else None
        let create s = tryCreate s |> Option.defaultWith (fun () -> invalidArg "s" $"invalid namespace \"{s}\"")

    [<RequireQualifiedAccess>]
    module QualifiedName =
        let tryParse (s: string) =
            if isNull s then None
            else
                let m = Regex.Match(s, qualifiedPattern, RegexOptions.CultureInvariant)
                if m.Success then Namespace.tryCreate m.Groups[1].Value |> Option.map (fun ns -> { Namespace = ns; Name = m.Groups[2].Value })
                else None

/// A semantic version (format version or Forma release).
[<CustomEquality; CustomComparison>]
type SemanticVersion =
    { Major: int
      Minor: int
      Patch: int
      Pre: string option }
    override this.ToString() =
        $"{this.Major}.{this.Minor}.{this.Patch}" + (this.Pre |> Option.map ((+) "-") |> Option.defaultValue "")
    override this.Equals(o) = match o with :? SemanticVersion as v -> v.ToString() = this.ToString() | _ -> false
    override this.GetHashCode() = this.ToString().GetHashCode()
    interface IComparable with
        member this.CompareTo(o) =
            match o with
            | :? SemanticVersion as v ->
                let core = compare (this.Major, this.Minor, this.Patch) (v.Major, v.Minor, v.Patch)
                if core <> 0 then core
                else
                    match this.Pre, v.Pre with
                    | None, None -> 0
                    | None, Some _ -> 1 // a release sorts after its pre-releases
                    | Some _, None -> -1
                    | Some a, Some b -> String.CompareOrdinal(a, b)
            | _ -> 1

[<RequireQualifiedAccess>]
module SemanticVersion =
    let tryParse (s: string) =
        if isNull s then None
        else
            let m = Regex.Match(s, "^(0|[1-9][0-9]{0,8})\\.(0|[1-9][0-9]{0,8})\\.(0|[1-9][0-9]{0,8})(?:-([0-9A-Za-z.-]{1,64}))?$")
            if not m.Success then None
            else
                Some
                    { Major = int m.Groups[1].Value
                      Minor = int m.Groups[2].Value
                      Patch = int m.Groups[3].Value
                      Pre = if m.Groups[4].Success then Some m.Groups[4].Value else None }

    let create s = tryParse s |> Option.defaultWith (fun () -> invalidArg "s" $"invalid version \"{s}\"")
