# 09 Marketing site

## Domain and deployment

MS-001 The public marketing site MUST publish at https://forma-studio.echelonfoundry.com/.

MS-002 The site MUST be deployable through GitHub Pages from this repository.

MS-003 The repository MUST contain the custom-domain CNAME artifact.

MS-004 The marketing site MUST remain usable if the Studio WebAssembly application fails to load.

MS-005 The editor MAY initially live at /app/ on the same origin.

## Brand

MS-020 The site MUST follow the current Echelon Foundry visual language.

MS-021 The site MUST consume Forma for shared presentation rather than independently recreating a competing design system.

MS-022 Primary surfaces SHOULD use Echelon charcoal/carbon, forged-iron, parchment/stone, bronze, and verdigris semantic treatments as supplied by Forma.

MS-023 Display typography SHOULD use the Echelon/Forma display family and body typography SHOULD use the Echelon/Forma sans family.

MS-024 The visual tone MUST remain restrained, technical, editorial, and evidence-oriented rather than startup-gradient/marketing-template styling.

MS-025 The site MUST support Light/Dark behavior consistent with Forma when available.

MS-026 The site MUST be mobile-friendly down to 320px.

## Content

MS-040 The hero MUST identify Forma Studio as a visual application specification environment.

MS-041 The hero MUST explain that Studio renders actual Forma components and saves machine-readable specifications.

MS-042 The site MUST explain the distinction between Forma Studio and a general-purpose vector editor.

MS-043 The site MUST explain multi-page composition and explicit page linking.

MS-044 The site MUST explain responsive preview using real browser layout.

MS-045 The site MUST explain developer/agent handoff without claiming unmeasured productivity gains.

MS-046 The site MUST explain Forma, Limen, and F#/Ordo boundaries at a high level.

MS-047 The site SHOULD include an architecture diagram using semantic HTML/CSS/SVG rather than a raster-only image.

MS-048 The site SHOULD show a real sample project flow with at least three linked pages.

MS-049 The site SHOULD include a comparison table focused on capabilities, not competitive scores or unsupported superiority claims.

MS-050 The site MUST link to the Forma documentation.

MS-051 The site SHOULD link to source/repository and product documentation once those surfaces are ready.

## Evidence and claims

MS-060 Marketing copy MUST distinguish architectural consequences from measured outcomes.

MS-061 The site MUST NOT publish invented speed, token, cost, quality, or accessibility-improvement percentages.

MS-062 Any quantitative claim MUST have an explicit evidence source and limitation.

MS-063 The site SHOULD state known limitations and current development status.

## Accessibility and performance

MS-080 The marketing site MUST meet the Studio accessibility requirements applicable to static content.

MS-081 The site MUST use semantic landmarks and heading order.

MS-082 Navigation MUST be keyboard accessible.

MS-083 The page MUST not depend on JavaScript for essential marketing content.

MS-084 Images MUST include appropriate alternative text or be marked decorative.

MS-085 The site SHOULD minimize third-party runtime dependencies.

MS-086 Fonts SHOULD use performant loading/fallback behavior.

MS-087 The initial marketing page SHOULD be static HTML/CSS plus optional progressively enhanced examples.

## Information architecture

MS-100 Initial single-page sections SHOULD include: navigation, hero, what it is, why specification not pixels, multi-page flow, real Forma rendering, responsive/accessibility, developer/agent handoff, architecture, limitations/status, and call to action.

MS-101 Future documentation pages MAY be added without changing the root marketing URL.

MS-102 The marketing page SHOULD expose a clear path to launch/open Studio when an editor build is available.

## Visual acceptance

MS-120 A reviewer familiar with echelonfoundry.com SHOULD recognize the site as part of the same family without requiring the pages to be visually identical.

MS-121 The site MUST avoid visual drift caused by copied stale CSS; shared values SHOULD come from the pinned Forma package.

MS-122 Responsive screenshots at 320, 768, and 1440 widths MUST be reviewed in CI/browser tests once implementation begins.
