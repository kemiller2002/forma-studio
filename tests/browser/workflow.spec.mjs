import { execFileSync } from "node:child_process";
import { mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";

// Portable workflows in Studio through the public Forma component
// (REQUIREMENTS.md "Portable workflows and embeddable designer").
const producer = "vendor/forma/workflow/external-producer/produce.mjs";
const consumer = "vendor/forma/workflow/external-producer/consume.mjs";

const openStudio = async (page) => {
  await page.goto("/");
  await expect(page.locator("html")).toHaveAttribute("data-studio-workflow-ready", "true", { timeout: 60_000 });
  await page.getByRole("button", { name: /^Workflows/ }).click();
};

const component = (page) => page.locator("#studio-workflow");

test("external producer -> Forma validation -> Studio open -> Studio edit -> save -> external consumer", async ({ page }) => {
  // 1. An external producer writes a workflow from the published schema alone.
  const dir = mkdtempSync(join(tmpdir(), "studio-roundtrip-"));
  const produced = execFileSync("node", [producer, dir], { encoding: "utf8" }).trim();
  // 2. Forma validation (the public validator, independent of the Studio UI).
  const validation = JSON.parse(execFileSync("dotnet", ["run", "--project", "tools/FormaStudio.Cli", "-c", "Release", "--", "workflow-validate", produced], { encoding: "utf8" }));
  expect(validation[0].report.class).toBe("supported-with-preserved-extensions");
  // 3. Studio opens the file as a first-class document.
  await openStudio(page);
  await page.locator("#workflow-file").setInputFiles(produced);
  await expect(page.locator(".studio-status")).toContainText("Opened Mission handoff");
  await expect(component(page).locator(".ef-workflow-editor")).toBeVisible();
  // 4. Studio edits it through the public component.
  await component(page).locator(".ef-workflow-editor__list button", { hasText: "Task: Handoff checklist" }).click();
  const name = component(page).getByLabel("Name", { exact: true });
  await name.fill("Handoff checklist (rev 2)");
  await name.press("Tab");
  await expect(page.locator(".studio-status")).toContainText("change kept");
  await component(page).getByRole("button", { name: "Add step" }).click();
  await expect(page.locator(".studio-status")).toContainText("change kept");
  await component(page).getByRole("button", { name: "Arrange" }).click();
  await expect(page.locator(".studio-status")).toContainText("change kept");
  // 5. Save the file.
  const [download] = await Promise.all([page.waitForEvent("download"), page.getByRole("button", { name: "Download file" }).click()]);
  expect(download.suggestedFilename()).toBe("mission-handoff.forma-workflow.json");
  const saved = join(dir, "saved.forma-workflow.json");
  await download.saveAs(saved);
  // 6. An external consumer reads it back: everything the producer wrote survived.
  const report = execFileSync("node", [consumer, produced, saved], { encoding: "utf8" });
  expect(report).toMatch(/ 0 lost$/m);
  const doc = JSON.parse(readFileSync(saved, "utf8"));
  expect(doc.nodes.find((n) => n.id === "checklist").label).toBe("Handoff checklist (rev 2)");
  expect(doc.nodes.some((n) => n.id === "task-1")).toBe(true);
  if (process.env.PROOF_EVIDENCE) {
    mkdirSync(join(process.env.PROOF_EVIDENCE, ".."), { recursive: true });
    writeFileSync(process.env.PROOF_EVIDENCE, [
      "# Evidence: external workflow round trip through Forma Studio",
      "",
      `Recorded by \`npm run proof:external\` (tests/browser/workflow.spec.mjs) against vendored forma@${JSON.parse(readFileSync("vendor/forma/MANIFEST.json", "utf8")).commit}.`,
      "",
      "1. The external producer (`produce.mjs`, schema only, no Forma code) wrote `mission-handoff.forma-workflow.json`.",
      `2. Forma validation: \`${validation[0].report.class}\`, extensions ${validation[0].report.extensions.join(", ")}.`,
      "3. Studio opened the file with the file picker. It became a first-class workflow document in the public Forma component.",
      "4. Studio edits: renamed `checklist`, added step `task-1`, and ran Arrange (deterministic layout).",
      "5. Studio saved the file with Download file.",
      "6. The external consumer (`consume.mjs`) compared the producer's file with Studio's file:",
      "",
      "```text",
      report.trim(),
      "```",
      ""
    ].join("\n"));
  }
});

test("a workflow persists in the browser and reopens unchanged", async ({ page }) => {
  await openStudio(page);
  await page.getByRole("button", { name: "New workflow" }).click();
  await component(page).getByRole("button", { name: "Add step" }).click();
  await expect(page.locator(".studio-status")).toContainText("change kept");
  const before = await component(page).evaluate((el) => el.getWorkflow());
  await page.getByRole("button", { name: "Save workflows in browser" }).click();
  await expect(page.locator(".studio-status")).toContainText("Saved 1 workflow");
  await page.reload();
  await expect(page.locator("html")).toHaveAttribute("data-studio-workflow-ready", "true", { timeout: 60_000 });
  await page.getByRole("button", { name: /^Workflows/ }).click();
  await page.getByRole("button", { name: "Open saved workflows" }).click();
  await expect(page.locator(".studio-status")).toContainText("Opened 1 saved workflow");
  await expect(component(page).locator("article[data-fw-key='node:task-1']")).toBeVisible();
  expect(await component(page).evaluate((el) => el.getWorkflow())).toEqual(before);
});

test("Studio exports the current workflow as Forma HTML with no script", async ({ page }) => {
  await openStudio(page);
  await page.getByRole("button", { name: "New workflow" }).click();
  await page.getByRole("button", { name: "Export current workflow" }).click();
  const html = await page.locator("#export-text").inputValue();
  expect(html).toContain('<figure class="ef-diagram"');
  expect(html).not.toContain("<script");
  await page.getByRole("button", { name: "Complete HTML document" }).click();
  await page.getByRole("button", { name: "Export current workflow" }).click();
  expect(await page.locator("#export-text").inputValue()).toMatch(/^<!doctype html>/);
  // Interactive output is opt-in and uses only the public runtime, declared.
  await page.getByRole("button", { name: /Interactive workflow/ }).click();
  await page.getByRole("button", { name: "Export current workflow" }).click();
  const interactive = await page.locator("#export-text").inputValue();
  expect(interactive).toContain('<forma-workflow mode="view"');
  expect(interactive).toContain('<script type="module" src="node_modules/@echelon-foundry/forma-workflow/dist/forma-workflow.js"></script>');
  // The document may name its producer in provenance; no Studio class, asset or path may appear.
  expect(interactive).not.toMatch(/studio-|studio\.css|forma-studio\//i);
  await expect(page.locator("p", { hasText: "@echelon-foundry/forma-workflow@1.0.0/forma-workflow.js" })).toBeVisible();
});

test("the workflow surface is keyboard operable and passes automated accessibility checks", async ({ page }) => {
  await openStudio(page);
  await page.getByRole("button", { name: "New workflow" }).click();
  const item = component(page).locator(".ef-workflow-editor__list button", { hasText: "Start: Start" });
  await item.focus();
  await page.keyboard.press("Enter");
  await item.press("ArrowRight");
  await expect(page.locator(".studio-status")).toContainText("change kept");
  const result = await new AxeBuilder({ page }).include("#studio-workflow").withTags(["wcag2a", "wcag2aa", "wcag21aa", "wcag22aa"]).analyze();
  expect(result.violations.map((v) => v.id)).toEqual([]);
});

test("on a phone-sized touch screen the workflow surface needs no drag and does not overflow", async ({ browser }) => {
  const context = await browser.newContext({ viewport: { width: 390, height: 844 }, hasTouch: true });
  const page = await context.newPage();
  await openStudio(page);
  await page.getByRole("button", { name: "New workflow" }).tap();
  await component(page).getByRole("button", { name: "Add step" }).tap();
  await expect(page.locator(".studio-status")).toContainText("change kept");
  await component(page).locator(".ef-workflow-editor__list button", { hasText: "Start: Start" }).tap();
  await component(page).getByRole("button", { name: "Connect", exact: true }).first().tap();
  await component(page).locator(".ef-workflow-editor__list button", { hasText: "Task: New task" }).tap();
  await expect(page.locator(".studio-status")).toContainText("change kept");
  // The mobile contract: no page-level horizontal scrolling (the canvas scrolls inside its own region).
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBeLessThanOrEqual(0);
  await context.close();
});
