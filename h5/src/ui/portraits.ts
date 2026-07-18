// ============================================================
// 程序化角色立绘——无外部美术资源，Canvas 2D 绘制写实比例半身像。
// 写实化第三轮（PRD §16.6，向真三国无双写实风尽力推进）：
// 比例：头:躯干≈1:1.3，脸形拉长、下颌线明确（方脸/瓜子脸按人设）；
// 光影：面部左侧主光+右侧环境反光的双向光照，盔甲与盔体右侧
//     兵种色/金色轮廓光（rim light），背景压暗+四角晕影突出人物；
// 细节：明光铠式双胸护+甲片行列、披膊分层、头盔金属拉丝与铆钉高光、
//     女性/束发角色发丝分组、写实杏仁眼（上下睑线+虹膜+双高光）；
// 神态：李世民剑眉英挺、李靖平眉睑垂沉稳、尉迟怒眉威猛、程咬金环眼
//     大笑、长孙皇后温婉、平阳公主英气；
// 兵种：骑兵凤翅盔/重甲铁兜鍪护颊/步兵皮帻颜题/弓兵束发带/器械工匠帽/
//     谋士文士冠金簪，主公束发金冠，女性高髻；秦琼双锏、刘弘基箭囊。
// 肤色/盔色/眼位按角色 id 哈希微调，避免千人一面。
// 仅在浏览器按需生成并缓存（key = `${charId}_${size}`）；
// 模块顶层不执行任何绘制，node 下导入无副作用。
// ============================================================
import { getCharacter } from '../data';
import type { ClassType, Gender, Role } from '../core/types';
import { CLASS_COLORS } from './common';

type Ctx = CanvasRenderingContext2D;
type Style = string | CanvasGradient;
type Beard = 'mustache' | 'goatee' | 'full';

/** 男性角色须型：尉迟/程咬金虬髯，谋臣与李靖山羊须，其余武将短髭 */
const BEARDS: Record<string, Beard> = {
  lishimin: 'mustache',
  qin_qiong: 'mustache',
  chai_shao: 'mustache',
  hou_junji: 'mustache',
  duan_zhixuan: 'mustache',
  liu_hongji: 'mustache',
  yin_kaishan: 'mustache',
  zhangsun_wuji: 'goatee',
  fang_xuanling: 'goatee',
  du_ruhui: 'goatee',
  li_jing: 'goatee',
  yuchi_jingde: 'full',   // 虬髯
  cheng_yaojin: 'full',   // 虬髯
};

/** 人设微调（PRD §4.1 气质）：肤色/脸型/眼型/表情/须色 */
interface Persona {
  skin?: string;        // 肤色基准（默认 #EFC9A2）
  faceW?: number;       // 脸宽系数（默认 1，文士清瘦收 0.93）
  roundEyes?: boolean;  // 环眼（程咬金）
  smile?: boolean;      // 张口笑（程咬金）
  glare?: boolean;      // 怒目倒竖眉（尉迟敬德）
  calm?: boolean;       // 沉稳：平眉睑垂（李靖）
  squareJaw?: boolean;  // 方下颌（尉迟/程咬金）
  beard?: string;       // 须色（文士山羊须深浅有别）
}

const PERSONA: Record<string, Persona> = {
  yuchi_jingde:  { skin: '#A06B3F', glare: true, squareJaw: true },   // 黑面虬髯
  cheng_yaojin:  { faceW: 1.13, roundEyes: true, smile: true, squareJaw: true }, // 阔脸环眼笑
  zhangsun_wuji: { faceW: 0.93, beard: '#241A12' },              // 清瘦，须浓黑
  fang_xuanling: { faceW: 0.93, beard: '#6B5A48' },              // 清瘦，须花白
  du_ruhui:      { faceW: 0.93, beard: '#3D2C1E' },              // 清瘦，须深棕
  li_jing:       { calm: true },                                 // 沉稳
};

/** 面部中线与五官纵向锚点（100 设计空间） */
const FX = 50;
const FY = { brow: 38.9, eye: 42.8, mouth: 52.6, chin: 57 } as const;

// ---------- 外部立绘图片层（AI 生成素材，优先于程序绘制） ----------
// 放置：h5/public/portraits/{charId}.png → 构建后随 dist 发布；
// 单文件版由 pack-single.mjs 注入 window.__PORTRAIT_IMGS（charId → dataURL）。
// 无图片的角色自动回退到程序绘制。
declare global {
  interface Window { __PORTRAIT_IMGS?: Record<string, string> }
}
const imgCache = new Map<string, HTMLImageElement>(); // 已加载图片
const imgPending = new Set<string>();                 // 加载中
const imgFailed = new Set<string>();                  // 无图（404 等），不再重试

/** 尝试加载某角色的外部立绘（幂等，fire-and-forget；按 jpg→png→webp 顺序探测） */
function tryLoadImage(charId: string): void {
  if (imgCache.has(charId) || imgPending.has(charId) || imgFailed.has(charId)) return;
  const inline = typeof window !== 'undefined' ? window.__PORTRAIT_IMGS?.[charId] : undefined;
  imgPending.add(charId);
  const tryExt = (exts: string[]): void => {
    if (exts.length === 0) {
      imgFailed.add(charId);
      imgPending.delete(charId);
      return;
    }
    const img = new Image();
    img.onload = () => { imgCache.set(charId, img); imgPending.delete(charId); };
    img.onerror = () => tryExt(exts.slice(1));
    img.src = inline ?? `portraits/${charId}.${exts[0]}`;
  };
  tryExt(['jpg', 'png', 'webp']);
}

/** 预加载全部角色外部立绘（main.ts 启动时调用） */
export function initPortraitImages(ids: string[]): void {
  ids.forEach(tryLoadImage);
}

/** 某角色的外部立绘图片是否已加载完成（用于战场优先绘制真实头像） */
export function portraitImageReady(charId: string): boolean {
  tryLoadImage(charId);
  return imgCache.has(charId);
}

/** 等待某角色外部立绘加载完成（失败也 resolve，保证不阻塞启动） */
export function loadPortraitImage(charId: string): Promise<HTMLImageElement | null> {
  tryLoadImage(charId);
  if (imgCache.has(charId)) return Promise.resolve(imgCache.get(charId)!);
  if (imgFailed.has(charId)) return Promise.resolve(null);
  return new Promise(resolve => {
    const check = () => {
      if (imgCache.has(charId)) {
        resolve(imgCache.get(charId)!);
        return true;
      }
      if (imgFailed.has(charId)) {
        resolve(null);
        return true;
      }
      return false;
    };
    if (check()) return;
    const interval = window.setInterval(() => {
      if (check()) window.clearInterval(interval);
    }, 50);
    // 超时 5s 放弃
    window.setTimeout(() => {
      window.clearInterval(interval);
      resolve(null);
    }, 5000);
  });
}

/** 已有外部图片的角色（图片绘制到 canvas 并缓存） */
function renderImage(size: number, img: HTMLImageElement): HTMLCanvasElement {
  const cv = document.createElement('canvas');
  cv.width = size * 2;
  cv.height = size * 2;
  cv.style.width = `${size}px`;
  cv.style.height = `${size}px`;
  const c = cv.getContext('2d')!;
  // cover 裁切：填满正方形，居中
  const s = Math.max(cv.width / img.width, cv.height / img.height);
  const w = img.width * s, h = img.height * s;
  c.drawImage(img, (cv.width - w) / 2, (cv.height - h) / 2, w, h);
  return cv;
}

const canvasCache = new Map<string, HTMLCanvasElement>();
const urlCache = new Map<string, string>();

/** 获取角色立绘（缓存）；size 为输出边长（CSS px，内部按 2x 绘制） */
export function getPortrait(charId: string, size: number): HTMLCanvasElement {
  tryLoadImage(charId);
  const img = imgCache.get(charId);
  const key = `${charId}_${size}${img ? '_img' : ''}`;
  let cv = canvasCache.get(key);
  if (!cv) {
    cv = img ? renderImage(size, img) : render(charId, size);
    canvasCache.set(key, cv);
  }
  return cv;
}

/** 获取角色立绘 dataURL（给 <img> 用） */
export function getPortraitURL(charId: string, size: number): string {
  tryLoadImage(charId);
  const img = imgCache.get(charId);
  const key = `${charId}_${size}${img ? '_img' : ''}`;
  let url = urlCache.get(key);
  if (!url) {
    url = img ? renderImage(size, img).toDataURL('image/png') : getPortrait(charId, size).toDataURL('image/png');
    urlCache.set(key, url);
  }
  return url;
}

// ---------- 工具 ----------

/** 字符串哈希 → 0..1 稳定伪随机值（按角色微调肤色/盔色/眼位） */
function hash01(id: string, salt: number): number {
  let h = salt >>> 0;
  for (let i = 0; i < id.length; i++) h = (h * 31 + id.charCodeAt(i)) >>> 0;
  return (h % 1000) / 1000;
}

/** #RRGGBB 明度偏移，amt ∈ [-1, 1] */
function shade(hex: string, amt: number): string {
  const n = parseInt(hex.slice(1), 16);
  const f = (v: number) => Math.max(0, Math.min(255, Math.round(v + amt * 255)));
  return `rgb(${f((n >> 16) & 255)},${f((n >> 8) & 255)},${f(n & 255)})`;
}

/** 线性渐变快捷构造 */
function lin(ctx: Ctx, x0: number, y0: number, x1: number, y1: number,
  stops: [number, string][]): CanvasGradient {
  const g = ctx.createLinearGradient(x0, y0, x1, y1);
  for (const [o, c] of stops) g.addColorStop(o, c);
  return g;
}

/** 径向渐变快捷构造（同心圆） */
function rad(ctx: Ctx, x: number, y: number, r0: number, r1: number,
  stops: [number, string][]): CanvasGradient {
  const g = ctx.createRadialGradient(x, y, r0, x, y, r1);
  for (const [o, c] of stops) g.addColorStop(o, c);
  return g;
}

function pathRoundRect(
  ctx: Ctx, x: number, y: number, w: number, h: number, r: number,
): void {
  ctx.beginPath();
  ctx.moveTo(x + r, y);
  ctx.arcTo(x + w, y, x + w, y + h, r);
  ctx.arcTo(x + w, y + h, x, y + h, r);
  ctx.arcTo(x, y + h, x, y, r);
  ctx.arcTo(x, y, x + w, y, r);
  ctx.closePath();
}

function fillRoundRect(
  ctx: Ctx, x: number, y: number, w: number, h: number, r: number, style: Style,
): void {
  ctx.fillStyle = style;
  pathRoundRect(ctx, x, y, w, h, r);
  ctx.fill();
}

function fillCircle(ctx: Ctx, x: number, y: number, r: number, style: Style): void {
  ctx.fillStyle = style;
  ctx.beginPath();
  ctx.arc(x, y, r, 0, Math.PI * 2);
  ctx.fill();
}

function fillEllipse(
  ctx: Ctx, x: number, y: number, rx: number, ry: number, style: Style,
): void {
  ctx.fillStyle = style;
  ctx.beginPath();
  ctx.ellipse(x, y, rx, ry, 0, 0, Math.PI * 2);
  ctx.fill();
}

