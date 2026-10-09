# Forma Studio document model

## Design principle

The saved artifact represents intent and composition, not pixels.

Stable identifiers are mandatory for projects, pages, diagrams, component nodes, diagram nodes, diagram edges, groups/lanes/phases, metadata field definitions, palette slots, appearance styles, presentation mappings, scenarios, navigation actions, assets, typed references where addressable, and annotations.

## Project

A Project contains:

- schemaVersion
- projectId
- name
- description
- createdWith
- formaVersion
- optional startPageId (required when pages exist; absent for diagram-only projects)
- pages
- diagrams
- metadataFieldDefinitions
- paletteSlots
- appearanceStyles
- presentationMappings
- scenarios
- assets
- typed project/resource references where applicable
- metadata

## Page

A Page contains:

- stable pageId
- name
- optional route
- optional title/description
- optional typed/namespaced metadata and visibility policy
- root component-node list
- page-level annotations
- preview settings
- optional requirements references

Routes must be unique after normalization.

A page may exist without a route for non-routable states, but any page targeted by URL navigation must have a route.

## Diagram

A Diagram contains:

- stable diagramId
- name
- profileId
- profileVersion
- optional extent mode and bounded-sheet dimensions where applicable
- nodes
- edges
- groups/containers and optional swimlanes
- diagram-level annotations
- optional requirements references
- optional presentation/view defaults that are explicitly part of authored output

A diagram is a graph specification. Rendered canvas objects are projections.

Flow node position and explicit geometry may be canonical authored data. Editor pan, zoom, current selection, hover, temporary drag state, and transient routing previews are not canonical project data.

## Diagram node

A DiagramNode contains:

- stable nodeId
- element kind
- normalized logical x/y position
- size or profile-defined sizing contract
- z-order/order key where overlap is supported
- authored locked state when supported
- authored visibility state when supported
- label/content
- optional typed/namespaced metadata
- optional metadata visibility/sensitivity policy
- optional named ports
- visual properties and public presentation capability identifiers
- optional authored appearance: fill, stroke/border, accent, foreground, palette/token/literal source
- optional typed cross-surface/resource references
- optional profile-specific semantic properties
- optional group/lane membership
- optional annotations and requirement references
- optional Forma icon name (see "Icons")

Typed profile semantics are explicit. Geometry or appearance alone never changes semantic node type.

## Diagram edge

A DiagramEdge contains:

- stable edgeId
- source node/port ID
- target node/port ID
- connector/relationship kind
- optional label
- optional typed/namespaced metadata
- optional metadata visibility/sensitivity policy
- optional profile-specific semantic properties
- optional authored appearance including connector stroke/accent and label foreground where supported
- optional routing mode
- optional explicit manual routing points
- optional edge-label position override
- optional visual marker/start/end presentation properties
- optional annotations and requirement references

Edges reference stable identities, not rendered DOM/canvas objects. Moving or resizing a node must not change edge identity or topology.

## Diagram groups, swimlanes, and phases

Groups, containers, lanes, and phases have stable identities and explicit membership/order semantics where applicable.

They may also carry legal typed/namespaced metadata, authored appearance/style references, annotations, and typed resource references according to the active profile.

General-profile grouping may be visual only. Typed profiles may assign semantic meaning such as actor, role, system, deployment boundary, or trust boundary, but that meaning must be explicit in project data and validated by the selected profile.

Visual overlap does not create membership.

## Component node

A component node contains:

- nodeId
- componentId corresponding to the canonical Forma contract
- componentVersion or inherited project Forma version
- properties
- tokenBindings
- content
- children by named slot
- optional navigation bindings
- optional typed/namespaced metadata and visibility policy
- optional typed resource references
- optional annotations
- optional scenario overrides
- optional Forma icon name as the `icon` property, on components whose contract takes one (button, link-button, alert, metric-card, workflow; see "Icons")

Studio must not invent component properties that are absent from the canonical contract.

## Icons

An icon is stored as the `icon` property of a component node (`properties.icon`) or as `"icon": "<name>"` on a diagram node: the name of a Forma icon in the registry grammar (lowercase letters and digits joined by single hyphens, at most 64 characters). The document stores the name only, never geometry, labels or status (Forma ICON-006, ICON-014).

- A name that the pinned Forma release does not have is kept and round-trips unchanged; it is not shown and not exported, and Studio says why. A newer Forma release may provide it.
- A stored value that is not a well-formed name (any other string, number, object or array) is kept verbatim as inert data: never rendered, never exported as markup, never rejected, never rewritten. Validation reports it as a warning (`icon.malformed`), never a blocker.
- An icon on a component with no place for one is kept and reported (`icon.unplaced`, advisory).
- Setting or removing an icon is the `SetIcon` appearance command: one history entry, reviewed and merged like other presentation edits. The command checks the name's form, not the pinned release, so agents may author names from a newer release.
- What the release has, and the artwork, come from the pinned Forma package's compiled `dist/icons` (registry, static SVG, decorative inline snippet), verified against each icon's `svgSha256` (see `docs/ICONS.md`).

A document is written as schema version 3 when a diagram node holds an icon and as schema version 2 otherwise, so a Studio build that reads only version 2 refuses a document with node icons instead of silently dropping them. Layout icons are component properties, which every schema version preserves. Version 1 and 2 documents that already contain node `icon` members are read without loss.

## Object metadata

Addressable Studio objects may carry typed, namespaced descriptive metadata, including Layout pages/component nodes and Flow diagram objects.

Metadata is separate from:

- stable identity;
- semantic element/relationship type;
- graph topology;
- geometry;
- authored appearance/color;
- accessibility name/description;
- provenance/evidence references where those are modeled separately.

