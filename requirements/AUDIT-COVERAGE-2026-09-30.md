# Requirements Audit Coverage — 2026-09-30

Status: active traceability document.

This document is not a replacement for normative requirements. It records where each decision from the final Forma Studio / Forma / Folio requirements audit is normatively specified so future agents do not rediscover or accidentally contradict it.

## Cross-repository requirement branches

- Forma Studio: `requirements/shared-editor-flow-4`, PR #5
- Forma: `requirements/object-metadata-workflow-color-53`, PR #54, issues #52/#53
- Folio: `requirements/object-metadata-diagram-print-20`, PR #21, issue #20

## Shared editor and authored surfaces

Normative coverage:

- Layout and Flow share one editor core: `ECC-180..188`.
- Equivalent pointer/touch/keyboard/inspector/agent intent uses the same typed commands: `ECC-182..188`.
- Project-wide chronological undo/redo: `ECC-106..110`.
- Page-only, diagram-only, and mixed projects: product/project lifecycle requirements in `00-product-scope.md` and `01-projects-pages-navigation.md`.
- Shared object metadata across Layout and Flow: `ECC-120..129`.

## Metadata as a first-class object capability

Normative coverage:

- Flow object metadata: `FDA-800..825`.
- Metadata schema validation: `FDA-900..910`.
- Authored/defaulted/derived/source-bound distinctions: `FDA-920..928`.
- Project-local metadata field definitions: `FDA-1180..1190`.
- Structured queries and bulk operations: `FDA-980..985`.
- Tabular metadata interchange: `FDA-990..996`.
- Metadata display configuration independent from stored values: `FDA-1230..1240`.
- Metadata-derived disclosure/privacy: `FDA-1260..1268`.
- Layout/shared object metadata: `ECC-120..129`.
- Canonical metadata schema/value model: `architecture/DOCUMENT-MODEL.md`.

Metadata remains independent from:

- stable identity;
- visible label/content;
- semantic type/status;
- graph topology;
- Layout/Flow geometry;
- appearance/style;
- accessibility semantics;
- typed resource references;
- provenance/evidence.

## Typed references and attachments

Normative coverage:

- Typed object references and attachments: `FDA-1030..1039`.
- Typed references remain distinct from descriptive metadata.
- Internal references participate in deletion/dependency validation.
- External reference availability/verification remains explicit.
- Project assets use the existing asset contracts rather than opaque embedded blobs.

## Workflow/diagram color

Normative coverage:

- Authored object color: `FDA-830..846`.
- Project palettes and metadata/color mapping: `FDA-850..860`.
- Color accessibility/validation: `FDA-870..878`.
- Workflow color acceptance examples: `FDA-890..896`.
- Color representation boundary and solid-color-first scope: `FDA-1070..1075`.
- Project palette management: `FDA-1200..1208`.
- Declarative metadata-to-appearance mapping: `FDA-1210..1220`.

Color is presentation, not semantic authority. Red does not imply Error; green does not imply Approved. Metadata-to-color behavior is an explicit mapping.

## Appearance styles and precedence

Normative coverage:

- Named reusable appearance styles: `FDA-930..943`.
- Shape/icon independence from semantic type: `FDA-950..958`.
- Appearance normalization and palette modes: `FDA-960..968`.
- Appearance editing UI: `FDA-970..976`.
- Deterministic appearance cascade: `FDA-1060..1069`.
- Canonical style/palette model: `architecture/DOCUMENT-MODEL.md`.

Effective appearance precedence is:

1. Forma/base defaults
2. diagram profile defaults
3. named appearance style
4. explicit metadata/data presentation mapping
5. explicit per-object overrides
6. transient editor-only focus/selection/validation adorners

Transient editor adorners never become authored appearance.

## Shape, icon, pattern, and line presentation

Normative coverage:

