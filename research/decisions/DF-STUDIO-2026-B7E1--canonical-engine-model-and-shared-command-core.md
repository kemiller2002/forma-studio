---
id: DF-STUDIO-2026-B7E1
title: Canonical engine model and shared command core for Layout and Flow
status: accepted
confidence: high
created: 2026-09-30
work_item: STUDIO-GH-9
related:
  - architecture/ARCHITECTURE.md
  - architecture/DOCUMENT-MODEL.md
  - requirements/16-flow-diagram-authoring.md
  - kemiller2002/forma-studio#9
---

# Decision

The first executable Studio domain lives in `src/engine/FormaStudio.Engine` as
pure F#: immutable records and unions, and functions from state to a new state
or findings. It has no browser APIs, no mutation and no external packages beyond
the pinned `EchelonFoundry.Aegis.Core`.

1. **One command surface.** `Command` covers Layout, Flow, metadata and
   appearance plus atomic `Batch`. `Commands.execute` validates, produces a
   successor project or blocker findings, and rejects any command that introduces
   a new integrity blocker. Rejection leaves the prior state untouched because
   nothing mutates (FDA-221, FDA-224).
2. **One session.** `Editor.dispatch/undo/redo/select` hold one project-wide
   history of whole-project snapshots and one selection of `ObjectRef` values
   for both surfaces. Selection, pan, zoom and previews never enter the project
   (FDA-027). A multi-node move is one `MoveNodes` command, so one history entry
   (FDA-063). The undo stack is session-local (FDA-1051).
3. **Typed identity.** `Id<'Kind>` with phantom marker types keeps page, node,
   edge, field, palette, style and mapping identifiers apart at compile time.
   They are validated with the schema v1 pattern.
4. **Geometry.** Flow uses whole logical units (1 unit = 1 CSS px at 100%).
   Negative coordinates are legal within +/-1,000,000. Sizes are 8..100,000.
   Commit rounds half away from zero through `Geometry.box`, the only
   normalization path. NaN, infinite, zero and out-of-range values are rejected.
   Layout components have no x/y at all.
5. **Metadata.** Stored values are `Explicit`, `SourceBound`, `Unknown` or
   `Unavailable`. Defaults and derivations (for example owner from lane
   membership) are computed, never stored. Resolution reports explicit,
   default, derived, source-bound, unknown, unavailable, invalid, conflict or
   missing. Undefined keys are preserved and treated as source-only.
6. **Disclosure.** A field declares its output scopes (`Rendered`,
   `AgentExport`, `ProvenanceExport`) and, separately, whether it may drive
   derived presentation. A mapping cannot be defined over a field that forbids
   derived presentation. Restricting a field later returns obligations, and
   output-scope resolution stops using the mapping; editor-only highlighting
   may still use it (FDA-1260..1268).
7. **Appearance.** `ColorRef` is `TokenColor` (public Forma tokens only, pinned in
   `contracts/forma/`), `PaletteColor` or `LiteralColor` (`#rrggbb`, solid). The
   cascade is Forma default -> profile default -> named style -> mapping ->
   override. An earlier mapping in project order beats a later one. Every
   effective property records its source layer. Removing an override reveals
   the next layer.
8. **Persistence.** Schema version 2 serializes deterministically: fixed member
   order, sorted metadata keys, canonical set order, LF endings. A v1 page-only
   project migrates in memory without gaining diagrams; its free-form `metadata`
   is kept verbatim as `legacyMetadata`. A newer schema version fails with an
   explanation. The revision of a project or diagram is a SHA-256 digest of its
   canonical form, so undo or reload of identical content yields the identical
   revision.

# Alternatives considered

- **Command log as undo history.** It needs an inverse for every command, which
  would duplicate legality logic. Snapshots of immutable state share structure
  and are trivially correct. A future recovery journal can still store commands
  separately (FDA-1053).
- **A mutable object graph with DOM mirrors.** Rejected by the architecture: the
  DOM would become a second source of truth.
- **A test framework package.** A 60-line harness in `tests/` is enough and keeps
  the dependency surface at zero (dependency policy).
- **Integer revision counters.** Rejected. After undo, a counter either repeats
  a value for different content or diverges from identical content, which
  would break stale-projection detection.

# Consequences

- The Flow slice (FDA-220) and the Layout Stack/Heading slice run through the
  same `Editor` and `Commands` functions; `tests/FormaStudio.Engine.Tests` proves both.
- Target frameworks: the engine targets `net8.0` and pins FSharp.Core 10.1.400
  because Aegis.Core 1.0.0 requires it. Tests and CI run on the .NET 10 SDK
  (`global.json`), matching Forma's toolchain. The engine had never built
  before: the implicit FSharp.Core version produced NU1605.
- WebAssembly packaging and the Limen transport are not part of this decision.
