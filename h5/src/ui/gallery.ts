// ============================================================
// 角色图鉴——15 名角色卡片网格（Q版立绘 + 定位/兵种），
// 点击卡片弹出详情：五维、HP/MP、移动/射程、计策、被动、解锁关卡。
// 立绘走 portraits.ts 缓存层（外部 PNG 优先，无图回退程序绘制）。
// ============================================================
import { CHARACTERS, getPassive, getSkill } from '../data';
import type { CharacterDef, Role } from '../core/types';
import type { Game } from './game';
import {
  CLASS_COLORS, CLASS_NAMES, h, statsLine, type Page,
} from './common';
import { getPortraitURL } from './portraits';

const ROLE_NAMES: Record<Role, string> = {
  monarch: '君主', warrior: '武将', strategist: '谋士', female: '巾帼',
};

export class GalleryPage implements Page {
  constructor(private game: Game) {}

  render(root: HTMLElement): void {
    const page = h('div', 'page');
    page.appendChild(h('div', 'page-title', '📜 角色图鉴'));

    const content = h('div', 'page-scroll');
    const grid = h('div', 'gallery-grid');
    for (const def of CHARACTERS) {
      grid.appendChild(this.card(def));
    }
    content.appendChild(grid);
    page.appendChild(content);

    const backBtn = h('button', 'btn btn-dark btn-bottom', '← 返回主菜单');
    backBtn.onclick = () => this.game.toMainMenu();
    page.appendChild(backBtn);

    root.appendChild(page);
  }

  /** 角色卡片：立绘 + 姓名 + 定位 + 兵种 */
  private card(def: CharacterDef): HTMLElement {
    const card = h('button', 'gallery-card');

    const face = document.createElement('img');
    face.className = 'gallery-face';
    face.src = getPortraitURL(def.id, 96);
    face.alt = def.name;
    face.draggable = false;

    const name = h('div', 'gallery-name', def.name);
    const pos = h('div', 'gallery-pos', def.pos);

    const cls = h('span', 'gallery-class', CLASS_NAMES[def.classType]);
    cls.style.background = CLASS_COLORS[def.classType];

    card.append(face, name, pos, cls);
    card.onclick = () => this.showDetail(def);
    return card;
  }

  /** 详情弹窗：大立绘 + 属性 + 计策 + 被动 */
  private showDetail(def: CharacterDef): void {
    const overlay = h('div', 'modal-overlay');
    const box = h('div', 'modal-box gallery-modal');

    // 头部：大立绘 + 姓名/定位/兵种
    const head = h('div', 'gallery-detail-head');
    const face = document.createElement('img');
    face.className = 'gallery-detail-face';
    face.src = getPortraitURL(def.id, 160);
    face.alt = def.name;
    face.draggable = false;

    const titleBox = h('div', 'gallery-detail-title');
    titleBox.appendChild(h('div', 'gallery-detail-name', def.name));
    const tags = h('div', 'gallery-detail-tags');
    const cls = h('span', 'gallery-class', CLASS_NAMES[def.classType]);
    cls.style.background = CLASS_COLORS[def.classType];
    tags.append(cls, h('span', 'gallery-tag', ROLE_NAMES[def.role]), h('span', 'gallery-tag', def.pos));
    titleBox.appendChild(tags);
    titleBox.appendChild(h('div', 'gallery-unlock',
      def.unlockLevel === 0 ? '初始即可出战' : `通关第 ${def.unlockLevel} 关解锁`));
    head.append(face, titleBox);
    box.appendChild(head);

    // 属性：五维 + HP/MP/移动/射程
    const statsPanel = h('div', 'gallery-section');
    statsPanel.appendChild(h('div', 'gallery-section-title', '属性（LV1）'));
    statsPanel.appendChild(h('div', 'gallery-stats', statsLine(def.base)));
    statsPanel.appendChild(h('div', 'gallery-sub',
      `HP ${def.hp}　MP ${def.mp}　移动 ${def.move}　射程 ${def.range}`));
    box.appendChild(statsPanel);

    // 计策
    if (def.skills.length > 0) {
      const skillPanel = h('div', 'gallery-section');
      skillPanel.appendChild(h('div', 'gallery-section-title', '计策'));
      for (const sid of def.skills) {
        const sk = getSkill(sid);
        if (!sk) continue;
        const row = h('div', 'gallery-skill');
        row.appendChild(h('span', 'gallery-skill-name', `${sk.name}`));
        row.appendChild(h('span', 'gallery-skill-desc', sk.desc));
        skillPanel.appendChild(row);
      }
      box.appendChild(skillPanel);
    }

    // 被动
    const ps = getPassive(def.passive);
    if (ps) {
      const psPanel = h('div', 'gallery-section');
      psPanel.appendChild(h('div', 'gallery-section-title', '被动'));
      const row = h('div', 'gallery-skill');
      row.appendChild(h('span', 'gallery-skill-name', ps.name));
      row.appendChild(h('span', 'gallery-skill-desc', ps.desc));
      psPanel.appendChild(row);
      box.appendChild(psPanel);
    }

    const closeBtn = h('button', 'btn btn-dark gallery-close', '关闭');
    const close = () => overlay.remove();
    closeBtn.onclick = close;
    box.appendChild(closeBtn);

    overlay.appendChild(box);
    overlay.addEventListener('click', e => { if (e.target === overlay) close(); });
    document.body.appendChild(overlay);
  }

  destroy(): void {
    // 详情弹窗挂在 body 上，切页时兜底移除
    document.querySelectorAll('.modal-overlay').forEach(el => el.remove());
  }
}
