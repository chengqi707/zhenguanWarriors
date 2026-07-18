// ============================================================
// 粒子系统与天气层（PRD §17.2 / §17.3，纯程序化、零素材）：
// - ParticleSystem：简单对象池（固定数组复用死槽，上限 420），
//   粒子位置是 (now - start) 的确定函数，无需逐步积分；
//   种类：dot 圆点（重力/上飘/闪烁）、ring 扩散环、arrow 箭矢
//   （抛物线+按速度定向）、pillar 光柱。
// - 计策发射器：火攻火焰 / 水攻波浪环+水花 / 落石碎石+碎裂 /
//   乱射箭矢 / 医疗光柱+光点 / 鼓舞·混乱·洞察对应色微光。
// - WeatherLayer：雨（斜丝 120）/ 雪（摇摆 80）/ 雾（漂移雾团 8）/
//   风（间歇流线 30），按相机视野范围生成，位置全部由 hash 种子
//   + 时间确定（无状态、视野平移即自动覆盖新区），每帧 ≤200 个。
// 只负责视觉，不触碰任何 core 状态；由 Animator/Renderer 驱动。
// ============================================================
import type { Weather } from '../core/types';

export type ParticleKind = 'dot' | 'ring' | 'arrow' | 'pillar';

/** renderer 每帧取用的绘制视图（世界坐标） */
export interface ParticleView {
  kind: ParticleKind;
  x: number;
  y: number;
  size: number; // dot 半径 / ring 当前半径 / arrow 长度 / pillar 宽
  color: string;
  alpha: number;
  angle: number; // 仅 arrow（沿速度方向）
}

interface Particle {
  kind: ParticleKind;
  x0: number;
  y0: number;
  vx: number; // px/ms
  vy: number;
  g: number; // 重力加速度 px/ms²（负=上飘）
  size: number;
  size1: number; // ring 结束半径
  color: string;
  start: number;
  dur: number;
  flicker: boolean;
}

const MAX_PARTICLES = 420;

export class ParticleSystem {
  private now = 0;
  private pool: Particle[] = [];
  private seed = 1;

  update(dtMs: number): void {
    this.now += dtMs;
    // 清理过期粒子（交换删除，池槽留给 spawn 复用）
    for (let i = this.pool.length - 1; i >= 0; i--) {
      if (this.now - this.pool[i].start >= this.pool[i].dur) {
        this.pool[i] = this.pool[this.pool.length - 1];
        this.pool.pop();
      }
    }
  }

  private rnd(): number {
    this.seed = (this.seed * 1103515245 + 12345) & 0x7fffffff;
    return this.seed / 0x7fffffff;
  }

  private spawn(p: Particle): void {
    if (this.pool.length >= MAX_PARTICLES) this.pool.shift(); // 满了顶掉最老的
    this.pool.push(p);
  }

  // ---------- 计策发射器（世界坐标锚点） ----------

  /** 火攻：格底升起橙红火焰（上飘 + 闪烁，约 600ms） */
  fire(pts: Array<{ x: number; y: number }>): void {
    const COLORS = ['#FF8C2A', '#FF5A1E', '#FFC93C', '#E83A14'];
    for (const c of pts) {
      for (let i = 0; i < 14; i++) {
        this.spawn({
          kind: 'dot',
          x0: c.x + (this.rnd() - 0.5) * 36,
          y0: c.y + 8 + this.rnd() * 8,
          vx: (this.rnd() - 0.5) * 0.02,
          vy: -(0.045 + this.rnd() * 0.075),
          g: -0.00003,
          size: 1.8 + this.rnd() * 2.4,
          size1: 0,
          color: COLORS[Math.floor(this.rnd() * COLORS.length)],
          start: this.now + this.rnd() * 120,
          dur: 420 + this.rnd() * 260,
          flicker: true,
        });
      }
    }
  }

  /** 水攻：蓝色波浪扩散环 ×2 + 水花粒子 */
  water(pts: Array<{ x: number; y: number }>): void {
    for (const c of pts) {
      for (let k = 0; k < 2; k++) {
        this.spawn({
          kind: 'ring',
          x0: c.x,
          y0: c.y,
          vx: 0,
          vy: 0,
          g: 0,
          size: 3,
          size1: 34,
          color: k === 0 ? '#4DA6FF' : '#9FD4FF',
          start: this.now + k * 170,
          dur: 520,
          flicker: false,
        });
      }
      for (let i = 0; i < 8; i++) {
        const a = this.rnd() * Math.PI * 2;
        const sp = 0.05 + this.rnd() * 0.06;
        this.spawn({
          kind: 'dot',
          x0: c.x,
          y0: c.y - 2,
          vx: Math.cos(a) * sp,
          vy: -(0.06 + this.rnd() * 0.08),
          g: 0.0009,
          size: 1.2 + this.rnd() * 1.2,
          size1: 0,
          color: '#BFE3FF',
          start: this.now + this.rnd() * 80,
          dur: 420 + this.rnd() * 120,
          flicker: false,
        });
      }
    }
  }

