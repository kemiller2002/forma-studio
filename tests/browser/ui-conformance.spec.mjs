import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";
import { ACTION_INVENTORY, SURFACES, classify } from "./ui-action-inventory.mjs";
import {
  VIEWPORTS, alignmentVerdict, clippedControls, expectActionOutcome, measureDeclaredGroups, openExport, openStudio, pageOverflow, press, status
} from "./ui-conformance-helpers.mjs";

// Universal UI layout and interaction conformance for Forma Studio
// (Visual Engineering UI-LAYOUT-INTERACTION@1.0.0, forma-studio#35). Every test
// is tagged @uli and run by `npm run ui:conformance`. Assertions read state
// before and after an action; controls are found by role and accessible name;
// nothing waits a fixed time; keyboard is the activation path.

const structure = (page) => page.getByRole("list", { name: "Page structure" });
const field = (page, label) => page.getByRole("textbox", { name: label, exact: true });
const button = (scope, name) => scope.getByRole("button", { name, exact: true });
const properties = (page, component) => page.getByRole("group", { name: `Properties of ${component}`, exact: true });
const exported = (page) => page.getByRole("textbox", { name: "Exported HTML" });
const checks = (page) => page.getByRole("list", { name: "Checks before export" });

const setText = async (page, label, text) => {
  const input = field(page, label);
  await input.fill(text);
  await input.press("Tab");
  await expect(status(page)).toHaveText("Text changed.");
};

/** Builds a page with a responsive grid holding two text fields, by keyboard only. */
const formRow = async (page, { first = "First name", second = "Email", align = "top" } = {}) => {
  await press(button(page, "Add Layout page"));
  await expect(page.getByRole("heading", { name: "Layout: Page 1" })).toBeVisible();
  await press(button(page, "Add responsive-grid"));
  await press(button(page, "Add into responsive-grid page-1-responsive-grid"));
  await expect(button(page, "Add into responsive-grid page-1-responsive-grid")).toHaveAttribute("aria-pressed", "true");
  await press(button(page, "Add text-field"));
  await press(button(page, "Add text-field"));
  await expect(structure(page).getByRole("listitem")).toHaveCount(3);
  await setText(page, "text-field label for page-1-text-field", first);
  await setText(page, "text-field label for page-1-text-field-2", second);
  if (align) {
    const choice = button(properties(page, "responsive-grid page-1-responsive-grid"), `alignment: ${align}`);
    await expectActionOutcome({ control: choice, read: () => choice.getAttribute("aria-pressed"), expected: "true" });
  }
};

const exportDocument = async (page) => {
  await press(button(page.getByRole("group", { name: "Export as" }), "Complete HTML document"));
  const before = await exported(page).inputValue();
  await press(button(page, "Export Layout page"));
  await expect.poll(() => exported(page).inputValue()).not.toBe(before);
  return exported(page).inputValue();
};

test.beforeEach(async ({ page }) => {
  page.on("console", (message) => {
    if (message.type() === "error" && message.text().startsWith("Limen")) throw new Error(message.text());
  });
});

