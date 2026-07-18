// ============================================================
// Canvas 2D 渲染器——flat-top 六边形网格（odd-q，与 core/hex.ts
// 坐标约定一致）、地形（每类地形带程序化装饰细节：松树/山峰/波浪/
// 城垛/砖缝/关门/帐篷/木桩等，按格坐标 hash 确定性微偏移）、范围高亮、路径预览、
// 迷你全身立绘单位（头盔/头巾 + 脸 + 躯干甲 + 腿 + 武器，兵种强剪影：
// 骑兵骑马 / 重步盾斧 / 步兵长枪 / 弓兵持弓 / 器械锤车 / 谋士长袍羽扇 /
// 矛兵超长矛 / 投石车投臂；按 兵种×阵营×步态帧 缓存 offscreen sprite
// 每帧 drawImage；移动中按兵种读 FxLayer 步态相位做奔腾/迈步/颠簸；
// 阵营底座圆环 己=金/敌=红/友=绿）、阵亡灰叉、飘字等特效层。
// PRD §17：受击屏抖（draw 时叠加偏移，不改 camera 状态）、刀光弧线、
// 计策粒子层、天气常驻粒子层（雨/雪/雾/风）、待机呼吸起伏、
// 营寨旗帜摆动、单位落地阴影。
// 纯 Canvas 程序化绘制，零外部资源；只负责画，不含任何战斗规则。
// ============================================================
import type { BattleState, ClassType, Faction, TerrainType, Unit, Weather } from '../core/types';
import type { Cell } from '../core/hex';
import type { Camera } from './camera';
import { WeatherLayer } from './particles';
import type { ParticleView } from './particles';
import { getPortrait, portraitImageReady } from '../ui/portraits';

// ---------- 六边形几何（odd-q / flat-top，公式同 redblobgames） ----------
export const HEX = 32; // 六边形外接圆半径（世界 px）
const SQRT3 = Math.sqrt(3);

/** 格 (q,r) 中心的世界坐标 */
export function cellToWorld(q: number, r: number): { x: number; y: number } {
  return {
    x: HEX + HEX * 1.5 * q,
    y: (HEX * SQRT3) / 2 + HEX * SQRT3 * (r + 0.5 * (q & 1)),
  };
}

/** 整张地图的世界像素尺寸 */
export function mapPixelSize(width: number, height: number): { w: number; h: number } {
  return { w: HEX * (1.5 * width + 0.5), h: HEX * SQRT3 * (height + 0.5) };
}

/** cube 小数坐标取整（redblobgames 标准算法） */
function cubeRound(x: number, y: number, z: number): { x: number; y: number; z: number } {
  let rx = Math.round(x);
  let ry = Math.round(y);
  let rz = Math.round(z);
  const dx = Math.abs(rx - x);
  const dy = Math.abs(ry - y);
  const dz = Math.abs(rz - z);
  if (dx > dy && dx > dz) rx = -ry - rz;
  else if (dy > dz) ry = -rx - ry;
  else rz = -rx - ry;
  return { x: rx, y: ry, z: rz };
}

/**
 * 世界坐标 → 格。命中判定半径放大 1.15×（H5_DESIGN §9：点不准格子的对策）。
 * 越界或距格心过远返回 null。
 */
export function worldToCell(wx: number, wy: number, width: number, height: number): Cell | null {
  const x = wx - HEX;
  const y = wy - (HEX * SQRT3) / 2;
  const qf = ((2 / 3) * x) / HEX;
  const rf = ((-1 / 3) * x + (SQRT3 / 3) * y) / HEX;
  const c = cubeRound(qf, -qf - rf, rf);
  const q = c.x;
  const r = c.z + (c.x - (c.x & 1)) / 2;
  if (q < 0 || q >= width || r < 0 || r >= height) return null;
  const center = cellToWorld(q, r);
  const dx = wx - center.x;
  const dy = wy - center.y;
  if (dx * dx + dy * dy > HEX * 1.15 * (HEX * 1.15)) return null;
  return { q, r };
}

/** 格内确定性伪随机（装饰微偏移用）：同格同 salt 恒定，避免整齐划一的假感 */
function cellRand(q: number, r: number, salt: number): number {
  let h = (q * 374761393 + r * 668265263 + salt * 1442695041) | 0;
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
}

// ---------- 色板 ----------
// 地形底色（第三轮迭代：拉开色相/明度差——平原暖米黄/林地深绿/山地冷灰褐/
// 水域湛蓝/城池赭石/城墙深灰蓝/关隘冷灰/营寨沙褐/栅栏土黄，相邻不同地形边界一眼可分）
const TERRAIN_COLORS: Record<TerrainType, string> = {
  plain: '#EAD9A8',
  forest: '#39603F',
  mountain: '#7C7268',
  water: '#3D7EC2',
  city: '#A06528',
  wall: '#4C5A70',
  pass: '#828A94',
  camp: '#D9C391',
  fence: '#C7A45C',
};
// 径向渐变高光色（格心稍亮，增强_tile_立体感）
const TERRAIN_HIGHLIGHT: Record<TerrainType, string> = {
  plain: '#F5ECD1',
  forest: '#4A7A50',
  mountain: '#9A9080',
  water: '#5B9BDC',
  city: '#B87A34',
  wall: '#5E6E84',
  pass: '#9AA2AC',
  camp: '#EBD9A8',
  fence: '#D9B878',
};
// 六边形描边：各地形同色系深色（替代统一暗描边，强化地形区分度）
const TERRAIN_STROKE: Record<TerrainType, string> = {
  plain: '#C4A96B',
  forest: '#24422B',
  mountain: '#564E45',
  water: '#24568C',
  city: '#6E4218',
  wall: '#333E50',
  pass: '#5A626C',
  camp: '#B09A68',
  fence: '#8E7038',
};
// 兵种色（沿用 Unity Theme.cs 实装值；梯形甲身用；新增兵种暂取近亲色系）
const CLASS_COLORS: Record<ClassType, string> = {
  infantry: '#808080',
  heavy: '#66594D',
  cavalry: '#B34D1A',
  archer: '#1A8C33',
  siege: '#66408C',
  strategist: '#1A66B3',
  spear: '#6E7B3A',
  catapult: '#503A6E',
};
// 头像模式下用于右下角兵种徽章的单字
const CLASS_BADGE: Record<ClassType, string> = {
  infantry: '步', heavy: '重', cavalry: '骑', archer: '弓',
  siege: '器', strategist: '谋', spear: '矛', catapult: '车',
};
// 阵营描边（任务书：己方金边/敌方红边）
const FACTION_STROKE: Record<Faction, string> = {
  player: '#E6BF33',
  enemy: '#C23A30',
  ally: '#5B8A72',
};
// 范围高亮（06-ui-ux §2.3 状态色）
const COLOR_REACH = 'rgba(47,79,79,0.42)'; // 可达格（青蓝）
const COLOR_ATTACK = 'rgba(194,58,48,0.42)'; // 可攻击格（红）
const COLOR_SKILL = 'rgba(138,43,226,0.42)'; // 计策格（紫）
const COLOR_PATH = '#E6BF33'; // 路径预览线（金）
// HP 条（PRD §9.4 功能色）
const HP_GREEN = '#33CC33';
const HP_YELLOW = '#D9B31A';
const HP_RED = '#D9261A';
// 天气图标
export const WEATHER_ICONS: Record<Weather, string> = {
  sunny: '☀',
  rain: '🌧',
  snow: '❄',
  fog: '🌫',
  windy: '💨',
};
// 负面 buff（头顶标记用红色，其余增益用金色）
const DEBUFF_IDS = new Set(['ignite', 'slow', 'confuse', 'insight_expose']);

// ---------- 特效层接口（由 anim.ts 的 Animator 实现，避免循环依赖） ----------
export interface UnitVisual {
  x: number;
  y: number;
  alpha: number;
  flash: number; // 0-1 受击闪强度
  flashColor: string; // 受击闪色（白=普通，红=暴击）
  movePhase: number | null; // 移动中步态相位 0-1（一个步态循环），静止为 null
}
export interface FloaterView {
  x: number;
  y: number;
  text: string;
  color: string;
  size: number;
  alpha: number;
}
export interface CellFlashView {
  cells: Cell[];
  color: string;
  alpha: number;
}
export interface DyingView {
  x: number;
  y: number;
  alpha: number; // 渐隐进度
  crossed: boolean; // 渐隐完成后显示灰叉
  classType: ClassType;
  faction: Faction;
  charId: string;
}
/** 刀光弧线（攻击命中瞬间，扫过 120°） */
export interface SlashView {
  x: number;
  y: number;
  angle: number; // 攻击方向（rad）
  t: number; // 0-1 进度
  alpha: number;
}
export interface FxLayer {
  unitVisual(uid: string): UnitVisual | undefined;
  floaters(): FloaterView[];
  cellFlashes(): CellFlashView[];
  dying(): DyingView[];
  slashes(): SlashView[];
  particles(): ParticleView[];
  /** 屏抖偏移（屏幕 px，draw 时叠加，不改变 camera 状态） */
  shake(): { x: number; y: number };
  /** 暴击屏红闪（屏幕空间整帧叠加），无则 null */
  screenFlash(): { color: string; alpha: number } | null;
  /** 动画时钟（ms）：呼吸/旗帜/天气等常驻动效的时间基准 */
  time(): number;
}

/** 渲染视图状态：当前该画哪些高亮/标记（由 battleScene 按交互模式组装） */
export interface ViewState {
  selectedUid: string | null;
  reachable: Cell[]; // 可达格（蓝）
  attackable: Cell[]; // 可攻击的敌人格（红圈）
  skillCells: Cell[]; // 计策范围/影响格（紫）
  pathPreview: Cell[]; // 路径预览（含起点）
  targetCell: Cell | null; // 计策目标格
  previewTargetUid: string | null; // 攻击预览目标（红圈）
  blinkAttack: boolean; // 可攻击红圈闪烁（选目标/攻击预览时）
}

export const EMPTY_VIEW: ViewState = {
  selectedUid: null,
  reachable: [],
  attackable: [],
  skillCells: [],
  pathPreview: [],
  targetCell: null,
  previewTargetUid: null,
  blinkAttack: false,
};

// ---------- 迷你全身立绘 sprite（按 兵种×阵营 缓存 offscreen，每帧 drawImage） ----------
const SPR_W = 56; // 立绘逻辑宽（世界 px）
const SPR_H = 48; // 立绘逻辑高 ≈ HEX×1.5（曹操传式地图小兵比例）
const SPR_SS = 3; // 超采样倍率（相机缩小时更平滑）
const SPR_FEET = 43.5; // 脚底基线（box 内 y 坐标）
const PORTRAIT_SIZE = 48; // 战场 AI 头像绘制直径（世界 px）
const SKIN = '#F0C9A2';
const SKIN_LINE = 'rgba(120,80,50,0.75)';
const LINE = 'rgba(26,20,13,0.6)'; // 通用深色描边
const WOOD = '#6B4A2F';
const STEEL = '#B9BEC6';
const STEEL_DARK = '#7E848A';

/** 脸：肤色圆 + 双眼点（全兵种通用） */
function drawFace(g: CanvasRenderingContext2D, cx: number, cy: number, r: number): void {
  g.beginPath();
  g.arc(cx, cy, r, 0, Math.PI * 2);
  g.fillStyle = SKIN;
  g.fill();
  g.strokeStyle = SKIN_LINE;
  g.lineWidth = 0.8;
  g.stroke();
  g.fillStyle = '#2E2015';
  g.beginPath();
  g.arc(cx - r * 0.36, cy - r * 0.02, r * 0.11, 0, Math.PI * 2);
  g.arc(cx + r * 0.36, cy - r * 0.02, r * 0.11, 0, Math.PI * 2);
  g.fill();
}

