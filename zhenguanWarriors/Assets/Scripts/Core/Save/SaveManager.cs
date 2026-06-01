using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ZhenguanWarriors.Core.Save
{
    /// <summary>
    /// 存档管理器——JSON序列化存档，支持5个手动槽位+1自动存档
    /// 存档路径: Application.persistentDataPath/saves/
    /// </summary>
    public static class SaveManager
    {
        public const int MAX_SLOTS = 5;
        private const string SAVE_DIR = "saves";
        private const string AUTO_SAVE = "auto";
        private const string SAVE_EXT = ".json";
        private const int SAVE_VERSION = 1;

        private static string SavePath => Path.Combine(Application.persistentDataPath, SAVE_DIR);

        static SaveManager()
        {
            if (!Directory.Exists(SavePath))
                Directory.CreateDirectory(SavePath);
        }

        // ========== 存档 ==========

        /// <summary>保存到指定槽位（0-4）</summary>
        public static bool SaveToSlot(int slot, SaveData data)
        {
            if (slot < 0 || slot >= MAX_SLOTS) return false;
            try
            {
                data.saveTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                data.version = SAVE_VERSION;
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(GetSlotPath(slot), json);
                Debug.Log($"[存档] 已保存到槽位 {slot + 1}: {data.levelName} Lv{data.avgLevel}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[存档] 保存失败: {e.Message}");
                return false;
            }
        }

        /// <summary>自动存档</summary>
        public static bool AutoSave(SaveData data)
        {
            try
            {
                data.saveTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                data.version = SAVE_VERSION;
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(GetAutoSavePath(), json);
                Debug.Log($"[存档] 自动存档: {data.levelName}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[存档] 自动存档失败: {e.Message}");
                return false;
            }
        }

        // ========== 读档 ==========

        /// <summary>从指定槽位读档</summary>
        public static SaveData LoadFromSlot(int slot)
        {
            if (slot < 0 || slot >= MAX_SLOTS) return null;
            string path = GetSlotPath(slot);
            return LoadFromPath(path);
        }

        /// <summary>加载最新的存档（优先手动，其次自动）</summary>
        public static SaveData LoadLatest()
        {
            // 优先手动槽位
            SaveData latest = null;
            DateTime latestTime = DateTime.MinValue;

            for (int i = 0; i < MAX_SLOTS; i++)
            {
                var data = LoadFromSlot(i);
                if (data != null && DateTime.TryParse(data.saveTime, out var t) && t > latestTime)
                {
                    latest = data;
                    latestTime = t;
                }
            }

            // 其次自动存档
            var auto = LoadAutoSave();
            if (auto != null && DateTime.TryParse(auto.saveTime, out var at) && at > latestTime)
            {
                latest = auto;
            }

            return latest;
        }

        /// <summary>加载自动存档</summary>
        public static SaveData LoadAutoSave()
        {
            return LoadFromPath(GetAutoSavePath());
        }

        /// <summary>获取槽位元数据（不反序列化完整角色数据）</summary>
        public static SaveData GetSlotMeta(int slot)
        {
            // 简化实现：直接完整加载
            return LoadFromSlot(slot);
        }

        // ========== 查询 ==========

        /// <summary>是否有任何存档</summary>
        public static bool HasAnySave()
        {
            for (int i = 0; i < MAX_SLOTS; i++)
            {
                if (File.Exists(GetSlotPath(i))) return true;
            }
            return File.Exists(GetAutoSavePath());
        }

        /// <summary>清空所有存档</summary>
        public static void DeleteAllSaves()
        {
            try
            {
                if (Directory.Exists(SavePath))
                    Directory.Delete(SavePath, true);
                Directory.CreateDirectory(SavePath);
                Debug.Log("[存档] 所有存档已清空");
            }
            catch (Exception e)
            {
                Debug.LogError($"[存档] 清空失败: {e.Message}");
            }
        }

        /// <summary>重置新游戏（清空存档+解锁第一关）</summary>
        public static void ResetNewGame()
        {
            DeleteAllSaves();
        }

        // ========== 从BattleUnit构建SaveData ==========

        /// <summary>从当前游戏状态构建存档数据</summary>
        public static SaveData BuildSaveData(
            string levelId, int levelIndex, string levelName,
            List<BattleUnit> playerParty,
            HashSet<string> unlockedLevels)
        {
            var data = SaveData.CreateNew();
            data.levelId = levelId;
            data.levelIndex = levelIndex;
            data.levelName = levelName;
            data.currentLevelId = levelId;
            data.unlockedLevels = new List<string>(unlockedLevels);

            foreach (var unit in playerParty)
            {
                data.characters.Add(new CharacterSaveData
                {
                    id = unit.Id,
                    name = unit.Name,
                    level = unit.Level,
                    experience = unit.Experience,
                    baseStr = unit.BaseStrength,
                    baseCmd = unit.BaseCommand,
                    baseInt = unit.BaseIntelligence,
                    baseAgi = unit.BaseAgility,
                    baseLuk = unit.BaseLuck,
                    strGrowth = unit.StrGrowth,
                    cmdGrowth = unit.CmdGrowth,
                    intGrowth = unit.IntGrowth,
                    agiGrowth = unit.AgiGrowth,
                    lukGrowth = unit.LukGrowth,
                    skillIds = new List<string>(unit.SkillIds),
                    weaponId = unit.WeaponId,
                    armorId = unit.ArmorId,
                    trinketId = unit.TrinketId
                });
            }

            return data;
        }

        // ========== 辅助 ==========

        private static string GetSlotPath(int slot) =>
            Path.Combine(SavePath, $"slot_{slot}{SAVE_EXT}");

        private static string GetAutoSavePath() =>
            Path.Combine(SavePath, $"{AUTO_SAVE}{SAVE_EXT}");

        private static SaveData LoadFromPath(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                string json = File.ReadAllText(path);
                return JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[存档] 读档失败: {e.Message}");
                return null;
            }
        }
    }
}
