// 新经典行动流验证：选单位→移动→行动菜单（攻击/计策/物品/待机/取消移动）
// 用法：先 npx vite preview --port 4173，再 node scripts/battle-flow.mjs
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
page.on('console', m => { if (m.type() === 'error') console.error('[console]', m.text()); });

let n = 0;
const shot = async name => { n++; await page.screenshot({ path: `${OUT}b${String(n).padStart(2, '0')}_${name}.png` }); console.log('📸', name); };
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
const waitIdle = async (ms = 10000) => {
  await page.waitForFunction(() => { const s = window.__scene; return !s || !s.animator || !s.animator.playing; }, null, { timeout: ms }).catch(() => {});
};
const distFn = `(function(a,b){const ax=a.q,az=a.r-((a.q-(a.q&1))/2),ay=-ax-az;const bx=b.q,bz=b.r-((b.q-(b.q&1))/2),by=-bx-bz;return (Math.abs(ax-bx)+Math.abs(ay-by)+Math.abs(az-bz))/2;})`;

await page.goto(BASE, { waitUntil: 'networkidle' });
await page.evaluate(() => {
  const mk = id => ({ id, level: 5, exp: 0, equipment: {}, isUnlocked: true });
  localStorage.setItem('zg_warriors_save', JSON.stringify({
    version: 1, timestamp: Date.now(), currentLevel: 1, difficulty: 'normal', gold: 0,
    characters: ['lishimin', 'zhangsun_wuji', 'chai_shao', 'liu_hongji', 'li_jing'].map(mk),
    inventory: [], items: { jinchuang: 3, qingxin: 2, shiqi: 1 }, levelStates: {}, tutorialDone: false,
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
await shot('heroselect_portrait'); // 立绘检查
await page.getByText('确认阵容').click();
await page.waitForSelector('.equip-main');
await shot('equip_portrait');
await page.getByText('⚔ 开始战斗').click();
await page.waitForFunction(() => !!window.__scene, null, { timeout: 8000 });
await waitIdle();
await page.waitForTimeout(3000); // 等开局横幅淡出 + AI 立绘加载完成
await shot('battle_visuals');

// ---------- 1. 点选李世民 ----------
const hero = await page.evaluate(() => { const u = window.__scene.battle.state.units.find(u => u.isHero); return { q: u.q, r: u.r }; });
await clickCell(hero.q, hero.r);
await page.waitForTimeout(400);
await shot('flow_selected');

// ---------- 2. 点蓝格移动 → 应弹行动菜单 ----------
const moveTarget = await page.evaluate((distSrc) => {
  const dist = eval(distSrc);
  const s = window.__scene;
  const u = s.battle.state.units.find(x => x.isHero);
  const sel = s.battle.selectUnit(u.uid);
  const foes = s.battle.state.units.filter(x => x.alive && x.faction === 'enemy');
  let best = null, bd = 1e9;
  for (const c of sel.reachable) {
    const d = Math.min(...foes.map(f => dist(c, f)));
    // 目标：移动到能打到敌人的最近格（距离<=射程）
    const score = d <= u.range ? d - 100 : d;
    if (score < bd) { bd = d <= u.range ? d - 100 : d; best = c; }
  }
  return best;
}, distFn);
await clickCell(moveTarget.q, moveTarget.r);
await waitIdle();
await page.waitForTimeout(300);
await shot('flow_action_menu'); // 关键：移动后应出现 攻击/计策/物品/待机/取消移动

// ---------- 3. 行动菜单：攻击 ----------
const canAttack = await page.getByText('攻击', { exact: true }).first().isVisible().catch(() => false);
if (canAttack) {
  const atkBtn = page.getByText('攻击', { exact: true }).first();
  if (await atkBtn.isEnabled().catch(() => true)) {
    await atkBtn.click();
    await page.waitForTimeout(300);
    await shot('flow_choose_target');
    // 点红圈敌人
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
      await shot('flow_attack_preview');
      const confirm = page.getByText('确认攻击').first();
      if (await confirm.isVisible().catch(() => false)) {
        await confirm.click();
        await waitIdle();
        await shot('flow_attack_done');
      }
    } else {
      console.log('移动后射程内无敌（意外），改点取消');
      await page.getByText('取消', { exact: true }).first().click().catch(() => {});
    }
  }
} else {
  console.log('⚠️ 未找到攻击按钮');
}

// ---------- 4. 另一单位：物品 ----------
const other = await page.evaluate(() => {
  const u = window.__scene.battle.state.units.find(x => x.alive && x.faction === 'player' && !x.acted && !x.isHero);
  return u ? { q: u.q, r: u.r, name: u.name } : null;
});
if (other) {
  await clickCell(other.q, other.r);
  await page.waitForTimeout(400);
  const itemBtn = page.getByText('物品', { exact: true }).first();
  if (await itemBtn.isVisible().catch(() => false)) {
    await itemBtn.click();
    await page.waitForTimeout(400);
    await shot('flow_items');
    const use = page.getByText('金疮药').first();
    if (await use.isVisible().catch(() => false)) {
      await use.click();
      await page.waitForTimeout(300);
      // 可能需要二次确认
      const cfm = page.getByText('确认使用').first();
      if (await cfm.isVisible().catch(() => false)) await cfm.click();
      await waitIdle();
      await shot('flow_item_used');
    }
  } else {
    console.log('⚠️ 未找到物品按钮');
  }
}

// ---------- 5. 结束回合 → 敌方行动 ----------
await page.locator('.zg-endturn-top').click();
await page.waitForTimeout(1500);
await shot('flow_enemy_turn');
await waitIdle(15000);
await shot('flow_turn2');

const st = await page.evaluate(() => {
  const s = window.__scene;
  return { turn: s.battle.state.turn, phase: s.battle.state.phase, items: s.battle.getItemCounts() };
});
console.log(`回合=${st.turn} 阶段=${st.phase} 剩余物品=${JSON.stringify(st.items)}`);
console.log('✅ 行动流走查完成');
await browser.close();
