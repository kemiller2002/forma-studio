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


## Cross-functional phases

FDA-730 Workflow/swimlane profiles MAY support ordered phases/milestones as a semantic organization axis distinct from responsibility lanes.

FDA-731 Phase membership MUST be explicit and MUST NOT be inferred solely from x/y position.

FDA-732 A workflow activity MAY belong to one responsibility lane and one phase when the selected profile defines that cross-functional grid model.

FDA-733 Moving an activity across a phase boundary MUST surface the resulting phase change just as lane movement surfaces responsibility change.

FDA-734 Lane order and phase order MUST be independently editable without rewriting activity identity.

FDA-735 Deleting/reordering a phase MUST preserve or explicitly reconcile activity phase membership; it MUST NOT silently discard activities.

FDA-736 Phase labels/orientation are presentation properties; phase identity and order are canonical semantic data when the profile uses phases.

## Quick create-and-connect

FDA-740 Flow MAY offer a quick create-and-connect affordance from a selected node/port.

FDA-741 Quick create-and-connect MUST show only element kinds and target ports legal under the active profile/connection rules.

FDA-742 Creating a new node plus connecting edge through one gesture/action MUST commit atomically as one logical command or command transaction.

FDA-743 If either node creation or connection is illegal, the atomic quick-connect operation MUST leave canonical graph state unchanged.

FDA-744 The same quick-connect capability MUST be invokable without hover/pointer precision through keyboard/touch/command UI where exposed.

FDA-745 Quick-connect is an input convenience only; it MUST produce the same canonical node/edge data as separate AddNode plus Connect commands.

## Subprocess extraction

FDA-750 Studio SHOULD support extracting a selected connected workflow subgraph into a new child/referenced diagram after the basic Flow editor is stable.

FDA-751 Extract-to-subflow MUST identify which selected internal edges move with the subgraph and which boundary relationships cross between retained and extracted elements.

FDA-752 Extraction MUST create a new stable diagram ID and preserve selected element IDs where moving the actual elements rather than copying them.

FDA-753 The source diagram MUST receive an explicit subflow/subprocess reference node or equivalent profile construct only when the profile supports it.

FDA-754 Boundary relationships MUST be rewired through explicit legal subflow interface/reference semantics; Studio MUST NOT invent hidden cross-diagram edges.

FDA-755 Extraction MUST be previewable/reviewable and undoable as one logical project operation.

FDA-756 Extract-to-subflow MUST preserve lane/phase/profile semantics where legal and surface obligations where the target diagram requires a different containment structure.

## Extended layer behavior

FDA-760 If named layers ship, layer contracts MAY support authored visibility, editing lock, print/export inclusion, snapping participation, connector-glue participation, and active-default assignment for newly created elements.

FDA-761 Layer properties MUST be independently modeled; "locked", "hidden", "not printed", "not snappable", and "not glueable" MUST NOT be collapsed into one disabled state.

FDA-762 An active layer MAY automatically assign newly created eligible elements to that layer, but assignment MUST be visible/undoable and must not override profile-required semantic containment.

FDA-763 Layer-level print/export exclusion MUST be explicit authored presentation behavior and MUST be reported in export/review metadata.

FDA-764 Layer-level snap/glue settings affect editor interaction only and MUST NOT change graph topology after an edge is already canonically connected.

FDA-765 Layer color/highlight overrides, if added, MUST remain presentation/review aids and MUST NOT become the sole representation of semantic meaning.

FDA-766 A layer MAY contain elements from multiple semantic groups/containers because layer membership is orthogonal organization; this MUST NOT imply those elements share one semantic owner.

## Background/master diagram content

FDA-770 Repeated background/border/title/watermark-like diagram content MAY be modeled through reusable canonical diagram fragments or background-layer references rather than a separate opaque drawing-page subsystem.

FDA-771 Background/master content MUST remain separately editable at its definition/source and MUST not be flattened silently into every consuming diagram.

FDA-772 A diagram referencing reusable background content MUST retain stable definition identity plus local placement/visibility parameters allowed by the reusable-fragment contract.

FDA-773 Background presentation MUST not obscure or replace graph semantics in Structure/Outline/agent export.

FDA-774 Print/export MUST resolve background/master content deterministically and identify missing definitions as blocking/unavailable dependencies.

## Data fields and data decorations

FDA-780 Diagram elements MAY expose typed data fields through profile/custom-metadata contracts independently of their visible label.

FDA-781 A future data-decoration/data-graphics capability MAY project data fields as badges, icons, bars, text callouts, status markers, or other public presentation primitives.

FDA-782 Data decorations MUST be projections of explicit data values and presentation rules; they MUST NOT become the authoritative storage location for the underlying data.

FDA-783 Data decorations MUST not rely on color alone and MUST provide accessible textual/structural equivalents for meaningful values.

FDA-784 Data-decoration rules MUST be deterministic, typed/versioned, and inspectable; arbitrary executable expressions are prohibited unless a separately governed expression/runtime contract exists.

FDA-785 Changing or removing a data decoration MUST NOT alter the underlying graph topology or source data.

FDA-786 Source-backed data decorations MUST preserve source/freshness authority according to the data-bound diagram requirements.

FDA-787 Agent/developer export MUST expose underlying typed values and decoration rules rather than forcing consumers to infer status from rendered icons/colors.

## Diagram feature-parity boundary

FDA-790 Studio MAY learn from mature diagramming systems, but external product feature parity is not itself a requirement.

