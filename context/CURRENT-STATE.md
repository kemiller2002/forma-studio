# Current state

Date: 2026-09-23

## Repository state

Forma Studio is a greenfield public repository with its engineering foundation established on `feature/bootstrap-requirements` under GitHub issue #1 and draft PR #2.

The branch contains the product requirements baseline, architecture and document model, project schema, linked-page fixture, initial F# engine boundary, marketing-site scaffold, and installed Echelon capability state.

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
- Aegis is the standard unexpected operational-fault mechanism in the F# engine; expected domain refusals and Limen OutcomeUnknown remain distinct concepts.

## Installed capability baseline

See `capabilities.lock.json` and the corresponding `.echelon/*.json` manifests.

Verified bootstrap baseline:
- SDE 1.3.0
- ROS 3.1.4
- Visual Engineering 1.0.0
- Communication Engineering 1.0.0, installed from pinned source commit
- Limen 0.6.2
- Forma 0.1.0 presentation dependency
- EchelonFoundry.Aegis.Core 1.0.0 pinned in the F# engine project

The lifecycle-generated repository state was committed by `chore: install current Echelon engineering capabilities` after the bootstrap runner passed registry build, ROS validation, ROS strict verification, SDE verification, Visual Engineering strict verification, Communication Engineering strict verification, and Limen strict verification.

Visual Engineering intentionally ignores its generated `.visual-engineering/` research context. A clean CI clone materializes that context with `visual-engineering init` before strict verification.

## Requirements baseline

The numbered requirements under `requirements/` cover 16 domains:
- product scope;
- projects/pages/routes/navigation;
- editor/canvas/composition;
- Forma catalog/properties;
- tokens/themes/responsive behavior;
- scenarios/prototyping;
- validation/accessibility;
- persistence/versioning/collaboration;
- export/developer/agent handoff;
- marketing;
- security/privacy/performance;
- testing/releases/operations;
- roadmap/non-goals;
- content/assets/forms;
- reusable compositions/templates/layouts;
- fault handling/recovery/Aegis.

## Open implementation obligations

- Implement the F# project/document model and legal editor transitions beyond the seed types.
- Implement deterministic project parsing, migration, normalization, and validation.
- Define/consume richer machine-readable Forma component-property and slot metadata where the current Forma contract is insufficient.
- Build the editor shell and real Forma component rendering.
- Build page/navigation graph authoring and interactive linked-page preview.
- Build theme/viewport/scenario review.
- Add browser and accessibility test harnesses.
- Add GitHub persistence and semantic conflict handling.
- Add developer/agent export.
- Configure DNS/Pages repository settings for the custom domain if they are not already configured.
- Begin all post-bootstrap implementation through the installed ROS work protocol before meaningful mutation.

## Bootstrap traceability

Issue #1 is the authoritative external record for the greenfield bootstrap task because ROS did not exist when that task began. This is an explicit bootstrap exception, not a retroactive claim that ROS commands were run before installation.

Do not manufacture a retrospective ROS execution record. All subsequent implementation work starts with the installed work protocol and real current timestamps.

## CI model

Normal validation is `.github/workflows/engineering-validation.yml` plus the ROS, Limen, and Pages workflows.

The bootstrap workflow is manual-only recovery/bootstrap tooling and is not part of ordinary pull-request execution.

## Known constraints

- The marketing site is scaffolded and build-validated, but custom-domain DNS/Pages settings are external repository/account configuration.
- The editor implementation itself has not been built yet; this branch establishes the governed requirements, architecture, schema, dependencies, and public-site foundation it will be built against.