/** 铆钉：金属圆点 + 高光 */
function rivet(ctx: Ctx, x: number, y: number, color: string): void {
  fillCircle(ctx, x, y, 1.05, color);
  fillCircle(ctx, x - 0.32, y - 0.32, 0.4, 'rgba(255,255,255,0.8)');
}

// ---------- 主绘制 ----------

/** 绘制单个立绘：100×100 设计空间，输出 size×size（内部 2x 像素） */
function render(charId: string, size: number): HTMLCanvasElement {
  const def = getCharacter(charId);
  const cls: ClassType = def?.classType ?? 'infantry';
  const role: Role = def?.role ?? 'warrior';
  const gender: Gender = def?.gender ?? 'male';
  const base = CLASS_COLORS[cls];
  const persona = PERSONA[charId] ?? {};

  const cv = document.createElement('canvas');
  cv.width = size * 2;
  cv.height = size * 2;
  cv.style.width = `${size}px`;
  cv.style.height = `${size}px`;
  const ctx = cv.getContext('2d')!;
  ctx.scale(cv.width / 100, cv.height / 100);
  ctx.lineCap = 'round';
  ctx.lineJoin = 'round';

  // ---- 兵种色渐变底（整体压暗）----
  const bg = ctx.createLinearGradient(0, 0, 0, 100);
  bg.addColorStop(0, shade(base, 0.08));
  bg.addColorStop(0.5, shade(base, -0.20));
  bg.addColorStop(1, shade(base, -0.54));
  ctx.fillStyle = bg;
  pathRoundRect(ctx, 1, 1, 98, 98, 15);
  ctx.fill();

  ctx.save();
  pathRoundRect(ctx, 1, 1, 98, 98, 15);
  ctx.clip();

  // 左上主光晕（与面部主光同向）
  ctx.fillStyle = rad(ctx, 28, 16, 4, 62, [
    [0, 'rgba(255,240,214,0.15)'], [1, 'rgba(255,240,214,0)'],
  ]);
  ctx.fillRect(0, 0, 100, 100);

  // 暗纹：近战兵种甲片纹，弓/谋云纹
  if (cls === 'archer' || cls === 'strategist') drawCloudPattern(ctx);
  else drawScalePattern(ctx);

  // 四角晕影 + 底部压暗（突出人物）
  ctx.fillStyle = rad(ctx, 50, 46, 24, 80, [
    [0, 'rgba(0,0,0,0)'], [0.7, 'rgba(0,0,0,0.06)'], [1, 'rgba(0,0,0,0.40)'],
  ]);
  ctx.fillRect(0, 0, 100, 100);
  ctx.fillStyle = lin(ctx, 0, 62, 0, 100, [
    [0, 'rgba(0,0,0,0)'], [1, 'rgba(0,0,0,0.20)'],
  ]);
  ctx.fillRect(0, 0, 100, 100);

  drawBackProps(ctx, charId);
  drawTorso(ctx, cls, role, gender, base, charId);
  drawNeckHead(ctx, charId, gender, persona);
  drawFace(ctx, charId, role, gender, persona);
  if (gender === 'female') drawFemaleHair(ctx, charId);
  else drawHeadgear(ctx, cls, base, charId, role);

  ctx.restore();

  // ---- 边框：外暗内金 ----
  pathRoundRect(ctx, 2, 2, 96, 96, 14);
  ctx.strokeStyle = 'rgba(20,12,6,0.55)';
  ctx.lineWidth = 2.4;
  ctx.stroke();
  pathRoundRect(ctx, 4.2, 4.2, 91.6, 91.6, 12);
  ctx.strokeStyle = 'rgba(230,191,51,0.35)';
  ctx.lineWidth = 1.2;
  ctx.stroke();

  return cv;
}

/** 甲片纹：交错半圆鳞甲（近战兵种底纹） */
function drawScalePattern(ctx: Ctx): void {
  ctx.strokeStyle = 'rgba(0,0,0,0.05)';
  ctx.lineWidth = 1.1;
  for (let row = 0; row < 6; row++) {
    const y = 9 + row * 17;
    const off = row % 2 ? 6 : 0;
    for (let x = -6 + off; x < 106; x += 12) {
      ctx.beginPath();
      ctx.arc(x, y, 6, Math.PI * 0.08, Math.PI * 0.92);
      ctx.stroke();
    }
  }
}

/** 云纹：简化勾卷（弓/谋兵种底纹） */
function drawCloudPattern(ctx: Ctx): void {
  ctx.strokeStyle = 'rgba(0,0,0,0.045)';
  ctx.lineWidth = 1.1;
  const motifs: [number, number][] = [[16, 20], [78, 16], [12, 62], [84, 58], [46, 92]];
  for (const [x, y] of motifs) {
    ctx.beginPath();
    ctx.arc(x, y, 5.5, Math.PI * 0.15, Math.PI * 1.5);
    ctx.stroke();
    ctx.beginPath();
    ctx.arc(x + 5.5, y - 1.5, 3.2, Math.PI * 0.6, Math.PI * 2.1);
    ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(x - 8, y + 5);
    ctx.quadraticCurveTo(x, y + 8.5, x + 10, y + 5);
    ctx.stroke();
  }
}

// ---------- 躯干 ----------

/** 躯干轮廓：颈根 → 斜方肌 → 三角肌 → 画面底边；女性肩窄 */
function torsoPath(ctx: Ctx, female: boolean): void {
  const s = female ? 4 : 0;
  ctx.beginPath();
  ctx.moveTo(7 + s, 100);
  ctx.bezierCurveTo(8 + s, 79, 22, 69, 34, 64.5);
  ctx.quadraticCurveTo(40, 62.4, 45, 62.4);
  ctx.lineTo(55, 62.4);
  ctx.quadraticCurveTo(60, 62.4, 66, 64.5);
  ctx.bezierCurveTo(78, 69, 92 - s, 79, 93 - s, 100);
  ctx.closePath();
}

/** 躯干右侧轮廓（rim light 路径） */
function rightEdgePath(ctx: Ctx, female: boolean): void {
  const s = female ? 4 : 0;
  ctx.beginPath();
  ctx.moveTo(66, 64.5);
  ctx.bezierCurveTo(78, 69, 92 - s, 79, 93 - s, 99);
}

/** 躯干：兵种色渐变铠甲/袍服 + 双向光照 + 右侧轮廓光 */
function drawTorso(
  ctx: Ctx, cls: ClassType, role: Role, gender: Gender, base: string, charId: string,
): void {
  const robe = cls === 'strategist';
  const female = gender === 'female';
  torsoPath(ctx, female);
  ctx.fillStyle = lin(ctx, 0, 60, 0, 100, [
    [0, shade(base, robe ? -0.12 : 0.14)],
    [0.45, shade(base, robe ? -0.26 : -0.08)],
    [1, shade(base, -0.52)],
  ]);
  ctx.fill();

  ctx.save();
  torsoPath(ctx, female);
  ctx.clip();
  // 左主光 → 右侧暗影
  ctx.fillStyle = lin(ctx, 8, 0, 92, 0, [
    [0, 'rgba(255,236,206,0.16)'], [0.42, 'rgba(255,236,206,0)'], [1, 'rgba(0,0,0,0.30)'],
  ]);
  ctx.fillRect(0, 58, 100, 44);
  // 左肩线受光
  ctx.strokeStyle = 'rgba(255,244,220,0.28)';
  ctx.lineWidth = 1.4;
  ctx.beginPath();
  ctx.moveTo(13, 83);
  ctx.bezierCurveTo(17, 73, 28, 66, 42, 62.8);
  ctx.stroke();

  if (robe) drawRobeDetails(ctx, charId);
  else drawArmorDetails(ctx, cls, role, female, base, charId);
  ctx.restore();

  // 右侧金色轮廓光（外晕 + 内芯两笔）
  ctx.strokeStyle = 'rgba(255,208,140,0.22)';
  ctx.lineWidth = 2.2;
  rightEdgePath(ctx, female);
  ctx.stroke();
  ctx.strokeStyle = 'rgba(255,228,175,0.55)';
  ctx.lineWidth = 1.0;
  rightEdgePath(ctx, female);
  ctx.stroke();
}

/** 甲片行列：交错半圆鳞甲，自上而下渐暗（腹甲/步兵全身甲） */
function lamellarRows(ctx: Ctx, base: string, yTop: number, yBot: number): void {
  let row = 0;
  for (let y = yTop; y < yBot - 0.5; y += 4.7, row++) {
    ctx.fillStyle = shade(base, -0.10 - row * 0.07);
    ctx.fillRect(6, y, 88, 4.1);
    ctx.strokeStyle = 'rgba(0,0,0,0.28)';
    ctx.lineWidth = 0.8;
    const off = row % 2 ? 2.7 : 0;
    for (let x = 11 + off; x < 91; x += 5.4) {
      ctx.beginPath();
      ctx.arc(x, y + 4.1, 2.7, Math.PI * 1.02, Math.PI * 1.98);
      ctx.stroke();
    }
    ctx.strokeStyle = 'rgba(255,255,255,0.13)';
    ctx.lineWidth = 0.7;
    ctx.beginPath();
    ctx.moveTo(8, y + 0.7);
    ctx.lineTo(92, y + 0.7);
    ctx.stroke();
  }
}

/** 披膊（肩甲）：半圆甲包 + 分层弧 + 铆钉；右侧加轮廓光 */
function drawPauldron(ctx: Ctx, px: number, base: string, layers: number, isRight: boolean): void {
  fillCircle(ctx, px, 76, 11.5, lin(ctx, px, 64, px, 88, [
    [0, shade(base, 0.20)], [1, shade(base, -0.40)],
  ]));
  ctx.strokeStyle = shade(base, -0.34);
  ctx.lineWidth = 1.0;
  for (let i = 0; i < layers; i++) {
    ctx.beginPath();
    ctx.arc(px, 71.5, 4.5 + i * 3.3, Math.PI * 0.12, Math.PI * 0.88);
    ctx.stroke();
  }
  // 顶缘受光 + 底缘铆钉
  ctx.strokeStyle = 'rgba(255,255,255,0.28)';
  ctx.lineWidth = 1.0;
  ctx.beginPath();
  ctx.arc(px, 74.5, 9.6, Math.PI * 1.15, Math.PI * 1.85);
  ctx.stroke();
  for (const dx of [-5.5, 0, 5.5]) rivet(ctx, px + dx, 84.2, shade(base, 0.16));
  if (isRight) {
    ctx.strokeStyle = 'rgba(255,222,160,0.45)';
    ctx.lineWidth = 1.0;
    ctx.beginPath();
    ctx.arc(px, 76, 11.2, Math.PI * 1.62, Math.PI * 2.28);
    ctx.stroke();
  }
}

