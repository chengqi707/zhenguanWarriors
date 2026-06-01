using System;
using System.Collections.Generic;
using System.Linq;
using ZhenguanWarriors.Core.Character;
using ZhenguanWarriors.Core.Combat;

namespace ZhenguanWarriors.Core.Battle
{
    /// <summary>
    /// 战斗单位状态机
    /// </summary>
    public enum UnitState
    {
        Idle,       // 待机（回合未开始）
        Ready,      // 准备行动（轮到该单位）
        Moving,     // 移动中
        Attacking,  // 攻击中
        Casting,    // 施放计策
        Done,       // 行动完毕
        Dead        // 阵亡
    }

    /// <summary>
    /// 阵营
    /// </summary>
    public enum Faction
    {
        Player,     // 玩家方
        Enemy,      // 敌方
        Ally        // 友军
    }

    /// <summary>
    /// 战斗单位——纯数据，无 Unity 依赖
    /// </summary>
    public class BattleUnit
    {
        // ========== 基础属性 ==========
        public string Id { get; }
        public string Name { get; }
        public Faction Faction { get; }
        public ClassType UnitClass { get; set; }  // 兵种
        public Gender Gender { get; set; }        // 性别
        public int Level { get; set; }
        public int Experience { get; set; }       // 当前经验值

        // ========== 成长率（每级增长）==========
        public int StrGrowth { get; set; }
        public int CmdGrowth { get; set; }
        public int IntGrowth { get; set; }
        public int AgiGrowth { get; set; }
        public int LukGrowth { get; set; }

        // ========== 基础五维（不加装备）==========
        public int BaseStrength { get; set; }
        public int BaseCommand { get; set; }
        public int BaseIntelligence { get; set; }
        public int BaseAgility { get; set; }
        public int BaseLuck { get; set; }

        // ========== 战斗五维（含装备加成 + 临时Buff）==========
        public int TempStrBuff { get; set; }    // 临时武力Buff（鼓舞等）

        public int Strength => BaseStrength + GetEquipmentStrBonus() + TempStrBuff;
        public int Command => BaseCommand + GetEquipmentCmdBonus();
        public int Intelligence => BaseIntelligence + GetEquipmentIntBonus();
        public int Agility => BaseAgility + GetEquipmentAgiBonus();
        public int Luck => BaseLuck + GetEquipmentLukBonus();

        // ========== 战斗值 ==========
        public int MaxHp { get; set; }
        public int CurrentHp { get; set; }
        public int MaxMp { get; set; }
        public int CurrentMp { get; set; }

        // 基础移动力/攻击范围（不含装备）
        public int BaseMoveRange { get; set; }
        public int BaseAttackRange { get; set; }

        // 实际移动力/攻击范围（含装备 + 兵种特性）
        public int MoveRange => BaseMoveRange + GetEquipmentMoveBonus();
        public int AttackRange => BaseAttackRange + GetEquipmentAttackRangeBonus()
            + ClassData.GetClassRangeBonus(UnitClass, HasMovedThisTurn);

        // ========== 装备 ==========
        public string WeaponId { get; set; }      // 武器
        public string ArmorId { get; set; }       // 防具
        public string TrinketId { get; set; }     // 饰品

        // ========== 计策 ==========
        public List<string> SkillIds { get; set; } = new();  // 已知计策ID列表
        public bool HasSkills => SkillIds.Count > 0;

        // ========== 被动技能 ==========
        public List<string> PassiveIds { get; set; } = new();

        // ========== 位置状态 ==========
        public HexCoord Position { get; set; }
        public UnitState State { get; set; } = UnitState.Idle;
        public bool HasActed { get; set; }      // 本回合是否已行动
        public bool HasMovedThisTurn { get; set; } // 本回合是否移动过（用于冲锋判定）

        public bool IsAlive => CurrentHp > 0;
        public bool IsDead => !IsAlive;

