// 战斗画布布局探针：打印 battle 容器内各元素的几何与样式
import { chromium } from 'playwright-core';

const browser = await chromium.launch({ channel: 'chrome', headless: true });
const ctx = await browser.newContext({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 2 });
const page = await ctx.newPage();
page.on('pageerror', e => console.error('[页面错误]', e.message));
page.on('console', m => { if (m.type() === 'error') console.error('[console]', m.text()); });

await page.goto('http://localhost:4173', { waitUntil: 'networkidle' });
await page.waitForSelector('.menu-title', { timeout: 8000 });
// 预置存档：解锁第1关+4名角色，跳过选人（直接注入 localStorage）
await page.evaluate(() => {
  localStorage.setItem('zg_warriors_save', JSON.stringify({
    version: 1, timestamp: Date.now(), currentLevel: 1, difficulty: 'normal', gold: 0,
    characters: [
      { id: 'lishimin', level: 1, exp: 0, equipment: {}, isUnlocked: true },
      { id: 'zhangsun_wuji', level: 1, exp: 0, equipment: {}, isUnlocked: true },
      { id: 'chai_shao', level: 1, exp: 0, equipment: {}, isUnlocked: true },
      { id: 'liu_hongji', level: 1, exp: 0, equipment: {}, isUnlocked: true },
    ],
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
await page.waitForTimeout(1500);

const info = await page.evaluate(() => {
  const out = [];
  const s = window.__scene;
  const canvas = s.canvas;
  const r = canvas.getBoundingClientRect();
  out.push(`canvas attr: ${canvas.width}x${canvas.height}, rect: ${r.width.toFixed(0)}x${r.height.toFixed(0)} @(${r.left.toFixed(0)},${r.top.toFixed(0)})`);
  out.push(`canvas style: ${canvas.style.cssText}`);
  const cs = getComputedStyle(canvas);
  out.push(`canvas computed: display=${cs.display} w=${cs.width} h=${cs.height} flex=${cs.flex}`);
  let el = canvas.parentElement;
  while (el && out.length < 14) {
    const er = el.getBoundingClientRect();
    const es = getComputedStyle(el);
    out.push(`${el.tagName}.${el.className}: ${er.width.toFixed(0)}x${er.height.toFixed(0)} display=${es.display} flex=${es.flex} overflow=${es.overflow}`);
    el = el.parentElement;
  }
  // 画布中心像素采样，确认是否真的没画
  const ctx2d = canvas.getContext('2d');
  const d = ctx2d.getImageData(0, 0, canvas.width, canvas.height).data;
  let nonBg = 0;
  for (let i = 0; i < d.length; i += 400) {
    if (d[i] !== 0 || d[i + 1] !== 0 || d[i + 2] !== 0) nonBg++;
  }
  out.push(`canvas non-black samples: ${nonBg}`);
  out.push(`camera: x=${s.camera.x.toFixed(0)} y=${s.camera.y.toFixed(0)} scale=${s.camera.scale.toFixed(2)} view=${s.camera.viewW}x${s.camera.viewH} map=${s.camera.mapW}x${s.camera.mapH}`);
  return out;
});
console.log(info.join('\n'));
await browser.close();
