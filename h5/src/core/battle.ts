// ============================================================
// Battle —— 战斗引擎核心（H5_DESIGN.md §5.1 契约）。
// 同步结算一整个行动/回合，把「发生了什么」作为 BattleEvent
// 事件流交给 UI 播放；UI 不直接改状态。
// 公式全部在 rules.ts，数值出自 docs/02-combat.md。
// ============================================================
import type {
  ActionResult,
  BattleEvent,
  BattleOutcome,
  BattleState,
  Buff,
  CharacterDef,
  Difficulty,
  EnemyDef,
  LevelDef,
  PartyMember,
  Selection,
  SkillDef,
  Stats,
  Unit,
} from './types';
import {
  BONDS,
  DIFFICULTY_MOD,
  EXP_RULES,
  TERRAIN_RULES,
  WEATHER_RULES,
  getCharacter,
  getEquipment,
  getItem,
  getSkill,
} from '../data';
import { Cell, hexDistance, hexRange, key, neighbors, parseKey } from './hex';
import { GridLike, findPath, inBounds, moveCost, reachableCells } from './pathfinding';
import * as rules from './rules';
import { runUnitAI } from './ai';

/** mulberry32 种子随机数——确定性，仿真可复现 */
export function mulberry32(seed: number): rules.Rng {
  let a = seed >>> 0;
  return () => {
    a |= 0;
    a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

export interface BattleResult {
  outcome: BattleOutcome;
  reason?: string;
}

/** 按等级迭代成长（口径与 rules.applyLevelUp 一致：HP/MP 按升级后属性取整） */
function computeGrowth(def: CharacterDef, level: number): { stats: Stats; maxHp: number; maxMp: number } {
  const stats: Stats = { ...def.base };
  let maxHp = def.hp;
  let maxMp = def.mp;
  for (let l = 1; l < level; l++) {
    stats.str += def.growth.str;
    stats.cmd += def.growth.cmd;
    stats.int += def.growth.int;
    stats.agi += def.growth.agi;
    stats.luk += def.growth.luk;
    maxHp += Math.floor(stats.cmd / EXP_RULES.hpGrowthCmdDiv) + EXP_RULES.hpGrowthBase;
    maxMp += Math.floor(stats.int / EXP_RULES.mpGrowthIntDiv) + EXP_RULES.mpGrowthBase;
  }
  return { stats, maxHp, maxMp };
}

export class Battle {
  readonly state: BattleState;
  private readonly rng: rules.Rng;
  private readonly grid: GridLike;
  private readonly killCounts: Record<string, number> = {};
  private readonly bossKillers: string[] = [];
  private readonly actedCharIds = new Set<string>(); // 行动过的玩家 charId（exp 100% 档）
  private readonly partyCharIds: string[] = []; // 全部出战玩家 charId
  private readonly activeBondIds: string[] = [];
  private readonly prevPos = new Map<string, Cell>(); // moveUnit 移动前位置（cancelMove 撤回用）
  private readonly items: Record<string, number>; // 本场出战携带的物品库存（构造函数内部拷贝）
  private uidSeq = 0;

  constructor(level: LevelDef, party: PartyMember[], difficulty: Difficulty, seed: number = 1, items?: Record<string, number>) {
    this.rng = mulberry32(seed);
    this.items = { ...(items ?? {}) };
    this.grid = {
      width: level.width,
      height: level.height,
      weather: level.weather,
      terrainAt: (q, r) => level.terrain[key(q, r)] ?? 'plain',
    };
    this.state = {
      level,
      units: [],
      turn: 1,
      phase: 'player',
      weather: level.weather,
      difficulty,
      outcome: null,
    };
    this.createUnits(level, party, difficulty);
  }

  // ---------- 查询 ----------

  getUnit(uid: string): Unit | undefined {
    return this.state.units.find(u => u.uid === uid);
  }

  isOver(): BattleResult | null {
    return this.state.outcome ? { outcome: this.state.outcome, reason: this.state.loseReason } : null;
  }

  /** 每单位击杀数（exp 结算用） */
  getKillCounts(): Record<string, number> {
    return { ...this.killCounts };
  }

  /** 击杀 Boss 的玩家 charId（exp Boss 加成用） */
  getBossKillers(): string[] {
    return [...this.bossKillers];
  }

  /** 行动过的玩家 charId（exp 100% 档） */
  getParticipated(): string[] {
    return [...this.actedCharIds];
  }

  /** 全部出战玩家 charId（exp 50% 档=出战未行动） */
  getPartyCharIds(): string[] {
    return [...this.partyCharIds];
  }

  /** AI 用：unit 的可达格及代价（含起点） */
  reachableFor(unit: Unit): Map<string, number> {
    const { blocked, noStop } = this.occupiedExcept(unit);
    return reachableCells(this.grid, unit, blocked, noStop, rules.effectiveMove(unit));
  }

  /** AI 用：A* 寻路到 dest（含起终点；不可达返回 null） */
  pathFor(unit: Unit, dest: Cell): Cell[] | null {
    const { blocked, noStop } = this.occupiedExcept(unit);
    return findPath(this.grid, { q: unit.q, r: unit.r }, dest, unit, blocked, noStop);
  }

  /** AI 用：(q,r) 是否可进入（界内、地形可进、未被占据） */
  canEnter(unit: Unit, q: number, r: number): boolean {
    return inBounds(this.grid, q, r) && !this.unitAt(q, r) && isFinite(moveCost(this.grid, unit, q, r));
  }

  // ---------- 玩家操作（§5.1 契约） ----------

  selectUnit(uid: string): Selection {
    const u = this.mustGet(uid);
    const reachable: Cell[] = [];
    for (const k of this.reachableFor(u).keys()) {
      const c = parseKey(k);
      if (c.q === u.q && c.r === u.r) continue; // 起点不算可移动格
      reachable.push(c);
    }
    // 可攻击格 = 当前位置射程内存活敌人的所在格
    const attackable: Cell[] = [];
    for (const c of hexRange(u.q, u.r, u.range)) {
      if (!inBounds(this.grid, c.q, c.r)) continue;
      const t = this.unitAt(c.q, c.r);
      if (t && this.isHostile(u, t)) attackable.push(c);
    }
    return { uid, reachable, attackable };
  }

  moveAndAttack(uid: string, path: Cell[], targetUid?: string): ActionResult {
    const fail = (reason: string): ActionResult => ({ ok: false, reason, events: [] });
    if (this.state.outcome) return fail('战斗已结束');
    if (this.state.phase !== 'player') return fail('非玩家回合');
    const u = this.mustGet(uid);
    if (!u.alive || u.faction !== 'player') return fail('单位不可操作');
    if (u.acted) return fail('本回合已行动');

    // 原地不动且无目标 = 待机
    if (path.length === 0 && !targetUid) return this.wait(uid);

    // 校验路径（UI 提供的路径须合法：相邻/可达/不越界/不越移动力）
    let steps = path;
    if (steps.length > 0 && steps[0].q === u.q && steps[0].r === u.r) steps = steps.slice(1);
    let dest: Cell = { q: u.q, r: u.r };
    if (steps.length > 0) {
      const { blocked, noStop } = this.occupiedExcept(u);
      let cost = 0;
      let prev: Cell = { q: u.q, r: u.r };
      for (let i = 0; i < steps.length; i++) {
        const s = steps[i];
        const k = key(s.q, s.r);
        if (hexDistance(prev.q, prev.r, s.q, s.r) !== 1) return fail('路径不连续');
        if (!inBounds(this.grid, s.q, s.r)) return fail('路径越界');
        if (blocked.has(k)) return fail('路径被占据');
        if (noStop.has(k) && i === steps.length - 1) return fail('目标格有友方单位');
        const step = moveCost(this.grid, u, s.q, s.r);
        if (!isFinite(step)) return fail('路径不可通行');
        cost += step;
        prev = s;
      }
      if (cost > rules.effectiveMove(u)) return fail('超出移动力');
      dest = steps[steps.length - 1];
    }

    // 校验攻击目标（在目的地开打）
    let target: Unit | undefined;
    if (targetUid) {
      target = this.mustGet(targetUid);
      if (!target.alive || !this.isHostile(u, target)) return fail('目标不可攻击');
      if (hexDistance(dest.q, dest.r, target.q, target.r) > u.range) return fail('目标超出射程');
    }

    // 执行
    const events: BattleEvent[] = [];
    if (steps.length > 0) {
      u.q = dest.q;
      u.r = dest.r;
      events.push({ type: 'move', uid: u.uid, path: steps.map(s => ({ q: s.q, r: s.r })) });
    }
    if (target) this.performAttack(u, target, events);
    u.acted = true;
    this.actedCharIds.add(u.charId);
    this.checkEnd(events);
    return { ok: true, events };
  }

  /** 战后回写存档用：本场剩余物品库存 */
  getItemCounts(): Record<string, number> {
    return { ...this.items };
  }

  /**
   * 经典行动流（曹操传式）：仅移动，不设 acted——移动后还可
   * 攻击/计策/物品/待机，行动前可 cancelMove 撤回。
   * moved=true 后本回合不可再移动。
   */
  moveUnit(uid: string, path: Cell[]): ActionResult {
    const fail = (reason: string): ActionResult => ({ ok: false, reason, events: [] });
    if (this.state.outcome) return fail('战斗已结束');
    if (this.state.phase !== 'player') return fail('非玩家回合');
    const u = this.mustGet(uid);
    if (!u.alive || u.faction !== 'player') return fail('单位不可操作');
    if (u.acted) return fail('本回合已行动');
    if (u.moved) return fail('本回合已移动');

    // 校验路径（与 moveAndAttack 同一套规则：相邻/可达/不越界/不越移动力）
    let steps = path;
    if (steps.length > 0 && steps[0].q === u.q && steps[0].r === u.r) steps = steps.slice(1);
    if (steps.length === 0) return fail('路径为空');
    const { blocked, noStop } = this.occupiedExcept(u);
    let cost = 0;
    let prev: Cell = { q: u.q, r: u.r };
    for (let i = 0; i < steps.length; i++) {
      const s = steps[i];
      const k = key(s.q, s.r);
      if (hexDistance(prev.q, prev.r, s.q, s.r) !== 1) return fail('路径不连续');
      if (!inBounds(this.grid, s.q, s.r)) return fail('路径越界');
      if (blocked.has(k)) return fail('路径被占据');
      if (noStop.has(k) && i === steps.length - 1) return fail('目标格有友方单位');
      const step = moveCost(this.grid, u, s.q, s.r);
      if (!isFinite(step)) return fail('路径不可通行');
      cost += step;
      prev = s;
    }
    if (cost > rules.effectiveMove(u)) return fail('超出移动力');

    // 执行：记录移动前位置（cancelMove 撤回用），不设 acted
    this.prevPos.set(u.uid, { q: u.q, r: u.r });
    const dest = steps[steps.length - 1];
    u.q = dest.q;
    u.r = dest.r;
    u.moved = true;
    return { ok: true, events: [{ type: 'move', uid: u.uid, path: steps.map(s => ({ q: s.q, r: s.r })) }] };
  }

  /** 经典行动流：撤回移动（仅当 moved && !acted），单位瞬移回移动前位置、moved 复位 */
  cancelMove(uid: string): ActionResult {
    const fail = (reason: string): ActionResult => ({ ok: false, reason, events: [] });
    if (this.state.outcome) return fail('战斗已结束');
    if (this.state.phase !== 'player') return fail('非玩家回合');
    const u = this.mustGet(uid);
    if (!u.alive || u.faction !== 'player') return fail('单位不可操作');
    if (!u.moved || u.acted) return fail('当前不可撤回');
    const prev = this.prevPos.get(u.uid);
    if (!prev) return fail('无移动记录');
    u.q = prev.q;
    u.r = prev.r;
    u.moved = false;
    this.prevPos.delete(u.uid);
    // 事件路径只带终点格（瞬移回原位，UI 播放回撤）
    return { ok: true, events: [{ type: 'move', uid: u.uid, path: [{ q: prev.q, r: prev.r }] }] };
  }

  /** 经典行动流：原地普攻（含反击结算，同 moveAndAttack 的攻击部分），执行后 acted=true */
  attackWith(uid: string, targetUid: string): ActionResult {
    const fail = (reason: string): ActionResult => ({ ok: false, reason, events: [] });
    if (this.state.outcome) return fail('战斗已结束');
    if (this.state.phase !== 'player') return fail('非玩家回合');
    const u = this.mustGet(uid);
    if (!u.alive || u.faction !== 'player') return fail('单位不可操作');
    if (u.acted) return fail('本回合已行动');
    const target = this.mustGet(targetUid);
    if (!target.alive || !this.isHostile(u, target)) return fail('目标不可攻击');
    if (hexDistance(u.q, u.r, target.q, target.r) > u.range) return fail('目标超出射程');
    const events: BattleEvent[] = [];
    this.performAttack(u, target, events);
    u.acted = true;
    this.actedCharIds.add(u.charId);
    this.checkEnd(events);
    return { ok: true, events };
  }

  /**
   * 经典行动流：使用战斗物品（data/items.ts），执行后 acted=true、库存-1。
   * 金疮药 heal 事件回血 30%maxHp / 清心丸 MP+20 封顶（无专用事件）/
   * 士气丹 attackPct+10 buff 3 回合（buff 事件）。
   */
  useItem(uid: string, itemId: string): ActionResult {
    const fail = (reason: string): ActionResult => ({ ok: false, reason, events: [] });
    if (this.state.outcome) return fail('战斗已结束');
    if (this.state.phase !== 'player') return fail('非玩家回合');
    const u = this.mustGet(uid);
    if (!u.alive || u.faction !== 'player') return fail('单位不可操作');
    if (u.acted) return fail('本回合已行动');
    const item = getItem(itemId);
    if (!item) return fail('未知物品');
    if ((this.items[itemId] ?? 0) <= 0) return fail('物品不足');

    const events: BattleEvent[] = [];
    switch (item.kind) {
      case 'heal_hp': {
        const amount = Math.floor((u.maxHp * item.value) / 100);
        u.hp = Math.min(u.maxHp, u.hp + amount);
        events.push({ type: 'heal', uid: u.uid, amount, hpAfter: u.hp });
        break;
      }
      case 'heal_mp':
        // MP 恢复无专用事件，只改状态（UI 刷新面板即可）
        u.mp = Math.min(u.maxMp, u.mp + item.value);
        break;
      case 'buff_atk':
        this.addBuff(u, { id: `item_${item.id}`, name: item.name, attackPct: item.value, remainingTurns: 3 }, events);
        break;
    }
    this.items[itemId] -= 1;
    u.acted = true;
    this.actedCharIds.add(u.charId);
    return { ok: true, events };
  }

  useSkill(uid: string, skillId: string, targetCell?: Cell): ActionResult {
    const fail = (reason: string): ActionResult => ({ ok: false, reason, events: [] });
    if (this.state.outcome) return fail('战斗已结束');
    if (this.state.phase !== 'player') return fail('非玩家回合');
    const u = this.mustGet(uid);
    if (!u.alive || u.faction !== 'player') return fail('单位不可操作');
    if (u.acted) return fail('本回合已行动');
    const skill = getSkill(skillId);
    if (!skill || !u.skills.includes(skillId)) return fail('不会该计策');
    const cost = Math.round(skill.mp * u.mpCostMult);
    if (u.mp < cost) return fail('MP不足');

    const events: BattleEvent[] = [];
    const res = this.execSkill(u, skill, targetCell, events);
    if (!res.ok) return fail(res.reason ?? '施放失败');
    u.mp -= cost;
    u.acted = true;
    this.actedCharIds.add(u.charId);
    this.checkEnd(events);
    return { ok: true, events };
  }

  wait(uid: string): ActionResult {
    const fail = (reason: string): ActionResult => ({ ok: false, reason, events: [] });
    if (this.state.outcome) return fail('战斗已结束');
    if (this.state.phase !== 'player') return fail('非玩家回合');
    const u = this.mustGet(uid);
    if (!u.alive || u.faction !== 'player') return fail('单位不可操作');
    if (u.acted) return fail('本回合已行动');
    u.acted = true;
    // 待机不算「行动」——战后经验按「参战未行动 ×0.5」计（05-systems §3.2）
    return { ok: true, events: [{ type: 'wait', uid: u.uid }] };
  }

  /**
   * 结束玩家回合：敌方回合（AI）→ 回合结束 buff 结算（点燃 DOT、
   * 回合数递减）→ 新回合开始（MP+5、acted 复位、混乱消耗）。
   * 返回整段事件流。
   */
  endTurn(): ActionResult {
    if (this.state.outcome) return { ok: false, reason: '战斗已结束', events: [] };
    if (this.state.phase !== 'player') return { ok: false, reason: '非玩家回合', events: [] };
    const events: BattleEvent[] = [];

    // 1. 敌方回合：AI 按 AGI 降序逐个行动
    this.state.phase = 'enemy';
    events.push({ type: 'turnBegin', turn: this.state.turn, phase: 'enemy' });
    const enemies = this.state.units
      .filter(u => u.alive && u.faction === 'enemy')
      .sort((a, b) => b.stats.agi - a.stats.agi);
    for (const e of enemies) {
      if (this.state.outcome) break;
      if (!e.alive) continue;
      if (this.consumeConfuse(e)) {
        events.push({ type: 'wait', uid: e.uid });
        continue;
      }
      runUnitAI(this, e, events);
    }

    // 2. 回合结束结算：点燃 DOT + buff 回合数递减
    if (!this.state.outcome) this.resolveRoundEnd(events);

    // 3. 新回合：回合数推进，判定坚守/超限
    if (!this.state.outcome) {
      this.state.turn += 1;
      const lv = this.state.level;
      if (lv.victory === 'defendTurns' && lv.defendTurns && this.state.turn > lv.defendTurns) {
        this.endBattle('win', `坚守${lv.defendTurns}回合成功`, events);
      } else if (lv.maxTurns && this.state.turn > lv.maxTurns) {
        this.endBattle('lose', `超过${lv.maxTurns}回合`, events);
      }
    }
    if (!this.state.outcome) {
      this.prevPos.clear(); // 移动撤回记录仅本回合有效
      for (const u of this.state.units) {
        if (!u.alive) continue;
        u.acted = false;
        u.moved = false; // 与 acted 同步复位（经典行动流）
        u.mp = Math.min(u.maxMp, u.mp + 5); // 回合开始 MP+5（02-combat §6.4）
      }
      // 兄妹羁绊：回合开始互相恢复 10MP
      if (this.activeBondIds.includes('bond_siblings')) {
        for (const u of this.state.units) {
          if (u.alive && u.faction === 'player' && (u.charId === 'zhangsun_wuji' || u.charId === 'zhangsun_empress')) {
            u.mp = Math.min(u.maxMp, u.mp + 10);
          }
        }
      }
      // 被混乱的玩家单位：本回合无法行动（混乱在行动方回合开始时消耗）
      for (const u of this.state.units) {
        if (u.alive && u.faction === 'player' && this.consumeConfuse(u)) {
          u.acted = true;
          events.push({ type: 'wait', uid: u.uid });
        }
      }
      this.state.phase = 'player';
      events.push({ type: 'turnBegin', turn: this.state.turn, phase: 'player' });
    }
    return { ok: true, events };
  }

  // ---------- AI 行动原语（ai.ts 调用；不做玩家回合校验） ----------

  /** AI 行动：移动到 dest（null=不动）+ 可选攻击 */
  aiMoveAttack(unit: Unit, dest: Cell | null, targetUid: string | null, events: BattleEvent[]): void {
    if (this.state.outcome || !unit.alive) return;
    if (dest && (dest.q !== unit.q || dest.r !== unit.r)) {
      const { blocked, noStop } = this.occupiedExcept(unit);
      const path = findPath(this.grid, { q: unit.q, r: unit.r }, dest, unit, blocked, noStop);
      if (path && path.length > 1) {
        unit.q = dest.q;
        unit.r = dest.r;
        events.push({ type: 'move', uid: unit.uid, path: path.slice(1) });
      }
    }
    if (targetUid) {
      const t = this.getUnit(targetUid);
      if (t && t.alive && hexDistance(unit.q, unit.r, t.q, t.r) <= unit.range) {
        this.performAttack(unit, t, events);
      }
    }
    unit.acted = true;
    if (unit.faction === 'player') this.actedCharIds.add(unit.charId);
    this.checkEnd(events);
  }

  /** AI 行动：施放计策（MP 不足或目标非法时退化为待机） */
  aiSkill(unit: Unit, skillId: string, targetCell: Cell | null, events: BattleEvent[]): void {
    if (this.state.outcome || !unit.alive) return;
    const skill = getSkill(skillId);
    const cost = skill ? Math.round(skill.mp * unit.mpCostMult) : Infinity;
    if (!skill || unit.mp < cost) {
      this.aiWait(unit, events);
      return;
    }
    const res = this.execSkill(unit, skill, targetCell ?? undefined, events);
    if (!res.ok) {
      this.aiWait(unit, events);
      return;
    }
    unit.mp -= cost;
    unit.acted = true;
    if (unit.faction === 'player') this.actedCharIds.add(unit.charId);
    this.checkEnd(events);
  }

  /** AI 行动：待机 */
  aiWait(unit: Unit, events: BattleEvent[]): void {
    unit.acted = true;
    events.push({ type: 'wait', uid: unit.uid });
  }

  // ---------- 内部：单位创建 ----------

  private createUnits(level: LevelDef, party: PartyMember[], difficulty: Difficulty): void {
    const mod = DIFFICULTY_MOD[difficulty];
    const cells = this.findDeployCells(party.length);
    party.forEach((m, i) => {
      const def = getCharacter(m.charId);
      if (!def) throw new Error(`未知角色: ${m.charId}`);
      const cell = cells[i];
      if (!cell) throw new Error('部署格不足');
      this.state.units.push(this.makePlayerUnit(def, m, cell, mod.playerAttack));
      this.partyCharIds.push(m.charId);
    });
    for (const e of level.enemies) this.state.units.push(this.makeEnemyUnit(e, mod));
    this.applyBonds();
  }

  /** 玩家单位：最终属性 = (base + growth×(level-1)) + 装备 + 难度玩家攻击乘子；羁绊在 applyBonds 另算 */
  private makePlayerUnit(def: CharacterDef, member: PartyMember, cell: Cell, playerAttackMult: number): Unit {
    const { stats, maxHp, maxMp } = computeGrowth(def, member.level);
    let attackPct = 0;
    let defensePct = 0;
    let critBonus = 0;
    let hp = maxHp;
    let mp = maxMp;
    let move = def.move;
    let range = def.range;
    for (const slot of ['weapon', 'armor', 'trinket'] as const) {
      const eqId = member.equipment[slot];
      if (!eqId) continue;
      const eq = getEquipment(eqId);
      if (!eq) continue;
      // 兵种/性别/专属限制不符则忽略（防御性处理，正常由 UI 层拦截）
      if (eq.classes && !eq.classes.includes(def.classType)) continue;
      if (eq.gender && eq.gender !== def.gender) continue;
      if (eq.charId && eq.charId !== def.id) continue;
      if (eq.statBonus) {
        for (const k of ['str', 'cmd', 'int', 'agi', 'luk'] as const) {
          stats[k] += eq.statBonus[k] ?? 0;
        }
      }
      hp += eq.hpBonus ?? 0;
      mp += eq.mpBonus ?? 0;
      move += eq.moveBonus ?? 0;
      range += eq.rangeBonus ?? 0;
      attackPct += eq.attackPct ?? 0;
      defensePct += eq.defensePct ?? 0;
      critBonus += eq.critBonus ?? 0;
    }
    // 难度：玩家攻击乘子（02-combat §4.6）乘在 STR 上
    stats.str = Math.round(stats.str * playerAttackMult);
    return {
      uid: `u${this.uidSeq++}`,
      charId: def.id,
      name: def.name,
      faction: 'player',
      classType: def.classType,
      level: member.level,
      stats,
      hp,
      maxHp: hp,
      mp,
      maxMp: mp,
      move: Math.max(1, move),
      range: Math.max(1, range),
      q: cell.q,
      r: cell.r,
      skills: [...def.skills],
      passive: def.passive,
      buffs: [],
      acted: false,
      moved: false,
      alive: true,
      isHero: def.id === 'lishimin',
      kills: 0,
      growth: { ...def.growth },
      attackPct,
      defensePct,
      critBonus,
      mpCostMult: 1,
    };
  }

  /** 敌方单位：属性照搬 EnemyDef + 关卡递增强化，HP/STR 乘难度系数（02-combat §4.6） */
  private makeEnemyUnit(def: EnemyDef, mod: { enemyAttack: number; enemyHp: number }): Unit {
    const stats: Stats = { ...def.base };
    // R2 平衡调整（docs/EVALUATION_R2.md）：敌军按关卡递增——
    // 防御半减后双方输出都变高，敌军需同步抬 HP/CMD 保证不被一击秒杀、
    // 抬 STR/AGI 保证能对我方造成有效伤害（第 1 关为教学基准不加成）
    const lvBonus = this.state.level.id - 1;
    stats.str += lvBonus * 4;
    stats.cmd += lvBonus * 3;
    stats.agi += lvBonus * 2;
    stats.str = Math.round(stats.str * mod.enemyAttack);
    const maxHp = Math.round((def.hp + lvBonus * 10) * mod.enemyHp);
    return {
      uid: `u${this.uidSeq++}`,
      charId: def.id,
      name: def.name,
      faction: 'enemy',
      classType: def.classType,
      level: def.level,
      stats,
      hp: maxHp,
      maxHp,
      mp: def.mp,
      maxMp: def.mp,
      move: def.move,
      range: def.range,
      q: def.q,
      r: def.r,
      skills: [...(def.skills ?? [])],
      passive: '',
      buffs: [],
      acted: false,
      moved: false,
      alive: true,
      isBoss: def.isBoss,
      kills: 0,
      growth: { str: 0, cmd: 0, int: 0, agi: 0, luk: 0 },
      attackPct: 0,
      defensePct: 0,
      critBonus: 0,
      mpCostMult: 1,
    };
  }

  /**
   * 羁绊（data/bonds.ts，仅玩家方同阵检测）：
   * 已实现——帝后同心/夫妻同阵（全属性×1.1/×1.15，五维取整）、
   * 瓦岗三杰（攻击×1.1+暴击+5；3人齐时不与2人版叠加）、
   * 房谋杜断（智力+10、MP消耗×0.8）、兄妹（MP上限+15、回合开始互回10MP）。
   * 未实现（注释说明）：皇后医疗范围+1、夫妻替死、瓦岗·齐「同袍」保留1HP。
   */
  private applyBonds(): void {
    const players = this.state.units.filter(u => u.faction === 'player');
    const multStats = (u: Unit, mult: number) => {
      u.stats.str = Math.round(u.stats.str * mult);
      u.stats.cmd = Math.round(u.stats.cmd * mult);
      u.stats.int = Math.round(u.stats.int * mult);
      u.stats.agi = Math.round(u.stats.agi * mult);
      u.stats.luk = Math.round(u.stats.luk * mult);
    };
    // 瓦岗 3 人齐时跳过 2 人版，避免叠加
    const wagangFullDef = BONDS.find(b => b.id === 'bond_wagang_full')!;
    const wagangFullActive =
      players.filter(u => wagangFullDef.members.includes(u.charId)).length >= wagangFullDef.minCount;
    for (const bond of BONDS) {
      const members = players.filter(u => bond.members.includes(u.charId));
      if (members.length < bond.minCount) continue;
      switch (bond.id) {
        case 'bond_emperor':
          members.forEach(u => multStats(u, 1.1));
          break;
        case 'bond_couple':
          members.forEach(u => multStats(u, 1.15));
          break;
        case 'bond_wagang':
          if (wagangFullActive) break;
          members.forEach(u => {
            u.stats.str = Math.round(u.stats.str * 1.1);
            u.critBonus += 5;
          });
          break;
        case 'bond_wagang_full':
          members.forEach(u => {
            u.stats.str = Math.round(u.stats.str * 1.1);
            u.critBonus += 5;
          });
          break;
        case 'bond_chancellor':
          members.forEach(u => {
            u.stats.int += 10;
            u.mpCostMult = 0.8;
          });
          break;
        case 'bond_siblings':
          members.forEach(u => {
            u.maxMp += 15;
            u.mp = Math.min(u.maxMp, u.mp + 15);
          });
          break;
      }
      this.activeBondIds.push(bond.id);
    }
  }

  /** 部署：己方从 (2, h/2) 附近 BFS 找空地排开（地图左侧） */
  private findDeployCells(count: number): Cell[] {
    const start: Cell = { q: 2, r: Math.floor(this.grid.height / 2) };
    const out: Cell[] = [];
    const seen = new Set<string>([key(start.q, start.r)]);
    const queue: Cell[] = [start];
    while (queue.length > 0 && out.length < count) {
      const c = queue.shift()!;
      if (!TERRAIN_RULES[this.grid.terrainAt(c.q, c.r)].impassable) out.push(c);
      for (const n of neighbors(c.q, c.r)) {
        const k = key(n.q, n.r);
        if (!seen.has(k) && inBounds(this.grid, n.q, n.r)) {
          seen.add(k);
          queue.push(n);
        }
      }
    }
    return out;
  }

  // ---------- 内部：战斗结算 ----------

  private isHostile(a: Unit, b: Unit): boolean {
    // player/ally 同侧（ally 预留，MVP 不生成友军单位）
    return (a.faction === 'enemy') !== (b.faction === 'enemy');
  }

  private mustGet(uid: string): Unit {
    const u = this.getUnit(uid);
    if (!u) throw new Error(`未知单位: ${uid}`);
    return u;
  }

  private unitAt(q: number, r: number): Unit | undefined {
    return this.state.units.find(u => u.alive && u.q === q && u.r === r);
  }

  private occupiedExcept(self: Unit): { blocked: Set<string>; noStop: Set<string> } {
    const blocked = new Set<string>();
    const noStop = new Set<string>();
    for (const u of this.state.units) {
      if (!u.alive || u === self) continue;
      if (u.faction === self.faction) {
        noStop.add(key(u.q, u.r)); // 同阵营：可穿越，不可落脚
      } else {
        blocked.add(key(u.q, u.r)); // 敌对阵营：完全阻挡
      }
    }
    return { blocked, noStop };
  }

  /** 普攻 + 反击（02-combat §4.5：守方存活+射程内+AGI>攻方×0.8 → 反击×0.7不暴击） */
  private performAttack(a: Unit, d: Unit, events: BattleEvent[]): void {
    this.resolveAttack(a, d, events, false);
    if (d.alive && rules.canCounter(a, d, hexDistance(a.q, a.r, d.q, d.r))) {
      this.resolveAttack(d, a, events, true);
    }
  }

  private resolveAttack(a: Unit, d: Unit, events: BattleEvent[], isCounter: boolean): void {
    const terrain = this.grid.terrainAt(d.q, d.r);
    const mod = DIFFICULTY_MOD[this.state.difficulty];
    const extraHit = a.faction === 'enemy' ? mod.enemyHitMod : mod.playerHitMod;
    const hit = rules.hitRate(a, d, terrain, this.state.weather, extraHit);
    if (this.rng() * 100 >= hit) {
      if (isCounter) {
        events.push({ type: 'counter', uid: a.uid, targetUid: d.uid, hit: false, dmg: 0, targetHpAfter: d.hp });
      } else {
        events.push({ type: 'attack', uid: a.uid, targetUid: d.uid, hit: false, crit: false, dmg: 0, targetHpAfter: d.hp });
      }
      return;
    }
    const crit = !isCounter && this.rng() * 100 < rules.critRate(a);
    const dmg = rules.attackDamage(a, d, { terrain, crit, counter: isCounter, rng: this.rng });
    d.hp = Math.max(0, d.hp - dmg);
    if (isCounter) {
      events.push({ type: 'counter', uid: a.uid, targetUid: d.uid, hit: true, dmg, targetHpAfter: d.hp });
    } else {
      events.push({ type: 'attack', uid: a.uid, targetUid: d.uid, hit: true, crit, dmg, targetHpAfter: d.hp });
    }
    if (d.hp <= 0) this.killUnit(a, d, events);
  }

  private killUnit(attacker: Unit | null, defender: Unit, events: BattleEvent[]): void {
    defender.alive = false;
    defender.hp = 0;
    events.push({ type: 'die', uid: defender.uid });
    if (attacker && attacker.faction === 'player') {
      attacker.kills += 1;
      this.killCounts[attacker.charId] = (this.killCounts[attacker.charId] ?? 0) + 1;
      if (defender.isBoss) this.bossKillers.push(attacker.charId);
    }
  }

  /** 计策结算：校验目标 → skill 事件 → 逐目标效果（damage/heal/buff/die） */
  private execSkill(
    u: Unit,
    skill: SkillDef,
    targetCell: Cell | undefined,
    events: BattleEvent[],
  ): { ok: boolean; reason?: string } {
    let cells: Cell[];
    let targets: Unit[];
    if (skill.aoe === 'all') {
      // 洞察：全体存活敌人，无需目标格
      targets = this.state.units.filter(t => t.alive && this.isHostile(u, t));
      cells = targets.map(t => ({ q: t.q, r: t.r }));
    } else {
      if (!targetCell) return { ok: false, reason: '缺少目标格' };
      if (hexDistance(u.q, u.r, targetCell.q, targetCell.r) > skill.range) {
        return { ok: false, reason: '超出施放距离' };
      }
      cells = rules.skillAoeCells(skill, u.q, u.r, targetCell.q, targetCell.r, this.grid.width, this.grid.height);
      const affectFriendly = skill.kind === 'heal' || skill.kind === 'buff';
      targets = this.state.units.filter(
        t =>
          t.alive &&
          cells.some(c => c.q === t.q && c.r === t.r) &&
          (affectFriendly ? !this.isHostile(u, t) : this.isHostile(u, t)),
      );
    }
    if (targets.length === 0) return { ok: false, reason: '无有效目标' };

    events.push({ type: 'skill', uid: u.uid, skillId: skill.id, cells });
    for (const t of targets) this.applySkillEffect(u, skill, t, events);
    return { ok: true };
  }

  private applySkillEffect(u: Unit, skill: SkillDef, t: Unit, events: BattleEvent[]): void {
    const opts = {
      weather: this.state.weather,
      terrain: this.grid.terrainAt(t.q, t.r),
      rng: this.rng,
    };
    switch (skill.id) {
      case 'fire_attack': {
        const dmg = rules.skillDamage(u, skill, t, opts);
        if (dmg > 0) this.dealSkillDamage(u, t, dmg, events);
        // 点燃 3 回合（雨天 firePowerMult=0 → 伤害与点燃皆无）
        if (t.alive && WEATHER_RULES[this.state.weather].firePowerMult > 0) {
          this.addBuff(t, { id: 'ignite', name: '点燃', remainingTurns: skill.duration ?? 3 }, events);
        }
        break;
      }
      case 'water_attack': {
        const dmg = rules.skillDamage(u, skill, t, opts);
        if (dmg > 0) this.dealSkillDamage(u, t, dmg, events);
        // 「移动消耗+2」简化为移动力-2 的 debuff（持续5回合）
        if (t.alive) {
          this.addBuff(t, { id: 'slow', name: '水攻·滞', moveBonus: -2, remainingTurns: skill.duration ?? 5 }, events);
        }
        break;
      }
      case 'rock_slide':
      case 'volley':
      case 'thunder_strike':
      case 'earth_split': {
        const dmg = rules.skillDamage(u, skill, t, opts);
        this.dealSkillDamage(u, t, dmg, events);
        break;
      }
      case 'heal': {
        const amount = rules.skillDamage(u, skill, t, opts);
        t.hp = Math.min(t.maxHp, t.hp + amount);
        events.push({ type: 'heal', uid: t.uid, amount, hpAfter: t.hp });
        // 解除中毒/燃烧（H5 无中毒，仅移除点燃）
        t.buffs = t.buffs.filter(b => b.id !== 'ignite');
        break;
      }
      case 'rally':
        this.addBuff(
          t,
          { id: 'rally', name: '鼓舞', attackPct: 15, hitBonus: 10, remainingTurns: skill.duration ?? 3 },
          events,
        );
        break;
      case 'confuse':
        // 混乱：目标下回合无法行动（文档「行动随机含攻击友军」简化，见报告）
        this.addBuff(t, { id: 'confuse', name: '混乱', remainingTurns: skill.duration ?? 1 }, events);
        break;
      case 'insight':
        // 洞察：简化实现——清除目标全部 buff 并使其回避-20（1回合）；「看意图」未实现
        t.buffs = [];
        this.addBuff(t, { id: 'insight_expose', name: '洞察·破', dodgeBonus: -20, remainingTurns: skill.duration ?? 1 }, events);
        break;
    }
  }

  private dealSkillDamage(attacker: Unit, target: Unit, dmg: number, events: BattleEvent[]): void {
    target.hp = Math.max(0, target.hp - dmg);
    events.push({ type: 'damage', uid: target.uid, amount: dmg, hpAfter: target.hp });
    if (target.hp <= 0) this.killUnit(attacker, target, events);
  }

  /** buff 同 id 刷新（覆盖剩余回合），并发出 buff 事件 */
  private addBuff(t: Unit, buff: Buff, events: BattleEvent[]): void {
    t.buffs = t.buffs.filter(b => b.id !== buff.id);
    t.buffs.push(buff);
    events.push({ type: 'buff', uid: t.uid, buff });
  }

  /** 消耗混乱：有混乱 buff 则移除并返回 true（本回合无法行动） */
  private consumeConfuse(u: Unit): boolean {
    const has = u.buffs.some(b => b.id === 'confuse');
    if (has) u.buffs = u.buffs.filter(b => b.id !== 'confuse');
    return has;
  }

  /** 回合结束：点燃 DOT（致死无击杀者）+ buff 回合数递减 */
  private resolveRoundEnd(events: BattleEvent[]): void {
    for (const u of this.state.units) {
      if (!u.alive) continue;
      if (u.buffs.some(b => b.id === 'ignite')) {
        const dot = rules.igniteTickDamage(u, this.state.weather, this.grid.terrainAt(u.q, u.r));
        u.hp = Math.max(0, u.hp - dot);
        events.push({ type: 'damage', uid: u.uid, amount: dot, hpAfter: u.hp });
        if (u.hp <= 0) this.killUnit(null, u, events);
      }
    }
    this.checkEnd(events);
    if (this.state.outcome) return;
    for (const u of this.state.units) {
      u.buffs = u.buffs.filter(b => {
        if (b.id === 'confuse') return true; // 混乱在行动方回合消耗，不在此递减
        b.remainingTurns -= 1;
        return b.remainingTurns > 0;
      });
    }
  }

  /** 胜负判定：主角阵亡/我方全灭/敌全灭/主将阵亡（坚守与超限在新回合判） */
  private checkEnd(events: BattleEvent[]): void {
    if (this.state.outcome) return;
    const hero = this.state.units.find(u => u.isHero);
    if (hero && !hero.alive) return this.endBattle('lose', '李世民阵亡', events);
    if (!this.state.units.some(u => u.alive && u.faction === 'player')) {
      return this.endBattle('lose', '我方全灭', events);
    }
    const lv = this.state.level;
    if (lv.victory === 'defeatAll' && !this.state.units.some(u => u.alive && u.faction === 'enemy')) {
      return this.endBattle('win', '全灭敌军', events);
    }
    if (lv.victory === 'defeatBoss') {
      const boss = this.state.units.find(u => u.isBoss);
      if (boss && !boss.alive) return this.endBattle('win', '击破主将', events);
    }
  }

  private endBattle(outcome: BattleOutcome, reason: string, events: BattleEvent[]): void {
    this.state.outcome = outcome;
    if (outcome === 'lose') this.state.loseReason = reason;
    this.state.phase = 'over';
    events.push({ type: 'battleEnd', outcome, reason });
  }
}
