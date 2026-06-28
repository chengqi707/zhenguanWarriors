using System.Collections.Generic;
using ZhenguanWarriors.Core.Battle;
using ZhenguanWarriors.Core.Character;

namespace ZhenguanWarriors.Core.Combat
{
    /// <summary>
    /// 装备类型
    /// </summary>
    public enum EquipmentType
    {
        Weapon,     // 武器
        Armor,      // 防具
        Trinket     // 饰品
    }

    /// <summary>
    /// 装备稀有度
    /// </summary>
    public enum EquipmentRarity
    {
        Common,     // 白色——普通
        Uncommon,   // 绿色——精良
        Rare,       // 蓝色——稀有
        Epic        // 紫色——史诗
    }

    /// <summary>
    /// 装备数据模型
    /// </summary>
    public class EquipmentData
    {
        public string id;
        public string name;
        public EquipmentType type;
        public EquipmentRarity rarity;

        // 基础属性加成
        public int strBonus;        // 武力加成
        public int cmdBonus;        // 统御加成
        public int intBonus;        // 智力加成
        public int agiBonus;        // 敏捷加成
        public int lukBonus;        // 运气加成
        public int hpBonus;         // HP加成
        public int mpBonus;         // MP加成
        public int moveBonus;       // 移动力加成
        public int attackRangeBonus;// 攻击范围加成

        // 百分比加成（基于基础值的百分比，如 10 = +10%）
        public int strPercent;      // 武力百分比加成
        public int cmdPercent;      // 统御百分比加成
        public int intPercent;      // 智力百分比加成

        // 特效描述（纯文本，战斗时由代码解析）
        public string effectDesc;

        // 限制条件
        public List<ClassType> classRestriction;  // 职业限制，null = 无限制
        public bool femaleOnly;                   // 仅限女性
        public bool maleOnly;                     // 仅限男性

        // 商店价格（0 表示未定价，由 ShopCatalog 自动计算）
        public int basePrice;

        public EquipmentData(string id, string name, EquipmentType type, EquipmentRarity rarity)
        {
            this.id = id;
            this.name = name;
            this.type = type;
            this.rarity = rarity;
            this.classRestriction = null;
            this.femaleOnly = false;
            this.maleOnly = false;
        }

        /// <summary>获取稀有度颜色（UI用）</summary>
        public string GetRarityColor() => rarity switch
        {
            EquipmentRarity.Common => "#FFFFFF",
            EquipmentRarity.Uncommon => "#00FF00",
            EquipmentRarity.Rare => "#0088FF",
            EquipmentRarity.Epic => "#AA00FF",
            _ => "#FFFFFF"
        };

        /// <summary>检查某角色是否可装备</summary>
        public bool CanEquip(BattleUnit unit)
        {
            if (maleOnly && unit.Gender != Gender.Male) return false;
            if (femaleOnly && unit.Gender != Gender.Female) return false;
            if (classRestriction != null && !classRestriction.Contains(unit.UnitClass)) return false;
            return true;
        }
    }

    /// <summary>
    /// 性别枚举
    /// </summary>
    public enum Gender
    {
        Male,
        Female
    }

    /// <summary>
    /// 装备库——预定义全部30件装备
    /// </summary>
    public static class EquipmentLibrary
    {
        private static Dictionary<string, EquipmentData> _equipments;

        public static Dictionary<string, EquipmentData> GetAll()
        {
            if (_equipments == null) Build();
            return _equipments;
        }

        public static EquipmentData Get(string id)
        {
            if (_equipments == null) Build();
            return _equipments.TryGetValue(id, out var e) ? e : null;
        }

