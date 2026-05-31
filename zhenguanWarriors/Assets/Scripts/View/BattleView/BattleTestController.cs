using UnityEngine;
using ZhenguanWarriors.Core.Battle;
using ZhenguanWarriors.Core.Combat;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

namespace ZhenguanWarriors.View.BattleView
{
    /// <summary>
    /// 战斗测试控制器——点击交互、单位放置、回合推进
    /// </summary>
    public class BattleTestController : MonoBehaviour
    {
        private HexGridView _hexView;
        private BattleUI _battleUI;
        private TurnManager _turnManager;
        private List<BattleUnit> _allUnits = new();

        private BattleUnit _selectedUnit;
        private Dictionary<BattleUnit, UnitVisual> _unitVisuals = new();
        private bool _isAnimating; // 防止动画中操作

        void Start()
        {
            _hexView = GetComponent<HexGridView>();
            _battleUI = GetComponent<BattleUI>();
            if (_hexView == null)
            {
                Debug.LogError("需要 HexGridView 组件");
                return;
            }

            // 如果没有 BattleUI，创建一个
            if (_battleUI == null)
                _battleUI = gameObject.AddComponent<BattleUI>();

            SetupBattleUnits();
            SetupCamera();

            _turnManager = new TurnManager(_allUnits);
            _turnManager.OnUnitTurnStart += OnUnitTurnStart;
            _turnManager.OnUnitTurnEnd += OnUnitTurnEnd;
            _turnManager.OnPhaseChanged += OnPhaseChanged;

            _battleUI.ShowTip("左键选中己方单位 → 点击格子移动/攻击 | Space=结束回合");
            _turnManager.StartBattle();
        }

        void Update()
        {
            if (_isAnimating) return;

            if (Input.GetMouseButtonDown(0))
                HandleClick();

            if (Input.GetKeyDown(KeyCode.Space))
                EndCurrentTurn();
        }

        // ========== 摄像机设置 ==========

        private void SetupCamera()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                cam.transform.position = new Vector3(6f, 4f, -10f);
            }
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
            {
                var visual = UnitVisual.Create(unit, _hexView);
                _unitVisuals[unit] = visual;
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
                    StartCoroutine(MoveUnitAnimation(_selectedUnit, cell));
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

            if (_unitVisuals.TryGetValue(unit, out var visual))
                visual.SetSelected(true);

            _battleUI?.ShowTip($"选中 {unit.Name} (HP:{unit.CurrentHp}/{unit.MaxHp})");
        }

        private void DeselectUnit()
        {
            if (_selectedUnit != null && _unitVisuals.TryGetValue(_selectedUnit, out var oldVis))
                oldVis.SetSelected(false);

            _selectedUnit = null;
            _hexView.ClearHighlights();
        }

        // ========== 平滑移动（协程） ==========

        private IEnumerator MoveUnitAnimation(BattleUnit unit, HexCoord target)
        {
            _isAnimating = true;
            _hexView.ClearHighlights();
            DeselectUnit();

            // 计算路径
            var path = _hexView.PathFinder.FindPath(unit.Position, target);
            if (path.Count < 2)
            {
                _isAnimating = false;
                yield break;
            }

            if (!_unitVisuals.TryGetValue(unit, out var visual))
            {
                _isAnimating = false;
                yield break;
            }

            // 沿路径逐格移动
            float stepDuration = 0.15f;
            for (int i = 1; i < path.Count; i++)
            {
                unit.Position = path[i];
                Vector3 startPos = visual.transform.position;
                Vector3 endPos = _hexView.HexToWorld(path[i]);

                float elapsed = 0f;
                while (elapsed < stepDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / stepDuration;
                    t = t * t * (3f - 2f * t); // smoothstep
                    visual.transform.position = Vector3.Lerp(startPos, endPos, t);
                    yield return null;
                }
                visual.transform.position = endPos;
            }

            _battleUI?.ShowTip($"{unit.Name} 移动到 ({target.q},{target.r})");
            _isAnimating = false;
            EndUnitAction();
        }