/** 明光铠式双胸护：径向渐变镜体 + 镜缘 + 反光 + 缘铆 */
function drawChestMirrors(ctx: Ctx, cls: ClassType, base: string): void {
  const r = cls === 'heavy' ? 6.9 : 6.1;
  // 中缝
  ctx.strokeStyle = 'rgba(0,0,0,0.25)';
  ctx.lineWidth = 1.0;
  ctx.beginPath();
  ctx.moveTo(FX, 70);
  ctx.lineTo(FX, 85.5);
  ctx.stroke();
  for (const mx of [37.5, 62.5]) {
    fillCircle(ctx, mx, 77.5, r, rad(ctx, mx - 1.6, 75.2, 1, r + 1.4, [
      [0, shade(base, 0.44)], [0.55, shade(base, 0.10)], [1, shade(base, -0.36)],
    ]));
    ctx.strokeStyle = shade(base, -0.46);
    ctx.lineWidth = 1.2;
    ctx.beginPath();
    ctx.arc(mx, 77.5, r, 0, Math.PI * 2);
    ctx.stroke();
    ctx.strokeStyle = 'rgba(255,255,255,0.55)';
    ctx.lineWidth = 1.0;
    ctx.beginPath();
    ctx.arc(mx, 77.5, r - 2.2, Math.PI * 1.05, Math.PI * 1.5);
    ctx.stroke();
    for (const a of [0.3, 1.0, 1.9, 2.6]) {
      rivet(ctx, mx + Math.cos(a * Math.PI) * (r + 0.4), 77.5 + Math.sin(a * Math.PI) * (r + 0.4), shade(base, 0.2));
    }
  }
  if (cls === 'cavalry') {
    // 绊甲绦：领口下红绳交结
    ctx.strokeStyle = '#B7261E';
    ctx.lineWidth = 1.1;
    ctx.beginPath();
    ctx.moveTo(43, 69.5);
    ctx.lineTo(37.5, 73.5);
    ctx.moveTo(57, 69.5);
    ctx.lineTo(62.5, 73.5);
    ctx.stroke();
    fillCircle(ctx, FX, 72.8, 1.3, '#8A1A14');
  }
}

/** 领口：内衬衣 + 护颈（主公/女将金缘） */
function drawCollar(ctx: Ctx, base: string, role: Role, female: boolean): void {
  ctx.fillStyle = '#E8D9B8';
  ctx.beginPath();
  ctx.moveTo(42.5, 63.4);
  ctx.quadraticCurveTo(FX, 68.4, 57.5, 63.4);
  ctx.lineTo(57.5, 66.8);
  ctx.quadraticCurveTo(FX, 72, 42.5, 66.8);
  ctx.closePath();
  ctx.fill();
  ctx.fillStyle = lin(ctx, 0, 65, 0, 78, [
    [0, shade(base, -0.28)], [1, shade(base, -0.46)],
  ]);
  ctx.beginPath();
  ctx.moveTo(37, 66.2);
  ctx.quadraticCurveTo(FX, 73.4, 63, 66.2);
  ctx.lineTo(63, 70.6);
  ctx.quadraticCurveTo(FX, 77.6, 37, 70.6);
  ctx.closePath();
  ctx.fill();
  ctx.strokeStyle = 'rgba(0,0,0,0.35)';
  ctx.lineWidth = 0.8;
  ctx.beginPath();
  ctx.moveTo(37.4, 70.3);
  ctx.quadraticCurveTo(FX, 77, 62.6, 70.3);
  ctx.stroke();
  if (role === 'monarch' || female) {
    ctx.strokeStyle = 'rgba(230,191,51,0.85)';
    ctx.lineWidth = 1.0;
    ctx.beginPath();
    ctx.moveTo(37.6, 66.8);
    ctx.quadraticCurveTo(FX, 73.8, 62.4, 66.8);
    ctx.stroke();
  }
}

/** 弓兵斜挎革带 + 带扣 */
function drawBaldric(ctx: Ctx): void {
  ctx.fillStyle = lin(ctx, 30, 62, 70, 100, [[0, '#6A4426'], [1, '#452A14']]);
  ctx.beginPath();
  ctx.moveTo(29, 62);
  ctx.lineTo(35.5, 62);
  ctx.lineTo(71, 100);
  ctx.lineTo(63.5, 100);
  ctx.closePath();
  ctx.fill();
  ctx.setLineDash([1.8, 1.8]);
  ctx.strokeStyle = 'rgba(240,220,180,0.35)';
  ctx.lineWidth = 0.7;
  ctx.beginPath();
  ctx.moveTo(30.2, 63.5);
  ctx.lineTo(65.5, 100);
  ctx.moveTo(34.3, 62.5);
  ctx.lineTo(69.8, 99.5);
  ctx.stroke();
  ctx.setLineDash([]);
  fillRoundRect(ctx, 46.4, 77.4, 5.4, 4.2, 1.2, '#8A6A3A');
  fillCircle(ctx, 49.1, 79.5, 0.9, '#3A2A14');
}

/** 器械兵前围裙 + 腰带 */
function drawApron(ctx: Ctx, base: string): void {
  fillRoundRect(ctx, 36, 71, 28, 29, 3, lin(ctx, 0, 71, 0, 100, [
    [0, shade(base, -0.06)], [1, shade(base, -0.34)],
  ]));
  ctx.setLineDash([1.8, 1.6]);
  ctx.strokeStyle = 'rgba(240,220,180,0.30)';
  ctx.lineWidth = 0.7;
  pathRoundRect(ctx, 37.6, 72.6, 24.8, 25.8, 2.4);
  ctx.stroke();
  ctx.setLineDash([]);
  fillRoundRect(ctx, 20, 84.5, 60, 4.6, 2, lin(ctx, 0, 84.5, 0, 89, [
    [0, '#4A3822'], [1, '#2E2112'],
  ]));
  fillRoundRect(ctx, 47, 84.8, 6, 4, 1.2, '#8A6A3A');
  fillCircle(ctx, FX, 86.8, 0.8, '#3A2A14');
}

/** 铠甲躯干：按兵种组合甲片/胸护/披膊/领口 */
function drawArmorDetails(
  ctx: Ctx, cls: ClassType, role: Role, female: boolean, base: string, charId: string,
): void {
  // 低位结构先行（被领口叠压）
  if (cls === 'cavalry' || cls === 'heavy') lamellarRows(ctx, base, 85.5, 100);
  else if (cls === 'infantry') lamellarRows(ctx, base, 71.5, 100);
  else if (cls === 'archer') drawBaldric(ctx);
  else if (cls === 'siege') drawApron(ctx, base);

  if (cls === 'cavalry' || cls === 'heavy') {
    drawChestMirrors(ctx, cls, base);
    for (const px of [20, 80]) drawPauldron(ctx, px, base, cls === 'heavy' ? 3 : 2, px > 50);
  } else if (cls === 'infantry') {
    for (const px of [20, 80]) drawPauldron(ctx, px, base, 1, px > 50);
  }

  drawCollar(ctx, base, role, female);

  // 平阳公主：娘子军红巾结
  if (charId === 'pingyang_princess') {
    ctx.fillStyle = '#B7261E';
    ctx.beginPath();
    ctx.moveTo(45.6, 65.8);
    ctx.quadraticCurveTo(FX, 70.6, 54.4, 65.8);
    ctx.lineTo(54.4, 69.4);
    ctx.quadraticCurveTo(FX, 73.8, 45.6, 69.4);
    ctx.closePath();
    ctx.fill();
    fillCircle(ctx, FX, 70.4, 1.4, '#8A1A14');
  }
}

/** 袍服躯干：交领 + 衣褶；长孙皇后加披帛/金缘/项链 */
function drawRobeDetails(ctx: Ctx, charId: string): void {
  const empress = charId === 'zhangsun_empress';
  if (empress) {
    // 披帛：过肩垂带
    for (const dir of [-1, 1]) {
      ctx.fillStyle = lin(ctx, FX + dir * 22, 62, FX + dir * 30, 100, [
        [0, '#C9755B'], [1, '#9C4A38'],
      ]);
      ctx.beginPath();
      ctx.moveTo(FX + dir * 12, 62.6);
      ctx.quadraticCurveTo(FX + dir * 22, 66, FX + dir * 26, 76);
      ctx.lineTo(FX + dir * 30, 100);
      ctx.lineTo(FX + dir * 23.5, 100);
      ctx.lineTo(FX + dir * 19.5, 77);
      ctx.quadraticCurveTo(FX + dir * 15.5, 68, FX + dir * 7.5, 65);
      ctx.closePath();
      ctx.fill();
      ctx.strokeStyle = 'rgba(255,220,190,0.25)';
      ctx.lineWidth = 0.8;
      ctx.beginPath();
      ctx.moveTo(FX + dir * 12.5, 64);
      ctx.quadraticCurveTo(FX + dir * 21, 67.5, FX + dir * 24.5, 76);
      ctx.stroke();
    }
  }
  // 内衬 + 交领（左衽压右衽）
  ctx.fillStyle = '#F2E8CE';
  ctx.beginPath();
  ctx.moveTo(40, 62.4);
  ctx.lineTo(FX, 74.5);
  ctx.lineTo(60, 62.4);
  ctx.lineTo(60, 65.6);
  ctx.lineTo(FX, 78);
  ctx.lineTo(40, 65.6);
  ctx.closePath();
  ctx.fill();
  ctx.fillStyle = '#D8C69E';
  ctx.beginPath();
  ctx.moveTo(63.5, 62.2);
  ctx.lineTo(49.5, 77);
  ctx.lineTo(55, 80.5);
  ctx.lineTo(68.5, 66);
  ctx.closePath();
  ctx.fill();
  ctx.fillStyle = '#E4D5B0';
  ctx.beginPath();
  ctx.moveTo(36.5, 62.2);
  ctx.lineTo(50.5, 77);
  ctx.lineTo(45, 80.5);
  ctx.lineTo(31.5, 66);
  ctx.closePath();
  ctx.fill();
  ctx.strokeStyle = empress ? 'rgba(230,191,51,0.8)' : 'rgba(110,90,58,0.5)';
  ctx.lineWidth = empress ? 1.0 : 0.8;
  ctx.beginPath();
  ctx.moveTo(36.9, 62.8);
  ctx.lineTo(50.3, 76.8);
  ctx.lineTo(63.1, 62.8);
  ctx.stroke();
  // 衣褶（竖向软影 + 间以亮痕）
  ctx.strokeStyle = 'rgba(0,0,0,0.10)';
  ctx.lineWidth = 2.2;
  for (const [x0, x1] of [[41, 39.5], [50, 50], [59, 60.5]] as [number, number][]) {
    ctx.beginPath();
    ctx.moveTo(x0, 82);
    ctx.quadraticCurveTo((x0 + x1) / 2, 90, x1, 99);
    ctx.stroke();
  }
  ctx.strokeStyle = 'rgba(255,255,255,0.08)';
  ctx.lineWidth = 1.6;
  for (const x of [45.5, 54.5]) {
    ctx.beginPath();
    ctx.moveTo(x, 83);
    ctx.lineTo(x, 99);
    ctx.stroke();
  }
  if (empress) {
    // 金项圈 + 红宝坠
    ctx.strokeStyle = '#E6BF33';
    ctx.lineWidth = 1.1;
    ctx.beginPath();
    ctx.arc(FX, 66.5, 4.6, Math.PI * 0.25, Math.PI * 0.75);
    ctx.stroke();
    fillCircle(ctx, FX, 71.6, 1.2, '#B7261E');
    fillCircle(ctx, FX - 0.4, 71.2, 0.4, 'rgba(255,255,255,0.7)');
  }
}