FDA-791 New diagram capabilities MUST be justified by Forma Studio's typed specification, review, handoff, workflow, or engineering goals rather than added solely because another editor exposes them.

FDA-792 Imported Visio-style constructs with no canonical Studio meaning MUST remain explicit adapter gaps/lossy mappings rather than forcing one-off core fields.


## Object metadata

FDA-800 Diagrams and addressable Flow objects MUST be able to carry descriptive metadata independently from their visible label and presentation.

FDA-801 Metadata MAY be attached to diagrams, nodes, edges, groups/containers, lanes, phases, junctions, annotations, and other profile-defined addressable graph objects where the profile permits it.

FDA-802 Metadata MUST remain separate from stable identity, semantic element/relationship kind, geometry, presentation appearance, and graph topology.

FDA-803 Metadata keys MUST be namespaced or otherwise collision-safe so profile, Studio core, external adapter, and consumer-defined fields can coexist.

FDA-804 Metadata schemas MAY be declared by a diagram profile or extension package, but Studio MUST preserve unknown forward-compatible fields when safe.

FDA-805 Studio MUST support typed metadata values sufficient for common descriptive use, including text, number, boolean, enum/token, date/time, URL/reference, repeated tags/list values, and namespaced structured values where explicitly supported.

FDA-806 Studio MUST NOT infer missing metadata values from object color, position, icon, type label, neighboring objects, or external context unless an explicit import/mapping contract authorizes that inference.

FDA-807 Metadata MUST NOT become authorization, transition legality, scoring, workflow execution state, approval, or other consequential domain truth merely because it is present in the Studio document.

FDA-808 A profile MAY designate metadata fields such as owner, role, phase, status, category, source, provenance, external ID, tags, description, revision, or custom attributes.

FDA-809 Studio MUST distinguish descriptive metadata from provenance/evidence references when the profile/model distinguishes them.

FDA-810 Studio MUST distinguish metadata from accessibility fields such as accessible name/description.

FDA-811 Studio MUST distinguish metadata from authored presentation fields such as fill, stroke, accent, text color, icon, shape, line style, and marker.

FDA-812 The inspector MUST expose object metadata separately from semantic properties and appearance/presentation properties.

FDA-813 Metadata edits MUST be typed canonical commands and participate in undo/redo, persistence, semantic diff, collaboration conflict detection, copy/paste, duplication, and agent export.

FDA-814 Metadata keys/values MUST be searchable through project/diagram search when the field's visibility policy permits search.

FDA-815 Metadata values MAY participate in filters, legends, data decorations, or color mappings only through an explicit mapping/filter contract.

FDA-816 Deleting or changing a metadata field definition in a profile migration MUST identify affected object values and preserve or explicitly migrate/drop them according to the migration contract.

FDA-817 Studio SHOULD support compact metadata summaries and expanded metadata inspection without requiring every field to appear on the node canvas.

FDA-818 Metadata rendering on the canvas MUST NOT be required for metadata to remain machine-readable in the canonical project.

FDA-819 Studio MUST preserve Unicode and bidirectional metadata values.

## Metadata visibility and sensitivity

FDA-820 A metadata field MAY declare a visibility/transport policy such as editor-only, rendered, agent/developer-export, provenance/export, or profile-defined scope.

FDA-821 Unknown metadata MUST default to the safest non-rendered/non-exported behavior compatible with lossless project preservation.

FDA-822 Studio MUST NOT encourage credentials, access tokens, private keys, passwords, or other secret material in object metadata.

FDA-823 Metadata marked source-only/editor-only MUST NOT leak into generated visible markup, rendered diagram output, PDF/SVG metadata, accessibility-only text, diagnostics, URLs, or agent packets unless the applicable export contract explicitly authorizes it.

FDA-824 Metadata visibility changes MUST NOT alter object identity, topology, semantic type, or authored color.

FDA-825 Search, filters, developer inspection, and export MUST respect metadata visibility/sensitivity policy.

## Authored object color

FDA-830 Workflow and diagram objects MUST support explicit authored color independent from semantic status/type.

FDA-831 Eligible nodes/objects MUST support authored fill/background presentation.

FDA-832 Eligible nodes/objects MUST support authored border/stroke presentation.

FDA-833 Eligible nodes/objects MUST support an authored accent presentation distinct from full fill.

FDA-834 Eligible edges/connectors MUST support authored stroke/accent presentation.

FDA-835 Eligible text/labels MAY support authored foreground color when the active presentation contract can maintain accessibility.

FDA-836 Studio MUST support Forma token references as a preferred color representation.

FDA-837 Studio SHOULD support project/profile palette-slot references for reusable authored colors.

FDA-838 Studio MAY support literal authored CSS color values where the active profile/project permits them.

FDA-839 The canonical color representation MUST distinguish fixed literal color from theme-relative token/palette references.

FDA-840 A theme-relative token/palette color MAY resolve differently across themes; a fixed literal color MUST remain fixed unless the user explicitly edits it.

FDA-841 Changing object color MUST NOT change semantic node/edge kind, metadata status, workflow legality, lane/phase ownership, or graph topology.

FDA-842 Studio MUST NOT infer Error from red, Approved from green, Warning from yellow, Selected from blue, or any other semantic meaning from authored color.

FDA-843 Two objects with identical semantic type/status MAY use different authored colors.

FDA-844 Two objects with different semantic types/statuses MAY use the same authored color.

