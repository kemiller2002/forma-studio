# 16 Flow and diagram authoring

## Surface and product boundary

FDA-001 Studio MUST provide a Flow surface for connected diagrams using the same shared editor core as Layout surfaces.

FDA-002 Flow is a spatial graph-authoring surface, not a second application and not a separate persistence/history subsystem.

FDA-003 Flow MUST support both general-purpose diagrams and explicitly typed semantic diagram profiles.

FDA-004 General-purpose diagrams MUST allow useful boxes/shapes, labels, notes, connectors, groups, and containers without requiring users to assign workflow/domain semantics.

FDA-005 Typed semantics MUST be opt-in and explicit. Studio MUST NOT infer consequential workflow, state, security, deployment, ownership, or data semantics merely from geometry or visual appearance.

FDA-006 Flow MUST NOT expand Studio into a freehand vector illustration tool. Arbitrary Bezier/path authoring, photo editing, and illustration remain outside the initial scope.

FDA-007 A Flow diagram MAY coexist with Layout pages in the same project and MAY reference pages, requirements, external systems, or other diagrams by stable identifiers/links where the relevant contract supports it.

## Canonical graph model

FDA-020 Every diagram MUST have a stable diagram ID, name, profile/kind, nodes, edges, groups/containers, and diagram-level metadata.

FDA-021 Every node MUST have a stable node ID, element kind, position, size or sizing contract, label/content, optional ports, and typed/profile-specific properties.

FDA-022 Every edge MUST have a stable edge ID, source endpoint, target endpoint, connector kind, optional label, and profile-specific properties.

FDA-023 Edge endpoints MUST reference stable node/port IDs rather than rendered DOM elements or screen coordinates.

FDA-024 Moving or resizing a node MUST preserve node identity and attached edge identity.

FDA-025 Deleting a node with incident edges MUST surface the affected edges before completion and MUST never leave silently dangling canonical references.

FDA-026 Flow geometry is valid persisted intent. Unlike ordinary Layout composition, node position and explicit size MAY be canonical project data.

FDA-027 Viewport pan, zoom, current selection, hover state, temporary drag state, and transient routing previews MUST remain editor/view state unless explicitly promoted into project data by a documented command.

FDA-028 Groups/containers MUST have stable IDs and explicit membership. Visual overlap alone MUST NOT imply membership.

FDA-029 Swimlanes MUST be modeled as explicit containers/lane structures rather than inferred from position alone.

FDA-030 Manual connector-routing control points MAY be persisted when the user explicitly chooses manual routing; automatically generated routing MUST remain reproducible from canonical graph state.

## Nodes, ports, and connectors

FDA-040 Nodes MUST expose legal connection targets/ports when the active profile or node kind constrains connection points.

FDA-041 A user MUST be able to create an edge by dragging from a compatible source port/connection affordance to a compatible target.

FDA-042 Candidate targets MUST visually indicate whether the proposed connection is legal before commit.

FDA-043 Incompatible typed connections MUST be rejected before canonical mutation and SHOULD explain the violated profile rule.

FDA-044 General-purpose diagrams MAY use unconstrained connectors where no semantic profile rule applies.

FDA-045 Connectors MUST remain attached as nodes move or resize.

FDA-046 Studio MUST support at least directional flow connectors and non-directional associations; typed profiles MAY add transition, dependency, message, data, control, or annotation connector kinds.

FDA-047 Connector labels MUST be editable without replacing the connector identity.

FDA-048 Automatic connector routing SHOULD avoid node interiors and SHOULD update as connected nodes move.

FDA-049 Connector routing MUST NOT change graph semantics.

FDA-050 Self-edges and cycles MUST be allowed when the selected diagram profile permits them.

## Direct manipulation

FDA-060 Users MUST be able to add, select, move, resize, duplicate, delete, and label Flow nodes through direct manipulation.

FDA-061 Users MUST be able to select and manipulate edges independently of their endpoint nodes.

FDA-062 Flow SHOULD support marquee/box selection and additive multi-selection on pointer-capable devices.

FDA-063 Multi-selection MUST support atomic move and SHOULD support align, distribute, and group operations.

FDA-064 Flow SHOULD provide snapping, alignment guides, and configurable grid assistance.

FDA-065 Snapping and guides MUST be editor aids; they MUST NOT create hidden semantic relationships.

FDA-066 Flow MUST support pan, zoom, fit-selection, and fit-diagram view operations without mutating canonical graph semantics.

FDA-067 Core node/edge creation, selection, movement, connection, deletion, and property editing MUST have keyboard-accessible alternatives.

FDA-068 Touch devices MUST provide non-hover alternatives for connection, selection, movement, and property editing.

FDA-069 Direct manipulation MUST dispatch typed editor commands through the shared command surface rather than mutating rendered elements.

## Diagram profiles

FDA-080 The canonical graph model MUST allow profiles to add element kinds, connector kinds, properties, legal-transition/connection rules, validation, and presentation mappings without forking the editor core.

FDA-081 Studio MUST initially define a General profile with minimal semantic constraints.

FDA-082 Studio MUST define a Workflow profile capable of representing at least start/end, activity/process, decision, actor/role or lane, and labeled outcomes/transitions.

FDA-083 Workflow decisions MUST support explicit outgoing outcome labels/conditions as authored specification data.

FDA-084 A Workflow diagram MUST NOT be described as executable merely because it contains activities and decisions.

FDA-085 Studio MUST define a State profile capable of representing states and typed transitions with optional trigger/guard/action metadata.

FDA-086 State profile semantics MAY reference an external/domain state model, but Studio MUST distinguish diagram-authored documentation/specification from authoritative application-domain legality unless an explicit integration contract establishes authority.

FDA-087 Studio MUST define an Architecture profile capable of representing actors, applications/services, storage, queues/messages, external systems, and explicit boundaries/containers.

FDA-088 Architecture profiles SHOULD support typed dependency/data/message/control relationships where doing so improves validation or handoff.

FDA-089 Trust/security boundaries MAY be explicitly modeled, but Studio MUST NOT infer that a boundary is secure or that a crossing is authenticated merely because of its visual notation.

FDA-090 Sequence and data/ER profiles SHOULD remain compatible future projections over the same shared graph model rather than separate canvas engines.

## Containers and swimlanes

FDA-100 A group/container MAY be purely visual in the General profile or semantic in a typed profile; that distinction MUST be explicit in saved data.

FDA-101 A swimlane MUST support an explicit identity/label and ordered contained elements.

FDA-102 In typed Workflow diagrams, moving an activity between semantic lanes MUST surface the resulting responsibility/ownership change before or as part of the command result.

FDA-103 Container movement SHOULD move explicitly contained child nodes as one atomic command unless the user requests detachment.

FDA-104 Studio MUST distinguish grouping, containment, and visual overlap.

## Auto-layout and arrangement

FDA-120 Flow SHOULD provide deterministic auto-layout commands for at least vertical/hierarchical and horizontal/hierarchical arrangements.

FDA-121 Additional arrangements MAY include radial, dependency-oriented, swimlane-aware, or profile-specific strategies.

FDA-122 Auto-layout MUST be an explicit reversible command and MUST NOT run silently after ordinary manual edits.

