// ============================================================
// 动画播放器——消费 ActionResult.events（core 已同步结算完毕），
// 按顺序演出：move 补间（0.15s/格，按兵种给步态相位供 renderer
// 做奔腾/迈步/颠簸）、attack 前冲+白闪、damage/heal 飘字（暴击大字
// 红色）、skill 目标格紫光、die 渐隐+灰叉、buff 头顶文字、turnBegin
// 回合横幅、battleEnd 结果横幅；同时在各事件演出时触发 Sfx 音效。
// 播放期间 playing=true，battleScene 据此锁定输入。
// PRD §17 打击感/计策特效：命中瞬间刀光弧线 + 屏抖（暴击加强）+
// 命中停顿帧（100ms/暴击150ms）+ 暴击红闪/屏红闪；计策按 skillId
// 发射火/水/落石/箭矢/治疗光柱等粒子（ParticleSystem，见 particles.ts）。
// ============================================================
import type { BattleEvent, ClassType, Faction, Unit } from '../core/types';
import { getSkill } from '../data';
import { Sfx } from '../audio';
import { cellToWorld } from './renderer';
import type { CellFlashView, DyingView, FloaterView, FxLayer, SlashView, UnitVisual } from './renderer';
import { ParticleSystem } from './particles';
import type { ParticleView } from './particles';

/** 横幅回调（由 battleScene 用 DOM 实现，文字更清晰） */
export type BannerFn = (text: string, kind: 'turn' | 'result', holdMs: number, sub?: string) => void;

interface Tween {
  start: number;
  dur: number;
  fn: (k: number) => void;
  done: () => void;
}
interface Visual {
  x: number;
  y: number;
  alpha: number;
  flashUntil: number;
  flashColor: string; // 受击闪色：白=普通命中，红=暴击
  movePhase: number | null; // 移动补间期间的步态相位 0-1（renderer 据此做兵种动作），静止 null
}

/** 各兵种步态循环频率（每格循环数；renderer 一个循环内部分帧/颠簸） */
const MOVE_STRIDES: Record<ClassType, number> = {
  cavalry: 2,
  infantry: 1.5,
  heavy: 1.5,
  spear: 1.5,
  archer: 1,
  strategist: 1,
  siege: 1.5,
  catapult: 1.5,
};
interface Floater {
  x: number;
  y: number;
  text: string;
  color: string;
  size: number;
  start: number;
  dur: number;
}
interface CellFlash {
  cells: Array<{ q: number; r: number }>;
  start: number;
  dur: number;
}
interface Dying {
  x: number;
  y: number;
  start: number;
  fadeDur: number; // 渐隐时长
  total: number; // 灰叉总停留时长
  classType: ClassType;
  faction: Faction;
  charId: string;
}
/** 刀光弧线（命中瞬间在受击者处扫过 120°） */
interface Slash {
  x: number;
  y: number;
  angle: number; // 攻击方向（rad）
  start: number;
  dur: number;
}

export interface PlayOpts {
  /** 敌方回合节奏：不同单位行动间插入 0.35s 停顿 */
  enemyPace?: boolean;
  getUnit: (uid: string) => Unit | undefined;
}

export class Animator implements FxLayer {
  /** 动画时钟（ms），由 update(dt) 推进 */
  private now = 0;
  playing = false;
  onBanner: BannerFn | null = null;
  private destroyed = false;

  private visuals = new Map<string, Visual>();
  private tweens: Tween[] = [];
  private floaterList: Floater[] = [];
  private flashList: CellFlash[] = [];
  private dyingList: Dying[] = [];
  private slashList: Slash[] = [];
  private readonly ps = new ParticleSystem(); // 计策粒子
  // 屏抖（不改变 camera 状态，renderer draw 时读偏移叠加）
  private shakeUntil = 0;
  private shakeDur = 1;
  private shakeAmp = 0;
  // 暴击屏红闪
  private sfUntil = 0;
  private sfDur = 1;

  // ---------- 时钟 ----------
  update(dtMs: number): void {
    if (this.destroyed) return;
    this.now += dtMs;
    this.ps.update(dtMs);
    for (let i = this.tweens.length - 1; i >= 0; i--) {
      const t = this.tweens[i];
      const k = t.dur <= 0 ? 1 : Math.min(1, Math.max(0, (this.now - t.start) / t.dur));
      t.fn(k);
      if (k >= 1) {
        this.tweens.splice(i, 1);
        t.done();
      }
    }
    this.floaterList = this.floaterList.filter(f => this.now - f.start < f.dur);
    this.flashList = this.flashList.filter(f => this.now - f.start < f.dur);
    this.dyingList = this.dyingList.filter(d => this.now - d.start < d.total);
    this.slashList = this.slashList.filter(s => this.now - s.start < s.dur);
  }

