using UnityEngine;
using ZhenguanWarriors.Core.Battle;
using ZhenguanWarriors.Core.Combat;
using ZhenguanWarriors.Core.Character;
using ZhenguanWarriors.Core.AI;
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

        private SkillExecutor _skillExecutor;
        private WeatherSystem _weather;
        private AIBehaviorTree _aiTree;
        private BattleUnit _selectedUnit;
        private Dictionary<BattleUnit, UnitVisual> _unitVisuals = new();
        private bool _isAnimating; // 防止动画中操作

        // 计策交互
        private string _selectedSkillId;   // null = 普通攻击
        private Vector2 _scrollPos;
        private Rect _skillPanelRect;

        // 单挑
        private DuelSystem _duelSystem;
        private bool _inDuel = false;
        private BattleUnit _duelEnemy;

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

            // 天气系统（当前晴天，可切换测试）
            _weather = new WeatherSystem(WeatherType.Sunny, WindDirection.None);
            _skillExecutor = new SkillExecutor(_allUnits, _hexView.Grid, _weather);
            _aiTree = new AIBehaviorTree(_allUnits, _hexView.Grid, _weather, _skillExecutor, "normal");

            _turnManager = new TurnManager(_allUnits);
            _turnManager.OnUnitTurnStart += OnUnitTurnStart;
            _turnManager.OnUnitTurnEnd += OnUnitTurnEnd;
            _turnManager.OnPhaseChanged += OnPhaseChanged;

            _battleUI.ShowTip("选中己方单位 → 点击移动/攻击 | 底部选择计策");
            _turnManager.StartBattle();
        }

        void Update()
        {
            if (_isAnimating) return;

            // 手机触摸输入
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                    HandleScreenTap(touch.position);
            }
            // 鼠标点击（Editor/PC 调试用）
            else if (Input.GetMouseButtonDown(0))
            {
                HandleScreenTap(Input.mousePosition);
            }

            if (Input.GetKeyDown(KeyCode.Space))
                EndCurrentTurn();
        }

        /// <summary>屏幕坐标 → 世界坐标 → 处理点击</summary>
        private void HandleScreenTap(Vector2 screenPos)
        {
            // Camera.main 安全检测
            if (Camera.main == null)
            {
                Debug.LogError("Camera.main 为空");
                return;
            }

            // 检查 HexGridView 是否就绪
            if (_hexView == null || _hexView.Grid == null)
            {
                Debug.LogError("HexGridView 尚未初始化");
                return;
            }

            Vector3 worldPos = Camera.main.ScreenToWorldPoint(
                new Vector3(screenPos.x, screenPos.y, 0));
            worldPos.z = 0;

            ProcessClick(worldPos);
        }

        // ========== 设置单位 ==========

        private void SetupBattleUnits()
        {
            // 玩家方——李世民（君主光环 + 鼓舞）
            var liShiMin = new BattleUnit("lishimin", "李世民", Faction.Player, ClassType.Cavalry,
                str: 82, cmd: 95, @int: 88, agi: 78, luk: 90,
                hp: 120, mp: 50, move: 5, attackRange: 1)
            {
                Position = new HexCoord(1, 3),
                SkillIds = new List<string> { "rally" }
            };
            _allUnits.Add(liShiMin);

            // 玩家方——李靖（统帅 + 火攻 + 水攻）
            var liJing = new BattleUnit("li_jing", "李靖", Faction.Player, ClassType.Cavalry,
                str: 90, cmd: 85, @int: 75, agi: 70, luk: 75,
                hp: 100, mp: 50, move: 6, attackRange: 1)
            {
                Position = new HexCoord(2, 5),
                SkillIds = new List<string> { "fire_attack", "water_attack" }
            };
            _allUnits.Add(liJing);

            // 敌方——校尉
            var enemy1 = new BattleUnit("enemy_1", "刘校尉", Faction.Enemy, ClassType.Infantry,
                str: 60, cmd: 50, @int: 30, agi: 40, luk: 30,
                hp: 60, mp: 10, move: 4, attackRange: 1)
            {
                Position = new HexCoord(8, 3)
            };
            _allUnits.Add(enemy1);

            // 敌方——步兵
            var enemy2 = new BattleUnit("enemy_2", "张步兵", Faction.Enemy, ClassType.HeavyInfantry,
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

        private void ProcessClick(Vector3 worldPos)
        {
            var clickedCell = _hexView.WorldToHex(worldPos);
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

            // 如果已经选中了单位——尝试计策/攻击/移动
            if (_selectedUnit != null && _selectedUnit.State == UnitState.Ready)
            {
                // 计策优先：如果选中了计策
                if (!string.IsNullOrEmpty(_selectedSkillId))
                {
                    TryUseSkill(_selectedUnit, cell);
                    return;
                }

                // 点击敌人→普通攻击
                if (unitAtCell != null && unitAtCell.Faction == Faction.Enemy
                    && _selectedUnit.Position.Distance(cell) <= _selectedUnit.AttackRange)
                {
                    AttackUnit(_selectedUnit, unitAtCell);
                    return;
                }

                // 点击友方→如果有治疗计策选中，尝试治疗
                if (unitAtCell != null && unitAtCell.Faction == Faction.Player && !_selectedUnit.HasActed)
                {
                    // 检查是否有治疗计策并自动使用
                    var healSkill = _selectedUnit.SkillIds
                        .Select(id => SkillLibrary.Get(id))
                        .FirstOrDefault(s => s != null && s.type == SkillType.Heal
                            && _selectedUnit.CurrentMp >= s.mpCost
                            && _selectedUnit.Position.Distance(cell) <= s.range);
                    if (healSkill != null)
                    {
                        _selectedSkillId = healSkill.id;
                        TryUseSkill(_selectedUnit, cell);
                        return;
                    }
                }

                // 点击空地→移动
                var range = _hexView.PathFinder.GetMoveRange(
                    _selectedUnit.Position, _selectedUnit.MoveRange, _selectedUnit.UnitClass);
                if (range.ContainsKey(cell))
                {
                    _selectedSkillId = null; // 移动时取消计策选择
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
            _hexView.ShowMoveRange(unit.Position, unit.MoveRange, unit.UnitClass);

            if (_unitVisuals.TryGetValue(unit, out var visual))
                visual.SetSelected(true);

            // 显示计策信息
            string skillInfo = "";
            if (unit.HasSkills)
            {
                var names = unit.SkillIds
                    .Select(id => SkillLibrary.Get(id))
                    .Where(s => s != null)
                    .Select(s => $"{s.name}({s.mpCost}MP)");
                skillInfo = " | 计策: " + string.Join(" ", names);
            }
            _battleUI?.ShowTip($"选中 {unit.Name} [HP:{unit.CurrentHp}/{unit.MaxHp} MP:{unit.CurrentMp}]{skillInfo}");
        }
        private void DeselectUnit()
        {
            if (_selectedUnit != null && _unitVisuals.TryGetValue(_selectedUnit, out var oldVis))
                oldVis.SetSelected(false);

            _selectedUnit = null;
            _selectedSkillId = null;
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

            unit.HasMovedThisTurn = true;
            _battleUI?.ShowTip($"{unit.Name} 移动到 ({target.q},{target.r})");
            _isAnimating = false;
            EndUnitAction();
        }

        // ========== 攻击 ==========

        private void AttackUnit(BattleUnit attacker, BattleUnit defender)
        {
            var attackerTerrain = _hexView.Grid.GetTerrain(attacker.Position);
            var result = CombatCalculator.CalcPhysicalDamage(attacker, defender, 0, 0, attackerTerrain);

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

        // ========== 计策系统 ==========

        /// <summary>尝试释放计策</summary>
        private void TryUseSkill(BattleUnit caster, HexCoord targetCell)
        {
            var skill = SkillLibrary.Get(_selectedSkillId);
            if (skill == null)
            {
                _selectedSkillId = null;
                return;
            }

            // 距离检测
            int dist = caster.Position.Distance(targetCell);
            if (dist > skill.range)
            {
                _battleUI?.ShowTip($"目标超出{skill.name}的射程（{skill.range}格）");
                return;
            }

            // 目标有效性检测
            if (skill.targetType == SkillTargetType.Enemy)
            {
                var targetUnit = _turnManager.GetUnitAt(targetCell);
                if (targetUnit == null || targetUnit.Faction == caster.Faction)
                {
                    _battleUI?.ShowTip("请选择一个敌人为目标");
                    return;
                }
            }
            else if (skill.targetType == SkillTargetType.Ally)
            {
                var targetUnit = _turnManager.GetUnitAt(targetCell);
                if (targetUnit == null || targetUnit.Faction != caster.Faction)
                {
                    _battleUI?.ShowTip("请选择一个友军为目标");
                    return;
                }
            }

            // MP检测
            if (caster.CurrentMp < skill.mpCost)
            {
                _battleUI?.ShowTip($"MP不足！需要{skill.mpCost}，当前{caster.CurrentMp}");
                _selectedSkillId = null;
                return;
            }

            _isAnimating = true;
            string log = _skillExecutor.Execute(skill, caster, targetCell);
            _battleUI?.ShowTip(log);
            Debug.Log(log);

            // 水攻后刷新地形颜色
            if (skill.type == SkillType.WaterAttack)
            {
                foreach (var c in targetCell.Range(skill.aoeRadius))
                {
                    if (_hexView.Grid.InBounds(c))
                        _hexView.RefreshCellColor(c);
                }
            }

            // 更新血条
            foreach (var kv in _unitVisuals)
                kv.Value.UpdateHpBar();

            _hexView.ClearHighlights();
            _selectedSkillId = null;
            _selectedUnit = null;
            _isAnimating = false;
            EndUnitAction();
        }

        /// <summary>OnGUI 计策选择面板 + 单挑按钮</summary>
        void OnGUI()
        {
            // 单挑面板优先
            if (_inDuel && _duelSystem != null)
            {
                DrawDuelPanel();
                return;
            }

            // 只在选中己方单位且未行动时显示
            if (_selectedUnit == null || _selectedUnit.HasActed
                || _selectedUnit.State != UnitState.Ready)
                return;

            float btnW = 90;
            float btnH = 40;
            float pad = 5;
            float startX = 10;
            float startY = Screen.height - 60;

            // 收集所有按钮：攻击 + 计策 + 单挑
            int btnCount = 1 + _selectedUnit.SkillIds.Count;

            // 检查是否有可单挑的相邻敌人
            var duelTarget = FindDuelTarget(_selectedUnit);
            bool canDuel = duelTarget != null;
            if (canDuel) btnCount++;

            // 绘制背景
            float totalW = btnCount * (btnW + pad) + pad;
            GUI.Box(new Rect(5, startY - 5, totalW + 10, btnH + 15), "");

            Color normalColor = GUI.backgroundColor;

            // "普通攻击" 按钮
            if (string.IsNullOrEmpty(_selectedSkillId) && !canDuel)
                GUI.backgroundColor = Color.green;
            if (GUI.Button(new Rect(startX, startY, btnW, btnH), "⚔ 攻击"))
            {
                _selectedSkillId = null;
                _battleUI?.ShowTip($"普通攻击模式");
            }
            GUI.backgroundColor = normalColor;

            // 计策按钮
            float x = startX + btnW + pad;
            foreach (var skillId in _selectedUnit.SkillIds)
            {
                var skill = SkillLibrary.Get(skillId);
                if (skill == null) continue;

                bool canUse = _selectedUnit.CurrentMp >= skill.mpCost;
                if (!canUse) GUI.enabled = false;

                if (_selectedSkillId == skillId)
                    GUI.backgroundColor = Color.cyan;
                else
                    GUI.backgroundColor = Color.white;

                string label = $"{skill.name}\n({skill.mpCost}MP)";
                if (GUI.Button(new Rect(x, startY, btnW, btnH), label))
                {
                    _selectedSkillId = (_selectedSkillId == skillId) ? null : skillId;
                    string mode = _selectedSkillId != null
                        ? $"选择{skill.name}目标" : "普通攻击模式";
                    _battleUI?.ShowTip(mode);
                }
                GUI.backgroundColor = normalColor;
                GUI.enabled = true;
                x += btnW + pad;
            }

            // 单挑按钮
            if (canDuel)
            {
                GUI.backgroundColor = new Color(1f, 0.4f, 0.1f);
                if (GUI.Button(new Rect(x, startY, btnW, btnH), $"⚔ 单挑\n{duelTarget.Name}"))
                {
                    StartDuel(_selectedUnit, duelTarget);
                }
                GUI.backgroundColor = normalColor;
            }
        }

        // ========== 单挑系统 ==========

        private BattleUnit FindDuelTarget(BattleUnit unit)
        {
            return _allUnits
                .Where(u => u.Faction != unit.Faction && u.IsAlive
                    && DuelSystem.CanDuel(unit, u))
                .FirstOrDefault();
        }

        private void StartDuel(BattleUnit player, BattleUnit enemy)
        {
            _duelSystem = new DuelSystem(player, enemy);
            _duelEnemy = enemy;
            _inDuel = true;
            DeselectUnit();
            _battleUI?.ShowTip($"{player.Name} 向 {enemy.Name} 发起单挑！");
        }

        private void DrawDuelPanel()
        {
            float w = 400;
            float h = 300;
            float x = (Screen.width - w) / 2;
            float y = (Screen.height - h) / 2;

            GUI.Box(new Rect(x, y, w, h), "单挑对决");

            float rowH = 30;
            float infoY = y + 40;

            // 双方信息
            GUI.Label(new Rect(x + 20, infoY, 160, rowH), $"{_duelSystem.Player.Name}");
            GUI.Label(new Rect(x + 220, infoY, 160, rowH), $"{_duelSystem.Enemy.Name}");

            infoY += rowH;
            GUI.Label(new Rect(x + 20, infoY, 160, rowH), $"HP: {_duelSystem.PlayerDuelHp}");
            GUI.Label(new Rect(x + 220, infoY, 160, rowH), $"HP: {_duelSystem.EnemyDuelHp}");

            infoY += rowH;
            GUI.Label(new Rect(x + 20, infoY, 160, rowH), $"必杀槽: {_duelSystem.PlayerSpecialGauge}");
            GUI.Label(new Rect(x + 220, infoY, 160, rowH), $"必杀槽: {_duelSystem.EnemySpecialGauge}");

            infoY += rowH + 10;
            GUI.Label(new Rect(x + 20, infoY, 360, rowH), $"第 {_duelSystem.Round + 1} / {_duelSystem.MaxRounds} 回合");

            // 操作按钮
            float btnY = y + h - 60;
            float btnW = 100;
            float btnH2 = 40;
            float btnX = x + (w - 330) / 2;

            if (GUI.Button(new Rect(btnX, btnY, btnW, btnH2), "攻击"))
            {
                ExecuteDuelRound(DuelAction.Attack);
            }
            btnX += 110;

            if (GUI.Button(new Rect(btnX, btnY, btnW, btnH2), "防御"))
            {
                ExecuteDuelRound(DuelAction.Defend);
            }
            btnX += 110;

            GUI.enabled = _duelSystem.PlayerSpecialGauge > 0;
            if (GUI.Button(new Rect(btnX, btnY, btnW, btnH2), "必杀"))
            {
                ExecuteDuelRound(DuelAction.Special);
            }
            GUI.enabled = true;
        }

        private void ExecuteDuelRound(DuelAction playerAction)
        {
            // 敌方随机选择
            var enemyAction = (DuelAction)UnityEngine.Random.Range(0, 3);
            _duelSystem.ExecuteRound(playerAction, enemyAction);

            if (_duelSystem.IsFinished)
            {
                string resultLog = _duelSystem.ApplyResult();
                _battleUI?.ShowTip(resultLog);
                Debug.Log(resultLog);

                // 更新血条和检查阵亡
                foreach (var kv in _unitVisuals)
                    kv.Value.UpdateHpBar();

                var deadUnits = _allUnits.Where(u => u.IsDead).ToList();
                foreach (var du in deadUnits)
                {
                    if (_unitVisuals.TryGetValue(du, out var deadVis))
                        StartCoroutine(DeathAnimation(deadVis, du));
                }

                _inDuel = false;
                _duelSystem = null;
                _duelEnemy = null;
                EndUnitAction();
            }
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

        // ========== AI行为树驱动 ==========

        private void EnemyAI(BattleUnit enemy)
        {
            if (!enemy.IsAlive)
            {
                _turnManager.EndUnitAction();
                return;
            }

            var action = _aiTree.Decide(enemy);
            Debug.Log($"AI {enemy.Name} 决策: {action.Reason}");

            switch (action.Type)
            {
                case AIActionType.Attack:
                    ExecuteAIAttack(enemy, action.TargetUnit);
                    break;

                case AIActionType.UseSkill:
                    ExecuteAISkill(enemy, action);
                    break;

                case AIActionType.Move:
                case AIActionType.Retreat:
                    enemy.Position = action.TargetCell;
                    enemy.HasMovedThisTurn = true;
                    if (_unitVisuals.TryGetValue(enemy, out var vis))
                        vis.UpdatePosition();
                    _battleUI?.ShowTip($"{enemy.Name} {action.Reason}");
                    break;

                case AIActionType.Skip:
                    break;
            }

            _turnManager.EndUnitAction();
        }

        private void ExecuteAIAttack(BattleUnit attacker, BattleUnit defender)
        {
            if (defender == null || defender.IsDead) return;

            var attackerTerrain = _hexView.Grid.GetTerrain(attacker.Position);
            var result = CombatCalculator.CalcPhysicalDamage(attacker, defender, 0, 0, attackerTerrain);
            defender.TakeDamage(result.damage);

            string log = $"AI {attacker.Name} 攻击 {defender.Name}：{(result.isHit ? $"造成{result.damage}伤害" : "未命中")}";
            if (result.isCrit) log += "【暴击】";
            Debug.Log(log);
            _battleUI?.ShowTip(log);

            if (_unitVisuals.TryGetValue(defender, out var defVis))
            {
                defVis.UpdateHpBar();
                StartCoroutine(FlashEffect(defVis));
            }

            if (defender.IsDead)
            {
                Debug.Log($"{defender.Name} 阵亡！");
                if (_unitVisuals.TryGetValue(defender, out var deadVis))
                    StartCoroutine(DeathAnimation(deadVis, defender));
            }
        }

        private void ExecuteAISkill(BattleUnit caster, AIAction action)
        {
            if (action.Skill == null) return;

            string log = _skillExecutor.Execute(action.Skill, caster, action.TargetCell);
            Debug.Log($"AI {caster.Name} 释放{action.Skill.name}：{log}");
            _battleUI?.ShowTip(log);

            // 水攻后刷新地形颜色
            if (action.Skill.type == SkillType.WaterAttack)
            {
                foreach (var c in action.TargetCell.Range(action.Skill.aoeRadius))
                {
                    if (_hexView.Grid.InBounds(c))
                        _hexView.RefreshCellColor(c);
                }
            }

            foreach (var kv in _unitVisuals)
                kv.Value.UpdateHpBar();

            // 检查阵亡
            var deadUnits = _allUnits.Where(u => u.IsDead).ToList();
            foreach (var du in deadUnits)
            {
                if (_unitVisuals.TryGetValue(du, out var deadVis))
                {
                    StartCoroutine(DeathAnimation(deadVis, du));
                }
            }
        }
    }
}
