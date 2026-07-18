// ============================================================
// 存档——localStorage 读写 + 战后结算（经验/升级/解锁/奖励）。
// 存档只存元进度（H5_DESIGN.md §5.1），不存战斗中状态。
// 经验倍率出自 docs/05-systems.md §3.2（常量在 data/rules.ts EXP_RULES）。
// SSR/node 环境无 localStorage 时降级为内存存储（仿真/测试用）。
// ============================================================
import type { Difficulty, SaveData } from './types';
import { CHARACTERS, EXP_RULES, getLevel } from '../data';
import { expForLevel } from './rules';

export const SAVE_KEY = 'zg_warriors_save';
export const SAVE_VERSION = 1;

// ---------- 存储后端：localStorage 优先，缺失时内存降级 ----------
interface StorageLike {
  getItem(k: string): string | null;
  setItem(k: string, v: string): void;
  removeItem(k: string): void;
}

const memoryStore = new Map<string, string>();
const memoryStorage: StorageLike = {
  getItem: k => memoryStore.get(k) ?? null,
  setItem: (k, v) => void memoryStore.set(k, v),
  removeItem: k => void memoryStore.delete(k),
};

function storage(): StorageLike {
  // typeof 检测：浏览器用 localStorage，node/SSR 用内存 Map
  // 某些环境（如 file:// 协议或隐私模式）localStorage 存在但访问会抛 SecurityError，捕获后降级内存
  if (typeof localStorage !== 'undefined') {
    try {
      localStorage.getItem('__test__');
      return localStorage;
    } catch {
      return memoryStorage;
    }
  }
  return memoryStorage;
}

// ---------- 读写 ----------

/** 读档；无存档、JSON 损坏或版本不符返回 null（v1 暂无迁移逻辑） */
export function load(): SaveData | null {
  const raw = storage().getItem(SAVE_KEY);
  if (!raw) return null;
  try {
    const data = JSON.parse(raw) as SaveData;
    if (data.version !== SAVE_VERSION) return null;
    return normalize(data);
  } catch {
    return null;
  }
}

/** 字段兜底：补齐老/残缺存档缺失的角色条目与容器字段（简易迁移） */
function normalize(data: SaveData): SaveData {
  data.characters = Array.isArray(data.characters) ? data.characters : [];
  for (const c of CHARACTERS) {
    if (!data.characters.some(p => p.id === c.id)) {
      data.characters.push({
        id: c.id, level: 1, exp: 0, equipment: {}, isUnlocked: c.unlockLevel <= data.currentLevel,
      });
    }
  }
  data.inventory = Array.isArray(data.inventory) ? data.inventory : [];
  data.items = data.items ?? {};
  data.levelStates = data.levelStates ?? {};
  data.tutorialDone = data.tutorialDone ?? false;
  return data;
}

/** 写档（刷新 timestamp） */
export function save(data: SaveData): void {
  data.timestamp = Date.now();
  storage().setItem(SAVE_KEY, JSON.stringify(data));
}

export function clear(): void {
  storage().removeItem(SAVE_KEY);
}

export function hasSave(): boolean {
  return storage().getItem(SAVE_KEY) !== null;
}

/** 新游戏：unlockLevel===0（序幕可用）的角色解锁、gold=0、difficulty='normal'，并立即落盘 */
export function newGame(): SaveData {
  const data: SaveData = {
    version: SAVE_VERSION,
    timestamp: Date.now(),
    currentLevel: 0, // §16.4：新游戏从序幕关（id=0）开始，序幕天然已解锁
    difficulty: 'normal',
    gold: 0,
    characters: CHARACTERS.map(c => ({
      id: c.id,
      level: 1,
      exp: 0,
      equipment: {},
      isUnlocked: c.unlockLevel === 0,
    })),
    inventory: [],
    items: { jinchuang: 3, qingxin: 2, shiqi: 1 }, // 初始战斗物品
    levelStates: {},
    tutorialDone: false,
  };
  save(data);
  return data;
}

// ---------- 战后结算 ----------

export interface LevelClearSummary {
  expGains: Record<string, number>; // 每个出战角色获得的经验
  levelUps: string[];               // 本场升过级的角色 id
  newUnlocks: string[];             // 本次新解锁的角色 id
  gold: number;                     // 获得的赏金
  equips: string[];                 // 获得的装备 id
}

