# Forma icons in Studio

Work item: https://github.com/kemiller2002/forma-studio/issues/27 (Praxis `GH-27`).

Status: authoring, persistence, picker and portable HTML export are implemented and tested against an icon-capable test fixture and, locally, the unpublished Forma 0.5.0 package. Studio's pinned Forma release is still 0.4.1, which has no icons, so the shipped editor reports icons unavailable until the pin moves to a published release that has them (see "Pinning").

This replaces the read-only JavaScript icon browser of the earlier draft: the catalog, search and copy now live in the F# engine, and the page holds no icon state.

## What Studio stores

A Layout component (heading, text, button, link-button, alert, metric-card) or a diagram node may carry `"icon": "<name>"`, a Forma icon name. Only the name is stored. The schema and its compatibility rules are in [`architecture/DOCUMENT-MODEL.md`](../architecture/DOCUMENT-MODEL.md) ("Icons"):

- a name the pinned release lacks is kept, round-trips unchanged and is not shown or exported;
- a value that is not a well-formed name is kept verbatim as inert data, warned about, never rendered;
- a document with an icon is written as schema 3, so an older Studio refuses it instead of dropping icons.

## Where the icons come from

Forma owns icon geometry, labels and packaging. Studio never copies icon geometry into its own source. It uses the compiled files of the pinned `@echelon-foundry/design-system` package:

- `dist/icons/registry.json` (schema 1, 24-unit grid, `formaVersion`, and `svgSha256` per icon);
- `dist/icons/<name>.svg`, the static SVG the digest covers;
- `dist/icons/html/<name>.html`, the decorative inline snippet.

`scripts/build-app.mjs` copies `dist/icons` from the installed, pinned package into the app unchanged (and fails if the registry's `formaVersion` differs from the package version). The F# engine fetches the files through Limen Http effects when the kernel starts, and verifies each icon (`IconCatalog.verify`):

1. the static SVG's SHA-256 equals the registry's `svgSha256`;
2. both files contain only the closed element and attribute vocabulary the Forma compiler emits (no script, event attribute, `foreignObject`, external reference, entity, comment or text);
3. the snippet is wrapped for exactly that name, marked `aria-hidden="true"`, and its SVG is the verified SVG.

An icon that fails is refused on its own and not offered; the collection is unavailable only if none verifies. A missing registry (Forma 0.4.1) or a malformed registry makes icons unavailable with the reason; nothing is substituted or fetched from anywhere else. The CLI reads the same files (`--forma-icons`, default `node_modules/@echelon-foundry/design-system/dist/icons`).

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

The pin is unchanged: `package.json`, `capabilities.lock.json`, `scripts/sync-forma.mjs` and `.conditor` still name Forma 0.4.1, because Forma 0.5.0 (kemiller2002/forma#114) is not published and CI must not point at a release that does not exist. When 0.5.0 is published:

1. move the `@echelon-foundry/design-system` dependency in `package.json` to the 0.5.0 release tarball;
2. update the Forma entry in `capabilities.lock.json`, `designSystem.version` in `scripts/sync-forma.mjs`, and re-vendor `vendor/forma` from the release commit (`node scripts/sync-forma.mjs ../forma`, then `npm run vendor:release`);
3. update the Forma pin through Conditor (`.conditor`, `conditor.json`) as that tool requires;
4. run `dotnet run --project tools/FormaStudio.Cli -c Release -- icons` (expects every icon verified), the engine tests (the "pinned Forma package" test then verifies every packaged icon) and the browser tests.

No code change is needed for the swap: the build copies whatever `dist/icons` the pinned package has.

## Not covered here

- Portable `.forma-workflow.json` documents are Forma's format, edited in Forma's `<forma-workflow>` component; an icon field for workflow steps is Forma's to define. Studio already preserves unknown workflow metadata and extensions.
- Icon style tokens (size, color) beyond Forma's defaults are not authored yet.
- Folio print/PDF integration is tracked in kemiller2002/folio#45.

## Tests

```sh
npm run engine:test                     # F# model, persistence, commands, catalog verification, export, editor events
npm run app:build && npm run app:test   # Playwright: 0.4.1 unavailable path, keyboard picker at 320px, axe light/dark, save/reopen, tampered files
```

The icon-capable cases use `tests/fixtures/forma-icons-test-fixture`, a committed TEST FIXTURE with synthetic shapes in the compiled registry layout. It is not Forma artwork and not a Forma release.