        // ========== 攻击 ==========

        private void AttackUnit(BattleUnit attacker, BattleUnit defender)
        {
            var result = CombatCalculator.CalcPhysicalDamage(attacker, defender, 0, 0);

            defender.TakeDamage(result.damage);

            string critText = result.isCrit ? "【暴击】" : "";
            string hitText = result.isHit ?
                $"造成 {result.damage} 点伤害" : "未命中";
            string log = $"{attacker.Name} 攻击 {defender.Name}：{hitText}{critText}";

            _battleUI?.ShowTip(log);
            Debug.Log(log);

            // 更新血条
            if (_unitVisuals.TryGetValue(defender, out var defVis))
            {
                defVis.UpdateHpBar();

                // 受击闪白效果
                StartCoroutine(FlashEffect(defVis));
            }

            // 检查阵亡
            if (defender.IsDead)
            {
                Debug.Log($"{defender.Name} 阵亡！");
                if (_unitVisuals.TryGetValue(defender, out var deadVis))
                {
                    StartCoroutine(DeathAnimation(deadVis, defender));
                }
            }

            _hexView.ClearHighlights();
            _selectedUnit = null;
            EndUnitAction();
        }

        private IEnumerator FlashEffect(UnitVisual visual)
        {
            visual.unitSprite.color = Color.white;
            yield return new WaitForSeconds(0.1f);
            if (visual != null) // 可能已被销毁
                visual.unitSprite.color = visual.unitSprite.color; // 恢复
        }

        private IEnumerator DeathAnimation(UnitVisual visual, BattleUnit unit)
        {
            float duration = 0.4f;
            float elapsed = 0f;
            Vector3 startScale = visual.transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float scale = 1f - t * t;
                visual.transform.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            Destroy(visual.gameObject);
            _unitVisuals.Remove(unit);
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
                _battleUI?.ShowTip("结束玩家回合...");
                while (_turnManager.CurrentUnit != null)
                    _turnManager.EndUnitAction();
            }
        }

        // ========== 回合事件回调 ==========

        private void OnUnitTurnStart(BattleUnit unit)
        {
            if (unit.Faction == Faction.Enemy)
            {
                _battleUI?.ShowTip($"敌方 {unit.Name} 行动...");
                EnemyAI(unit);
            }
            else if (unit.Faction == Faction.Player)
            {
                _battleUI?.ShowTip($"请操作 {unit.Name}");
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
                    _battleUI?.UpdateTurnInfo(_turnManager.TurnNumber, "玩家回合");
                    break;
                case TurnManager.TurnPhase.EnemyTurn:
                    _battleUI?.UpdateTurnInfo(_turnManager.TurnNumber, "敌方回合");
                    break;
                case TurnManager.TurnPhase.Victory:
                    _battleUI?.UpdateTurnInfo(_turnManager.TurnNumber, "🎉 胜利！");
                    _battleUI?.ShowTip("恭喜通关！按空格重新开始？");
                    break;
                case TurnManager.TurnPhase.Defeat:
                    _battleUI?.UpdateTurnInfo(_turnManager.TurnNumber, "💀 失败");
                    _battleUI?.ShowTip("战败... 按空格重新挑战");
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

                if (_unitVisuals.TryGetValue(target, out var tVis))
                    tVis.UpdateHpBar();

                if (target.IsDead && _unitVisuals.TryGetValue(target, out var deadVis))
                    Destroy(deadVis.gameObject);
            }
            else
            {
                // 移动一步接近
                var path = _hexView.PathFinder.FindPath(enemy.Position, target.Position);
                if (path.Count > 1)
                {
                    var nextStep = path[1];
                    enemy.Position = nextStep;
                    if (_unitVisuals.TryGetValue(enemy, out var eVis))
                        eVis.UpdatePosition();
                }
            }

            _turnManager.EndUnitAction();
        }
    }
}