// ---------- 背后道具 ----------

/** 背后道具：秦琼双锏、刘弘基箭囊（先画，被躯干叠压） */
function drawBackProps(ctx: Ctx, charId: string): void {
  if (charId === 'qin_qiong') drawMaces(ctx);
  if (charId === 'liu_hongji') drawQuiver(ctx);
}

/** 秦琼双锏：一对四棱铁锏自肩后斜出，分节棱线 + 锏首圆珠 */
function drawMaces(ctx: Ctx): void {
  for (const dir of [-1, 1]) {
    ctx.save();
    ctx.translate(FX + dir * 24.5, 97);
    ctx.rotate(dir * 0.20);
    fillRoundRect(ctx, -1.6, -44, 3.2, 44, 1.5, lin(ctx, -1.6, 0, 1.6, 0, [
      [0, '#55555F'], [0.5, '#B9B9C4'], [1, '#4A4A54'],
    ]));
    ctx.strokeStyle = 'rgba(20,16,10,0.55)';
    ctx.lineWidth = 0.8;
    for (const y of [-10, -17, -24, -31, -38]) {
      ctx.beginPath();
      ctx.moveTo(-1.6, y);
      ctx.lineTo(1.6, y);
      ctx.stroke();
    }
    fillCircle(ctx, 0, -45.5, 2.2, '#9A9AA8');
    fillCircle(ctx, -0.7, -46.3, 0.75, 'rgba(255,255,255,0.7)');
    fillRoundRect(ctx, -2.8, -2.5, 5.6, 2.5, 1, '#3A3428');
    ctx.restore();
  }
}

/** 刘弘基箭囊：囊口 + 三矢自右肩后探出（羽朝上） */
function drawQuiver(ctx: Ctx): void {
  ctx.save();
  ctx.translate(79, 66);
  ctx.rotate(0.16);
  for (const [dx, dy] of [[-1, 0], [1.6, -1.2], [4.2, 0.3]] as [number, number][]) {
    ctx.strokeStyle = '#8A6A42';
    ctx.lineWidth = 0.9;
    ctx.beginPath();
    ctx.moveTo(dx, -1);
    ctx.lineTo(dx, -13 + dy);
    ctx.stroke();
    ctx.strokeStyle = 'rgba(230,225,215,0.9)';
    ctx.lineWidth = 1.1;
    ctx.beginPath();
    ctx.moveTo(dx - 1.3, -12 + dy);
    ctx.lineTo(dx, -16.5 + dy);
    ctx.lineTo(dx + 1.3, -12 + dy);
    ctx.stroke();
  }
  fillRoundRect(ctx, -3.4, -2.5, 8.2, 4.5, 2, '#4A3520');
  ctx.restore();
}

// ---------- 头颈 ----------

/** 脸形路径：颞部 → 颊 → 下颌角 → 颏（方脸/瓜子脸按人设） */
function facePath(ctx: Ctx, fw: number, jaw: number): void {
  const tw = 12.4 * fw, cw = 12.9 * fw, jw = jaw, cy = FY.chin;
  ctx.beginPath();
  ctx.moveTo(FX - tw, 30);
  ctx.bezierCurveTo(FX - cw - 0.8, 37, FX - cw + 0.4, 45, FX - jw, 51.5);
  ctx.bezierCurveTo(FX - jw + 1.6, 54.8, FX - 4.6, cy, FX, cy);
  ctx.bezierCurveTo(FX + 4.6, cy, FX + jw - 1.6, 54.8, FX + jw, 51.5);
  ctx.bezierCurveTo(FX + cw - 0.4, 45, FX + cw + 0.8, 37, FX + tw, 30);
  ctx.bezierCurveTo(FX + tw * 0.6, 24, FX - tw * 0.6, 24, FX - tw, 30);
  ctx.closePath();
}

/** 颈 + 头：颈部颌下投影、耳、脸基色与双向光照 */
function drawNeckHead(ctx: Ctx, charId: string, gender: Gender, persona: Persona): void {
  const jit = (hash01(charId, 7) - 0.5) * 0.10;
  const skin = shade(persona.skin ?? '#EFC9A2', jit);
  const female = gender === 'female';
  const fw = persona.faceW ?? (female ? 0.94 : 1);
  const jaw = (female ? 7.0 : persona.squareJaw ? 10.3 : 8.9) * fw;
  const cw = 12.9 * fw;

  // 颈：上暗下亮
  const wTop = female ? 4.4 : 5.4;
  const wBot = female ? 6.8 : 8.6;
  ctx.fillStyle = lin(ctx, 0, 48, 0, 66, [
    [0, shade(skin, -0.30)], [1, shade(skin, -0.12)],
  ]);
  ctx.beginPath();
  ctx.moveTo(FX - wTop, 49);
  ctx.lineTo(FX - wBot, 65.4);
  ctx.lineTo(FX + wBot, 65.4);
  ctx.lineTo(FX + wTop, 49);
  ctx.closePath();
  ctx.fill();
  // 颌下投影
  fillEllipse(ctx, FX, 57.8, wTop + 1.6, 2.8, 'rgba(58,26,10,0.30)');

  // 耳（先画，脸缘覆盖内侧）
  for (const dir of [-1, 1]) {
    const ex = FX + dir * (cw + 0.15);
    fillEllipse(ctx, ex, 42.6, 2.4, 3.4, shade(skin, -0.05));
    ctx.strokeStyle = shade(skin, -0.26);
    ctx.lineWidth = 0.8;
    ctx.beginPath();
    ctx.arc(ex, 42.6, 1.3, Math.PI * 0.2, Math.PI * 1.4);
    ctx.stroke();
  }

  // 脸：纵向渐变基色
  facePath(ctx, fw, jaw);
  ctx.fillStyle = lin(ctx, 0, 26, 0, 58, [
    [0, shade(skin, 0.11)], [0.5, skin], [1, shade(skin, -0.13)],
  ]);
  ctx.fill();
  // 双向光照（clip 于脸形内）
  ctx.save();
  facePath(ctx, fw, jaw);
  ctx.clip();
  ctx.fillStyle = lin(ctx, 35, 0, 65, 0, [
    [0, 'rgba(255,238,212,0.22)'], [0.42, 'rgba(255,238,212,0)'], [1, 'rgba(64,26,9,0.30)'],
  ]);
  ctx.fillRect(33, 22, 34, 38);
  // 额头受光斑
  fillEllipse(ctx, 46.5, 33.6, 5.6, 3.0, 'rgba(255,246,226,0.15)');
  // 右颊环境反光（暖色反弹）
  ctx.fillStyle = rad(ctx, 60.5, 49, 1, 7, [[0, 'rgba(255,175,115,0.11)'], [1, 'rgba(255,175,115,0)']]);
  ctx.fillRect(52, 41, 15, 15);
  // 颧下结构影
  ctx.strokeStyle = 'rgba(140,80,45,0.15)';
  ctx.lineWidth = 2.0;
  for (const dir of [-1, 1]) {
    ctx.beginPath();
    ctx.moveTo(FX + dir * (cw - 1.2), 44.2);
    ctx.quadraticCurveTo(FX + dir * (cw - 4.2), 47, FX + dir * 7.4, 50.6);
    ctx.stroke();
  }
  // 颏唇沟微影
  ctx.strokeStyle = 'rgba(140,80,45,0.18)';
  ctx.lineWidth = 1.1;
  ctx.beginPath();
  ctx.moveTo(46.8, 55.4);
  ctx.quadraticCurveTo(FX, 56.4, 53.2, 55.4);
  ctx.stroke();
  ctx.restore();
  // 脸缘收线（强化下颌轮廓）
  facePath(ctx, fw, jaw);
  ctx.strokeStyle = 'rgba(64,30,12,0.26)';
  ctx.lineWidth = 0.9;
  ctx.stroke();
}

// ---------- 五官 ----------

/** 五官调度：眉（身份分型）→ 眼（写实杏仁）→ 鼻 → 嘴/须 */
function drawFace(
  ctx: Ctx, charId: string, role: Role, gender: Gender, persona: Persona,
): void {
  const ink = '#261A0D';
  const fw = persona.faceW ?? (gender === 'female' ? 0.94 : 1);
  const eyeDx = 6.9 + (fw - 1) * 4;
  const eyeY = FY.eye + (hash01(charId, 29) - 0.5) * 1.6;

  drawBrows(ctx, charId, role, gender, ink, persona);
  for (const dir of [-1, 1]) drawEye(ctx, FX + dir * eyeDx, eyeY, dir, gender, persona);
  drawNose(ctx);
  if (gender === 'female') drawFemaleMouth(ctx, charId);
  else drawMaleMouthBeard(ctx, charId, role, persona);
}

/** 眉：君主剑眉 / 武将粗眉 / 尉迟倒竖怒眉 / 李靖沉稳平眉 / 文士细眉 / 女性弯眉（平阳略直） */
function drawBrows(
  ctx: Ctx, charId: string, role: Role, gender: Gender, ink: string, persona: Persona,
): void {
  const y = FY.brow;
  ctx.strokeStyle = ink;
  ctx.fillStyle = ink;
  if (gender === 'female') {
    const straight = charId === 'pingyang_princess';
    ctx.lineWidth = straight ? 1.3 : 1.1;
    for (const dir of [-1, 1]) {
      ctx.beginPath();
      ctx.moveTo(FX + dir * 3.6, y + 0.4);
      ctx.quadraticCurveTo(FX + dir * 6.8, y - (straight ? 2.0 : 2.6), FX + dir * 10.6, y - (straight ? 1.6 : 0.9));
      ctx.stroke();
    }
    return;
  }
  if (role === 'monarch') {
    // 剑眉：斜飞入鬓的尖尾眉形（内端扬起，英挺不怒）
    for (const dir of [-1, 1]) {
      ctx.beginPath();
      ctx.moveTo(FX + dir * 3.4, y + 0.6);
      ctx.lineTo(FX + dir * 10.6, y - 2.4);
      ctx.lineTo(FX + dir * 12.0, y - 3.2);
      ctx.lineTo(FX + dir * 11.4, y - 1.2);
      ctx.lineTo(FX + dir * 4.4, y + 1.7);
      ctx.closePath();
      ctx.fill();
    }
    return;
  }
  if (persona.glare) {
    // 怒眉倒竖 + 眉心皱线
    for (const dir of [-1, 1]) {
      ctx.beginPath();
      ctx.moveTo(FX + dir * 3.0, y + 2.2);
      ctx.lineTo(FX + dir * 10.8, y - 2.6);
      ctx.lineTo(FX + dir * 11.6, y - 1.2);
      ctx.lineTo(FX + dir * 4.2, y + 3.2);
      ctx.closePath();
      ctx.fill();
    }
    ctx.strokeStyle = 'rgba(110,60,32,0.5)';
    ctx.lineWidth = 0.8;
    ctx.beginPath();
    ctx.moveTo(48.8, y + 1.2);
    ctx.lineTo(48.9, y + 3.6);
    ctx.moveTo(51.2, y + 1.2);
    ctx.lineTo(51.1, y + 3.6);
    ctx.stroke();
    return;
  }
  if (persona.calm) {
    // 沉稳平眉（位置略低，近于压眼）
    for (const dir of [-1, 1]) {
      ctx.beginPath();
      ctx.moveTo(FX + dir * 3.6, y + 1.0);
      ctx.lineTo(FX + dir * 10.8, y - 0.4);
      ctx.lineTo(FX + dir * 10.9, y + 0.8);
      ctx.lineTo(FX + dir * 3.9, y + 2.2);
      ctx.closePath();
      ctx.fill();
    }
    return;
  }
  if (role === 'warrior') {
    if (persona.smile) {
      // 豪爽笑眉：粗眉上扬成弧（程咬金）
      ctx.lineWidth = 2.1;
      for (const dir of [-1, 1]) {
        ctx.beginPath();
        ctx.moveTo(FX + dir * 3.8, y + 0.4);
        ctx.quadraticCurveTo(FX + dir * 7.2, y - 2.6, FX + dir * 11.0, y - 1.2);
        ctx.stroke();
      }
      return;
    }
    // 粗眉斜起
    for (const dir of [-1, 1]) {
      ctx.beginPath();
      ctx.moveTo(FX + dir * 3.6, y + 0.5);
      ctx.lineTo(FX + dir * 10.6, y - 1.5);
      ctx.lineTo(FX + dir * 11.0, y - 0.1);
      ctx.lineTo(FX + dir * 4.0, y + 1.8);
      ctx.closePath();
      ctx.fill();
    }
    return;
  }
  // 文士细眉（疏淡弧线）
  ctx.lineWidth = 1.2;
  for (const dir of [-1, 1]) {
    ctx.beginPath();
    ctx.moveTo(FX + dir * 3.8, y + 0.6);
    ctx.quadraticCurveTo(FX + dir * 7.0, y - 1.8, FX + dir * 10.4, y - 0.6);
    ctx.stroke();
  }
}

