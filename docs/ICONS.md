# Forma icons in Studio

Work item: https://github.com/kemiller2002/forma-studio/issues/27 (Praxis `GH-27`).

Status: Studio pins Forma 0.5.0 and ships its 40 compiled icons. Authoring, persistence, picker and portable HTML export are tested against the pinned package, against an icon-less release (no registry) and against a small test fixture for edge cases.

This replaces the read-only JavaScript icon browser of the earlier draft: the catalog, search and copy now live in the F# engine, and the page holds no icon state.

## What Studio stores

A Layout component whose Forma contract takes an icon (button, link-button, alert, metric-card, workflow) stores it as its `icon` property (`"properties": { "icon": "<name>" }`, the representation #33 introduced). A diagram node stores `"icon": "<name>"`. Only the name is stored. The schema and its compatibility rules are in [`architecture/DOCUMENT-MODEL.md`](../architecture/DOCUMENT-MODEL.md) ("Icons"):

- a name the pinned release lacks is kept, round-trips unchanged and is not shown or exported;
- a value that is not a well-formed name is kept verbatim as inert data, warned about, never rendered;
- a document with a diagram-node icon is written as schema 3, so an older Studio refuses it instead of dropping icons; Layout icons are component properties, which schema 2 readers already keep.

## Where the icons come from

Forma owns icon geometry, labels and packaging. Studio never copies icon geometry into its own source. It uses the compiled files of the pinned `@echelon-foundry/design-system` package:

- `dist/icons/registry.json` (schema 1, 24-unit grid, `formaVersion`, and `svgSha256` per icon);
- `dist/icons/<name>.svg`, the static SVG the digest covers;
- `dist/icons/html/<name>.html`, the decorative inline snippet.

`scripts/build-app.mjs` copies `dist/icons` from the installed, pinned package into the app unchanged (and fails if the registry's `formaVersion` differs from the package version). The F# engine fetches the files through Limen Http effects when the kernel starts, and verifies each icon (`IconCatalog.verify`):

1. the static SVG's SHA-256 equals the registry's `svgSha256`;
2. both files contain only the closed element and attribute vocabulary the Forma compiler emits (no script, event attribute, `foreignObject`, external reference, entity, comment or text);
3. the snippet is wrapped for exactly that name, marked `aria-hidden="true"`, and its SVG is the verified SVG.

An icon that fails is refused on its own and not offered; the collection is unavailable only if none verifies. A missing registry (Forma 0.4.1 and earlier) or a malformed registry makes icons unavailable with the reason; nothing is substituted or fetched from anywhere else. The CLI reads the same files (`--forma-icons`, default `node_modules/@echelon-foundry/design-system/dist/icons`).

## Authoring

Every change goes through the F# engine; the page binds native controls through Limen.

- Layout: each item with a place for an icon has a "Choose icon for …" button. Diagram: the inspector shows the selected node's icon in text and a "Choose icon" button.
- The picker is a labelled region with the current icon in text, a search field (names, categories, keywords; Enter searches), a group of toggle buttons named by the icon's label (`aria-pressed`, decorative preview images), and Remove icon, Copy icon HTML and Done.
- Choosing or removing is one `SetIcon` command: undo, redo, review, merge and save treat it like any other presentation edit.
- Icons never name a control: buttons keep their text, and previews use `alt=""`.

Events (`IconEvent`, wire names in `src/kernel/editor-events.js`): `icon-pick`, `icon-search`, `icon-choose`, `icon-clear`, `icon-copy`, `icon-close`.

## Export

See [`HTML-EXPORT.md`](HTML-EXPORT.md) ("Icons"). Exported icons are the release's decorative snippets, stamped `data-forma-version`, with the stylesheets declared at that release. Nothing is fetched at view time.

## Pinning

Forma 0.5.0 (kemiller2002/forma#114, tag v0.5.0, commit 8a5a599, tarball sha256 `c4ad3ae5…f558f`) is pinned in:

1. `package.json` (the v0.5.0 release tarball);
2. `capabilities.lock.json`, `designSystem.version` in `scripts/sync-forma.mjs`, and `vendor/forma` re-vendored from the release commit (`node scripts/sync-forma.mjs <forma checkout>`; `npm run vendor:release` verifies every packaged file against the published 0.5.0 package);
3. the Conditor authority (`.conditor`, `conditor.json`), moved with Conditor 0.8.1 `upgrade --current` to echelon-current 1.13.0, the channel version that selects Forma 0.5.0 without changing Studio's managed component versions.

To move to a later Forma release, repeat these steps; `dotnet run --project tools/FormaStudio.Cli -c Release -- icons` must report every icon verified. The build copies whatever `dist/icons` the pinned package has, so no code change is needed.

## Not covered here

- Portable `.forma-workflow.json` documents are Forma's format, edited in Forma's `<forma-workflow>` component; an icon field for workflow steps is Forma's to define. Studio already preserves unknown workflow metadata and extensions.
- Icon style tokens (size, color) beyond Forma's defaults are not authored yet.
- Folio print/PDF integration is tracked in kemiller2002/folio#45.

## Tests

```sh
npm run engine:test                     # F# model, persistence, commands, catalog verification, export, editor events
npm run app:build && npm run app:test   # Playwright: the pinned 0.5.0 icons end to end, the icon-less path, keyboard picker at 320px, axe light/dark, save/reopen, tampered files
```

The icon-capable cases use `tests/fixtures/forma-icons-test-fixture`, a committed TEST FIXTURE with synthetic shapes in the compiled registry layout. It is not Forma artwork and not a Forma release.
