// ============================================================
// 战斗公式——全部纯函数，可单测。数值出自 docs/02-combat.md
// §4.2/4.3/4.5/4.6 与 docs/05-systems.md §3（常量在 data/rules.ts）。
// 所有含随机的函数必须传入 rng，禁止直接用 Math.random。
// ============================================================
import type { Buff, SkillDef, Stats, TerrainType, Unit, Weather } from './types';
import { CLASS_COUNTER, COMBAT_FORMULA, EXP_RULES, TERRAIN_RULES, WEATHER_RULES } from '../data/rules';
import { Cell, hexDirectionIndex, hexRange, lineCells } from './hex';

export type Rng = () => number;

/** buff 某数值字段的合计 */
function buffSum(u: Unit, field: keyof Pick<Buff, 'attackPct' | 'defensePct' | 'hitBonus' | 'dodgeBonus' | 'moveBonus'>): number {
  let s = 0;
  for (const b of u.buffs) s += b[field] ?? 0;
  return s;
}

/** 攻击+%合计 = 装备 + buff（02-combat §4.3「武器倍率」） */
export function totalAttackPct(u: Unit): number {
  return u.attackPct + buffSum(u, 'attackPct');
}

/** 防御+%合计 = 装备 + buff（02-combat §4.3「防具系数」） */
export function totalDefensePct(u: Unit): number {
  return u.defensePct + buffSum(u, 'defensePct');
}

/** 有效移动力 = 基础 + buff（水攻减速等），下限 0 */
export function effectiveMove(u: Unit): number {
  return Math.max(0, u.move + buffSum(u, 'moveBonus'));
}

/**
 * 命中率——第三轮迭代（PRD §16.1）起恒为 100：
 * 常规攻击与技能不再 miss（消除 SL 读档诱因）；地形回避、天气命中、
 * 难度命中修正同时移除；伤害浮动（±5%）与暴击保留。
 * 保留原签名与参数：仅作特殊装备/技能「按条件规避特定攻击」的预留钩子
 * （届时由调用方特判，不经过本函数），目前无实例，参数一律不参与计算。
 */
export function hitRate(
  attacker: Unit,
  defender: Unit,
  terrain: TerrainType,
  weather: Weather,
  extraHitMod: number = 0,
): number {
  void attacker; void defender; void terrain; void weather; void extraHitMod;
  return 100;
}

/** 暴击率（02-combat §4.5）：5 + LUK × 0.3 + 装备暴击加成 */
export function critRate(attacker: Unit): number {
  return COMBAT_FORMULA.critBase + attacker.stats.luk * COMBAT_FORMULA.critLukFactor + attacker.critBonus;
}

export interface AttackOpts {
  terrain: TerrainType; // 守方所在格地形
  crit?: boolean;       // 是否暴击（×1.5）
  counter?: boolean;    // 是否反击（×0.7，不暴击由调用方保证）
  rng: Rng;
}

/**
 * 普攻伤害（02-combat §4.3）：
 * 基础 = 攻方STR × 克制系数 × (1+攻击%) - 守方CMD × (1+防御%)
 * 最终 = 基础 × (1±5%随机) × (1-守方地形防御%) × 暴击1.5（×反击0.7）
 * 最低伤害保护 1。
 */
export function attackDamage(attacker: Unit, defender: Unit, opts: AttackOpts): number {
  const F = COMBAT_FORMULA;
  const counter = CLASS_COUNTER[attacker.classType][defender.classType];
  let dmg = attacker.stats.str * counter * (1 + totalAttackPct(attacker) / 100)
    - defender.stats.cmd * F.defenseCoef * (1 + totalDefensePct(defender) / 100);
  const variance = 1 + (opts.rng() * 2 - 1) * F.damageVariance;
  dmg *= variance;
  dmg *= 1 - TERRAIN_RULES[opts.terrain].defense / 100;
  if (opts.crit) dmg *= F.critDamageMult;
  if (opts.counter) dmg *= F.counterDamageMult;
  return Math.max(F.minDamage, Math.round(dmg));
}

/**
 * 反击条件（02-combat §4.5）：
 * 守方存活 + 双方距离 ≤ 守方射程 + 守方AGI > 攻方AGI × 0.8
 */
export function canCounter(attacker: Unit, defender: Unit, distance: number): boolean {
  return defender.alive
    && distance <= defender.range
    && defender.stats.agi > attacker.stats.agi * COMBAT_FORMULA.counterAgiFactor;
}

export interface SkillOpts {
  weather: Weather;
  terrain: TerrainType; // 目标所在格地形
  rng: Rng;
}

/**
 * 计策数值（02-combat §6.1 + data/skills.ts 各条注释）。
 * 返回伤害/治疗量（≥0）；buff/debuff 类恒返回 0（效果由 Battle 落地）。
 * - 火攻：目标 maxHp × 25% × INT/80 × 天气倍率（雨天0/雪天0.5）× 林地加成1.5
 * - 水攻：目标 maxHp × 15% × INT/80
 * - 落石/乱射：INT × power/100 - 守方CMD×(1+防御%)，再乘地形防御与±5%浮动（物理向）
 * - 医疗：恢复 maxHp × 30%
 */
