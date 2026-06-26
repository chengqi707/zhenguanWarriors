using System.Collections.Generic;
using System.Linq;
using ZhenguanWarriors.Core.Character;

namespace ZhenguanWarriors.Core.Battle
{
    /// <summary>
    /// A* 寻路——基于六边形网格，支持地形消耗与兵种适性
    /// </summary>
    public class PathFinder
    {
        private readonly HexGrid _grid;

        public PathFinder(HexGrid grid) => _grid = grid;

        /// <summary>
        /// 寻路（不考虑兵种）
        /// </summary>
        public List<HexCoord> FindPath(HexCoord start, HexCoord end) => FindPath(start, end, ClassType.Infantry);

        /// <summary>
        /// 寻路（考虑兵种地形适性）。返回从起点到终点的路径（含起点、终点），
        /// 不可达时返回空列表。
        /// 此方法忽略其他单位，如需排除请使用带 occupiedCells 的重载。
        /// </summary>
        public List<HexCoord> FindPath(HexCoord start, HexCoord end, ClassType unitClass)
        {
            return FindPath(start, end, unitClass, null);
        }

        /// <summary>
        /// 寻路（考虑兵种地形适性 + 其他单位占据）。
        /// occupiedCells 为需要排除的格子（通常传入其他单位位置），路径会绕过这些格子。
        /// </summary>
        public List<HexCoord> FindPath(HexCoord start, HexCoord end, ClassType unitClass, HashSet<HexCoord> occupiedCells)
        {
            if (!_grid.IsWalkable(end, unitClass))
            {
                UnityEngine.Debug.LogWarning($"[PathFinder] 目标格({end.q},{end.r})不可通行！");
                return new List<HexCoord>();
            }

            // 目标格被占据则不可达
            if (occupiedCells != null && occupiedCells.Contains(end))
            {
                UnityEngine.Debug.LogWarning($"[PathFinder] 目标格({end.q},{end.r})被其他单位占据！");
                return new List<HexCoord>();
            }

            var frontier = new SortedSet<(int priority, int index, HexCoord coord)>();
            var cameFrom = new Dictionary<HexCoord, HexCoord>();
            var costSoFar = new Dictionary<HexCoord, int>();
            int indexCounter = 0;

            frontier.Add((0, indexCounter++, start));
            costSoFar[start] = 0;

            while (frontier.Count > 0)
            {
                var current = frontier.Min.coord;
                frontier.Remove(frontier.Min);

                if (current == end)
                    break;

                foreach (var next in current.Neighbors())
                {
                    // 跳过被其他单位占据的格子（起点除外）
                    if (occupiedCells != null && occupiedCells.Contains(next) && next != start)
                        continue;

                    if (!_grid.IsWalkable(next, unitClass))
                        continue;

                    int terrainCost = TerrainData.MoveCost(_grid.GetTerrain(next));
                    float multiplier = ClassData.GetTerrainCostMultiplier(unitClass, _grid.GetTerrain(next));
                    int moveCost = (int)(terrainCost * multiplier);
                    int newCost = costSoFar[current] + moveCost;

                    if (!costSoFar.ContainsKey(next) || newCost < costSoFar[next])
                    {
                        costSoFar[next] = newCost;
                        int priority = newCost + current.Distance(end);
                        frontier.Add((priority, indexCounter++, next));
                        cameFrom[next] = current;
                    }
                }
            }

            if (!cameFrom.ContainsKey(end) && start != end)
            {
                UnityEngine.Debug.LogWarning($"[PathFinder] 从({start.q},{start.r})到({end.q},{end.r})不可达！");
                return new List<HexCoord>();
            }

            // 回溯路径
            var path = new List<HexCoord>();
            var cur = end;
            while (cur != start)
            {
                path.Add(cur);
                cur = cameFrom[cur];
            }
            path.Add(start);
            path.Reverse();
            return path;
        }

        /// <summary>
        /// 在移动范围内寻找最短路径（不考虑兵种）
        /// </summary>
        public Dictionary<HexCoord, int> GetMoveRange(HexCoord start, int movePoints) =>
            GetMoveRange(start, movePoints, ClassType.Infantry);

        /// <summary>
        /// 在移动范围内寻找最短路径（考虑兵种地形适性，用于移动范围显示）
        /// </summary>
        public Dictionary<HexCoord, int> GetMoveRange(HexCoord start, int movePoints,
            ClassType unitClass, HashSet<HexCoord> occupiedCells = null)
        {
            var costs = new Dictionary<HexCoord, int> { [start] = 0 };
            var frontier = new Queue<HexCoord>();
            frontier.Enqueue(start);

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                int currentCost = costs[current];

                foreach (var next in current.Neighbors())
                {
                    // 不可通行地形
                    if (!_grid.IsWalkable(next, unitClass))
                        continue;

                    // ★ 其他单位占据的格子不可走
                    if (occupiedCells != null && occupiedCells.Contains(next))
                        continue;

                    int terrainCost = TerrainData.MoveCost(_grid.GetTerrain(next));
                    float multiplier = ClassData.GetTerrainCostMultiplier(unitClass, _grid.GetTerrain(next));
                    int moveCost = (int)(terrainCost * multiplier);
                    int newCost = currentCost + moveCost;

                    if (newCost <= movePoints &&
                        (!costs.ContainsKey(next) || newCost < costs[next]))
                    {
                        costs[next] = newCost;
                        frontier.Enqueue(next);
                    }
                }
            }
            costs.Remove(start);
            return costs;
        }
    }
}
