// ============================================================
// 敌方 AI 行为树（02-combat.md §7.1 优先级简化版 + §20 态势策略）：
// 0. 按敌我总战力/数量与关卡目标评估态势 → offensive / defensive / neutral
// 1. HP<阈值 且能远离最近敌人 → 撤退移动（不攻击）；防守态势阈值更高
// 2. MP 够且 AOE 计策覆盖 ≥2 敌人（或医疗奶到 HP<50% 友军）→ 计策
// 3. 射程内有敌人 → 集火 HP 最低者（克制系数≥1.2 权重+50%）
// 4. 进攻/中立：A* 向最近敌人逼近；防守：优先退守最近防御地形，已到位则待机
// Boss 无特殊逻辑；玩家方自动战斗（sim）复用同一套决策（态势恒为 neutral）。
// 简化：文档的「移向治疗者」「单挑」「威胁值」「全队协同/诱敌」未实现。
// ============================================================
import type { BattleEvent, TerrainType, Unit } from './types';
import type { Battle } from './battle';
import { CLASS_COUNTER, WEATHER_RULES, getSkill } from '../data';
import { Cell, hexDistance, hexRange, key, parseKey } from './hex';
import * as rules from './rules';

const isHostile = (a: Unit, b: Unit): boolean => (a.faction === 'enemy') !== (b.faction === 'enemy');

type Stance = 'offensive' | 'defensive' | 'neutral';

/** 地形防御优先级（用于 AI 找有利地形待机） */
const DEFENSE_PRIORITY: Record<TerrainType, number> = {
  plain: 0,
  forest: 5,
  mountain: 15,
  water: 0,
  city: 30,
  wall: 0,
  pass: 20,
  camp: 10,
  fence: 10,
};

/** 可被 AI 视为"防御地形"的类型 */
const DEFENSIVE_TERRAINS: TerrainType[] = ['city', 'pass', 'mountain', 'camp', 'fence', 'forest'];

/** 估算一组单位的总战力（HP×综合属性/100） */
function estimatePower(units: Unit[]): number {
  return units.reduce((sum, u) => {
    const attrs = u.stats.str + u.stats.cmd + u.stats.int * 0.5;
    return sum + (u.hp * attrs) / 100;
  }, 0);
}

/**
 * 评估当前战场态势。
 * 仅对敌方生效（玩家自动战斗仿真保持中立，避免 sim 结果剧烈波动）。
 * - 我方（玩家）强势 → 敌方以防守为主（后撤、占有利地形、保持距离）
 * - 敌方占优势 → 敌方主动进攻（激进集火、低血量也不后撤）
 * - 坚守关卡随回合推进越来越偏防守
 */
function evaluateStance(battle: Battle, unit: Unit): Stance {
  if (unit.faction !== 'enemy') return 'neutral';
  const lv = battle.state.level;
  const enemies = battle.state.units.filter(u => u.alive && u.faction === 'enemy');
  const players = battle.state.units.filter(u => u.alive && u.faction === 'player');
  if (players.length === 0) return 'neutral';

  const enemyPower = estimatePower(enemies);
  const playerPower = estimatePower(players);
  const ratio = enemyPower / Math.max(1, playerPower);
  const countRatio = enemies.length / Math.max(1, players.length);

  // 坚守关：回合越往后越倾向于防守
  if (lv.victory === 'defendTurns' && lv.defendTurns) {
    const progress = battle.state.turn / lv.defendTurns;
    if (progress >= 0.5 || ratio < 0.8) return 'defensive';
    if (progress <= 0.25 && ratio > 1.2) return 'offensive';
  }

  // 斩首关：Boss 存活时保护 Boss，偏弱则收缩防守
  if (lv.victory === 'defeatBoss') {
    const bossAlive = enemies.some(u => u.isBoss);
    if (bossAlive && ratio < 0.9) return 'defensive';
    if (ratio > 1.3) return 'offensive';
    return 'neutral';
  }

  // 全灭关：纯战力对比
  if (ratio < 0.6 || countRatio < 0.5) return 'defensive';
  if (ratio > 1.5 || countRatio > 1.5) return 'offensive';
  return 'neutral';
}

