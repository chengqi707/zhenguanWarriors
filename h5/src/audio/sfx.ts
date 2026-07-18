// ============================================================
// 一次性音效——全部用 engine 的振荡器/噪声原语程序生成。
// 战斗代理接入只需：import { Sfx } from '../audio'; Sfx.play('attack')。
// win/lose 与结算 BGM 共用 themes.ts 里的短句数据。
// ============================================================
import {
  audioNow, isSfxOn, noiseBurst, sfxDest, startTheme, voice,
} from './engine';
import { RESULTS_LOSE, RESULTS_WIN, freqOf } from './themes';

export type SfxName =
  | 'attack' | 'crit' | 'skill' | 'heal' | 'die'
  | 'move' | 'win' | 'lose' | 'click';

export const Sfx = {
  play(name: SfxName): void {
    if (!isSfxOn()) return;
    const dest = sfxDest();
    const t = audioNow() + 0.01;
    switch (name) {
      case 'attack': {
        // 短噪声爆发 + 音高下坠（挥砍）
        noiseBurst({ t, dur: 0.08, vel: 0.6, filter: { type: 'bandpass', freq: 1400, q: 0.7 }, dest });
        voice({
          type: 'sawtooth', freq: 320, freqEnd: 70, t, dur: 0.16, vel: 0.4,
          attack: 0.003, release: 0.1,
          filter: { type: 'lowpass', freq: 1800 }, dest,
        });
        break;
      }
      case 'crit': {
        // 更大动态 + 金属泛音
        noiseBurst({ t, dur: 0.12, vel: 0.85, filter: { type: 'bandpass', freq: 2200, q: 0.6 }, dest });
        voice({
          type: 'square', freq: 520, freqEnd: 110, t, dur: 0.24, vel: 0.35,
          attack: 0.002, release: 0.16,
          filter: { type: 'lowpass', freq: 2600 }, dest,
        });
        [1568, 2349, 3136].forEach((f, i) => voice({
          type: 'square', freq: f * (1 + i * 0.004), t, dur: 0.1 + i * 0.03, vel: 0.09,
          attack: 0.002, release: 0.1,
          filter: { type: 'highpass', freq: 2800 }, dest,
        }));
        break;
      }
      case 'skill': {
        // 上行琶音微光（宫调五声 C-E-G-C，正弦+高八度微光）
        ['C5', 'E5', 'G5', 'C6'].forEach((n, i) => {
          voice({ type: 'sine', freq: freqOf(n), t: t + i * 0.07, dur: 0.32, vel: 0.26, attack: 0.01, release: 0.28, dest });
          voice({ type: 'triangle', freq: freqOf(n) * 2, t: t + i * 0.07, dur: 0.2, vel: 0.06, attack: 0.01, release: 0.18, dest });
        });
        break;
      }
      case 'heal': {
        // 柔和上行三音
        ['E4', 'G4', 'C5'].forEach((n, i) => voice({
          type: 'sine', freq: freqOf(n), t: t + i * 0.12, dur: 0.5, vel: 0.24,
          attack: 0.08, release: 0.4, dest,
        }));
        break;
      }
      case 'die': {
        // 低音下坠
        voice({
          type: 'sawtooth', freq: 180, freqEnd: 38, t, dur: 0.6, vel: 0.42,
          attack: 0.01, release: 0.4,
          filter: { type: 'lowpass', freq: 600 }, dest,
        });
        noiseBurst({ t, dur: 0.2, vel: 0.16, filter: { type: 'lowpass', freq: 500 }, dest });
        break;
      }
      case 'move': {
        // 轻快步点（两连短音）
        voice({ type: 'triangle', freq: 740, t, dur: 0.05, vel: 0.22, attack: 0.002, release: 0.04, dest });
        voice({ type: 'triangle', freq: 880, t: t + 0.07, dur: 0.05, vel: 0.18, attack: 0.002, release: 0.04, dest });
        break;
      }
      case 'click': {
        // 轻"嗒"
        voice({
          type: 'square', freq: 1100, freqEnd: 700, t, dur: 0.035, vel: 0.16,
          attack: 0.001, release: 0.03,
          filter: { type: 'lowpass', freq: 3200 }, dest,
        });
        noiseBurst({ t, dur: 0.015, vel: 0.08, filter: { type: 'bandpass', freq: 3000, q: 1 }, dest });
        break;
      }
      case 'win':
        startTheme(RESULTS_WIN, dest, false);
        break;
      case 'lose':
        startTheme(RESULTS_LOSE, dest, false);
        break;
    }
  },
};

// ---------- 全局按钮点击音效（事件委托，无需改各页面） ----------
let clickBound = false;

/** 在 document 上委托一层：任意 <button>/.btn/.btn-mini 点击播放 click */
export function bindGlobalClickSfx(): void {
  if (clickBound) return;
  clickBound = true;
  document.addEventListener('click', e => {
    const el = e.target as HTMLElement | null;
    if (el?.closest?.('button, .btn, .btn-mini')) Sfx.play('click');
  });
}
