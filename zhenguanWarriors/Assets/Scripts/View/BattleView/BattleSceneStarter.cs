using UnityEngine;

namespace ZhenguanWarriors.View.BattleView
{
    /// <summary>
    /// 战斗场景启动器——初始化网格、战斗控制器和对话系统
    /// </summary>
    public class BattleSceneStarter : MonoBehaviour
    {
        [Header("场景启动配置")]
        public int gridWidth = 12;
        public int gridHeight = 10;
        public float hexSize = 0.5f;

        void Awake()
        {
            var hexView = gameObject.AddComponent<HexGridView>();
            hexView.gridWidth = gridWidth;
            hexView.gridHeight = gridHeight;
            hexView.hexSize = hexSize;

            gameObject.AddComponent<BattleTestController>();

            // 对话系统
            gameObject.AddComponent<DialogueUI>();

            Debug.Log("[贞观勇士] 战斗场景初始化完成！");
        }
    }
}
