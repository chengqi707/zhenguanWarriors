using UnityEngine;
using ZhenguanWarriors.Core.Battle;
using ZhenguanWarriors.Core.Character;

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
        private SpriteRenderer _mpBarFill;

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
            if (visual._mainCam == null)
                Debug.LogWarning($"[UnitVisual] Camera.main 为空，单位 {unit.Name} 的屏幕坐标更新可能异常。");
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

            // --- 单位兵种形状 Sprite（优先加载外部图片 Assets/Sprites/{角色Id}.png） ---
            Sprite loaded = TryLoadPortrait(_unit.Id);
            if (loaded != null)
            {
                var go = new GameObject("UnitSprite");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.forward * 0;
                unitSprite = go.AddComponent<SpriteRenderer>();
                unitSprite.sprite = loaded;
                unitSprite.sortingOrder = 5;
                // 添加阵营色小圆点（左下角），方便区分敌我
                CreateFactionDot(unitRadius * 0.4f, color, transform,
                    new Vector3(-unitRadius * 0.65f, -unitRadius * 0.65f, 0.02f));
            }
            else
            {
                unitSprite = CreateUnitSprite("UnitSprite", unitRadius, color,
                    transform, Vector3.forward * 0, _unit.UnitClass);
            }

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

            // --- MP 条（蓝色，在HP条下方） ---
            var mpBg = CreateBarSprite("MpBarBg", 0.6f, 0.04f, new Color(0.15f, 0.15f, 0.25f),
                transform, Vector3.forward * -0.01f);
            mpBg.transform.localPosition = new Vector3(0, barY - 0.07f, -0.01f);
            var mpFill = CreateBarSprite("MpBarFill", 0.6f, 0.04f, new Color(0.3f, 0.5f, 1f),
                transform, Vector3.forward * -0.02f);
            mpFill.transform.localPosition = new Vector3(0, barY - 0.07f, -0.02f);

            // 存引用以便更新
            var mpFillRef = mpFill;
            _mpBarFill = mpFill;

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
            // TextMesh 自带 MeshRenderer，无需手动设置 material
            // Unity 会自动处理字体渲染

            // --- 兵种标签 ---
            var classGo = new GameObject("ClassLabel");
            classGo.transform.SetParent(transform, false);
            var classLabel = classGo.AddComponent<TextMesh>();
            classLabel.text = ClassData.GetName(_unit.UnitClass);
            classLabel.fontSize = 8;
            classLabel.color = new Color(0.8f, 0.8f, 0.6f);
            classLabel.anchor = TextAnchor.MiddleCenter;
            classLabel.alignment = TextAlignment.Center;
            classLabel.transform.localPosition = new Vector3(0, -unitRadius - 0.35f, -0.03f);
        }

        // ========== 生成纹理方法 ==========

        /// <summary>根据兵种创建不同形状的单位精灵</summary>
        private SpriteRenderer CreateUnitSprite(string name, float radius,
            Color color, Transform parent, Vector3 localPos, ClassType? classType = null)
        {
            int texSize = 64;
            int center = texSize / 2;
            int r = (int)(radius / 0.5f * center);
            var tex = new Texture2D(texSize, texSize);
            var pixels = new Color[texSize * texSize];

            System.Func<float, float, bool> inside = classType switch
            {
                ClassType.HeavyInfantry => (dx, dy) => Mathf.Abs(dx) < r * 0.85f && Mathf.Abs(dy) < r * 0.85f,
                ClassType.Cavalry      => (dx, dy) => dy > -r*0.7f && dy < r*0.8f && Mathf.Abs(dx * 1.5f) < (r*0.9f - dy*0.3f),
                ClassType.Archer       => (dx, dy) => Mathf.Abs(dx) + Mathf.Abs(dy) < r * 0.95f,
                ClassType.Siege        => (dx, dy) => HexCoordInHex(dx, dy, r * 0.95f),
                ClassType.Strategist   => (dx, dy) => CircleIn(dx, dy, r * 0.7f) || StarIn(dx, dy, r * 0.3f),
                _                      => (dx, dy) => Mathf.Sqrt(dx * dx + dy * dy) < r  // Infantry → circle
            };

            for (int x = 0; x < texSize; x++)
            for (int y = 0; y < texSize; y++)
            {
                float dx = x - center + 0.5f;
                float dy = y - center + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                int idx = y * texSize + x;

                if (inside(dx, dy))
                    pixels[idx] = color;
                else if (dist < r + 2f && dist > r - 3f)
                    pixels[idx] = new Color(color.r * 0.7f, color.g * 0.7f, color.b * 0.7f);
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

        // ---- 形状辅助函数 ----

        private static bool CircleIn(float dx, float dy, float r) => dx * dx + dy * dy < r * r;

        private static bool StarIn(float dx, float dy, float r)
        {
            float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 90;
            if (angle < 0) angle += 360;
            int spoke = (int)(angle / 72f) % 5;
            float spokeAngle = spoke * 72f;
            float cosA = Mathf.Cos(spokeAngle * Mathf.Deg2Rad);
            float sinA = Mathf.Sin(spokeAngle * Mathf.Deg2Rad);
            float proj = dx * cosA + dy * sinA;
            return proj > r * 0.6f && Mathf.Abs(dx * sinA - dy * cosA) < r * 0.25f;
        }

        private static bool HexCoordInHex(float dx, float dy, float size)
        {
            float q = (Mathf.Sqrt(3f) / 3f * dx - 1f / 3f * dy) / size;
            float r2 = (2f / 3f * dy) / size;
            float s = -q - r2;
            return Mathf.Abs(q) <= 1 && Mathf.Abs(r2) <= 1 && Mathf.Abs(s) <= 1;
        }

        /// <summary>创建阵营色小圆点（加载外部精灵时替代纯色圆块）</summary>
        private void CreateFactionDot(float dotRadius, Color color, Transform parent, Vector3 localPos)
        {
            int texSize = 16;
            int center = texSize / 2;
            int r = (int)(dotRadius / 0.5f * center);
            var tex = new Texture2D(texSize, texSize);
            var pixels = new Color[texSize * texSize];
            for (int x = 0; x < texSize; x++)
            for (int y = 0; y < texSize; y++)
            {
                float dx = x - center, dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                pixels[y * texSize + x] = dist < r ? color : (dist < r + 1.5f
                    ? new Color(color.r * 0.7f, color.g * 0.7f, color.b * 0.7f) : Color.clear);
            }
            tex.SetPixels(pixels); tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, texSize, texSize), Vector2.one * 0.5f, 64f);
            var go = new GameObject("FactionDot");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 6;
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

            float hpRatio = (float)_unit.CurrentHp / _unit.MaxHp;
            hpBarFill.transform.localScale = new Vector3(hpRatio, 1f, 1f);

            // 颜色按血量变化
            if (hpRatio > 0.5f)
                hpBarFill.color = hpGood;
            else if (hpRatio > 0.25f)
                hpBarFill.color = hpMid;
            else
                hpBarFill.color = hpLow;

            // MP条更新
            if (_mpBarFill != null)
            {
                float mpRatio = _unit.MaxMp > 0 ? (float)_unit.CurrentMp / _unit.MaxMp : 0f;
                _mpBarFill.transform.localScale = new Vector3(mpRatio, 1f, 1f);
            }
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

        /// <summary>
        /// 加载角色肖像（支持 PNG + JPEG），自动缩放适配六边形大小。
        /// 放入 Assets/Resources/Sprites/{角色Id}.png / {角色Id}.jpg
        /// 或按兵种：class_infantry.png 等。
        /// </summary>
        private Sprite TryLoadPortrait(string charId)
        {
            if (string.IsNullOrEmpty(charId)) return null;

            // 目标世界大小（≈ 六边形宽度的 75%，不遮挡邻格）
            float targetWorldSize = 0.65f;

            // 尝试按角色Id加载（PNG 优先）
            Texture2D tex = TryLoadTexture(charId);
            if (tex == null)
                tex = TryLoadTexture($"class_{_unit.UnitClass.ToString().ToLower()}");
            if (tex == null) return null;

            // 计算 PPU：让图片在 world 中约 targetWorldSize 宽
            int w = tex.width;
            float ppu = w / targetWorldSize;
            if (ppu < 1f) ppu = 64f;

            var sprite = Sprite.Create(tex, new Rect(0, 0, w, tex.height),
                new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
            sprite.name = charId;
            return sprite;
        }

        /// <summary>从 Resources/Sprites/ 加载纹理（PNG/JPEG 均支持）</summary>
        private Texture2D TryLoadTexture(string name)
        {
            // 直接 Load<Texture2D> 兼容 PNG 和 JPEG
            Texture2D tex = Resources.Load<Texture2D>($"Sprites/{name}");
            if (tex != null) return tex;
            return null;
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
