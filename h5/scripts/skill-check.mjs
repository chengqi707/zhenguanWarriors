// 验证新技能（落雷/地裂）在战斗 UI 中可见
import { chromium } from 'playwright-core';
import { mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const BASE = process.env.BASE_URL ?? 'http://localhost:4173';
const OUT = fileURLToPath(new URL('../shots/', import.meta.url));
mkdirSync(OUT, { recursive: true });

const browser = await chromium.launch({ channel: 'chrome', headless: true });
const ctx = await browser.newContext({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true });
const page = await ctx.newPage();

await page.goto(BASE, { waitUntil: 'networkidle' });
await page.evaluate(() => {
  const mk = id => ({ id, level: 10, exp: 0, equipment: {}, isUnlocked: true });
  localStorage.setItem('zg_warriors_save', JSON.stringify({
    version: 1, timestamp: Date.now(), currentLevel: 6, difficulty: 'normal', gold: 0,
    characters: ['lishimin','zhangsun_wuji','fang_xuanling','du_ruhui','li_jing','chai_shao','liu_hongji','qin_qiong'].map(mk),
    inventory: [], items: { jinchuang: 3 }, levelStates: {}, tutorialDone: true,
  }));
});
await page.reload({ waitUntil: 'networkidle' });
await page.waitForSelector('.menu-title');
await page.getByText('继续游戏').click();
await page.waitForSelector('.level-list');
await page.getByText('第6关').click();
if (await page.$('.story-skip')) await page.getByText('跳过').click();
await page.waitForSelector('.hero-grid');
await page.getByText('确认阵容').click();
await page.waitForSelector('.equip-main');
await page.getByText('⚔ 开始战斗').click();
await page.waitForFunction(() => !!window.__scene, null, { timeout: 8000 });
await page.waitForTimeout(1500);

const clickCell = async (q, r) => {
  const pos = await page.evaluate(({ q, r }) => {
    const s = window.__scene;
    const HEX = 32, SQRT3 = Math.sqrt(3);
    const wx = HEX + HEX * 1.5 * q;
    const wy = (HEX * SQRT3) / 2 + HEX * SQRT3 * (r + 0.5 * (q & 1));
    const p = s.camera.worldToScreen(wx, wy);
    const rect = s.canvas.getBoundingClientRect();
    return { x: rect.left + p.x, y: rect.top + p.y };
  }, { q, r });
  await page.mouse.click(pos.x, pos.y);
};

// 点长孙无忌
const hero = await page.evaluate(() => {
  const u = window.__scene.battle.state.units.find(u => u.faction === 'player' && u.charId === 'zhangsun_wuji');
  if (u) console.log('unit skills:', u.skills);
  return u ? { q: u.q, r: u.r, skills: u.skills } : null;
});
console.log('hero:', hero);
if (!hero) { console.log('未找到长孙无忌'); await browser.close(); process.exit(1); }
await clickCell(hero.q, hero.r);
await page.waitForTimeout(400);
await page.screenshot({ path: `${OUT}skill_select_zhangsun.png` });

// 点计策
await page.getByText('计策', { exact: true }).first().click();
await page.waitForTimeout(400);
await page.screenshot({ path: `${OUT}skill_list_zhangsun.png` });

// 点落雷
const hasThunder = await page.getByText('落雷', { exact: true }).first().isVisible().catch(() => false);
console.log('长孙无忌 落雷 可见:', hasThunder);

// 点房玄龄
const hero2 = await page.evaluate(() => {
  const u = window.__scene.battle.state.units.find(u => u.faction === 'player' && u.charId === 'fang_xuanling');
  return u ? { q: u.q, r: u.r } : null;
});
if (hero2) {
  await clickCell(hero2.q, hero2.r);
  await page.waitForTimeout(400);
  await page.getByText('计策', { exact: true }).first().click();
  await page.waitForTimeout(400);
  const hasThunder2 = await page.getByText('落雷', { exact: true }).first().isVisible().catch(() => false);
  console.log('房玄龄 落雷 可见:', hasThunder2);
} else { console.log('未找到房玄龄'); }

// 点杜如晦
const hero3 = await page.evaluate(() => {
  const u = window.__scene.battle.state.units.find(u => u.faction === 'player' && u.charId === 'du_ruhui');
  return u ? { q: u.q, r: u.r } : null;
});
if (hero3) {
  await clickCell(hero3.q, hero3.r);
  await page.waitForTimeout(400);
  await page.getByText('计策', { exact: true }).first().click();
  await page.waitForTimeout(400);
  const hasEarth = await page.getByText('地裂', { exact: true }).first().isVisible().catch(() => false);
  console.log('杜如晦 地裂 可见:', hasEarth);
} else { console.log('未找到杜如晦'); }

await browser.close();
console.log('✅ 技能检查完成');
