import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";

// Flow editor vertical slice (#11): every canonical change is one F# engine
// command; the canvas, Structure list and inspector are projections of it.
const open = async (page, width = 1400) => {
  await page.setViewportSize({ width, height: 1000 });
  await page.goto("/");
  await page.waitForFunction(() => document.documentElement.dataset.studioReady === "true");
  await expect(page.locator(".ef-diagram__canvas .ef-diagram-node")).toHaveCount(7);
};
const history = (page) => page.locator('[data-text="historyCount"]');
const node = (page, label) => page.locator(".ef-diagram__canvas .ef-diagram-node", { has: page.locator(".ef-diagram-node__label", { hasText: label }) });
const structure = (page) => page.getByRole("navigation", { name: "Structure" });
const outlineItem = (page, text) => structure(page).locator("button[data-event=select]").filter({ hasText: text });
const status = (page) => page.getByRole("status");
// Geometry and colors arrive as data-* values that studio-projection.css maps onto
// Forma's custom properties (Limen 0.7.0 refuses inline style): read what CSS computed.
const computed = (locator, name) => locator.evaluate((el, property) => getComputedStyle(el).getPropertyValue(property).trim(), name);
const position = async (locator) => ({ x: parseInt(await computed(locator, "--ef-diagram-x")), y: parseInt(await computed(locator, "--ef-diagram-y")) });

test.beforeEach(async ({ page }) => {
  page.on("console", (message) => {
    if (message.type() === "error" && message.text().startsWith("Limen")) throw new Error(message.text());
  });
});

test("the sample renders with Forma contracts and hides source-only metadata on the canvas", async ({ page }) => {
  await open(page);
  await expect(page.locator("path.ef-diagram-connector")).toHaveCount(7);
  await expect(structure(page).locator("button[data-event=select]")).toHaveCount(14);
  await expect(page.locator(".ef-diagram__canvas")).not.toContainText("CC-7731");
  await expect(page.locator(".ef-diagram__canvas")).not.toContainText("Cost center");
  await expect(node(page, "Within budget?")).toHaveAttribute("data-ef-shape", "diamond");
  const adornerClasses = await page.locator(".ef-diagram__canvas [class*='studio-']").evaluateAll((els) => els.map((el) => el.getAttribute("class")));
  expect(adornerClasses.every((c) => !/\bef-diagram-(?!node|connector)\S*studio/.test(c))).toBe(true);
});

test("the Structure list selects without the canvas and the inspector renames through a command", async ({ page }) => {
  await open(page);
  await outlineItem(page, "Activity: Approve spend").focus();
  await page.keyboard.press("Enter");
  await expect(outlineItem(page, "Activity: Approve spend")).toHaveAttribute("aria-current", "true");
  await expect(history(page)).toHaveText("0");
  const label = page.getByLabel("Label");
  await label.fill("Approve purchase");
  await label.press("Tab");
  await expect(node(page, "Approve purchase")).toHaveCount(1);
  await expect(history(page)).toHaveText("1");
  await page.getByRole("button", { name: "Undo" }).click();
  await expect(node(page, "Approve spend")).toHaveCount(1);
  await page.getByRole("button", { name: "Redo" }).click();
  await expect(node(page, "Approve purchase")).toHaveCount(1);
});

test("a completed drag is one command; the preview never changes the project", async ({ page }) => {
  await open(page);
  const target = node(page, "Request submitted");
  const before = await position(target);
  const box = await target.boundingBox();
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
  await page.mouse.down();
  await page.mouse.move(box.x + box.width / 2 + 30, box.y + box.height / 2 + 10, { steps: 6 });
  await expect(history(page)).toHaveText("0");
  expect(await position(target)).toEqual(before);
  await page.mouse.move(box.x + box.width / 2 + 60, box.y + box.height / 2 + 20, { steps: 6 });
  await page.mouse.up();
  await expect(history(page)).toHaveText("1");
  await expect.poll(() => position(target)).toEqual({ x: before.x + 60, y: before.y + 20 });
  await page.getByRole("button", { name: "Undo" }).click();
  await expect.poll(() => position(target)).toEqual(before);
});

