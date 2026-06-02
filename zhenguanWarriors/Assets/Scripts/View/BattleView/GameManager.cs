using UnityEngine;
using ZhenguanWarriors.Core.Save;

namespace ZhenguanWarriors.View.BattleView
{
    public enum GamePage
    {
        Splash, MainMenu, Settings, LevelSelect,
        Story, HeroSelect, EquipSetup, Battle, Results
    }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GamePage CurrentPage { get; private set; } = GamePage.Splash;
        public bool IsTransitioning { get; private set; } = false;
        public bool IsPaused { get; private set; } = false;

        // 组件
        private SplashScreen _splash;
        private MainMenuController _mainMenu;
        private HexGridView _hexView;
        private BattleTestController _battleCtrl;
        private DialogueUI _dialogue;
        private PauseMenu _pauseMenu;
        private ConfirmDialog _confirm;
        private LevelSelectUI _levelSelectUI;
        private GamePage _previousPage;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _splash = gameObject.AddComponent<SplashScreen>();
            _mainMenu = gameObject.AddComponent<MainMenuController>();
            _hexView = gameObject.AddComponent<HexGridView>();
            gameObject.AddComponent<BattleUI>();
            _battleCtrl = gameObject.AddComponent<BattleTestController>();
            _dialogue = gameObject.AddComponent<DialogueUI>();
            _confirm = gameObject.AddComponent<ConfirmDialog>();
            _pauseMenu = gameObject.AddComponent<PauseMenu>();
            _levelSelectUI = gameObject.AddComponent<LevelSelectUI>();

            // 横屏模式
            Screen.orientation = ScreenOrientation.LandscapeLeft;

            SetAllEnabled(false);
            _splash.enabled = true;
            Debug.Log("[GameManager] 初始化完成");
        }

        void Update()
        {
            // 对话框打开时返回键关闭对话框
            if (_confirm.IsOpen)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    _confirm.Close();
                return;
            }

            // 暂停菜单打开时返回键继续战斗
            if (_pauseMenu.IsOpen)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    _pauseMenu.Close();
                return;
            }

            // 过渡中不处理
            if (IsTransitioning) return;

            // Android 返回键
            if (Input.GetKeyDown(KeyCode.Escape))
                HandleBackButton();
        }

        void HandleBackButton()
        {
            switch (CurrentPage)
            {
                case GamePage.Splash: break;

                case GamePage.MainMenu:
                    _confirm.Show("退出游戏？",
                        "退出后进度不会丢失，\n下次可从主菜单继续。",
                        () => Application.Quit(),
                        null, "确认退出", "取消");
                    break;

                case GamePage.Settings:
                    TransitionTo(GamePage.MainMenu);
                    break;

                case GamePage.LevelSelect:
                    _confirm.Show("选关菜单",
                        "返回主菜单 或 继续选关？",
                        () => TransitionTo(GamePage.MainMenu),
                        null, "返回主菜单", "继续选关");
                    break;

                case GamePage.HeroSelect:
                case GamePage.EquipSetup:
                    TransitionTo(GamePage.LevelSelect);
                    break;

                case GamePage.Story:
                    _dialogue?.FastForward();
                    break;

                case GamePage.Battle:
                    _pauseMenu.Open();
                    break;

                case GamePage.Results: break;
            }
        }

        public void TransitionTo(GamePage page, System.Action onComplete = null)
        {
            IsTransitioning = true;
            CurrentPage = page;
            SetAllEnabled(false);

            switch (page)
            {
                case GamePage.Splash:       _splash.enabled = true; break;
                case GamePage.MainMenu:
                case GamePage.Settings:
                    _mainMenu.enabled = true;
                    _mainMenu.SetPage(page);
                    break;
                case GamePage.LevelSelect:
                    _hexView.enabled = true;
                    _battleCtrl.enabled = true;
                    _battleCtrl.SetPage(page);
                    // uGUI 关卡选择：显示 Canvas
                    _levelSelectUI.Show();
                    break;
                case GamePage.HeroSelect:
                case GamePage.EquipSetup:
                    _hexView.enabled = true;
                    _battleCtrl.enabled = true;
                    _battleCtrl.SetPage(page);
                    _levelSelectUI.Hide();
                    break;
                case GamePage.Battle:
                case GamePage.Results:
                    _hexView.enabled = true;
                    _battleCtrl.enabled = true;
                    _battleCtrl.SetPage(page);
                    _levelSelectUI.Hide();
                    break;
                case GamePage.Story:
                    RestorePreviousPage();
                    _dialogue.enabled = true;
                    break;
            }

            IsTransitioning = false;
            onComplete?.Invoke();
        }

        public void EnterStory()
        {
            _previousPage = CurrentPage;
            _dialogue.enabled = true;
        }

        public void ExitStory()
        {
            _dialogue.enabled = false;
        }

        void RestorePreviousPage()
        {
            switch (_previousPage)
            {
                case GamePage.HeroSelect:
                case GamePage.EquipSetup:
                case GamePage.LevelSelect:
                    _hexView.enabled = true;
                    _battleCtrl.enabled = true;
                    break;
                default:
                    _hexView.enabled = true;
                    _battleCtrl.enabled = true;
                    break;
            }
        }

        void SetAllEnabled(bool enabled)
        {
            _splash.enabled = enabled;
            _mainMenu.enabled = enabled;
            _hexView.enabled = enabled;
            _battleCtrl.enabled = enabled;
            _dialogue.enabled = enabled;
            if (!enabled) _levelSelectUI.Hide(); // 离开关卡选择时隐藏
        }

        public void SetPaused(bool paused)
        {
            IsPaused = paused;
        }
    }
}