  /** 销毁：立即完成所有等待中的补间，让 play() 的 await 全部返回 */
  destroy(): void {
    this.destroyed = true;
    const ts = this.tweens.splice(0);
    for (const t of ts) t.done();
  }

  private tween(durMs: number, fn: (k: number) => void): Promise<void> {
    if (this.destroyed) return Promise.resolve();
    return new Promise(resolve => {
      this.tweens.push({ start: this.now, dur: durMs, fn, done: resolve });
    });
  }

  private wait(ms: number): Promise<void> {
    return this.tween(ms, () => {});
  }

  // ---------- 单位视觉快照 ----------
  /** 把存活单位的视觉位置对齐到逻辑坐标（战斗开始/每次行动播完后调用） */
  snapAll(units: Unit[]): void {
    this.visuals.clear();
    for (const u of units) {
      if (!u.alive) continue;
      const p = cellToWorld(u.q, u.r);
      this.visuals.set(u.uid, { x: p.x, y: p.y, alpha: 1, flashUntil: 0, flashColor: '#FFFFFF', movePhase: null });
    }
  }

  private visualOf(uid: string, getUnit: (uid: string) => Unit | undefined): Visual {
    let v = this.visuals.get(uid);
    if (!v) {
      const u = getUnit(uid);
      const p = u ? cellToWorld(u.q, u.r) : { x: 0, y: 0 };
      v = { x: p.x, y: p.y, alpha: 1, flashUntil: 0, flashColor: '#FFFFFF', movePhase: null };
      this.visuals.set(uid, v);
    }
    return v;
  }

  // ---------- FxLayer 实现（renderer 每帧查询） ----------
  unitVisual(uid: string): UnitVisual | undefined {
    const v = this.visuals.get(uid);
    if (!v) return undefined;
    return {
      x: v.x,
      y: v.y,
      alpha: v.alpha,
      flash: Math.max(0, (v.flashUntil - this.now) / 150),
      flashColor: v.flashColor,
      movePhase: v.movePhase,
    };
  }

  floaters(): FloaterView[] {
    return this.floaterList.map(f => {
      const t = Math.min(1, (this.now - f.start) / f.dur);
      return { x: f.x, y: f.y - 26 * t, text: f.text, color: f.color, size: f.size, alpha: 1 - t };
    });
  }

  cellFlashes(): CellFlashView[] {
    return this.flashList.map(f => {
      const t = Math.min(1, (this.now - f.start) / f.dur);
      return { cells: f.cells, color: '#8A2BE2', alpha: 0.55 * (1 - t) };
    });
  }

  dying(): DyingView[] {
    return this.dyingList.map(d => {
      const t = this.now - d.start;
      if (t < d.fadeDur) {
        return { x: d.x, y: d.y, alpha: 1 - t / d.fadeDur, crossed: false, classType: d.classType, faction: d.faction, charId: d.charId };
      }
      const remain = 1 - (t - d.fadeDur) / Math.max(1, d.total - d.fadeDur);
      return { x: d.x, y: d.y, alpha: Math.max(0, remain), crossed: true, classType: d.classType, faction: d.faction, charId: d.charId };
    });
  }

  /** 刀光弧线（150ms 扫过 120° 渐隐） */
  slashes(): SlashView[] {
    return this.slashList.map(s => {
      const t = Math.min(1, (this.now - s.start) / s.dur);
      return { x: s.x, y: s.y, angle: s.angle, t, alpha: 1 - t };
    });
  }

  /** 计策粒子帧视图 */
  particles(): ParticleView[] {
    return this.ps.views();
  }

  /** 屏抖偏移（屏幕 px；随剩余时间衰减，高频正弦叠加近似抖动） */
  shake(): { x: number; y: number } {
    const rem = this.shakeUntil - this.now;
    if (rem <= 0) return { x: 0, y: 0 };
    const a = this.shakeAmp * (rem / this.shakeDur);
    return { x: Math.sin(this.now * 0.55) * a, y: Math.cos(this.now * 0.43) * a };
  }

  /** 暴击屏红闪（~120ms） */
  screenFlash(): { color: string; alpha: number } | null {
    const rem = this.sfUntil - this.now;
    if (rem <= 0) return null;
    return { color: '#FF2A1A', alpha: 0.22 * (rem / this.sfDur) };
  }

  /** 动画时钟（呼吸/旗帜/天气等常驻动效的时间基准） */
  time(): number {
    return this.now;
  }

  // ---------- 特效小工具 ----------
  private addFloater(x: number, y: number, text: string, color: string, size: number, dur = 900): void {
    this.floaterList.push({ x, y: y - 24, text, color, size, start: this.now, dur });
  }