test("arrow keys move a focused item through the same command path", async ({ page }) => {
  await open(page);
  const target = node(page, "Prepare request");
  const before = await position(target);
  await target.focus();
  await page.keyboard.press("ArrowRight");
  await expect.poll(() => position(target)).toEqual({ x: before.x + 8, y: before.y });
  await page.keyboard.press("Shift+ArrowDown");
  await expect.poll(() => position(target)).toEqual({ x: before.x + 8, y: before.y + 1 });
  await expect(history(page)).toHaveText("2");
});

test("connecting needs no drag, and illegal connections are refused with a reason", async ({ page }) => {
  await open(page);
  await outlineItem(page, "Start: Request submitted").click();
  await page.getByRole("button", { name: "Connect from selection" }).click();
  await expect(status(page)).toContainText("Choose the item that Request submitted connects to");
  await outlineItem(page, "Activity: Revise request").click();
  await expect(page.locator("path.ef-diagram-connector")).toHaveCount(8);
  await expect(outlineItem(page, "Connector: Request submitted to Revise request")).toHaveCount(1);
  await outlineItem(page, "End: Order placed").click();
  await page.getByRole("button", { name: "Connect from selection" }).click();
  await outlineItem(page, "Activity: Prepare request").click();
  await expect(status(page)).toContainText("An end node cannot have outgoing flow");
  await expect(page.locator("path.ef-diagram-connector")).toHaveCount(8);
});

test("metadata edits change the mapped color without touching other layers", async ({ page }) => {
  await open(page);
  await outlineItem(page, "Activity: Approve spend").click();
  await expect(page.locator("#inspector-fill-source")).toContainText("mapping Status color");
  await page.getByRole("group", { name: "Field to edit" }).getByRole("button", { name: "Status" }).click();
  await page.getByRole("group", { name: "Value", exact: true }).getByRole("button", { name: "Done" }).click();
  await expect(node(page, "Approve spend")).toContainText("Done");
  await expect(page.locator("#inspector-fill-source")).toContainText("Forma default");
  await page.getByRole("button", { name: "Clear to default" }).click();
  await expect(node(page, "Approve spend")).toContainText("Not started");
  await expect(node(page, "Approve spend").locator("[data-ef-value-state='default']")).toBeVisible();
});

test("fill color accepts text entry, records its source and resets to the next layer", async ({ page }) => {
  await open(page);
  await outlineItem(page, "Start: Request submitted").click();
  const fill = page.getByLabel("Fill color (#rrggbb)");
  await fill.fill("#FFE0B2");
  await fill.press("Tab");
  await expect(node(page, "Request submitted")).toHaveAttribute("data-fill", "#ffe0b2");
  await expect.poll(() => computed(node(page, "Request submitted"), "--ef-diagram-fill")).toBe("rgb(255, 224, 178)");
  await expect(page.locator("#inspector-fill-source")).toContainText("literal #ffe0b2 (set on this item)");
  await fill.fill("teal-ish");
  await fill.press("Tab");
  await expect(status(page)).toContainText("Not changed");
  await page.getByRole("button", { name: "Reset fill" }).click();
  await expect(node(page, "Request submitted")).toHaveAttribute("data-fill", "");
  await expect.poll(() => computed(node(page, "Request submitted"), "--ef-diagram-fill")).toBe("");
});

test("save and reopen restore the canonical project with a fresh history", async ({ page }) => {
  await open(page);
  await outlineItem(page, "Activity: Revise request").click();
  const label = page.getByLabel("Label");
  await label.fill("Revise and resubmit");
  await label.press("Tab");
  await page.getByRole("button", { name: "Save", exact: true }).click();
  await expect(status(page)).toHaveText("Saved.");
  await page.reload();
  await page.waitForFunction(() => document.documentElement.dataset.studioReady === "true");
  await expect(node(page, "Revise request")).toHaveCount(1);
  await page.getByRole("button", { name: "Open saved" }).click();
  await expect(node(page, "Revise and resubmit")).toHaveCount(1);
  await expect(page.getByRole("button", { name: "Undo" })).toBeDisabled();
});

