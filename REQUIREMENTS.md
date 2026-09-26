# Forma Studio Requirements

## Shared Echelon application foundations

Status: **Required**

### Forma

Forma Studio exists to specify and compose Forma. The application MUST consume a pinned released Forma artifact for its own interactive UI and MUST dogfood the same public components, patterns, tokens, responsive rules, accessibility contracts, brands, skins, motion contracts, and layout vocabulary offered to other Echelon applications. Forma Studio MUST NOT rely on privileged private presentation behavior that ordinary Forma consumers cannot use without a recorded design-system requirement.

The Studio authoring model MUST identify the exact Forma version against which a design was created and validated. A Forma upgrade MUST trigger compatibility review of the Studio component/pattern inventory, authoring schema, previews, examples, validation rules, and generated output.

### Current Forma capability review

Forma Studio MUST treat the current public Forma package and repository artifacts as its source capability surface rather than maintaining an independent hand-curated approximation.

At minimum, Studio MUST review and represent:

- Forma tokens, foundations, components, assessment styles, skins, brands, and public pattern exports.
- Forma's component manifests and pattern manifests, including state, accessibility, and motion semantics where declared.
- The Forma layout catalog and its specimens as reusable composition starting points.
- Responsive behavior and mobile requirements as first-class design data rather than desktop-preview-only behavior.
- Aegis fault/error presentation patterns exposed by Forma.
- Current interactive control families, including checkbox, select, slider, switch, and subsequent public controls.
- Workspace/application patterns such as shells, navigation, sidebars, split panes, command surfaces, collections, validation surfaces, notifications, dialogs/overlays, and other public Forma patterns.
- Survey, assessment, decision, and understanding patterns where they are part of the public Forma capability surface.
- Motion/surface-motion contracts and reduced-motion behavior.
- White-label, brand, and skin capabilities exposed by Forma.

This review MUST be repeatable. Studio MUST provide a machine-checkable inventory or synchronization mechanism that can detect when a pinned/new Forma release adds, removes, renames, or materially changes a public authoring capability.

### Capability parity and gap handling

A design that can be expressed with public Forma MUST be expressible in Forma Studio without requiring hand-written private CSS or Studio-only presentation behavior.

When a requested design cannot be faithfully represented by current public Forma capabilities, Studio MUST NOT silently emulate the missing capability with a private implementation. It MUST:

1. identify the unsupported design intent,
2. record the missing primitive/component/pattern/token/state as a Forma capability gap,
3. preserve the design intent without falsely marking it as production-valid,
4. link the gap to the appropriate governed Forma requirement/work item, and
5. allow the design to become valid automatically or through an explicit migration once the capability ships.

Studio SHOULD distinguish between unsupported capability, invalid composition, accessibility failure, responsive failure, and version incompatibility.

## Visual authoring model

Status: **Required**

### Composition

Studio MUST support visual composition of multiple application pages/views using public Forma primitives and patterns. Authors MUST be able to add, remove, select, reorder, nest, duplicate, and configure supported elements while preserving valid semantic structure.

The persisted specification MUST represent design intent and Forma semantics, not browser pixel coordinates alone. Absolute positioning MAY be supported for Forma capabilities that intentionally require it, but MUST NOT become the default application-layout model.

### Layout catalog

Studio MUST expose Forma's layout catalog and specimens as discoverable starting points. Authors MUST be able to instantiate a cataloged layout, inspect its composition, modify it using supported Forma capabilities, and retain provenance to the originating layout where useful.

Catalog updates MUST be detectable during Forma synchronization. Studio MUST NOT fork catalog definitions merely to make them editable.

### Responsive and mobile authoring

Responsive behavior MUST be first-class. Studio MUST provide preview/validation across representative narrow mobile, wide mobile, tablet, desktop, and wide-desktop viewports without requiring separate independent designs for each viewport.

Authors MUST be able to specify only responsive decisions that Forma itself supports. Studio MUST surface overflow, clipping, inaccessible target sizing, unusable navigation, inappropriate fixed dimensions, and other detectable responsive failures before export.

Mobile authoring MUST be fully usable on touch devices. Core Studio operations MUST NOT depend exclusively on hover, right-click, precision mouse interaction, or desktop keyboard shortcuts.

### Components, patterns, states, and variants

Studio MUST derive or synchronize its component and pattern palette from the pinned Forma capability surface.

Where Forma defines meaningful states or variants, Studio MUST allow those states to be previewed and configured. This includes, where applicable, default, hover, focus-visible, active/pressed, selected, checked, expanded/collapsed, disabled, loading, empty, validation, warning, error/fault, success, and other component-specific states.

