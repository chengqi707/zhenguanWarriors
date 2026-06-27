using UnityEngine;
using ZhenguanWarriors.Core.Save;
using ZhenguanWarriors.Core.UI;

namespace ZhenguanWarriors.View.BattleView
{
    /// <summary>
    /// 战斗暂停菜单——半透明遮罩 + 四个选项
    /// 打开时 Time.timeScale = 0 冻结游戏逻辑
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        private bool _isOpen;
        private float _scale;
        private ConfirmDialog _confirm;
        private BattleTestController _battleCtrl;
        private DebugPanel _debugPanel;
        private bool _pendingSaveAndExit;  // 标记保存退出后需要回到主菜单
        private bool _pendingRetreat;      // 标记需要触发失败结算

        public bool IsOpen => _isOpen;
        public bool HasPendingAction => _pendingSaveAndExit || _pendingRetreat;
        public bool IsDebugPanelOpen => _debugPanel?.IsOpen ?? false;

        public void CloseDebugPanel() => _debugPanel?.Hide();

        void Start()
        {
            _confirm = GetComponent<ConfirmDialog>();
            if (_confirm == null)
                _confirm = gameObject.AddComponent<ConfirmDialog>();
            _battleCtrl = GetComponent<BattleTestController>();
            _debugPanel = GetComponent<DebugPanel>();
            if (_debugPanel == null)
                _debugPanel = gameObject.AddComponent<DebugPanel>();
            _scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            if (_scale < 0.5f) _scale = 0.5f;
            if (_scale > 2.0f) _scale = 2.0f;
        }

        public void Open()
        {
            _isOpen = true;
            Time.timeScale = 0f;
            _pendingSaveAndExit = false;
            _pendingRetreat = false;
        }

        public void Close()
        {
            _isOpen = false;
            Time.timeScale = 1f;
            // ★ 关闭暂停菜单时执行待处理的保存退出/撤退
            ExecutePendingAction();
        }

        /// <summary>外部调用：检查是否有待处理的退出/撤退操作</summary>
        public void ExecutePendingAction()
        {
            if (_pendingSaveAndExit)
            {
                _pendingSaveAndExit = false;
                GameManager.Instance.TransitionTo(GamePage.MainMenu);
            }
            if (_pendingRetreat)
            {
                _pendingRetreat = false;
                _battleCtrl?.ForceDefeat("撤退");
            }
        }

