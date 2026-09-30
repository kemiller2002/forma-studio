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
every decision is in `EditorApp.fs`.

`gestures.js` is the one piece of editor-only DOM integration. A pointer drag
previews with a CSS translate and emits a single `gesture-move` semantic event
when released. Arrow keys on a focused item emit the same event. The canonical
project changes only through that one engine command.

`studio.css` holds editor chrome and adorners (`studio-*`). These never reuse
`ef-*` classes and never appear in exported output.

Build and test:

```bash
npm run app:build   # dotnet publish + assemble app-dist/
npm run app:test    # Playwright (Chromium)
```
