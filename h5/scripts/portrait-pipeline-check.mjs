// 立绘图片管线验证：放一张占位图 → 选人页应显示占位图（其他角色仍程序绘制）
import { chromium } from 'playwright-core';
import { mkdirSync, writeFileSync, rmSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const BASE = process.env.BASE_URL ?? 'http://localhost:4173';
const OUT = fileURLToPath(new URL('../shots/', import.meta.url));
const PORTRAITS = fileURLToPath(new URL('../public/portraits/', import.meta.url));
mkdirSync(OUT, { recursive: true });
mkdirSync(PORTRAITS, { recursive: true });

const browser = await chromium.launch({ channel: 'chrome', headless: true });
const page = await (await browser.newContext({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 2 })).newPage();

// 1. 造一张明显可辨的占位图（朱红底+金色「测」字）
await page.goto('about:blank');
const dataUrl = await page.evaluate(() => {
  const cv = document.createElement('canvas');
  cv.width = cv.height = 256;
  const c = cv.getContext('2d');
  c.fillStyle = '#B7261E';
  c.fillRect(0, 0, 256, 256);
  c.fillStyle = '#E6BF33';
  c.font = 'bold 120px sans-serif';
  c.textAlign = 'center';
  c.textBaseline = 'middle';
  c.fillText('测', 128, 128);
  return cv.toDataURL('image/png');
});
writeFileSync(`${PORTRAITS}lishimin.png`, Buffer.from(dataUrl.split(',')[1], 'base64'));
// preview 只服务 dist，同步一份过去（正式流程是 build 时由 vite 拷贝）
const DIST_P = fileURLToPath(new URL('../dist/portraits/', import.meta.url));
mkdirSync(DIST_P, { recursive: true });
writeFileSync(`${DIST_P}lishimin.png`, Buffer.from(dataUrl.split(',')[1], 'base64'));
console.log('占位图已写入 public/portraits/ 与 dist/portraits/');

// 2. 进选人页验证
await page.goto(BASE, { waitUntil: 'networkidle' });
await page.evaluate(() => {
  const mk = id => ({ id, level: 5, exp: 0, equipment: {}, isUnlocked: true });
  localStorage.setItem('zg_warriors_save', JSON.stringify({
    version: 1, timestamp: Date.now(), currentLevel: 1, difficulty: 'normal', gold: 0,
    characters: ['lishimin', 'zhangsun_wuji', 'chai_shao', 'liu_hongji', 'li_jing'].map(mk),
    inventory: [], items: {}, levelStates: {}, tutorialDone: true,
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
await page.waitForTimeout(800); // 等图片加载
await page.screenshot({ path: `${OUT}p01_pipeline_heroselect.png` });
console.log('📸 p01_pipeline_heroselect.png（李世民应为朱红占位图，其余为程序立绘）');

// 3. 清理占位图
rmSync(`${PORTRAITS}lishimin.png`);
console.log('占位图已清理');
await browser.close();
