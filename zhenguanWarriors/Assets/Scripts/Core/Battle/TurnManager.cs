using System;
using System.Collections.Generic;
using System.Linq;

namespace ZhenguanWarriors.Core.Battle
{
    /// <summary>
    /// 回合控制器——管理战斗流程
    /// </summary>
    public class TurnManager
    {
        public enum TurnPhase
        {
            Setup,          // 战前部署
            PlayerTurn,     // 玩家回合
            EnemyTurn,      // 敌方回合
            AllyTurn,       // 友军回合
            VictoryCheck,   // 胜负判定
            Victory,        // 胜利
            Defeat,         // 失败
            Event           // 关卡事件
        }

        public TurnPhase CurrentPhase { get; private set; } = TurnPhase.Setup;
        public int TurnNumber { get; private set; } = 0;

        private readonly List<BattleUnit> _allUnits;
        private readonly Queue<BattleUnit> _actionQueue = new();
        private BattleUnit _currentUnit;

        // 事件回调
        public Action<BattleUnit> OnUnitTurnStart;
        public Action<BattleUnit> OnUnitTurnEnd;
        public Action<TurnPhase> OnPhaseChanged;

        /// <summary>自定义胜负检查回调（由BattleTestController设置，使用VictoryChecker）</summary>
        public Action OnCustomVictoryCheck { get; set; }

        public TurnManager(List<BattleUnit> allUnits)
        {
            _allUnits = allUnits;
        }

        /// <summary>开始战斗</summary>
        public void StartBattle()
        {
            TurnNumber = 1;
            StartPlayerTurn();
        }

        /// <summary>开始玩家回合</summary>
        public void StartPlayerTurn()
        {
            SetPhase(TurnPhase.PlayerTurn);
            var playerUnits = GetAliveUnits(Faction.Player);
            foreach (var u in playerUnits)
                u.SetReady();

            _actionQueue.Clear();
            foreach (var u in playerUnits)
                _actionQueue.Enqueue(u);

            NextUnit();
        }

        /// <summary>切换到敌方回合</summary>
        public void StartEnemyTurn()
        {
            SetPhase(TurnPhase.EnemyTurn);
            var enemyUnits = GetAliveUnits(Faction.Enemy);
            foreach (var u in enemyUnits)
                u.SetReady();

            _actionQueue.Clear();
            foreach (var u in enemyUnits)
                _actionQueue.Enqueue(u);

            NextUnit();
        }

        /// <summary>切换到友军回合（当前无友军AI，作为兜底避免卡死）</summary>
        public void StartAllyTurn()
        {
            SetPhase(TurnPhase.AllyTurn);
            var allyUnits = GetAliveUnits(Faction.Ally);
            foreach (var u in allyUnits)
                u.SetReady();

            _actionQueue.Clear();
            foreach (var u in allyUnits)
                _actionQueue.Enqueue(u);

            NextUnit();
        }

        /// <summary>获取当前行动单位</summary>
        public BattleUnit CurrentUnit => _currentUnit;

        /// <summary>当前单位结束行动</summary>
        public void EndUnitAction()
        {
            if (_currentUnit != null)
            {
                _currentUnit.State = UnitState.Done;
                _currentUnit.HasActed = true;
                OnUnitTurnEnd?.Invoke(_currentUnit);
                _currentUnit = null;
            }
            NextUnit();
        }

        /// <summary>队列中下一个单位</summary>
        private void NextUnit()
        {
            if (_actionQueue.Count > 0)
            {
                _currentUnit = _actionQueue.Dequeue();
                _currentUnit.State = UnitState.Ready;
                OnUnitTurnStart?.Invoke(_currentUnit);
            }
            else
            {
                // 当前方所有单位行动完毕
                _currentUnit = null;
                CheckPhaseEnd();
            }
        }

        /// <summary>检查当前方是否全部行动完毕，切换回合或检查胜负</summary>
        private void CheckPhaseEnd()
        {
            // 优先使用自定义胜负检查（VictoryChecker）
            if (OnCustomVictoryCheck != null)
            {
                OnCustomVictoryCheck();
                if (CurrentPhase == TurnPhase.Victory || CurrentPhase == TurnPhase.Defeat)
                    return;
            }
            else
            {
                if (CheckVictory() || CheckDefeat())
                    return;
            }

            switch (CurrentPhase)
            {
                case TurnPhase.PlayerTurn:
                    StartEnemyTurn();
                    break;
                case TurnPhase.EnemyTurn:
                    StartAllyTurn(); // 敌方回合后进入友军回合（当前多作为兜底）
                    break;
                case TurnPhase.AllyTurn:
                    TurnNumber++;
                    StartPlayerTurn();
                    break;
            }
        }

        /// <summary>跳过当前单位（AI使用）</summary>
        public void SkipCurrentUnit() => EndUnitAction();

        // ========== 胜负判定 ==========

        public bool CheckVictory()
        {
            // 敌方全灭即为胜利
            if (GetAliveUnits(Faction.Enemy).Count == 0)
            {
                SetPhase(TurnPhase.Victory);
                return true;
            }
            return false;
        }

        public bool CheckDefeat()
        {
            // 玩家全灭即为失败
            if (GetAliveUnits(Faction.Player).Count == 0)
            {
                SetPhase(TurnPhase.Defeat);
                return true;
            }
            return false;
        }

        // ========== 辅助 ==========

        /// <summary>设置当前阶段（公开供VictoryChecker使用）</summary>
        public void SetPhase(TurnPhase phase)
        {
            CurrentPhase = phase;
            OnPhaseChanged?.Invoke(phase);
        }

        public List<BattleUnit> GetAliveUnits(Faction faction) =>
            _allUnits.Where(u => u.Faction == faction && u.IsAlive).ToList();

        public List<BattleUnit> GetAllAliveUnits() =>
            _allUnits.Where(u => u.IsAlive).ToList();

        /// <summary>获取某格上的单位</summary>
        public BattleUnit GetUnitAt(HexCoord pos) =>
            _allUnits.FirstOrDefault(u => u.Position == pos && u.IsAlive);
    }
}
