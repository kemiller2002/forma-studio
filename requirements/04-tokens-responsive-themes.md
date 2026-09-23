# 04 Tokens, themes, and responsive behavior

## Token authority

TRT-001 Forma design tokens MUST be the default token authority.

TRT-002 Studio MUST consume token identifiers and aliases rather than copying resolved values into project documents.

TRT-003 Studio MUST preserve primitive/semantic alias relationships for inspection where Forma exposes them.

TRT-004 Components SHOULD bind to semantic tokens rather than primitive colors.

TRT-005 Studio MUST not create a private token with the same semantic name but a different meaning.

TRT-006 Project-local theme extensions, if later allowed, MUST live in an explicit namespace and MUST NOT masquerade as canonical Forma tokens.

## Theme preview

TRT-020 Studio MUST preview every theme/mode supplied by the pinned Forma version.

TRT-021 Light and Dark MUST be supported when supplied by Forma.

TRT-022 Theme switching MUST change real CSS/token resolution, not approximate colors in the editor.

TRT-023 Theme selection for preview MUST be editor state unless the project explicitly specifies a default application theme.

TRT-024 Validation SHOULD run relevant visual/accessibility checks across every supported theme.

TRT-025 Components MUST not require duplicate page definitions for Light and Dark.

## Viewports

TRT-040 Studio MUST provide named viewport presets including 320, 375, 768, 1024, and 1440 CSS pixels.

TRT-041 Studio MUST provide a custom-width viewport.

TRT-042 Height MUST be configurable independently for scroll/above-the-fold review.

TRT-043 Viewport changes MUST resize the actual preview container.

TRT-044 Viewport settings MUST NOT modify the project unless saved as page preview metadata.

TRT-045 Studio MUST support portrait and landscape review.

TRT-046 Studio SHOULD expose safe-area simulation for mobile devices without pretending to be a full device emulator.

## Responsive contract

TRT-060 Studio MUST prefer intrinsic/browser/Forma responsive behavior over breakpoint-specific editor overrides.

TRT-061 Studio MUST NOT offer a breakpoint override for a property unless the project format and component contract explicitly support it.

TRT-062 If a layout fails at a viewport, Studio SHOULD identify the smallest width at which the failure appears.

TRT-063 Horizontal overflow caused by a project composition SHOULD be a validation finding unless explicitly allowed by the component contract.

TRT-064 Text clipping, inaccessible off-screen controls, and overlapping interactive targets SHOULD be validation findings.

TRT-065 Studio MUST test the canonical 320px baseline.

TRT-066 Responsive validation MUST include zoom/text enlargement considerations where automation can reliably assess them.

## Token browser

TRT-080 Studio MUST provide a token browser organized by semantic role.

TRT-081 The token browser SHOULD expose primitive aliases for developers while steering ordinary component authoring toward semantic tokens.

TRT-082 Token search MUST support canonical name and code syntax.

TRT-083 A token detail view SHOULD show type, modes, aliases, resolved values, and source package version.

TRT-084 Color tokens MUST include accessible text equivalents/labels; swatches cannot be the only representation.

TRT-085 Spacing/radius tokens SHOULD have visual examples generated from the real value.

## Project theme configuration

TRT-100 A project MAY specify which canonical themes are allowed for its target application.

TRT-101 Disabling a theme MUST be explicit project metadata and MUST NOT delete token information.

TRT-102 A project SHOULD be able to specify its initial/default theme separately from the Studio preview theme.

TRT-103 Studio MUST warn if a page contains presentation that only works in one required theme.

## Future extensibility

TRT-120 The document format SHOULD allow future brand/theme packs without changing page/component identity.

TRT-121 Theme migration MUST be versioned.

TRT-122 Theme packs MUST define accessibility/contrast validation obligations.

TRT-123 Arbitrary user CSS is out of v1 scope because it defeats deterministic validation.
