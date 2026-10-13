// The declared action inventory of the Studio editor (Visual Engineering
// UI-LAYOUT-INTERACTION@1.0.0, ULI-A01/A02/A10; forma-studio#35). Every enabled,
// visible control on every surface must match one entry, which names the owner
// of its behaviour, its machine-observable outcome and the test that asserts it.
// The ULI-A10 test in ui-conformance.spec.mjs fails when a control appears that
// no entry describes, so a new action cannot ship unclassified.
//
// Outcome vocabulary: "project" (one command; history and canonical state change),
// "view" (view state such as zoom, selection, a pressed choice or a shown panel),
// "navigation" (the visible surface changes), "persistence" (browser storage
// through Limen), "export" (exported text, a download or the clipboard),
// "explains" (no change is possible and the status says why, in words).

const engine = "F# engine through Limen (Editor.dispatch for project changes)";
const workflowComponent = "public Forma <forma-workflow> component (Forma owns its behaviour)";

export const ACTION_INVENTORY = [
  // History, file and surface navigation
  { name: /^(Undo|Redo)$/, owner: engine, outcome: "project", evidence: "editor.spec: Layout and Flow share one session; ui-conformance: rapid repeated activation" },
  { name: /^(Save|Open saved)$/, owner: engine, outcome: "persistence", evidence: "editor.spec: Layout and Flow share one session (save, reload, open saved)" },
  { name: /^(Flow: .+|Layout: .+|Workflows \(\d+\))$/, owner: engine, outcome: "navigation", evidence: "editor.spec: Layout and Flow share one session; ui-conformance: every surface at each width" },
  { name: /^Add Layout page$/, owner: engine, outcome: "project", evidence: "ui-conformance: declared form row" },
  // Flow canvas, structure and inspector
  { surface: "flow", name: /^(Start|End|Activity|Decision)$/, owner: engine, outcome: "view", evidence: "editor.spec: kind choice before Add item" },
  { surface: "flow", name: /^(Add item|Delete selection|Connect from selection|Cancel connecting|Cancel reconnecting)$/, owner: engine, outcome: "project", evidence: "editor.spec: add, connect, delete; 'Delete selection' explains when nothing is selected" },
  { surface: "flow", name: /^(Zoom out|Zoom in|Actual size|Snap to grid)$/, owner: engine, outcome: "view", evidence: "editor.spec: Zoom and snapping are view state" },
  { surface: "flow", name: /^(Start|End|Activity|Decision|Connector): .+$/, owner: engine, outcome: "view", evidence: "editor.spec: the Structure list selects without the canvas" },
  { surface: "flow", name: /^(Add .+ to selection|Remove .+ from selection)$/, owner: engine, outcome: "view", evidence: "editor.spec: multi-selection edits metadata atomically" },
  { surface: "flow", name: /^(Move left|Move right|Move up|Move down)$/, owner: engine, outcome: "project", evidence: "ui-conformance: drag edits have keyboard and field paths" },
  { surface: "flow", name: /^(Align left edges|Align top edges|Distribute horizontally)$/, owner: engine, outcome: "project", evidence: "editor.spec: multi-selection arranges in one step" },
  { surface: "flow", name: /^Choose icon$/, owner: engine, outcome: "view", evidence: "icons.spec: a diagram node's icon is chosen in the inspector" },
  { surface: "flow", name: /^(Requester|Finance|Procurement|No lane)$/, owner: engine, outcome: "project", evidence: "editor.spec: moving an activity between lanes reports the responsibility change" },
  { surface: "flow", name: /^(Status|Owner|Phase|Tags|Ticket|Cost center)$/, owner: engine, outcome: "view", evidence: "editor.spec: metadata field choice opens its editor" },
  { surface: "flow", name: /^(Workflow blocked|Author highlight|Reset fill|Decision emphasis|No style)$/, owner: engine, outcome: "project", evidence: "editor.spec: appearance from palette and named styles" },
  { surface: "flow", name: /^(Check the saved copy for changes|Keep mine|Use saved|Apply merge|Cancel merge)$/, owner: engine, outcome: "project", evidence: "editor.spec: Merge review; explains when nothing was saved" },
  { surface: "flow", name: /^(Copy selection as template|Approval decision|Insert template)$/, owner: engine, outcome: "project", evidence: "editor.spec: Templates insert in one step" },
  { surface: "flow", name: /^(Text|Number|Yes or no|Choice list|Date or time|Link|Tags|Printed and exported|Exported only|Editor only)$/, owner: engine, outcome: "view", evidence: "editor.spec: draft field type and scope are pressed choices" },
  { surface: "flow", name: /^(Create field|Add palette slot|Delete|Delete, keep colors)$/, owner: engine, outcome: "project", evidence: "editor.spec: project definitions; refusals explain why" },
  // Layout pages
  { surface: "layout", name: /^(relaxed|standard|compact|analytical)$/, owner: engine, outcome: "project", evidence: "editor.spec: spacing" },
  { surface: "layout", name: /^Add (heading|text|button|link-button|text-field|actions|surface|responsive-grid|alert|metric-card|workflow)$/, owner: engine, outcome: "project", evidence: "ui-conformance: declared form row; rapid repeated activation" },
  { surface: "layout", name: /^Add into .+$/, owner: engine, outcome: "view", evidence: "ui-conformance: declared form row (pressed target)" },
  { surface: "layout", name: /^(Move earlier|Move later|Remove): .+$/, owner: engine, outcome: "project", evidence: "ui-conformance: Layout actions change authoritative state" },
  { surface: "layout", name: /^Export as HTML: .+$/, owner: engine, outcome: "export", evidence: "WorkflowStudioTests: component tree export" },
  { surface: "layout", name: /^Choose icon for .+$/, owner: engine, outcome: "view", evidence: "icons.spec: a Layout button gets an icon by keyboard alone" },
  { surface: "layout", name: /^(alignment|type|inputType|required|level|density): .+$/, owner: engine, outcome: "project", evidence: "ui-conformance: Layout actions change authoritative state" },
  // Workflows (the component's own controls are Forma's)
  { surface: "workflows", name: /^(New workflow|Save workflows in browser|Open saved workflows)$/, owner: engine, outcome: "persistence", evidence: "workflow.spec: a workflow persists in the browser and reopens unchanged" },
  { surface: "workflows", name: /^Download file$/, owner: "Studio workflow adapter (browser download)", outcome: "export", evidence: "workflow.spec: external round trip; disabled with an explanation when no workflow is open" },
  { surface: "workflows", name: /^.+ (fully-supported|partially-supported|unsupported|invalid)$/, owner: engine, outcome: "navigation", evidence: "workflow.spec: open workflows list" },
  { surface: "workflows", name: /^(Add step|Arrange|Zoom out|Zoom in|Actual size|Add group|Start: .+)$/, owner: workflowComponent, outcome: "project", evidence: "workflow.spec: the workflow surface is keyboard operable (Forma's component conformance is Forma's)" },
  // HTML export (every surface)
  { name: /^(HTML fragment|Complete HTML document|No brand|Echelon|Example Harbor|Interactive workflow \(adds the public forma-workflow runtime\))$/, owner: engine, outcome: "view", evidence: "workflow.spec: Studio exports the current workflow; ui-conformance: declared form row" },
  { name: /^(Export Layout page|Export current workflow)$/, owner: engine, outcome: "export", evidence: "ui-conformance: declared form row; explains when there is nothing to export" },
  { name: /^(Copy HTML|Download HTML)$/, owner: "Limen clipboard effect / Studio adapter download", outcome: "export", evidence: "icons.spec and workflow.spec export tests; disabled until something is exported" }
];

