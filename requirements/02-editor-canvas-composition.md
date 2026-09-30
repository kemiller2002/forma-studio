# 02 Editor, canvas, and composition

## Editor layout

ECC-001 The editor MUST provide an authored-surface navigator, surface-appropriate palette/catalog, selected-surface Structure/Outline view, visual preview/canvas, property inspector, validation surface, and project/save status.

ECC-002 Major editor regions MUST be keyboard reachable.

ECC-003 Desktop layouts SHOULD allow panels to be resized and collapsed.

ECC-004 Mobile layouts MUST prioritize the active authored surface, selection, Structure/Outline navigation, property editing, and validation without requiring a desktop-width canvas.

ECC-005 Studio MUST provide a distraction-reduced preview mode.

ECC-006 The authored-surface navigator MUST distinguish Layout pages from Flow diagrams without requiring users to infer surface type from icons/color alone.

ECC-007 Selecting a surface MUST switch palette, Structure/Outline projection, inspector capabilities, validation scope, and canvas behavior through the shared editor capability model.

ECC-008 A diagram-only project MUST be fully usable without a hidden or synthetic page navigator entry.

ECC-009 A mixed project SHOULD make cross-surface references discoverable without merging page/component hierarchy and graph topology into one misleading tree.

ECC-010 The selected/active surface is editor state; changing active surface MUST NOT mutate canonical project content.

## Canvas model

ECC-020 The Layout surface canvas MUST render actual browser layout using Forma CSS.

ECC-021 The Layout surface MUST NOT store arbitrary x/y coordinates for ordinary browser flow-layout components.

ECC-022 Component placement MUST follow the canonical component/slot/layout contract.

ECC-023 On the Layout surface, absolute positioning MAY exist only for an explicitly modeled Forma overlay/free-position capability and MUST NOT be the default composition model. This restriction does not prohibit canonical spatial geometry on Flow diagrams.

ECC-024 Canvas zoom MUST be a view preference and MUST NOT alter project data.

ECC-025 Canvas panning MUST be a view preference and MUST NOT alter project data.

ECC-026 Layout SHOULD support fit-to-page/fit-to-width commands; Flow uses its separately specified fit-diagram/fit-selection/content-bounds commands.

ECC-027 On Layout surfaces, selected component boundaries MUST remain visible without changing actual exported component markup.

ECC-028 Editor adorners MUST NOT leak into generated/exported markup.

## Component insertion

ECC-040 A user MUST be able to add a component from the catalog into every compatible insertion point.

ECC-041 Incompatible insertion points MUST be disabled or absent, with an explanation available.

ECC-042 Drag/drop MUST be one interaction method but MUST NOT be the only way to insert or reorder components.

ECC-043 Keyboard insertion and movement MUST be supported.

ECC-044 Studio MUST expose named slots when a Forma pattern defines them.

ECC-045 A component with required children/slots MUST surface unresolved obligations until satisfied.

ECC-046 Adding a component MUST create a stable node ID.

ECC-047 Adding a component MUST initialize only canonical defaults; Studio MUST NOT invent domain content.

## Selection and tree

ECC-060 A click/tap on preview content SHOULD select the corresponding component node.

ECC-061 Selecting an element in the active Structure/Outline view MUST highlight its corresponding preview/canvas representation.

ECC-062 Nested components MUST appear hierarchically.

ECC-063 Layout component trees and Flow structure/outline representations MUST support expand/collapse independent of authored presentation state.

ECC-064 Multi-selection SHOULD be supported for compatible batch operations.

ECC-067 The inspector for a multi-selection MUST expose only operations/properties legal for the complete selection or clearly partition incompatible subsets without partial silent mutation.

ECC-068 Mixed property values in multi-selection MUST be represented as mixed/indeterminate rather than choosing an arbitrary selected value.

ECC-069 A batch property edit MUST be atomic for the targeted selection: all legal selected targets update or the command rejects/returns an explicit partial-capability plan before mutation.

ECC-070 Project-wide search SHOULD span pages/components, diagrams/elements, annotations, requirement references, routes, and stable IDs and navigate to the matching authored surface.