  private hitFx(v: Visual, dmg: number, crit: boolean): void {
    v.flashUntil = this.now + (crit ? 200 : 150);
    v.flashColor = crit ? '#FF3B30' : '#FFFFFF';
    if (dmg > 0) {
      // 暴击：1.8 倍大红字，停留更久
      this.addFloater(v.x, v.y, crit ? `${dmg}!` : `${dmg}`, crit ? '#FF2A1A' : '#FAFAFA', crit ? 29 : 16, crit ? 1050 : 900);
    }
    this.kickShake(crit ? 7 : 3, crit ? 250 : 150);
    if (crit) {
      this.sfDur = 120;
      this.sfUntil = this.now + 120;
    }
  }

  /** 触发一次屏抖（取更强的一次，避免连击时叠加放大） */
  private kickShake(amp: number, dur: number): void {
    if (this.now < this.shakeUntil && amp < this.shakeAmp) return;
    this.shakeAmp = amp;
    this.shakeDur = dur;
    this.shakeUntil = this.now + dur;
  }

  /** 计策粒子：按 skillId 在影响格发射对应特效（§17.2） */
  private skillFx(skillId: string, caster: Visual, cells: Array<{ q: number; r: number }>): void {
    const pts = cells.map(c => cellToWorld(c.q, c.r));
    switch (skillId) {
      case 'fire_attack':
        this.ps.fire(pts);
        break;
      case 'water_attack':
        this.ps.water(pts);
        break;
      case 'rock_slide':
        this.ps.rocks(pts);
        break;
      case 'volley':
        this.ps.volley({ x: caster.x, y: caster.y }, pts);
        break;
      case 'heal':
        this.ps.heal(pts);
        break;
      case 'rally':
        this.ps.glow(pts, '#F0C040');
        break;
      case 'confuse':
        this.ps.glow(pts, '#B26BE8');
        break;
      case 'insight':
        this.ps.glow(pts, '#4DD0E1');
        break;
      default:
        this.ps.glow(pts, '#F0C040');
        break;
    }
  }

  // ---------- 事件流播放 ----------
  async play(events: BattleEvent[], opts: PlayOpts): Promise<void> {
    if (events.length === 0) return;
    this.playing = true;
    let lastActor: string | null = null;
    try {
      for (const ev of events) {
        if (this.destroyed) return;
        // 敌方回合：每个单位行动之间留 0.35s 间隔
        if (opts.enemyPace && (ev.type === 'move' || ev.type === 'attack' || ev.type === 'skill')) {
          if (lastActor && lastActor !== ev.uid) await this.wait(350);
          lastActor = ev.uid;
        }
        switch (ev.type) {
          case 'turnBegin': {
            const sub = ev.phase === 'player' ? '我方行动' : ev.phase === 'enemy' ? '敌方行动' : '友军行动';
            this.onBanner?.(`第 ${ev.turn} 回合`, 'turn', 800, sub);
            await this.wait(750);
            break;
          }
          case 'move':
            await this.playMove(ev.uid, ev.path, opts);
            break;
          case 'attack':
            await this.playStrike(ev.uid, ev.targetUid, ev.hit, ev.crit, ev.dmg, opts, false);
            break;
          case 'counter':
            await this.playStrike(ev.uid, ev.targetUid, ev.hit, false, ev.dmg, opts, true);
            break;
          case 'damage': {
            const v = this.visualOf(ev.uid, opts.getUnit);
            v.flashUntil = this.now + 150;
            v.flashColor = '#FFFFFF';
            this.addFloater(v.x, v.y, `${ev.amount}`, '#E85D4E', 16);
            this.kickShake(2, 120); // 计策/物品伤害也带轻微屏抖
            await this.wait(280);
            break;
          }
          case 'heal': {
            Sfx.play('heal');
            const v = this.visualOf(ev.uid, opts.getUnit);
            this.addFloater(v.x, v.y, `+${ev.amount}`, '#33CC33', 16);
            this.ps.heal([{ x: v.x, y: v.y }]); // 绿色光柱+上升光点
            await this.wait(280);
            break;
          }
          case 'skill': {
            Sfx.play('skill');
            const name = getSkill(ev.skillId)?.name ?? ev.skillId;
            const v = this.visualOf(ev.uid, opts.getUnit);
            this.addFloater(v.x, v.y, `【${name}】`, '#F0C040', 15);
            this.flashList.push({ cells: ev.cells, start: this.now, dur: 500 });
            this.skillFx(ev.skillId, v, ev.cells);
            await this.wait(480);
            break;
          }
          case 'buff': {
            const v = this.visualOf(ev.uid, opts.getUnit);
            const debuff = ['ignite', 'slow', 'confuse', 'insight_expose'].includes(ev.buff.id);
            this.addFloater(v.x, v.y, ev.buff.name, debuff ? '#E85D4E' : '#F0C040', 13);
            await this.wait(240);
            break;
          }
          case 'die': {
            Sfx.play('die');
            const v = this.visualOf(ev.uid, opts.getUnit);
            const u = opts.getUnit(ev.uid);
            this.dyingList.push({
              x: v.x,
              y: v.y,
              start: this.now,
              fadeDur: 450,
              total: 900,
              classType: u?.classType ?? 'infantry',
              faction: u?.faction ?? 'enemy',
              charId: u?.charId ?? ev.uid,
            });
            v.alpha = 0;
            await this.wait(600);
            break;
          }
          case 'wait': {
            const v = this.visualOf(ev.uid, opts.getUnit);
            this.addFloater(v.x, v.y, '待机', '#AAAAAA', 12, 600);
            await this.wait(260);
            break;
          }
          case 'battleEnd': {
            Sfx.play(ev.outcome === 'win' ? 'win' : 'lose');
            this.onBanner?.(ev.outcome === 'win' ? '大  捷' : '战  败', 'result', 1200, ev.reason ?? '');
            await this.wait(1250);
            break;
          }
        }
      }
    } finally {
      this.playing = false;
    }
  }

