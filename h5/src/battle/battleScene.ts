// ============================================================
// BattleScene——战斗场景总控（对 ui 层暴露的冻结接口）。
// 职责：DOM 结构（顶部回合栏/中央 canvas/底部操作区/菜单弹窗/
// 新手引导横幅）、Pointer Events 输入分发（点选/拖拽/捏合缩放）、
// 经典行动流交互状态机（曹操传式两段操作：点选→蓝格移动→行动菜单
// [攻击/计策/物品/待机/取消移动]，移动不设 acted、行动前可撤回）、
// 与 core/battle 对接（公式一律调 core/rules 纯函数，不重写）。
// 动画与逻辑解耦：core 同步算完，事件流交 Animator 播放。
// ============================================================
import type {
  ActionResult,
  BattleOutcome,
  Difficulty,
  LevelDef,
  PartyMember,
  Selection,
  SkillDef,
  TerrainType,
  Unit,
} from '../core/types';
import { APP_VERSION } from '../data/version';
import { isLandscapeMode } from '../core/settings';
import { Battle } from '../core/battle';
import { getUnitStance } from '../core/ai';
import { hexDistance, hexRange, key } from '../core/hex';
import type { Cell } from '../core/hex';
import * as rules from '../core/rules';
import { ITEMS, TERRAIN_RULES, getItem, getSkill } from '../data';
import { Camera } from './camera';
import { Animator } from './anim';
import { BattleRenderer, EMPTY_VIEW, mapPixelSize, WEATHER_ICONS, worldToCell } from './renderer';
import type { ViewState } from './renderer';
import { CLASS_NAMES, CLASS_KEY_STAT, CLASS_TRAITS, TERRAIN_NAMES, enterBlockReason, terrainClassRules } from './fieldInfo';
import './battle.css';

// ---------- 冻结的对外接口（ui 层按此调用，签名不可改） ----------
export interface BattleSceneOptions {
  level: LevelDef;
  party: PartyMember[];
  difficulty: Difficulty;
  items?: Record<string, number>; // 出战携带的战斗物品（战后用 battle.getItemCounts() 回写存档）
  tutorial?: boolean; // 第1关新手引导
  onFinish: (outcome: BattleOutcome, battle: Battle) => void; // win/lose（撤退按 lose 上报）
}

/** 子流程（选目标/计策/物品）取消后的返回点：选中态 或 移动后行动菜单 */
type Back = { kind: 'selected'; sel: Selection } | { kind: 'actionMenu' };

/** 交互模式（底部操作区与画布高亮随模式切换） */
type Mode =
  | { kind: 'idle' }
  | { kind: 'selected'; uid: string; sel: Selection; hover: Cell[] | null }
  | { kind: 'actionMenu'; uid: string } // 已移动未行动：菜单选 攻击/计策/物品/待机/取消移动
  | { kind: 'chooseTarget'; uid: string; back: Back } // 红圈闪烁，点敌人进预览
  | { kind: 'attackPreview'; uid: string; targetUid: string; back: Back }
  | { kind: 'skills'; uid: string; back: Back }
  | { kind: 'aim'; uid: string; back: Back; skill: SkillDef; cells: Cell[] }
  | { kind: 'skillPreview'; uid: string; back: Back; skill: SkillDef; cell: Cell; cells: Cell[]; targets: Unit[] }
  | { kind: 'items'; uid: string; back: Back }
  | { kind: 'itemConfirm'; uid: string; back: Back; itemId: string }
  | { kind: 'busy' };

/** 创建带类名/文本的元素 */
function el(tag: string, cls: string, text?: string): HTMLElement {
  const e = document.createElement(tag);
  e.className = cls;
  if (text !== undefined) e.textContent = text;
  return e;
}

// 新手引导 4 步（按玩家操作逐步推进，允许跳步收口）
const TUT_ORDER = ['select', 'move', 'attack', 'endTurn'] as const;
type TutKey = (typeof TUT_ORDER)[number];
const TUT_TEXT = [
  '① 点选「李世民」（金边的己方单位）',
  '② 点击蓝色高亮格移动',
  '③ 点「攻击」再点红圈敌人',
  '④ 点击「结束回合」',
];

export class BattleScene {
  private readonly battle: Battle;
  private readonly camera = new Camera();
  private readonly renderer: BattleRenderer;
  private readonly animator = new Animator();

  // DOM
  private readonly root: HTMLElement;
  private readonly stage: HTMLElement;
  private readonly canvas: HTMLCanvasElement;
  private readonly bottom: HTMLElement;
  private readonly turnEl: HTMLElement;
  private readonly phaseEl: HTMLElement;
  private readonly weatherEl: HTMLElement;
  private readonly stanceEl: HTMLElement;
  private readonly endTurnBtn: HTMLButtonElement;
  private readonly tutoEl: HTMLElement;
  private readonly banner: HTMLElement;
  private readonly bannerText: HTMLElement;
  private readonly bannerSub: HTMLElement;
  private readonly toastEl: HTMLElement;
  private readonly terrainBar: HTMLElement;
  private readonly menuMask: HTMLElement;
  private readonly menuPanel: HTMLElement;
  private readonly ro: ResizeObserver;

  private mode: Mode = { kind: 'idle' };
  private terrainInfo: Cell | null = null; // 地形信息条当前展示的格
  private viewUid: string | null = null; // idle 状态点选敌方单位：只读属性卡
  private tutorialStep = 0;
  private finished = false;
  private destroyed = false;
  private raf = 0;
  private lastT = 0;
  private bannerTimer = 0;
  private toastTimer = 0;

  // 指针手势状态
  private pointers = new Map<number, { x: number; y: number }>();
  private tapStart: { x: number; y: number } | null = null;
  private panning = false;
  private pinchPrev: { dist: number; mx: number; my: number } | null = null;

