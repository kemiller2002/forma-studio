# Semantic Forma icons in Studio layout documents

This change provides a **document-model foundation**, not an icon renderer or completed picker.

Editable Forma layout components that represent actions or status may store an optional `properties.icon` string in the existing versioned Studio document. Supported contract types are button, link-button, alert, metric-card and workflow. The F# engine checks a bounded lower-kebab identifier and rejects markup, filesystem paths, URLs, unbounded values and non-strings. It does **not** duplicate the currently shipped Forma icon-name list. Unknown IDs from future source files must be preserved through serialization but remain visually inert until a known, pinned release's registry verifies them.

Examples of portable data:

```json
{
  "component": "button",
  "properties": {"icon": "email"},
  "content": {"label": "Send email"}
}
```

The button's text is the accessible name. An icon is not a command, permission, consequence or state machine. The existing Codec preserves typed property values through source-to-document round trips, undo and redo.

**Status (forma-studio#31):** the renderer, picker and pin described below are delivered on top of this property; see [`ICONS.md`](ICONS.md). Studio pins Forma 0.5.0, and `SetIcon` and `SetComponentProperty "icon"` write the same property.

**Remaining work at the time of #33 (since addressed in #31 except where ICONS.md says otherwise):**

- Upgrade the actual Forma dependency, vendor package and lock only after 0.6.0 is published and its artifacts/digests have been verified.
- Link searchable picker selections to F# `SetComponentProperty` commands, with mobile/keyboard controls and semantic Playwright selectors.
- Extend `HtmlExport` to safely embed the **compiled SVG markup from the pinned package**, not registry source geometry or an external runtime request; preserve unknown future names as inert data and warn when output omits unresolved glyphs.
- Preserve IDs for workflow elements using the workflow schema and authoring command path. Do not treat a Layout property as workflow-node support.
- Run F# unit tests, offline export round-trip, axe, 320px and actual release-package browser proof.

Tracking: [Forma Studio issue #27](https://github.com/kemiller2002/forma-studio/issues/27). The existing searchable picker stub is in staged PR #29; rebase/reconcile it with current main before merging.
