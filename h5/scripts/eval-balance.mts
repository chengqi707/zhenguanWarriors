// ============================================================
// 数值机制评估脚本（PRD §15.4）：输出角色强度/命中倒挂/伤害回合/
// 难度区分度/关卡曲线 的量化数据，供评估结论使用。
// 用法：cd h5 && npx tsx scripts/eval-balance.mts
// ============================================================
import { CHARACTERS, LEVELS, DIFFICULTY_MOD, CLASS_COUNTER, getLevel } from '../src/data';
import type { CharacterDef, ClassType, Difficulty, PartyMember } from '../src/core/types';
import { simulateBattle } from '../src/core/sim';

const f = (n: number) => n.toFixed(1);
const pad = (s: string, n: number) => s.padEnd(n, '　');

// ---------- 1. 角色强度梯队（基础五维和 / Lv1、Lv20 对比） ----------
console.log('===== 1. 角色强度（五维和：Lv1 / Lv20 / HP Lv1） =====');
const statSum = (c: CharacterDef, lv: number) =>
  (['str', 'cmd', 'int', 'agi', 'luk'] as const).reduce(
    (s, k) => s + c.base[k] + c.growth[k] * (lv - 1), 0,
  );
const rows = CHARACTERS.map(c => ({
  name: c.name, cls: c.classType, pos: (c as unknown as { pos?: string }).pos ?? '-',
  s1: statSum(c, 1), s20: statSum(c, 20), hp: c.hp,
})).sort((a, b) => b.s1 - a.s1);
for (const r of rows) {
  console.log(`  ${pad(r.name, 6)} ${pad(r.cls, 10)} ${pad(r.pos, 12)} 五维和 ${r.s1} / ${r.s20}  HP ${r.hp}`);
}
const s1s = rows.map(r => r.s1);
console.log(`  → 最高 ${Math.max(...s1s)} / 最低 ${Math.min(...s1s)}，极差 ${Math.max(...s1s) - Math.min(...s1s)}（${f((Math.max(...s1s) / Math.min(...s1s) - 1) * 100)}%）`);

// ---------- 2. 命中率倒挂检查（02-combat：命中 = 75+(攻INT-守AGI)×0.5） ----------
console.log('\n===== 2. 命中率样本（公式 75+(INT-AGI)×0.5，钳制5-99） =====');
const hit = (int: number, agi: number) => Math.min(99, Math.max(5, Math.round(75 + (int - agi) * 0.5)));
const lishimin = CHARACTERS.find(c => c.id === 'lishimin')!;
const zswj = CHARACTERS.find(c => c.id === 'zhangsun_wuji')!;
const qin = CHARACTERS.find(c => c.id === 'qin_qiong')!;
const soldierAgi = 35; // 敌军步兵模板
console.log(`  李世民(INT${lishimin.base.int}) 攻 步兵(AGI${soldierAgi})：${hit(lishimin.base.int, soldierAgi)}%`);
console.log(`  秦琼(INT${qin.base.int}) 攻 步兵(AGI${soldierAgi})：${hit(qin.base.int, soldierAgi)}%`);
console.log(`  长孙无忌(INT${zswj.base.int}) 攻 步兵(AGI${soldierAgi})：${hit(zswj.base.int, soldierAgi)}%（谋士物理命中反超武将）`);
console.log(`  步兵(INT25) 攻 李世民(AGI${lishimin.base.agi})：${hit(25, lishimin.base.agi)}%`);
console.log(`  步兵(INT25) 攻 长孙无忌(AGI${zswj.base.agi})：${hit(25, zswj.base.agi)}%`);

