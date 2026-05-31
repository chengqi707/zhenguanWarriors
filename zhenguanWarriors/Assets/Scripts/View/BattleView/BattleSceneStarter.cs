using UnityEngine;

namespace ZhenguanWarriors.View.BattleView
{
    /// <summary>
    /// 战斗场景启动器——在 Unity Editor 中按 Play 即可看到网格和单位
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

            Debug.Log("[贞观勇士] 战斗场景初始化完成！");
        }
    }
}
