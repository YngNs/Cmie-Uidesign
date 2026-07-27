import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const root = dirname(fileURLToPath(import.meta.url));
const page = join(root, "index.html");

createServer(async (_request, response) => {
  try {
    response.writeHead(200, { "Content-Type": "text/html; charset=utf-8" });
    response.end(await readFile(page));
  } catch (error) {
    response.writeHead(500, { "Content-Type": "text/plain; charset=utf-8" });
    response.end(String(error));
  }
}).listen(4173, "127.0.0.1", () => {
  console.log("Server ready at http://127.0.0.1:4173/");
});