  /** 落石：目标格上方落下灰色碎石（重力加速），落地溅起碎屑 */
  rocks(pts: Array<{ x: number; y: number }>): void {
    const COLORS = ['#8A8580', '#6E6A64', '#9C968E'];
    const G = 0.0016;
    for (const c of pts) {
      for (let i = 0; i < 5; i++) {
        const rx = c.x + (this.rnd() - 0.5) * 30;
        const drop = 110 + this.rnd() * 50;
        const te = Math.sqrt((2 * drop) / G); // 落地耗时
        const delay = this.rnd() * 140;
        this.spawn({
          kind: 'dot',
          x0: rx,
          y0: c.y - drop,
          vx: 0,
          vy: 0,
          g: G,
          size: 2.5 + this.rnd() * 2,
          size1: 0,
          color: COLORS[Math.floor(this.rnd() * COLORS.length)],
          start: this.now + delay,
          dur: te,
          flicker: false,
        });
        // 落地碎裂小屑
        for (let k = 0; k < 4; k++) {
          this.spawn({
            kind: 'dot',
            x0: rx,
            y0: c.y + 2,
            vx: (this.rnd() - 0.5) * 0.12,
            vy: -(0.04 + this.rnd() * 0.06),
            g: 0.0011,
            size: 0.9 + this.rnd() * 0.9,
            size1: 0,
            color: COLORS[Math.floor(this.rnd() * COLORS.length)],
            start: this.now + delay + te,
            dur: 260 + this.rnd() * 80,
            flicker: false,
          });
        }
      }
    }
  }

  /** 乱射：多支小箭矢从施法者抛物线飞向目标格（按速度定向） */
  volley(from: { x: number; y: number }, pts: Array<{ x: number; y: number }>): void {
    const G = 0.00035;
    for (const c of pts) {
      for (let i = 0; i < 3; i++) {
        const sx = from.x + (this.rnd() - 0.5) * 10;
        const sy = from.y - 14;
        const tx = c.x + (this.rnd() - 0.5) * 22;
        const ty = c.y + (this.rnd() - 0.5) * 14;
        const dx = tx - sx;
        const dy = ty - sy;
        const dur = Math.max(160, Math.hypot(dx, dy) / 0.45);
        this.spawn({
          kind: 'arrow',
          x0: sx,
          y0: sy,
          vx: dx / dur,
          vy: (dy - 0.5 * G * dur * dur) / dur,
          g: G,
          size: 10,
          size1: 0,
          color: '#3A2A18',
          start: this.now + i * 60 + this.rnd() * 40,
          dur,
          flicker: false,
        });
      }
    }
  }

  /** 医疗：绿色光柱 + 上升光点 */
  heal(pts: Array<{ x: number; y: number }>): void {
    const COLORS = ['#8CFF8C', '#D8FFD0', '#5CE65C'];
    for (const c of pts) {
      this.spawn({
        kind: 'pillar',
        x0: c.x,
        y0: c.y,
        vx: 0,
        vy: 0,
        g: 0,
        size: 13,
        size1: 0,
        color: '#5CE65C',
        start: this.now,
        dur: 780,
        flicker: false,
      });
      for (let i = 0; i < 10; i++) {
        this.spawn({
          kind: 'dot',
          x0: c.x + (this.rnd() - 0.5) * 24,
          y0: c.y + 6 + this.rnd() * 6,
          vx: (this.rnd() - 0.5) * 0.008,
          vy: -(0.02 + this.rnd() * 0.035),
          g: -0.00001,
          size: 1.1 + this.rnd() * 1.3,
          size1: 0,
          color: COLORS[Math.floor(this.rnd() * COLORS.length)],
          start: this.now + this.rnd() * 220,
          dur: 620 + this.rnd() * 200,
          flicker: true,
        });
      }
    }
  }

  /** 增益/减益微光：对应色扩散环 + 缓升光点 */
  glow(pts: Array<{ x: number; y: number }>, color: string): void {
    for (const c of pts) {
      this.spawn({
        kind: 'ring',
        x0: c.x,
        y0: c.y,
        vx: 0,
        vy: 0,
        g: 0,
        size: 4,
        size1: 24,
        color,
        start: this.now,
        dur: 500,
        flicker: false,
      });
      for (let i = 0; i < 5; i++) {
        this.spawn({
          kind: 'dot',
          x0: c.x + (this.rnd() - 0.5) * 20,
          y0: c.y + 4,
          vx: 0,
          vy: -(0.015 + this.rnd() * 0.02),
          g: 0,
          size: 1 + this.rnd() * 1.1,
          size1: 0,
          color,
          start: this.now + this.rnd() * 160,
          dur: 520,
          flicker: true,
        });
      }
    }
  }

