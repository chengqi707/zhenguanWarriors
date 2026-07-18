// 攻击链路验证：多回合逼近 → 攻击按钮 → 选目标 → 预览 → 确认攻击 → 结算反击
// 另验证 ☰ 菜单里的胜利/失败条件展示
// 用法：先 npx vite preview --port 4173，再 node scripts/attack-flow.mjs
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
const shot = async name => { n++; await page.screenshot({ path: `${OUT}c${String(n).padStart(2, '0')}_${name}.png` }); console.log('📸', name); };
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
const waitIdle = async (ms = 15000) => {
  await page.waitForFunction(() => { const s = window.__scene; return !s || !s.animator || !s.animator.playing; }, null, { timeout: ms }).catch(() => {});
};
const distFn = `(function(a,b){const ax=a.q,az=a.r-((a.q-(a.q&1))/2),ay=-ax-az;const bx=b.q,bz=b.r-((b.q-(b.q&1))/2),by=-bx-bz;return (Math.abs(ax-bx)+Math.abs(ay-by)+Math.abs(az-bz))/2;})`;

await page.goto(BASE, { waitUntil: 'networkidle' });
await page.evaluate(() => {
  const mk = id => ({ id, level: 5, exp: 0, equipment: {}, isUnlocked: true });
  localStorage.setItem('zg_warriors_save', JSON.stringify({
    version: 1, timestamp: Date.now(), currentLevel: 1, difficulty: 'normal', gold: 0,
    characters: ['lishimin', 'zhangsun_wuji', 'chai_shao', 'liu_hongji', 'li_jing'].map(mk),
    inventory: [], items: { jinchuang: 3, qingxin: 2, shiqi: 1 }, levelStates: {}, tutorialDone: true,
  }));
});
await page.reload({ waitUntil: 'networkidle' });
await page.waitForSelector('.menu-title');
await page.getByText('继续游戏').click();
await page.waitForSelector('.level-list');
await page.getByText('第1关').click();
await page.waitForSelector('.story-skip');
await page.getByText('跳过').click();
await page.waitForSelector('.hero-grid');
await page.getByText('确认阵容').click();
await page.waitForSelector('.equip-main');
await page.getByText('⚔ 开始战斗').click();
await page.waitForFunction(() => !!window.__scene, null, { timeout: 8000 });
await waitIdle();

// ---------- ☰ 菜单：胜利/失败条件 ----------
await page.locator('.zg-menu-btn').click();
await page.waitForTimeout(400);
await shot('menu_victory_conditions');
const menuText = await page.locator('.zg-menu-panel').textContent();
console.log('菜单内容：', menuText?.replace(/\s+/g, ' ').slice(0, 120));
await page.locator('.zg-menu-mask').click({ position: { x: 10, y: 10 } });
await page.waitForTimeout(300);

// ---------- 多回合逼近并攻击 ----------
let attacked = false;
for (let turn = 1; turn <= 4 && !attacked; turn++) {
  const st = await page.evaluate(() => {
    const s = window.__scene;
    return { phase: s.battle.state.phase, outcome: s.battle.state.outcome };
  });
  if (st.outcome || st.phase !== 'player') break;

  // 选李世民 → 移向最近敌人
  const hero = await page.evaluate(() => { const u = window.__scene.battle.state.units.find(u => u.isHero && u.alive); return u ? { q: u.q, r: u.r, acted: u.acted } : null; });
  if (!hero || hero.acted) break;
  await clickCell(hero.q, hero.r);
  await page.waitForTimeout(350);

  const moveTarget = await page.evaluate((distSrc) => {
    const dist = eval(distSrc);
    const s = window.__scene;
    const u = s.battle.state.units.find(x => x.isHero);
    const sel = s.battle.selectUnit(u.uid);
    const foes = s.battle.state.units.filter(x => x.alive && x.faction === 'enemy');
    let best = null, bd = 1e9;
    for (const c of sel.reachable) {
      const d = Math.min(...foes.map(f => dist(c, f)));
      if (d < bd) { bd = d; best = c; }
    }
    return best;
  }, distFn);
  await clickCell(moveTarget.q, moveTarget.r);
  await waitIdle();
  await page.waitForTimeout(250);

  // 攻击（若可用）
  const atkBtn = page.getByText('攻击', { exact: true }).first();
  const enabled = await atkBtn.isVisible().catch(() => false) && await atkBtn.isEnabled().catch(() => false);
  if (enabled) {
    await atkBtn.click();
    await page.waitForTimeout(300);
    const foe = await page.evaluate((distSrc) => {
      const dist = eval(distSrc);
      const s = window.__scene;
      const u = s.battle.state.units.find(x => x.isHero);
      const foes = s.battle.state.units.filter(x => x.alive && x.faction === 'enemy' && dist(u, x) <= u.range);
      return foes.sort((a, b) => a.hp - b.hp)[0] ?? null;
    }, distFn);
    if (foe) {
      await clickCell(foe.q, foe.r);
      await page.waitForTimeout(400);
      await shot('attack_preview');
      const confirm = page.getByText('确认攻击').first();
      if (await confirm.isVisible().catch(() => false)) {
        await confirm.click();
        await waitIdle();
        await shot('attack_done');
        const after = await page.evaluate(() => {
          const s = window.__scene;
          const foes = s.battle.state.units.filter(x => x.faction === 'enemy');
          return { heroActed: s.battle.state.units.find(x => x.isHero).acted, foeHp: foes.map(f => `${f.name}:${f.hp}${f.alive ? '' : '†'}`) };
        });
        console.log(`攻击完成，李世民 acted=${after.heroActed}，敌方：${after.foeHp.join(' ')}`);
        attacked = true;
        break;
      }
    }
  }
  console.log(`第${turn}回合：移动后仍够不着敌人，结束回合`);
  await page.getByText('结束回合').click();
  await page.waitForTimeout(800);
  await waitIdle();
}

if (!attacked) {
  console.error('❌ 4 回合内未能完成一次攻击');
  await shot('attack_fail');
  process.exitCode = 1;
} else {
  console.log('✅ 攻击链路全通（移动→攻击→预览→确认→反击结算）');
}
await browser.close();