ECC-065 Selection MUST be editor-only state.

ECC-066 Structure/Outline views MUST expose stable IDs for debugging without making them primary user-facing labels.

## Movement and restructuring

ECC-080 A user MUST be able to reorder siblings where the parent contract permits ordering.

ECC-081 A user MUST be able to move a node between compatible slots/parents.

ECC-082 Illegal moves MUST be rejected before document mutation.

ECC-083 Moves MUST preserve node IDs.

ECC-084 Duplicate operations MUST create new node/action/annotation IDs.

ECC-085 Delete MUST surface any navigation or annotation dependencies before completion.

ECC-086 Cut/copy/paste SHOULD be implemented as explicit commands against serialized node fragments.

ECC-087 Clipboard integration MUST use Limen rather than direct browser calls from application state code.

## Undo and redo

ECC-100 All user-visible project mutations MUST be undoable unless documented as an external side effect.

ECC-101 Undo/redo MUST operate on F# editor transitions, not browser DOM snapshots.

ECC-102 Undo MUST restore component structure, properties, links, and relevant editor-visible validation state.

ECC-103 Redo history MUST be invalidated predictably after a new mutation following undo.

ECC-104 Save/persist actions MUST NOT erase undo history merely because persistence succeeded.

ECC-105 External persistence results with unknown outcome MUST be represented explicitly and MUST NOT cause automatic duplicate writes.

ECC-106 Canonical undo/redo history MUST be project-wide and chronological across Layout and Flow surfaces unless a later version explicitly introduces a scoped-history mode.

ECC-107 If undo/redo affects a non-active authored surface, Studio MUST make the affected surface/result discoverable and SHOULD navigate/select it when doing so is not disruptive.

ECC-108 View-only changes such as pan, zoom, active surface, panel size, hover, transient isolation, and selection MUST NOT enter canonical project undo history.

ECC-109 Gesture coalescing MUST preserve chronological project history: one committed move/resize/route gesture is one logical history entry, not hundreds of pointer-event entries.

ECC-110 External side effects such as save/export are not reversed by project undo merely because they were initiated after an edit; Studio MUST distinguish canonical-state undo from effect history.

## Shared object metadata

ECC-130 Every addressable authored object MAY carry typed, namespaced descriptive metadata independently from its visible content and presentation.

ECC-131 Shared object metadata applies to at least projects, Layout pages, addressable Layout component nodes, Flow diagrams/elements, scenarios, annotations, and future Folio/document objects when those objects are represented canonically in Studio.

ECC-132 Metadata MUST remain separate from stable identity, visible text/content, semantic type, layout/geometry, style/appearance, accessibility semantics, and typed resource references.

ECC-133 The same metadata schema/value infrastructure SHOULD be reused across Layout and Flow rather than creating surface-specific metadata stores.

ECC-134 Metadata edits MUST use the shared typed command/validation/history mechanism regardless of authored surface.

ECC-135 The inspector MUST provide a consistent Metadata region for addressable objects while allowing profile/component contracts to restrict which fields are legal.

ECC-136 Layout component metadata MUST NOT be emitted into runtime DOM attributes automatically; only explicit Forma/component presentation or integration contracts may project selected metadata into markup.

ECC-137 Metadata on a Layout component MUST NOT change component semantics, CSS layout, responsive behavior, or interaction legality unless an explicit consuming contract maps that metadata to a legal property/command.

ECC-138 Project/page/surface metadata MAY describe purpose, owner, tags, source, revision, documentation references, or other non-secret context without requiring visible rendering.

ECC-139 Agent/developer export MUST expose shared object metadata through the same stable metadata model on both Layout and Flow objects.

## Content editing

ECC-120 Text/content fields MUST be edited through typed component properties/content contracts.

ECC-121 Inline editing MAY be provided as a convenience but MUST dispatch the same command as inspector editing.

ECC-122 Studio MUST support realistic long-content testing.

ECC-123 Studio SHOULD provide content-stress presets such as empty, one-character, very long, multiline, and internationalized strings.

ECC-124 Content changes MUST not modify Forma component internals.

## Layout assistance

