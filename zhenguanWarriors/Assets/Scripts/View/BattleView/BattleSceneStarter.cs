using UnityEngine;

namespace ZhenguanWarriors.View.BattleView
{
    /// <summary>
    /// 战斗场景启动器——在 Unity Editor 中按 Play 即可看到网格和单位
    /// 挂载到场景中的空 GameObject 上即可
    /// </summary>
    public class BattleSceneStarter : MonoBehaviour
    {
        [Header("场景启动配置")]
        public int gridWidth = 12;
        public int gridHeight = 10;
        public float hexSize = 0.5f;

        void Awake()
        {
            // 设置摄像机为 Orthographic
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.orthographic = true;
                mainCam.orthographicSize = 5f;
                mainCam.transform.position = new Vector3(6f, 4f, -10f);
                mainCam.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
            }

            // 添加 HexGridView
            var hexView = gameObject.AddComponent<HexGridView>();
            hexView.gridWidth = gridWidth;
            hexView.gridHeight = gridHeight;
            hexView.hexSize = hexSize;

            // 添加战斗测试控制器（在 Start 中执行）
            gameObject.AddComponent<BattleTestController>();

            Debug.Log("[贞观勇士] 战斗场景初始化完成！按 Play 即可测试。");
            Debug.Log("操作说明：左键点击选中己方单位 → 再点击移动范围格子移动 → 点击相邻敌方攻击");
            Debug.Log("按 Space 键快速结束当前玩家回合");
        }
    }
}
