using UnityEngine;
using ZhenguanWarriors.Core.Battle;

namespace ZhenguanWarriors.Core.Character
{
    /// <summary>
    /// 转职/进阶系统：同兵种内多次晋升，提升基础属性
    /// </summary>
    public static class ClassChangeLibrary
    {
        public const int MAX_PROMOTION = 2;

        /// <summary>获取该单位当前阶段所需的等级</summary>
        public static int GetRequiredLevel(BattleUnit unit)
        {
            return unit.PromotionCount switch
            {
                0 => 10,
                1 => 20,
                _ => int.MaxValue
            };
        }

        /// <summary>是否满足晋升条件</summary>
        public static bool CanPromote(BattleUnit unit)
        {
            if (unit == null) return false;
            if (unit.PromotionCount >= MAX_PROMOTION) return false;
            return unit.Level >= GetRequiredLevel(unit);
        }

        /// <summary>执行晋升，返回是否成功</summary>
        public static bool Promote(BattleUnit unit)
        {
            if (!CanPromote(unit)) return false;

            unit.PromotionCount++;

            // 每次晋升全基础属性 +3，HP +10，MP +5
            unit.BaseStrength += 3;
            unit.BaseCommand += 3;
            unit.BaseIntelligence += 3;
            unit.BaseAgility += 3;
            unit.BaseLuck += 3;
            unit.MaxHp += 10;
            unit.CurrentHp += 10;
            unit.MaxMp += 5;
            unit.CurrentMp += 5;

            return true;
        }

        /// <summary>获取晋升后的显示称号</summary>
        public static string GetPromotionTitle(BattleUnit unit)
        {
            return unit.PromotionCount switch
            {
                1 => "★",
                2 => "★★",
                _ => ""
            };
        }
    }
}
