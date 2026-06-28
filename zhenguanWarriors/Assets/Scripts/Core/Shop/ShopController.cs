using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ZhenguanWarriors.Core.Audio;
using ZhenguanWarriors.Core.Character;
using ZhenguanWarriors.Core.Combat;
using ZhenguanWarriors.Core.Save;
using ZhenguanWarriors.Core.Shop;
using ZhenguanWarriors.Core.UI;
using ZhenguanWarriors.Utils;

namespace ZhenguanWarriors.View.BattleView
{
    /// <summary>
    /// 商店 UI 控制器——战后/战前购买、出售装备
    /// </summary>
    public class ShopController : MonoBehaviour
    {
        public static ShopController Instance { get; private set; }

        private bool _isOpen;
        private bool _isBuyTab = true;
        private List<string> _stock = new();
        private string _selectedItemId;
        private string _message;
        private float _messageTimer;
        private Vector2 _listScroll;

        private const int REFRESH_COST = 50;
        private const int STOCK_COUNT = 6;

        public bool IsOpen => _isOpen;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnGUI()
        {
            if (!_isOpen) return;
            if (GameManager.Instance != null && GameManager.Instance.IsTransitioning) return;
            DrawShop();
        }

        /// <summary>打开商店</summary>
        public void Open()
        {
            if (_stock.Count == 0) RefreshStock();
            _isOpen = true;
            _isBuyTab = true;
            _selectedItemId = null;
            _message = string.Empty;
        }

        /// <summary>关闭商店</summary>
        public void Close()
        {
            _isOpen = false;
            _selectedItemId = null;
            _message = string.Empty;
        }

        private void RefreshStock()
        {
            _stock = ShopCatalog.GenerateStock(STOCK_COUNT);
            _selectedItemId = _stock.Count > 0 ? _stock[0] : null;
        }

