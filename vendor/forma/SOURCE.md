# Vendored Forma (release 0.5.0)

Everything in this directory is copied verbatim from kemiller2002/forma by
`node scripts/sync-forma.mjs <forma checkout>`. `vendor/forma/MANIFEST.json`
records the source commit, the SHA-256 of every file, and for each file a
release package ships, its path in that package.
`node scripts/sync-forma.mjs --check` (run by `npm run check`) fails if a file
drifts; `--release` (run in CI) also fails unless every packaged file is
byte-identical to the published `@echelon-foundry/design-system@0.5.0` and
`@echelon-foundry/forma-workflow@1.0.0` tarballs.

| Path | Forma source | Used for |
|---|---|---|
| `tokens.css`, `foundations.css`, `components.css`, `brands/*.css` | `dist/` (built with `npm run build`) | Studio's own UI and export previews |
| `workflow/src/*.fs` | `src/workflow/Forma.Workflow` | the public workflow model, validation, layout, rendering and editor engine, compiled into Studio's engine |
| `workflow/src/Forma.Workflow.fsproj` | the upstream project file, with resource paths pointing at the vendored contracts | |
| `workflow/forma-workflow.schema.json`, `workflow/workflow-capabilities.json`, `workflow/diagram-presentation.json` | `schemas/`, `contracts/` | the published contracts |
| `workflow/browser/forma-workflow.js`, `.css` | `packages/workflow/src` | the public `<forma-workflow>` element, driven by Studio's transport |
| `workflow/fixtures/*.forma-workflow.json` | `examples/workflows/forma/workflows` | compatibility fixtures |
| `workflow/external-producer/*.mjs` | `examples/external-producer` | the external producer and consumer in the round-trip proof |

Source: the kemiller2002/forma `main` commit that released
`@echelon-foundry/design-system` 0.5.0 (merged PR #114, 8a5a599, tag v0.5.0;
`dist/` files are the release tarball's). Its
workflow sources, contracts and fixtures are unchanged from 0.4.0, which
released `@echelon-foundry/forma-workflow` 1.0.0, so the workflow contract
still reports Forma 0.4.0. Studio's engine compiles the `Forma.Workflow` F#
source, which no Forma package ships, so the source stays vendored from that
release commit. The `workflow/src` files and the external producer example are
verified against the commit only; everything else is also verified against the
published packages.

The 0.5.0 icon collection (`dist/icons`) is not vendored here: `scripts/build-app.mjs`
copies it unchanged from the installed, pinned package (see `docs/ICONS.md`).
