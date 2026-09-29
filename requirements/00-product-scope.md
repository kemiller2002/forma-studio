# 00 Product and scope

## Product purpose

PS-001 Forma Studio MUST let a person visually design one or more application pages using the canonical Forma component library and create diagram/flow surfaces using Studio's canonical graph model.

PS-002 Forma Studio MUST save the result as a typed, machine-readable project specification rather than as screenshots or opaque canvas data.

PS-003 Forma Studio MUST render canonical Forma components using the actual Forma HTML/CSS implementation whenever technically possible.

PS-004 The saved specification MUST remain independent of the editor's transient DOM structure.

PS-005 The project format MUST be usable by humans, coding agents, CI validation, and future tooling without requiring Forma Studio to be running.

PS-006 A project MUST pin a concrete Forma version.

PS-007 A project MUST record the Forma Studio schema version.

PS-008 Studio MUST distinguish presentation state from the application-domain state of the application being designed.

PS-009 Studio MUST NOT become the source of truth for business rules, permissions, scoring, obligations, or domain transition legality in generated applications.

PS-010 Studio MUST support page-only, diagram-only, and mixed projects, including single-page, multi-page, single-diagram, and multi-diagram specifications.

PS-011 Studio MUST support explicit links between pages and allow a user to preview those links interactively.

PS-012 Studio MUST support static marketing/content pages as well as application-oriented pages.

PS-013 Studio MUST be usable without Figma.

PS-014 Studio MUST NOT require proprietary design-file formats for its core workflow.

PS-015 Studio MUST provide deterministic import/export of its own project format.

PS-016 Studio SHOULD be able to coexist with Figma as an optional downstream/upstream integration later without making Figma authoritative.

## Primary users

PS-020 Studio MUST support a designer/product user who does not want to hand-author HTML.

PS-021 Studio MUST support a developer inspecting exact component contracts, markup, token names, and responsive behavior.

PS-022 Studio MUST support an AI coding agent consuming a project specification without screenshot interpretation.

PS-023 Studio MUST support a technical reviewer validating accessibility, responsiveness, component usage, and navigation completeness.

PS-024 Studio SHOULD support a stakeholder in presentation/preview mode without exposing editing controls.

## Initial workflows

PS-030 A user MUST be able to create a project, add pages and diagrams, add Forma components or diagram elements appropriate to the active surface, configure allowed properties, enter sample content, link pages or diagram elements, preview, validate, save, reopen, and export.

PS-031 A user MUST be able to start from a blank-template creation flow and choose the first Layout page or Flow diagram; canonical project creation MUST establish that first authored surface atomically rather than persisting an invalid zero-surface project or an unrelated dummy surface.

PS-032 A user SHOULD be able to start from a future template without changing the project file format.

PS-033 A user MUST be able to clone/duplicate a project file safely.

PS-035 Studio MUST distinguish copying the same project file/revision from creating a new project derived from an existing project.

PS-036 "Duplicate as new project" MUST allocate a new project ID and MUST define deterministic remapping/preservation rules for internal page/diagram/element IDs and internal references.

PS-037 A copied file intended to remain the same project MAY preserve project/internal IDs, but Studio MUST make the identity distinction visible enough to avoid accidental divergent saves to the same remote project location.

PS-034 A user MUST be warned before a destructive project migration or unsupported downgrade.

## Product principles

PS-040 Canonical Forma contracts win over editor convenience.

PS-041 Browser-native semantics MUST be preserved rather than replaced with div-based visual approximations.

PS-042 Accessibility MUST be a construction constraint, not a post-export report only.

PS-043 Responsive behavior MUST be previewed by resizing the real rendered composition.

PS-044 Invalid states MUST be prevented where practical and otherwise surfaced explicitly.

PS-045 Every automatic repair MUST be explainable and reversible.

PS-046 Studio MUST preserve unknown or unresolved information rather than silently inventing values.

PS-047 Studio MUST record gaps when a requested capability is absent from Forma.

PS-048 Studio SHOULD make the legal next actions discoverable from current editor state.

## v1 boundary

PS-060 v1 MUST include project/page management, component composition, page linking, a basic Flow surface with nodes and connectors, responsive preview, accessibility/contract validation, local draft persistence, JSON import/export, and agent-oriented export.

PS-061 v1 SHOULD include GitHub persistence as an explicit save/commit workflow.

PS-062 v1 SHOULD include undo/redo based on explicit editor transitions.

PS-063 v1 MAY include comments/annotations, but real-time multiplayer editing is not required.

PS-064 v1 MUST NOT include arbitrary JavaScript execution inside a project.

PS-065 v1 MUST NOT include general freehand/vector illustration tools.

PS-066 v1 MUST NOT allow users to redefine canonical Forma component internals inside Studio.

PS-067 v1 MAY support external image/SVG assets subject to security and accessibility rules.

PS-068 v1 MUST be mobile-usable for review and basic editing; desktop/tablet MAY provide richer spatial editing affordances.

PS-069 Studio MUST expose Layout and Flow as distinct surface behaviors over one shared editor core; adding Flow MUST NOT create a second selection, history, persistence, or command architecture.

PS-070 The Flow surface MUST support general-purpose diagrams without requiring semantic typing and MUST also support explicit typed profiles such as workflow, state, and architecture diagrams.

PS-071 Free spatial placement is valid project intent on Flow surfaces, but MUST NOT weaken the rule that ordinary Forma layout composition is semantic/constraint-driven rather than arbitrary x/y placement.

PS-072 Studio MUST NOT claim that a typed diagram is executable application/domain logic merely because it has workflow or state semantics; execution/code-generation requires a separate explicit contract.

## Completion criteria

PS-080 A representative project with at least five pages, nested Forma components, internal navigation cycles, external links, Light/Dark preview, and 320px mobile preview MUST be expressible without editing raw JSON.

PS-081 The same representative project MUST round-trip export/import without semantic loss.

PS-082 An agent MUST be able to determine page structure, component contracts, token bindings, links, scenarios, diagram topology, typed relationship/profile semantics, authored geometry, and cross-surface references from the exported project without inspecting screenshots.

PS-083 The editor MUST be capable of identifying at least broken page targets, duplicate routes, invalid component IDs, unsupported properties, missing required accessibility metadata, broken diagram edge endpoints, and invalid typed-diagram relationships.

PS-084 The shared editor architecture MUST prove round-trip editing on both surfaces: a Layout slice (Stack -> Heading -> edit -> reorder -> spacing -> undo/redo -> save/reload) and a Flow slice (two nodes -> connect -> move -> label -> undo/redo -> save/reload).
