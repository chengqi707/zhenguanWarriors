using UnityEngine;
using ZhenguanWarriors.Core.Battle;
using System.Collections.Generic;
using System.Linq;

namespace ZhenguanWarriors.View.BattleView
{
    /// <summary>
    /// 战斗测试控制器——点击交互、单位放置、回合推进
    /// </summary>
    public class BattleTestController : MonoBehaviour
    {
        private HexGridView _hexView;
        private TurnManager _turnManager;
        private List<BattleUnit> _allUnits = new();

        // Unit prefab (用简单方块代替)
        public GameObject unitPrefab;

        private BattleUnit _selectedUnit;
        private HexCoord? _selectedCell;

        // 单位可视化对象
        private Dictionary<BattleUnit, GameObject> _unitObjects = new();

        void Start()
        {
            _hexView = GetComponent<HexGridView>();
            if (_hexView == null)
            {
                Debug.LogError("需要 HexGridView 组件");
                return;
            }

            SetupBattleUnits();

            _turnManager = new TurnManager(_allUnits);
            _turnManager.OnUnitTurnStart += OnUnitTurnStart;
            _turnManager.OnUnitTurnEnd += OnUnitTurnEnd;
            _turnManager.OnPhaseChanged += OnPhaseChanged;

            _turnManager.StartBattle();
        }

        void Update()
        {
            if (Input.GetMouseButtonDown(0))
                HandleClick();

            if (Input.GetKeyDown(KeyCode.Space))
                EndCurrentTurn();
        }

        // ========== 设置单位 ==========

        private void SetupBattleUnits()
        {
            // 玩家方——李世民
            var liShiMin = new BattleUnit("lishimin", "李世民", Faction.Player,
                str: 82, cmd: 95, @int: 88, agi: 78, luk: 90,
                hp: 120, mp: 50, move: 5, attackRange: 1)
            {
                Position = new HexCoord(1, 3)
            };
            _allUnits.Add(liShiMin);

            // 玩家方——李靖
            var liJing = new BattleUnit("li_jing", "李靖", Faction.Player,
                str: 90, cmd: 85, @int: 75, agi: 70, luk: 75,
                hp: 100, mp: 30, move: 6, attackRange: 1)
            {
                Position = new HexCoord(2, 5)
            };
            _allUnits.Add(liJing);

            // 敌方——校尉
            var enemy1 = new BattleUnit("enemy_1", "刘校尉", Faction.Enemy,
                str: 60, cmd: 50, @int: 30, agi: 40, luk: 30,
                hp: 60, mp: 10, move: 4, attackRange: 1)
            {
                Position = new HexCoord(8, 3)
            };
            _allUnits.Add(enemy1);

            // 敌方——步兵
            var enemy2 = new BattleUnit("enemy_2", "张步兵", Faction.Enemy,
                str: 55, cmd: 60, @int: 20, agi: 35, luk: 25,
                hp: 80, mp: 0, move: 3, attackRange: 1)
            {
                Position = new HexCoord(9, 5)
            };
            _allUnits.Add(enemy2);

            // 生成单位可视化
            foreach (var unit in _allUnits)
                SpawnUnitVisual(unit);
        }

        private void SpawnUnitVisual(BattleUnit unit)
        {
            if (unitPrefab == null)
            {
                // 没有预制体时用临时方块
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.localScale = new Vector3(0.4f, 0.4f, 0.1f);
                cube.transform.position = _hexView.HexToWorld(unit.Position);
                var color = unit.Faction == Faction.Player ? Color.blue : Color.red;
                cube.GetComponent<Renderer>().material.color = color;
                _unitObjects[unit] = cube;
            }
        }

        // ========== 交互处理 ==========

        private void HandleClick()
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0;

            var clickedCell = _hexView.WorldToHex(mousePos);
            if (clickedCell == null) return;

            HexCoord cell = clickedCell.Value;
            BattleUnit unitAtCell = _turnManager.GetUnitAt(cell);

            // 如果点击的是自己的单位——选中它
            if (unitAtCell != null && unitAtCell.Faction == Faction.Player
                && unitAtCell.State == UnitState.Ready)
            {
                SelectUnit(unitAtCell);
                return;
            }

            // 如果已经选中了单位——尝试移动/攻击
            if (_selectedUnit != null && _selectedUnit.State == UnitState.Ready)
            {
                // 点击敌人→攻击
                if (unitAtCell != null && unitAtCell.Faction == Faction.Enemy
                    && _selectedUnit.Position.Distance(cell) <= _selectedUnit.AttackRange)
                {
                    AttackUnit(_selectedUnit, unitAtCell);
                    return;
                }

                // 点击空地→移动
                var range = _hexView.PathFinder.GetMoveRange(
                    _selectedUnit.Position, _selectedUnit.MoveRange);
                if (range.ContainsKey(cell))
                {
                    MoveUnit(_selectedUnit, cell);
                    return;
                }
            }

            // 点击其他地方取消选中
            DeselectUnit();
        }