test("at 390px the page reflows and the canvas scrolls in its own region", async ({ page }) => {
  await open(page, 390);
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(1);
  const viewport = page.getByRole("group", { name: /Diagram canvas/ });
  expect(await viewport.evaluate((el) => el.scrollWidth > el.clientWidth)).toBe(true);
  await outlineItem(page, "Decision: Within budget?").click();
  await expect(outlineItem(page, "Decision: Within budget?")).toHaveAttribute("aria-current", "true");
});

test("an ordinary user defines a choice field, sets it and colors by it without JSON", async ({ page }) => {
  await open(page);
  await page.getByLabel("Field name").fill("Risk level");
  await page.getByLabel("Field name").press("Tab");
  await page.getByRole("group", { name: "Field type" }).getByRole("button", { name: "Choice list" }).click();
  await page.getByLabel("Choices").fill("Low, High");
  await page.getByLabel("Choices").press("Tab");
  await page.getByRole("button", { name: "Create field" }).click();
  await expect(page.getByRole("status")).toContainText("Field Risk level added");
  await expect(history(page)).toHaveText("1");

  await page.getByLabel("New slot name").fill("Risk high");
  await page.getByLabel("New slot name").press("Tab");
  await page.getByLabel("New slot color (#rrggbb)").fill("#fbd3e9");
  await page.getByLabel("New slot color (#rrggbb)").press("Tab");
  await page.getByRole("button", { name: "Add palette slot" }).click();
  await expect(page.getByRole("list", { name: "Palette slots" })).toContainText("Risk high");

  await outlineItem(page, "Activity: Prepare request").click();
  await page.getByRole("group", { name: "Field to edit" }).getByRole("button", { name: "Risk level" }).click();
  await page.getByRole("group", { name: "Value", exact: true }).getByRole("button", { name: "High" }).click();
  await expect(node(page, "Prepare request")).toContainText("High");

  await page.getByRole("group", { name: "When the value is" }).getByRole("button", { name: "High" }).click();
  await page.getByRole("group", { name: "Use the palette slot" }).getByRole("button", { name: "Risk high" }).click();
  await page.getByRole("button", { name: "Add color rule" }).click();
  await expect(page.getByRole("list", { name: "Color rules" })).toContainText("Risk level is High");
  // Prepare request keeps its authored highlight: an override beats a mapping.
  await expect(page.locator("#inspector-fill-source")).toContainText("set on this item");
  await page.getByRole("button", { name: "Reset fill" }).click();
  await expect(node(page, "Prepare request")).toHaveAttribute("data-fill", "#fbd3e9");
  await expect.poll(() => computed(node(page, "Prepare request"), "--ef-diagram-fill")).toBe("rgb(251, 211, 233)");
  await expect(page.locator("#inspector-fill-source")).toContainText("Risk level is High");
});

test("an in-use palette slot cannot be deleted silently; materializing keeps the colors", async ({ page }) => {
  await open(page);
  const row = page.getByRole("list", { name: "Palette slots" }).getByRole("listitem").filter({ hasText: "Author highlight" });
  await expect(row).toContainText("2 uses");
  await row.getByRole("button", { name: "Delete", exact: true }).click();
  await expect(page.getByRole("status")).toContainText("Not changed");
  await row.getByRole("button", { name: "Delete, keep colors" }).click();
  await expect(page.getByRole("list", { name: "Palette slots" })).not.toContainText("Author highlight");
  await expect(node(page, "Prepare request")).toHaveAttribute("data-fill", "#efe6fb");
  await expect.poll(() => computed(node(page, "Prepare request"), "--ef-diagram-fill")).toBe("rgb(239, 230, 251)");
});

test("moving an activity between lanes reports the responsibility change", async ({ page }) => {
  await open(page);
  await outlineItem(page, "Activity: Approve spend").click();
  await page.getByRole("group", { name: "Lane (responsibility)" }).getByRole("button", { name: "Procurement" }).click();
  await expect(page.getByRole("status")).toContainText("Responsibility for 'Approve spend' changes from Finance to Procurement");
  await expect(node(page, "Approve spend")).toContainText("Procurement");
});