/** 写实杏仁眼：睑形眼白 + 虹膜渐变 + 瞳孔 + 双高光 + 上下睑线；
 *  环眼（程咬金）瞳小显瞪，沉稳（李靖）睑垂，女性双眼皮+睫毛外挑 */
function drawEye(
  ctx: Ctx, cx: number, cy: number, dir: number, gender: Gender, persona: Persona,
): void {
  const female = gender === 'female';
  const round = !!persona.roundEyes;
  const calm = !!persona.calm;
  const w = female ? 3.3 : round ? 3.2 : 3.0;
  const h = round ? 3.15 : female ? 2.75 : calm ? 2.15 : 2.55;
  const ir = round ? h * 0.60 : h * 0.78;

  // 镜像到本眼朝向：内眦在 -x 侧，外眦在 +x 侧
  ctx.save();
  ctx.translate(cx, cy);
  ctx.scale(dir, 1);
  // 眼白（杏仁形，微暖灰）
  ctx.beginPath();
  ctx.moveTo(-w, 0.35);
  ctx.quadraticCurveTo(-w * 0.15, -h * 1.02, w, 0.05);
  ctx.quadraticCurveTo(w * 0.2, h * 0.82, -w, 0.35);
  ctx.closePath();
  ctx.fillStyle = '#F5EDDE';
  ctx.fill();
  // 虹膜/瞳孔（clip 于眼形内）
  ctx.save();
  ctx.clip();
  const iy = round ? 0 : 0.25;
  fillCircle(ctx, 0, iy, ir, rad(ctx, -0.3, iy - 0.5, 0.2, ir, [
    [0, '#8A6038'], [0.55, '#4A2E16'], [1, '#221206'],
  ]));
  fillCircle(ctx, 0, iy, ir * 0.46, '#140C05');
  // 上睑投影（虹膜顶部压暗）
  fillEllipse(ctx, 0, iy - ir * 0.85, ir * 1.05, ir * 0.55, 'rgba(46,22,8,0.22)');
  ctx.restore();
  // 上睑线（外角微延）
  ctx.strokeStyle = '#26170B';
  ctx.lineWidth = 0.95;
  ctx.beginPath();
  ctx.moveTo(-w, 0.35);
  ctx.quadraticCurveTo(-w * 0.15, -h * 1.02, w, 0.05);
  ctx.lineTo(w + 0.9, female ? -0.55 : 0.05);
  ctx.stroke();
  // 下睑线（淡）
  ctx.strokeStyle = 'rgba(150,95,58,0.45)';
  ctx.lineWidth = 0.7;
  ctx.beginPath();
  ctx.moveTo(-w + 0.6, 0.45);
  ctx.quadraticCurveTo(w * 0.3, h * 0.85, w - 0.4, 0.1);
  ctx.stroke();
  // 内眦（泪阜一点）
  fillCircle(ctx, -w + 0.35, 0.3, 0.32, 'rgba(198,118,104,0.55)');
  if (female) {
    // 双眼皮褶皱
    ctx.strokeStyle = 'rgba(122,76,44,0.40)';
    ctx.lineWidth = 0.7;
    ctx.beginPath();
    ctx.moveTo(-w * 0.78, -h * 0.85);
    ctx.quadraticCurveTo(w * 0.1, -h * 1.55, w * 0.92, -h * 0.72);
    ctx.stroke();
    // 睫毛外挑
    ctx.strokeStyle = '#26170B';
    ctx.lineWidth = 0.75;
    ctx.beginPath();
    ctx.moveTo(w + 0.3, -0.5);
    ctx.lineTo(w + 1.4, -1.35);
    ctx.moveTo(w + 0.6, -0.15);
    ctx.lineTo(w + 1.8, -0.75);
    ctx.stroke();
  }
  if (persona.smile) {
    // 笑纹（外眦放射两笔）
    ctx.strokeStyle = 'rgba(120,70,40,0.45)';
    ctx.lineWidth = 0.7;
    ctx.beginPath();
    ctx.moveTo(w + 0.8, -0.9);
    ctx.lineTo(w + 1.9, -1.3);
    ctx.moveTo(w + 0.9, 0.1);
    ctx.lineTo(w + 2.0, 0.3);
    ctx.stroke();
  }
  ctx.restore();
  // 双高光（屏幕空间左上，不受镜像影响）
  const hy = cy + (round ? 0 : 0.25);
  fillCircle(ctx, cx - ir * 0.34, hy - ir * 0.42, 0.62, 'rgba(255,255,255,0.95)');
  fillCircle(ctx, cx + ir * 0.32, hy + ir * 0.40, 0.3, 'rgba(255,255,255,0.4)');
}

/** 鼻：梁左受光、梁右阴影、鼻头下影 + 鼻翼点 */
function drawNose(ctx: Ctx): void {
  ctx.strokeStyle = 'rgba(255,240,216,0.30)';
  ctx.lineWidth = 0.8;
  ctx.beginPath();
  ctx.moveTo(48.9, 43.4);
  ctx.lineTo(48.7, 46.4);
  ctx.stroke();
  ctx.strokeStyle = 'rgba(150,88,48,0.30)';
  ctx.lineWidth = 1.0;
  ctx.beginPath();
  ctx.moveTo(51.5, 42.9);
  ctx.quadraticCurveTo(52.1, 45.6, 51.4, 47.9);
  ctx.stroke();
  ctx.strokeStyle = 'rgba(140,78,42,0.38)';
  ctx.lineWidth = 1.1;
  ctx.beginPath();
  ctx.moveTo(47.9, 48.3);
  ctx.quadraticCurveTo(FX, 49.7, 52.1, 48.3);
  ctx.stroke();
  fillCircle(ctx, 48.3, 49.4, 0.4, 'rgba(80,40,18,0.48)');
  fillCircle(ctx, 51.7, 49.4, 0.4, 'rgba(80,40,18,0.48)');
  // 鼻底人中微影
  fillEllipse(ctx, FX, 50.6, 1.3, 0.45, 'rgba(120,60,30,0.22)');
}

/** 女性嘴：朱唇（上唇 M 形 + 下唇饱满 + 唇缝 + 下唇高光）；皇后色淡显温婉；配腮红 */
function drawFemaleMouth(ctx: Ctx, charId: string): void {
  const empress = charId === 'zhangsun_empress';
  const my = 52.4;
  fillCircle(ctx, 40.2, 47.8, 3.4, 'rgba(222,112,102,0.15)');
  fillCircle(ctx, 59.8, 47.8, 3.4, 'rgba(222,112,102,0.15)');
  const w = empress ? 3.7 : 4.1;
  ctx.fillStyle = lin(ctx, 0, my - 1.9, 0, my + 2.5, [
    [0, empress ? '#CF7268' : '#BE463C'],
    [1, empress ? '#B25A50' : '#93302A'],
  ]);
  ctx.beginPath();
  ctx.moveTo(FX - w, my);
  ctx.quadraticCurveTo(FX - w * 0.45, my - 1.9, FX, my - 0.5);
  ctx.quadraticCurveTo(FX + w * 0.45, my - 1.9, FX + w, my);
  ctx.quadraticCurveTo(FX + w * 0.5, my + 2.5, FX, my + 2.5);
  ctx.quadraticCurveTo(FX - w * 0.5, my + 2.5, FX - w, my);
  ctx.closePath();
  ctx.fill();
  ctx.strokeStyle = 'rgba(88,20,16,0.55)';
  ctx.lineWidth = 0.7;
  ctx.beginPath();
  ctx.moveTo(FX - w + 0.3, my + 0.1);
  ctx.quadraticCurveTo(FX, my + 0.9, FX + w - 0.3, my + 0.1);
  ctx.stroke();
  fillEllipse(ctx, FX - 0.4, my + 1.5, 1.1, 0.5, 'rgba(255,224,214,0.5)');
}

