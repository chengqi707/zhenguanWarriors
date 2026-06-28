using UnityEngine;
using ZhenguanWarriors.Core.UI;

namespace ZhenguanWarriors.View.BattleView
{
    /// <summary>
    /// 全屏简单视觉特效：闪白/闪色，用于暴击、升级、招募等反馈
    /// </summary>
    public class ScreenFx : MonoBehaviour
    {
        public static ScreenFx Instance { get; private set; }

        private bool _isFlashing;
        private Color _flashColor = Color.white;
        private float _flashDuration;
        private float _flashTimer;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>触发一次白色全屏闪烁</summary>
        public void FlashWhite(float duration = 0.12f)
        {
            Flash(Color.white, duration);
        }

        /// <summary>触发一次指定颜色全屏闪烁</summary>
        public void Flash(Color color, float duration = 0.12f)
        {
            _isFlashing = true;
            _flashColor = color;
            _flashDuration = Mathf.Max(0.01f, duration);
            _flashTimer = 0f;
        }

        void Update()
        {
            if (!_isFlashing) return;
            _flashTimer += Time.unscaledDeltaTime;
            if (_flashTimer >= _flashDuration)
                _isFlashing = false;
        }

        void OnGUI()
        {
            if (!_isFlashing) return;

            float t = _flashTimer / _flashDuration;
            // 前半段淡入，后半段淡出
            float alpha = t < 0.5f ? t * 2f : (1f - t) * 2f;
            alpha = Mathf.Clamp01(alpha) * 0.4f;

            Color c = _flashColor;
            c.a = alpha;
            GUI.backgroundColor = c;
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");
            GUI.backgroundColor = Color.white;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
