// ============================================================
// 选人页——双列网格角色卡片（48px 立绘+名字+pos 徽章+等级+兵种+
// 五维精简一行+勾选框），点击勾选/取消；必出角色锁定，最多 8 人；
// 羁绊面板默认折叠为一行摘要（点击展开明细）；底部固定操作栏。
// ============================================================
import { BONDS, getCharacter, getLevel } from '../data';
import type { Game } from './game';
import {
  CLASS_COLORS, CLASS_NAMES, STAT_KEYS, STAT_NAMES, h, statsAtLevel, type Page,
} from './common';
import { getPortraitURL } from './portraits';

const MAX_PARTY = 8; // 出战上限

export class HeroSelectPage implements Page {
  private selected = new Set<string>();
  private bondExpanded = false; // 羁绊面板默认折叠

  constructor(private game: Game) {}

  render(root: HTMLElement): void {
    const save = this.game.save;
    const level = getLevel(this.game.levelId);
    if (!save || !level) { this.game.toLevelSelect(); return; }

    // 可用且已解锁的角色
    const candidates = level.available.filter(id =>
      save.characters.some(c => c.id === id && c.isUnlocked));

    // 初始勾选：沿用上次已选阵容（配装页返回时）；否则必出 + 候选前序补足至上限
    if (this.game.partyIds.length > 0) {
      for (const id of this.game.partyIds) {
        if (candidates.includes(id)) this.selected.add(id);
      }
    } else {
      for (const id of level.required) {
        if (candidates.includes(id)) this.selected.add(id);
      }
      for (const id of candidates) {
        if (this.selected.size >= MAX_PARTY) break;
        this.selected.add(id);
      }
    }

    const page = h('div', 'page page-compact');

    // 标题栏：已选计数实时刷新
    const titleEl = h('div', 'page-title');
    const updateTitle = () => {
      titleEl.textContent = `👥 选择出战武将 ${this.selected.size}/${MAX_PARTY}`;
    };
    updateTitle();
    page.appendChild(titleEl);

    // ---------- 角色双列网格 ----------
    const listEl = h('div', 'hero-grid');
    const bondEl = h('div', 'bond-panel');
    const confirmBtn = h('button', 'btn btn-gold btn-bottom', '确认阵容 →');
    const backBtn = h('button', 'btn btn-dark btn-bottom-half', '← 返回选关');

    const renderList = () => {
      listEl.replaceChildren();
      const full = this.selected.size >= MAX_PARTY;
      for (const id of candidates) {
        const def = getCharacter(id)!;
        const prog = save.characters.find(c => c.id === id)!;
        const isRequired = level.required.includes(id);
        const isOn = this.selected.has(id);
        const disabled = !isOn && full; // 满员后未选不可勾

        const card = h('div',
          `hero-card${isOn ? ' on' : ''}${disabled ? ' disabled' : ''}`);

        // 程序生成立绘（48×48 圆形）
        const face = h('img', 'portrait portrait-md');
        face.src = getPortraitURL(id, 48);
        face.alt = def.name;
        card.appendChild(face);

        const body = h('div', 'hero-body');
        const line1 = h('div', 'hero-line1');
        line1.appendChild(h('span', 'hero-name', def.name));
        line1.appendChild(h('span', 'pos-badge', def.pos)); // 定位徽章（名字旁）
        body.appendChild(line1);

        const line2 = h('div', 'hero-line2');
        line2.appendChild(h('span', 'hero-lv', `Lv.${prog.level}`));
        const cls = h('span', 'hero-class', CLASS_NAMES[def.classType]);
        cls.style.color = CLASS_COLORS[def.classType];
        line2.appendChild(cls);
        if (isRequired) line2.appendChild(h('span', 'hero-required', '必出'));
        body.appendChild(line2);

        // 五维精简一行（独占一行全宽，高等级三位数也能放下）
        const s = statsAtLevel(def, prog.level);
        const statsEl = h('div', 'hero-stats',
          STAT_KEYS.map(k => `${STAT_NAMES[k]}${s[k]}`).join(' '));
        card.append(body, statsEl);

        card.appendChild(h('div', 'hero-check',
          isRequired ? '🔒' : isOn ? '✅' : disabled ? '—' : '☐'));

        if (!isRequired && !disabled) {
          card.onclick = () => {
            if (this.selected.has(id)) this.selected.delete(id);
            else this.selected.add(id);
            refresh();
          };
        }
        listEl.appendChild(card);
      }
      if (candidates.length === 0) {
        listEl.appendChild(h('div', 'empty-tip', '可用角色不足'));
      }
    };

    // ---------- 羁绊面板（默认折叠为一行摘要，点击展开明细） ----------
    const renderBonds = () => {
      bondEl.replaceChildren();
      const rows: Array<{ active: boolean; text: string }> = [];
      for (const bond of BONDS) {
        const inParty = bond.members.filter(m => this.selected.has(m));
        if (inParty.length === 0) continue; // 无人沾边不显示
        if (inParty.length >= bond.minCount) {
          rows.push({ active: true, text: `👑 ${bond.name} 已激活：${bond.desc}` });
        } else {
          const missing = bond.members
            .filter(m => !this.selected.has(m))
            .map(m => getCharacter(m)?.name ?? m)
            .join('、');
          rows.push({ active: false, text: `⚔ ${bond.name} 还需 ${missing}：${bond.desc}` });
        }
      }
      if (rows.length === 0) {
        bondEl.appendChild(h('div', 'bond-summary muted', '✦ 当前阵容未触发羁绊'));
        return;
      }
      const activeCount = rows.filter(r => r.active).length;
      const summary = h('button', 'bond-summary',
        `✦ 已激活 ${activeCount} 组羁绊 ${this.bondExpanded ? '▲' : '▼'}`);
      summary.onclick = () => {
        this.bondExpanded = !this.bondExpanded;
        renderBonds();
      };
      bondEl.appendChild(summary);
      if (this.bondExpanded) {
        for (const r of rows) {
          bondEl.appendChild(h('div', `bond-row${r.active ? ' active' : ''}`, r.text));
        }
      }
    };

    const refresh = () => {
      updateTitle();
      renderList();
      renderBonds();
      // 未满必出或 0 人时禁用确认
      const requiredOk = level.required.every(id => this.selected.has(id));
      confirmBtn.disabled = this.selected.size === 0 || !requiredOk;
    };

    renderList();
    renderBonds();
    refresh();

    confirmBtn.onclick = () => {
      this.game.partyIds = [...this.selected];
      this.game.toEquipSetup();
    };
    backBtn.onclick = () => this.game.toLevelSelect();

    // 底部固定操作栏
    const bottomBar = h('div', 'bottom-bar');
    bottomBar.append(backBtn, confirmBtn);

    page.append(listEl, bondEl, bottomBar);
    root.appendChild(page);
  }

  destroy(): void {}
}
