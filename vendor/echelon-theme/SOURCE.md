# Packaged Echelon theme (release 1.1.0)

Forma 1.0 became brand-neutral (BNR-001, EDS-ADR-002) and no longer ships
`dist/brands/echelon.css`. The Echelon brand for Forma 1.x is the public,
released `@echelon-foundry/echelon-theme`. Studio uses it in three places: the
editor and the marketing site wear it, and the HTML export's "Echelon" brand
declares and links it.

Unlike `vendor/forma`, the theme's files are **packaged, not copied into git**:
`package.json` pins the release tarball (the same asset echelon-current 1.42.0
records), and this directory holds only its provenance. The package is
published `UNLICENSED` (all rights reserved by its owner), so Studio
redistributes it only where it already ships it, in built output, never as
committed source.

`MANIFEST.json` records:

- the release: tag v1.1.0, commit `a03c5ac05d99c0bace43df35c90e74c3fe352d02`
  of kemiller2002/echelon-design-system;
- the tarball URL and its sha256
  (`e9360d743ad9e9a62abb3cf22d29bc8a26a4f9fd2b4980dd1d050c27937ec6bd`, the
  digest echelon-current 1.42.0 records);
- the SHA-256 of every file Studio uses from the package. They equal the
  package's own `dist/checksums.txt` entries.

| Package file | Used for |
|---|---|
| `dist/echelon.css` | the compiled brand: `[data-forma-brand="echelon"]` token values |
| `dist/echelon-fonts.css` | the self-hosted fonts' `@font-face` rules (relative `fonts/*.woff2` URLs) |
| `dist/echelon-app.css` | the Echelon application layer |
| `dist/fonts/*.woff2` | Newsreader, Manrope and IBM Plex Mono (latin subsets) |
| `dist/fonts/licenses/*-OFL.txt` | the fonts' SIL Open Font License 1.1 texts, shipped beside them |

Load order (the theme's own): Forma's `tokens.css`, `foundations.css`,
`components.css` (or `all.css`), then `echelon.css`, `echelon-fonts.css`,
`echelon-app.css`, with `data-forma-brand="echelon"` on the root element.

Checks:

- `node scripts/echelon-theme.mjs --check` (run by `npm run vendor:check`):
  `package.json` and `capabilities.lock.json` name this release, and the
  installed package's files match the manifest (offline; needs `npm install`).
- `node scripts/echelon-theme.mjs --release` (run by `npm run vendor:release`
  in CI): also downloads the release tarball, checks its sha256 and every
  recorded file inside it.
- `scripts/build-app.mjs` and `scripts/build-site.mjs` copy only the recorded
  files, and refuse to build if the installed package differs.

To move to a later theme release: change the pin in `package.json` and
`capabilities.lock.json`, `version`, `tag`, `commit` and `tarball` here, and
`EchelonTheme.version` in `src/engine/FormaStudio.Engine/HtmlExport.fs`;
`npm install`, then `node scripts/echelon-theme.mjs --record` and `--release`.
An engine test fails if the engine's version and this manifest disagree.
