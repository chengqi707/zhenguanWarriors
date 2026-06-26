using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZhenguanWarriors.Core.Battle;
using ZhenguanWarriors.Core.Character;

namespace ZhenguanWarriors.Core.Level
{
    /// <summary>
    /// 关卡JSON序列化器——将LevelData导出/导入为JSON格式
    /// 支持 Assets/Data/Levels/ 下的JSON文件加载，也支持运行时构建
    /// </summary>
    public static class LevelJsonLoader
    {
        // ========== 序列化：LevelData -> JSON ==========

        /// <summary>将LevelData序列化为JSON字符串</summary>
        public static string SerializeLevel(LevelData level)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");

            sb.AppendLine($"  \"levelId\": \"{EscapeJson(level.levelId)}\",");
            sb.AppendLine($"  \"name\": \"{EscapeJson(level.name)}\",");
            sb.AppendLine($"  \"width\": {level.width},");
            sb.AppendLine($"  \"height\": {level.height},");
            sb.AppendLine($"  \"weather\": \"{level.weather}\",");
            sb.AppendLine($"  \"wind\": \"{level.wind}\",");
            sb.AppendLine($"  \"victoryType\": \"{level.victoryType}\",");
            sb.AppendLine($"  \"defendTurns\": {level.defendTurns},");
            sb.AppendLine($"  \"maxTurns\": {level.maxTurns},");

            // 目标Boss ID
            if (!string.IsNullOrEmpty(level.targetBossId))
                sb.AppendLine($"  \"targetBossId\": \"{EscapeJson(level.targetBossId)}\",");
            else
                sb.AppendLine($"  \"targetBossId\": \"\",");

            // 到达点坐标
            sb.AppendLine($"  \"reachPointQ\": {level.reachPoint.q},");
            sb.AppendLine($"  \"reachPointR\": {level.reachPoint.r},");

            // 地形覆盖
            sb.AppendLine("  \"terrainOverrides\": [");
            int idx = 0;
            foreach (var kv2 in level.terrainOverrides)
            {
                sb.Append($"    {{\"q\":{kv2.Key.q},\"r\":{kv2.Key.r},\"t\":\"{kv2.Value}\"}}");
                if (idx < level.terrainOverrides.Count - 1) sb.Append(",");
                sb.AppendLine();
                idx++;
            }
            sb.AppendLine("  ],");

            // 可用角色
            sb.Append("  \"availableCharacters\": [");
            sb.Append(string.Join(", ", level.availableCharacters.Select(id => $"\"{id}\"")));
            sb.AppendLine("],");

            // 必出角色
            sb.Append("  \"requiredCharacters\": [");
            sb.Append(string.Join(", ", level.requiredCharacters.Select(id => $"\"{id}\"")));
            sb.AppendLine("],");

            // 失败条件
            sb.Append("  \"defeatTypes\": [");
            sb.Append(string.Join(", ", level.defeatTypes.Select(dt => $"\"{dt}\"")));
            sb.AppendLine("],");

