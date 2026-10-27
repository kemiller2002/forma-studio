namespace Forma.Workflow

open System

/// Node and label sizing (LCE-001), split from Layout along its seam. The
/// public entry points stay on `Layout` (shapeOf, contentHeight,
/// contentWidth, requiredHeight, estimateHeight, labelSize).
[<RequireQualifiedAccess>]
module internal LayoutSizing =
    let private widthFor (n: Node) =
        match n.Layout.Size with
        | Some CompactSize -> 160.0
        | Some WideSize -> 264.0
        | _ -> 200.0

    // Node box model, mirroring .ef-diagram-node in components.css (LCE-001):
    // 0.75rem padding, a 2px border with a 6px inline-start accent, a 0.25rem
    // grid gap, and the kind, label, description and meta type styles.
    let private nodePad = 12.0
    let private pillPadInline = 24.0
    let private border = 2.0
    let private accent = 6.0
    let private gap = 4.0
    /// Distance kept between content and a curved boundary.
    let clearance = 4.0
    // Every estimate reserves WCAG 1.4.12 text-spacing room (LayoutText.withTextSpacing).
    let private kindStyle = LayoutText.withTextSpacing { SizePx = 13.0; LineHeightPx = 20.8; Bold = true; Uppercase = true; LetterSpacingEm = 0.04; WordSpacingEm = 0.0 }
    let private labelStyle = LayoutText.withTextSpacing { SizePx = 16.0; LineHeightPx = 20.0; Bold = true; Uppercase = false; LetterSpacingEm = 0.0; WordSpacingEm = 0.0 }
    let private bodyStyle = LayoutText.withTextSpacing { SizePx = 14.0; LineHeightPx = 18.9; Bold = false; Uppercase = false; LetterSpacingEm = 0.0; WordSpacingEm = 0.0 }
    let private metaStyle = LayoutText.withTextSpacing { SizePx = 14.0; LineHeightPx = 22.4; Bold = false; Uppercase = false; LetterSpacingEm = 0.0; WordSpacingEm = 0.0 }
    /// Connector labels (.ef-diagram-connector__label): 0.875rem, one line, 0.25rem inline padding.
    let labelTextStyle = LayoutText.withTextSpacing { SizePx = 14.0; LineHeightPx = 22.4; Bold = false; Uppercase = false; LetterSpacingEm = 0.0; WordSpacingEm = 0.0 }

    let private kindText (n: Node) =
        match n.Kind with
        | CoreNode(_, Some label) -> label
        | CoreNode(c, None) -> string c
        | CustomNode(_, label) -> label

    /// Height of a node's content stack laid out at `width` px.
    let contentHeight (n: Node) (width: float) =
        let block (style: LayoutText.Style) (text: string) = LayoutText.height style width text
        let metaRow (key: string) (value: string) =
            // The key column is `auto`: it keeps its full width and the value gets the rest (.ef-diagram-node__meta).
            let keyWidth = LayoutText.width { metaStyle with Bold = true } key + 8.0
            LayoutText.height metaStyle (width - keyWidth) value
        let meta =
            [ match n.Status with
              | Some s -> yield metaRow "Status" (match s.State with CoreStatus(c, l) -> l |> Option.defaultValue (string c) | CustomStatus(_, l) -> l)
              | None -> ()
              for r in n.References -> metaRow (r.Type |> Option.defaultValue "Reference") (r.Label |> Option.defaultValue r.Key) ]
        let parts =
            [ yield block kindStyle (kindText n)
              yield block labelStyle n.Label
              match n.Description with Some d -> yield block bodyStyle d | None -> ()
              if not meta.IsEmpty then yield List.sum meta ]
        let ports = if n.Ports |> List.exists (fun p -> p.Label.IsSome) then 12.0 else 0.0
        // Room for the action button an interactive host shows for command and open intents.
        let action =
            match n.Interaction with
            | Some(Command _) | Some(Open _) -> 52.0
            | Some(Navigate(t, _)) -> (match t with ToUrl _ -> 0.0 | _ -> 52.0)
            | _ -> 0.0
        List.sum parts + gap * float (parts.Length - 1) + ports + action

    let private ceil8 (v: float) = Math.Ceiling(v / 8.0) * 8.0

    /// Horizontal room the shape leaves for content at width `w` (the CSS padding plus borders).
    let contentWidth (shape: Shape) (w: float) =
        match shape with
        | Pill -> w - 2.0 * pillPadInline - border - accent
        | Ellipse -> w - 2.0 * (w * 0.1465 + clearance) - 2.0 * border
        | Diamond -> w * 0.5 - 2.0 * clearance
        | _ -> w - 2.0 * nodePad - border - accent

    /// Estimated rectangular-box height (kept for callers of the 1.0 API).
    let estimateHeight (n: Node) (width: float) =
        ceil8 (contentHeight n (contentWidth Rectangle width) + 2.0 * (nodePad + border))

    let shapeOf (n: Node) =
        match n.Variant with
        | Some s -> s
        | None ->
            match n.Kind with
            | CoreNode((Start | End), _) -> Pill
            | CoreNode((Decision | Merge), _) -> Diamond
            | CoreNode(Event, _) -> Ellipse
            | CoreNode(Task, _) -> Rounded
            | _ -> Rectangle

    /// The height (for a diamond, the side) a node needs at width `w` so its
    /// visible region holds the content (LCE-001): a box needs its content plus
    /// padding; an ellipse needs the content's corners inside the inner
    /// ellipse; a diamond needs the content inside its inscribed square.
    let requiredHeight (n: Node) (w: float) =
        match shapeOf n with
        | Diamond ->
            // The inscribed square of a diamond of side s is s/2: grow until the content fits it.
            let fits s = contentHeight n (contentWidth Diamond s) <= s * 0.5 - 2.0 * clearance
            Seq.initInfinite (fun i -> w + 8.0 * float i) |> Seq.find (fun s -> fits s || s > 4096.0)
        | Ellipse ->
            let cw = contentWidth Ellipse w
            let ch = contentHeight n cw
            let a = w / 2.0 - border
            let hw = cw / 2.0 + clearance
            let hh = ch / 2.0 + clearance
            // (hw/a)² + (hh/b)² <= 1, plus the 1rem block padding and the 6px block-end accent.
            let b = hh / Math.Sqrt(max 0.05 (1.0 - (hw / a) ** 2.0))
            ceil8 (max (2.0 * b + border + accent) (ch + 32.0 + border + accent))
        | shape -> ceil8 (contentHeight n (contentWidth shape w) + 2.0 * (nodePad + border))

    /// The allocated size. Authored dimensions are kept as the author's intent;
    /// `LayoutCheck` reports when they cannot hold the content.
    let sizeOf (n: Node) =
        let w = n.Layout.Width |> Option.defaultValue (widthFor n)
        match shapeOf n, n.Layout.Width with
        | Diamond, Some aw -> aw, aw
        | Diamond, None -> let side = requiredHeight n w in side, side
        | _ -> w, n.Layout.Height |> Option.defaultWith (fun () -> requiredHeight n w)

    /// Widest connector label box (.ef-diagram-connector__label max-inline-size: 12rem).
    let labelMaxWidth = 192.0

    /// Estimated size of a connector label box: one line up to 12rem, then wrapped.
    let labelSize (text: string) =
        let inner = labelMaxWidth - 8.0
        let w = LayoutText.width labelTextStyle text
        (min w inner) + 8.0, LayoutText.height labelTextStyle inner text

