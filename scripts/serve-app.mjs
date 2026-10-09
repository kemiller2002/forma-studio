// Static server for app-dist during browser tests (no dependencies).
import { createServer } from "node:http"; import { readFile, stat } from "node:fs/promises"; import { resolve, extname } from "node:path";
const root = process.argv[2]; const port = Number(process.argv[3]);
const types = { ".html":"text/html", ".js":"text/javascript", ".css":"text/css", ".wasm":"application/wasm", ".json":"application/json", ".dat":"application/octet-stream", ".svg":"image/svg+xml" };
createServer(async (req, res) => { try { const p = resolve(root, "." + decodeURIComponent(new URL(req.url, "http://x").pathname)); const f = (await stat(p)).isDirectory() ? resolve(p, "index.html") : p; res.writeHead(200, { "content-type": types[extname(f)] ?? "application/octet-stream" }); res.end(await readFile(f)); } catch { res.writeHead(404); res.end(); } }).listen(port);
