# HTML export

Studio exports designs as standards-based HTML that any web project can use
without Studio. The export panel is at the bottom of the editor. The CLI
produces the same output.

## Targets

| Target | Output |
|---|---|
| **HTML fragment** | Markup for the Layout page or workflow, to insert into an existing page. The first line declares the public assets it needs. |
| **Complete HTML document** | `<!doctype html>`, `lang`, charset, viewport, title, description, links to exactly the declared Forma stylesheets, an optional public brand (`data-ef-brand`), and the design inside `<main>`. |

```bash
dotnet run --project tools/FormaStudio.Cli -c Release -- html project.json application out.html --document --forma-base /assets/forma/ --brand example-harbor
dotnet run --project tools/FormaStudio.Cli -c Release -- workflow-html forma/workflows/launch.forma-workflow.json launch.html --document
```

A fragment starts like this:

```html
<!-- Requires @echelon-foundry/design-system@0.4.0/tokens.css, @echelon-foundry/design-system@0.4.0/foundations.css, @echelon-foundry/design-system@0.4.0/components.css -->
<div class="ef-stack" data-density="standard">
  <h1>Crew scheduling</h1>
  <section class="ef-surface" aria-labelledby="surface-app-form">
    <h2 id="surface-app-form">Request a session</h2>
    <div class="ef-field">
      <label class="ef-field__label" for="field-app-email">Work email</label>
      <span class="ef-field__description" id="field-app-email-description">We send the confirmation here.</span>
      <input id="field-app-email" name="email" type="email" aria-describedby="field-app-email-description" required>
    </div>
    <div class="ef-actions"><button type="submit">Request session</button><a class="ef-button" href="https://example.org/policy">Read the training policy</a></div>
  </section>
</div>
```

## Dependencies

- **Static designs** need only `tokens.css`, `foundations.css` and `components.css` from `@echelon-foundry/design-system`. They add a brand stylesheet if a brand is chosen. No JavaScript is added because a design was made visually.
- **Interactive workflows** are opt-in. Use the "Interactive workflow" toggle. The static figure is wrapped in the public `<forma-workflow mode="view">` element, plus one module script from `@echelon-foundry/forma-workflow`, which the output declares. Studio never emits its own JavaScript, classes, ids or assets.
- The full Forma stylesheet is never copied into a fragment.

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
- a branded composition;
- a workflow exported directly;
- the fragments inserted into its own layout.

The browser tests run axe and check 320px reflow, responsive columns, the brand
and form semantics against it.

Layout components available in Studio: stack, heading, text, button, link-button, text-field, actions, surface, responsive-grid, alert, metric-card and workflow.
