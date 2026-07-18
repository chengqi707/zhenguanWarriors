// 单文件打包：把 dist 的 JS/CSS 全部内联到一个 HTML，双击 file:// 即可玩
// 用法：先 npm run build，再 node scripts/pack-single.mjs
// 产物：h5/dist-single/贞观勇士.html
// 附加：public/portraits/*.png 以 dataURL 形式注入 window.__PORTRAIT_IMGS
import { readFileSync, writeFileSync, mkdirSync, readdirSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const dist = fileURLToPath(new URL('../dist/', import.meta.url));
const portraitsDir = fileURLToPath(new URL('../public/portraits/', import.meta.url));
const outDir = fileURLToPath(new URL('../dist-single/', import.meta.url));
mkdirSync(outDir, { recursive: true });

const assets = readdirSync(`${dist}assets`);
const jsFile = assets.find(f => f.endsWith('.js'));
const cssFile = assets.find(f => f.endsWith('.css'));
if (!jsFile) throw new Error('dist/assets 下找不到 JS，请先 npm run build');

const js = readFileSync(`${dist}assets/${jsFile}`, 'utf8');
const css = cssFile ? readFileSync(`${dist}assets/${cssFile}`, 'utf8') : '';

// 内嵌立绘图片（AI 生成素材；没有该目录就跳过，游戏自动回退程序绘制）
let portraitImgs = '{}';
if (existsSync(portraitsDir)) {
  const entries = readdirSync(portraitsDir)
    .filter(f => f.endsWith('.png') || f.endsWith('.jpg') || f.endsWith('.webp'))
    .map(f => {
      const id = f.replace(/\.(png|jpg|webp)$/, '');
      const mime = f.endsWith('.png') ? 'image/png' : f.endsWith('.webp') ? 'image/webp' : 'image/jpeg';
      const b64 = readFileSync(`${portraitsDir}${f}`).toString('base64');
      return `"${id}":"data:${mime};base64,${b64}"`;
    });
  portraitImgs = `{${entries.join(',')}}`;
  console.log(`内嵌立绘图片 ${entries.length} 张`);
}

// 注意：Vite 产物是单 chunk 无外部 import，可直接以经典 <script> 内联执行
const html = `<!DOCTYPE html>
<html lang="zh-CN">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no, viewport-fit=cover" />
  <meta name="apple-mobile-web-app-capable" content="yes" />
  <meta name="theme-color" content="#1F140D" />
  <link rel="icon" href="data:," />
  <title>贞观勇士 · 李世民战棋录</title>
  <style>${css}</style>
</head>
<body>
  <div id="app">
    <pre id="boot-log" style="white-space:pre-wrap;word-break:break-all;color:#E6BF33;padding:20px;font-size:13px;line-height:1.6;font-family:monospace;"></pre>
    <noscript>
      <div style="padding:40px 20px;text-align:center;color:#F5EBD1;font-family:sans-serif;">
        <div style="font-size:22px;color:#E6BF33;margin-bottom:12px;">贞观勇士</div>
        <div>请开启 JavaScript 后刷新页面。</div>
      </div>
    </noscript>
  </div>
  <script>window.__PORTRAIT_IMGS=${portraitImgs};</script>
  <script>${js.replace(/<\/script>/gi, '<\\/script>')}</script>
</body>
</html>
`;

const out = `${outDir}贞观勇士.html`;
writeFileSync(out, html);
console.log(`✅ 单文件已生成：${out}（${(html.length / 1024).toFixed(0)}KB）`);
