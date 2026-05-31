using System;
using System.Collections.Generic;
using System.Linq;
using ZhenguanWarriors.Core.Battle;
using ZhenguanWarriors.Core.Character;

namespace ZhenguanWarriors.Core.Combat
{
    /// <summary>
    /// 计策执行器——处理所有计策的效果
    /// </summary>
    public class SkillExecutor
    {
        private readonly List<BattleUnit> _allUnits;
        private readonly HexGrid _grid;

        public SkillExecutor(List<BattleUnit> allUnits, HexGrid grid)
        {
            _allUnits = allUnits;
            _grid = grid;
        }

        /// <summary>
        /// 尝试释放计策。返回操作日志。
        /// </summary>
        public string Execute(SkillData skill, BattleUnit caster, HexCoord targetCell)
        {
            if (!caster.ConsumeMp(skill.mpCost))
                return $"{caster.Name} MP不足！需要{skill.mpCost}";

            return skill.type switch
            {
                SkillType.FireAttack => ExecuteFireAttack(caster, targetCell, skill),
                SkillType.RockSlide => ExecuteRockSlide(caster, targetCell, skill),
                SkillType.Rally => ExecuteRally(caster, skill),
                SkillType.Heal => ExecuteHeal(caster, targetCell, skill),
                SkillType.Volley => ExecuteFireAttack(caster, targetCell, skill), // 类似火攻
                SkillType.Confuse => ExecuteConfuse(caster, targetCell),
                SkillType.Insight => ExecuteInsight(caster, targetCell),
                _ => $"{skill.name} 暂未实现"
            };
        }

        /// <summary>获取目标格子上的单位</summary>
        private BattleUnit UnitAt(HexCoord cell) =>
            _allUnits.FirstOrDefault(u => u.Position == cell && u.IsAlive);

        /// <summary>获取半径内的所有敌方单位</summary>
        private List<BattleUnit> EnemiesInRange(BattleUnit caster, HexCoord center, int radius)
        {
            var cells = center.Range(radius);
            return _allUnits.Where(u =>
                u.IsAlive && u.Faction != caster.Faction &&
                cells.Any(c => c == u.Position)).ToList();
        }

        /// <summary>获取半径内的所有友方单位</summary>
        private List<BattleUnit> AlliesInRange(BattleUnit caster, HexCoord center, int radius)
        {
            var cells = center.Range(radius);
            return _allUnits.Where(u =>
                u.IsAlive && u.Faction == caster.Faction &&
                cells.Any(c => c == u.Position)).ToList();
        }

        // ========== 火攻 ==========

        private string ExecuteFireAttack(BattleUnit caster, HexCoord target, SkillData skill)
        {
            var targets = target.Range(skill.aoeRadius)
                .Where(c => _grid.InBounds(c))
                .Select(c => UnitAt(c))
                .Where(u => u != null && u.Faction != caster.Faction && u.IsAlive)
                .ToList();

            if (targets.Count == 0)
                return "范围内没有敌人";

            int totalDamage = 0;
            foreach (var t in targets)
            {
                int damage = CombatCalculator.CalcMagicDamage(
                    caster.Intelligence, t.Intelligence, skill.power);
                t.TakeDamage(damage);
                totalDamage += damage;
            }

            return $"{caster.Name} 释放【{skill.name}】对 {targets.Count} 个敌人造成 {totalDamage} 点伤害";
        }

        // ========== 落石 ==========

        private string ExecuteRockSlide(BattleUnit caster, HexCoord target, SkillData skill)
        {
            // 检查目标地形——山地/城墙附近伤害+50%
            TerrainType terrain = _grid.GetTerrain(target);
            float terrainBonus = (terrain == TerrainType.Mountain || terrain == TerrainType.City) ? 1.5f : 1.0f;

            var targets = target.Range(skill.aoeRadius)
                .Where(c => _grid.InBounds(c))
                .Select(c => UnitAt(c))
                .Where(u => u != null && u.Faction != caster.Faction && u.IsAlive)
                .ToList();

            if (targets.Count == 0)
                return "范围内没有敌人";

            int adjustedPower = (int)(skill.power * terrainBonus);
            int totalDamage = 0;
            foreach (var t in targets)
            {
                int damage = CombatCalculator.CalcMagicDamage(
                    caster.Intelligence, t.Intelligence, adjustedPower);
                t.TakeDamage(damage);
                totalDamage += damage;
            }

            string terrainText = terrainBonus > 1f ? "（地形加成！）" : "";
            return $"{caster.Name} 释放【{skill.name}】对 {targets.Count} 个敌人造成 {totalDamage} 点伤害{terrainText}";
        }

        // ========== 鼓舞 ==========

        private string ExecuteRally(BattleUnit caster, SkillData skill)
        {
            var allies = AlliesInRange(caster, caster.Position, skill.aoeRadius)
                .Where(u => u != caster)
                .ToList();

            // 鼓舞效果：本回合+10攻击
            foreach (var ally in allies)
            {
                ally.Strength += 10;
                // 用简单标记：存到 HasActed 的反面——这里用 TempBuff 概念
                // 简版：直接加攻击力，回合管理器会重置
            }

            string names = allies.Count > 0
                ? string.Join("、", allies.Select(a => a.Name))
                : "无友军";
            return $"{caster.Name} 释放【鼓舞】！{names} 攻击力提升";
        }

        // ========== 医疗 ==========

        private string ExecuteHeal(BattleUnit caster, HexCoord target, SkillData skill)
        {
            var targetUnit = UnitAt(target);
            if (targetUnit == null)
                return "目标格没有友军";

            if (targetUnit.Faction != caster.Faction)
                return "不能治疗敌人";

            int healAmount = skill.power + caster.Intelligence / 2;
            targetUnit.Heal(healAmount);

            return $"{caster.Name} 对 {targetUnit.Name} 释放【医疗】，恢复 {healAmount} HP";
        }

        // ========== 混乱 ==========

        private string ExecuteConfuse(BattleUnit caster, HexCoord target)
        {
            var targetUnit = UnitAt(target);
            if (targetUnit == null || targetUnit.Faction == caster.Faction)
                return "目标无效";

            // 让目标跳过下一回合——简单实现：标记为已行动
            targetUnit.HasActed = true;
            return $"{caster.Name} 对 {targetUnit.Name} 释放【混乱】！跳过下回合";
        }

        // ========== 洞察 ==========

        private string ExecuteInsight(BattleUnit caster, HexCoord target)
        {
            var targetUnit = UnitAt(target);
            if (targetUnit == null || targetUnit.Faction != caster.Faction)
                return "目标无效";

            // 恢复少量MP（驱散/净化的简版效果）
            targetUnit.RestoreMp(10);
            return $"{caster.Name} 对 {targetUnit.Name} 释放【洞察】，恢复10MP";
        }
    }
}
