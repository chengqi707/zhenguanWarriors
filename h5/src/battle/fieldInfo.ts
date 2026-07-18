// ============================================================
// 战场展示层常量——地形中文名 / 兵种特性一句话 / 地形兵种特别规则 /
// 不可进入原因文案。纯展示用途，不含任何战斗规则计算；
// 地形数值一律读 data/rules.ts 的 TERRAIN_RULES（此处不重复定义）。
// 注：CLASS_TRAITS 与 data/rules.ts 规划中的同名常量内容一致
// （另一工作流负责落到 data 层）；若 data 层已导出 CLASS_TRAITS，
// 此处应删除并改为从 ../data import，保持单一事实来源。
// ============================================================
import type { ClassType, TerrainType } from '../core/types';

/** 地形中文名（地形信息条用） */
export const TERRAIN_NAMES: Record<TerrainType, string> = {
  plain: '平原',
  forest: '林地',
  mountain: '山地',
  water: '水域',
  city: '城池',
  wall: '城墙',
  pass: '关隘',
  camp: '营寨',
  fence: '栅栏',
};

/** 兵种短名（属性卡用） */
export const CLASS_NAMES: Record<ClassType, string> = {
  infantry: '步兵',
  heavy: '重甲',
  cavalry: '骑兵',
  archer: '弓兵',
  siege: '器械',
  strategist: '谋士',
  spear: '枪兵',
  catapult: '投石',
};

/** 兵种关键属性（属性卡金色高亮用）：武/统/智 对应 Stats 字段 */
export const CLASS_KEY_STAT: Record<ClassType, 'str' | 'cmd' | 'int'> = {
  cavalry: 'str',
  infantry: 'str',
  archer: 'str',
  siege: 'str',
  heavy: 'cmd',
  strategist: 'int',
  spear: 'str',
  catapult: 'str',
};

/** 兵种特性一句话（选中单位信息行追加，13px 暗金；数值依据 02-combat §4.4 克制表） */
export const CLASS_TRAITS: Record<ClassType, string> = {
  infantry: '步兵：克制骑兵与器械，被重甲克制',
  heavy: '重甲：盾阵抗骑兵，克制步兵与弓兵',
  cavalry: '骑兵：高机动突击，碾压弓兵与谋士；不可进入山地',
  archer: '弓兵：远程打击风筝步兵；林地射程-1',
  siege: '器械：攻城重锤可破坏城墙；惧怕谋士计策',
  strategist: '谋士：施放计策克制器械；近战脆弱',
  spear: '枪兵：长枪列阵克制骑兵',
  catapult: '投石车：远程轰击，惧怕近身',
};

/**
 * 地形对兵种的特别规则（地形信息条第二行，02-combat §1.2）。
 * 水域/城墙的「不可进入」已在移耗位展示，此处不重复。
 */
export function terrainClassRules(t: TerrainType): string[] {
  switch (t) {
    case 'mountain':
      return ['骑兵不可进入'];
    case 'forest':
      return ['弓兵射程-1'];
    default:
      return [];
  }
}

/**
 * 单位点选不可进入地形时的具体原因（toast 用），
 * 与 core/pathfinding.moveCost 的不可进入口径一致（只读判断，不改规则）。
 */
export function enterBlockReason(u: { classType: ClassType }, t: TerrainType): string | null {
  if (u.classType === 'cavalry' && t === 'mountain') return '骑兵不可进入山地';
  if (t === 'water') return '水域不可进入';
  if (t === 'wall') return '城墙不可进入';
  return null;
}
