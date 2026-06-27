using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ZhenguanWarriors.Core.Battle;
using ZhenguanWarriors.Core.Character;
using ZhenguanWarriors.Core.Combat;
using ZhenguanWarriors.Utils;

namespace ZhenguanWarriors.Core.AI
{
    /// <summary>
    /// AI行动类型
    /// </summary>
    public enum AIActionType
    {
        Move,
        Attack,
        UseSkill,
        Retreat,
        Skip
    }

    /// <summary>
    /// AI行动决策
    /// </summary>
    public class AIAction
    {
        public AIActionType Type;
        public HexCoord TargetCell;     // 移动目标 / 计策目标
        public BattleUnit TargetUnit;   // 攻击目标
        public SkillData Skill;         // 使用的计策
        public string Reason;           // 决策原因（调试用）
    }

    /// <summary>
    /// AI行为树——敌方单位每回合的决策逻辑
    /// v2: 增加路径诊断日志，帮助定位重叠问题
    /// </summary>
    public class AIBehaviorTree
    {
        private readonly List<BattleUnit> _allUnits;
        private readonly HexGrid _grid;
        private readonly WeatherSystem _weather;
        private readonly SkillExecutor _skillExecutor;
        private readonly string _difficulty; // "easy" / "normal" / "hard"

        public AIBehaviorTree(List<BattleUnit> allUnits, HexGrid grid,
            WeatherSystem weather, SkillExecutor skillExecutor, string difficulty = "normal")
        {
            _allUnits = allUnits;
            _grid = grid;
            _weather = weather;
            _skillExecutor = skillExecutor;
            _difficulty = difficulty;
        }

        /// <summary>为指定敌方单位做决策</summary>
        public AIAction Decide(BattleUnit unit)
        {
            var action = DecideInternal(unit);

            string targetDesc = action.Type == AIActionType.Attack && action.TargetUnit != null
                ? $"{action.TargetUnit.Name}@({action.TargetUnit.Position.q},{action.TargetUnit.Position.r})"
                : $"({action.TargetCell.q},{action.TargetCell.r})";

            GameLogger.LogInfoFormat(LogCategory.AI,
                "AI决策|单位={0}|阵营={1}|行动={2}|目标={3}|原因={4}",
                unit.Name, unit.Faction, action.Type, targetDesc, action.Reason);

            return action;
        }

        private AIAction DecideInternal(BattleUnit unit)
        {
            if (!unit.IsAlive)
                return new AIAction { Type = AIActionType.Skip, Reason = "已阵亡" };

            // === 1. 撤退判断 ===
            var retreat = CheckRetreat(unit);
            if (retreat != null)
                return retreat;

            // 极简难度：只用集火+移动，不用计策不撤退
            if (_difficulty == "easy")
                return DecideAttackOrMove(unit);

            // === 2. 高价值计策 ===
            var skillAction = CheckSkillUsage(unit);
            if (skillAction != null)
                return skillAction;

            // === 3. 集火攻击 ===
            var attack = CheckAttack(unit);
            if (attack != null)
                return attack;

            // === 4. 移动逼近 ===
            var move = CheckMoveTowards(unit);
            if (move != null)
                return move;

            return new AIAction { Type = AIActionType.Skip, Reason = "无行动" };
        }

        // ========== 1. 撤退判断 ==========
        private AIAction CheckRetreat(BattleUnit unit)
        {
            // 简单/极简难度不撤退
            if (_difficulty == "easy") return null;

            float hpRatio = (float)unit.CurrentHp / unit.MaxHp;
            int retreatThreshold = _difficulty == "hard" ? 25 : 30;
            if (hpRatio > retreatThreshold / 100f) return null;

            // 找最近的友方治疗者或安全后方
            var allies = GetAliveUnits(unit.Faction)
                .Where(u => u != unit && u.HasSkills
                    && u.SkillIds.Any(id => {
                        var s = SkillLibrary.Get(id);
                        return s != null && s.type == SkillType.Heal;
                    }))
                .ToList();

            HexCoord retreatTarget;
            BattleUnit healerUnit = null;
            if (allies.Count > 0)
            {
                healerUnit = allies.OrderBy(a => unit.Position.Distance(a.Position)).First();
                retreatTarget = healerUnit.Position;
                GameLogger.LogDebugFormat(LogCategory.AI, "{0} HP低，向治疗者 {1}@({2},{3}) 撤退", unit.Name, healerUnit.Name, healerUnit.Position.q, healerUnit.Position.r);
            }
            else
            {
                // 向阵营后方撤退（假设敌方在右侧，向左退）
                retreatTarget = new HexCoord(Math.Max(0, unit.Position.q - 3), unit.Position.r);
                GameLogger.LogDebugFormat(LogCategory.AI, "{0} HP低，无治疗者，向阵营后方({1},{2})撤退", unit.Name, retreatTarget.q, retreatTarget.r);
            }

            var path = FindPath(unit, retreatTarget, healerUnit?.Position);
            if (path.Count > 1)
            {
                var nextStep = path[1];
                // 最终防线：如果路径第一格仍被占（理论上FindPath已排除），记录日志
                var blocker = _allUnits.FirstOrDefault(u => u.IsAlive && u != unit && u.Position == nextStep);
                if (blocker != null)
                {
                    GameLogger.LogDebugFormat(LogCategory.AI, "{0} 撤退路径第一格({1},{2})仍被 {3}[{4}] 占据，将尝试移动到该格",
                        unit.Name, nextStep.q, nextStep.r, blocker.Name, blocker.Faction);
                }
                return new AIAction
                {
                    Type = AIActionType.Retreat,
                    TargetCell = nextStep,
                    Reason = $"HP低（{unit.CurrentHp}/{unit.MaxHp}），撤退"
                };
            }
            return null;
        }