  // ---------- 帧视图 ----------
  views(): ParticleView[] {
    const out: ParticleView[] = [];
    for (const p of this.pool) {
      const te = this.now - p.start;
      if (te < 0 || te >= p.dur) continue;
      const t = te / p.dur;
      const x = p.x0 + p.vx * te;
      const y = p.y0 + p.vy * te + 0.5 * p.g * te * te;
      switch (p.kind) {
        case 'dot': {
          let alpha = 1 - t;
          if (p.flicker) alpha *= 0.55 + 0.45 * Math.sin(this.now * 0.06 + p.x0);
          out.push({ kind: 'dot', x, y, size: p.size * (1 - t * 0.4), color: p.color, alpha, angle: 0 });
          break;
        }
        case 'ring':
          out.push({
            kind: 'ring',
            x,
            y,
            size: p.size + (p.size1 - p.size) * (1 - (1 - t) * (1 - t)), // easeOut 扩散
            color: p.color,
            alpha: (1 - t) * 0.9,
            angle: 0,
          });
          break;
        case 'arrow': {
          const alpha = t < 0.85 ? 1 : (1 - t) / 0.15;
          out.push({ kind: 'arrow', x, y, size: p.size, color: p.color, alpha, angle: Math.atan2(p.vy + p.g * te, p.vx) });
          break;
        }
        case 'pillar':
          out.push({ kind: 'pillar', x, y, size: p.size, color: p.color, alpha: Math.sin(Math.PI * Math.min(1, t)) * 0.75, angle: 0 });
          break;
      }
    }
    return out;
  }
}

// ============================================================
// 天气常驻粒子层（位置 = hash 种子 + 时间的确定函数，无内部状态）
// ============================================================
export class WeatherLayer {
  /** 格内确定性伪随机（与 renderer.cellRand 同款 hash） */
  private h(i: number, salt: number): number {
    let x = (i * 374761393 + salt * 668265263) | 0;
    x = Math.imul(x ^ (x >>> 13), 1274126177);
    return ((x ^ (x >>> 16)) >>> 0) / 4294967296;
  }

  /** 在世界坐标系内绘制（x0,y0)-(x1,y1) 为当前可见世界范围 */
  draw(ctx: CanvasRenderingContext2D, type: Weather, x0: number, y0: number, x1: number, y1: number, now: number): void {
    if (type === 'sunny') return;
    const W = Math.max(1, x1 - x0);
    const H = Math.max(1, y1 - y0);
    switch (type) {
      case 'rain': {
        // ~120 条斜向雨丝，循环下落（横向微移保持雨丝斜率一致）
        ctx.strokeStyle = 'rgba(170,195,225,0.45)';
        ctx.lineWidth = 1;
        ctx.beginPath();
        for (let i = 0; i < 120; i++) {
          const sp = 0.45 + this.h(i, 2) * 0.3;
          const py = y0 + ((this.h(i, 3) * H + now * sp) % H);
          const px = x0 + ((this.h(i, 1) * W + now * sp * 0.267) % W);
          ctx.moveTo(px, py);
          ctx.lineTo(px - 2.4, py - 9);
        }
        ctx.stroke();
        return;
      }
      case 'snow': {
        // ~80 片雪花，左右摇摆缓落
        ctx.fillStyle = 'rgba(255,255,255,0.85)';
        for (let i = 0; i < 80; i++) {
          const sp = 0.035 + this.h(i, 2) * 0.03;
          const py = y0 + ((this.h(i, 3) * H + now * sp) % H);
          const px = x0 + this.h(i, 1) * W + Math.sin(now / 900 + this.h(i, 4) * 6.28) * 8;
          ctx.beginPath();
          ctx.arc(px, py, 1.1 + this.h(i, 5) * 1.4, 0, Math.PI * 2);
          ctx.fill();
        }
        return;
      }
      case 'fog': {
        // ~8 团大块半透明雾团缓慢漂移（径向渐隐）
        for (let i = 0; i < 8; i++) {
          const r = 100 + this.h(i, 1) * 140;
          const span = W + r * 2;
          const drift = now * 0.006 * (this.h(i, 2) > 0.5 ? 1 : -1);
          const raw = this.h(i, 3) * span + drift;
          const px = x0 - r + ((raw % span) + span) % span;
          const py = y0 + this.h(i, 4) * H;
          const grad = ctx.createRadialGradient(px, py, r * 0.15, px, py, r);
          grad.addColorStop(0, 'rgba(235,238,242,0.13)');
          grad.addColorStop(1, 'rgba(235,238,242,0)');
          ctx.fillStyle = grad;
          ctx.beginPath();
          ctx.arc(px, py, r, 0, Math.PI * 2);
          ctx.fill();
        }
        return;
      }
      case 'windy': {
        // ~30 条间歇性流线，快速横掠（周期性出现）
        ctx.lineWidth = 1.2;
        for (let i = 0; i < 30; i++) {
          const period = 2400 + this.h(i, 1) * 1600;
          const t = (now + this.h(i, 2) * period) % period;
          const act = 700;
          if (t >= act) continue;
          const p = t / act;
          const px = x0 - 80 + p * (W + 160);
          const py = y0 + this.h(i, 3) * H;
          const len = 26 + this.h(i, 4) * 30;
          ctx.strokeStyle = `rgba(232,226,206,${(Math.sin(p * Math.PI) * 0.4).toFixed(3)})`;
          ctx.beginPath();
          ctx.moveTo(px, py);
          ctx.quadraticCurveTo(px + len / 2, py - 3, px + len, py);
          ctx.stroke();
        }
        return;
      }
    }
  }
}
