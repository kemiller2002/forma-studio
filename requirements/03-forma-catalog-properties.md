# 03 Forma catalog and component properties

## Catalog source

FCP-001 The component catalog MUST be generated from a pinned Forma package/contract, not manually maintained as an unrelated list.

FCP-002 Every selectable component MUST have a canonical Forma component ID.

FCP-003 Studio MUST record the Forma package version associated with the project.

FCP-004 Studio MUST NOT silently substitute a component from another Forma version.

FCP-005 Missing or incompatible component contracts MUST be explicit project validation errors.

FCP-006 If Forma metadata is insufficient for safe visual authoring, Studio MUST record a gap for Forma rather than inventing undocumented properties.

FCP-007 Studio MUST synchronize any public Forma diagram-presentation capability manifests once Forma exposes them; Studio MUST NOT maintain an unrelated hand-authored mirror.

FCP-008 Diagram presentation capabilities supplied by Forma MUST have stable identifiers/version compatibility information usable by Flow profile mappings.

FCP-009 Missing diagram node/connector/container/swimlane presentation primitives required by a profile MUST be recorded as explicit Forma capability gaps.

FCP-010 The initial public diagram-presentation gap and ownership decision is tracked by kemiller2002/forma#52; Studio requirements MUST remain valid whether Forma resolves that gap with new primitives or an explicit alternate public contract.

## Catalog UX

FCP-020 Components MUST be searchable by name, ID, category, and documented purpose.

FCP-021 The catalog SHOULD support categories such as layout, navigation, input, assessment, feedback, data display, workflow, and utility.

FCP-022 Catalog entries SHOULD show a live miniature preview where performance permits.

FCP-023 Catalog entries MUST identify deprecated components.

FCP-024 Deprecated components MAY remain renderable for old projects but MUST not be presented as preferred for new insertions.

FCP-025 Catalog search MUST be keyboard accessible.

FCP-026 Frequently used/recent components MAY be surfaced without changing canonical ordering.

FCP-027 Flow palette presentation entries backed by Forma MUST expose the canonical Forma presentation capability ID separately from the Studio diagram semantic element-kind ID.

FCP-028 Studio MUST NOT infer Workflow/State/Architecture semantics solely from a Forma visual primitive.

FCP-029 A Forma presentation primitive MAY be reused by multiple diagram semantic kinds when the profile explicitly maps them; visual reuse does not collapse semantic identity.

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

FCP-080 Layout rendering MUST use canonical semantic HTML patterns and Forma CSS. Forma-backed Flow presentation MUST use the applicable public Forma presentation contract without requiring Flow's renderer technology itself to be HTML.

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

FCP-126 A Forma upgrade MUST also review diagram profile presentation mappings and report removed/changed diagram presentation capabilities used by project diagrams.

FCP-127 Forma upgrade preview SHOULD render affected diagrams without rewriting graph topology or geometry.

FCP-128 A changed Forma presentation contract MUST NOT silently coerce a diagram semantic element kind into another kind.

FCP-129 If upgraded Forma presentation causes clipping/contrast/legibility failures in diagrams, those findings MUST be surfaced before the project upgrade is accepted where detectable.

## Developer inspection

FCP-140 Selecting a component MUST expose canonical component ID, Forma version, source pattern identifier/path when available, active properties, token bindings, accessibility notes, and generated markup.

FCP-141 Developer inspection MUST show code-facing token syntax where available.

FCP-142 Studio SHOULD link to the matching Forma documentation page.

FCP-143 The inspector MUST distinguish canonical markup from editor-only wrappers.

FCP-144 Diagram developer inspection MUST distinguish canonical Studio graph semantics from the Forma presentation primitive used to render them.

FCP-145 When a selected diagram element is Forma-backed, Studio SHOULD link to the matching public Forma documentation/manifest entry.

FCP-146 Editor-only diagram adorners MUST never be reported as public Forma component capabilities.


## Cross-surface object metadata

FCP-150 Studio MUST allow descriptive typed/namespaced metadata on addressable Layout objects, including at least pages and component nodes, without requiring that metadata to be a Forma component property.

FCP-151 Layout object metadata MUST remain separate from canonical Forma component properties, DOM attributes, CSS classes, token bindings, navigation bindings, and scenario overrides.

FCP-152 Studio MUST NOT pass arbitrary object metadata into rendered component markup by default.

FCP-153 A metadata field intended to affect actual component behavior/content MUST map through an explicit supported component property, scenario value, navigation/action contract, or application/domain integration rather than hidden metadata behavior.

FCP-154 Layout object metadata MAY use the same versioned metadata schema/value model as Flow objects, including stable field keys, typed values, defaults, constraints, visibility policy, derived/source-bound status, and localized display labels.

FCP-155 Component-node metadata edits MUST use the shared typed command/history/persistence/diff model.

FCP-156 Component-node metadata MUST participate in copy/paste, duplication, reusable compositions/templates, migration, search/filter, validation, and agent/developer export according to visibility policy.

FCP-157 A page MAY carry descriptive metadata independently from route, title, component tree, and preview settings.

FCP-158 Page/component metadata MUST NOT imply authorization, domain state, application legality, accessibility semantics, or component state unless an explicit consuming contract establishes that meaning.

FCP-159 Metadata attached to a reusable composition instance MUST distinguish definition metadata from instance-local metadata/overrides when both are supported.

FCP-160 Unknown forward-compatible Layout object metadata MUST be preserved when safe rather than dropped because the current Forma component contract does not understand it.

FCP-161 Studio SHOULD expose Layout metadata in the same inspector conceptual section as Flow metadata so users/agents do not learn two unrelated metadata systems.

FCP-162 Metadata visibility/sensitivity policy MUST apply equally to Layout and Flow exports, diagnostics, search, and agent packets.

FCP-163 Stable typed references on Layout objects MUST remain distinct from arbitrary metadata strings/URLs and use the project's reference/dependency contracts where applicable.