  constructor(container: HTMLElement, private opts: BattleSceneOptions) {
    this.battle = new Battle(opts.level, opts.party, opts.difficulty, 1, opts.items);

    // ---------- DOM 结构 ----------
    this.root = el('div', 'zg-battle');
    // 顶部回合栏（36px：回合数/天气/菜单）
    const topbar = el('div', 'zg-topbar');
    this.turnEl = el('span', 'zg-turn');
    this.phaseEl = el('span', 'zg-phase player');
    this.weatherEl = el('span', 'zg-weather');
    this.stanceEl = el('span', 'zg-stance');
    const mid = el('div', 'zg-top-mid');
    mid.appendChild(this.phaseEl);
    mid.appendChild(this.weatherEl);
    mid.appendChild(this.stanceEl);
    const menuBtn = document.createElement('button');
    menuBtn.type = 'button';
    menuBtn.className = 'zg-menu-btn';
    menuBtn.textContent = '☰';
    menuBtn.addEventListener('click', () => this.openMenu());
    // 常驻「结束回合」（任何交互模式下都可结束回合，不仅 idle 底栏）
    this.endTurnBtn = document.createElement('button');
    this.endTurnBtn.type = 'button';
    this.endTurnBtn.className = 'zg-endturn-top';
    this.endTurnBtn.textContent = '结束回合';
    this.endTurnBtn.addEventListener('click', () => void this.doEndTurn());
    topbar.appendChild(this.turnEl);
    topbar.appendChild(mid);
    topbar.appendChild(this.endTurnBtn);
    topbar.appendChild(menuBtn);
    this.root.appendChild(topbar);
    // 新手引导横幅
    this.tutoEl = el('div', 'zg-tuto hide');
    this.root.appendChild(this.tutoEl);
    // 中央 canvas
    this.stage = el('div', 'zg-stage');
    this.canvas = document.createElement('canvas');
    this.canvas.className = 'zg-canvas';
    this.stage.appendChild(this.canvas);
    this.root.appendChild(this.stage);
    // 底部操作区（约 120px）
    this.bottom = el('div', 'zg-bottom');
    this.root.appendChild(this.bottom);
    // 回合/结果横幅（DOM 覆盖层）
    this.banner = el('div', 'zg-banner');
    this.bannerText = el('div', 'zg-banner-text');
    this.bannerSub = el('div', 'zg-banner-sub');
    this.banner.appendChild(this.bannerText);
    this.banner.appendChild(this.bannerSub);
    this.root.appendChild(this.banner);
    // 轻提示
    this.toastEl = el('div', 'zg-toast');
    this.root.appendChild(this.toastEl);
    // 地形信息条（idle 点空白格浮出，底部操作区上方）
    this.terrainBar = el('div', 'zg-terrainbar hide');
    this.root.appendChild(this.terrainBar);
    // 菜单弹窗
    this.menuMask = el('div', 'zg-menu-mask');
    this.menuPanel = el('div', 'zg-menu-panel');
    this.menuMask.appendChild(this.menuPanel);
    this.menuMask.addEventListener('click', e => {
      if (e.target === this.menuMask) this.closeMenu();
    });
    this.root.appendChild(this.menuMask);
    container.appendChild(this.root);

    // ---------- 渲染/相机 ----------
    this.renderer = new BattleRenderer(this.canvas);
    const size = mapPixelSize(opts.level.width, opts.level.height);
    this.camera.setMap(size.w, size.h);
    this.ro = new ResizeObserver(() => this.onResize());
    this.ro.observe(this.stage);
    this.onResize();

    // ---------- 动画器 ----------
    this.animator.snapAll(this.battle.state.units);
    this.animator.onBanner = (text, kind, holdMs, sub) => this.showBanner(text, kind, holdMs, sub);

    // ---------- 输入（Pointer Events 统一鼠标/触控） ----------
    this.canvas.addEventListener('pointerdown', this.onPointerDown);
    this.canvas.addEventListener('pointermove', this.onPointerMove);
    this.canvas.addEventListener('pointerup', this.onPointerUp);
    this.canvas.addEventListener('pointercancel', this.onPointerCancel);
    this.canvas.addEventListener('pointerleave', this.onPointerLeave);
    this.canvas.addEventListener('wheel', this.onWheel, { passive: false });
    this.canvas.addEventListener('contextmenu', e => e.preventDefault());

    this.updateTopbar();
    this.renderBottom();
    this.renderTutorial();
    // 开局横幅：副标题展示胜利条件（☰ 菜单内也有胜/败条件两行）
    this.showBanner(`第 ${this.battle.state.turn} 回合`, 'turn', 1100, `胜利条件：${this.victoryText()}`);
    this.raf = requestAnimationFrame(this.tick);
  }

  /** 移除全部 DOM 监听/canvas/RAF */
  destroy(): void {
    if (this.destroyed) return;
    this.destroyed = true;
    cancelAnimationFrame(this.raf);
    this.ro.disconnect();
    this.animator.destroy();
    window.clearTimeout(this.bannerTimer);
    window.clearTimeout(this.toastTimer);
    this.root.remove();
  }

  // ============================================================
  // 渲染循环
  // ============================================================
  private tick = (t: number): void => {
    if (this.destroyed) return;
    const dt = this.lastT === 0 ? 16 : Math.min(50, t - this.lastT);
    this.lastT = t;
    this.animator.update(dt);
    this.renderer.draw(this.battle.state, this.buildView(), this.animator, this.camera);
    this.raf = requestAnimationFrame(this.tick);
  };

  private onResize(): void {
    this.applyLandscape();
    const w = this.stage.clientWidth;
    const h = this.stage.clientHeight;
    if (w <= 0 || h <= 0) return;
    this.renderer.resize(w, h);
    this.camera.setView(w, h);
  }

  /** 若设置开启 PC 横屏且视口足够宽，切换为右侧操作栏布局 */
  private applyLandscape(): void {
    const enabled = isLandscapeMode();
    const wide = window.innerWidth >= 640 && window.innerWidth > window.innerHeight;
    this.root.classList.toggle('landscape', enabled && wide);
  }

  /** 按当前交互模式组装渲染视图 */
  private buildView(): ViewState {
    const m = this.mode;
    switch (m.kind) {
      case 'selected':
        return {
          ...EMPTY_VIEW,
          selectedUid: m.uid,
          reachable: m.sel.reachable,
          attackable: m.sel.attackable,
          pathPreview: m.hover ?? [],
        };
      case 'actionMenu':
        // 新位置可攻击敌人红圈
        return { ...EMPTY_VIEW, selectedUid: m.uid, attackable: this.attackTargetCells(m.uid) };
      case 'chooseTarget':
        return { ...EMPTY_VIEW, selectedUid: m.uid, attackable: this.attackTargetCells(m.uid), blinkAttack: true };
      case 'attackPreview': {
        const t = this.battle.getUnit(m.targetUid);
        return {
          ...EMPTY_VIEW,
          selectedUid: m.uid,
          attackable: t ? [{ q: t.q, r: t.r }] : [],
          previewTargetUid: m.targetUid,
          blinkAttack: true,
        };
      }
      case 'skills':
      case 'items':
      case 'itemConfirm':
        return { ...EMPTY_VIEW, selectedUid: m.uid };
      case 'aim':
        return { ...EMPTY_VIEW, selectedUid: m.uid, skillCells: m.cells };
      case 'skillPreview':
        return {
          ...EMPTY_VIEW,
          selectedUid: m.uid,
          skillCells: m.cells,
          targetCell: m.skill.aoe === 'all' ? null : m.cell,
        };
      default:
        return EMPTY_VIEW;
    }
  }

