using System.Collections.Generic;
using ZhenguanWarriors.Core.Battle;

namespace ZhenguanWarriors.Core.Save
{
    /// <summary>
    /// 全局游戏状态——持有当前存档、关卡进度等跨场景数据
    /// </summary>
    public static class GameState
    {
        /// <summary>当前加载的存档（null = 新游戏）</summary>
        public static SaveData CurrentSave { get; set; }

        /// <summary>当前已解锁的关卡ID集合</summary>
        public static HashSet<string> UnlockedLevels
        {
            get
            {
                if (CurrentSave != null)
                    return new HashSet<string>(CurrentSave.unlockedLevels);
                return new HashSet<string> { "level_01" };
            }
            set
            {
                if (CurrentSave != null)
                    CurrentSave.unlockedLevels = new List<string>(value);
            }
        }

        /// <summary>获取/设置下一关要播放的剧情ID（关前/关后）</summary>
        public static string PendingStoryId { get; set; }
    }
}
