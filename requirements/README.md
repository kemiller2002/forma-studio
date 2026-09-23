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

## Normative language

MUST and MUST NOT are required for the stated scope.
SHOULD and SHOULD NOT require an explicit recorded reason to deviate.
MAY is optional.

## Product invariant

A saved Forma Studio project is a machine-readable visual application specification.

The rendered canvas is a projection of that specification.

The DOM is never the authoritative project state.
