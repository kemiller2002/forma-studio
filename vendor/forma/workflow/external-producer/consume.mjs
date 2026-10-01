// An external consumer: reads a workflow another tool edited and proves that
// everything the producer wrote survived. Plain Node; no Forma code.
//   node consume.mjs <original.forma-workflow.json> <edited.forma-workflow.json>
import { readFileSync } from "node:fs";

const [originalPath, editedPath] = process.argv.slice(2);
const original = JSON.parse(readFileSync(originalPath, "utf8"));
const edited = JSON.parse(readFileSync(editedPath, "utf8"));

// Semantic equality: object key order and number spelling do not matter.
const same = (a, b) => JSON.stringify(sort(a)) === JSON.stringify(sort(b));
const sort = (v) => Array.isArray(v) ? v.map(sort) : v && typeof v === "object"
  ? Object.fromEntries(Object.keys(v).sort().map((k) => [k, sort(v[k])])) : v;
const byId = (items = []) => new Map(items.map((i) => [i.id, i]));
const semantic = (o, keys) => Object.fromEntries(keys.filter((k) => k in o).map((k) => [k, o[k]]));

const checks = [];
const check = (name, ok) => checks.push({ name, ok });

check("format and workflow id", edited.format === "forma-workflow" && edited.id === original.id);
check("workflow metadata", same(original.metadata, edited.metadata));
check("workflow extensions", same(original.extensions, edited.extensions));
const nodes = byId(edited.nodes);
for (const n of original.nodes) {
  const e = nodes.get(n.id);
  check(`node ${n.id} kept its id`, Boolean(e));
  if (!e) continue;
  check(`node ${n.id} semantics (kind, kindLabel, status, colour, references)`, same(semantic(n, ["kind", "kindLabel", "status", "color", "references"]), semantic(e, ["kind", "kindLabel", "status", "color", "references"])));
  check(`node ${n.id} metadata`, same(n.metadata, e.metadata));
  check(`node ${n.id} extensions`, same(n.extensions, e.extensions));
}
const edges = byId(edited.edges);
for (const x of original.edges) {
  const e = edges.get(x.id);
  check(`edge ${x.id} kept its id`, Boolean(e));
  if (!e) continue;
  check(`edge ${x.id} semantics`, same(semantic(x, ["source", "target", "kind", "label", "line", "direction"]), semantic(e, ["source", "target", "kind", "label", "line", "direction"])));
  check(`edge ${x.id} metadata and extensions`, same(x.metadata, e.metadata) && same(x.extensions, e.extensions));
}
const groups = byId(edited.groups);
for (const g of original.groups ?? []) check(`group ${g.id}`, same(g, semantic(groups.get(g.id) ?? {}, Object.keys(g))));

for (const c of checks) console.log(`${c.ok ? "ok  " : "LOST"} ${c.name}`);
const lost = checks.filter((c) => !c.ok).length;
console.log(`\n${checks.length - lost} preserved, ${lost} lost`);
process.exit(lost === 0 ? 0 : 1);
