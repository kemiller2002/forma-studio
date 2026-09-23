# Forma Studio project charter

## Mission

Build a visual application specification environment in which people compose real Forma components into responsive pages, link those pages into navigable flows, validate the result, and persist a typed specification that developers and agents can implement without reconstructing intent from screenshots.

## Product boundary

Forma Studio owns visual specification authoring and review.

It does not own the business-domain state of applications being designed, and it does not fork Forma component implementation.

## Architectural commitments

- F# owns Studio application state and legal transitions.
- Limen is the browser capability boundary.
- Forma is the presentation/component authority.
- SDE/Ordo governs explicit state, legal actions, obligations, unknown effects, and evidence.
- ROS governs durable work and repository evidence.
- Visual Engineering and Communication Engineering are installed operational contexts.
- Runtime/framework dependencies remain minimal.
- The saved project is authoritative; DOM/canvas are projections.
- Mobile and accessibility are first-class requirements.

## Initial deliverables

1. Governed repository and capability installation.
2. Complete requirements baseline.
3. Versioned project schema.
4. Linked multi-page project fixture.
5. Echelon-styled public marketing site at forma-studio.echelonfoundry.com.
6. F# domain/editor model.
7. Real Forma component catalog and rendering.
8. Multi-page navigation graph authoring and preview.
9. Responsive/theme/scenario validation.
10. Machine-readable developer/agent handoff.

## Success criteria for the first product baseline

A user can create a multi-page project visually, compose canonical Forma patterns, configure allowed properties, link pages, preview the real responsive rendering, validate it, save/reopen it, and export a machine-readable artifact with no screenshot interpretation required to determine component/page/navigation intent.

## Evidence discipline

Do not publish quantitative productivity, quality, accessibility, cost, or agent-accuracy claims without measured evidence and stated limitations.

## Work item

Initial bootstrap and requirements work is tracked by GitHub issue #1 until ROS installation creates the repository-native work record.