        // ========== 单位操作 ==========

        private void SelectUnit(BattleUnit unit)
        {
            DeselectUnit();
            _selectedUnit = unit;
            _hexView.ShowMoveRange(unit.Position, unit.MoveRange);
            Debug.Log($"选中: {unit.Name}");
        }

        private void DeselectUnit()
        {
            _selectedUnit = null;
            _hexView.ClearHighlights();
        }

        private void MoveUnit(BattleUnit unit, HexCoord target)
        {
            // 更新逻辑位置
            var oldPos = unit.Position;
            unit.Position = target;

            // 更新可视化位置
            if (_unitObjects.TryGetValue(unit, out var go))
                go.transform.position = _hexView.HexToWorld(target);

            _hexView.ClearHighlights();
            _selectedUnit = null;

            Debug.Log($"{unit.Name} 移动到 {target}");

            // 移动后自动结束行动
            EndUnitAction();
        }

        private void AttackUnit(BattleUnit attacker, BattleUnit defender)
        {
            var result = CombatCalculator.CalcPhysicalDamage(attacker, defender, 0, 0);
            string critText = result.isCrit ? "【暴击】" : "";
            string hitText = result.isHit ? $"造成 {result.damage} 点伤害" : "未命中";

            defender.TakeDamage(result.damage);
            Debug.Log($"{attacker.Name} 攻击 {defender.Name}：{hitText}{critText} (HP:{defender.CurrentHp}/{defender.MaxHp})");

            // 检查是否击杀
            if (defender.IsDead)
            {
                Debug.Log($"{defender.Name} 阵亡！");
                if (_unitObjects.TryGetValue(defender, out var go))
                    Destroy(go);
                _unitObjects.Remove(defender);
            }

            _hexView.ClearHighlights();
            _selectedUnit = null;
            EndUnitAction();
        }

        /// <summary>当前单位结束行动</summary>
        private void EndUnitAction()
        {
            if (_turnManager.CurrentPhase == TurnManager.TurnPhase.PlayerTurn)
                _turnManager.EndUnitAction();
        }

        /// <summary>按空格结束整个玩家回合</summary>
        private void EndCurrentTurn()
        {
            if (_turnManager.CurrentPhase == TurnManager.TurnPhase.PlayerTurn)
            {
                DeselectUnit();
                // 把所有还没行动的单位标记为已行动
                while (_turnManager.CurrentUnit != null)
                    _turnManager.EndUnitAction();
            }
        }

        // ========== 回合事件回调 ==========

        private void OnUnitTurnStart(BattleUnit unit)
        {
            if (unit.Faction == Faction.Enemy)
            {
                // 敌方AI：简单的自动攻击
                EnemyAI(unit);
            }
        }

        private void OnUnitTurnEnd(BattleUnit unit)
        {
            // 不需要额外处理
        }

        private void OnPhaseChanged(TurnManager.TurnPhase phase)
        {
            switch (phase)
            {
                case TurnManager.TurnPhase.PlayerTurn:
                    Debug.Log($"===== 玩家回合 {_turnManager.TurnNumber} =====");
                    break;
                case TurnManager.TurnPhase.EnemyTurn:
                    Debug.Log("===== 敌方回合 =====");
                    break;
                case TurnManager.TurnPhase.Victory:
                    Debug.Log("🎉 胜利！");
                    break;
                case TurnManager.TurnPhase.Defeat:
                    Debug.Log("💀 失败...");
                    break;
            }
        }

        // ========== 简易 AI ==========

        private void EnemyAI(BattleUnit enemy)
        {
            if (!enemy.IsAlive) return;

            // 找最近的玩家单位
            var targets = _allUnits
                .Where(u => u.Faction == Faction.Player && u.IsAlive)
                .OrderBy(u => enemy.Position.Distance(u.Position))
                .ToList();

            if (targets.Count == 0) return;
            var target = targets[0];
            int dist = enemy.Position.Distance(target.Position);

            if (dist <= enemy.AttackRange)
            {
                // 攻击
                var result = CombatCalculator.CalcPhysicalDamage(enemy, target, 0, 0);
                target.TakeDamage(result.damage);
                Debug.Log($"AI {enemy.Name} 攻击 {target.Name}：{(result.isHit ? $"造成{result.damage}伤害" : "未命中")}");

                if (target.IsDead && _unitObjects.TryGetValue(target, out var go))
                {
                    Destroy(go);
                    _unitObjects.Remove(target);
                }
            }
            else
            {
                // 移动到目标方向
                var path = _hexView.PathFinder.FindPath(enemy.Position, target.Position);
                if (path.Count > 1)
                {
                    var nextStep = path[1];
                    enemy.Position = nextStep;
                    if (_unitObjects.TryGetValue(enemy, out var go))
                        go.transform.position = _hexView.HexToWorld(nextStep);
                }
            }

            // AI 行动完毕
            _turnManager.EndUnitAction();
        }
    }
}
