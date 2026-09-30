import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";

// Flow editor vertical slice (#11): every canonical change is one F# engine
// command; the canvas, Structure list and inspector are projections of it.
const open = async (page, width = 1400) => {
  await page.setViewportSize({ width, height: 1000 });
  await page.goto("/");
  await page.waitForFunction(() => document.documentElement.dataset.studioReady === "true");
  await expect(page.locator(".ef-diagram__canvas article")).toHaveCount(7);
};
const history = (page) => page.locator('[data-text="historyCount"]');
const node = (page, label) => page.locator(".ef-diagram__canvas article", { has: page.locator(".ef-diagram-node__label", { hasText: label }) });
const structure = (page) => page.getByRole("navigation", { name: "Structure" });
const outlineItem = (page, text) => structure(page).getByRole("button").filter({ hasText: text });
const status = (page) => page.getByRole("status");
const position = (locator) => locator.evaluate((el) => ({ x: parseInt(el.style.getPropertyValue("--ef-diagram-x")), y: parseInt(el.style.getPropertyValue("--ef-diagram-y")) }));

test.beforeEach(async ({ page }) => {
  page.on("console", (message) => {
    if (message.type() === "error" && message.text().startsWith("Limen")) throw new Error(message.text());
  });
});

test("the sample renders with Forma contracts and hides source-only metadata on the canvas", async ({ page }) => {
  await open(page);
  await expect(page.locator("path.ef-diagram-connector")).toHaveCount(7);
  await expect(structure(page).getByRole("button")).toHaveCount(14);
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
  await expect(node(page, "Request submitted")).toHaveAttribute("style", /--ef-diagram-fill: #ffe0b2;/);
  await expect(page.locator("#inspector-fill-source")).toContainText("literal #ffe0b2 (set on this item)");
  await fill.fill("teal-ish");
  await fill.press("Tab");
  await expect(status(page)).toContainText("Not changed");
  await page.getByRole("button", { name: "Reset fill" }).click();
  await expect(node(page, "Request submitted")).not.toHaveAttribute("style", /--ef-diagram-fill/);
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
  await expect(node(page, "Prepare request")).toHaveAttribute("style", /--ef-diagram-fill: #fbd3e9;/);
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
  await expect(node(page, "Prepare request")).toHaveAttribute("style", /--ef-diagram-fill: #efe6fb;/);
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