test("the editor has no automatically detectable WCAG A/AA violations, with and without a selection", async ({ page }) => {
  const wcag = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"];
  await open(page);
  const empty = await new AxeBuilder({ page }).withTags(wcag).analyze();
  expect(empty.violations, JSON.stringify(empty.violations, null, 2)).toEqual([]);
  await outlineItem(page, "Activity: Approve spend").click();
  await page.getByRole("group", { name: "Field to edit" }).getByRole("button", { name: "Status" }).click();
  const selected = await new AxeBuilder({ page }).withTags(wcag).analyze();
  expect(selected.violations, JSON.stringify(selected.violations, null, 2)).toEqual([]);
});

test("forced colors keep selection and boundaries visible", async ({ page }) => {
  await open(page);
  await page.emulateMedia({ forcedColors: "active" });
  await outlineItem(page, "Activity: Approve spend").click();
  const selected = node(page, "Approve spend");
  await expect(selected).toHaveClass(/studio-selected/);
  expect(await selected.evaluate((el) => getComputedStyle(el).outlineStyle)).toBe("dashed");
  expect(await node(page, "Prepare request").evaluate((el) => getComputedStyle(el).borderTopStyle)).toBe("solid");
});

test("multi-selection edits metadata atomically, shows mixed values and arranges in one step", async ({ page }) => {
  await open(page);
  await outlineItem(page, "Activity: Prepare request").click();
  await structure(page).getByRole("button", { name: "Add Revise request to selection" }).click();
  await structure(page).getByRole("button", { name: "Add Issue purchase order to selection" }).click();
  await expect(status(page)).toHaveText("3 selected.");
  await expect(page.locator(".studio-meta")).toContainText("Mixed");
  const before = await history(page).textContent();
  await page.getByRole("group", { name: "Field to edit" }).getByRole("button", { name: "Status" }).click();
  await page.getByRole("group", { name: "Value", exact: true }).getByRole("button", { name: "Done" }).click();
  await expect(history(page)).toHaveText(String(Number(before) + 1));
  for (const label of ["Prepare request", "Revise request", "Issue purchase order"]) await expect(node(page, label)).toContainText("Done");
  await page.getByRole("button", { name: "Align top edges" }).click();
  await expect(history(page)).toHaveText(String(Number(before) + 2));
  const tops = await Promise.all(["Prepare request", "Revise request", "Issue purchase order"].map(async (label) => (await position(node(page, label))).y));
  expect(new Set(tops).size).toBe(1);
  await page.getByRole("button", { name: "Undo" }).click();
  expect(new Set(await Promise.all(["Prepare request", "Issue purchase order"].map(async (label) => (await position(node(page, label))).y))).size).toBe(2);
});

test("Layout and Flow share one session: stack, heading, edit, reorder, spacing, undo, redo, save, reopen", async ({ page }) => {
  await open(page);
  await page.getByRole("button", { name: "Add Layout page" }).click();
  await expect(page.getByRole("heading", { name: "Layout: Page 1" })).toBeVisible();
  await page.getByRole("button", { name: "Add heading" }).click();
  await page.getByRole("button", { name: "Add heading" }).click();
  const fields = page.getByRole("list", { name: "Page structure" }).getByRole("textbox");
  await fields.nth(0).fill("Welcome");
  await fields.nth(0).press("Tab");
  await fields.nth(1).fill("Details");
  await fields.nth(1).press("Tab");
  const preview = page.getByRole("region", { name: "Page preview" });
  await expect(preview.getByRole("heading")).toHaveText(["Welcome", "Details"]);
  await page.getByRole("list", { name: "Page structure" }).getByRole("listitem").nth(1).getByRole("button", { name: "Move earlier" }).click();
  await expect(preview.getByRole("heading")).toHaveText(["Details", "Welcome"]);
  await page.getByRole("group", { name: "Stack spacing" }).getByRole("button", { name: "compact" }).click();
  await expect(preview).toHaveAttribute("data-density", "compact");
  const layoutEntries = Number(await history(page).textContent());
  // Switch surface, edit the Flow diagram, then undo across both surfaces.
  await page.getByRole("button", { name: "Flow: Purchase request" }).click();
  const target = node(page, "Request submitted");
  const before = await position(target);
  await target.focus();
  await page.keyboard.press("ArrowRight");
  await expect(history(page)).toHaveText(String(layoutEntries + 1));
  await page.getByRole("button", { name: "Undo" }).click();
  await expect.poll(() => position(target)).toEqual(before);
  await page.getByRole("button", { name: "Undo" }).click();
  await page.getByRole("button", { name: "Layout: Page 1" }).click();
  await expect(preview).toHaveAttribute("data-density", "standard");
  await page.getByRole("button", { name: "Redo" }).click();
  await expect(preview).toHaveAttribute("data-density", "compact");
  await page.getByRole("button", { name: "Save", exact: true }).click();
  await expect(status(page)).toHaveText("Saved.");
  await page.reload();
  await page.waitForFunction(() => document.documentElement.dataset.studioReady === "true");
  await page.getByRole("button", { name: "Open saved" }).click();
  await page.getByRole("button", { name: "Layout: Page 1" }).click();
  await expect(preview.getByRole("heading")).toHaveText(["Details", "Welcome"]);
  await expect(preview).toHaveAttribute("data-density", "compact");
});