FDA-845 Selection/focus/validation/disabled editor cues MUST remain independently visible regardless of authored object color.

FDA-846 Color edits MUST be typed canonical commands and participate in undo/redo, diff, copy/paste, duplication, collaboration merge, persistence, and agent export.

## Project palettes and color mapping

FDA-850 A project MAY define named diagram/workflow palette slots separate from Forma's canonical design-token namespace.

FDA-851 Palette slots MUST have stable names/IDs and explicit resolved values or Forma token references.

FDA-852 Palette slot changes MUST update objects that reference the slot without rewriting each object's semantic metadata.

FDA-853 A palette slot MUST NOT imply semantic meaning unless the project/profile explicitly declares that mapping.

FDA-854 Studio MAY provide an explicit metadata-to-color mapping such as status -> palette slot.

FDA-855 Metadata-to-color mappings MUST be persisted as explicit inspectable project/profile rules and MUST NOT be inferred from current object colors.

FDA-856 An object MAY override a metadata-driven mapped color with an explicit authored color only when the active mapping contract permits override; precedence MUST be deterministic and inspectable.

FDA-857 Mapping rules MUST define behavior for missing/unknown metadata values without guessing.

FDA-858 Mapping rules MUST NOT execute arbitrary JavaScript or ungoverned expressions.

FDA-859 A color mapping change MUST be reviewable as a presentation-rule change distinct from underlying metadata-value changes.

FDA-860 Legends generated from metadata/color mappings MUST derive from the explicit mapping and MUST not become the semantic authority.

## Color accessibility and validation

FDA-870 Studio MUST validate meaningful use of color against the rule that color cannot be the sole semantic carrier.

FDA-871 When a metadata/status category is visualized by color, Studio SHOULD require or recommend a textual, icon, shape, line-style, marker, label, pattern, grouping, or other non-color cue according to the profile.

FDA-872 Studio SHOULD validate text/icon contrast against authored fills when reliable.

FDA-873 Studio MUST allow preview/review of Flow diagrams in grayscale.

FDA-874 Studio MUST support a backgrounds-disabled/print-safe review projection when Folio/print export is in scope.

FDA-875 Studio SHOULD provide color-vision simulation as a review aid consistent with Visual Engineering guidance, without claiming simulation proves accessibility.

FDA-876 Forced-colors/high-contrast mode MUST preserve object/connector distinction and text/relationship meaning through Forma presentation fallbacks.

FDA-877 A color change that makes required content detectably illegible MUST produce a validation/review finding.

FDA-878 Color/palette validation findings MUST identify the affected stable object/mapping ID.

## Metadata and color acceptance

FDA-890 A canonical Workflow example MUST include at least five nodes with descriptive metadata, including at least owner/role, phase/category or tags, status text, and one external/reference field.

FDA-891 The Workflow example MUST include user-authored node colors that are intentionally not equivalent to workflow semantic status.

FDA-892 The example MUST demonstrate an explicit metadata-to-color mapping separately from manual authored colors.

FDA-893 Agent export MUST make metadata values, color source (token/palette/literal/mapping), and semantic type/status separately readable without screenshot interpretation.

FDA-894 Semantic diff MUST distinguish metadata changes, semantic type changes, manual color changes, palette changes, and metadata-to-color mapping changes.

FDA-895 Copy/paste/duplication MUST preserve metadata and color references while remapping only identities/references that require remapping.

FDA-896 Print/export through Folio MUST preserve eligible authored color while remaining understandable in grayscale/backgrounds-disabled output.


## Metadata schema validation and documentation

FDA-900 A metadata schema field MUST have a stable field key independent of its localized/user-facing label.

FDA-901 A metadata schema MAY define required/optional status, value type, cardinality, default value, allowed values, numeric range, string length/pattern constraints, URL/reference constraints, and profile-specific validation rules.

FDA-902 Metadata defaults MUST be distinguishable from explicitly authored values so a later default change does not silently rewrite explicit user data.

FDA-903 Required metadata MUST be validated explicitly; a missing required field MUST NOT be silently filled from color, geometry, label text, neighboring objects, or unrelated metadata.

FDA-904 Enum/token metadata MUST persist a stable value identifier separately from its localized display label.

FDA-905 Metadata field definitions SHOULD provide a human-readable description/help text and MAY link to profile/domain documentation.

FDA-906 Metadata schema display labels/descriptions MAY be localized, but stable field/value IDs MUST remain locale independent.

FDA-907 Metadata cardinality MUST distinguish scalar, set-like repeated values, and ordered repeated values where order is meaningful.

FDA-908 Canonical serialization MUST normalize metadata values according to their declared type while preserving semantically significant distinctions such as timezone/offset where the profile requires them.

FDA-909 Invalid metadata MUST produce field-level findings tied to the stable object and metadata field key.

FDA-910 A metadata schema change that tightens constraints MUST surface now-invalid existing values before migration/upgrade commit.

## Derived, inherited, and source-bound metadata

FDA-920 Studio MUST distinguish explicitly authored metadata from metadata derived from structural/profile context or an external source.

FDA-921 A profile MAY derive metadata from explicit canonical relationships such as lane/phase/container membership, but the derivation rule MUST be explicit and deterministic.

FDA-922 Studio SHOULD avoid denormalized duplication when a metadata value can be derived losslessly from an authoritative relationship; if both are stored, precedence and conflict validation MUST be explicit.