        void OnGUI()
        {
            if (_debugPanel.IsOpen)
                return;

            if (!_isOpen || _confirm.IsOpen) return;
            float s = _scale;

            // 半透明遮罩
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");

            // 暂停菜单面板
            float pw = 360 * s, ph = 400 * s;
            float px = (Screen.width - pw) / 2, py = (Screen.height - ph) / 2;

            GUI.backgroundColor = Theme.BgPanel;
            GUI.Box(new Rect(px, py, pw, ph), "");
            GUI.backgroundColor = Color.white;

            // 装饰
            GUI.backgroundColor = Theme.Gold;
            GUI.Box(new Rect(px, py, pw, 4 * s), "");

            // 标题
            GUI.Label(new Rect(px, py + 20 * s, pw, 40 * s),
                "⏸ 战 斗 暂 停",
                Theme.MakeLabel((int)(24 * s), FontStyle.Bold, Theme.Gold, TextAnchor.MiddleCenter));

            float btnW = 280 * s, btnH = 48 * s, gap = 12 * s;
            float startX = px + (pw - btnW) / 2;
            float startY = py + 70 * s;

            // 继续战斗
            GUI.backgroundColor = Theme.Primary;
            if (GUI.Button(new Rect(startX, startY, btnW, btnH),
                "▶ 继续战斗", Theme.MakeButton((int)(18 * s))))
                Close();

            // 保存并退出
            GUI.backgroundColor = Theme.PrimaryDark;
            if (GUI.Button(new Rect(startX, startY + (btnH + gap), btnW, btnH),
                "💾 保存并退出", Theme.MakeButton((int)(18 * s))))
            {
                _confirm.Show("保存并退出",
                    "当前战斗进度将被保存。\n下次可从主菜单→继续游戏恢复战场。",
                    () => {
                        _pendingSaveAndExit = true;
                        Time.timeScale = 1f; // 允许存档操作
                        _battleCtrl?.AutoSaveGame();
                        Close();
                    }, null, "保存退出", "取消");
            }

            // 撤退
            GUI.backgroundColor = Theme.BgCard;
            if (GUI.Button(new Rect(startX, startY + (btnH + gap) * 2, btnW, btnH),
                "🏳 撤退（失败）", Theme.MakeButton((int)(18 * s))))
            {
                _confirm.Show("确认撤退？",
                    "撤退将以失败结算本关，\n但已获得的经验值保留50%。",
                    () => {
                        _pendingRetreat = true;
                        Time.timeScale = 1f;
                        Close();
                    }, null, "撤退", "取消");
            }

            // 返回主菜单（不存档）
            GUI.backgroundColor = Theme.BgCard;
            if (GUI.Button(new Rect(startX, startY + (btnH + gap) * 3, btnW, btnH),
                "🏯 返回主菜单", Theme.MakeButton((int)(18 * s))))
            {
                _confirm.Show("返回主菜单？",
                    "当前战斗进度将丢失。\n建议先「保存并退出」。",
                    () => {
                        Time.timeScale = 1f;
                        Close();
                        GameManager.Instance.TransitionTo(GamePage.MainMenu);
                    }, null, "不保存返回", "取消");
            }

            // 调试日志
            GUI.backgroundColor = Theme.BgCard;
            if (GUI.Button(new Rect(startX, startY + (btnH + gap) * 4, btnW, btnH),
                "🐛 调试日志", Theme.MakeButton((int)(18 * s))))
            {
                _debugPanel.Show();
            }
            GUI.backgroundColor = Color.white;

            // 相机缩放
            float zoomY = py + ph - 95 * s;
            GUI.Label(new Rect(px + 20 * s, zoomY, pw - 40 * s, 22 * s),
                "🔍 战场缩放",
                Theme.MakeLabel((int)(18 * s), FontStyle.Bold, Theme.TextDim, TextAnchor.MiddleCenter));

            float curZoom = GameState.CurrentSave?.cameraZoom ?? 1f;
            float newZoom = GUI.HorizontalSlider(
                new Rect(px + 40 * s, zoomY + 24 * s, pw - 80 * s, 18 * s),
                curZoom, 0.5f, 1.5f);

            if (!Mathf.Approximately(newZoom, curZoom))
            {
                newZoom = Mathf.Clamp(newZoom, 0.5f, 1.5f);
                if (GameState.CurrentSave != null) GameState.CurrentSave.cameraZoom = newZoom;

                var hexView = _battleCtrl?.GetComponent<HexGridView>();
                hexView?.ApplyZoom(newZoom);
            }

            GUI.Label(new Rect(px + pw / 2 - 30 * s, zoomY + 42 * s, 60 * s, 18 * s),
                $"{newZoom:F2}x",
                Theme.MakeLabel((int)(13 * s), FontStyle.Normal, Theme.TextDim, TextAnchor.MiddleCenter));

            // 底部关卡信息
            var level = _battleCtrl?.CurrentLevel;
            if (level != null)
            {
                GUI.Label(new Rect(px, py + ph - 30 * s, pw, 25 * s),
                    $"{level.name}  第{_battleCtrl?.CurrentTurn}回合",
                    Theme.MakeLabel((int)(13 * s), FontStyle.Normal, Theme.TextDim, TextAnchor.MiddleCenter));
            }
        }

        void OnDisable()
        {
            if (_isOpen)
            {
                _isOpen = false;
                Time.timeScale = 1f;
            }
        }
    }
}