/** 男性嘴与须：唇线 + 下唇高光（主公嘴角微扬）；短髭八字、山羊须垂颔、虬髯见 drawFullBeard */
function drawMaleMouthBeard(
  ctx: Ctx, charId: string, role: Role, persona: Persona,
): void {
  const beard: Beard | undefined = BEARDS[charId];
  const bc = persona.beard ?? '#2A1B12';
  const my = FY.mouth;

  if (beard === 'full') {
    drawFullBeard(ctx, bc, !!persona.smile);
    return;
  }
  // 唇线（秦琼端正平直，主公嘴角微扬）
  const curve = charId === 'qin_qiong' ? 0.35 : role === 'monarch' ? 1.0 : 0.7;
  ctx.strokeStyle = 'rgba(122,52,34,0.85)';
  ctx.lineWidth = 1.15;
  ctx.beginPath();
  ctx.moveTo(FX - 4.4, my);
  ctx.quadraticCurveTo(FX, my + curve, FX + 4.4, my);
  ctx.stroke();
  if (role === 'monarch') {
    ctx.beginPath();
    ctx.moveTo(FX - 4.4, my);
    ctx.lineTo(FX - 5.2, my - 0.9);
    ctx.moveTo(FX + 4.4, my);
    ctx.lineTo(FX + 5.2, my - 0.9);
    ctx.stroke();
  }
  // 下唇高光
  ctx.strokeStyle = 'rgba(255,216,188,0.30)';
  ctx.lineWidth = 1.2;
  ctx.beginPath();
  ctx.moveTo(FX - 2.6, my + 2.1);
  ctx.quadraticCurveTo(FX, my + 3.0, FX + 2.6, my + 2.1);
  ctx.stroke();

  if (beard === 'goatee') {
    // 上髭 + 颔下山羊须（须色按人设深浅有别）
    ctx.strokeStyle = bc;
    ctx.lineWidth = 1.6;
    ctx.beginPath();
    ctx.moveTo(FX - 4.6, my - 1.7);
    ctx.quadraticCurveTo(FX, my - 0.9, FX + 4.6, my - 1.7);
    ctx.stroke();
    ctx.fillStyle = lin(ctx, 0, 58, 0, 69, [[0, bc], [1, shade(bc, 0.10)]]);
    ctx.beginPath();
    ctx.moveTo(FX - 3.4, 58.2);
    ctx.quadraticCurveTo(FX, 60.0, FX + 3.4, 58.2);
    ctx.quadraticCurveTo(FX + 2.4, 64.5, FX, 68.6);
    ctx.quadraticCurveTo(FX - 2.4, 64.5, FX - 3.4, 58.2);
    ctx.closePath();
    ctx.fill();
    ctx.strokeStyle = shade(bc, 0.18);
    ctx.lineWidth = 0.7;
    for (const dx of [-1.4, 0, 1.4]) {
      ctx.beginPath();
      ctx.moveTo(FX + dx, 59.6);
      ctx.quadraticCurveTo(FX + dx * 0.7, 63.5, FX + dx * 0.4, 67.2);
      ctx.stroke();
    }
  } else if (beard === 'mustache') {
    // 八字短髭：两片 tapered 髭身 + 须丝
    for (const dir of [-1, 1]) {
      ctx.fillStyle = bc;
      ctx.beginPath();
      ctx.moveTo(FX + dir * 0.4, my - 2.2);
      ctx.quadraticCurveTo(FX + dir * 3.4, my - 2.8, FX + dir * 5.6, my - 0.6);
      ctx.quadraticCurveTo(FX + dir * 3.2, my - 0.9, FX + dir * 0.6, my - 1.0);
      ctx.closePath();
      ctx.fill();
      ctx.strokeStyle = shade(bc, 0.20);
      ctx.lineWidth = 0.6;
      ctx.beginPath();
      ctx.moveTo(FX + dir * 1.2, my - 1.9);
      ctx.quadraticCurveTo(FX + dir * 3.2, my - 2.2, FX + dir * 4.9, my - 0.9);
      ctx.stroke();
    }
  }
}

/** 虬髯（尉迟/程咬金）：覆颊垂颔，边缘叠圆成虬结，多股卷须纹 */
function drawFullBeard(ctx: Ctx, bc: string, smile: boolean): void {
  const g = rad(ctx, FX, 56, 4, 26, [[0, shade(bc, 0.10)], [1, bc]]);
  ctx.fillStyle = g;
  ctx.beginPath();
  ctx.moveTo(37.2, 44.8);
  ctx.bezierCurveTo(35.4, 52.5, 38.5, 60.5, 43, 65);
  ctx.quadraticCurveTo(46.5, 69, FX, 69.5);
  ctx.quadraticCurveTo(53.5, 69, 57, 65);
  ctx.bezierCurveTo(61.5, 60.5, 64.6, 52.5, 62.8, 44.8);
  ctx.quadraticCurveTo(57, 49.8, FX, 50.4);
  ctx.quadraticCurveTo(43, 49.8, 37.2, 44.8);
  ctx.closePath();
  ctx.fill();
  // 虬结边缘（叠圆）
  const knots: [number, number, number][] = [
    [38.5, 56, 3.2], [41, 63, 3.4], [46.5, 67.5, 3.2], [53.5, 67.5, 3.2],
    [59, 63, 3.4], [61.5, 56, 3.2], [36.8, 49.5, 2.8], [63.2, 49.5, 2.8],
  ];
  for (const [x, y, r] of knots) fillCircle(ctx, x, y, r, g);
  // 上髭
  fillEllipse(ctx, 45.8, 51, 3.4, 1.9, bc);
  fillEllipse(ctx, 54.2, 51, 3.4, 1.9, bc);
  // 卷须纹：多股弧旋
  ctx.strokeStyle = 'rgba(150,105,70,0.80)';
  ctx.lineWidth = 0.8;
  const curls: [number, number, number, number][] = [
    [41.5, 54.5, 2.4, 0.9], [47, 57.5, 2.6, 0.2], [53, 57.5, 2.6, 0.9],
    [58.5, 54.5, 2.4, 0.2], [44, 63, 2.2, 0.6], [56, 63, 2.2, 0.6], [50, 65.5, 2.0, 0.4],
  ];
  for (const [x, y, r, a] of curls) {
    ctx.beginPath();
    ctx.arc(x, y, r, Math.PI * a, Math.PI * (a + 1.3));
    ctx.stroke();
  }
  if (smile) {
    // 程咬金：张口大笑（齿白 + 口腔暗红）
    fillRoundRect(ctx, 46.2, 53.4, 7.6, 1.9, 0.9, '#F5EBD8');
    ctx.fillStyle = '#5A2018';
    ctx.beginPath();
    ctx.moveTo(45.8, 55.2);
    ctx.quadraticCurveTo(FX, 60.6, 54.2, 55.2);
    ctx.closePath();
    ctx.fill();
  } else {
    // 尉迟：须中一线抿嘴
    ctx.strokeStyle = 'rgba(150,80,58,0.9)';
    ctx.lineWidth = 1.2;
    ctx.beginPath();
    ctx.moveTo(46.8, 53.4);
    ctx.quadraticCurveTo(FX, 54.8, 53.2, 53.4);
    ctx.stroke();
  }
}

// ---------- 头部装备 ----------

/** 盔顶轮廓：覆颅至额带上缘（y≈34.5），给眉眼留出空间 */
function capPath(ctx: Ctx, top: number): void {
  ctx.beginPath();
  ctx.moveTo(33.8, 34.5);
  ctx.bezierCurveTo(33.2, top, 66.8, top, 66.2, 34.5);
  ctx.closePath();
}

/** 额带（各盔/巾/冠通用） */
function browBand(ctx: Ctx, y: number, h: number, style: Style): void {
  fillRoundRect(ctx, 33, y, 34, h, Math.min(2.4, h / 2), style);
}

/** 男性束发底：发体 + 发丝分组 + 鬓角（主公/弓兵/谋士共用） */
function drawMaleHair(ctx: Ctx, top: number): void {
  ctx.fillStyle = lin(ctx, 0, top, 0, 36, [[0, '#3A2A1E'], [1, '#1E140C']]);
  capPath(ctx, top);
  ctx.fill();
  ctx.strokeStyle = 'rgba(150,110,70,0.40)';
  ctx.lineWidth = 0.75;
  for (const dx of [-7, -3.5, 0, 3.5, 7]) {
    ctx.beginPath();
    ctx.moveTo(FX + dx * 0.4, top + 1.5);
    ctx.quadraticCurveTo(FX + dx, top + 6, FX + dx * 1.35, 30);
    ctx.stroke();
  }
  for (const dir of [-1, 1]) {
    ctx.fillStyle = '#241A12';
    ctx.beginPath();
    ctx.moveTo(FX + dir * 12.0, 34.5);
    ctx.lineTo(FX + dir * 12.9, 34.5);
    ctx.lineTo(FX + dir * 12.4, 42);
    ctx.closePath();
    ctx.fill();
  }
}

/** 盔体金属拉丝（三道同心弧）+ 右侧轮廓光 */
function helmBrush(ctx: Ctx, rimColor: string): void {
  ctx.strokeStyle = 'rgba(255,240,200,0.30)';
  ctx.lineWidth = 0.8;
  for (const r of [7, 10.5, 14]) {
    ctx.beginPath();
    ctx.arc(FX, 34, r, Math.PI * 1.28, Math.PI * 1.72);
    ctx.stroke();
  }
  ctx.strokeStyle = rimColor;
  ctx.lineWidth = 1.0;
  ctx.beginPath();
  ctx.arc(FX, 34, 16.6, Math.PI * 1.62, Math.PI * 1.94);
  ctx.stroke();
}

/** 头部装备（男性）：按兵种——凤翅盔/铁兜鍪/皮帻/束发带/工匠帽/文士冠；
 *  主公为束发+三点金冠；李靖特例为儒将巾（文士巾偏武将色） */
