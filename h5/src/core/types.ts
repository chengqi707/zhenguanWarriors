// ============================================================
// 贞观勇士 H5 — 全局类型定义（唯一事实来源）
// 数值口径见 docs/H5_DESIGN.md §4：文档 > Unity JSON > Unity C#
// ============================================================

// ---------- 枚举 ----------
// 第三轮（PRD §16.2）：新增 spear 矛兵（克骑兵/怕弓兵）、catapult 投石车（超远射程/近身脆弱/不可进山地）
export type ClassType = 'infantry' | 'heavy' | 'cavalry' | 'archer' | 'siege' | 'strategist' | 'spear' | 'catapult';
// 第三轮（PRD §16.3）：新增 pass 关隘 / camp 营寨 / fence 栅栏
export type TerrainType = 'plain' | 'forest' | 'mountain' | 'water' | 'city' | 'wall' | 'pass' | 'camp' | 'fence';
export type Weather = 'sunny' | 'rain' | 'snow' | 'fog' | 'windy';
export type WindDir = 'none' | 'north' | 'south' | 'east' | 'west';
export type Faction = 'player' | 'enemy' | 'ally';
export type Difficulty = 'story' | 'easy' | 'normal' | 'hard'; // 极简/简单/普通/困难
export type Role = 'monarch' | 'warrior' | 'strategist' | 'female';
export type Gender = 'male' | 'female';
export type EquipSlot = 'weapon' | 'armor' | 'trinket';
export type Rarity = 'white' | 'green' | 'blue' | 'purple';

// ---------- 五维 ----------
export interface Stats {
  str: number; // 武
  cmd: number; // 统
  int: number; // 智
  agi: number; // 敏
  luk: number; // 运
}

// ---------- 静态数据定义 ----------
export interface CharacterDef {
  id: string;
  name: string;
  role: Role;
  gender: Gender;
  classType: ClassType;
  pos: string; // 角色定位短标签（PRD §4.1，如「T0·全能」「T1·治疗」）
  base: Stats;
  growth: Stats;
  hp: number;
  mp: number;
  move: number;
  range: number;
  skills: string[];   // 计策 id
  passive: string;    // 被动 id
  unlockLevel: number; // 第几关解锁（0=序幕，1-8）
}

export interface EquipmentDef {
  id: string;
  name: string;
  slot: EquipSlot;
  rarity: Rarity;
  classes?: ClassType[]; // 兵种限制（缺省=不限）
  gender?: Gender;       // 性别限制
  charId?: string;       // 角色专属
  statBonus?: Partial<Stats>;
  hpBonus?: number;
  mpBonus?: number;
  moveBonus?: number;
  rangeBonus?: number;
  attackPct?: number;    // 攻击+%
  defensePct?: number;   // 防御+%
  critBonus?: number;    // 暴击+%
  effectDesc?: string;   // 特效描述（MVP 仅展示，特殊逻辑按需实现）
  price: number;
}

export type SkillAoe = 'single' | 'area3' | 'line4' | 'cone' | 'allyArea3' | 'allySingle' | 'all';
export interface SkillDef {
  id: string;
  name: string;
  mp: number;
  range: number;        // 施放距离（格）
  aoe: SkillAoe;
  kind: 'damage' | 'heal' | 'buff' | 'debuff';
  power: number;        // 伤害/治疗系数（×INT/80 等，具体见 rules.ts）
  duration?: number;    // buff/debuff 持续回合
  desc: string;
}

export interface PassiveDef {
  id: string;
  name: string;
  desc: string;
}

/** 战斗物品类别：回血（最大HP%）/ 回蓝（固定MP）/ 攻击buff（+%） */
export type ItemKind = 'heal_hp' | 'heal_mp' | 'buff_atk';
export interface ItemDef {
  id: string;
  name: string;
  desc: string;
  kind: ItemKind;
  value: number; // heal_hp=最大HP百分比 / heal_mp=MP点数 / buff_atk=攻击+%
  price: number;
}

export interface BondDef {
  id: string;
  name: string;
  members: string[];   // 角色 id
  minCount: number;    // 至少几人同阵触发
  desc: string;
}

export interface EnemyDef {
  id: string;
  name: string;
  classType: ClassType;
  level: number;
  base: Stats;
  hp: number;
  mp: number;
  move: number;
  range: number;
  q: number;
  r: number;
  isBoss?: boolean;
  skills?: string[];
}

export type VictoryType = 'defeatAll' | 'defeatBoss' | 'defendTurns';
export interface LevelDef {
  id: number;
  name: string;
  subName?: string;
  weather: Weather;
  wind?: WindDir;
  width: number;
  height: number;
  terrain: Record<string, TerrainType>; // "q,r" → 地形（稀疏，缺省 plain）
  available: string[]; // 可用角色 id
  required: string[];  // 必出角色 id
  enemies: EnemyDef[];
  victory: VictoryType;
  bossId?: string;      // victory=defeatBoss 时
  defendTurns?: number; // victory=defendTurns 时
  maxTurns?: number;    // 超过则失败（0/缺省=不限）
  rewardGold: number;
  rewardEquip?: string[];
  intro?: string;       // 关卡简介（选关卡片）
}

