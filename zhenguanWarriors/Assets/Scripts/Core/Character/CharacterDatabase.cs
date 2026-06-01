using System.Collections.Generic;
using ZhenguanWarriors.Core.Battle;
using ZhenguanWarriors.Core.Combat;

namespace ZhenguanWarriors.Core.Character
{
    /// <summary>
    /// 角色数据库——预定义全部15名可上阵角色
    /// </summary>
    public static class CharacterDatabase
    {
        private static Dictionary<string, BattleUnit> _characters;

        public static Dictionary<string, BattleUnit> GetAll()
        {
            if (_characters == null) Build();
            return _characters;
        }

        public static BattleUnit Get(string id)
        {
            if (_characters == null) Build();
            return _characters.TryGetValue(id, out var c) ? c : null;
        }

        /// <summary>创建角色副本（用于出战）</summary>
        public static BattleUnit CreateInstance(string id)
        {
            var template = Get(id);
            if (template == null) return null;
            return new BattleUnit(
                template.Id, template.Name, template.Faction, template.UnitClass,
                template.BaseStrength, template.BaseCommand, template.BaseIntelligence,
                template.BaseAgility, template.BaseLuck,
                template.MaxHp, template.MaxMp, template.BaseMoveRange, template.BaseAttackRange,
                template.Gender)
            {
                Level = template.Level,
                Experience = template.Experience,
                StrGrowth = template.StrGrowth,
                CmdGrowth = template.CmdGrowth,
                IntGrowth = template.IntGrowth,
                AgiGrowth = template.AgiGrowth,
                LukGrowth = template.LukGrowth,
                SkillIds = new List<string>(template.SkillIds),
                WeaponId = template.WeaponId,
                ArmorId = template.ArmorId,
                TrinketId = template.TrinketId
            };
        }