test("@uli ULI-L01 a declared form row is authored by keyboard, survives export, and its controls share a top edge at 320/390/768/1280 and in RTL", async ({ page, browser }) => {
  await openStudio(page);
  await formRow(page);
  await expect(structure(page)).toContainText("Declares an alignment group: its items share a top edge.");
  await expect(checks(page).getByRole("listitem")).toHaveCount(0);
  await expect(page.getByText("No problems found before export.", { exact: true })).toBeVisible();
  const html = await exportDocument(page);
  expect(html).toContain('data-uli-align-group="page-1-responsive-grid"');
  expect(html).toContain('data-uli-align-edge="top"');
  expect(html.match(/<input [^>]*data-uli-align-peer="page-1-responsive-grid"/g)).toHaveLength(2);

  const evidence = [];
  for (const dir of ["ltr", "rtl"]) {
    for (const width of VIEWPORTS) {
      const view = await browser.newPage();
      await openExport(view, html, { width, dir });
      expect(await pageOverflow(view), `export overflow at ${width}px ${dir}`).toBe(0);
      const [group] = await measureDeclaredGroups(view);
      const verdict = alignmentVerdict(group);
      evidence.push({ width, dir, ...verdict });
      // The composition is a row wherever there is room for two 16rem columns: the check is not vacuous there.
      if (width >= 768) expect(verdict.status, `${width}px ${dir}: ${JSON.stringify(verdict)}`).toBe("PASS");
      else expect(["PASS", "NOT_APPLICABLE"]).toContain(verdict.status);
      await view.close();
    }
  }
  test.info().annotations.push({ type: "ULI-L01 evidence", description: JSON.stringify(evidence.map(({ peers, ...rest }) => rest)) });
});

test("@uli ULI-L01 adversarial peers: Studio warns before export, and the browser confirms the stagger it warned about", async ({ page, browser }) => {
  await openStudio(page);
  const long = "Given name exactly as it appears on your passport or national identity card";
  await formRow(page, { first: long });
  await expect(checks(page)).toContainText("ULI-L01: In aligned group page-1-responsive-grid the label of page-1-text-field is longer than 40 characters");
  await setText(page, "text-field description for page-1-text-field-2", "We reply within one working day.");
  await expect(checks(page)).toContainText("ULI-L01: In aligned group page-1-responsive-grid some fields have a description and some do not");
  await expect(page.getByRole("list", { name: "Checks for the exported page" })).toContainText("some fields have a description");
  const html = await exportDocument(page);
  const view = await browser.newPage();
  await openExport(view, html, { width: 1280 });
  const verdict = alignmentVerdict((await measureDeclaredGroups(view))[0]);
  // The warning is truthful: Forma's field pattern places label and help text above
  // the input and has no row primitive that aligns peer controls (reported upstream).
  expect(verdict.status, JSON.stringify(verdict)).toBe("FAIL");
  expect(verdict.deltaCssPx).toBeGreaterThan(1);
  await view.close();
  // Repair through the authoring model: give both peers help text and a short label.
  await setText(page, "text-field label for page-1-text-field", "Given name");
  await setText(page, "text-field description for page-1-text-field", "As on your passport.");
  await setText(page, "text-field description for page-1-text-field-2", "We reply in a day.");
  await expect(checks(page).getByRole("listitem")).toHaveCount(0);
  const repaired = await exportDocument(page);
  const after = await browser.newPage();
  await openExport(after, repaired, { width: 1280 });
  expect(alignmentVerdict((await measureDeclaredGroups(after))[0]).status).toBe("PASS");
  await after.close();
});

