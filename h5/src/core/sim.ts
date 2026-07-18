// ============================================================
// 无头仿真——`npm run sim`（tsx 直跑，node 环境，零 DOM）。
// 玩家方复用 ai.ts 同一套决策自动打完整场，用于验证引擎：
// 9 关（§16.4 含序幕 id=0）× 3 种子跑胜负/回合数，再打 1 次第 1 关打印事件样本。
// 200 回合上限兜底：超限判负并告警（防 AI 互相风筝死循环）。
// ============================================================
import type { BattleEvent, BattleOutcome, Difficulty, PartyMember } from './types';
import { Battle } from './battle';
import { runUnitAI } from './ai';
import { getLevel } from '../data';
import { hexDistance, neighbors } from './hex';

const TURN_CAP = 200;

export interface SimResult {
  outcome: BattleOutcome;
  turns: number;      // 实际进行的回合数
  eventCount: number; // 全场事件总数
  timedOut: boolean;  // 触发 200 回合兜底
  events: BattleEvent[]; // keepEvents=true 时收集，否则为空
}

/**
 * 自动打一场战斗：玩家回合按 AGI 降序逐个跑 runUnitAI（与敌方同一套
 * 行为树），全部行动完调用 endTurn()（敌方回合在 Battle 内部跑）。
 */
export function simulateBattle(
  levelId: number,
  party: PartyMember[],
  difficulty: Difficulty,
  seed: number,
  keepEvents: boolean = false,
): SimResult {
  const level = getLevel(levelId);
  if (!level) throw new Error(`未知关卡: ${levelId}`);
  const battle = new Battle(level, party, difficulty, seed);
  const events: BattleEvent[] = [];
  let eventCount = 0;
  let timedOut = false;

  while (!battle.isOver()) {
    if (battle.state.turn > TURN_CAP) {
      timedOut = true;
      console.warn(`[sim] 警告：第${levelId}关 seed=${seed} 超过 ${TURN_CAP} 回合未分胜负，判负兜底`);
      break;
    }
    // 玩家回合：AGI 降序逐个 AI 行动
    const actors = battle.state.units
      .filter(u => u.alive && u.faction === 'player' && !u.acted)
      .sort((a, b) => b.stats.agi - a.stats.agi);
    for (const u of actors) {
      if (battle.isOver()) break;
      if (!u.alive || u.acted) continue;
      const evts: BattleEvent[] = [];
      runUnitAI(battle, u, evts);
      eventCount += evts.length;
      if (keepEvents) events.push(...evts);
    }
    if (battle.isOver()) break;
    const res = battle.endTurn();
    eventCount += res.events.length;
    if (keepEvents) events.push(...res.events);
  }

  return {
    outcome: battle.isOver()?.outcome ?? 'lose',
    turns: Math.min(battle.state.turn, TURN_CAP),
    eventCount,
    timedOut,
    events,
  };
}

/** 组出战队伍：必出角色优先，再按 available 顺序补足 8 人；等级=关卡id×2+1，默认装备（空） */
function defaultParty(levelId: number): PartyMember[] {
  const level = getLevel(levelId)!;
  const ids: string[] = [];
  for (const id of level.required) if (!ids.includes(id)) ids.push(id);
  for (const id of level.available) {
    if (ids.length >= 8) break;
    if (!ids.includes(id)) ids.push(id);
  }
  const lv = levelId * 2 + 1; // 推荐等级（任务指定）
  return ids.map(charId => ({ charId, level: lv, equipment: {} }));
}

/** 单行描述一条事件（样本打印用） */
function fmtEvent(e: BattleEvent): string {
  switch (e.type) {
    case 'turnBegin': return `回合${e.turn} 开始（${e.phase}）`;
    case 'move': return `${e.uid} 移动 ${e.path.length} 格 → (${e.path[e.path.length - 1].q},${e.path[e.path.length - 1].r})`;
    case 'attack': return `${e.uid} 攻击 ${e.targetUid} ${e.hit ? `命中${e.crit ? '·暴击' : ''} 伤害${e.dmg}` : '未命中'}（目标余${e.targetHpAfter}）`;
    case 'counter': return `${e.uid} 反击 ${e.targetUid} ${e.hit ? `伤害${e.dmg}` : '未命中'}（目标余${e.targetHpAfter}）`;
    case 'skill': return `${e.uid} 施放 ${e.skillId}（${e.cells.length} 格）`;
    case 'damage': return `${e.uid} 受 ${e.amount} 伤害（余${e.hpAfter}）`;
    case 'heal': return `${e.uid} 恢复 ${e.amount}（余${e.hpAfter}）`;
    case 'buff': return `${e.uid} 获得 ${e.buff.name}（${e.buff.remainingTurns}回合）`;
    case 'die': return `${e.uid} 阵亡`;
    case 'wait': return `${e.uid} 待机`;
    case 'battleEnd': return `战斗结束：${e.outcome}${e.reason ? `（${e.reason}）` : ''}`;
  }
}

