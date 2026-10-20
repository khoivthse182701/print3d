import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '../..');
const sources = path.join(root, 'docs/architecture/diagrams');
const output = path.join(root, 'artifacts/diagrams');
fs.mkdirSync(output, { recursive: true });
const config = { args: ['--no-sandbox'] };
if (process.env.MERMAID_BROWSER_PATH) config.executablePath = process.env.MERMAID_BROWSER_PATH;
const configPath = path.join(output, 'puppeteer.json');
fs.writeFileSync(configPath, JSON.stringify(config));
const cli = path.join(here, 'node_modules/@mermaid-js/mermaid-cli/src/cli.js');
const entries = fs.readdirSync(sources).filter(name => name.endsWith('.mmd')).sort();
let gallery = '# Mermaid gallery — thiết kế mục tiêu\n\nNguồn chính là các file `.mmd`. File này được sinh bằng `node tools/diagrams/render.mjs`. Xem [giải thích và nguồn slide](../README.md).\n';
for (const name of entries) {
  const result = spawnSync(process.execPath, [cli, '-i', path.join(sources, name), '-o', path.join(output, name.replace('.mmd', '.svg')), '-p', configPath, '-b', 'white'], { stdio: 'inherit' });
  if (result.status !== 0) throw new Error(`Mermaid render failed: ${name}`);
  gallery += `\n## ${name.replace('.mmd', '')}\n\n[Source](${name})\n\n\`\`\`mermaid\n${fs.readFileSync(path.join(sources, name), 'utf8').trim()}\n\`\`\`\n`;
}
fs.writeFileSync(path.join(sources, 'README.md'), gallery);
console.log(`Rendered ${entries.length} diagrams; gallery synchronized.`);
