// ============================================================
// A* 寻路 + 可达格计算（Dijkstra）。
// 移动消耗规则（02-combat.md §1.2/§1.3，常量在 data/rules.ts）：
// - 水域/城墙不可进；骑兵/投石车不可进山地（§16.2 投石车特例）
// - 雨天移动消耗+1（骑兵豁免）；雪天全员+1
// - 敌对阵营单位格：不可穿越、不可落脚（blocked）
// - 同阵营单位格：可以穿越，但不能作为落脚点（noStop）
// ============================================================
import type { TerrainType, Unit } from './types';
import { TERRAIN_RULES, WEATHER_RULES } from '../data/rules';
import { Cell, hexDistance, key, neighbors } from './hex';

/** 战场地图接口（由 Battle 提供；weather 随关卡固定） */
export interface GridLike {
  width: number;
  height: number;
  weather: import('./types').Weather;
  terrainAt(q: number, r: number): TerrainType;
}

export function inBounds(grid: GridLike, q: number, r: number): boolean {
  return q >= 0 && q < grid.width && r >= 0 && r < grid.height;
}

/** 进入 (q,r) 一格的移动消耗；不可进入返回 Infinity */
export function moveCost(grid: GridLike, unit: Unit, q: number, r: number): number {
  const terrain = grid.terrainAt(q, r);
  const rule = TERRAIN_RULES[terrain];
  if (rule.impassable) return Infinity; // 水域/城墙
  // 兵种特例：骑兵、投石车（§16.2）不可进山地
  if ((unit.classType === 'cavalry' || unit.classType === 'catapult') && terrain === 'mountain') return Infinity;
  const w = WEATHER_RULES[grid.weather];
  let plus = w.moveCostPlus;
  if (plus > 0 && w.movePlusExceptCavalry && unit.classType === 'cavalry') plus = 0; // 雨天骑兵豁免
  return rule.moveCost + plus;
}

/**
 * 从 unit 当前位置出发、移动力 movePoints 内所有可达格及代价（含起点，代价 0）。
 * - blocked：完全不可进入（敌对阵营单位）
 * - noStop：可以穿越，但不能作为落脚点（同阵营单位）
 * 返回的 Map 不含 noStop 格（但会以它们为跳板扩展更远格）。
 */
export function reachableCells(
  grid: GridLike,
  unit: Unit,
  blocked: Set<string>,
  noStop: Set<string>,
  movePoints: number = unit.move,
): Map<string, number> {
  const dist = new Map<string, number>();
  dist.set(key(unit.q, unit.r), 0);
  // 地图最大 24×18，数组+线性取最小足够快，不引堆结构
  const frontier: Array<{ q: number; r: number; cost: number }> = [{ q: unit.q, r: unit.r, cost: 0 }];
  while (frontier.length > 0) {
    let bi = 0;
    for (let i = 1; i < frontier.length; i++) {
      if (frontier[i].cost < frontier[bi].cost) bi = i;
    }
    const cur = frontier.splice(bi, 1)[0];
    if (cur.cost > (dist.get(key(cur.q, cur.r)) ?? Infinity)) continue;
    for (const n of neighbors(cur.q, cur.r)) {
      if (!inBounds(grid, n.q, n.r)) continue;
      const k = key(n.q, n.r);
      if (blocked.has(k)) continue;
      const step = moveCost(grid, unit, n.q, n.r);
      if (!isFinite(step)) continue;
      const nc = cur.cost + step;
      if (nc > movePoints) continue;
      // noStop 格可以穿越，但不作为落脚点加入结果集
      if (noStop.has(k)) {
        if (nc < (dist.get(k) ?? Infinity)) {
          dist.set(k, nc);
          frontier.push({ q: n.q, r: n.r, cost: nc });
        }
        continue;
      }
      if (nc < (dist.get(k) ?? Infinity)) {
        dist.set(k, nc);
        frontier.push({ q: n.q, r: n.r, cost: nc });
      }
    }
  }
  // 过滤掉同阵营占用格（可作为跳板，但不能落脚）
  for (const k of noStop) {
    dist.delete(k);
  }
  return dist;
}

/**
 * A* 寻路：from → to 的完整路径（含起终点）；不可达返回 null。
 * - blocked 格完全不可穿越；to 落在 blocked/noStop 时直接返回 null。
 * - noStop 格可以穿越，但不能作为终点。
 */
export function findPath(
  grid: GridLike,
  from: Cell,
  to: Cell,
  unit: Unit,
  blocked: Set<string>,
  noStop: Set<string>,
): Cell[] | null {
  const toK = key(to.q, to.r);
  if (from.q === to.q && from.r === to.r) return [{ q: from.q, r: from.r }];
  if (!inBounds(grid, to.q, to.r) || blocked.has(toK) || noStop.has(toK)) return null;
  if (!isFinite(moveCost(grid, unit, to.q, to.r))) return null;

  const startK = key(from.q, from.r);
  const gScore = new Map<string, number>();
  gScore.set(startK, 0);
  const cameFrom = new Map<string, string>();
  const open: Array<{ q: number; r: number; f: number; g: number }> = [
    { q: from.q, r: from.r, f: hexDistance(from.q, from.r, to.q, to.r), g: 0 },
  ];
  const closed = new Set<string>();

  while (open.length > 0) {
    let bi = 0;
    for (let i = 1; i < open.length; i++) {
      if (open[i].f < open[bi].f) bi = i;
    }
    const cur = open.splice(bi, 1)[0];
    const curK = key(cur.q, cur.r);
    if (cur.q === to.q && cur.r === to.r) {
      // 回溯路径
      const path: Cell[] = [{ q: to.q, r: to.r }];
      let k = curK;
      while (k !== startK) {
        k = cameFrom.get(k)!;
        const i = k.indexOf(',');
        path.unshift({ q: Number(k.slice(0, i)), r: Number(k.slice(i + 1)) });
      }
      return path;
    }
    if (closed.has(curK)) continue;
    closed.add(curK);
    for (const n of neighbors(cur.q, cur.r)) {
      if (!inBounds(grid, n.q, n.r)) continue;
      const nk = key(n.q, n.r);
      if (closed.has(nk)) continue;
      const isGoal = n.q === to.q && n.r === to.r;
      if (blocked.has(nk) && !isGoal) continue;
      if (noStop.has(nk) && !isGoal) continue;
      const step = moveCost(grid, unit, n.q, n.r);
      if (!isFinite(step)) continue;
      const g = cur.g + step;
      if (g < (gScore.get(nk) ?? Infinity)) {
        gScore.set(nk, g);
        cameFrom.set(nk, curK);
        open.push({ q: n.q, r: n.r, f: g + hexDistance(n.q, n.r, to.q, to.r), g });
      }
    }
  }
  return null;
}
