using System.Collections.Generic;

namespace ZhenguanWarriors.Core.Combat
{
    /// <summary>
    /// 被动技能触发时机
    /// </summary>
    public enum PassiveTrigger
    {
        Always,             // 常驻效果
        OnBattleStart,      // 战斗开始时
        OnAttack,           // 攻击时
        OnAttacked,         // 被攻击时
        OnKill,             // 击杀时
        OnTurnStart,        // 回合开始时
        OnTurnEnd,          // 回合结束时
        OnDuel,             // 单挑时
        OnLevelUp,          // 升级时
        LimitedRounds       // 前N回合有效
    }

    /// <summary>
    /// 被动技能数据模型
    /// </summary>
    public class PassiveSkillData
    {
        public string id;               // 唯一ID
        public string name;             // 技能名
        public string description;      // 效果描述
        public PassiveTrigger trigger;  // 触发时机
        public string effectType;       // 效果类型
        public float effectValue;       // 效果数值
        public int duration;            // 持续时间（LimitedRounds用）
        public string target;           // "self" / "ally" / "all"

        public PassiveSkillData(string id, string name, string desc,
            PassiveTrigger trigger, string effectType, float effectValue,
            int duration = 0, string target = "self")
        {
            this.id = id;
            this.name = name;
            this.description = desc;
            this.trigger = trigger;
            this.effectType = effectType;
            this.effectValue = effectValue;
            this.duration = duration;
            this.target = target;
        }
    }

    /// <summary>
    /// 被动技能库——预定义全部25个被动技能
    /// </summary>
    public static class PassiveSkillLibrary
    {
        private static Dictionary<string, PassiveSkillData> _skills;

        public static PassiveSkillData Get(string id)
        {
            if (_skills == null) Build();
            return _skills.TryGetValue(id, out var s) ? s : null;
        }

        public static Dictionary<string, PassiveSkillData> GetAll()
        {
            if (_skills == null) Build();
            return _skills;
        }