function drawHeadgear(
  ctx: Ctx, cls: ClassType, base: string, charId: string, role: Role,
): void {
  const j = (hash01(charId, 13) - 0.5) * 0.14; // 盔色明度微调

  if (role === 'monarch') {
    drawMaleHair(ctx, 21);
    drawCrown(ctx);
    return;
  }
  if (charId === 'li_jing') {
    drawRujiangScarf(ctx, j);
    return;
  }

  switch (cls) {
    case 'cavalry': {
      const bronze = '#B08D4F';
      // 凤翅：左右两重阔羽，自盔侧向上扬起（先画翅，压在盔体下缘之后）
      for (const dir of [-1, 1]) {
        const rootX = FX + dir * 13.5;
        for (const [spread, lift] of [[26, 21], [20.5, 14.5]] as [number, number][]) {
          const tx = FX + dir * spread;
          const ty = 34 - lift;
          ctx.fillStyle = lin(ctx, rootX, 33, tx, ty, [
            [0, shade('#C8AC72', j + 0.06)], [1, shade('#8A6E3E', j)],
          ]);
          ctx.beginPath();
          ctx.moveTo(rootX, 33.5);
          ctx.quadraticCurveTo(FX + dir * (spread + 3.5), 34 - lift * 0.55, tx, ty);
          ctx.quadraticCurveTo(FX + dir * (spread * 0.60), 34 - lift * 0.40, rootX + dir * 3.2, 31.2);
          ctx.closePath();
          ctx.fill();
          ctx.strokeStyle = 'rgba(60,40,16,0.35)';
          ctx.lineWidth = 0.7;
          ctx.stroke();
        }
        // 羽脉
        ctx.strokeStyle = 'rgba(60,40,16,0.3)';
        ctx.lineWidth = 0.7;
        ctx.beginPath();
        ctx.moveTo(rootX + dir * 1.6, 32.5);
        ctx.quadraticCurveTo(FX + dir * 21, 22, FX + dir * 24.5, 13.5);
        ctx.stroke();
      }
      // 盔体：金属线性渐变 + 拉丝 + 轮廓光
      ctx.fillStyle = lin(ctx, 0, 15, 0, 36, [
        [0, shade(bronze, 0.24 + j)], [0.5, shade(bronze, j)], [1, shade(bronze, -0.30 + j)],
      ]);
      capPath(ctx, 16);
      ctx.fill();
      helmBrush(ctx, 'rgba(255,222,160,0.50)');
      // 中锋脊 + 红缨珠 + 缨丝
      fillRoundRect(ctx, 48.9, 12.5, 2.2, 19.5, 1.1, shade('#8A6E3E', j + 0.08));
      fillCircle(ctx, FX, 11.6, 2.6, '#B7261E');
      fillCircle(ctx, 49.2, 10.8, 0.85, 'rgba(255,255,255,0.6)');
      ctx.strokeStyle = '#B7261E';
      ctx.lineWidth = 0.9;
      for (const dx of [-2, -1, 0, 1, 2]) {
        ctx.beginPath();
        ctx.moveTo(FX + dx * 0.5, 9.6);
        ctx.quadraticCurveTo(FX + dx * 1.2, 6.6, FX + dx * 1.7, 4.4);
        ctx.stroke();
      }
      // 额带 + 底部暗边 + 铆钉
      browBand(ctx, 31.6, 5.2, lin(ctx, 0, 31.6, 0, 36.8, [
        [0, shade('#6E5A36', j + 0.10)], [1, shade('#54432A', j)],
      ]));
      ctx.strokeStyle = 'rgba(30,20,8,0.5)';
      ctx.lineWidth = 0.9;
      ctx.beginPath();
      ctx.moveTo(35, 36.5);
      ctx.lineTo(65, 36.5);
      ctx.stroke();
      rivet(ctx, 40, 34.2, shade('#C9A876', j));
      rivet(ctx, FX, 34.2, shade('#C9A876', j));
      rivet(ctx, 60, 34.2, shade('#C9A876', j));
      break;
    }
    case 'heavy': {
      const steel = '#86868F';
      // 盔体
      ctx.fillStyle = lin(ctx, 0, 13, 0, 36, [
        [0, shade(steel, 0.26 + j)], [0.5, shade(steel, j)], [1, shade(steel, -0.30 + j)],
      ]);
      capPath(ctx, 14);
      ctx.fill();
      helmBrush(ctx, 'rgba(230,235,245,0.45)');
      // 眉庇额带 + 铆钉列 + 底部暗边
      browBand(ctx, 31.2, 5.4, lin(ctx, 0, 31.2, 0, 36.6, [
        [0, shade('#5C5C66', j + 0.06)], [1, shade('#44444E', j)],
      ]));
      ctx.strokeStyle = 'rgba(0,0,0,0.4)';
      ctx.lineWidth = 0.9;
      ctx.beginPath();
      ctx.moveTo(34.5, 36.3);
      ctx.lineTo(65.5, 36.3);
      ctx.stroke();
      for (const x of [38, 45.3, 54.7, 62]) rivet(ctx, x, 33.9, '#B9B9C4');
      // 护颊（覆耳）：渐变 + 底部暗边 + 铆钉 + 外缘轮廓光
      for (const dir of [-1, 1]) {
        const x = dir < 0 ? 32.6 : 60.9;
        fillRoundRect(ctx, x, 35.5, 6.5, 12.5, 2.6, lin(ctx, 0, 36, 0, 49, [
          [0, shade(steel, 0.10 + j)], [1, shade(steel, -0.26 + j)],
        ]));
        ctx.strokeStyle = 'rgba(0,0,0,0.35)';
        ctx.lineWidth = 1.0;
        ctx.beginPath();
        ctx.moveTo(x + 0.8, 47.2);
        ctx.lineTo(x + 5.7, 47.2);
        ctx.stroke();
        rivet(ctx, x + 3.2, 40, '#B9B9C4');
        rivet(ctx, x + 3.2, 44.5, '#B9B9C4');
        if (dir > 0) {
          ctx.strokeStyle = 'rgba(255,222,160,0.45)';
          ctx.lineWidth = 0.9;
          ctx.beginPath();
          ctx.moveTo(x + 6.2, 36.5);
          ctx.lineTo(x + 6.2, 46.5);
          ctx.stroke();
        }
      }
      // 顶钮
      fillRoundRect(ctx, 48.9, 11.5, 2.2, 5.5, 1.1, shade('#55555F', j));
      fillCircle(ctx, FX, 10.8, 2.5, rad(ctx, 49, 9.6, 0.5, 3, [
        [0, '#D9D9E2'], [1, shade('#77777F', j)],
      ]));
      break;
    }
    case 'infantry': {
      const leather = '#7A5230';
      // 帻体
      ctx.fillStyle = lin(ctx, 0, 20, 0, 36, [
        [0, shade(leather, 0.18 + j)], [1, shade(leather, -0.22 + j)],
      ]);
      capPath(ctx, 20);
      ctx.fill();
      // 顶部缝线 + 右侧轮廓光
      ctx.strokeStyle = 'rgba(240,220,180,0.35)';
      ctx.lineWidth = 0.8;
      ctx.beginPath();
      ctx.moveTo(39, 25.5);
      ctx.quadraticCurveTo(FX, 21.5, 61, 25.5);
      ctx.stroke();
      ctx.strokeStyle = 'rgba(255,222,160,0.40)';
      ctx.lineWidth = 0.9;
      ctx.beginPath();
      ctx.arc(FX, 34, 16.2, Math.PI * 1.66, Math.PI * 1.94);
      ctx.stroke();
      // 颜题（前标牌）+ 缝线描边
      fillRoundRect(ctx, 41.5, 23.5, 17, 8.5, 2.5, lin(ctx, 0, 23.5, 0, 32, [
        [0, shade(leather, 0.10 + j)], [1, shade(leather, -0.16 + j)],
      ]));
      ctx.setLineDash([1.5, 1.4]);
      ctx.strokeStyle = 'rgba(240,220,180,0.40)';
      ctx.lineWidth = 0.7;
      pathRoundRect(ctx, 42.7, 24.7, 14.6, 6.1, 1.8);
      ctx.stroke();
      ctx.setLineDash([]);
      // 额带 + 铆钉
      browBand(ctx, 32, 5, lin(ctx, 0, 32, 0, 37, [
        [0, shade('#54371E', j + 0.08)], [1, shade('#402A14', j)],
      ]));
      for (const x of [40, FX, 60]) rivet(ctx, x, 34.5, '#C9A876');
      break;
    }
    case 'archer': {
      // 束发：发体 + 高髻 + 发丝
      drawMaleHair(ctx, 20);
      fillCircle(ctx, FX, 18.2, 4.4, '#2A1B12');
      ctx.strokeStyle = 'rgba(120,90,60,0.5)';
      ctx.lineWidth = 0.8;
      ctx.beginPath();
      ctx.arc(FX, 18.2, 2.8, Math.PI * 1.1, Math.PI * 1.9);
      ctx.stroke();
      fillRoundRect(ctx, 46.8, 21.2, 6.4, 1.8, 0.9, shade(base, -0.05));
      // 额带（兵种色渐变）
      browBand(ctx, 32, 5, lin(ctx, 0, 32, 0, 37, [
        [0, shade(base, 0.10)], [1, shade(base, -0.28)],
      ]));
      // 带结（右侧）+ 双飘带
      const kx = 68, ky = 34.5;
      ctx.strokeStyle = shade(base, -0.12);
      ctx.lineWidth = 1.8;
      for (const [dy, len] of [[1.4, 8.5], [-1, 6.5]] as [number, number][]) {
        ctx.beginPath();
        ctx.moveTo(kx + 1.4, ky + dy * 0.4);
        ctx.quadraticCurveTo(kx + 5.6, ky + dy * 2, kx + 7.2, ky + dy * 2 + len * 0.55);
        ctx.stroke();
      }
      fillCircle(ctx, kx, ky, 2.3, shade(base, -0.05));
      ctx.strokeStyle = shade(base, -0.35);
      ctx.lineWidth = 0.8;
      ctx.beginPath();
      ctx.arc(kx, ky, 2.3, 0, Math.PI * 2);
      ctx.stroke();
      break;
    }
    case 'siege': {
      const cloth = '#7C6A4A';
      // 软帽体
      ctx.fillStyle = lin(ctx, 0, 13, 0, 38, [
        [0, shade(cloth, 0.16 + j)], [1, shade(cloth, -0.20 + j)],
      ]);
      ctx.beginPath();
      ctx.moveTo(35, 35);
      ctx.lineTo(38.5, 21);
      ctx.quadraticCurveTo(FX, 12.5, 61.5, 21);
      ctx.lineTo(65, 35);
      ctx.quadraticCurveTo(FX, 29, 35, 35);
      ctx.closePath();
      ctx.fill();
      // 顶部高光
      ctx.strokeStyle = 'rgba(255,255,255,0.3)';
      ctx.lineWidth = 1.1;
      ctx.beginPath();
      ctx.moveTo(42.5, 19.5);
      ctx.quadraticCurveTo(FX, 15.8, 57.5, 19.5);
      ctx.stroke();
      // 折边
      ctx.fillStyle = lin(ctx, 0, 32, 0, 39, [
        [0, shade('#57492F', j + 0.08)], [1, shade('#463A26', j)],
      ]);
      ctx.beginPath();
      ctx.moveTo(34.5, 34.5);
      ctx.quadraticCurveTo(FX, 28.5, 65.5, 34.5);
      ctx.lineTo(65.5, 38);
      ctx.quadraticCurveTo(FX, 32, 34.5, 38);
      ctx.closePath();
      ctx.fill();
      // 顶钮 + 缝线
      fillCircle(ctx, FX, 13.5, 2.2, shade('#57492F', j));
      ctx.strokeStyle = 'rgba(240,220,180,0.3)';
      ctx.lineWidth = 0.9;
      ctx.beginPath();
      ctx.moveTo(44.5, 21);
      ctx.lineTo(43.2, 31.5);
      ctx.moveTo(55.5, 21);
      ctx.lineTo(56.8, 31.5);
      ctx.stroke();
      break;
    }
    case 'strategist': {
      // 束发底
      drawMaleHair(ctx, 21.5);
      // 冠体：高耸 + 顶部高光
      fillRoundRect(ctx, 38.5, 8.5, 23, 24.5, 6, lin(ctx, 0, 8, 0, 34, [
        [0, '#3E3830'], [0.5, '#2E2A26'], [1, '#1B1712'],
      ]));
      ctx.strokeStyle = 'rgba(255,255,255,0.18)';
      ctx.lineWidth = 1.1;
      ctx.beginPath();
      ctx.moveTo(44, 12.5);
      ctx.quadraticCurveTo(FX, 10, 56, 12.5);
      ctx.stroke();
      // 梁线（三道竖梁）
      ctx.strokeStyle = 'rgba(220,200,160,0.65)';
      ctx.lineWidth = 1.1;
      for (const x of [44.5, FX, 55.5]) {
        ctx.beginPath();
        ctx.moveTo(x, 11.5);
        ctx.quadraticCurveTo(x + (x - FX) * 0.06, 21, x, 31.5);
        ctx.stroke();
      }
      // 额带 + 金簪横穿（簪首金珠）
      browBand(ctx, 31.8, 4.8, lin(ctx, 0, 31.8, 0, 36.6, [
        [0, '#33291E'], [1, '#1D1915'],
      ]));
      ctx.strokeStyle = lin(ctx, 35, 0, 65, 0, [
        [0, '#A8861B'], [0.5, '#F2D675'], [1, '#A8861B'],
      ]);
      ctx.lineWidth = 1.6;
      ctx.beginPath();
      ctx.moveTo(35.5, 20);
      ctx.lineTo(64.5, 20);
      ctx.stroke();
      fillCircle(ctx, 34.8, 20, 1.6, '#E6BF33');
      fillCircle(ctx, 65.2, 20, 1.6, '#E6BF33');
      fillCircle(ctx, 34.3, 19.5, 0.55, 'rgba(255,255,255,0.75)');
      fillCircle(ctx, 64.7, 19.5, 0.55, 'rgba(255,255,255,0.75)');
      break;
    }
  }
}