FDA-123 Auto-layout MUST preserve node IDs, edge IDs, semantic profile data, group membership, labels, and external references.

FDA-124 A user MUST be able to manually adjust an auto-laid-out diagram without leaving the canonical model.

FDA-125 Align/distribute/space commands MUST operate only on the selected eligible nodes and MUST be undoable.

## Inspector and structure representation

FDA-140 Selecting a Flow node, edge, group, lane, or diagram MUST expose only properties legal for that element/profile.

FDA-141 Studio MUST expose a non-spatial structure/outline view of Flow content so a user or agent can understand and navigate the diagram without relying solely on position.

FDA-142 The structure representation MUST expose stable IDs for debugging and handoff while presenting human-readable labels as the primary UI.

FDA-143 For typed diagrams, the inspector SHOULD distinguish visual properties from semantic properties.

FDA-144 Changing visual appearance MUST NOT silently change semantic element or relationship type.

FDA-145 Changing a semantic type MUST be validated against existing edges/properties and MUST surface obligations or invalid relationships created by the change.

## Validation

FDA-160 Flow validation MUST detect duplicate stable IDs, missing endpoints, dangling edges, invalid ports, invalid containment references, and invalid profile-specific relationships.

FDA-161 Typed-profile validation MUST distinguish graph-integrity failures from domain unknowns and advisory design findings.

FDA-162 Validation MUST identify the affected diagram element and SHOULD provide legal repair actions when they are known.

FDA-163 Studio MUST NOT treat visual neatness, successful auto-layout, or absence of overlapping nodes as proof of semantic validity.

FDA-164 A diagram with unresolved validation blockers MUST remain exportable as a diagnostic artifact when explicitly requested, but MUST be labeled invalid/incomplete.

## Accessibility and communication

FDA-180 Every meaningful diagram node and relationship MUST have a machine-readable identity independent of its visual appearance.

FDA-181 Color, connector shape, spatial placement, animation, or line style MUST NOT be the sole carrier of required semantic meaning.

FDA-182 Typed relationship labels and node meanings MUST be available to keyboard/assistive-technology users through the structure/inspector representation even when the visual canvas is difficult to consume directly.

FDA-183 Flow editing focus order and keyboard commands MUST be documented and discoverable.

FDA-184 Zooming and panning MUST NOT make inspector/structure access unusable.

## Persistence, export, and interoperability

FDA-200 Canonical project export MUST preserve diagrams, graph topology, explicit geometry, profiles, semantic properties, groups/lanes, labels, and manual routing data without requiring screenshots.

FDA-201 Import/export round-trip MUST preserve graph meaning and stable IDs.

FDA-202 Semantic diff MUST be able to distinguish node/edge additions, removals, property changes, movement/geometry changes, and relationship changes.

FDA-203 Agent-oriented export MUST make graph topology and typed semantics directly readable without image recognition.

FDA-204 SVG, image, PDF, or print projections MAY be provided, but no rendered projection may replace the canonical graph specification.

FDA-205 Printable/paginated diagram composition MUST use Folio once that capability enters scope, consistent with the existing Folio dependency rule.

FDA-206 Future external diagram-tool adapters MUST translate through the canonical graph model rather than becoming a second source of truth.

## Initial proof and acceptance

FDA-220 The first Flow vertical slice MUST support: create diagram -> add two nodes -> connect -> move a node -> keep connector attached -> label the relationship -> undo -> redo -> save -> reload.

FDA-221 The first shared-editor proof MUST demonstrate that the Flow slice and the Layout Stack/Heading slice use the same selection abstraction, command dispatcher, history mechanism, persistence envelope, and validation-result pattern where their semantics overlap.

FDA-222 The Flow proof MUST include automated tests for command legality, stable IDs, edge preservation during node movement, undo/redo, and deterministic round-trip serialization.

FDA-223 The Flow proof MUST include at least one General diagram and one typed Workflow or State example.

FDA-224 The architecture MUST be rejected/reworked if Flow implementation requires a separate authoritative editor state, separate undo engine, or DOM/canvas state as the persisted source of truth.


## Diagram lifecycle and organization

FDA-230 A user MUST be able to create, rename, duplicate, reorder, and delete diagrams through ordinary Studio commands.

FDA-231 Diagram IDs MUST remain stable across rename, reordering, folder/tag changes, and view-state changes.

FDA-232 Diagram duplication MUST create new diagram, node, edge, group, lane, annotation, and other internal IDs while preserving valid relationships inside the duplicate.

FDA-233 Diagram duplication MUST surface an explicit policy for references that target resources outside the duplicated diagram rather than silently remapping or dropping them.

FDA-234 Deleting a diagram with inbound project references MUST surface every affected reference before completion and MUST NOT silently orphan references.

FDA-235 Studio SHOULD support diagram folders/groups and tags for large projects without changing graph semantics.

FDA-236 Studio MUST support search by diagram name, diagram ID, node/edge label, stable element ID, annotation text, and requirement reference.

FDA-237 Search results MUST allow navigation to and selection of the matching diagram element without requiring spatial scanning.

FDA-238 Recent/frequently opened diagram affordances MAY be provided as editor conveniences but MUST NOT alter canonical project ordering or semantics.

## Diagram presentation authority

FDA-240 Forma remains the authority for production presentation contracts used by Forma Studio.

FDA-241 Studio MUST NOT create a second private production design system for diagram nodes, connectors, groups, lanes, labels, markers, or diagram surfaces merely because the current Forma release lacks diagram primitives.

FDA-242 Before implementation of production-facing diagram rendering, Studio MUST classify each visual primitive as one of: public Forma capability, editor-only Studio adorner/chrome, external asset, or unresolved Forma capability gap.

FDA-243 A diagram primitive intended to appear in exported/published application or documentation presentation MUST use a public Forma/Folio contract when such a contract exists.

FDA-244 If a required reusable diagram presentation primitive does not exist in Forma, Studio MUST record the missing capability as a governed Forma gap rather than silently treating Studio-specific CSS as reusable production presentation.

FDA-245 Editor-only handles, selection outlines, connection affordances, guides, route previews, hover targets, marquee boxes, and similar authoring adorners MAY remain Studio-owned because they are not exported production content.

FDA-246 Diagram export MUST distinguish Studio editor adorners from authored diagram presentation, and editor adorners MUST never leak into ordinary exported output.

FDA-247 The diagram profile contract MUST identify which element kinds depend on public Forma presentation identifiers/versions and which are editor-only structural constructs.

## Coordinate system and geometry determinism

FDA-250 The Flow coordinate system MUST use documented logical units independent of device pixel ratio, browser zoom, and canvas zoom.

FDA-251 The project schema MUST define the coordinate origin, axis direction, legal numeric range, precision, and normalization rules.

FDA-252 Studio MUST explicitly define whether negative coordinates are legal. If legal, save/export/auto-layout/fit operations MUST handle them deterministically.

FDA-253 Canonical geometry MUST be normalized before deterministic export so sub-pixel pointer noise does not create meaningless source-control diffs.

FDA-254 Pointer movement MAY use higher transient precision than persisted geometry, but commit MUST normalize to the canonical precision contract.

