// ============================================================
// 战斗规则常量——来源 docs/02-combat.md（地形/天气/克制/难度）
// 与 docs/05-systems.md §3（经验）。与 Unity C# 冲突处以文档为准。
// ============================================================
import type { ClassType, Difficulty, TerrainType, Weather } from '../core/types';

// ---------- 地形（02-combat.md §1.2） ----------
// 注：第三轮迭代（PRD §16.1）起命中恒 100，hit/dodge 字段不再参与任何计算，
// 仅为兼容文档口径保留；defense 仍按 (1-defense/100) 减免伤害。
export interface TerrainRule {
  moveCost: number;      // 移动消耗；impassable=true 时无意义
  impassable?: boolean;  // 不可进入
  hit: number;           // 命中修正（%）——§16.1 起失效，仅保留字段
  dodge: number;         // 回避修正（%）——§16.1 起失效，仅保留字段
  defense: number;       // 防御修正（%）
  special?: string;      // 特殊规则（文字描述）
}

export const TERRAIN_RULES: Record<TerrainType, TerrainRule> = {
  plain: { moveCost: 1, hit: 0, dodge: 0, defense: 0, special: '骑兵冲锋距离+1' },
  // 防御修正文档为+5%（Unity 实装为+10%，以文档为准）
  forest: { moveCost: 2, hit: -10, dodge: 10, defense: 5, special: '弓兵射程-1，不可穿越' },
  // 防御修正文档为+15%（Unity 实装为+20%，以文档为准）
  mountain: { moveCost: 3, hit: -15, dodge: 15, defense: 15, special: '骑兵/投石车不可进入' },
  // 文档：水域不可进（Unity 实装移动消耗4可进，以文档为准）
  water: { moveCost: 0, impassable: true, hit: 0, dodge: 0, defense: 0, special: '火攻在相邻水域格触发"扑灭"，效果减半' },
  // 文档地形表无"城池"行，以下数值取自 Unity TerrainData（MoveCost/DefenseBonus/HitBonus）
  city: { moveCost: 1, hit: 0, dodge: 0, defense: 30 },
  wall: { moveCost: 0, impassable: true, hit: 0, dodge: 0, defense: 0, special: '攻城器械可破坏，破坏后变为废墟（移动消耗2）' },
  // 第三轮新增（PRD §16.3）：关隘/营寨/栅栏，均可进入、无兵种限制
  pass: { moveCost: 1, hit: 0, dodge: 0, defense: 20, special: '关隘要冲，防御+20%' },
  camp: { moveCost: 1, hit: 0, dodge: 0, defense: 10, special: '驻军营寨，防御+10%' },
  fence: { moveCost: 2, hit: 0, dodge: 0, defense: 10, special: '木质栅栏，防御+10%、移动消耗2' },
};
// 注：文档另有"桥梁"地形（移动1/防御-10%/可被破坏），H5 TerrainType 无此枚举，关卡 JSON 亦未使用，暂不移植。

// ---------- 天气（02-combat.md §1.3） ----------
// 注：§16.1 起命中恒 100，archerHitMod/rangedHitMod 不再生效（保留字段兼容结构）。
export interface WeatherRule {
  moveCostPlus: number;          // 移动消耗增加值
  movePlusExceptCavalry: boolean;// 移动惩罚是否豁免骑兵
  archerHitMod: number;          // 弓箭命中修正（%）——§16.1 起失效，仅保留字段
  rangedHitMod: number;          // 远程命中修正（%，雾天用）——§16.1 起失效，仅保留字段
  firePowerMult: number;         // 火攻效果倍率（0=无效，0.5=减半，1=正常）
  vision?: number;               // 视野格数（雾天）
  special?: string;
}

export const WEATHER_RULES: Record<Weather, WeatherRule> = {
  sunny: { moveCostPlus: 0, movePlusExceptCavalry: false, archerHitMod: 0, rangedHitMod: 0, firePowerMult: 1 },
  rain: { moveCostPlus: 1, movePlusExceptCavalry: true, archerHitMod: -10, rangedHitMod: 0, firePowerMult: 0, special: '火攻无效；移动消耗+1（除骑兵）' },
  snow: { moveCostPlus: 1, movePlusExceptCavalry: false, archerHitMod: 0, rangedHitMod: 0, firePowerMult: 0.5, special: '全员移动消耗+1；火攻效果-50%' },
  fog: { moveCostPlus: 0, movePlusExceptCavalry: false, archerHitMod: 0, rangedHitMod: -20, firePowerMult: 1, vision: 4, special: '视野缩短至4格' },
  windy: { moveCostPlus: 0, movePlusExceptCavalry: false, archerHitMod: 0, rangedHitMod: 0, firePowerMult: 1, special: '火攻沿风向扩散1-2格' },
};