const size = async (locator) => ({ w: parseInt(await computed(locator, "--ef-diagram-w")), h: parseInt(await computed(locator, "--ef-diagram-h")) });

test("Resize: the handle drag and the inspector fields are each one command", async ({ page }) => {
  await open(page);
  const target = node(page, "Request submitted");
  await target.click();
  const before = await size(target);
  const entries = Number(await history(page).textContent());
  const handle = target.locator(".studio-resize-handle");
  const box = await handle.boundingBox();
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
  await page.mouse.down();
  await page.mouse.move(box.x + 20, box.y + 10, { steps: 6 });
  await page.mouse.move(box.x + 46, box.y + 26, { steps: 6 });
  await page.mouse.up();
  await expect.poll(() => size(target)).toEqual({ w: before.w + 40, h: before.h + 20 });
  await expect(history(page)).toHaveText(String(entries + 1));
  await expect(target).toHaveAttribute("aria-pressed", "true");
  const width = page.getByRole("group", { name: "Size" }).getByLabel("Width");
  await expect(width).toHaveValue(String(before.w + 40));
  await width.fill("300");
  await width.press("Tab");
  await expect.poll(() => size(target)).toEqual({ w: 300, h: before.h + 20 });
  await expect(history(page)).toHaveText(String(entries + 2));
  await width.fill("0");
  await width.press("Tab");
  await expect(status(page)).toContainText("Not changed");
  await expect(history(page)).toHaveText(String(entries + 2));
  await page.getByRole("button", { name: "Undo" }).click();
  await page.getByRole("button", { name: "Undo" }).click();
  await expect.poll(() => size(target)).toEqual(before);
});

test("Reconnect: an endpoint drag and the keyboard path are each one Reconnect command", async ({ page }) => {
  await open(page);
  // Select an existing connector through the Structure list.
  const structure = page.getByRole("navigation", { name: "Structure" });
  const connectors = structure.getByRole("button", { name: /^Connector:/ });
  await connectors.first().click();
  const handles = page.locator(".studio-endpoint");
  await expect(handles).toHaveCount(2);
  const entries = Number(await history(page).textContent());
  const before = await page.locator("path.ef-diagram-connector.studio-selected-wire").getAttribute("d");

  // Pointer path: drag the end handle onto another node.
  const destination = node(page, "Revise request");
  const end = await handles.nth(1).boundingBox();
  const drop = await destination.boundingBox();
  await page.mouse.move(end.x + end.width / 2, end.y + end.height / 2);
  await page.mouse.down();
  await page.mouse.move(drop.x + drop.width / 2, drop.y + drop.height / 2, { steps: 10 });
  await page.mouse.up();
  await expect(status(page)).toHaveText("Reconnected.");
  await expect(history(page)).toHaveText(String(entries + 1));
  const dragged = await page.locator("path.ef-diagram-connector.studio-selected-wire").getAttribute("d");
  expect(dragged).not.toBe(before);

  // Keyboard path: press the handle, then choose a node.
  await page.locator(".studio-endpoint[data-end='target']").focus();
  await page.keyboard.press("Enter");
  await expect(page.getByRole("button", { name: "Cancel reconnecting" })).toBeVisible();
  await node(page, "Approve spend").focus();
  await page.keyboard.press("Enter");
  await expect(history(page)).toHaveText(String(entries + 2));
  await expect(structure.getByRole("button", { name: /^Connector: .* to Approve spend/ }).first()).toBeVisible();

  await page.getByRole("button", { name: "Undo" }).click();
  await page.getByRole("button", { name: "Undo" }).click();
  await connectors.first().click();
  await expect(page.locator("path.ef-diagram-connector.studio-selected-wire")).toHaveAttribute("d", before);
});

