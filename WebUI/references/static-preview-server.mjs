import { createServer } from "node:http";
import { readFile, stat, readdir } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import { dirname, join, extname } from "node:path";

const root = dirname(fileURLToPath(import.meta.url));
const host = "127.0.0.1";
const port = 4173;
const base = `http://${host}:${port}`;

const shortcuts = {
  "/": "/layout-proposal-v1.html",
  "/v1": "/layout-proposal-v1.html",
  "/six": "/static-six-scheme-draft.html",
};

const mime = {
  ".html": "text/html; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".svg": "image/svg+xml; charset=utf-8",
  ".png": "image/png",
  ".jpg": "image/jpeg",
  ".jpeg": "image/jpeg",
  ".webp": "image/webp",
};

createServer(async (request, response) => {
  try {
    const url = new URL(request.url || "/", base);
    let pathname = decodeURIComponent(url.pathname);
    if (shortcuts[pathname]) pathname = shortcuts[pathname];

    const filePath = join(root, pathname.replace(/^\/+/, ""));
    if (!filePath.startsWith(root)) {
      response.writeHead(403, { "Content-Type": "text/plain; charset=utf-8" });
      response.end("Forbidden");
      return;
    }

    const info = await stat(filePath);
    if (!info.isFile()) {
      response.writeHead(404, { "Content-Type": "text/plain; charset=utf-8" });
      response.end("Not Found");
      return;
    }

    const ext = extname(filePath).toLowerCase();
    response.writeHead(200, { "Content-Type": mime[ext] || "application/octet-stream" });
    response.end(await readFile(filePath));
  } catch (error) {
    response.writeHead(500, { "Content-Type": "text/plain; charset=utf-8" });
    response.end(String(error));
  }
}).listen(port, host, async () => {
  const htmlFiles = (await readdir(root))
    .filter((name) => name.endsWith(".html"))
    .sort();

  console.log(`Server ready at ${base}/`);
  console.log("Shortcuts:");
  for (const [route, target] of Object.entries(shortcuts)) {
    console.log(`  ${route.padEnd(5)} -> ${base}${target}`);
  }
  console.log("All HTML pages:");
  for (const name of htmlFiles) {
    console.log(`  ${base}/${name}`);
  }
});
