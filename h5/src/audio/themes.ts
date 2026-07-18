// ============================================================
// 程序化配乐——主题数据表（不含任何 WebAudio 调用，纯数据）。
// 全部旋律为原创五声音阶（宫商角徵羽）手写音符序列，无随机生成。
//   menu    ：C 宫调，70bpm，16 小节（64 拍）舒缓循环
//   battle  ：A 羽调，110bpm，8 小节（32 拍）推进循环 + 噪声鼓点
//   results ：胜利 4 小节明亮短句 / 战败 4 小节低沉短句（不循环）
// ============================================================

/** 音色标签：由 engine 的 playTimbre 解释 */
export type Timbre =
  | 'flute'  // 三角波 + 高八度正弦叠加，模拟箫/笛
  | 'pluck'  // 短促方波，拨弦点缀
  | 'drone'  // 长音持续低音（五度）
  | 'bass'   // 低音行进
  | 'kick' | 'snare' | 'hat'; // 噪声鼓

export interface NoteEvent {
  beat: number;  // 起始拍（相对循环起点）
  dur: number;   // 时值（拍）
  midi: number;  // 音高（鼓轨忽略）
  vel: number;   // 力度 0~1
}

export interface Track { timbre: Timbre; events: NoteEvent[] }

export interface Theme {
  bpm: number;
  loopBeats: number;
  tracks: Track[];
}

// ---------- 音名 → MIDI / 频率 ----------
const SEMI: Record<string, number> = { C: 0, D: 2, E: 4, F: 5, G: 7, A: 9, B: 11 };

export function midiOf(name: string): number {
  const m = /^([A-G])(-?\d+)$/.exec(name);
  if (!m) throw new Error(`非法音名: ${name}`);
  return (parseInt(m[2], 10) + 1) * 12 + SEMI[m[1]];
}

export function midiFreq(midi: number): number {
  return 440 * Math.pow(2, (midi - 69) / 12);
}

export function freqOf(name: string): number {
  return midiFreq(midiOf(name));
}

// ---------- 数据表构建辅助 ----------
/** [音名(null=休止), 时值(拍), 力度?] */
type Step = [note: string | null, dur: number, vel?: number];

/** 顺序旋律 → 事件序列（beat 自动累加） */
function seq(steps: Step[], defVel = 1): NoteEvent[] {
  const out: NoteEvent[] = [];
  let b = 0;
  for (const [n, d, v] of steps) {
    if (n) out.push({ beat: b, dur: d, midi: midiOf(n), vel: v ?? defVel });
    b += d;
  }
  return out;
}

/** 多声部同起点（drone 五度） */
function chord(notes: string[], beat: number, dur: number, vel = 1): NoteEvent[] {
  return notes.map(n => ({ beat, dur, midi: midiOf(n), vel }));
}

/** 鼓点模式：每小节 8 个八分音符符号，x=重击 o=轻击 .=休止；可按小节给不同模式 */
function drumPat(patterns: string | string[]): NoteEvent[] {
  const pats = Array.isArray(patterns) ? patterns : [patterns];
  const out: NoteEvent[] = [];
  pats.forEach((pat, bar) => {
    for (let i = 0; i < 8; i++) {
      const ch = pat[i];
      if (ch === 'x' || ch === 'o') {
        out.push({ beat: bar * 4 + i * 0.5, dur: 0.1, midi: 0, vel: ch === 'x' ? 1 : 0.45 });
      }
    }
  });
  return out;
}

/** 低音行进：每拍一个音，拆成两个八分音符脉冲（行进感） */
function bassWalk(notesPerBeat: string[]): NoteEvent[] {
  const out: NoteEvent[] = [];
  notesPerBeat.forEach((n, beat) => {
    const midi = midiOf(n);
    out.push({ beat, dur: 0.45, midi, vel: 1 });
    out.push({ beat: beat + 0.5, dur: 0.45, midi, vel: 0.55 });
  });
  return out;
}

// ============================================================
// menu —— 《宫阙》：C 宫五声（C D E G A），70bpm，16 小节循环
// 起承转合四句（A A' B B'），句尾落回宫音 C，听感舒缓
// ============================================================
const MENU_LEAD = seq([
  // 句 A（小节 1-4）
  ['E4', 2], ['G4', 2],
  ['A4', 3], ['G4', 1],
  ['E4', 2], ['D4', 2],
  ['C4', 4],
  // 句 A'（小节 5-8，高八度变化收尾）
  ['E4', 2], ['G4', 2],
  ['A4', 3], ['C5', 1],
  ['A4', 2], ['G4', 2],
  ['E4', 4],
  // 句 B（小节 9-12，转向下属色彩）
  ['G4', 2], ['A4', 2],
  ['C5', 3], ['A4', 1],
  ['G4', 2], ['E4', 2],
  ['D4', 4],
  // 句 B'（小节 13-16，回归宫音收束）
  ['E4', 2], ['D4', 2],
  ['C4', 3], ['D4', 1],
  ['E4', 1.5], ['D4', 0.5], ['E4', 1], ['C4', 1],
  ['C4', 4],
]);