  // ============================================================
  // 输入：Pointer Events（位移>8px 视为拖拽不触发点选）
  // ============================================================
  private evPos(e: PointerEvent | WheelEvent): { x: number; y: number } {
    const r = this.canvas.getBoundingClientRect();
    return { x: e.clientX - r.left, y: e.clientY - r.top };
  }

  private pinchState(): { dist: number; mx: number; my: number } | null {
    const pts = [...this.pointers.values()];
    if (pts.length < 2) return null;
    return {
      dist: Math.hypot(pts[0].x - pts[1].x, pts[0].y - pts[1].y),
      mx: (pts[0].x + pts[1].x) / 2,
      my: (pts[0].y + pts[1].y) / 2,
    };
  }

  private onPointerDown = (e: PointerEvent): void => {
    this.canvas.setPointerCapture(e.pointerId);
    const p = this.evPos(e);
    this.pointers.set(e.pointerId, p);
    if (this.pointers.size === 1) {
      this.tapStart = p;
      this.panning = false;
    } else if (this.pointers.size === 2) {
      this.panning = true; // 双指操作不触发点选
      this.pinchPrev = this.pinchState();
    }
  };

  private onPointerMove = (e: PointerEvent): void => {
    const p = this.evPos(e);
    if (!this.pointers.has(e.pointerId)) {
      this.updateHover(p.x, p.y); // 鼠标悬停路径预览
      return;
    }
    const prev = this.pointers.get(e.pointerId)!;
    this.pointers.set(e.pointerId, p);
    if (this.pointers.size === 1) {
      if (this.tapStart && !this.panning) {
        if (Math.hypot(p.x - this.tapStart.x, p.y - this.tapStart.y) > 8) this.panning = true;
      }
      if (this.panning) {
        this.camera.panBy(p.x - prev.x, p.y - prev.y);
        const m = this.mode;
        if (m.kind === 'selected') m.hover = null;
      }
    } else if (this.pointers.size === 2) {
      const cur = this.pinchState();
      if (cur && this.pinchPrev && this.pinchPrev.dist > 0) {
        this.camera.zoomAt(cur.mx, cur.my, cur.dist / this.pinchPrev.dist);
        this.camera.panBy(cur.mx - this.pinchPrev.mx, cur.my - this.pinchPrev.my);
      }
      this.pinchPrev = cur;
    }
  };

  private onPointerUp = (e: PointerEvent): void => {
    const p = this.evPos(e);
    const wasTap = this.pointers.size === 1 && !this.panning && this.tapStart !== null;
    this.pointers.delete(e.pointerId);
    if (this.pointers.size < 2) this.pinchPrev = null;
    if (this.pointers.size === 0) {
      if (wasTap) this.handleTap(p.x, p.y);
      this.tapStart = null;
      this.panning = false;
    } else if (this.pointers.size === 1) {
      // 双指抬起其一：剩余指重置为拖拽起点，避免误触发点选
      this.tapStart = [...this.pointers.values()][0];
      this.panning = true;
    }
  };

  private onPointerCancel = (e: PointerEvent): void => {
    this.pointers.delete(e.pointerId);
    if (this.pointers.size < 2) this.pinchPrev = null;
    if (this.pointers.size === 0) {
      this.tapStart = null;
      this.panning = false;
    }
  };

  private onPointerLeave = (): void => {
    const m = this.mode;
    if (m.kind === 'selected') m.hover = null;
  };

  private onWheel = (e: WheelEvent): void => {
    e.preventDefault();
    const p = this.evPos(e);
    this.camera.zoomAt(p.x, p.y, Math.exp(-e.deltaY * 0.0012));
  };

  /** 鼠标悬停：选中态下预览到可达格的路径 */
  private updateHover(x: number, y: number): void {
    const m = this.mode;
    if (m.kind !== 'selected' || this.animator.playing) return;
    const u = this.battle.getUnit(m.uid);
    if (!u) return;
    const w = this.camera.screenToWorld(x, y);
    const lv = this.opts.level;
    const cell = worldToCell(w.x, w.y, lv.width, lv.height);
    let hover: Cell[] | null = null;
    if (cell && m.sel.reachable.some(c => c.q === cell.q && c.r === cell.r)) {
      hover = this.battle.pathFor(u, cell);
    }
    m.hover = hover;
  }

  // ============================================================
  // 点选分发（交互状态机）
  // ============================================================
  private unitAt(q: number, r: number): Unit | undefined {
    return this.battle.state.units.find(u => u.alive && u.q === q && u.r === r);
  }

  /** 与 core/battle.isHostile 同口径：enemy 与 非enemy 互为敌对 */
  private isHostile(a: Unit, b: Unit): boolean {
    return (a.faction === 'enemy') !== (b.faction === 'enemy');
  }

