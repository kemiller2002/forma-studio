# 05 Preview scenarios and prototyping

## Scenario purpose

SP-001 A scenario MUST represent preview data/presentation conditions, not business-domain truth.

SP-002 Scenarios MUST be plain serializable data.

SP-003 A project MUST be usable without scenarios.

SP-004 A project SHOULD provide a Default scenario when nontrivial fixture data is required.

SP-005 Scenario application MUST be deterministic.

SP-006 Selecting a scenario MUST NOT mutate canonical component configuration unless the user explicitly promotes an override.

## Scenario lifecycle

SP-020 Users MUST be able to add, rename, duplicate, reorder, and delete scenarios.

SP-021 Every scenario MUST have a stable scenario ID.

SP-022 A scenario MAY be project-wide or scoped to selected pages/components.

SP-023 Scenario deletion MUST identify saved preview/bookmark references that depend on it.

SP-024 Scenario overrides MUST reference stable node IDs.

SP-025 An override for a missing node MUST be reported, not ignored.

## Built-in stress scenarios

SP-040 Studio SHOULD provide assisted creation for common scenarios: loading, empty, error, validation-error, network-failure, long-content, minimal-content, permission-limited presentation, and mobile-stress.

SP-041 Built-in scenarios MUST be templates that create explicit project scenario data rather than hidden editor magic.

SP-042 Studio SHOULD support internationalized/long-string stress content.

SP-043 Studio SHOULD support large-collection fixture generation with bounded sizes.

SP-044 Generated fixture data MUST be clearly synthetic.

SP-045 Synthetic data generators MUST NOT create realistic secrets, credentials, or sensitive personal data.

## Interactive prototyping

SP-060 Preview MUST support internal page navigation defined by the project navigation graph.

SP-061 Preview SHOULD support canonical native interactions that Forma/browser HTML already provides.

SP-062 Studio MAY model visual open/closed/selected/expanded states as scenario values when those are presentation-level states.

SP-063 Studio MUST NOT create hidden business logic to make a prototype appear functional.

SP-064 Unsupported behavior MUST be visibly marked as noninteractive rather than silently approximated.

SP-065 Preview MUST distinguish editor selection from simulated application focus/selection.

SP-066 Keyboard navigation in preview MUST exercise the real rendered controls.

## State inspection

SP-080 The editor SHOULD expose active scenario values affecting the selected component.

SP-081 A developer view SHOULD show which values come from canonical component config versus scenario overrides.

SP-082 Preview should make unresolved/unknown values visually identifiable without rendering them as false certainty.

SP-083 Scenario validation SHOULD detect unused overrides and missing targets.

## Comparison

SP-100 Users SHOULD be able to compare at least two scenarios for the same page.

SP-101 Users SHOULD be able to compare two viewport/theme combinations for the same page.

SP-102 Comparison mode MUST render the same canonical component tree under different projections; it MUST NOT duplicate the page in saved state.

SP-103 Visual-difference tooling MAY be added later but MUST distinguish expected theme/responsive differences from regressions.

## Acceptance

SP-120 A five-page example project MUST be previewable under Default, Loading, Empty, Error, and Long Content scenarios without editing page structure.

SP-121 Switching scenarios MUST not alter the exported canonical page tree.

SP-122 A broken scenario override MUST identify scenario ID, node ID, and property/path.
