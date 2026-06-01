using ZhenguanWarriors.Core.Battle;

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
                // 骑兵：森林减速，山地极难，水域不可
                (ClassType.Cavalry, TerrainType.Forest) => 1.5f,
                (ClassType.Cavalry, TerrainType.Mountain) => 3.0f,
                (ClassType.Cavalry, TerrainType.Water) => 4.0f,
                (ClassType.Cavalry, TerrainType.City) => 1.0f,
                // 重步兵：山地有利，水域极难
                (ClassType.HeavyInfantry, TerrainType.Mountain) => 0.5f,
                (ClassType.HeavyInfantry, TerrainType.Water) => 3.0f,
                (ClassType.HeavyInfantry, TerrainType.Forest) => 1.5f,
                // 步兵：标准
                (ClassType.Infantry, TerrainType.Forest) => 1.0f,
                (ClassType.Infantry, TerrainType.Mountain) => 1.5f,
                (ClassType.Infantry, TerrainType.City) => 0.8f,
                // 弓兵：森林有利（隐蔽），山地标准
                (ClassType.Archer, TerrainType.Forest) => 0.8f,
                (ClassType.Archer, TerrainType.Mountain) => 1.2f,
                // 器械：全地形减速（笨重），山地极难
                (ClassType.Siege, TerrainType.Plain) => 1.0f,
                (ClassType.Siege, TerrainType.Forest) => 2.0f,
                (ClassType.Siege, TerrainType.Mountain) => 4.0f,
                (ClassType.Siege, TerrainType.Water) => 4.0f,
                (ClassType.Siege, TerrainType.City) => 1.0f,
                // 谋士：标准
                (ClassType.Strategist, TerrainType.Forest) => 1.0f,
                (ClassType.Strategist, TerrainType.Mountain) => 1.5f,
                _ => 1.0f
            };

        /// <summary>
        /// 兵种是否可进入某地形（false=不可通行）
        /// </summary>
        public static bool CanEnterTerrain(ClassType t, TerrainType terrain) =>
            (t, terrain) switch
            {
                // 骑兵不可进入山地（悬崖峭壁）
                (ClassType.Cavalry, TerrainType.Mountain) => false,
                // 器械不可进入水域
                (ClassType.Siege, TerrainType.Water) => false,
                // 城墙任何人都不可进（由TerrainData处理）
                (_, TerrainType.Wall) => false,
                _ => true
            };

        // ========== 兵种通用特性 ==========

        /// <summary>兵种特性类型</summary>
        public enum ClassTrait
        {
            None,
            Charge,      // 骑兵冲锋：平原移动后首次攻击+20%
            LongShot,    // 弓兵远射：射程+1
            SiegeAOE,    // 器械AOE：普攻对目标周围1格造成伤害
            HealBonus,   // 谋士治疗强化（通用）
        }

        /// <summary>获取兵种通用特性</summary>
        public static ClassTrait GetClassTrait(ClassType t) => t switch
        {
            ClassType.Cavalry => ClassTrait.Charge,
            ClassType.Archer => ClassTrait.LongShot,
            ClassType.Siege => ClassTrait.SiegeAOE,
            _ => ClassTrait.None
        };

        /// <summary>
        /// 获取兵种特性带来的攻击范围加成
        /// LongShot = Archer射程+1, Charge = Cavalry突击时+1
        /// </summary>
        public static int GetClassRangeBonus(ClassType t, bool hasMoved = false) => GetClassTrait(t) switch
        {
            ClassTrait.LongShot => 1,                     // 弓兵常驻+1射程
            ClassTrait.Charge => hasMoved ? 1 : 0,        // 骑兵移动后+1突击距离
            _ => 0
        };

        /// <summary>兵种特性中文描述</summary>
        public static string GetTraitDescription(ClassType t) => t switch
        {
            ClassType.Cavalry => "冲锋：平原移动后首次攻击伤害+20%",
            ClassType.Archer => "远射：射程+1",
            ClassType.Siege => "破阵：普攻对目标及周围1格造成伤害",
            ClassType.Strategist => "远程：普攻射程2格，计策强化",
            ClassType.HeavyInfantry => "铁壁：受到物理伤害-10%",
            ClassType.Infantry => "均衡：不受地形负面移动力影响",
            _ => ""
        };
    }
}
