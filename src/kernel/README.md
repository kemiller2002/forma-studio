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