            // 敌人配置
            sb.AppendLine("  \"enemies\": [");
            for (int i = 0; i < level.enemies.Count; i++)
            {
                var e = level.enemies[i];
                sb.AppendLine("    {");
                sb.AppendLine($"      \"id\": \"{EscapeJson(e.id)}\",");
                sb.AppendLine($"      \"name\": \"{EscapeJson(e.name)}\",");
                sb.AppendLine($"      \"class\": \"{e.unitClass}\",");
                sb.AppendLine($"      \"level\": {e.level},");
                sb.AppendLine($"      \"str\": {e.str}, \"cmd\": {e.cmd}, \"int\": {e.@int}, \"agi\": {e.agi}, \"luk\": {e.luk},");
                sb.AppendLine($"      \"hp\": {e.hp}, \"mp\": {e.mp},");
                sb.AppendLine($"      \"move\": {e.move}, \"attackRange\": {e.attackRange},");
                sb.AppendLine($"      \"positionQ\": {e.position.q}, \"positionR\": {e.position.r},");
                sb.AppendLine($"      \"isBoss\": {(e.isBoss ? "true" : "false")},");

                if (e.skillIds != null && e.skillIds.Count > 0)
                {
                    sb.Append("      \"skills\": [");
                    sb.Append(string.Join(", ", e.skillIds.Select(s => $"\"{s}\"")));
                    sb.AppendLine("]");
                }
                else
                {
                    sb.AppendLine("      \"skills\": []");
                }

                sb.Append("    }");
                if (i < level.enemies.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.AppendLine("  ],");

            // 回合事件（剧情触发点）
            sb.AppendLine("  \"turnEvents\": {");
            int teIdx = 0;
            foreach (var kv in level.turnEvents)
            {
                sb.Append($"    \"{kv.Key}\": \"{EscapeJson(kv.Value)}\"");
                if (teIdx < level.turnEvents.Count - 1) sb.Append(",");
                sb.AppendLine();
                teIdx++;
            }
            sb.AppendLine("  }");

            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>将LevelData保存到JSON文件路径</summary>
        public static void SaveToFile(LevelData level, string filePath)
        {
            System.IO.File.WriteAllText(filePath, SerializeLevel(level));
        }

        // ========== 反序列化：JSON -> LevelData ==========

        /// <summary>从JSON字符串加载LevelData</summary>
        public static LevelData DeserializeLevel(string json)
        {
            var level = new LevelData();
            var lines = json.Split('\n');

            string GetField(string line)
            {
                var idx = line.IndexOf(':');
                if (idx < 0) return "";
                var val = line.Substring(idx + 1).Trim().TrimEnd(',');
                if (val.StartsWith("\"") && val.EndsWith("\""))
                    val = val.Substring(1, val.Length - 2);
                return val;
            }

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.StartsWith("\"levelId\"")) level.levelId = GetField(line);
                else if (line.StartsWith("\"name\"")) level.name = GetField(line);
                else if (line.StartsWith("\"width\"")) int.TryParse(GetField(line), out level.width);
                else if (line.StartsWith("\"height\"")) int.TryParse(GetField(line), out level.height);
                else if (line.StartsWith("\"weather\"")) Enum.TryParse(GetField(line), out level.weather);
                else if (line.StartsWith("\"wind\"")) Enum.TryParse(GetField(line), out level.wind);
                else if (line.StartsWith("\"victoryType\"")) Enum.TryParse(GetField(line), out level.victoryType);
                else if (line.StartsWith("\"defendTurns\"")) int.TryParse(GetField(line), out level.defendTurns);
                else if (line.StartsWith("\"maxTurns\"")) int.TryParse(GetField(line), out level.maxTurns);
                else if (line.StartsWith("\"targetBossId\"")) level.targetBossId = GetField(line);
                else if (line.StartsWith("\"reachPointQ\""))
                {
                    int q = 0; int.TryParse(GetField(line), out q);
                    level.reachPoint = new HexCoord(q, level.reachPoint.r);
                }
                else if (line.StartsWith("\"reachPointR\""))
                {
                    int r = 0; int.TryParse(GetField(line), out r);
                    level.reachPoint = new HexCoord(level.reachPoint.q, r);
                }
                // Parse terrain overrides (multi-line JSON array)
                else if (line.Contains("\"terrainOverrides\""))
                {
                    i++; // skip to first entry
                    while (i < lines.Length && !lines[i].Trim().StartsWith("]"))
                    {
                        var tLine = lines[i].Trim().TrimEnd(',').TrimEnd('}').TrimStart('{');
                        int tq = 0, tr = 0;
                        string tt = "Plain";
                        var parts = tLine.Split(',');
                        foreach (var p in parts)
                        {
                            var kv = p.Split(':');
                            if (kv.Length == 2)
                            {
                                var key = kv[0].Trim().Trim('"');
                                var val = kv[1].Trim().Trim('"').TrimEnd('}');
                                if (key == "q") int.TryParse(val, out tq);
                                else if (key == "r") int.TryParse(val, out tr);
                                else if (key == "t" && Enum.TryParse<TerrainType>(val, out var tt2)) tt = val;
                            }
                        }
                        if (Enum.TryParse<TerrainType>(tt, out var terrain))
                            level.terrainOverrides[new HexCoord(tq, tr)] = terrain;
                        i++;
                    }
                }
                // Parse enemies
                else if (line.Contains("\"enemies\""))
                {
                    i++; // skip [
                    while (i < lines.Length && !lines[i].Trim().StartsWith("]"))
                    {
                        if (lines[i].Trim() == "{")
                        {
                            var enemy = new EnemyConfig();
                            i++;
                            while (i < lines.Length && !lines[i].Trim().StartsWith("}"))
                            {
                                var eLine = lines[i].Trim().TrimEnd(',');
                                var colon = eLine.IndexOf(':');
                                if (colon > 0)
                                {
                                    var key = eLine.Substring(0, colon).Trim().Trim('"');
                                    var val = eLine.Substring(colon + 1).Trim().TrimEnd(',').Trim('"');
                                    switch (key)
                                    {
                                        case "id": enemy.id = val; break;
                                        case "name": enemy.name = val; break;
                                        case "class": Enum.TryParse<ClassType>(val, out enemy.unitClass); break;
                                        case "level": int.TryParse(val, out enemy.level); break;
                                        case "str": int.TryParse(val, out enemy.str); break;
                                        case "cmd": int.TryParse(val, out enemy.cmd); break;
                                        case "int": int.TryParse(val, out enemy.@int); break;
                                        case "agi": int.TryParse(val, out enemy.agi); break;
                                        case "luk": int.TryParse(val, out enemy.luk); break;
                                        case "hp": int.TryParse(val, out enemy.hp); break;
                                        case "mp": int.TryParse(val, out enemy.mp); break;
                                        case "move": int.TryParse(val, out enemy.move); break;
                                        case "attackRange": int.TryParse(val, out enemy.attackRange); break;
                                        case "positionQ": int.TryParse(val, out int pq); enemy.position = new HexCoord(pq, enemy.position.r); break;
                                        case "positionR": int.TryParse(val, out int pr); enemy.position = new HexCoord(enemy.position.q, pr); break;
                                        case "isBoss": enemy.isBoss = val == "true"; break;
                                        case "skills":
                                            enemy.skillIds = new List<string>();
                                            if (val.StartsWith("["))
                                            {
                                                var skillPart = eLine.Substring(colon + 1).Trim();
                                                var skills = skillPart.TrimStart('[').TrimEnd(']', ',').Trim('"');
                                                if (!string.IsNullOrEmpty(skills))
                                                    enemy.skillIds.AddRange(skills.Split(new[] { "\",\"" }, StringSplitOptions.None));
                                            }
                                            break;
                                    }
                                }
                                i++;
                            }
                            level.enemies.Add(enemy);
                        }
                        i++;
                    }
                }
                // Parse arrays (availableCharacters, requiredCharacters, defeatTypes)
                else if (line.Contains("\"availableCharacters\""))
                {
                    var arr = ParseJsonArray(lines, ref i);
                    level.availableCharacters = arr;
                }
                else if (line.Contains("\"requiredCharacters\""))
                {
                    var arr = ParseJsonArray(lines, ref i);
                    level.requiredCharacters = arr;
                }
                else if (line.Contains("\"defeatTypes\""))
                {
                    var arr = ParseJsonArray(lines, ref i);
                    foreach (var s in arr)
                        if (Enum.TryParse<DefeatConditionType>(s, out var dt))
                            level.defeatTypes.Add(dt);
                }
                // 回合事件（剧情触发点）
                else if (line.Contains("\"turnEvents\""))
                {
                    i++; // skip to first entry
                    while (i < lines.Length && !lines[i].Trim().StartsWith("}"))
                    {
                        var teLine = lines[i].Trim().TrimEnd(',').TrimEnd('}');
                        var colonIdx = teLine.IndexOf(':');
                        if (colonIdx > 0)
                        {
                            var keyStr = teLine.Substring(0, colonIdx).Trim().Trim('"');
                            var valStr = teLine.Substring(colonIdx + 1).Trim().Trim('"').TrimEnd(',');
                            if (int.TryParse(keyStr, out int turn))
                                level.turnEvents[turn] = valStr;
                        }
                        i++;
                    }
                }
            }

            return level;
        }

