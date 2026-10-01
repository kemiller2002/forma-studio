---
id: DF-STUDIO-2026-C4A2
title: Studio authors portable workflows with the public Forma component and exports standards-based HTML
status: accepted
confidence: high
created: 2026-10-01
work_item: STUDIO-GH-14, STUDIO-GH-15
related:
  - REQUIREMENTS.md
  - kemiller2002/forma requirements/PORTABLE-WORKFLOW-INTERCHANGE.md
  - kemiller2002/forma docs/decisions/ADR-0006-portable-workflow-capability.md
  - research/decisions/DF-STUDIO-2026-B7E1--canonical-engine-model-and-shared-command-core.md
---

# Decision

## Workflows

1. **The format is Forma's.** Studio opens, validates, edits and saves
   `.forma-workflow.json` documents. They are first-class documents of their
   own, under the Forma file convention, not converted into a Studio format.
   `WorkflowLibrary` keeps Forma's canonical text and validates every document
   with `Forma.Workflow.Validation`.
2. **The editor is Forma's.** The Workflows surface hosts the public
   `<forma-workflow>` element from `@echelon-foundry/forma-workflow`. Studio
   supplies the element's transport, `StudioWorkflowInterop`, backed by
   `Forma.Workflow.EmbedHost` in Studio's own .NET WebAssembly runtime, so there
   is one runtime and no private editor. Studio receives the component's
   validated changes as one semantic event and persists them through Limen
   Storage under `forma-studio.workflows`.
3. **No private substitutes.** When Studio needs presentation Forma lacks, the
   gap is fixed in Forma. This work fixed two: `.ef-actions` was styled only in
   marketing CSS, and field descriptions on a surface had insufficient contrast.
4. **Until release, Forma is vendored.** `vendor/forma` is copied by
   `scripts/sync-forma.mjs` from a cited forma commit, with SHA-256 hashes in
   `vendor/forma/MANIFEST.json`. CI checks them. When Forma 0.4.0 and
   `@echelon-foundry/forma-workflow` 1.0.0 are published, Studio switches to
   the pinned release artifacts.

## Flow diagrams

Studio's existing Flow surface remains, unchanged. It edits the project's
typed diagrams with profiles, palettes, named styles, metadata mappings,
semantic diff and merge (DF-STUDIO-2026-B7E1, requirements/16). Portable
workflow authoring does not go through it. Converging Flow's Workflow profile
onto the portable format is a migration in its own right: Flow-only concepts
such as named styles, mappings and project fields would have to become Forma
capabilities or explicit Studio projections, and the conversion loss would need
reporting. It is tracked as kemiller2002/forma-studio#16 so that it is not done
silently or partially here.

## HTML export

`HtmlExport` maps each Studio Layout component to its public Forma markup
(`Components.catalog` names the Forma pattern for each). It renders workflows
with Forma's own renderer, and returns an HTML fragment (with a leading
asset-requirement comment) or a complete document (doctype, `lang`, charset,
viewport, title, description, the declared stylesheet links and an optional
public brand).

Output is built with Forma's escaping `Markup` tree. It is deterministic,
contains no script or event handlers, references no Studio class, id, asset or
runtime, and drops unsafe links. Design content without a public Forma contract
is reported as omitted, never faked. Interactive workflow output uses only the
public `<forma-workflow>` runtime and declares it as a dependency.

# Evidence

- `tests/FormaStudio.Engine.Tests/WorkflowStudioTests.fs`: open, edit, save and
  consume lifecycle; extension preservation; persistence; export determinism,
  safety and public classes.
- `tests/browser/workflow.spec.mjs`: the real Studio app, including the external
  round trip, keyboard, touch, axe and persistence.
- `tests/browser/html-consumer.spec.mjs`: an ordinary web project renders the
  exports. Checks cover axe, 320px, responsive columns, brand and form
  semantics.
- `docs/evidence/external-workflow-roundtrip.md`, recorded by
  `npm run proof:external`.
