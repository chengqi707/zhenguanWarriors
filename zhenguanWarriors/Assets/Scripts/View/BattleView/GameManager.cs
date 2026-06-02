using UnityEngine;
using ZhenguanWarriors.Core.Save;

namespace ZhenguanWarriors.View.BattleView
{
    /// <summary>
    /// 游戏页面状态机——管理所有页面的生命周期和切换
    /// 唯一 GameRoot，组件通过 enabled 开关控制，杜绝 Destroy/Create 重叠
    /// </summary>
    public enum GamePage
    {
        Splash,         // 启动画面
        MainMenu,       // 主菜单
        Settings,       // 设置（主菜单子页）
        LevelSelect,    // 关卡选择
        Story,          // 剧情对话（叠加层）
        PreBattle,      // 战前编组
        Battle,         // 战斗中
        Results         // 战斗结算
    }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GamePage CurrentPage { get; private set; } = GamePage.Splash;
        public bool IsTransitioning { get; private set; } = false;

        // 组件引用
        private SplashScreen _splash;
        private MainMenuController _mainMenu;
        private HexGridView _hexView;
        private BattleTestController _battleCtrl;
        private DialogueUI _dialogue;
        private BattleUI _battleUI;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 按依赖顺序添加组件
            _splash = gameObject.AddComponent<SplashScreen>();
            _mainMenu = gameObject.AddComponent<MainMenuController>();
            _hexView = gameObject.AddComponent<HexGridView>();
            _battleUI = gameObject.AddComponent<BattleUI>();
            _battleCtrl = gameObject.AddComponent<BattleTestController>();
            _dialogue = gameObject.AddComponent<DialogueUI>();

            // 初始全部禁用
            SetAllEnabled(false);
            _splash.enabled = true;

            Debug.Log("[GameManager] 初始化完成，启动画面");
        }

        /// <summary>切换到指定页面（带过渡保护）</summary>
        public void TransitionTo(GamePage page, System.Action onComplete = null)
        {
            IsTransitioning = true;
            CurrentPage = page;

            SetAllEnabled(false);

            switch (page)
            {
                case GamePage.Splash:
                    _splash.enabled = true;
                    break;

                case GamePage.MainMenu:
                case GamePage.Settings:
                    _mainMenu.enabled = true;
                    _mainMenu.SetPage(page);
                    break;

                case GamePage.LevelSelect:
                case GamePage.PreBattle:
                case GamePage.Battle:
                case GamePage.Results:
                    _hexView.enabled = true;
                    _battleCtrl.enabled = true;
                    _battleCtrl.SetPage(page);
                    break;

                case GamePage.Story:
                    // 保留当前页面组件，同时启用对话
                    RestorePreviousPage();
                    _dialogue.enabled = true;
                    break;
            }

            IsTransitioning = false;
            onComplete?.Invoke();
        }

        private GamePage _previousPage;

        /// <summary>进入Story时记住当前页</summary>
        public void EnterStory()
        {
            _previousPage = CurrentPage;
            _dialogue.enabled = true;
        }

        /// <summary>退出Story时恢复</summary>
        public void ExitStory()
        {
            _dialogue.enabled = false;
            // _battleCtrl 或 _mainMenu 已在之前启用
        }

        private void RestorePreviousPage()
        {
            switch (_previousPage)
            {
                case GamePage.PreBattle:
                case GamePage.LevelSelect:
                case GamePage.Results:
                    _hexView.enabled = true;
                    _battleCtrl.enabled = true;
                    break;
                case GamePage.MainMenu:
                case GamePage.Settings:
                    _mainMenu.enabled = true;
                    break;
                default:
                    _hexView.enabled = true;
                    _battleCtrl.enabled = true;
                    break;
            }
        }

        private void SetAllEnabled(bool enabled)
        {
            _splash.enabled = enabled;
            _mainMenu.enabled = enabled;
            _hexView.enabled = enabled;
            _battleCtrl.enabled = enabled;
            _dialogue.enabled = enabled;
        }
    }
}