FDA-255 Resizing MUST define minimum and, where applicable, maximum dimensions by element/profile contract.

FDA-256 Zero, negative, NaN, infinite, or otherwise invalid geometry MUST be rejected before entering canonical project state.

FDA-257 Geometry normalization MUST NOT silently move an element by a visibly material amount; if normalization would be materially lossy, the operation MUST surface that fact.

FDA-258 Auto-layout, import adapters, paste, alignment, and distribution MUST all emit geometry through the same canonical normalization rules.

## Canvas extent and navigation

FDA-260 Studio MUST define Flow surface extent behavior explicitly as infinite/unbounded logical canvas, bounded sheet, or a supported choice between the two.

FDA-261 An unbounded canvas MUST still have deterministic content bounds derived from authored elements for fit/export/navigation operations.

FDA-262 A bounded-sheet diagram MUST persist its sheet/page dimensions and MUST reject or explicitly handle authored elements outside the legal boundary.

FDA-263 Switching between bounded and unbounded surface modes, if supported, MUST be an explicit reversible project command with validation of elements that would become out of bounds.

FDA-264 Flow MUST support zoom-to-selection and zoom-to-search-result.

FDA-265 Flow SHOULD support an overview/minimap or equivalent large-diagram navigation aid when diagram size makes panning alone inefficient.

FDA-266 Any minimap/overview MUST be a projection of canonical graph state and MUST NOT become a second editable source of truth.

FDA-267 Flow SHOULD expose current zoom level and provide deterministic reset/100%-equivalent and fit-content commands.

FDA-268 Navigation commands MUST preserve canonical project state and SHOULD preserve current selection where sensible.

## Ordering, locking, and visibility

FDA-270 Overlapping Flow elements MUST have deterministic render/hit-test order.

FDA-271 The canonical model MUST define whether z-order is explicit authored data or derived from a stable ordering field.

FDA-272 Studio MUST support bring forward, send backward, bring to front, and send to back for eligible elements when overlap is supported.

FDA-273 Z-order changes MUST be explicit undoable commands and MUST NOT change semantic containment or graph relationships.

FDA-274 Studio SHOULD support locking diagram elements against accidental pointer movement/resizing while still allowing selection/inspection.

FDA-275 Locking MUST NOT imply authorization, immutability, domain protection, or security semantics.

FDA-276 Studio MAY support author-controlled visibility of diagram elements/layers/groups, but hidden state MUST be explicit project data when it affects authored output.

FDA-277 Editor-only hiding used for temporary focus MUST remain view state and MUST be distinguishable from authored hidden state.

FDA-278 Validation MUST continue to include semantically relevant hidden authored elements unless the profile explicitly defines exclusion semantics.

## Connector editing and routing

FDA-280 A user MUST be able to reconnect either endpoint of an existing edge without replacing the edge ID when the resulting relationship remains legal.

FDA-281 Reconnection MUST preview candidate endpoint legality before commit and MUST leave the prior connection intact if the new target is rejected.

FDA-282 Studio MUST define supported routing modes, including at least straight and orthogonal/elbow routing when the visual contract supports them.

FDA-283 A user SHOULD be able to add, move, and remove manual bend/control points for eligible connector routing modes.

FDA-284 Manual route edits MUST be distinguishable from automatically generated routing.

FDA-285 When a connected node moves, Studio MUST preserve user-authored route intent where possible and MUST define deterministic fallback behavior when the old route is no longer valid.

FDA-286 Studio MUST define connector start/end marker semantics, including direction arrowheads where applicable, as typed presentation properties rather than arbitrary SVG path decoration.

FDA-287 Typed relationship kind and visual marker style MUST remain distinct concepts unless the profile explicitly binds them.

FDA-288 Multiple edges between the same pair of nodes MUST be supported when the active profile permits them and MUST remain independently selectable by stable ID.

FDA-289 Parallel/multiple edges MUST be routed or offset so that distinct relationships remain discoverable where technically feasible.

FDA-290 Edge labels MUST have deterministic anchoring/positioning behavior and MUST remain attached to the edge during automatic rerouting.

FDA-291 A user MAY override edge-label position when supported; that override MUST be persisted explicitly and remain independent of relationship semantics.

FDA-292 Edge selection/hit testing MUST remain usable for short, overlapping, and parallel connectors without requiring pixel-perfect pointer precision.

## Copy, paste, duplicate, and fragments

FDA-300 Copy/paste of Flow selections MUST serialize canonical diagram fragments rather than browser/canvas objects.

FDA-301 Copying selected nodes MUST include an explicit, deterministic policy for incident edges: internal edges between selected nodes SHOULD be included; edges to unselected nodes MUST be omitted or represented as unresolved external references, never silently rebound.

FDA-302 Pasting a fragment MUST allocate new stable IDs for copied elements while preserving internal graph topology through ID remapping.

FDA-303 Pasting within the same diagram MUST offset or otherwise place the duplicate predictably so it does not become visually indistinguishable from the source.

FDA-304 Cross-diagram paste MUST preserve compatible profile semantics and MUST surface incompatible element/relationship kinds before canonical mutation.

FDA-305 Cross-project paste MUST preserve external/requirement references only when legal and MUST surface unresolved local references.

FDA-306 Duplicate-selection MUST be implemented through the same fragment/remap semantics as copy/paste so behavior cannot diverge.

FDA-307 Groups, lanes, manual routes, labels, annotations, profile properties, and authored visibility/locking state MUST participate in fragment serialization when included by the copied selection.

FDA-308 Clipboard fragments MUST be versioned or self-describing enough to reject or migrate incompatible future formats safely.

## Shape, stencil, and profile catalogs

FDA-310 The Flow palette MUST be derived from the active diagram profile and registered public presentation capabilities rather than an unrelated hard-coded list.

FDA-311 Palette entries MUST have stable element-kind identifiers separate from display labels.

FDA-312 Palette entries SHOULD expose searchable names, categories, purpose/semantic description, and a miniature preview where performance permits.

FDA-313 Profile-provided node/relationship kinds MUST declare legal properties, ports, containment roles, defaults, and validation constraints.

FDA-314 A profile MUST be able to declare that a node kind uses a public Forma presentation primitive, an external asset, or an unresolved presentation gap.

FDA-315 Studio MUST NOT silently substitute a visually similar node/shape kind when the requested profile kind is unavailable.

FDA-316 User-defined reusable diagram fragments MAY be supported after the base graph model stabilizes and MUST use ordinary canonical graph concepts rather than arbitrary opaque stencil data.

FDA-317 User-defined reusable fragments MUST preserve clear provenance and dependency/version identity when shared across projects.

FDA-318 Icons/images used inside nodes MUST use the existing Studio asset security/accessibility contracts.

## Profile identity, versioning, and migration

FDA-320 Every typed diagram MUST persist a stable profile identifier and profile version or version range sufficient to validate the document deterministically.

FDA-321 General/untyped diagrams MUST still declare the canonical General profile/version rather than relying on an implicit undocumented default.

FDA-322 Profile definitions MUST be versioned independently from individual diagrams.

FDA-323 Opening a diagram whose profile version is unavailable MUST preserve the diagram data and report the profile as unavailable/unsupported rather than flattening it to General.