/** 两条腿 + 鞋（步战兵种通用，裤色按兵种微调）；step=步态帧（0 站立，1/3 左右腿交替前迈） */
function drawLegs(g: CanvasRenderingContext2D, cx: number, top: number, color: string, step = 0): void {
  const ldx = step === 1 ? 1.8 : step === 3 ? -1.2 : 0; // 前迈/后收的水平偏移
  const rdx = step === 3 ? 1.8 : step === 1 ? -1.2 : 0;
  const lLift = step === 1 ? 1.8 : 0; // 前迈腿略抬（裤腿与鞋变短）
  const rLift = step === 3 ? 1.8 : 0;
  g.fillStyle = color;
  g.fillRect(cx - 3.9 + ldx, top, 3.2, SPR_FEET - 2.5 - lLift - top);
  g.fillRect(cx + 0.7 + rdx, top, 3.2, SPR_FEET - 2.5 - rLift - top);
  g.fillStyle = '#2E2015';
  g.fillRect(cx - 4.3 + ldx * 1.4, SPR_FEET - 2.8 - lLift, 4, 2.4);
  g.fillRect(cx + 0.3 + rdx * 1.4, SPR_FEET - 2.8 - rLift, 4, 2.4);
}

/** 木轮（轮毂 + 辐条；rot 为辐条旋转角，器械/投石车移动时滚动示意） */
function drawWheel(g: CanvasRenderingContext2D, wx: number, wy: number, r: number, rot = 0): void {
  g.beginPath();
  g.arc(wx, wy, r, 0, Math.PI * 2);
  g.fillStyle = '#8A6A42';
  g.fill();
  g.strokeStyle = '#3A2A18';
  g.lineWidth = 1.4;
  g.stroke();
  g.beginPath();
  for (const a of [rot, rot + Math.PI / 2]) {
    g.moveTo(wx - r * Math.cos(a), wy - r * Math.sin(a));
    g.lineTo(wx + r * Math.cos(a), wy + r * Math.sin(a));
  }
  g.stroke();
  g.beginPath();
  g.arc(wx, wy, 1.2, 0, Math.PI * 2);
  g.fillStyle = '#3A2A18';
  g.fill();
}

/** 梯形躯干甲（兵种色）+ 深色描边 + 腰带 */
function drawTorso(
  g: CanvasRenderingContext2D,
  cx: number,
  shoulderY: number,
  waistY: number,
  hwTop: number,
  hwBot: number,
  color: string,
): void {
  g.beginPath();
  g.moveTo(cx - hwTop, shoulderY);
  g.lineTo(cx + hwTop, shoulderY);
  g.lineTo(cx + hwBot, waistY);
  g.lineTo(cx - hwBot, waistY);
  g.closePath();
  g.fillStyle = color;
  g.fill();
  g.strokeStyle = LINE;
  g.lineWidth = 1;
  g.stroke();
  g.fillStyle = 'rgba(26,20,13,0.5)';
  g.fillRect(cx - hwBot, waistY - 3.4, hwBot * 2, 2.2); // 腰带
}

