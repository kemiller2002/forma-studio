// Assembles the Flow editor into app-dist/: the Limen kernel page (src/kernel),
// the F# engine published to .NET WebAssembly (src/wasm), the Limen browser
// kernel, and the vendored Forma presentation. No bundler.
import { execFileSync } from "node:child_process";
import { cpSync, mkdirSync, rmSync } from "node:fs";
import { resolve } from "node:path";

const root = resolve(new URL("..", import.meta.url).pathname);
const out = resolve(root, "app-dist");
const published = resolve(root, "build/wasm");

rmSync(out, { recursive: true, force: true });
rmSync(published, { recursive: true, force: true });
execFileSync("dotnet", ["publish", resolve(root, "src/wasm/FormaStudio.Wasm/FormaStudio.Wasm.csproj"), "-c", "Release", "-o", published], { stdio: "inherit" });

mkdirSync(out, { recursive: true });
for (const file of ["index.html", "main.js", "transport.js", "gestures.js", "workflows.js", "studio.css"]) {
  cpSync(resolve(root, "src/kernel", file), resolve(out, file));
}
cpSync(resolve(published, "wwwroot/_framework"), resolve(out, "_framework"), { recursive: true });
cpSync(resolve(root, "node_modules/@echelon-foundry/typescript-wasm-kernel/dist"), resolve(out, "limen"), { recursive: true });
cpSync(resolve(root, "vendor/forma"), resolve(out, "forma"), { recursive: true });
// The public Forma workflow component (element + editor CSS); Studio supplies its transport.
cpSync(resolve(root, "vendor/forma/workflow/browser"), resolve(out, "forma-workflow"), { recursive: true });
console.log(`Forma Studio editor assembled at ${out}`);