FDA-923 If lane membership authoritatively determines an owner/role field, a contradictory explicit owner value MUST produce a conflict or follow the profile's documented override rule rather than silently diverging.

FDA-924 Derived metadata MUST identify its source rule/reference so inspector, export, and agents can distinguish derived from authored values.

FDA-925 Externally source-bound metadata MUST identify source authority/freshness where available and MUST distinguish unavailable/stale/unknown from an authored empty value.

FDA-926 Editing source-bound metadata MUST either map to an explicit source mutation capability, create an explicit local override where permitted, or be unavailable.

FDA-927 Refreshing source-bound metadata MUST not overwrite permitted local overrides silently.

FDA-928 Materializing a derived/source-bound metadata value into an authored value MUST be an explicit command and MUST preserve provenance when applicable.

## Reusable appearance styles

FDA-930 Studio SHOULD support named reusable diagram appearance styles so repeated presentation is not duplicated as raw properties across every object.

FDA-931 A named appearance style MUST have a stable style ID, name, version/revision or equivalent change identity, supported target kinds, and typed appearance properties.

FDA-932 Appearance styles MAY include fill, stroke, accent, foreground, stroke width, line/dash style, marker treatment, shape/presentation variant, icon treatment, pattern/hatch, typography emphasis, and other public Forma-supported presentation properties.

FDA-933 An object MAY reference one named appearance style plus explicit legal per-object overrides.

FDA-934 Style reference, style definition, resolved appearance, and per-object overrides MUST remain distinguishable in canonical data and developer/agent inspection.

FDA-935 Updating a named style MUST propagate to referencing objects by reference without rewriting each object into duplicated resolved properties.

FDA-936 A style-definition edit MUST be one logical undoable project command and semantic diff SHOULD report the definition change separately from every affected object's resolved rendering change.

FDA-937 Removing a style that remains referenced MUST block, reassign, or detach/materialize through an explicit reviewed command; Studio MUST NOT silently drop appearance.

FDA-938 Detaching an object from a named style MUST materialize the effective legal appearance explicitly and be undoable.

FDA-939 Reset-to-style/default MUST remove eligible local overrides without changing semantic type, metadata, identity, or topology.

FDA-940 Style inheritance, if supported, MUST be acyclic and deterministic; cycles MUST be rejected.

FDA-941 Style definitions MAY reference Forma tokens and project palette slots but MUST NOT embed executable styling logic.

FDA-942 Unknown/unavailable style definitions MUST preserve references/overrides and surface an unavailable-presentation finding rather than substituting an unrelated style silently.

FDA-943 Shared/reusable style libraries MAY be introduced later only through explicit version/pinning/update-review contracts.

## Shape and icon presentation independence

FDA-950 Semantic node kind and visual shape/presentation variant MUST be independent concepts unless a profile explicitly constrains them.

FDA-951 General diagrams MUST support changing an eligible node's visual shape without replacing its stable node identity.

FDA-952 Changing visual shape MUST preserve attached edges, metadata, references, annotations, and semantic properties unless the new presentation contract makes a property illegal and Studio surfaces the required reconciliation.

FDA-953 Typed profiles MAY restrict legal shapes/presentation variants for a semantic kind, but the restriction MUST be explicit profile data.

FDA-954 Forma presentation identifiers for shape variants MUST be stored separately from semantic element-kind identifiers.

FDA-955 Icons/symbols inside nodes MUST reference public Forma icons or project assets by stable identifiers rather than embedding identity into color or arbitrary CSS.

FDA-956 Meaningful icons MUST have an accessible textual equivalent and MUST NOT be the sole representation of semantic type/status.

FDA-957 User-authored arbitrary vector path editing remains outside the initial Flow scope; custom reusable shapes require a separate governed presentation/import contract.

FDA-958 Shape swapping MUST use canonical commands and participate in undo/redo, diff, copy/paste, templates, and agent export.

## Appearance normalization and palette modes

FDA-960 Literal color values MUST be validated against a documented accepted color syntax and serialized deterministically to avoid meaningless diffs.

FDA-961 Studio MUST preserve whether a color came from a literal, Forma token, palette slot, named style, or explicit metadata/presentation mapping rather than storing only a resolved pixel color.

FDA-962 Project palette slots MAY define theme/output-mode variants such as light, dark, high-contrast-compatible, color-print, or grayscale-print values where the presentation contract supports them.

FDA-963 Missing palette/style/token references MUST produce explicit findings and MUST NOT resolve to a misleading arbitrary color.

FDA-964 Palette/style definitions MUST have deterministic canonical ordering/serialization independent of inspector sorting or recent-use UI.

FDA-965 Tags/metadata collections whose semantics are set-like SHOULD serialize deterministically; ordered collections MUST preserve authored order explicitly.

FDA-966 A palette rename MUST preserve stable palette-slot identity and object references.

FDA-967 A palette deletion with references MUST block or require explicit reassignment/materialization.

FDA-968 Palette/style changes MUST be previewable when they affect many objects and SHOULD report the affected object count.

## Color and appearance editing UI

FDA-970 The inspector MUST provide keyboard-operable, non-spatial editing for authored color/appearance.

FDA-971 Literal color editing MUST provide a text/value entry path in addition to any 2D color picker or eyedropper-style interaction.

FDA-972 Token, palette, and named-style choices SHOULD be searchable/browsable and expose human-readable descriptions where supplied by Forma/project definitions.