  private handleTap(x: number, y: number): void {
    if (this.destroyed || this.finished || this.animator.playing) return;
    if (this.battle.state.phase !== 'player') return;
    const m = this.mode;
    if (m.kind === 'busy') return;
    const lv = this.opts.level;
    const w = this.camera.screenToWorld(x, y);
    const cell = worldToCell(w.x, w.y, lv.width, lv.height);
    const tapped = cell ? this.unitAt(cell.q, cell.r) : undefined;

    switch (m.kind) {
      case 'idle': {
        if (tapped && tapped.faction === 'player') {
          this.hideTerrainInfo();
          if (tapped.acted) this.toast('该单位本回合已行动');
          else this.select(tapped);
          return;
        }
        if (tapped) {
          // 敌方/友军单位：只读属性卡（再点同一单位收起，点其他单位切换）
          this.hideTerrainInfo();
          this.viewUid = this.viewUid === tapped.uid ? null : tapped.uid;
          this.renderBottom();
          return;
        }
        if (cell && !tapped) {
          // 地形信息条：点空白格浮出；再点同格或图外收起，点其他空白格切换
          if (this.terrainInfo && this.terrainInfo.q === cell.q && this.terrainInfo.r === cell.r) this.hideTerrainInfo();
          else this.showTerrainInfo(cell);
          return;
        }
        if (!cell) {
          this.hideTerrainInfo();
          if (this.viewUid) {
            this.viewUid = null;
            this.renderBottom();
          }
        }
        return;
      }
      case 'selected': {
        if (!cell) {
          this.setIdle();
          return;
        }
        if (tapped && tapped.uid === m.uid) {
          this.setIdle();
          return;
        }
        if (tapped && tapped.faction === 'player') {
          if (tapped.acted) this.toast('该单位本回合已行动');
          else this.select(tapped);
          return;
        }
        if (tapped && this.isHostile(this.mustGet(m.uid), tapped)) {
          // 快捷入口：当前位置射程内的敌人可直接点开预览（等价于 攻击→选目标）
          if (m.sel.attackable.some(c => c.q === tapped.q && c.r === tapped.r)) {
            this.mode = { kind: 'attackPreview', uid: m.uid, targetUid: tapped.uid, back: { kind: 'selected', sel: m.sel } };
            this.renderBottom();
          } else {
            this.toast('超出攻击范围，请先点蓝格移动靠近');
          }
          return;
        }
        if (m.sel.reachable.some(c => c.q === cell.q && c.r === cell.r)) {
          void this.doMove(m, cell);
          return;
        }
        // 点到不可进入地形：toast 具体原因（保持选中，如「骑兵不可进入山地」）
        const blocked = enterBlockReason(this.mustGet(m.uid), this.terrainAt(cell.q, cell.r));
        if (blocked) {
          this.toast(blocked);
          return;
        }
        this.setIdle();
        return;
      }
      case 'actionMenu': {
        // 已移动：格子点击无效，必须从菜单选行动（或取消移动撤回）
        this.toast('已移动：请从下方菜单选择行动（或「取消移动」撤回）');
        return;
      }
      case 'chooseTarget': {
        const u = this.mustGet(m.uid);
        if (tapped && this.isHostile(u, tapped) && hexDistance(u.q, u.r, tapped.q, tapped.r) <= u.range) {
          this.mode = { kind: 'attackPreview', uid: m.uid, targetUid: tapped.uid, back: m.back };
          this.renderBottom();
        } else {
          this.backTo(m);
        }
        return;
      }
      case 'attackPreview': {
        // 点画布任意处返回选目标
        this.mode = { kind: 'chooseTarget', uid: m.uid, back: m.back };
        this.renderBottom();
        return;
      }
      case 'skills': {
        this.backTo(m);
        return;
      }
      case 'aim': {
        if (cell && m.cells.some(c => c.q === cell.q && c.r === cell.r)) {
          this.openSkillPreview(m, cell);
        } else {
          this.mode = { kind: 'skills', uid: m.uid, back: m.back };
          this.renderBottom();
        }
        return;
      }
      case 'skillPreview': {
        // 点画布返回目标选择（全体技返回计策列表）
        if (m.skill.aoe === 'all') this.mode = { kind: 'skills', uid: m.uid, back: m.back };
        else this.mode = { kind: 'aim', uid: m.uid, back: m.back, skill: m.skill, cells: this.skillRangeCells(this.mustGet(m.uid), m.skill) };
        this.renderBottom();
        return;
      }
      case 'items': {
        this.backTo(m);
        return;
      }
      case 'itemConfirm': {
        this.mode = { kind: 'items', uid: m.uid, back: m.back };
        this.renderBottom();
        return;
      }
    }
  }

  private mustGet(uid: string): Unit {
    const u = this.battle.getUnit(uid);
    if (!u) throw new Error(`单位不存在: ${uid}`);
    return u;
  }

  private select(u: Unit): void {
    this.hideTerrainInfo();
    this.viewUid = null;
    const sel = this.battle.selectUnit(u.uid);
    this.mode = { kind: 'selected', uid: u.uid, sel, hover: null };
    if (u.isHero) this.tutorialAdvance('select');
    this.renderBottom();
  }

  private setIdle(): void {
    this.mode = { kind: 'idle' };
    this.viewUid = null;
    this.renderBottom();
  }

  /** 子流程取消后返回：选中态 或 移动后行动菜单 */
  private backTo(m: { uid: string; back: Back }): void {
    if (m.back.kind === 'selected') this.mode = { kind: 'selected', uid: m.uid, sel: m.back.sel, hover: null };
    else this.mode = { kind: 'actionMenu', uid: m.uid };
    this.renderBottom();
  }

  /** 当前位置射程内存活的敌对单位所在格（行动菜单/选目标红圈用） */
  private attackTargetCells(uid: string): Cell[] {
    const u = this.battle.getUnit(uid);
    if (!u) return [];
    return this.battle.state.units
      .filter(t => t.alive && this.isHostile(u, t) && hexDistance(u.q, u.r, t.q, t.r) <= u.range)
      .map(t => ({ q: t.q, r: t.r }));
  }

  /** 计策可施放格（施放距离内的全部格，含自身格） */
  private skillRangeCells(u: Unit, skill: SkillDef): Cell[] {
    const lv = this.opts.level;
    return hexRange(u.q, u.r, skill.range).filter(c => c.q >= 0 && c.q < lv.width && c.r >= 0 && c.r < lv.height);
  }

  /** 物品库存是否有剩（入口按钮置灰用） */
  private hasItems(): boolean {
    const counts = this.battle.getItemCounts();
    return ITEMS.some(it => (counts[it.id] ?? 0) > 0);
  }

  // ============================================================
  // 行动执行（调 core → 播事件流 → 收尾）
  // ============================================================
  private async runAction(res: ActionResult, busyText: string): Promise<void> {
    if (!res.ok) {
      this.toast(res.reason ?? '操作失败');
      this.setIdle();
      return;
    }
    this.mode = { kind: 'busy' };
    this.renderBottom(busyText);
    await this.animator.play(res.events, { getUnit: uid => this.battle.getUnit(uid) });
    this.settle();
  }

  /** 每次行动/回合播放完毕后的统一收尾 */
  private settle(): void {
    if (this.destroyed) return;
    this.animator.snapAll(this.battle.state.units);
    this.mode = { kind: 'idle' };
    this.viewUid = null;
    this.hideTerrainInfo(); // 行动后地形信息条消失
    this.updateTopbar();
    this.renderBottom();
    const over = this.battle.isOver();
    if (over) {
      this.finish(over.outcome);
      return;
    }
    if (this.allActed()) this.toast('全部单位已行动，请点击「结束回合」');
  }

  private allActed(): boolean {
    const ps = this.battle.state.units.filter(u => u.alive && u.faction === 'player');
    return ps.length > 0 && ps.every(u => u.acted);
  }

