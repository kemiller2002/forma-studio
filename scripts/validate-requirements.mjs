import fs from "node:fs";
import path from "node:path";

const dir = path.resolve("requirements");
const files = fs.readdirSync(dir).filter((name) => /^\d\d-.*\.md$/.test(name)).sort();
if (files.length < 13) {
  throw new Error(`expected at least 13 numbered requirement documents, found ${files.length}`);
}

const seen = new Map();
const pattern = /^([A-Z]{2,4}-\d{3})\s+/gm;
let count = 0;

for (const file of files) {
  const text = fs.readFileSync(path.join(dir, file), "utf8");
  let match;
  while ((match = pattern.exec(text)) !== null) {
    const id = match[1];
    count += 1;
    if (seen.has(id)) {
      throw new Error(`duplicate requirement id ${id} in ${file} and ${seen.get(id)}`);
    }
    seen.set(id, file);
  }
}

if (count < 250) {
  throw new Error(`requirements baseline unexpectedly small: ${count} ids`);
}

console.log(`requirements validation passed: ${count} unique requirements across ${files.length} documents`);
