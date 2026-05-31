namespace ZhenguanWarriors.Core.Battle
{
    /// <summary>
    /// 地形类型
    /// </summary>
    public enum TerrainType
    {
        Plain,      // 平原——标准
        Forest,     // 森林——防御+，命中-
        Mountain,   // 山地——移动消耗大，防御++
        Water,      // 水域——移动消耗极大
        City,       // 城池——防御+++
        Bridge,     // 桥——标准
        Wall        // 不可通行
    }

    public static class TerrainData
    {
        /// <summary>移动力消耗（移动一格需要多少步）</summary>
        public static int MoveCost(TerrainType t) => t switch
        {
            TerrainType.Plain => 1,
            TerrainType.Forest => 2,
            TerrainType.Mountain => 3,
            TerrainType.Water => 4,
            TerrainType.City => 1,
            TerrainType.Bridge => 1,
            TerrainType.Wall => int.MaxValue, // 不可通行
            _ => 1
        };

        /// <summary>地形防御修正（百分比）</summary>
        public static int DefenseBonus(TerrainType t) => t switch
        {
            TerrainType.Plain => 0,
            TerrainType.Forest => 10,
            TerrainType.Mountain => 20,
            TerrainType.Water => 0,
            TerrainType.City => 30,
            TerrainType.Bridge => 0,
            _ => 0
        };

        /// <summary>地形命中修正（百分比）</summary>
        public static int HitBonus(TerrainType t) => t switch
        {
            TerrainType.Plain => 0,
            TerrainType.Forest => -10,
            TerrainType.Mountain => -15,
            TerrainType.Water => -5,
            TerrainType.City => 0,
            TerrainType.Bridge => 0,
            _ => 0
        };
    }
}
