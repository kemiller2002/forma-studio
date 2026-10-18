---
id: RQ-FORMA-STUDIO-DBC-001
title: Contract-constrained design authoring
status: draft
version: 1.0.0
owners: [forma-studio-maintainers]
created: 2026-10-18
updated: 2026-10-18
priority: critical
related_documents:
  - requirements/06-validation-accessibility.md
  - requirements/14-reuse-templates-layouts.md
tags: [design-by-construction, contracts, accessibility, responsive-design]
derived_from: [INPUT-b88710cff74bf520]
provenance:
  contributions:
    EXE-20261018T124842408Z-153b4e6e:
      operations: [created]
      at: 2026-10-18T12:53:00.522Z
      actor:
        kind: agent
        id: openai/codex
        provider: openai
        model: unknown
        runtime: codex
      reason: "Canonical ingestion of claimed requirement source"
---

# Contract-constrained design authoring

This draft is the canonical ingestion of the Design by Construction source for
Forma Studio. Ingestion records the obligation; it is not approval or evidence
that the behavior has been implemented.

## Objective and repository scope

Prevent known classes of invalid UI and print designs structurally instead of
depending primarily on post-hoc visual inspection and testing. Forma Studio
MUST author only contract-valid component trees, prevent invalid drops and
properties, generate standards-compliant HTML with Forma tags, surface
actionable diagnostics, and provide an explicit advanced escape hatch.

## Shared requirements

1. Use a versioned machine-readable design contract with explicit compatibility rules.
2. Standard mode constrains semantic layout, alignment, overflow, typography, color, accessibility, motion, and action semantics.
3. Extended mode permits documented semantic overrides; custom mode requires a reason and additional validation evidence.
4. Preserve ordinary HTML/CSS and accessible web semantics; do not make a proprietary designer format mandatory for consumers.
5. Reject structurally invalid compositions before code generation with actionable error messages.
6. Preserve backward compatibility or provide a documented, versioned migration path.
7. Ensure interactive controls can be driven by keyboard and machine tools such as Playwright, and commands have observable meaningful behavior.
8. Treat runtime and visual testing as evidence for properties that contracts cannot guarantee.

## First end-to-end slice

A customer form containing two fields, email validation, a real save command,
and responsive layout. The contract MUST prevent misaligned standard fields,
unsupported layout overrides, missing action bindings, and invalid parent-child
compositions. Verification MUST cover narrow/mobile and desktop viewports and
reduced-motion behavior.

## Expressiveness and flexibility

- Support composable stack, grid, split, dock, flow, canvas, overlay, page, and repeater layout models where applicable; irrelevant renderers need not implement every model.
- Apply context-specific invariants: standard forms align fields, canvases permit deliberate free positioning/overlap, and print pages enforce pagination and safe areas.
- Allow nested compositions, responsive container-based adaptation, dense/comfortable modes, themes, and white labeling.
- Do not hard-code one visual arrangement, navigation pattern, or form column count as universal.
- Support declarative data binding, computed values, validation, conditional visibility, state-dependent commands, and asynchronous feedback.
- Support appropriate drag/drop, reordering, resizing, docking, virtualization, keyboard navigation, and Playwright-operable interactions.
- Provide documented extension points for charts, diagrams, maps, photography, and specialized content without bypassing semantic or safety contracts.
- Distinguish hard structural constraints from subjective design guidance. Unsupported but legitimate designs require a documented, auditable extension path.

## Reference-layout certification

- Maintain a cross-repository reference suite of at least 20 diverse layouts, including a dashboard, complex form, mobile wizard, financial ledger, calendar, kanban board, visual workflow editor, marketing page, article, resume, menu, business card, and journal publication.
- For each reference, record required capabilities, expected rendering modes, accessibility and machine-operability evidence, and whether custom overrides were needed.
- Proposed acceptance targets, which MUST be validated rather than claimed in advance, are: 100% of reference layouts representable through documented APIs; 100% of defined invalid structural fixtures rejected; fewer than 10% of reference layouts requiring custom exceptions.
- Measure agent implementation effort, escape-hatch usage, regressions, and visual quality separately. Visual taste and composition balance remain reviewable guidance, not arbitrary compile failures.

## Acceptance evidence

- Versioned schema and documented supported/unsupported examples.
- Invalid fixtures fail deterministically with specific diagnostics.
- Valid fixtures generate or render correctly on desktop and mobile.
- Keyboard operation, screen-reader semantics, and Playwright operability are demonstrated.
- Regression and migration tests record escape defects and agent correction effort.
- Repository-local work and provenance link implementation evidence to this requirement.

## Dependencies and execution

Contract/schema owners MUST agree on layout-model taxonomy and compatibility
rules before consumer enforcement. Coordinate Forma contract first, then Forma
Studio and Ordo integration, Praxis agent guidance, Visual Engineering evidence,
and Folio print extensions. Visual Engineering owns the certification matrix
and evidence method; Forma Studio MUST exercise constrained authoring and
permitted extensions. Implementation should use narrow, independently validated
slices and MUST NOT be claimed complete until evidence is committed.