export function skillDamage(caster: Unit, skill: SkillDef, target: Unit, opts: SkillOpts): number {
  const int = caster.stats.int;
  switch (skill.id) {
    case 'fire_attack': {
      const mult = WEATHER_RULES[opts.weather].firePowerMult;
      const forestBonus = opts.terrain === 'forest' ? COMBAT_FORMULA.fireForestBonus : 1;
      return Math.round(target.maxHp * (skill.power / 100) * (int / 80) * mult * forestBonus);
    }
    case 'water_attack':
      return Math.round(target.maxHp * (skill.power / 100) * (int / 80));
    case 'rock_slide':
    case 'volley': {
      // 简化：落石「山地+50%/连环落石」、乱射的锥形弹道未实现（见报告简化项）
      // 防御减免同普攻半减口径（defenseCoef）
      let dmg = int * skill.power / 100
        - target.stats.cmd * COMBAT_FORMULA.defenseCoef * (1 + totalDefensePct(target) / 100);
      dmg *= 1 + (opts.rng() * 2 - 1) * COMBAT_FORMULA.damageVariance;
      dmg *= 1 - TERRAIN_RULES[opts.terrain].defense / 100;
      return Math.max(COMBAT_FORMULA.minDamage, Math.round(dmg));
    }
    case 'thunder_strike': {
      // 落雷：纯智力伤害，无视地形防御（万能输出）
      let dmg = int * skill.power / 100
        - target.stats.cmd * COMBAT_FORMULA.defenseCoef * (1 + totalDefensePct(target) / 100);
      dmg *= 1 + (opts.rng() * 2 - 1) * COMBAT_FORMULA.damageVariance;
      return Math.max(COMBAT_FORMULA.minDamage, Math.round(dmg));
    }
    case 'earth_split': {
      // 地裂：3×3范围；山地/城墙/关隘/城池伤害+30%
      const bonus = ['mountain', 'wall', 'pass', 'city'].includes(opts.terrain) ? 1.3 : 1;
      let dmg = (int * skill.power / 100
        - target.stats.cmd * COMBAT_FORMULA.defenseCoef * (1 + totalDefensePct(target) / 100)) * bonus;
      dmg *= 1 + (opts.rng() * 2 - 1) * COMBAT_FORMULA.damageVariance;
      dmg *= 1 - TERRAIN_RULES[opts.terrain].defense / 100;
      return Math.max(COMBAT_FORMULA.minDamage, Math.round(dmg));
    }
    case 'heal':
      return Math.round(target.maxHp * (skill.power / 100));
    default:
      return 0;
  }
}

/** 点燃每回合伤害 = maxHp × 5% × 天气倍率 × 林地加成（02-combat §6.1/6.2） */
export function igniteTickDamage(target: Unit, weather: Weather, terrain: TerrainType): number {
  const forestBonus = terrain === 'forest' ? COMBAT_FORMULA.fireForestBonus : 1;
  return Math.max(1, Math.round(target.maxHp * 0.05 * WEATHER_RULES[weather].firePowerMult * forestBonus));
}

/** 升级所需经验（05-systems §3.1）：100 + (等级-1) × 20 */
export function expForLevel(level: number): number {
  return EXP_RULES.expBase + (level - 1) * EXP_RULES.expPerLevel;
}

/**
 * 升 1 级（05-systems §3.3）：五维 +growth，
 * HP 上限 +CMD/10+2、MP 上限 +INT/10+1（向下取整，按升级后属性算）。
 * 直接改 unit（需要 unit.growth 已填充）。
 */
export function applyLevelUp(unit: Unit): void {
  unit.level += 1;
  const g: Stats = unit.growth;
  unit.stats.str += g.str;
  unit.stats.cmd += g.cmd;
  unit.stats.int += g.int;
  unit.stats.agi += g.agi;
  unit.stats.luk += g.luk;
  unit.maxHp += Math.floor(unit.stats.cmd / EXP_RULES.hpGrowthCmdDiv) + EXP_RULES.hpGrowthBase;
  unit.maxMp += Math.floor(unit.stats.int / EXP_RULES.mpGrowthIntDiv) + EXP_RULES.mpGrowthBase;
}

/**
 * 计策影响格（data/skills.ts 的 aoe 字段）：
 * single/allySingle = 目标格；area3/allyArea3 = 目标+6邻格；
 * cone 简化为 area3（90°锥形弹道未实现）；
 * line4 = 施法者→目标方向直线 4 格（从施法者起算，不含施法者）；
 * all = 返回空数组（由 Battle 特判为全体敌人）。
 */
export function skillAoeCells(
  skill: SkillDef,
  casterQ: number,
  casterR: number,
  targetQ: number,
  targetR: number,
  width: number,
  height: number,
): Cell[] {
  const inB = (c: Cell) => c.q >= 0 && c.q < width && c.r >= 0 && c.r < height;
  switch (skill.aoe) {
    case 'single':
    case 'allySingle':
      return inB({ q: targetQ, r: targetR }) ? [{ q: targetQ, r: targetR }] : [];
    case 'area3':
    case 'allyArea3':
    case 'cone':
      return hexRange(targetQ, targetR, 1).filter(inB);
    case 'line4': {
      const dir = hexDirectionIndex(casterQ, casterR, targetQ, targetR);
      return lineCells(casterQ, casterR, dir, 4).filter(inB);
    }
    case 'all':
      return [];
  }
}
