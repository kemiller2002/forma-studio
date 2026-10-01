// Copies the public Forma artifacts Studio consumes from a kemiller2002/forma
// checkout into vendor/forma, and records their hashes and source commit in
// vendor/forma/MANIFEST.json. Used until Studio pins a released Forma artifact
// (REQUIREMENTS.md "Dependency rules"); `node scripts/sync-forma.mjs --check`
// verifies the vendored files still match the manifest.
//   node scripts/sync-forma.mjs ../forma
import { createHash } from "node:crypto";
import { execFileSync } from "node:child_process";
import { cpSync, existsSync, mkdirSync, readFileSync, readdirSync, rmSync, statSync, writeFileSync } from "node:fs";
import { dirname, join, relative, resolve } from "node:path";

const root = resolve(new URL("..", import.meta.url).pathname);
const vendor = resolve(root, "vendor/forma");
const manifestPath = join(vendor, "MANIFEST.json");
const sha = (file) => createHash("sha256").update(readFileSync(file)).digest("hex");
const walk = (dir) => readdirSync(dir).flatMap((n) => (statSync(join(dir, n)).isDirectory() ? walk(join(dir, n)) : [join(dir, n)]));

if (process.argv.includes("--check")) {
  const manifest = JSON.parse(readFileSync(manifestPath, "utf8"));
  const problems = Object.entries(manifest.files).filter(([file, hash]) => !existsSync(join(vendor, file)) || sha(join(vendor, file)) !== hash);
  if (problems.length) {
    console.error(`vendor/forma differs from forma@${manifest.commit}: ${problems.map(([f]) => f).join(", ")}`);
    process.exit(1);
  }
  console.log(`vendor/forma matches forma@${manifest.commit} (${Object.keys(manifest.files).length} files)`);
  process.exit(0);
}

const forma = resolve(process.argv[2] ?? "../forma");
const commit = execFileSync("git", ["-C", forma, "rev-parse", "HEAD"], { encoding: "utf8" }).trim();
const copies = [
  ["dist/tokens.css", "tokens.css"],
  ["dist/foundations.css", "foundations.css"],
  ["dist/components.css", "components.css"],
  ["dist/brands/echelon.css", "brands/echelon.css"],
  ["dist/brands/example-harbor.css", "brands/example-harbor.css"],
  ["schemas/workflow/1.0/forma-workflow.schema.json", "workflow/forma-workflow.schema.json"],
  ["contracts/workflow-capabilities.json", "workflow/workflow-capabilities.json"],
  ["contracts/diagram-presentation.json", "workflow/diagram-presentation.json"],
  ["packages/workflow/src/forma-workflow.js", "workflow/browser/forma-workflow.js"],
  ["packages/workflow/src/forma-workflow.css", "workflow/browser/forma-workflow.css"],
  ["examples/external-producer/produce.mjs", "workflow/external-producer/produce.mjs"],
  ["examples/external-producer/consume.mjs", "workflow/external-producer/consume.mjs"]
];
rmSync(join(vendor, "workflow"), { recursive: true, force: true });
for (const [from, to] of copies) {
  mkdirSync(dirname(join(vendor, to)), { recursive: true });
  cpSync(join(forma, from), join(vendor, to));
}
const sources = join(forma, "src/workflow/Forma.Workflow");
const fsproj = readFileSync(join(sources, "Forma.Workflow.fsproj"), "utf8");
const compiled = [...fsproj.matchAll(/<Compile Include="([^"]+)"/g)].map((m) => m[1]);
mkdirSync(join(vendor, "workflow/src"), { recursive: true });
for (const file of compiled) cpSync(join(sources, file), join(vendor, "workflow/src", file));
for (const file of readdirSync(join(forma, "examples/workflows/forma/workflows"))) {
  mkdirSync(join(vendor, "workflow/fixtures"), { recursive: true });
  cpSync(join(forma, "examples/workflows/forma/workflows", file), join(vendor, "workflow/fixtures", file));
}
// The project file is the upstream one with resource paths pointing at the vendored contracts,
// and with FSharp.Core pinned to Studio's version (Aegis.Core needs >= 10.1.400) so the SDK's
// implicit FSharp.Core cannot differ between Studio's projects and this one (NU1605).
const studioFSharpCore = `  <ItemGroup>
    <PackageReference Update="FSharp.Core" Version="10.1.400" />
  </ItemGroup>
</Project>`;
writeFileSync(
  join(vendor, "workflow/src/Forma.Workflow.fsproj"),
  fsproj
    .replace("../../../schemas/workflow/1.0/forma-workflow.schema.json", "../forma-workflow.schema.json")
    .replace("../../../contracts/workflow-capabilities.json", "../workflow-capabilities.json")
    .replace("</Project>", studioFSharpCore)
);
const files = Object.fromEntries(walk(vendor).filter((f) => !f.endsWith("MANIFEST.json") && !f.endsWith("SOURCE.md") && !f.includes("/bin/") && !f.includes("/obj/")).sort().map((f) => [relative(vendor, f), sha(f)]));
writeFileSync(manifestPath, JSON.stringify({ source: "https://github.com/kemiller2002/forma", commit, files }, null, 2) + "\n");
console.log(`vendored forma@${commit}: ${Object.keys(files).length} files`);
