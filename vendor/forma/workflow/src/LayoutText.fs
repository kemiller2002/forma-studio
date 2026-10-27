namespace Forma.Workflow

open System
open System.Globalization
open System.Text

/// A deterministic, font-independent text measure for layout estimates
/// (LCE-001). Character count is not rendered width: an ideograph or an emoji
/// is about twice a Latin letter and a combining mark adds nothing. Advances
/// are em fractions per character class, taken as the widest of the sans
/// fonts measured in Chromium (Inter, DejaVu Sans, Liberation Sans, FreeSans),
/// so the estimate errs towards more room. It cannot reproduce a browser's
/// shaping; the rendered-geometry check (tests/layout) is the authority.
[<RequireQualifiedAccess>]
module LayoutText =
    type Style =
        { SizePx: float
          LineHeightPx: float
          Bold: bool
          Uppercase: bool
          LetterSpacingEm: float
          WordSpacingEm: float }

    /// WCAG 2.2 SC 1.4.12 text spacing: a reader may set line height 1.5,
    /// letter spacing 0.12em and word spacing 0.16em without losing content.
    /// A layout that reserves this room stays valid under those overrides.
    let withTextSpacing (s: Style) =
        { s with
            LineHeightPx = max s.LineHeightPx (1.5 * s.SizePx)
            LetterSpacingEm = max s.LetterSpacingEm 0.12
            WordSpacingEm = max s.WordSpacingEm 0.16 }

    let private isCombining (r: Rune) =
        match Rune.GetUnicodeCategory r with
        | UnicodeCategory.NonSpacingMark | UnicodeCategory.EnclosingMark | UnicodeCategory.Format -> true
        | _ -> r.Value = 0x200D || (r.Value >= 0xFE00 && r.Value <= 0xFE0F) || (r.Value >= 0x1F3FB && r.Value <= 0x1F3FF)

    /// East Asian wide characters: each is its own line-break opportunity.
    let isWide (r: Rune) =
        let v = r.Value
        (v >= 0x1100 && v <= 0x115F) || (v >= 0x2E80 && v <= 0xA4CF) || (v >= 0xAC00 && v <= 0xD7A3)
        || (v >= 0xF900 && v <= 0xFAFF) || (v >= 0xFE30 && v <= 0xFE4F) || (v >= 0xFF00 && v <= 0xFF60)
        || (v >= 0xFFE0 && v <= 0xFFE6) || (v >= 0x20000 && v <= 0x3FFFD)

    let private isEmoji (r: Rune) =
        let v = r.Value
        (v >= 0x1F000 && v <= 0x1FAFF) || (v >= 0x2600 && v <= 0x27BF) || (v >= 0x2B00 && v <= 0x2BFF)

    /// Advance of one character in em.
    let advance (bold: bool) (r: Rune) : float =
        let pick regular strong = if bold then strong else regular
        if isCombining r then 0.0
        elif isWide r then 1.0
        elif isEmoji r then 1.25
        elif Rune.IsWhiteSpace r then pick 0.32 0.35
        else
            let c = if r.IsBmp then char r.Value else '\u0000'
            if "iIl.,:;'!|j".IndexOf c >= 0 then pick 0.32 0.38
            elif "fjrt()-[]".IndexOf c >= 0 then pick 0.38 0.45
            elif "mwMW%@".IndexOf c >= 0 then pick 0.94 1.02
            elif Rune.IsDigit r then pick 0.64 0.70
            elif r.Value >= 0x0590 && r.Value <= 0x05FF then pick 0.56 0.62
            elif r.Value >= 0x0600 && r.Value <= 0x06FF then pick 0.47 0.56
            elif Rune.IsUpper r then pick 0.68 0.75
            else pick 0.61 0.68

    let private runes (s: string) = s.EnumerateRunes() |> Seq.toList

    /// Width in px of a run of text in a style.
    let width (style: Style) (text: string) =
        let t = if style.Uppercase then text.ToUpperInvariant() else text
        runes t
        |> List.sumBy (fun r ->
            let spacing = (if isCombining r then 0.0 else style.LetterSpacingEm) + (if Rune.IsWhiteSpace r then style.WordSpacingEm else 0.0)
            (advance style.Bold r + spacing) * style.SizePx)

    /// Break-opportunity segments: words separated by white space, and each
    /// wide (CJK) character on its own. `true` marks a segment preceded by a space.
    let private segments (text: string) =
        text.Split([| ' '; '\t'; '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
        |> List.ofArray
        |> List.collect (fun word ->
            let rs = runes word
            if rs |> List.exists isWide then
                rs
                |> List.fold
                    (fun (acc: string list) r ->
                        match acc with
                        | last :: rest when not (isWide r) && not (last.EnumerateRunes() |> Seq.exists isWide) -> (last + r.ToString()) :: rest
                        | _ -> r.ToString() :: acc)
                    []
                |> List.rev
                |> List.mapi (fun i s -> s, i = 0)
            else [ word, true ])

    /// Lines after greedy wrapping at `maxWidth` px. Words wider than the line
    /// break anywhere, as Forma's `overflow-wrap: anywhere` does. Empty text is one line.
    let lines (style: Style) (maxWidth: float) (text: string) : int =
        let space = width style " "
        // A line narrower than one character still holds one character (CSS breaks anywhere).
        let limit = max (style.SizePx * 0.7) maxWidth
        let place (count: int, used: float) (segment: string, spaced: bool) =
            let w = width style segment
            let lead = if used > 0.0 && spaced then space else 0.0
            if used + lead + w <= limit then count, used + lead + w
            elif w <= limit then count + 1, w
            else
                // An unbreakable run wider than the line: it fills whole lines.
                let full = Math.Floor(w / limit)
                let rest = w - full * limit
                count + int full + (if used > 0.0 then 1 else 0) - (if rest = 0.0 then 1 else 0), (if rest = 0.0 then limit else rest)
        match segments text with
        | [] -> 1
        | segs -> segs |> List.fold place (1, 0.0) |> fst

    let height (style: Style) (maxWidth: float) (text: string) = float (lines style maxWidth text) * style.LineHeightPx