        // ========== 构造函数 ==========
        public BattleUnit(string id, string name, Faction faction, ClassType unitClass,
            int str, int cmd, int @int, int agi, int luk,
            int hp, int mp, int move, int attackRange,
            Gender gender = Gender.Male)
        {
            Id = id;
            Name = name;
            Faction = faction;
            UnitClass = unitClass;
            Gender = gender;
            BaseStrength = str;
            BaseCommand = cmd;
            BaseIntelligence = @int;
            BaseAgility = agi;
            BaseLuck = luk;
            MaxHp = hp;
            CurrentHp = hp;
            MaxMp = mp;
            CurrentMp = mp;
            BaseMoveRange = move > 0 ? move : ClassData.GetBaseMove(unitClass);
            BaseAttackRange = attackRange > 0 ? attackRange : ClassData.GetBaseAttackRange(unitClass);
            Level = 1;
            Experience = 0;
        }

        // ========== 基础方法 ==========

        /// <summary>受伤害</summary>
        public void TakeDamage(int damage)
        {
            CurrentHp = Math.Max(0, CurrentHp - damage);
            if (CurrentHp <= 0)
                State = UnitState.Dead;
        }

        /// <summary>恢复HP</summary>
        public void Heal(int amount)
        {
            CurrentHp = Math.Min(MaxHp, CurrentHp + amount);
            if (CurrentHp > 0 && State == UnitState.Dead)
                State = UnitState.Idle; // 复活
        }

        /// <summary>消耗MP</summary>
        public bool ConsumeMp(int cost)
        {
            if (CurrentMp < cost) return false;
            CurrentMp -= cost;
            return true;
        }

        /// <summary>恢复MP</summary>
        public void RestoreMp(int amount)
        {
            CurrentMp = Math.Min(MaxMp, CurrentMp + amount);
        }

        /// <summary>重置回合状态</summary>
        public void NewTurn()
        {
            HasActed = false;
            HasMovedThisTurn = false;
            TempStrBuff = 0;
            if (State == UnitState.Done)
                State = UnitState.Ready;
        }

        /// <summary>本回合开始时的Ready状态</summary>
        public void SetReady()
        {
            if (IsAlive)
            {
                State = UnitState.Ready;
                HasActed = false;
            }
        }

        // ========== 装备属性计算 ==========

        private EquipmentData GetWeapon() => string.IsNullOrEmpty(WeaponId) ? null : EquipmentLibrary.Get(WeaponId);
        private EquipmentData GetArmor() => string.IsNullOrEmpty(ArmorId) ? null : EquipmentLibrary.Get(ArmorId);
        private EquipmentData GetTrinket() => string.IsNullOrEmpty(TrinketId) ? null : EquipmentLibrary.Get(TrinketId);

        private int GetEquipmentStrBonus() => SumEquip(e => e.strBonus) + (BaseStrength * SumEquipPercent(e => e.strPercent) / 100);
        private int GetEquipmentCmdBonus() => SumEquip(e => e.cmdBonus) + (BaseCommand * SumEquipPercent(e => e.cmdPercent) / 100);
        private int GetEquipmentIntBonus() => SumEquip(e => e.intBonus) + (BaseIntelligence * SumEquipPercent(e => e.intPercent) / 100);
        private int GetEquipmentAgiBonus() => SumEquip(e => e.agiBonus);
        private int GetEquipmentLukBonus() => SumEquip(e => e.lukBonus);

        private int GetEquipmentMoveBonus() => SumEquip(e => e.moveBonus);
        private int GetEquipmentAttackRangeBonus() => SumEquip(e => e.attackRangeBonus);

        private int GetEquipmentHpBonus() => SumEquip(e => e.hpBonus);
        private int GetEquipmentMpBonus() => SumEquip(e => e.mpBonus);

        private int SumEquip(Func<EquipmentData, int> selector)
        {
            int sum = 0;
            var w = GetWeapon(); if (w != null) sum += selector(w);
            var a = GetArmor(); if (a != null) sum += selector(a);
            var t = GetTrinket(); if (t != null) sum += selector(t);
            return sum;
        }

