using System;
using ZhenguanWarriors.Core.Battle;
using ZhenguanWarriors.Core.Character;

namespace ZhenguanWarriors.Core.Combat
{
    /// <summary>
    /// 伤害计算器——纯数据计算，无 Unity 依赖
    /// 公式参考曹操传基础逻辑
    /// </summary>
    public static class CombatCalculator
    {
        /// <summary>计算物理伤害</summary>
        public static (int damage, bool isCrit, bool isHit) CalcPhysicalDamage(
            BattleUnit attacker, BattleUnit defender,
            int terrainDefBonus = 0, int terrainHitBonus = 0,
            TerrainType attackerTerrain = TerrainType.Plain)
        {
            // 命中判定
            int hitRate = attacker.Agility * 3 - defender.Agility * 2 + terrainHitBonus;
            hitRate = Math.Clamp(hitRate, 10, 95); // 至少10%，最高95%
            bool isHit = UnityEngine.Random.Range(0, 100) < hitRate;

            if (!isHit)
                return (0, false, false);

            // 暴击判定
            int critRate = attacker.Luck - defender.Luck / 2;
            critRate = Math.Clamp(critRate, 3, 50);
            bool isCrit = UnityEngine.Random.Range(0, 100) < critRate;

            // 基础伤害
            float baseDamage = attacker.Strength * 1.5f - defender.Command;
            if (baseDamage < 1) baseDamage = 1;

            // 暴击加成
            if (isCrit) baseDamage *= 1.5f;

            // ★ 兵种相克加成
            float classBonus = ClassData.GetCounterMultiplier(attacker.UnitClass, defender.UnitClass);
            baseDamage *= classBonus;

            // ★ 骑兵冲锋：平原移动后首次攻击+20%
            if (attacker.UnitClass == ClassType.Cavalry
                && attacker.HasMovedThisTurn
                && attackerTerrain == TerrainType.Plain)
            {
                baseDamage *= 1.2f;
            }

            // 地形防御
            baseDamage *= (100 - terrainDefBonus) / 100f;

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

        /// <summary>计算计策伤害</summary>
        public static int CalcMagicDamage(
            int casterInt, int targetInt, int basePower,
            int targetMagicResist = 0)
        {
            float damage = basePower * (1 + (casterInt - targetInt) / 100f);
            damage *= (100 - targetMagicResist) / 100f;
            float variance = UnityEngine.Random.Range(0.95f, 1.05f);
            return Math.Max(1, (int)(damage * variance));
        }

        /// <summary>计算经验值</summary>
        public static int CalcExp(int levelDiff, bool isKill, bool isBoss)
        {
            int baseExp = isKill ? 30 : 10;
            if (isBoss) baseExp *= 2;
            int diffMod = Math.Clamp(5 - levelDiff, 1, 10);
            return baseExp * diffMod / 5;
        }
    }
}
