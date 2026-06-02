using System;
using System.Collections.Generic;

namespace ZhenguanWarriors.Core.Save
{
    [Serializable]
    public class SaveData
    {
        // ========== 元数据 ==========
        public string saveTime;
        public int version;
        public string levelId;
        public int levelIndex;
        public string levelName;

        // ========== 关卡进度 ==========
        public List<string> unlockedLevels;
        public string currentLevelId;

        // ========== 角色状态 ==========
        public List<CharacterSaveData> characters;

        // ========== 游戏设置 ==========
        public bool bgmOn = true;
        public bool sfxOn = true;

        // ========== 战场状态（战斗中存档时保存） ==========
        public bool isInBattle;
        public int turnNumber;
        public string weather;
        public string wind;
        public List<BattlefieldUnitSave> battlefieldUnits;
        public List<TerrainChangeSave> terrainChanges;

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
                bgmOn = true, sfxOn = true
            };
        }
    }

    [Serializable]
    public class CharacterSaveData
    {
        public string id, name;
        public int level, experience;
        public int baseStr, baseCmd, baseInt, baseAgi, baseLuk;
        public int strGrowth, cmdGrowth, intGrowth, agiGrowth, lukGrowth;
        public List<string> skillIds;
        public string weaponId, armorId, trinketId;
    }

    /// <summary>战场单位状态（用于战斗中存档精确恢复）</summary>
    [Serializable]
    public class BattlefieldUnitSave
    {
        public string id;
        public int posQ, posR;
        public int currentHp, currentMp;
        public bool hasActed, hasMovedThisTurn;
        public string unitState;
        public int tempStrBuff;
    }

    /// <summary>地形变化记录（用于战斗中存档恢复）</summary>
    [Serializable]
    public class TerrainChangeSave
    {
        public int q, r;
        public string terrainType;
    }
}
