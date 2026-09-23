# 11 Testing, release, and operations

## Test layers

TRO-001 The F# domain/editor transition layer MUST have unit tests independent of a browser.

TRO-002 Document parsing, validation, normalization, and migration MUST have deterministic tests.

TRO-003 Navigation graph rules MUST have unit tests including cycles, broken links, route conflicts, and unreachable pages.

TRO-004 Undo/redo MUST have property/invariant tests for representative commands.

TRO-005 Component-contract adapters MUST be tested against the pinned Forma package.

TRO-006 Limen boundary tests MUST verify application authority does not migrate into browser code.

TRO-007 Browser tests MUST exercise real DOM rendering of representative Forma compositions.

TRO-008 Accessibility tests MUST use automated tooling plus explicit manual-review obligations.

TRO-009 Marketing-site responsive tests MUST include 320, 768, and 1440 widths.

TRO-010 Editor responsive tests MUST include at least 320, tablet, and desktop widths.

## Project fixtures

TRO-020 The repository MUST contain a minimal linked-pages project fixture.

TRO-021 The repository SHOULD contain a kitchen-sink project using every supported Forma pattern.

TRO-022 The repository SHOULD contain invalid fixtures for each structural validation rule.

TRO-023 Migration tests MUST retain fixtures for every supported historical schema version.

TRO-024 Stress fixtures SHOULD include long content, large page counts, deep nesting, and large collections.

## Golden/snapshot discipline

TRO-040 Snapshot tests MUST not replace semantic assertions.

TRO-041 Visual screenshots MAY detect regressions but must be reviewed against intended responsive/theme behavior.

TRO-042 Generated markup snapshots SHOULD normalize irrelevant attributes/order before comparison.

TRO-043 A screenshot alone is insufficient evidence that navigation or accessibility works.

## CI

TRO-060 Pull requests MUST run requirements validation, schema/example validation, F# tests once implementation exists, Limen architecture verification, browser tests, accessibility tests, and marketing-site build tests as applicable.

TRO-061 CI MUST verify installed SDE/ROS/Visual Engineering/Communication Engineering/Limen lifecycle state.

TRO-062 CI MUST fail on requirements with duplicate IDs.

TRO-063 CI MUST fail if the example project no longer conforms to the canonical schema.

TRO-064 CI MUST fail on broken internal marketing-site links.

TRO-065 CI MUST not publish from a failing build.

## Releases

TRO-080 Studio versions MUST be explicit and traceable to repository commits.

TRO-081 Project schema version and Studio product version MUST be distinct concepts.

TRO-082 Forma dependency upgrades MUST be explicit release changes.

TRO-083 Limen upgrades MUST run boundary and browser regression tests.

TRO-084 Release notes MUST identify schema changes, migration behavior, Forma compatibility, and known limitations.

TRO-085 Breaking project-format changes require a schema migration path or an explicit unsupported-version decision.

## GitHub Pages

TRO-100 The marketing site MUST deploy from generated site-dist output.

TRO-101 The Pages workflow MUST preserve the CNAME for forma-studio.echelonfoundry.com.

TRO-102 Pull requests SHOULD build the Pages artifact without deploying.

TRO-103 Pushes to main MAY deploy after validation.

TRO-104 Deployment MUST be reproducible from repository content and pinned dependencies.

## Observability

TRO-120 User-facing application diagnostics MUST avoid secrets/project-sensitive content by default.

TRO-121 ROS development telemetry is repository-operational evidence and MUST not be conflated with end-user product analytics.

TRO-122 Client failures SHOULD include stable error codes suitable for support/debugging.

TRO-123 External-effect failures SHOULD retain provider-safe diagnostic context and correlation IDs.

## Definition of done for a feature

TRO-140 Requirements and acceptance cases exist.

TRO-141 Legal editor/domain transitions are represented explicitly.

TRO-142 Unit tests cover success, refusal, boundary, and unknown/error cases.

TRO-143 Browser behavior is tested when the feature crosses the browser boundary.

TRO-144 Accessibility impact is reviewed.

TRO-145 Responsive/mobile impact is reviewed.

TRO-146 Documentation/agent contract is updated.

TRO-147 ROS work item contains completion evidence.

TRO-148 Generated registries are current.

TRO-149 CI passes on the revision proposed for merge.
