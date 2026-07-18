// ============================================================
// 选关页——8 关卡片列表（关卡名/天气/敌人数/简介），未解锁置灰🔒；
// 顶部显示金币与当前难度。
// ============================================================
import { LEVELS } from '../data';
import type { LevelDef } from '../core/types';
import type { Game } from './game';
import {
  DIFFICULTY_NAMES, WEATHER_ICONS, WEATHER_NAMES, h, type Page,
} from './common';

/** 关卡简介：数据有 intro 用之，否则按胜利条件生成 */
function levelIntro(level: LevelDef): string {
  if (level.intro) return level.intro;
  switch (level.victory) {
    case 'defeatAll': return '全歼敌军即可获胜';
    case 'defeatBoss': return '击破敌方主将即可获胜';
    case 'defendTurns': return `坚守 ${level.defendTurns ?? '?'} 回合即可获胜`;
  }
}

export class LevelSelectPage implements Page {
  constructor(private game: Game) {}

  render(root: HTMLElement): void {
    const save = this.game.save;
    if (!save) { this.game.toMainMenu(); return; }

    const page = h('div', 'page page-compact');
    page.appendChild(h('div', 'page-title', '🗺 选择关卡'));

    // 顶部状态条：金币 + 当前难度
    const bar = h('div', 'status-bar');
    bar.appendChild(h('span', 'status-item', `💰 金币 ${save.gold}`));
    bar.appendChild(h('span', 'status-item', `难度：${DIFFICULTY_NAMES[save.difficulty]}`));
    page.appendChild(bar);

    const list = h('div', 'level-list');
    for (const level of LEVELS) {
      const unlocked = level.id <= save.currentLevel;
      const completed = !!save.levelStates[level.id]?.completed;
      const card = h('div', `level-card${unlocked ? '' : ' locked'}`);

      const head = h('div', 'level-head');
      // 序幕关（id=0）显示「序幕」而非「第0关」
      const levelNo = level.id === 0 ? '序幕' : `第${level.id}关`;
      head.appendChild(h('span', 'level-name', `${levelNo} ${level.name}`));
      head.appendChild(h('span', 'level-weather',
        `${WEATHER_ICONS[level.weather]} ${WEATHER_NAMES[level.weather]}`));
      card.appendChild(head);

      const info = h('div', 'level-info',
        unlocked
          ? `敌方 ${level.enemies.length} 人 · ${levelIntro(level)}`
          : '通关上一关后解锁');
      card.appendChild(info);

      if (!unlocked) {
        card.appendChild(h('div', 'level-lock', '🔒'));
      } else if (completed) {
        card.appendChild(h('div', 'level-done', '✓ 已通关'));
      }

      if (unlocked) {
        card.onclick = () => this.game.startLevel(level.id);
      }
      list.appendChild(card);
    }
    page.appendChild(list);

    const backBtn = h('button', 'btn btn-dark btn-bottom', '← 返回主菜单');
    backBtn.onclick = () => this.game.toMainMenu();
    page.appendChild(backBtn);

    root.appendChild(page);
  }

  destroy(): void {}
}