// ---------- 剧情 ----------
export interface DialogueLine {
  speaker: string;   // 说话人名
  portrait?: string; // 角色 id（用于头像底色）
  text: string;
}
export interface StoryScene {
  id: string;        // story_{level}_pre / story_{level}_post
  lines: DialogueLine[];
}

// ---------- 运行时战斗单位 ----------
export interface Buff {
  id: string;
  name: string;
  attackPct?: number;
  defensePct?: number;
  hitBonus?: number;
  dodgeBonus?: number;
  moveBonus?: number;
  remainingTurns: number;
}

export interface Unit {
  uid: string;          // 运行时唯一 id
  charId: string;       // 角色/敌人定义 id
  name: string;
  faction: Faction;
  classType: ClassType;
  level: number;
  stats: Stats;         // 最终五维（成长+装备+羁绊+难度已计入）
  hp: number;
  maxHp: number;
  mp: number;
  maxMp: number;
  move: number;
  range: number;
  q: number;
  r: number;
  skills: string[];
  passive: string;
  buffs: Buff[];
  acted: boolean;       // 本回合已行动
  moved: boolean;       // 本回合已移动（经典行动流：移动后还可攻击/计策/物品/待机，可 cancelMove 撤回）
  alive: boolean;
  isBoss?: boolean;
  isHero?: boolean;     // 李世民（阵亡=失败）
  // ---- 引擎扩展字段（创建时由 Battle 填充，UI 只读） ----
  kills: number;        // 本关击杀数（战后 exp 结算用）
  growth: Stats;        // 成长值（applyLevelUp 用；敌人为 0）
  attackPct: number;    // 装备提供的攻击+%（buff/羁绊另算）
  defensePct: number;   // 装备提供的防御+%
  critBonus: number;    // 装备+羁绊提供的暴击率+%
  mpCostMult: number;   // 计策 MP 消耗倍率（房谋杜断羁绊=0.8，默认 1）
}

// ---------- 战斗状态与事件 ----------
export type BattlePhase = 'player' | 'enemy' | 'ally' | 'over';
export type BattleOutcome = 'win' | 'lose';

export interface BattleState {
  level: LevelDef;
  units: Unit[];
  turn: number;
  phase: BattlePhase;
  weather: Weather;
  difficulty: Difficulty;
  outcome: BattleOutcome | null;
  loseReason?: string;
}

export type BattleEvent =
  | { type: 'turnBegin'; turn: number; phase: BattlePhase }
  | { type: 'move'; uid: string; path: Array<{ q: number; r: number }> }
  | { type: 'attack'; uid: string; targetUid: string; hit: boolean; crit: boolean; dmg: number; targetHpAfter: number }
  | { type: 'counter'; uid: string; targetUid: string; hit: boolean; dmg: number; targetHpAfter: number }
  | { type: 'skill'; uid: string; skillId: string; cells: Array<{ q: number; r: number }> }
  | { type: 'damage'; uid: string; amount: number; hpAfter: number }
  | { type: 'heal'; uid: string; amount: number; hpAfter: number }
  | { type: 'buff'; uid: string; buff: Buff }
  | { type: 'die'; uid: string }
  | { type: 'wait'; uid: string }
  | { type: 'battleEnd'; outcome: BattleOutcome; reason?: string };

export interface ActionResult {
  ok: boolean;
  reason?: string;
  events: BattleEvent[];
}

export interface Selection {
  uid: string;
  reachable: Array<{ q: number; r: number }>; // 可移动格
  attackable: Array<{ q: number; r: number }>; // 可攻击格（当前位置）
}

// ---------- 进度与存档 ----------
export interface CharacterProgress {
  id: string;
  level: number;
  exp: number;
  equipment: { weapon?: string; armor?: string; trinket?: string };
  isUnlocked: boolean;
}

export interface SaveData {
  version: number;
  timestamp: number;
  currentLevel: number;      // 已解锁到第几关（0=序幕，1-8）
  difficulty: Difficulty;
  gold: number;
  characters: CharacterProgress[];
  inventory: string[];       // 仓库装备 id
  items: Record<string, number>; // 战斗物品库存（物品 id → 数量）
  levelStates: Record<number, { completed: boolean; perfect?: boolean }>;
  tutorialDone: boolean;
}

// ---------- 出战配置 ----------
export interface PartyMember {
  charId: string;
  level: number;
  equipment: { weapon?: string; armor?: string; trinket?: string };
}