  /** 经典行动流：仅移动（不设 acted），播完动画进入行动菜单 */
  private async doMove(m: { uid: string; sel: Selection }, cell: Cell): Promise<void> {
    const u = this.mustGet(m.uid);
    const path = this.battle.pathFor(u, cell);
    if (!path) {
      this.toast('无法到达该格');
      return;
    }
    const res = this.battle.moveUnit(u.uid, path);
    if (!res.ok) {
      this.toast(res.reason ?? '无法移动');
      this.setIdle();
      return;
    }
    this.mode = { kind: 'busy' };
    this.renderBottom('移动中…');
    this.tutorialAdvance('move');
    await this.animator.play(res.events, { getUnit: uid => this.battle.getUnit(uid) });
    if (this.destroyed) return;
    this.animator.snapAll(this.battle.state.units);
    this.mode = { kind: 'actionMenu', uid: m.uid };
    this.renderBottom();
  }

  /** 取消移动：core 撤回 + 播回移动画，完成后回到选中态（重新计算可达格） */
  private async doCancelMove(uid: string): Promise<void> {
    const res = this.battle.cancelMove(uid);
    if (!res.ok) {
      this.toast(res.reason ?? '当前不可撤回');
      return;
    }
    this.mode = { kind: 'busy' };
    this.renderBottom('撤回移动…');
    await this.animator.play(res.events, { getUnit: id => this.battle.getUnit(id) });
    if (this.destroyed) return;
    this.animator.snapAll(this.battle.state.units);
    this.select(this.mustGet(uid));
  }

  /** 「攻击」入口：进入选目标（无目标时按钮已置灰，此处双保险） */
  private openChooseTarget(): void {
    const m = this.mode;
    if (m.kind !== 'selected' && m.kind !== 'actionMenu') return;
    if (this.attackTargetCells(m.uid).length === 0) {
      this.toast('射程内没有敌人');
      return;
    }
    const back: Back = m.kind === 'selected' ? { kind: 'selected', sel: m.sel } : { kind: 'actionMenu' };
    this.mode = { kind: 'chooseTarget', uid: m.uid, back };
    this.renderBottom();
  }

  private confirmAttack(): void {
    const m = this.mode;
    if (m.kind !== 'attackPreview') return;
    // 原地普攻（无论是否已移动，单位都已在当前格；含反击结算）
    const res = this.battle.attackWith(m.uid, m.targetUid);
    this.tutorialAdvance('attack');
    void this.runAction(res, '攻击中…');
  }

  private doWait(): void {
    const m = this.mode;
    if (m.kind !== 'selected' && m.kind !== 'actionMenu') return;
    const res = this.battle.wait(m.uid);
    void this.runAction(res, '待机…');
  }

  private openSkills(): void {
    const m = this.mode;
    if (m.kind !== 'selected' && m.kind !== 'actionMenu') return;
    const back: Back = m.kind === 'selected' ? { kind: 'selected', sel: m.sel } : { kind: 'actionMenu' };
    this.mode = { kind: 'skills', uid: m.uid, back };
    this.renderBottom();
  }

  private pickSkill(skill: SkillDef): void {
    const m = this.mode;
    if (m.kind !== 'skills') return;
    const u = this.mustGet(m.uid);
    const cost = Math.round(skill.mp * u.mpCostMult);
    if (u.mp < cost) {
      this.toast('MP 不足');
      return;
    }
    if (skill.aoe === 'all') {
      // 全体技（洞察）：无需目标格，直接确认
      const targets = this.battle.state.units.filter(t => t.alive && this.isHostile(u, t));
      this.mode = { kind: 'skillPreview', uid: m.uid, back: m.back, skill, cell: { q: u.q, r: u.r }, cells: [], targets };
    } else {
      this.mode = { kind: 'aim', uid: m.uid, back: m.back, skill, cells: this.skillRangeCells(u, skill) };
    }
    this.renderBottom();
  }

  private openSkillPreview(m: { uid: string; back: Back; skill: SkillDef }, cell: Cell): void {
    const u = this.mustGet(m.uid);
    const lv = this.opts.level;
    const cells = rules.skillAoeCells(m.skill, u.q, u.r, cell.q, cell.r, lv.width, lv.height);
    const friendly = m.skill.kind === 'heal' || m.skill.kind === 'buff';
    const targets = this.battle.state.units.filter(
      t =>
        t.alive &&
        cells.some(c => c.q === t.q && c.r === t.r) &&
        (friendly ? !this.isHostile(u, t) : this.isHostile(u, t)),
    );
    this.mode = { kind: 'skillPreview', uid: m.uid, back: m.back, skill: m.skill, cell, cells, targets };
    this.renderBottom();
  }

  private confirmSkill(): void {
    const m = this.mode;
    if (m.kind !== 'skillPreview') return;
    const res =
      m.skill.aoe === 'all'
        ? this.battle.useSkill(m.uid, m.skill.id)
        : this.battle.useSkill(m.uid, m.skill.id, m.cell);
    void this.runAction(res, '施放计策…');
  }

  private openItems(): void {
    const m = this.mode;
    if (m.kind !== 'selected' && m.kind !== 'actionMenu') return;
    if (!this.hasItems()) {
      this.toast('没有可用物品');
      return;
    }
    const back: Back = m.kind === 'selected' ? { kind: 'selected', sel: m.sel } : { kind: 'actionMenu' };
    this.mode = { kind: 'items', uid: m.uid, back };
    this.renderBottom();
  }

  private pickItem(itemId: string): void {
    const m = this.mode;
    if (m.kind !== 'items') return;
    const counts = this.battle.getItemCounts();
    if ((counts[itemId] ?? 0) <= 0) {
      this.toast('该物品已用完');
      return;
    }
    this.mode = { kind: 'itemConfirm', uid: m.uid, back: m.back, itemId };
    this.renderBottom();
  }

  private confirmItem(): void {
    const m = this.mode;
    if (m.kind !== 'itemConfirm') return;
    const item = getItem(m.itemId);
    const res = this.battle.useItem(m.uid, m.itemId);
    // 清心丸等无专用事件（events 为空）时给个反馈
    if (res.ok && res.events.length === 0 && item) this.toast(`「${item.name}」生效`);
    void this.runAction(res, '使用物品…');
  }

  /** 结束回合 → 播放敌方回合事件流（单位间 0.35s 间隔） */
  private async doEndTurn(): Promise<void> {
    if (this.animator.playing || this.finished || this.destroyed) return;
    if (this.battle.state.phase !== 'player') return;
    this.mode = { kind: 'busy' };
    this.renderBottom('敌方行动中…');
    this.tutorialAdvance('endTurn');
    const res = this.battle.endTurn();
    if (!res.ok) {
      this.toast(res.reason ?? '无法结束回合');
      this.settle();
      return;
    }
    this.updateTopbar(); // 敌方回合开始，立即显示态势栏
    await this.animator.play(res.events, { enemyPace: true, getUnit: uid => this.battle.getUnit(uid) });
    this.settle();
  }