Profiles/extensions may define metadata schemas and visibility/transport policies. Unknown metadata is preserved when safe and defaults to non-rendered/non-exported behavior unless an explicit contract says otherwise.

Metadata edits use canonical commands and participate in undo/redo, diff/merge, copy/paste, migration, validation, search, and agent export.

## Authored appearance and color

Flow objects may carry authored appearance independently from their semantic type/status.

Color sources are distinguishable as:

- Forma token reference;
- project/profile palette slot;
- fixed literal color where permitted;
- explicit metadata-to-color mapping result.

The project model preserves the source, not only a resolved rendered color.

Semantic type/status never derives from color. Metadata-to-color behavior is an explicit inspectable mapping contract with deterministic precedence and unknown-value behavior.

Project palettes and mapping rules are project/profile presentation data and do not become graph topology or domain transition authority.

## Shared object metadata

Metadata is a cross-surface object capability, not a Flow-only feature.

Projects, pages, addressable component nodes, diagrams, diagram elements, scenarios/annotations, and future canonical Folio/document objects may carry the same typed/namespaced metadata envelope where their contract permits it.

Object metadata is orthogonal to visible content, semantic type, Layout/Flow structure, geometry, appearance/style, accessibility semantics, and typed resource references. Surface/profile/component contracts decide which fields are legal and which fields may be projected into visible/runtime output.

## Metadata schemas and values

Profiles/extensions may define versioned metadata schemas. A field definition identifies a stable key, value type/cardinality, optional default, validation constraints, display/help metadata, and visibility/transport policy.

Object values distinguish explicit authored values from defaults, derived values, and external/source-bound values when those distinctions are meaningful. Localized labels are projections; stable field/value IDs are canonical.

Derived metadata should reference its source rule/relationship instead of duplicating authoritative structural facts silently.

## Appearance styles and palettes

A project may define named reusable Flow appearance styles and palette slots.

An object appearance may therefore contain:

- optional named style reference;
- explicit per-object overrides;
- Forma token references;
- project/profile palette references;
- fixed literals where allowed;
- an explicit resolved/mapping source where needed for inspection/export.

Style definitions and palette slots use stable identity. Resolved appearance is a projection and is not substituted for the style/palette source in canonical data.

Semantic element/relationship kind remains independent from shape, icon, fill, stroke, marker, pattern, and other appearance unless a profile explicitly constrains legal presentation choices.

## Typed resource references

Addressable diagram objects may carry typed references to internal or external resources.

A typed reference is not generic metadata: it has a reference kind, target identity, optional display/location data, and availability/verification state where known. Internal references participate in deletion-impact and referential validation.

## Diagram profile

A profile definition identifies:

- stable profileId
- profileVersion
- legal node/edge/container/lane kinds
- legal ports and connection rules
- property schemas/defaults
- containment rules
- validation rules
- presentation capability mappings
- migration/version compatibility rules

A project must never infer a profile merely from rendered appearance.

If a profile/version is unavailable, Studio preserves the canonical diagram and reports the unavailable capability; it does not flatten the diagram to General.

## Geometry normalization

Canonical Flow geometry uses one documented logical coordinate system.

Transient pointer precision may exceed persisted precision. Commands that commit geometry normalize position, dimensions, route points, and label offsets through one shared normalization contract before canonical state is produced.

NaN/infinite/invalid dimensions never enter canonical state.

## Cross-surface references

Diagram elements may carry typed references to other diagrams, diagram nodes, Forma pages, requirements, documents, external URLs, or later registered targets.

Internal references use stable IDs. Display names/routes are projections.

Deletion/migration must surface inbound references before producing dangling canonical relationships.

## Shared editor commands

All canonical changes to pages and diagrams use one command/transition mechanism.

Equivalent intents from drag/drop, inspector editing, structure/outline editing, keyboard/touch input, or an agent must converge on the same typed command.

Examples include:

- AddComponent / MoveComponent / SetProperty
- AddDiagram / RemoveDiagram
- AddDiagramNode / MoveDiagramNode / ResizeDiagramNode
- ConnectDiagramNodes / RemoveDiagramEdge
- SetDiagramElementProperty
- GroupDiagramNodes / MoveDiagramNodeToLane
- ApplyDiagramLayout

Commands validate against the current project state and selected surface/profile before producing a successor state. Failed commands leave the prior canonical state unchanged.

## Navigation

Navigation is represented explicitly rather than encoded in arbitrary href strings.

A NavigationAction contains:

- actionId
- sourceNodeId
- trigger
- target kind
- targetPageId or external URL
- optional query parameters
- optional fragment
- history behavior: push or replace

Internal navigation targets stable page IDs. Routes are a projection.

Deleting or changing a target page must surface obligations for affected links.

Cycles are legal. Broken targets are not.

## Scenario

A Scenario provides deterministic preview data/state without claiming to be application domain logic.

Examples:

- default
- loading
- empty
- validation-error
- network-failure
- permission-limited
- long-content
- mobile-stress

Scenario data must remain plain serializable data.

## Token binding

Visual properties that map to Forma tokens store token identifiers, not copied values.

Literal visual values are allowed only where the relevant Forma contract explicitly permits them.

## Migration

Every persisted document has a schemaVersion.

Migrations are ordered, deterministic, testable, and never silently discard unsupported data.

A document from a newer schema version must fail safely and explain the incompatibility.

Schema versions: 1 (page-only, migrated in memory), 2 (diagrams, metadata, appearance), 3 (2 plus optional diagram-node icon names; written only when a node holds an icon).