  /** 移动补间：沿路径逐格 0.15s/格；补间期间按兵种给步态相位（renderer 据此奔腾/迈步/颠簸） */
  private async playMove(
    uid: string,
    path: Array<{ q: number; r: number }>,
    opts: PlayOpts,
  ): Promise<void> {
    if (path.length === 0) return;
    const v = this.visualOf(uid, opts.getUnit);
    const u = opts.getUnit(uid);
    if (u?.faction === 'player') Sfx.play('move'); // 玩家单位每次移动
    const strides = MOVE_STRIDES[u?.classType ?? 'infantry'];
    const pts = path.map(c => cellToWorld(c.q, c.r));
    const sx = v.x;
    const sy = v.y;
    const dur = 150 * path.length;
    await this.tween(dur, k => {
      const pos = k * pts.length;
      const i = Math.min(pts.length - 1, Math.floor(pos));
      const frac = Math.min(1, pos - i);
      const from = i === 0 ? { x: sx, y: sy } : pts[i - 1];
      const to = pts[i];
      v.x = from.x + (to.x - from.x) * frac;
      v.y = from.y + (to.y - from.y) * frac;
      v.movePhase = (k * path.length * strides) % 1;
    });
    const last = pts[pts.length - 1];
    v.x = last.x;
    v.y = last.y;
    v.movePhase = null;
  }

  /** 攻击/反击：攻击方前冲 → 命中瞬间（刀光+受击闪+飘字+屏抖）→ 命中停顿帧 → 收势 */
  private async playStrike(
    uid: string,
    targetUid: string,
    hit: boolean,
    crit: boolean,
    dmg: number,
    opts: PlayOpts,
    isCounter: boolean,
  ): Promise<void> {
    const a = this.visualOf(uid, opts.getUnit);
    const d = this.visualOf(targetUid, opts.getUnit);
    const dx = d.x - a.x;
    const dy = d.y - a.y;
    const len = Math.max(1, Math.hypot(dx, dy));
    const lunge = isCounter ? 9 : 14; // 前冲距离（世界 px）
    const ox = (dx / len) * lunge;
    const oy = (dy / len) * lunge;
    const ax = a.x;
    const ay = a.y;
    const dur = isCounter ? 200 : 240;
    // 前半程：冲出到最大伸展
    await this.tween(dur / 2, k => {
      a.x = ax + ox * k;
      a.y = ay + oy * k;
    });
    if (this.destroyed) return;
    // 命中瞬间
    if (hit) {
      Sfx.play(crit ? 'crit' : 'attack'); // 命中瞬间出刀声，暴击用暴击音
      this.hitFx(d, dmg, crit);
      this.slashList.push({ x: d.x, y: d.y, angle: Math.atan2(dy, dx), start: this.now, dur: 150 });
      await this.wait(crit ? 150 : 100); // 命中停顿帧（暴击更长）
      if (this.destroyed) return;
    } else {
      this.addFloater(d.x, d.y, 'MISS', '#AAAAAA', 14);
    }
    // 后半程：收势回位
    await this.tween(dur / 2, k => {
      a.x = ax + ox * (1 - k);
      a.y = ay + oy * (1 - k);
    });
    a.x = ax;
    a.y = ay;
    await this.wait(isCounter ? 120 : 150);
  }
}