- Shape/icon presentation independence: `FDA-950..958`.
- Forma public shape/icon requirements: `requirements/OBJECT-METADATA-AND-DIAGRAM-PRESENTATION.md` on the Forma branch.
- Non-color differentiation through line style/marker/pattern is specified on the Forma branch and projected by Folio.

Arbitrary path/Bezier/freeform illustration is not an initial capability.

## Diagram geometry, units, rulers, grids, snapping

Normative coverage:

- Coordinate system and normalization: `FDA-250..258`.
- Canvas extent/navigation: `FDA-260..268`.
- Spatial selection and movement: `FDA-470..477`.
- Measurement units/rulers/grids: `FDA-1080..1088`.
- Snap and glue behavior: `FDA-1040..1046`.
- Rotation/advanced transforms: `FDA-530..533`.

Canonical Flow geometry remains logical-coordinate data. Displaying inches/mm/etc. does not rewrite geometry.

## Connectors and graph integrity

Normative coverage:

- Canonical graph model: `FDA-020..030`.
- Nodes/ports/connectors: `FDA-040..050`.
- Connector editing/routing: `FDA-280..292`.
- Port identity/cardinality: `FDA-430..438`.
- Crossing/junction semantics: `FDA-510..514`.
- Graph identity/scope: `FDA-640..647`.

Visual line crossing never implies connectivity.

## Groups, lanes, phases, layers, subflows

Normative coverage:

- Containers/swimlanes: `FDA-100..104`.
- Containment integrity: `FDA-450..458`.
- Layers: `FDA-460..466`, `FDA-760..766`.
- Cross-functional phases: `FDA-730..736`.
- Subflows/drill-down: `FDA-330..336`.
- Subprocess extraction: `FDA-750..756`.

## Workflow, state, architecture semantics

Normative coverage:

- Diagram profiles: `FDA-080..090`.
- Workflow/state validation: `FDA-360..371`.
- Walkthrough/simulation vs execution: `FDA-380..385`.
- Advanced Workflow semantics: `FDA-580..586`.
- Advanced State semantics: `FDA-590..593`.
- Architecture boundary semantics: `FDA-600..604`.

A typed diagram is a specification. Production execution/orchestration/code generation requires a separate runtime/security contract.

## Reuse, templates, libraries, dependency closure

Normative coverage:

- Reusable diagram templates/fragments: `14-reuse-templates-layouts.md`.
- Diagram template dependency closure: `FDA-1250..1257`, `RTL-124..129`.
- Imported/reused content must reconcile metadata field definitions, palette slots, styles, mappings, assets, profile versions, and Forma presentation capabilities.

## Search, filtering, review, deep links

Normative coverage:

- Diagram lifecycle/search: `FDA-230..238`.
- Read-only review/deep linking: `FDA-700..706`.
- Filters/focus/isolation: `FDA-710..715`.
- Structured metadata query/bulk editing: `FDA-980..985`.
- Diagram semantic diff/review: `FDA-490..495`.

## Accessibility and internationalization

Normative coverage:

- Accessible graph traversal/status: `FDA-390..397`.
- Semantic/read order: `FDA-570..578`.
- Mobile/touch Flow authoring: `FDA-610..616`.
- Internationalization/bidirectional content: `FDA-680..685`.
- Legends/semantic keys: `FDA-690..693`.
- Forma and Folio have corresponding non-color, forced-colors, grayscale, metadata-label/value, and print accessibility requirements.

## Performance and long-running computation

Normative coverage:

- Large diagram performance: `FDA-350..359`.
- Long-running/stale computations: `FDA-660..665`.
- Auto-layout reproducibility/versioning: `FDA-420..426`.
- Flow-specific performance budgets also exist in `10-security-privacy-performance.md`.

## Copy/paste, clipboard, import/export

Normative coverage:

- Canonical Flow copy/paste/fragments: `FDA-300..308`.
- External adapter compatibility/loss reporting: `FDA-410..416`.
- Diagram import placement/merge: `FDA-550..555`.
- Clipboard/selection export interoperability: `FDA-1110..1116`.
- SVG/vector safety: `FDA-1010..1014`.

