// Standalone level JSON exporter - compile with: csc export_levels_json.cs
// Run: export_levels_json.exe
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// ===== Minimal type stubs matching the Unity project =====
public enum ClassType { Infantry, HeavyInfantry, Cavalry, Archer, Siege, Strategist }
public enum TerrainType { Plain, Forest, Mountain, Water, City, Bridge, Wall }
public enum WeatherType { Sunny, Rain, Snow, Fog, Windy }
public enum WindDirection { None, North, South, East, West }
public enum VictoryConditionType { DefeatAll, DefeatBoss, DefendTurns, ReachPoint, Survive }
public enum DefeatConditionType { PlayerDead, AllDead, BossReachPoint, TurnLimit }

public struct HexCoord
{
    public int q, r;
    public HexCoord(int q, int r) { this.q = q; this.r = r; }
}

public class EnemyConfig
{
    public string id, name; public ClassType unitClass; public int level;
    public int str, cmd, @int, agi, luk; public int hp, mp;
    public int move, attackRange; public HexCoord position;
    public List<string> skillIds; public bool isBoss;
}

public class LevelData
{
    public string levelId, name; public int width, height;
    public WeatherType weather; public WindDirection wind;
    public Dictionary<HexCoord, TerrainType> terrainOverrides = new();
    public List<string> availableCharacters = new(), requiredCharacters = new();
    public List<EnemyConfig> enemies = new();
    public VictoryConditionType victoryType; public string targetBossId;
    public int defendTurns; public HexCoord reachPoint;
    public List<DefeatConditionType> defeatTypes = new();
    public int maxTurns;
}

public class Program
{
    public static void Main()
    {
        var levels = BuildAll();
        string dir = "Assets/Data/Levels";
        Directory.CreateDirectory(dir);
        int count = 0;
        foreach (var kv in levels)
        {
            string json = Serialize(kv.Value);
            File.WriteAllText(Path.Combine(dir, kv.Key + ".json"), json);
            Console.WriteLine("  " + kv.Key + ".json");
            count++;
        }
        Console.WriteLine("\nExported " + count + " files to " + dir + "/");
    }

