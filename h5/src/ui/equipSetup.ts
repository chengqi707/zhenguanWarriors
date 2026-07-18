// ============================================================
// 配装页——左侧已选角色列表，右侧当前角色详情：
// 概要（名字+pos 定位徽章）、五维三列对比（基础含成长 / 装备加成 / 总计）、
// 3 个装备槽卡片（稀有度色+属性+卸下，点击槽弹可选装备列表）、
// 兵种特性一句话、被动与已激活羁绊；装备即改即存；底部「开始战斗」。
// ============================================================
import { BONDS, CLASS_TRAITS, getCharacter, getEquipment, getPassive } from '../data';
import { save as persist } from '../core/save';
import type {
  CharacterDef, CharacterProgress, EquipmentDef, EquipSlot, SaveData, Stats,
} from '../core/types';
import type { Game } from './game';
import {
  CLASS_COLORS, CLASS_NAMES, RARITY_COLORS, RARITY_NAMES, STAT_KEYS, STAT_NAMES,
  equipBonusText, h, statsAtLevel, type Page,
} from './common';
import { getPortraitURL } from './portraits';

const SLOT_NAMES: Record<EquipSlot, string> = {
  weapon: '武器', armor: '防具', trinket: '饰品',
};
const SLOT_ORDER: EquipSlot[] = ['weapon', 'armor', 'trinket'];

/** 装备是否可被该角色使用（部位/兵种/性别/专属过滤） */
function canEquip(def: EquipmentDef, char: CharacterDef): boolean {
  if (def.charId && def.charId !== char.id) return false;
  if (def.gender && def.gender !== char.gender) return false;
  if (def.classes && !def.classes.includes(char.classType)) return false;
  return true;
}

export class EquipSetupPage implements Page {
  private currentId = '';
  private detailEl: HTMLElement | null = null;
  private listEl: HTMLElement | null = null;

  constructor(private game: Game) {}

  render(root: HTMLElement): void {
    const save = this.game.save;
    if (!save || this.game.partyIds.length === 0) { this.game.toLevelSelect(); return; }
    this.currentId = this.game.partyIds[0];

    const page = h('div', 'page');
    page.appendChild(h('div', 'page-title', '⚔ 装备调整'));

    const main = h('div', 'equip-main');
    this.listEl = h('div', 'equip-roster');
    this.detailEl = h('div', 'equip-detail');
    main.append(this.listEl, this.detailEl);
    page.appendChild(main);

    const backBtn = h('button', 'btn btn-dark btn-bottom-half', '← 返回选人');
    backBtn.onclick = () => this.game.toHeroSelect();
    const startBtn = h('button', 'btn btn-primary btn-bottom', '⚔ 开始战斗');
    startBtn.onclick = () => this.game.toBattle();
    const bottomBar = h('div', 'bottom-bar');
    bottomBar.append(backBtn, startBtn);
    page.appendChild(bottomBar);

    this.renderRoster();
    this.renderDetail();
    root.appendChild(page);
  }

  // ---------- 左侧：已选角色列表 ----------
  private renderRoster(): void {
    const save = this.game.save!;
    this.listEl!.replaceChildren();
    for (const id of this.game.partyIds) {
      const def = getCharacter(id)!;
      const prog = save.characters.find(c => c.id === id)!;
      const item = h('div', `roster-item${id === this.currentId ? ' current' : ''}`);
      // 程序生成立绘（40×40 圆形）
      const face = h('img', 'portrait portrait-sm');
      face.src = getPortraitURL(id, 40);
      face.alt = def.name;
      const txt = h('div', 'roster-text');
      txt.appendChild(h('div', 'roster-name', def.name));
      txt.appendChild(h('div', 'roster-sub',
        `${CLASS_NAMES[def.classType]} Lv.${prog.level}`));
      item.append(face, txt);
      item.onclick = () => {
        this.currentId = id;
        this.renderRoster();
        this.renderDetail();
      };
      this.listEl!.appendChild(item);
    }
  }

