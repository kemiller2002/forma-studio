import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { expect } from "@playwright/test";
import { exceptionFor } from "./ui-conformance-exceptions.mjs";

// Browser helpers for the universal UI layout and interaction contract
// (Visual Engineering UI-LAYOUT-INTERACTION@1.0.0). They follow the API of
// Forma's reusable helpers (kemiller2002/forma PR #155: expectPeerAlignment,
// expectActionOutcome); replace these with an import once Forma publishes them.
// Nothing here guesses intent: peers come from an explicit declaration
// (data-uli-align-*) or from role and accessible name.

export const VIEWPORTS = [320, 390, 768, 1280];
export const TOLERANCE_CSS_PX = 1;

export const openStudio = async (page, width = 1280) => {
  await page.setViewportSize({ width, height: 900 });
  await page.goto("/");
  await page.waitForFunction(() => document.documentElement.dataset.studioReady === "true" && document.documentElement.dataset.studioWorkflowReady === "true");
};

export const status = (page) => page.getByRole("status");

/** Activates a control by keyboard: focus, then Enter (no pointer, no coordinates). */
export const press = async (locator, key = "Enter") => {
  await locator.focus();
  await expect(locator).toBeFocused();
  await locator.press(key);
};

/** Horizontal page overflow in CSS px (0 means none). */
export const pageOverflow = (page) => page.evaluate(() => Math.max(0, document.documentElement.scrollWidth - document.documentElement.clientWidth));

/** Controls whose own text is clipped (scrollWidth beyond their box): a cropped accessible label (ULI-L06). */
export const clippedControls = (scope) =>
  scope.locator("button, a[href]").evaluateAll((els) =>
    els.filter((el) => el.getClientRects().length && el.scrollWidth > el.clientWidth + 1).map((el) => el.textContent.trim()));

const edgeOf = (box, edge) => (edge === "top" ? box.y : edge === "bottom" ? box.y + box.height : box.y + box.height / 2);

/**
 * Measures every alignment group the page declares (data-uli-align-group and
 * data-uli-align-edge on the container, data-uli-align-peer on each measured
 * element). A group whose peers reflowed into separate rows at this width is
 * NOT_APPLICABLE for the shared edge, with the reason recorded.
 */
export const measureDeclaredGroups = async (page) => {
  await page.evaluate(() => document.fonts.ready);
  return page.locator("[data-uli-align-group]").evaluateAll((groups) =>
    groups.map((group) => {
      const id = group.getAttribute("data-uli-align-group");
      const edge = group.getAttribute("data-uli-align-edge");
      const peers = [...group.querySelectorAll(`[data-uli-align-peer="${CSS.escape(id)}"]`)].map((el) => {
        const r = el.getBoundingClientRect();
        return { tag: el.tagName.toLowerCase(), name: el.getAttribute("name") || el.textContent.trim(), x: r.x, y: r.y, width: r.width, height: r.height };
      });
      return { id, edge, peers };
    }));
};

/** The ULI-L01 verdict for one measured group: PASS, FAIL, EXCEPTION (an approved, bounded deviation) or NOT_APPLICABLE, with its delta. */
export const alignmentVerdict = ({ id, edge, peers }, tolerance = TOLERANCE_CSS_PX) => {
  if (peers.length < 2) return { id, edge, status: "NOT_APPLICABLE", reason: "fewer than two declared peers", deltaCssPx: null };
  const sorted = [...peers].sort((a, b) => a.y - b.y);
  const sameRow = sorted.every((p, i) => i === 0 || p.y < sorted[i - 1].y + sorted[i - 1].height);
  if (!sameRow) return { id, edge, status: "NOT_APPLICABLE", reason: "peers reflowed into separate rows at this width", deltaCssPx: null };
  const coords = peers.map((p) => edgeOf(p, edge));
  const deltaCssPx = Math.max(...coords) - Math.min(...coords);
  if (deltaCssPx <= tolerance) return { id, edge, status: "PASS", deltaCssPx, peers };
  const exception = exceptionFor({ id, deltaCssPx });
  return exception ? { id, edge, status: "EXCEPTION", rule: exception.rule, deltaCssPx, peers } : { id, edge, status: "FAIL", deltaCssPx, peers };
};

/**
 * Asserts an action's observable outcome: reads the state, activates the
 * control by keyboard, and requires the state to change to the expected value.
 * A state that already held before activation can never pass.
 */
export const expectActionOutcome = async ({ control, read, expected, key = "Enter" }) => {
  const before = await read();
  expect(before, "the expected outcome must not already hold before activation").not.toEqual(expected);
  await press(control, key);
  await expect.poll(read).toEqual(expected);
};

const formaDist = resolve(new URL("../..", import.meta.url).pathname, "vendor/forma");

/**
 * Opens exported HTML (a complete document) as an ordinary page, serving the
 * Forma stylesheets it declares from the vendored release. Optional `dir` sets
 * the consumer page's writing direction.
 */
export const openExport = async (page, html, { width = 1280, dir } = {}) => {
  const body = dir ? html.replace(/<html /, `<html dir="${dir}" `) : html;
  await page.route("http://127.0.0.1:4380/export/**", (route) => {
    const url = new URL(route.request().url());
    if (url.pathname === "/export/index.html") return route.fulfill({ contentType: "text/html", body });
    const file = url.pathname.split("/node_modules/@echelon-foundry/design-system/dist/")[1];
    if (file && !file.includes("..")) return route.fulfill({ contentType: "text/css", body: readFileSync(resolve(formaDist, file), "utf8") });
    return route.fulfill({ status: 404, body: "" });
  });
  await page.setViewportSize({ width, height: 900 });
  await page.goto("http://127.0.0.1:4380/export/index.html");
  await page.evaluate(() => document.fonts.ready);
};