FDA-324 Profile upgrades MUST be explicit when they change element kinds, properties, relationship legality, defaults, or validation semantics.

FDA-325 Profile migration MUST have deterministic before/after fixtures and MUST never silently discard unknown profile-specific data.

FDA-326 A profile migration that requires a lossy choice MUST block and create an explicit obligation.

FDA-327 Profile upgrade preview SHOULD identify affected nodes/edges and changed validation findings before commit.

FDA-328 Unknown forward-compatible element/property data SHOULD be preserved when safe even if the current Studio version cannot edit it.

FDA-329 The profile contract MUST separate visual/presentation version compatibility from semantic validation-version compatibility when those can change independently.

## Subflows, drill-down, and cross-surface references

FDA-330 A diagram node MAY reference another diagram as a subflow/subprocess/drill-down target when the active profile permits it.

FDA-331 Cross-diagram references MUST use stable diagram IDs and optional stable node IDs, not copied labels.

FDA-332 Workflow subflow nodes SHOULD distinguish referenced-diagram navigation from executable subprocess semantics unless an explicit execution contract exists.

FDA-333 A diagram node MAY reference a Forma page, requirement, documentation artifact, external URL, or other registered resource through typed link/reference kinds.

FDA-334 Deleting or changing a referenced target MUST surface inbound diagram references using the same dependency principles as page navigation.

FDA-335 Studio MUST prevent recursive-reference validation from becoming nonterminating; cycles between diagrams MAY be legal where the reference kind permits them.

FDA-336 Structure/outline and inspector surfaces SHOULD provide drill-down/back navigation without requiring spatial repositioning.

## Undo transaction/coalescing semantics

FDA-340 A continuous drag of one or more nodes MUST commit as one logical undoable command unless the user explicitly confirms multiple stages.

FDA-341 A continuous resize gesture MUST commit as one logical undoable command.

FDA-342 Continuous connector route editing SHOULD commit as one logical undo unit per completed gesture.

FDA-343 Inline label/text editing SHOULD coalesce ordinary keystrokes into sensible undo units while preserving deterministic canonical commands.

FDA-344 Preview/intermediate pointer movement MUST NOT flood canonical command history.

FDA-345 Coalescing MUST NOT merge across semantic boundaries such as changing selection, changing element type, committing a connection, or triggering an external effect.

FDA-346 Undo/redo of a coalesced spatial command MUST restore exact canonical pre/post geometry and topology.

## Large-diagram performance and responsiveness

FDA-350 Studio MUST define and test a Flow reference workload separately from the page/component workload.

FDA-351 Before release claims are made, Studio MUST measure at least one documented large-diagram fixture containing substantial node and edge counts and representative labels/groups.

FDA-352 Common local graph commands MUST remain interactively responsive under the documented reference workload.

FDA-353 Flow rendering SHOULD cull/virtualize off-screen authored elements when needed without changing canonical graph state.

FDA-354 Edge rendering/routing SHOULD avoid recomputing unrelated edges after a local move when dependencies are known.

FDA-355 Incremental graph validation SHOULD reevaluate only affected graph regions/rules when correctness permits.

FDA-356 Whole-diagram auto-layout MUST be cancellable before commit or run as a staged preview that leaves canonical state unchanged until accepted.

FDA-357 Expensive layout/routing/validation work MUST not block access to recovery/cancel UI on supported devices.

FDA-358 Large-diagram structure/outline views SHOULD virtualize long collections when needed.

FDA-359 Performance optimization MUST NOT alter graph semantics, skip validation silently, or produce nondeterministic canonical output.

## Workflow and state semantic validation

FDA-360 Workflow validation SHOULD detect decision nodes with no outgoing outcomes.

FDA-361 Workflow validation SHOULD detect decision outcome labels/conditions that are duplicated or missing where the profile requires distinguishable outcomes.

FDA-362 Workflow validation SHOULD identify unreachable activities from declared workflow entry nodes when entry semantics are present.

FDA-363 Workflow validation SHOULD identify terminal dead ends that are not declared terminal outcomes where the profile can determine this safely.

FDA-364 Workflow validation SHOULD identify activities assigned to missing/deleted lanes/actors.

FDA-365 State diagrams MUST identify initial-state semantics explicitly when the selected profile requires an initial state.

FDA-366 State validation SHOULD detect unreachable states from declared initial states.

FDA-367 State validation MUST detect transition endpoints that reference unavailable states/ports.

FDA-368 State validation SHOULD detect transitions whose required trigger/guard metadata is structurally incomplete.

FDA-369 State validation MUST NOT claim a guard expression is logically correct merely because its field is syntactically present unless a separate validated expression language is defined.

FDA-370 Multiple initial/final states MUST be validated according to the selected State profile rather than by a universal hard-coded assumption.

FDA-371 Typed diagram validation MUST distinguish structural invalidity, advisory modeling smells, unresolved external references, and unavailable domain authority.

## Walkthrough/simulation versus execution

FDA-380 Studio MAY provide a non-executing visual walkthrough/simulation mode for typed workflow/state diagrams.

FDA-381 A walkthrough MUST operate only on explicitly authored graph semantics and user-provided scenario data; it MUST NOT invent missing domain rules.

FDA-382 Walkthrough state MUST be preview/editor state unless the user explicitly persists a scenario definition.

FDA-383 Studio MUST label walkthrough/simulation as specification preview, not production workflow execution.

FDA-384 Real workflow execution, orchestration, code generation, or compilation requires a separate explicit architecture/security/runtime contract and is out of scope until that contract exists.

FDA-385 A future execution adapter MUST consume the canonical typed graph rather than adding executable state directly to canvas/DOM objects.

## Accessibility traversal and announcements

FDA-390 Keyboard users MUST be able to navigate among diagram elements without relying on two-dimensional pointer movement alone.

FDA-391 The structure/outline view MUST provide deterministic sequential navigation for nodes, edges, groups, and lanes.

FDA-392 Studio SHOULD support relationship-oriented navigation such as next outgoing edge, next incoming edge, source node, and target node for typed graphs.

FDA-393 Accessible relationship descriptions MUST expose source label/identity, relationship kind/label, and target label/identity where available.

FDA-394 After create/connect/delete/move operations, focus MUST remain on a predictable relevant editor element and MUST NOT be lost to the document body.

FDA-395 Connection, deletion, validation failure, and other material graph changes SHOULD produce accessible status announcements without excessive verbosity.

FDA-396 Canvas zoom/pan MUST NOT be the only means to locate the currently focused/selected element.

FDA-397 Any minimap or purely visual overview MUST have a nonvisual equivalent through search/outline/navigation commands.

## Print and rendered export

FDA-400 Diagram export MUST compute explicit authored content bounds independent of current pan/zoom state.

FDA-401 SVG export SHOULD preserve vector text/shapes/connectors when the presentation contract supports vector output.

FDA-402 Image export MUST define resolution/scale behavior and SHOULD allow transparent versus explicit-background output where supported.

FDA-403 PDF/print export MUST define fit-to-page, actual-size, and multi-page tiling behavior where appropriate.

FDA-404 Multi-page tiled output MUST expose page boundaries in preview before export and MUST avoid silently clipping authored content.

