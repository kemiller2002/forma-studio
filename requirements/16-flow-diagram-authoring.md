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