/** 兵种身体（box 56×48，脚底 SPR_FEET；C=兵种甲色；step=步态帧 0-3，仅移动动画传非 0） */
function drawClassBody(g: CanvasRenderingContext2D, classType: ClassType, step = 0): void {
  const C = CLASS_COLORS[classType];
  switch (classType) {
    case 'infantry': {
      // 步兵：皮帽 + 长枪斜持（长剪影）
      drawLegs(g, 28, 29, '#4A3A28', step);
      drawTorso(g, 28, 17, 31, 6, 4.6, C);
      g.strokeStyle = C;
      g.lineWidth = 2.4;
      g.beginPath();
      g.moveTo(22.6, 19.5);
      g.lineTo(20, 26);
      g.moveTo(33.4, 19.5);
      g.lineTo(36.5, 23.5);
      g.stroke();
      drawFace(g, 28, 10.8, 6.2);
      g.fillStyle = '#8A5A33'; // 皮帽（矮半球 + 帽带）
      g.beginPath();
      g.arc(28, 10.6, 6.6, Math.PI * 1.02, -Math.PI * 0.02);
      g.closePath();
      g.fill();
      g.fillStyle = '#5E3A1E';
      g.fillRect(21.6, 8.9, 12.8, 1.6);
      g.strokeStyle = WOOD; // 长枪：木杆 + 钢尖
      g.lineWidth = 1.9;
      g.beginPath();
      g.moveTo(37.5, 41);
      g.lineTo(45.5, 5.5);
      g.stroke();
      g.fillStyle = STEEL;
      g.strokeStyle = LINE;
      g.lineWidth = 0.8;
      g.beginPath();
      g.moveTo(46.3, 0.8);
      g.lineTo(48.6, 6.4);
      g.lineTo(44, 5.2);
      g.closePath();
      g.fill();
      g.stroke();
      return;
    }
    case 'spear': {
      // 矛兵：束甲直立士兵 + 超长矛斜持（矛身明显超步兵长枪，红缨枪头）
      drawLegs(g, 28, 29, '#3E3626', step);
      drawTorso(g, 28, 17, 31, 5.8, 4.4, C);
      g.strokeStyle = 'rgba(0,0,0,0.22)'; // 甲片横线
      g.lineWidth = 0.9;
      g.beginPath();
      g.moveTo(22.4, 23);
      g.lineTo(33.6, 23);
      g.stroke();
      g.strokeStyle = C; // 双臂前伸握矛
      g.lineWidth = 2.3;
      g.beginPath();
      g.moveTo(23, 19.5);
      g.lineTo(30.5, 26.5);
      g.moveTo(33, 19.5);
      g.lineTo(35.5, 21.5);
      g.stroke();
      drawFace(g, 28, 10.8, 6.2);
      g.fillStyle = '#4E5642'; // 尖顶皮盔（半球 + 尖顶 + 护颈沿）
      g.beginPath();
      g.arc(28, 10.4, 6.6, Math.PI, 0);
      g.closePath();
      g.fill();
      g.beginPath();
      g.moveTo(26, 4.6);
      g.lineTo(28, 1.2);
      g.lineTo(30, 4.6);
      g.closePath();
      g.fill();
      g.fillStyle = '#39402E';
      g.fillRect(21.2, 9.6, 13.6, 1.7);
      g.fillStyle = '#B7261E'; // 盔顶红缨
      g.fillRect(27, 0.2, 2, 2.2);
      g.strokeStyle = WOOD; // 超长矛：木杆斜持（近对角线，明显长过步兵枪）
      g.lineWidth = 2;
      g.beginPath();
      g.moveTo(30.5, 44);
      g.lineTo(49.5, 4.5);
      g.stroke();
      g.fillStyle = '#B7261E'; // 红缨（枪尖下一小簇）
      g.beginPath();
      g.moveTo(48.6, 6.2);
      g.lineTo(46.4, 9.8);
      g.lineTo(50.2, 8.6);
      g.closePath();
      g.fill();
      g.fillStyle = STEEL; // 长钢尖
      g.strokeStyle = LINE;
      g.lineWidth = 0.8;
      g.beginPath();
      g.moveTo(50.3, 0.4);
      g.lineTo(52.8, 6.2);
      g.lineTo(48.2, 5);
      g.closePath();
      g.fill();
      g.stroke();
      return;
    }
    case 'heavy': {
      // 重步：圆铁盔 + 大矩形盾 + 斧
      drawLegs(g, 28, 29, '#3A332B', step);
      drawTorso(g, 28, 17, 31, 7, 5.2, C);
      g.strokeStyle = 'rgba(0,0,0,0.22)'; // 甲片横线
      g.lineWidth = 0.9;
      g.beginPath();
      g.moveTo(22, 22);
      g.lineTo(34, 22);
      g.moveTo(21.6, 26);
      g.lineTo(34.4, 26);
      g.stroke();
      drawFace(g, 28, 10.8, 6.2);
      g.fillStyle = STEEL_DARK; // 圆铁盔（半球 + 帽檐 + 护鼻）
      g.beginPath();
      g.arc(28, 10.4, 7, Math.PI, 0);
      g.closePath();
      g.fill();
      g.fillRect(20.8, 9.4, 14.4, 1.9);
      g.fillRect(27.3, 10.8, 1.4, 3.2);
      g.fillStyle = '#6E4A2A'; // 大盾（左，木底钢边 + 盾心）
      g.fillRect(9, 15, 12.5, 24);
      g.strokeStyle = STEEL_DARK;
      g.lineWidth = 2.2;
      g.strokeRect(9, 15, 12.5, 24);
      g.fillStyle = STEEL;
      g.beginPath();
      g.arc(15.2, 26.5, 2.5, 0, Math.PI * 2);
      g.fill();
      g.strokeStyle = WOOD; // 斧（右）：杆 + 钢刃
      g.lineWidth = 2;
      g.beginPath();
      g.moveTo(38, 40);
      g.lineTo(40, 13);
      g.stroke();
      g.fillStyle = STEEL;
      g.strokeStyle = LINE;
      g.lineWidth = 0.8;
      g.beginPath();
      g.moveTo(39.4, 12);
      g.lineTo(48, 15.6);
      g.lineTo(48, 21);
      g.lineTo(39.4, 23.4);
      g.closePath();
      g.fill();
      g.stroke();
      return;
    }
    case 'cavalry': {
      // 骑兵：骑马（马身椭圆 + 四腿 + 马头 + 骑手 + 长槊），最重要剪影
      const HORSE = '#7A4A28';
      const HORSE_D = '#4A2E18';
      // 马腿 4 帧奔腾循环（对角腿成组：帧间前后腿交替前甩/后收，抬起的腿缩短）
      const GALLOP = [
        [2.2, -0.8, -1.6, 1.8],
        [0.8, 2.0, 0.6, -1.8],
        [-2.0, 0.8, 2.0, -0.6],
        [-0.8, -2.0, -0.8, 2.2],
      ];
      const gs = GALLOP[step % 4];
      const LEG_TOPS: Array<[number, number]> = [
        [17, 33],
        [20.5, 34],
        [32.5, 34],
        [36, 33],
      ];
      const LEG_BOTS: Array<[number, number]> = [
        [15.3, 42.8],
        [20.5, 43],
        [33.5, 43],
        [38.3, 42.4],
      ];
      g.strokeStyle = '#5A3A20'; // 四腿（先画腿，身体盖住顶端）
      g.lineWidth = 2.6;
      g.beginPath();
      for (let i = 0; i < 4; i++) {
        const lift = Math.max(0, -gs[i]); // 后收的腿抬起
        g.moveTo(LEG_TOPS[i][0], LEG_TOPS[i][1]);
        g.lineTo(LEG_BOTS[i][0] + gs[i], LEG_BOTS[i][1] - lift);
      }
      g.stroke();
      g.strokeStyle = HORSE_D; // 尾
      g.lineWidth = 2.2;
      g.beginPath();
      g.moveTo(14, 27);
      g.quadraticCurveTo(9.5, 32, 9.5, 38);
      g.stroke();
      g.beginPath(); // 马身
      g.ellipse(25.5, 30.5, 12.5, 6.2, 0, 0, Math.PI * 2);
      g.fillStyle = HORSE;
      g.fill();
      g.strokeStyle = HORSE_D;
      g.lineWidth = 1;
      g.stroke();
      g.beginPath(); // 颈
      g.moveTo(32.5, 28.5);
      g.lineTo(38, 14.5);
      g.lineTo(44.5, 15);
      g.lineTo(40, 29);
      g.closePath();
      g.fillStyle = HORSE;
      g.fill();
      g.stroke();
      g.beginPath(); // 头
      g.moveTo(38.6, 13.2);
      g.lineTo(46.8, 15.4);
      g.lineTo(45, 19.4);
      g.lineTo(38, 18);
      g.closePath();
      g.fillStyle = HORSE;
      g.fill();
      g.stroke();
      g.beginPath(); // 耳
      g.moveTo(39.4, 13.4);
      g.lineTo(40.8, 10.2);
      g.lineTo(42.4, 13.2);
      g.closePath();
      g.fill();
      g.strokeStyle = HORSE_D; // 鬃
      g.lineWidth = 2.2;
      g.beginPath();
      g.moveTo(34, 26.5);
      g.lineTo(39, 14);
      g.stroke();
      g.fillStyle = '#1F140D'; // 马眼
      g.beginPath();
      g.arc(43.4, 15.6, 0.8, 0, Math.PI * 2);
      g.fill();
      g.fillStyle = '#8C2B22'; // 鞍
      g.fillRect(21.8, 24.6, 8, 3.2);
      g.strokeStyle = LINE;
      g.lineWidth = 0.8;
      g.strokeRect(21.8, 24.6, 8, 3.2);
      g.strokeStyle = '#3A2E22'; // 骑手腿（跨骑）
      g.lineWidth = 2.4;
      g.beginPath();
      g.moveTo(23.5, 23.5);
      g.lineTo(21.8, 29.5);
      g.moveTo(30, 23.5);
      g.lineTo(31.8, 29.5);
      g.stroke();
      g.beginPath(); // 骑手躯干
      g.moveTo(22.4, 13);
      g.lineTo(31.6, 13);
      g.lineTo(32.2, 24);
      g.lineTo(21.8, 24);
      g.closePath();
      g.fillStyle = C;
      g.fill();
      g.strokeStyle = LINE;
      g.lineWidth = 1;
      g.stroke();
      g.strokeStyle = C; // 持缰手臂
      g.lineWidth = 2.2;
      g.beginPath();
      g.moveTo(30.5, 15.5);
      g.lineTo(35, 20.5);
      g.stroke();
      drawFace(g, 27, 8.8, 4.4);
      g.fillStyle = '#9AA0A6'; // 尖顶盔
      g.beginPath();
      g.moveTo(22.7, 7.2);
      g.lineTo(27, 0.8);
      g.lineTo(31.3, 7.2);
      g.closePath();
      g.fill();
      g.fillStyle = STEEL_DARK;
      g.fillRect(22.3, 6.6, 9.4, 1.3);
      g.strokeStyle = WOOD; // 长槊
      g.lineWidth = 1.8;
      g.beginPath();
      g.moveTo(33.5, 22);
      g.lineTo(47, 4);
      g.stroke();
      g.fillStyle = STEEL;
      g.strokeStyle = LINE;
      g.lineWidth = 0.7;
      g.beginPath();
      g.moveTo(47.9, 0.6);
      g.lineTo(49.4, 4.8);
      g.lineTo(45.7, 3.8);
      g.closePath();
      g.fill();
      g.stroke();
      return;
    }
    case 'archer': {
      // 弓兵：束发 + 持弓（弓弧 + 弦 + 搭箭）+ 背箭囊
      drawLegs(g, 28, 29, '#3E3524');
      drawTorso(g, 28, 17, 31, 5.6, 4.4, C);
      g.strokeStyle = 'rgba(0,0,0,0.3)'; // 斜挎带
      g.lineWidth = 1.6;
      g.beginPath();
      g.moveTo(23, 17.5);
      g.lineTo(32.5, 30.5);
      g.stroke();
      g.strokeStyle = C; // 持弓手臂前伸
      g.lineWidth = 2.4;
      g.beginPath();
      g.moveTo(23, 19.5);
      g.lineTo(16.5, 20.5);
      g.stroke();
      drawFace(g, 28, 10.8, 6.2);
      g.fillStyle = '#2E2015'; // 束发小髻
      g.beginPath();
      g.arc(28, 3.9, 2, 0, Math.PI * 2);
      g.fill();
      g.fillStyle = '#8C2B22'; // 红束带
      g.fillRect(22, 6.6, 12, 1.7);
      g.strokeStyle = WOOD; // 弓弧
      g.lineWidth = 2.1;
      g.beginPath();
      g.moveTo(15.5, 6.5);
      g.quadraticCurveTo(8.5, 19, 15.5, 31.5);
      g.stroke();
      g.strokeStyle = '#E8E2CE'; // 弦
      g.lineWidth = 0.8;
      g.beginPath();
      g.moveTo(15.5, 6.5);
      g.lineTo(15.5, 31.5);
      g.stroke();
      g.strokeStyle = WOOD; // 搭箭
      g.lineWidth = 1;
      g.beginPath();
      g.moveTo(14, 19);
      g.lineTo(30.5, 19);
      g.stroke();
      g.fillStyle = STEEL;
      g.beginPath();
      g.moveTo(33, 19);
      g.lineTo(29.8, 17.4);
      g.lineTo(29.8, 20.6);
      g.closePath();
      g.fill();
      g.fillStyle = '#5A3A22'; // 箭囊
      g.fillRect(35, 13.5, 3.8, 13);
      g.strokeStyle = LINE;
      g.lineWidth = 0.8;
      g.strokeRect(35, 13.5, 3.8, 13);
      g.strokeStyle = '#E8E2CE';
      g.lineWidth = 1;
      g.beginPath();
      g.moveTo(36, 13.5);
      g.lineTo(36, 10);
      g.moveTo(37.8, 13.5);
      g.lineTo(37.8, 10.6);
      g.stroke();
      return;
    }
    case 'siege': {
      // 器械：小型器械车（木轮×2 + 立柱大锤）+ 推车士兵
      drawLegs(g, 17, 29, '#413748');
      drawTorso(g, 17, 17, 30.5, 5, 4, C);
      g.strokeStyle = C; // 扶车臂
      g.lineWidth = 2.2;
      g.beginPath();
      g.moveTo(21.4, 20);
      g.lineTo(27, 26.5);
      g.stroke();
      drawFace(g, 17, 11.2, 5.6);
      g.fillStyle = '#5E6E5A'; // 布帽
      g.beginPath();
      g.moveTo(12.2, 8.6);
      g.lineTo(21.8, 8.6);
      g.lineTo(20.6, 4.2);
      g.lineTo(13.4, 4.2);
      g.closePath();
      g.fill();
      g.strokeStyle = WOOD; // 车把
      g.lineWidth = 2.2;
      g.beginPath();
      g.moveTo(24, 27);
      g.lineTo(30.5, 32);
      g.stroke();
      g.fillStyle = '#7A5A36'; // 车底板
      g.fillRect(27.5, 30, 20.5, 3.6);
      g.strokeStyle = LINE;
      g.lineWidth = 0.9;
      g.strokeRect(27.5, 30, 20.5, 3.6);
      g.strokeStyle = WOOD; // 立柱 + 斜撑
      g.lineWidth = 2.4;
      g.beginPath();
      g.moveTo(41.5, 30);
      g.lineTo(41.5, 15);
      g.stroke();
      g.lineWidth = 1.6;
      g.beginPath();
      g.moveTo(41.5, 21);
      g.lineTo(33, 30.5);
      g.stroke();
      g.fillStyle = '#5A5F66'; // 锤头（钢制 + 钉）
      g.fillRect(35.8, 10.5, 11.4, 7.5);
      g.strokeStyle = LINE;
      g.lineWidth = 1;
      g.strokeRect(35.8, 10.5, 11.4, 7.5);
      g.fillStyle = STEEL;
      g.beginPath();
      g.arc(39, 14.2, 1, 0, Math.PI * 2);
      g.arc(44, 14.2, 1, 0, Math.PI * 2);
      g.fill();
      for (const wx of [31.5, 44.5]) {
        // 木轮（轮毂 + 辐条，移动时随步态帧滚动）
        drawWheel(g, wx, 38, 4.6, (step * Math.PI) / 4);
      }
      return;
    }
    case 'catapult': {
      // 投石车：木质车架 + 双轮 + A 字支架 + 投臂（配重石 + 石弹），无士兵
      const WOOD_D = '#4A3018';
      const WOOD_L = '#8A6A42';
      // 车架底梁（纵梁 + 横梁）
      g.fillStyle = '#7A5A36';
      g.fillRect(8, 32.5, 40, 4);
      g.strokeStyle = LINE;
      g.lineWidth = 0.9;
      g.strokeRect(8, 32.5, 40, 4);
      g.fillRect(12, 29.5, 32, 3);
      g.strokeRect(12, 29.5, 32, 3);
      // 兵种色饰带（车架横条，标识兵种甲色）
      g.fillStyle = C;
      g.fillRect(8, 34.6, 40, 1.6);
      // A 字支架（两斜撑 + 顶轴）
      g.strokeStyle = WOOD_D;
      g.lineWidth = 2.6;
      g.beginPath();
      g.moveTo(17, 32);
      g.lineTo(28, 13.5);
      g.moveTo(39, 32);
      g.lineTo(28, 13.5);
      g.stroke();
      g.strokeStyle = WOOD_L; // 支架横档
      g.lineWidth = 1.6;
      g.beginPath();
      g.moveTo(20.5, 24.5);
      g.lineTo(35.5, 24.5);
      g.stroke();
      // 投臂（长端上扬带勺与石弹，短端挂配重石；绕顶轴）
      g.strokeStyle = WOOD_L;
      g.lineWidth = 2.8;
      g.beginPath();
      g.moveTo(34, 23.5);
      g.lineTo(12.5, 6.5);
      g.stroke();
      g.beginPath(); // 顶轴
      g.arc(28, 13.5, 2.2, 0, Math.PI * 2);
      g.fillStyle = WOOD_D;
      g.fill();
      g.fillStyle = '#5A5F66'; // 配重石（短端方石块）
      g.fillRect(30.5, 21.5, 7, 6.5);
      g.strokeStyle = LINE;
      g.lineWidth = 0.9;
      g.strokeRect(30.5, 21.5, 7, 6.5);
      g.strokeStyle = WOOD_D; // 弹勺（长端小弧斗）
      g.lineWidth = 1.6;
      g.beginPath();
      g.arc(12.5, 5.5, 3.4, 0.4, Math.PI * 1.4);
      g.stroke();
      g.beginPath(); // 石弹
      g.arc(12.5, 4.6, 2.7, 0, Math.PI * 2);
      g.fillStyle = '#7E848A';
      g.fill();
      g.strokeStyle = LINE;
      g.lineWidth = 0.8;
      g.stroke();
      // 绞盘（车架前部小圆盘 + 手柄）
      g.beginPath();
      g.arc(44, 30.5, 2.6, 0, Math.PI * 2);
      g.fillStyle = WOOD_D;
      g.fill();
      g.strokeStyle = WOOD_D;
      g.lineWidth = 1.2;
      g.beginPath();
      g.moveTo(44, 30.5);
      g.lineTo(47.5, 27.5);
      g.stroke();
      // 双轮（移动时随步态帧滚动）
      for (const wx of [16, 40]) {
        drawWheel(g, wx, 40.5, 5.4, (step * Math.PI) / 4);
      }
      return;
    }
    case 'strategist': {
      // 谋士：长袍（无盔，文士巾）+ 羽扇
      g.beginPath(); // 长袍（遮腿大梯形）
      g.moveTo(22, 16.5);
      g.lineTo(34, 16.5);
      g.lineTo(38.5, SPR_FEET - 1);
      g.lineTo(17.5, SPR_FEET - 1);
      g.closePath();
      g.fillStyle = C;
      g.fill();
      g.strokeStyle = LINE;
      g.lineWidth = 1;
      g.stroke();
      g.strokeStyle = 'rgba(0,0,0,0.22)'; // 衣摆中缝
      g.lineWidth = 0.9;
      g.beginPath();
      g.moveTo(28, 27);
      g.lineTo(28, SPR_FEET - 1.5);
      g.stroke();
      g.fillStyle = '#123A5E'; // 腰带
      g.fillRect(22.8, 24.5, 10.4, 2.4);
      g.strokeStyle = '#D8E2EC'; // 交领
      g.lineWidth = 1.2;
      g.beginPath();
      g.moveTo(24, 16.8);
      g.lineTo(28, 21.6);
      g.lineTo(32, 16.8);
      g.stroke();
      drawFace(g, 28, 10.8, 6.2);
      g.strokeStyle = '#3A2E22'; // 胡须（两撇 + 山羊胡）
      g.lineWidth = 0.9;
      g.beginPath();
      g.moveTo(25.4, 14.6);
      g.lineTo(27.2, 15.4);
      g.moveTo(30.6, 14.6);
      g.lineTo(28.8, 15.4);
      g.moveTo(28, 16.6);
      g.lineTo(28, 19.6);
      g.stroke();
      g.fillStyle = '#2E445C'; // 文士巾（平顶高巾 + 顶沿）
      g.fillRect(22.8, 2.8, 10.4, 5);
      g.fillStyle = '#3E5A78';
      g.fillRect(22, 2, 12, 1.8);
      g.strokeStyle = '#3E5A78'; // 巾带（两侧飘带）
      g.lineWidth = 1.2;
      g.beginPath();
      g.moveTo(23.4, 7.6);
      g.lineTo(21.8, 11.5);
      g.moveTo(32.6, 7.6);
      g.lineTo(34.2, 11.5);
      g.stroke();
      g.strokeStyle = WOOD; // 扇柄
      g.lineWidth = 1.5;
      g.beginPath();
      g.moveTo(33.5, 24);
      g.lineTo(36.5, 26);
      g.stroke();
      g.beginPath(); // 羽扇扇面（浅色扇形）
      g.moveTo(36.5, 26);
      g.arc(36.5, 26, 9, -1.35, -0.3);
      g.closePath();
      g.fillStyle = '#E8E2CE';
      g.fill();
      g.strokeStyle = LINE;
      g.lineWidth = 0.8;
      g.stroke();
      g.strokeStyle = '#B8AE92'; // 扇骨
      g.lineWidth = 0.7;
      for (const a of [-1.15, -0.85, -0.55]) {
        g.beginPath();
        g.moveTo(36.5, 26);
        g.lineTo(36.5 + 8.6 * Math.cos(a), 26 + 8.6 * Math.sin(a));
        g.stroke();
      }
      return;
    }
  }
}