Studio-to-Studio fidelity uses the canonical graph fragment, not SVG/image pixels.

## Derived and source-backed graphs

Normative coverage:

- Derived graph projections: `FDA-540..545`.
- Data-bound/derived diagrams: `FDA-670..676`.
- Source-backed graph integration: `FDA-720..725`.
- Metadata source-bound/derived rules: `FDA-920..928`.

Derived views never become authoritative merely because they use the Flow renderer.

## Provenance and local history

Normative coverage:

- Command/revision provenance: `FDA-1100..1106`.
- Local undo-history persistence boundary: `FDA-1050..1055`.
- Editor names/aliases: `FDA-1090..1095`.

Git/Praxis/revision history remains distinct from local command undo history.

## Collaboration

Normative coverage:

- Future real-time collaboration session state/review threads: `FDA-1000..1006`.
- Real-time collaboration remains future scope in `12-roadmap-nongoals.md`.
- Presence, remote cursors, remote selections, and viewports are ephemeral session state, not canonical project data.

## Forma presentation ownership

Normative coverage:

- Diagram presentation authority: `FDA-240..247`.
- Forma issue #52 and Forma issue #53 / PR #54 define public diagram presentation, metadata, color, style, shape/icon, non-color differentiation, security, and disclosure boundaries.
- Studio owns graph/domain/editor semantics.
- Forma owns reusable production presentation.
- Editor adorners remain Studio-owned and are excluded from ordinary output.

## Folio print/export ownership

Normative coverage:

- Print/rendered export: `FDA-400..408`.
- Studio-to-Folio projection handoff: `FDA-1120..1127`.
- Folio issue #20 / PR #21 defines:
  - metadata visibility classes;
  - diagram/workflow print projection;
  - color/grayscale/backgrounds-disabled behavior;
  - bounds/fit/tiling;
  - legends/indexes;
  - accessibility;
  - provenance;
  - vector-input security;
  - style/appearance provenance;
  - fit-scale legibility;
  - metadata-derived disclosure.

Folio paginates/projects the supplied diagram. It does not become a second graph engine.

## Explicitly deferred/future capabilities

These are intentionally recorded as future/conditional capabilities rather than missing requirements:

- BPMN/UML/SysML/ArchiMate conformance requires dedicated versioned profiles/adapters/tests: `RN-012`, `FDA-586`.
- Precision CAD/drafting/dimensioning/tolerances are outside initial scope: `RN-011`, `RN-054`.
- Arbitrary freeform/vector illustration is outside initial scope: `FDA-006`, `FDA-530..533`.
- Production workflow execution/orchestration/code generation is outside initial scope: `FDA-384..385`, `RN-013`.
- Real-time multiplayer collaboration is future scope: `RN-045`, `FDA-1000..1006`.
- Arbitrary rotation/skew/freeform transforms are deferred and require explicit contracts: `FDA-530..533`.
- Gradients/blend modes/complex paint are deferred; solid colors are the initial contract: `FDA-1070..1075` plus Forma/Folio paint boundaries.
- General expression/scripting languages for metadata mappings are deferred; initial mappings are declarative: `FDA-1210..1220`.
- Untrusted executable profile/plugin code requires a separate security/runtime design: `FDA-520..526`, `RN-048`.

## Implementation gate

The existence of this broad requirements baseline does not authorize an agent to implement every capability in one pass.

The first shared-editor proof remains:

### Layout

Stack -> Heading -> edit -> reorder -> spacing -> undo/redo -> save/reload.

### Flow

Node -> Node -> connect -> move -> label -> metadata -> authored color -> undo/redo -> save/reload.

The first cross-repository projection proof then extends the Flow artifact through:

Forma Studio -> Forma presentation -> Folio color PDF -> grayscale/backgrounds-disabled projection -> agent/developer export.

Implementation must follow the roadmap and current work-item acceptance criteria rather than treating this coverage document as a single implementation backlog.