ECC-140 On Layout surfaces, Studio SHOULD offer alignment/spacing guidance only where it maps to actual Forma or browser layout contracts; Flow alignment/snapping follows its separate spatial requirements.

ECC-141 Studio MUST NOT imply pixel-perfect arbitrary alignment capabilities that cannot be represented in the saved specification.

ECC-142 Layout grid/ruler overlays MAY assist review but MUST not become hidden layout dependencies; Flow grid/guide behavior is governed by the Flow requirements.

ECC-143 Studio SHOULD expose which Forma spacing/radius/token choices are active on the selected component when those choices are configurable.

## Shared editor core

ECC-180 Layout and Flow surfaces MUST use one shared editor core for selection, command dispatch, undo/redo, clipboard semantics, persistence coordination, validation projection, pan/zoom view state, and inspector integration.

ECC-181 Surface-specific behavior MUST be expressed through capabilities and legal commands rather than by creating parallel editor state machines.

ECC-182 Every user-visible project mutation MUST be representable as an explicit typed editor command with stable target identifiers and enough information to validate legality before canonical state changes.

ECC-183 Pointer drag/drop, inline editing, inspector editing, structure-tree editing, keyboard commands, touch actions, and future agent operations MUST dispatch the same underlying command type when they express the same intent.

ECC-184 A command MUST be atomic with respect to canonical project state: it either produces one valid successor state or leaves the prior state unchanged.

ECC-185 Command results MUST distinguish applied, rejected/illegal, blocked-by-obligation, and external-effect-unknown outcomes where applicable.

ECC-186 Command history MUST remain independent of transient DOM nodes, pointer coordinates, hover state, animation frames, and rendered preview artifacts.

ECC-187 Studio SHOULD expose a command palette that can invoke legal editor commands without requiring pointer interaction.

ECC-188 Studio SHOULD expose a breadcrumb/ancestry selector for nested selection and allow moving selection to the containing element without precision pointer interaction.

## Direct manipulation and drag/drop

ECC-200 Drag/drop MUST provide visible candidate insertion or connection targets before commit.

ECC-201 Layout-surface dragging MUST resolve to semantic operations such as insert, reorder, move-to-slot, wrap, or replace; it MUST NOT persist arbitrary pointer x/y coordinates for ordinary Forma layout nodes.

ECC-202 A drag preview MUST NOT mutate canonical project state before a legal drop/commit action.

ECC-203 Illegal drop targets MUST be visibly distinguishable from legal targets and MUST NOT be made legal by silently rewriting unrelated project structure.

ECC-204 Moving an existing node by drag/drop MUST preserve its stable ID unless the operation is explicitly a copy/duplicate.

ECC-205 Resize handles MAY provide direct manipulation, but the resulting persisted value MUST map to the active surface's canonical contract: semantic Forma sizing/layout choices on Layout surfaces and explicit geometry on Flow surfaces.

ECC-206 Token-backed spacing, sizing, alignment, and layout controls SHOULD snap to legal Forma values where the selected Forma contract defines such values.

ECC-207 Studio MAY provide an explicit escape hatch for literal values only where the relevant Forma contract permits literals; use of a literal MUST remain inspectable.

ECC-208 Inline add affordances, catalog drag/drop, structure-tree commands, and command-palette insertion MUST converge on the same insertion legality rules.

ECC-209 Multi-selection drag/move MUST either apply one legal atomic operation to the entire selection or reject without partial mutation.

ECC-210 Touch authoring MUST provide alternatives for operations whose desktop form depends on hover, tiny handles, modifier keys, or right-click.

ECC-211 Pan/zoom gestures MUST operate on editor view state and MUST not be confused with moving project elements.

## Error handling

ECC-160 A failed command MUST leave the prior valid document state intact.

ECC-161 Rejected commands MUST explain why they are illegal and, when known, identify legal next actions.

ECC-162 Unknown external effect outcomes MUST remain reconcilable.

ECC-163 Studio MUST preserve unsaved local draft state across ordinary reloads when storage is available.

ECC-164 Failure to persist a draft MUST be visible and MUST NOT claim a successful save.
