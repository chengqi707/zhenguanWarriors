// R4 战场视觉验证：新兵种模型（矛兵/投石车）、新地形（关隘/营寨/栅栏）、
// 移动动画、攻击预览无命中行、地形条无回避/命中字段
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

let n = 0;
const shot = async name => { n++; await page.screenshot({ path: `${OUT}r4_${String(n).padStart(2, '0')}_${name}.png` }); console.log('📸', name); };

async function enterBattle(levelId, chars) {
  await page.goto(BASE, { waitUntil: 'networkidle' });
  await page.evaluate(({ levelId, chars }) => {
    const mk = id => ({ id, level: 10, exp: 0, equipment: {}, isUnlocked: true });
    localStorage.setItem('zg_warriors_save', JSON.stringify({
      version: 1, timestamp: Date.now(), currentLevel: 8, difficulty: 'normal', gold: 0,
      characters: chars.map(mk), inventory: [], items: { jinchuang: 3 }, levelStates: {}, tutorialDone: true,
    }));
  }, { levelId, chars });
  await page.reload({ waitUntil: 'networkidle' });
  await page.waitForSelector('.menu-title');
  await page.getByText('继续游戏').click();
  await page.waitForSelector('.level-list');
  await page.getByText(levelId === 0 ? '序幕' : `第${levelId}关`).click();
  if (await page.$('.story-skip')) await page.getByText('跳过').click();
  await page.waitForSelector('.hero-grid');
  await page.getByText('确认阵容').click();
  await page.waitForSelector('.equip-main');
  await page.getByText('⚔ 开始战斗').click();
  await page.waitForFunction(() => !!window.__scene, null, { timeout: 8000 });
  await page.waitForTimeout(1500);
}
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
const zoomOn = async (q, r, scale) => {
  await page.evaluate(({ q, r, scale }) => {
    const s = window.__scene;
    const HEX = 32, SQRT3 = Math.sqrt(3);
    const wx = HEX + HEX * 1.5 * q;
    const wy = (HEX * SQRT3) / 2 + HEX * SQRT3 * (r + 0.5 * (q & 1));
    const rect = s.canvas.getBoundingClientRect();
    const p = s.camera.worldToScreen(wx, wy);
    s.camera.zoomAt(p.x, p.y, scale);
  }, { q, r, scale });
  await page.waitForTimeout(300);
};

// ---------- 第 2 关：矛兵（2 个步兵改矛兵）+ 栅栏地形 ----------
await enterBattle(2, ['lishimin', 'zhangsun_wuji', 'chai_shao', 'liu_hongji', 'li_jing', 'duan_zhixuan', 'yin_kaishan', 'qin_qiong']);
await zoomOn(14, 7, 1.8);
await shot('spear_enemies'); // 敌军区域放大看矛兵
// 点栅栏格看地形条
await page.evaluate(() => {
  const s = window.__scene;
  const t = s.opts.level.terrain;
  const fence = Object.keys(t).find(k => t[k] === 'fence');
  window.__fence = fence ? fence.split(',').map(Number) : null;
});
const fence = await page.evaluate(() => window.__fence);
if (fence) { await clickCell(fence[0], fence[1]); await page.waitForTimeout(300); await shot('fence_terrain_info'); }

// ---------- 第 6 关：投石车 + 关隘 ----------
await enterBattle(6, ['lishimin', 'zhangsun_wuji', 'chai_shao', 'liu_hongji', 'li_jing', 'yin_kaishan', 'qin_qiong', 'cheng_yaojin']);
await zoomOn(16, 9, 1.6);
await shot('catapult_enemies');
const pass = await page.evaluate(() => {
  const s = window.__scene;
  const t = s.opts.level.terrain;
  const k2 = Object.keys(t).find(k => t[k] === 'pass');
  return k2 ? k2.split(',').map(Number) : null;
});
if (pass) { await clickCell(pass[0], pass[1]); await page.waitForTimeout(300); await shot('pass_terrain_info'); }

// ---------- 第 5 关：营寨 ----------
await enterBattle(5, ['lishimin', 'zhangsun_wuji', 'chai_shao', 'liu_hongji', 'li_jing', 'yuchi_jingde', 'zhangsun_empress', 'qin_qiong']);
const camp = await page.evaluate(() => {
  const s = window.__scene;
  const t = s.opts.level.terrain;
  const k2 = Object.keys(t).find(k => t[k] === 'camp');
  return k2 ? k2.split(',').map(Number) : null;
});
if (camp) {
  await zoomOn(camp[0], camp[1], 1.8);
  await clickCell(camp[0], camp[1]);
  await page.waitForTimeout(300);
  await shot('camp_terrain');
}

// ---------- 序幕：骑兵移动动画（连拍 3 帧） ----------
await enterBattle(0, ['lishimin', 'zhangsun_wuji']);
const hero = await page.evaluate(() => { const u = window.__scene.battle.state.units.find(u => u.isHero); return { q: u.q, r: u.r, uid: u.uid }; });
await clickCell(hero.q, hero.r);
await page.waitForTimeout(300);
const target = await page.evaluate(() => {
  const s = window.__scene;
  const u = s.battle.state.units.find(x => x.isHero);
  const sel = s.battle.selectUnit(u.uid);
  // 选最远的可达格，让移动动画足够长
  return sel.reachable.sort((a, b) => (b.q + Math.abs(b.r - u.r)) - (a.q + Math.abs(a.r - u.r)))[0];
});
await clickCell(target.q, target.r);
await page.waitForTimeout(250);
await shot('cavalry_move_f1');
await page.waitForTimeout(250);
await shot('cavalry_move_f2');
await page.waitForTimeout(300);
await shot('cavalry_move_f3');

// ---------- 攻击预览（应无命中行） ----------
const previewText = await page.evaluate(() => {
  const cards = document.querySelector('.zg-bottom');
  return cards ? cards.textContent : '';
});
console.log('底部面板文本：', previewText?.slice(0, 100));
console.log('✅ R4 战场视觉验证完成');
await browser.close();
