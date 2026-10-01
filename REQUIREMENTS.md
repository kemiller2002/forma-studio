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

## Folio

Folio is **not a mandatory runtime dependency for the initial Forma-only composition scope**. It becomes mandatory when Forma Studio adds printable/PDF/paginated document composition, print preview, or Folio component authoring/testing. At that point, it MUST consume a pinned Folio release and use its public primitives rather than creating a parallel print system.

## Dependency rules

Shared dependencies MUST be pinned to released versions or immutable artifacts. Floating versions and moving repository branches are prohibited as application baselines. Any not-applicable decision or exception MUST be explicit and reviewable.

Forma Studio MUST record the reviewed Forma version and SHOULD automate detection of a newer available Forma release without automatically changing the pinned version.


## Portable workflows and embeddable designer

Status: **Required**

Forma Studio workflow authoring MUST be designed as an embeddable capability, not only as a standalone Studio screen. Applications MUST be able to host the workflow designer/editor within their own Forma-based user experience without reproducing Studio internals.

The embeddable workflow surface MUST support explicit host modes including, at minimum:

- view/read-only,
- inspect,
- edit,
- select/pick,
- and interactive/runtime visualization where application state is projected onto a workflow.

Embedding MUST expose a small, versioned host contract for loading a workflow, receiving validated changes, selecting/focusing workflow objects, reporting validation findings, and requesting supported commands. Host applications MUST NOT need direct access to Studio's internal state representation.

The embedded designer MUST retain Forma accessibility, responsive, mobile/touch, theme/skin, motion, and keyboard contracts. An embedding host MUST be able to inherit an approved Forma brand/skin without rewriting workflow content.

Embedding MUST NOT require an iframe when the same-origin/application architecture can host the public component directly. If an iframe or isolated host is supported for stronger isolation, it MUST use the same portable workflow contract and an explicit versioned message boundary.

The workflow renderer/editor SHOULD be independently consumable from the full Studio application so applications that only need workflow display or constrained editing do not need to ship the complete Studio shell.

### Standard portable workflow file

Forma MUST publish a canonical, documented, versioned workflow interchange format that can be created without Forma Studio and then opened, validated, rendered, and edited by Forma Studio.

The canonical format SHOULD use a human-readable, source-control-friendly representation. JSON MUST be the normative interchange encoding unless a later governed requirement replaces it.

A portable workflow file MUST NOT contain private Studio implementation state. The file MUST describe the workflow itself.

The normative top-level contract MUST include, at minimum:

- format identifier,
- schema/version identifier,
- stable workflow identifier,
- title/name and optional description,
- target Forma compatibility/version information,
- workflow metadata,
- nodes/items,
- connections/edges,
- optional groups/containers/swimlanes,
- presentation/layout information,
- interaction/navigation information where applicable,
- validation-relevant semantics,
- extension data,
- and provenance information sufficient to identify the producing system when supplied.

Every addressable workflow object MUST have a stable identifier that survives ordinary editing and layout changes. References between objects MUST use those stable identifiers rather than array position or transient DOM identity.

### Workflow node/item contract

A workflow node/item MUST be capable of representing, where applicable:

- stable identifier,
- semantic kind/type,
- label/title and optional description,
- position and size or layout intent,
- visual variant,
- color using an approved semantic/token representation where possible,
- icon or public visual role,
- state/status,
- ports/connection points when required,
- application/domain reference,
- arbitrary metadata,
- accessibility text/semantics,
- interaction/navigation intent,
- and namespaced extension data.

Workflow objects MUST support metadata so applications can attach domain meaning without changing the visual workflow contract. Metadata MUST be serializable and round-trip through Studio unchanged when Studio does not understand it.

### Connection/edge contract

Connections MUST be first-class objects with stable identifiers. They MUST be capable of representing source, target, optional source/target ports, direction, semantic kind, label, visual treatment, state, metadata, and namespaced extension data.

Studio MUST validate dangling references, invalid connection types, unsupported ports, duplicate identifiers, and other structural errors without silently deleting external data.

### Extensibility and namespacing

The portable format MUST define a safe extension mechanism for producer-specific data. Extensions MUST be namespaced so multiple systems can add information without key collisions.

Unknown extension data MUST round-trip unchanged unless it is invalid according to the base serialization rules. Studio MUST NOT require understanding an extension merely to open and save an otherwise valid workflow.

Core semantics required for interoperable rendering MUST NOT be hidden inside producer-specific extensions.

### Separation of semantics and layout

The workflow format MUST distinguish semantic workflow information from editor/layout information sufficiently that another system can generate a meaningful workflow without calculating exact pixel placement.

Position/layout information MAY be omitted when the producer does not know it. Studio MUST be able to apply a deterministic default layout or request layout generation without changing workflow semantics.

Re-layout MUST NOT change stable IDs, metadata, domain references, edge semantics, or other non-layout information.

### File naming and published layout

Forma MUST publish a standard repository/file layout for workflow assets so humans and automated systems can discover them consistently.

The initial convention MUST support a structure equivalent to:

```text
forma/
  workflows/
    <workflow-id>.forma-workflow.json
```

A repository MAY contain multiple workflow files. Related optional assets or examples MAY be placed beneath a directory named for the workflow when needed, but the workflow file MUST remain independently understandable unless its manifest explicitly declares those dependencies.

The canonical filename suffix MUST be `.forma-workflow.json` unless superseded by a versioned migration.

Forma MUST publish the JSON Schema for the portable format at a stable repository/package path. The schema MUST be usable by systems that do not depend on Forma Studio.

### External producers and consumers

Creating a valid workflow MUST NOT require running Forma Studio. Other Echelon systems, agents, command-line tools, backend services, and third-party applications MUST be able to produce a workflow file by following the published schema.

Studio MUST treat externally generated valid workflows as first-class documents rather than imports that are converted into a private one-way format.

A workflow created by another system MUST be able to follow this lifecycle without semantic loss:

```text
external producer
  -> portable workflow file
  -> Forma Studio open/edit/validate
  -> portable workflow file
  -> external consumer
```

Round-trip tests MUST prove preservation of stable IDs, semantic fields, metadata, unknown namespaced extensions, and connections.

### Validation and compatibility

The workflow schema MUST use explicit semantic versioning or another governed compatibility scheme. Producers MUST declare the format version they emit.

Studio MUST distinguish:

- valid and fully supported,
- valid with unknown preserved extensions,
- valid but requiring migration,
- structurally invalid,
- semantically invalid,
- and valid format using Forma capabilities unavailable in the selected target version.

Schema validation MUST be available independently of the visual designer so CI, Praxis, agents, and other systems can validate generated workflows.

### Security boundary

Portable workflow files MUST be data, not executable code. The core format MUST NOT permit arbitrary JavaScript, inline event-handler code, executable HTML, or other producer-supplied code to execute merely because a workflow is opened.

Interactions MUST be represented as declarative intents/actions whose implementation is supplied by the embedding host or approved runtime capability.

Studio MUST safely display untrusted workflow labels, descriptions, metadata, and extension data without treating them as executable markup.

### Reference implementation and examples

Forma MUST publish:

- the normative workflow schema,
- a minimal valid workflow,
- a representative multi-step workflow,
- a workflow containing groups/swimlanes,
- a workflow demonstrating object and edge metadata,
- a workflow demonstrating namespaced external-system extensions,
- and an embedding example showing a workflow created outside Studio and then hosted/edited using the embeddable designer.

These examples MUST be part of compatibility tests so the published interchange contract remains executable evidence rather than documentation only.
