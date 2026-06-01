using UnityEngine;
using ZhenguanWarriors.Core.Save;
using ZhenguanWarriors.Core.UI;

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
            GUI.backgroundColor = Theme.BgDark;
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");

            float cx = Screen.width / 2f;
            float cy = Screen.height / 2f;

            // 顶部装饰线
            GUI.backgroundColor = Theme.Primary;
            GUI.Box(new Rect(0, 0, Screen.width, 4), "");

            // 标题（朱红+金色）
            Theme.DrawTitle(new Rect(cx - 160, cy - 180, 320, 60), "🏯 贞观勇士", 42);

            // 副标题
            GUI.Label(new Rect(cx - 120, cy - 120, 240, 30),
                "—— 李世民战棋录 ——",
                Theme.MakeLabel(16, FontStyle.Normal, Theme.Gold, TextAnchor.MiddleCenter));

            // 装饰中轴
            GUI.backgroundColor = Theme.Gold;
            GUI.Box(new Rect(cx - 1, cy - 85, 2, 40), "");

            float btnW = 240;
            float btnH = 55;
            float gap = 18;
            float startY = cy - 30;

            // 新游戏按钮（朱红背景）
            GUI.backgroundColor = Theme.Primary;
            if (GUI.Button(new Rect(cx - btnW / 2, startY, btnW, btnH),
                "⚔  新 游 戏",
                Theme.MakeButton(22)))
            {
                StartNewGame();
            }
            GUI.backgroundColor = Color.white;

            // 继续游戏
            GUI.enabled = _hasSaveData;
            GUI.backgroundColor = Theme.PrimaryDark;
            if (GUI.Button(new Rect(cx - btnW / 2, startY + btnH + gap, btnW, btnH),
                "💾  继 续 游 戏",
                Theme.MakeButton(22)))
            {
                ContinueGame();
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;

            // 设置
            GUI.backgroundColor = Theme.BgCard;
            if (GUI.Button(new Rect(cx - btnW / 2, startY + (btnH + gap) * 2, btnW, btnH),
                "⚙  设 置",
                Theme.MakeButton(20, FontStyle.Normal)))
            {
                _currentPage = MenuPage.Settings;
            }
            GUI.backgroundColor = Color.white;

            // 底部版本号
            GUI.Label(new Rect(10, Screen.height - 30, 200, 25),
                "v0.7 (Sprint 7)",
                Theme.MakeLabel(11, FontStyle.Normal, Theme.TextDim));

            // 底部装饰线
            GUI.backgroundColor = Theme.Primary;
            GUI.Box(new Rect(0, Screen.height - 4, Screen.width, 4), "");
            GUI.backgroundColor = Color.white;
        }

        private void DrawSettings()
        {
            GUI.backgroundColor = Theme.BgDark;
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");
            GUI.backgroundColor = Color.white;

            Theme.DrawTitle(new Rect(Screen.width / 2f - 80, 30, 160, 40), "⚙ 设置", 24);

            // 音效开关（占位）
            float x = Screen.width / 2f - 120;
            float y = 100;
            float w = 240;
            float h = 35;

            GUI.Label(new Rect(x, y, w, h), "🎵 音效将在最终版本添加", Theme.MakeLabel(14));
            y += 50;

            // 存档管理
            GUI.Label(new Rect(x, y, w, h), "📦 存档管理", Theme.MakeLabel(16, FontStyle.Bold));
            y += 5;

            GUI.backgroundColor = Theme.PrimaryDark;
            if (GUI.Button(new Rect(x + w + 10, y, 100, h), "清空存档"))
            {
                SaveManager.DeleteAllSaves();
                _hasSaveData = false;
            }
            GUI.backgroundColor = Color.white;

            y += 50;
            // 显示存档列表
            for (int i = 0; i < SaveManager.MAX_SLOTS; i++)
            {
                var meta = SaveManager.GetSlotMeta(i);
                if (meta != null)
                {
                    GUI.Label(new Rect(x, y, w, 25),
                        $"槽位{i + 1}: 第{meta.levelIndex + 1}关 {meta.levelName} Lv{meta.avgLevel}",
                        Theme.MakeLabel(12, FontStyle.Normal, Theme.TextDim));
                    y += 28;
                }
            }

            // 返回
            GUI.backgroundColor = Theme.BgCard;
            if (GUI.Button(new Rect(Screen.width / 2f - 80, Screen.height - 70, 160, 40),
                "← 返回", Theme.MakeButton(16)))
            {
                _currentPage = MenuPage.Main;
            }
            GUI.backgroundColor = Color.white;
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
