# Evidence: Studio HTML export consumed by an ordinary web project

```text
Forma Studio -> design -> HTML export -> ordinary web project -> render
```

- **Designs.** `examples/html-consumer/designs.forma-studio.json`, authored through Studio's command core (`Designs.fs`).
- **Exports.** `node scripts/export-reference-html.mjs` writes complete documents to `examples/html-consumer/site/` and fragments to `examples/html-consumer/content/`. `--check` proves that a fresh export is byte-identical.
- **Consumer.** `examples/html-consumer/build.mjs` installs Forma's public CSS and includes fragments in its own `layout.html`. It has no Studio code, format, classes or script.
- **Rendering.** `tests/browser/html-consumer.spec.mjs` checks every page:
  - application, responsive, workflow, branded, direct workflow, and both embedded fragments;
  - no axe WCAG A/AA violations;
  - no page-level overflow at 320px;
  - the responsive grid has one column at 320px and several at 1280px;
  - the branded page has the same markup plus the brand, and resolves a different accent token;
  - the workflow relationships are in text and the canvas is keyboard-focusable;
  - form labels, descriptions and submit semantics.

Local run on 2026-10-01 (Chromium 1194): the full `tests/browser` suite passed, 46 tests.

The run found two Forma defects, which were fixed in Forma rather than in Studio:

- `.ef-actions` had no application-layer style;
- field-description contrast on `.ef-surface` was too low.
