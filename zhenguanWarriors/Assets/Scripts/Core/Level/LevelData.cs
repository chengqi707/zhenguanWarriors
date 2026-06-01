using System.Collections.Generic;
using ZhenguanWarriors.Core.Battle;
using ZhenguanWarriors.Core.Character;

namespace ZhenguanWarriors.Core.Level
{
    /// <summary>
    /// 胜利条件类型
    /// </summary>
    public enum VictoryConditionType
    {
        DefeatAll,      // 全灭敌军
        DefeatBoss,     // 击破指定敌将
        DefendTurns,    // 守住指定回合数
        ReachPoint,     // 到达指定地点
        Survive         // 主角存活即可（剧情关）
    }

    /// <summary>
    /// 失败条件类型
    /// </summary>
    public enum DefeatConditionType
    {
        PlayerDead,     // 主角阵亡
        AllDead,        // 全员阵亡
        BossReachPoint, // 敌方到达指定地点
        TurnLimit       // 超过回合上限
    }

    /// <summary>
    /// 敌方单位配置
    /// </summary>
    public class EnemyConfig
    {
        public string id;
        public string name;
        public ClassType unitClass;
        public int level;
        public int str, cmd, @int, agi, luk;
        public int hp, mp;
        public int move, attackRange;
        public HexCoord position;
        public List<string> skillIds;
        public bool isBoss;
    }

    /// <summary>
    /// 关卡配置数据
    /// </summary>
    public class LevelData
    {
        public string levelId;
        public string name;
        public int width;
        public int height;
        public WeatherType weather;
        public WindDirection wind;

        // 地形布局：坐标 -> 地形类型
        public Dictionary<HexCoord, TerrainType> terrainOverrides;

        // 己方可用角色ID列表
        public List<string> availableCharacters;

        // 固定出阵角色（必须上场）
        public List<string> requiredCharacters;

        // 敌方配置
        public List<EnemyConfig> enemies;

        // 胜利条件
        public VictoryConditionType victoryType;
        public string targetBossId;     // 击破主将时的目标ID
        public int defendTurns;         // 守回合数
        public HexCoord reachPoint;     // 到达点

        // 失败条件
        public List<DefeatConditionType> defeatTypes;
        public int maxTurns;            // 回合上限

        // 剧情触发点（回合 -> 事件ID）
        public Dictionary<int, string> turnEvents;

        public LevelData()
        {
            terrainOverrides = new Dictionary<HexCoord, TerrainType>();
            availableCharacters = new List<string>();
            requiredCharacters = new List<string>();
            enemies = new List<EnemyConfig>();
            defeatTypes = new List<DefeatConditionType>();
            turnEvents = new Dictionary<int, string>();
        }
    }

    /// <summary>
    /// 预定义关卡库
    /// </summary>
    public static class LevelLibrary
    {
        private static Dictionary<string, LevelData> _levels;

        public static Dictionary<string, LevelData> GetAll()
        {
            if (_levels == null) Build();
            return _levels;
        }

        public static LevelData Get(string id)
        {
            if (_levels == null) Build();
            return _levels.TryGetValue(id, out var l) ? l : null;
        }

