# Current state

Date: 2026-09-23

## Repository state

Forma Studio is a greenfield public repository.

The initial requirements baseline, architecture documents, project schema, linked-page fixture, marketing-site scaffold, and capability lock are being established under GitHub issue #1.

## Confirmed decisions

- Product name: Forma Studio.
- Marketing domain: https://forma-studio.echelonfoundry.com/.
- Forma is the presentation authority.
- F# is the application-authority implementation language.
- Limen is the browser boundary.
- The canonical project artifact is typed JSON/specification data, not pixels.
- Projects support multiple pages and an explicit page-navigation graph.
- Internal links target stable page IDs; routes are projections.
- Git/local-file persistence precedes any hosted database requirement.
- Figma is optional interoperability, not a dependency.
- General-purpose vector editing is outside v1.

## Capability baseline

See capabilities.lock.json.

Target baseline:
- SDE 1.3.0
- ROS 3.1.4
- Visual Engineering 1.0.0
- Communication Engineering main package reporting 1.0.0, pinned by commit for lifecycle bootstrap
- Limen 0.6.2
- Forma 0.1.0

## Open implementation obligations

- Complete lifecycle bootstrap and verify every capability.
- Convert GitHub issue #1 into/associate it with ROS-native work state after ROS installation.
- Implement the F# domain model and migration/validation core.
- Define/consume richer machine-readable Forma component-property metadata where the existing Forma contract is insufficient.
- Build the editor shell and real component rendering.
- Build navigation graph authoring and preview.
- Add browser/accessibility test harnesses.
- Configure DNS/Pages repository settings for the custom domain if not already present.

## Known constraints

The current environment can mutate GitHub repository content but cannot execute npm packages locally. Capability lifecycle installation is therefore delegated to a one-time GitHub Actions bootstrap using the official CLIs; success must be verified from workflow/committed installation evidence rather than assumed.