/** 构建兵种×阵营×步态帧 立绘 sprite（阵营底座圆环 + 兵种身体），3× 超采样 */
function buildUnitSprite(classType: ClassType, faction: Faction, step: number): HTMLCanvasElement {
  const c = document.createElement('canvas');
  c.width = SPR_W * SPR_SS;
  c.height = SPR_H * SPR_SS;
  const g = c.getContext('2d');
  if (!g) return c;
  g.scale(SPR_SS, SPR_SS);
  g.lineJoin = 'round';
  g.lineCap = 'round';
  // 阵营底座圆环（己=金 / 敌=红 / 友=绿）
  g.beginPath();
  g.ellipse(SPR_W / 2, SPR_FEET + 0.5, 15.5, 4.4, 0, 0, Math.PI * 2);
  g.fillStyle = 'rgba(31,20,13,0.3)';
  g.fill();
  g.strokeStyle = FACTION_STROKE[faction];
  g.lineWidth = 2.4;
  g.stroke();
  drawClassBody(g, classType, step);
  return c;
}

/**
 * 移动中兵种动作（§16.2）：ph=步态相位 0-1。
 * 返回 y 偏移 / 旋转角（rad）/ 步态帧（0-3，用于选 sprite 变体）。
 * 骑兵奔腾起伏+马腿循环；步兵/重步/矛兵迈步顿挫 bob；弓/谋平稳滑步；
 * 器械/投石车滚动颠簸+车轮转动。
 */
function moveAnim(classType: ClassType, ph: number): { dy: number; rot: number; frame: number } {
  const s = Math.sin(ph * Math.PI * 2);
  switch (classType) {
    case 'cavalry':
      return { dy: -Math.abs(s) * 2.4, rot: s * 0.06, frame: Math.floor(ph * 4) % 4 };
    case 'infantry':
    case 'heavy':
    case 'spear':
      // 顿挫感：bob 用 abs(sin) 两步频，帧 0/1/2/3 对应 站/左迈/站/右迈
      return { dy: -Math.abs(s) * 1.7, rot: s * 0.05, frame: Math.floor(ph * 4) % 4 };
    case 'archer':
    case 'strategist':
      return { dy: s * 0.7, rot: 0, frame: 0 };
    case 'siege':
    case 'catapult':
      return { dy: Math.sin(ph * Math.PI * 6) * 0.9, rot: Math.sin(ph * Math.PI * 6) * 0.022, frame: Math.floor(ph * 4) % 4 };
  }
}

// ---------- 渲染器 ----------
export class BattleRenderer {
  private ctx: CanvasRenderingContext2D;
  private dpr = 1;
  private cw = 1; // CSS 宽
  private ch = 1; // CSS 高
  private readonly weather = new WeatherLayer(); // 天气常驻粒子层（无状态）

  constructor(private canvas: HTMLCanvasElement) {
    const ctx = canvas.getContext('2d');
    if (!ctx) throw new Error('Canvas 2D 不可用');
    this.ctx = ctx;
  }

  /** 视口尺寸变化时调用（CSS px） */
  resize(w: number, h: number): void {
    this.cw = Math.max(1, w);
    this.ch = Math.max(1, h);
    this.dpr = Math.min(2.5, window.devicePixelRatio || 1);
    this.canvas.width = Math.round(this.cw * this.dpr);
    this.canvas.height = Math.round(this.ch * this.dpr);
    this.canvas.style.width = `${this.cw}px`;
    this.canvas.style.height = `${this.ch}px`;
  }

  /** 画一帧 */
  draw(state: BattleState, view: ViewState, fx: FxLayer, cam: Camera): void {
    const { ctx } = this;
    ctx.setTransform(this.dpr, 0, 0, this.dpr, 0, 0);
    ctx.fillStyle = '#1F140D'; // 深褐底
    ctx.fillRect(0, 0, this.cw, this.ch);
    const now = fx.time();
    const sh = fx.shake(); // 受击屏抖：仅叠加渲染偏移，不改 camera 状态
    ctx.translate(cam.x + sh.x, cam.y + sh.y);
    ctx.scale(cam.scale, cam.scale);
    // 文字/血条反向缩放因子：保持屏幕上近似固定字号，不受缩放影响
    const ts = Math.min(2.2, Math.max(0.8, 1 / cam.scale));

    this.drawTerrain(state, now);
    this.drawHighlights(view);
    this.drawPath(view.pathPreview);
    for (const u of state.units) {
      if (u.alive) this.drawUnit(u, view, fx, ts, now);
    }
    this.drawDying(fx, ts);
    this.drawSlashes(fx);
    this.drawParticles(fx);
    this.drawFloaters(fx, ts);
    // 天气常驻粒子层（世界坐标，按可见范围生成，不挡输入）
    const wx0 = -cam.x / cam.scale - 60;
    const wy0 = -cam.y / cam.scale - 60;
    this.weather.draw(ctx, state.weather, wx0, wy0, wx0 + this.cw / cam.scale + 120, wy0 + this.ch / cam.scale + 120, now);
    // 暴击屏红闪（屏幕空间整帧叠加）
    const sf = fx.screenFlash();
    if (sf && sf.alpha > 0.005) {
      ctx.setTransform(this.dpr, 0, 0, this.dpr, 0, 0);
      ctx.globalAlpha = sf.alpha;
      ctx.fillStyle = sf.color;
      ctx.fillRect(0, 0, this.cw, this.ch);
      ctx.globalAlpha = 1;
    }
  }

  // ---------- 地形 ----------
  private hexPath(cx: number, cy: number, r: number): void {
    const { ctx } = this;
    ctx.beginPath();
    for (let i = 0; i < 6; i++) {
      const a = (Math.PI / 3) * i;
      const px = cx + r * Math.cos(a);
      const py = cy + r * Math.sin(a);
      if (i === 0) ctx.moveTo(px, py);
      else ctx.lineTo(px, py);
    }
    ctx.closePath();
  }

  private drawTerrain(state: BattleState, now: number): void {
    const { ctx } = this;
    const lv = state.level;
    for (let q = 0; q < lv.width; q++) {
      for (let r = 0; r < lv.height; r++) {
        const t: TerrainType = lv.terrain[`${q},${r}`] ?? 'plain';
        const c = cellToWorld(q, r);
        // 底色：统一用径向渐变（中心高光→边缘本色），水域额外保留竖向深蓝
        this.hexPath(c.x, c.y, HEX - 1);
        if (t === 'water') {
          const g = ctx.createLinearGradient(c.x, c.y - HEX, c.x, c.y + HEX);
          g.addColorStop(0, '#5B9BDC');
          g.addColorStop(1, '#2C62A0');
          ctx.fillStyle = g;
        } else {
          const g = ctx.createRadialGradient(c.x, c.y - 3, 2, c.x, c.y, HEX * 0.92);
          g.addColorStop(0, TERRAIN_HIGHLIGHT[t]);
          g.addColorStop(0.65, TERRAIN_COLORS[t]);
          g.addColorStop(1, TERRAIN_COLORS[t]);
          ctx.fillStyle = g;
        }
        ctx.fill();
        // 地形装饰细节（裁剪在六边形内）
        ctx.save();
        this.hexPath(c.x, c.y, HEX - 1);
        ctx.clip();
        this.drawTerrainDeco(t, q, r, c.x, c.y, now);
        ctx.restore();
        // 网格描边（该地形同色系深色）
        this.hexPath(c.x, c.y, HEX - 1);
        ctx.strokeStyle = TERRAIN_STROKE[t];
        ctx.lineWidth = 1.2;
        ctx.stroke();
      }
    }
  }

