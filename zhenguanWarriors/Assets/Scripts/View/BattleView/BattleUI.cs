using UnityEngine;
using TMPro;
using ZhenguanWarriors.Core.Battle;

namespace ZhenguanWarriors.View.BattleView
{
    /// <summary>
    /// 战斗 UI：回合信息、操作提示等
    /// </summary>
    public class BattleUI : MonoBehaviour
    {
        [Header("UI 引用")]
        public TextMeshPro turnInfoText;
        public TextMeshPro phaseText;
        public TextMeshPro tipText;

        private void Start()
        {
            CreateCanvas();
        }

        private void CreateCanvas()
        {
            // 创建 UI Canvas（World Space 模式，方便与场景配合）
            var canvasGo = new GameObject("BattleCanvas");
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = canvas.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(12, 8);
            rect.localScale = Vector3.one * 0.02f;
            rect.position = new Vector3(6, 0, -2);

            // 回合标题（左上）
            turnInfoText = CreateText("TurnText", "第 1 回合",
                new Vector3(-6, 3.5f, 0), 3.5f, Color.white, rect);

            // 阶段文字（左上第二行）
            phaseText = CreateText("PhaseText", "玩家回合",
                new Vector3(-6, 2.8f, 0), 2.2f, new Color(0.6f, 0.8f, 1f), rect);

            // 底部操作提示
            tipText = CreateText("TipText", "左键选中己方单位 → 点击格子移动/攻击",
                new Vector3(0, -3.5f, 0), 1.8f, new Color(0.7f, 0.7f, 0.7f), rect);

            // 添加一个半透明背景条（底部提示）
            var bgGo = new GameObject("TipBg");
            bgGo.transform.SetParent(rect, false);
            var bgImage = bgGo.AddComponent<UnityEngine.UI.Image>();
            bgImage.color = new Color(0, 0, 0, 0.4f);
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.sizeDelta = new Vector2(14, 1.2f);
            bgRect.anchoredPosition = new Vector2(0, -3.5f);
        }

        private TextMeshPro CreateText(string name, string text,
            Vector3 pos, float fontSize, Color color, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            return tmp;
        }

        // ========== 公共更新方法 ==========

        public void UpdateTurnInfo(int turnNumber, string phaseName)
        {
            if (turnInfoText != null)
                turnInfoText.text = $"第 {turnNumber} 回合";
            if (phaseText != null)
                phaseText.text = phaseName;
        }

        public void ShowTip(string tip)
        {
            if (tipText != null)
                tipText.text = tip;
        }

        public void ShowActionLog(string log)
        {
            Debug.Log(log);
        }
    }
}
