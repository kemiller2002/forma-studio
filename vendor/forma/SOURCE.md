# Vendored Forma (pre-release)

Everything in this directory is copied verbatim from kemiller2002/forma by
`node scripts/sync-forma.mjs <forma checkout>`. `vendor/forma/MANIFEST.json`
records the source commit and the SHA-256 of every file;
`node scripts/sync-forma.mjs --check` (run by `npm run check`) fails if a file
drifts.

| Path | Forma source | Used for |
|---|---|---|
| `tokens.css`, `foundations.css`, `components.css`, `brands/*.css` | `dist/` (built with `npm run build`) | Studio's own UI and export previews |
| `workflow/src/*.fs` | `src/workflow/Forma.Workflow` | the public workflow model, validation, layout, rendering and editor engine, compiled into Studio's engine |
| `workflow/src/Forma.Workflow.fsproj` | the upstream project file, with resource paths pointing at the vendored contracts | |
| `workflow/forma-workflow.schema.json`, `workflow/workflow-capabilities.json`, `workflow/diagram-presentation.json` | `schemas/`, `contracts/` | the published contracts |
| `workflow/browser/forma-workflow.js`, `.css` | `packages/workflow/src` | the public `<forma-workflow>` element, driven by Studio's transport |
| `workflow/fixtures/*.forma-workflow.json` | `examples/workflows/forma/workflows` | compatibility fixtures |
| `workflow/external-producer/*.mjs` | `examples/external-producer` | the external producer and consumer in the round-trip proof |

Source: kemiller2002/forma PR #95 (branch `claude/forma-workflow-interchange-wo5aq3`). The
workflow capability is not in a published Forma release yet. When Forma 0.4.0
and `@echelon-foundry/forma-workflow` 1.0.0 are published, replace this
directory with the pinned release artifacts (REQUIREMENTS.md, "Dependency
rules").