  /** 每类地形的格内装饰（位置/数量由 cellRand 确定性偏移；第三轮加密：草簇麦点/两层松树/阴阳面雪峰/波光光斑/城门砖纹；now 用于营寨小旗摆动等微动效） */
  private drawTerrainDeco(t: TerrainType, q: number, r: number, cx: number, cy: number, now: number): void {
    const { ctx } = this;
    const R = HEX - 1;
    const rnd = (salt: number) => cellRand(q, r, salt);
    switch (t) {
      case 'plain': {
        // 平原：柔和田垄 + 多层草丛 + 随机小花 + 细碎石子，营造丰茂田野感
        if (rnd(60) < 0.45) {
          ctx.strokeStyle = 'rgba(196,169,107,0.55)';
          ctx.lineWidth = 1.2;
          const off = (rnd(61) - 0.5) * R * 0.45;
          for (let i = 0; i < 3; i++) {
            const py = cy + off + (i - 1) * R * 0.30;
            ctx.beginPath();
            ctx.moveTo(cx - R * 0.85, py);
            ctx.lineTo(cx + R * 0.85, py);
            ctx.stroke();
          }
        }
        // 4-6 簇草叶（深浅绿黄交错，更茂密）
        const nGrass = 4 + Math.floor(rnd(0) * 3);
        const grassColors = ['#C4A962', '#D8C284', '#B89A55', '#E2CE8C'];
        for (let i = 0; i < nGrass; i++) {
          const px = cx + (rnd(i * 7 + 1) - 0.5) * R * 1.3;
          const py = cy + (rnd(i * 7 + 2) - 0.5) * R * 1.3;
          ctx.strokeStyle = grassColors[i % grassColors.length];
          ctx.lineWidth = 1.1;
          for (let k = 0; k < 4; k++) {
            const ang = -Math.PI / 2 + (k - 1.5) * 0.35 + (rnd(i * 7 + 3 + k) - 0.5) * 0.3;
            const len = 3 + rnd(i * 7 + 6 + k) * 2.5;
            ctx.beginPath();
            ctx.moveTo(px, py);
            ctx.quadraticCurveTo(px + Math.cos(ang) * len * 0.6, py + Math.sin(ang) * len * 0.6 - 1, px + Math.cos(ang) * len, py + Math.sin(ang) * len);
            ctx.stroke();
          }
        }
        // 小花（点缀，约 1/4 格出现 1-2 朵，避免过密）
        if (rnd(70) < 0.25) {
          const flowerColors = ['#D96B6B', '#E6BF33', '#F0F0C9', '#B07EC2'];
          const nFlower = 1 + Math.floor(rnd(71) * 2);
          for (let i = 0; i < nFlower; i++) {
            const px = cx + (rnd(i + 80) - 0.5) * R * 1.2;
            const py = cy + (rnd(i + 90) - 0.5) * R * 1.2;
            ctx.fillStyle = flowerColors[i % flowerColors.length];
            for (let p = 0; p < 5; p++) {
              const a = (p / 5) * Math.PI * 2;
              ctx.beginPath();
              ctx.arc(px + Math.cos(a) * 1.6, py + Math.sin(a) * 1.6, 1.1, 0, Math.PI * 2);
              ctx.fill();
            }
            ctx.fillStyle = '#FFF8DC';
            ctx.beginPath();
            ctx.arc(px, py, 0.8, 0, Math.PI * 2);
            ctx.fill();
          }
        }
        // 细碎石子/土块
        ctx.fillStyle = 'rgba(150,130,90,0.45)';
        const nStone = 3 + Math.floor(rnd(9) * 3);
        for (let i = 0; i < nStone; i++) {
          const px = cx + (rnd(i + 20) - 0.5) * R * 1.45;
          const py = cy + (rnd(i + 30) - 0.5) * R * 1.45;
          ctx.beginPath();
          ctx.ellipse(px, py, 1.2 + rnd(i + 40), 0.8 + rnd(i + 50) * 0.5, rnd(i + 60) * Math.PI, 0, Math.PI * 2);
          ctx.fill();
        }
        return;
      }
      case 'forest': {
        // 林地：远/中/近三层松树 + 地面落叶/灌木，营造纵深密林
        // 远景剪影（2 棵较小较暗）
        for (let i = 0; i < 2; i++) {
          const px = cx + (rnd(i * 3 + 50) - 0.5) * R * 1.2;
          const py = cy + R * 0.55 + (rnd(i * 3 + 51) - 0.5) * R * 0.4;
          const h = 10 + rnd(i * 3 + 52) * 4;
          const w = h * 0.75;
          ctx.fillStyle = '#203025';
          ctx.beginPath();
          ctx.moveTo(px, py - h);
          ctx.lineTo(px + w / 2, py);
          ctx.lineTo(px - w / 2, py);
          ctx.closePath();
          ctx.fill();
        }
        // 中近景 3-4 棵两层松树
        const n = 3 + Math.floor(rnd(0) * 2);
        for (let i = 0; i < n; i++) {
          const px = cx + (rnd(i * 5 + 1) - 0.5) * R * 1.15;
          const py = cy + (rnd(i * 5 + 2) - 0.5) * R * 0.95 + 5;
          const h = 16 + rnd(i * 5 + 3) * 6;
          const w = h * 0.9;
          const dark = i % 2 === 0;
          // 树干
          ctx.fillStyle = '#5A4128';
          ctx.fillRect(px - 1.4, py - 3, 2.8, 5.5);
          // 三层树冠（下大/中/上小）
          const layers = [
            { h: h * 0.55, w: w * 0.95, c: dark ? '#23402A' : '#3A6B42' },
            { h: h * 0.82, w: w * 0.72, c: dark ? '#2E5236' : '#488A52' },
            { h: h, w: w * 0.48, c: dark ? '#396540' : '#57A060' },
          ];
          for (const ly of layers) {
            ctx.fillStyle = ly.c;
            ctx.beginPath();
            ctx.moveTo(px, py - ly.h);
            ctx.lineTo(px + ly.w / 2, py - 1);
            ctx.lineTo(px - ly.w / 2, py - 1);
            ctx.closePath();
            ctx.fill();
          }
        }
        // 地面落叶与灌木
        ctx.fillStyle = '#2A4E30';
        for (let i = 0; i < 5; i++) {
          const px = cx + (rnd(i + 20) - 0.5) * R * 1.3;
          const py = cy + R * 0.35 + (rnd(i + 30) - 0.5) * R * 0.45;
          ctx.beginPath();
          ctx.ellipse(px, py, 2 + rnd(i + 40), 1 + rnd(i + 50) * 0.6, rnd(i + 60) * Math.PI, 0, Math.PI * 2);
          ctx.fill();
        }
        return;
      }
      case 'mountain': {
        // 山地：梯形山体 + 山脊线 + 雪顶阴影 + 山脚碎石，更厚重立体
        // 背景远山剪影
        for (let i = 0; i < 2; i++) {
          const px = cx + (rnd(i * 4 + 50) - 0.5) * R * 1.1;
          const py = cy + R * 0.55 + (rnd(i * 4 + 51) - 0.5) * R * 0.25;
          const h = 10 + rnd(i * 4 + 52) * 4;
          const w = h * 1.3;
          ctx.fillStyle = '#625A50';
          ctx.beginPath();
          ctx.moveTo(px, py - h);
          ctx.lineTo(px + w / 2, py);
          ctx.lineTo(px - w / 2, py);
          ctx.closePath();
          ctx.fill();
        }
        // 主山峰 2 个
        for (let i = 0; i < 2; i++) {
          const px = cx + (rnd(i * 4 + 1) - 0.5) * R * 0.85;
          const py = cy + (rnd(i * 4 + 2) - 0.5) * R * 0.5 + 7;
          const h = 18 - i * 4 + rnd(i * 4 + 3) * 3;
          const w = h * 1.05;
          // 山体阴面（左）
          ctx.fillStyle = '#4A4440';
          ctx.beginPath();
          ctx.moveTo(px, py - h);
          ctx.lineTo(px, py);
          ctx.lineTo(px - w / 2, py);
          ctx.closePath();
          ctx.fill();
          // 山体阳面（右）
          ctx.fillStyle = '#A89A8A';
          ctx.beginPath();
          ctx.moveTo(px, py - h);
          ctx.lineTo(px + w / 2 * 0.85, py);
          ctx.lineTo(px, py);
          ctx.closePath();
          ctx.fill();
          // 雪顶（带阴影厚度）
          const snowH = h * 0.38;
          const snowW = w * 0.34;
          ctx.fillStyle = '#D8D4C8';
          ctx.beginPath();
          ctx.moveTo(px, py - h);
          ctx.lineTo(px + snowW * 0.5, py - h + snowH);
          ctx.lineTo(px - snowW * 0.5, py - h + snowH);
          ctx.closePath();
          ctx.fill();
          ctx.fillStyle = '#EDEAE2';
          ctx.beginPath();
          ctx.moveTo(px, py - h);
          ctx.lineTo(px + snowW * 0.35, py - h + snowH * 0.7);
          ctx.lineTo(px - snowW * 0.35, py - h + snowH * 0.7);
          ctx.closePath();
          ctx.fill();
          // 山脊线
          ctx.strokeStyle = 'rgba(60,55,50,0.4)';
          ctx.lineWidth = 1;
          ctx.beginPath();
          ctx.moveTo(px, py - h);
          ctx.lineTo(px, py - h * 0.25);
          ctx.stroke();
        }
        // 山脚碎石带
        ctx.fillStyle = '#615A50';
        for (let i = 0; i < 6; i++) {
          const px = cx + (rnd(i + 20) - 0.5) * R * 1.35;
          const py = cy + R * 0.38 + (rnd(i + 30) - 0.5) * R * 0.5;
          ctx.beginPath();
          ctx.ellipse(px, py, 1.2 + rnd(i + 40), 0.9 + rnd(i + 50) * 0.5, rnd(i + 60) * Math.PI, 0, Math.PI * 2);
          ctx.fill();
        }
        return;
      }
      case 'water': {
        // 水域：多层波纹 + 高光 + 偶现荷叶/小鱼影，增强流动感
        const n = 3 + Math.floor(rnd(0) * 2);
        for (let i = 0; i < n; i++) {
          const py = cy + (rnd(i * 3 + 1) - 0.5) * R * 1.15;
          const px = cx + (rnd(i * 3 + 2) - 0.5) * R * 0.85;
          const ph = rnd(i * 3 + 3) * 6;
          ctx.strokeStyle = `rgba(255,255,255,${0.35 + rnd(i + 4) * 0.35})`;
          ctx.lineWidth = 1.2 + rnd(i + 5) * 0.6;
          ctx.beginPath();
          ctx.moveTo(px - 9, py);
          ctx.quadraticCurveTo(px - 4, py - 3 + Math.sin(ph) * 1.8, px, py);
          ctx.quadraticCurveTo(px + 4, py + 3 - Math.sin(ph) * 1.8, px + 9, py);
          ctx.stroke();
        }
        // 光斑/泡沫
        ctx.fillStyle = 'rgba(255,255,255,0.45)';
        for (let i = 0; i < 5; i++) {
          const px = cx + (rnd(i + 20) - 0.5) * R * 1.4;
          const py = cy + (rnd(i + 30) - 0.5) * R * 1.4;
          ctx.beginPath();
          ctx.ellipse(px, py, 1.1 + rnd(i + 40) * 0.7, 0.7 + rnd(i + 50) * 0.4, rnd(i + 60) * Math.PI, 0, Math.PI * 2);
          ctx.fill();
        }
        // 偶现荷叶（约 1/4 格）
        if (rnd(70) < 0.25) {
          const px = cx + (rnd(71) - 0.5) * R * 0.7;
          const py = cy + (rnd(72) - 0.5) * R * 0.7;
          ctx.fillStyle = '#4A8A5A';
          ctx.beginPath();
          ctx.arc(px, py, 4 + rnd(73) * 2, 0, Math.PI * 2);
          ctx.fill();
          ctx.strokeStyle = '#356B40';
          ctx.lineWidth = 0.8;
          ctx.beginPath();
          ctx.moveTo(px, py - 3);
          ctx.lineTo(px, py + 3);
          ctx.stroke();
        }
        return;
      }
      case 'city': {
        // 城池：高城墙 + 城垛 + 瓦顶 + 拱门门钉 + 墙基阴影
        const top = cy - R * 0.62;
        // 墙体主体（略带渐变感的深色块）
        ctx.fillStyle = '#8A5A22';
        ctx.fillRect(cx - R * 0.78, top, R * 1.56, R * 1.06);
        // 城垛（5 个，带阴影）
        ctx.fillStyle = '#784818';
        for (let i = 0; i < 5; i++) {
          const x0 = cx - R * 0.72 + i * (R * 0.36);
          ctx.fillRect(x0, top - R * 0.08, R * 0.22, R * 0.28);
          ctx.fillStyle = 'rgba(40,24,8,0.35)';
          ctx.fillRect(x0 + R * 0.16, top - R * 0.08, R * 0.06, R * 0.28);
          ctx.fillStyle = '#784818';
        }
        // 瓦顶屋檐
        ctx.fillStyle = '#5C3412';
        ctx.beginPath();
        ctx.moveTo(cx - R * 0.85, top + R * 0.12);
        ctx.lineTo(cx, top - R * 0.08);
        ctx.lineTo(cx + R * 0.85, top + R * 0.12);
        ctx.lineTo(cx + R * 0.82, top + R * 0.20);
        ctx.lineTo(cx, top);
        ctx.lineTo(cx - R * 0.82, top + R * 0.20);
        ctx.closePath();
        ctx.fill();
        // 砖缝横线
        ctx.strokeStyle = 'rgba(64,38,14,0.55)';
        ctx.lineWidth = 1;
        for (let i = 0; i < 3; i++) {
          const py = top + R * 0.10 + i * R * 0.28;
          ctx.beginPath();
          ctx.moveTo(cx - R * 0.78, py);
          ctx.lineTo(cx + R * 0.78, py);
          ctx.stroke();
        }
        // 竖向砖缝（交错）
        for (let row = 0; row < 3; row++) {
          const py = top + R * 0.10 + row * R * 0.28;
          const off = row % 2 === 0 ? 0 : R * 0.14;
          for (let i = -2; i <= 2; i++) {
            const px = cx + i * R * 0.28 + off;
            ctx.beginPath();
            ctx.moveTo(px, py);
            ctx.lineTo(px, py + R * 0.28);
            ctx.stroke();
          }
        }
        // 城门拱门
        const gw = R * 0.30;
        const gh = R * 0.48;
        const gy = cy + R * 0.60;
        ctx.fillStyle = '#3A2210';
        ctx.beginPath();
        ctx.moveTo(cx - gw, gy);
        ctx.lineTo(cx - gw, gy - gh * 0.55);
        ctx.arc(cx, gy - gh * 0.55, gw, Math.PI, 0);
        ctx.lineTo(cx + gw, gy);
        ctx.closePath();
        ctx.fill();
        // 门钉
        ctx.fillStyle = '#B88A40';
        for (let i = 0; i < 3; i++) {
          const dx = (i - 1) * gw * 0.5;
          ctx.beginPath();
          ctx.arc(cx + dx, gy - gh * 0.25, 1.6, 0, Math.PI * 2);
          ctx.fill();
        }
        // 墙基阴影
        ctx.fillStyle = 'rgba(50,30,10,0.25)';
        ctx.fillRect(cx - R * 0.78, gy, R * 1.56, R * 0.12);
        return;
      }
      case 'wall': {
        // 城墙：石基 + 城垛 + 更规则砖缝 + 风化斑驳
        // 石基
        ctx.fillStyle = '#3E4A5A';
        ctx.fillRect(cx - R, cy + R * 0.55, R * 2, R * 0.18);
        // 墙体
        ctx.fillStyle = '#5A6A80';
        ctx.fillRect(cx - R, cy - R * 0.78, R * 2, R * 1.36);
        // 顶部城垛（凹凸）
        ctx.fillStyle = '#687792';
        for (let i = -1; i <= 2; i++) {
          const x0 = cx - R * 0.75 + i * R * 0.50;
          ctx.fillRect(x0, cy - R * 0.92, R * 0.30, R * 0.16);
        }
        // 砖缝横线
        ctx.strokeStyle = 'rgba(24,32,44,0.55)';
        ctx.lineWidth = 1;
        for (let i = -1; i <= 2; i++) {
          const py = cy - R * 0.55 + i * R * 0.32;
          ctx.beginPath();
          ctx.moveTo(cx - R, py);
          ctx.lineTo(cx + R, py);
          ctx.stroke();
        }
        // 交错竖缝
        for (let row = -1; row <= 2; row++) {
          const py = cy - R * 0.55 + row * R * 0.32;
          const off = row % 2 === 0 ? 0 : R * 0.16;
          for (let i = -3; i <= 3; i++) {
            const px = cx + i * R * 0.24 + off;
            ctx.beginPath();
            ctx.moveTo(px, py);
            ctx.lineTo(px, py + R * 0.32);
            ctx.stroke();
          }
        }
        // 风化斑驳
        ctx.fillStyle = 'rgba(120,130,145,0.35)';
        for (let i = 0; i < 4; i++) {
          const px = cx + (rnd(i + 20) - 0.5) * R * 1.6;
          const py = cy + (rnd(i + 30) - 0.5) * R * 1.2;
          ctx.beginPath();
          ctx.ellipse(px, py, 2 + rnd(i + 40), 1.2 + rnd(i + 50), rnd(i + 60) * Math.PI, 0, Math.PI * 2);
          ctx.fill();
        }
        return;
      }
      case 'pass': {
        // 关隘：陡峭双峰 + 峡谷关门 + 石阶山路 + 岩壁纹理
        // 两侧山峰
        for (const side of [-1, 1]) {
          const px = cx + side * R * 0.48 + (rnd(side + 10) - 0.5) * 2;
          const py = cy + R * 0.45;
          const h = R * 1.15 - rnd(side + 20) * 3;
          const w = R * 0.82;
          // 山体阴面
          ctx.fillStyle = '#4A525C';
          ctx.beginPath();
          ctx.moveTo(px, py - h);
          ctx.lineTo(px, py);
          ctx.lineTo(px - side * (w / 2), py);
          ctx.closePath();
          ctx.fill();
          // 山体阳面
          ctx.fillStyle = '#A8B0BA';
          ctx.beginPath();
          ctx.moveTo(px, py - h);
          ctx.lineTo(px + side * (w / 2) * 0.9, py);
          ctx.lineTo(px, py);
          ctx.closePath();
          ctx.fill();
          // 岩壁横纹
          ctx.strokeStyle = 'rgba(60,68,76,0.35)';
          ctx.lineWidth = 1;
          for (let i = 0; i < 3; i++) {
            const ly = py - h * 0.25 - i * h * 0.22;
            ctx.beginPath();
            ctx.moveTo(px - side * w * 0.15, ly);
            ctx.lineTo(px - side * w * 0.45, ly);
            ctx.stroke();
          }
        }
        // 关门城楼
        const gw = R * 0.30;
        const gy = cy + R * 0.55;
        // 门洞
        ctx.fillStyle = '#2E2015';
        ctx.beginPath();
        ctx.moveTo(cx - gw, gy);
        ctx.lineTo(cx - gw, cy - R * 0.05);
        ctx.lineTo(cx - gw * 0.3, cy - R * 0.25);
        ctx.lineTo(cx + gw * 0.3, cy - R * 0.25);
          ctx.lineTo(cx + gw, cy - R * 0.05);
        ctx.lineTo(cx + gw, gy);
        ctx.closePath();
        ctx.fill();
        // 门扇横木
        ctx.strokeStyle = '#5A4630';
        ctx.lineWidth = 1.8;
        for (let i = 0; i < 4; i++) {
          const py = cy + R * 0.02 + i * R * 0.18;
          ctx.beginPath();
          ctx.moveTo(cx - gw, py);
          ctx.lineTo(cx + gw, py);
          ctx.stroke();
        }
        // 门楼屋顶
        ctx.fillStyle = '#5A3A22';
        ctx.beginPath();
        ctx.moveTo(cx - gw * 1.4, cy - R * 0.12);
        ctx.lineTo(cx, cy - R * 0.38);
        ctx.lineTo(cx + gw * 1.4, cy - R * 0.12);
        ctx.lineTo(cx + gw * 1.2, cy - R * 0.05);
        ctx.lineTo(cx - gw * 1.2, cy - R * 0.05);
        ctx.closePath();
        ctx.fill();
        // 山路石阶
        ctx.strokeStyle = 'rgba(80,88,96,0.5)';
        ctx.lineWidth = 1;
        for (let i = 0; i < 3; i++) {
          const py = cy + R * 0.45 + i * R * 0.18;
          ctx.beginPath();
          ctx.moveTo(cx - R * 0.35, py);
          ctx.lineTo(cx + R * 0.35, py);
          ctx.stroke();
        }
        return;
      }
      case 'camp': {
        // 营寨：主帐 + 小帐 + 木栅栏 + 篝火 + 飘旗
        // 主帐
        const px = cx + (rnd(1) - 0.5) * R * 0.2;
        const py = cy + R * 0.55;
        const tw = R * 1.10;
        const th = R * 0.92;
        // 帐身阴阳面
        ctx.fillStyle = '#A9805A';
        ctx.beginPath();
        ctx.moveTo(px, py - th);
        ctx.lineTo(px + tw / 2, py);
        ctx.lineTo(px, py);
        ctx.closePath();
        ctx.fill();
        ctx.fillStyle = '#8A6540';
        ctx.beginPath();
        ctx.moveTo(px, py - th);
        ctx.lineTo(px, py);
        ctx.lineTo(px - tw / 2, py);
        ctx.closePath();
        ctx.fill();
        // 帐门
        ctx.fillStyle = '#452A0E';
        ctx.beginPath();
        ctx.moveTo(px, py - th * 0.42);
        ctx.lineTo(px + tw * 0.13, py);
        ctx.lineTo(px - tw * 0.13, py);
        ctx.closePath();
        ctx.fill();
        // 支架
        ctx.strokeStyle = '#5A4128';
        ctx.lineWidth = 1.4;
        ctx.beginPath();
        ctx.moveTo(px, py - th);
        ctx.lineTo(px, py - th - 5);
        ctx.moveTo(px - tw * 0.32, py + 2);
        ctx.lineTo(px - tw * 0.18, py - 1);
        ctx.stroke();
        // 小旗
        const fl = (rnd(2) - 0.5) * 2 + Math.sin(now / 480 + q * 1.3 + r * 2.1) * 1.8;
        ctx.fillStyle = '#B7261E';
        ctx.beginPath();
        ctx.moveTo(px, py - th - 5);
        ctx.lineTo(px + 7 + fl, py - th - 3.2);
        ctx.lineTo(px, py - th - 1.4);
        ctx.closePath();
        ctx.fill();
        // 侧后方小帐
        const sx = cx + (rnd(3) > 0.5 ? 1 : -1) * (R * 0.55 + rnd(4) * R * 0.15);
        const sy = cy + R * 0.55 + rnd(5) * R * 0.1;
        const stw = R * 0.55;
        const sth = R * 0.45;
        ctx.fillStyle = '#9A7550';
        ctx.beginPath();
        ctx.moveTo(sx, sy - sth);
        ctx.lineTo(sx + stw / 2, sy);
        ctx.lineTo(sx - stw / 2, sy);
        ctx.closePath();
        ctx.fill();
        // 木栅栏（营寨外围）
        ctx.strokeStyle = '#7A5A3A';
        ctx.lineWidth = 1.6;
        ctx.beginPath();
        ctx.moveTo(cx - R * 0.7, cy + R * 0.30);
        ctx.lineTo(cx + R * 0.7, cy + R * 0.30);
        ctx.stroke();
        for (let i = 0; i < 5; i++) {
          const fx = cx - R * 0.6 + i * R * 0.30;
          const fh = R * 0.22 + rnd(i + 10) * R * 0.08;
          ctx.fillStyle = '#8A6A42';
          ctx.beginPath();
          ctx.moveTo(fx - 1.8, cy + R * 0.30);
          ctx.lineTo(fx, cy + R * 0.30 - fh);
          ctx.lineTo(fx + 1.8, cy + R * 0.30);
          ctx.closePath();
          ctx.fill();
        }
        // 篝火（暖橙小圆堆）
        const hx = cx + (rnd(6) - 0.5) * R * 0.5;
        const hy = cy + R * 0.45 + (rnd(7) - 0.5) * R * 0.25;
        ctx.fillStyle = '#B7261E';
        ctx.beginPath();
        ctx.arc(hx, hy, 2.5 + Math.sin(now / 300 + q) * 0.5, 0, Math.PI * 2);
        ctx.fill();
        ctx.fillStyle = '#E6BF33';
        ctx.beginPath();
        ctx.arc(hx, hy - 1, 1.4 + Math.sin(now / 300 + q) * 0.3, 0, Math.PI * 2);
        ctx.fill();
        return;
      }
      case 'fence': {
        // 栅栏：高低尖木桩 + 双横向绑木 + 绳索/铁丝 + 底部草丛
        const n = 6;
        const posts: { x: number; h: number; top: number }[] = [];
        for (let i = 0; i < n; i++) {
          const px = cx - R * 0.72 + (i / (n - 1)) * R * 1.44 + (rnd(i + 3) - 0.5) * 2;
          const h = R * 0.68 + rnd(i + 10) * R * 0.32;
          const top = cy + R * 0.48 - h;
          posts.push({ x: px, h, top });
        }
        // 双横向绑木
        ctx.strokeStyle = '#6B4A2F';
        ctx.lineWidth = 1.6;
        for (const by of [cy + R * 0.12, cy + R * 0.34]) {
          ctx.beginPath();
          ctx.moveTo(cx - R * 0.8, by);
          ctx.lineTo(cx + R * 0.8, by);
          ctx.stroke();
        }
        // 木桩
        for (const p of posts) {
          ctx.fillStyle = rnd(posts.indexOf(p) + 20) < 0.5 ? '#8A6A42' : '#7A5A36';
          ctx.strokeStyle = '#4A3018';
          ctx.lineWidth = 1;
          ctx.beginPath();
          ctx.moveTo(p.x - 2.6, cy + R * 0.48);
          ctx.lineTo(p.x - 2.2, p.top + 3.5);
          ctx.lineTo(p.x, p.top);
          ctx.lineTo(p.x + 2.2, p.top + 3.5);
          ctx.lineTo(p.x + 2.6, cy + R * 0.48);
          ctx.closePath();
          ctx.fill();
          ctx.stroke();
          // 木桩纹理竖线
          ctx.strokeStyle = 'rgba(60,40,20,0.4)';
          ctx.lineWidth = 0.8;
          ctx.beginPath();
          ctx.moveTo(p.x, p.top + 4);
          ctx.lineTo(p.x, cy + R * 0.45);
          ctx.stroke();
        }
        // 铁丝/绳索（虚线连接相邻桩顶）
        ctx.strokeStyle = 'rgba(90,70,45,0.6)';
        ctx.lineWidth = 1;
        ctx.setLineDash([2, 2]);
        ctx.beginPath();
        for (let i = 0; i < posts.length - 1; i++) {
          const y = Math.min(posts[i].top, posts[i + 1].top) + 2;
          ctx.moveTo(posts[i].x, y);
          ctx.lineTo(posts[i + 1].x, y);
        }
        ctx.stroke();
        ctx.setLineDash([]);
        // 底部草丛
        ctx.strokeStyle = '#A09050';
        ctx.lineWidth = 1;
        for (let i = 0; i < 6; i++) {
          const px = cx + (rnd(i + 50) - 0.5) * R * 1.4;
          const py = cy + R * 0.48 + rnd(i + 60) * R * 0.08;
          ctx.beginPath();
          ctx.moveTo(px, py);
          ctx.lineTo(px + (rnd(i + 70) - 0.5) * 3, py - 3 - rnd(i + 80) * 2);
          ctx.stroke();
        }
        return;
      }
    }
  }

