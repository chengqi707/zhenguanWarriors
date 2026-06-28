using System.Collections.Generic;
using ZhenguanWarriors.Core.Battle;
using ZhenguanWarriors.Core.Character;
using ZhenguanWarriors.Utils;

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

        // 通关奖励
        public List<string> rewardEquipIds;
        public int rewardGold;

        // 战后招募（可选）
        public string recruitCharacterId;

        public LevelData()
        {
            terrainOverrides = new Dictionary<HexCoord, TerrainType>();
            availableCharacters = new List<string>();
            requiredCharacters = new List<string>();
            enemies = new List<EnemyConfig>();
            defeatTypes = new List<DefeatConditionType>();
            turnEvents = new Dictionary<int, string>();
            rewardEquipIds = new List<string>();
            rewardGold = 0;
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

            // 尝试从 JSON 加载，失败则回退硬编码
            try
            {
                var jsonLevel = LevelJsonLoader.LoadFromResources(id);
                if (jsonLevel != null) return jsonLevel;
            }
            catch (System.Exception e)
            {
                GameLogger.LogWarningFormat(LogCategory.Level, "JSON加载失败，使用硬编码|关卡={0}|原因={1}", id, e.Message);
            }

            if (_levels.TryGetValue(id, out var l)) return l;

            // 兜底：硬编码库也没有，再次 Build
            GameLogger.LogWarningFormat(LogCategory.Level, "关卡不在缓存中，重新Build|关卡={0}", id);
            Build();
            return _levels.TryGetValue(id, out var l2) ? l2 : null;
        }

        /// <summary>导出全部关卡到JSON文件（编辑器下使用）</summary>
        public static void ExportAllToJson(string outputDir = null)
        {
            if (_levels == null) Build();

#if UNITY_EDITOR
            if (string.IsNullOrEmpty(outputDir))
                outputDir = UnityEngine.Application.dataPath + "/Data/Levels";

            if (!System.IO.Directory.Exists(outputDir))
                System.IO.Directory.CreateDirectory(outputDir);

            foreach (var kv3 in _levels)
            {
                string path = $"{outputDir}/{kv3.Key}.json";
                LevelJsonLoader.SaveToFile(kv3.Value, path);
                GameLogger.LogInfoFormat(LogCategory.Level, "导出关卡|关卡={0}|路径={1}", kv3.Key, path);
            }
            GameLogger.LogInfoFormat(LogCategory.Level, "全部关卡已导出|数量={0}|目录={1}", _levels.Count, outputDir);
#endif
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
                availableCharacters = new List<string> { "lishimin", "li_jing", "zhangsun_wuji", "chai_shao", "liu_hongji" },
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
            level1.rewardGold = 100;
            level1.rewardEquipIds = new List<string> { "w001" };
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
            level2.rewardGold = 150;
            level2.rewardEquipIds = new List<string> { "a001" };
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
            level3.rewardGold = 200;
            level3.rewardEquipIds = new List<string> { "w002" };
            level3.recruitCharacterId = "cheng_yaojin";
            _levels["level_03"] = level3;

            // ========== 第4关：浅水原之战 ==========
            var level4 = new LevelData
            {
                levelId = "level_04",
                name = "浅水原之战",
                width = 20,
                height = 14,
                weather = WeatherType.Windy,
                wind = WindDirection.North,
                availableCharacters = new List<string> { "lishimin", "li_jing", "zhangsun_wuji", "chai_shao", "liu_hongji", "duan_zhixuan", "qin_qiong", "yuchi_jingde", "fang_xuanling", "hou_junji" },
                requiredCharacters = new List<string> { "lishimin" },
                victoryType = VictoryConditionType.DefeatBoss,
                targetBossId = "enemy_boss_04",
                defeatTypes = new List<DefeatConditionType> { DefeatConditionType.PlayerDead }
            };
            // 地形：中部平原开阔，四周浅丘
            for (int q = 8; q <= 12; q++)
                for (int r = 5; r <= 8; r++)
                    level4.terrainOverrides[new HexCoord(q, r)] = TerrainType.Plain;
            for (int q = 3; q <= 5; q++)
                for (int r = 2; r <= 4; r++)
                    level4.terrainOverrides[new HexCoord(q, r)] = TerrainType.Forest;
            // 敌军
            level4.enemies.Add(new EnemyConfig
            {
                id = "enemy_boss_04", name = "薛举", unitClass = ClassType.Cavalry,
                level = 7, str = 75, cmd = 65, @int = 40, agi = 60, luk = 45,
                hp = 100, mp = 20, move = 6, attackRange = 1,
                position = new HexCoord(16, 7), isBoss = true,
                skillIds = new List<string> { "rally" }
            });
            for (int i = 0; i < 4; i++)
            {
                level4.enemies.Add(new EnemyConfig
                {
                    id = $"enemy_04_{i}", name = "西秦骑兵", unitClass = ClassType.Cavalry,
                    level = 5, str = 60, cmd = 50, @int = 25, agi = 50, luk = 30,
                    hp = 70, mp = 10, move = 5, attackRange = 1,
                    position = new HexCoord(14 + i % 2, 5 + i / 2)
                });
            }
            level4.enemies.Add(new EnemyConfig
            {
                id = "enemy_04_4", name = "西秦谋士", unitClass = ClassType.Strategist,
                level = 6, str = 25, cmd = 40, @int = 75, agi = 50, luk = 55,
                hp = 60, mp = 50, move = 5, attackRange = 2,
                position = new HexCoord(15, 9),
                skillIds = new List<string> { "fire_attack", "confuse" }
            });
            level4.rewardGold = 250;
            level4.rewardEquipIds = new List<string> { "t001" };
            _levels["level_04"] = level4;

            // ========== 第5关：柏壁之战 ==========
            var level5 = new LevelData
            {
                levelId = "level_05",
                name = "柏壁之战",
                width = 22,
                height = 16,
                weather = WeatherType.Snow,
                wind = WindDirection.None,
                availableCharacters = new List<string> { "lishimin", "li_jing", "yuchi_jingde", "qin_qiong", "zhangsun_wuji", "chai_shao", "liu_hongji", "yin_kaishan", "duan_zhixuan", "cheng_yaojin", "hou_junji" },
                requiredCharacters = new List<string> { "lishimin" },
                victoryType = VictoryConditionType.DefendTurns,
                defendTurns = 10,
                defeatTypes = new List<DefeatConditionType> { DefeatConditionType.AllDead, DefeatConditionType.TurnLimit },
                maxTurns = 15
            };
            // 地形：城墙防守战，中间一座城，城外山地
            for (int q = 4; q <= 10; q++)
                for (int r = 6; r <= 10; r++)
                    level5.terrainOverrides[new HexCoord(q, r)] = TerrainType.City;
            for (int q = 3; q <= 4; q++)
                for (int r = 6; r <= 10; r++)
                    level5.terrainOverrides[new HexCoord(q, r)] = TerrainType.Wall;
            for (int q = 12; q <= 18; q++)
                for (int r = 4; r <= 8; r++)
                    level5.terrainOverrides[new HexCoord(q, r)] = TerrainType.Mountain;
            // 敌军（大量）
            for (int i = 0; i < 6; i++)
            {
                level5.enemies.Add(new EnemyConfig
                {
                    id = $"enemy_05_{i}", name = "刘军步兵", unitClass = ClassType.Infantry,
                    level = 6, str = 55, cmd = 50, @int = 25, agi = 35, luk = 25,
                    hp = 65, mp = 0, move = 4, attackRange = 1,
                    position = new HexCoord(14 + i % 3, 4 + i / 3)
                });
            }
            for (int i = 0; i < 3; i++)
            {
                level5.enemies.Add(new EnemyConfig
                {
                    id = $"enemy_05_{i + 6}", name = "刘军弓兵", unitClass = ClassType.Archer,
                    level = 6, str = 50, cmd = 42, @int = 30, agi = 45, luk = 28,
                    hp = 55, mp = 10, move = 4, attackRange = 2,
                    position = new HexCoord(16 + i, 10),
                    skillIds = new List<string> { "volley" }
                });
            }
            level5.rewardGold = 300;
            level5.rewardEquipIds = new List<string> { "a002" };
            _levels["level_05"] = level5;

            // ========== 第6关：洛阳攻坚战 ==========
            var level6 = new LevelData
            {
                levelId = "level_06",
                name = "洛阳攻坚战",
                width = 24,
                height = 18,
                weather = WeatherType.Sunny,
                wind = WindDirection.None,
                availableCharacters = new List<string> { "lishimin", "li_jing", "yuchi_jingde", "qin_qiong", "zhangsun_wuji", "chai_shao", "liu_hongji", "yin_kaishan", "duan_zhixuan", "cheng_yaojin", "hou_junji", "fang_xuanling" },
                requiredCharacters = new List<string> { "lishimin", "yin_kaishan" },
                victoryType = VictoryConditionType.DefeatBoss,
                targetBossId = "enemy_boss_06",
                defeatTypes = new List<DefeatConditionType> { DefeatConditionType.PlayerDead, DefeatConditionType.TurnLimit },
                maxTurns = 25
            };
            // 洛阳城（大型城市+护城河）
            for (int q = 16; q <= 22; q++)
                for (int r = 7; r <= 12; r++)
                    level6.terrainOverrides[new HexCoord(q, r)] = TerrainType.City;
            for (int q = 14; q <= 16; q++)
                for (int r = 8; r <= 11; r++)
                    level6.terrainOverrides[new HexCoord(q, r)] = TerrainType.Water;
            // 敌军（大量）
            level6.enemies.Add(new EnemyConfig
            {
                id = "enemy_boss_06", name = "王世充", unitClass = ClassType.HeavyInfantry,
                level = 10, str = 78, cmd = 82, @int = 60, agi = 50, luk = 55,
                hp = 140, mp = 30, move = 4, attackRange = 1,
                position = new HexCoord(20, 10), isBoss = true,
                skillIds = new List<string> { "rally", "confuse" }
            });
            for (int i = 0; i < 5; i++)
            {
                level6.enemies.Add(new EnemyConfig
                {
                    id = $"enemy_06_{i}", name = "郑军重步", unitClass = ClassType.HeavyInfantry,
                    level = 7, str = 62, cmd = 65, @int = 25, agi = 30, luk = 25,
                    hp = 90, mp = 0, move = 3, attackRange = 1,
                    position = new HexCoord(17 + i, 8)
                });
            }
            for (int i = 0; i < 3; i++)
            {
                level6.enemies.Add(new EnemyConfig
                {
                    id = $"enemy_06_{i + 5}", name = "郑军弓兵", unitClass = ClassType.Archer,
                    level = 7, str = 52, cmd = 45, @int = 30, agi = 42, luk = 28,
                    hp = 58, mp = 10, move = 4, attackRange = 3,
                    position = new HexCoord(18 + i, 12),
                    skillIds = new List<string> { "volley" }
                });
            }
            level6.enemies.Add(new EnemyConfig
            {
                id = "enemy_06_8", name = "郑军谋士", unitClass = ClassType.Strategist,
                level = 8, str = 28, cmd = 45, @int = 80, agi = 55, luk = 60,
                hp = 65, mp = 55, move = 5, attackRange = 2,
                position = new HexCoord(21, 7),
                skillIds = new List<string> { "fire_attack", "rock_slide" }
            });
            level6.rewardGold = 350;
            level6.rewardEquipIds = new List<string> { "w007" };
            _levels["level_06"] = level6;

            // ========== 第7关：虎牢关之战 ==========
            var level7 = new LevelData
            {
                levelId = "level_07",
                name = "虎牢关之战",
                width = 24,
                height = 16,
                weather = WeatherType.Sunny,
                wind = WindDirection.East,
                availableCharacters = new List<string> { "lishimin", "li_jing", "yuchi_jingde", "qin_qiong", "zhangsun_wuji", "chai_shao", "liu_hongji", "yin_kaishan", "duan_zhixuan", "cheng_yaojin", "hou_junji", "pingyang_princess" },
                requiredCharacters = new List<string> { "lishimin", "qin_qiong", "yuchi_jingde" },
                victoryType = VictoryConditionType.DefeatBoss,
                targetBossId = "enemy_boss_07",
                defeatTypes = new List<DefeatConditionType> { DefeatConditionType.PlayerDead }
            };
            // 虎牢关地形：一夫当关的关隘
            for (int q = 12; q <= 14; q++)
                for (int r = 5; r <= 10; r++)
                    level7.terrainOverrides[new HexCoord(q, r)] = TerrainType.City;
            for (int q = 10; q <= 11; q++)
                for (int r = 5; r <= 10; r++)
                    level7.terrainOverrides[new HexCoord(q, r)] = TerrainType.Wall;
            for (int q = 5; q <= 9; q++)
                for (int r = 3; r <= 6; r++)
                    level7.terrainOverrides[new HexCoord(q, r)] = TerrainType.Mountain;
            for (int q = 16; q <= 20; q++)
                for (int r = 8; r <= 12; r++)
                    level7.terrainOverrides[new HexCoord(q, r)] = TerrainType.Forest;
            // 敌军（含骑兵突击队）
            level7.enemies.Add(new EnemyConfig
            {
                id = "enemy_boss_07", name = "窦建德", unitClass = ClassType.Cavalry,
                level = 12, str = 85, cmd = 78, @int = 55, agi = 65, luk = 60,
                hp = 130, mp = 30, move = 6, attackRange = 1,
                position = new HexCoord(13, 8), isBoss = true,
                skillIds = new List<string> { "rally", "fire_attack" }
            });
            for (int i = 0; i < 4; i++)
            {
                level7.enemies.Add(new EnemyConfig
                {
                    id = $"enemy_07_{i}", name = "夏军骑兵", unitClass = ClassType.Cavalry,
                    level = 8, str = 68, cmd = 55, @int = 30, agi = 55, luk = 35,
                    hp = 85, mp = 10, move = 6, attackRange = 1,
                    position = new HexCoord(15 + i, 6 + i / 2)
                });
            }
            for (int i = 0; i < 3; i++)
            {
                level7.enemies.Add(new EnemyConfig
                {
                    id = $"enemy_07_{i + 4}", name = "夏军步兵", unitClass = ClassType.Infantry,
                    level = 7, str = 58, cmd = 52, @int = 28, agi = 38, luk = 28,
                    hp = 70, mp = 0, move = 4, attackRange = 1,
                    position = new HexCoord(14, 9 + i)
                });
            }
            level7.enemies.Add(new EnemyConfig
            {
                id = "enemy_07_7", name = "夏军谋士", unitClass = ClassType.Strategist,
                level = 8, str = 30, cmd = 48, @int = 78, agi = 52, luk = 58,
                hp = 62, mp = 50, move = 5, attackRange = 2,
                position = new HexCoord(16, 11),
                skillIds = new List<string> { "water_attack", "confuse" }
            });
            level7.rewardGold = 400;
            level7.rewardEquipIds = new List<string> { "a005" };
            _levels["level_07"] = level7;

            // ========== 第8关：玄武门前夜 ==========
            var level8 = new LevelData
            {
                levelId = "level_08",
                name = "玄武门前夜",
                width = 18,
                height = 14,
                weather = WeatherType.Rain,
                wind = WindDirection.North,
                availableCharacters = new List<string> { "lishimin", "li_jing", "yuchi_jingde", "qin_qiong", "zhangsun_wuji", "chai_shao", "liu_hongji", "duan_zhixuan", "cheng_yaojin", "fang_xuanling", "du_ruhui", "pingyang_princess", "zhangsun_empress" },
                requiredCharacters = new List<string> { "lishimin" },
                victoryType = VictoryConditionType.DefeatAll,
                defeatTypes = new List<DefeatConditionType> { DefeatConditionType.PlayerDead }
            };
            // 皇宫地形
            for (int q = 8; q <= 14; q++)
                for (int r = 5; r <= 9; r++)
                    level8.terrainOverrides[new HexCoord(q, r)] = TerrainType.City;
            for (int q = 7; q <= 8; q++)
                for (int r = 6; r <= 8; r++)
                    level8.terrainOverrides[new HexCoord(q, r)] = TerrainType.Wall;
            // 敌军（精锐禁军）
            level8.enemies.Add(new EnemyConfig
            {
                id = "enemy_boss_08", name = "玄武禁军统领", unitClass = ClassType.HeavyInfantry,
                level = 15, str = 82, cmd = 80, @int = 50, agi = 55, luk = 60,
                hp = 150, mp = 30, move = 4, attackRange = 1,
                position = new HexCoord(12, 7), isBoss = true,
                skillIds = new List<string> { "rally" }
            });
            for (int i = 0; i < 4; i++)
            {
                level8.enemies.Add(new EnemyConfig
                {
                    id = $"enemy_08_{i}", name = "禁军重步", unitClass = ClassType.HeavyInfantry,
                    level = 10, str = 70, cmd = 72, @int = 28, agi = 35, luk = 30,
                    hp = 110, mp = 0, move = 3, attackRange = 1,
                    position = new HexCoord(10 + i % 2, 6 + i / 2)
                });
            }
            for (int i = 0; i < 2; i++)
            {
                level8.enemies.Add(new EnemyConfig
                {
                    id = $"enemy_08_{i + 4}", name = "禁军弓兵", unitClass = ClassType.Archer,
                    level = 10, str = 58, cmd = 50, @int = 35, agi = 48, luk = 32,
                    hp = 65, mp = 15, move = 4, attackRange = 3,
                    position = new HexCoord(13, 8 + i),
                    skillIds = new List<string> { "volley" }
                });
            }
            level8.enemies.Add(new EnemyConfig
            {
                id = "enemy_08_6", name = "禁军谋士", unitClass = ClassType.Strategist,
                level = 10, str = 30, cmd = 50, @int = 82, agi = 55, luk = 60,
                hp = 68, mp = 55, move = 5, attackRange = 2,
                position = new HexCoord(14, 6),
                skillIds = new List<string> { "fire_attack", "rock_slide", "heal" }
            });
            level8.rewardGold = 500;
            level8.rewardEquipIds = new List<string> { "w011" };
            _levels["level_08"] = level8;
        }
    }
}
