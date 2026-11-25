# HTML export

Studio exports designs as standards-based HTML that any web project can use
without Studio. The export panel is at the bottom of the editor. The CLI
produces the same output.

## Targets

| Target | Output |
|---|---|
| **HTML fragment** | Markup for the Layout page, one component hierarchy ("Export as HTML" on a Layout item) or workflow, to insert into an existing page. The first line declares the public assets it needs. |
| **Complete HTML document** | `<!doctype html>`, `lang`, charset, viewport, title, description, links to exactly the declared Forma stylesheets, an optional public brand (`data-forma-brand`), and the design inside `<main>`. |

```bash
dotnet run --project tools/FormaStudio.Cli -c Release -- html project.json application out.html --document --forma-base /assets/forma/ --brand example-harbor
dotnet run --project tools/FormaStudio.Cli -c Release -- html project.json application out.html --document --forma-base /assets/forma/ --brand echelon --theme-base /assets/echelon-theme/
dotnet run --project tools/FormaStudio.Cli -c Release -- workflow-html forma/workflows/launch.forma-workflow.json launch.html --document
```

A fragment starts like this:

```html
<!-- Requires @echelon-foundry/design-system@0.4.0/tokens.css, @echelon-foundry/design-system@0.4.0/foundations.css, @echelon-foundry/design-system@0.4.0/components.css -->
<div class="forma-stack" data-density="standard">
  <h1>Crew scheduling</h1>
  <section class="forma-surface" aria-labelledby="surface-app-form">
    <h2 id="surface-app-form">Request a session</h2>
    <div class="forma-field">
      <label class="forma-field__label" for="field-app-email">Work email</label>
      <span class="forma-field__description" id="field-app-email-description">We send the confirmation here.</span>
      <input id="field-app-email" name="email" type="email" aria-describedby="field-app-email-description" required>
    </div>
    <div class="forma-actions"><button type="submit">Request session</button><a class="forma-button" href="https://example.org/policy">Read the training policy</a></div>
  </section>
</div>
```

## Dependencies

- **Static designs** need only `tokens.css`, `foundations.css` and `components.css` from `@echelon-foundry/design-system`. They add a brand stylesheet if a brand is chosen. No JavaScript is added because a design was made visually.
- **Interactive workflows** are opt-in. Use the "Interactive workflow" toggle. The static figure is wrapped in the public `<forma-workflow mode="view">` element, plus one module script from `@echelon-foundry/forma-workflow`, which the output declares. Studio never emits its own JavaScript, classes, ids or assets.
- The full Forma stylesheet is never copied into a fragment.

## Brands

The export offers **No brand**, **Echelon** and **Example Harbor**. A brand sets
`data-forma-brand` on the document's root and adds stylesheets after Forma's; it
never changes the markup.

| Brand | Stylesheets added after Forma's | Package |
|---|---|---|
| Echelon | `echelon.css`, `echelon-fonts.css`, `echelon-app.css`, in that order | `@echelon-foundry/echelon-theme` 1.1.0 |
| Example Harbor | `brands/example-harbor.css` | `@echelon-foundry/design-system` (Forma's example brand) |

Forma 1.0 became brand-neutral and no longer ships `brands/echelon.css`. The
Echelon brand is the public, released `@echelon-foundry/echelon-theme`, so an
Echelon export declares that package (`@echelon-foundry/echelon-theme@1.1.0/echelon.css`, …)
and a document links it from `node_modules/@echelon-foundry/echelon-theme/dist/`
(CLI: `--theme-base URL`). `echelon-fonts.css` loads the self-hosted fonts from
`fonts/` beside it, so a project that serves the package's `dist/` directory as
published has everything the export needs; nothing is fetched from elsewhere.
An interactive workflow export with Echelon is Forma's own interactive element
and runtime, with the theme between Forma's stylesheets and the runtime's.

Exports made before Forma 1.0 with the Echelon brand linked
`brands/echelon.css` from Forma and used `data-ef-brand`; re-export them to get
the theme. The brand is a choice in the export panel, not part of the saved
project, so saved projects need no change. The id `echelon` (an old session, an
automation, `--brand echelon`) now always means the theme, never a Forma brand file.

## Icons

A Layout button, link-button, alert or metric-card with an `icon` property exports the pinned Forma release's own decorative inline snippet for that icon (`<forma-icon>` wrapping `aria-hidden="true"` SVG), placed before the text it decorates, so the text stays the accessible name. Studio stamps the `<forma-icon>` element with `data-forma-version`, and a design with icons declares its Forma stylesheets at that release, whose `components.css` sizes and colors `.forma-icon`. The SVG is read from the pinned package's compiled `dist/icons` and verified against the registry digest before use; Studio holds no icon geometry of its own. Nothing is fetched by the exported HTML.

An icon is left out, and listed with the reason, when the pinned release has no icon collection (Forma 0.4.1 and earlier), when the release does not have that name, when the stored value is not a valid name, or when the component has no place for an inline icon (including the workflow figure). The document keeps the icon in every case.

```bash
dotnet run --project tools/FormaStudio.Cli -c Release -- icons                     # report the pinned collection (exit 5 when unavailable)
dotnet run --project tools/FormaStudio.Cli -c Release -- html project.json home out.html --forma-icons node_modules/@echelon-foundry/design-system/dist/icons
```

## What is guaranteed

Each guarantee is tested in `tests/FormaStudio.Engine.Tests/WorkflowStudioTests.fs` and `tests/browser/html-consumer.spec.mjs`.

- **Deterministic.** The same design and options give byte-identical output. `npm run html:check` re-exports the reference designs and compares them.
- **Public Forma only.** Each component maps to its Forma pattern (`Components.catalog` names the source). Content without a public contract is listed as left out, never imitated.
- **Accessible.** Native elements, labels and descriptions, heading levels, named regions, and the textual relationship list for workflows.
- **Safe.** All authored text is escaped. Unsafe links are dropped and reported. There is no inline script or event handler.
- **Responsive.** There is no page-level overflow at 320px. The responsive grid becomes one column.

## Consumer proof

[`examples/html-consumer`](../examples/html-consumer) is an ordinary static site.
It knows Forma and nothing about Studio. It renders:

- an application composition;
- a responsive composition;
- a workflow page;
- a branded composition (Forma's Example Harbor brand);
- the same composition with the Echelon brand, served from the installed theme package;
- a workflow exported directly;
- the fragments inserted into its own layout.

The browser tests run axe and check 320px reflow, responsive columns, the brand
and form semantics against it.

Layout components available in Studio: stack, heading, text, button, link-button, text-field, actions, surface, responsive-grid, alert, metric-card and workflow.