test("Zoom and snapping are view state; drags stay in logical units", async ({ page }) => {
  await open(page);
  const entries = Number(await history(page).textContent());
  const view = page.getByRole("toolbar", { name: "View" });
  await view.getByRole("button", { name: "Zoom in" }).click();
  await view.getByRole("button", { name: "Zoom in" }).click();
  await expect(view.locator('[data-text="zoomLabel"]')).toHaveText("150%");
  await expect(history(page)).toHaveText(String(entries));

  const target = node(page, "Prepare request");
  const before = await position(target);
  const box = await target.boundingBox();
  await page.mouse.move(box.x + box.width / 2, box.y + 20);
  await page.mouse.down();
  await page.mouse.move(box.x + box.width / 2 + 30, box.y + 20, { steps: 5 });
  await page.mouse.move(box.x + box.width / 2 + 60, box.y + 20 + 30, { steps: 5 });
  await page.mouse.up();
  await expect.poll(() => position(target)).toEqual({ x: before.x + 40, y: before.y + 20 });

  await view.getByRole("button", { name: "Snap to grid" }).click();
  await expect(view.getByRole("button", { name: "Snap to grid" })).toHaveAttribute("aria-pressed", "true");
  await view.getByRole("button", { name: "Actual size" }).click();
  const moved = await target.boundingBox();
  await page.mouse.move(moved.x + moved.width / 2, moved.y + 20);
  await page.mouse.down();
  await page.mouse.move(moved.x + moved.width / 2 + 13, moved.y + 20 + 5, { steps: 5 });
  await page.mouse.up();
  await expect(history(page)).toHaveText(String(entries + 2));
  const snappedTo = await position(target);
  expect(snappedTo.x % 8).toBe(0);
  expect(snappedTo.y % 8).toBe(0);
});

test("Templates: a built-in or copied fragment inserts in one step with its dependencies reviewed first", async ({ page }) => {
  await open(page);
  const templates = page.getByRole("region", { name: "Templates" });
  const entries = Number(await history(page).textContent());
  const edgesBefore = await page.locator("path.ef-diagram-connector").count();

  await templates.getByRole("button", { name: "Approval decision" }).click();
  await expect(templates.getByRole("list", { name: "What inserting brings along" })).toContainText("No fields, colors or rules");
  await templates.getByRole("button", { name: "Insert template" }).click();
  await expect(status(page)).toHaveText("Inserted Approval decision.");
  await expect(history(page)).toHaveText(String(entries + 1));
  await expect(node(page, "Approved?")).toHaveAttribute("aria-pressed", "true");
  await expect(page.locator("path.ef-diagram-connector")).toHaveCount(edgesBefore + 2);

  // Copy a node that carries metadata and a color rule, then review and insert it.
  await page.getByRole("button", { name: "Undo" }).click();
  await node(page, "Approve spend").click();
  await templates.getByRole("button", { name: "Copy selection as template" }).click();
  await expect(status(page)).toContainText("Copied 1 item(s)");
  const plan = templates.getByRole("list", { name: "What inserting brings along" });
  await expect(plan).toContainText("reuses the identical one already in this project");
  await templates.getByRole("button", { name: "Insert template" }).click();
  await expect(node(page, "Approve spend")).toHaveCount(2);
  await expect(history(page)).toHaveText(String(entries + 1));
  await page.getByRole("button", { name: "Undo" }).click();
  await expect(node(page, "Approve spend")).toHaveCount(1);
});

