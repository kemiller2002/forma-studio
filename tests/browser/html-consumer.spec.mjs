import { readFileSync } from "node:fs";
import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";

// Forma Studio -> design -> HTML export -> ordinary web project -> render.
// The site (examples/html-consumer) knows Forma and nothing about Studio.
const site = "http://127.0.0.1:4381/";
const pages = ["application.html", "responsive.html", "workflow.html", "branded.html", "launch-readiness.workflow.html", "application.embedded.html", "workflow.embedded.html"];
const wcag = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"];

for (const file of pages) {
  test(`${file}: renders with Forma, passes automated accessibility checks, has no Studio dependency`, async ({ page }) => {
    await page.goto(site + file);
    const source = readFileSync(`examples/html-consumer/site/${file}`, "utf8");
    expect(source.toLowerCase()).not.toContain("studio");
    expect(source).not.toContain("<script");
    // Forma CSS is applied (the consumer installed the public assets).
    expect(await page.evaluate(() => getComputedStyle(document.documentElement).getPropertyValue("--ef-color-text-primary").trim())).not.toBe("");
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
  const columns = () => page.locator(".ef-responsive-grid").evaluate((el) => getComputedStyle(el).gridTemplateColumns.split(" ").length);
  expect(await columns()).toBeGreaterThan(1);
  await page.setViewportSize({ width: 320, height: 800 });
  expect(await columns()).toBe(1);
});

test("the branded composition applies the public brand without changing the markup", async ({ page }) => {
  const plain = readFileSync("examples/html-consumer/site/application.html", "utf8");
  const branded = readFileSync("examples/html-consumer/site/branded.html", "utf8");
  expect(branded.replace(' data-ef-brand="example-harbor"', "").replace(/\s*<link rel="stylesheet" href="assets\/forma\/brands\/example-harbor.css">/, "")).toBe(plain);
  await page.goto(site + "application.html");
  const before = await page.evaluate(() => getComputedStyle(document.documentElement).getPropertyValue("--ef-color-accent-primary").trim());
  await page.goto(site + "branded.html");
  const after = await page.evaluate(() => getComputedStyle(document.documentElement).getPropertyValue("--ef-color-accent-primary").trim());
  expect(after).not.toBe(before);
});

test("the workflow page keeps relationships in text and is usable by keyboard", async ({ page }) => {
  await page.goto(site + "workflow.html");
  await expect(page.locator(".ef-diagram__relations li")).toHaveCount(5);
  await page.keyboard.press("Tab");
  await expect(page.locator(".ef-diagram__viewport")).toBeFocused();
});

test("form semantics survive export", async ({ page }) => {
  await page.goto(site + "application.html");
  await expect(page.getByLabel("Work email")).toHaveAttribute("type", "email");
  await expect(page.getByLabel("Work email")).toHaveAccessibleDescription("We send the confirmation here.");
  await expect(page.getByRole("button", { name: "Request session" })).toHaveAttribute("type", "submit");
});