    static Dictionary<string, LevelData> BuildAll()
    {
        var all = new Dictionary<string, LevelData>();

        // Level 1
        var l1 = new LevelData
        {
            levelId = "level_01", name = "晋阳举义", width = 16, height = 12,
            weather = WeatherType.Sunny, wind = WindDirection.None,
            availableCharacters = new List<string> { "lishimin", "li_jing", "zhangsun_wuji", "chai_shao", "liu_hongji" },
            requiredCharacters = new List<string> { "lishimin" },
            victoryType = VictoryConditionType.DefeatAll,
            defeatTypes = new List<DefeatConditionType> { DefeatConditionType.PlayerDead }
        };
        for (int q = 6; q <= 9; q++)
            for (int r = 4; r <= 7; r++)
                l1.terrainOverrides[new HexCoord(q, r)] = TerrainType.Forest;
        l1.enemies.Add(new EnemyConfig { id = "enemy_boss_01", name = "隋军校尉", unitClass = ClassType.Cavalry, level = 3, str = 55, cmd = 45, @int = 25, agi = 35, luk = 25, hp = 55, mp = 10, move = 5, attackRange = 1, position = new HexCoord(12, 5), isBoss = true });
        for (int i = 0; i < 4; i++)
            l1.enemies.Add(new EnemyConfig { id = "enemy_01_" + i, name = "隋军步兵", unitClass = ClassType.Infantry, level = 2, str = 45, cmd = 40, @int = 20, agi = 30, luk = 20, hp = 45, mp = 0, move = 4, attackRange = 1, position = new HexCoord(10 + i % 2, 3 + i / 2) });
        all["level_01"] = l1;

        // Level 2
        var l2 = new LevelData
        {
            levelId = "level_02", name = "霍邑攻坚", width = 20, height = 14,
            weather = WeatherType.Rain, wind = WindDirection.North,
            availableCharacters = new List<string> { "lishimin", "zhangsun_wuji", "chai_shao", "liu_hongji", "yin_kaishan", "duan_zhixuan" },
            requiredCharacters = new List<string> { "lishimin" },
            victoryType = VictoryConditionType.DefeatBoss, targetBossId = "enemy_boss_02",
            defeatTypes = new List<DefeatConditionType> { DefeatConditionType.PlayerDead, DefeatConditionType.TurnLimit },
            maxTurns = 20
        };
        for (int q = 14; q <= 17; q++) for (int r = 5; r <= 9; r++) l2.terrainOverrides[new HexCoord(q, r)] = TerrainType.Mountain;
        for (int q = 16; q <= 18; q++) for (int r = 6; r <= 8; r++) l2.terrainOverrides[new HexCoord(q, r)] = TerrainType.City;
        l2.enemies.Add(new EnemyConfig { id = "enemy_boss_02", name = "宋老生", unitClass = ClassType.Cavalry, level = 5, str = 70, cmd = 55, @int = 35, agi = 50, luk = 40, hp = 90, mp = 20, move = 5, attackRange = 1, position = new HexCoord(16, 7), isBoss = true });
        l2.enemies.Add(new EnemyConfig { id = "enemy_02_0", name = "隋军骑兵", unitClass = ClassType.Cavalry, level = 4, str = 55, cmd = 45, @int = 30, agi = 45, luk = 30, hp = 60, mp = 15, move = 5, attackRange = 1, position = new HexCoord(15, 6) });
        for (int i = 0; i < 5; i++) l2.enemies.Add(new EnemyConfig { id = "enemy_02_" + (i+1), name = "隋军步兵", unitClass = ClassType.Infantry, level = 3, str = 48, cmd = 42, @int = 22, agi = 32, luk = 22, hp = 50, mp = 0, move = 4, attackRange = 1, position = new HexCoord(13 + i % 3, 4 + i / 3) });
        all["level_02"] = l2;

        // Level 3
        var l3 = new LevelData
        {
            levelId = "level_03", name = "直取长安", width = 24, height = 16,
            weather = WeatherType.Sunny, wind = WindDirection.None,
            availableCharacters = new List<string> { "lishimin", "zhangsun_wuji", "chai_shao", "liu_hongji", "yin_kaishan", "duan_zhixuan", "pingyang_princess", "fang_xuanling" },
            requiredCharacters = new List<string> { "lishimin" },
            victoryType = VictoryConditionType.DefeatBoss, targetBossId = "enemy_boss_03",
            defeatTypes = new List<DefeatConditionType> { DefeatConditionType.PlayerDead }
        };
        for (int q = 18; q <= 22; q++) for (int r = 6; r <= 10; r++) l3.terrainOverrides[new HexCoord(q, r)] = TerrainType.City;
        for (int q = 17; q <= 18; q++) for (int r = 7; r <= 9; r++) l3.terrainOverrides[new HexCoord(q, r)] = TerrainType.Water;
        l3.enemies.Add(new EnemyConfig { id = "enemy_boss_03", name = "长安守将", unitClass = ClassType.HeavyInfantry, level = 8, str = 65, cmd = 75, @int = 30, agi = 40, luk = 35, hp = 110, mp = 15, move = 3, attackRange = 1, position = new HexCoord(20, 8), isBoss = true });
        for (int i = 0; i < 3; i++) l3.enemies.Add(new EnemyConfig { id = "enemy_03_" + i, name = "隋军重步", unitClass = ClassType.HeavyInfantry, level = 5, str = 55, cmd = 60, @int = 20, agi = 28, luk = 20, hp = 75, mp = 0, move = 3, attackRange = 1, position = new HexCoord(18 + i, 7) });
        for (int i = 0; i < 3; i++) l3.enemies.Add(new EnemyConfig { id = "enemy_03_" + (i+3), name = "隋军弓兵", unitClass = ClassType.Archer, level = 5, str = 45, cmd = 38, @int = 30, agi = 40, luk = 28, hp = 50, mp = 10, move = 4, attackRange = 2, position = new HexCoord(19, 6 + i), skillIds = new List<string> { "volley" } });
        all["level_03"] = l3;

        // Level 4
        var l4 = new LevelData
        {
            levelId = "level_04", name = "浅水原之战", width = 20, height = 14,
            weather = WeatherType.Windy, wind = WindDirection.North,
            availableCharacters = new List<string> { "lishimin", "li_jing", "zhangsun_wuji", "chai_shao", "liu_hongji", "duan_zhixuan", "qin_qiong", "yuchi_jingde", "fang_xuanling", "hou_junji" },
            requiredCharacters = new List<string> { "lishimin" },
            victoryType = VictoryConditionType.DefeatBoss, targetBossId = "enemy_boss_04",
            defeatTypes = new List<DefeatConditionType> { DefeatConditionType.PlayerDead }
        };
        for (int q = 8; q <= 12; q++) for (int r = 5; r <= 8; r++) l4.terrainOverrides[new HexCoord(q, r)] = TerrainType.Plain;
        for (int q = 3; q <= 5; q++) for (int r = 2; r <= 4; r++) l4.terrainOverrides[new HexCoord(q, r)] = TerrainType.Forest;
        l4.enemies.Add(new EnemyConfig { id = "enemy_boss_04", name = "薛举", unitClass = ClassType.Cavalry, level = 7, str = 75, cmd = 65, @int = 40, agi = 60, luk = 45, hp = 100, mp = 20, move = 6, attackRange = 1, position = new HexCoord(16, 7), isBoss = true, skillIds = new List<string> { "rally" } });
        for (int i = 0; i < 4; i++) l4.enemies.Add(new EnemyConfig { id = "enemy_04_" + i, name = "西秦骑兵", unitClass = ClassType.Cavalry, level = 5, str = 60, cmd = 50, @int = 25, agi = 50, luk = 30, hp = 70, mp = 10, move = 5, attackRange = 1, position = new HexCoord(14 + i % 2, 5 + i / 2) });
        l4.enemies.Add(new EnemyConfig { id = "enemy_04_4", name = "西秦谋士", unitClass = ClassType.Strategist, level = 6, str = 25, cmd = 40, @int = 75, agi = 50, luk = 55, hp = 60, mp = 50, move = 5, attackRange = 2, position = new HexCoord(15, 9), skillIds = new List<string> { "fire_attack", "confuse" } });
        all["level_04"] = l4;

        // Level 5
        var l5 = new LevelData
        {
            levelId = "level_05", name = "柏壁之战", width = 22, height = 16,
            weather = WeatherType.Snow, wind = WindDirection.None,
            availableCharacters = new List<string> { "lishimin", "li_jing", "yuchi_jingde", "qin_qiong", "zhangsun_wuji", "chai_shao", "liu_hongji", "yin_kaishan", "duan_zhixuan", "cheng_yaojin", "hou_junji" },
            requiredCharacters = new List<string> { "lishimin" },
            victoryType = VictoryConditionType.DefendTurns, defendTurns = 10,
            defeatTypes = new List<DefeatConditionType> { DefeatConditionType.AllDead, DefeatConditionType.TurnLimit },
            maxTurns = 15
        };
        for (int q = 4; q <= 10; q++) for (int r = 6; r <= 10; r++) l5.terrainOverrides[new HexCoord(q, r)] = TerrainType.City;
        for (int q = 3; q <= 4; q++) for (int r = 6; r <= 10; r++) l5.terrainOverrides[new HexCoord(q, r)] = TerrainType.Wall;
        for (int q = 12; q <= 18; q++) for (int r = 4; r <= 8; r++) l5.terrainOverrides[new HexCoord(q, r)] = TerrainType.Mountain;
        for (int i = 0; i < 6; i++) l5.enemies.Add(new EnemyConfig { id = "enemy_05_" + i, name = "刘军步兵", unitClass = ClassType.Infantry, level = 6, str = 55, cmd = 50, @int = 25, agi = 35, luk = 25, hp = 65, mp = 0, move = 4, attackRange = 1, position = new HexCoord(14 + i % 3, 4 + i / 3) });
        for (int i = 0; i < 3; i++) l5.enemies.Add(new EnemyConfig { id = "enemy_05_" + (i+6), name = "刘军弓兵", unitClass = ClassType.Archer, level = 6, str = 50, cmd = 42, @int = 30, agi = 45, luk = 28, hp = 55, mp = 10, move = 4, attackRange = 2, position = new HexCoord(16 + i, 10), skillIds = new List<string> { "volley" } });
        all["level_05"] = l5;

        // Level 6
        var l6 = new LevelData
        {
            levelId = "level_06", name = "洛阳攻坚战", width = 24, height = 18,
            weather = WeatherType.Sunny, wind = WindDirection.None,
            availableCharacters = new List<string> { "lishimin", "li_jing", "yuchi_jingde", "qin_qiong", "zhangsun_wuji", "chai_shao", "liu_hongji", "yin_kaishan", "duan_zhixuan", "cheng_yaojin", "hou_junji", "fang_xuanling" },
            requiredCharacters = new List<string> { "lishimin", "yin_kaishan" },
            victoryType = VictoryConditionType.DefeatBoss, targetBossId = "enemy_boss_06",
            defeatTypes = new List<DefeatConditionType> { DefeatConditionType.PlayerDead, DefeatConditionType.TurnLimit },
            maxTurns = 25
        };
        for (int q = 16; q <= 22; q++) for (int r = 7; r <= 12; r++) l6.terrainOverrides[new HexCoord(q, r)] = TerrainType.City;
        for (int q = 14; q <= 16; q++) for (int r = 8; r <= 11; r++) l6.terrainOverrides[new HexCoord(q, r)] = TerrainType.Water;
        l6.enemies.Add(new EnemyConfig { id = "enemy_boss_06", name = "王世充", unitClass = ClassType.HeavyInfantry, level = 10, str = 78, cmd = 82, @int = 60, agi = 50, luk = 55, hp = 140, mp = 30, move = 4, attackRange = 1, position = new HexCoord(20, 10), isBoss = true, skillIds = new List<string> { "rally", "confuse" } });
        for (int i = 0; i < 5; i++) l6.enemies.Add(new EnemyConfig { id = "enemy_06_" + i, name = "郑军重步", unitClass = ClassType.HeavyInfantry, level = 7, str = 62, cmd = 65, @int = 25, agi = 30, luk = 25, hp = 90, mp = 0, move = 3, attackRange = 1, position = new HexCoord(17 + i, 8) });
        for (int i = 0; i < 3; i++) l6.enemies.Add(new EnemyConfig { id = "enemy_06_" + (i+5), name = "郑军弓兵", unitClass = ClassType.Archer, level = 7, str = 52, cmd = 45, @int = 30, agi = 42, luk = 28, hp = 58, mp = 10, move = 4, attackRange = 3, position = new HexCoord(18 + i, 12), skillIds = new List<string> { "volley" } });
        l6.enemies.Add(new EnemyConfig { id = "enemy_06_8", name = "郑军谋士", unitClass = ClassType.Strategist, level = 8, str = 28, cmd = 45, @int = 80, agi = 55, luk = 60, hp = 65, mp = 55, move = 5, attackRange = 2, position = new HexCoord(21, 7), skillIds = new List<string> { "fire_attack", "rock_slide" } });
        all["level_06"] = l6;

        // Level 7
        var l7 = new LevelData
        {
            levelId = "level_07", name = "虎牢关之战", width = 24, height = 16,
            weather = WeatherType.Sunny, wind = WindDirection.East,
            availableCharacters = new List<string> { "lishimin", "li_jing", "yuchi_jingde", "qin_qiong", "zhangsun_wuji", "chai_shao", "liu_hongji", "yin_kaishan", "duan_zhixuan", "cheng_yaojin", "hou_junji", "pingyang_princess" },
            requiredCharacters = new List<string> { "lishimin", "qin_qiong", "yuchi_jingde" },
            victoryType = VictoryConditionType.DefeatBoss, targetBossId = "enemy_boss_07",
            defeatTypes = new List<DefeatConditionType> { DefeatConditionType.PlayerDead }
        };
        for (int q = 12; q <= 14; q++) for (int r = 5; r <= 10; r++) l7.terrainOverrides[new HexCoord(q, r)] = TerrainType.City;
        for (int q = 10; q <= 11; q++) for (int r = 5; r <= 10; r++) l7.terrainOverrides[new HexCoord(q, r)] = TerrainType.Wall;
        for (int q = 5; q <= 9; q++) for (int r = 3; r <= 6; r++) l7.terrainOverrides[new HexCoord(q, r)] = TerrainType.Mountain;
        for (int q = 16; q <= 20; q++) for (int r = 8; r <= 12; r++) l7.terrainOverrides[new HexCoord(q, r)] = TerrainType.Forest;
        l7.enemies.Add(new EnemyConfig { id = "enemy_boss_07", name = "窦建德", unitClass = ClassType.Cavalry, level = 12, str = 85, cmd = 78, @int = 55, agi = 65, luk = 60, hp = 130, mp = 30, move = 6, attackRange = 1, position = new HexCoord(13, 8), isBoss = true, skillIds = new List<string> { "rally", "fire_attack" } });
        for (int i = 0; i < 4; i++) l7.enemies.Add(new EnemyConfig { id = "enemy_07_" + i, name = "夏军骑兵", unitClass = ClassType.Cavalry, level = 8, str = 68, cmd = 55, @int = 30, agi = 55, luk = 35, hp = 85, mp = 10, move = 6, attackRange = 1, position = new HexCoord(15 + i, 6 + i / 2) });
        for (int i = 0; i < 3; i++) l7.enemies.Add(new EnemyConfig { id = "enemy_07_" + (i+4), name = "夏军步兵", unitClass = ClassType.Infantry, level = 7, str = 58, cmd = 52, @int = 28, agi = 38, luk = 28, hp = 70, mp = 0, move = 4, attackRange = 1, position = new HexCoord(14, 9 + i) });
        l7.enemies.Add(new EnemyConfig { id = "enemy_07_7", name = "夏军谋士", unitClass = ClassType.Strategist, level = 8, str = 30, cmd = 48, @int = 78, agi = 52, luk = 58, hp = 62, mp = 50, move = 5, attackRange = 2, position = new HexCoord(16, 11), skillIds = new List<string> { "water_attack", "confuse" } });
        all["level_07"] = l7;

        // Level 8
        var l8 = new LevelData
        {
            levelId = "level_08", name = "玄武门前夜", width = 18, height = 14,
            weather = WeatherType.Rain, wind = WindDirection.North,
            availableCharacters = new List<string> { "lishimin", "li_jing", "yuchi_jingde", "qin_qiong", "zhangsun_wuji", "chai_shao", "liu_hongji", "duan_zhixuan", "cheng_yaojin", "fang_xuanling", "du_ruhui", "pingyang_princess", "zhangsun_empress" },
            requiredCharacters = new List<string> { "lishimin" },
            victoryType = VictoryConditionType.DefeatAll,
            defeatTypes = new List<DefeatConditionType> { DefeatConditionType.PlayerDead }
        };
        for (int q = 8; q <= 14; q++) for (int r = 5; r <= 9; r++) l8.terrainOverrides[new HexCoord(q, r)] = TerrainType.City;
        for (int q = 7; q <= 8; q++) for (int r = 6; r <= 8; r++) l8.terrainOverrides[new HexCoord(q, r)] = TerrainType.Wall;
        l8.enemies.Add(new EnemyConfig { id = "enemy_boss_08", name = "玄武禁军统领", unitClass = ClassType.HeavyInfantry, level = 15, str = 82, cmd = 80, @int = 50, agi = 55, luk = 60, hp = 150, mp = 30, move = 4, attackRange = 1, position = new HexCoord(12, 7), isBoss = true, skillIds = new List<string> { "rally" } });
        for (int i = 0; i < 4; i++) l8.enemies.Add(new EnemyConfig { id = "enemy_08_" + i, name = "禁军重步", unitClass = ClassType.HeavyInfantry, level = 10, str = 70, cmd = 72, @int = 28, agi = 35, luk = 30, hp = 110, mp = 0, move = 3, attackRange = 1, position = new HexCoord(10 + i % 2, 6 + i / 2) });
        for (int i = 0; i < 2; i++) l8.enemies.Add(new EnemyConfig { id = "enemy_08_" + (i+4), name = "禁军弓兵", unitClass = ClassType.Archer, level = 10, str = 58, cmd = 50, @int = 35, agi = 48, luk = 32, hp = 65, mp = 15, move = 4, attackRange = 3, position = new HexCoord(13, 8 + i), skillIds = new List<string> { "volley" } });
        l8.enemies.Add(new EnemyConfig { id = "enemy_08_6", name = "禁军谋士", unitClass = ClassType.Strategist, level = 10, str = 30, cmd = 50, @int = 82, agi = 55, luk = 60, hp = 68, mp = 55, move = 5, attackRange = 2, position = new HexCoord(14, 6), skillIds = new List<string> { "fire_attack", "rock_slide", "heal" } });
        all["level_08"] = l8;

        return all;
    }

