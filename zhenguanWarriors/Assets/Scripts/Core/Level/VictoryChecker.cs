using System.Collections.Generic;
using System.Linq;
using ZhenguanWarriors.Core.Battle;

namespace ZhenguanWarriors.Core.Level
{
    /// <summary>
    /// 胜负检查器——每回合结束后检查胜负条件
    /// </summary>
    public class VictoryChecker
    {
        private readonly LevelData _level;
        private readonly List<BattleUnit> _allUnits;
        private readonly TurnManager _turnManager;

        public bool IsVictory { get; private set; }
        public bool IsDefeat { get; private set; }
        public string ResultMessage { get; private set; }

        public VictoryChecker(LevelData level, List<BattleUnit> allUnits, TurnManager turnManager)
        {
            _level = level;
            _allUnits = allUnits;
            _turnManager = turnManager;
        }

        /// <summary>
        /// 检查胜负（每回合结束时调用）
        /// </summary>
        public void Check()
        {
            if (IsVictory || IsDefeat) return;

            // 先检查失败条件（优先级更高）
            foreach (var defeatType in _level.defeatTypes)
            {
                if (CheckDefeatCondition(defeatType))
                {
                    IsDefeat = true;
                    return;
                }
            }

            // 再检查胜利条件
            if (CheckVictoryCondition())
            {
                IsVictory = true;
            }
        }

        private bool CheckVictoryCondition()
        {
            switch (_level.victoryType)
            {
                case VictoryConditionType.DefeatAll:
                    var enemies = GetAliveUnits(Faction.Enemy);
                    if (enemies.Count == 0)
                    {
                        ResultMessage = "🎉 敌军全灭，大获全胜！";
                        return true;
                    }
                    break;

                case VictoryConditionType.DefeatBoss:
                    var boss = _allUnits.FirstOrDefault(u =>
                        u.Id == _level.targetBossId && u.Faction == Faction.Enemy);
                    if (boss != null && boss.IsDead)
                    {
                        ResultMessage = $"🎉 击破 {boss.Name}，胜利！";
                        return true;
                    }
                    break;

                case VictoryConditionType.DefendTurns:
                    if (_turnManager.TurnNumber >= _level.defendTurns)
                    {
                        ResultMessage = $"🎉 坚守 {_level.defendTurns} 回合，敌军退兵！";
                        return true;
                    }
                    break;

                case VictoryConditionType.ReachPoint:
                    var players = GetAliveUnits(Faction.Player);
                    if (players.Any(u => u.Position == _level.reachPoint))
                    {
                        ResultMessage = "🎉 成功抵达目标地点！";
                        return true;
                    }
                    break;

                case VictoryConditionType.Survive:
                    var liShiMin = _allUnits.FirstOrDefault(u => u.Id == "lishimin");
                    if (liShiMin != null && liShiMin.IsAlive)
                    {
                        ResultMessage = "🎉 战斗结束，全员生还！";
                        return true;
                    }
                    break;
            }
            return false;
        }

        private bool CheckDefeatCondition(DefeatConditionType type)
        {
            switch (type)
            {
                case DefeatConditionType.PlayerDead:
                    var liShiMin = _allUnits.FirstOrDefault(u => u.Id == "lishimin");
                    if (liShiMin != null && liShiMin.IsDead)
                    {
                        ResultMessage = "💀 李世民阵亡，战斗失败...";
                        return true;
                    }
                    break;

                case DefeatConditionType.AllDead:
                    var players = GetAliveUnits(Faction.Player);
                    if (players.Count == 0)
                    {
                        ResultMessage = "💀 全军覆没...";
                        return true;
                    }
                    break;

                case DefeatConditionType.BossReachPoint:
                    // 检查敌方Boss是否到达指定点
                    var boss = _allUnits.FirstOrDefault(u =>
                        u.Id == _level.targetBossId && u.Faction == Faction.Enemy);
                    if (boss != null && boss.IsAlive && boss.Position == _level.reachPoint)
                    {
                        ResultMessage = $"💀 {boss.Name} 突破了防线！";
                        return true;
                    }
                    break;

                case DefeatConditionType.TurnLimit:
                    if (_level.maxTurns > 0 && _turnManager.TurnNumber > _level.maxTurns)
                    {
                        ResultMessage = $"💀 超过 {_level.maxTurns} 回合上限，战斗失败...";
                        return true;
                    }
                    break;
            }
            return false;
        }

        private List<BattleUnit> GetAliveUnits(Faction faction) =>
            _allUnits.Where(u => u.Faction == faction && u.IsAlive).ToList();
    }
}
