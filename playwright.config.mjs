import { defineConfig } from "@playwright/test";

// Browser tests for the Studio editor (app-dist, `npm run app:build` first) and
// for the ordinary web project that consumes Studio's HTML export
// (examples/html-consumer/site, `node examples/html-consumer/build.mjs vendor/forma` first).
export default defineConfig({
  testDir: "tests/browser",
  timeout: 60_000,
  expect: { timeout: 20_000 },
  use: { baseURL: "http://127.0.0.1:4380/" },
  webServer: [
    { command: "node scripts/serve-app.mjs app-dist 4380", url: "http://127.0.0.1:4380/", reuseExistingServer: true },
    { command: "node scripts/serve-app.mjs examples/html-consumer/site 4381", url: "http://127.0.0.1:4381/index.html", reuseExistingServer: true }
  ],
  projects: [{ name: "chromium", use: { browserName: "chromium" } }]
});
