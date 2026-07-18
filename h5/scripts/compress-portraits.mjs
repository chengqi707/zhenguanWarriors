// 立绘压缩：public/portraits/*.png (512px ~360KB) → 256×256 JPEG q0.85 (~30-50KB)
// 显示尺寸仅 40-80px，256px 足够。原 PNG 删除。
import { chromium } from 'playwright-core';
import { readdirSync, readFileSync, writeFileSync, rmSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const DIR = fileURLToPath(new URL('../public/portraits/', import.meta.url));
const pngs = readdirSync(DIR).filter(f => f.endsWith('.png'));
if (pngs.length === 0) { console.log('无 PNG 可压缩'); process.exit(0); }

const browser = await chromium.launch({ channel: 'chrome', headless: true });
const page = await (await browser.newContext()).newPage();
await page.goto('about:blank');

for (const f of pngs) {
  const b64 = readFileSync(`${DIR}${f}`).toString('base64');
  const jpeg = await page.evaluate(async (data) => {
    const img = new Image();
    await new Promise((res, rej) => { img.onload = res; img.onerror = rej; img.src = `data:image/png;base64,${data}`; });
    const cv = document.createElement('canvas');
    cv.width = cv.height = 256;
    const c = cv.getContext('2d');
    c.fillStyle = '#1F140D'; // 底色填充（JPG 无透明）
    c.fillRect(0, 0, 256, 256);
    const s = Math.max(256 / img.width, 256 / img.height);
    const w = img.width * s, h = img.height * s;
    c.drawImage(img, (256 - w) / 2, (256 - h) / 2, w, h);
    return cv.toDataURL('image/jpeg', 0.85).split(',')[1];
  }, b64);
  const out = f.replace('.png', '.jpg');
  writeFileSync(`${DIR}${out}`, Buffer.from(jpeg, 'base64'));
  rmSync(`${DIR}${f}`);
  console.log(`${f} → ${out}（${Math.round(jpeg.length * 0.75 / 1024)}KB）`);
}
console.log(`✅ 压缩完成，共 ${pngs.length} 张`);
await browser.close();
