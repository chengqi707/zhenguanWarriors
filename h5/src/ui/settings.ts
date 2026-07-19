// ============================================================
// 设置页——难度 4 档（radio + 各档修正说明，即时写入存档）/
// 清空存档（二次确认）/ 关于。无存档时难度先记在游戏上下文中，新游戏时应用。
// ============================================================
import { APP_VERSION } from '../data/version';
import { isLandscapeMode, setLandscapeMode } from '../core/settings';
import { clear, save as persist } from '../core/save';
import { DIFFICULTY_DESC } from '../data';
import type { Difficulty } from '../core/types';
import type { Game } from './game';
import {
  isMusicOn, isSfxOn, setMusicOn, setSfxOn,
} from '../audio';
import {
  DIFFICULTY_NAMES, DIFFICULTY_ORDER, h, showConfirm, type Page,
} from './common';

export class SettingsPage implements Page {
  constructor(private game: Game) {}

  render(root: HTMLElement): void {
    const page = h('div', 'page');
    page.appendChild(h('div', 'page-title', '⚙ 设置'));

    // 中部内容可滚动（底部返回键固定）
    const content = h('div', 'page-scroll');

    // ---------- 难度 ----------
    const diffPanel = h('div', 'panel');
    diffPanel.appendChild(h('div', 'panel-title', '难度'));
    const current = this.game.save?.difficulty ?? this.game.pendingDifficulty;
    for (const d of DIFFICULTY_ORDER) {
      const label = h('label', 'radio-row');
      const radio = document.createElement('input');
      radio.type = 'radio';
      radio.name = 'difficulty';
      radio.checked = d === current;
      radio.onchange = () => this.setDifficulty(d);
      const text = h('div', 'radio-text');
      text.appendChild(h('span', 'radio-label', DIFFICULTY_NAMES[d]));
      // 该档修正说明（由 DIFFICULTY_MOD 数值生成）
      text.appendChild(h('span', 'diff-desc', DIFFICULTY_DESC[d]));
      label.append(radio, text);
      diffPanel.appendChild(label);
    }
    content.appendChild(diffPanel);

    // ---------- 音频 ----------
    const audioPanel = h('div', 'panel');
    audioPanel.appendChild(h('div', 'panel-title', '音频'));
    for (const item of [
      { label: '音乐', get: isMusicOn, set: setMusicOn },
      { label: '音效', get: isSfxOn, set: setSfxOn },
    ]) {
      const row = h('label', 'check-row');
      const cb = document.createElement('input');
      cb.type = 'checkbox';
      cb.checked = item.get();
      cb.onchange = () => item.set(cb.checked); // 即读即写 localStorage
      row.append(cb, h('span', 'check-label', item.label));
      audioPanel.appendChild(row);
    }
    content.appendChild(audioPanel);

    // ---------- 画面 ----------
    const viewPanel = h('div', 'panel');
    viewPanel.appendChild(h('div', 'panel-title', '画面'));
    const landscapeRow = h('label', 'check-row');
    const landscapeCb = document.createElement('input');
    landscapeCb.type = 'checkbox';
    landscapeCb.checked = isLandscapeMode();
    landscapeCb.onchange = () => setLandscapeMode(landscapeCb.checked);
    landscapeRow.append(landscapeCb, h('span', 'check-label', 'PC 横屏模式（战斗页右侧操作栏）'));
    viewPanel.appendChild(landscapeRow);
    content.appendChild(viewPanel);

    // ---------- 存档管理 ----------
    const savePanel = h('div', 'panel');
    savePanel.appendChild(h('div', 'panel-title', '存档管理'));
    const clearBtn = h('button', 'btn btn-primary', '清空存档');
    clearBtn.onclick = () => {
      // 二次确认
      showConfirm({
        text: '确定要清空存档吗？所有关卡进度、角色等级与装备都将丢失。',
        okText: '确定清空',
        danger: true,
        onOk: () => showConfirm({
          text: '该操作不可恢复，请再次确认。',
          okText: '我已知晓，清空',
          danger: true,
          onOk: () => {
            clear();
            this.game.save = null;
            this.game.toMainMenu();
          },
        }),
      });
    };
    savePanel.appendChild(clearBtn);
    content.appendChild(savePanel);

    // ---------- 关于 ----------
    const aboutPanel = h('div', 'panel');
    aboutPanel.appendChild(h('div', 'panel-title', '关于'));
    aboutPanel.appendChild(
      h('p', 'about-text', `《贞观勇士》——以李世民开国征程为主线的六边形战棋小游戏。版本 ${APP_VERSION}`),
    );
    content.appendChild(aboutPanel);

    page.appendChild(content);

    const backBtn = h('button', 'btn btn-dark btn-bottom', '← 返回主菜单');
    backBtn.onclick = () => this.game.toMainMenu();
    page.appendChild(backBtn);

    root.appendChild(page);
  }

  /** 难度即时写入存档；无存档时暂存，新游戏时应用 */
  private setDifficulty(d: Difficulty): void {
    if (this.game.save) {
      this.game.save.difficulty = d;
      persist(this.game.save);
    }
    this.game.pendingDifficulty = d;
  }

  destroy(): void {}
}
