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
        /// </summary>
        public List<HexCoord> FindPath(HexCoord start, HexCoord end, ClassType unitClass)
        {
            if (!_grid.IsWalkable(end, unitClass))
                return new List<HexCoord>();

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
                return new List<HexCoord>();

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