/** 找到离 (q,r) 最近且可进入的防御地形格（优先防御值高、次之距离近） */
function nearestDefensiveCell(
  battle: Battle,
  unit: Unit,
  reach: Map<string, number>,
): Cell | null {
  let best: Cell | null = null;
  let bestScore = -Infinity;
  const terrainOf = (q: number, r: number) => battle.state.level.terrain[key(q, r)] ?? 'plain';
  for (const k of reach.keys()) {
    const c = parseKey(k);
    const terrain = terrainOf(c.q, c.r);
    if (!DEFENSIVE_TERRAINS.includes(terrain)) continue;
    // reach 已排除他人占据的格子，起点保留即可
    const dist = hexDistance(unit.q, unit.r, c.q, c.r);
    const score = DEFENSE_PRIORITY[terrain] - dist * 2;
    if (score > bestScore) {
      bestScore = score;
      best = c;
    }
  }
  return best;
}

/** 选目标：HP 最低；克制系数 ≥1.2 的目标权重 +50%（02-combat §7.1 集火） */
function pickTarget(attacker: Unit, foes: Unit[]): Unit {
  let best = foes[0];
  let bestScore = Infinity;
  for (const f of foes) {
    const w = CLASS_COUNTER[attacker.classType][f.classType] >= 1.2 ? 1.5 : 1;
    const score = f.hp / w;
    if (score < bestScore) {
      bestScore = score;
      best = f;
    }
  }
  return best;
}

function minDistToFoes(q: number, r: number, foes: Unit[]): number {
  let d = Infinity;
  for (const f of foes) d = Math.min(d, hexDistance(q, r, f.q, f.r));
  return d;
}