// ---------- 3. 伤害与击杀回合（Lv1，普通，平原；R2 公式：STR×克制 - CMD×0.5） ----------
console.log('\n===== 3. 伤害样本（R2：基础=(STR×克制)-(CMD×0.5)，±5%浮动） =====');
const dmg = (str: number, counter: number, cmd: number) => Math.max(1, Math.round(str * counter - cmd * 0.5));
const lv1 = getLevel(1)!;
const bubu = lv1.enemies.find(e => !e.isBoss)!;
const boss = lv1.enemies.find(e => e.isBoss)!;
console.log(`  敌军模板：${bubu.name} STR${bubu.base.str} CMD${bubu.base.cmd} HP${bubu.hp}；${boss.name} HP${boss.hp}`);
const d1 = dmg(lishimin.base.str, CLASS_COUNTER.cavalry[bubu.classType as ClassType] ?? 1, bubu.base.cmd);
console.log(`  李世民(STR${lishimin.base.str}) → ${bubu.name}：${d1}/击，击杀约 ${Math.ceil(bubu.hp / d1)} 击`);
const d2 = dmg(bubu.base.str, CLASS_COUNTER[bubu.classType as ClassType].cavalry ?? 1, lishimin.base.cmd);
console.log(`  ${bubu.name}(STR${bubu.base.str}) → 李世民(CMD${lishimin.base.cmd})：${d2}/击，李世民HP${lishimin.hp} 可抗 ${Math.floor(lishimin.hp / Math.max(1, d2))} 击`);
const d3 = dmg(bubu.base.str, CLASS_COUNTER[bubu.classType as ClassType].strategist ?? 1, zswj.base.cmd);
console.log(`  ${bubu.name} → 长孙无忌(CMD${zswj.base.cmd})：${d3}/击，长孙无忌HP${zswj.hp} 可抗 ${Math.floor(zswj.hp / Math.max(1, d3))} 击（谋士生存压力）`);

// ---------- 4. 兵种克制系数极值 ----------
console.log('\n===== 4. 克制系数极值（1.0 为无克制） =====');
const flat: Array<[string, string, number]> = [];
for (const atk of Object.keys(CLASS_COUNTER) as ClassType[]) {
  for (const def of Object.keys(CLASS_COUNTER[atk]) as ClassType[]) {
    flat.push([atk, def, CLASS_COUNTER[atk][def]]);
  }
}
flat.sort((a, b) => b[2] - a[2]);
console.log(`  最强克制：${flat[0][0]}→${flat[0][1]} ×${flat[0][2]}；${flat[1][0]}→${flat[1][1]} ×${flat[1][2]}`);
console.log(`  最强抵抗：${flat[flat.length - 1][0]}→${flat[flat.length - 1][1]} ×${flat[flat.length - 1][2]}`);

// ---------- 5. 难度区分度仿真（第1/7关 × story/normal/hard × 5种子） ----------
console.log('\n===== 5. 难度区分度（AI 自动对战，胜率/平均回合） =====');
function defaultParty(levelId: number): PartyMember[] {
  const lv = getLevel(levelId)!;
  const recLv = levelId * 2 + 1;
  return lv.available.slice(0, 8).map(id => ({
    charId: id, level: recLv, equipment: {},
  }));
}
for (const lvId of [1, 7]) {
  for (const diff of ['story', 'normal', 'hard'] as Difficulty[]) {
    let wins = 0, turns = 0;
    const N = 5;
    for (let seed = 1; seed <= N; seed++) {
      const r = simulateBattle(lvId, defaultParty(lvId), diff, seed);
      if (r.outcome === 'win') wins++;
      turns += r.turns;
    }
    console.log(`  第${lvId}关 ${DIFFICULTY_MOD[diff] ? diff : diff}：胜率 ${wins}/${N}，平均回合 ${f(turns / N)}`);
  }
}

// ---------- 6. 关卡曲线（normal，默认队伍等级） ----------
console.log('\n===== 6. 关卡曲线（normal，推荐等级=关id×2+1） =====');
for (const lv of LEVELS) {
  let wins = 0, turns = 0;
  const N = 3;
  for (let seed = 1; seed <= N; seed++) {
    const r = simulateBattle(lv.id, defaultParty(lv.id), 'normal', seed);
    if (r.outcome === 'win') wins++;
    turns += r.turns;
  }
  console.log(`  第${lv.id}关「${lv.name}」 敌${lv.enemies.length}人：胜率 ${wins}/${N}，平均回合 ${f(turns / N)}`);
}
console.log('\n（注：仿真双方同 AI，仅作数值走向参考，不代表真人体验）');