FDA-973 Multi-selection appearance editing MUST follow the shared mixed-value/atomic batch-edit rules and MUST NOT silently overwrite incompatible target properties.

FDA-974 Studio SHOULD support explicit reset-to-default/style for appearance fields.

FDA-975 Studio MUST distinguish a visually similar resolved color from the same token/palette/style identity; selecting a raw matching color MUST NOT silently bind the object to an unrelated token.

FDA-976 Automatic contrast assistance MAY suggest a foreground or alternate presentation, but changing a fixed authored color MUST require an explicit user/command decision.

## Structured metadata queries and bulk operations

FDA-980 Studio SHOULD support structured filtering/querying by metadata field/value, object kind, profile kind, style, palette slot, validation state, lane/phase, and tag where those concepts exist.

FDA-981 Query/filter results MUST operate on canonical stable identities and MUST remain independent of canvas viewport/culling.

FDA-982 A user SHOULD be able to create a selection from query/filter results for legal bulk editing.

FDA-983 Bulk metadata edits MUST validate field availability/type/constraints across the selected objects before mutation and use atomic batch-command semantics.

FDA-984 Bulk clear/remove operations MUST distinguish removing an explicit value from restoring/using a schema default or derived value.

FDA-985 Search/filter/query over sensitive or non-exportable metadata MUST respect the current user's/application's allowed Studio capability and metadata visibility policy.

## Tabular metadata interchange

FDA-990 Studio MAY support CSV/TSV or equivalent tabular metadata import/export as a metadata adapter after the canonical metadata model stabilizes.

FDA-991 Tabular import MUST map rows to canonical objects through explicit stable keys or reviewed mapping rules; matching by visible label alone MUST NOT silently decide identity.

FDA-992 Tabular import MUST validate field schemas, types, allowed values, references, and visibility policies before canonical commit.

FDA-993 Tabular import MUST preview additions/changes/removals/conflicts when the operation can modify existing objects.

FDA-994 Tabular import MUST NOT create/delete graph topology unless the selected adapter explicitly supports and previews topology operations.

FDA-995 Tabular export MUST distinguish canonical IDs, stable metadata keys/values, localized display labels, and derived/source-bound values where applicable.

FDA-996 Spreadsheet/tabular round-trip MUST NOT turn unknown/unavailable values into empty strings or zero silently.

## Collaborative session state and review threads

FDA-1000 Future real-time collaboration presence, remote cursors, remote selections, and viewport positions MUST be ephemeral session/view state and MUST NOT dirty canonical project data.

FDA-1001 An authored lock flag used to prevent accidental editing MUST remain distinct from collaborative edit leases/permissions.

FDA-1002 If collaborative edit leases are introduced, they MUST be coordination state and MUST NOT imply application/domain authorization.

FDA-1003 Diagram review comments/threads SHOULD have stable identities and explicit open/resolved state if collaborative review is implemented.

FDA-1004 A resolved review comment MUST remain distinguishable from deleting the comment/history where retention is supported.

FDA-1005 Review-thread targets MUST reference stable diagram/object IDs or explicit spatial anchors and MUST survive ordinary movement of a targeted object.

FDA-1006 Review author/timestamp/presence data MUST follow the product's privacy/data-retention policy rather than becoming unrestricted graph metadata.

## Vector and link safety

FDA-1010 Imported SVG/vector content MUST be treated as untrusted input.

FDA-1011 SVG/vector import or asset handling MUST reject or neutralize executable script, event handlers, unsafe external-resource loading, and dangerous URL schemes before rendering/export where Studio owns that boundary.

FDA-1012 Object metadata/reference URLs MUST use an explicit allowed-scheme/security policy and MUST NOT become executable HTML/JavaScript through rendering.

FDA-1013 Rich metadata text MUST be rendered as text/approved semantic markup rather than unsanitized HTML supplied by metadata values.

FDA-1014 Exported SVG/HTML MUST not reintroduce editor-only secrets/source-only metadata or unsafe imported executable content.

## Agent operations over metadata and appearance

FDA-1020 Agents MAY propose metadata, palette, named-style, shape/presentation, and appearance changes only through the same typed command surface as human editor actions.

FDA-1021 An agent MUST NOT infer semantic status/type from authored color when proposing or explaining a change.

FDA-1022 Agent change plans SHOULD distinguish semantic changes, metadata changes, style-definition changes, palette changes, and resolved visual consequences.

FDA-1023 A broad style/palette/metadata-schema change SHOULD report its expected affected-object set/count before commit when determinable.

FDA-1024 Agent export MUST expose stable metadata field IDs, authored/derived/source-bound status, style references/overrides, palette/token identities, and resolved presentation separately where applicable.


## Typed object references and attachments

FDA-1030 Any addressable diagram object MAY carry typed references to registered project/external resources when the active profile permits them.

FDA-1031 Typed references MUST remain distinct from arbitrary descriptive URL/text metadata because references participate in navigation, dependency checks, deletion impact, and referential validation.

FDA-1032 Reference kinds MAY include requirement, page, diagram, diagram element, project asset, document/artifact, repository, issue/work item, source record, external URL, or future registered resource kinds.

FDA-1033 Internal project references MUST use stable canonical IDs rather than visible names/paths alone.

FDA-1034 External references MUST preserve a stable external identity/key where available separately from a human-readable label/URL.

FDA-1035 Deleting or changing an internally referenced target MUST surface inbound references before commit.

FDA-1036 A broken/unavailable external reference MUST remain distinguishable from a deleted internal target and from an ordinary metadata string.

