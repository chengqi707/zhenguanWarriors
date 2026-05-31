namespace ZhenguanWarriors.Core.Character
{
    /// <summary>
    /// 兵种类型
    /// </summary>
    public enum ClassType
    {
        Infantry,        // 步兵——均衡
        HeavyInfantry,   // 重步——高防坦克
        Cavalry,         // 骑兵——高机动
        Archer,          // 弓兵——远程
        Siege,           // 器械——攻城AOE
        Strategist       // 谋士——计策输出
    }

    /// <summary>
    /// 兵种相克表 + 兵种特性
    /// </summary>
    public static class ClassData
    {
        /// <summary>
        /// 攻击方 vs 防御方 → 伤害倍率（1.0=无加成）
        /// </summary>
        public static float GetCounterMultiplier(ClassType attacker, ClassType defender) =>
            (attacker, defender) switch
            {
                (ClassType.Infantry, ClassType.Cavalry) => 1.2f,
                (ClassType.Cavalry, ClassType.Archer) => 1.2f,
                (ClassType.Cavalry, ClassType.Strategist) => 1.25f,
                (ClassType.Archer, ClassType.Infantry) => 1.2f,
                (ClassType.HeavyInfantry, ClassType.Infantry) => 1.15f,
                (ClassType.Siege, ClassType.HeavyInfantry) => 1.2f,
                (ClassType.Strategist, ClassType.Siege) => 1.3f,
                (ClassType.Strategist, ClassType.HeavyInfantry) => 1.15f,
                _ => 1.0f
            };

        /// <summary>兵种中文名</summary>
        public static string GetName(ClassType t) => t switch
        {
            ClassType.Infantry => "步兵",
            ClassType.HeavyInfantry => "重步",
            ClassType.Cavalry => "骑兵",
            ClassType.Archer => "弓兵",
            ClassType.Siege => "器械",
            ClassType.Strategist => "谋士",
            _ => "未知"
        };

        /// <summary>基本移动力</summary>
        public static int GetBaseMove(ClassType t) => t switch
        {
            ClassType.Cavalry => 6,
            ClassType.Archer => 4,
            ClassType.Strategist => 5,
            _ => 5
        };

        /// <summary>基本攻击范围</summary>
        public static int GetBaseAttackRange(ClassType t) => t switch
        {
            ClassType.Archer => 2,
            ClassType.Strategist => 2,
            ClassType.Siege => 2,
            _ => 1
        };

        /// <summary>地形消耗修正（兵种特殊地形适性，1.0=标准）</summary>
        public static float GetTerrainCostMultiplier(ClassType t, TerrainType terrain) =>
            (t, terrain) switch
            {
                (ClassType.Cavalry, TerrainType.Forest) => 1.5f,
                (ClassType.Cavalry, TerrainType.Mountain) => 2.0f,
                (ClassType.HeavyInfantry, TerrainType.Water) => 2.0f,
                (ClassType.HeavyInfantry, TerrainType.Mountain) => 0.5f,
                _ => 1.0f
            };
    }
}
