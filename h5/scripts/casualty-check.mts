// 玩家方阵亡率检查：评估新数值下我方生存压力
import { simulateBattle } from '../src/core/sim';
import { getLevel } from '../src/data';
import type { Difficulty, PartyMember } from '../src/core/types';

for (const lvId of [1, 3, 5, 7]) {
  const lv = getLevel(lvId)!;
  const party: PartyMember[] = lv.available.slice(0, 8).map(id => ({ charId: id, level: lvId * 2 + 1, equipment: {} }));
  for (const diff of ['normal', 'hard'] as Difficulty[]) {
    let deadTotal = 0, turns = 0, wins = 0;
    const N = 5;
    for (let s = 1; s <= N; s++) {
      const r = simulateBattle(lvId, party, diff, s, true);
      if (r.outcome === 'win') wins++;
      turns += r.turns;
      const died = new Set(
        r.events.filter(e => e.type === 'die').map(e => (e as { uid: string }).uid),
      );
      // 阵亡 uid 中属于我方的（uid 前 8 个为玩家方，按 Battle 创建顺序）
      const playerDead = [...died].filter(uid => {
        const n = parseInt(uid.slice(1), 10);
        return n < party.length;
      }).length;
      deadTotal += playerDead;
    }
    console.log(`第${lvId}关 ${diff}: 胜率 ${wins}/${N}，平均回合 ${(turns / N).toFixed(1)}，场均我方阵亡 ${(deadTotal / N).toFixed(1)} 人`);
  }
}
