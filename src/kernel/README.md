# Forma Studio browser boundary

This directory is the browser-mechanism side of the Limen boundary.

It is intentionally small. It may contain the wiring required to:

- start the Limen BrowserKernel;
- transport serializable messages to/from the F# WebAssembly engine;
- perform browser capabilities through Limen;
- host editor-only DOM integration that contains no application meaning.

It MUST NOT decide project legality, component legality, navigation semantics,
validation outcomes, undo/redo behavior, or other Studio domain decisions.

Forma owns the rendered presentation contracts. The F# engine owns application
authority. Limen carries data across this threshold.

## Flow editor (kemiller2002/forma-studio#11)

`index.html` is the Flow editor page, built from public Forma classes and
Limen `data-*` bindings. `main.js` starts the Limen `BrowserKernel` with
`transport.js`, which carries messages to the F# engine compiled to .NET
WebAssembly (`src/wasm/FormaStudio.Wasm`). That C# file only marshals messages;
every decision is in the F# engine (`EditorApp.fs` dispatches typed events to
the `Interaction.*.fs` update functions).

`editor-events.js` is generated from the F# `EditorEvent` union, the single
source of event names. Kernel scripts emit events through it rather than string
literals, and every `data-event` in `index.html` must name one of its events;
the engine test suite checks both (`EditorEventContractTests.fs`). Regenerate it
with `FORMA_STUDIO_UPDATE_GENERATED=1 npm run engine:test`.

`gestures.js` is the one piece of editor-only DOM integration. A pointer drag
previews with a CSS translate and emits a single `gesture-move` semantic event
when released. Canvas nodes are not text-selectable (`studio.css`): a selection
left by one drag would turn the next drag into a native text drag, which
cancels the pointer before the move commits. Arrow keys on a focused item emit the same event. The canonical
project changes only through that one engine command.

`studio.css` holds editor chrome and adorners (`studio-*`). These never reuse
`ef-*` classes and never appear in exported output.

The canvas binds no inline style: Limen 0.7.0 refuses `data-bind-style`
(limen#18, Limen `docs/29-binding-security.md`). The engine projects geometry
and colors as plain `data-*` values (`data-x`/`-y`/`-w`/`-h`, `data-fill`,
`data-stroke`, `data-accent`, `data-foreground`, `data-connector-stroke`, and
`data-width`/`-height`/`-zoom` on the canvas). `studio-projection.css` maps
them onto Forma's diagram custom properties. Logical pixels and `#rrggbb`
literals go through typed `attr()`. Each public Forma color token has its own
rule, so a token stays a `var()` and themes and brands still apply. Typed
`attr()` needs Chromium 133 or later, the browser the editor is tested in.
`EditorConformanceTests` checks that every token has a rule in every slot.

Build and test:

```bash
npm run app:build   # dotnet publish + assemble app-dist/
npm run app:test    # Playwright (Chromium)
```
