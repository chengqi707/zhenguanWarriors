// ============================================================
// WebAudio 引擎——懒初始化 AudioContext、自动播放策略解锁、
// 主音量总线（BGM 0.25 / SFX 0.5）、程序生成音符/噪声原语、
// 前瞻式循环调度器、Bgm 交叉淡入淡出（1s）、开关持久化。
// 不 import sfx（避免循环依赖）：Sfx 在 sfx.ts 基于本模块原语实现。
// ============================================================
import {
  BATTLE_THEME, MENU_THEME, RESULTS_LOSE, RESULTS_WIN,
  midiFreq, type NoteEvent, type Theme, type Timbre,
} from './themes';

export type BgmName = 'menu' | 'battle' | 'results';

const KEY_MUSIC = 'zg_audio_music';
const KEY_SFX = 'zg_audio_sfx';
const FADE_SEC = 1;      // BGM 交叉淡入淡出时长
const LOOKAHEAD = 0.15;  // 调度前瞻（秒）
const TICK_MS = 40;      // 调度间隔（毫秒）

// ---------- AudioContext 懒初始化 + 自动播放解锁 ----------
let ctx: AudioContext | null = null;
let musicBus: GainNode | null = null;
let sfxBus: GainNode | null = null;
let noiseBuf: AudioBuffer | null = null;
let unlockBound = false;
/** 已调度的发声节点数（调试钩子用） */
let scheduled = 0;

function bindUnlock(): void {
  if (unlockBound) return;
  unlockBound = true;
  const unlock = () => {
    if (ctx && ctx.state === 'suspended') void ctx.resume();
  };
  document.addEventListener('pointerdown', unlock);
  document.addEventListener('keydown', unlock);
}

/** 获取（必要时创建）AudioContext；首次调用时挂一次性解锁监听 */
export function ac(): AudioContext {
  if (!ctx) {
    ctx = new AudioContext();
    const master = ctx.createGain();
    master.gain.value = 1;
    master.connect(ctx.destination);
    musicBus = ctx.createGain();
    musicBus.gain.value = musicOn ? musicVol : 0;
    musicBus.connect(master);
    sfxBus = ctx.createGain();
    sfxBus.gain.value = sfxOn ? sfxVol : 0;
    sfxBus.connect(master);
    // 1 秒白噪声缓冲，各噪声音源共享
    noiseBuf = ctx.createBuffer(1, ctx.sampleRate, ctx.sampleRate);
    const data = noiseBuf.getChannelData(0);
    for (let i = 0; i < data.length; i++) data[i] = Math.random() * 2 - 1;
    bindUnlock();
    installDebugHook();
  }
  return ctx;
}

/** SFX 输出总线（sfx.ts 用） */
export function sfxDest(): GainNode {
  ac();
  return sfxBus!;
}

export function audioNow(): number {
  return ac().currentTime;
}

// ---------- 发声原语 ----------
export interface VoiceOpts {
  type: OscillatorType;
  freq: number;
  freqEnd?: number;   // 音高滑行目标（指数滑变）
  t: number;          // 起始时间（ctx 时间轴）
  dur: number;        // 时长（秒）
  vel: number;
  attack?: number;
  release?: number;
  filter?: { type: BiquadFilterType; freq: number; q?: number };
  dest: AudioNode;
}

/** 一个带包络（attack→保持→release）的振荡器音符 */
export function voice(o: VoiceOpts): void {
  const c = ac();
  const atk = Math.min(o.attack ?? 0.005, o.dur * 0.5);
  const rel = Math.min(o.release ?? 0.08, o.dur * 0.9);
  const osc = c.createOscillator();
  osc.type = o.type;
  osc.frequency.setValueAtTime(Math.max(20, o.freq), o.t);
  if (o.freqEnd) {
    osc.frequency.exponentialRampToValueAtTime(Math.max(20, o.freqEnd), o.t + o.dur);
  }
  const g = c.createGain();
  g.gain.setValueAtTime(0, o.t);
  g.gain.linearRampToValueAtTime(o.vel, o.t + atk);
  g.gain.setValueAtTime(o.vel, Math.max(o.t + atk, o.t + o.dur - rel));
  g.gain.linearRampToValueAtTime(0.0001, o.t + o.dur);
  let head: AudioNode = g;
  if (o.filter) {
    const f = c.createBiquadFilter();
    f.type = o.filter.type;
    f.frequency.value = o.filter.freq;
    if (o.filter.q) f.Q.value = o.filter.q;
    g.connect(f);
    head = f;
  }
  osc.connect(g);
  head.connect(o.dest);
  osc.start(o.t);
  osc.stop(o.t + o.dur + 0.05);
  osc.onended = () => { osc.disconnect(); g.disconnect(); head.disconnect(); };
  scheduled++;
}

