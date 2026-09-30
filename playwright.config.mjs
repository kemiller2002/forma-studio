import { defineConfig } from "@playwright/test";

// Browser tests for the Flow editor. Run `npm run app:build` first.
export default defineConfig({
  testDir: "tests/browser",
  timeout: 60_000,
  expect: { timeout: 20_000 },
  use: { baseURL: "http://127.0.0.1:4380/" },
  webServer: { command: "node scripts/serve-app.mjs app-dist 4380", url: "http://127.0.0.1:4380/", reuseExistingServer: true },
  projects: [{ name: "chromium", use: { browserName: "chromium" } }]
});
