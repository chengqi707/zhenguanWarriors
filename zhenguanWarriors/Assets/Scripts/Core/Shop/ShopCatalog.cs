using System.Collections.Generic;
using ZhenguanWarriors.Core.Combat;

namespace ZhenguanWarriors.Core.Shop
{
    /// <summary>
    /// 商店目录——为所有装备生成购买/出售价格，并提供随机库存
    /// </summary>
    public static class ShopCatalog
    {
        private static bool _initialized;

        /// <summary>初始化所有装备价格（幂等）</summary>
        public static void EnsurePrices()
        {
            if (_initialized) return;
            _initialized = true;

            foreach (var kv in EquipmentLibrary.GetAll())
            {
                var e = kv.Value;
                if (e.basePrice <= 0)
                    e.basePrice = CalculatePrice(e);
            }
        }

        /// <summary>获取购买价</summary>
        public static int GetBuyPrice(string equipId)
        {
            EnsurePrices();
            var e = EquipmentLibrary.Get(equipId);
            return e?.basePrice ?? 0;
        }

        /// <summary>获取出售价（购买价的一半，最低 10）</summary>
        public static int GetSellPrice(string equipId)
        {
            int buy = GetBuyPrice(equipId);
            return buy > 0 ? System.Math.Max(10, buy / 2) : 0;
        }

        /// <summary>随机生成商店库存</summary>
        /// <param name="count">商品数量</param>
        /// <param name="maxRarity">允许的最高稀有度（用于限制后期商店）</param>
        public static List<string> GenerateStock(int count = 6, EquipmentRarity maxRarity = EquipmentRarity.Epic)
        {
            EnsurePrices();
            var all = new List<EquipmentData>(EquipmentLibrary.GetAll().Values);
            all.RemoveAll(e => (int)e.rarity > (int)maxRarity || e.basePrice <= 0);

            var stock = new List<string>();
            if (all.Count == 0) return stock;

            var rng = new System.Random();
            while (stock.Count < count && all.Count > 0)
            {
                int idx = rng.Next(all.Count);
                var e = all[idx];
                if (!stock.Contains(e.id))
                    stock.Add(e.id);
                else
                    all.RemoveAt(idx);
            }
            return stock;
        }

        private static int CalculatePrice(EquipmentData e)
        {
            int rarityBase = e.rarity switch
            {
                EquipmentRarity.Common => 100,
                EquipmentRarity.Uncommon => 250,
                EquipmentRarity.Rare => 600,
                EquipmentRarity.Epic => 1200,
                _ => 100
            };

            int statValue =
                e.strBonus * 5 +
                e.cmdBonus * 5 +
                e.intBonus * 5 +
                e.agiBonus * 5 +
                e.lukBonus * 5 +
                e.hpBonus * 2 +
                e.mpBonus * 2 +
                e.moveBonus * 80 +
                e.attackRangeBonus * 80 +
                e.strPercent * 10 +
                e.cmdPercent * 10 +
                e.intPercent * 10;

            return rarityBase + statValue;
        }
    }
}
