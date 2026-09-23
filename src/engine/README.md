# Forma Studio engine boundary

This directory is the application-authority side of the Limen boundary.

Production implementation here will be F# compiled to WebAssembly. It owns:

- canonical Studio project state;
- legal editor transitions and capabilities;
- page and component composition rules;
- navigation graph semantics;
- document validation and migration;
- undo/redo semantics;
- scenario application;
- projections consumed by the browser;
- effect requests, but never browser mechanisms.

Code under this boundary MUST NOT directly use browser APIs such as document,
window, fetch, localStorage, or sessionStorage. Browser capabilities are
requested through the Limen protocol.

The authoritative design is architecture/ARCHITECTURE.md.
