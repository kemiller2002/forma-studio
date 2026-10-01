namespace Forma.Workflow

open System

type Rect =
    { X: float
      Y: float
      W: float
      H: float }
    member r.Right = r.X + r.W
    member r.Bottom = r.Y + r.H
    member r.CenterX = r.X + r.W / 2.0
    member r.CenterY = r.Y + r.H / 2.0

/// A routed connector: its SVG path, its points and where its label sits.
type Route =
    { Points: Point list
      LabelAt: Point }

/// Complete resolved geometry for rendering. Derived; never stored unless
/// `Layout.apply` is asked to write it back into the document's layout fields.
type ResolvedLayout =
    { Nodes: Map<ObjectId, Rect>
      Groups: Map<ObjectId, Rect>
      Ports: Map<ObjectId * LocalId, Point * PortSide>
      Edges: Map<ObjectId, Route>
      Canvas: float * float
      Direction: FlowDirection }

/// How `Layout.apply` treats authored geometry.
type LayoutRequest =
    /// Keep authored positions and sizes; place only what has none.
    | FillMissing
    /// Recompute every position (the explicit "auto-layout" command).
    | Recompute
