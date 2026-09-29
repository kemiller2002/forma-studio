# Forma Studio requirements

This directory is the product requirements baseline for Forma Studio.

The requirements are intentionally broader than a first implementation sprint. They define the full product boundary so later slicing does not accidentally create incompatible local solutions.

## Requirement sets

- 00-product-scope.md
- 01-projects-pages-navigation.md
- 02-editor-canvas-composition.md
- 03-forma-catalog-properties.md
- 04-tokens-responsive-themes.md
- 05-scenarios-prototyping.md
- 06-validation-accessibility.md
- 07-persistence-versioning-collaboration.md
- 08-export-handoff-agents.md
- 09-marketing-site.md
- 10-security-privacy-performance.md
- 11-testing-release-operations.md
- 12-roadmap-nongoals.md
- 13-content-assets-forms.md
- 14-reuse-templates-layouts.md
- 15-faults-recovery-aegis.md
- 16-flow-diagram-authoring.md

## Execution scope

This requirements baseline defines the durable product boundary, not one implementation sprint.

For Flow work, agents MUST use `12-roadmap-nongoals.md` plus the acceptance/proof requirements in `16-flow-diagram-authoring.md` to determine the current implementation slice. The existence of deferred, conditional, SHOULD, or MAY requirements is not authorization to implement all of them at once.

The first architecture proof remains deliberately small:

- Layout: Stack -> Heading -> edit -> reorder -> spacing -> undo/redo -> save/reload.
- Flow: two nodes -> connect -> move -> label -> undo/redo -> save/reload.

Later capabilities must reuse the proven shared editor/document/command architecture rather than expanding the first slice opportunistically.

## Normative language

MUST and MUST NOT are required for the stated scope.
SHOULD and SHOULD NOT require an explicit recorded reason to deviate.
MAY is optional.

## Product invariant

A saved Forma Studio project is a machine-readable visual application and diagram specification.

Layout and Flow canvases are projections of that specification and share one editor core.

The DOM is never the authoritative project state.
