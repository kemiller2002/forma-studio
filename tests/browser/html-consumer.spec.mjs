import { readFileSync } from "node:fs";
import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";
import { VIEWPORTS, alignmentVerdict, measureDeclaredGroups } from "./ui-conformance-helpers.mjs";

// Forma Studio -> design -> HTML export -> ordinary web project -> render.
// The site (examples/html-consumer) knows Forma and nothing about Studio.
const site = "http://127.0.0.1:4381/";
const pages = ["application.html", "responsive.html", "workflow.html", "branded.html", "echelon.html", "launch-readiness.workflow.html", "application.embedded.html", "workflow.embedded.html"];
const wcag = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"];

for (const file of pages) {
  test(`${file}: renders with Forma, passes automated accessibility checks, has no Studio dependency`, async ({ page }) => {
    await page.goto(site + file);
    const source = readFileSync(`examples/html-consumer/site/${file}`, "utf8");
    expect(source.toLowerCase()).not.toContain("studio");
    expect(source).not.toContain("<script");
    // Forma CSS is applied (the consumer installed the public assets).
    expect(await page.evaluate(() => getComputedStyle(document.documentElement).getPropertyValue("--forma-color-text-primary").trim())).not.toBe("");
    const result = await new AxeBuilder({ page }).withTags(wcag).analyze();
    expect(result.violations.map((v) => `${v.id}: ${v.nodes.map((n) => n.target).join(" ")}`)).toEqual([]);
  });

  test(`${file}: no page-level horizontal overflow at 320px`, async ({ page }) => {
    await page.setViewportSize({ width: 320, height: 800 });
    await page.goto(site + file);
    expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(0);
  });
}

test("the responsive composition reflows to one column on a phone and several on a desktop", async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 800 });
  await page.goto(site + "responsive.html");
  const columns = () => page.locator(".forma-responsive-grid").evaluate((el) => getComputedStyle(el).gridTemplateColumns.split(" ").length);
  expect(await columns()).toBeGreaterThan(1);
  await page.setViewportSize({ width: 320, height: 800 });
  expect(await columns()).toBe(1);
});

test("the branded composition applies the public brand without changing the markup", async ({ page }) => {
  const plain = readFileSync("examples/html-consumer/site/application.html", "utf8");
  const branded = readFileSync("examples/html-consumer/site/branded.html", "utf8");
  expect(branded.replace(' data-forma-brand="example-harbor"', "").replace(/\s*<link rel="stylesheet" href="assets\/forma\/brands\/example-harbor.css">/, "")).toBe(plain);
  await page.goto(site + "application.html");
  const before = await page.evaluate(() => getComputedStyle(document.documentElement).getPropertyValue("--forma-color-accent-primary").trim());
  await page.goto(site + "branded.html");
  const after = await page.evaluate(() => getComputedStyle(document.documentElement).getPropertyValue("--forma-color-accent-primary").trim());
  expect(after).not.toBe(before);
});

test("the Echelon composition wears the released Echelon theme after Forma, self-contained, without changing the markup", async ({ page }) => {
  const plain = readFileSync("examples/html-consumer/site/application.html", "utf8");
  const echelon = readFileSync("examples/html-consumer/site/echelon.html", "utf8");
  // Only the head changes: the brand attribute and the theme's three stylesheets, in its order.
  const theme = ["echelon.css", "echelon-fonts.css", "echelon-app.css"].map((f) => `\n    <link rel="stylesheet" href="assets/echelon-theme/${f}">`).join("");
  expect(echelon.replace(' data-forma-brand="echelon"', "").replace(theme, "")).toBe(plain);
  expect(echelon).not.toContain("brands/echelon.css");
  const failed = [];
  page.on("requestfailed", (r) => failed.push(r.url()));
  page.on("response", (r) => { if (r.status() >= 400) failed.push(`${r.status()} ${r.url()}`); });
  await page.goto(site + "application.html");
  const before = await page.evaluate(() => getComputedStyle(document.documentElement).getPropertyValue("--forma-color-accent-primary").trim());
  await page.goto(site + "echelon.html");
  await expect(page.locator("html")).toHaveAttribute("data-forma-brand", "echelon");
  const sheets = await page.evaluate(() => [...document.styleSheets].map((s) => new URL(s.href).pathname));
  expect(sheets).toEqual(["/assets/forma/tokens.css", "/assets/forma/foundations.css", "/assets/forma/components.css", "/assets/echelon-theme/echelon.css", "/assets/echelon-theme/echelon-fonts.css", "/assets/echelon-theme/echelon-app.css"]);
  // The brand restyles Forma's tokens, and the theme's own layer applies.
  expect(await page.evaluate(() => getComputedStyle(document.documentElement).getPropertyValue("--forma-color-accent-primary").trim())).not.toBe(before);
  expect(await page.evaluate(() => getComputedStyle(document.documentElement).getPropertyValue("--echelon-surface-canvas").trim())).not.toBe("");
  // The self-hosted fonts load from the project's own copy of the package; nothing is fetched elsewhere.
  await page.evaluate(() => document.fonts.ready);
  const loaded = await page.evaluate(async () => {
    const faces = [...document.fonts].filter((f) => f.status === "loaded").map((f) => f.family.replaceAll('"', ""));
    return [...new Set(faces)].sort();
  });
  expect(loaded.length, `loaded faces: ${loaded}`).toBeGreaterThan(0);
  expect(failed).toEqual([]);
});

test("the workflow page keeps relationships in text and is usable by keyboard", async ({ page }) => {
  await page.goto(site + "workflow.html");
  await expect(page.locator(".forma-diagram__relations li")).toHaveCount(5);
  await page.keyboard.press("Tab");
  await expect(page.locator(".forma-diagram__viewport")).toBeFocused();
});

test("form semantics survive export", async ({ page }) => {
  await page.goto(site + "application.html");
  await expect(page.getByLabel("Work email")).toHaveAttribute("type", "email");
  await expect(page.getByLabel("Work email")).toHaveAccessibleDescription("We send the confirmation here.");
  await expect(page.getByRole("button", { name: "Request session" })).toHaveAttribute("type", "submit");
});

// ULI-L01 (visual-engineering UI-LAYOUT-INTERACTION@1.0.0): the exported markup carries
// Studio's declared alignment groups, so the consumer page is measured against the
// declaration, never against guessed peers. Reflowed (stacked) widths are NOT_APPLICABLE.
for (const file of ["application.html", "branded.html", "echelon.html", "application.embedded.html"]) {
  test(`@uli ${file}: every declared alignment group shares its edge within 1 CSS px (or a recorded, bounded exception) at 320/390/768/1280`, async ({ page }) => {
    const verdicts = [];
    for (const width of VIEWPORTS) {
      await page.setViewportSize({ width, height: 900 });
      await page.goto(site + file);
      const groups = await measureDeclaredGroups(page);
      expect(groups.length, "the export declares its form row").toBeGreaterThan(0);
      for (const group of groups) verdicts.push({ width, ...alignmentVerdict(group) });
    }
    expect(verdicts.filter((v) => v.status === "FAIL"), JSON.stringify(verdicts)).toEqual([]);
    expect(verdicts.some((v) => v.status === "PASS" || v.status === "EXCEPTION"), "the row is side by side at some supported width").toBe(true);
    test.info().annotations.push({ type: "ULI-L01 evidence", description: JSON.stringify(verdicts.map(({ peers, ...rest }) => rest)) });
  });
}
