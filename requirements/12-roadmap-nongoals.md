# 12 Roadmap, deferred capabilities, and non-goals

## Explicit v1 non-goals

RN-001 General freehand drawing is not a v1 goal.

RN-002 Arbitrary Bezier/vector path editing is not a v1 goal.

RN-003 Photo/image editing is not a v1 goal.

RN-004 3D design is not a v1 goal.

RN-005 Real-time multiplayer cursor/presence editing is not a v1 goal.

RN-006 A proprietary cloud database/account system is not required for v1.

RN-007 Arbitrary JavaScript plugins executing in project preview are not a v1 goal.

RN-008 Studio does not own generated application's business-domain state.

RN-009 Studio does not replace Forma's component implementation authority.

RN-010 Studio does not promise pixel identity with unrelated design tools.

## Near-term phases

RN-020 Phase 0: governed repository bootstrap, complete requirements, schema, marketing shell, and capability installation.

RN-021 Phase 1: F# domain model, project import/export, page management, navigation graph, validation core, and command/undo model.

RN-022 Phase 2: Forma catalog integration, real component rendering, tree/inspector editing, token browser, responsive/theme preview.

RN-023 Phase 3: scenarios, interactive linked-page preview, accessibility validation, agent/developer export.

RN-024 Phase 4: GitHub persistence, semantic diff/review workflow, richer marketing/demo experience.

RN-025 Phase 5: implementation-generation experiments and cross-tool adapters.

## Deferred but compatible capabilities

RN-040 Figma import/export MAY be explored as an adapter to the canonical project model.

RN-041 Other design-tool adapters MAY be added behind explicit translation contracts.

RN-042 A template/gallery system MAY be added using ordinary versioned project files.

RN-043 Reusable user-defined page fragments/compositions MAY be added with stable IDs and dependency/version rules.

RN-044 Brand/theme packs MAY be added through namespaced token packages.

RN-045 Real-time collaboration MAY be added using the same command/event semantics.

RN-046 A hosted persistence service MAY be added without invalidating local/Git workflows.

RN-047 Comments/review threads MAY be stored externally or in-project if ownership semantics remain explicit.

RN-048 Plugin extensibility MAY be explored, but untrusted code execution requires a separate security design.

RN-049 Asset pipelines MAY grow to include managed uploads/CDN storage.

RN-050 Localization workflows MAY add per-locale fixture/content packs.

## Advanced validation/reasoning

RN-060 Studio SHOULD eventually validate that visual components which imply application responsibilities have corresponding requirement references where teams opt into that rule.

RN-061 Studio MAY surface a warning when a visual construct such as validation summary, work queue, or permission-sensitive action has no declared implementation responsibility.

RN-062 Such cross-layer checks MUST be configurable and MUST NOT infer domain facts from visuals as certainty.

RN-063 Ordo integration MAY eventually map Studio obligations/unknowns into engineering requirements/work items through a versioned adapter.

RN-064 Visual Engineering MAY eventually provide executable design checks if/when its evidence supports them.

RN-065 Communication Engineering MAY eventually provide executable content checks if/when its evidence supports them.

## AI-assisted editing

RN-080 An agent MAY propose page/component changes as commands.

RN-081 Agent proposals MUST be previewable as a diff before canonical state changes when the action is consequential.

RN-082 Agents MUST use the same legal command surface as human editing; there is no privileged DOM mutation path.

RN-083 Agent-generated content MUST preserve unknowns and MUST not invent business rules.

RN-084 Agent operations SHOULD cite requirement/component IDs in their change explanations.

RN-085 Future natural-language-to-page generation MUST result in ordinary project specification data that can be edited without the model.

## Research questions

RN-100 Measure whether machine-readable visual specifications reduce interpretation/rework relative to screenshot/mockup handoff before making quantitative marketing claims.

RN-101 Measure whether use of canonical real components improves responsive mismatch rates.

RN-102 Measure agent implementation accuracy from Studio project packets versus screenshots and prose-only requirements.

RN-103 Measure authoring friction for designers compared with code-first Forma composition.

RN-104 Study whether semantic navigation/component diffs improve review quality.

RN-105 Keep experimental findings separate from product claims until evidence is sufficient.

## Architecture guardrail

RN-120 New capabilities MUST not weaken the central invariant: the project specification is authoritative; canvas/DOM/editor chrome are projections.

RN-121 New capabilities MUST not create a second private implementation of Forma components.

RN-122 New browser capabilities MUST cross Limen.

RN-123 New stateful editor behavior MUST be modeled through explicit F# transitions.

RN-124 New repository process MUST follow ROS/SDE rather than relying on conversation state.
