// ============================================================
// flat-top 六边形网格——坐标直接用 offset (q,r)（奇数列偏移 odd-q），
// 算距离/邻居时内部转 cube。公式来源：redblobgames hex grid guide。
// 纯函数，零 DOM 依赖。
// ============================================================

export interface Cell {
  q: number;
  r: number;
}

export interface Cube {
  x: number;
  y: number;
  z: number;
}

/** offset (q,r) → "q,r" 键（Map/Set 用） */
export function key(q: number, r: number): string {
  return `${q},${r}`;
}

/** "q,r" 键 → offset 坐标 */
export function parseKey(k: string): Cell {
  const i = k.indexOf(',');
  return { q: Number(k.slice(0, i)), r: Number(k.slice(i + 1)) };
}

/** odd-q offset → cube（列偏移适用于 flat-top） */
export function offsetToCube(q: number, r: number): Cube {
  const x = q;
  const z = r - (q - (q & 1)) / 2;
  const y = -x - z;
  return { x, y, z };
}

/** cube → odd-q offset */
export function cubeToOffset(c: Cube): Cell {
  const q = c.x;
  const r = c.z + (c.x - (c.x & 1)) / 2;
  return { q, r };
}

/** cube 空间 6 个方向（顺序固定，line4 计策按索引使用） */
export const DIRECTIONS: Cube[] = [
  { x: 1, y: -1, z: 0 },
  { x: 1, y: 0, z: -1 },
  { x: 0, y: 1, z: -1 },
  { x: 0, y: -1, z: 1 },
  { x: -1, y: 0, z: 1 },
  { x: -1, y: 1, z: 0 },
];

/** 某方向的相邻格 */
export function neighbor(q: number, r: number, dir: number): Cell {
  const c = offsetToCube(q, r);
  const d = DIRECTIONS[dir];
  return cubeToOffset({ x: c.x + d.x, y: c.y + d.y, z: c.z + d.z });
}

/** 6 个相邻格（不检查边界，调用方自行过滤） */
export function neighbors(q: number, r: number): Cell[] {
  const c = offsetToCube(q, r);
  return DIRECTIONS.map(d => cubeToOffset({ x: c.x + d.x, y: c.y + d.y, z: c.z + d.z }));
}

/** hex 距离 = cube 坐标差绝对值之和 / 2 */
export function hexDistance(q1: number, r1: number, q2: number, r2: number): number {
  const a = offsetToCube(q1, r1);
  const b = offsetToCube(q2, r2);
  return (Math.abs(a.x - b.x) + Math.abs(a.y - b.y) + Math.abs(a.z - b.z)) / 2;
}

/** (q,r) 周围 n 格内的所有格（含中心；不检查边界） */
export function hexRange(q: number, r: number, n: number): Cell[] {
  const c = offsetToCube(q, r);
  const out: Cell[] = [];
  for (let dx = -n; dx <= n; dx++) {
    const lo = Math.max(-n, -dx - n);
    const hi = Math.min(n, -dx + n);
    for (let dy = lo; dy <= hi; dy++) {
      const dz = -dx - dy;
      out.push(cubeToOffset({ x: c.x + dx, y: c.y + dy, z: c.z + dz }));
    }
  }
  return out;
}

/**
 * from → to 的主方向（DIRECTIONS 索引 0-5）。
 * 取 cube 位移与各方向点积最大者；from==to 时返回 0。
 */
export function hexDirectionIndex(fromQ: number, fromR: number, toQ: number, toR: number): number {
  const a = offsetToCube(fromQ, fromR);
  const b = offsetToCube(toQ, toR);
  const dx = b.x - a.x;
  const dy = b.y - a.y;
  const dz = b.z - a.z;
  let best = 0;
  let bestDot = -Infinity;
  for (let i = 0; i < DIRECTIONS.length; i++) {
    const d = DIRECTIONS[i];
    const dot = dx * d.x + dy * d.y + dz * d.z;
    if (dot > bestDot) {
      bestDot = dot;
      best = i;
    }
  }
  return best;
}

/** 从 (q,r) 沿方向 dir 的 length 格直线（不含起点；不检查边界），落石 line4 用 */
export function lineCells(q: number, r: number, dir: number, length: number): Cell[] {
  const c = offsetToCube(q, r);
  const d = DIRECTIONS[dir];
  const out: Cell[] = [];
  for (let i = 1; i <= length; i++) {
    out.push(cubeToOffset({ x: c.x + d.x * i, y: c.y + d.y * i, z: c.z + d.z * i }));
  }
  return out;
}
