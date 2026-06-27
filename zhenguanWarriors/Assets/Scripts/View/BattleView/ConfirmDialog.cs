using UnityEngine;
using ZhenguanWarriors.Core.UI;

namespace ZhenguanWarriors.View.BattleView
{
    /// <summary>
    /// 通用确认弹窗——模态对话框，覆盖在所有UI之上
    /// </summary>
    public class ConfirmDialog : MonoBehaviour
    {
        private string _title;
        private string _message;
        private string _confirmText;
        private string _cancelText;
        private System.Action _onConfirm;
        private System.Action _onCancel;
        private bool _isOpen;
        private float _scale;

        public bool IsOpen => _isOpen;

        void OnGUI()
        {
            if (!_isOpen) return;
            float s = _scale;

            // 半透明遮罩（阻止下层点击）
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");

            // 对话框（限制在安全区）
            float w = 440 * s;
            float h = 220 * s;
            float x = (Screen.width - w) / 2;
            float y = (Screen.height - h) / 2;
            Rect dlgRect = Theme.ClampToSafeArea(new Rect(x, y, w, h));
            x = dlgRect.x;
            y = dlgRect.y;

            GUI.backgroundColor = Theme.BgPanel;
            GUI.Box(new Rect(x, y, w, h), "");
            GUI.backgroundColor = Color.white;

            // 装饰线
            GUI.backgroundColor = Theme.Primary;
            GUI.Box(new Rect(x, y, w, 4 * s), "");
            GUI.backgroundColor = Color.white;

            // 标题
            GUI.Label(new Rect(x + 20 * s, y + 20 * s, w - 40 * s, 35 * s),
                _title, Theme.MakeLabel((int)(22 * s), FontStyle.Bold, Theme.Gold));

            // 消息
            GUI.Label(new Rect(x + 20 * s, y + 65 * s, w - 40 * s, 70 * s),
                _message, Theme.MakeLabel((int)(16 * s), FontStyle.Normal, Theme.TextLight));

            // 取消按钮
            GUI.backgroundColor = Theme.BgCard;
            if (GUI.Button(new Rect(x + 30 * s, y + h - 60 * s, 170 * s, 45 * s),
                _cancelText, Theme.MakeButton((int)(16 * s))))
            {
                Close();
                _onCancel?.Invoke();
            }

            // 确认按钮
            GUI.backgroundColor = Theme.Primary;
            if (GUI.Button(new Rect(x + w - 200 * s, y + h - 60 * s, 170 * s, 45 * s),
                _confirmText, Theme.MakeButton((int)(16 * s))))
            {
                Close();
                _onConfirm?.Invoke();
            }
            GUI.backgroundColor = Color.white;
        }

        public void Show(string title, string message,
            System.Action onConfirm, System.Action onCancel = null,
            string confirmText = "确认", string cancelText = "取消")
        {
            _title = title;
            _message = message;
            _confirmText = confirmText;
            _cancelText = cancelText;
            _onConfirm = onConfirm;
            _onCancel = onCancel;
            _scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            if (_scale < 0.5f) _scale = 0.5f;
            if (_scale > 2.0f) _scale = 2.0f;
            _isOpen = true;
        }

        public void Close()
        {
            _isOpen = false;
        }
    }
}
