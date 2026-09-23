# Forma Studio

Forma Studio is a visual application specification environment for building pages from real Forma components, linking those pages into navigable flows, previewing the actual browser result, and exporting a typed specification for developers and agents.

Public marketing URL: https://forma-studio.echelonfoundry.com/

## Core idea

The project document is authoritative.

The canvas is a projection.

Forma owns presentation. F# owns Studio state and legal editing transitions. Limen owns browser capabilities. Ordo/SDE and ROS govern engineering state and repository work.

A design should be understandable without reverse-engineering screenshots.

## Current baseline

The greenfield baseline is tracked by GitHub issue #1.

Pinned capability versions are recorded in capabilities.lock.json:

- SDE / Ordo 1.3.0
- ROS 3.1.4
- Visual Engineering 1.0.0
- Communication Engineering current 1.0.0 source pinned by commit
- Limen 0.6.2
- Forma 0.1.0
- Aegis Core 1.0.0

Lifecycle-managed capability state is committed in the repository. `scripts/bootstrap-capabilities.sh` and the manual-only bootstrap workflow provide reproducible repair/reinstallation; normal changes use the installed lifecycle verification workflows.

## Requirements

The complete product baseline is under requirements/.

It covers:

- product scope and non-goals;
- projects/pages/routes/navigation graph;
- editor/canvas/component-tree behavior;
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

examples/two-page-project.json demonstrates two pages connected by an explicit internal navigation action.

Internal navigation targets stable page IDs. Routes are projections and may change without breaking links.

## Engine boundary

`src/engine/FormaStudio.Engine` is the initial F# application-authority project and pins `EchelonFoundry.Aegis.Core` 1.0.0.

`src/engine` and `src/kernel` are the enforced Limen boundary directories. Browser capabilities do not belong in the engine.

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
