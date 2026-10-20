import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL, fileURLToPath } from 'node:url';
import puppeteer from 'puppeteer';

const here = path.dirname(fileURLToPath(import.meta.url));
const output = path.resolve(here, '../../artifacts/diagrams');
const browser = await puppeteer.launch({ headless: true, executablePath: process.env.MERMAID_BROWSER_PATH || undefined, args: ['--no-sandbox'] });
try {
  const page = await browser.newPage();
  await page.setViewport({ width: 1600, height: 1200, deviceScaleFactor: 1 });
  for (const name of fs.readdirSync(output).filter(n => n.endsWith('.svg')).sort()) {
    await page.goto(pathToFileURL(path.join(output, name)).href);
    await page.evaluate(() => {
      const svg = document.documentElement;
      const box = svg.viewBox.baseVal;
      const scale = Math.min(1580 / box.width, 1160 / box.height, 1);
      svg.setAttribute('width', `${box.width * scale}`);
      svg.setAttribute('height', `${box.height * scale}`);
      svg.style.maxWidth = 'none';
      document.documentElement.style.background = 'white';
    });
    await page.screenshot({ path: path.join(output, name.replace('.svg', '.png')) });
  }
} finally {
  await browser.close();
}
