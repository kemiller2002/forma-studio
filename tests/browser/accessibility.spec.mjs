import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";

// axe gate for the editor (app-dist, port 4380) and the marketing site
// (site-dist, port 4382), in light and dark color schemes. It runs every axe
// rule axe enables by default (WCAG A/AA plus best practices) and disables none.
//
// axe reports text whose contrast it can measure but considers suspicious, such
// as text drawn in the same color as its background, as "incomplete" rather
// than as a violation. A measured ratio below the requirement still fails here.
const site = "http://127.0.0.1:4382/index.html";

const audit = async (page) => {
  const result = await new AxeBuilder({ page }).analyze();
  const violations = result.violations.flatMap((v) => v.nodes.map((n) => `${v.id}: ${n.target.join(" ")}: ${n.failureSummary ?? ""}`));
  const unreadable = result.incomplete
    .filter((v) => v.id === "color-contrast")
    .flatMap((v) => v.nodes)
    .filter((n) => {
      const data = n.any.find((check) => check.id === "color-contrast")?.data ?? {};
      // expectedContrastRatio is a string such as "4.5:1".
      const required = Number.parseFloat(data.expectedContrastRatio);
      return typeof data.contrastRatio === "number" && data.contrastRatio > 0 && data.contrastRatio < required;
    })
    .map((n) => `color-contrast (measured, reported incomplete): ${n.target.join(" ")}: ${n.any[0].message}`);
  return [...violations, ...unreadable];
};

for (const colorScheme of ["light", "dark"]) {
  test.describe(`${colorScheme} color scheme`, () => {
    test.use({ colorScheme });

    test(`the editor has no axe violations, with and without a selection (${colorScheme})`, async ({ page }) => {
      await page.setViewportSize({ width: 1400, height: 1000 });
      await page.goto("/");
      await page.waitForFunction(() => document.documentElement.dataset.studioReady === "true");
      await expect(page.locator(".ef-diagram__canvas .ef-diagram-node")).toHaveCount(7);
      expect(await audit(page)).toEqual([]);
      await page.getByRole("navigation", { name: "Structure" }).locator("button[data-event=select]").filter({ hasText: "Activity: Approve spend" }).click();
      await expect(page.locator(".ef-diagram__canvas .studio-selected")).toHaveCount(1);
      expect(await audit(page)).toEqual([]);
    });

    test(`the marketing site has no axe violations (${colorScheme})`, async ({ page }) => {
      await page.setViewportSize({ width: 1400, height: 1000 });
      await page.goto(site);
      await expect(page.locator("h1")).toBeVisible();
      expect(await audit(page)).toEqual([]);
    });
  });
}
