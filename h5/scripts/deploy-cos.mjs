// ============================================================
// 腾讯云 COS 静态网站部署脚本
// 用法：
//   1. 复制 h5/.env.example 为 h5/.env.local，填入 SecretId / SecretKey / Bucket / Region
//   2. 确保 h5/.env.local 已在 .gitignore 中（默认已加）
//   3. 运行：npm run build && npm run pack && npm run deploy:cos
//
// 也支持临时通过环境变量传入（会覆盖 .env.local）：
//   COS_SECRET_ID=xxx COS_SECRET_KEY=xxx COS_BUCKET=xxx COS_REGION=xxx npm run deploy:cos
//
// 默认上传 `h5/dist/` 作为静态网站根目录，并把单文件版 `h5/dist-single/贞观勇士.html`
// 上传到根目录，方便直接转发单个链接。
// ============================================================
import COS from 'cos-nodejs-sdk-v5';
import dotenv from 'dotenv';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '..');

// 从本地 .env.local 加载密钥；环境变量优先级更高
for (const envFile of ['.env.local', '.env']) {
  const p = path.resolve(ROOT, envFile);
  if (fs.existsSync(p)) dotenv.config({ path: p });
}

const SECRET_ID = process.env.COS_SECRET_ID;
const SECRET_KEY = process.env.COS_SECRET_KEY;
const BUCKET = process.env.COS_BUCKET;
const REGION = process.env.COS_REGION;
const PREFIX = process.env.COS_PATH_PREFIX || ''; // 例如 'h5/'，留空则放根目录

function required(name, value) {
  if (!value) {
    console.error(`❌ 缺少环境变量 ${name}，请参照脚本顶部说明设置`);
    process.exit(1);
  }
}

required('COS_SECRET_ID', SECRET_ID);
required('COS_SECRET_KEY', SECRET_KEY);
required('COS_BUCKET', BUCKET);
required('COS_REGION', REGION);

const cos = new COS({
  SecretId: SECRET_ID,
  SecretKey: SECRET_KEY,
});

/** 遍历目录，返回 { 相对路径: 绝对路径 } */
function walk(dir, prefix = '') {
  const map = {};
  const entries = fs.readdirSync(dir, { withFileTypes: true });
  for (const entry of entries) {
    const abs = path.join(dir, entry.name);
    const rel = prefix ? `${prefix}/${entry.name}` : entry.name;
    if (entry.isDirectory()) {
      Object.assign(map, walk(abs, rel));
    } else {
      map[rel.replace(/\\/g, '/')] = abs;
    }
  }
  return map;
}

/** 根据扩展名推断 Content-Type */
function mime(filePath) {
  const ext = path.extname(filePath).toLowerCase();
  const table = {
    '.html': 'text/html; charset=utf-8',
    '.js': 'application/javascript; charset=utf-8',
    '.css': 'text/css; charset=utf-8',
    '.json': 'application/json',
    '.png': 'image/png',
    '.jpg': 'image/jpeg',
    '.jpeg': 'image/jpeg',
    '.webp': 'image/webp',
    '.svg': 'image/svg+xml',
    '.ico': 'image/x-icon',
    '.woff2': 'font/woff2',
    '.woff': 'font/woff',
    '.ttf': 'font/ttf',
  };
  return table[ext] || 'application/octet-stream';
}

function uploadFile(localPath, cosKey, opts = {}) {
  return new Promise((resolve, reject) => {
    const params = {
      Bucket: BUCKET,
      Region: REGION,
      Key: cosKey,
      Body: fs.createReadStream(localPath),
      ContentType: mime(localPath),
      // html/js/css 建议开启浏览器缓存；index.html 可短缓存便于更新
      CacheControl: opts.cacheControl ?? (localPath.endsWith('index.html') ? 'max-age=60' : 'max-age=86400'),
    };
    // 默认 inline；显式传 undefined 表示不设置 Content-Disposition
    if (opts.contentDisposition !== undefined) {
      params.ContentDisposition = opts.contentDisposition;
    }
    cos.putObject(params, (err, data) => {
      if (err) reject(err);
      else resolve(data);
    });
  });
}

