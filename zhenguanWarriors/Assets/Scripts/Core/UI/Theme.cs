using UnityEngine;

namespace ZhenguanWarriors.Core.UI
{
    /// <summary>
    /// 唐风UI主题色——朱红+宣纸黄
    /// </summary>
    public static class Theme
    {
        // ===== 主色调 =====
        public static Color Primary => new Color(0.72f, 0.15f, 0.12f);    // 朱红
        public static Color PrimaryLight => new Color(0.85f, 0.25f, 0.20f);
        public static Color PrimaryDark => new Color(0.55f, 0.10f, 0.08f);

        public static Color Gold => new Color(0.90f, 0.75f, 0.30f);      // 金色
        public static Color GoldLight => new Color(0.95f, 0.85f, 0.40f);

        public static Color Parchment => new Color(0.96f, 0.92f, 0.82f); // 宣纸黄
        public static Color ParchmentDark => new Color(0.85f, 0.80f, 0.70f);

        // ===== 文字颜色 =====
        public static Color TextMain => new Color(0.15f, 0.10f, 0.05f);  // 墨色
        public static Color TextLight => new Color(0.95f, 0.92f, 0.85f); // 浅色文字
        public static Color TextDim => new Color(0.50f, 0.45f, 0.40f);   // 暗淡文字
        public static Color TextAccent => Gold;                          // 强调文字

        // ===== 功能色 =====
        public static Color HpGreen => new Color(0.20f, 0.75f, 0.20f);
        public static Color HpYellow => new Color(0.85f, 0.70f, 0.10f);
        public static Color HpRed => new Color(0.85f, 0.15f, 0.10f);
        public static Color MpBlue => new Color(0.20f, 0.45f, 0.85f);
        public static Color BuffCyan => new Color(0.20f, 0.80f, 0.85f);

        // ===== 背景色 =====
        public static Color BgDark => new Color(0.12f, 0.08f, 0.05f);     // 深褐
        public static Color BgPanel => new Color(0.20f, 0.14f, 0.10f);    // 面板背景
        public static Color BgCard => new Color(0.28f, 0.20f, 0.14f);     // 卡片背景

        // ===== 兵种色 =====
        public static Color ClassInfantry => new Color(0.50f, 0.50f, 0.50f);
        public static Color ClassHeavyInf => new Color(0.40f, 0.35f, 0.30f);
        public static Color ClassCavalry => new Color(0.70f, 0.30f, 0.10f);
        public static Color ClassArcher => new Color(0.10f, 0.55f, 0.20f);
        public static Color ClassSiege => new Color(0.40f, 0.25f, 0.55f);
        public static Color ClassStrategist => new Color(0.10f, 0.40f, 0.70f);
        public static Color BossGold => new Color(0.90f, 0.70f, 0.10f);

        // ===== 阵营色 =====
        public static Color FactionPlayer => new Color(0.20f, 0.40f, 0.80f);
        public static Color FactionEnemy => Primary;
        public static Color FactionAlly => new Color(0.20f, 0.70f, 0.30f);

        // ===== GUIStyle 辅助 =====

        public static GUIStyle MakeLabel(int fontSize = 14, FontStyle fontStyle = FontStyle.Normal,
            Color? color = null, TextAnchor align = TextAnchor.UpperLeft)
        {
            return new GUIStyle
            {
                fontSize = fontSize,
                fontStyle = fontStyle,
                alignment = align,
                normal = { textColor = color ?? TextLight }
            };
        }

        public static GUIStyle MakeButton(int fontSize = 16, FontStyle fontStyle = FontStyle.Bold)
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = fontSize,
                fontStyle = fontStyle,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = TextLight;
            style.hover.textColor = GoldLight;
            return style;
        }

        /// <summary>绘制唐风面板背景</summary>
        public static void DrawPanel(Rect rect, string title = null, Color? bgColor = null)
        {
            GUI.backgroundColor = bgColor ?? BgPanel;
            GUI.Box(rect, title ?? "");
            GUI.backgroundColor = Color.white;
        }

        /// <summary>绘制朱红标题</summary>
        public static void DrawTitle(Rect rect, string text, int fontSize = 28)
        {
            GUI.Label(rect, text, MakeLabel(fontSize, FontStyle.Bold, Gold, TextAnchor.MiddleCenter));
        }

        // ===== 屏幕边界安全工具 =====

        /// <summary>将 Rect 裁剪到屏幕范围内，防止 UI 溢出屏幕边缘</summary>
        public static Rect ClampToScreen(Rect r)
        {
            float sw = Screen.width;
            float sh = Screen.height;
            float x = UnityEngine.Mathf.Clamp(r.x, 0, sw - 10);
            float y = UnityEngine.Mathf.Clamp(r.y, 0, sh - 10);
            float w = UnityEngine.Mathf.Min(r.width, sw - x);
            float h = UnityEngine.Mathf.Min(r.height, sh - y);
            w = UnityEngine.Mathf.Max(w, 10); // 最小宽度
            h = UnityEngine.Mathf.Max(h, 10); // 最小高度
            return new Rect(x, y, w, h);
        }
    }
}