test("@uli ULI-A01/A02 Layout actions change authoritative state: every content value, properties, nested order, removal and undo", async ({ page }) => {
  await openStudio(page, 390);
  await formRow(page, { align: null });
  const preview = page.getByRole("region", { name: "Page preview" });
  // A content value beyond the first one is editable and reaches the preview's source.
  await setText(page, "text-field name for page-1-text-field-2", "contact-email");
  await expect(field(page, "text-field name for page-1-text-field-2")).toHaveValue("contact-email");
  // Properties: set, then clear, each one command with an observable pressed state.
  const grid = properties(page, "responsive-grid page-1-responsive-grid");
  const top = button(grid, "alignment: top");
  await expectActionOutcome({ control: top, read: () => top.getAttribute("aria-pressed"), expected: "true" });
  const none = button(grid, "alignment: none");
  await expectActionOutcome({ control: none, read: () => top.getAttribute("aria-pressed"), expected: "false" });
  const required = button(properties(page, "text-field page-1-text-field"), "required: true");
  await expectActionOutcome({ control: required, read: () => required.getAttribute("aria-pressed"), expected: "true" });
  // Nested reorder: the second field moves before the first inside the grid.
  const order = () => preview.getByRole("paragraph").allTextContents();
  await expect.poll(order).toEqual(["responsive-grid: Summary", "text-field: First name", "text-field: Email"]);
  await expectActionOutcome({
    control: button(page, "Move earlier: text-field page-1-text-field-2"), read: order,
    expected: ["responsive-grid: Summary", "text-field: Email", "text-field: First name"]
  });
  // Removal, then undo restores it.
  await expectActionOutcome({ control: button(page, "Remove: text-field page-1-text-field"), read: () => structure(page).getByRole("listitem").count(), expected: 2 });
  await expect(status(page)).toHaveText("Removed page-1-text-field.");
  await expectActionOutcome({ control: button(page, "Undo"), read: () => structure(page).getByRole("listitem").count(), expected: 3 });
  // Removing a container removes its contents and clears it as the insertion target.
  await expectActionOutcome({ control: button(page, "Remove: responsive-grid page-1-responsive-grid"), read: () => structure(page).getByRole("listitem").count(), expected: 0 });
  await expect(button(page, "Add into page")).toHaveAttribute("aria-pressed", "true");
  await expect(page.getByRole("button", { name: "Add into responsive-grid page-1-responsive-grid" })).toHaveCount(0);
});

test("@uli ULI-A01 a new link button is never a placeholder link: flagged, exported as text, then a real link once it has a destination", async ({ page }) => {
  await openStudio(page, 320);
  await press(button(page, "Add Layout page"));
  await press(button(page, "Add link-button"));
  await expect(checks(page)).toContainText("ULI-A01: Link button page-1-link-button has no real destination");
  const html = await exportDocument(page);
  expect(html).not.toMatch(/href="#"|javascript:/);
  await expect(page.getByRole("list", { name: "Left out of the export" })).toContainText("link target is missing");
  await setText(page, "link-button href for page-1-link-button", "#");
  await expect(checks(page)).toContainText("has no real destination");
  await setText(page, "link-button href for page-1-link-button", "/help");
  await expect(checks(page)).not.toContainText("has no real destination");
  const linked = await exportDocument(page);
  expect(linked).toContain('<a class="ef-button" href="/help">Continue</a>');
  expect(await pageOverflow(page)).toBe(0);
});

test("@uli ULI-A07 rapid repeated activation adds exactly one item per activation and undo reverses each one", async ({ page }) => {
  await openStudio(page);
  await press(button(page, "Add Layout page"));
  const add = button(page, "Add heading");
  await add.focus();
  for (let i = 0; i < 6; i += 1) await page.keyboard.press("Enter");
  await expect(structure(page).getByRole("listitem")).toHaveCount(6);
  const ids = await structure(page).getByRole("textbox").evaluateAll((els) => els.map((el) => el.closest("li").textContent.trim().split(" ")[1]));
  expect(new Set(ids).size).toBe(6);
  // Rapid Move earlier at the top is clamped: the order never changes.
  const first = button(page, "Move earlier: heading page-1-heading");
  await first.focus();
  for (let i = 0; i < 4; i += 1) await page.keyboard.press("Enter");
  await expect(structure(page).getByRole("listitem").first()).toContainText("heading page-1-heading");
  const undo = button(page, "Undo");
  for (let n = 5; n >= 0; n -= 1) {
    await press(undo);
    await expect(structure(page).getByRole("listitem")).toHaveCount(n);
  }
});