FDA-405 Bounded-sheet diagrams SHOULD map naturally to corresponding Folio page dimensions when Folio integration is active.

FDA-406 Unbounded diagrams exported through Folio MUST require an explicit content-bounds/fit/tiling decision rather than inventing a page scale silently.

FDA-407 Exported relationship/node labels MUST remain legible at the selected output scale or produce a review finding when detectably too small.

FDA-408 Current selection, guides, handles, minimap, validation badges, and other editor adorners MUST be excluded from ordinary rendered exports unless the user explicitly requests a diagnostic/editor-state capture.

## External adapter compatibility and loss reporting

FDA-410 Every external diagram import/export adapter MUST define a mapping contract between source concepts and the canonical Studio graph/profile model.

FDA-411 Adapter results MUST classify unsupported or mismatched source concepts as lossless, transformed, lossy, unsupported, or unresolved rather than silently discarding them.

FDA-412 Import MUST preserve source identifiers/metadata where safe and useful for round-trip/provenance without making the external format authoritative.

FDA-413 Import with lossy mappings MUST produce a review report before canonical replacement/merge when the loss could alter meaning.

FDA-414 Export MUST not claim semantic fidelity for source-format features that Studio cannot represent.

FDA-415 Adapter-specific metadata MUST remain namespaced and MUST NOT leak into unrelated core graph semantics.

FDA-416 External formats that express executable/security/domain semantics require profile-specific review; visual similarity alone is insufficient for semantic mapping.

## Auto-layout identity and reproducibility

FDA-420 An auto-layout operation MUST record or otherwise deterministically identify the layout strategy and relevant options used to produce authored geometry when reproducibility requires it.

FDA-421 Layout algorithms/options used for deterministic canonical output MUST have versioned behavior or migration/compatibility rules when behavior changes materially.

FDA-422 Re-running the same deterministic layout strategy/version/options against equivalent normalized graph input SHOULD produce equivalent normalized geometry.

FDA-423 Randomized/stochastic layout MAY be offered only when a deterministic seed is recorded as part of the command/options when reproducibility matters.

FDA-424 Auto-layout preview MUST identify which elements will move and MUST not alter locked elements unless the user explicitly includes/unlocks them.

FDA-425 Auto-layout MUST respect semantic containers/lanes and profile constraints when the chosen strategy claims awareness of them.

FDA-426 Upgrading a layout algorithm MUST NOT silently rearrange existing saved diagrams merely because the project is opened in a newer Studio version.


## Port identity and connection cardinality

FDA-430 Ports used as canonical edge endpoints MUST have stable identities within their owning node/profile contract.

FDA-431 A port definition MAY declare direction such as input, output, bidirectional, or semantic/profile-specific direction.

FDA-432 A port definition MAY declare connection cardinality/capacity, including single, bounded-many, or unbounded-many relationships.

FDA-433 Connection creation and reconnection MUST validate port direction and capacity before canonical mutation when the profile defines those constraints.

FDA-434 Removing or renaming a port definition during profile migration MUST surface every affected edge endpoint before migration can complete.

FDA-435 Dynamically added user-authored ports MAY be supported only through an explicit profile capability and MUST receive stable IDs independent of their visual position.

FDA-436 Moving a visual port anchor around a node MUST NOT silently change semantic port identity.

FDA-437 Automatic port selection MAY be used for unconstrained General-profile connectors, but the selected canonical endpoint/attachment behavior MUST be deterministic after commit.

FDA-438 Edge endpoints attached to a whole node rather than a named port MUST remain distinguishable from named-port endpoints in canonical data.

## Node content, sizing, and overflow

FDA-440 Node contracts MUST declare whether sizing is fixed, content-auto-sized, constrained-auto-sized, or user-resizable.

FDA-441 Auto-sized nodes MUST resize deterministically from canonical content and presentation metrics available to the pinned presentation contract.

FDA-442 User resizing an auto-sized node MUST either switch to an explicit supported sizing mode or be rejected; Studio MUST NOT create hidden local size overrides.

FDA-443 Node labels/content MUST support multiline text when the profile/presentation contract permits it.

FDA-444 Text wrapping, truncation, clipping, scrolling, or overflow behavior MUST be explicit in the relevant node/presentation contract rather than accidental browser overflow.

FDA-445 Detectable text clipping/illegibility caused by node size SHOULD produce a validation or review finding.

FDA-446 Long-content stress scenarios MUST include diagram nodes and connector labels.

FDA-447 Content edits that trigger auto-sizing MUST remain one coherent undoable operation including the resulting deterministic geometry update.

FDA-448 Node content MAY reference project assets where the node contract permits it, using existing asset identity/security/accessibility rules.

## Containment integrity

FDA-450 Canonical containment/group membership MUST be acyclic.

FDA-451 A group/container MUST NOT contain itself directly or indirectly.

FDA-452 Nested groups/containers MAY be supported, but membership semantics and transform behavior MUST be explicit and deterministic.

FDA-453 A node MUST NOT belong to multiple exclusive semantic containers/lanes when the active profile defines exclusive ownership.

FDA-454 Visual groups MAY permit different membership cardinality than semantic containers, but Studio MUST distinguish those concepts in the model and inspector.

FDA-455 Moving a semantic container MUST define whether child geometry is relative or absolute in canonical coordinates and MUST preserve the chosen contract deterministically.

FDA-456 Reparenting a node between containers MUST preserve world-visible position unless the command explicitly requests profile-specific repositioning.

FDA-457 Deleting a container MUST require an explicit policy for contained elements: delete, detach/reparent, or block; it MUST NOT silently discard contained authored content.

FDA-458 Collapsing/expanding a group or subflow, if supported, MUST distinguish editor view state from authored collapsed presentation state.

## Layers

FDA-460 Named layers MAY be supported as an organizational/presentation feature distinct from groups and semantic containers.

FDA-461 Layer membership MUST NOT be inferred from z-order, overlap, or containment.

FDA-462 If layers are supported, an element MAY belong to more than one layer only if the layer contract explicitly permits multi-membership.

FDA-463 Layer visibility/locking MUST be distinguishable from element-level authored visibility/locking and from editor-only focus/isolation state.

FDA-464 Layer operations MUST NOT change graph topology or semantic container/lane ownership.

FDA-465 Export adapters that cannot represent layers MUST report the mapping as transformed/lossy/unsupported according to adapter rules rather than flattening silently.

FDA-466 Layers are not required for the first Flow vertical slice; the canonical model MUST nevertheless avoid using group semantics in a way that prevents later orthogonal layer support.

## Spatial selection and movement details

FDA-470 Selection MUST be scoped to the active authored surface unless an explicit cross-surface operation is designed.

FDA-471 Marquee selection MUST define containment/intersection behavior consistently and SHOULD allow the user to understand which rule is active.

FDA-472 Keyboard movement MUST operate in canonical logical units and SHOULD provide a documented coarse/fine movement mechanism without making modifier keys the only accessible path.

FDA-473 Keyboard resizing, when supported, MUST follow the same canonical size constraints and normalization as pointer resizing.