  // ---------- 右侧：角色详情 ----------
  private renderDetail(): void {
    const save = this.game.save!;
    const def = getCharacter(this.currentId)!;
    const prog = save.characters.find(c => c.id === this.currentId)!;
    const box = this.detailEl!;
    box.replaceChildren();

    // 概要（名字旁 64×64 立绘；兵种名后 pos 定位徽章）
    const head = h('div', 'detail-head');
    const face = h('img', 'portrait portrait-lg');
    face.src = getPortraitURL(def.id, 64);
    face.alt = def.name;
    head.appendChild(face);
    head.appendChild(h('span', 'detail-name', def.name));
    head.appendChild(h('span', 'pos-badge pos-badge-lg', def.pos));
    head.appendChild(h('span', 'detail-lv', `Lv.${prog.level}`));
    const cls = h('span', 'detail-class', CLASS_NAMES[def.classType]);
    cls.style.color = CLASS_COLORS[def.classType];
    head.appendChild(cls);
    box.appendChild(head);

    box.appendChild(this.buildStatTable(def, prog));

    // 装备槽 ×3
    box.appendChild(h('div', 'section-title', '── 装备 ──'));
    for (const slot of SLOT_ORDER) {
      box.appendChild(this.buildSlotCard(save, def, prog, slot));
    }

    // 兵种特性（被动技能上方）
    box.appendChild(h('div', 'detail-trait',
      `兵种特性：${CLASS_TRAITS[def.classType]}`));

    // 被动技能
    const passive = getPassive(def.passive);
    if (passive) {
      box.appendChild(h('div', 'detail-passive',
        `⚡ 被动：${passive.name}（${passive.desc}）`));
    }

    // 已激活羁绊（该角色参与且条件满足的）
    for (const bond of BONDS) {
      if (!bond.members.includes(def.id)) continue;
      const count = bond.members.filter(m => this.game.partyIds.includes(m)).length;
      if (count >= bond.minCount) {
        box.appendChild(h('div', 'detail-bond', `✦ ${bond.name} 已激活`));
      }
    }

    // 射程/移动力（含装备）
    const eqs = this.equippedDefs(prog);
    const moveBonus = eqs.reduce((s, e) => s + (e.moveBonus ?? 0), 0);
    const rangeBonus = eqs.reduce((s, e) => s + (e.rangeBonus ?? 0), 0);
    const hpBonus = eqs.reduce((s, e) => s + (e.hpBonus ?? 0), 0);
    const mpBonus = eqs.reduce((s, e) => s + (e.mpBonus ?? 0), 0);
    box.appendChild(h('div', 'detail-extra',
      `攻击范围：${def.range + rangeBonus}　移动力：${def.move + moveBonus}` +
      `　HP+${hpBonus}　MP+${mpBonus}`));
  }

  /** 五维三列对比表：基础（含成长） / 装备加成（绿） / 总计 */
  private buildStatTable(def: CharacterDef, prog: CharacterProgress): HTMLElement {
    const base = statsAtLevel(def, prog.level);
    const bonus: Stats = { str: 0, cmd: 0, int: 0, agi: 0, luk: 0 };
    for (const e of this.equippedDefs(prog)) {
      if (!e.statBonus) continue;
      for (const k of STAT_KEYS) bonus[k] += e.statBonus[k] ?? 0;
    }

    const table = h('div', 'stat-table');
    const header = h('div', 'stat-row stat-head');
    header.append(h('span', 'stat-cell', '属性'), h('span', 'stat-cell', '基础'),
      h('span', 'stat-cell', '装备'), h('span', 'stat-cell', '总计'));
    table.appendChild(header);
    for (const k of STAT_KEYS) {
      const row = h('div', 'stat-row');
      row.appendChild(h('span', 'stat-cell', STAT_NAMES[k]));
      row.appendChild(h('span', 'stat-cell', String(base[k])));
      const b = h('span', 'stat-cell stat-bonus', bonus[k] > 0 ? `+${bonus[k]}` : '—');
      row.appendChild(b);
      row.appendChild(h('span', 'stat-cell stat-total', String(base[k] + bonus[k])));
      table.appendChild(row);
    }
    return table;
  }

