import { expect, test } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";

const catalog = {schemaVersion:1,grid:24,icons:[
  {name:"search",category:"actions",label:"Search",keywords:["find"],origin:"original",svg:"icons/search.svg",html:"icons/html/search.html"},
  {name:"workflow",category:"workflow",label:"Workflow",keywords:["diagram"],origin:"original",svg:"icons/workflow.svg",html:"icons/html/workflow.html"}
]};
const staticSvg = '<svg xmlns="http://www.w3.org/2000/svg" class="ef-icon__svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M5 12H19"/></svg>';
const staticHtml = id => '<ef-icon class="ef-component-tag"><span class="ef-icon" data-ef-icon="' + id + '">' + staticSvg.replace('<svg ', '<svg aria-hidden="true" ') + '</span></ef-icon>';

test("Studio icon picker uses pinned registry, role-name controls, and copyable offline SVG", async ({ page }) => {
  await page.setViewportSize({width:320,height:750});
  await page.addInitScript(() => {
    Object.defineProperty(navigator, "clipboard", {
      configurable:true,
      value:{writeText:async text=>{ window.__copiedIconText=text; }}
    });
  });
  await page.route("**/forma/icons/registry.json", route => route.fulfill({status:200,contentType:"application/json",body:JSON.stringify(catalog)}));
  await page.route("**/forma/icons/html/*.html", route => {
    const id = route.request().url().includes("workflow") ? "workflow" : "search";
    route.fulfill({status:200,contentType:"text/html",body:staticHtml(id)});
  });
  await page.route("**/forma/icons/*.svg", route => route.fulfill({status:200,contentType:"image/svg+xml",body:staticSvg}));
  await page.goto("/");
  const browser = page.locator("#studio-icon-library");
  await browser.locator("summary").click();
  await expect(browser).toHaveAttribute("data-icon-catalog-ready","true");
  await expect(browser.getByRole("button",{name:"Copy Search SVG HTML"})).toBeVisible();
  await browser.getByRole("searchbox",{name:"Search icon names and categories"}).fill("diagram");
  await expect(browser.getByRole("button",{name:"Copy Workflow SVG HTML"})).toBeVisible();
  await expect(browser.getByRole("button",{name:"Copy Search SVG HTML"})).toHaveCount(0);
  await browser.getByRole("button",{name:"Copy Workflow SVG HTML"}).click();
  await expect.poll(()=>page.evaluate(()=>window.__copiedIconText)).toContain('data-ef-icon="workflow"');
  const axe=await new AxeBuilder({page}).include("#studio-icon-library").withTags(["wcag2a","wcag2aa","wcag21a","wcag21aa"]).analyze();
  expect(axe.violations.map(v=>v.id)).toEqual([]);
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=document.documentElement.clientWidth+1)).toBeTruthy();
});

test("unavailable pinned icon release is explicit and does not silently load remote artwork", async ({page}) => {
  await page.route("**/forma/icons/registry.json",route=>route.fulfill({status:404,body:"missing"}));
  await page.goto("/");
  const browser=page.locator("#studio-icon-library");
  await browser.locator("summary").click();
  await expect(browser.getByRole("status")).toContainText("does not include the icon collection");
  await expect(browser.getByRole("searchbox")).toBeDisabled();
});
