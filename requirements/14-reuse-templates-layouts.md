# 14 Reusable compositions, templates, and shared layout

## Reuse problem

RTL-001 Studio SHOULD support reuse without forcing users to duplicate identical page chrome across many pages.

RTL-002 Reuse MUST preserve the central rule that Forma remains authority for primitive/component implementation.

RTL-003 A reusable Studio composition is a composition of Forma nodes, not a forked Forma component.

## Shared compositions

RTL-020 Studio SHOULD support named reusable compositions/fragments after the basic page editor is stable.

RTL-021 A reusable composition MUST have a stable definition ID.

RTL-022 Instances MUST retain identity separate from their definition.

RTL-023 Definition updates SHOULD propagate to instances unless an explicit supported override exists.

RTL-024 Overrides MUST be explicit, typed, inspectable, and limited to declared parameters/slots.

RTL-025 Studio MUST NOT silently detach instances when an override becomes incompatible.

RTL-026 Shared compositions SHOULD support headers, application shells, navigation bars, footer regions, repeated panels, and other project-level structures.

RTL-027 Shared compositions MUST participate in validation and navigation dependency analysis.

## Page layouts

RTL-040 A project SHOULD be able to define page-layout compositions.

RTL-041 A page layout SHOULD expose named content slots.

RTL-042 Pages using a layout MUST persist their layout reference plus slot content, not a hidden flattened copy, unless explicitly detached.

RTL-043 Detaching from a layout MUST be an explicit undoable operation.

RTL-044 Layout changes MUST identify pages that would become invalid before finalization.

RTL-045 Layouts MUST render through ordinary Forma/Studio composition rules.

## Project navigation structures

RTL-060 Reusable navigation/menu compositions SHOULD be able to bind entries to project page IDs.

RTL-061 Renaming/re-routing a page MUST update navigation projection without requiring every menu instance to be rewritten.

RTL-062 Deleting a page referenced by shared navigation MUST surface one dependency at the shared definition plus affected instances/pages.

RTL-063 Active-page styling in preview MAY be derived from current preview location as presentation state.

## Templates

RTL-080 Studio MAY ship starter project/page templates.

RTL-081 Templates MUST be ordinary versioned Studio project fragments/documents using public schema concepts.

RTL-082 Applying a template MUST create new project/page/node IDs where required.

RTL-083 Template content MUST be clearly distinguishable from canonical Forma components and from user project content.

RTL-084 Templates MUST pin or declare compatible Forma versions.

RTL-085 Template upgrades MUST NOT silently rewrite projects created from older template versions.

## Libraries

RTL-100 Future organization/user composition libraries MAY publish reusable Studio compositions independently of Forma.

RTL-101 Such libraries MUST use separate package/namespace identity so users can distinguish Echelon Forma primitives from organization-specific compositions.

RTL-102 Library dependencies MUST be pinned and versioned.

RTL-103 Missing library versions MUST fail explicitly rather than flattening to an approximation.

RTL-104 A project SHOULD remain inspectable even when a reusable library dependency is unavailable, preserving identifiers/unknown state.

## Acceptance

RTL-120 A project with ten pages sharing one header/navigation composition MUST be able to change the shared navigation label once and preview the update on all ten pages.

RTL-121 Deleting a page referenced by the shared navigation MUST identify the broken shared navigation entry.

RTL-122 Export must make it possible for an implementation agent to distinguish Forma components, Studio shared compositions, and page-local nodes.