// ---------- 兵种克制系数（02-combat.md §4.4，攻→守；第三轮 §16.2 扩为 8×8） ----------
// 矛兵（spear）：攻——对骑兵×1.3（长枪克骑）、对弓兵×0.8、对重步×0.9、其余×1.0；
//               受——骑兵→矛×0.8、弓→矛×1.2、重步→矛×1.1、其余×1.0。
// 投石车（catapult）：攻——对器械×1.2、其余×1.0；
//               受——骑兵→砲×1.4、步兵/矛兵/弓兵→砲×1.2、重步/谋士→砲×1.0（近身脆弱）。
export const CLASS_COUNTER: Record<ClassType, Record<ClassType, number>> = {
  infantry:   { infantry: 1.0, heavy: 0.8, cavalry: 1.2, archer: 1.0, siege: 1.2, strategist: 1.0, spear: 1.0, catapult: 1.2 },
  heavy:      { infantry: 1.2, heavy: 1.0, cavalry: 0.8, archer: 1.2, siege: 1.0, strategist: 0.9, spear: 1.1, catapult: 1.0 },
  cavalry:    { infantry: 0.8, heavy: 1.2, cavalry: 1.0, archer: 1.5, siege: 0.9, strategist: 1.5, spear: 0.8, catapult: 1.4 },
  archer:     { infantry: 1.0, heavy: 0.8, cavalry: 0.7, archer: 1.0, siege: 1.2, strategist: 1.2, spear: 1.2, catapult: 1.2 },
  siege:      { infantry: 0.8, heavy: 1.0, cavalry: 1.1, archer: 0.8, siege: 1.0, strategist: 0.7, spear: 1.0, catapult: 1.0 },
  strategist: { infantry: 1.0, heavy: 1.1, cavalry: 0.7, archer: 0.8, siege: 1.5, strategist: 1.0, spear: 1.0, catapult: 1.0 },
  spear:      { infantry: 1.0, heavy: 0.9, cavalry: 1.3, archer: 0.8, siege: 1.0, strategist: 1.0, spear: 1.0, catapult: 1.2 },
  catapult:   { infantry: 1.0, heavy: 1.0, cavalry: 1.0, archer: 1.0, siege: 1.2, strategist: 1.0, spear: 1.0, catapult: 1.0 },
};

// ---------- 难度乘子（02-combat.md §4.6） ----------
export interface DifficultyMod {
  enemyAttack: number;  // 敌方攻击乘子
  enemyHp: number;      // 敌方 HP 乘子
  playerAttack: number; // 玩家攻击乘子
  playerHitMod: number; // 玩家命中修正（%）——§16.1 起失效（命中恒100），保留字段兼容结构
  enemyHitMod: number;  // 敌方命中修正（%）——§16.1 起失效（命中恒100），保留字段兼容结构
}

export const DIFFICULTY_MOD: Record<Difficulty, DifficultyMod> = {
  story:  { enemyAttack: 0.7,  enemyHp: 0.75, playerAttack: 1.3,  playerHitMod: 10, enemyHitMod: -10 }, // 极简
  easy:   { enemyAttack: 0.85, enemyHp: 0.9,  playerAttack: 1.15, playerHitMod: 5,  enemyHitMod: -5 },  // 简单
  normal: { enemyAttack: 1.0,  enemyHp: 1.0,  playerAttack: 1.0,  playerHitMod: 0,  enemyHitMod: 0 },   // 普通
  hard:   { enemyAttack: 1.15, enemyHp: 1.15, playerAttack: 0.9,  playerHitMod: 0,  enemyHitMod: 5 },   // 困难
};

/** 难度说明文案——由 DIFFICULTY_MOD 实际数值生成，设置页逐档展示 */
function difficultyDesc(d: Difficulty, lead: string): string {
  const m = DIFFICULTY_MOD[d];
  // §16.1 起命中恒 100，难度命中修正已移除，不再展示命中项
  return `${lead}：敌攻×${m.enemyAttack} 敌HP×${m.enemyHp} 我攻×${m.playerAttack}`;
}