test("@uli ULI-A06/A08 drag edits on the canvas have keyboard and field paths with the same outcome", async ({ page }) => {
  await openStudio(page, 390);
  const nav = page.getByRole("navigation", { name: "Structure" });
  await press(nav.getByRole("button", { name: "Activity: Prepare request" }));
  // State is read from the projected geometry; the controls are found by role and name.
  const canvasNode = page.locator(".ef-diagram__canvas [data-node-id='prepare']");
  const geometry = (name) => async () => parseInt(await canvasNode.evaluate((el, p) => getComputedStyle(el).getPropertyValue(p).trim(), name));
  const x = geometry("--ef-diagram-x");
  await expectActionOutcome({ control: button(page, "Move right"), read: x, expected: (await x()) + 8 });
  const y = geometry("--ef-diagram-y");
  await expectActionOutcome({ control: button(page, "Move down"), read: y, expected: (await y()) + 8 });
  const width = page.getByRole("spinbutton", { name: "Width" });
  const w = geometry("--ef-diagram-w");
  expect(await w()).not.toBe(260);
  await width.fill("260");
  await width.press("Tab");
  await expect.poll(w).toBe(260);
  // The canvas item itself is focusable and the arrow keys move it through the same command.
  const before = await x();
  await canvasNode.focus();
  await page.keyboard.press("ArrowLeft");
  await expect.poll(x).toBe(before - 8);
});

for (const width of VIEWPORTS) {
  test(`@uli ULI-L05/L06/L09 every surface at ${width}px with long, RTL and unbroken content has no page overflow and no clipped control`, async ({ page }) => {
    await openStudio(page, width);
    expect(await pageOverflow(page), "Flow").toBe(0);
    expect(await clippedControls(page.locator("main"))).toEqual([]);
    await press(button(page, "Add Layout page"));
    await press(button(page, "Add text"));
    await press(button(page, "Add text-field"));
    await setText(page, "text text for page-1-text", "https://example.com/an-extremely-long-unbroken-address-without-any-spaces-at-all");
    await setText(page, "text-field label for page-1-text-field", "שם פרטי ומשפחה כפי שמופיע בדרכון — الاسم الكامل كما يظهر في جواز السفر");
    await expect(checks(page)).toContainText("ULI-L05:");
    expect(await pageOverflow(page), "Layout").toBe(0);
    expect(await clippedControls(page.locator("main"))).toEqual([]);
    const navigation = page.getByRole("navigation", { name: "Surfaces" });
    for (const name of ["Flow: Purchase request", "Layout: Page 1", "Add Layout page"]) await expect(button(navigation, name)).toBeVisible();
    await press(navigation.getByRole("button", { name: /^Workflows/ }));
    await press(button(page, "New workflow"));
    await expect(page.getByRole("navigation", { name: "Open workflows" }).getByRole("button")).toHaveCount(1);
    expect(await pageOverflow(page), "Workflows").toBe(0);
  });
}

test("@uli ULI-A10 every enabled, visible control on every surface is in the declared action inventory", async ({ page }) => {
  await openStudio(page);
  const unclassified = [];
  for (const surface of SURFACES) {
    await surface.enter(page);
    const names = await page.locator("main button:not([disabled])").evaluateAll((els) =>
      els.filter((el) => el.checkVisibility()).map((el) => (el.getAttribute("aria-label") || el.textContent).replace(/\s+/g, " ").trim()));
    for (const name of names) if (!classify(surface.id, name)) unclassified.push(`${surface.id}: ${name}`);
  }
  expect(unclassified, "add each new action to tests/browser/ui-action-inventory.mjs with its outcome and test").toEqual([]);
  expect(ACTION_INVENTORY.every((entry) => entry.outcome && entry.evidence)).toBe(true);
});

test("@uli the Layout surface with nested components and checks has no automatically detectable WCAG A/AA violations", async ({ page }) => {
  await openStudio(page, 390);
  await formRow(page);
  await setText(page, "text-field description for page-1-text-field", "Help text");
  await expect(checks(page).getByRole("listitem")).toHaveCount(1);
  const result = await new AxeBuilder({ page }).include("main").analyze();
  expect(result.violations.flatMap((v) => v.nodes.map((n) => `${v.id}: ${n.target.join(" ")}`))).toEqual([]);
});
