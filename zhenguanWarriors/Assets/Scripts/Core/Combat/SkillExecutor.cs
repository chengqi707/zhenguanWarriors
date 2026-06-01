using System;
using System.Collections.Generic;
using System.Linq;
using ZhenguanWarriors.Core.Battle;
using ZhenguanWarriors.Core.Character;

namespace ZhenguanWarriors.Core.Combat
{
    /// <summary>
    /// 计策执行器——处理所有计策的效果（含环境联动）
    /// </summary>
    public class SkillExecutor
    {
        private readonly List<BattleUnit> _allUnits;
        private readonly HexGrid _grid;
        private readonly WeatherSystem _weather;

        public SkillExecutor(List<BattleUnit> allUnits, HexGrid grid, WeatherSystem weather = null)
        {
            _allUnits = allUnits;
            _grid = grid;
            _weather = weather;
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
                SkillType.WaterAttack => ExecuteWaterAttack(caster, targetCell, skill),
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
            // 天气影响：雨天完全无效
            if (_weather != null && _weather.CurrentWeather == WeatherType.Rain)
            {
                return $"{caster.Name} 释放【{skill.name}】——但大雨倾盆，火焰无法燃起！";
            }

            // 收集所有受影响的格子
            var affectedCells = new HashSet<HexCoord>();
            affectedCells.UnionWith(target.Range(skill.aoeRadius).Where(c => _grid.InBounds(c)));

            // ★ 大风扩散：沿风向多扩散1格
            if (_weather != null && _weather.CurrentWeather == WeatherType.Windy && _weather.Wind != WindDirection.None)
            {
                var windOffset = _weather.GetWindOffset();
                var windSpread = target + windOffset;
                if (_grid.InBounds(windSpread))
                    affectedCells.Add(windSpread);
            }

            // ★ 林地连烧：相邻林地格被点燃，最多扩散3格
            var forestCells = affectedCells.Where(c => _grid.GetTerrain(c) == TerrainType.Forest).ToList();
            var burnQueue = new Queue<(HexCoord cell, int depth)>();
            foreach (var fc in forestCells)
                burnQueue.Enqueue((fc, 0));

            while (burnQueue.Count > 0)
            {
                var (current, depth) = burnQueue.Dequeue();
                if (depth >= 3) continue; // 最多扩散3格

                foreach (var neighbor in current.Neighbors())
                {
                    if (_grid.InBounds(neighbor)
                        && _grid.GetTerrain(neighbor) == TerrainType.Forest
                        && !affectedCells.Contains(neighbor))
                    {
                        affectedCells.Add(neighbor);
                        burnQueue.Enqueue((neighbor, depth + 1));
                    }
                }
            }

            // 收集目标单位
            var targets = affectedCells
                .Select(c => UnitAt(c))
                .Where(u => u != null && u.Faction != caster.Faction && u.IsAlive)
                .ToList();

            if (targets.Count == 0)
                return "范围内没有敌人";

            // 天气伤害倍率
            float weatherMult = _weather?.FireDamageMultiplier ?? 1.0f;
            int adjustedPower = (int)(skill.power * weatherMult);

            int totalDamage = 0;
            foreach (var t in targets)
            {
                int damage = CombatCalculator.CalcMagicDamage(
                    caster.Intelligence, t.Intelligence, adjustedPower);
                t.TakeDamage(damage);
                totalDamage += damage;
            }

            string weatherText = _weather?.CurrentWeather switch
            {
                WeatherType.Snow => "（大雪削弱了火势）",
                WeatherType.Windy => "（大风助长了火势！）",
                _ => ""
            };

            return $"{caster.Name} 释放【{skill.name}】对 {targets.Count} 个敌人造成 {totalDamage} 点伤害{weatherText}";
        }

        // ========== 水攻 ==========

        private string ExecuteWaterAttack(BattleUnit caster, HexCoord target, SkillData skill)
        {
            var affectedCells = target.Range(skill.aoeRadius)
                .Where(c => _grid.InBounds(c))
                .ToList();

            // 检查是否邻近水域——若目标格或周围有水，威力+30%
            bool nearWater = affectedCells.Any(c =>
                c.Range(1).Any(n => _grid.InBounds(n) && _grid.GetTerrain(n) == TerrainType.Water));
            float powerMult = nearWater ? 1.3f : 1.0f;

            var targets = affectedCells
                .Select(c => UnitAt(c))
                .Where(u => u != null && u.Faction != caster.Faction && u.IsAlive)
                .ToList();

            int adjustedPower = (int)(skill.power * powerMult);
            int totalDamage = 0;
            foreach (var t in targets)
            {
                int damage = CombatCalculator.CalcMagicDamage(
                    caster.Intelligence, t.Intelligence, adjustedPower);
                t.TakeDamage(damage);
                totalDamage += damage;
            }

            // ★ 改变地形：低地（平原/森林）变为水域
            int flooded = 0;
            foreach (var c in affectedCells)
            {
                var terrain = _grid.GetTerrain(c);
                if (terrain == TerrainType.Plain || terrain == TerrainType.Forest)
                {
                    _grid.SetTerrain(c, TerrainType.Water);
                    flooded++;
                }
            }

            string waterText = nearWater ? "（决堤！威力大增）" : "";
            string floodText = flooded > 0 ? $"，{flooded}格低地变为水域" : "";
            return $"{caster.Name} 释放【{skill.name}】对 {targets.Count} 个敌人造成 {totalDamage} 点伤害{waterText}{floodText}";
        }

        // ========== 落石 ==========

        private string ExecuteRockSlide(BattleUnit caster, HexCoord target, SkillData skill)
        {
            // 检查目标地形——山地/城墙附近伤害+50%
            TerrainType terrain = _grid.GetTerrain(target);
            float terrainBonus = (terrain == TerrainType.Mountain || terrain == TerrainType.City) ? 1.5f : 1.0f;

            var affectedCells = new HashSet<HexCoord>();
            affectedCells.UnionWith(target.Range(skill.aoeRadius).Where(c => _grid.InBounds(c)));

            // ★ 连环落石：若目标附近2格内有山地，额外触发连带伤害
            var nearbyMountains = affectedCells
                .SelectMany(c => c.Range(2))
                .Where(c => _grid.InBounds(c) && _grid.GetTerrain(c) == TerrainType.Mountain)
                .ToList();

            if (nearbyMountains.Count > 0)
            {
                foreach (var m in nearbyMountains)
                {
                    affectedCells.UnionWith(m.Range(1).Where(c => _grid.InBounds(c)));
                }
                terrainBonus = Math.Max(terrainBonus, 1.3f); // 连环落石最低也有30%加成
            }

            var targets = affectedCells
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
            string chainText = nearbyMountains.Count > 0 ? "【连环落石！】" : "";
            return $"{caster.Name} 释放【{skill.name}】{chainText}对 {targets.Count} 个敌人造成 {totalDamage} 点伤害{terrainText}";
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
                ally.TempStrBuff += 10;
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
            // ★ 谋士医疗强化：治疗量+30%
            if (caster.UnitClass == ClassType.Strategist)
            {
                healAmount = (int)(healAmount * 1.3f);
            }
            targetUnit.Heal(healAmount);

            string bonusText = caster.UnitClass == ClassType.Strategist ? "（谋士精通医道+30%）" : "";
            return $"{caster.Name} 对 {targetUnit.Name} 释放【医疗】，恢复 {healAmount} HP{bonusText}";
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
