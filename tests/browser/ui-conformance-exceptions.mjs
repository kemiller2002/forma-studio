// Approved, bounded exceptions to the universal UI layout and interaction contract
// (Visual Engineering UI-LAYOUT-INTERACTION@1.0.0 "Conformance evidence" item 6).
// An exception is never a pass: the verdict is EXCEPTION, the measured delta is kept,
// and anything beyond the bound still fails. Each names its rule, surface, owner,
// reason and the event that ends it.
export const EXCEPTIONS = [
  {
    rule: "ULI-L02",
    group: "app-row",
    surface: "examples/html-consumer application, branded and embedded pages (Studio reference design)",
    maxDeltaCssPx: 2,
    owner: "Forma (upstream: kemiller2002/forma), tracked from forma-studio#35",
    reason:
      "Forma 0.5.0 gives input[type=date] a taller intrinsic block size than input[type=email] (about 52 vs 50.6 CSS px in Chromium), " +
      "so peers in a declared row differ by about 1.3 CSS px at their top edge. The fix is Forma's intrinsic control geometry, not a Studio offset (ULI-L03, ULI-L13).",
    review: "The Forma release that normalizes native input heights; remove this entry when the group passes without it.",
    expires: "2027-01-31"
  }
];

/** The exception covering a FAIL verdict for a group, if one applies and has not expired. */
export const exceptionFor = (verdict, today = new Date()) =>
  EXCEPTIONS.find((e) => e.group === verdict.id && verdict.deltaCssPx !== null && verdict.deltaCssPx <= e.maxDeltaCssPx && new Date(e.expires) >= today);