FDA-1037 Copy/paste/duplication/import MUST define whether references remain external, remap to duplicated internal targets, or become unresolved obligations.

FDA-1038 Project asset attachments MUST use the existing asset identity/security/accessibility contracts rather than embedding arbitrary opaque file blobs into graph objects.

FDA-1039 Agent/developer export MUST expose reference kind, target identity, availability/verification state where known, and display label separately.

## Snapping and glue behavior

FDA-1040 Flow snapping MUST distinguish supported snap targets such as grid, authored guides, node/container edges, centers/baselines, alignment relationships, ports/connection anchors, and profile-specific targets where implemented.

FDA-1041 Studio MUST make the active snap target/cue visible during direct manipulation when ambiguity would otherwise make the resulting position surprising.

FDA-1042 Snap priority/tie-breaking MUST be deterministic for equivalent geometry and settings.

FDA-1043 Users MUST have a discoverable way to temporarily bypass optional snapping during precision movement without changing canonical project settings.

FDA-1044 Port/connector glue behavior MUST remain distinct from ordinary geometry snapping: a canonically connected edge remains connected even when snapping is disabled.

FDA-1045 Snap-to-object/guide behavior MUST NOT create hidden grouping, containment, alignment constraints, or graph relationships after the gesture commits.

FDA-1046 Persisted authored guides, if supported, MUST have stable identity; user-only temporary guides remain view/editor state.

## Local undo-history persistence boundary

FDA-1050 Canonical project data MUST NOT depend on serialized undo/redo stacks.

FDA-1051 The v1 local undo/redo history MAY be session-local; if it is not restored after reopen/reload, Studio MUST NOT imply that reopening preserves the previous interactive undo stack.

FDA-1052 Git/project revision history and local command undo history MUST remain separate concepts and UI surfaces.

FDA-1053 A future persisted recovery journal MAY restore undo-capable commands after restart only through an explicit versioned recovery/history format separate from the canonical project document.

FDA-1054 Failure to restore an optional local history/recovery journal MUST leave the last valid canonical project readable.

FDA-1055 Reverting a durable Git/project revision MUST create/produce ordinary canonical state/history according to persistence rules and MUST NOT masquerade as a local single-command undo.


## User-defined metadata fields

FDA-2000 The General diagram profile MUST allow users to define descriptive custom metadata fields for diagram objects without requiring a new compiled Studio profile.

FDA-2001 User-defined metadata SHOULD be represented by project-scoped/versioned field definitions rather than unrelated per-object free-text keys whenever the same field is intended for reuse.

FDA-2002 Creating a reusable custom field definition MUST assign a stable collision-safe field ID/key, value type, display label, and visibility policy; optional default/allowed-values/constraints MAY also be supplied.

FDA-2003 A user MAY add a one-off custom field only through a contract that still gives the field a stable namespaced identity and explicit type; Studio MUST NOT persist ambiguous untyped arbitrary JSON blobs as ordinary metadata.

FDA-2004 Schema-defined profile fields and user-defined project fields MUST be visibly distinguishable in the inspector.

FDA-2005 Renaming a user-facing field label MUST NOT change the stable field key/ID.

FDA-2006 Changing a custom field's stable key/type/cardinality is a schema migration and MUST preview affected values before commit.

FDA-2007 Deleting a custom field definition with values MUST surface affected objects and require explicit removal/migration; Studio MUST NOT silently orphan invisible values.

FDA-2008 Custom field definitions MAY declare which object/profile kinds they apply to.

FDA-2009 Copying objects to another project MUST reconcile custom field-definition identity through explicit reuse/import/remap behavior rather than matching only by display label.

FDA-2010 User-defined metadata fields remain descriptive until a profile/application explicitly binds them to semantic rules; creating a field named "approved" does not create approval authority.

## Container/lane/phase appearance

FDA-2020 Eligible groups, semantic containers, swimlanes, phases, and boundaries MAY carry authored fill, border/stroke, and accent appearance where supported by the active Forma/profile presentation contract.

FDA-2021 Container/lane/phase appearance MUST remain independent of membership/ownership semantics.

FDA-2022 Child object labels, focus, selection, validation, connectors, and non-color semantic cues MUST remain distinguishable over an authored container/lane/phase fill.

FDA-2023 A profile MAY restrict container/lane/phase appearance options for communication/accessibility consistency, but restrictions MUST be explicit profile/presentation capability data.


## Appearance resolution cascade

FDA-1060 Effective diagram appearance MUST resolve through one deterministic precedence model.

FDA-1061 Unless a future schema version explicitly changes the model, precedence from lowest to highest MUST be: public Forma/base defaults -> diagram-profile defaults -> referenced named appearance style -> explicit metadata/data presentation mapping -> explicit per-object overrides -> transient editor-only focus/selection/validation adorners.

FDA-1062 Editor-only adorners MUST never be materialized into canonical authored appearance merely because they render with higher visual precedence.

FDA-1063 A per-object explicit color/appearance override MUST take precedence over a metadata-driven mapping for the overridden property.

FDA-1064 Removing a per-object override MUST reveal the next applicable mapped/style/profile/default value rather than copying the previously resolved value into the object.

FDA-1065 Removing/disabling a metadata mapping MUST reveal the referenced named-style/profile/default appearance without mutating object metadata.

FDA-1066 Changing a named style MUST update effective rendering of references while preserving higher-precedence mappings/overrides.

