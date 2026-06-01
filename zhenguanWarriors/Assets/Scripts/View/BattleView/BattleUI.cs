using UnityEngine;
using ZhenguanWarriors.Core.Battle;

namespace ZhenguanWarriors.View.BattleView
{
    /// <summary>
    /// 战斗 UI：回合信息、操作提示（使用内建 TextMesh，无需TMP）
    /// </summary>
    public class BattleUI : MonoBehaviour
    {
        private TextMesh _turnInfoText;
        private TextMesh _phaseText;
        private TextMesh _tipText;

        private void Start()
        {
            CreateUI();
        }

        private void CreateUI()
        {
            // 回合标题（左上）
            _turnInfoText = CreateText("TurnText", "第 1 回合",
                new Vector3(0.5f, 7.0f, -1f), 28, Color.white,
                TextAnchor.UpperLeft);

            // 阶段文字（左上第二行）
            _phaseText = CreateText("PhaseText", "玩家回合",
                new Vector3(0.5f, 6.3f, -1f), 20, new Color(0.6f, 0.8f, 1f),
                TextAnchor.UpperLeft);

            // 底部操作提示背景（深色条）
            var bgGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bgGo.name = "TipBg";
            bgGo.transform.localScale = new Vector3(12f, 0.8f, 1f);
            bgGo.transform.position = new Vector3(6f, 0.5f, -1f);
            var bgRenderer = bgGo.GetComponent<Renderer>();
            bgRenderer.material = new Material(Shader.Find("Sprites/Default"));
            bgRenderer.material.color = new Color(0, 0, 0, 0.5f);

            // 底部操作提示
            _tipText = CreateText("TipText", "左键选中己方单位 → 点击格子移动/攻击  |  Space=结束回合",
                new Vector3(6f, 0.5f, -0.5f), 16, new Color(0.8f, 0.8f, 0.8f),
                TextAnchor.MiddleCenter);
        }

        private TextMesh CreateText(string name, string content,
            Vector3 position, int fontSize, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name);
            go.transform.position = position;

            var text = go.AddComponent<TextMesh>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.anchor = anchor;
            text.alignment = TextAlignment.Center;

            // TextMesh 自带 MeshRenderer，无需手动添加
            // 让文字始终面向摄像机（用 Billboard 方式？不——固定位置更稳定）
            // 在 orthographic 摄像机下，World Space TextMesh 总是可见

            return text;
        }

        // ========== 公共更新方法 ==========

        public void UpdateTurnInfo(int turnNumber, string phaseName)
        {
            if (_turnInfoText != null)
                _turnInfoText.text = $"第 {turnNumber} 回合";
            if (_phaseText != null)
                _phaseText.text = phaseName;
        }

        public void ShowTip(string tip)
        {
            if (_tipText != null)
                _tipText.text = tip;
        }
    }
}