/** 李靖特例·儒将巾：文士式软巾裹发，染武将铜褐色，前峰 + 铜銙 + 双巾脚 */
function drawRujiangScarf(ctx: Ctx, j: number): void {
  const cloth = '#8A5A34';
  // 巾体
  ctx.fillStyle = lin(ctx, 0, 15, 0, 36, [
    [0, shade(cloth, 0.14 + j)], [1, shade(cloth, -0.24 + j)],
  ]);
  capPath(ctx, 16.5);
  ctx.fill();
  // 前峰
  ctx.fillStyle = shade(cloth, -0.02 + j);
  ctx.beginPath();
  ctx.moveTo(43, 20.5);
  ctx.quadraticCurveTo(FX, 11, 57, 20.5);
  ctx.closePath();
  ctx.fill();
  // 巾褶
  ctx.strokeStyle = 'rgba(30,18,8,0.4)';
  ctx.lineWidth = 0.9;
  ctx.beginPath();
  ctx.moveTo(46, 16.5);
  ctx.quadraticCurveTo(42.5, 23, 41.5, 30);
  ctx.moveTo(54, 16.5);
  ctx.quadraticCurveTo(57.5, 23, 58.5, 30);
  ctx.stroke();
  // 右侧轮廓光
  ctx.strokeStyle = 'rgba(255,222,160,0.35)';
  ctx.lineWidth = 0.9;
  ctx.beginPath();
  ctx.arc(FX, 34, 16.4, Math.PI * 1.66, Math.PI * 1.94);
  ctx.stroke();
  // 额带 + 铜銙
  browBand(ctx, 31.8, 5, lin(ctx, 0, 31.8, 0, 36.8, [
    [0, shade('#5C3A1E', j + 0.06)], [1, shade('#462C14', j)],
  ]));
  fillRoundRect(ctx, 47.2, 32.9, 5.6, 3.4, 1.1, '#C9A876');
  fillCircle(ctx, 48.7, 33.8, 0.5, 'rgba(255,255,255,0.7)');
  // 双巾脚（垂带）
  ctx.strokeStyle = shade(cloth, -0.18 + j);
  ctx.lineWidth = 2.2;
  ctx.beginPath();
  ctx.moveTo(35.5, 36);
  ctx.quadraticCurveTo(33.2, 41.5, 34.2, 46);
  ctx.moveTo(64.5, 36);
  ctx.quadraticCurveTo(66.8, 41.5, 65.8, 46);
  ctx.stroke();
}

/** 女性发式：高髻 + 中分刘海 + 侧鬓垂绺 + 发丝分组；平阳公主英气高髻红缨，长孙皇后金钗耳坠花钿 */
function drawFemaleHair(ctx: Ctx, charId: string): void {
  const princess = charId === 'pingyang_princess';
  const hg = lin(ctx, 0, 12, 0, 50, [[0, '#402D1E'], [1, '#1C120A']]);
  // 顶发主体（自头顶覆至发际，中分微尖）
  ctx.fillStyle = hg;
  ctx.beginPath();
  ctx.moveTo(36.4, 42);
  ctx.bezierCurveTo(35, 16, 65, 16, 63.6, 42);
  ctx.quadraticCurveTo(60.5, 32.5, 55.5, 31.2);
  ctx.quadraticCurveTo(52.5, 30.4, FX, 31.6);
  ctx.quadraticCurveTo(47.5, 30.4, 44.5, 31.2);
  ctx.quadraticCurveTo(39.5, 32.5, 36.4, 42);
  ctx.closePath();
  ctx.fill();
  // 侧鬓垂绺（沿脸侧收窄）
  for (const dir of [-1, 1]) {
    ctx.beginPath();
    ctx.moveTo(FX + dir * 12.6, 33);
    ctx.quadraticCurveTo(FX + dir * 14.0, 40, FX + dir * 13.0, 48.5);
    ctx.lineTo(FX + dir * 11.4, 48.5);
    ctx.quadraticCurveTo(FX + dir * 12.2, 40.5, FX + dir * 11.2, 34);
    ctx.closePath();
    ctx.fill();
  }
  // 发丝分组：自顶心辐射 + 侧绺
  ctx.strokeStyle = 'rgba(158,114,72,0.42)';
  ctx.lineWidth = 0.75;
  const strands: [number, number, number, number][] = [
    [46, 17, 40, 26], [42.5, 19, 37.8, 30], [54, 17, 60, 26], [57.5, 19, 62.2, 30], [50, 15.5, 50, 28.5],
  ];
  for (const [cx0, cy0, x1, y1] of strands) {
    ctx.beginPath();
    ctx.moveTo(FX, 14.5);
    ctx.quadraticCurveTo(cx0, cy0, x1, y1);
    ctx.stroke();
  }
  for (const dir of [-1, 1]) {
    ctx.beginPath();
    ctx.moveTo(FX + dir * 12.4, 35.5);
    ctx.quadraticCurveTo(FX + dir * 13.4, 41, FX + dir * 12.6, 47);
    ctx.stroke();
  }
  // 顶发高光带（左侧主光）
  ctx.strokeStyle = 'rgba(255,232,196,0.20)';
  ctx.lineWidth = 2.2;
  ctx.beginPath();
  ctx.arc(FX, 26, 11, Math.PI * 1.18, Math.PI * 1.62);
  ctx.stroke();

  if (princess) {
    // 英气高髻：窄高 + 红绳束髻 + 双红缨穗 + 小金簪
    fillEllipse(ctx, FX, 11.5, 4.6, 6.2, hg);
    ctx.strokeStyle = 'rgba(150,110,70,0.4)';
    ctx.lineWidth = 0.75;
    ctx.beginPath();
    ctx.arc(FX, 11.5, 3.0, Math.PI * 1.15, Math.PI * 1.85);
    ctx.stroke();
    fillRoundRect(ctx, 45.8, 14.8, 8.4, 2.6, 1.3, '#B7261E');
    ctx.strokeStyle = '#B7261E';
    ctx.lineWidth = 1.2;
    ctx.beginPath();
    ctx.moveTo(53.2, 16.5);
    ctx.quadraticCurveTo(57.5, 21, 56.5, 26.5);
    ctx.moveTo(54.8, 16);
    ctx.quadraticCurveTo(60, 19.5, 59, 25);
    ctx.stroke();
    ctx.strokeStyle = '#E6BF33';
    ctx.lineWidth = 1.5;
    ctx.beginPath();
    ctx.moveTo(44, 11.5);
    ctx.lineTo(56, 8.5);
    ctx.stroke();
    fillCircle(ctx, 57, 8.3, 1.5, '#B7261E');
    return;
  }

  // 长孙皇后：圆高髻 + 朱绳 + 金钗红宝 + 耳坠 + 额间花钿
  fillEllipse(ctx, FX, 12, 6.6, 5.0, hg);
  ctx.strokeStyle = 'rgba(150,110,70,0.4)';
  ctx.lineWidth = 0.75;
  ctx.beginPath();
  ctx.arc(FX, 12, 4.0, Math.PI * 1.15, Math.PI * 1.85);
  ctx.stroke();
  fillRoundRect(ctx, 44.2, 15, 11.6, 2.8, 1.4, '#8A2A22');
  // 金钗
  ctx.strokeStyle = lin(ctx, 41, 0, 60, 0, [[0, '#A8861B'], [0.5, '#F2D675'], [1, '#A8861B']]);
  ctx.lineWidth = 1.8;
  ctx.beginPath();
  ctx.moveTo(41, 13.5);
  ctx.lineTo(59.5, 9);
  ctx.stroke();
  fillCircle(ctx, 60.5, 8.6, 1.9, '#B7261E');
  fillCircle(ctx, 59.9, 8.0, 0.7, 'rgba(255,255,255,0.7)');
  fillCircle(ctx, 40.2, 13.8, 1.2, '#E6BF33');
  // 耳坠（鬓下金环红珠）
  for (const x of [36.9, 63.1]) {
    fillCircle(ctx, x, 49.6, 0.9, '#E6BF33');
    fillCircle(ctx, x, 51.9, 1.2, '#B7261E');
    fillCircle(ctx, x - 0.35, 51.5, 0.4, 'rgba(255,255,255,0.65)');
  }
  // 额间花钿
  ctx.fillStyle = '#C0392B';
  ctx.beginPath();
  ctx.moveTo(FX, 33.4);
  ctx.lineTo(51.2, 34.8);
  ctx.lineTo(FX, 36.2);
  ctx.lineTo(48.8, 34.8);
  ctx.closePath();
  ctx.fill();
}

/** 主公专属：三点金冠（中尖最高，镶红宝，两侧金珠，座面錾刻） */
function drawCrown(ctx: Ctx): void {
  const gold = lin(ctx, 0, 8, 0, 30, [
    [0, '#F2D675'], [0.55, '#E6BF33'], [1, '#B8931F'],
  ]);
  ctx.fillStyle = gold;
  // 三尖（中高侧低，宽肩钝尖）
  for (const [x, h] of [[42.5, 6.2], [FX, 9.6], [57.5, 6.2]] as [number, number][]) {
    ctx.beginPath();
    ctx.moveTo(x - 4.4, 22.5);
    ctx.quadraticCurveTo(x - 1.8, 22.5 - h, x, 22.5 - h - 1.5);
    ctx.quadraticCurveTo(x + 1.8, 22.5 - h, x + 4.4, 22.5);
    ctx.closePath();
    ctx.fill();
  }
  // 冠座
  fillRoundRect(ctx, 38, 21.5, 24, 5.6, 2.4, gold);
  ctx.strokeStyle = '#A8861B';
  ctx.lineWidth = 0.8;
  pathRoundRect(ctx, 38, 21.5, 24, 5.6, 2.4);
  ctx.stroke();
  // 座面錾刻 + 顶沿高光
  ctx.strokeStyle = 'rgba(120,86,20,0.6)';
  ctx.lineWidth = 0.6;
  ctx.beginPath();
  ctx.moveTo(40, 23.6);
  ctx.lineTo(60, 23.6);
  ctx.moveTo(40, 25.6);
  ctx.lineTo(60, 25.6);
  ctx.stroke();
  ctx.strokeStyle = 'rgba(255,255,255,0.35)';
  ctx.lineWidth = 0.7;
  ctx.beginPath();
  ctx.moveTo(39.5, 22.2);
  ctx.lineTo(60.5, 22.2);
  ctx.stroke();
  // 红宝 + 高光，两侧金珠
  fillCircle(ctx, FX, 24.3, 2.0, '#B7261E');
  fillCircle(ctx, 49.3, 23.6, 0.75, 'rgba(255,255,255,0.7)');
  fillCircle(ctx, 41.5, 24.3, 1.1, '#F2D675');
  fillCircle(ctx, 58.5, 24.3, 1.1, '#F2D675');
}
