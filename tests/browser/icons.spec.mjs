import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";
import { readFileSync } from "node:fs";
import { join } from "node:path";

// Forma icons in the editor (forma-studio#27). The F# engine fetches the pinned
// Forma release's compiled icons through Limen, verifies them, and owns every
// change; the page only binds native controls.
//
// Three situations are tested:
//  - the real pin as built (Forma 0.5.0): nothing is routed; its 40 compiled icons
//    are verified, offered, set and exported;
//  - an icon-less release (no registry, as Forma 0.4.1 and earlier): the registry is
//    answered 404, and the editor must say icons are unavailable and fabricate nothing;
//  - edge cases on a small, committed TEST FIXTURE
//    (tests/fixtures/forma-icons-test-fixture: synthetic shapes, not Forma artwork)
//    served at the same URLs the app uses for the pinned package's dist/icons.
const fixtureDir = join(process.cwd(), "tests/fixtures/forma-icons-test-fixture");
const fixture = (relative) => readFileSync(join(fixtureDir, relative), "utf8");

const serveFixture = async (page, replace = {}) => {
  await page.route("**/forma/icons/**", (route) => {
    const relative = new URL(route.request().url()).pathname.split("/forma/icons/")[1];
    const body = replace[relative] ?? fixture(relative);
    const type = relative.endsWith(".json") ? "application/json" : relative.endsWith(".svg") ? "image/svg+xml" : "text/html";
    return route.fulfill({ status: 200, contentType: type, body });
  });
};

const open = async (page, width = 1400) => {
  await page.setViewportSize({ width, height: 900 });
  await page.goto("/");
  await page.waitForFunction(() => document.documentElement.dataset.studioReady === "true");
};

const audit = async (page) => {
  const result = await new AxeBuilder({ page }).analyze();
  return result.violations.flatMap((v) => v.nodes.map((n) => `${v.id}: ${n.target.join(" ")}`));
};

const noPageOverflow = (page) => page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth);

/** Presses Tab until `target` has focus, proving it is reachable by keyboard alone. */
const tabTo = async (page, target, limit = 80) => {
  for (let i = 0; i < limit; i += 1) {
    if (await target.evaluate((el) => el === document.activeElement)) return;
    await page.keyboard.press("Tab");
  }
  throw new Error("not reachable with Tab");
};

const structureItem = (page, text) => page.getByRole("navigation", { name: "Structure" }).locator("button[data-event=select]").filter({ hasText: text });

test.beforeEach(async ({ page }) => {
  page.on("console", (message) => {
    if (message.type() === "error" && message.text().startsWith("Limen")) throw new Error(message.text());
  });
});

const pinned = JSON.parse(readFileSync(join(process.cwd(), "node_modules/@echelon-foundry/design-system/package.json"), "utf8")).version;

test("the pinned Forma release's own icons are verified, offered, set and exported", async ({ page }) => {
  const requests = [];
  page.on("request", (request) => requests.push(request.url()));
  await open(page);
  await structureItem(page, "Activity: Prepare request").click();
  await page.getByRole("button", { name: "Choose icon", exact: true }).click();
  const picker = page.getByRole("region", { name: "Icon for Prepare request" });
  await expect(picker).toContainText(`40 of 40 icons from Forma ${pinned} match.`);
  await expect(picker.locator(".studio-icon-choice")).toHaveCount(40);
  await picker.getByRole("searchbox", { name: "Search icons" }).fill("find");
  await picker.getByRole("searchbox", { name: "Search icons" }).press("Enter");
  await expect(picker.locator(".studio-icon-choice")).toHaveCount(1);
  await picker.getByRole("button", { name: "Search" }).click();
  await expect(page.getByRole("status")).toHaveText("Icon set to Search.");
  const node = page.locator(".ef-diagram__canvas .ef-diagram-node", { has: page.locator(".ef-diagram-node__label", { hasText: "Prepare request" }) });
  await expect(node.locator("img.studio-node-icon")).toHaveAttribute("src", "./forma/icons/search.svg");
  // The pinned static SVG is served and drawn (an SVG without width/height reports no natural size, so check the load).
  expect((await page.request.get("/forma/icons/search.svg")).headers()["content-type"]).toContain("image/svg+xml");
  await expect.poll(() => node.locator("img.studio-node-icon").evaluate((img) => img.complete && img.getBoundingClientRect().width > 0)).toBe(true);
  // A Layout button gets the release's own inline SVG in the export.
  await page.getByRole("button", { name: "Add Layout page" }).click();
  await page.getByRole("button", { name: "Add button" }).click();
  await page.getByRole("button", { name: "Choose icon for button page-1-button" }).click();
  const layoutPicker = page.getByRole("region", { name: "Icon for button page-1-button" });
  await layoutPicker.getByRole("button", { name: "Add", exact: true }).click();
  await expect(layoutPicker.getByRole("button", { name: "Add", exact: true, pressed: true })).toBeVisible();
  await page.getByRole("button", { name: "Export Layout page" }).click();
  const html = await page.locator("#export-text").inputValue();
  expect(html).toContain(`data-forma-version="${pinned}"`);
  expect(html).toContain('data-ef-icon="add"');
  expect(html).toContain(`@echelon-foundry/design-system@${pinned}/components.css`);
  expect(html).not.toMatch(/<script|\son[a-z]+=|javascript:/i);
  expect(requests.filter((url) => !url.startsWith("http://127.0.0.1:4380/"))).toEqual([]);
  expect(await audit(page)).toEqual([]);
});