    // ===== JSON serializer =====
    static string Serialize(LevelData level)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        Field(sb, "levelId", level.levelId, true);
        Field(sb, "name", level.name, true);
        Field(sb, "width", level.width, true);
        Field(sb, "height", level.height, true);
        Field(sb, "weather", level.weather.ToString(), true);
        Field(sb, "wind", level.wind.ToString(), true);
        Field(sb, "victoryType", level.victoryType.ToString(), true);
        Field(sb, "defendTurns", level.defendTurns, true);
        Field(sb, "maxTurns", level.maxTurns, true);
        Field(sb, "targetBossId", level.targetBossId ?? "", true);
        Field(sb, "reachPointQ", level.reachPoint.q, true);
        Field(sb, "reachPointR", level.reachPoint.r, true);

        sb.AppendLine("  \"terrainOverrides\": [");
        int idx = 0;
        foreach (var kv in level.terrainOverrides)
        {
            sb.Append("    {\"q\":" + kv.Key.q + ",\"r\":" + kv.Key.r + ",\"t\":\"" + kv.Value + "\"}");
            if (idx < level.terrainOverrides.Count - 1) sb.Append(",");
            sb.AppendLine();
            idx++;
        }
        sb.AppendLine("  ],");

        ArrayField(sb, "availableCharacters", level.availableCharacters, true);
        ArrayField(sb, "requiredCharacters", level.requiredCharacters, true);

