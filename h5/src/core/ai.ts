// ============================================================
// 敌方 AI 行为树（02-combat.md §7.1 优先级简化版）：
// 1. HP<30% 且能远离最近敌人 → 撤退移动（不攻击）
// 2. MP 够且 AOE 计策覆盖 ≥2 敌人（或医疗奶到 HP<50% 友军）→ 计策
// 3. 射程内有敌人 → 集火 HP 最低者（克制系数≥1.2 权重+50%）
// 4. 否则 A* 向最近敌人逼近（贴近后若可攻击则攻击）
// Boss 无特殊逻辑；玩家方自动战斗（sim）复用同一套决策。
// 简化：文档的「移向治疗者」「单挑」「威胁值」「全队协同/诱敌」未实现。
// ============================================================
import type { BattleEvent, Unit } from './types';
import type { Battle } from './battle';
import { CLASS_COUNTER, WEATHER_RULES, getSkill } from '../data';
import { Cell, hexDistance, hexRange, parseKey } from './hex';
import * as rules from './rules';

const isHostile = (a: Unit, b: Unit): boolean => (a.faction === 'enemy') !== (b.faction === 'enemy');

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

  // 1. 撤退：HP<30% 且能找到离最近敌人更远的格子
  if (unit.hp / unit.maxHp < 0.3) {
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
    battle.aiMoveAttack(unit, null, pickTarget(unit, inRange).uid, events);
    return;
  }

  // 4. 逼近：A* 找到可攻击落点（敌人射程内、可进入、未占据），沿路径走到
  //    移动力允许的最后一格（真实路径绕行山地/城郭，避免贪心直线距离卡在
  //    局部最优——第2关霍邑攻坚的城池口袋地形就是这种情况）；到点可打则打
  const reach = battle.reachableFor(unit);
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
