using System.Collections.Generic;

namespace ZhenguanWarriors.Core.Battle
{
    /// <summary>
    /// 六边形网格数据——管理所有格子及其地形
    /// </summary>
    public class HexGrid
    {
        private readonly Dictionary<HexCoord, TerrainType> _cells = new();
        public int Width { get; private set; }
        public int Height { get; private set; }

        public HexGrid(int width, int height, TerrainType defaultTerrain = TerrainType.Plain)
        {
            Width = width;
            Height = height;
            for (int q = 0; q < width; q++)
            for (int r = 0; r < height; r++)
                _cells[new HexCoord(q, r)] = defaultTerrain;
        }

        public bool InBounds(HexCoord c) =>
            c.q >= 0 && c.q < Width && c.r >= 0 && c.r < Height;

        public TerrainType GetTerrain(HexCoord c) =>
            _cells.TryGetValue(c, out var t) ? t : TerrainType.Wall;

        public void SetTerrain(HexCoord c, TerrainType t)
        {
            if (InBounds(c))
                _cells[c] = t;
        }

        /// <summary>是否可通行</summary>
        public bool IsWalkable(HexCoord c, bool ignoreUnits = false) =>
            InBounds(c) && TerrainData.MoveCost(GetTerrain(c)) < int.MaxValue;

        /// <summary>获取某格范围内所有可通行的格子</summary>
        public List<HexCoord> GetReachableCells(HexCoord start, int movePoints)
        {
            var visited = new HashSet<HexCoord> { start };
            var frontier = new Queue<(HexCoord coord, int cost)>();
            frontier.Enqueue((start, 0));

            while (frontier.Count > 0)
            {
                var (current, cost) = frontier.Dequeue();
                foreach (var next in current.Neighbors())
                {
                    if (!IsWalkable(next) || visited.Contains(next))
                        continue;

                    int nextCost = cost + TerrainData.MoveCost(GetTerrain(next));
                    if (nextCost <= movePoints)
                    {
                        visited.Add(next);
                        frontier.Enqueue((next, nextCost));
                    }
                }
            }
            visited.Remove(start);
            return new List<HexCoord>(visited);
        }

        /// <summary>将所有格子转为列表</summary>
        public List<HexCoord> AllCells()
        {
            var list = new List<HexCoord>(Width * Height);
            for (int q = 0; q < Width; q++)
            for (int r = 0; r < Height; r++)
                list.Add(new HexCoord(q, r));
            return list;
        }
    }
}
