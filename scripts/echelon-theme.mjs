// The Echelon brand for Forma 1.x: the public, released @echelon-foundry/echelon-theme.
// Studio packages it rather than copying it into git: package.json pins the release
// tarball, and vendor/echelon-theme/MANIFEST.json records its provenance (release tag,
// commit, tarball sha256) and the SHA-256 of every file Studio uses from it. Every
// build that ships the theme (editor, marketing site) copies only verified files.
//   node scripts/echelon-theme.mjs --check     the installed package matches the manifest (offline)
//   node scripts/echelon-theme.mjs --release   also download the release tarball, check its
//                                              sha256 and every recorded file inside it (network)
//   node scripts/echelon-theme.mjs --record    rewrite the manifest's file digests from the
//                                              installed package (after moving the pin)
import { createHash } from "node:crypto";
import { execFileSync } from "node:child_process";
import { cpSync, existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const manifestPath = join(root, "vendor/echelon-theme/MANIFEST.json");
export const installed = join(root, "node_modules/@echelon-foundry/echelon-theme");
const sha = (file) => createHash("sha256").update(readFileSync(file)).digest("hex");
const readJson = (file) => JSON.parse(readFileSync(file, "utf8"));

export const manifest = () => readJson(manifestPath);

/** Files of the package (paths relative to it) whose digest differs from the manifest, or that are missing. */
const drift = (packageDir, files) => Object.entries(files).filter(([file, hash]) => !existsSync(join(packageDir, file)) || sha(join(packageDir, file)) !== hash).map(([file]) => file);

/** The pins that must name the manifest's release. */
const pinProblems = (m) => {
  const problems = [];
  const pinned = readJson(join(root, "package.json")).dependencies?.[m.package];
  if (pinned !== m.tarball.url) problems.push(`package.json pins ${m.package} to ${pinned}, not ${m.tarball.url}`);
  const lock = readJson(join(root, "capabilities.lock.json")).capabilities?.echelonTheme;
  if (lock?.version !== m.version || lock?.source !== m.tarball.url || lock?.sha256 !== m.tarball.sha256) problems.push(`capabilities.lock.json echelonTheme does not name ${m.version} at ${m.tarball.url} (sha256 ${m.tarball.sha256})`);
  if (existsSync(join(installed, "package.json"))) {
    const { name, version } = readJson(join(installed, "package.json"));
    if (name !== m.package || version !== m.version) problems.push(`the installed package is ${name}@${version}, not ${m.package}@${m.version}`);
  }
  return problems;
};

/**
 * Copies the verified theme into `target` (dist files at its root, as the theme's own
 * relative font URLs expect). Throws, copying nothing, if the installed package drifts.
 */
export const copyVerifiedTheme = (target) => {
  const m = manifest();
  if (!existsSync(join(installed, "package.json"))) throw new Error(`${m.package} is not installed; run npm install first.`);
  const problems = [...pinProblems(m), ...drift(installed, m.files).map((f) => `${f} differs from ${m.package}@${m.version}`)];
  if (problems.length) throw new Error(problems.join("\n"));
  for (const file of Object.keys(m.files)) {
    const to = join(target, file.replace(/^dist\//, ""));
    mkdirSync(dirname(to), { recursive: true });
    cpSync(join(installed, file), to);
  }
  return Object.keys(m.files).length;
};

const run = () => {
  const m = manifest();
  if (process.argv.includes("--record")) {
    const files = Object.fromEntries(Object.keys(m.files).sort().map((file) => [file, sha(join(installed, file))]));
    writeFileSync(manifestPath, JSON.stringify({ ...m, files }, null, 2) + "\n");
    console.log(`recorded ${Object.keys(files).length} files of the installed ${m.package}`);
    return 0;
  }
  const pins = pinProblems(m);
  if (pins.length) { console.error(pins.join("\n")); return 1; }
  if (!existsSync(join(installed, "package.json"))) { console.error(`${m.package} is not installed; run npm install first.`); return 1; }
  const local = drift(installed, m.files);
  if (local.length) { console.error(`the installed ${m.package} differs from the manifest: ${local.join(", ")}`); return 1; }
  console.log(`${m.package}@${m.version} matches vendor/echelon-theme/MANIFEST.json (${Object.keys(m.files).length} files)`);
  if (process.argv.includes("--release")) {
    const dir = mkdtempSync(join(tmpdir(), "echelon-theme-release-"));
    try {
      const tarball = join(dir, "theme.tgz");
      execFileSync("curl", ["-fsSL", "--retry", "3", "-o", tarball, m.tarball.url]);
      const digest = sha(tarball);
      if (digest !== m.tarball.sha256) { console.error(`the release tarball's sha256 is ${digest}, not ${m.tarball.sha256}`); return 1; }
      execFileSync("tar", ["-xzf", tarball, "-C", dir]);
      const published = drift(join(dir, "package"), m.files);
      if (published.length) { console.error(`recorded files differ from the published release: ${published.join(", ")}`); return 1; }
      console.log(`${Object.keys(m.files).length} files match the ${m.tag} release tarball (sha256 ${digest})`);
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  }
  return 0;
};

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) process.exit(run());
