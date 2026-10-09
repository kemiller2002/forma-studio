// Assembles the Flow editor into app-dist/: the Limen kernel page (src/kernel),
// the F# engine published to .NET WebAssembly (src/wasm), the Limen browser
// kernel, and the vendored Forma presentation. No bundler.
import { execFileSync } from "node:child_process";
import { cpSync, existsSync, mkdirSync, readFileSync, rmSync } from "node:fs";
import { resolve } from "node:path";

const root = resolve(new URL("..", import.meta.url).pathname);
const out = resolve(root, "app-dist");
const published = resolve(root, "build/wasm");

rmSync(out, { recursive: true, force: true });
rmSync(published, { recursive: true, force: true });
execFileSync("dotnet", ["publish", resolve(root, "src/wasm/FormaStudio.Wasm/FormaStudio.Wasm.csproj"), "-c", "Release", "-o", published], { stdio: "inherit" });

mkdirSync(out, { recursive: true });
for (const file of ["index.html", "main.js", "transport.js", "gestures.js", "workflows.js", "editor-events.js", "studio.css", "studio-projection.css"]) {
  cpSync(resolve(root, "src/kernel", file), resolve(out, file));
}
cpSync(resolve(published, "wwwroot/_framework"), resolve(out, "_framework"), { recursive: true });
cpSync(resolve(root, "node_modules/@echelon-foundry/limen/dist"), resolve(out, "limen"), { recursive: true });
cpSync(resolve(root, "vendor/forma"), resolve(out, "forma"), { recursive: true });
// The pinned Forma package's compiled icons (dist/icons), copied unchanged from the
// installed dependency that package.json pins. A release without icons (Forma 0.4.1 and
// earlier) ships none, and the editor reports icons unavailable; nothing is substituted.
const pinned = resolve(root, "node_modules/@echelon-foundry/design-system");
const pinnedIcons = resolve(pinned, "dist/icons");
if (existsSync(resolve(pinnedIcons, "registry.json"))) {
  const { version } = JSON.parse(readFileSync(resolve(pinned, "package.json"), "utf8"));
  const { formaVersion } = JSON.parse(readFileSync(resolve(pinnedIcons, "registry.json"), "utf8"));
  if (formaVersion !== version) throw new Error(`dist/icons/registry.json is from Forma ${formaVersion}, but the pinned package is ${version}`);
  cpSync(pinnedIcons, resolve(out, "forma/icons"), { recursive: true });
  console.log(`Forma ${version} icons copied from the pinned package`);
} else {
  console.log("The pinned Forma package has no icons; the editor will report icons unavailable");
}
// The public Forma workflow component (element + editor CSS); Studio supplies its transport.
cpSync(resolve(root, "vendor/forma/workflow/browser"), resolve(out, "forma-workflow"), { recursive: true });
console.log(`Forma Studio editor assembled at ${out}`);