        /// <summary>从JSON文件加载LevelData</summary>
        public static LevelData LoadFromFile(string filePath)
        {
            if (!System.IO.File.Exists(filePath)) return null;
            var json = System.IO.File.ReadAllText(filePath);
            return DeserializeLevel(json);
        }

        /// <summary>从Resources路径加载关卡JSON（Assets/Data/Levels/level_XX.json）</summary>
        public static LevelData LoadFromResources(string levelId)
        {
            try
            {
                var textAsset = UnityEngine.Resources.Load<UnityEngine.TextAsset>($"Data/Levels/{levelId}");
                if (textAsset != null)
                    return DeserializeLevel(textAsset.text);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning($"[LevelJsonLoader] 加载 {levelId} 失败: {e.Message}");
            }
            return null;
        }

        // ========== 辅助 ==========

        private static string EscapeJson(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");

        /// <summary>解析JSON数组 ["a", "b", "c"]</summary>
        private static List<string> ParseJsonArray(string[] lines, ref int startIdx)
        {
            var result = new List<string>();
            int i = startIdx;
            while (i < lines.Length)
            {
                var line = lines[i].Trim().TrimEnd(',');
                if (line.StartsWith("["))
                {
                    var content = line.TrimStart('[').TrimEnd(']', ',').Trim('"');
                    if (!string.IsNullOrEmpty(content))
                        result.AddRange(content.Split(new[] { "\", \"" }, StringSplitOptions.None));
                    if (line.Contains("]")) break;
                }
                else if (line.StartsWith("\"") && !line.Contains("["))
                {
                    var val = line.Trim('"');
                    result.Add(val);
                }
                else if (line.Contains("]"))
                {
                    var content = line.TrimEnd(']', ',').Trim('"');
                    if (!string.IsNullOrEmpty(content))
                        result.Add(content);
                    break;
                }
                i++;
            }
            startIdx = i;
            return result;
        }
    }
}
