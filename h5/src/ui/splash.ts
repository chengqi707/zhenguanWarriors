// ============================================================
// 启动页——金色标题淡入，2.5s 后自动进入主菜单。
// ============================================================
import { APP_VERSION } from '../data/version';
import type { Game } from './game';
import { h, type Page } from './common';

const SPLASH_MS = 2500; // 停留总时长

export class SplashPage implements Page {
  private timer: number | null = null;

  constructor(private game: Game) {}

  render(root: HTMLElement): void {
    const page = h('div', 'page splash-page');
    const inner = h('div', 'splash-inner');
    inner.appendChild(h('div', 'splash-title', '贞观勇士'));
    inner.appendChild(h('div', 'splash-sub', '李世民战棋录'));
    inner.appendChild(h('div', 'splash-ver', APP_VERSION));
    page.appendChild(inner);
    root.appendChild(page);
    this.timer = window.setTimeout(() => this.game.toMainMenu(), SPLASH_MS);
  }

  destroy(): void {
    if (this.timer !== null) {
      clearTimeout(this.timer);
      this.timer = null;
    }
  }
}
