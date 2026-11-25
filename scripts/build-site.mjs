import fs from "node:fs";
import path from "node:path";
import { copyVerifiedTheme } from "./echelon-theme.mjs";

const source = path.resolve("site");
const output = path.resolve("site-dist");
const forma = path.resolve("node_modules/@echelon-foundry/design-system/dist/all.css");

if (!fs.existsSync(forma)) {
  throw new Error("Forma CSS is missing. Install the pinned package before building the site.");
}

fs.rmSync(output, { recursive: true, force: true });
fs.mkdirSync(path.join(output, "assets"), { recursive: true });

for (const name of ["index.html", "styles.css", "CNAME"]) {
  fs.copyFileSync(path.join(source, name), path.join(output, name));
}
fs.copyFileSync(forma, path.join(output, "assets/forma.css"));
// The Echelon brand for Forma 1.x, from the installed, pinned @echelon-foundry/echelon-theme:
// only the files vendor/echelon-theme/MANIFEST.json records (echelon.css, echelon-fonts.css with
// the self-hosted fonts it loads and their licenses, echelon-app.css), each verified against its digest.
copyVerifiedTheme(path.join(output, "assets/echelon-theme"));

const html = fs.readFileSync(path.join(output, "index.html"), "utf8");
if (!html.includes("forma-studio.echelonfoundry.com") && fs.readFileSync(path.join(output, "CNAME"), "utf8").trim() !== "forma-studio.echelonfoundry.com") {
  throw new Error("custom domain contract missing");
}
if (!html.includes("./assets/forma.css")) throw new Error("marketing site does not consume Forma CSS");
if (!html.includes('name="viewport"')) throw new Error("mobile viewport metadata missing");

console.log("Forma Studio marketing site built at site-dist/");