        private static void Build()
        {
            _skills = new Dictionary<string, PassiveSkillData>();

            // ===== 角色专属被动（15个） =====
            _skills["ps_tiance"] = new PassiveSkillData("ps_tiance", "天策上将",
                "全队攻击+8%", PassiveTrigger.Always, "team_attack_pct", 0.08f, 0, "all");

            _skills["ps_zhiji"] = new PassiveSkillData("ps_zhiji", "智计辅佐",
                "计策伤害+20%", PassiveTrigger.Always, "magic_damage_pct", 0.20f, 0, "self");

            _skills["ps_zhizheng"] = new PassiveSkillData("ps_zhizheng", "治政",
                "战后经验获取+15%", PassiveTrigger.Always, "exp_bonus_pct", 0.15f, 0, "all");

            _skills["ps_jueduan"] = new PassiveSkillData("ps_jueduan", "决断",
                "行动后20%概率再次行动", PassiveTrigger.OnTurnEnd, "extra_turn_chance", 0.20f, 0, "self");

            _skills["ps_tongshuai"] = new PassiveSkillData("ps_tongshuai", "统帅",
                "骑兵突击距离+1", PassiveTrigger.Always, "charge_range", 1f, 0, "self");

            _skills["ps_danliao"] = new PassiveSkillData("ps_danliao", "单挑达人",
                "单挑胜率+20%", PassiveTrigger.OnDuel, "duel_win_pct", 0.20f, 0, "self");

            _skills["ps_shuangjian"] = new PassiveSkillData("ps_shuangjian", "双锏",
                "暴击率+15%，反击率+20%", PassiveTrigger.Always, "crit_rate_pct", 0.15f, 0, "self");

            _skills["ps_sanbanfu"] = new PassiveSkillData("ps_sanbanfu", "三板斧",
                "前3回合攻击翻倍", PassiveTrigger.LimitedRounds, "attack_double", 2.0f, 3, "self");

            _skills["ps_xiaojiang"] = new PassiveSkillData("ps_xiaojiang", "骁将",
                "不受地形移动限制", PassiveTrigger.Always, "terrain_free", 1f, 0, "self");

            _skills["ps_youqi"] = new PassiveSkillData("ps_youqi", "游骑",
                "移动力+1，可两次行动", PassiveTrigger.Always, "move_plus", 1f, 0, "self");

            _skills["ps_shenshe"] = new PassiveSkillData("ps_shenshe", "神射",
                "远程伤害+15%", PassiveTrigger.Always, "ranged_damage_pct", 0.15f, 0, "self");

            _skills["ps_gongcheng"] = new PassiveSkillData("ps_gongcheng", "攻城",
                "攻城时伤害+25%", PassiveTrigger.Always, "siege_damage_pct", 0.25f, 0, "self");

            _skills["ps_fuzhuo"] = new PassiveSkillData("ps_fuzhuo", "辅佐",
                "与平阳公主同阵时双方属性+15%", PassiveTrigger.Always, "bond_bonus", 0.15f, 0, "self");

            _skills["ps_niangzijun"] = new PassiveSkillData("ps_niangzijun", "娘子军",
                "全女性单位攻击+20%", PassiveTrigger.Always, "female_attack_pct", 0.20f, 0, "ally");

            _skills["ps_rende"] = new PassiveSkillData("ps_rende", "仁德",
                "每回合恢复全队5%HP", PassiveTrigger.OnTurnStart, "team_heal_pct", 0.05f, 0, "all");

            // ===== 额外通用被动（10个，装备/升级习得） =====
            _skills["ps_ironwall"] = new PassiveSkillData("ps_ironwall", "铁壁",
                "物理伤害减免+10%", PassiveTrigger.Always, "phys_def_pct", 0.10f, 0, "self");

            _skills["ps_magicbarrier"] = new PassiveSkillData("ps_magicbarrier", "魔障",
                "计策伤害减免+15%", PassiveTrigger.Always, "magic_def_pct", 0.15f, 0, "self");

            _skills["ps_counter"] = new PassiveSkillData("ps_counter", "反击",
                "受到攻击时30%概率反击", PassiveTrigger.OnAttacked, "counter_chance", 0.30f, 0, "self");

            _skills["ps_berserk"] = new PassiveSkillData("ps_berserk", "狂暴",
                "HP低于30%时攻击+25%", PassiveTrigger.Always, "low_hp_attack_pct", 0.25f, 0, "self");

            _skills["ps_regenerate"] = new PassiveSkillData("ps_regenerate", "再生",
                "每回合恢复15%HP", PassiveTrigger.OnTurnStart, "self_heal_pct", 0.15f, 0, "self");

            _skills["ps_vanguard"] = new PassiveSkillData("ps_vanguard", "先锋",
                "首次攻击伤害+30%", PassiveTrigger.OnAttack, "first_attack_pct", 0.30f, 0, "self");

            _skills["ps_guardian"] = new PassiveSkillData("ps_guardian", "护卫",
                "相邻友军受到伤害-15%", PassiveTrigger.Always, "adjacent_ally_def", 0.15f, 0, "ally");

            _skills["ps_assassin"] = new PassiveSkillData("ps_assassin", "奇袭",
                "从背后攻击时伤害+20%", PassiveTrigger.OnAttack, "backstab_pct", 0.20f, 0, "self");

            _skills["ps_inspire"] = new PassiveSkillData("ps_inspire", "鼓舞",
                "击杀时全队攻击+10%（本关有效）", PassiveTrigger.OnKill, "kill_team_boost", 0.10f, 0, "all");

            _skills["ps_fortitude"] = new PassiveSkillData("ps_fortitude", "坚韧",
                "每减少10%HP，防御+3%", PassiveTrigger.Always, "hp_def_scale", 0.03f, 0, "self");
        }

        /// <summary>获取角色的专属被动技能ID列表</summary>
        public static List<string> GetCharacterPassives(string charId)
        {
            return charId switch
            {
                "lishimin" => new List<string> { "ps_tiance" },
                "zhangsun_wuji" => new List<string> { "ps_zhiji" },
                "fang_xuanling" => new List<string> { "ps_zhizheng" },
                "du_ruhui" => new List<string> { "ps_jueduan" },
                "li_jing" => new List<string> { "ps_tongshuai" },
                "yuchi_jingde" => new List<string> { "ps_danliao", "ps_ironwall" },
                "qin_qiong" => new List<string> { "ps_shuangjian", "ps_counter" },
                "cheng_yaojin" => new List<string> { "ps_sanbanfu", "ps_berserk" },
                "hou_junji" => new List<string> { "ps_xiaojiang" },
                "duan_zhixuan" => new List<string> { "ps_youqi", "ps_vanguard" },
                "liu_hongji" => new List<string> { "ps_shenshe", "ps_assassin" },
                "yin_kaishan" => new List<string> { "ps_gongcheng", "ps_fortitude" },
                "chai_shao" => new List<string> { "ps_fuzhuo", "ps_guardian" },
                "pingyang_princess" => new List<string> { "ps_niangzijun" },
                "zhangsun_empress" => new List<string> { "ps_rende", "ps_regenerate" },
                _ => new List<string>()
            };
        }
    }
}