/**
 * 通关结算（05-systems §3.2/§9）。原地改 save 并落盘。
 * - 解锁下一关（currentLevel+1，封顶 8）、记 levelStates.completed
 * - rewardGold 入 gold、rewardEquip 入 inventory
 * - 基础经验 = 100 + (关卡id-1) × 20（沿用升级曲线）：
 *   参战并行动 ×1.0 / 参战未行动 ×0.5 / 未参战 ×0；
 *   每次击杀 +30% 基础、击杀 Boss 再 +50% 基础；
 *   极简 ×0.8 / 困难 ×1.2（storyExpMult/hardExpMult）
 * - 循环结算升级（封顶 maxLevel=30），并解锁 unlockLevel 达标的新角色
 * @param participated 行动过的角色（×1.0 档），Battle.getParticipated()
 * @param party        全部出战角色（缺省=participated），Battle.getPartyCharIds()
 * @param bossKillers  击杀 Boss 的角色，Battle.getBossKillers()
 */
export function completeLevel(
  data: SaveData,
  levelId: number,
  killCounts: Record<string, number>,
  participated: string[],
  party: string[] = participated,
  bossKillers: string[] = [],
): LevelClearSummary {
  const level = getLevel(levelId);
  if (!level) throw new Error(`未知关卡: ${levelId}`);

  // 1. 关卡进度与奖励
  data.levelStates[levelId] = { ...data.levelStates[levelId], completed: true };
  if (levelId < 8) data.currentLevel = Math.max(data.currentLevel, levelId + 1);
  data.gold += level.rewardGold;
  const equips = level.rewardEquip ?? [];
  data.inventory.push(...equips);
  // 战利品：每通关一关固定追加 1 个金疮药（data/items.ts）
  data.items = data.items ?? {};
  data.items.jinchuang = (data.items.jinchuang ?? 0) + 1;

  // 2. 经验（05-systems §3.2）
  const base = expForLevel(levelId);
  const diffMult =
    data.difficulty === 'story' ? EXP_RULES.storyExpMult
    : data.difficulty === 'hard' ? EXP_RULES.hardExpMult
    : 1;
  const acted = new Set(participated);
  const bossSet = new Set(bossKillers);
  const summary: LevelClearSummary = { expGains: {}, levelUps: [], newUnlocks: [], gold: level.rewardGold, equips };

  for (const charId of party) {
    const prog = data.characters.find(c => c.id === charId);
    if (!prog) continue;
    const mult = acted.has(charId) ? EXP_RULES.actedMult : EXP_RULES.noActMult;
    const kills = killCounts[charId] ?? 0;
    let exp = base * mult + base * EXP_RULES.killBonus * kills;
    if (bossSet.has(charId)) exp += base * EXP_RULES.bossKillBonus;
    exp = Math.round(exp * diffMult);
    summary.expGains[charId] = exp;

    // 3. 升级（循环，封顶 maxLevel；满级不再积累经验）
    if (prog.level >= EXP_RULES.maxLevel) continue;
    prog.exp += exp;
    while (prog.level < EXP_RULES.maxLevel && prog.exp >= expForLevel(prog.level)) {
      prog.exp -= expForLevel(prog.level);
      prog.level += 1;
      if (!summary.levelUps.includes(charId)) summary.levelUps.push(charId);
    }
    if (prog.level >= EXP_RULES.maxLevel) prog.exp = 0;
  }

  // 4. 解锁新角色（按当前已解锁关卡数）
  for (const c of CHARACTERS) {
    if (c.unlockLevel <= data.currentLevel) {
      const prog = data.characters.find(p => p.id === c.id);
      if (!prog) continue; // 老/残缺存档缺条目时跳过（load 已做补齐兜底）
      if (!prog.isUnlocked) {
        prog.isUnlocked = true;
        summary.newUnlocks.push(c.id);
      }
    }
  }

  save(data);
  return summary;
}

// 类型守卫辅助：外部按 Difficulty 字符串反序列化时用
export function isDifficulty(s: string): s is Difficulty {
  return s === 'story' || s === 'easy' || s === 'normal' || s === 'hard';
}