FDA-1067 Inspector/developer/agent views MUST be able to distinguish effective/resolved appearance from each contributing source layer.

FDA-1068 Semantic diff SHOULD report the source-level change responsible for a broad appearance change rather than emitting misleading per-object semantic changes.

FDA-1069 Appearance resolution MUST be deterministic across save/reload, supported renderers, and agent/human command paths.

## Color representation boundaries

FDA-1070 The first workflow/diagram color implementation MUST support solid fills/strokes/accents before gradients or complex paint servers are considered required.

FDA-1071 Literal color values MUST support a documented deterministic canonical syntax; Studio SHOULD normalize equivalent literal representations on commit.

FDA-1072 Alpha/transparency MAY be introduced only through an explicit presentation capability that defines contrast, overlap, forced-colors, print, SVG/PDF, and backgrounds-disabled behavior.

FDA-1073 Gradients, blend modes, filters, shadows that carry meaning, and arbitrary SVG paint servers are not required for the initial Flow color contract.

FDA-1074 If transparency/gradient features are later added, they MUST NOT become the sole carrier of semantic meaning and MUST have a defined fallback.

FDA-1075 Recent colors, temporary swatches, eyedropper samples, and picker history SHOULD remain per-user/editor preference state unless explicitly saved into a named project palette.

## Measurement units, rulers, and grids

FDA-1080 Canonical Flow geometry MUST remain expressed in the documented logical coordinate unit regardless of the display unit selected in the editor.

FDA-1081 Studio SHOULD support ruler/inspector display in logical units and MAY support physical/user-facing units such as points, inches, millimeters, or centimeters where a bounded sheet/export scale exists.

FDA-1082 Unit conversion MUST be deterministic and MUST NOT rewrite canonical geometry merely because the user changes the displayed measurement unit.

FDA-1083 A bounded/physical diagram MUST define an explicit scale/conversion between canonical logical units and physical output units before actual-size printing can be claimed.

FDA-1084 Grid spacing MAY be a user preference or authored diagram setting; Studio MUST distinguish those cases.

FDA-1085 An authored grid definition MUST declare its logical spacing/origin and optional display-unit intent and MUST remain a non-semantic layout aid.

FDA-1086 Changing grid/ruler display units MUST NOT dirty the canonical project unless an authored physical-scale/grid property changes.

FDA-1087 Inspector position/size entry MUST use the same normalization, min/max, and geometry-validity rules as drag/resize commands.

FDA-1088 Measurement rounding displayed to a user MUST NOT silently truncate higher-precision canonical geometry when no edit is committed.

## Editor names and aliases

FDA-1090 Addressable authored objects MAY have an editor-facing name/alias independent from stable ID and visible rendered label/content.

FDA-1091 Editor names MUST NOT be required to be globally unique unless a specific feature/profile declares a scoped uniqueness rule.

FDA-1092 Renaming an editor alias MUST preserve stable identity, visible label/content, metadata, appearance, topology, and external/internal references.

FDA-1093 Structure/Outline, search, commands, and agent explanations SHOULD prefer a useful editor name/visible label while retaining stable IDs for disambiguation.

FDA-1094 A missing editor name MUST NOT force Studio to mutate visible rendered content merely to create one.

FDA-1095 Importers MAY preserve source object names as aliases/provenance when safe, but source names MUST NOT replace canonical IDs.

## Command/revision provenance

FDA-1100 Studio SHOULD record non-secret provenance for meaningful project revisions/command batches sufficient to distinguish human, agent, migration, import, and automated/refactoring sources where the surrounding governance system provides that identity.

FDA-1101 Change provenance MUST remain separate from object descriptive metadata unless a user/profile explicitly copies selected provenance into authored metadata.

FDA-1102 Recording provenance MUST NOT mutate every touched object's descriptive metadata with timestamps/actor fields by default, because that would create noisy semantic diffs.

FDA-1103 Agent/migration/import provenance SHOULD identify the responsible tool/profile/adapter version where available.

FDA-1104 Provenance records MUST NOT contain secrets, raw credentials, hidden sensitive fixture data, or unrestricted prompt/context dumps.

FDA-1105 Undo/redo MUST operate on canonical commands regardless of provenance source; provenance MUST NOT grant extra command legality.

FDA-1106 Git/Praxis/other repository-governance provenance MAY remain the durable audit authority while Studio preserves only the minimum project-local provenance required for understandable handoff/review.

## Clipboard and selection export interoperability

FDA-1110 Internal copy/paste MUST continue to use the canonical versioned graph-fragment representation.

FDA-1111 Studio MAY additionally place safe convenience representations such as plain text, SVG, or image data on the operating-system/browser clipboard where platform capability permits.

FDA-1112 Convenience clipboard formats MUST NOT replace the canonical graph fragment as the fidelity-preserving Studio-to-Studio copy representation.

FDA-1113 Copy-as-SVG/image MUST exclude editor-only adorners unless the user explicitly requests diagnostic/editor-state capture.

FDA-1114 Studio SHOULD support export of the current selection as SVG/image/structured fragment independently from whole-diagram export.

FDA-1115 Selection export MUST compute deterministic selected-content bounds and MUST define how incident edges to unselected objects are represented or omitted.

FDA-1116 Pasting external SVG/image/text must follow the applicable untrusted-content, asset, profile, and adapter rules and MUST NOT infer graph semantics from pixels.

## Studio-to-Folio diagram projection handoff

