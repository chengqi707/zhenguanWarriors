// ============================================================
// 战斗物品——出战携带的消耗品（曹操传式「物品」行动）。
// 效果结算在 core/battle.ts useItem()：
//   heal_hp 按最大HP百分比回血 / heal_mp 固定回蓝（封顶 maxMp）/
//   buff_atk 加 attackPct buff（持续3回合，回合结束递减）。
// ============================================================
import type { ItemDef } from '../core/types';

export const ITEMS: ItemDef[] = [
  { id: 'jinchuang', name: '金疮药', desc: '恢复30%最大HP', kind: 'heal_hp', value: 30, price: 50 },
  { id: 'qingxin', name: '清心丸', desc: '恢复20MP', kind: 'heal_mp', value: 20, price: 80 },
  { id: 'shiqi', name: '士气丹', desc: '攻击+10%，持续3回合', kind: 'buff_atk', value: 10, price: 100 },
];

/** 按 id 查物品，未找到返回 undefined */
export function getItem(id: string): ItemDef | undefined {
  return ITEMS.find(i => i.id === id);
}
