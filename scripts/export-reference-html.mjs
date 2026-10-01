// Studio -> design -> HTML export for the consumer proof (examples/html-consumer).
// Runs the Studio CLI: writes the reference designs (authored through Studio's
// command core) and exports complete documents and fragments. Output is
// deterministic and committed; `--check` fails if a fresh export differs.
import { execFileSync } from "node:child_process";
import { mkdtempSync, readFileSync, writeFileSync, mkdirSync, existsSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";

const root = resolve(new URL("..", import.meta.url).pathname);
const out = resolve(root, "examples/html-consumer");
const check = process.argv.includes("--check");
const work = mkdtempSync(join(tmpdir(), "studio-export-"));
const cli = (...args) => execFileSync("dotnet", ["run", "--project", resolve(root, "tools/FormaStudio.Cli"), "-c", "Release", "--", ...args], { encoding: "utf8", stdio: ["ignore", "pipe", "inherit"] });
const workflows = resolve(root, "vendor/forma/workflow/fixtures");
const project = join(work, "designs.forma-studio.json");
cli("designs", project, "branching");

const exports = [
  // [file, page, extra args]
  ["site/application.html", "application", ["--document", "--forma-base", "assets/forma/"]],
  ["site/responsive.html", "responsive", ["--document", "--forma-base", "assets/forma/"]],
  ["site/workflow.html", "workflow", ["--document", "--forma-base", "assets/forma/"]],
  ["site/branded.html", "application", ["--document", "--forma-base", "assets/forma/", "--brand", "example-harbor"]],
  ["content/application.fragment.html", "application", []],
  ["content/workflow.fragment.html", "workflow", []]
];
const produced = {};
for (const [file, pageId, extra] of exports) {
  const target = join(work, file);
  mkdirSync(join(target, ".."), { recursive: true });
  cli("html", project, pageId, target, "--workflows", workflows, ...extra);
  produced[file] = readFileSync(target, "utf8");
}
const workflowFile = join(work, "site/launch-readiness.workflow.html");
cli("workflow-html", join(workflows, "branching.forma-workflow.json"), workflowFile, "--document", "--forma-base", "assets/forma/");
produced["site/launch-readiness.workflow.html"] = readFileSync(workflowFile, "utf8");
produced["designs.forma-studio.json"] = readFileSync(project, "utf8");

let differences = 0;
for (const [file, text] of Object.entries(produced)) {
  const path = join(out, file);
  if (check) {
    if (!existsSync(path) || readFileSync(path, "utf8") !== text) { console.error(`differs: ${file}`); differences++; }
  } else {
    mkdirSync(join(path, ".."), { recursive: true });
    writeFileSync(path, text);
    console.log(`wrote examples/html-consumer/${file}`);
  }
}
if (check) {
  if (differences) process.exit(1);
  console.log(`HTML export is deterministic: ${Object.keys(produced).length} files match.`);
}
