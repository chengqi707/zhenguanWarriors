using System;
using ZhenguanWarriors.Core.Battle;
using ZhenguanWarriors.Core.Character;

namespace ZhenguanWarriors.Core.Combat
{
    /// <summary>
    /// 伤害计算器——纯数据计算，无 Unity 依赖
    /// 公式参考曹操传基础逻辑
    /// v2.0: 加入被动技能/羁绊修正
    /// </summary>
    public static class CombatCalculator
    {
        /// <summary>计算物理伤害（含被动技能修正）</summary>
        public static (int damage, bool isCrit, bool isHit) CalcPhysicalDamage(
            BattleUnit attacker, BattleUnit defender,
            int terrainDefBonus = 0, int terrainHitBonus = 0,
            TerrainType attackerTerrain = TerrainType.Plain,
            int currentTurn = 1, bool isFirstAttack = false)
        {
            // ---- 命中判定 ----
            int hitRate = attacker.Agility * 3 - defender.Agility * 2 + terrainHitBonus;
            hitRate = Math.Clamp(hitRate, 10, 95);
            bool isHit = UnityEngine.Random.Range(0, 100) < hitRate;
            if (!isHit) return (0, false, false);

            // ---- 暴击判定（含被动修正） ----
            float critRate = attacker.Luck - defender.Luck / 2f;
            critRate += attacker.GetPassiveModifier("crit_rate_pct") * 100f; // 双锏+15%
            critRate = Math.Clamp(critRate, 3, 65);
            bool isCrit = UnityEngine.Random.Range(0, 100) < (int)critRate;

            // ---- 基础伤害 ----
            float baseDamage = attacker.Strength * 1.5f - defender.Command;
            if (baseDamage < 1) baseDamage = 1;

            // 暴击加成
            if (isCrit) baseDamage *= 1.5f;

            // ★ 兵种相克加成
            float classBonus = ClassData.GetCounterMultiplier(attacker.UnitClass, defender.UnitClass);
            baseDamage *= classBonus;

            // ★ 被动：双锏反击加成（在反击时额外+20%伤害）
            if (attacker.HasPassiveType("counter_chance"))
            {
                float counterChance = attacker.GetPassiveModifier("counter_chance");
                // counter_chance 在反击逻辑中处理，这里只加反击伤害
            }

            // ★ 被动：三板斧（前N回合攻击翻倍）
            if (attacker.HasPassiveType("attack_double") && currentTurn <= 3)
            {
                float mult = attacker.GetPassiveModifier("attack_double");
                if (mult > 1f) baseDamage *= mult; // 翻倍
            }

            // ★ 被动：狂暴（HP低于30%时攻击+25%）
            if (attacker.HasPassiveType("low_hp_attack_pct")
                && attacker.CurrentHp <= attacker.MaxHp * 0.3f)
            {
                baseDamage *= (1f + attacker.GetPassiveModifier("low_hp_attack_pct"));
            }

            // ★ 被动：先锋（首次攻击伤害+30%）
            if (isFirstAttack && attacker.HasPassiveType("first_attack_pct"))
            {
                baseDamage *= (1f + attacker.GetPassiveModifier("first_attack_pct"));
            }

            // ★ 被动：神射（远程伤害+15%）
            if (attacker.UnitClass == ClassType.Archer
                && attacker.HasPassiveType("ranged_damage_pct"))
            {
                baseDamage *= (1f + attacker.GetPassiveModifier("ranged_damage_pct"));
            }

            // ★ 被动：攻城（攻城伤害+25%）
            if (attacker.HasPassiveType("siege_damage_pct")
                && attackerTerrain == TerrainType.City)
            {
                baseDamage *= (1f + attacker.GetPassiveModifier("siege_damage_pct"));
            }

            // ★ 骑兵冲锋：平原移动后首次攻击+20%
            if (attacker.UnitClass == ClassType.Cavalry
                && attacker.HasMovedThisTurn
                && attackerTerrain == TerrainType.Plain)
            {
                baseDamage *= 1.2f;
            }

            // ---- 防御方被动修正 ----

            // ★ 地形防御
            baseDamage *= (100 - terrainDefBonus) / 100f;

            // ★ 被动：铁壁（物理伤害减免+10%）
            if (defender.HasPassiveType("phys_def_pct"))
            {
                baseDamage *= (1f - defender.GetPassiveModifier("phys_def_pct"));
            }

            // ★ 被动：坚韧（每减少10%HP，防御+3%）
            if (defender.HasPassiveType("hp_def_scale"))
            {
                float hpRatio = (float)defender.CurrentHp / defender.MaxHp;
                float defBonus = (1f - hpRatio) * 10f * defender.GetPassiveModifier("hp_def_scale");
                baseDamage *= (1f - Math.Min(defBonus, 0.5f)); // 最多减50%
            }

            // ★ 重步兵铁壁：受到物理伤害-10%
            if (defender.UnitClass == ClassType.HeavyInfantry)
            {
                baseDamage *= 0.9f;
            }

            // 随机波动 ±5%
            float variance = UnityEngine.Random.Range(0.95f, 1.05f);
            int finalDamage = Math.Max(1, (int)(baseDamage * variance));

            return (finalDamage, isCrit, true);
        }

        /// <summary>计算计策伤害（含被动修正）</summary>
        public static int CalcMagicDamage(
            BattleUnit caster, BattleUnit target, int basePower)
        {
            float damage = basePower * (1 + (caster.Intelligence - target.Intelligence) / 100f);

            // ★ 被动：智计辅佐（计策伤害+20%）
            if (caster.HasPassiveType("magic_damage_pct"))
            {
                damage *= (1f + caster.GetPassiveModifier("magic_damage_pct"));
            }

            // ★ 被动：魔障（计策伤害减免+15%）
            if (target.HasPassiveType("magic_def_pct"))
            {
                damage *= (1f - target.GetPassiveModifier("magic_def_pct"));
            }

            float variance = UnityEngine.Random.Range(0.95f, 1.05f);
            return Math.Max(1, (int)(damage * variance));
        }

        /// <summary>计算计策伤害（基于int值，无被动修正，兼容旧调用）</summary>
        public static int CalcMagicDamage(
            int casterInt, int targetInt, int basePower,
            int targetMagicResist = 0)
        {
            float damage = basePower * (1 + (casterInt - targetInt) / 100f);
            damage *= (100 - targetMagicResist) / 100f;
            float variance = UnityEngine.Random.Range(0.95f, 1.05f);
            return Math.Max(1, (int)(damage * variance));
        }

        /// <summary>计算溅射伤害（器械AOE用，主伤害的50%）</summary>
        public static int CalcSplashDamage(int mainDamage) =>
            Math.Max(1, mainDamage / 2);

        /// <summary>计算经验值（含被动修正）</summary>
        public static int CalcExp(int levelDiff, bool isKill, bool isBoss,
            BattleUnit gainer = null)
        {
            int baseExp = isKill ? 30 : 10;
            if (isBoss) baseExp *= 2;
            int diffMod = Math.Clamp(5 - levelDiff, 1, 10);
            int exp = baseExp * diffMod / 5;
            // 被动：治政（经验获取+15%）
            if (gainer != null && gainer.HasPassiveType("exp_bonus_pct"))
            {
                exp = (int)(exp * (1f + gainer.GetPassiveModifier("exp_bonus_pct")));
            }
            return Math.Max(1, exp);
        }
    }
}