        private static void Build()
        {
            _levels = new Dictionary<string, LevelData>();

            // ========== 第1关：晋阳举义 ==========
            var level1 = new LevelData
            {
                levelId = "level_01",
                name = "晋阳举义",
                width = 16,
                height = 12,
                weather = WeatherType.Sunny,
                wind = WindDirection.None,
                availableCharacters = new List<string> { "lishimin", "zhangsun_wuji", "chai_shao", "liu_hongji" },
                requiredCharacters = new List<string> { "lishimin" },
                victoryType = VictoryConditionType.DefeatAll,
                defeatTypes = new List<DefeatConditionType> { DefeatConditionType.PlayerDead }
            };
            // 地形：中部森林
            for (int q = 6; q <= 9; q++)
                for (int r = 4; r <= 7; r++)
                    level1.terrainOverrides[new HexCoord(q, r)] = TerrainType.Forest;
            // 敌军
            level1.enemies.Add(new EnemyConfig
            {
                id = "enemy_boss_01", name = "隋军校尉", unitClass = ClassType.Cavalry,
                level = 3, str = 55, cmd = 45, @int = 25, agi = 35, luk = 25,
                hp = 55, mp = 10, move = 5, attackRange = 1,
                position = new HexCoord(12, 5), isBoss = true
            });
            for (int i = 0; i < 4; i++)
            {
                level1.enemies.Add(new EnemyConfig
                {
                    id = $"enemy_01_{i}", name = "隋军步兵", unitClass = ClassType.Infantry,
                    level = 2, str = 45, cmd = 40, @int = 20, agi = 30, luk = 20,
                    hp = 45, mp = 0, move = 4, attackRange = 1,
                    position = new HexCoord(10 + i % 2, 3 + i / 2)
                });
            }
            _levels["level_01"] = level1;

            // ========== 第2关：霍邑攻坚 ==========
            var level2 = new LevelData
            {
                levelId = "level_02",
                name = "霍邑攻坚",
                width = 20,
                height = 14,
                weather = WeatherType.Rain,
                wind = WindDirection.North,
                availableCharacters = new List<string> { "lishimin", "zhangsun_wuji", "chai_shao", "liu_hongji", "yin_kaishan", "duan_zhixuan" },
                requiredCharacters = new List<string> { "lishimin" },
                victoryType = VictoryConditionType.DefeatBoss,
                targetBossId = "enemy_boss_02",
                defeatTypes = new List<DefeatConditionType> { DefeatConditionType.PlayerDead, DefeatConditionType.TurnLimit },
                maxTurns = 20
            };
            // 山地地形
            for (int q = 14; q <= 17; q++)
                for (int r = 5; r <= 9; r++)
                    level2.terrainOverrides[new HexCoord(q, r)] = TerrainType.Mountain;
            // 城池
            for (int q = 16; q <= 18; q++)
                for (int r = 6; r <= 8; r++)
                    level2.terrainOverrides[new HexCoord(q, r)] = TerrainType.City;
            // 敌军
            level2.enemies.Add(new EnemyConfig
            {
                id = "enemy_boss_02", name = "宋老生", unitClass = ClassType.Cavalry,
                level = 5, str = 70, cmd = 55, @int = 35, agi = 50, luk = 40,
                hp = 90, mp = 20, move = 5, attackRange = 1,
                position = new HexCoord(16, 7), isBoss = true
            });
            level2.enemies.Add(new EnemyConfig
            {
                id = "enemy_02_0", name = "隋军骑兵", unitClass = ClassType.Cavalry,
                level = 4, str = 55, cmd = 45, @int = 30, agi = 45, luk = 30,
                hp = 60, mp = 15, move = 5, attackRange = 1,
                position = new HexCoord(15, 6)
            });
            for (int i = 0; i < 5; i++)
            {
                level2.enemies.Add(new EnemyConfig
                {
                    id = $"enemy_02_{i + 1}", name = "隋军步兵", unitClass = ClassType.Infantry,
                    level = 3, str = 48, cmd = 42, @int = 22, agi = 32, luk = 22,
                    hp = 50, mp = 0, move = 4, attackRange = 1,
                    position = new HexCoord(13 + i % 3, 4 + i / 3)
                });
            }
            _levels["level_02"] = level2;

            // ========== 第3关：直取长安 ==========
            var level3 = new LevelData
            {
                levelId = "level_03",
                name = "直取长安",
                width = 24,
                height = 16,
                weather = WeatherType.Sunny,
                wind = WindDirection.None,
                availableCharacters = new List<string> { "lishimin", "zhangsun_wuji", "chai_shao", "liu_hongji", "yin_kaishan", "duan_zhixuan", "pingyang_princess", "fang_xuanling" },
                requiredCharacters = new List<string> { "lishimin" },
                victoryType = VictoryConditionType.DefeatBoss,
                targetBossId = "enemy_boss_03",
                defeatTypes = new List<DefeatConditionType> { DefeatConditionType.PlayerDead }
            };
            // 城池城墙
            for (int q = 18; q <= 22; q++)
                for (int r = 6; r <= 10; r++)
                    level3.terrainOverrides[new HexCoord(q, r)] = TerrainType.City;
            // 护城河
            for (int q = 17; q <= 18; q++)
                for (int r = 7; r <= 9; r++)
                    level3.terrainOverrides[new HexCoord(q, r)] = TerrainType.Water;
            // 敌军
            level3.enemies.Add(new EnemyConfig
            {
                id = "enemy_boss_03", name = "长安守将", unitClass = ClassType.HeavyInfantry,
                level = 8, str = 65, cmd = 75, @int = 30, agi = 40, luk = 35,
                hp = 110, mp = 15, move = 3, attackRange = 1,
                position = new HexCoord(20, 8), isBoss = true
            });
            for (int i = 0; i < 3; i++)
            {
                level3.enemies.Add(new EnemyConfig
                {
                    id = $"enemy_03_{i}", name = "隋军重步", unitClass = ClassType.HeavyInfantry,
                    level = 5, str = 55, cmd = 60, @int = 20, agi = 28, luk = 20,
                    hp = 75, mp = 0, move = 3, attackRange = 1,
                    position = new HexCoord(18 + i, 7)
                });
            }
            for (int i = 0; i < 3; i++)
            {
                level3.enemies.Add(new EnemyConfig
                {
                    id = $"enemy_03_{i + 3}", name = "隋军弓兵", unitClass = ClassType.Archer,
                    level = 5, str = 45, cmd = 38, @int = 30, agi = 40, luk = 28,
                    hp = 50, mp = 10, move = 4, attackRange = 2,
                    position = new HexCoord(19, 6 + i),
                    skillIds = new List<string> { "volley" }
                });
            }
            _levels["level_03"] = level3;
        }
    }
}
