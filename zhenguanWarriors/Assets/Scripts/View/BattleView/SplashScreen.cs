using UnityEngine;
using ZhenguanWarriors.Core.UI;

namespace ZhenguanWarriors.View.BattleView
{
    /// <summary>
    /// 启动画面——显示Logo后通知GameManager切换到主菜单
    /// </summary>
    public class SplashScreen : MonoBehaviour
    {
        private float _timer = 0f;
        private float _duration = 2.5f;
        private float _alpha = 0f;
        private bool _done = false;

        void OnEnable()
        {
            _timer = 0f;
            _alpha = 0f;
            _done = false;
        }

        void Update()
        {
            if (_done) return;
            _timer += Time.deltaTime;

            if (_timer >= _duration)
            {
                _done = true;
                if (GameManager.Instance != null)
                    GameManager.Instance.TransitionTo(GamePage.MainMenu);
            }
        }

        void OnGUI()
        {
            if (GameManager.Instance != null && GameManager.Instance.IsTransitioning) return;

            // 淡入淡出计算
            if (_timer < 0.5f)
                _alpha = _timer / 0.5f;
            else if (_timer > _duration - 0.5f)
                _alpha = (_duration - _timer) / 0.5f;
            else
                _alpha = 1f;

            // 背景渐变
            Color bg = Color.Lerp(Color.black, Theme.PrimaryDark, Mathf.Min(1f, _timer * 2f));
            GUI.backgroundColor = bg;
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");
            GUI.backgroundColor = Color.white;

            // 标题
            Color titleColor = new Color(Theme.Gold.r, Theme.Gold.g, Theme.Gold.b, _alpha);
            GUI.Label(new Rect(0, Screen.height / 2f - 80, Screen.width, 80),
                "贞观勇士",
                new GUIStyle { fontSize = 48, fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter, normal = { textColor = titleColor } });

            // 副标题
            GUI.Label(new Rect(0, Screen.height / 2f, Screen.width, 40),
                "—— 李世民战棋录 ——",
                new GUIStyle { fontSize = 20, alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(Theme.TextDim.r, Theme.TextDim.g, Theme.TextDim.b, _alpha) } });

            // 版本
            GUI.Label(new Rect(0, Screen.height - 40, Screen.width, 30),
                "v1.0", new GUIStyle { fontSize = 12, alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.5f, 0.5f, 0.5f, _alpha * 0.5f) } });
        }
    }
}
