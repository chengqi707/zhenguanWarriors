using UnityEngine;

namespace ZhenguanWarriors.View.BattleView
{
    /// <summary>
    /// 游戏启动器——场景加载完毕后自动显示启动画面
    /// </summary>
    public static class GameBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBoot()
        {
            GameObject go = new GameObject("GameRoot");
            go.AddComponent<SplashScreen>();
            Debug.Log("[贞观勇士] 启动：启动画面");
        }
    }
}
