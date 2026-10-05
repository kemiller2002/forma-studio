# Forma Studio

Forma Studio is a visual application and diagram specification environment for building pages from real Forma components, authoring connected workflow/diagram surfaces, previewing the actual result, and exporting a typed specification for developers and agents.

Public marketing URL: https://forma-studio.echelonfoundry.com/

## Core idea

The project document is authoritative.

The canvas is a projection.

Forma owns presentation. F# owns Studio state and legal editing transitions. Limen owns browser capabilities. Ordo/SDE and ROS govern engineering state and repository work.

A design or diagram should be understandable without reverse-engineering screenshots.

## Current baseline

The greenfield baseline is tracked by GitHub issue #1.

Pinned capability versions are recorded in capabilities.lock.json:

- SDE / Ordo 1.3.0
- ROS 3.1.4
- Visual Engineering 1.0.0
- Communication Engineering current 1.0.0 source pinned by commit
- Limen 0.7.0 (`@echelon-foundry/limen`)
- Forma 0.1.0
- Aegis Core 1.0.0

Lifecycle-managed capability state is committed in the repository. `scripts/bootstrap-capabilities.sh` and the manual-only bootstrap workflow provide reproducible repair/reinstallation; normal changes use the installed lifecycle verification workflows.

## Requirements

The complete product baseline is under requirements/.

`REQUIREMENTS.md` is the normative shared-capability contract shared with Aegis, Forma, and Folio. Forma Studio MUST dogfood Forma for its own interactive UI, MUST adopt Aegis when operational boundaries are introduced, and MUST adopt Folio when printable/PDF/paginated document composition or Folio authoring/testing enters scope.

It covers:

- product scope and non-goals;
- projects/pages/routes/navigation graph;
- shared editor/canvas/Structure-and-Outline behavior;
- Flow/diagram graph authoring with nodes, connectors, groups, swimlanes, and typed profiles;
- Forma catalog/property contracts;
- tokens/themes/responsive preview;
- scenarios and interactive prototypes;
- accessibility and validation;
- persistence/versioning/Git collaboration;
- developer and agent handoff;
- public marketing site;
- security/privacy/performance;
- testing/release/operations;
- roadmap and deferred work;
- content, assets, and forms;
- reusable compositions, layouts, and templates;
- Aegis fault handling and recovery.

Run:

~~~bash
node scripts/validate-requirements.mjs
node scripts/validate-schema.mjs
~~~

## Canonical project format

The initial schema is schemas/forma-studio.schema.json.

examples/two-page-project.json demonstrates two pages connected by an explicit internal navigation action. The Flow requirements define the next schema extension for canonical diagrams/graphs; the bootstrap schema is not silently changed until that governed implementation work executes.

Internal navigation targets stable page IDs. Routes are projections and may change without breaking links.

## Engine boundary

`src/engine/FormaStudio.Engine` is the initial F# application-authority project and pins `EchelonFoundry.Aegis.Core` 1.0.0.

`src/engine` and `src/kernel` are the enforced Limen boundary directories. Browser capabilities do not belong in the engine.

## Portable workflows and HTML export

- **Workflows.** Studio opens, edits and saves Forma's portable
  `.forma-workflow.json` documents with the public `<forma-workflow>` component.
  See [`docs/WORKFLOWS.md`](docs/WORKFLOWS.md).
- **HTML export.** Studio exports deterministic HTML fragments and complete
  documents that use only public Forma markup and assets. See
  [`docs/HTML-EXPORT.md`](docs/HTML-EXPORT.md) and
  [`examples/html-consumer`](examples/html-consumer).
- **Evidence.** [`docs/evidence/`](docs/evidence/).
- **Decision.** [`DF-STUDIO-2026-C4A2`](research/decisions/DF-STUDIO-2026-C4A2--portable-workflows-and-html-export.md).

~~~bash
npm run engine:test      # F# engine, workflow library and HTML export tests
npm run html:check       # export determinism
npm run app:build && npm run app:test   # editor, workflows, round trip, HTML consumer (Chromium)
npm run proof:external   # records docs/evidence/external-workflow-roundtrip.md
npm run vendor:check     # vendored Forma matches its recorded commit
npm run vendor:release   # ... and the published Forma 0.4.0 packages (network)
~~~

## Marketing site

site/ contains the static public marketing source.

It consumes the pinned Forma CSS during build and uses a small local shell stylesheet for page composition.

~~~bash
npm install --ignore-scripts
npm run site:build
~~~

The Pages workflow publishes site-dist and preserves site/CNAME for forma-studio.echelonfoundry.com.

## Architecture

Start with:

- PROJECT-CHARTER.md
- context/CURRENT-STATE.md
- architecture/ARCHITECTURE.md
- architecture/DOCUMENT-MODEL.md
- requirements/README.md

All new implementation work must follow the installed AGENTS.md / ROS / SDE work protocol before meaningful mutation.