  // ---------- 高亮 ----------
  private fillCells(cells: Cell[], color: string): void {
    const { ctx } = this;
    ctx.fillStyle = color;
    for (const c of cells) {
      const p = cellToWorld(c.q, c.r);
      this.hexPath(p.x, p.y, HEX - 2);
      ctx.fill();
    }
  }

  private drawHighlights(view: ViewState): void {
    this.fillCells(view.reachable, COLOR_REACH);
    this.fillCells(view.attackable, COLOR_ATTACK);
    this.fillCells(view.skillCells, COLOR_SKILL);
    // 计策目标格紫圈
    if (view.targetCell) {
      const p = cellToWorld(view.targetCell.q, view.targetCell.r);
      this.ctx.strokeStyle = '#8A2BE2';
      this.ctx.lineWidth = 3;
      this.hexPath(p.x, p.y, HEX - 2);
      this.ctx.stroke();
    }
  }

  /** 路径预览线（金色折线 + 格心圆点） */
  private drawPath(path: Cell[]): void {
    if (path.length === 0) return;
    const { ctx } = this;
    ctx.strokeStyle = COLOR_PATH;
    ctx.lineWidth = 3;
    ctx.lineJoin = 'round';
    ctx.beginPath();
    path.forEach((c, i) => {
      const p = cellToWorld(c.q, c.r);
      if (i === 0) ctx.moveTo(p.x, p.y);
      else ctx.lineTo(p.x, p.y);
    });
    ctx.stroke();
    ctx.fillStyle = COLOR_PATH;
    for (const c of path) {
      const p = cellToWorld(c.q, c.r);
      ctx.beginPath();
      ctx.arc(p.x, p.y, 3.5, 0, Math.PI * 2);
      ctx.fill();
    }
  }