        // ========== 2. 高价值计策 ==========
        private AIAction CheckSkillUsage(BattleUnit unit)
        {
            if (!unit.HasSkills || unit.CurrentMp <= 0) return null;

            foreach (var skillId in unit.SkillIds)
            {
                var skill = SkillLibrary.Get(skillId);
                if (skill == null || unit.CurrentMp < skill.mpCost) continue;

                // 极简难度不用计策
                if (_difficulty == "easy" && skill.type != SkillType.Heal) continue;

                // AOE计策：找范围内敌人最多的点
                if (skill.isAOE && skill.targetType == SkillTargetType.Enemy)
                {
                    var bestTarget = FindBestAOETarget(unit, skill);
                    if (bestTarget.enemyCount >= (_difficulty == "hard" ? 2 : 3))
                    {
                        return new AIAction
                        {
                            Type = AIActionType.UseSkill,
                            Skill = skill,
                            TargetCell = bestTarget.cell,
                            Reason = $"AOE计策覆盖{bestTarget.enemyCount}个敌人"
                        };
                    }
                }

                // 单体攻击计策：找范围内HP最低的敌人
                if (!skill.isAOE && skill.targetType == SkillTargetType.Enemy
                    && skill.power > 0)
                {
                    var enemies = GetAliveUnits(GetEnemyFaction(unit.Faction))
                        .Where(e => unit.Position.Distance(e.Position) <= skill.range)
                        .OrderBy(e => e.CurrentHp)
                        .ToList();

                    if (enemies.Count > 0)
                    {
                        return new AIAction
                        {
                            Type = AIActionType.UseSkill,
                            Skill = skill,
                            TargetCell = enemies[0].Position,
                            Reason = $"单体计策攻击最弱目标 {enemies[0].Name}"
                        };
                    }
                }
            }
            return null;
        }

        // ========== 3. 集火攻击 ==========
        private AIAction CheckAttack(BattleUnit unit)
        {
            var enemies = GetAliveUnits(GetEnemyFaction(unit.Faction))
                .Where(e => unit.Position.Distance(e.Position) <= unit.AttackRange)
                .ToList();

            if (enemies.Count == 0) return null;

            // 威胁评估排序
            var scored = enemies.Select(e => new
            {
                unit = e,
                score = EvaluateThreat(e, unit)
            }).OrderByDescending(x => x.score).ToList();

            var target = scored[0].unit;
            return new AIAction
            {
                Type = AIActionType.Attack,
                TargetUnit = target,
                TargetCell = target.Position,
                Reason = $"攻击目标 {target.Name}（威胁值{scored[0].score:F0}）"
            };
        }

        // ========== 4. 移动逼近 ==========
        private AIAction CheckMoveTowards(BattleUnit unit)
        {
            var enemies = GetAliveUnits(GetEnemyFaction(unit.Faction));
            if (enemies.Count == 0) return null;

            // 找威胁值最低且最近的敌人
            var target = enemies
                .Select(e => new { unit = e, dist = unit.Position.Distance(e.Position), threat = EvaluateThreat(e, unit) })
                .OrderBy(e => e.dist)
                .ThenBy(e => e.threat)
                .First().unit;

            // 目标格是敌人位置，寻路时应临时排除该格，否则 FindPath 会因"目标被占据"返回空
            var path = FindPath(unit, target.Position, target.Position);
            if (path.Count <= 1)
            {
                GameLogger.LogWarningFormat(LogCategory.AI, "{0} 到 {1} 无路可通", unit.Name, target.Name);
                return null;
            }

            // 走到离敌人最近且仍在移动力范围内的格子，不踩敌人格
            int maxSteps = Math.Min(path.Count - 2, unit.MoveRange);
            if (maxSteps < 1)
            {
                GameLogger.LogDebugFormat(LogCategory.AI, "{0} 已贴近 {1}，无需移动", unit.Name, target.Name);
                return null;
            }

            var dest = path[maxSteps];
            // 确保目标格没有友方单位挡路
            if (IsCellOccupiedByAlly(dest, unit.Faction) && maxSteps > 1)
            {
                // 找前一个可用格
                for (int i = maxSteps - 1; i >= 1; i--)
                {
                    if (!IsCellOccupiedByAlly(path[i], unit.Faction))
                    {
                        dest = path[i];
                        break;
                    }
                }
            }

            GameLogger.LogDebugFormat(LogCategory.AI, "{0} 当前@({1},{2}) → 目标{3}@({4},{5}) 路径{6}格 dest=({7},{8})",
                unit.Name, unit.Position.q, unit.Position.r, target.Name, target.Position.q, target.Position.r, path.Count, dest.q, dest.r);

            if (dest == unit.Position) return null;

            return new AIAction
            {
                Type = AIActionType.Move,
                TargetCell = dest,
                Reason = $"向 {target.Name} 逼近"
            };
        }