/** 跑一个单位的完整决策，事件 push 进 events */
export function runUnitAI(battle: Battle, unit: Unit, events: BattleEvent[]): void {
  const foes = battle.state.units.filter(u => u.alive && isHostile(unit, u));
  if (foes.length === 0) {
    battle.aiWait(unit, events);
    return;
  }
  const friends = battle.state.units.filter(u => u.alive && !isHostile(unit, u));
  const stance = evaluateStance(battle, unit);
  const retreatThreshold = stance === 'defensive' ? 0.5 : stance === 'offensive' ? 0.2 : 0.3;
  const terrainOf = (q: number, r: number) => battle.state.level.terrain[key(q, r)] ?? 'plain';

  // 1. 撤退：HP<阈值 且能找到离最近敌人更远的格子
  if (unit.hp / unit.maxHp < retreatThreshold) {
    const reach = battle.reachableFor(unit);
    const cur = minDistToFoes(unit.q, unit.r, foes);
    let best: Cell | null = null;
    let bestDist = cur;
    for (const k of reach.keys()) {
      const c = parseKey(k);
      if (c.q === unit.q && c.r === unit.r) continue;
      const d = minDistToFoes(c.q, c.r, foes);
      if (d > bestDist) {
        bestDist = d;
        best = c;
      }
    }
    if (best) {
      battle.aiMoveAttack(unit, best, null, events);
      return;
    }
    // 无法更远 → 落到后续正常逻辑
  }

  // 2a. 医疗：射程内有 HP<50% 友军 → 奶血量比例最低者
  for (const sid of unit.skills) {
    const sk = getSkill(sid);
    if (!sk || sk.kind !== 'heal') continue;
    if (unit.mp < Math.round(sk.mp * unit.mpCostMult)) continue;
    const candidates = friends.filter(
      f => f.hp / f.maxHp < 0.5 && hexDistance(unit.q, unit.r, f.q, f.r) <= sk.range,
    );
    if (candidates.length > 0) {
      candidates.sort((a, b) => a.hp / a.maxHp - b.hp / b.maxHp);
      battle.aiSkill(unit, sid, { q: candidates[0].q, r: candidates[0].r }, events);
      return;
    }
  }

  // 2b. AOE 伤害计策：枚举敌人所在格为施放点，覆盖 ≥2 敌人则用（雨天不放火攻）
  const weather = battle.state.weather;
  let bestSkill: string | null = null;
  let bestCell: Cell | null = null;
  let bestCount = 1; // 需 ≥2 才值得放
  for (const sid of unit.skills) {
    const sk = getSkill(sid);
    if (!sk || sk.kind !== 'damage') continue;
    if (sk.aoe === 'single' || sk.aoe === 'all') continue;
    if (unit.mp < Math.round(sk.mp * unit.mpCostMult)) continue;
    if (sk.id === 'fire_attack' && WEATHER_RULES[weather].firePowerMult === 0) continue;
    for (const foe of foes) {
      if (hexDistance(unit.q, unit.r, foe.q, foe.r) > sk.range) continue;
      const cells = rules.skillAoeCells(
        sk, unit.q, unit.r, foe.q, foe.r, battle.state.level.width, battle.state.level.height,
      );
      const count = foes.filter(f => cells.some(c => c.q === f.q && c.r === f.r)).length;
      if (count > bestCount) {
        bestCount = count;
        bestSkill = sid;
        bestCell = { q: foe.q, r: foe.r };
      }
    }
  }
  if (bestSkill && bestCell) {
    battle.aiSkill(unit, bestSkill, bestCell, events);
    return;
  }

  // 3. 射程内有敌人 → 集火
  const inRange = foes.filter(f => hexDistance(unit.q, unit.r, f.q, f.r) <= unit.range);
  if (inRange.length > 0) {
    // 防守态势：若已站在防御地形且血量尚可，原地射击/反击，不轻易前出
    if (stance === 'defensive') {
      const curTerrain = terrainOf(unit.q, unit.r);
      if (DEFENSIVE_TERRAINS.includes(curTerrain) && unit.hp / unit.maxHp >= 0.4) {
        battle.aiMoveAttack(unit, null, pickTarget(unit, inRange).uid, events);
        return;
      }
    }
    battle.aiMoveAttack(unit, null, pickTarget(unit, inRange).uid, events);
    return;
  }

  // 4. 移动策略
  const reach = battle.reachableFor(unit);

  // 4a. 防守态势：优先退守防御地形；已在防御地形则原地待机诱敌
  if (stance === 'defensive') {
    const curTerrain = terrainOf(unit.q, unit.r);
    if (DEFENSIVE_TERRAINS.includes(curTerrain)) {
      battle.aiWait(unit, events);
      return;
    }
    const best = nearestDefensiveCell(battle, unit, reach);
    if (best) {
      battle.aiMoveAttack(unit, best, null, events);
      return;
    }
    // 找不到防御地形：落到后续逼近逻辑
  }

  // 4b. 进攻/中立：A* 找到可攻击落点（敌人射程内、可进入、未占据），沿路径走到
  //     移动力允许的最后一格（真实路径绕行山地/城郭，避免贪心直线距离卡在
  //     局部最优——第2关霍邑攻坚的城池口袋地形就是这种情况）；到点可打则打
  const foesByDist = [...foes].sort(
    (a, b) => hexDistance(unit.q, unit.r, a.q, a.r) - hexDistance(unit.q, unit.r, b.q, b.r),
  );
  let path: Cell[] | null = null;
  for (const foe of foesByDist) {
    // 候选落点：距该敌人 ≤ 自身射程、可进入的格子
    for (const c of hexRange(foe.q, foe.r, unit.range)) {
      if (c.q === foe.q && c.r === foe.r) continue;
      if (!battle.canEnter(unit, c.q, c.r)) continue;
      const p = battle.pathFor(unit, c);
      if (p && (!path || p.length < path.length)) path = p;
    }
    if (path) break; // 找到通往最近敌人的路径即可
  }
  let dest: Cell = { q: unit.q, r: unit.r };
  if (path) {
    for (const c of path) {
      if (reach.has(`${c.q},${c.r}`)) dest = c;
      else break; // 超出本回合移动力，停在上一个可达格
    }
  }
  const inRange2 = foes.filter(f => hexDistance(dest.q, dest.r, f.q, f.r) <= unit.range);
  battle.aiMoveAttack(unit, dest, inRange2.length > 0 ? pickTarget(unit, inRange2).uid : null, events);
}
