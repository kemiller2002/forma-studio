import fs from "node:fs";

const schema = JSON.parse(fs.readFileSync("schemas/forma-studio.schema.json", "utf8"));
const example = JSON.parse(fs.readFileSync("examples/two-page-project.json", "utf8"));

if (schema.properties?.schemaVersion?.const !== 1) {
  throw new Error("schema version 1 contract missing");
}
if (example.schemaVersion !== 1) {
  throw new Error("example schema version does not match");
}
if (!Array.isArray(example.pages) || example.pages.length < 2) {
  throw new Error("linked-page example must have at least two pages");
}
const pageIds = new Set(example.pages.map((p) => p.pageId));
if (!pageIds.has(example.startPageId)) {
  throw new Error("start page does not exist");
}
const routes = example.pages.map((p) => p.route).filter(Boolean);
if (new Set(routes).size !== routes.length) {
  throw new Error("example has duplicate routes");
}

let linkCount = 0;
function visit(node) {
  for (const action of node.navigation ?? []) {
    linkCount += 1;
    if (action.target?.kind === "page" && !pageIds.has(action.target.pageId)) {
      throw new Error(`broken internal target: ${action.target.pageId}`);
    }
  }
  for (const children of Object.values(node.slots ?? {})) {
    for (const child of children) visit(child);
  }
}
for (const page of example.pages) for (const node of page.nodes) visit(node);
if (linkCount < 1) throw new Error("example must demonstrate internal page linking");

console.log(`schema fixture validation passed: ${example.pages.length} pages, ${linkCount} internal navigation action(s)`);