/** 经典行动流冒烟：moveUnit / cancelMove / attackWith / useItem 契约（第1关 seed=1） */
function smokeClassicFlow(): void {
  console.log('\n===== 经典行动流冒烟（第1关，seed=1，携带金疮药×2） =====');
  const checks: Array<[string, boolean]> = [];
  const battle = new Battle(getLevel(1)!, defaultParty(1), 'normal', 1, { jinchuang: 2 });
  const hero = battle.state.units.find(u => u.isHero)!; // 李世民
  const from = { q: hero.q, r: hero.r };

  // 1. moveUnit 一格 → ok、moved=true、acted 不变、产生 move 事件
  const step = neighbors(hero.q, hero.r).find(c => battle.canEnter(hero, c.q, c.r));
  checks.push(['存在相邻可移动格', !!step]);
  if (step) {
    const r1 = battle.moveUnit(hero.uid, [step]);
    checks.push(['moveUnit ok 且产生 move 事件', r1.ok && r1.events.some(e => e.type === 'move')]);
    checks.push(['moveUnit 后 moved=true、acted=false', hero.moved && !hero.acted]);
    // 2. 未撤回前不可再移动
    checks.push(['重复 moveUnit 被拒（本回合已移动）', !battle.moveUnit(hero.uid, [from]).ok]);
    // 3. cancelMove 回原位
    const r2 = battle.cancelMove(hero.uid);
    checks.push(['cancelMove ok', r2.ok]);
    checks.push(['cancelMove 回到原位且 moved=false', hero.q === from.q && hero.r === from.r && !hero.moved]);
    // 4. 撤回后再 moveUnit 成功
    const r3 = battle.moveUnit(hero.uid, [step]);
    checks.push(['撤回后再次 moveUnit ok', r3.ok && hero.q === step.q && hero.r === step.r]);
  }

  // 5. attackWith：射程内有敌 → ok+acted；无敌 → fail 且 acted 不变
  const inRange = battle.state.units.find(
    e => e.alive && e.faction === 'enemy' && hexDistance(hero.q, hero.r, e.q, e.r) <= hero.range,
  );
  if (inRange) {
    const r4 = battle.attackWith(hero.uid, inRange.uid);
    checks.push(['attackWith 射程内 ok 且 acted=true', r4.ok && hero.acted]);
  } else {
    const far = battle.state.units.find(e => e.alive && e.faction === 'enemy')!;
    const r4 = battle.attackWith(hero.uid, far.uid);
    checks.push(['attackWith 超射程 fail 且 acted 不变', !r4.ok && !hero.acted]);
  }

  // 6. useItem 金疮药：回血（heal 事件）、acted=true、库存-1（英雄已行动则换未行动队友）
  const user = !hero.acted ? hero : battle.state.units.find(u => u.alive && u.faction === 'player' && !u.acted);
  if (user) {
    user.hp = Math.max(1, user.hp - 100); // 模拟战损以便观察回血
    const hpBefore = user.hp;
    const cntBefore = battle.getItemCounts().jinchuang ?? 0;
    const r5 = battle.useItem(user.uid, 'jinchuang');
    checks.push([
      'useItem 金疮药 ok 且回血（heal 事件）',
      r5.ok && user.hp > hpBefore && r5.events.some(e => e.type === 'heal' && e.uid === user.uid),
    ]);
    checks.push(['useItem 后 acted=true', user.acted]);
    checks.push(['useItem 后库存-1', (battle.getItemCounts().jinchuang ?? 0) === cntBefore - 1]);
  }

  let pass = 0;
  for (const [name, ok] of checks) {
    if (ok) pass++;
    console.log(`  ${ok ? 'PASS' : 'FAIL'}  ${name}`);
  }
  console.log(`冒烟结果：${pass}/${checks.length} 通过`);
}

function main(): void {
  const seeds = [1, 2, 3];
  const difficulty: Difficulty = 'normal';
  console.log(`===== 贞观勇士 H5 引擎仿真（${difficulty}，玩家方=AI 自动） =====`);

  // 1. 9 关（含序幕 id=0）× 3 种子
  let totalWin = 0;
  let totalRun = 0;
  for (let levelId = 0; levelId <= 8; levelId++) {
    const level = getLevel(levelId)!;
    const party = defaultParty(levelId);
    const parts: string[] = [];
    let wins = 0;
    for (const seed of seeds) {
      const r = simulateBattle(levelId, party, difficulty, seed);
      if (r.outcome === 'win') wins++;
      totalRun++;
      if (r.outcome === 'win') totalWin++;
      parts.push(`seed${seed}:${r.outcome === 'win' ? '胜' : '负'}${r.turns}回合${r.timedOut ? '(超时)' : ''}`);
    }
    console.log(
      `第${levelId}关「${level.name}」 胜率${wins}/${seeds.length} | ${parts.join(' | ')}`,
    );
  }
  console.log(`总计：${totalWin}/${totalRun} 胜`);

  // 2. 第 1 关 seed=1 事件样本（前 30 条）
  console.log('\n===== 第1关事件样本（seed=1，前30条） =====');
  const sample = simulateBattle(1, defaultParty(1), difficulty, 1, true);
  sample.events.slice(0, 30).forEach((e, i) => console.log(`${String(i + 1).padStart(3)}. ${fmtEvent(e)}`));
  console.log(`（全场共 ${sample.eventCount} 条事件，${sample.turns} 回合，${sample.outcome}）`);

  // 3. 经典行动流冒烟
  smokeClassicFlow();
}

main();