  private finish(outcome: BattleOutcome): void {
    if (this.finished || this.destroyed) return;
    this.finished = true;
    this.opts.onFinish(outcome, this.battle);
  }

  // ============================================================
  // 底部操作区
  // ============================================================
  private makeBtn(label: string, cls: string, onClick: () => void, disabled = false): HTMLButtonElement {
    const b = document.createElement('button');
    b.type = 'button';
    b.className = cls;
    b.textContent = label;
    b.disabled = disabled;
    b.addEventListener('click', () => {
      if (!b.disabled) onClick();
    });
    return b;
  }

  private terrainAt(q: number, r: number): TerrainType {
    return this.opts.level.terrain[key(q, r)] ?? 'plain';
  }

  /** 地形信息条：地形名 + 防御/移耗（读 TERRAIN_RULES，0 修正省略）+ 兵种特别规则；§16.1 起回避/命中修正已移除不再显示 */
  private showTerrainInfo(cell: Cell): void {
    this.terrainInfo = cell;
    this.viewUid = null; // 地形条与单位属性卡互斥
    this.renderBottom();
    const t = this.terrainAt(cell.q, cell.r);
    const rule = TERRAIN_RULES[t];
    const bar = this.terrainBar;
    bar.textContent = '';
    const pct = (v: number): string => (v > 0 ? `+${v}%` : `${v}%`);
    const mods: string[] = [];
    if (rule.defense !== 0) mods.push(`防御${pct(rule.defense)}`);
    mods.push(rule.impassable ? '不可进入' : `移耗${rule.moveCost}`);
    const title = el('div', 'zg-tb-title');
    title.appendChild(el('span', 'zg-tb-name', TERRAIN_NAMES[t]));
    title.appendChild(el('span', 'zg-tb-mods', mods.join('　')));
    bar.appendChild(title);
    const specials = terrainClassRules(t);
    if (specials.length > 0) bar.appendChild(el('div', 'zg-tb-rule', specials.join('；')));
    bar.classList.remove('hide');
  }

  private hideTerrainInfo(): void {
    if (!this.terrainInfo) return;
    this.terrainInfo = null;
    this.terrainBar.classList.add('hide');
  }

  /** 选中/行动菜单共用的行动按钮行（攻击无目标置灰、计策无技能置灰、物品无库存置灰） */
  private actionRow(uid: string, skillsCount: number): HTMLElement {
    const row = el('div', 'zg-btnrow');
    row.appendChild(
      this.makeBtn('攻击', 'zg-btn', () => this.openChooseTarget(), this.attackTargetCells(uid).length === 0),
    );
    row.appendChild(this.makeBtn('计策', 'zg-btn', () => this.openSkills(), skillsCount === 0));
    row.appendChild(this.makeBtn('物品', 'zg-btn', () => this.openItems(), !this.hasItems()));
    row.appendChild(this.makeBtn('待机', 'zg-btn', () => this.doWait()));
    return row;
  }

  /**
   * 单位属性卡（第三轮迭代）：第 1 行 名字/Boss金「将」/兵种/Lv/HP/MP；
   * 第 2 行 五维等宽列（关键属性金色高亮：骑兵步兵弓兵器械→武、重步→统、
   * 谋士→智且行尾追加「计策伤害与智力挂钩」）；第 3 行 移动力/射程/兵种特性。
   * 敌方单位只读查看时行末追加暗色小字「敌方单位」。
   */
  private unitCard(u: Unit): HTMLElement {
    const card = el('div', 'zg-unitcard');
    // 第 1 行
    const r1 = el('div', 'zg-uc-row1');
    r1.appendChild(el('span', 'zg-uc-name', u.name));
    if (u.isBoss) r1.appendChild(el('span', 'zg-uc-boss', '将'));
    r1.appendChild(el('span', 'zg-uc-sub', `${CLASS_NAMES[u.classType]}　Lv.${u.level}`));
    r1.appendChild(el('span', 'zg-uc-hpmp', `HP ${u.hp}/${u.maxHp}　MP ${u.mp}/${u.maxMp}`));
    card.appendChild(r1);
    // 第 2 行（五维）
    const keyStat = CLASS_KEY_STAT[u.classType];
    const defs: Array<[label: string, value: number, k: string]> = [
      ['武', u.stats.str, 'str'],
      ['统', u.stats.cmd, 'cmd'],
      ['智', u.stats.int, 'int'],
      ['敏', u.stats.agi, 'agi'],
      ['运', u.stats.luk, 'luk'],
    ];
    const r2 = el('div', 'zg-uc-stats');
    for (const [label, value, k] of defs) {
      const cell = el('div', `zg-uc-stat${k === keyStat ? ' key' : ''}`);
      cell.appendChild(el('span', 'zg-uc-sl', label));
      cell.appendChild(el('span', 'zg-uc-sv', String(value)));
      r2.appendChild(cell);
    }
    if (u.classType === 'strategist') r2.appendChild(el('span', 'zg-uc-note', '计策伤害与智力挂钩'));
    card.appendChild(r2);
    // 第 3 行
    card.appendChild(el('div', 'zg-uc-row3', `移动力 ${u.move}　射程 ${u.range}　${CLASS_TRAITS[u.classType]}`));
    if (u.faction === 'enemy') card.appendChild(el('div', 'zg-uc-enemy', '敌方单位'));
    return card;
  }

  private stanceText(): string {
    const enemies = this.battle.state.units.filter(u => u.alive && u.faction === 'enemy');
    const counts = { offensive: 0, defensive: 0, neutral: 0 };
    for (const u of enemies) counts[getUnitStance(this.battle, u)] += 1;
    return `敌方态势：攻${counts.offensive} 守${counts.defensive} 中${counts.neutral}`;
  }

