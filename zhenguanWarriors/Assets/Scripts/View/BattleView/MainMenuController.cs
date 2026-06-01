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

        // ===== UI缩放 =====
        private float _scale;
        private float SW => Screen.width;
        private float SH => Screen.height;

        void Start()
        {
            _hasSaveData = SaveManager.HasAnySave();
            // 以1080p为基准自动缩放
            _scale = Mathf.Min(SW / 1920f, SH / 1080f);
            if (_scale < 0.5f) _scale = 0.5f;
            if (_scale > 2.0f) _scale = 2.0f;
            Debug.Log($"[贞观勇士] 主菜单启动 (缩放比: {_scale:F2})");
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
            float s = _scale;

            // 背景
            GUI.backgroundColor = Theme.BgDark;
            GUI.Box(new Rect(0, 0, SW, SH), "");

            float cx = SW / 2f;
            float cy = SH / 2f;

            // 顶部装饰线
            GUI.backgroundColor = Theme.Primary;
            GUI.Box(new Rect(0, 0, SW, 6 * s), "");

            // 标题
            Theme.DrawTitle(new Rect(0, 60 * s, SW, 100 * s), "🏯 贞观勇士", (int)(56 * s));

            // 副标题
            GUI.Label(new Rect(0, 150 * s, SW, 40 * s),
                "—— 李世民战棋录 ——",
                Theme.MakeLabel((int)(22 * s), FontStyle.Normal, Theme.Gold, TextAnchor.MiddleCenter));

            // 装饰分隔线
            GUI.backgroundColor = Theme.Gold;
            GUI.Box(new Rect(cx - 60 * s, 200 * s, 120 * s, 2 * s), "");
            GUI.backgroundColor = Color.white;

            // ---- 三按钮 ----
            float btnW = 280 * s;
            float btnH = 70 * s;
            float gap = 24 * s;
            float startY = 240 * s;

            // 新游戏（朱红）
            GUI.backgroundColor = Theme.Primary;
            if (GUI.Button(new Rect(cx - btnW / 2, startY, btnW, btnH),
                "⚔  新 游 戏",
                Theme.MakeButton((int)(26 * s))))
            {
                StartNewGame();
            }
            GUI.backgroundColor = Color.white;

            // 继续游戏
            GUI.enabled = _hasSaveData;
            GUI.backgroundColor = Theme.PrimaryDark;
            if (GUI.Button(new Rect(cx - btnW / 2, startY + btnH + gap, btnW, btnH),
                "💾  继 续 游 戏",
                Theme.MakeButton((int)(26 * s))))
            {
                ContinueGame();
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;

            // 设置
            GUI.backgroundColor = Theme.BgCard;
            if (GUI.Button(new Rect(cx - btnW / 2, startY + (btnH + gap) * 2, btnW, btnH),
                "⚙  设 置",
                Theme.MakeButton((int)(24 * s), FontStyle.Normal)))
            {
                _currentPage = MenuPage.Settings;
            }
            GUI.backgroundColor = Color.white;

            // 底部版本号
            GUI.Label(new Rect(20 * s, SH - 50 * s, 200 * s, 30 * s),
                "v1.0 (贞观勇士)",
                Theme.MakeLabel((int)(16 * s), FontStyle.Normal, Theme.TextDim));

            // 底部装饰线
            GUI.backgroundColor = Theme.Primary;
            GUI.Box(new Rect(0, SH - 6 * s, SW, 6 * s), "");
            GUI.backgroundColor = Color.white;
        }

        private void DrawSettings()
        {
            float s = _scale;

            GUI.backgroundColor = Theme.BgDark;
            GUI.Box(new Rect(0, 0, SW, SH), "");
            GUI.backgroundColor = Color.white;

            Theme.DrawTitle(new Rect(0, 40 * s, SW, 60 * s), "⚙ 设置", (int)(32 * s));

            float x = SW / 2f - 160 * s;
            float y = 120 * s;
            float w = 320 * s;
            float h = 40 * s;

            // 音效开关（占位）
            GUI.Label(new Rect(x, y, w, h), "🎵 音效将在最终版本添加", Theme.MakeLabel((int)(18 * s)));
            y += 60 * s;

            // 存档管理
            GUI.Label(new Rect(x, y, w, h), "📦 存档管理", Theme.MakeLabel((int)(22 * s), FontStyle.Bold));
            y += 8 * s;

            GUI.backgroundColor = Theme.PrimaryDark;
            if (GUI.Button(new Rect(x + w + 20 * s, y, 140 * s, h), "清空存档",
                Theme.MakeButton((int)(18 * s))))
            {
                SaveManager.DeleteAllSaves();
                _hasSaveData = false;
            }
            GUI.backgroundColor = Color.white;

            y += 70 * s;
            // 显示存档列表
            for (int i = 0; i < SaveManager.MAX_SLOTS; i++)
            {
                var meta = SaveManager.GetSlotMeta(i);
                if (meta != null)
                {
                    GUI.Label(new Rect(x, y, w + 200 * s, 30 * s),
                        $"槽位{i + 1}: 第{meta.levelIndex + 1}关 {meta.levelName} Lv{meta.avgLevel}",
                        Theme.MakeLabel((int)(16 * s), FontStyle.Normal, Theme.TextDim));
                    y += 36 * s;
                }
            }

            // 返回
            GUI.backgroundColor = Theme.BgCard;
            if (GUI.Button(new Rect(SW / 2f - 120 * s, SH - 90 * s, 240 * s, 60 * s),
                "← 返回", Theme.MakeButton((int)(20 * s))))
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