Studio MUST distinguish authoring-time simulated state from persisted application/domain state.

### Overlays and layered interaction

Studio MUST support public Forma overlay/layered interaction patterns, including dialogs/modals, left/right flyouts or drawers, popovers, toasts, and similar supported surfaces.

The authoring model MUST represent trigger, placement, dismissal, focus-management expectations, backdrop behavior, responsive behavior, and accessibility semantics where those concepts are part of the Forma contract.

### Motion and physics

Studio MUST expose public Forma motion capabilities without introducing a separate animation system. Authors MUST be able to preview motion behavior and reduced-motion behavior.

Where Forma exposes physics-derived motion parameters or semantic motion presets, Studio MUST author those public parameters/presets rather than generating arbitrary private animation CSS. Motion validation MUST include reduced-motion compatibility and MUST prevent motion from being the sole carrier of essential meaning.

## Design system, themes, and branding

Status: **Required**

Studio MUST expose Forma design tokens semantically. Authors SHOULD choose semantic roles rather than hard-coded values when a corresponding Forma token exists.

Studio MUST support previewing public Forma brands and skins and MUST make white-label behavior inspectable across a composed application.

Brand/skin authoring MUST validate against Forma's public branding contract/schema. Studio MUST distinguish global design-system tokens, brand tokens, application-level choices, and local component configuration so that local edits do not accidentally fork the design system.

Studio SHOULD provide contrast and accessibility feedback while token/brand decisions are being made.

## Accessibility authoring

Status: **Required**

Accessibility is a design constraint, not an export-time audit only.

Studio MUST preserve semantic HTML intent and public Forma accessibility contracts. It MUST support authoring accessible names, labels, descriptions, landmark/heading relationships, form semantics, error relationships, and keyboard/focus behavior where required by the selected component/pattern.

Studio MUST provide automated validation for detectable accessibility failures and MUST make keyboard-only and reduced-motion preview practical. Generated output MUST NOT require Studio-specific runtime behavior to remain accessible.

## Aegis and result/fault surfaces

### Aegis

When Forma Studio introduces operational boundaries such as repository access, filesystem access, remote persistence, import/export, browser/WASM interop, or external service calls, those unexpected operational failures MUST use Aegis. Expected editor/domain outcomes remain typed application/Ordo outcomes.

Aegis user-facing presentation MUST use Forma's shared fault/error presentation components.

Studio MUST make Forma's public Aegis fault presentation patterns available for application design and preview. Simulated fault examples used while designing MUST remain clearly separate from actual Studio operational faults.

## Generated output and round-trip transparency

Status: **Required**

Studio MUST be able to show the Forma-oriented representation of a design rather than hiding the implementation behind an opaque proprietary format.

Exported/generated UI MUST use public Forma markup/classes/custom elements/pattern contracts as applicable to the pinned Forma release. Studio MUST NOT emit private Studio-only CSS as a prerequisite for ordinary exported applications unless the design explicitly contains a governed extension.

Generated output SHOULD be deterministic for an equivalent design specification so that it is reviewable in source control.

Where practical, Studio SHOULD show authors the generated markup/configuration for the currently selected composition and identify which Forma capability each generated construct comes from.

Import/export MUST preserve unknown forward-compatible data when safe to do so and MUST report lossy migrations explicitly.

## Validation and evidence

Status: **Required**

A design MUST have an explicit validation state. Studio MUST NOT equate a visually plausible preview with a valid Forma application.

Validation MUST cover, as applicable:

- schema/specification validity,
- availability in the pinned Forma version,
- component/pattern composition rules,
- responsive/mobile behavior,
- accessibility,
- brand/token validity,
- motion/reduced-motion compatibility,
- unresolved Forma capability gaps,
- broken page/navigation targets,
- invalid overlay/focus relationships, and
- export compatibility.

Validation findings SHOULD link to the affected design element and, when possible, the relevant Forma capability or requirement.

Studio MUST retain sufficient evidence to distinguish authored intent, automated validation, manual review, and unresolved exceptions.

## Versioning and migration

Status: **Required**

Every persisted Studio design MUST declare its Studio specification version and target Forma version.

Opening a design against a newer Forma release MUST NOT silently rewrite it. Studio MUST determine whether migration is unnecessary, lossless, requires review, or is blocked by an incompatibility.

Migration operations MUST be explicit, reviewable, and testable. Removed or renamed Forma capabilities MUST produce actionable migration findings rather than silently degrading the design.

