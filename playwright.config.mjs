import { defineConfig } from "@playwright/test";

// Browser tests for the Studio editor (app-dist, `npm run app:build` first) and
// for the ordinary web project that consumes Studio's HTML export
// (examples/html-consumer/site, `node examples/html-consumer/build.mjs vendor/forma` first),
// and for the marketing site (site-dist, built by the web server command below).
export default defineConfig({
  testDir: "tests/browser",
  timeout: 60_000,
  expect: { timeout: 20_000 },
  // Failure evidence (UI-LAYOUT-INTERACTION@1.0.0 "Conformance evidence" item 4): traces and
  // screenshots of failing tests, and a machine-readable result list for CI to keep.
  reporter: [["list"], ["json", { outputFile: "playwright-evidence/results.json" }]],
  use: { baseURL: "http://127.0.0.1:4380/", trace: "retain-on-failure", screenshot: "only-on-failure" },
  webServer: [
    { command: "node scripts/serve-app.mjs app-dist 4380", url: "http://127.0.0.1:4380/", reuseExistingServer: true },
    { command: "node scripts/serve-app.mjs examples/html-consumer/site 4381", url: "http://127.0.0.1:4381/index.html", reuseExistingServer: true },
    // The marketing site (site/) is built from source on start, so its axe gate never tests a stale build.
    { command: "node scripts/build-site.mjs && node scripts/serve-app.mjs site-dist 4382", url: "http://127.0.0.1:4382/index.html", reuseExistingServer: true }
  ],
  // PLAYWRIGHT_CHROMIUM_EXECUTABLE lets an environment with a preinstalled Chromium of a
  // different revision run the suite without `playwright install`; CI leaves it unset.
  projects: [{
    name: "chromium",
    use: {
      browserName: "chromium",
      launchOptions: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE ? { executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE } : {}
    }
  }]
});
