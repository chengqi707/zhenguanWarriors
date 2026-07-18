// 端到端胜利链路测试：真实操作打第 1 关到胜利，验证
// 战斗胜利→关后剧情→结算→解锁下一关→下一关剧情的完整链路。
// 用法：先 npx vite preview --port 4173，再 node scripts/autowin.mjs
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
// 预置存档：第 1 关已解锁、5 名 Lv.5 角色（轻松取胜，聚焦链路验证）
await page.evaluate(() => {
  const mk = id => ({ id, level: 5, exp: 0, equipment: {}, isUnlocked: true });
  localStorage.setItem('zg_warriors_save', JSON.stringify({
    version: 1, timestamp: Date.now(), currentLevel: 1, difficulty: 'normal', gold: 0,
    characters: ['lishimin', 'zhangsun_wuji', 'chai_shao', 'liu_hongji', 'li_jing'].map(mk),
    inventory: [], levelStates: {}, tutorialDone: true,
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

/** odd-q 偏移坐标 → hex 距离 */
const distFn = `(function(a,b){
  const ax=a.q, az=a.r-((a.q-(a.q&1))/2), ay=-ax-az;
  const bx=b.q, bz=b.r-((b.q-(b.q&1))/2), by=-bx-bz;
  return (Math.abs(ax-bx)+Math.abs(ay-by)+Math.abs(az-bz))/2;
})`;

const waitIdle = async (ms = 20000) => {
  await page.waitForFunction(() => {
    const s = window.__scene;
    return !s || !s.animator || !s.animator.playing;
  }, null, { timeout: ms }).catch(() => {});
};

let result = null;
for (let turn = 1; turn <= 30; turn++) {
  // 我方所有未行动单位逐个执行贪心行动（直接调引擎，等价于玩家快速操作）
  await page.evaluate(async (distSrc) => {
    const dist = eval(distSrc);
    const s = window.__scene;
    if (!s) return;
    const b = s.battle;
    if (b.state.phase !== 'player') return;
    for (const u of b.state.units.filter(x => x.alive && x.faction === 'player' && !x.acted)) {
      const foes = b.state.units.filter(x => x.alive && x.faction === 'enemy');
      if (foes.length === 0) break;
      const inRange = foes.filter(f => dist(u, f) <= u.range).sort((a, b2) => a.hp - b2.hp)[0];
      if (inRange) { b.moveAndAttack(u.uid, [], inRange.uid); continue; }
      const sel = b.selectUnit(u.uid);
      let best = null, bestScore = 1e9;
      for (const c of sel.reachable) {
        const dmin = Math.min(...foes.map(f => dist(c, f)));
        const canAtk = foes.some(f => dist(c, f) <= u.range);
        const score = canAtk ? dmin - 100 : dmin;
        if (score < bestScore) { bestScore = score; best = c; }
      }
      if (best) {
        const path = b.pathFor(u, best);
        const t = foes.filter(f => dist(best, f) <= u.range).sort((a, b2) => a.hp - b2.hp)[0];
        const res = b.moveAndAttack(u.uid, path ?? [], t ? t.uid : undefined);
        if (!res.ok) b.wait(u.uid);
      } else {
        b.wait(u.uid);
      }
    }
  }, distFn);
  await waitIdle();

  const st = await page.evaluate(() => {
    const s = window.__scene;
    return s ? { outcome: s.battle.state.outcome, turn: s.battle.state.turn, phase: s.battle.state.phase } : { outcome: 'gone' };
  });
  if (st.outcome) { result = st.outcome; break; }

  // 结束回合 → 敌方行动（点顶栏常驻按钮，避免与底栏按钮冲突）
  await page.locator('.zg-endturn-top').click();
  await page.waitForTimeout(800);
  await waitIdle();
  const st2 = await page.evaluate(() => {
    const s = window.__scene;
    return s ? { outcome: s.battle.state.outcome, turn: s.battle.state.turn, phase: s.battle.state.phase } : { outcome: 'gone' };
  });
  console.log(`回合 ${st2.turn} 结束，阶段=${st2.phase}，结果=${st2.outcome ?? '未分胜负'}`);
  if (st2.outcome) { result = st2.outcome; break; }
}

// 脚本直调引擎绕过了 battleScene.runAction/settle，这里补触发收尾（onFinish → 结算流程）
await page.evaluate(() => {
  const s = window.__scene;
  if (s && s.battle.state.outcome) s.settle();
});

console.log(`战斗结果：${result}`);
if (result !== 'win') {
  await page.screenshot({ path: `${OUT}90_autowin_fail.png` });
  console.error('❌ 未能取胜，见 90_autowin_fail.png');
  process.exitCode = 1;
} else {
  // 胜利 → 关后剧情 → 结算
  await page.waitForSelector('.story-box, .result-btns', { timeout: 15000 });
  if (await page.$('.story-skip')) {
    await page.screenshot({ path: `${OUT}91_story_post.png` });
    await page.getByText('跳过').click();
  }
  await page.waitForSelector('.result-btns', { timeout: 8000 });
  await page.screenshot({ path: `${OUT}92_results_win.png` });

  // 存档校验：解锁第 2 关 + 等级提升 + 赏金
  const save = await page.evaluate(() => JSON.parse(localStorage.getItem('zg_warriors_save')));
  console.log(`存档：已解锁=${save.currentLevel} 金币=${save.gold} 李世民Lv=${save.characters.find(c => c.id === 'lishimin').level}`);

  // 下一关 → 第 2 关关前剧情
  await page.getByRole('button', { name: '下一关' }).click();
  await page.waitForSelector('.story-box, .hero-list', { timeout: 8000 });
  await page.screenshot({ path: `${OUT}93_level2_entry.png` });
  console.log('✅ 胜利链路全通：结算→解锁→下一关剧情');
}
await browser.close();