## Design provenance

Status: **Required**

Requirement IDs: this section introduces the repository's first numbered requirements, using the prefix `FS-PROV-NNN`. Earlier sections remain unnumbered until they are refined; new normative requirements SHOULD take an `FS-<AREA>-NNN` ID in the same style.

The identity and provenance shapes are owned by Praxis and are referenced, not restated: actor `RQ-ROS-2026-A001`, execution identity `RQ-ROS-2026-A002`, provenance interchange record `praxis.provenance-record` `RQ-ROS-2026-A013`, execution propagation `RQ-ROS-2026-A014`, and no silent stripping `RQ-ROS-2026-A015`. Forma Studio MUST NOT depend on Praxis code or availability to open, edit, validate, or export a design; it re-implements the record's structural rules locally.

- **FS-PROV-001 Separate metadata envelope.** A persisted Studio design MUST keep provenance in a metadata envelope that is separate from the presentation (design) tree. Presentation nodes MUST NOT carry actor or execution fields.
- **FS-PROV-002 Interchange record.** The envelope MUST hold the design's provenance as a Praxis provenance interchange record (`contract`, `version`, `subject`, `contributions`, `derivedFrom`, `sources`), with `subject` a namespaced reference such as `forma-studio:design/<id>`.
- **FS-PROV-003 Contributions keyed by execution.** Each contribution MUST be keyed by the execution that produced it: the propagated Praxis execution (`ROS_EXECUTION_ID`) when present, otherwise Studio's own run as `EXE-forma-studio.<run>`, or `CTB-...` for a human outside any run. An agent contribution MUST be keyed `EXE-...`. Studio MUST NOT mint a Praxis-shaped `EXE-<timestamp>-<random>` key. The history is append-only: at most one `created`, nothing before it, no re-attribution.
- **FS-PROV-004 Lineage is not authorship.** A design instantiated from a cataloged layout or specimen MUST list it in `derivedFrom` (for example `forma:layout/<id>`) and MAY carry that source's provenance verbatim in `sources`. The source's contributors MUST NOT be copied into the design's `contributions`; the actor who instantiated the layout is the design's creator.
- **FS-PROV-005 Unknown is not guessed.** Actor attributes that are not known MUST be recorded as the literal `unknown`. Studio MUST NOT infer an actor from Git authorship, the signed-in user of another tool, or prose. A legacy design with no provenance stays unattributed.
- **FS-PROV-006 Preserve on import/export.** Import, export, save, and migration MUST preserve unknown fields in the envelope, the record, each contribution, and each actor. A record in an unsupported major version MUST be carried verbatim and not extended. Converting the representation MUST append a `migrated` contribution; changing design content MUST append `modified` (or an `x-...` operation). Malformed provenance MUST be reported, not silently dropped.
- **FS-PROV-007 Deterministic output is provenance-free.** Generated markup, CSS, and configuration MUST NOT embed actor, execution, or provenance data. Generating output from the same design with or without its provenance envelope MUST produce byte-identical output.
- **FS-PROV-008 Human review.** A human review or approval of a design MUST be recorded as a `reviewed` or `approved` contribution by that human. Review MUST NOT replace, re-key, or re-attribute the agent or human contribution that created or modified the design.
- **FS-PROV-009 Identity is not authorization.** Provenance is self-reported. It MUST NOT be treated as authentication, authorization, or validation evidence, and MUST NOT carry credentials or other secrets.
- **FS-PROV-010 Conformance.** When the Studio persistence format is implemented, its codec MUST pass the Praxis conformance fixtures (`schemas/conformance/provenance-record/` in Praxis, vendored with a pinned commit and SHA-256 digests) and a test proving FS-PROV-007 byte-identical output.

Implementation status: the repository is not yet bootstrapped (GitHub issue #1), so these requirements have no code, schema, or tests yet. FS-PROV-010 is the acceptance gate for the first persistence implementation.

## Folio

Folio is **not a mandatory runtime dependency for the initial Forma-only composition scope**. It becomes mandatory when Forma Studio adds printable/PDF/paginated document composition, print preview, or Folio component authoring/testing. At that point, it MUST consume a pinned Folio release and use its public primitives rather than creating a parallel print system.

## Dependency rules

Shared dependencies MUST be pinned to released versions or immutable artifacts. Floating versions and moving repository branches are prohibited as application baselines. Any not-applicable decision or exception MUST be explicit and reviewable.

Forma Studio MUST record the reviewed Forma version and SHOULD automate detection of a newer available Forma release without automatically changing the pinned version.
