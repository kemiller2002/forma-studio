// Runs the external workflow round trip through the real Studio app and records
// the evidence in docs/evidence/external-workflow-roundtrip.md:
//   external producer -> .forma-workflow.json -> Forma validation -> Studio open
//   -> Studio edit -> save -> external consumer
// Needs `npm run app:build` first.
import { execFileSync } from "node:child_process";
import { resolve } from "node:path";

const root = resolve(new URL("..", import.meta.url).pathname);
const evidence = resolve(root, "docs/evidence/external-workflow-roundtrip.md");
execFileSync("npx", ["playwright", "test", "tests/browser/workflow.spec.mjs", "-g", "external producer", ...process.argv.slice(2)], {
  cwd: root, stdio: "inherit", env: { ...process.env, PROOF_EVIDENCE: evidence }
});
console.log(`evidence: ${evidence}`);
