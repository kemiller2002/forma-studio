namespace Forma.Workflow

open System

/// One elliptical corner radius in CSS px: horizontal and vertical semi-axes.
type CornerRadius = { Rx: float; Ry: float }

/// The four corner radii of a box, in CSS order.
type Corners =
    { TopLeft: CornerRadius
      TopRight: CornerRadius
      BottomRight: CornerRadius
      BottomLeft: CornerRadius }

/// Side thicknesses (border widths, padding or a clearance) in CSS order.
type Sides =
    { Top: float
      Right: float
      Bottom: float
      Left: float }

/// The visible boundary of a component. Every Forma shape is one of these:
/// a rectangle, rounded rectangle, pill and ellipse are boxes with (possibly
/// elliptical) corners; a diamond is a box rotated about its centre.
type Boundary =
    | RoundedBox of Rect * Corners
    | RotatedBox of Rect * degrees: float

/// Pure plane geometry for layout constraints (LCE-001). It answers whether
/// content lies inside the region a shape actually shows, which a bounding
/// rectangle cannot: text can sit inside a box's rectangle and still cross
/// its curved corner.
[<RequireQualifiedAccess>]
module LayoutGeometry =
    let corner rx ry = { Rx = max 0.0 rx; Ry = max 0.0 ry }
    let circular r = corner r r
    let uniformCorners (c: CornerRadius) = { TopLeft = c; TopRight = c; BottomRight = c; BottomLeft = c }
    let square = uniformCorners (circular 0.0)
    let sides t r b l = { Top = t; Right = r; Bottom = b; Left = l }
    let even v = sides v v v v
    let noSides = even 0.0

    let private mapCorners f (c: Corners) =
        { TopLeft = f c.TopLeft; TopRight = f c.TopRight; BottomRight = f c.BottomRight; BottomLeft = f c.BottomLeft }

    /// CSS Backgrounds 3 §5.5: when adjacent radii exceed a side, every radius
    /// is scaled by the same factor. This is why `border-radius: 999px` makes a
    /// pill whose radius is half the box's shorter side.
    let normalize (r: Rect) (c: Corners) : Corners =
        let ratio (side: float) (a: float) (b: float) = if a + b > 0.0 then side / (a + b) else Double.PositiveInfinity
        let f =
            [ ratio r.W c.TopLeft.Rx c.TopRight.Rx
              ratio r.W c.BottomLeft.Rx c.BottomRight.Rx
              ratio r.H c.TopLeft.Ry c.BottomLeft.Ry
              ratio r.H c.TopRight.Ry c.BottomRight.Ry ]
            |> List.fold min 1.0
        if f < 1.0 then c |> mapCorners (fun k -> corner (k.Rx * f) (k.Ry * f)) else c

    let roundedBox (r: Rect) (c: Corners) = RoundedBox(r, normalize r c)

    let insetRect (s: Sides) (r: Rect) =
        { X = r.X + s.Left; Y = r.Y + s.Top; W = max 0.0 (r.W - s.Left - s.Right); H = max 0.0 (r.H - s.Top - s.Bottom) }

    /// The boundary moved inwards by `s`. For a box this is the CSS inner
    /// (padding-edge) curve: each radius shrinks by the adjoining side, never
    /// below zero. Applied with a uniform clearance it is the set of points at
    /// least that far from a circular corner.
    let inset (s: Sides) (b: Boundary) =
        match b with
        | RoundedBox(r, c) ->
            RoundedBox(
                insetRect s r,
                { TopLeft = corner (c.TopLeft.Rx - s.Left) (c.TopLeft.Ry - s.Top)
                  TopRight = corner (c.TopRight.Rx - s.Right) (c.TopRight.Ry - s.Top)
                  BottomRight = corner (c.BottomRight.Rx - s.Right) (c.BottomRight.Ry - s.Bottom)
                  BottomLeft = corner (c.BottomLeft.Rx - s.Left) (c.BottomLeft.Ry - s.Bottom) }
            )
        | RotatedBox(r, deg) -> RotatedBox(insetRect s r, deg)

    let private rotate (deg: float) (cx: float) (cy: float) (p: Point) =
        let a = deg * Math.PI / 180.0
        let dx, dy = p.X - cx, p.Y - cy
        { X = cx + dx * cos a - dy * sin a; Y = cy + dx * sin a + dy * cos a }

    /// How far a point lies outside a boundary, in px (0 when inside). Exact
    /// for straight edges and circular corners; for an elliptical corner it is
    /// the radial excess scaled by the shorter semi-axis, an approximation that
    /// is zero exactly when the point is inside.
    let excess (b: Boundary) (p: Point) : float =
        match b with
        | RoundedBox(r, c) ->
            let outsideRect = [ r.X - p.X; p.X - r.Right; r.Y - p.Y; p.Y - r.Bottom ] |> List.fold max 0.0
            let cornerExcess (k: CornerRadius) (cx: float) (cy: float) (inX: bool) (inY: bool) =
                if k.Rx <= 0.0 || k.Ry <= 0.0 || not (inX && inY) then 0.0
                else
                    let q = Math.Sqrt(((p.X - cx) / k.Rx) ** 2.0 + ((p.Y - cy) / k.Ry) ** 2.0)
                    if q <= 1.0 then 0.0 else (q - 1.0) * min k.Rx k.Ry
            let corners =
                [ cornerExcess c.TopLeft (r.X + c.TopLeft.Rx) (r.Y + c.TopLeft.Ry) (p.X < r.X + c.TopLeft.Rx) (p.Y < r.Y + c.TopLeft.Ry)
                  cornerExcess c.TopRight (r.Right - c.TopRight.Rx) (r.Y + c.TopRight.Ry) (p.X > r.Right - c.TopRight.Rx) (p.Y < r.Y + c.TopRight.Ry)
                  cornerExcess c.BottomRight (r.Right - c.BottomRight.Rx) (r.Bottom - c.BottomRight.Ry) (p.X > r.Right - c.BottomRight.Rx) (p.Y > r.Bottom - c.BottomRight.Ry)
                  cornerExcess c.BottomLeft (r.X + c.BottomLeft.Rx) (r.Bottom - c.BottomLeft.Ry) (p.X < r.X + c.BottomLeft.Rx) (p.Y > r.Bottom - c.BottomLeft.Ry) ]
            if outsideRect > 0.0 then outsideRect else List.fold max 0.0 corners
        | RotatedBox(r, deg) ->
            // Undo the rotation, then measure against the unrotated rectangle.
            let local = rotate -deg r.CenterX r.CenterY p
            [ r.X - local.X; local.X - r.Right; r.Y - local.Y; local.Y - r.Bottom ] |> List.fold max 0.0

    let contains (tolerance: float) (b: Boundary) (p: Point) = excess b p <= tolerance

    let rectCorners (r: Rect) =
        [ { X = r.X; Y = r.Y }; { X = r.Right; Y = r.Y }; { X = r.Right; Y = r.Bottom }; { X = r.X; Y = r.Bottom } ]

    /// Every supported boundary is convex, so a rectangle is inside exactly when
    /// its four corners are. Returns the worst corner excess.
    let rectExcess (b: Boundary) (r: Rect) = rectCorners r |> List.map (excess b) |> List.fold max 0.0

    let intersects (a: Rect) (b: Rect) = a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom

    let overlapArea (a: Rect) (b: Rect) =
        max 0.0 (min a.Right b.Right - max a.X b.X) * max 0.0 (min a.Bottom b.Bottom - max a.Y b.Y)

    /// Whether the segment a-b passes through the interior of `r` (Liang-Barsky clipping).
    let segmentCrosses (r: Rect) (a: Point) (b: Point) =
        let dx, dy = b.X - a.X, b.Y - a.Y
        let clip (p: float) (q: float) (t0: float, t1: float) =
            // Parallel to this edge: on or beyond its line never enters the interior.
            if abs p < 1e-12 then (if q <= 0.0 then None else Some(t0, t1))
            else
                let t = q / p
                if p < 0.0 then (if t > t1 then None else Some(max t0 t, t1))
                else (if t < t0 then None else Some(t0, min t1 t))
        Some(0.0, 1.0)
        |> Option.bind (clip -dx (a.X - r.X))
        |> Option.bind (clip dx (r.Right - a.X))
        |> Option.bind (clip -dy (a.Y - r.Y))
        |> Option.bind (clip dy (r.Bottom - a.Y))
        |> Option.exists (fun (t0, t1) -> t1 - t0 > 1e-9)

    /// The largest axis-aligned rectangle with aspect `aspect` (width/height),
    /// centred in a boundary, found by bisection on its scale. Content laid out
    /// inside it cannot cross the boundary whatever the shape.
    let inscribed (aspect: float) (b: Boundary) : Rect =
        let r = match b with RoundedBox(r, _) | RotatedBox(r, _) -> r
        let candidate (s: float) =
            let w, h = (if aspect >= 1.0 then s, s / aspect else s * aspect, s)
            { X = r.CenterX - w / 2.0; Y = r.CenterY - h / 2.0; W = w; H = h }
        let rec search lo hi n =
            if n = 0 then lo
            else
                let mid = (lo + hi) / 2.0
                if rectExcess b (candidate mid) <= 1e-9 then search mid hi (n - 1) else search lo mid (n - 1)
        candidate (search 0.0 (max r.W r.H * 2.0) 48)

    /// The smallest inline (left and right) padding that keeps content clear of
    /// a box's corners by `clearance`, given its block padding and corner radius
    /// (both measured from the inner, padding-edge curve). It is `clearance`
    /// when the corners do not reach the content. A single padding value does
    /// not work for every radius.
    let safeInlinePadding (radius: CornerRadius) (blockPadding: float) (clearance: float) =
        // The clearance-inset corner keeps the arc's centre and loses `clearance`
        // from each semi-axis; the content's top edge is `blockPadding` down.
        let rx, ry = radius.Rx - clearance, radius.Ry - clearance
        let dy = radius.Ry - blockPadding
        if rx <= 0.0 || ry <= 0.0 || dy <= 0.0 then clearance
        elif dy >= ry then radius.Rx
        else max clearance (radius.Rx - rx * Math.Sqrt(1.0 - (dy / ry) ** 2.0))