  private renderBottom(busyText = '行动中…'): void {
    const b = this.bottom;
    b.textContent = '';
    const m = this.mode;
    // PC 横屏时把敌方态势放在右侧操作栏顶部（避免顶部空间不足被截断）
    if (this.battle.state.phase === 'enemy') {
      b.appendChild(el('div', 'zg-side-status', this.stanceText()));
    }
    switch (m.kind) {
      case 'idle': {
        // idle 点选敌方单位：只读属性卡（无行动按钮）
        const vu = this.viewUid ? this.battle.getUnit(this.viewUid) : undefined;
        if (vu && vu.alive) {
          b.appendChild(this.unitCard(vu));
          return;
        }
        const all = this.allActed();
        b.appendChild(el('div', 'zg-hint', all ? '全部单位已行动' : '点选己方单位行动'));
        b.appendChild(
          this.makeBtn('结束回合', `zg-btn zg-btn-end${all ? ' pulse' : ''}`, () => void this.doEndTurn()),
        );
        return;
      }
      case 'selected': {
        const u = this.battle.getUnit(m.uid);
        if (!u) {
          this.setIdle();
          return;
        }
        b.appendChild(this.unitCard(u));
        const row = this.actionRow(m.uid, u.skills.length);
        row.appendChild(this.makeBtn('取消', 'zg-btn', () => this.setIdle()));
        b.appendChild(row);
        return;
      }
      case 'actionMenu': {
        const u = this.battle.getUnit(m.uid);
        if (!u) {
          this.setIdle();
          return;
        }
        b.appendChild(this.actionRow(m.uid, u.skills.length));
        b.appendChild(this.makeBtn('取消移动', 'zg-btn', () => void this.doCancelMove(m.uid)));
        return;
      }
      case 'chooseTarget': {
        b.appendChild(el('div', 'zg-hint', '点击红圈敌人发起攻击（点空地返回）'));
        b.appendChild(this.makeBtn('返回', 'zg-btn', () => this.backTo(m)));
        return;
      }
      case 'attackPreview': {
        const u = this.battle.getUnit(m.uid);
        const t = this.battle.getUnit(m.targetUid);
        if (!u || !t) {
          this.setIdle();
          return;
        }
        // 预览数值全部用 core/rules 纯函数（与 core 结算同口径）；§16.1 起命中恒 100，不再显示
        const terrain = this.terrainAt(t.q, t.r);
        const crit = Math.round(rules.critRate(u));
        const lo = rules.attackDamage(u, t, { terrain, crit: false, rng: () => 0 });
        const hi = rules.attackDamage(u, t, { terrain, crit: false, rng: () => 0.9999 });
        const dist = hexDistance(u.q, u.r, t.q, t.r);
        const countered = rules.canCounter(u, t, dist);
        const panel = el('div', 'zg-preview');
        panel.appendChild(el('div', 'zg-preview-title', `${u.name} → ${t.name}`));
        panel.appendChild(el('div', 'zg-preview-line', `暴击 ${crit}%　伤害 ${lo}~${hi}`));
        if (countered) panel.appendChild(el('div', 'zg-preview-warn', '⚠ 目标可能反击'));
        b.appendChild(panel);
        const row = el('div', 'zg-btnrow');
        row.appendChild(this.makeBtn('确认攻击', 'zg-btn zg-btn-primary', () => this.confirmAttack()));
        row.appendChild(
          this.makeBtn('取消', 'zg-btn', () => {
            this.mode = { kind: 'chooseTarget', uid: m.uid, back: m.back };
            this.renderBottom();
          }),
        );
        b.appendChild(row);
        return;
      }
      case 'skills': {
        const u = this.battle.getUnit(m.uid);
        if (!u) {
          this.setIdle();
          return;
        }
        const row = el('div', 'zg-skillrow');
        for (const sid of u.skills) {
          const sk = getSkill(sid);
          if (!sk) continue;
          const cost = Math.round(sk.mp * u.mpCostMult);
          const noMp = u.mp < cost;
          const btn = this.makeBtn(`${sk.name} MP${cost}`, `zg-btn zg-skill${noMp ? ' nomp' : ''}`, () => this.pickSkill(sk), noMp);
          btn.title = sk.desc;
          row.appendChild(btn);
        }
        b.appendChild(row);
        b.appendChild(this.makeBtn('返回', 'zg-btn', () => this.backTo(m)));
        return;
      }
      case 'aim': {
        b.appendChild(el('div', 'zg-hint', `选择「${m.skill.name}」的目标格（紫色范围内）`));
        b.appendChild(
          this.makeBtn('取消', 'zg-btn', () => {
            this.mode = { kind: 'skills', uid: m.uid, back: m.back };
            this.renderBottom();
          }),
        );
        return;
      }
      case 'skillPreview': {
        const u = this.battle.getUnit(m.uid);
        if (!u) {
          this.setIdle();
          return;
        }
        const weather = this.battle.state.weather;
        const panel = el('div', 'zg-preview');
        const scope = m.skill.aoe === 'all' ? '全体目标' : `影响 ${m.cells.length} 格`;
        panel.appendChild(el('div', 'zg-preview-title', `「${m.skill.name}」 ${scope} · 命中 ${m.targets.length} 名`));
        // 逐目标预估（伤害/治疗用 core/rules.skillDamage，rng=0.5 取中值）
        const lines = m.targets.slice(0, 2).map(t => {
          let suffix = '';
          if (m.skill.kind === 'damage' || m.skill.kind === 'heal') {
            const terrain = this.terrainAt(t.q, t.r);
            const v = rules.skillDamage(u, m.skill, t, { weather, terrain, rng: () => 0.5 });
            const forestTag = m.skill.id === 'fire_attack' && terrain === 'forest' ? '·林火' : '';
            suffix = m.skill.kind === 'heal' ? ` +${v}` : ` -${v}${forestTag}`;
          }
          return `${t.name}${suffix}`;
        });
        if (lines.length > 0) panel.appendChild(el('div', 'zg-preview-line', lines.join('　')));
        if (m.targets.length === 0) panel.appendChild(el('div', 'zg-preview-warn', '⚠ 范围内无有效目标'));
        b.appendChild(panel);
        const row = el('div', 'zg-btnrow');
        row.appendChild(this.makeBtn('确认施放', 'zg-btn zg-btn-primary', () => this.confirmSkill(), m.targets.length === 0));
        row.appendChild(
          this.makeBtn('取消', 'zg-btn', () => {
            if (m.skill.aoe === 'all') this.mode = { kind: 'skills', uid: m.uid, back: m.back };
            else this.mode = { kind: 'aim', uid: m.uid, back: m.back, skill: m.skill, cells: this.skillRangeCells(u, m.skill) };
            this.renderBottom();
          }),
        );
        b.appendChild(row);
        return;
      }
      case 'items': {
        const counts = this.battle.getItemCounts();
        const row = el('div', 'zg-skillrow');
        for (const item of ITEMS) {
          const n = counts[item.id] ?? 0;
          const btn = this.makeBtn('', `zg-btn zg-item${n === 0 ? ' nomp' : ''}`, () => this.pickItem(item.id), n === 0);
          btn.title = item.desc;
          btn.appendChild(el('span', `zg-item-ico ${item.kind}`, item.name.slice(0, 1)));
          btn.appendChild(el('span', 'zg-item-name', `${item.name} ×${n}`));
          btn.appendChild(el('span', 'zg-item-desc', item.desc));
          row.appendChild(btn);
        }
        row.appendChild(this.makeBtn('返回', 'zg-btn zg-item-back', () => this.backTo(m)));
        b.appendChild(row);
        return;
      }
      case 'itemConfirm': {
        const u = this.battle.getUnit(m.uid);
        const item = getItem(m.itemId);
        if (!u || !item) {
          this.setIdle();
          return;
        }
        const counts = this.battle.getItemCounts();
        const panel = el('div', 'zg-preview');
        panel.appendChild(el('div', 'zg-preview-title', `对 ${u.name} 使用「${item.name}」？`));
        panel.appendChild(el('div', 'zg-preview-line', `${item.desc}（剩余 ${counts[item.id] ?? 0} 个）`));
        b.appendChild(panel);
        const row = el('div', 'zg-btnrow');
        row.appendChild(this.makeBtn('确认使用', 'zg-btn zg-btn-primary', () => this.confirmItem()));
        row.appendChild(
          this.makeBtn('返回', 'zg-btn', () => {
            this.mode = { kind: 'items', uid: m.uid, back: m.back };
            this.renderBottom();
          }),
        );
        b.appendChild(row);
        return;
      }
      case 'busy': {
        b.appendChild(el('div', 'zg-hint', busyText));
        return;
      }
    }
  }

