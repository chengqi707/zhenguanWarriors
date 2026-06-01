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
    /// Sprint 3：支持战前编组装备 + 兵种特性
    /// </summary>
    public class BattleTestController : MonoBehaviour
    {
        // ========== 游戏阶段 ==========
        private enum GamePhase { PreBattle, Battle }
        private GamePhase _gamePhase = GamePhase.PreBattle;

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

        // ========== 战前编组 ==========
        private List<BattleUnit> _playerParty = new();
        private int _selectedPartyIndex;         // 当前选中的角色索引
        private bool _showEquipList;             // 是否显示装备选择列表
        private EquipmentType _editingSlot;      // 正在编辑的装备槽
        private string _hoverEquipId;            // 鼠标悬停的装备ID（显示详情）
        private Vector2 _partyScrollPos;
        private Vector2 _equipScrollPos;

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

            // 初始化玩家队伍（从角色数据库选取默认阵容）
            InitPlayerParty();
        }

        void Update()
        {
            if (_gamePhase != GamePhase.Battle) return;
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

        // ========== 战前编组 ==========

        /// <summary>初始化玩家队伍（从角色数据库加载）</summary>
        private void InitPlayerParty()
        {
            var db = CharacterDatabase.GetAll();
            // 默认出场阵容：李世民 + 李靖 + 长孙无忌 + 可选的其他角色
            string[] defaultRoster = { "lishimin", "li_jing", "zhangsun_wuji", "chai_shao",
                                       "liu_hongji", "yin_kaishan", "duan_zhixuan", "pingyang_princess" };

            foreach (var charId in defaultRoster)
            {
                if (db.TryGetValue(charId, out var charData))
                {
                    var unit = CharacterDatabase.CreateInstance(charId);
                    if (unit != null)
                    {
                        _playerParty.Add(unit);
                    }
                }
            }

            // 默认装备
            AutoEquipDefault();
            _selectedPartyIndex = 0;
        }

        /// <summary>自动装备默认装备</summary>
        private void AutoEquipDefault()
        {
            foreach (var unit in _playerParty)
            {
                // 根据兵种自动配装
                var cls = unit.UnitClass;
                // 武器默认
                string weaponId = cls switch
                {
                    ClassType.Cavalry => "w003",        // 马槊
                    ClassType.Archer => "w004",          // 长弓
                    ClassType.Siege => "w005",            // 攻城锤
                    ClassType.Strategist => "w006",       // 羽扇
                    ClassType.HeavyInfantry => "w002",    // 铁枪
                    _ => "w001"                            // 环首刀
                };
                unit.Equip(weaponId);
                // 防具
                unit.Equip(cls == ClassType.Strategist || cls == ClassType.Archer ? "a003" : "a001");
            }
        }

        // ========== 战前编组 UI ==========

        private void DrawPreBattleUI()
        {
            // 全屏半透明背景
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");

            // 标题
            GUI.Label(new Rect(Screen.width / 2 - 80, 10, 200, 30),
                "⚔ 贞观勇士 · 战前编组", new GUIStyle { fontSize = 20, normal = { textColor = Color.white } });

            // ---- 左面板：角色列表 ----
            float leftW = 260;
            float rightW = Screen.width - leftW - 30;
            float panelY = 50;
            float panelH = Screen.height - 120;

            GUI.Box(new Rect(10, panelY, leftW, panelH), "出阵武将");

            _partyScrollPos = GUI.BeginScrollView(
                new Rect(15, panelY + 25, leftW - 10, panelH - 35),
                _partyScrollPos,
                new Rect(0, 0, leftW - 30, _playerParty.Count * 70));

            for (int i = 0; i < _playerParty.Count; i++)
            {
                var unit = _playerParty[i];
                float itemY = i * 70;
                bool isSelected = i == _selectedPartyIndex;

                // 背景
                GUI.backgroundColor = isSelected ? new Color(0.3f, 0.5f, 0.8f) : new Color(0.2f, 0.2f, 0.2f);
                GUI.Box(new Rect(0, itemY, leftW - 30, 65), "");

                // 名字 + 等级 + 兵种
                string clsName = ClassData.GetName(unit.UnitClass);
                string traitDesc = ClassData.GetTraitDescription(unit.UnitClass);
                GUI.Label(new Rect(5, itemY + 2, 200, 20),
                    $"{unit.Name} Lv.{unit.Level} {clsName}");
                GUI.Label(new Rect(5, itemY + 22, 200, 15),
                    $"武{unit.BaseStrength} 统{unit.BaseCommand} 智{unit.BaseIntelligence}",
                    new GUIStyle { fontSize = 10, normal = { textColor = Color.gray } });
                GUI.Label(new Rect(5, itemY + 37, 220, 25),
                    $"敏{unit.BaseAgility} 运{unit.BaseLuck}  HP{unit.MaxHp} MP{unit.MaxMp}",
                    new GUIStyle { fontSize = 10, normal = { textColor = Color.gray } });

                // 点击选择
                if (GUI.Button(new Rect(0, itemY, leftW - 30, 65), "", GUIStyle.none))
                {
                    _selectedPartyIndex = i;
                    _showEquipList = false;
                }
            }
            GUI.EndScrollView();

            // ---- 右面板：当前选中角色的装备 ----
            GUI.Box(new Rect(leftW + 20, panelY, Screen.width - leftW - 30, panelH), "装备调整");

            if (_selectedPartyIndex >= 0 && _selectedPartyIndex < _playerParty.Count)
            {
                var unit = _playerParty[_selectedPartyIndex];
                float rightX = leftW + 30;
                float rightInnerW = Screen.width - leftW - 50;
                float infoY = panelY + 30;

                // 角色信息
                string traitInfo = ClassData.GetTraitDescription(unit.UnitClass);
                GUI.Label(new Rect(rightX, infoY, rightInnerW, 25),
                    $"选择: {unit.Name}  |  等级 {unit.Level}  |  兵种: {ClassData.GetName(unit.UnitClass)}");
                GUI.Label(new Rect(rightX, infoY + 22, rightInnerW, 20),
                    $"五维: 武{unit.Strength}(+{unit.Strength - unit.BaseStrength}) " +
                    $"统{unit.Command}(+{unit.Command - unit.BaseCommand}) " +
                    $"智{unit.Intelligence} 敏{unit.Agility} 运{unit.Luck}",
                    new GUIStyle { fontSize = 11, normal = { textColor = Color.yellow } });
                if (!string.IsNullOrEmpty(traitInfo))
                {
                    GUI.Label(new Rect(rightX, infoY + 42, rightInnerW, 18),
                        $"特性: {traitInfo}",
                        new GUIStyle { fontSize = 10, normal = { textColor = Color.cyan } });
                }

                // 当前装备显示 + 编辑按钮
                float equipY = infoY + 65;
                DrawEquipSlot(rightX, equipY, rightInnerW, unit, EquipmentType.Weapon);
                DrawEquipSlot(rightX, equipY + 35, rightInnerW, unit, EquipmentType.Armor);
                DrawEquipSlot(rightX, equipY + 70, rightInnerW, unit, EquipmentType.Trinket);

                // 装备选择列表（点击某槽后展开）
                if (_showEquipList)
                {
                    DrawEquipSelectionList(rightX, equipY + 115, rightInnerW, unit);
                }
                else
                {
                    // 显示最终属性
                    GUI.Label(new Rect(rightX, equipY + 115, rightInnerW, 20),
                        $"最终属性 → 攻击范围: {unit.AttackRange}  移动力: {unit.MoveRange}",
                        new GUIStyle { fontSize = 11, normal = { textColor = Color.green } });
                }
            }

            // ---- 底部按钮 ----
            float btnY = Screen.height - 55;
            if (GUI.Button(new Rect(Screen.width / 2 - 120, btnY, 240, 40),
                "⚔ 开始战斗（敌方将迎战）"))
            {
                StartBattle();
            }
        }

        /// <summary>绘制单个装备槽</summary>
        private void DrawEquipSlot(float x, float y, float w, BattleUnit unit, EquipmentType slot)
        {
            string slotName = slot switch
            {
                EquipmentType.Weapon => "武器",
                EquipmentType.Armor => "防具",
                EquipmentType.Trinket => "饰品",
                _ => "未知"
            };

            // 获取当前装备
            string equipId = slot switch
            {
                EquipmentType.Weapon => unit.WeaponId,
                EquipmentType.Armor => unit.ArmorId,
                EquipmentType.Trinket => unit.TrinketId,
                _ => null
            };
            var equip = string.IsNullOrEmpty(equipId) ? null : EquipmentLibrary.Get(equipId);
            string equipName = equip?.name ?? "[空]";
            string statText = equip != null ? FormatEquipStats(equip) : "";

            bool isEditing = _showEquipList && _editingSlot == slot;

            GUI.backgroundColor = isEditing ? new Color(0.4f, 0.7f, 0.4f) : Color.gray;
            GUI.Box(new Rect(x, y, w, 30), $"{slotName}: {equipName}  {statText}");

            // 点击该槽→打开装备选择
            if (GUI.Button(new Rect(x, y, w - 60, 30), "", GUIStyle.none))
            {
                _editingSlot = slot;
                _showEquipList = true;
                _equipScrollPos = Vector2.zero;
            }

            // 卸下按钮（仅当有装备时）
            if (equip != null)
            {
                if (GUI.Button(new Rect(x + w - 55, y + 2, 50, 26), "卸下"))
                {
                    unit.Unequip(slot);
                    _showEquipList = false;
                }
            }
        }

        /// <summary>格式化装备属性文本</summary>
        private string FormatEquipStats(EquipmentData equip)
        {
            var parts = new List<string>();
            if (equip.strBonus != 0) parts.Add($"武+{equip.strBonus}");
            if (equip.cmdBonus != 0) parts.Add($"统+{equip.cmdBonus}");
            if (equip.intBonus != 0) parts.Add($"智+{equip.intBonus}");
            if (equip.agiBonus != 0) parts.Add($"敏+{equip.agiBonus}");
            if (equip.lukBonus != 0) parts.Add($"运+{equip.lukBonus}");
            if (equip.hpBonus != 0) parts.Add($"HP+{equip.hpBonus}");
            if (equip.mpBonus != 0) parts.Add($"MP+{equip.mpBonus}");
            if (equip.moveBonus != 0) parts.Add($"移+{equip.moveBonus}");
            if (equip.attackRangeBonus != 0) parts.Add($"射+{equip.attackRangeBonus}");
            if (equip.strPercent != 0) parts.Add($"武%+{equip.strPercent}");
            if (equip.cmdPercent != 0) parts.Add($"统%+{equip.cmdPercent}");
            if (equip.intPercent != 0) parts.Add($"智%+{equip.intPercent}");
            return parts.Count > 0 ? "(" + string.Join(" ", parts) + ")" : "";
        }

        /// <summary>绘制可选择的装备列表</summary>
        private void DrawEquipSelectionList(float x, float y, float w, BattleUnit unit)
        {
            GUI.Box(new Rect(x, y, w, 200), $"选择{(_editingSlot == EquipmentType.Weapon ? "武器" :
                _editingSlot == EquipmentType.Armor ? "防具" : "饰品")}");

            // 获取该槽位所有可用装备
            var allEquips = EquipmentLibrary.GetAll().Values
                .Where(e => e.type == _editingSlot && e.CanEquip(unit))
                .OrderBy(e => e.rarity)
                .ThenBy(e => e.name)
                .ToList();

            if (allEquips.Count == 0)
            {
                GUI.Label(new Rect(x + 10, y + 25, w - 20, 20), "没有可用装备");
                return;
            }

            float listH = 170;
            _equipScrollPos = GUI.BeginScrollView(
                new Rect(x + 5, y + 25, w - 15, listH),
                _equipScrollPos,
                new Rect(0, 0, w - 35, allEquips.Count * 28));

            for (int i = 0; i < allEquips.Count; i++)
            {
                var e = allEquips[i];
                float itemY = i * 28;

                string rarityColor = e.GetRarityColor();
                string equipped = IsEquipped(unit, e.id) ? " ★" : "";
                GUI.Label(new Rect(5, itemY, w - 50, 28),
                    $"<color={rarityColor}>{e.name}</color>{equipped}   {FormatEquipStats(e)}");

                // 点击装备
                if (GUI.Button(new Rect(5, itemY, w - 50, 28), "", GUIStyle.none))
                {
                    unit.Equip(e.id);
                    _showEquipList = false;
                }

                // 鼠标悬停显示详情
                string hoverId = GUI.tooltip;
                _hoverEquipId = hoverId;
            }
            GUI.EndScrollView();

            // 关闭按钮
            if (GUI.Button(new Rect(x + w - 80, y + 2, 70, 20), "关闭"))
            {
                _showEquipList = false;
            }
        }

        /// <summary>检查某装备是否已被某单位装备</summary>
        private bool IsEquipped(BattleUnit unit, string equipId)
        {
            return unit.WeaponId == equipId || unit.ArmorId == equipId || unit.TrinketId == equipId;
        }

        // ========== 开始战斗 ==========

        /// <summary>从战前编组过渡到战斗</summary>
        private void StartBattle()
        {
            _gamePhase = GamePhase.Battle;
            _showEquipList = false;

            // 将玩家队伍设置为战斗单位
            _allUnits.Clear();
            _allUnits.AddRange(_playerParty);

            // 敌方单位
            CreateEnemyUnits();

            // 设置位置
            float startX = 1;
            float startY = 3;
            for (int i = 0; i < _playerParty.Count; i++)
            {
                _playerParty[i].Position = new HexCoord((int)(startX + i % 2), (int)(startY + i / 2));
                _playerParty[i].NewTurn();
            }

            // 天气系统
            _weather = new WeatherSystem(WeatherType.Sunny, WindDirection.None);
            _skillExecutor = new SkillExecutor(_allUnits, _hexView.Grid, _weather);
            _aiTree = new AIBehaviorTree(_allUnits, _hexView.Grid, _weather, _skillExecutor, "normal");

            // 创建可视化
            foreach (var unit in _allUnits)
            {
                var visual = UnitVisual.Create(unit, _hexView);
                _unitVisuals[unit] = visual;
            }

            // 回合管理器
            _turnManager = new TurnManager(_allUnits);
            _turnManager.OnUnitTurnStart += OnUnitTurnStart;
            _turnManager.OnUnitTurnEnd += OnUnitTurnEnd;
            _turnManager.OnPhaseChanged += OnPhaseChanged;

            _battleUI.ShowTip("选中己方单位 → 点击移动/攻击 | 底部选择计策");
            _turnManager.StartBattle();
        }

        /// <summary>创建敌方单位</summary>
        private void CreateEnemyUnits()
        {
            // 敌方——校尉
            var enemy1 = new BattleUnit("enemy_1", "刘校尉", Faction.Enemy, ClassType.Infantry,
                str: 60, cmd: 50, @int: 30, agi: 40, luk: 30,
                hp: 60, mp: 10, move: 4, attackRange: 1,
                gender: Gender.Male)
            {
                Position = new HexCoord(8, 3),
                StrGrowth = 2, CmdGrowth = 2, IntGrowth = 1, AgiGrowth = 2, LukGrowth = 1
            };
            _allUnits.Add(enemy1);

            // 敌方——重步
            var enemy2 = new BattleUnit("enemy_2", "张步兵", Faction.Enemy, ClassType.HeavyInfantry,
                str: 55, cmd: 60, @int: 20, agi: 35, luk: 25,
                hp: 80, mp: 0, move: 3, attackRange: 1,
                gender: Gender.Male)
            {
                Position = new HexCoord(9, 5),
                StrGrowth = 2, CmdGrowth = 3, IntGrowth = 1, AgiGrowth = 1, LukGrowth = 1
            };
            _allUnits.Add(enemy2);

            // 敌方——弓兵
            var enemy3 = new BattleUnit("enemy_3", "王弓兵", Faction.Enemy, ClassType.Archer,
                str: 45, cmd: 35, @int: 25, agi: 45, luk: 30,
                hp: 45, mp: 10, move: 4, attackRange: 2,
                gender: Gender.Male)
            {
                Position = new HexCoord(10, 2),
                SkillIds = new List<string> { "volley" },
                StrGrowth = 2, CmdGrowth = 1, IntGrowth = 2, AgiGrowth = 3, LukGrowth = 2
            };
            _allUnits.Add(enemy3);
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
                hp: 120, mp: 50, move: 5, attackRange: 1,
                gender: Gender.Male)
            {
                Position = new HexCoord(1, 3),
                SkillIds = new List<string> { "rally" },
                StrGrowth = 4, CmdGrowth = 5, IntGrowth = 4, AgiGrowth = 3, LukGrowth = 4
            };
            liShiMin.Equip("w011"); // 秦王剑
            liShiMin.Equip("a009"); // 金鳞甲
            _allUnits.Add(liShiMin);

            // 玩家方——李靖（统帅 + 火攻 + 水攻）
            var liJing = new BattleUnit("li_jing", "李靖", Faction.Player, ClassType.Cavalry,
                str: 90, cmd: 85, @int: 75, agi: 70, luk: 75,
                hp: 100, mp: 50, move: 6, attackRange: 1,
                gender: Gender.Male)
            {
                Position = new HexCoord(2, 5),
                SkillIds = new List<string> { "fire_attack", "water_attack" },
                StrGrowth = 5, CmdGrowth = 4, IntGrowth = 3, AgiGrowth = 3, LukGrowth = 3
            };
            liJing.Equip("w009"); // 饮血刀
            liJing.Equip("a006"); // 锁子甲
            _allUnits.Add(liJing);

            // 敌方——校尉
            var enemy1 = new BattleUnit("enemy_1", "刘校尉", Faction.Enemy, ClassType.Infantry,
                str: 60, cmd: 50, @int: 30, agi: 40, luk: 30,
                hp: 60, mp: 10, move: 4, attackRange: 1,
                gender: Gender.Male)
            {
                Position = new HexCoord(8, 3)
            };
            _allUnits.Add(enemy1);

            // 敌方——步兵
            var enemy2 = new BattleUnit("enemy_2", "张步兵", Faction.Enemy, ClassType.HeavyInfantry,
                str: 55, cmd: 60, @int: 20, agi: 35, luk: 25,
                hp: 80, mp: 0, move: 3, attackRange: 1,
                gender: Gender.Male)
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
            // 装备信息
            string equipInfo = "";
            if (!string.IsNullOrEmpty(unit.WeaponId)) equipInfo += $" 武:{EquipmentLibrary.Get(unit.WeaponId)?.name}";
            if (!string.IsNullOrEmpty(unit.ArmorId)) equipInfo += $" 防:{EquipmentLibrary.Get(unit.ArmorId)?.name}";
            if (!string.IsNullOrEmpty(unit.TrinketId)) equipInfo += $" 饰:{EquipmentLibrary.Get(unit.TrinketId)?.name}";

            _battleUI?.ShowTip($"选中 Lv{unit.Level} {unit.Name} [HP:{unit.CurrentHp}/{unit.MaxHp} MP:{unit.CurrentMp}] 武{unit.Strength}统{unit.Command}智{unit.Intelligence}敏{unit.Agility}运{unit.Luck}{equipInfo}{skillInfo}");
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

            // ★ 器械AOE溅射
            if (result.isHit && result.damage > 0)
            {
                ApplySiegeSplash(attacker, defender.Position, result.damage);
            }

            // 检查阵亡
            if (defender.IsDead)
            {
                Debug.Log($"{defender.Name} 阵亡！");
                if (_unitVisuals.TryGetValue(defender, out var deadVis))
                {
                    StartCoroutine(DeathAnimation(deadVis, defender));
                }

                // 经验分配
                int exp = CombatCalculator.CalcExp(attacker.Level - defender.Level, true, false);
                bool leveled = attacker.GainExperience(exp);
                string expLog = $"{attacker.Name} 获得 {exp} 点经验";
                if (leveled)
                {
                    expLog += $" 🎉 升级至 Lv{attacker.Level}！";
                    var newSkills = attacker.GetLearnableSkills();
                    if (newSkills.Count > 0)
                    {
                        foreach (var sid in newSkills)
                        {
                            if (!attacker.SkillIds.Contains(sid))
                            {
                                attacker.SkillIds.Add(sid);
                                var skill = SkillLibrary.Get(sid);
                                expLog += $" 习得【{skill?.name ?? sid}】！";
                            }
                        }
                    }
                }
                _battleUI?.ShowTip(expLog);
                Debug.Log(expLog);
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

        /// <summary>OnGUI 计策选择面板 + 单挑按钮 + 战前编组</summary>
        void OnGUI()
        {
            // 战前编组UI
            if (_gamePhase == GamePhase.PreBattle)
            {
                DrawPreBattleUI();
                return;
            }

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

        // ========== 兵种特性：器械AOE溅射 ==========

        /// <summary>器械攻击后对目标周围1格造成溅射伤害</summary>
        private void ApplySiegeSplash(BattleUnit attacker, HexCoord targetCell, int mainDamage)
        {
            if (attacker.UnitClass != ClassType.Siege) return;
            if (mainDamage <= 0) return;

            foreach (var neighbor in targetCell.Neighbors())
            {
                var splashTarget = _turnManager?.GetUnitAt(neighbor);
                if (splashTarget == null || !splashTarget.IsAlive) continue;
                if (splashTarget.Faction == attacker.Faction) continue; // 不伤友军

                int splashDmg = CombatCalculator.CalcSplashDamage(mainDamage);
                splashTarget.TakeDamage(splashDmg);

                string splashLog = $"{attacker.Name} 的【破阵】溅射 {splashTarget.Name}，造成 {splashDmg} 点伤害！";
                Debug.Log(splashLog);
                _battleUI?.ShowTip(splashLog);

                if (_unitVisuals.TryGetValue(splashTarget, out var vis))
                {
                    vis.UpdateHpBar();
                    StartCoroutine(FlashEffect(vis));
                }

                // 检查溅射阵亡
                if (splashTarget.IsDead && _unitVisuals.TryGetValue(splashTarget, out var deadVis))
                {
                    StartCoroutine(DeathAnimation(deadVis, splashTarget));
                    // 溅射击杀也给经验
                    int exp = CombatCalculator.CalcExp(attacker.Level - splashTarget.Level, true, false);
                    attacker.GainExperience(exp);
                }
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

            // ★ 器械AOE溅射（AI方也用）
            if (result.isHit && result.damage > 0)
            {
                ApplySiegeSplash(attacker, defender.Position, result.damage);
            }

            if (defender.IsDead)
            {
                Debug.Log($"{defender.Name} 阵亡！");
                if (_unitVisuals.TryGetValue(defender, out var deadVis))
                    StartCoroutine(DeathAnimation(deadVis, defender));

                // AI 经验分配
                int exp = CombatCalculator.CalcExp(attacker.Level - defender.Level, true, false);
                bool leveled = attacker.GainExperience(exp);
                string expLog = $"{attacker.Name} 获得 {exp} 点经验";
                if (leveled)
                {
                    expLog += $" 🎉 升级至 Lv{attacker.Level}！";
                    var newSkills = attacker.GetLearnableSkills();
                    foreach (var sid in newSkills)
                    {
                        if (!attacker.SkillIds.Contains(sid))
                        {
                            attacker.SkillIds.Add(sid);
                            var skill = SkillLibrary.Get(sid);
                            expLog += $" 习得【{skill?.name ?? sid}】！";
                        }
                    }
                }
                Debug.Log(expLog);
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