test("Review lists semantic changes since the last save, separating movement from meaning", async ({ page }) => {
  await open(page);
  const review = page.getByRole("region", { name: "Review changes" });
  await expect(review).toContainText("No changes since the last save or open.");
  const target = node(page, "Prepare request");
  await target.focus();
  await page.keyboard.press("ArrowRight");
  await target.click();
  const label = page.getByLabel("Label", { exact: true });
  await label.fill("Draft request");
  await label.press("Tab");
  const changes = review.getByRole("list", { name: "Changes since the last save or open" }).getByRole("listitem");
  await expect(changes).toHaveCount(2);
  await expect(changes.filter({ hasText: "Position or size" })).toHaveCount(1);
  await expect(changes.filter({ hasText: "Label" })).toHaveCount(1);
  await page.getByRole("button", { name: "Save", exact: true }).click();
  await expect(status(page)).toHaveText("Saved.");
  await expect(review).toContainText("No changes since the last save or open.");
});

test("Alignment guides show while dragging and the drop lands on the aligned edge", async ({ page }) => {
  await open(page);
  const target = node(page, "Prepare request");
  const before = await position(target);
  const box = await target.boundingBox();
  await page.mouse.move(box.x + box.width / 2, box.y + 20);
  await page.mouse.down();
  // 27 units right puts the left edge 3 units from "Within budget?" (x + 30).
  await page.mouse.move(box.x + box.width / 2 + 15, box.y + 20, { steps: 4 });
  await page.mouse.move(box.x + box.width / 2 + 27, box.y + 20, { steps: 4 });
  await expect(page.locator('.studio-guide[data-axis="x"]:not([hidden])')).toHaveCount(1);
  await page.mouse.up();
  await expect(page.locator(".studio-guide")).toHaveCount(0);
  await expect.poll(() => position(target)).toEqual({ x: before.x + 30, y: before.y });
});

// Simulates another tab changing the saved copy: edits the stored project text.
const editSavedCopy = (page, from, to) =>
  page.evaluate(([a, b]) => {
    const key = "forma-studio.project";
    localStorage.setItem(key, localStorage.getItem(key).replaceAll(a, b));
  }, [from, to]);

test("Merge review: saved-copy changes merge with local edits; a conflict is resolved per item; one undo reverts", async ({ page }) => {
  await open(page);
  await page.getByRole("button", { name: "Save", exact: true }).click();
  await expect(status(page)).toHaveText("Saved.");
  const review = page.getByRole("region", { name: "Review changes" });
  await review.getByRole("button", { name: "Check the saved copy for changes" }).click();
  await expect(status(page)).toContainText("has not changed");

  // Another tab renames two nodes; locally we rename one of the same nodes differently.
  await editSavedCopy(page, "Revise request", "Revise and resubmit");
  await editSavedCopy(page, "Issue purchase order", "Raise purchase order");
  await node(page, "Issue purchase order").click();
  const label = page.getByLabel("Label", { exact: true });
  await label.fill("Send purchase order");
  await label.press("Tab");
  const entries = Number(await history(page).textContent());

  await review.getByRole("button", { name: "Check the saved copy for changes" }).click();
  await expect(review.getByRole("list", { name: "Changes in the saved copy" }).getByRole("listitem")).toHaveCount(2);
  const conflicts = review.getByRole("list", { name: "Merge conflicts" }).getByRole("listitem");
  await expect(conflicts).toHaveCount(1);
  await expect(conflicts.first()).toContainText("node:order");
  await conflicts.first().getByRole("button", { name: "Use saved" }).click();
  await expect(conflicts.first().getByRole("button", { name: "Use saved" })).toHaveAttribute("aria-pressed", "true");
  await review.getByRole("button", { name: "Apply merge" }).click();
  await expect(status(page)).toContainText("Merged the saved changes");
  await expect(history(page)).toHaveText(String(entries + 1));
  await expect(node(page, "Revise and resubmit")).toHaveCount(1);
  await expect(node(page, "Raise purchase order")).toHaveCount(1);
  await expect(review).toContainText("No changes since the last save or open.");

  await page.getByRole("button", { name: "Undo" }).click();
  await expect(node(page, "Send purchase order")).toHaveCount(1);
  await expect(node(page, "Revise request")).toHaveCount(1);
});

