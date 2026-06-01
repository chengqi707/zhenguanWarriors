using UnityEngine;
using ZhenguanWarriors.Core.Save;

namespace ZhenguanWarriors.View.BattleView
{
    /// <summary>
    /// 主菜单控制器——游戏入口，新游戏/继续/设置
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        private enum MenuPage { Main, Settings }
        private MenuPage _currentPage = MenuPage.Main;
        private Vector2 _settingsScroll;
        private bool _hasSaveData;

        void Start()
        {
            _hasSaveData = SaveManager.HasAnySave();
            Debug.Log("[贞观勇士] 主菜单启动");
        }

        void OnGUI()
        {
            if (_currentPage == MenuPage.Main)
                DrawMainMenu();
            else
                DrawSettings();
        }

        private void DrawMainMenu()
        {
            // 背景
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");

            float cx = Screen.width / 2f;
            float cy = Screen.height / 2f;

            // 标题
            GUI.Label(new Rect(cx - 160, cy - 180, 320, 60),
                "🏯 贞观勇士",
                new GUIStyle
                {
                    fontSize = 42,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.9f, 0.7f, 0.2f) } // 金色
                });

            // 副标题
            GUI.Label(new Rect(cx - 120, cy - 120, 240, 30),
                "—— 李世民战棋录 ——",
                new GUIStyle
                {
                    fontSize = 16,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.7f, 0.6f, 0.4f) }
                });

            float btnW = 220;
            float btnH = 50;
            float gap = 15;
            float startY = cy - 30;

            // 新游戏
            if (GUI.Button(new Rect(cx - btnW / 2, startY, btnW, btnH),
                "⚔  新 游 戏",
                new GUIStyle(GUI.skin.button)
                {
                    fontSize = 20,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                }))
            {
                StartNewGame();
            }

            // 继续游戏
            GUI.enabled = _hasSaveData;
            if (GUI.Button(new Rect(cx - btnW / 2, startY + btnH + gap, btnW, btnH),
                "💾  继 续 游 戏",
                new GUIStyle(GUI.skin.button)
                {
                    fontSize = 20,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                }))
            {
                ContinueGame();
            }
            GUI.enabled = true;

            // 设置
            if (GUI.Button(new Rect(cx - btnW / 2, startY + (btnH + gap) * 2, btnW, btnH),
                "⚙  设 置",
                new GUIStyle(GUI.skin.button)
                {
                    fontSize = 20,
                    alignment = TextAnchor.MiddleCenter
                }))
            {
                _currentPage = MenuPage.Settings;
            }

            // 底部版本号
            GUI.Label(new Rect(10, Screen.height - 30, 200, 25),
                "v0.5 (Sprint 5)",
                new GUIStyle { fontSize = 11, normal = { textColor = Color.gray } });
        }

        private void DrawSettings()
        {
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");

            GUI.Label(new Rect(Screen.width / 2f - 80, 30, 160, 40),
                "⚙ 设置",
                new GUIStyle { fontSize = 24, fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } });

            // 音效开关（占位）
            float x = Screen.width / 2f - 120;
            float y = 100;
            float w = 240;
            float h = 35;

            GUI.Label(new Rect(x, y, w, h), "BGM: 暂无音效（待Sprint 7）");
            y += 50;

            // 存档管理
            GUI.Label(new Rect(x, y, w, h), "存档管理");

            if (GUI.Button(new Rect(x + w + 10, y, 100, h), "清空存档"))
            {
                SaveManager.DeleteAllSaves();
                _hasSaveData = false;
            }

            y += 50;
            // 显示存档列表
            for (int i = 0; i < SaveManager.MAX_SLOTS; i++)
            {
                var meta = SaveManager.GetSlotMeta(i);
                if (meta != null)
                {
                    GUI.Label(new Rect(x, y, w, 25),
                        $"槽位{i + 1}: 第{meta.levelIndex + 1}关 {meta.levelName} Lv{meta.avgLevel}",
                        new GUIStyle { fontSize = 12, normal = { textColor = Color.gray } });
                    y += 28;
                }
            }

            // 返回
            if (GUI.Button(new Rect(Screen.width / 2f - 80, Screen.height - 70, 160, 40),
                "← 返回"))
            {
                _currentPage = MenuPage.Main;
            }
        }

        private void StartNewGame()
        {
            Debug.Log("[贞观勇士] 开始新游戏");
            // 清空旧存档，解锁第一关
            SaveManager.ResetNewGame();

            // 切换到战斗场景
            SwitchToBattle();
        }

        private void ContinueGame()
        {
            Debug.Log("[贞观勇士] 继续游戏");
            var save = SaveManager.LoadLatest();
            if (save != null)
            {
                // 恢复关卡进度
                GameState.CurrentSave = save;
                SwitchToBattle();
            }
        }

        private void SwitchToBattle()
        {
            // 移除主菜单，启动战斗场景
            Destroy(gameObject);

            var starter = new GameObject("BattleSystem");
            starter.AddComponent<BattleSceneStarter>();
        }
    }
}
