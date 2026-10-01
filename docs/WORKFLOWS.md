# Workflow authoring in Forma Studio

Studio authors **portable Forma workflows**: `.forma-workflow.json` documents
defined by Forma (kemiller2002/forma `docs/workflow/FORMAT.md`). The editor is
the public Forma component `<forma-workflow>`, the same component any other
application embeds. Studio does not keep a private workflow format or editor
(DF-STUDIO-2026-C4A2).

## Open, create and save

On the **Workflows** surface (the "Workflows (n)" button):

| Action | What happens |
|---|---|
| **New workflow** | Creates a valid document (`workflow-1`, `workflow-2` …) with a start step. |
| **Open .forma-workflow.json** | Opens any workflow file, including files written by other systems. Forma validates it first. A semantically invalid file still opens and shows its findings, and no data is removed. A file that is not a workflow is refused with Forma's reason. |
| **Download file** | Saves the document as `<id>.forma-workflow.json`, the Forma repository convention (`forma/workflows/<id>.forma-workflow.json`). |
| **Save workflows in browser** / **Open saved workflows** | Persists the open documents through Limen Storage (`forma-studio.workflows`). |

Each open workflow shows its validation class, for example
`fully-supported` or `supported-with-preserved-extensions`.

## Editing

All editing goes through the component's validated commands. A change that
would break the workflow, such as a dangling connection, a port over its limit
or a blank name, is refused and nothing changes.

- **Steps.** Add a step of any kind, rename it, describe it, and change its kind, shape, colour (Forma token, palette slot or `#rrggbb`), status, size and position.
- **Connections.** Use **Connect** and then choose the target, or drag the round handle onto a step, or use the Connect form (including ports). Set label, kind, line and direction. Reconnect either end.
- **Ports, groups and lanes.** Add and remove ports. Create groups, containers, lanes and phases from the selection. Set membership with checkboxes.
- **Metadata.** Add or edit fields as text, number, yes/no or JSON. Unknown metadata and namespaced extensions are shown, preserved and written back exactly.
- **References and interactions.** Typed domain references, and declarative intents. Studio never runs them.
- **Arrange.** The deterministic Forma layout. It never changes ids or meaning.
- **Undo and redo, zoom.** Pan by scrolling the canvas.
- **Keyboard.** Select from the Objects list. Arrow keys move (Shift moves further). Delete removes. Escape cancels.
- **Touch.** Tap to select. Nothing needs a drag: use Connect then tap a target, the move buttons, and the position fields.

## Embedding the same editor elsewhere

Applications embed the same component without Studio. See kemiller2002/forma
`docs/workflow/EMBEDDING.md` and `examples/workflow-embedding`. Studio is one
host: it sets the element's `transport` to its own runtime
(`src/kernel/workflows.js`, `StudioWorkflowInterop`).

## External files

```bash
node vendor/forma/workflow/external-producer/produce.mjs out/         # writes out/forma/workflows/mission-handoff.forma-workflow.json
dotnet run --project tools/FormaStudio.Cli -c Release -- workflow-validate out/forma/workflows/mission-handoff.forma-workflow.json
# open it in Studio, edit, Download file, then:
node vendor/forma/workflow/external-producer/consume.mjs out/forma/workflows/mission-handoff.forma-workflow.json ~/Downloads/mission-handoff.forma-workflow.json
```

`npm run proof:external` automates this against the real app. Its last result
is recorded in [`docs/evidence/external-workflow-roundtrip.md`](evidence/external-workflow-roundtrip.md).