FDA-1120 Studio MUST define a versioned diagram-projection handoff suitable for Folio without requiring Folio to interpret or own the complete canonical graph editor model.

FDA-1121 The projection handoff SHOULD include source project/diagram/revision identity, deterministic content bounds, resolved public Forma presentation references/output, meaningful labels, legend/key where requested, selected rendered metadata, accessibility/structured relationship summary, and non-secret appearance provenance needed for reproducible output.

FDA-1122 The projection handoff MUST exclude editor-only selection/hover/guides/minimap/transient routing state unless diagnostic capture is explicitly requested.

FDA-1123 Folio page fitting/tiling MUST operate on the projection/output bounds and MUST NOT mutate Studio canonical node/edge geometry.

FDA-1124 A stale projection whose source diagram revision no longer matches the requested export revision MUST be rejected or explicitly labeled stale rather than silently presented as current.

FDA-1125 Strict export MUST fail when a required public Forma presentation capability or required projection artifact is unavailable rather than asking Folio to invent replacement graph semantics.

FDA-1126 The projection format MAY use SVG/semantic HTML plus a structured sidecar/manifest; no single renderer technology is mandatory as long as canonical semantics and accessibility requirements are preserved.

FDA-1127 The handoff MUST preserve enough stable object/reference identity to support printed indexes/metadata tables and provenance without exposing source-only metadata.


## Project-local metadata field definitions

FDA-1180 Studio MUST allow a project to define reusable project-local metadata fields without requiring a custom external diagram profile/package.

FDA-1181 A project-local metadata field MUST have a stable field ID/key, display name, value type, applicability scope, optional help text, visibility/transport policy, and optional validation/default configuration.

FDA-1182 Applicability MAY target all addressable project objects or an explicit subset such as pages, Layout components, diagrams, nodes, edges, groups, lanes/phases, or specific profile element kinds.

FDA-1183 Creating, renaming, reordering, changing, or deleting a project-local metadata field MUST use typed canonical commands and be undoable.

FDA-1184 Renaming the display name of a field MUST preserve its stable field key and every existing value/reference/query/mapping.

FDA-1185 Changing a field's value type or cardinality when values already exist MUST require deterministic validation/migration and MUST NOT silently coerce incompatible values.

FDA-1186 Deleting a field with existing values, filters, mappings, templates, agent references, or export dependencies MUST surface those uses and require explicit removal/migration.

FDA-1187 Studio SHOULD allow a field to be promoted from a one-off/project-local definition to a reusable profile/library definition only through an explicit migration/mapping process.

FDA-1188 General diagrams MUST be able to use project-local metadata fields even when no typed Workflow/State/Architecture profile is active.

FDA-1189 The Metadata inspector SHOULD let a user add values to an object by choosing from applicable registered fields rather than forcing arbitrary raw JSON editing.

FDA-1190 Studio MAY support an advanced raw/structured metadata inspection view for debugging, but it MUST NOT be the only editing surface for ordinary metadata.

## Project palette management

FDA-1200 Studio MUST provide project-level management for named palette slots when project palettes are used.

FDA-1201 A palette slot MUST have stable identity, display name, and a color source/value consistent with the canonical color model.

FDA-1202 Users MUST be able to create, rename, reorder, edit, and delete palette slots through typed commands.

FDA-1203 Renaming a palette slot MUST preserve references through stable identity.

FDA-1204 Deleting an in-use palette slot MUST block or require explicit reassignment/materialization for all references.

FDA-1205 Palette editing SHOULD expose swatch preview plus textual color/token value and applicable accessibility/contrast findings.

FDA-1206 Recent colors and personal picker history MUST remain editor preference state; converting one into a reusable project palette slot requires an explicit command.

FDA-1207 Studio SHOULD provide a project palette view showing usage counts/affected objects to make broad color changes reviewable.

FDA-1208 A palette slot MAY define output/theme variants only through an explicit versioned property model; missing variants follow documented fallback rather than guessing.

## Declarative metadata-to-appearance mappings

FDA-1210 The initial metadata-to-appearance system MUST use a small declarative mapping model rather than arbitrary scripting.

FDA-1211 A mapping MUST identify a source metadata field, applicable object scope, match rule, target appearance property or named style, and fallback behavior.

FDA-1212 The initial mapping model SHOULD prioritize categorical exact-match/set-membership mappings suitable for status, category, phase, owner, or tags before introducing general expression languages.

FDA-1213 Numeric ranges, compound predicates, and formulas MAY be introduced only through a separately versioned declarative rule contract with deterministic evaluation.

FDA-1214 Mapping evaluation order/precedence MUST be deterministic; conflicting rules affecting the same property MUST either have explicit precedence or be rejected as ambiguous.

FDA-1215 A mapping MUST define behavior for missing, unknown, unavailable, invalid, and unmapped metadata values.

FDA-1216 Mapping a metadata value to a named style or palette slot MUST preserve the stable style/palette identity, not just copy the resolved color.

FDA-1217 Editing a mapping MUST preview or report the affected object set/count when determinable before a broad change is committed.

FDA-1218 Mapping rules MUST participate in undo/redo, semantic diff/merge, copy/project duplication, schema migration, agent export, and validation.

FDA-1219 Deleting/renaming/changing a metadata field referenced by a mapping MUST surface the dependent mapping and block or migrate explicitly.

FDA-1220 Mapping rules MUST NOT mutate the underlying metadata values they read.