/** 一段带滤波与包络的噪声（鼓点/打击音效用） */
export function noiseBurst(o: {
  t: number; dur: number; vel: number;
  filter: { type: BiquadFilterType; freq: number; q?: number };
  dest: AudioNode;
}): void {
  const c = ac();
  const src = c.createBufferSource();
  src.buffer = noiseBuf!;
  src.loop = true;
  const f = c.createBiquadFilter();
  f.type = o.filter.type;
  f.frequency.value = o.filter.freq;
  if (o.filter.q) f.Q.value = o.filter.q;
  const g = c.createGain();
  g.gain.setValueAtTime(o.vel, o.t);
  g.gain.exponentialRampToValueAtTime(0.0001, o.t + o.dur);
  src.connect(f);
  f.connect(g);
  g.connect(o.dest);
  src.start(o.t);
  src.stop(o.t + o.dur + 0.02);
  src.onended = () => { src.disconnect(); f.disconnect(); g.disconnect(); };
  scheduled++;
}

// ---------- 音色解释 ----------
function playTimbre(timbre: Timbre, ev: NoteEvent, t: number, durSec: number, dest: AudioNode): void {
  const f = midiFreq(ev.midi);
  const v = ev.vel;
  switch (timbre) {
    case 'flute': {
      // 箫/笛：三角波主体 + 高八度正弦泛音，柔和起音
      const d = Math.max(0.08, durSec * 0.92);
      voice({ type: 'triangle', freq: f, t, dur: d, vel: v * 0.5, attack: 0.06, release: 0.14, dest });
      voice({ type: 'sine', freq: f * 2, t, dur: d, vel: v * 0.13, attack: 0.06, release: 0.12, dest });
      break;
    }
    case 'pluck':
      voice({
        type: 'square', freq: f, t, dur: 0.16, vel: v * 0.16,
        attack: 0.003, release: 0.14,
        filter: { type: 'lowpass', freq: 2400 }, dest,
      });
      break;
    case 'drone': {
      const d = Math.max(0.5, durSec);
      voice({ type: 'sine', freq: f, t, dur: d, vel: v * 0.22, attack: Math.min(1.2, d * 0.2), release: Math.min(1.5, d * 0.3), dest });
      voice({ type: 'triangle', freq: f, t, dur: d, vel: v * 0.1, attack: Math.min(1.2, d * 0.2), release: Math.min(1.5, d * 0.3), dest });
      break;
    }
    case 'bass':
      voice({ type: 'triangle', freq: f, t, dur: durSec, vel: v * 0.5, attack: 0.01, release: 0.08, dest });
      voice({
        type: 'square', freq: f, t, dur: durSec, vel: v * 0.08,
        attack: 0.01, release: 0.06,
        filter: { type: 'lowpass', freq: 500 }, dest,
      });
      break;
    case 'kick':
      voice({ type: 'sine', freq: 130, freqEnd: 45, t, dur: 0.13, vel: v * 0.85, attack: 0.002, release: 0.1, dest });
      noiseBurst({ t, dur: 0.03, vel: v * 0.18, filter: { type: 'lowpass', freq: 900 }, dest });
      break;
    case 'snare':
      noiseBurst({ t, dur: 0.09, vel: v * 0.42, filter: { type: 'bandpass', freq: 1800, q: 0.8 }, dest });
      voice({ type: 'triangle', freq: 190, freqEnd: 120, t, dur: 0.06, vel: v * 0.14, attack: 0.002, release: 0.05, dest });
      break;
    case 'hat':
      noiseBurst({ t, dur: 0.035, vel: v * 0.15, filter: { type: 'highpass', freq: 6500 }, dest });
      break;
  }
}

// ---------- 主题调度器（前瞻式循环） ----------
export interface ThemeHandle { stop(): void }

/** 播放一个 Theme；loop=false 播完自动停止并回调 onEnd */
export function startTheme(
  theme: Theme, dest: AudioNode, loop: boolean, onEnd?: () => void,
): ThemeHandle {
  const c = ac();
  const spb = 60 / theme.bpm;
  const flat: (NoteEvent & { timbre: Timbre })[] = [];
  for (const tr of theme.tracks) {
    for (const ev of tr.events) flat.push({ ...ev, timbre: tr.timbre });
  }
  flat.sort((a, b) => a.beat - b.beat);

  const t0 = c.currentTime + 0.06;
  let ptr = 0;
  let lap = 0;
  let stopped = false;

  const finish = () => {
    if (stopped) return;
    stopped = true;
    window.clearInterval(timer);
    onEnd?.();
  };

  const tick = () => {
    if (stopped) return;
    const horizon = c.currentTime + LOOKAHEAD;
    for (;;) {
      if (ptr >= flat.length) {
        if (!loop || flat.length === 0) { finish(); return; }
        lap++;
        ptr = 0;
      }
      const ev = flat[ptr];
      const t = t0 + (lap * theme.loopBeats + ev.beat) * spb;
      if (t > horizon) break;
      playTimbre(ev.timbre, ev, t, ev.dur * spb, dest);
      ptr++;
    }
  };
  const timer = window.setInterval(tick, TICK_MS);
  tick();

  return {
    stop() {
      if (stopped) return;
      stopped = true;
      window.clearInterval(timer);
    },
  };
}