FDA-474 Alignment/distribution commands MUST define the reference geometry used, such as bounds/centers/baselines, and MUST behave deterministically for equivalent input.

FDA-475 Alignment/distribution MUST respect locked elements and semantic-container constraints unless the user explicitly chooses a legal override.

FDA-476 Custom guides/rulers MAY be provided as editor aids. If guides are persisted for author collaboration, they MUST be explicitly modeled and MUST remain non-semantic.

FDA-477 Snap settings SHOULD be user/editor preferences unless the project intentionally authors grid/guide behavior for collaboration; transient preference changes MUST not dirty the project.

## Diagram metadata and custom properties

FDA-480 Diagrams, nodes, edges, groups, and lanes MAY carry namespaced metadata/custom properties where a registered profile or extension contract permits them.

FDA-481 Core Studio semantics MUST NOT depend on opaque custom-property blobs.

FDA-482 Custom metadata schemas MUST be versioned/namespaced to avoid collisions with future core fields.

FDA-483 Unknown custom metadata MUST be preserved when safe through import/export/migration even when the current Studio version cannot interpret it.

FDA-484 Secrets/credentials MUST NOT be stored in diagram custom properties.

FDA-485 Inspector UI MUST distinguish core semantic properties, presentation properties, and extension/custom metadata.

## Diagram review and comparison

FDA-490 Semantic diagram diff MUST distinguish topology changes from presentation/geometry-only changes.

FDA-491 A node label edit, node move, node type change, endpoint reconnection, edge-label edit, route-only change, and semantic relationship-kind change MUST appear as distinguishable diff categories.

FDA-492 Review UI SHOULD be able to focus a changed diagram element directly from a semantic diff result.

FDA-493 Geometry-only diffs SHOULD suppress meaningless changes eliminated by canonical normalization.

FDA-494 A review/export MUST not describe a moved node as a semantic workflow transition change unless topology/semantic properties actually changed.

FDA-495 Future visual before/after overlay MAY be added as a review aid but MUST remain secondary to canonical semantic diff.


## Spatial stability, themes, and viewport behavior

FDA-500 Flow diagrams MUST NOT automatically reflow/reposition canonical node geometry merely because the editor viewport, browser window, device orientation, zoom level, theme, skin, or brand changes.

FDA-501 Responsive page-layout rules from Forma MUST NOT be implicitly applied to Flow graph geometry.

FDA-502 A future responsive/constrained diagram mode MAY exist only through an explicit versioned diagram/profile capability with deterministic rules.

FDA-503 Theme/skin/brand changes MAY alter presentation but MUST NOT silently rewrite saved graph geometry.

FDA-504 If a presentation/font change causes detectable clipping or label-fit problems, Studio SHOULD report a review finding and MAY offer an explicit reversible reflow/resize command.

FDA-505 Browser-measured text metrics MUST NOT cause canonical geometry to drift merely by opening the same project in another supported environment.

FDA-506 If browser measurement is used to propose auto-size geometry, the committed normalized size MUST become explicit canonical state or be derivable by a separately specified deterministic layout contract.

FDA-507 Re-running an explicit text/node reflow command MUST be reviewable as a geometry change and MUST participate in undo/redo.

FDA-508 Connector routing and label positioning MUST remain stable under pure pan/zoom operations.

## Crossing and junction semantics

FDA-510 Visual intersection of two connector paths MUST NOT imply graph connectivity.

FDA-511 Graph connectivity exists only through canonical endpoints/ports or explicit junction-node semantics defined by the active profile.

FDA-512 If the presentation contract supports line-jump/bridge/crossover notation, its use MUST remain presentation-only and MUST NOT change topology.

FDA-513 An explicit junction, merge, split, or bus node MUST have stable identity when the profile models it as semantic topology.

FDA-514 Connector hit testing at crossings MUST allow each independent edge to remain individually selectable through an accessible alternative even when pointer selection is ambiguous.

## Declarative profile extensibility

FDA-520 Built-in and future external diagram profiles MUST expose declarative, typed contracts for element kinds, properties, ports, containment, relationships, validation, and presentation mappings.

FDA-521 Project documents MUST NOT carry arbitrary JavaScript or executable plugin code as part of a diagram profile.

FDA-522 Future externally distributed profile packages MUST be pinned/versioned and governed under the repository dependency/security policy.

FDA-523 Profile validation extensions MUST execute only through approved Studio extension/runtime boundaries and MUST NOT bypass canonical command legality or project validation.

FDA-524 Unknown/unavailable external profile packages MUST leave project data inspectable and preserved, while editing that requires the missing profile MAY be blocked.

FDA-525 Profile package identity MUST be distinguishable from profileId/profileVersion when multiple sources could publish similarly named profiles.

FDA-526 Built-in profile IDs MUST use stable namespaced identifiers so future third-party profiles cannot collide silently.

## Rotation and advanced transforms

FDA-530 Arbitrary rotation, skew, and freeform transform matrices are not required for the initial Flow implementation.

FDA-531 If node rotation is later introduced, it MUST be an explicit typed geometry property with documented units/normalization and profile/presentation support.

FDA-532 Rotation MUST NOT be inferred from connector direction or semantic node type.

FDA-533 Skew/freeform transform support requires a separate requirement because it materially affects hit testing, bounds, routing, accessibility, export, and print.

## Derived graph projections

FDA-540 Studio MAY use the shared Flow graph infrastructure to render derived/non-authored graphs such as page navigation, dependency, validation, requirement, or traceability views.

FDA-541 A derived graph MUST identify its authoritative source model and MUST NOT become canonical authored Diagram data merely because it uses the Flow renderer/editor shell.

FDA-542 Editing a derived graph MAY be allowed only when the gesture/command maps unambiguously to a legal command against its authoritative source model.

FDA-543 Derived graph auto-layout/geometry SHOULD remain view state unless the source feature explicitly authors presentation geometry.

FDA-544 The same graph projection infrastructure SHOULD be reused instead of creating independent bespoke node/edge selection, pan/zoom, routing, or accessibility implementations for each Studio graph view.

FDA-545 Authored Flow diagrams and derived graph views MUST remain distinguishable in project/export/agent semantics.

## Diagram import placement and merge modes

FDA-550 Importing an external diagram MUST explicitly choose or infer through a documented command whether the result creates a new diagram, replaces an existing diagram, or attempts a semantic merge.

FDA-551 Replace import MUST preserve the existing diagram ID only when the user explicitly chooses replacement and the adapter can legally map the new content into that identity.

FDA-552 Merge import MUST use stable/source identities and semantic matching rules explicitly; visual proximity or similar labels alone MUST NOT silently decide entity identity.

FDA-553 Ambiguous entity matching during import merge MUST create review obligations rather than guessing.

FDA-554 Import into a typed profile MUST validate the mapped graph before canonical commit.

FDA-555 Import preview SHOULD show source-to-canonical mapping outcomes and lossy/unsupported concepts before commit when practical.


## Renderer independence

FDA-560 Canonical diagram data MUST NOT depend on whether a Flow surface is rendered with HTML, SVG, Canvas, WebGL, or another approved projection technology.

FDA-561 Renderer-specific object IDs, DOM nodes, SVG path data generated from automatic routing, canvas display-list handles, or GPU resources MUST NOT appear as authoritative project identity.

