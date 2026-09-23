# Forma Studio architecture

## Purpose

Forma Studio is a visual application specification environment, not a general-purpose vector editor.

Its primary artifact is a typed, versioned application specification that humans can edit visually and machines can reason about deterministically.

## Authority boundaries

1. Forma owns presentation contracts, semantic markup, design tokens, responsive presentation, accessibility presentation, and the canonical component catalog.
2. Forma Studio owns projects, pages, component composition, visual-specification editing, preview scenarios, navigation graph authoring, validation, export, and editor workflow.
3. The F# application engine owns all Studio state, legal editing transitions, undo/redo semantics, validation state, document migrations, and projections.
4. Limen owns browser capabilities and browser/application transport. The browser must not become a second source of application truth.
5. ROS owns durable work protocol, evidence, telemetry, and repository operating state.
6. SDE/Ordo governs explicit state, legal transitions, capabilities, obligations, unknown effects, and negative knowledge.
7. Visual Engineering constrains visual decisions.
8. Communication Engineering constrains product copy, labels, explanations, and user-facing guidance.

## High-level structure

HTML/CSS + actual Forma patterns
        |
        v
Limen browser boundary
        |
        v
F# WebAssembly application engine
        |
        +-- Studio document state
        +-- transition legality
        +-- undo/redo command history
        +-- validation
        +-- component catalog projection
        +-- navigation graph
        +-- export/import
        +-- persistence requests

## Central architectural rule

The canvas renders real Forma HTML/CSS whenever a canonical Forma pattern exists.

Studio must not maintain a second visual implementation of a Forma component.

If a required visual capability does not exist in Forma, Studio records a Forma gap rather than silently inventing a private equivalent.

## Project document

A project is a versioned specification containing:

- project metadata;
- pinned Forma version;
- pages;
- page routes;
- component-node trees;
- typed component properties;
- token bindings;
- content fixtures;
- preview scenarios;
- navigation actions and graph;
- assets;
- annotations;
- requirements/evidence links;
- export settings;
- migration metadata.

The normative machine-readable shape begins in schemas/forma-studio.schema.json.

## Editing model

All meaningful edits are commands against explicit state. Examples include:

- AddPage
- RenamePage
- SetStartPage
- SetRoute
- DeletePage
- AddComponent
- MoveComponent
- DuplicateComponent
- RemoveComponent
- SetProperty
- BindToken
- SetContent
- LinkNavigation
- RemoveNavigation
- AddScenario
- SetViewport
- Undo
- Redo

Illegal transitions must be rejected by the F# engine, not hidden or repaired by DOM state.

## Persistence

Initial persistence is deliberately simple:

- local draft persistence through Limen Storage effects;
- import/export of the complete project JSON;
- GitHub repository persistence as explicit commits for durable projects;
- no server database required for v1.

Git history is the initial collaboration/version-history mechanism.

## Deployment surfaces

- Marketing: https://forma-studio.echelonfoundry.com/
- Editor: initially /app/ under the same origin unless a later requirement changes deployment.
- Marketing is static and may function without WebAssembly.
- The editor uses F# WebAssembly plus Limen and Forma.

## Non-goals for v1

Forma Studio is not:

- Illustrator;
- a freehand vector drawing package;
- a photo editor;
- a 3D editor;
- a replacement for every Figma workflow;
- a runtime application framework;
- an owner of business-domain state for the applications being designed;
- a place to write arbitrary JavaScript into previews.