// ---------- 开关与音量（localStorage 持久化，默认都开） ----------
function readPref(key: string): boolean {
  try { return localStorage.getItem(key) !== '0'; } catch { return true; }
}

function writePref(key: string, on: boolean): void {
  try { localStorage.setItem(key, on ? '1' : '0'); } catch { /* 隐私模式忽略 */ }
}

let musicOn = readPref(KEY_MUSIC);
let sfxOn = readPref(KEY_SFX);
let musicVol = 0.25;
let sfxVol = 0.5;

export function isMusicOn(): boolean { return musicOn; }
export function isSfxOn(): boolean { return sfxOn; }

export function setMusicOn(on: boolean): void {
  musicOn = on;
  writePref(KEY_MUSIC, on);
  applyMusicGain();
}

export function setSfxOn(on: boolean): void {
  sfxOn = on;
  writePref(KEY_SFX, on);
  if (ctx && sfxBus) {
    sfxBus.gain.setTargetAtTime(on ? sfxVol : 0, ctx.currentTime, 0.05);
  }
}

export function setMusicVolume(v: number): void {
  musicVol = Math.max(0, Math.min(1, v));
  applyMusicGain();
}

export function setSfxVolume(v: number): void {
  sfxVol = Math.max(0, Math.min(1, v));
  if (ctx && sfxBus && sfxOn) sfxBus.gain.setTargetAtTime(sfxVol, ctx.currentTime, 0.05);
}

function applyMusicGain(): void {
  if (ctx && musicBus) {
    musicBus.gain.setTargetAtTime(musicOn ? musicVol : 0, ctx.currentTime, 0.15);
  }
}

// ---------- BGM：交叉淡入淡出切换 ----------
interface CurrentBgm {
  key: string;
  gain: GainNode;
  handle: ThemeHandle;
}

let current: CurrentBgm | null = null;

function fadeOutAndStop(old: CurrentBgm): void {
  const c = ac();
  old.gain.gain.setTargetAtTime(0, c.currentTime, FADE_SEC / 3);
  window.setTimeout(() => {
    old.handle.stop();
    old.gain.disconnect();
  }, FADE_SEC * 1000 + 200);
}

export const Bgm = {
  /** 切换 BGM；同名（含 results 变体）不重触发。results 需给 variant */
  play(name: BgmName, opts?: { variant?: 'win' | 'lose' }): void {
    const variant = opts?.variant ?? 'win';
    const key = name === 'results' ? `results:${variant}` : name;
    if (current?.key === key) return;
    const c = ac();
    if (current) {
      fadeOutAndStop(current);
      current = null;
    }
    const theme =
      name === 'menu' ? MENU_THEME
        : name === 'battle' ? BATTLE_THEME
          : variant === 'lose' ? RESULTS_LOSE : RESULTS_WIN;
    const gain = c.createGain();
    gain.gain.setValueAtTime(0, c.currentTime);
    gain.gain.linearRampToValueAtTime(1, c.currentTime + FADE_SEC);
    gain.connect(musicBus!);
    const loop = name !== 'results';
    const handle = startTheme(theme, gain, loop, () => {
      // 非循环短曲播完：若仍是当前 BGM 则清除，便于后续重触发
      if (current?.key === key) {
        current.gain.disconnect();
        current = null;
      }
    });
    current = { key, gain, handle };
  },

  /** 停止当前 BGM（淡出 1s） */
  stop(): void {
    if (!current) return;
    fadeOutAndStop(current);
    current = null;
  },

  /** 当前 BGM 键名（调试用） */
  current(): string | null { return current?.key ?? null; },
};

// ---------- 调试钩子（验收/排查用） ----------
function installDebugHook(): void {
  window.__zgAudio = {
    state: () => ctx?.state ?? 'none',
    bgm: () => current?.key ?? null,
    scheduled: () => scheduled,
    musicOn: isMusicOn,
    sfxOn: isSfxOn,
  };
}

declare global {
  interface Window {
    __zgAudio?: {
      state(): string;
      bgm(): string | null;
      scheduled(): number;
      musicOn(): boolean;
      sfxOn(): boolean;
    };
  }
}
