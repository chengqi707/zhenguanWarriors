using UnityEngine;

namespace ZhenguanWarriors.View.BattleView
{
    /// <summary>
    /// 游戏启动器——场景加载完毕后自动创建战斗场景，
    /// 无需手动在场景中挂载任何组件
    /// </summary>
    public static class GameBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBoot()
        {
            GameObject go = new GameObject("BattleSystem");
            go.AddComponent<BattleSceneStarter>();
        }
    }
}
