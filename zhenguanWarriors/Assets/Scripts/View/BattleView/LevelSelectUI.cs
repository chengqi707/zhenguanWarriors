using UnityEngine;
using UnityEngine.UI;
using ZhenguanWarriors.Core.Level;
using ZhenguanWarriors.Core.Save;
using ZhenguanWarriors.Core.Character;
using ZhenguanWarriors.Core.UI;

namespace ZhenguanWarriors.View.BattleView
{
    /// <summary>
    /// 关卡选择 UI — uGUI 实现，替代 OnGUI 的 DrawLevelSelectUI
    /// 自动处理 DPI 缩放和触控
    /// </summary>
    public class LevelSelectUI : MonoBehaviour
    {
        private Canvas _canvas;
        private GameObject _cardContainer;
        private BattleTestController _battleCtrl;

        void Start()
        {
            _battleCtrl = GetComponent<BattleTestController>();
            CreateCanvas();
        }

        void CreateCanvas()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            // 容器
            _cardContainer = new GameObject("LevelCardContainer");
            _cardContainer.transform.SetParent(_canvas.transform, false);
            var containerRect = _cardContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0, 0);
            containerRect.anchorMax = new Vector2(1, 1);
            containerRect.offsetMin = new Vector2(30, 20);
            containerRect.offsetMax = new Vector2(-30, -20);
        }

        /// <summary>重建关卡卡片（每次进入关卡选择时调用）</summary>
        public void RebuildLevelCards()
        {
            // 清除旧卡片
            foreach (Transform child in _cardContainer.transform)
                Destroy(child.gameObject);

            var levelOrder = _battleCtrl.GetLevelOrder();
            if (levelOrder == null) return;

            float cardH = 130f;
            float gap = 10f;
            float startY = -80f; // 从上往下

            for (int i = 0; i < levelOrder.Count; i++)
            {
                string levelId = levelOrder[i];
                var level = LevelLibrary.Get(levelId);
                if (level == null) continue;

                bool unlocked = GameState.IsLevelUnlocked(levelId);
                int idx = i; // 闭包捕获

                // 卡片
                var cardObj = new GameObject($"Card_{i}");
                cardObj.transform.SetParent(_cardContainer.transform, false);
                var rect = cardObj.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(1, 1);
                rect.sizeDelta = new Vector2(0, cardH);
                rect.anchoredPosition = new Vector2(0, startY - i * (cardH + gap));
                rect.pivot = new Vector2(0.5f, 1);

                // 背景
                var img = cardObj.AddComponent<Image>();
                img.color = unlocked ? new Color(0.25f, 0.18f, 0.12f) : new Color(0.12f, 0.08f, 0.06f);

                // 左侧装饰条
                if (unlocked)
                {
                    var decorObj = new GameObject("Decor");
                    decorObj.transform.SetParent(cardObj.transform, false);
                    var decorRect = decorObj.AddComponent<RectTransform>();
                    decorRect.anchorMin = new Vector2(0, 0);
                    decorRect.anchorMax = new Vector2(0, 1);
                    decorRect.sizeDelta = new Vector2(6, 0);
                    decorRect.anchoredPosition = new Vector2(0, 0);
                    var decorImg = decorObj.AddComponent<Image>();
                    decorImg.color = Theme.Primary;
                }

                // 点击
                var btn = cardObj.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => {
                    if (_battleCtrl != null && unlocked)
                        _battleCtrl.SelectLevel(levelId);
                });

                // 关卡名
                CreateLabel(cardObj, $"第{i + 1}关  {level.name}",
                    new Vector2(0, -10), new Vector2(1, 30),
                    TextAnchor.UpperLeft, 20,
                    unlocked ? Theme.Gold : Theme.TextDim);

                // 信息行
                string info = level.victoryType switch
                {
                    VictoryConditionType.DefeatAll => "全灭敌军",
                    VictoryConditionType.DefeatBoss => "击破主将",
                    VictoryConditionType.DefendTurns => $"坚守{level.defendTurns}回合",
                    _ => "未知"
                };
                CreateLabel(cardObj, $"{info}    敌方{level.enemies.Count}人",
                    new Vector2(0, -48), new Vector2(1, 26),
                    TextAnchor.UpperLeft, 16, Theme.TextDim);

                // 出场角色
                string roster = string.Join(" ", level.availableCharacters.Take(4)
                    .Select(id => CharacterDatabase.Get(id)?.Name ?? id));
                if (level.availableCharacters.Count > 4) roster += " …";
                CreateLabel(cardObj, $"出场: {roster}",
                    new Vector2(0, -78), new Vector2(1, 22),
                    TextAnchor.UpperLeft, 14, new Color(0.7f, 0.9f, 0.7f));

                // 锁定图标
                if (!unlocked)
                {
                    CreateLabel(cardObj, "🔒",
                        new Vector2(-40, -40), new Vector2(50, 50),
                        TextAnchor.MiddleCenter, 30, Color.white);
                }
            }
        }

        private void CreateLabel(GameObject parent, string text, Vector2 pos, Vector2 size,
            TextAnchor anchor, int fontSize, Color color)
        {
            var obj = new GameObject("Label");
            obj.transform.SetParent(parent.transform, false);

            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 1);
            rect.offsetMin = new Vector2(20 + pos.x, -pos.y - size.y);
            rect.offsetMax = new Vector2(-20 + pos.x + size.x, -pos.y);

            var txt = obj.AddComponent<Text>();
            txt.text = text;
            txt.fontSize = fontSize;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.color = color;
            txt.alignment = anchor;
            txt.supportRichText = false;
        }

        public void Show()
        {
            if (_canvas != null) _canvas.enabled = true;
            RebuildLevelCards();
        }

        public void Hide()
        {
            if (_canvas != null) _canvas.enabled = false;
        }
    }
}