async function main() {
  const distDir = path.join(ROOT, 'dist');
  const singleFile = path.join(ROOT, 'dist-single', '贞观勇士.html');

  if (!fs.existsSync(distDir)) {
    console.error('❌ 未找到 h5/dist/ 目录，请先运行 npm run build');
    process.exit(1);
  }
  if (!fs.existsSync(singleFile)) {
    console.error('❌ 未找到 h5/dist-single/贞观勇士.html，请先运行 npm run build && npm run pack');
    process.exit(1);
  }

  const files = walk(distDir);
  const playFile = path.join(ROOT, 'play.html');
  const hasPlayFile = fs.existsSync(playFile);
  const total = Object.keys(files).length + 2 + (hasPlayFile ? 1 : 0); // dist/ + 中文单文件 + ASCII 单文件 + play.html
  let done = 0;

  console.log(`\n🚀 开始部署到 COS：${BUCKET} / ${REGION}`);
  console.log(`   目标前缀：${PREFIX || '(根目录)'}`);
  console.log(`   共 ${total} 个对象\n`);

  for (const [rel, abs] of Object.entries(files)) {
    const key = `${PREFIX}${rel}`;
    await uploadFile(abs, key);
    done += 1;
    console.log(`[${done}/${total}] ${key}`);
  }

  // 同时上传单文件版，便于直接分享一个 HTML 链接
  // 额外上传一个 ASCII 文件名 game.html，避免部分浏览器/聊天软件对中文 URL 处理异常
  const singleKey = `${PREFIX}贞观勇士.html`;
  const asciiSingleKey = `${PREFIX}game.html`;
  await uploadFile(singleFile, singleKey);
  done += 1;
  console.log(`[${done}/${total}] ${singleKey}`);
  // 对单文件版不设 Content-Disposition，避免 Chrome 把 inline + filename 识别为下载
  await uploadFile(singleFile, asciiSingleKey, { contentDisposition: undefined });
  done += 1;
  console.log(`[${done}/${total}] ${asciiSingleKey}`);

  // 备用入口：轻量落地页，绝对不会再触发下载
  if (hasPlayFile) {
    const playKey = `${PREFIX}play.html`;
    await uploadFile(playFile, playKey);
    done += 1;
    console.log(`[${done}/${total}] ${playKey}`);
  }

  // 配置跨域：Vite 生成的 index.html 对 JS/CSS 带 crossorigin 属性，
  // 静态网站入口需要 CORS 头才能正常加载资源
  console.log('\n🌐 配置存储桶 CORS...');
  await new Promise((resolve, reject) => {
    cos.putBucketCors(
      {
        Bucket: BUCKET,
        Region: REGION,
        CORSRules: [
          {
            AllowedOrigin: ['*'],
            AllowedMethod: ['GET', 'HEAD'],
            AllowedHeader: ['*'],
            MaxAgeSeconds: '600',
            ExposeHeader: ['ETag', 'x-cos-request-id'],
          },
        ],
      },
      (err, data) => {
        if (err) reject(err);
        else resolve(data);
      },
    );
  });

  const websiteEndpoint = `https://${BUCKET}.cos-website.${REGION}.myqcloud.com/${PREFIX}`;
  const singleUrl = `https://${BUCKET}.cos-website.${REGION}.myqcloud.com/${singleKey}`;
  const asciiSingleUrl = `https://${BUCKET}.cos-website.${REGION}.myqcloud.com/${asciiSingleKey}`;
  const playUrl = `https://${BUCKET}.cos-website.${REGION}.myqcloud.com/${PREFIX}play.html`;

  console.log('\n✅ 部署完成');
  console.log(`   静态网站入口：${websiteEndpoint}`);
  console.log(`   单文件版链接：${singleUrl}`);
  console.log(`   单文件版（ASCII 文件名）：${asciiSingleUrl}`);
  if (hasPlayFile) console.log(`   落地页入口（防下载）：${playUrl}`);
  console.log('\n提示：若已开启自定义域名，请把上述链接里的 cos-website 域名替换为你的域名。');
}

main().catch((err) => {
  console.error('\n❌ 部署失败：', err.message || err);
  process.exit(1);
});
