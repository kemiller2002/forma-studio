// The consuming web project's own build. It knows Forma (it installs Forma's
// public CSS, and the Echelon theme for its Echelon-branded page) and nothing
// about Forma Studio: it copies the installed public assets and inserts each
// content fragment into its own page layout.
import { cpSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const formaDist = process.argv[2]; // where Forma's dist files are installed (for example node_modules/@echelon-foundry/design-system/dist)
const themeDist = process.argv[3]; // where @echelon-foundry/echelon-theme's dist files are installed
if (!formaDist || !themeDist) { console.error("usage: node build.mjs <forma dist directory> <echelon-theme dist directory>"); process.exit(2); }

const site = join(here, "site");
mkdirSync(join(site, "assets/forma/brands"), { recursive: true });
for (const file of ["tokens.css", "foundations.css", "components.css"]) cpSync(join(formaDist, file), join(site, "assets/forma", file));
for (const file of ["example-harbor.css"]) cpSync(join(formaDist, "brands", file), join(site, "assets/forma/brands", file));
// The Echelon theme as its package lays it out: echelon-fonts.css loads fonts/*.woff2 beside it.
mkdirSync(join(site, "assets/echelon-theme"), { recursive: true });
for (const file of ["echelon.css", "echelon-fonts.css", "echelon-app.css"]) cpSync(join(themeDist, file), join(site, "assets/echelon-theme", file));
cpSync(join(themeDist, "fonts"), join(site, "assets/echelon-theme/fonts"), { recursive: true });

const layout = readFileSync(join(here, "layout.html"), "utf8");
for (const file of readdirSync(join(here, "content")).filter((f) => f.endsWith(".fragment.html"))) {
  const fragment = readFileSync(join(here, "content", file), "utf8");
  writeFileSync(join(site, file.replace(".fragment.html", ".embedded.html")), layout.replace("<!-- content -->", fragment));
}
console.log(`built ${site}`);
