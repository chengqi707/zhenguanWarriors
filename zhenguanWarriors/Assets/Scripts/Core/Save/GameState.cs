using System.Collections.Generic;
using System.Linq;

namespace ZhenguanWarriors.Core.Save
{
    /// <summary>
    /// 全局游戏状态——持有当前存档、关卡进度、难度等跨场景数据
    /// </summary>
    public static class GameState
    {
        /// <summary>难度级别</summary>
        public enum Difficulty { Easy, Normal, Hard, Hell }
        public static Difficulty CurrentDifficulty { get; set; } = Difficulty.Normal;
    {
        /// <summary>当前加载的存档（null = 新游戏）</summary>
        public static SaveData CurrentSave { get; set; }

        /// <summary>检查某关卡是否已解锁</summary>
        public static bool IsLevelUnlocked(string levelId)
        {
            if (CurrentSave != null)
                return CurrentSave.unlockedLevels.Contains(levelId);
            return levelId == "level_01";
        }

        /// <summary>解锁一个关卡</summary>
        public static void UnlockLevel(string levelId)
        {
            if (CurrentSave == null) return;
            if (!CurrentSave.unlockedLevels.Contains(levelId))
                CurrentSave.unlockedLevels.Add(levelId);
        }

        /// <summary>获取所有已解锁关卡ID（用于遍历检查）</summary>
        public static HashSet<string> GetAllUnlocked()
        {
            if (CurrentSave != null)
                return new HashSet<string>(CurrentSave.unlockedLevels);
            return new HashSet<string> { "level_01" };
        }

        /// <summary>获取/设置下一关要播放的剧情ID（关前/关后）</summary>
        public static string PendingStoryId { get; set; }
    }
}
