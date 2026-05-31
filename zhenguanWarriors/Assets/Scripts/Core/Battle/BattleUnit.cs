using System;

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
        public int Level { get; set; }

        // ========== 战斗五维 ==========
        public int Strength { get; set; }       // 武力——物理攻击
        public int Command { get; set; }        // 统御——物理防御
        public int Intelligence { get; set; }   // 智力——计策伤害/抗性
        public int Agility { get; set; }        // 敏捷——命中/回避
        public int Luck { get; set; }           // 运气——暴击/抗暴

        // ========== 战斗值 ==========
        public int MaxHp { get; set; }
        public int CurrentHp { get; set; }
        public int MaxMp { get; set; }
        public int CurrentMp { get; set; }
        public int MoveRange { get; set; }      // 移动力
        public int AttackRange { get; set; }    // 攻击范围

        // ========== 位置状态 ==========
        public HexCoord Position { get; set; }
        public UnitState State { get; set; } = UnitState.Idle;
        public bool HasActed { get; set; }      // 本回合是否已行动

        public bool IsAlive => CurrentHp > 0;
        public bool IsDead => !IsAlive;

        // ========== 构造函数 ==========
        public BattleUnit(string id, string name, Faction faction,
            int str, int cmd, int @int, int agi, int luk,
            int hp, int mp, int move, int attackRange)
        {
            Id = id;
            Name = name;
            Faction = faction;
            Strength = str;
            Command = cmd;
            Intelligence = @int;
            Agility = agi;
            Luck = luk;
            MaxHp = hp;
            CurrentHp = hp;
            MaxMp = mp;
            CurrentMp = mp;
            MoveRange = move;
            AttackRange = attackRange;
            Level = 1;
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

        public override string ToString() =>
            $"{Name}[{Faction}] HP:{CurrentHp}/{MaxHp} MP:{CurrentMp}/{MaxMp} @{Position}";
    }
}
