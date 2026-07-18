// AI 立绘接入 + 特效 验证截图
import { chromium } from 'playwright-core';
import { mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const BASE = process.env.BASE_URL ?? 'http://localhost:4173';
const OUT = fileURLToPath(new URL('../shots/', import.meta.url));
mkdirSync(OUT, { recursive: true });

const browser = await chromium.launch({ channel: 'chrome', headless: true });
const ctx = await browser.newContext({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true });
const page = await ctx.newPage();
page.on('pageerror', e => console.error('[页面错误]', e.message));

await page.goto(BASE, { waitUntil: 'networkidle' });
await page.evaluate(() => {
  const mk = id => ({ id, level: 5, exp: 0, equipment: {}, isUnlocked: true });
  localStorage.setItem('zg_warriors_save', JSON.stringify({
    version: 1, timestamp: Date.now(), currentLevel: 8, difficulty: 'normal', gold: 0,
    characters: ['lishimin', 'zhangsun_wuji', 'chai_shao', 'liu_hongji', 'li_jing', 'yuchi_jingde', 'cheng_yaojin', 'qin_qiong', 'zhangsun_empress', 'pingyang_princess'].map(mk),
    inventory: [], items: { jinchuang: 3 }, levelStates: {}, tutorialDone: true,
  }));
});
await page.reload({ waitUntil: 'networkidle' });
await page.waitForSelector('.menu-title');
await page.getByText('继续游戏').click();
await page.waitForSelector('.level-list');

// 剧情（AI 立绘大图）
await page.getByText('第1关').click();
await page.waitForSelector('.story-box');
await page.waitForTimeout(1500);
await page.screenshot({ path: `${OUT}g01_story_ai_portrait.png` });
console.log('📸 g01_story_ai_portrait');
await page.getByText('跳过').click();

// 选人页（AI 立绘卡片）
await page.waitForSelector('.hero-grid');
await page.waitForTimeout(600);
await page.screenshot({ path: `${OUT}g02_heroselect_ai.png` });
console.log('📸 g02_heroselect_ai');
await page.getByText('确认阵容').click();
await page.waitForSelector('.equip-main');
await page.screenshot({ path: `${OUT}g03_equip_ai.png` });
console.log('📸 g03_equip_ai');

// 第 2 关雨天粒子特效
await page.goto(BASE, { waitUntil: 'networkidle' });
await page.getByText('继续游戏').click();
await page.waitForSelector('.level-list');
await page.getByText('第2关').click();
if (await page.$('.story-skip')) await page.getByText('跳过').click();
await page.waitForSelector('.hero-grid');
await page.getByText('确认阵容').click();
await page.waitForSelector('.equip-main');
await page.getByText('⚔ 开始战斗').click();
await page.waitForFunction(() => !!window.__scene, null, { timeout: 8000 });
await page.waitForTimeout(2500);
await page.screenshot({ path: `${OUT}g04_rain_fx.png` });
console.log('📸 g04_rain_fx');

console.log('✅ 验证完成');
await browser.close();
