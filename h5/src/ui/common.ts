// ============================================================
// UI 公共工具——页面接口、DOM 快捷创建、中文标签映射、确认弹窗。
// ============================================================
import type {
  CharacterDef, ClassType, Difficulty, EquipmentDef, Rarity, Stats, Weather,
} from '../core/types';

/** 页面接口：每页一个类，由 game.ts 状态机路由 */
export interface Page {
  render(root: HTMLElement): void;
  destroy(): void;
}

/** 快捷创建元素 */
export function h<K extends keyof HTMLElementTagNameMap>(
  tag: K, className?: string, text?: string,
): HTMLElementTagNameMap[K] {
  const el = document.createElement(tag);
  if (className) el.className = className;
  if (text !== undefined) el.textContent = text;
  return el;
}

// ---------- 中文标签 ----------
export const CLASS_NAMES: Record<ClassType, string> = {
  infantry: '步兵', heavy: '重甲', cavalry: '骑兵',
  archer: '弓兵', siege: '器械', strategist: '谋士',
  spear: '矛兵', catapult: '投石车', // §16.2 新兵种
};

/** 兵种色（用于卡片色条/兵种名） */
export const CLASS_COLORS: Record<ClassType, string> = {
  infantry: '#7BA05B', heavy: '#8C8C9E', cavalry: '#C2682B',
  archer: '#3E8E7E', siege: '#A67C3D', strategist: '#6B5B95',
  spear: '#5B7FA0', catapult: '#8C5B3D', // §16.2 新兵种
};

export const WEATHER_ICONS: Record<Weather, string> = {
  sunny: '☀', rain: '🌧', snow: '❄', fog: '🌫', windy: '🌬',
};

export const WEATHER_NAMES: Record<Weather, string> = {
  sunny: '晴', rain: '雨', snow: '雪', fog: '雾', windy: '风',
};

export const DIFFICULTY_NAMES: Record<Difficulty, string> = {
  story: '极简', easy: '简单', normal: '普通', hard: '困难',
};

export const DIFFICULTY_ORDER: Difficulty[] = ['story', 'easy', 'normal', 'hard'];

/** 稀有度色：白/绿/蓝/紫 */
export const RARITY_COLORS: Record<Rarity, string> = {
  white: '#D9D9D9', green: '#33CC33', blue: '#3373D9', purple: '#A64DFF',
};

export const RARITY_NAMES: Record<Rarity, string> = {
  white: '普通', green: '精良', blue: '稀有', purple: '史诗',
};

export const STAT_NAMES: Record<keyof Stats, string> = {
  str: '武', cmd: '统', int: '智', agi: '敏', luk: '运',
};

export const STAT_KEYS: (keyof Stats)[] = ['str', 'cmd', 'int', 'agi', 'luk'];

/** 按等级计算基础五维（含成长，口径与 core/battle.ts computeGrowth 一致） */
export function statsAtLevel(def: CharacterDef, level: number): Stats {
  const s: Stats = { ...def.base };
  for (let l = 1; l < level; l++) {
    s.str += def.growth.str;
    s.cmd += def.growth.cmd;
    s.int += def.growth.int;
    s.agi += def.growth.agi;
    s.luk += def.growth.luk;
  }
  return s;
}

/** 五维一行展示：武82 统95 智88 敏78 运90 */
export function statsLine(s: Stats): string {
  return STAT_KEYS.map(k => `${STAT_NAMES[k]}${s[k]}`).join(' ');
}

/** 装备属性加成摘要文本 */
export function equipBonusText(def: EquipmentDef): string {
  const parts: string[] = [];
  if (def.statBonus) {
    for (const k of STAT_KEYS) {
      const v = def.statBonus[k];
      if (v) parts.push(`${STAT_NAMES[k]}+${v}`);
    }
  }
  if (def.hpBonus) parts.push(`HP+${def.hpBonus}`);
  if (def.mpBonus) parts.push(`MP+${def.mpBonus}`);
  if (def.moveBonus) parts.push(`移动${def.moveBonus > 0 ? '+' : ''}${def.moveBonus}`);
  if (def.rangeBonus) parts.push(`射程+${def.rangeBonus}`);
  if (def.attackPct) parts.push(`攻击+${def.attackPct}%`);
  if (def.defensePct) parts.push(`防御+${def.defensePct}%`);
  if (def.critBonus) parts.push(`暴击+${def.critBonus}%`);
  return parts.join(' ');
}

/** 通用确认弹窗（遮罩 + 确定/取消） */
export function showConfirm(opts: {
  text: string;
  okText?: string;
  cancelText?: string;
  danger?: boolean;
  onOk: () => void;
}): void {
  const overlay = h('div', 'modal-overlay');
  const box = h('div', 'modal-box');
  box.appendChild(h('p', 'modal-text', opts.text));
  const btns = h('div', 'modal-btns');
  const cancelBtn = h('button', 'btn btn-dark', opts.cancelText ?? '取消');
  const okBtn = h('button', `btn ${opts.danger ? 'btn-primary' : 'btn-gold'}`, opts.okText ?? '确定');
  const close = () => overlay.remove();
  cancelBtn.onclick = close;
  okBtn.onclick = () => { close(); opts.onOk(); };
  btns.append(cancelBtn, okBtn);
  box.appendChild(btns);
  overlay.appendChild(box);
  overlay.addEventListener('click', e => { if (e.target === overlay) close(); });
  document.body.appendChild(overlay);
}