        // ========== 辅助方法 ==========

        private AIAction DecideAttackOrMove(BattleUnit unit)
        {
            var attack = CheckAttack(unit);
            if (attack != null) return attack;
            return CheckMoveTowards(unit) ?? new AIAction { Type = AIActionType.Skip };
        }

        private Faction GetEnemyFaction(Faction self) => self == Faction.Enemy ? Faction.Player : Faction.Enemy;

        private List<BattleUnit> GetAliveUnits(Faction faction) =>
            _allUnits.Where(u => u.Faction == faction && u.IsAlive).ToList();

        /// <summary>获取除自己外所有存活单位占据的格子</summary>
        private HashSet<HexCoord> GetOccupiedCells(BattleUnit self)
        {
            var occupied = new HashSet<HexCoord>();
            foreach (var u in _allUnits)
            {
                if (u.IsAlive && u != self)
                    occupied.Add(u.Position);
            }
            return occupied;
        }

        private List<HexCoord> FindPath(BattleUnit unit, HexCoord target)
        {
            var pf = new PathFinder(_grid);
            return pf.FindPath(unit.Position, target, unit.UnitClass, GetOccupiedCells(unit));
        }

        /// <summary>寻路，但临时将 excludeCell 从占据列表中排除（用于目标格本身被占据时）</summary>
        private List<HexCoord> FindPath(BattleUnit unit, HexCoord target, HexCoord? excludeCell)
        {
            var occupied = GetOccupiedCells(unit);
            if (excludeCell.HasValue)
                occupied.Remove(excludeCell.Value);
            var pf = new PathFinder(_grid);
            return pf.FindPath(unit.Position, target, unit.UnitClass, occupied);
        }

        private bool IsCellOccupiedByAlly(HexCoord cell, Faction faction) =>
            _allUnits.Any(u => u.IsAlive && u.Faction == faction && u.Position == cell);

        /// <summary>威胁评估</summary>
        private float EvaluateThreat(BattleUnit target, BattleUnit self)
        {
            float threat = 0;
            // HP权重：HP越低越优先集火
            threat += (1f - (float)target.CurrentHp / target.MaxHp) * 300f;
            // 输出权重
            threat += target.Strength * 2f;
            // 距离权重：近的优先
            int dist = self.Position.Distance(target.Position);
            threat -= dist * 10f;
            // 兵种克制加成
            float counter = ClassData.GetCounterMultiplier(self.UnitClass, target.UnitClass);
            if (counter > 1f) threat += 150f;
            // 关键单位加成
            if (target.Name.Contains("李世民")) threat += 500f;
            if (target.UnitClass == ClassType.Strategist) threat += 200f;
            if (target.SkillIds.Any(id => {
                var s = SkillLibrary.Get(id);
                return s != null && s.type == SkillType.Heal;
            })) threat += 150f;

            return threat;
        }

        /// <summary>找最佳AOE目标点</summary>
        private (HexCoord cell, int enemyCount) FindBestAOETarget(BattleUnit caster, SkillData skill)
        {
            var enemies = GetAliveUnits(GetEnemyFaction(caster.Faction));
            int bestCount = 0;
            HexCoord bestCell = caster.Position;

            // 在施法范围内搜索所有可达格
            var pf = new PathFinder(_grid);
            var reachable = pf.GetMoveRange(caster.Position, skill.range, caster.UnitClass);

            foreach (var (cell, _) in reachable)
            {
                int count = cell.Range(skill.aoeRadius)
                    .Count(c => _grid.InBounds(c) && enemies.Any(e => e.Position == c && e.IsAlive));
                if (count > bestCount)
                {
                    bestCount = count;
                    bestCell = cell;
                }
            }

            return (bestCell, bestCount);
        }
    }
}
