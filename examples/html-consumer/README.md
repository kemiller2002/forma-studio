# HTML consumer proof

An ordinary static web project that uses HTML exported from Forma Studio. It
knows Forma (it installs Forma's public CSS) and nothing about Studio: no Studio
runtime, format, classes or script.

```text
Forma Studio -> design -> HTML export -> this project -> render
```

- `designs.forma-studio.json` holds the reference designs. They were authored
  through Studio's command core (`src/engine/FormaStudio.Engine/Designs.fs`):
  a normal application composition, a responsive composition and a page with
  an embedded workflow.
- `site/*.html` are complete-document exports: application, responsive,
  workflow, branded (the application page with the Example Harbor brand) and a
  workflow file exported directly.
- `content/*.fragment.html` are fragment exports. The project's own
  `build.mjs` inserts them into its `layout.html`, as any site would include
  markup.

Regenerate with `node scripts/export-reference-html.mjs`. Check determinism
with `node scripts/export-reference-html.mjs --check`. Build the site with
`node examples/html-consumer/build.mjs vendor/forma`. Tests are in
`tests/browser/html-consumer.spec.mjs`.
