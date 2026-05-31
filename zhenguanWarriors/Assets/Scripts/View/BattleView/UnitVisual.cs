using UnityEngine;
using ZhenguanWarriors.Core.Battle;

namespace ZhenguanWarriors.View.BattleView
{
    /// <summary>
    /// 单位可视化：圆形Sprite + HP血条 + 选中高亮
    /// </summary>
    public class UnitVisual : MonoBehaviour
    {
        [Header("组件引用")]
        public SpriteRenderer unitSprite;
        public SpriteRenderer hpBarBg;
        public SpriteRenderer hpBarFill;
        public SpriteRenderer selectionRing;
        public TextMesh nameLabel;

        [Header("颜色")]
        public Color playerColor = new Color(0.2f, 0.5f, 0.9f);   // 蓝
        public Color enemyColor = new Color(0.9f, 0.2f, 0.2f);    // 红
        public Color allyColor = new Color(0.2f, 0.8f, 0.3f);     // 绿
        public Color hpGood = new Color(0.2f, 0.8f, 0.2f);
        public Color hpMid = new Color(0.9f, 0.7f, 0.1f);
        public Color hpLow = new Color(0.9f, 0.2f, 0.2f);

        private BattleUnit _unit;
        private HexGridView _hexView;
        private Camera _mainCam;

        // ========== 工厂方法 ==========

        public static UnitVisual Create(BattleUnit unit, HexGridView hexView)
        {
            var go = new GameObject($"Unit_{unit.Name}");
            var visual = go.AddComponent<UnitVisual>();
            visual._unit = unit;
            visual._hexView = hexView;
            visual._mainCam = Camera.main;
            visual.BuildVisuals();
            visual.UpdatePosition();
            visual.UpdateHpBar();
            return visual;
        }

        // ========== 构建可视化元素 ==========

        private void BuildVisuals()
        {
            float unitRadius = 0.35f;
            Color color = GetFactionColor();

            // --- 单位圆形 Sprite ---
            unitSprite = CreateCircleSprite("UnitSprite", unitRadius, color,
                transform, Vector3.forward * 0);

            // --- HP 条背景 ---
            hpBarBg = CreateBarSprite("HpBarBg", 0.6f, 0.06f, new Color(0.2f, 0.2f, 0.2f),
                transform, Vector3.forward * -0.01f);

            // --- HP 条填充 ---
            hpBarFill = CreateBarSprite("HpBarFill", 0.6f, 0.06f, hpGood,
                transform, Vector3.forward * -0.02f);

            // 血条位置：单位上方
            float barY = unitRadius + 0.12f;
            hpBarBg.transform.localPosition = new Vector3(0, barY, -0.01f);
            hpBarFill.transform.localPosition = new Vector3(0, barY, -0.02f);

            // --- 选中高亮环 ---
            selectionRing = CreateRingSprite("SelectionRing", unitRadius + 0.08f,
                new Color(1f, 1f, 0.3f, 0.6f),
                transform, Vector3.forward * 0.01f);
            selectionRing.gameObject.SetActive(false);

            // --- 名字标签（TextMesh，无需TMP） ---
            var labelGo = new GameObject("NameLabel");
            labelGo.transform.SetParent(transform, false);
            nameLabel = labelGo.AddComponent<TextMesh>();
            nameLabel.text = _unit.Name;
            nameLabel.fontSize = 10;
            nameLabel.color = Color.white;
            nameLabel.anchor = TextAnchor.MiddleCenter;
            nameLabel.alignment = TextAlignment.Center;
            nameLabel.transform.localPosition = new Vector3(0, -unitRadius - 0.15f, -0.03f);
            var labelMr = labelGo.AddComponent<MeshRenderer>();
            labelMr.material = nameLabel.font.material;
        }

        // ========== 生成纹理方法 ==========

        private SpriteRenderer CreateCircleSprite(string name, float radius,
            Color color, Transform parent, Vector3 localPos)
        {
            int texSize = 64;
            int center = texSize / 2;
            int r = (int)(radius / 0.5f * center); // scale radius to tex coords
            var tex = new Texture2D(texSize, texSize);
            var pixels = new Color[texSize * texSize];

            for (int x = 0; x < texSize; x++)
            for (int y = 0; y < texSize; y++)
            {
                float dx = x - center + 0.5f;
                float dy = y - center + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                int idx = y * texSize + x;

                if (dist < r)
                    pixels[idx] = color;
                else if (dist < r + 2f)
                    pixels[idx] = new Color(color.r * 0.7f, color.g * 0.7f, color.b * 0.7f); // 边缘
                else
                    pixels[idx] = Color.clear;
            }

            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;

            var sprite = Sprite.Create(tex, new Rect(0, 0, texSize, texSize),
                new Vector2(0.5f, 0.5f), 64f);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 5;
            return sr;
        }

        private SpriteRenderer CreateBarSprite(string name, float width, float height,
            Color color, Transform parent, Vector3 localPos)
        {
            int texW = 32;
            int texH = 4;
            var tex = new Texture2D(texW, texH);
            var pixels = new Color[texW * texH];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;

            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Point;

            var sprite = Sprite.Create(tex,
                new Rect(0, 0, texW, texH),
                new Vector2(0f, 0.5f), // pivot left
                texW / width); // PPU：确保世界单位宽度正确

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 6;
            return sr;
        }

        private SpriteRenderer CreateRingSprite(string name, float radius,
            Color color, Transform parent, Vector3 localPos)
        {
            int texSize = 64;
            int center = texSize / 2;
            int r = (int)(radius / 0.5f * center);
            int ringWidth = 3;
            var tex = new Texture2D(texSize, texSize);
            var pixels = new Color[texSize * texSize];

            for (int x = 0; x < texSize; x++)
            for (int y = 0; y < texSize; y++)
            {
                float dx = x - center + 0.5f;
                float dy = y - center + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                int idx = y * texSize + x;

                if (dist >= r - ringWidth && dist <= r)
                    pixels[idx] = color;
                else
                    pixels[idx] = Color.clear;
            }

            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;

            var sprite = Sprite.Create(tex, new Rect(0, 0, texSize, texSize),
                new Vector2(0.5f, 0.5f), 64f);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 7;
            sr.gameObject.SetActive(false);
            return sr;
        }

        // ========== 更新方法 ==========

        public void UpdatePosition()
        {
            transform.position = _hexView.HexToWorld(_unit.Position);
        }

        public void UpdateHpBar()
        {
            if (hpBarFill == null || _unit == null) return;

            float ratio = (float)_unit.CurrentHp / _unit.MaxHp;
            hpBarFill.transform.localScale = new Vector3(ratio, 1f, 1f);

            // 颜色按血量变化
            if (ratio > 0.5f)
                hpBarFill.color = hpGood;
            else if (ratio > 0.25f)
                hpBarFill.color = hpMid;
            else
                hpBarFill.color = hpLow;
        }

        public void SetSelected(bool selected)
        {
            if (selectionRing != null)
                selectionRing.gameObject.SetActive(selected);
        }

        public void UpdateNameVisibility()
        {
            if (nameLabel != null)
                nameLabel.gameObject.SetActive(false); // 简版先隐藏，复杂版再加
        }

        private Color GetFactionColor() => _unit.Faction switch
        {
            Faction.Player => playerColor,
            Faction.Enemy => enemyColor,
            Faction.Ally => allyColor,
            _ => Color.gray
        };
    }
}