  // ---------- 单位 ----------
  private drawUnit(u: Unit, view: ViewState, fx: FxLayer, ts: number, now: number): void {
    const { ctx } = this;
    const center = cellToWorld(u.q, u.r);
    const v = fx.unitVisual(u.uid);
    const x = v ? v.x : center.x;
    const y = v ? v.y : center.y;
    const baseAlpha = u.faction === 'player' && u.acted ? 0.42 : 1; // 已行动灰化
    const alpha = baseAlpha * (v ? v.alpha : 1);
    if (alpha <= 0.01) return;

    ctx.save();
    ctx.globalAlpha = alpha;

    // 选中光环（金色六边形）
    if (view.selectedUid === u.uid) {
      ctx.strokeStyle = '#E6BF33';
      ctx.lineWidth = 3.5;
      this.hexPath(center.x, center.y, HEX - 2);
      ctx.stroke();
    }
    // 攻击预览目标红圈 / 可攻击敌人红圈（blinkAttack 时闪烁呼吸）
    const isAttackTarget =
      view.previewTargetUid === u.uid || view.attackable.some(c => c.q === u.q && c.r === u.r);
    if (isAttackTarget) {
      const pulse = view.blinkAttack ? Math.sin(performance.now() / 150) : 0;
      ctx.strokeStyle = '#C23A30';
      ctx.lineWidth = 3;
      ctx.globalAlpha = alpha * (view.blinkAttack ? 0.55 + 0.45 * pulse : 1);
      ctx.beginPath();
      ctx.arc(x, y, HEX * 0.78 + pulse * 2, 0, Math.PI * 2);
      ctx.stroke();
      ctx.globalAlpha = alpha;
    }

    // 落地阴影（脚下椭圆半透明，贴地不随呼吸/步态起伏；马匹/器械略大）
    const groundY = y + HEX * 0.28;
    const shScale = u.classType === 'cavalry' ? 1.3 : u.classType === 'siege' || u.classType === 'catapult' ? 1.4 : 1;
    ctx.globalAlpha = alpha * 0.26;
    ctx.fillStyle = '#1F140D';
    ctx.beginPath();
    ctx.ellipse(x, groundY + 2, 16.5 * shScale, 5 * shScale, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.globalAlpha = alpha;

    // 迷你全身立绘（缓存 sprite，每帧 drawImage；脚底站在格中心偏下）。
    // 移动中按兵种步态相位：选步态帧 sprite 变体 + y 偏移/微旋转（§16.2）。
    // 待机呼吸（§17.4）：未行动/未移动/未受击时 y 正弦微起伏（±1.5px，周期 2s，相位按 uid 错开）
    let mv: { dy: number; rot: number; frame: number } | null = null;
    if (v && v.movePhase !== null) mv = moveAnim(u.classType, v.movePhase);
    let breath = 0;
    if (!mv && (!v || v.flash <= 0) && !u.acted) {
      let ph = 0;
      for (let i = 0; i < u.uid.length; i++) ph += u.uid.charCodeAt(i);
      breath = Math.sin((now / 2000) * Math.PI * 2 + ph * 0.7) * 1.5;
    }
    const feetY = groundY + (mv ? mv.dy : 0) + breath;
    // 我方角色优先使用 AI 写实头像，敌方/友方仍用程序模型以保证阵营识别度
    const portrait = u.faction === 'player' ? this.getPortraitSprite(u.charId) : null;
    const topY = feetY - (portrait ? PORTRAIT_SIZE : SPR_H);
    if (portrait) {
      const cy = feetY - PORTRAIT_SIZE / 2;
      // 移动中轻微呼吸式缩放（头像不适合帧动画，用整体缩放模拟颠簸）
      const bounce = mv ? 1 + Math.sin((v?.movePhase ?? 0) * Math.PI * 4) * 0.035 : 1;
      const sz = PORTRAIT_SIZE * bounce;
      ctx.save();
      ctx.beginPath();
      ctx.arc(x, cy, PORTRAIT_SIZE / 2, 0, Math.PI * 2);
      ctx.closePath();
      ctx.clip();
      ctx.drawImage(portrait, x - sz / 2, cy - sz / 2, sz, sz);
      ctx.restore();
      // 阵营描边（金色）+ 细黑边让头像从地形中浮出
      ctx.beginPath();
      ctx.arc(x, cy, PORTRAIT_SIZE / 2, 0, Math.PI * 2);
      ctx.strokeStyle = '#1F140D';
      ctx.lineWidth = 3.5;
      ctx.stroke();
      ctx.beginPath();
      ctx.arc(x, cy, PORTRAIT_SIZE / 2, 0, Math.PI * 2);
      ctx.strokeStyle = FACTION_STROKE[u.faction];
      ctx.lineWidth = 2;
      ctx.stroke();

      // 兵种徽章：头像模式下补回兵种识别度（左下角彩色圆标+白字，避免挡脸）
      const bx = x - PORTRAIT_SIZE * 0.30;
      const by = feetY - PORTRAIT_SIZE * 0.22;
      const br = 10.5;
      ctx.beginPath();
      ctx.arc(bx, by, br, 0, Math.PI * 2);
      ctx.fillStyle = CLASS_COLORS[u.classType];
      ctx.fill();
      ctx.strokeStyle = '#1F140D';
      ctx.lineWidth = 2;
      ctx.stroke();
      ctx.fillStyle = '#FAFAFA';
      ctx.font = `bold ${Math.round(12 * ts)}px "PingFang SC","Microsoft YaHei",sans-serif`;
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.fillText(CLASS_BADGE[u.classType], bx, by + 0.5);
    } else {
      const sprite = this.spriteFor(u.classType, u.faction, mv ? mv.frame : 0);
      if (mv && mv.rot !== 0) {
        ctx.save();
        ctx.translate(x, feetY);
        ctx.rotate(mv.rot);
        ctx.drawImage(sprite, -SPR_W / 2, -SPR_H, SPR_W, SPR_H);
        ctx.restore();
      } else {
        ctx.drawImage(sprite, x - SPR_W / 2, topY, SPR_W, SPR_H);
      }
    }

    // Boss 标记（金色「将」徽章，头顶右上）
    if (u.isBoss) {
      const bx = x + 17;
      const by = topY + 5;
      ctx.beginPath();
      ctx.arc(bx, by, 8 * ts * 0.6, 0, Math.PI * 2);
      ctx.fillStyle = '#E6BF33';
      ctx.fill();
      ctx.fillStyle = '#1F140D';
      ctx.font = `bold ${Math.round(9 * ts)}px sans-serif`;
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.fillText('将', bx, by + 0.5);
    }

    // buff 头顶标记（最多 2 个，减益红底/增益金底）
    const buffs = u.buffs.slice(0, 2);
    buffs.forEach((b, i) => {
      const bx = x - (buffs.length - 1) * 14 * ts + i * 28 * ts;
      const by = topY - 8 * ts;
      const label = b.name.slice(0, 2);
      ctx.font = `${Math.round(9 * ts)}px sans-serif`;
      const wpx = Math.max(20 * ts, ctx.measureText(label).width + 6 * ts);
      ctx.fillStyle = DEBUFF_IDS.has(b.id) ? 'rgba(194,58,48,0.9)' : 'rgba(230,191,51,0.9)';
      ctx.fillRect(bx - wpx / 2, by - 7 * ts, wpx, 13 * ts);
      ctx.fillStyle = DEBUFF_IDS.has(b.id) ? '#FAFAFA' : '#1F140D';
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.fillText(label, bx, by);
    });

    // 受击闪（椭圆罩住立绘区域；白=普通命中，红=暴击；头像用圆形）
    if (v && v.flash > 0) {
      ctx.globalAlpha = alpha * Math.min(1, v.flash) * 0.8;
      ctx.beginPath();
      if (portrait) {
        ctx.arc(x, feetY - PORTRAIT_SIZE / 2, PORTRAIT_SIZE / 2, 0, Math.PI * 2);
      } else {
        ctx.ellipse(x, feetY - SPR_H / 2, SPR_W * 0.4, SPR_H * 0.52, 0, 0, Math.PI * 2);
      }
      ctx.fillStyle = v.flashColor;
      ctx.fill();
      ctx.globalAlpha = alpha;
    }

    // 名字（白字黑边，脚下）：缩小（ts≥1.4）时只画血条，避免密集单位名字互相遮挡；
    // 放大或选中时显示名字
    const showName = ts < 1.4 || view.selectedUid === u.uid;
    let nameY = feetY + 9 * ts;
    if (showName) {
      ctx.font = `${Math.round(11 * ts)}px "PingFang SC","Microsoft YaHei",sans-serif`;
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.lineWidth = 3 * ts;
      ctx.strokeStyle = 'rgba(26,26,26,0.85)';
      ctx.strokeText(u.name, x, nameY);
      ctx.fillStyle = '#FAFAFA';
      ctx.fillText(u.name, x, nameY);
    } else {
      nameY = feetY + 2 * ts;
    }

    // HP 条（>50% 绿 / 25-50% 黄 / <25% 红）
    const bw = 42 * ts;
    const bh = 5 * ts;
    const bx = x - bw / 2;
    const by = nameY + 6 * ts;
    const ratio = u.maxHp > 0 ? u.hp / u.maxHp : 0;
    ctx.fillStyle = '#1A1A1A';
    ctx.fillRect(bx - 1, by - 1, bw + 2, bh + 2);
    ctx.fillStyle = ratio > 0.5 ? HP_GREEN : ratio > 0.25 ? HP_YELLOW : HP_RED;
    ctx.fillRect(bx, by, bw * Math.max(0, ratio), bh);

    ctx.restore();
  }

  /** 兵种×阵营×步态帧 立绘 sprite 缓存（静态内容，无需失效；站立帧全表 ≤18 张，步态变体移动时按需建） */
  private sprites = new Map<string, HTMLCanvasElement>();

  private spriteFor(classType: ClassType, faction: Faction, step = 0): HTMLCanvasElement {
    const k = `${faction}:${classType}:${step}`;
    let s = this.sprites.get(k);
    if (!s) {
      s = buildUnitSprite(classType, faction, step);
      this.sprites.set(k, s);
    }
    return s;
  }

  /** 战场 AI 头像缓存（按 charId；getPortrait 内部已做 cover 裁切） */
  private portraitCache = new Map<string, HTMLCanvasElement>();

  private getPortraitSprite(charId: string): HTMLCanvasElement | null {
    if (!portraitImageReady(charId)) return null;
    let cv = this.portraitCache.get(charId);
    if (!cv) {
      cv = getPortrait(charId, PORTRAIT_SIZE);
      this.portraitCache.set(charId, cv);
    }
    return cv;
  }

  /** 阵亡渐隐（立绘原地淡出）+ 灰叉 */
  private drawDying(fx: FxLayer, ts: number): void {
    const { ctx } = this;
    for (const d of fx.dying()) {
      ctx.save();
      ctx.globalAlpha = Math.max(0, d.alpha);
      const feetY = d.y + HEX * 0.28;
      const portrait = d.faction === 'player' ? this.getPortraitSprite(d.charId) : null;
      if (!d.crossed) {
        if (portrait) {
          const cy = feetY - PORTRAIT_SIZE / 2;
          ctx.save();
          ctx.beginPath();
          ctx.arc(d.x, cy, PORTRAIT_SIZE / 2, 0, Math.PI * 2);
          ctx.closePath();
          ctx.clip();
          ctx.drawImage(portrait, d.x - PORTRAIT_SIZE / 2, cy - PORTRAIT_SIZE / 2, PORTRAIT_SIZE, PORTRAIT_SIZE);
          ctx.restore();
        } else {
          ctx.drawImage(this.spriteFor(d.classType, d.faction), d.x - SPR_W / 2, feetY - SPR_H, SPR_W, SPR_H);
        }
      } else {
        // 灰叉
        ctx.strokeStyle = '#888888';
        ctx.lineWidth = 4 * ts * 0.6;
        const s = HEX * 0.5;
        const cy = d.y + HEX * 0.28 - (portrait ? PORTRAIT_SIZE / 2 : SPR_H / 2);
        ctx.beginPath();
        ctx.moveTo(d.x - s, cy - s);
        ctx.lineTo(d.x + s, cy + s);
        ctx.moveTo(d.x + s, cy - s);
        ctx.lineTo(d.x - s, cy + s);
        ctx.stroke();
      }
      ctx.restore();
    }
  }

  /** 飘字（伤害/治疗/MISS/buff 名） */
  private drawFloaters(fx: FxLayer, ts: number): void {
    const { ctx } = this;
    for (const f of fx.floaters()) {
      ctx.save();
      ctx.globalAlpha = Math.max(0, Math.min(1, f.alpha));
      ctx.font = `bold ${Math.round(f.size * ts)}px "PingFang SC","Microsoft YaHei",sans-serif`;
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.lineWidth = 3;
      ctx.strokeStyle = 'rgba(26,26,26,0.9)';
      ctx.strokeText(f.text, f.x, f.y);
      ctx.fillStyle = f.color;
      ctx.fillText(f.text, f.x, f.y);
      ctx.restore();
    }
  }

  /** 刀光弧线（§17.1：命中瞬间在受击单位处扫过 120°，150ms 渐隐） */
  private drawSlashes(fx: FxLayer): void {
    const { ctx } = this;
    const span = (Math.PI * 2) / 3; // 120°
    for (const s of fx.slashes()) {
      const from = s.angle - span / 2;
      const to = from + span * Math.min(1, s.t * 1.35); // 弧头随进度扫出
      ctx.save();
      ctx.globalAlpha = Math.max(0, s.alpha);
      ctx.strokeStyle = '#FFFFFF';
      ctx.lineWidth = 3.5;
      ctx.lineCap = 'round';
      ctx.beginPath();
      ctx.arc(s.x, s.y, 26, from, to);
      ctx.stroke();
      ctx.globalAlpha = Math.max(0, s.alpha) * 0.6; // 内侧第二道细光
      ctx.lineWidth = 1.5;
      ctx.beginPath();
      ctx.arc(s.x, s.y, 20, from, to);
      ctx.stroke();
      ctx.restore();
    }
  }

  /** 计策粒子层（§17.2：dot 圆点 / ring 扩散环 / arrow 箭矢 / pillar 光柱） */
  private drawParticles(fx: FxLayer): void {
    const { ctx } = this;
    for (const p of fx.particles()) {
      ctx.save();
      ctx.globalAlpha = Math.max(0, Math.min(1, p.alpha));
      switch (p.kind) {
        case 'dot':
          ctx.fillStyle = p.color;
          ctx.beginPath();
          ctx.arc(p.x, p.y, Math.max(0.4, p.size), 0, Math.PI * 2);
          ctx.fill();
          break;
        case 'ring':
          ctx.strokeStyle = p.color;
          ctx.lineWidth = 2.5;
          ctx.beginPath();
          ctx.arc(p.x, p.y, Math.max(0.5, p.size), 0, Math.PI * 2);
          ctx.stroke();
          break;
        case 'arrow': {
          const c = Math.cos(p.angle);
          const s = Math.sin(p.angle);
          ctx.strokeStyle = p.color;
          ctx.lineWidth = 1.8;
          ctx.beginPath(); // 箭杆
          ctx.moveTo(p.x - c * p.size, p.y - s * p.size);
          ctx.lineTo(p.x, p.y);
          ctx.stroke();
          ctx.fillStyle = '#B9BEC6'; // 钢箭头小三角
          ctx.beginPath();
          ctx.moveTo(p.x + c * 2.5, p.y + s * 2.5);
          ctx.lineTo(p.x - s * 1.8 - c * 2, p.y + c * 1.8 - s * 2);
          ctx.lineTo(p.x + s * 1.8 - c * 2, p.y - c * 1.8 - s * 2);
          ctx.closePath();
          ctx.fill();
          break;
        }
        case 'pillar':
          ctx.fillStyle = p.color;
          ctx.fillRect(p.x - p.size / 2, p.y - 46, p.size, 54);
          ctx.globalAlpha *= 0.75; // 白色亮芯
          ctx.fillStyle = '#F0FFF0';
          ctx.fillRect(p.x - p.size / 6, p.y - 46, p.size / 3, 54);
          break;
      }
      ctx.restore();
    }
  }
}
