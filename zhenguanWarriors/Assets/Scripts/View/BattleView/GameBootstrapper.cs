using UnityEngine;

namespace ZhenguanWarriors.View.BattleView
{
    /// <summary>
    /// 游戏启动器——场景加载完毕后自动创建主菜单
    /// </summary>
    public static class GameBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBoot()
        {
            GameObject go = new GameObject("GameRoot");
            go.AddComponent<MainMenuController>();
            Debug.Log("[贞观勇士] 启动：主菜单加载完成");
        }
    }
}
