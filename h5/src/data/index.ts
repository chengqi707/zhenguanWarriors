// ============================================================
// 数据层统一入口——re-export 全部数据表 + 便捷查询函数。
// 纯数据/纯函数，零 DOM 依赖，可在 node 下运行。
// ============================================================
import type { BondDef, CharacterDef, EquipmentDef, LevelDef, PassiveDef, SkillDef, StoryScene } from '../core/types';
import { CHARACTERS } from './characters';
import { EQUIPMENT } from './equipment';
import { PASSIVES, SKILLS } from './skills';
import { BONDS } from './bonds';
import { LEVELS } from './levels';
import { STORIES } from './story';

export { CHARACTERS } from './characters';
export { EQUIPMENT } from './equipment';
export { SKILLS, PASSIVES } from './skills';
export { BONDS } from './bonds';
export { ITEMS, getItem } from './items';
export { LEVELS } from './levels';
export { STORIES } from './story';
export {
  TERRAIN_RULES, WEATHER_RULES, CLASS_COUNTER, DIFFICULTY_MOD,
  DIFFICULTY_DESC, CLASS_TRAITS, COMBAT_FORMULA, EXP_RULES,
} from './rules';
export type { TerrainRule, WeatherRule, DifficultyMod } from './rules';

/** 按 id 查角色，未找到返回 undefined */
export function getCharacter(id: string): CharacterDef | undefined {
  return CHARACTERS.find(c => c.id === id);
}

/** 按 id 查装备，未找到返回 undefined */
export function getEquipment(id: string): EquipmentDef | undefined {
  return EQUIPMENT.find(e => e.id === id);
}

/** 按 id 查计策，未找到返回 undefined */
export function getSkill(id: string): SkillDef | undefined {
  return SKILLS.find(s => s.id === id);
}

/** 按 id 查被动技能，未找到返回 undefined */
export function getPassive(id: string): PassiveDef | undefined {
  return PASSIVES.find(p => p.id === id);
}

/** 按 id 查羁绊，未找到返回 undefined */
export function getBond(id: string): BondDef | undefined {
  return BONDS.find(b => b.id === id);
}

/** 按关卡号（1-8）查关卡，未找到返回 undefined */
export function getLevel(id: number): LevelDef | undefined {
  return LEVELS.find(l => l.id === id);
}

/** 按 id 查剧情（如 story_1_pre / story_1_post），未找到返回 undefined */
export function getStory(id: string): StoryScene | undefined {
  return STORIES.find(s => s.id === id);
}
