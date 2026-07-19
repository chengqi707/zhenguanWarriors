// ============================================================
// 主菜单——新游戏（有存档时二次确认清档）/ 继续游戏（无存档置灰）/ 设置。
// ============================================================
import { APP_VERSION } from '../data/version';
import { hasSave } from '../core/save';
import type { Game } from './game';
import { h, showConfirm, type Page } from './common';

export class MainMenuPage implements Page {
  constructor(private game: Game) {}

  render(root: HTMLElement): void {
    const page = h('div', 'page menu-page');

    const head = h('div', 'menu-head');
    head.appendChild(h('div', 'menu-title', '🏯 贞观勇士'));
    head.appendChild(h('div', 'menu-sub', '李世民战棋录'));
    page.appendChild(head);

    const btns = h('div', 'menu-btns');

    const newBtn = h('button', 'btn btn-primary btn-big', '新游戏');
    newBtn.onclick = () => {
      // 已有存档时弹确认：清档重开
      if (hasSave()) {
        showConfirm({
          text: '已有游戏存档，开始新游戏将清除当前进度，确定吗？',
          okText: '清档重开',
          danger: true,
          onOk: () => this.game.startNewGame(),
        });
      } else {
        this.game.startNewGame();
      }
    };

    const contBtn = h('button', 'btn btn-primary btn-big', '继续游戏');
    if (!hasSave()) {
      contBtn.disabled = true;
      contBtn.title = '暂无存档';
    } else {
      contBtn.onclick = () => this.game.continueGame();
    }

    const galleryBtn = h('button', 'btn btn-dark btn-big', '角色图鉴');
    galleryBtn.onclick = () => this.game.toGallery();

    const setBtn = h('button', 'btn btn-dark btn-big', '设置');
    setBtn.onclick = () => this.game.toSettings();

    btns.append(newBtn, contBtn, galleryBtn, setBtn);
    page.appendChild(btns);
    page.appendChild(h('div', 'menu-ver', APP_VERSION));
    root.appendChild(page);
  }

  destroy(): void {
    // 无定时器/全局监听，DOM 由状态机统一清除
  }
}