  /** 单个装备槽卡片 */
  private buildSlotCard(
    save: SaveData, def: CharacterDef, prog: CharacterProgress, slot: EquipSlot,
  ): HTMLElement {
    const equipId = prog.equipment[slot];
    const eq = equipId ? getEquipment(equipId) : undefined;

    const card = h('div', `slot-card${eq ? '' : ' empty'}`);
    const tag = h('span', 'slot-tag', SLOT_NAMES[slot]);

    if (eq) {
      const nameEl = h('span', 'slot-name', eq.name);
      nameEl.style.color = RARITY_COLORS[eq.rarity];
      const head = h('div', 'slot-head');
      head.append(tag, nameEl,
        h('span', 'slot-rarity', RARITY_NAMES[eq.rarity]));
      card.appendChild(head);
      const bonus = equipBonusText(eq);
      if (bonus) card.appendChild(h('div', 'slot-bonus', bonus));
      if (eq.effectDesc) card.appendChild(h('div', 'slot-effect', eq.effectDesc));

      const unequipBtn = h('button', 'btn-mini', '卸下');
      unequipBtn.onclick = e => {
        e.stopPropagation();
        this.unequip(save, prog, slot);
      };
      card.appendChild(unequipBtn);
      // 点击卡片也可换装
      card.onclick = () => this.showEquipPopup(save, def, prog, slot);
    } else {
      const head = h('div', 'slot-head');
      head.append(tag, h('span', 'slot-empty-tip', '点击装备'));
      card.appendChild(head);
      card.onclick = () => this.showEquipPopup(save, def, prog, slot);
    }
    return card;
  }

  // ---------- 装备选择弹窗 ----------
  private showEquipPopup(
    save: SaveData, def: CharacterDef, prog: CharacterProgress, slot: EquipSlot,
  ): void {
    // 候选 = 仓库中该部位且可用的装备
    const candidates = save.inventory
      .map(id => getEquipment(id))
      .filter((e): e is EquipmentDef => !!e && e.slot === slot && canEquip(e, def));

    const overlay = h('div', 'modal-overlay');
    const box = h('div', 'modal-box equip-popup');
    const head = h('div', 'popup-head');
    head.appendChild(h('span', 'popup-title', `选择${SLOT_NAMES[slot]}（${def.name}）`));
    const closeBtn = h('button', 'btn-mini', '关闭');
    closeBtn.onclick = () => overlay.remove();
    head.appendChild(closeBtn);
    box.appendChild(head);

    if (candidates.length === 0) {
      box.appendChild(h('div', 'empty-tip', '无可用装备'));
    } else {
      const list = h('div', 'equip-candidates');
      for (const eq of candidates) {
        const row = h('div', 'equip-candidate');
        const bar = h('div', 'cand-bar');
        bar.style.background = RARITY_COLORS[eq.rarity];
        row.appendChild(bar);
        const body = h('div', 'cand-body');
        const nameEl = h('span', 'cand-name', eq.name);
        nameEl.style.color = RARITY_COLORS[eq.rarity];
        body.appendChild(nameEl);
        const bonus = equipBonusText(eq);
        if (bonus) body.appendChild(h('div', 'cand-bonus', bonus));
        if (eq.effectDesc) body.appendChild(h('div', 'cand-effect', eq.effectDesc));
        row.appendChild(body);
        row.onclick = () => {
          this.equip(save, prog, slot, eq.id);
          overlay.remove();
        };
        list.appendChild(row);
      }
      box.appendChild(list);
    }

    overlay.appendChild(box);
    overlay.addEventListener('click', e => { if (e.target === overlay) overlay.remove(); });
    document.body.appendChild(overlay);
  }

  // ---------- 装备操作（即改即存） ----------
  private equip(save: SaveData, prog: CharacterProgress, slot: EquipSlot, itemId: string): void {
    const old = prog.equipment[slot];
    if (old) save.inventory.push(old); // 换下的进仓库
    prog.equipment[slot] = itemId;
    const idx = save.inventory.indexOf(itemId);
    if (idx >= 0) save.inventory.splice(idx, 1);
    persist(save);
    this.renderDetail();
  }

  private unequip(save: SaveData, prog: CharacterProgress, slot: EquipSlot): void {
    const old = prog.equipment[slot];
    if (!old) return;
    delete prog.equipment[slot];
    save.inventory.push(old);
    persist(save);
    this.renderDetail();
  }

  private equippedDefs(prog: CharacterProgress): EquipmentDef[] {
    return SLOT_ORDER
      .map(s => prog.equipment[s])
      .filter((id): id is string => !!id)
      .map(id => getEquipment(id))
      .filter((e): e is EquipmentDef => !!e);
  }

  destroy(): void {}
}
