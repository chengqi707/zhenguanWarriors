// ============================================================
// 战斗页——把容器交给战斗层 BattleScene（另一代理实现），
// 战斗结束后回调 game.handleBattleFinish 进入结算流程。
// ============================================================
import { BattleScene } from '../battle/battleScene';
import { getLevel } from '../data';
import type { Battle } from '../core/battle';
import type { BattleOutcome, PartyMember } from '../core/types';
import type { Game } from './game';
import { h, type Page } from './common';

export class BattlePage implements Page {
  private scene: BattleScene | null = null;

  constructor(private game: Game) {}

  render(root: HTMLElement): void {
    const save = this.game.save;
    const level = getLevel(this.game.levelId);
    if (!save || !level) { this.game.toLevelSelect(); return; }

    // 出战阵容：从存档的角色进度（等级/装备）构建
    const party: PartyMember[] = this.game.partyIds.map(id => {
      const prog = save.characters.find(c => c.id === id)!;
      return { charId: id, level: prog.level, equipment: { ...prog.equipment } };
    });

    const page = h('div', 'page battle-page');
    const container = h('div', 'battle-container');
    page.appendChild(container);
    root.appendChild(page);

    this.scene = new BattleScene(container, {
      level,
      party,
      difficulty: save.difficulty,
      items: { ...save.items }, // 出战携带物品（战后按剩余回写）
      // 第 1 关且未完成过引导时开启教学提示
      tutorial: this.game.levelId === 1 && !save.tutorialDone,
      onFinish: (outcome: BattleOutcome, battle: Battle) =>
        this.game.handleBattleFinish(outcome, battle),
    });
    // 调试/自动化测试钩子（仅开发期使用）
    (window as unknown as Record<string, unknown>).__scene = this.scene;
  }

  destroy(): void {
    this.scene?.destroy();
    this.scene = null;
  }
}
