# 03 Forma catalog and component properties

## Catalog source

FCP-001 The component catalog MUST be generated from a pinned Forma package/contract, not manually maintained as an unrelated list.

FCP-002 Every selectable component MUST have a canonical Forma component ID.

FCP-003 Studio MUST record the Forma package version associated with the project.

FCP-004 Studio MUST NOT silently substitute a component from another Forma version.

FCP-005 Missing or incompatible component contracts MUST be explicit project validation errors.

FCP-006 If Forma metadata is insufficient for safe visual authoring, Studio MUST record a gap for Forma rather than inventing undocumented properties.

## Catalog UX

FCP-020 Components MUST be searchable by name, ID, category, and documented purpose.

FCP-021 The catalog SHOULD support categories such as layout, navigation, input, assessment, feedback, data display, workflow, and utility.

FCP-022 Catalog entries SHOULD show a live miniature preview where performance permits.

FCP-023 Catalog entries MUST identify deprecated components.

FCP-024 Deprecated components MAY remain renderable for old projects but MUST not be presented as preferred for new insertions.

FCP-025 Catalog search MUST be keyboard accessible.

FCP-026 Frequently used/recent components MAY be surfaced without changing canonical ordering.

## Property model

FCP-040 The property inspector MUST be generated from typed component property metadata where available.

FCP-041 Property values MUST be validated before they enter canonical project state.

FCP-042 Supported property types SHOULD include strings, numbers, booleans, enums, token references, page references, URLs, content collections, and component slots.

FCP-043 Enum properties MUST expose only legal options.

FCP-044 Required properties MUST be visually distinguished and included in validation.

FCP-045 Read-only/derived properties MUST not be editable.

FCP-046 Conditional properties MUST be shown only when their applicability condition is met or shown disabled with an explanation.

FCP-047 Property editing MUST preserve unknown forward-compatible fields during migrations when safe.

FCP-048 A property reset MUST restore the canonical default, not a Studio-specific value.

## Shared vocabulary

FCP-060 Where Forma exposes concepts equivalent to State, Size, Tone, Density, Disabled, Read only, Selected, Checked, Expanded, Loading, Label, Supporting text, Leading icon, or Trailing icon, Studio SHOULD preserve those canonical names.

FCP-061 Studio MUST NOT use presentation properties to encode domain permissions or business legality.

FCP-062 Visual states used only for preview MUST be identifiable as scenarios/presentation state.

## Render integrity

FCP-080 Studio MUST use canonical semantic HTML patterns and Forma CSS.

FCP-081 Studio MUST not fork component CSS merely to make the editor easier.

FCP-082 Editor selection/highlight styling MUST be layered outside the component's production contract.

FCP-083 Browser-native controls MUST remain browser-native unless Forma defines otherwise.

FCP-084 Component accessibility attributes defined by Forma MUST be preserved.

FCP-085 Studio MUST support 320px presentation for every canonical component.

FCP-086 If a component fails its documented mobile contract inside Studio, validation MUST identify the component and viewport.

## Slots and composition constraints

FCP-100 Studio MUST know which child slots exist for components that accept nested content.

FCP-101 Studio MUST validate allowed child component kinds where Forma declares restrictions.

FCP-102 Required slots MUST produce obligations when empty.

FCP-103 Maximum cardinality MUST be enforced where declared.

FCP-104 Moving a child between slots MUST preserve the child ID when legal.

## Version changes

FCP-120 Upgrading Forma MUST be an explicit project operation.

FCP-121 Studio MUST present changed/removed component contracts before finalizing an upgrade.

FCP-122 Projects MUST remain pinned to the old Forma version until migration is accepted.

FCP-123 Studio MUST support migration reports listing affected pages/nodes.

FCP-124 A migration MUST not silently drop unsupported properties or children.

FCP-125 Studio SHOULD allow previewing the upgraded rendering before committing the project version change.

## Developer inspection

FCP-140 Selecting a component MUST expose canonical component ID, Forma version, source pattern identifier/path when available, active properties, token bindings, accessibility notes, and generated markup.

FCP-141 Developer inspection MUST show code-facing token syntax where available.

FCP-142 Studio SHOULD link to the matching Forma documentation page.

FCP-143 The inspector MUST distinguish canonical markup from editor-only wrappers.