        private void DrawShop()
        {
            float s = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            s = Mathf.Clamp(s, 0.6f, 1.5f);

            // 半透明遮罩
            GUI.backgroundColor = new Color(0f, 0f, 0f, 0.7f);
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");
            GUI.backgroundColor = Color.white;

            // 面板
            float pw = 720 * s, ph = 520 * s;
            float px = (Screen.width - pw) / 2, py = (Screen.height - ph) / 2;
            Theme.DrawPanel(new Rect(px, py, pw, ph));

            // 标题
            Theme.DrawTitle(new Rect(px, py + 10 * s, pw, 45 * s), "🏪 军械坊", (int)(32 * s));

            // 金币
            int gold = GameState.CurrentSave?.gold ?? 0;
            GUI.Label(new Rect(px + pw - 160 * s, py + 18 * s, 140 * s, 28 * s),
                $"💰 {gold}", Theme.MakeLabel((int)(20 * s), FontStyle.Bold, Theme.Gold, TextAnchor.MiddleRight));

            // 标签页
            float tabY = py + 65 * s;
            float tabW = 120 * s, tabH = 36 * s;
            GUI.backgroundColor = _isBuyTab ? Theme.Primary : Theme.BgCard;
            if (GUI.Button(new Rect(px + 20 * s, tabY, tabW, tabH), "购买", Theme.MakeButton((int)(18 * s))))
            {
                AudioManager.PlaySfx(AudioManager.SfxClips.Click);
                _isBuyTab = true;
                _selectedItemId = null;
                _message = string.Empty;
            }
            GUI.backgroundColor = !_isBuyTab ? Theme.Primary : Theme.BgCard;
            if (GUI.Button(new Rect(px + 20 * s + tabW + 10 * s, tabY, tabW, tabH), "出售", Theme.MakeButton((int)(18 * s))))
            {
                AudioManager.PlaySfx(AudioManager.SfxClips.Click);
                _isBuyTab = false;
                _selectedItemId = null;
                _message = string.Empty;
            }
            GUI.backgroundColor = Color.white;

            // 列表
            float listX = px + 20 * s, listY = tabY + tabH + 10 * s;
            float listW = 280 * s, listH = ph - (tabY - py) - tabH - 70 * s;
            Theme.DrawPanel(new Rect(listX, listY, listW, listH), null, Theme.BgCard);

            var items = _isBuyTab
                ? _stock
                : (GameState.CurrentSave?.inventoryEquipIds ?? new List<string>());

            _listScroll = GUI.BeginScrollView(
                new Rect(listX, listY, listW, listH),
                _listScroll,
                new Rect(0, 0, listW - 30 * s, items.Count * 40 * s));

            for (int i = 0; i < items.Count; i++)
            {
                string id = items[i];
                var equip = EquipmentLibrary.Get(id);
                if (equip == null) continue;
                bool sel = id == _selectedItemId;
                GUI.backgroundColor = sel ? Theme.Primary : Theme.BgPanel;
                if (GUI.Button(new Rect(5 * s, i * 40 * s, listW - 10 * s, 36 * s),
                    equip.name, Theme.MakeButton((int)(16 * s))))
                {
                    AudioManager.PlaySfx(AudioManager.SfxClips.Click);
                    _selectedItemId = id;
                    _message = string.Empty;
                }
                GUI.backgroundColor = Color.white;
            }
            GUI.EndScrollView();

            // 详情
            float detailX = listX + listW + 20 * s;
            float detailW = pw - listW - 60 * s;
            float detailY = listY;
            float detailH = listH;
            Theme.DrawPanel(new Rect(detailX, detailY, detailW, detailH), null, Theme.BgCard);

            if (!string.IsNullOrEmpty(_selectedItemId))
            {
                var equip = EquipmentLibrary.Get(_selectedItemId);
                if (equip != null)
                {
                    float dy = detailY + 10 * s;
                    GUI.Label(new Rect(detailX + 10 * s, dy, detailW - 20 * s, 30 * s),
                        equip.name, Theme.MakeLabel((int)(24 * s), FontStyle.Bold, ParseColor(equip.GetRarityColor()), TextAnchor.MiddleCenter));
                    dy += 36 * s;
                    GUI.Label(new Rect(detailX + 10 * s, dy, detailW - 20 * s, 22 * s),
                        $"{TypeName(equip.type)} · {RarityName(equip.rarity)}", Theme.MakeLabel((int)(16 * s), FontStyle.Normal, Theme.TextDim, TextAnchor.MiddleCenter));
                    dy += 30 * s;
                    GUI.Label(new Rect(detailX + 10 * s, dy, detailW - 20 * s, detailH - 120 * s),
                        DescribeEquip(equip), Theme.MakeLabel((int)(15 * s), FontStyle.Normal, Theme.Parchment));

                    dy = detailY + detailH - 60 * s;
                    int price = _isBuyTab ? ShopCatalog.GetBuyPrice(equip.id) : ShopCatalog.GetSellPrice(equip.id);
                    GUI.Label(new Rect(detailX + 10 * s, dy, detailW - 20 * s, 26 * s),
                        (_isBuyTab ? "售价：" : "回收价：") + price,
                        Theme.MakeLabel((int)(20 * s), FontStyle.Bold, Theme.Gold, TextAnchor.MiddleCenter));
                    dy += 32 * s;

                    bool canAfford = !_isBuyTab || (GameState.CurrentSave?.gold ?? 0) >= price;
                    GUI.enabled = canAfford;
                    GUI.backgroundColor = canAfford ? Theme.Primary : Theme.BgCard;
                    string btnLabel = _isBuyTab ? "购买" : "出售";
                    if (GUI.Button(new Rect(detailX + (detailW - 120 * s) / 2, dy, 120 * s, 36 * s), btnLabel, Theme.MakeButton((int)(18 * s))))
                    {
                        AudioManager.PlaySfx(AudioManager.SfxClips.Click);
                        if (_isBuyTab) TryBuy(equip.id, price);
                        else TrySell(equip.id, price);
                    }
                    GUI.backgroundColor = Color.white;
                    GUI.enabled = true;
                }
            }
            else
            {
                GUI.Label(new Rect(detailX, detailY + detailH / 2 - 15 * s, detailW, 30 * s),
                    "请选择物品", Theme.MakeLabel((int)(18 * s), FontStyle.Normal, Theme.TextDim, TextAnchor.MiddleCenter));
            }

            // 底部按钮
            float botY = py + ph - 50 * s;
            if (_isBuyTab)
            {
                GUI.enabled = (GameState.CurrentSave?.gold ?? 0) >= REFRESH_COST;
                GUI.backgroundColor = GUI.enabled ? Theme.Gold : Theme.BgCard;
                if (GUI.Button(new Rect(px + 20 * s, botY, 140 * s, 40 * s), $"刷新 ({REFRESH_COST})", Theme.MakeButton((int)(16 * s))))
                {
                    AudioManager.PlaySfx(AudioManager.SfxClips.Click);
                    TryRefresh();
                }
                GUI.backgroundColor = Color.white;
                GUI.enabled = true;
            }

            GUI.backgroundColor = Theme.PrimaryDark;
            if (GUI.Button(new Rect(px + pw - 160 * s, botY, 140 * s, 40 * s), "关闭", Theme.MakeButton((int)(18 * s))))
            {
                AudioManager.PlaySfx(AudioManager.SfxClips.Click);
                Close();
            }
            GUI.backgroundColor = Color.white;

            // 提示信息
            if (!string.IsNullOrEmpty(_message) && Time.realtimeSinceStartup < _messageTimer)
            {
                GUI.Label(new Rect(px, py + ph - 85 * s, pw, 24 * s), _message,
                    Theme.MakeLabel((int)(16 * s), FontStyle.Bold, Theme.Gold, TextAnchor.MiddleCenter));
            }
        }