  // ============================================================
  // 顶部栏 / 横幅 / 提示 / 菜单 / 新手引导
  // ============================================================
  private updateTopbar(): void {
    const s = this.battle.state;
    this.turnEl.textContent = `第 ${s.turn} 回合 · ${APP_VERSION}`;
    const phaseText =
      s.phase === 'player' ? '我方行动' : s.phase === 'enemy' ? '敌方行动' : s.phase === 'ally' ? '友军行动' : '战斗结束';
    this.phaseEl.textContent = phaseText;
    this.phaseEl.className = `zg-phase ${s.phase === 'enemy' ? 'enemy' : 'player'}`;
    this.weatherEl.textContent = WEATHER_ICONS[s.weather];
    // 敌方回合显示实时态势统计（攻/守/中）
    if (s.phase === 'enemy') {
      this.stanceEl.textContent = this.stanceText();
      this.stanceEl.classList.remove('hide');
    } else {
      this.stanceEl.classList.add('hide');
    }
    // 顶栏结束回合仅我方回合可用
    this.endTurnBtn.disabled = s.phase !== 'player';
  }

  /** 胜利条件文案（开局横幅副标题 / ☰ 菜单面板用） */
  private victoryText(): string {
    const lv = this.opts.level;
    switch (lv.victory) {
      case 'defeatBoss': {
        const boss = lv.enemies.find(e => e.id === lv.bossId);
        return `击破敌将·${boss?.name ?? '敌将'}`;
      }
      case 'defendTurns':
        return `坚守 ${lv.defendTurns ?? '?'} 回合`;
      default:
        return '全歼敌军';
    }
  }

  /** 失败条件文案（level.maxTurns 有值时追加超限失败） */
  private defeatText(): string {
    const lv = this.opts.level;
    return lv.maxTurns ? `李世民阵亡 / 我方全灭 / 超过 ${lv.maxTurns} 回合` : '李世民阵亡 / 我方全灭';
  }

  private showBanner(text: string, kind: 'turn' | 'result', holdMs: number, sub?: string): void {
    if (this.destroyed) return;
    this.bannerText.textContent = text;
    this.bannerSub.textContent = sub ?? '';
    this.banner.className = `zg-banner show ${kind}`;
    window.clearTimeout(this.bannerTimer);
    this.bannerTimer = window.setTimeout(() => {
      this.banner.classList.remove('show');
    }, holdMs);
  }

  private toast(text: string): void {
    if (this.destroyed) return;
    this.toastEl.textContent = text;
    this.toastEl.classList.add('show');
    window.clearTimeout(this.toastTimer);
    this.toastTimer = window.setTimeout(() => {
      this.toastEl.classList.remove('show');
    }, 1600);
  }

  private openMenu(): void {
    if (this.animator.playing || this.finished || this.destroyed) return;
    this.renderMenuMain();
    this.menuMask.classList.add('show');
  }

  private closeMenu(): void {
    this.menuMask.classList.remove('show');
  }

  private renderMenuMain(): void {
    this.menuPanel.textContent = '';
    this.menuPanel.appendChild(el('div', 'zg-menu-title', '菜单'));
    // 胜/败条件两行（与开局横幅副标题同口径）
    this.menuPanel.appendChild(el('div', 'zg-menu-cond', `胜利条件：${this.victoryText()}`));
    this.menuPanel.appendChild(el('div', 'zg-menu-cond lose', `失败条件：${this.defeatText()}`));
    this.menuPanel.appendChild(this.makeBtn('继续战斗', 'zg-btn zg-btn-primary zg-menu-item', () => this.closeMenu()));
    this.menuPanel.appendChild(
      this.makeBtn('撤退', 'zg-btn zg-menu-item', () => {
        this.menuPanel.textContent = '';
        this.menuPanel.appendChild(el('div', 'zg-menu-title', '确定撤退？'));
        this.menuPanel.appendChild(el('div', 'zg-hint', '撤退将按战败结算'));
        this.menuPanel.appendChild(
          this.makeBtn('确认撤退', 'zg-btn zg-btn-danger zg-menu-item', () => {
            this.closeMenu();
            this.finish('lose'); // 撤退按 lose 上报
          }),
        );
        this.menuPanel.appendChild(this.makeBtn('再想想', 'zg-btn zg-menu-item', () => this.renderMenuMain()));
      }),
    );
  }

  private tutorialAdvance(k: TutKey): void {
    if (!this.opts.tutorial) return;
    const idx = TUT_ORDER.indexOf(k);
    if (idx >= this.tutorialStep) {
      this.tutorialStep = idx + 1;
      this.renderTutorial();
    }
  }

  private renderTutorial(): void {
    if (!this.opts.tutorial || this.tutorialStep >= TUT_TEXT.length) {
      this.tutoEl.classList.add('hide');
      return;
    }
    this.tutoEl.classList.remove('hide');
    this.tutoEl.textContent = TUT_TEXT[this.tutorialStep];
  }
}