test("the canvas is laid out and colored by the stylesheet from data-* values, never inline style", async ({ page }) => {
  await open(page);
  const canvas = page.locator(".ef-diagram__canvas");
  // Limen 0.7.0 refuses inline style as a binding target (limen#18): nothing projected carries one.
  const projected = page.locator(".ef-diagram__canvas, .ef-diagram__canvas :is(.ef-diagram-node, .ef-diagram-group, .ef-diagram-connector__label, path.ef-diagram-connector)");
  expect(await projected.evaluateAll((els) => els.filter((el) => el.hasAttribute("style")).map((el) => el.outerHTML.slice(0, 80)))).toEqual([]);

  // Geometry: every node, group and connector label sits exactly at its projected logical position.
  const misplaced = await page.locator(".ef-diagram__canvas :is(.ef-diagram-node, .ef-diagram-group, .ef-diagram-connector__label)").evaluateAll((els) =>
    els.filter((el) => el.offsetLeft !== Number(el.dataset.x) || el.offsetTop !== Number(el.dataset.y)).map((el) => el.textContent.trim().slice(0, 40)));
  expect(misplaced).toEqual([]);
  expect(await page.locator(".ef-diagram__canvas .ef-diagram-node").evaluateAll((els) => els.every((el) => el.offsetWidth === Number(el.dataset.w)))).toBe(true);
  const area = await canvas.evaluate((el) => ({ w: el.offsetWidth, h: el.offsetHeight, dw: Number(el.dataset.width), dh: Number(el.dataset.height) }));
  expect([area.w, area.h]).toEqual([area.dw, area.dh]);

  // A token color stays a token (themes and brands still apply); a literal is read as a <color>.
  const decision = node(page, "Within budget?");
  await expect(decision).toHaveAttribute("data-stroke", "--ef-color-accent-primary");
  const token = await page.evaluate(() => getComputedStyle(document.documentElement).getPropertyValue("--ef-color-accent-primary").trim());
  expect(token).not.toBe("");
  expect(await computed(decision, "--ef-diagram-stroke")).toBe(token);
  expect(await computed(decision, "--ef-diagram-accent")).toBe(token);

  // Zoom is a view preference applied by CSS from data-zoom.
  await page.getByRole("toolbar", { name: "View" }).getByRole("button", { name: "Zoom in" }).click();
  await expect(canvas).toHaveAttribute("data-zoom", "1.25");
  await expect(canvas).toHaveCSS("zoom", "1.25");

  // Endpoint handles of the selected connector sit on its first and last route points.
  await page.getByRole("navigation", { name: "Structure" }).getByRole("button", { name: /^Connector:/ }).first().click();
  const handles = page.locator(".studio-endpoint");
  await expect(handles).toHaveCount(2);
  const route = (await page.locator("path.ef-diagram-connector.studio-selected-wire").getAttribute("d")).match(/-?\d+/g).map(Number);
  const placed = await handles.evaluateAll((els) => els.map((el) => [parseInt(getComputedStyle(el).getPropertyValue("--studio-x")), parseInt(getComputedStyle(el).getPropertyValue("--studio-y"))]));
  expect(placed).toEqual([route.slice(0, 2), route.slice(-2)]);
});

test("a drag leaves no text selection, so the next drag on the same node is a drag too", async ({ page }) => {
  await open(page);
  await page.evaluate(() => { window.studioNativeDrags = 0; document.addEventListener("dragstart", () => { window.studioNativeDrags += 1; }, true); });
  const entries = Number(await history(page).textContent());
  const target = node(page, "Prepare request");
  for (const [n, dx] of [[1, 40], [2, 24]]) {
    const before = await position(target);
    // Start on the kind text: a text selection left there would make this a native text drag.
    const kind = await target.locator(".ef-diagram-node__kind").boundingBox();
    await page.mouse.move(kind.x + 4, kind.y + kind.height / 2);
    await page.mouse.down();
    await page.mouse.move(kind.x + 4 + dx, kind.y + kind.height / 2 + 8, { steps: 6 });
    await page.mouse.up();
    await expect(history(page)).toHaveText(String(entries + n));
    await expect.poll(() => position(target)).toEqual({ x: before.x + dx, y: before.y + 8 });
    expect(await page.evaluate(() => getSelection().toString())).toBe("");
  }
  expect(await page.evaluate(() => window.studioNativeDrags)).toBe(0);
});