        private void TryBuy(string id, int price)
        {
            var save = GameState.CurrentSave;
            if (save == null) return;
            if (save.gold < price)
            {
                ShowMessage("金币不足");
                return;
            }
            save.gold -= price;
            if (save.inventoryEquipIds == null) save.inventoryEquipIds = new List<string>();
            save.inventoryEquipIds.Add(id);
            SaveManager.AutoSave(save);
            ShowMessage($"购买成功：{EquipmentLibrary.Get(id)?.name}");
        }

        private void TrySell(string id, int price)
        {
            var save = GameState.CurrentSave;
            if (save == null) return;
            if (save.inventoryEquipIds == null || !save.inventoryEquipIds.Remove(id)) return;
            save.gold += price;
            SaveManager.AutoSave(save);
            _selectedItemId = null;
            ShowMessage($"出售成功，获得 {price} 金币");
        }

        private void TryRefresh()
        {
            var save = GameState.CurrentSave;
            if (save == null) return;
            if (save.gold < REFRESH_COST)
            {
                ShowMessage("金币不足，无法刷新");
                return;
            }
            save.gold -= REFRESH_COST;
            RefreshStock();
            SaveManager.AutoSave(save);
            ShowMessage("已刷新商品");
        }

        private void ShowMessage(string msg)
        {
            _message = msg;
            _messageTimer = Time.realtimeSinceStartup + 2f;
        }

        private static Color ParseColor(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out var c)) return c;
            return Color.white;
        }

        private static string TypeName(EquipmentType t) => t switch
        {
            EquipmentType.Weapon => "武器",
            EquipmentType.Armor => "防具",
            EquipmentType.Trinket => "饰品",
            _ => "其他"
        };

        private static string RarityName(EquipmentRarity r) => r switch
        {
            EquipmentRarity.Common => "普通",
            EquipmentRarity.Uncommon => "精良",
            EquipmentRarity.Rare => "稀有",
            EquipmentRarity.Epic => "史诗",
            _ => "未知"
        };

        private static string DescribeEquip(EquipmentData e)
        {
            var parts = new List<string>();
            if (e.strBonus != 0) parts.Add($"武+{e.strBonus}");
            if (e.cmdBonus != 0) parts.Add($"统+{e.cmdBonus}");
            if (e.intBonus != 0) parts.Add($"智+{e.intBonus}");
            if (e.agiBonus != 0) parts.Add($"敏+{e.agiBonus}");
            if (e.lukBonus != 0) parts.Add($"运+{e.lukBonus}");
            if (e.hpBonus != 0) parts.Add($"体+{e.hpBonus}");
            if (e.mpBonus != 0) parts.Add($"策+{e.mpBonus}");
            if (e.moveBonus != 0) parts.Add($"移+{e.moveBonus}");
            if (e.attackRangeBonus != 0) parts.Add($"射程+{e.attackRangeBonus}");
            if (e.strPercent != 0) parts.Add($"武+{e.strPercent}%");
            if (e.cmdPercent != 0) parts.Add($"统+{e.cmdPercent}%");
            if (e.intPercent != 0) parts.Add($"智+{e.intPercent}%");
            if (!string.IsNullOrEmpty(e.effectDesc)) parts.Add(e.effectDesc);

            if (e.classRestriction != null && e.classRestriction.Count > 0)
                parts.Add($"限定：{string.Join(",", e.classRestriction.Select(ClassName))}");
            if (e.maleOnly) parts.Add("男性限定");
            if (e.femaleOnly) parts.Add("女性限定");

            return parts.Count > 0 ? string.Join("  ", parts) : "无特殊效果";
        }

        private static string ClassName(ClassType c) => c switch
        {
            ClassType.Infantry => "步兵",
            ClassType.HeavyInfantry => "重步兵",
            ClassType.Cavalry => "骑兵",
            ClassType.Archer => "弓兵",
            ClassType.Siege => "器械",
            ClassType.Strategist => "谋士",
            _ => "全兵种"
        };

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
