# 10 Security, privacy, and performance

## Security boundary

SPP-001 Project documents MUST be treated as untrusted input on import.

SPP-002 Imported text/content MUST NOT be executed as script.

SPP-003 Studio MUST NOT support arbitrary project-supplied JavaScript in v1.

SPP-004 HTML-like user content MUST be escaped or passed only through explicitly safe/typed content contracts.

SPP-005 External URLs MUST be validated before navigation/export.

SPP-006 URL schemes such as javascript: MUST be rejected.

SPP-007 Studio MUST use a Content Security Policy appropriate to its deployment model.

SPP-008 Studio SHOULD avoid inline script requirements.

SPP-009 Browser capabilities MUST cross Limen rather than being called from F# domain transitions.

SPP-010 Secrets/tokens MUST NOT be stored in project documents.

## GitHub credentials

SPP-020 GitHub authentication material MUST remain outside persisted project state.

SPP-021 Studio MUST request only the repository permissions necessary for the selected operation.

SPP-022 Credentials MUST NOT appear in logs, diagnostics, exported agent packets, or generated previews.

SPP-023 Failed authentication MUST leave local project state intact.

SPP-024 Authorization failure MUST be distinguished from not-found when the provider permits doing so safely.

## Privacy

SPP-040 Studio MUST be usable with synthetic/non-sensitive preview data.

SPP-041 Studio SHOULD warn users not to place production secrets or unnecessary sensitive personal information in design fixtures.

SPP-042 Project exports MUST make fixture/scenario data plainly discoverable so reviewers can inspect what would be shared.

SPP-043 Analytics/telemetry, if added to the product, MUST be documented, minimized, and separable from ROS repository telemetry.

SPP-044 Studio MUST NOT sell or expose project content to advertisers.

SPP-045 External asset loading MUST be explicit and visible because it may disclose network metadata to third parties.

## Asset safety

SPP-060 SVG assets MUST be sanitized before inline rendering or handled as non-executable resources.

SPP-061 Raster image metadata SHOULD be stripped on upload/import where feasible and documented.

SPP-062 Studio SHOULD support project-local or Git-hosted assets before introducing arbitrary third-party asset pipelines.

SPP-063 Missing assets MUST not make the project unreadable; placeholders and findings MUST identify the missing asset.

## Availability and data integrity

SPP-080 Local editor functionality MUST continue when GitHub is unavailable, except operations that require GitHub.

SPP-081 Remote save failure MUST not discard the local project.

SPP-082 Unknown remote write outcome MUST enter reconciliation state.

SPP-083 Automatic retry MUST be limited to operations proven safe/idempotent.

SPP-084 Every external effect MUST carry correlation identity sufficient for stale-result rejection where needed.

## Performance budgets

SPP-100 Performance targets MUST be measured on a documented reference environment before release claims are made.

SPP-101 v1 SHOULD remain interactively usable for a project containing 50 pages and 2,000 component nodes.

SPP-102 Common local editing commands SHOULD complete their F# state transition and projection without perceptible blocking under the reference project.

SPP-103 Long component trees SHOULD use incremental/virtualized editor rendering where needed.

SPP-104 Changing viewport/theme SHOULD not reconstruct unrelated project state.

SPP-105 Validation SHOULD be incremental where dependencies are known.

SPP-106 Expensive whole-project validation MUST be cancellable or clearly progress-reported.

SPP-107 Preview rendering MUST avoid duplicating complete hidden DOM trees for every page unless required for comparison mode.

SPP-108 Large fixture collections SHOULD be bounded and generated lazily.

## Offline/caching

SPP-120 Studio SHOULD cache the pinned Forma assets needed for an open project.

SPP-121 Failure to fetch a noncached pinned Forma version MUST be explicit.

SPP-122 Studio MUST NOT silently render with a different Forma version because the requested one is unavailable.

SPP-123 Future offline/PWA capability MAY be added without changing the project format.

## Dependency policy

SPP-140 Runtime dependencies MUST be minimized.

SPP-141 New framework/runtime dependencies require an architectural justification.

SPP-142 F# owns application authority; JavaScript/TypeScript is limited to Limen/browser mechanics and build/bootstrap needs.

SPP-143 Forma owns presentation; Studio-specific CSS is limited to editor shell/adorners and marketing composition.

SPP-144 Supply-chain-sensitive dependencies MUST be pinned or otherwise governed by repository policy.