        private static void Build()
        {
            _characters = new Dictionary<string, BattleUnit>();

            // 1. 李世民（君主）
            _characters["lishimin"] = CreateChar("lishimin", "李世民", Faction.Player, ClassType.Cavalry,
                str: 82, cmd: 95, @int: 88, agi: 78, luk: 90,
                hp: 120, mp: 50, move: 5, range: 1,
                Gender.Male, skills: new[] { "rally", "insight" });
            _characters["lishimin"].StrGrowth = 4; _characters["lishimin"].CmdGrowth = 5;
            _characters["lishimin"].IntGrowth = 4; _characters["lishimin"].AgiGrowth = 3;
            _characters["lishimin"].LukGrowth = 4;

            // 2. 长孙无忌（谋士）
            _characters["zhangsun_wuji"] = CreateChar("zhangsun_wuji", "长孙无忌", Faction.Player, ClassType.Strategist,
                str: 30, cmd: 60, @int: 92, agi: 65, luk: 85,
                hp: 80, mp: 65, move: 5, range: 2,
                Gender.Male, skills: new[] { "insight", "fire_attack", "confuse" });
            _characters["zhangsun_wuji"].StrGrowth = 1; _characters["zhangsun_wuji"].CmdGrowth = 3;
            _characters["zhangsun_wuji"].IntGrowth = 5; _characters["zhangsun_wuji"].AgiGrowth = 3;
            _characters["zhangsun_wuji"].LukGrowth = 4;

            // 3. 房玄龄（谋士）
            _characters["fang_xuanling"] = CreateChar("fang_xuanling", "房玄龄", Faction.Player, ClassType.Strategist,
                str: 25, cmd: 70, @int: 92, agi: 60, luk: 80,
                hp: 75, mp: 60, move: 5, range: 2,
                Gender.Male, skills: new[] { "rally", "heal" });
            _characters["fang_xuanling"].StrGrowth = 1; _characters["fang_xuanling"].CmdGrowth = 3;
            _characters["fang_xuanling"].IntGrowth = 5; _characters["fang_xuanling"].AgiGrowth = 2;
            _characters["fang_xuanling"].LukGrowth = 3;

            // 4. 杜如晦（谋士）
            _characters["du_ruhui"] = CreateChar("du_ruhui", "杜如晦", Faction.Player, ClassType.Strategist,
                str: 28, cmd: 68, @int: 90, agi: 62, luk: 78,
                hp: 78, mp: 60, move: 5, range: 2,
                Gender.Male, skills: new[] { "confuse", "rock_slide", "insight" });
            _characters["du_ruhui"].StrGrowth = 1; _characters["du_ruhui"].CmdGrowth = 2;
            _characters["du_ruhui"].IntGrowth = 5; _characters["du_ruhui"].AgiGrowth = 3;
            _characters["du_ruhui"].LukGrowth = 3;

            // 5. 李靖（武将）
            _characters["li_jing"] = CreateChar("li_jing", "李靖", Faction.Player, ClassType.Cavalry,
                str: 90, cmd: 85, @int: 75, agi: 70, luk: 75,
                hp: 105, mp: 35, move: 6, range: 1,
                Gender.Male, skills: new[] { "fire_attack" });
            _characters["li_jing"].StrGrowth = 5; _characters["li_jing"].CmdGrowth = 4;
            _characters["li_jing"].IntGrowth = 3; _characters["li_jing"].AgiGrowth = 3;
            _characters["li_jing"].LukGrowth = 3;

            // 6. 尉迟敬德（武将）
            _characters["yuchi_jingde"] = CreateChar("yuchi_jingde", "尉迟敬德", Faction.Player, ClassType.HeavyInfantry,
                str: 92, cmd: 88, @int: 45, agi: 65, luk: 60,
                hp: 130, mp: 20, move: 4, range: 1,
                Gender.Male);
            _characters["yuchi_jingde"].StrGrowth = 5; _characters["yuchi_jingde"].CmdGrowth = 5;
            _characters["yuchi_jingde"].IntGrowth = 1; _characters["yuchi_jingde"].AgiGrowth = 2;
            _characters["yuchi_jingde"].LukGrowth = 2;

            // 7. 秦琼（武将）
            _characters["qin_qiong"] = CreateChar("qin_qiong", "秦琼", Faction.Player, ClassType.Cavalry,
                str: 88, cmd: 80, @int: 55, agi: 72, luk: 70,
                hp: 100, mp: 25, move: 5, range: 1,
                Gender.Male, skills: new[] { "rally" });
            _characters["qin_qiong"].StrGrowth = 4; _characters["qin_qiong"].CmdGrowth = 4;
            _characters["qin_qiong"].IntGrowth = 2; _characters["qin_qiong"].AgiGrowth = 3;
            _characters["qin_qiong"].LukGrowth = 3;

            // 8. 程咬金（武将）
            _characters["cheng_yaojin"] = CreateChar("cheng_yaojin", "程咬金", Faction.Player, ClassType.HeavyInfantry,
                str: 85, cmd: 75, @int: 40, agi: 60, luk: 85,
                hp: 115, mp: 15, move: 4, range: 1,
                Gender.Male);
            _characters["cheng_yaojin"].StrGrowth = 4; _characters["cheng_yaojin"].CmdGrowth = 3;
            _characters["cheng_yaojin"].IntGrowth = 1; _characters["cheng_yaojin"].AgiGrowth = 2;
            _characters["cheng_yaojin"].LukGrowth = 5;

            // 9. 侯君集（武将）
            _characters["hou_junji"] = CreateChar("hou_junji", "侯君集", Faction.Player, ClassType.Infantry,
                str: 78, cmd: 72, @int: 65, agi: 68, luk: 60,
                hp: 100, mp: 20, move: 5, range: 1,
                Gender.Male);
            _characters["hou_junji"].StrGrowth = 3; _characters["hou_junji"].CmdGrowth = 3;
            _characters["hou_junji"].IntGrowth = 3; _characters["hou_junji"].AgiGrowth = 3;
            _characters["hou_junji"].LukGrowth = 2;

            // 10. 段志玄（武将）
            _characters["duan_zhixuan"] = CreateChar("duan_zhixuan", "段志玄", Faction.Player, ClassType.Cavalry,
                str: 75, cmd: 70, @int: 60, agi: 85, luk: 65,
                hp: 95, mp: 20, move: 6, range: 1,
                Gender.Male);
            _characters["duan_zhixuan"].StrGrowth = 3; _characters["duan_zhixuan"].CmdGrowth = 3;
            _characters["duan_zhixuan"].IntGrowth = 2; _characters["duan_zhixuan"].AgiGrowth = 5;
            _characters["duan_zhixuan"].LukGrowth = 2;

            // 11. 刘弘基（武将）
            _characters["liu_hongji"] = CreateChar("liu_hongji", "刘弘基", Faction.Player, ClassType.Archer,
                str: 80, cmd: 68, @int: 55, agi: 70, luk: 55,
                hp: 90, mp: 25, move: 4, range: 3,
                Gender.Male, skills: new[] { "volley" });
            _characters["liu_hongji"].StrGrowth = 4; _characters["liu_hongji"].CmdGrowth = 3;
            _characters["liu_hongji"].IntGrowth = 2; _characters["liu_hongji"].AgiGrowth = 3;
            _characters["liu_hongji"].LukGrowth = 2;

            // 12. 殷开山（武将）
            _characters["yin_kaishan"] = CreateChar("yin_kaishan", "殷开山", Faction.Player, ClassType.Siege,
                str: 70, cmd: 75, @int: 60, agi: 55, luk: 50,
                hp: 95, mp: 20, move: 3, range: 2,
                Gender.Male, skills: new[] { "rock_slide" });
            _characters["yin_kaishan"].StrGrowth = 3; _characters["yin_kaishan"].CmdGrowth = 4;
            _characters["yin_kaishan"].IntGrowth = 3; _characters["yin_kaishan"].AgiGrowth = 1;
            _characters["yin_kaishan"].LukGrowth = 2;

            // 13. 柴绍（武将）
            _characters["chai_shao"] = CreateChar("chai_shao", "柴绍", Faction.Player, ClassType.Cavalry,
                str: 72, cmd: 70, @int: 68, agi: 72, luk: 70,
                hp: 98, mp: 25, move: 5, range: 1,
                Gender.Male, skills: new[] { "rally" });
            _characters["chai_shao"].StrGrowth = 3; _characters["chai_shao"].CmdGrowth = 3;
            _characters["chai_shao"].IntGrowth = 3; _characters["chai_shao"].AgiGrowth = 3;
            _characters["chai_shao"].LukGrowth = 3;

            // 14. 平阳公主（女性）
            _characters["pingyang_princess"] = CreateChar("pingyang_princess", "平阳公主", Faction.Player, ClassType.Cavalry,
                str: 80, cmd: 82, @int: 75, agi: 80, luk: 78,
                hp: 100, mp: 30, move: 5, range: 1,
                Gender.Female, skills: new[] { "rally" });
            _characters["pingyang_princess"].StrGrowth = 4; _characters["pingyang_princess"].CmdGrowth = 4;
            _characters["pingyang_princess"].IntGrowth = 3; _characters["pingyang_princess"].AgiGrowth = 4;
            _characters["pingyang_princess"].LukGrowth = 3;

            // 15. 长孙皇后（女性）
            _characters["zhangsun_empress"] = CreateChar("zhangsun_empress", "长孙皇后", Faction.Player, ClassType.Strategist,
                str: 20, cmd: 55, @int: 88, agi: 60, luk: 90,
                hp: 70, mp: 70, move: 5, range: 2,
                Gender.Female, skills: new[] { "heal", "revive", "rally" });
            _characters["zhangsun_empress"].StrGrowth = 1; _characters["zhangsun_empress"].CmdGrowth = 2;
            _characters["zhangsun_empress"].IntGrowth = 4; _characters["zhangsun_empress"].AgiGrowth = 2;
            _characters["zhangsun_empress"].LukGrowth = 5;
        }

        private static BattleUnit CreateChar(string id, string name, Faction faction, ClassType cls,
            int str, int cmd, int @int, int agi, int luk,
            int hp, int mp, int move, int range,
            Gender gender, string[] skills = null)
        {
            var unit = new BattleUnit(id, name, faction, cls, str, cmd, @int, agi, luk, hp, mp, move, range, gender);
            if (skills != null)
                unit.SkillIds = new List<string>(skills);
            return unit;
        }
    }
}
