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
- Projects support page-only, diagram-only, and mixed authored surfaces.
- Layout pages use semantic Forma composition; Flow diagrams use explicit graph topology and authored spatial geometry.
- Layout and Flow share one editor command/selection/history/persistence architecture.
- Projects support multiple pages and an explicit page-navigation graph.
- Internal links target stable page IDs; routes are projections.
- Git/local-file persistence precedes any hosted database requirement.
- Figma is optional interoperability, not a dependency.
- General-purpose vector/freehand editing is outside v1.
- General, Workflow, State, and Architecture diagram profiles are the intended initial Flow profile family.
- Typed diagrams remain specifications; they do not become executable business logic without a separate explicit runtime/generation contract.
- Reusable production diagram presentation remains Forma-owned; the current missing public diagram presentation family is tracked by kemiller2002/forma#52.
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

The numbered requirements under `requirements/` cover 17 domains:
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
- fault handling/recovery/Aegis;
- Flow/diagram authoring.

## Open implementation obligations

- Implement the F# project/document model and legal editor transitions beyond the seed types.
- Implement deterministic project parsing, migration, normalization, and validation.
- Define/consume richer machine-readable Forma component-property and slot metadata where the current Forma contract is insufficient.
- Build the editor shell and real Forma component rendering.
- Extend the project schema/domain model with Diagram/Node/Edge/Profile/Group/Lane geometry/topology contracts.
- Implement the shared editor command core needed by both Layout and Flow vertical slices.
- Implement the first Flow vertical slice: two nodes, connection, movement, label, undo/redo, save/reload.
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

## Implementation state (2026-09-30, branch `claude/forma-studio-design-authoring-kv77ea`, PR #12)

This supersedes the "Open implementation obligations" and "Known constraints"
sections above where they differ.

Done and tested:

- **STUDIO-GH-9 (complete):** a pure F# canonical model and shared command
  core in `src/engine/FormaStudio.Engine`. It covers typed identity,
  geometry, pages, diagrams, profiles (General, Workflow), metadata,
  references, palette, styles, mappings and overrides; one command surface,
  one Editor session, the appearance cascade and disclosure; the schema v2
  codec and v1 migration. Decision: `DF-STUDIO-2026-B7E1`.
- **STUDIO-GH-10 (complete):** `AgentExport` and `Projection` (Folio
  handoff 1.0.0, Forma contract 2.0.0), the CLI in `tools/FormaStudio.Cli`,
  and the cross-repo fixture in `examples/purchase-request/`, which Folio
  consumes.
- **STUDIO-GH-11 (active):** the Flow editor. The engine runs on .NET
  WebAssembly (`src/wasm`) behind the Limen kernel (`src/kernel`). It has
  - a canvas with drag and arrow-key moves, and resize by corner handle or
    inspector Width/Height (one command per gesture);
  - a Structure list with multi-selection toggles;
  - an inspector for label, lane, metadata (Mixed across a selection),
    fill, palette and style, with the effective source shown;
  - align and distribute;
  - a new-field form (no JSON), palette management with usage counts and
    safe deletion, and color rules;
  - non-drag connect; reconnect by dragging a selected connector's endpoint
    handle onto a node, or by pressing the handle and choosing a node;
  - zoom (50-200%) and 8-unit grid snapping as view state outside the
    project and its history; drags convert screen pixels to logical units;
  - a Templates panel: built-in fragments per profile (`Templates.fs`, each
    authored through commands and extracted) and "Copy selection as
    template"; the dependency plan is shown before insert, and insert is
    one batch placed below the diagram;
  - undo/redo, and save/open via Limen Storage;
  - axe and forced-colors checks;
  - a Layout surface on the same session: a surface navigator switches
    between the Flow diagram and Layout pages; pages compose a Forma stack
    of headings with text editing, reordering and contract-bounded density
    (no x/y). One undo history spans both surfaces, and a command that
    changes nothing adds no history entry.
- **STUDIO-GH-13 (active):** State and Architecture profiles, `Diff.between`,
  `Merge.three` (graph-aware conflicts), `Fragment` (dependency closure and
  reconciliation by id), and a 2,000-node / 3,000-edge proof.
- Tests: `dotnet run --project tests/FormaStudio.Engine.Tests -c Release`
  (51) and `npm run app:build && npm run app:test` (20 Playwright tests,
  Chromium).

Next legal work, in order:

1. Editor refinements: alignment guides while dragging.
2. Surface the semantic diff and merge in a review UI (read-only compare of
   two revisions).
3. Replace `vendor/forma` with a released Forma artifact once
   kemiller2002/forma#91 ships.

Known constraints: the WebAssembly bundle is about 47 MB untrimmed (trimming
is off because F# formatting uses reflection); editor browser tests run in
Chromium only.
