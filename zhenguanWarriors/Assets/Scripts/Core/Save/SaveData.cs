using System;
using System.Collections.Generic;

namespace ZhenguanWarriors.Core.Save
{
    /// <summary>
    /// 存档数据模型——记录完整游戏进度
    /// </summary>
    [Serializable]
    public class SaveData
    {
        // ========== 元数据 ==========
        public string saveTime;          // 存档时间（字符串，便于JSON序列化）
        public int version;              // 存档版本（兼容升级）
        public string levelId;           // 当前所在关卡ID
        public int levelIndex;           // 关卡序号（0-based）
        public string levelName;         // 关卡名（显示用）

        // ========== 关卡进度 ==========
        public List<string> unlockedLevels;   // 已解锁的关卡ID列表
        public string currentLevelId;         // 当前选中的关卡

        // ========== 角色状态 ==========
        public List<CharacterSaveData> characters;   // 所有角色的状态

        // ========== 游戏设置 ==========
        public bool bgmOn = true;
        public bool sfxOn = true;

        // ========== 计算属性 ==========
        public int avgLevel
        {
            get
            {
                if (characters == null || characters.Count == 0) return 1;
                int sum = 0;
                foreach (var c in characters) sum += c.level;
                return sum / characters.Count;
            }
        }

        /// <summary>创建新游戏默认存档</summary>
        public static SaveData CreateNew()
        {
            return new SaveData
            {
                version = 1,
                saveTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                unlockedLevels = new List<string> { "level_01" },
                currentLevelId = "level_01",
                levelId = "level_01",
                levelIndex = 0,
                levelName = "晋阳举义",
                characters = new List<CharacterSaveData>(),
                bgmOn = true,
                sfxOn = true
            };
        }
    }

    /// <summary>角色存档数据</summary>
    [Serializable]
    public class CharacterSaveData
    {
        public string id;
        public string name;
        public int level;
        public int experience;
        public int baseStr, baseCmd, baseInt, baseAgi, baseLuk;
        public int strGrowth, cmdGrowth, intGrowth, agiGrowth, lukGrowth;
        public List<string> skillIds;
        public string weaponId;
        public string armorId;
        public string trinketId;
    }
}