        private int SumEquipPercent(Func<EquipmentData, int> selector)
        {
            int sum = 0;
            var w = GetWeapon(); if (w != null) sum += selector(w);
            var a = GetArmor(); if (a != null) sum += selector(a);
            var t = GetTrinket(); if (t != null) sum += selector(t);
            return sum;
        }

        /// <summary>装备某件装备</summary>
        public bool Equip(string equipmentId)
        {
            var equip = EquipmentLibrary.Get(equipmentId);
            if (equip == null) return false;
            if (!equip.CanEquip(this)) return false;

            switch (equip.type)
            {
                case EquipmentType.Weapon: WeaponId = equipmentId; break;
                case EquipmentType.Armor: ArmorId = equipmentId; break;
                case EquipmentType.Trinket: TrinketId = equipmentId; break;
            }

            // 重新计算HP/MP上限（装备可能增加HP/MP）
            int oldMaxHp = MaxHp;
            int oldMaxMp = MaxMp;
            RecalculateStats();
            // 保持当前HP/MP比例
            CurrentHp = Math.Min(MaxHp, CurrentHp + (MaxHp - oldMaxHp));
            CurrentMp = Math.Min(MaxMp, CurrentMp + (MaxMp - oldMaxMp));
            return true;
        }

        /// <summary>卸下装备</summary>
        public void Unequip(EquipmentType type)
        {
            switch (type)
            {
                case EquipmentType.Weapon: WeaponId = null; break;
                case EquipmentType.Armor: ArmorId = null; break;
                case EquipmentType.Trinket: TrinketId = null; break;
            }
            RecalculateStats();
        }

        /// <summary>重新计算基础战斗数值</summary>
        public void RecalculateStats()
        {
            MaxHp = (int)(BaseCommand * 1.2f) + 50 + GetEquipmentHpBonus();
            MaxMp = (int)(BaseIntelligence * 0.8f) + 20 + GetEquipmentMpBonus();
            CurrentHp = Math.Min(CurrentHp, MaxHp);
            CurrentMp = Math.Min(CurrentMp, MaxMp);
        }

        // ========== 经验与升级 ==========

        /// <summary>经验曲线：升到下一级所需经验</summary>
        public int ExpToNextLevel() => 100 + (Level - 1) * 20;

        /// <summary>获得经验</summary>
        public bool GainExperience(int amount)
        {
            if (Level >= 30) return false; // 满级
            Experience += amount;
            bool leveled = false;
            while (Experience >= ExpToNextLevel() && Level < 30)
            {
                Experience -= ExpToNextLevel();
                LevelUp();
                leveled = true;
            }
            return leveled;
        }

        /// <summary>升级</summary>
        private void LevelUp()
        {
            Level++;
            BaseStrength += StrGrowth;
            BaseCommand += CmdGrowth;
            BaseIntelligence += IntGrowth;
            BaseAgility += AgiGrowth;
            BaseLuck += LukGrowth;
            RecalculateStats();
            CurrentHp = MaxHp;
            CurrentMp = MaxMp;
        }

        /// <summary>获取等级可习得的新计策</summary>
        public List<string> GetLearnableSkills()
        {
            var result = new List<string>();
            // 按角色ID和等级定义可习得的计策
            var learnTable = GetSkillLearnTable();
            foreach (var entry in learnTable)
            {
                if (entry.level == Level && !SkillIds.Contains(entry.skillId))
                    result.Add(entry.skillId);
            }
            return result;
        }

        private List<(int level, string skillId)> GetSkillLearnTable()
        {
            return Id switch
            {
                "li_jing" => new List<(int, string)> { (8, "water_attack") },
                "lishimin" => new List<(int, string)> { (15, "revive") },
                "zhangsun_wuji" => new List<(int, string)> { (5, "confuse"), (10, "water_attack") },
                "fang_xuanling" => new List<(int, string)> { (6, "insight") },
                "qin_qiong" => new List<(int, string)> { (10, "rally") },
                _ => new List<(int, string)>()
            };
        }

        public override string ToString() =>
            $"{Name}[{Faction}] Lv{Level} HP:{CurrentHp}/{MaxHp} MP:{CurrentMp}/{MaxMp} @{Position}";
    }
}