        var defeatTypeStrs = level.defeatTypes.ConvertAll(d => d.ToString());
        ArrayField(sb, "defeatTypes", defeatTypeStrs, true);

        sb.AppendLine("  \"enemies\": [");
        for (int i = 0; i < level.enemies.Count; i++)
        {
            var e = level.enemies[i];
            sb.AppendLine("    {");
            Field(sb, "id", e.id, true);
            Field(sb, "name", e.name, true);
            Field(sb, "class", e.unitClass.ToString(), true);
            Field(sb, "level", e.level, true);
            sb.AppendLine("      \"str\": " + e.str + ", \"cmd\": " + e.cmd + ", \"int\": " + e.@int + ", \"agi\": " + e.agi + ", \"luk\": " + e.luk + ",");
            sb.AppendLine("      \"hp\": " + e.hp + ", \"mp\": " + e.mp + ",");
            Field(sb, "move", e.move, true);
            Field(sb, "attackRange", e.attackRange, true);
            Field(sb, "positionQ", e.position.q, true);
            Field(sb, "positionR", e.position.r, true);
            Field(sb, "isBoss", e.isBoss ? "true" : "false", true);
            sb.Append("      \"skills\": [");
            if (e.skillIds != null && e.skillIds.Count > 0)
            {
                for (int j = 0; j < e.skillIds.Count; j++)
                {
                    if (j > 0) sb.Append(", ");
                    sb.Append("\"" + e.skillIds[j] + "\"");
                }
            }
            sb.AppendLine("]");
            sb.Append("    }");
            if (i < level.enemies.Count - 1) sb.Append(",");
            sb.AppendLine();
        }
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        return sb.ToString();
    }

    static void Field(StringBuilder sb, string key, string val, bool comma)
    {
        sb.Append("  \"" + key + "\": \"" + Escape(val) + "\"");
        if (comma) sb.Append(",");
        sb.AppendLine();
    }
    static void Field(StringBuilder sb, string key, int val, bool comma)
    {
        sb.Append("  \"" + key + "\": " + val);
        if (comma) sb.Append(",");
        sb.AppendLine();
    }
    static void ArrayField(StringBuilder sb, string key, List<string> vals, bool comma)
    {
        sb.Append("  \"" + key + "\": [");
        for (int i = 0; i < vals.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append("\"" + vals[i] + "\"");
        }
        sb.Append("]");
        if (comma) sb.Append(",");
        sb.AppendLine();
    }
    static string Escape(string s) { return s.Replace("\\", "\\\\").Replace("\"", "\\\""); }
}