        private static void Build()
        {
            _equipments = new Dictionary<string, EquipmentData>();

            // ========== 武器（12件）==========
            _equipments["w001"] = new EquipmentData("w001", "环首刀", EquipmentType.Weapon, EquipmentRarity.Common)
            {
                strBonus = 8
            };
            _equipments["w002"] = new EquipmentData("w002", "铁枪", EquipmentType.Weapon, EquipmentRarity.Common)
            {
                strBonus = 12,
                classRestriction = new List<ClassType> { ClassType.Infantry, ClassType.HeavyInfantry }
            };
            _equipments["w003"] = new EquipmentData("w003", "马槊", EquipmentType.Weapon, EquipmentRarity.Common)
            {
                strBonus = 14,
                moveBonus = 1,
                classRestriction = new List<ClassType> { ClassType.Cavalry }
            };
            _equipments["w004"] = new EquipmentData("w004", "长弓", EquipmentType.Weapon, EquipmentRarity.Common)
            {
                strBonus = 10,
                attackRangeBonus = 1,
                classRestriction = new List<ClassType> { ClassType.Archer }
            };
            _equipments["w005"] = new EquipmentData("w005", "攻城锤", EquipmentType.Weapon, EquipmentRarity.Common)
            {
                strBonus = 15,
                classRestriction = new List<ClassType> { ClassType.Siege }
            };
            _equipments["w006"] = new EquipmentData("w006", "羽扇", EquipmentType.Weapon, EquipmentRarity.Common)
            {
                strBonus = 5,
                intPercent = 10,
                classRestriction = new List<ClassType> { ClassType.Strategist }
            };
            _equipments["w007"] = new EquipmentData("w007", "精钢剑", EquipmentType.Weapon, EquipmentRarity.Uncommon)
            {
                strBonus = 18,
                lukBonus = 5
            };
            _equipments["w008"] = new EquipmentData("w008", "破甲枪", EquipmentType.Weapon, EquipmentRarity.Uncommon)
            {
                strBonus = 22,
                effectDesc = "无视15%防御",
                classRestriction = new List<ClassType> { ClassType.Infantry, ClassType.HeavyInfantry }
            };
            _equipments["w009"] = new EquipmentData("w009", "饮血刀", EquipmentType.Weapon, EquipmentRarity.Uncommon)
            {
                strBonus = 20,
                effectDesc = "击杀恢复10%HP",
                classRestriction = new List<ClassType> { ClassType.Cavalry }
            };
            _equipments["w010"] = new EquipmentData("w010", "连弩", EquipmentType.Weapon, EquipmentRarity.Uncommon)
            {
                strBonus = 16,
                effectDesc = "可攻击2次（伤害-30%）",
                classRestriction = new List<ClassType> { ClassType.Archer }
            };
            _equipments["w011"] = new EquipmentData("w011", "秦王剑", EquipmentType.Weapon, EquipmentRarity.Epic)
            {
                strBonus = 30,
                strPercent = 15,
                effectDesc = "攻击+15%，光环范围+1",
                classRestriction = new List<ClassType> { ClassType.Cavalry },
                maleOnly = true
            };
            _equipments["w012"] = new EquipmentData("w012", "门神锏", EquipmentType.Weapon, EquipmentRarity.Epic)
            {
                strBonus = 28,
                effectDesc = "反击伤害+50%",
                classRestriction = new List<ClassType> { ClassType.Cavalry }
            };

            // ========== 防具（10件）==========
            _equipments["a001"] = new EquipmentData("a001", "皮甲", EquipmentType.Armor, EquipmentRarity.Common)
            {
                cmdBonus = 5,
                hpBonus = 10
            };
            _equipments["a002"] = new EquipmentData("a002", "铁甲", EquipmentType.Armor, EquipmentRarity.Common)
            {
                cmdBonus = 10,
                hpBonus = 20,
                moveBonus = -1,
                classRestriction = new List<ClassType> { ClassType.Infantry, ClassType.HeavyInfantry, ClassType.Cavalry }
            };
            _equipments["a003"] = new EquipmentData("a003", "轻甲", EquipmentType.Armor, EquipmentRarity.Common)
            {
                cmdBonus = 6,
                hpBonus = 15,
                classRestriction = new List<ClassType> { ClassType.Archer, ClassType.Strategist }
            };
            _equipments["a004"] = new EquipmentData("a004", "藤甲", EquipmentType.Armor, EquipmentRarity.Uncommon)
            {
                cmdBonus = 12,
                hpBonus = 25,
                effectDesc = "火攻伤害+20%（负面）"
            };
            _equipments["a005"] = new EquipmentData("a005", "明光铠", EquipmentType.Armor, EquipmentRarity.Uncommon)
            {
                cmdBonus = 18,
                hpBonus = 35,
                effectDesc = "被暴击率-10%",
                classRestriction = new List<ClassType> { ClassType.Infantry, ClassType.HeavyInfantry }
            };
            _equipments["a006"] = new EquipmentData("a006", "锁子甲", EquipmentType.Armor, EquipmentRarity.Uncommon)
            {
                cmdBonus = 15,
                hpBonus = 30,
                effectDesc = "骑兵突击伤害+10%",
                classRestriction = new List<ClassType> { ClassType.Cavalry }
            };
            _equipments["a007"] = new EquipmentData("a007", "锦袍", EquipmentType.Armor, EquipmentRarity.Uncommon)
            {
                cmdBonus = 8,
                hpBonus = 20,
                mpBonus = 20,
                classRestriction = new List<ClassType> { ClassType.Strategist }
            };
            _equipments["a008"] = new EquipmentData("a008", "乌铁甲", EquipmentType.Armor, EquipmentRarity.Rare)
            {
                cmdBonus = 22,
                hpBonus = 45,
                effectDesc = "物理伤害-10%"
            };
            _equipments["a009"] = new EquipmentData("a009", "金鳞甲", EquipmentType.Armor, EquipmentRarity.Rare)
            {
                cmdBonus = 25,
                hpBonus = 50,
                effectDesc = "全队防御+5%",
                classRestriction = new List<ClassType> { ClassType.Cavalry },
                maleOnly = true
            };
            _equipments["a010"] = new EquipmentData("a010", "铁壁盾", EquipmentType.Armor, EquipmentRarity.Rare)
            {
                cmdBonus = 20,
                hpBonus = 40,
                effectDesc = "正面伤害-20%",
                classRestriction = new List<ClassType> { ClassType.HeavyInfantry }
            };

            // ========== 饰品（8件）==========
            _equipments["t001"] = new EquipmentData("t001", "玉佩", EquipmentType.Trinket, EquipmentRarity.Common)
            {
                lukBonus = 5
            };
            _equipments["t002"] = new EquipmentData("t002", "护心镜", EquipmentType.Trinket, EquipmentRarity.Common)
            {
                lukBonus = 5,
                effectDesc = "被暴击率-5%"
            };
            _equipments["t003"] = new EquipmentData("t003", "香囊", EquipmentType.Trinket, EquipmentRarity.Uncommon)
            {
                hpBonus = 15,
                femaleOnly = true
            };
            _equipments["t004"] = new EquipmentData("t004", "兵书", EquipmentType.Trinket, EquipmentRarity.Uncommon)
            {
                intBonus = 8,
                classRestriction = new List<ClassType> { ClassType.Strategist }
            };
            _equipments["t005"] = new EquipmentData("t005", "酒壶", EquipmentType.Trinket, EquipmentRarity.Uncommon)
            {
                effectDesc = "每关开始HP+10%"
            };
            _equipments["t006"] = new EquipmentData("t006", "战鼓", EquipmentType.Trinket, EquipmentRarity.Rare)
            {
                effectDesc = "同阵友军攻击+3%"
            };
            _equipments["t007"] = new EquipmentData("t007", "令箭", EquipmentType.Trinket, EquipmentRarity.Rare)
            {
                moveBonus = 1
            };
            _equipments["t008"] = new EquipmentData("t008", "虎符", EquipmentType.Trinket, EquipmentRarity.Epic)
            {
                cmdBonus = 10,
                effectDesc = "统御+10，全队命中+5%"
            };
        }
    }
}
