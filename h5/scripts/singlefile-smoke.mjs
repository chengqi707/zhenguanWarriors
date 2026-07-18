// 单文件（file://）冒烟验证：打开 dist-single/贞观勇士.html 跑通 主菜单→选关→剧情→选人
import { chromium } from 'playwright-core';
import { mkdirSync } from 'node:fs';
import { fileURLToPath, pathToFileURL } from 'node:url';

const fileUrl = pathToFileURL(fileURLToPath(new URL('../dist-single/贞观勇士.html', import.meta.url))).href;
const OUT = fileURLToPath(new URL('../shots/', import.meta.url));
mkdirSync(OUT, { recursive: true });

const browser = await chromium.launch({ channel: 'chrome', headless: true });
const page = await (await browser.newContext({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 2 })).newPage();
page.on('pageerror', e => { console.error('[页面错误]', e.message); process.exitCode = 1; });

await page.goto(fileUrl);
await page.waitForSelector('.menu-title', { timeout: 8000 });
await page.screenshot({ path: `${OUT}d01_singlefile_menu.png` });
await page.getByText('新游戏').click();
await page.waitForSelector('.level-list', { timeout: 5000 });
await page.getByText('第1关').click();
await page.waitForSelector('.story-skip', { timeout: 5000 });
await page.getByText('跳过').click();
await page.waitForSelector('.hero-grid', { timeout: 5000 });
await page.getByText('确认阵容').click();
await page.waitForSelector('.equip-main', { timeout: 5000 });
await page.getByText('⚔ 开始战斗').click();
await page.waitForFunction(() => !!window.__scene, null, { timeout: 8000 });
await page.waitForTimeout(2000);
await page.screenshot({ path: `${OUT}d02_singlefile_battle.png` });
console.log('✅ 单文件 file:// 冒烟通过：主菜单→选关→剧情→选人→配装→战斗');
await browser.close();
