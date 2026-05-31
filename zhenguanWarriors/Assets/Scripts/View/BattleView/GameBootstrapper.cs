using UnityEngine;

namespace ZhenguanWarriors.View.BattleView
{
    /// <summary>
    /// 游戏启动器——进入 Play 模式后自动创建战斗场景，
    /// 无需手动在场景中挂载任何组件
    /// </summary>
    public static class GameBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoBoot()
        {
            // 延迟一帧执行，确保场景加载完毕
            GameObject go = new GameObject("BattleSystem");
            go.AddComponent<BattleSceneStarter>();
            Object.DontDestroyOnLoad(go);
        }
    }
}
