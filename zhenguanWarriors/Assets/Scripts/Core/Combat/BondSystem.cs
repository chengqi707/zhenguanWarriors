using System.Collections.Generic;
using System.Linq;
using ZhenguanWarriors.Core.Battle;

namespace ZhenguanWarriors.Core.Combat
{
    /// <summary>
    /// 羁绊定义
    /// </summary>
    public class BondDef
    {
        public string id;
        public string name;
        public string description;
        public List<string> characterIds;       // 羁绊角色ID
        public string effectType;               // 效果类型
        public float effectValue;               // 效果数值
        public bool requiresAll;                // 是否要求全部在场

        public BondDef(string id, string name, string desc,
            List<string> charIds, string effectType, float effectValue,
            bool requiresAll = true)
        {
            this.id = id;
            this.name = name;
            this.description = desc;
            this.characterIds = charIds;
            this.effectType = effectType;
            this.effectValue = effectValue;
            this.requiresAll = requiresAll;
        }
    }

    /// <summary>
    /// 羁绊系统——检测上阵角色触发羁绊，计算加成
    /// </summary>
    public static class BondSystem
    {
        private static List<BondDef> _bonds;

        public static List<BondDef> GetAll()
        {
            if (_bonds == null) Build();
            return _bonds;
        }

        private static void Build()
        {
            _bonds = new List<BondDef>
            {
                // 帝后：李世民 + 长孙皇后
                new BondDef("bond_emperor", "👑 帝后同心",
                    "李世民与长孙皇后同时上阵，双方全属性+10%",
                    new List<string> { "lishimin", "zhangsun_empress" },
                    "all_stats_pct", 0.10f),

                // 夫妻：柴绍 + 平阳公主
                new BondDef("bond_couple", "💑 夫妻并肩",
                    "柴绍与平阳公主同时上阵，双方全属性+15%",
                    new List<string> { "chai_shao", "pingyang_princess" },
                    "all_stats_pct", 0.15f),

                // 瓦岗三杰：秦琼 + 程咬金 + 尉迟敬德
                new BondDef("bond_wagang", "⚔ 瓦岗三杰",
                    "秦琼、程咬金、尉迟敬德三杰齐出，全队攻击+12%",
                    new List<string> { "qin_qiong", "cheng_yaojin", "yuchi_jingde" },
                    "team_attack_pct", 0.12f),

                // 天策四将：李靖 + 李世勣 + 秦琼 + 尉迟敬德
                // 注：李世勣不在当前角色池，改为李靖+秦琼+尉迟敬德
                new BondDef("bond_tiance", "🗡 天策府将",
                    "李靖、秦琼、尉迟敬德同上阵，骑兵移动力+1",
                    new List<string> { "li_jing", "qin_qiong", "yuchi_jingde" },
                    "cavalry_move", 1f),

                // 贞观名相：房玄龄 + 杜如晦
                new BondDef("bond_chancellor", "📜 房谋杜断",
                    "房玄龄与杜如晦同时上阵，计策伤害+15%",
                    new List<string> { "fang_xuanling", "du_ruhui" },
                    "magic_damage_pct", 0.15f),
            };
        }

        /// <summary>
        /// 检测已上阵角色触发的羁绊
        /// </summary>
        public static List<BondDef> CheckBonds(List<BattleUnit> party)
        {
            var triggered = new List<BondDef>();
            var partyIds = new HashSet<string>(party.Select(u => u.Id));

            foreach (var bond in GetAll())
            {
                bool active = bond.requiresAll
                    ? bond.characterIds.All(id => partyIds.Contains(id))
                    : bond.characterIds.Any(id => partyIds.Contains(id));

                if (active)
                    triggered.Add(bond);
            }

            return triggered;
        }

        /// <summary>
        /// 获取羁绊加成文本（用于UI显示）
        /// </summary>
        public static string GetBondDescription(BondDef bond, List<BattleUnit> party)
        {
            var names = bond.characterIds
                .Select(id => party.FirstOrDefault(u => u.Id == id)?.Name ?? id)
                .ToList();

            string roster = string.Join(" + ", names);
            return $"{bond.name} ({roster}): {bond.description}";
        }
    }
}
