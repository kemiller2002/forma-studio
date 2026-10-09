// Copies the public Forma artifacts Studio consumes from a kemiller2002/forma
// checkout into vendor/forma, and records their hashes, source commit and
// release provenance in vendor/forma/MANIFEST.json.
//   node scripts/sync-forma.mjs ../forma     re-vendor from a checkout of a release commit
//   node scripts/sync-forma.mjs --check      verify against the manifest (offline)
//   node scripts/sync-forma.mjs --release    also verify every packaged file against
//                                            the published npm tarballs (network)
// Studio's engine compiles Forma.Workflow's F# source, which no package ships, so
// the source stays vendored; every file a release package does ship must match it.
import { createHash } from "node:crypto";
import { execFileSync } from "node:child_process";
import { cpSync, existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, rmSync, statSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, relative, resolve } from "node:path";

const root = resolve(new URL("..", import.meta.url).pathname);
const vendor = resolve(root, "vendor/forma");
const manifestPath = join(vendor, "MANIFEST.json");
const sha = (file) => createHash("sha256").update(readFileSync(file)).digest("hex");
const walk = (dir) => readdirSync(dir).flatMap((n) => (statSync(join(dir, n)).isDirectory() ? walk(join(dir, n)) : [join(dir, n)]));

// The released packages, and where each vendored file sits inside their tarballs.
const designSystem = { name: "@echelon-foundry/design-system", version: "0.5.0" };
const workflowPackage = { name: "@echelon-foundry/forma-workflow", version: "1.0.0" };
const packaged = (vendorPath) =>
  vendorPath.startsWith("workflow/browser/")
    ? [workflowPackage, "dist/" + vendorPath.slice("workflow/browser/".length)]
    : vendorPath.startsWith("workflow/fixtures/")
      ? [designSystem, "examples/workflows/forma/workflows/" + vendorPath.slice("workflow/fixtures/".length)]
      : ({
          "tokens.css": [designSystem, "dist/tokens.css"],
          "foundations.css": [designSystem, "dist/foundations.css"],
          "components.css": [designSystem, "dist/components.css"],
          "brands/echelon.css": [designSystem, "dist/brands/echelon.css"],
          "brands/example-harbor.css": [designSystem, "dist/brands/example-harbor.css"],
          "workflow/forma-workflow.schema.json": [designSystem, "schemas/workflow/1.0/forma-workflow.schema.json"],
          "workflow/workflow-capabilities.json": [designSystem, "contracts/workflow-capabilities.json"],
          "workflow/diagram-presentation.json": [designSystem, "contracts/diagram-presentation.json"]
        })[vendorPath];

const fetchPackage = (pkg) => {
  const dir = mkdtempSync(join(tmpdir(), "forma-release-"));
  const tarball = execFileSync("npm", ["pack", `${pkg.name}@${pkg.version}`, "--pack-destination", dir, "--silent"], { encoding: "utf8" }).trim().split("\n").pop();
  execFileSync("tar", ["-xzf", join(dir, tarball), "-C", dir]);
  return join(dir, "package");
};

const checkRelease = (manifest) => {
  const releases = manifest.releases ?? [];
  const unpacked = new Map(releases.map((pkg) => [pkg.name, fetchPackage(pkg)]));
  const problems = Object.entries(manifest.released ?? {}).filter(([file, { package: name, path }]) => {
    const published = join(unpacked.get(name) ?? "", path);
    return !unpacked.has(name) || !existsSync(published) || sha(published) !== manifest.files[file];
  });
  [...unpacked.values()].forEach((dir) => rmSync(dirname(dir), { recursive: true, force: true }));
  return problems.map(([file, { package: name, path }]) => `${file} (${name}/${path})`);
};

if (process.argv.includes("--check") || process.argv.includes("--release")) {
  const manifest = JSON.parse(readFileSync(manifestPath, "utf8"));
  const drift = Object.entries(manifest.files).filter(([file, hash]) => !existsSync(join(vendor, file)) || sha(join(vendor, file)) !== hash).map(([f]) => f);
  if (drift.length) {
    console.error(`vendor/forma differs from forma@${manifest.commit}: ${drift.join(", ")}`);
    process.exit(1);
  }
  console.log(`vendor/forma matches forma@${manifest.commit} (${Object.keys(manifest.files).length} files)`);
  if (process.argv.includes("--release")) {
    const mismatched = checkRelease(manifest);
    if (mismatched.length) {
      console.error(`vendored files differ from the published releases: ${mismatched.join(", ")}`);
      process.exit(1);
    }
    const names = (manifest.releases ?? []).map((p) => `${p.name}@${p.version}`).join(", ");
    console.log(`${Object.keys(manifest.released ?? {}).length} packaged files match ${names}`);
  }
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
const released = Object.fromEntries(
  Object.keys(files).flatMap((file) => {
    const found = packaged(file);
    return found ? [[file, { package: found[0].name, path: found[1] }]] : [];
  })
);
const manifest = { source: "https://github.com/kemiller2002/forma", commit, releases: [designSystem, workflowPackage], files, released };
writeFileSync(manifestPath, JSON.stringify(manifest, null, 2) + "\n");
console.log(`vendored forma@${commit}: ${Object.keys(files).length} files, ${Object.keys(released).length} from ${designSystem.name}@${designSystem.version} and ${workflowPackage.name}@${workflowPackage.version}`);