export const MENU_THEME: Theme = {
  bpm: 70,
  loopBeats: 64,
  tracks: [
    { timbre: 'flute', events: MENU_LEAD.map(e => ({ ...e, vel: e.vel * 0.9 })) },
    // 低音五度 drone：前半 C2+G2，中段转 A1+E2，末段回归
    {
      timbre: 'drone',
      events: [
        ...chord(['C2', 'G2'], 0, 32),
        ...chord(['A1', 'E2'], 32, 16),
        ...chord(['C2', 'G2'], 48, 16),
      ],
    },
    // 偶尔拨弦点缀（句尾空隙处）
    {
      timbre: 'pluck',
      events: [
        { beat: 7, dur: 0.5, midi: midiOf('A3'), vel: 0.5 },
        { beat: 23, dur: 0.5, midi: midiOf('C4'), vel: 0.5 },
        { beat: 39, dur: 0.5, midi: midiOf('G3'), vel: 0.5 },
        { beat: 55, dur: 0.5, midi: midiOf('D4'), vel: 0.5 },
        { beat: 63, dur: 0.5, midi: midiOf('A3'), vel: 0.4 },
      ],
    },
  ],
};

// ============================================================
// battle —— 《破阵》：A 羽调（A C D E G，近似羽调式），110bpm，
// 8 小节循环。短促动机反复 + 鼓点（1/3 拍底鼓、2/4 拍军鼓、
// 反拍踩镲）+ 八分音符低音行进，末小节鼓加花推向循环点。
// ============================================================
const BATTLE_LEAD = seq([
  // 小节 1-2：主动机（A 起，附点推进）
  ['A3', 1], ['A3', 0.5], ['C4', 0.5], ['D4', 1], ['E4', 1],
  ['E4', 1], ['D4', 0.5], ['C4', 0.5], ['D4', 1], ['A3', 1],
  // 小节 3-4：上行展开
  ['G3', 1], ['A3', 1], ['C4', 1], ['D4', 1],
  ['E4', 1.5], ['D4', 1], ['C4', 0.5], ['D4', 1],
  // 小节 5-6：动机再现，冲高到 G4
  ['A3', 1], ['A3', 0.5], ['C4', 0.5], ['D4', 1], ['E4', 1],
  ['G4', 1.5], ['E4', 0.5], ['D4', 1], ['C4', 1],
  // 小节 7-8：回落收束到羽音 A
  ['D4', 1], ['C4', 1], ['D4', 1], ['E4', 1],
  ['A3', 2], ['A3', 1], [null, 1],
]);

export const BATTLE_THEME: Theme = {
  bpm: 110,
  loopBeats: 32,
  tracks: [
    { timbre: 'flute', events: BATTLE_LEAD },
    {
      timbre: 'bass',
      events: bassWalk([
        'A1', 'A1', 'A1', 'A1', // 小节 1
        'A1', 'A1', 'G1', 'A1', // 小节 2
        'G1', 'G1', 'G1', 'G1', // 小节 3
        'A1', 'A1', 'E2', 'A1', // 小节 4
        'A1', 'A1', 'A1', 'A1', // 小节 5
        'G1', 'G1', 'G1', 'G1', // 小节 6
        'D2', 'D2', 'D2', 'D2', // 小节 7
        'A1', 'A1', 'E2', 'A1', // 小节 8
      ]),
    },
    {
      timbre: 'kick',
      events: drumPat([
        'x...x...', 'x...x...', 'x...x...', 'x...x...',
        'x...x...', 'x...x...', 'x...x...', 'x...x.x.',
      ]),
    },
    {
      timbre: 'snare',
      events: drumPat([
        '..x...x.', '..x...x.', '..x...x.', '..x...x.',
        '..x...x.', '..x...x.', '..x...x.', '..x..xxx',
      ]),
    },
    { timbre: 'hat', events: drumPat('.x.x.x.x') },
  ],
};

// ============================================================
// results —— 结算短句（不循环）
// 胜利：C 宫，96bpm，4 小节，级进上行后落高八度宫音，明亮
// 战败：A 羽，60bpm，4 小节，低八度下行，低沉
// ============================================================
export const RESULTS_WIN: Theme = {
  bpm: 96,
  loopBeats: 16,
  tracks: [
    {
      timbre: 'flute',
      events: seq([
        ['C4', 1], ['E4', 1], ['G4', 1], ['C5', 1],
        ['E5', 1.5], ['D5', 0.5], ['C5', 2],
        ['G4', 1], ['A4', 1], ['C5', 2],
        ['C5', 4],
      ]),
    },
    {
      timbre: 'pluck',
      events: [
        { beat: 0, dur: 0.5, midi: midiOf('C3'), vel: 0.6 },
        { beat: 4, dur: 0.5, midi: midiOf('C3'), vel: 0.5 },
        { beat: 8, dur: 0.5, midi: midiOf('G2'), vel: 0.5 },
        { beat: 12, dur: 0.5, midi: midiOf('C3'), vel: 0.6 },
      ],
    },
    { timbre: 'drone', events: chord(['C2', 'G2'], 0, 16, 0.7) },
  ],
};

export const RESULTS_LOSE: Theme = {
  bpm: 60,
  loopBeats: 16,
  tracks: [
    {
      timbre: 'flute',
      events: seq([
        ['E3', 2], ['D3', 2],
        ['C3', 3], ['D3', 1],
        ['A2', 2], ['C3', 2],
        ['A2', 4],
      ]),
    },
    { timbre: 'drone', events: chord(['A1', 'E2'], 0, 16, 0.6) },
  ],
};
