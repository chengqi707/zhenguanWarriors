// ============================================================
// 完整流程截图走查（playwright-core + 本机 Chrome）
// 用法：先 npx vite preview --port 4173，再 node scripts/walkthrough.mjs
// 输出：h5/shots/*.png
// ============================================================
import { chromium } from 'playwright-core';
import { mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const BASE = process.env.BASE_URL ?? 'http://localhost:4173';
const OUT = fileURLToPath(new URL('../shots/', import.meta.url));
mkdirSync(OUT, { recursive: true });

const HEX = 32, SQRT3 = Math.sqrt(3);

const browser = await chromium.launch({ channel: 'chrome', headless: true });
const ctx = await browser.newContext({
  viewport: { width: 390, height: 844 },
  deviceScaleFactor: 2,
  isMobile: true,
  hasTouch: true,
  locale: 'zh-CN',
});
const page = await ctx.newPage();
page.on('pageerror', e => console.error('[页面错误]', e.message));
page.on('console', m => { if (m.type() === 'error') console.error('[console.error]', m.text()); });

let step = 0;
const shot = async name => {
  step++;
  const f = `${OUT}${String(step).padStart(2, '0')}_${name}.png`;
  await page.screenshot({ path: f });
  console.log('📸', name);
};
/** 点按钮（按文本），失败即抛错 */
const clickText = async (text, timeout = 4000) => {
  await page.getByText(text, { exact: false }).first().click({ timeout });
};
/** 战斗画布坐标换算并点击某格（复刻 renderer.cellToWorld + camera.worldToScreen） */
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
const battleState = () => page.evaluate(() => {
  const s = window.__scene;
  return {
    turn: s.battle.state.turn,
    phase: s.battle.state.phase,
    outcome: s.battle.state.outcome,
    units: s.battle.state.units.map(u => ({
      uid: u.uid, charId: u.charId, name: u.name, faction: u.faction,
      q: u.q, r: u.r, hp: u.hp, alive: u.alive, acted: u.acted, isHero: !!u.isHero,
    })),
  };
});
const waitIdle = async (ms = 6000) => {
  await page.waitForFunction(() => {
    const s = window.__scene;
    return s && !s.animator.playing;
  }, null, { timeout: ms }).catch(() => {});
};

try {
  // ---------- 启动 → 主菜单 ----------
  await page.goto(BASE, { waitUntil: 'networkidle' });
  await page.waitForTimeout(900);
  await shot('splash');
  await page.waitForSelector('.menu-title', { timeout: 8000 });
  await shot('mainmenu');

  // ---------- 设置页 ----------
  await clickText('设置');
  await page.waitForSelector('.panel-title');
  await shot('settings');
  await clickText('← 返回主菜单');
  await page.waitForSelector('.menu-title');

  // ---------- 新游戏 → 选关 ----------
  await clickText('新游戏');
  // 无存档时直接进选关；若有确认弹窗则点确认
  await page.waitForTimeout(500);
  if (await page.$('.modal-overlay')) await clickText('确认');
  await page.waitForSelector('.level-list', { timeout: 5000 });
  await shot('levelselect');

  // ---------- 序幕：剧情 → 选人（新游戏从第 0 关「雁门救驾」开始） ----------
  await clickText('序幕');
  await page.waitForSelector('.story-box', { timeout: 5000 });
  await page.waitForTimeout(1200);
  await shot('story_pre');
  await clickText('跳过');
  await page.waitForSelector('.hero-grid', { timeout: 5000 });
  await shot('heroselect');

  // 勾选前 8 名（必出已锁，未选中的逐个点上）
  const need = await page.evaluate(() => {
    const rows = [...document.querySelectorAll('.hero-card')];
    let checked = 0;
    for (const row of rows) {
      const check = row.querySelector('.hero-check');
      if (check && check.textContent.includes('✅')) checked++;
    }
    return 8 - checked;
  });
  if (need > 0) {
    const rows = await page.$$('.hero-card');
    let n = need;
    for (const row of rows) {
      if (n <= 0) break;
      const check = await row.$('.hero-check');
      const t = check ? await check.textContent() : '';
      if (t && !t.includes('✅') && !t.includes('🔒')) { await row.click(); n--; }
    }
  }
  await shot('heroselect_picked');
  await clickText('确认阵容');

  // ---------- 配装 ----------
  await page.waitForSelector('.equip-main', { timeout: 5000 });
  await shot('equipsetup');
  await clickText('⚔ 开始战斗');

  // ---------- 战斗 ----------
  await page.waitForSelector('.battle-container canvas', { timeout: 8000 });
  await page.waitForFunction(() => !!window.__scene, null, { timeout: 8000 });
  await waitIdle();
  await shot('battle_initial');

  // 选中李世民 → 移动
  let st = await battleState();
  const hero = st.units.find(u => u.isHero);
  console.log(`战斗开始：回合${st.turn} ${st.phase}，李世民@(${hero.q},${hero.r}) HP${hero.hp}`);
  await clickCell(hero.q, hero.r);
  await page.waitForTimeout(400);
  await shot('battle_selected');

  // 选一个靠敌人的可达格移动（直接问引擎可达格，挑离最近敌人最近的一格）
  const moveTarget = await page.evaluate(() => {
    const s = window.__scene;
    const heroU = s.battle.state.units.find(u => u.isHero);
    const sel = s.battle.selectUnit(heroU.uid);
    const foes = s.battle.state.units.filter(u => u.alive && u.faction === 'enemy');
    const dist = (a, b) => Math.abs(a.q - b.q) + Math.abs(a.r - b.r);
    let best = null, bd = 1e9;
    for (const c of sel.reachable) {
      if (c.q === heroU.q && c.r === heroU.r) continue;
      const d = Math.min(...foes.map(f => dist(c, f)));
      if (d < bd) { bd = d; best = c; }
    }
    return best;
  });
  if (moveTarget) {
    await clickCell(moveTarget.q, moveTarget.r);
    await waitIdle();
    await shot('battle_moved');
  }

  // 尝试攻击：找范围内敌人
  st = await battleState();
  const hero2 = st.units.find(u => u.isHero);
  const foe = st.units.filter(u => u.faction === 'enemy' && u.alive)
    .map(f => ({ ...f, d: Math.abs(f.q - hero2.q) + Math.abs(f.r - hero2.r) }))
    .sort((a, b) => a.d - b.d)[0];
  if (foe && foe.d <= 2) {
    await clickCell(foe.q, foe.r);
    await page.waitForTimeout(500);
    await shot('battle_attack_preview');
    const btn = await page.getByText('确认攻击').first();
    if (await btn.isVisible().catch(() => false)) {
      await btn.click();
      await waitIdle();
      await shot('battle_attack_done');
    }
  } else {
    console.log(`最近敌人距离 ${foe?.d}，超出攻击范围，跳过攻击步骤`);
  }

  // 结束回合 → 敌方行动
  await clickText('结束回合');
  await page.waitForTimeout(1200);
  await shot('battle_enemy_turn');
  await waitIdle(15000);
  st = await battleState();
  console.log(`敌方回合结束：回合${st.turn} ${st.phase}`);
  await shot('battle_turn2');

  // 撤退 → 战败结算
  await page.locator('.zg-menu-btn').click();
  await page.waitForTimeout(400);
  await shot('battle_menu');
  await clickText('撤退');
  await page.waitForTimeout(400);
  const confirmBtn = await page.getByText('确认撤退').first();
  if (await confirmBtn.isVisible().catch(() => false)) await confirmBtn.click();
  else await clickText('撤退').catch(() => {});
  await page.waitForSelector('.result-btns', { timeout: 8000 });
  await shot('results_lose');

  // 返回选关 → 刷新页面 → 继续游戏（验证存档持久化）
  await clickText('返回选关');
  await page.waitForSelector('.level-list', { timeout: 5000 });
  await page.reload({ waitUntil: 'networkidle' });
  await page.waitForSelector('.menu-title', { timeout: 8000 });
  await shot('mainmenu_after_reload');
  await clickText('继续游戏');
  await page.waitForSelector('.level-list', { timeout: 5000 });
  await shot('levelselect_continue');

  console.log('✅ 走查完成，截图在 h5/shots/');
} catch (e) {
  console.error('❌ 走查失败：', e.message);
  await shot('error_state');
  process.exitCode = 1;
} finally {
  await browser.close();
}
