using UnityEngine;
using ZhenguanWarriors.Core.Ads;
using ZhenguanWarriors.Core.Save;
using ZhenguanWarriors.Core.UI;
using ZhenguanWarriors.Core.Audio;

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

            AudioManager.PlayBgm(AudioManager.BgmClips.Title);
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

            // 标题（向下避开刘海/挖孔安全区）
            float titleY = Theme.ApplySafeTop(60 * s, 12 * s);
            Theme.DrawTitle(new Rect(0, titleY, SW, 100 * s),
                "🏯 贞观勇士", (int)(56 * s));

            // 副标题
            GUI.Label(new Rect(0, titleY + 90 * s, SW, 40 * s),
                "—— 李世民战棋录 ——",
                Theme.MakeLabel((int)(22 * s), FontStyle.Normal, Theme.Gold, TextAnchor.MiddleCenter));

            // 分隔
            float startY = Mathf.Max(240 * s, titleY + 170 * s);
            GUI.backgroundColor = Theme.Gold;
            GUI.Box(new Rect(cx - 60 * s, startY - 40 * s, 120 * s, 2 * s), "");
            GUI.backgroundColor = Color.white;

            // 按钮
            float btnW = 280 * s, btnH = 70 * s, gap = 24 * s;

            GUI.backgroundColor = Theme.Primary;
            if (GUI.Button(new Rect(cx - btnW / 2, startY, btnW, btnH),
                "⚔  新 游 戏", Theme.MakeButton((int)(26 * s))))
            {
                AudioManager.PlaySfx(AudioManager.SfxClips.Click);
                SaveManager.ResetNewGame();
                GameManager.Instance.TransitionTo(GamePage.LevelSelect);
            }
            GUI.backgroundColor = Color.white;

            GUI.enabled = _hasSaveData;
            GUI.backgroundColor = Theme.PrimaryDark;
            if (GUI.Button(new Rect(cx - btnW / 2, startY + btnH + gap, btnW, btnH),
                "💾  继 续 游 戏", Theme.MakeButton((int)(26 * s))))
            {
                AudioManager.PlaySfx(AudioManager.SfxClips.Click);
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
                AudioManager.PlaySfx(AudioManager.SfxClips.Click);
                _currentSubPage = SubPage.Settings;
            }
            GUI.backgroundColor = Color.white;

            // 每日福利
            if (GameState.CurrentSave != null)
            {
                bool canDaily = AdManager.Instance != null && AdManager.Instance.CanShowDailyExtraReward();
                GUI.enabled = canDaily;
                GUI.backgroundColor = canDaily ? Theme.Gold : Theme.BgCard;
                string dailyLabel = canDaily
                    ? $"🎬 每日福利 +{AdManager.Instance.DailyRewardGold}"
                    : "今日已领";
                if (GUI.Button(new Rect(cx - btnW / 2, startY + (btnH + gap) * 3, btnW, btnH),
                    dailyLabel, Theme.MakeButton((int)(24 * s), FontStyle.Normal)))
                {
                    AudioManager.PlaySfx(AudioManager.SfxClips.Click);
                    AdManager.Instance.ShowAd(AdPlacementType.DailyExtraReward,
                        () =>
                        {
                            var dlg = GameManager.Instance?.GetComponent<ConfirmDialog>();
                            dlg?.Show("每日福利", $"获得 {AdManager.Instance.DailyRewardGold} 金币", null, null, "确定");
                        },
                        null);
                }
                GUI.backgroundColor = Color.white;
                GUI.enabled = true;
            }

            // 版本（向上避开底部手势条安全区）
            float versionY = Theme.ApplySafeBottom(50 * s, 30 * s, 8 * s);
            GUI.Label(new Rect(20 * s, SH - versionY, 200 * s, 30 * s),
                $"v{Application.version}", Theme.MakeLabel((int)(16 * s), FontStyle.Normal, Theme.TextDim));

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

            Theme.DrawTitle(new Rect(0, Theme.ApplySafeTop(40 * s, 12 * s), SW, 60 * s), "⚙ 设置", (int)(32 * s));

            float x = SW / 2f - 160 * s, y = Theme.ApplySafeTop(120 * s, 12 * s), w = 320 * s, h = 40 * s;

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
                {
                    AudioManager.PlaySfx(AudioManager.SfxClips.Click);
                    GameState.CurrentDifficulty = diff;
                }
            }
            GUI.backgroundColor = Color.white;
            y += 70 * s;

            // BGM / SFX 开关
            GUI.Label(new Rect(x, y, w, h), "🎵 音乐音效", Theme.MakeLabel((int)(22 * s), FontStyle.Bold));
            y += 45 * s;

            bool bgmOn = GameState.CurrentSave?.bgmOn ?? true;
            bool sfxOn = GameState.CurrentSave?.sfxOn ?? true;

            GUI.backgroundColor = bgmOn ? Theme.Primary : Theme.BgCard;
            if (GUI.Button(new Rect(x, y, 150 * s, 45 * s), $"BGM: {(bgmOn ? "开" : "关")}",
                Theme.MakeButton((int)(18 * s))))
            {
                AudioManager.PlaySfx(AudioManager.SfxClips.Click);
                bgmOn = !bgmOn;
                AudioManager.Instance.SetBGMEnabled(bgmOn);
                if (GameState.CurrentSave != null) GameState.CurrentSave.bgmOn = bgmOn;
            }
            GUI.backgroundColor = sfxOn ? Theme.Primary : Theme.BgCard;
            if (GUI.Button(new Rect(x + 170 * s, y, 150 * s, 45 * s), $"SFX: {(sfxOn ? "开" : "关")}",
                Theme.MakeButton((int)(18 * s))))
            {
                AudioManager.PlaySfx(AudioManager.SfxClips.Click);
                sfxOn = !sfxOn;
                AudioManager.Instance.SetSFXEnabled(sfxOn);
                if (GameState.CurrentSave != null) GameState.CurrentSave.sfxOn = sfxOn;
            }
            GUI.backgroundColor = Color.white;
            y += 70 * s;

            // 相机缩放
            y += 10 * s;
            GUI.Label(new Rect(x, y, w, h), "🔍 战场缩放", Theme.MakeLabel((int)(22 * s), FontStyle.Bold));
            y += 45 * s;

            float zoom = GameState.CurrentSave?.cameraZoom ?? 1f;
            GUI.Label(new Rect(x, y, 60 * s, h), "近",
                Theme.MakeLabel((int)(16 * s), FontStyle.Normal, Theme.TextDim, TextAnchor.MiddleCenter));
            GUI.Label(new Rect(x + w - 60 * s, y, 60 * s, h), "远",
                Theme.MakeLabel((int)(16 * s), FontStyle.Normal, Theme.TextDim, TextAnchor.MiddleCenter));

            float newZoom = GUI.HorizontalSlider(
                new Rect(x + 70 * s, y + 8 * s, w - 140 * s, 20 * s),
                zoom, 0.5f, 1.5f);

            if (!Mathf.Approximately(newZoom, zoom) && GameState.CurrentSave != null)
                GameState.CurrentSave.cameraZoom = Mathf.Clamp(newZoom, 0.5f, 1.5f);

            GUI.Label(new Rect(x + w / 2 - 40 * s, y + 22 * s, 80 * s, 20 * s),
                $"{newZoom:F2}x",
                Theme.MakeLabel((int)(14 * s), FontStyle.Normal, Theme.TextDim, TextAnchor.MiddleCenter));
            y += 50 * s;

            // 存档管理
            GUI.Label(new Rect(x, y, w, h), "📦 存档管理", Theme.MakeLabel((int)(22 * s), FontStyle.Bold));
            y += 8 * s;
            GUI.backgroundColor = Theme.PrimaryDark;
            if (GUI.Button(new Rect(x + w + 20 * s, y, 140 * s, h), "清空存档",
                Theme.MakeButton((int)(18 * s))))
            {
                AudioManager.PlaySfx(AudioManager.SfxClips.Click);
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
            float backY = Theme.ApplySafeBottom(90 * s, 60 * s, 12 * s);
            if (GUI.Button(new Rect(SW / 2f - 120 * s, SH - backY, 240 * s, 60 * s),
                "← 返回", Theme.MakeButton((int)(20 * s))))
            {
                AudioManager.PlaySfx(AudioManager.SfxClips.Click);
                _currentSubPage = SubPage.Main;
            }
            GUI.backgroundColor = Color.white;
        }
    }
}