export const DIFFICULTY_DESC: Record<Difficulty, string> = {
  story: difficultyDesc('story', '剧情向'),
  easy: difficultyDesc('easy', '轻松向'),
  normal: difficultyDesc('normal', '标准体验'),
  hard: difficultyDesc('hard', '挑战向'),
};

// ---------- 兵种特性一句话（02-combat.md §1.2 地形表 / §4.4 克制系数；§16.2 新增矛兵/投石车） ----------
// 只描述文档与上表实装的机制，倍率与 CLASS_COUNTER 一致。
// §16.1 起命中恒 100，不再出现命中/回避类描述。
export const CLASS_TRAITS: Record<ClassType, string> = {
  infantry: '攻防均衡；克制骑兵与器械（×1.2），对重步乏力（×0.8）',
  heavy: '重甲高防、移动缓慢；克制步兵与弓兵（×1.2），忌骑兵突击（受×1.2）',
  cavalry: '平原冲锋距离+1，不可进入山地；碾压弓兵与谋士（×1.5），忌步兵方阵（受×1.2）与矛兵长枪（受×1.3）',
  archer: '射程远（3格）；林地射程-1；克制矛兵（×1.2），被骑兵克制（受×1.5）',
  siege: '远程攻坚，可破坏城墙；移动缓慢；被谋士计策克制（受×1.5）',
  strategist: '计策伤害高、普攻范围2；HP 低需保护；克制器械（×1.5），被骑兵克制（受×1.5）',
  spear: '长枪方阵：克制骑兵（×1.3，受骑兵×0.8）；怕弓兵风筝（受弓兵×1.2）',
  catapult: '超远射程（4格）：克城防与器械（×1.2）；近身脆弱（受骑兵×1.4）；不可进入山地',
};

// ---------- 命中/暴击/反击公式常量（02-combat.md §4.2/4.5） ----------
// Unity CombatCalculator 实装（命中=AGI×3-AGI×2、暴击=LUK-LUK/2）与文档冲突，以文档为准。
// §16.1 起命中恒 100：hitBase/hitStatFactor/hitMin/hitMax 仅保留作历史口径参考，
// 不再参与任何计算（core/rules.ts hitRate 恒返回 100）。
export const COMBAT_FORMULA = {
  hitBase: 75,             // 【已失效 §16.1】原基础命中 = 75 + (攻方INT - 守方AGI) × hitStatFactor
  hitStatFactor: 0.5,      // 【已失效 §16.1】
  hitMin: 5,               // 【已失效 §16.1】命中率下限（%）
  hitMax: 99,              // 【已失效 §16.1】命中率上限（%）
  critBase: 5,             // 暴击率 = 5 + LUK × critLukFactor + 武器暴击加成
  critLukFactor: 0.3,
  critDamageMult: 1.5,     // 暴击伤害倍率
  counterAgiFactor: 0.8,   // 反击条件：守方AGI > 攻方AGI × 0.8
  counterDamageMult: 0.7,  // 反击伤害倍率（不暴击）
  damageVariance: 0.05,    // 伤害随机浮动 ±5%
  minDamage: 1,            // 最低伤害保护
  // R2 平衡调整（docs/EVALUATION_R2.md）：文档原为防御全额减法，
  // 高防单位对低攻敌人近乎免伤（CMD95 vs STR45 仅保底 1 点）→ 改为半减
  defenseCoef: 0.5,        // 防御减免系数：伤害 = 攻STR×克制 - 守CMD × defenseCoef
} as const;

// ---------- 经验与成长（03-character.md §5 + 05-systems.md §3） ----------
export const EXP_RULES = {
  maxLevel: 30,
  expBase: 100,            // 升级所需经验 = expBase + (等级-1) × expPerLevel
  expPerLevel: 20,
  actedMult: 1.0,          // 参战并行动
  noActMult: 0.5,          // 参战未行动
  benchedMult: 0,          // 未参战
  killBonus: 0.3,          // 击杀额外 +30%
  bossKillBonus: 0.5,      // 击杀Boss额外 +50%
  storyExpMult: 0.8,       // 极简难度经验 ×0.8
  hardExpMult: 1.2,        // 困难难度经验 ×1.2
  hpGrowthCmdDiv: 10,      // 升级 HP 上限增加 = CMD/10 + 2（向下取整）
  hpGrowthBase: 2,
  mpGrowthIntDiv: 10,      // 升级 MP 上限增加 = INT/10 + 1（向下取整）
  mpGrowthBase: 1,
} as const;
