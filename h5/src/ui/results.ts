// ============================================================
// 结算页——胜利🎉/战败💀、回合数、存活/阵亡、各角色经验与升级提示；
// 胜利时显示解锁下一关与新角色；按钮：胜利=[下一关][重试][选关]，战败=[重试][选关]。
// ============================================================
import { getCharacter, getEquipment, getLevel } from '../data';
import type { Game } from './game';
import { h, type Page } from './common';

export class ResultsPage implements Page {
  constructor(private game: Game) {}

  render(root: HTMLElement): void {
    const result = this.game.result;
    if (!result) { this.game.toLevelSelect(); return; }
    const win = result.outcome === 'win';
    const summary = result.summary;

    const page = h('div', 'page');
    page.appendChild(h('div', `result-title ${win ? 'win' : 'lose'}`,
      win ? '🎉 胜利！' : '💀 战败'));
    page.appendChild(h('div', 'result-sub',
      `第${result.levelId}关 ${result.levelName} · ${result.turns} 回合`));
    page.appendChild(h('div', 'result-sub',
      `存活 ${result.alive} 人 · 阵亡 ${result.dead} 人`));

    // ---------- 胜利结算明细（中部可滚动，底部按钮固定） ----------
    const content = h('div', 'page-scroll');
    if (win && summary) {
      const panel = h('div', 'panel');
      panel.appendChild(h('div', 'panel-title', '战斗结算'));

      // 各角色经验与升级
      for (const charId of result.partyIds) {
        const exp = summary.expGains[charId] ?? 0;
        const name = getCharacter(charId)?.name ?? charId;
        const leveled = summary.levelUps.includes(charId);
        const lvAfter = this.game.save?.characters.find(c => c.id === charId)?.level;
        const row = h('div', 'result-row');
        row.appendChild(h('span', '', name));
        row.appendChild(h('span', 'result-exp',
          `经验 +${exp}${leveled ? `　⬆ 升级！Lv.${lvAfter ?? '?'}` : ''}`));
        panel.appendChild(row);
      }

      // 奖励
      if (summary.gold > 0) {
        panel.appendChild(h('div', 'result-reward', `💰 获得赏金 ${summary.gold}`));
      }
      for (const eqId of summary.equips) {
        const eq = getEquipment(eqId);
        panel.appendChild(h('div', 'result-reward', `🎁 获得装备：${eq?.name ?? eqId}`));
      }

      // 解锁下一关
      const next = result.levelId < 8 ? getLevel(result.levelId + 1) : undefined;
      if (next) {
        panel.appendChild(h('div', 'result-unlock', `📢 解锁下一关：${next.name}`));
      }
      // 新解锁角色
      for (const id of summary.newUnlocks) {
        const name = getCharacter(id)?.name ?? id;
        panel.appendChild(h('div', 'result-unlock', `🎊 新角色加入：${name}`));
      }
      if (result.levelId >= 8) {
        panel.appendChild(h('div', 'result-unlock', '🏆 恭喜通关全部八关，开创贞观盛世！'));
      }
      content.appendChild(panel);
    } else if (!win) {
      content.appendChild(h('div', 'result-tip', '胜败乃兵家常事，整军再战！'));
    }
    page.appendChild(content);

    // ---------- 按钮 ----------
    const btns = h('div', 'result-btns');
    if (win && result.levelId < 8) {
      const nextBtn = h('button', 'btn btn-gold btn-bottom', '下一关 →');
      nextBtn.onclick = () => this.game.nextLevel();
      btns.appendChild(nextBtn);
    }
    const retryBtn = h('button', 'btn btn-primary btn-bottom', '重试本关');
    retryBtn.onclick = () => this.game.retryLevel();
    const selectBtn = h('button', 'btn btn-dark btn-bottom', '返回选关');
    selectBtn.onclick = () => this.game.toLevelSelect();
    btns.append(retryBtn, selectBtn);
    page.appendChild(btns);

    root.appendChild(page);
  }

  destroy(): void {}
}