test("with an icon-less release the picker says icons are unavailable and loads nothing else", async ({ page }) => {
  const requests = [];
  page.on("request", (request) => requests.push(request.url()));
  await page.route("**/forma/icons/registry.json", (route) => route.fulfill({ status: 404, body: "Not found" }));
  await open(page);
  await structureItem(page, "Activity: Prepare request").focus();
  await page.keyboard.press("Enter");
  await expect(page.getByText("Icon: No icon.", { exact: true })).toBeVisible();
  const choose = page.getByRole("button", { name: "Choose icon", exact: true });
  await tabTo(page, choose);
  await page.keyboard.press("Enter");
  const picker = page.getByRole("region", { name: "Icon for Prepare request" });
  await expect(picker).toContainText("does not include the icon collection");
  await expect(picker.getByRole("searchbox", { name: "Search icons" })).toBeDisabled();
  await expect(picker.locator(".studio-icon-choice")).toHaveCount(0);
  await expect(picker.getByRole("button", { name: "Remove icon" })).toBeDisabled();
  await expect(picker.getByRole("button", { name: "Copy icon HTML" })).toBeDisabled();
  await expect(page.locator("img[src*='forma/icons/']")).toHaveCount(0);
  expect(requests.filter((url) => !url.startsWith("http://127.0.0.1:4380/"))).toEqual([]);
  expect(requests.filter((url) => url.includes("/forma/icons/") && !url.endsWith("/forma/icons/registry.json"))).toEqual([]);
  expect(await audit(page)).toEqual([]);
});

