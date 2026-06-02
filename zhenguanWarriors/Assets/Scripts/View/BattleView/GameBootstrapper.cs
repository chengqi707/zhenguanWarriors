using UnityEngine;

namespace ZhenguanWarriors.View.BattleView
{
    /// <summary>
    /// 游戏启动器——创建唯一 GameRoot
    /// GameManager 接管所有页面生命周期
    /// </summary>
    public static class GameBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBoot()
        {
            GameObject go = new GameObject("GameRoot");
            go.AddComponent<GameManager>();
            Debug.Log("[贞观勇士] GameRoot 创建完成");
        }
    }
}
