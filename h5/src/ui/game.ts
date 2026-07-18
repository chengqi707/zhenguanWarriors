// ============================================================
// 页面状态机——任何时刻只显示一个页面。
// 流程：Splash → MainMenu → LevelSelect → Story(pre) → HeroSelect
//   → EquipSetup → Battle → Story(post) → Results →（下一关 pre 剧情/重试/选关）；
// Settings 从 MainMenu 进出。
// ============================================================
import type { BattleOutcome, Difficulty, SaveData } from '../core/types';
import type { Battle } from '../core/battle';
import {
  completeLevel, load, newGame, save as persist, type LevelClearSummary,
} from '../core/save';
import { getLevel, getStory } from '../data';
import type { Page } from './common';
import { Bgm, bindGlobalClickSfx } from '../audio';
import { SplashPage } from './splash';
import { MainMenuPage } from './mainMenu';
import { SettingsPage } from './settings';
import { GalleryPage } from './gallery';
import { LevelSelectPage } from './levelSelect';
import { StoryPage } from './story';
import { HeroSelectPage } from './heroSelect';
import { EquipSetupPage } from './equipSetup';
import { BattlePage } from './battlePage';
import { ResultsPage } from './results';

/** 一次战斗的结算上下文（Results 页展示用） */
export interface BattleResultInfo {
  outcome: BattleOutcome;
  levelId: number;
  levelName: string;
  turns: number;          // 战斗回合数
  alive: number;          // 存活人数
  dead: number;           // 阵亡人数
  partyIds: string[];     // 出战角色
  summary: LevelClearSummary | null; // 胜利时的经验/升级/解锁结算
}

export class Game {
  save: SaveData | null = null;
  levelId = 1;                    // 当前攻关关卡
  partyIds: string[] = [];        // 已选出战角色
  result: BattleResultInfo | null = null;
  /** 无存档时在设置页选的难度，新游戏时应用 */
  pendingDifficulty: Difficulty = 'normal';

  private current: Page | null = null;

  constructor(private root: HTMLElement) {
    // 全局按钮点击音效（document 委托，各页面无需改动）
    bindGlobalClickSfx();
  }

  start(): void {
    this.setPage(new SplashPage(this));
  }

  /** 路由：销毁旧页 → 清空容器 → 渲染新页 */
  setPage(page: Page): void {
    this.current?.destroy();
    this.current = null;
    this.root.replaceChildren();
    page.render(this.root);
    this.current = page;
    this.updateBgm(page);
  }

  /** 按页面切换 BGM：菜单类页面=menu，战斗=battle，结算=results（按胜负选短句），Splash 随菜单淡入 */
  private updateBgm(page: Page): void {
    if (page instanceof BattlePage) {
      Bgm.play('battle');
    } else if (page instanceof ResultsPage) {
      Bgm.play('results', { variant: this.result?.outcome === 'lose' ? 'lose' : 'win' });
    } else {
      Bgm.play('menu');
    }
  }

  // ---------- 主菜单 ----------
  toMainMenu(): void {
    this.save = load(); // 回主菜单时刷新存档快照
    this.setPage(new MainMenuPage(this));
  }

  startNewGame(): void {
    this.save = newGame();
    // 应用设置页里预先选好的难度
    this.save.difficulty = this.pendingDifficulty;
    persist(this.save);
    this.toLevelSelect();
  }

  continueGame(): void {
    this.save = load();
    if (!this.save) { this.toMainMenu(); return; }
    this.toLevelSelect();
  }

  toSettings(): void {
    this.save = this.save ?? load();
    this.setPage(new SettingsPage(this));
  }

  /** 角色图鉴：纯展示页，无需存档 */
  toGallery(): void {
    this.setPage(new GalleryPage(this));
  }

  // ---------- 选关与关卡流程 ----------
  toLevelSelect(): void {
    this.save = this.save ?? load();
    if (!this.save) { this.toMainMenu(); return; }
    this.setPage(new LevelSelectPage(this));
  }

  /** 进入某关：先播关前剧情（无数据则跳过），再选人 */
  startLevel(levelId: number): void {
    this.levelId = levelId;
    this.partyIds = [];
    const story = getStory(`story_${levelId}_pre`);
    if (story && story.lines.length > 0) {
      this.setPage(new StoryPage(this, story, () => this.toHeroSelect()));
    } else {
      this.toHeroSelect();
    }
  }

  toHeroSelect(): void {
    this.setPage(new HeroSelectPage(this));
  }

  toEquipSetup(): void {
    this.setPage(new EquipSetupPage(this));
  }

  toBattle(): void {
    this.setPage(new BattlePage(this));
  }

  // ---------- 战斗结束 ----------
  /** BattleScene onFinish 回调：胜利→completeLevel 结算→关后剧情→结算页；失败→结算页 */
  handleBattleFinish(outcome: BattleOutcome, battle: Battle): void {
    const level = getLevel(this.levelId);
    const players = battle.state.units.filter(u => u.faction === 'player');
    this.result = {
      outcome,
      levelId: this.levelId,
      levelName: level?.name ?? `第${this.levelId}关`,
      turns: battle.state.turn,
      alive: players.filter(u => u.alive).length,
      dead: players.filter(u => !u.alive).length,
      partyIds: [...this.partyIds],
      summary: null,
    };

    if (this.save) {
      // 战斗物品消耗回写（无论胜负）
      this.save.items = battle.getItemCounts();
      // 教学关打完（无论胜负）标记教学完成
      if (this.levelId === 1 && !this.save.tutorialDone) {
        this.save.tutorialDone = true;
      }
      if (outcome === 'win') {
        // completeLevel 内部会落盘（含上面的 tutorialDone）
        this.result.summary = completeLevel(
          this.save, this.levelId,
          battle.getKillCounts(),
          battle.getParticipated(),
          battle.getPartyCharIds(),
          battle.getBossKillers(),
        );
      } else {
        persist(this.save);
      }
    }

    if (outcome === 'win') {
      const story = getStory(`story_${this.levelId}_post`);
      if (story && story.lines.length > 0) {
        this.setPage(new StoryPage(this, story, () => this.toResults()));
        return;
      }
    }
    this.toResults();
  }

  toResults(): void {
    this.setPage(new ResultsPage(this));
  }

  /** 下一关：直接进下一关关前剧情（数据缺失则自动跳过剧情环节） */
  nextLevel(): void {
    if (!this.result) { this.toLevelSelect(); return; }
    if (this.result.levelId >= 8) { this.toLevelSelect(); return; }
    this.startLevel(this.result.levelId + 1);
  }

  /** 重试：重新本关（从关前剧情开始，可跳过） */
  retryLevel(): void {
    if (!this.result) { this.toLevelSelect(); return; }
    this.startLevel(this.result.levelId);
  }
}
