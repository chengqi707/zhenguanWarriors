using System.Collections.Generic;

namespace ZhenguanWarriors.Core.Combat
{
    /// <summary>
    /// 计策类型
    /// </summary>
    public enum SkillType
    {
        FireAttack,     // 火攻——范围火伤
        RockSlide,      // 落石——地形依赖高伤
        Rally,          // 鼓舞——群体加攻
        Heal,           // 医疗——单体治疗
        Volley,         // 乱射——多目标弓技
        Confuse,        // 混乱——跳过回合
        Insight,        // 洞察——驱散/加抗
        Revive          // 复活——复活阵亡（皇后限定）
    }

    /// <summary>
    /// 计策目标类型
    /// </summary>
    public enum SkillTargetType
    {
        Enemy,      // 对敌
        Ally,       // 对友
        Self,       // 对己
        Cell        // 对地面格
    }

    /// <summary>
    /// 计策数据模型
    /// </summary>
    public class SkillData
    {
        public string id;
        public string name;
        public SkillType type;
        public SkillTargetType targetType;
        public int mpCost;
        public int range;           // 施法距离
        public int power;           // 基础威力
        public bool isAOE;
        public int aoeRadius;       // 0 = 单体
        public string description;

        public SkillData(string id, string name, SkillType type, SkillTargetType targetType,
            int mpCost, int range, int power, bool isAOE = false, int aoeRadius = 0,
            string desc = "")
        {
            this.id = id;
            this.name = name;
            this.type = type;
            this.targetType = targetType;
            this.mpCost = mpCost;
            this.range = range;
            this.power = power;
            this.isAOE = isAOE;
            this.aoeRadius = aoeRadius;
            this.description = desc;
        }
    }

    /// <summary>
    /// 预定义计策库
    /// </summary>
    public static class SkillLibrary
    {
        private static Dictionary<string, SkillData> _skills;

        public static Dictionary<string, SkillData> GetAll()
        {
            if (_skills == null) Build();
            return _skills;
        }

        public static SkillData Get(string id)
        {
            if (_skills == null) Build();
            return _skills.TryGetValue(id, out var s) ? s : null;
        }

        private static void Build()
        {
            _skills = new Dictionary<string, SkillData>
            {
                ["fire_attack"] = new SkillData(
                    "fire_attack", "火攻", SkillType.FireAttack, SkillTargetType.Enemy,
                    mpCost: 20, range: 3, power: 45, isAOE: true, aoeRadius: 1,
                    desc: "对目标及其周围1格造成火系伤害"),

                ["rock_slide"] = new SkillData(
                    "rock_slide", "落石", SkillType.RockSlide, SkillTargetType.Enemy,
                    mpCost: 25, range: 3, power: 70, isAOE: true, aoeRadius: 0,
                    desc: "对目标造成大量伤害，山地地形伤害+50%"),

                ["rally"] = new SkillData(
                    "rally", "鼓舞", SkillType.Rally, SkillTargetType.Ally,
                    mpCost: 15, range: 2, power: 0, isAOE: true, aoeRadius: 2,
                    desc: "提升范围内友军攻击力，持续2回合"),

                ["heal"] = new SkillData(
                    "heal", "医疗", SkillType.Heal, SkillTargetType.Ally,
                    mpCost: 12, range: 3, power: 50,
                    desc: "恢复目标HP"),

                ["volley"] = new SkillData(
                    "volley", "乱射", SkillType.Volley, SkillTargetType.Enemy,
                    mpCost: 18, range: 3, power: 30, isAOE: true, aoeRadius: 1,
                    desc: "对目标及其周围1格造成弓系伤害"),

                ["confuse"] = new SkillData(
                    "confuse", "混乱", SkillType.Confuse, SkillTargetType.Enemy,
                    mpCost: 22, range: 3, power: 0,
                    desc: "使目标跳过下一回合"),

                ["insight"] = new SkillData(
                    "insight", "洞察", SkillType.Insight, SkillTargetType.Ally,
                    mpCost: 10, range: 3, power: 0,
                    desc: "驱散目标身上的负面状态"),

                ["revive"] = new SkillData(
                    "revive", "回春", SkillType.Revive, SkillTargetType.Ally,
                    40, 2, 30, false, 0, "复活目标并恢复30%HP（每关限1次）"),
            };
        }
    }
}
