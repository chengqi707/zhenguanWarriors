// 角色图鉴页验证：主菜单 → 角色图鉴 → 卡片网格 → 点击卡片看详情弹窗
// 用法：先启动 dev/preview 服务，再 BASE_URL=... node scripts/gallery-check.mjs
import { chromium } from 'playwright-core';
import { mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const BASE = process.env.BASE_URL ?? 'http://localhost:4173';
const OUT = fileURLToPath(new URL('../shots/', import.meta.url));
mkdirSync(OUT, { recursive: true });

const browser = await chromium.launch({ channel: 'chrome', headless: true });
const ctx = await browser.newContext({
  viewport: { width: 390, height: 844 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true,
});
const page = await ctx.newPage();
page.on('pageerror', e => console.error('[页面错误]', e.message));

await page.goto(BASE, { waitUntil: 'networkidle' });
// 启动页：点击进入主菜单（若有）
await page.waitForTimeout(800);
await page.mouse.click(195, 422);
await page.waitForTimeout(600);

// 主菜单应出现「角色图鉴」按钮
const galleryBtn = page.locator('button', { hasText: '角色图鉴' });
await galleryBtn.waitFor({ timeout: 5000 });
await page.screenshot({ path: `${OUT}g01_mainmenu.png` });
console.log('📸 主菜单（含图鉴入口）');

await galleryBtn.click();
await page.waitForTimeout(1200); // 等立绘图片加载
await page.screenshot({ path: `${OUT}g02_gallery.png`, fullPage: false });
console.log('📸 图鉴网格');

// 点第一张卡（李世民）看详情
await page.locator('.gallery-card').first().click();
await page.waitForTimeout(500);
await page.screenshot({ path: `${OUT}g03_detail.png` });
console.log('📸 角色详情');

// 滚动图鉴列表看后半部分
await page.locator('.gallery-close').click();
await page.locator('.page-scroll').evaluate(el => { el.scrollTop = el.scrollHeight; });
await page.waitForTimeout(800);
await page.screenshot({ path: `${OUT}g04_gallery_bottom.png` });
console.log('📸 图鉴底部');

await browser.close();
console.log('OK');