test("at 320px a Layout button gets an icon by keyboard alone, and the export inlines the pinned SVG", async ({ page }) => {
  await serveFixture(page);
  await open(page, 320);
  await page.getByRole("button", { name: "Add Layout page" }).click();
  await page.getByRole("button", { name: "Add button" }).click();
  const choose = page.getByRole("button", { name: "Choose icon for button page-1-button" });
  await choose.focus();
  await page.keyboard.press("Enter");
  const picker = page.getByRole("region", { name: "Icon for button page-1-button" });
  await expect(picker).toContainText("3 of 3 icons from Forma 0.0.0-test-fixture match.");
  await expect(picker.getByRole("group", { name: "Icons in Forma 0.0.0-test-fixture" })).toBeVisible();
  const search = picker.getByRole("searchbox", { name: "Search icons" });
  await tabTo(page, search);
  await page.keyboard.type("line");
  await page.keyboard.press("Enter");
  await expect(picker).toContainText("1 of 3 icons from Forma 0.0.0-test-fixture match.");
  const choice = picker.getByRole("button", { name: "Test line" });
  await expect(choice).toHaveAttribute("aria-pressed", "false");
  await tabTo(page, choice);
  await page.keyboard.press("Space");
  await expect(choice).toHaveAttribute("aria-pressed", "true");
  await expect(picker).toContainText("Current icon: Test line (test-line).");
  await expect(page.getByRole("status")).toHaveText("Icon set to Test line.");
  await expect(page.getByText("Icon: Test line (test-line).", { exact: true })).toBeVisible();
  // The Layout list draws the pinned static SVG, decoratively; the item keeps its text.
  await expect(page.getByRole("list", { name: "Page structure" }).locator("img[src='./forma/icons/test-line.svg'][alt='']")).toHaveCount(1);
  expect(await noPageOverflow(page)).toBe(true);
  expect(await audit(page)).toEqual([]);

  await page.getByRole("button", { name: "Export Layout page" }).click();
  const html = await page.locator("#export-text").inputValue();
  expect(html).toContain('data-forma-version="0.0.0-test-fixture"');
  expect(html).toContain('data-ef-icon="test-line"');
  expect(html).toContain('<path d="M4 12H20" />');
  expect(html).toContain("@echelon-foundry/design-system@0.0.0-test-fixture/components.css");
  expect(html).not.toMatch(/<script|\son[a-z]+=|javascript:/i);

  await picker.getByRole("button", { name: "Remove icon" }).click();
  await expect(page.getByText("Icon: No icon.", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Undo" }).click();
  await expect(page.getByText("Icon: Test line (test-line).", { exact: true })).toBeVisible();
  await picker.getByRole("button", { name: "Done" }).click();
  await expect(picker).toHaveCount(0);
  expect(await noPageOverflow(page)).toBe(true);
});

test("a diagram node's icon is chosen in the inspector, shown on the canvas, saved and reopened", async ({ page }) => {
  await serveFixture(page);
  await open(page);
  await structureItem(page, "Activity: Prepare request").focus();
  await page.keyboard.press("Enter");
  await page.getByRole("button", { name: "Choose icon", exact: true }).click();
  const picker = page.getByRole("region", { name: "Icon for Prepare request" });
  await picker.getByRole("button", { name: "Test dot" }).click();
  await expect(picker.getByRole("button", { name: "Test dot", pressed: true })).toBeVisible();
  await expect(page.getByText("Icon: Test dot (test-dot).", { exact: true })).toBeVisible();
  const node = page.locator(".ef-diagram__canvas .ef-diagram-node", { has: page.locator(".ef-diagram-node__label", { hasText: "Prepare request" }) });
  await expect(node.locator("img.studio-node-icon")).toHaveAttribute("src", "./forma/icons/test-dot.svg");
  await expect(node).toHaveAttribute("aria-label", "Activity: Prepare request");
  expect(await audit(page)).toEqual([]);
  await page.getByRole("button", { name: "Save", exact: true }).click();
  await expect(page.getByRole("status")).toHaveText("Saved.");
  const saved = await page.evaluate(() => localStorage.getItem("forma-studio.project"));
  expect(saved).toContain('"icon": "test-dot"');
  expect(saved).toContain('"schemaVersion": 3');
  await page.reload();
  await page.waitForFunction(() => document.documentElement.dataset.studioReady === "true");
  await page.getByRole("button", { name: "Open saved" }).click();
  await expect(page.getByRole("status")).toContainText("Loaded the saved project");
  await expect(node.locator("img.studio-node-icon")).toHaveAttribute("src", "./forma/icons/test-dot.svg");
});

test("an icon whose file does not match the registry digest is refused, and the rest are offered", async ({ page }) => {
  await serveFixture(page, { "test-dot.svg": fixture("test-dot.svg").replace('r="3"', 'r="4"') });
  await open(page);
  await structureItem(page, "Activity: Prepare request").click();
  await page.getByRole("button", { name: "Choose icon", exact: true }).click();
  const picker = page.getByRole("region", { name: "Icon for Prepare request" });
  await expect(picker).toContainText("2 of 2 icons from Forma 0.0.0-test-fixture match. 1 icon(s) failed verification and are not offered.");
  await expect(picker.getByRole("button", { name: "Test dot" })).toHaveCount(0);
  await expect(picker.getByRole("button", { name: "Test box" })).toBeVisible();
});

test("an unknown icon name in a saved document is kept, never drawn, and survives another save", async ({ page }) => {
  await serveFixture(page);
  await open(page);
  // Write a document that names an icon this release does not have and one malformed value.
  await page.getByRole("button", { name: "Save", exact: true }).click();
  await expect(page.getByRole("status")).toHaveText("Saved.");
  await page.evaluate(() => {
    const text = localStorage.getItem("forma-studio.project");
    const doc = JSON.parse(text);
    const nodes = doc.diagrams[0].nodes;
    nodes.find((n) => n.nodeId === "prepare").icon = "future-glyph";
    nodes.find((n) => n.nodeId === "approve").icon = "<img src=x onerror=alert(1)>";
    doc.schemaVersion = 3;
    localStorage.setItem("forma-studio.project", JSON.stringify(doc, null, 2));
  });
  await page.getByRole("button", { name: "Open saved" }).click();
  await expect(page.getByRole("status")).toContainText("Loaded the saved project");
  await expect(page.locator(".ef-diagram__canvas img")).toHaveCount(0);
  await structureItem(page, "Activity: Prepare request").click();
  await expect(page.getByText('Icon: "future-glyph" is not in Forma 0.0.0-test-fixture; the name is kept and not shown.', { exact: true })).toBeVisible();
  await structureItem(page, "Activity: Approve spend").click();
  await expect(page.getByText("Icon: The stored icon value is not a valid icon name; it is kept as data and never shown.", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Save", exact: true }).click();
  await expect(page.getByRole("status")).toHaveText("Saved.");
  const saved = JSON.parse(await page.evaluate(() => localStorage.getItem("forma-studio.project")));
  const icons = Object.fromEntries(saved.diagrams[0].nodes.map((n) => [n.nodeId, n.icon]));
  expect(icons.prepare).toBe("future-glyph");
  expect(icons.approve).toBe("<img src=x onerror=alert(1)>");
});

test.describe("dark color scheme", () => {
  test.use({ colorScheme: "dark" });

  test("the open picker has no axe violations in dark mode", async ({ page }) => {
    await serveFixture(page);
    await open(page);
    await structureItem(page, "Activity: Prepare request").click();
    await page.getByRole("button", { name: "Choose icon", exact: true }).click();
    const picker = page.getByRole("region", { name: "Icon for Prepare request" });
    await picker.getByRole("button", { name: "Test box" }).click();
    await expect(picker.getByRole("button", { name: "Test box", pressed: true })).toBeVisible();
    expect(await audit(page)).toEqual([]);
  });
});
