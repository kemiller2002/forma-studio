# 02 Editor, canvas, and composition

## Editor layout

ECC-001 The editor MUST provide a component catalog, page navigator, selected-page component tree, visual preview/canvas, property inspector, validation surface, and project/save status.

ECC-002 Major editor regions MUST be keyboard reachable.

ECC-003 Desktop layouts SHOULD allow panels to be resized and collapsed.

ECC-004 Mobile layouts MUST prioritize page preview, selection, property editing, and validation without requiring a desktop-width canvas.

ECC-005 Studio MUST provide a distraction-reduced preview mode.

## Canvas model

ECC-020 The canvas MUST render actual browser layout using Forma CSS.

ECC-021 The canvas MUST NOT store arbitrary x/y coordinates for ordinary flow-layout components.

ECC-022 Component placement MUST follow the canonical component/slot/layout contract.

ECC-023 Absolute positioning MAY exist only for a future explicitly modeled overlay/free-position capability and MUST NOT be the default composition model.

ECC-024 Canvas zoom MUST be a view preference and MUST NOT alter project data.

ECC-025 Canvas panning MUST be a view preference and MUST NOT alter project data.

ECC-026 The canvas SHOULD support fit-to-page/fit-to-width commands.

ECC-027 Selected component boundaries MUST remain visible without changing actual exported component markup.

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

ECC-061 Selecting a node in the tree MUST highlight its preview representation.

ECC-062 Nested components MUST appear hierarchically.

ECC-063 The component tree MUST support expand/collapse independent of component presentation state.

ECC-064 Multi-selection SHOULD be supported for compatible batch operations.

ECC-065 Selection MUST be editor-only state.

ECC-066 The tree MUST expose stable IDs for debugging without making them primary user-facing labels.

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

## Content editing

ECC-120 Text/content fields MUST be edited through typed component properties/content contracts.

ECC-121 Inline editing MAY be provided as a convenience but MUST dispatch the same command as inspector editing.

ECC-122 Studio MUST support realistic long-content testing.

ECC-123 Studio SHOULD provide content-stress presets such as empty, one-character, very long, multiline, and internationalized strings.

ECC-124 Content changes MUST not modify Forma component internals.

## Layout assistance

ECC-140 Studio SHOULD offer alignment/spacing guidance only where it maps to actual Forma or browser layout contracts.

ECC-141 Studio MUST NOT imply pixel-perfect arbitrary alignment capabilities that cannot be represented in the saved specification.

ECC-142 Grid/ruler overlays MAY assist review but MUST not become hidden layout dependencies.

ECC-143 Studio SHOULD expose which Forma spacing/radius/token choices are active on the selected component when those choices are configurable.

## Error handling

ECC-160 A failed command MUST leave the prior valid document state intact.

ECC-161 Rejected commands MUST explain why they are illegal and, when known, identify legal next actions.

ECC-162 Unknown external effect outcomes MUST remain reconcilable.

ECC-163 Studio MUST preserve unsaved local draft state across ordinary reloads when storage is available.

ECC-164 Failure to persist a draft MUST be visible and MUST NOT claim a successful save.
