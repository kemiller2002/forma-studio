# 06 Validation and accessibility

## Validation model

VA-001 Validation MUST be deterministic for a given project, Forma version, Studio version, and validation profile.

VA-002 Validation findings MUST have stable rule IDs.

VA-003 Findings MUST include severity, location, explanation, and a recommended repair or next investigation when known.

VA-004 Validation MUST distinguish errors, warnings, and informational findings.

VA-005 A validation error MUST block implementation-ready export unless the user selects a clearly labeled diagnostic export.

VA-006 Studio MUST NOT claim a check passed when the check could not be performed.

VA-007 Unavailable checks MUST be reported separately from passing checks.

## Structural validation

VA-020 Validation MUST detect duplicate IDs within their required scope.

VA-021 Validation MUST detect unknown component IDs.

VA-022 Validation MUST detect unsupported component properties.

VA-023 Validation MUST detect missing required component properties/slots.

VA-024 Validation MUST detect broken navigation targets.

VA-025 Validation MUST detect duplicate normalized routes.

VA-026 Validation MUST detect invalid token references.

VA-027 Validation MUST detect scenario overrides targeting missing nodes.

VA-028 Validation SHOULD report unreachable pages.

VA-029 Validation SHOULD report unused project assets.

VA-030 Validation SHOULD report deprecated Forma components.

## Accessibility construction requirements

VA-040 Studio MUST target WCAG 2.2 AA for the Studio editor itself and for checks it can reliably apply to rendered specifications.

VA-041 Every interactive Studio feature MUST have a keyboard-operable path.

VA-042 Focus indication MUST be visible and must not rely on color alone.

VA-043 Studio MUST respect reduced-motion preferences.

VA-044 Studio MUST preserve browser zoom and text resizing.

VA-045 Studio MUST not disable pinch zoom in mobile layouts.

VA-046 The editor MUST expose accessible names for controls and component-tree items.

VA-047 Drag/drop operations MUST have non-drag alternatives.

VA-048 Validation findings MUST be understandable without color alone.

VA-049 Canvas selection MUST have a programmatic equivalent in the component tree.

VA-050 Screen-reader users MUST be able to understand page/component hierarchy without entering the visual canvas.

## Specification accessibility checks

VA-060 Studio MUST check required accessible-name metadata when the Forma contract requires it.

VA-061 Studio SHOULD check heading structure based on rendered semantic HTML.

VA-062 Studio SHOULD check landmark presence/duplication where meaningful.

VA-063 Studio SHOULD run automated contrast checks against resolved theme tokens.

VA-064 Studio MUST test contrast separately for every required theme.

VA-065 Studio SHOULD identify images/SVG usages missing required text alternatives.

VA-066 Studio SHOULD detect empty link/button names.

VA-067 Studio SHOULD detect duplicate HTML IDs produced by content/configuration.

VA-068 Studio SHOULD detect unreachable or obviously invalid focus targets.

VA-069 Studio SHOULD detect interactive targets clipped or hidden at required viewports.

VA-070 Studio MUST include the 320px viewport in responsive accessibility validation.

VA-071 Automated accessibility findings MUST state that automated checking is not proof of total accessibility.

## Vision and color

VA-080 Information MUST NOT rely solely on hue.

VA-081 Error/success/warning states MUST expose text/icon/shape cues in addition to color.

VA-082 Studio SHOULD support grayscale/color-vision simulation as an optional review aid.

VA-083 Simulation MUST be labeled as a visual aid, not as proof of accessibility for a disability group.

## Reading/cognitive accessibility

VA-100 Studio SHOULD encourage clear labels and concise supporting text without forcing one writing style.

VA-101 Communication guidance MUST be evidence-bounded and come from installed Communication Engineering context.

VA-102 Studio SHOULD expose content-density and reading-stress review scenarios without claiming diagnosis of dyslexia or cognitive conditions.

VA-103 User-facing errors MUST identify what happened, what remains true, and the next available action where known.

## Validation UX

VA-120 Findings MUST be filterable by severity, page, component, rule, and category.

VA-121 Selecting a finding MUST navigate to the affected page/node when available.

VA-122 Fix actions MAY be offered only when the repair is deterministic and does not invent domain meaning.

VA-123 Automatic fixes MUST participate in undo/redo.

VA-124 Users MUST be able to suppress a warning only with a recorded reason; errors required for schema integrity MUST not be suppressible.

VA-125 Suppression records MUST survive export/import.

## Continuous validation

VA-140 Fast local validations SHOULD run incrementally after relevant commands.

VA-141 Expensive browser/accessibility validations MAY run on demand or debounced.

VA-142 CI MUST support headless validation of exported project files.

VA-143 Validation rules MUST have unit tests and representative positive/negative fixtures.