FDA-562 Switching or optimizing the rendering backend MUST preserve canonical graph semantics, geometry, selection identity, command legality, accessibility alternatives, and deterministic export behavior.

FDA-563 Hit testing MAY be renderer-specific internally, but the resolved editor target MUST be a canonical stable element/port identity before a mutating command is requested.

FDA-564 If a high-performance renderer cannot expose sufficient native accessibility semantics, the Structure/Outline and relationship-navigation surfaces remain mandatory nonvisual equivalents.

FDA-565 Renderer fallback/degradation MUST be explicit when a device cannot support the preferred renderer; Studio MUST NOT silently drop diagram elements or relationships.

FDA-566 Rendering backend choice is an implementation concern and MUST NOT become a project-format compatibility requirement unless an authored capability genuinely depends on it.

## Semantic/read order

FDA-570 Diagram reading/navigation order MUST be deterministic and MUST NOT be inferred solely from x/y coordinates, z-order, or DOM paint order.

FDA-571 Studio MUST provide or derive a stable Structure/Outline order for nodes/groups/lanes independently of visual stacking order.

FDA-572 Profiles MAY define a default semantic traversal order, such as lane order plus authored element order or graph reachability, but the rule MUST be documented and deterministic.

FDA-573 Where profile semantics do not provide a safe order, authors SHOULD be able to adjust an explicit outline/reading order without moving geometry.

FDA-574 Edge/transition ordering from a node MUST be deterministic; decision outcomes SHOULD expose author-controlled ordering where order affects review/walkthrough presentation.

FDA-575 Reading-order changes MUST be undoable and MUST NOT change graph topology.

FDA-576 Node/edge content SHOULD support a longer accessible description distinct from the visible short label when the profile/presentation contract needs it.

FDA-577 A diagram SHOULD support a diagram-level summary/description that explains purpose/scope without requiring interpretation of spatial placement.

FDA-578 Static HTML/developer exports SHOULD include a structured nonvisual representation of graph relationships in addition to any visual SVG/image projection.

## Advanced workflow semantics

FDA-580 The Workflow profile SHOULD support explicit merge and parallel fork/join semantics without requiring users to fake them through unlabeled generic nodes.

FDA-581 Parallel/fork/join semantics MUST be explicit element/relationship types and MUST NOT be inferred merely because multiple edges enter or leave a node.

FDA-582 Workflow cycles/loops MAY be legal and MUST remain explicit graph topology.

FDA-583 Workflow profiles MAY add event/wait/timer/message concepts later, but such concepts MUST be typed and MUST not imply an operational scheduler exists.

FDA-584 Workflow subprocess/subflow references MUST distinguish inline visual grouping from a referenced reusable/child diagram.

FDA-585 Validation SHOULD detect fork/join structures that are structurally incomplete when the selected profile defines completion rules.

FDA-586 Studio MUST avoid claiming BPMN compliance unless a separately defined BPMN profile/adapter proves conformance to the applicable standard/version.

## Advanced state semantics

FDA-590 The State profile SHOULD remain compatible with future nested/composite states without requiring a new editor engine.

FDA-591 Composite-state containment, if introduced, MUST be explicit semantic containment rather than inferred from visual nesting.

FDA-592 Entry/exit actions, history states, concurrent regions, and similar advanced state semantics are not required initially; if added, they MUST be typed/versioned profile capabilities.

FDA-593 State diagrams MUST NOT infer executable event handlers or side effects from labels alone.

## Architecture-boundary semantics

FDA-600 Architecture diagrams MAY model explicit trust, network, deployment, process, repository, or organizational boundaries as profile-defined semantic containers.

FDA-601 Crossing a semantic boundary MUST be derivable from explicit containment plus relationship endpoints, not from a connector merely drawing across a rectangle.

FDA-602 A security-aware validator MAY report a relationship crossing a declared trust boundary without required authentication/encryption metadata only when the selected profile defines those metadata obligations.

FDA-603 Such validation MUST report missing/unknown evidence; it MUST NOT conclude that a system is insecure merely from absent diagram decoration when the profile does not require that declaration.

FDA-604 Architecture nodes MAY reference repositories/services/applications owned elsewhere, but those references MUST remain distinguishable from Studio-authored claims about runtime truth.

## Mobile and touch Flow authoring

FDA-610 Core Flow review and basic editing MUST be usable on touch/mobile without hover, right-click, mouse-wheel, or precision connector-line hit testing.

FDA-611 Mobile Flow MUST provide a non-drag connection workflow, for example select source -> choose Connect -> select target/port -> confirm, or an equivalent accessible capability-driven interaction.

FDA-612 Mobile Flow MUST provide non-drag movement/resizing/property alternatives when direct manipulation is impractical.

FDA-613 Touch hit targets for nodes, handles, ports, and connector controls SHOULD meet the editor's accessibility target-size guidance or provide an equivalent larger control surface.

FDA-614 Canvas panning gestures MUST not permanently disable browser/page accessibility gestures such as pinch zoom contrary to Studio accessibility requirements.

FDA-615 Studio SHOULD distinguish pan/selection/connect modes clearly on touch devices when gesture ambiguity would otherwise cause accidental edits.

FDA-616 A touch gesture cancelled by browser/OS interruption MUST leave canonical project state at the last committed command.

## Diagram annotations and review notes

FDA-620 An annotation/comment attached to a diagram element MUST reference its stable element ID rather than rely solely on absolute canvas coordinates.

FDA-621 Diagram-level annotations MAY also exist without an element target.

FDA-622 Spatial annotation anchors MAY be stored as supplemental presentation metadata, but loss of a spatial anchor MUST NOT sever an element-targeted annotation from its canonical target.

FDA-623 Deleting an annotated element MUST surface dependent annotations before completion and apply an explicit delete/re-anchor/orphan policy.

FDA-624 Review annotations MUST remain distinguishable from normative requirements, profile semantics, and executable workflow data.

FDA-625 Semantic diff/review SHOULD retain annotation references to the changed stable element whenever that element identity survives.

## Cross-surface embedding

FDA-630 A Layout page MAY embed/reference an authored diagram only through an explicit public embedding/presentation capability once such a capability exists.

FDA-631 Diagram embedding MUST reference the stable diagram ID rather than storing a flattened screenshot as the only source.

FDA-632 An embedded diagram MAY project a snapshot/vector rendering for presentation, but the canonical editable graph remains the referenced Diagram.

FDA-633 Deleting an embedded/referenced diagram MUST surface dependent Layout/Folio references.

FDA-634 Cycles created through cross-surface references/embeds MUST be detected where they could cause nonterminating rendering/export.

FDA-635 Folio/document embedding of diagrams MUST use the same stable diagram identity and explicit projection/export rules when Folio integration ships.


## Identity and graph scope

FDA-640 Diagram IDs MUST be unique within the project.

FDA-641 Canonical diagram element IDs for nodes, edges, groups, lanes, junctions, and other addressable graph elements MUST be unique project-wide unless a future schema explicitly introduces typed composite identity; Studio MUST NOT rely on visible labels for identity.

