# Unbound dashboard/report authoring capability review (proposed)
Date: 2026-11-23. No implementation or test execution.

Review whether the existing versioned Forma Studio document model can author responsive Unbound dashboards with public Forma patterns, reusable widget slots, saved layout variants, theme previews and permission-aware simulation. Keep F# application authority and Limen browser boundary.

## Requirements to validate
- Typed dashboard composition: widget placeholder, semantic metric binding reference, filters, layout by breakpoint, accessible headings, safe interactions and empty/loading/Unknown previews.
- Preview sponsor branding and roles without using production financial data or claiming authorization.
- Roundtrip deterministic export with pinned Forma release and no Studio-private CSS.
- Version migration, validation findings, unsupported capability registry, publish approval and layout diffs.
- Separate gated report designer scope only when Folio pinned and document/report preview, print layout and renderer constraints are proven.
- Unbound remains authoritative for metrics, permissions, report approval, distribution and retention; Studio owns design authoring only.
## Gates
Inventory current implemented editor capabilities and distinguish requirements from verified code; test representative dashboards and print documents before declaring support.
