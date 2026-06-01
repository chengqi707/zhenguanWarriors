using UnityEngine;
using ZhenguanWarriors.Core.UI;

namespace ZhenguanWarriors.View.BattleView
{
    /// <summary>
    /// 启动画面——显示Logo后自动进入主菜单
    /// </summary>
    public class SplashScreen : MonoBehaviour
    {
        private float _timer = 0f;
        private float _duration = 2.5f;  // 显示时长
        private float _alpha = 0f;
        private bool _done = false;

        void Start()
        {
            Debug.Log("[贞观勇士] 启动画面");
        }

        void Update()
        {
            if (_done) return;
            _timer += Time.deltaTime;

            // 淡入 0.5s → 保持 1.5s → 淡出 0.5s
            if (_timer < 0.5f)
                _alpha = _timer / 0.5f;
            else if (_timer > _duration - 0.5f)
                _alpha = (_duration - _timer) / 0.5f;
            else
                _alpha = 1f;

            if (_timer >= _duration)
            {
                _done = true;
                SwitchToMenu();
            }
        }

        void OnGUI()
        {
            // 背景（从黑到主题色渐变）
            Color bg = Color.Lerp(Color.black, Theme.PrimaryDark, Mathf.Min(1f, _timer * 2f));
            GUI.backgroundColor = bg;
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");
            GUI.backgroundColor = Color.white;

            // 主标题（金色，淡入）
            Color titleColor = new Color(Theme.Gold.r, Theme.Gold.g, Theme.Gold.b, _alpha);
            GUI.Label(new Rect(0, Screen.height / 2f - 80, Screen.width, 80),
                "贞观勇士",
                new GUIStyle
                {
                    fontSize = 48,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = titleColor }
                });

            // 副标题
            GUI.Label(new Rect(0, Screen.height / 2f, Screen.width, 40),
                "—— 李世民战棋录 ——",
                new GUIStyle
                {
                    fontSize = 20,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(Theme.TextDim.r, Theme.TextDim.g, Theme.TextDim.b, _alpha) }
                });

            // 底部版权
            GUI.Label(new Rect(0, Screen.height - 40, Screen.width, 30),
                "Powered by Unity  |  v1.0",
                new GUIStyle
                {
                    fontSize = 12,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.5f, 0.5f, 0.5f, _alpha * 0.5f) }
                });
        }

        private void SwitchToMenu()
        {
            Destroy(gameObject);
            var menu = new GameObject("MainMenu");
            menu.AddComponent<MainMenuController>();
        }
    }
}