FDA-642 Profile-defined/dynamic port IDs MUST be stable within their owning node; canonical references to a port MUST include enough owning-node identity to be unambiguous.

FDA-643 A canonical edge belongs to exactly one diagram and MUST NOT directly span two diagram containers.

FDA-644 Cross-diagram relationships MUST use typed references/subflow/dependency links rather than an edge whose endpoints live in different diagram graphs.

FDA-645 External/source-format IDs MAY be preserved as namespaced provenance metadata but MUST NOT replace Studio canonical IDs.

FDA-646 Import/paste/duplication MUST remap colliding IDs before canonical commit and preserve all affected internal references consistently.

FDA-647 Undo that restores a deleted canonical graph element MUST restore its original stable ID when the history entry represents restoration of the same entity.

## Nested selection normalization

FDA-650 If a selection includes a container/group and one of its descendants, a spatial transform MUST NOT apply twice to the descendant.

FDA-651 Studio MUST normalize or explicitly explain ancestor/descendant multi-selection semantics before move/resize/group operations.

FDA-652 Delete of a selection containing both ancestor and descendant elements MUST produce one deterministic dependency/deletion plan rather than duplicate deletion effects.

FDA-653 Copy/duplicate of ancestor plus descendant MUST include each canonical element once and preserve containment relationships.

FDA-654 Batch inspector edits MAY intentionally target both ancestor and descendant only when the property is legal for each target and the command semantics are unambiguous.

## Long-running computation and stale results

FDA-660 Auto-layout, expensive routing, import transformation, and whole-graph analysis MUST operate against an identified canonical project/diagram revision.

FDA-661 A completed asynchronous computation MUST NOT overwrite newer canonical edits silently.

FDA-662 If canonical input changed while a computation was running, Studio MUST reject the stale result, recompute, or present an explicit rebase/review path.

FDA-663 Auto-layout preview acceptance MUST validate that referenced node/edge identities and applicable constraints still match the revision assumptions used to compute the preview.

FDA-664 Cancellation MUST leave the last committed canonical state intact and MUST invalidate late-arriving cancelled results.

FDA-665 Progress reporting MUST distinguish computation in progress from canonical project mutation/save progress.

## Data-bound and derived diagrams

FDA-670 Future data-bound diagrams MUST explicitly identify the external/source model, source version/revision when available, and whether the diagram is derived/read-only, refreshable with local overrides, or materialized as independent authored graph data.

FDA-671 Studio MUST NOT silently overwrite manual authored graph changes during a data-source refresh.

FDA-672 Refresh MUST compute a semantic change set and surface conflicts between source changes and local authored overrides before commit.

FDA-673 A derived/read-only graph MUST retain its external authority boundary; visual edits that cannot map to legal source changes MUST remain view-only or be rejected.

FDA-674 Materializing a derived graph into authored Flow content MUST be an explicit command that creates ordinary canonical graph identities and records source provenance separately.

FDA-675 Loss of access to a data source MUST leave the last known canonical/materialized data inspectable and MUST report freshness/verification as unavailable rather than deleting nodes.

FDA-676 Data-bound status, stale/fresh state, and source authority MUST be explicit and MUST NOT be inferred from visual styling alone.

## Internationalization and bidirectional content

FDA-680 Diagram labels, descriptions, metadata, and annotations MUST support Unicode text.

FDA-681 Diagram text rendering/inspection MUST support bidirectional and right-to-left text to the extent supported by the selected Forma/browser presentation contract.

FDA-682 Stable node/edge/profile identities MUST remain independent of translated/localized visible labels.

FDA-683 Locale changes MUST NOT silently rewrite canonical graph topology or geometry.

FDA-684 If localized text no longer fits authored node geometry, Studio SHOULD surface a clipping/legibility finding and MAY offer an explicit reflow command rather than resizing silently.

FDA-685 Agent/developer export MUST preserve canonical identifiers alongside localized visible content so implementations do not infer identity from translated labels.

## Legends and semantic keying

FDA-690 Typed diagram profiles SHOULD be able to provide a generated legend/key explaining node/relationship kinds used in the current diagram.

FDA-691 A generated legend MUST derive from explicit profile semantics and presentation mappings rather than reverse-engineering shapes/colors from rendered output.

FDA-692 Legends MUST expose textual names/descriptions for semantic kinds; color/shape samples MAY supplement but MUST NOT be the only explanation.

FDA-693 An authored custom legend, if supported, MUST remain distinguishable from the profile-generated semantic key so it cannot silently redefine profile meaning.


## Read-only review and deep linking

FDA-700 Studio MUST support a read-only/review mode for diagrams that permits pan, zoom, search, selection, inspection, validation review, and relationship traversal without exposing mutating capabilities.

FDA-701 Read-only mode MUST be enforced by capability availability in the editor model rather than by merely hiding buttons while mutation commands remain callable.

FDA-702 Review mode SHOULD support linking to a specific diagram and stable element ID for collaboration/review workflows without encoding fragile screen coordinates.

FDA-703 A deep link to an unavailable/deleted element MUST open the owning diagram when possible and report the missing target explicitly rather than silently selecting something else.

FDA-704 Shared review links MUST NOT embed secrets, repository tokens, or sensitive fixture data in URLs.

FDA-705 View bookmarks MAY persist pan/zoom/selected-element presentation for review, but they MUST remain separate from graph semantics and MUST not affect ordinary canonical export unless explicitly included as authored presentation metadata.

FDA-706 Full-screen/presentation mode MAY hide editor chrome, but must preserve nonvisual structure/search/navigation and must not change canonical graph state.

## Filters and focus/isolation

FDA-710 Flow SHOULD support non-destructive view filters for large diagrams based on element kind, relationship kind, lane/group, validation state, tags/metadata, and text search where those data exist.

FDA-711 View filters MUST NOT delete, reparent, disconnect, or otherwise mutate hidden canonical elements.

FDA-712 Filter/isolation state SHOULD remain editor/view state by default.

FDA-713 If an authored presentation intentionally persists a filter, that distinction MUST be explicit and independently reviewable from canonical graph topology.

FDA-714 Validation MUST not treat temporarily filtered-out canonical elements as absent.

FDA-715 Fit-to-content/selection operations MUST define whether filtered elements participate and make that behavior predictable.

## Source-backed graph integration

FDA-720 Studio MAY expose authoritative external models such as page navigation, Ordo/state definitions, repository/service inventories, or other registered sources as derived graph views using the shared Flow infrastructure.

FDA-721 Source-backed graph views MUST clearly identify which fields/relationships are authoritative externally versus authored locally.

FDA-722 If a source-backed view allows mutation, each supported mutation MUST map to an explicit legal command/action against the authoritative source adapter; unsupported visual edits remain unavailable.

FDA-723 A comparison between an authored diagram and an authoritative source MAY report drift, missing elements, extra elements, or changed relationships, but MUST distinguish evidence-backed differences from unknown/unavailable verification.

FDA-724 Studio MUST NOT silently convert an authored architecture/workflow diagram into authoritative runtime truth because an external adapter later becomes available.

FDA-725 Materializing a source-backed graph into an authored diagram MUST preserve source provenance while severing live authority unless an explicit refreshable-data-bound mode is selected.
