// ============================================================
// 剧情播放页——说话人名 + 打字机（约 40ms/字）；
// 点击 = 当行补全 / 下一行；右上角「跳过」；播完自动进入下一状态。
// ============================================================
import type { StoryScene } from '../core/types';
import { getCharacter } from '../data';
import type { Game } from './game';
import { h, type Page } from './common';
import { getPortraitURL } from './portraits';

const TYPE_MS = 40; // 打字机间隔

export class StoryPage implements Page {
  private lineIdx = 0;
  private timer: number | null = null;
  private speakerEl: HTMLElement | null = null;
  private textEl: HTMLElement | null = null;
  private portraitEl: HTMLElement | null = null;
  private finished = false;

  constructor(
    _game: Game, // 保留统一签名，页面无需直接引用
    private scene: StoryScene,
    private onDone: () => void,
  ) {}

  render(root: HTMLElement): void {
    const page = h('div', 'page story-page');

    const skipBtn = h('button', 'story-skip', '跳过 »');
    skipBtn.onclick = e => {
      e.stopPropagation();
      this.finish();
    };
    page.appendChild(skipBtn);

    // 底部对话框
    const box = h('div', 'story-box');
    const head = h('div', 'story-head');
    this.portraitEl = h('div', 'story-portrait');
    this.speakerEl = h('div', 'story-speaker');
    head.append(this.portraitEl, this.speakerEl);
    this.textEl = h('div', 'story-text');
    box.append(head, this.textEl, h('div', 'story-hint', '▼ 点击继续'));
    page.appendChild(box);

    // 点击任意处（除跳过按钮）：补全当行 / 下一行
    page.addEventListener('click', () => this.advance());

    root.appendChild(page);
    this.playLine();
  }

  /** 播放当前行（打字机） */
  private playLine(): void {
    const line = this.scene.lines[this.lineIdx];
    if (!line) { this.finish(); return; }

    this.speakerEl!.textContent = line.speaker;
    // 头像：portrait 能匹配到角色 → 程序生成立绘（80×80 圆角）；
    // 否则保留原「姓氏首字圆块」兜底
    const def = line.portrait ? getCharacter(line.portrait) : undefined;
    const el = this.portraitEl!;
    el.replaceChildren();
    el.classList.toggle('has-img', !!def);
    el.style.background = '';
    if (def) {
      const img = h('img', 'portrait portrait-xl');
      img.src = getPortraitURL(def.id, 80);
      img.alt = def.name;
      el.appendChild(img);
    } else {
      el.textContent = line.speaker.slice(0, 1);
      el.style.background = 'var(--card)';
    }

    this.textEl!.textContent = '';
    let i = 0;
    this.timer = window.setInterval(() => {
      i += 1;
      this.textEl!.textContent = line.text.slice(0, i);
      if (i >= line.text.length) this.stopTypewriter();
    }, TYPE_MS);
  }

  private stopTypewriter(): void {
    if (this.timer !== null) {
      clearInterval(this.timer);
      this.timer = null;
    }
  }

  /** 点击：打字中→补全当行；已打完→下一行 */
  private advance(): void {
    if (this.finished) return;
    const line = this.scene.lines[this.lineIdx];
    if (!line) { this.finish(); return; }
    if (this.timer !== null) {
      // 补全当行
      this.stopTypewriter();
      this.textEl!.textContent = line.text;
      return;
    }
    this.lineIdx += 1;
    if (this.lineIdx >= this.scene.lines.length) {
      this.finish();
    } else {
      this.playLine();
    }
  }

  private finish(): void {
    if (this.finished) return;
    this.finished = true;
    this.stopTypewriter();
    this.onDone();
  }

  destroy(): void {
    this.stopTypewriter();
  }
}
