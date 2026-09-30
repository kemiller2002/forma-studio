namespace FormaStudio.Engine

open System

/// Canonical Flow geometry (FDA-250..258, DOCUMENT-MODEL "Geometry normalization").
///
/// - One logical unit equals one CSS pixel at 100% zoom; it is independent of device
///   pixel ratio, browser zoom and canvas zoom.
/// - The origin is the top-left of the diagram plane; x grows right, y grows down.
/// - Negative coordinates are legal. Positions lie in [-1_000_000, 1_000_000].
/// - Sizes lie in [8, 100_000].
/// - Canonical precision is one whole logical unit. Commit rounds half away from zero,
///   so normalization never moves an element by more than half a unit.
type Point = { X: int; Y: int }

type Size = { Width: int; Height: int }

type Box = { Position: Point; Size: Size }

type GeometryError =
    | NotFinite of field: string
    | OutOfRange of field: string * value: float
    | TooSmall of field: string * value: float
    | TooLarge of field: string * value: float

[<RequireQualifiedAccess>]
module Geometry =
    let coordinateLimit = 1_000_000.0
    let minimumSize = 8.0
    let maximumSize = 100_000.0

    let describe error =
        match error with
        | NotFinite field -> sprintf "%s must be a finite number" field
        | OutOfRange(field, value) -> sprintf "%s %g is outside the legal range of +/-%g logical units" field value coordinateLimit
        | TooSmall(field, value) -> sprintf "%s %g is below the minimum size of %g logical units" field value minimumSize
        | TooLarge(field, value) -> sprintf "%s %g exceeds the maximum size of %g logical units" field value maximumSize

    let private round (value: float) = int (Math.Round(value, MidpointRounding.AwayFromZero))

    let coordinate field (value: float) =
        if Double.IsNaN value || Double.IsInfinity value then Error(NotFinite field)
        elif abs value > coordinateLimit then Error(OutOfRange(field, value))
        else Ok(round value)

    let length field (value: float) =
        if Double.IsNaN value || Double.IsInfinity value then Error(NotFinite field)
        elif value < minimumSize then Error(TooSmall(field, value))
        elif value > maximumSize then Error(TooLarge(field, value))
        else Ok(round value)

    let point (x: float) (y: float) =
        match coordinate "x" x, coordinate "y" y with
        | Ok x, Ok y -> Ok { X = x; Y = y }
        | Error e, _
        | _, Error e -> Error e

    /// The single normalization path used by commands, import, paste and layout.
    let box (x: float) (y: float) (width: float) (height: float) =
        match point x y, length "width" width, length "height" height with
        | Ok position, Ok w, Ok h -> Ok { Position = position; Size = { Width = w; Height = h } }
        | Error e, _, _
        | _, Error e, _
        | _, _, Error e -> Error e

    let translate (dx: float) (dy: float) (current: Box) =
        box (float current.Position.X + dx) (float current.Position.Y + dy) (float current.Size.Width) (float current.Size.Height)

    let right (b: Box) = b.Position.X + b.Size.Width
    let bottom (b: Box) = b.Position.Y + b.Size.Height
    let centerX (b: Box) = b.Position.X + b.Size.Width / 2
    let centerY (b: Box) = b.Position.Y + b.Size.Height / 2

    /// Deterministic union of boxes, or None for an empty set.
    let bounds (boxes: Box list) =
        match boxes with
        | [] -> None
        | _ ->
            let left = boxes |> List.map (fun b -> b.Position.X) |> List.min
            let top = boxes |> List.map (fun b -> b.Position.Y) |> List.min
            let r = boxes |> List.map right |> List.max
            let btm = boxes |> List.map bottom |> List.max
            Some { Position = { X = left; Y = top }; Size = { Width = r - left; Height = btm - top } }
