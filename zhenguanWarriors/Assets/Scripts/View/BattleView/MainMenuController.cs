using UnityEngine;
using ZhenguanWarriors.Core.Save;
using ZhenguanWarriors.Core.UI;

namespace ZhenguanWarriors.View.BattleView
{
    public class MainMenuController : MonoBehaviour
    {
        private enum SubPage { Main, Settings }
        private SubPage _currentSubPage = SubPage.Main;
        private bool _hasSaveData;
        private float _scale;
        private float SW => Screen.width;
        private float SH => Screen.height;

        void OnEnable()
        {
            _hasSaveData = SaveManager.HasAnySave();
            _scale = Mathf.Min(SW / 1920f, SH / 1080f);
            if (_scale < 0.6f) _scale = 0.6f;
            if (_scale > 2.5f) _scale = 2.5f;
            _currentSubPage = SubPage.Main;
        }

        /// <summary>GameManager 调用，设置子页面</summary>
        public void SetPage(GamePage page)
        {
            _currentSubPage = (page == GamePage.Settings) ? SubPage.Settings : SubPage.Main;
        }

        void OnGUI()
        {
            if (GameManager.Instance != null && GameManager.Instance.IsTransitioning) return;
            if (_currentSubPage == SubPage.Main) DrawMainMenu();
            else DrawSettings();
        }

        void DrawMainMenu()
        {
            float s = _scale;
            GUI.backgroundColor = Theme.BgDark;
            GUI.Box(new Rect(0, 0, SW, SH), "");
            GUI.backgroundColor = Color.white;

            // 装饰线
            GUI.backgroundColor = Theme.Primary;
            GUI.Box(new Rect(0, 0, SW, 6 * s), "");

            float cx = SW / 2f;

            // 标题
            Theme.DrawTitle(new Rect(0, 60 * s, SW, 100 * s),
                "🏯 贞观勇士", (int)(56 * s));

            // 副标题
            GUI.Label(new Rect(0, 150 * s, SW, 40 * s),
                "—— 李世民战棋录 ——",
                Theme.MakeLabel((int)(22 * s), FontStyle.Normal, Theme.Gold, TextAnchor.MiddleCenter));

            // 分隔
            GUI.backgroundColor = Theme.Gold;
            GUI.Box(new Rect(cx - 60 * s, 200 * s, 120 * s, 2 * s), "");
            GUI.backgroundColor = Color.white;

            // 按钮
            float btnW = 280 * s, btnH = 70 * s, gap = 24 * s, startY = 240 * s;

            GUI.backgroundColor = Theme.Primary;
            if (GUI.Button(new Rect(cx - btnW / 2, startY, btnW, btnH),
                "⚔  新 游 戏", Theme.MakeButton((int)(26 * s))))
            {
                SaveManager.ResetNewGame();
                GameManager.Instance.TransitionTo(GamePage.LevelSelect);
            }
            GUI.backgroundColor = Color.white;

            GUI.enabled = _hasSaveData;
            GUI.backgroundColor = Theme.PrimaryDark;
            if (GUI.Button(new Rect(cx - btnW / 2, startY + btnH + gap, btnW, btnH),
                "💾  继 续 游 戏", Theme.MakeButton((int)(26 * s))))
            {
                var save = SaveManager.LoadLatest();
                if (save != null)
                {
                    GameState.CurrentSave = save;
                    GameManager.Instance.TransitionTo(GamePage.LevelSelect);
                }
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;

            GUI.backgroundColor = Theme.BgCard;
            if (GUI.Button(new Rect(cx - btnW / 2, startY + (btnH + gap) * 2, btnW, btnH),
                "⚙  设 置", Theme.MakeButton((int)(24 * s), FontStyle.Normal)))
            {
                _currentSubPage = SubPage.Settings;
            }
            GUI.backgroundColor = Color.white;

            // 版本
            GUI.Label(new Rect(20 * s, SH - 50 * s, 200 * s, 30 * s),
                "v1.0", Theme.MakeLabel((int)(16 * s), FontStyle.Normal, Theme.TextDim));

            GUI.backgroundColor = Theme.Primary;
            GUI.Box(new Rect(0, SH - 6 * s, SW, 6 * s), "");
            GUI.backgroundColor = Color.white;
        }

        void DrawSettings()
        {
            float s = _scale;
            GUI.backgroundColor = Theme.BgDark;
            GUI.Box(new Rect(0, 0, SW, SH), "");
            GUI.backgroundColor = Color.white;

            Theme.DrawTitle(new Rect(0, 40 * s, SW, 60 * s), "⚙ 设置", (int)(32 * s));

            float x = SW / 2f - 160 * s, y = 120 * s, w = 320 * s, h = 40 * s;

            // 难度
            GUI.Label(new Rect(x, y, w, h), "🎯 游戏难度", Theme.MakeLabel((int)(22 * s), FontStyle.Bold));
            y += 45 * s;
            string[] diffNames = { "极简", "简单", "普通", "困难" };
            var curDiff = GameState.CurrentDifficulty;
            float btnW2 = 140 * s;
            for (int i = 0; i < 4; i++)
            {
                var diff = (GameState.Difficulty)i;
                bool sel = curDiff == diff;
                GUI.backgroundColor = sel ? Theme.Primary : Theme.BgCard;
                if (GUI.Button(new Rect(SW / 2f - btnW2 * 2 + i * (btnW2 + 10 * s), y,
                    btnW2, 45 * s), diffNames[i],
                    Theme.MakeButton(sel ? (int)(18 * s) : (int)(16 * s))))
                    GameState.CurrentDifficulty = diff;
            }
            GUI.backgroundColor = Color.white;
            y += 70 * s;

            // 音效占位
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
                _currentSubPage = SubPage.Main;
            GUI.backgroundColor = Color.white;
        }
    }
}