/** The inventory entry for a control's accessible name on a surface, or undefined. */
export const classify = (surface, name) => ACTION_INVENTORY.find((entry) => (!entry.surface || entry.surface === surface) && entry.name.test(name));

const structureSelect = async (page, name) => {
  const control = page.getByRole("navigation", { name: "Structure" }).getByRole("button", { name, exact: true });
  await control.focus();
  await control.press("Enter");
  await control.and(page.locator("[aria-current=true]")).waitFor();
};

/** Surfaces and the states that expose their conditional controls. */
export const SURFACES = [
  { id: "flow", enter: async () => {} },
  { id: "flow", enter: (page) => structureSelect(page, "Activity: Approve spend, lane Finance") },
  {
    id: "layout",
    enter: async (page) => {
      const add = async (name) => { const b = page.getByRole("button", { name, exact: true }); await b.focus(); await b.press("Enter"); };
      await add("Add Layout page");
      for (const kind of ["heading", "text", "button", "link-button", "text-field", "actions", "surface", "responsive-grid", "alert", "metric-card", "workflow"]) await add(`Add ${kind}`);
      await page.getByRole("list", { name: "Page structure" }).getByRole("listitem").nth(10).waitFor();
    }
  },
  {
    id: "workflows",
    enter: async (page) => {
      const go = page.getByRole("navigation", { name: "Surfaces" }).getByRole("button", { name: /^Workflows/ });
      await go.focus(); await go.press("Enter");
      const create = page.getByRole("button", { name: "New workflow", exact: true });
      await create.focus(); await create.press("Enter");
      await page.getByRole("navigation", { name: "Open workflows" }).getByRole("button").first().waitFor();
    }
  }
];
