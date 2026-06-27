using UnityEngine;
using ZhenguanWarriors.Core.AI;
using ZhenguanWarriors.Core.Audio;
using ZhenguanWarriors.Core.Battle;
using ZhenguanWarriors.Core.Character;
using ZhenguanWarriors.Core.Combat;
using ZhenguanWarriors.Core.Level;
using ZhenguanWarriors.Core.Save;
using ZhenguanWarriors.Core.Story;
using ZhenguanWarriors.Core.UI;
using ZhenguanWarriors.Utils;
using System.Collections;
using System.Collections.Generic;
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
        private enum GamePhase { LevelSelect, HeroSelect, EquipSetup, Battle, Results }
        private GamePhase _gamePhase = GamePhase.LevelSelect;
        private bool _heroConfirmClicked;  // 选人阶段已确认

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

        // ========== 关卡系统 ==========
        private LevelData _currentLevel;
        public LevelData CurrentLevel => _currentLevel;
        public int CurrentTurn => _turnManager?.TurnNumber ?? 0;
        public List<string> GetLevelOrder() => _levelOrder;
        private VictoryChecker _victoryChecker;
        private List<string> _levelOrder = new() {
            "level_01", "level_02", "level_03", "level_04",
            "level_05", "level_06", "level_07", "level_08"
        };
        private int _currentLevelIndex;
        private Vector2 _levelSelectScroll;

        // ========== 剧情系统 ==========
        private DialogueUI _dialogueUI;
        private bool _waitingForDialogue;   // 等待对话结束后继续流程

        // ========== UI缩放 ==========
        private float _uiScale = 1f;
        private float SW => Screen.width;
        private float SH => Screen.height;

        // ========== 结算界面 ==========
        private string _resultsTitle;
        private string _resultsMessage;
        private List<string> _resultsLog = new();
        private Vector2 _resultsScroll;

        void Start()
        {
            _hexView = GetComponent<HexGridView>();
            _battleUI = GetComponent<BattleUI>();
            _dialogueUI = GetComponent<DialogueUI>();

            // 防御性校验：核心组件缺失时禁用自身，避免后续空引用崩溃
            if (_hexView == null)
            {
                GameLogger.LogError(LogCategory.Battle, "缺少 HexGridView 组件，战斗控制器将禁用。");
                enabled = false;
                return;
            }
            if (_battleUI == null)
            {
                GameLogger.LogError(LogCategory.Battle, "缺少 BattleUI 组件，战斗控制器将禁用。");
                enabled = false;
                return;
            }
            if (_dialogueUI == null)
            {
                GameLogger.LogWarning(LogCategory.Battle, "缺少 DialogueUI 组件，剧情功能不可用。");
            }

            // DPI自适应缩放（竖屏基准：1080x1920）
            float wScale = SW / 1920f;
            float hScale = SH / 1080f;
            _uiScale = Mathf.Min(wScale, hScale);
            if (_uiScale < 0.6f) _uiScale = 0.6f;
            if (_uiScale > 2.5f) _uiScale = 2.5f;
            GameLogger.LogInfoFormat(LogCategory.UI, "战斗UI缩放|SW={0}|SH={1}|wScale={2:F2}|hScale={3:F2}|scale={4:F2}", SW, SH, wScale, hScale, _uiScale);

            GameLogger.LogInfoFormat(LogCategory.Battle, "战斗控制器启动|日志文件={0}", GameLogger.LogFilePath);
        }

        /// <summary>GameManager 调用，设置当前页面</summary>
        public void SetPage(GamePage page)
        {
            switch (page)
            {
                case GamePage.HeroSelect:
                    _gamePhase = GamePhase.HeroSelect;
                    _heroConfirmClicked = false;
                    break;
                case GamePage.EquipSetup:
                    _gamePhase = GamePhase.EquipSetup;
                    break;
                case GamePage.LevelSelect:
                case GamePage.Battle:
                    _gamePhase = GamePhase.Battle;
                    break;
                case GamePage.Results:
                    _gamePhase = GamePhase.Results;
                    break;
            }
        }

        void Update()
        {
            // 暂停中不处理战斗输入
            if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

            // 等待对话时阻止所有输入
            if (_waitingForDialogue) return;

            // 仅战斗阶段处理输入
            if (_gamePhase != GamePhase.Battle) return;
            if (_isAnimating) return;
            // 网格未就绪时也跳过
            if (_hexView == null || _hexView.Grid == null) return;

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

        /// <summary>初始化玩家队伍（从角色数据库 + 关卡可用角色加载，优先从存档恢复）</summary>
        private void InitPlayerParty()
        {
            // 已由 ConfirmHeroSelection() 填充
            // 这里只做存档恢复检查
            if (_playerParty.Count == 0 && GameState.CurrentSave?.characters != null)
            {
                foreach (var saved in GameState.CurrentSave.characters)
                {
                    var unit = RestoreCharacterFromSave(saved);
                    if (unit != null && _heroPool.Any(h => h.Id == unit.Id))
                        _playerParty.Add(unit);
                }
            }
            AutoEquipDefault();
            _selectedPartyIndex = 0;
        }

        /// <summary>从存档查找角色</summary>
        private CharacterSaveData FindSavedCharacter(string charId)
        {
            if (GameState.CurrentSave?.characters == null) return null;
            return GameState.CurrentSave.characters.FirstOrDefault(c => c.id == charId);
        }

        /// <summary>从存档恢复角色状态</summary>
        private BattleUnit RestoreCharacterFromSave(CharacterSaveData saved)
        {
            var charData = CharacterDatabase.Get(saved.id);
            if (charData == null) return null;

            var unit = new BattleUnit(
                saved.id, saved.name, Faction.Player, charData.UnitClass,
                saved.baseStr, saved.baseCmd, saved.baseInt, saved.baseAgi, saved.baseLuk,
                charData.MaxHp, charData.MaxMp,
                charData.BaseMoveRange, charData.BaseAttackRange,
                charData.Gender)
            {
                Level = saved.level,
                Experience = saved.experience,
                StrGrowth = saved.strGrowth,
                CmdGrowth = saved.cmdGrowth,
                IntGrowth = saved.intGrowth,
                AgiGrowth = saved.agiGrowth,
                LukGrowth = saved.lukGrowth,
                SkillIds = saved.skillIds != null ? new List<string>(saved.skillIds) : new List<string>(),
                PassiveIds = PassiveSkillLibrary.GetCharacterPassives(saved.id)
            };

            // 恢复装备
            if (!string.IsNullOrEmpty(saved.weaponId)) unit.Equip(saved.weaponId);
            if (!string.IsNullOrEmpty(saved.armorId)) unit.Equip(saved.armorId);
            if (!string.IsNullOrEmpty(saved.trinketId)) unit.Equip(saved.trinketId);

            return unit;
        }

        /// <summary>自动装备默认装备</summary>
        private void AutoEquipDefault()
        {
            foreach (var unit in _playerParty)
            {
                // 已有装备的不覆盖（从存档恢复的保持不变）
                if (!string.IsNullOrEmpty(unit.WeaponId)) continue;

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

        // ========== 选人界面 (HeroSelect) ==========

        private void DrawHeroSelectUI()
        {
            float s = _uiScale;
            int selected = _heroSelected.Count(v => v);

            // 背景
            GUI.backgroundColor = Theme.BgDark;
            GUI.Box(Theme.ClampToScreen(new Rect(0, 0, SW, SH)), "");
            GUI.backgroundColor = Color.white;

            // 顶部装饰
            GUI.backgroundColor = Theme.Primary;
            GUI.Box(new Rect(0, 0, SW, 8 * s), "");
            GUI.backgroundColor = Color.white;

            // 标题
            Theme.DrawTitle(new Rect(0, 20 * s, SW, 50 * s), $"👥 选择出战武将   {selected}/8", (int)(36 * s));

            // 角色卡片列表（带粗滚动条，支持触控滑动）
            float cardH = 165 * s;
            float gap = 12 * s;
            float startY = 80 * s;
            float svH = SH - startY - 145 * s; // 滚动区域高度
            float contentH = _heroPool.Count * (cardH + gap);

            // 自定义垂直滚动条样式（加宽到30px，方便触控）
            GUIStyle vsBar = new GUIStyle(GUI.skin.verticalScrollbar);
            vsBar.fixedWidth = 30 * s;
            GUIStyle vsThumb = new GUIStyle(GUI.skin.verticalScrollbarThumb);
            vsThumb.fixedWidth = 30 * s;

            _partyScrollPos = GUI.BeginScrollView(
                new Rect(20 * s, startY, SW - 40 * s, svH),
                _partyScrollPos,
                new Rect(0, 0, SW - 60 * s, contentH),
                false, true, GUIStyle.none, vsBar);

            for (int i = 0; i < _heroPool.Count; i++)
            {
                var unit = _heroPool[i];
                bool isRequired = _currentLevel.requiredCharacters.Contains(unit.Id);
                bool isChecked = _heroSelected[i];
                bool atMax = selected >= 8 && !isChecked;
                bool canToggle = !isRequired && !atMax;
                float iy = i * (cardH + gap);
                float cardW = SW - 65 * s; // 减滚动条宽度

                // 卡片背景
                Color bg = isChecked ? new Color(0.22f, 0.28f, 0.38f) : new Color(0.14f, 0.10f, 0.08f);
                if (isRequired) bg = new Color(0.28f, 0.22f, 0.15f);
                GUI.backgroundColor = bg;
                GUI.Box(new Rect(0, iy, cardW, cardH), "");

                // 兵种色条
                GUI.backgroundColor = GetClassColor(unit.UnitClass);
                GUI.Box(new Rect(0, iy, 8 * s, cardH), "");
                GUI.backgroundColor = Color.white;

                // 角色名
                GUI.Label(new Rect(24 * s, iy + 6 * s, cardW - 100 * s, 52 * s),
                    unit.Name,
                    Theme.MakeLabel((int)(42 * s), FontStyle.Bold, isChecked ? Theme.TextLight : Theme.TextDim,
                                    TextAnchor.MiddleLeft));

                // 等级 + 兵种
                GUI.Label(new Rect(24 * s, iy + 62 * s, cardW - 100 * s, 36 * s),
                    $"Lv.{unit.Level}  {ClassData.GetName(unit.UnitClass)}",
                    Theme.MakeLabel((int)(28 * s), FontStyle.Normal, Theme.TextDim, TextAnchor.MiddleLeft));

                // 五维
                GUI.Label(new Rect(24 * s, iy + 104 * s, cardW - 100 * s, 36 * s),
                    $"武{unit.BaseStrength} 统{unit.BaseCommand} 智{unit.BaseIntelligence} 敏{unit.BaseAgility} 运{unit.BaseLuck}",
                    Theme.MakeLabel((int)(28 * s), FontStyle.Normal, Theme.TextDim, TextAnchor.MiddleLeft));

                // 勾选框
                float cbSize = 48 * s;
                float cbX = cardW - cbSize - 10 * s;
                float cbY = iy + (cardH - cbSize) / 2;
                GUIStyle cbStyle = new GUIStyle { fontSize = (int)(48 * s), alignment = TextAnchor.MiddleCenter };

                if (isRequired)
                {
                    cbStyle.normal.textColor = Color.green;
                    GUI.Label(new Rect(cbX, cbY, cbSize, cbSize), "✅", cbStyle);
                }
                else if (!canToggle)
                {
                    cbStyle.normal.textColor = Theme.TextDim;
                    GUI.Label(new Rect(cbX, cbY, cbSize, cbSize), atMax ? "✖" : "☐", cbStyle);
                }
                else
                {
                    string mark = isChecked ? "✅" : "☐";
                    cbStyle.normal.textColor = Color.white;
                    if (GUI.Button(new Rect(cbX - 10 * s, cbY - 10 * s, cbSize + 20 * s, cbSize + 20 * s), mark, cbStyle))
                        _heroSelected[i] = !isChecked;
                    // 点击整行也可切换
                    if (GUI.Button(new Rect(0, iy, SW - 40 * s, cardH), "", GUIStyle.none))
                        _heroSelected[i] = !isChecked;
                }
            }
            GUI.EndScrollView();

            // ---- 羁绊实时预览 ----
            var previewParty = _heroPool.Where((_, idx) => _heroSelected[idx]).ToList();
            var previewBonds = BondSystem.CheckBonds(previewParty);
            float bondY = SH - 115 * s;
            if (previewBonds.Count > 0)
            {
                GUI.Label(new Rect(24 * s, bondY, SW - 48 * s, 24 * s),
                    "✦ 羁绊已激活:", Theme.MakeLabel((int)(22 * s), FontStyle.Bold, Theme.Gold));
                for (int bi = 0; bi < previewBonds.Count; bi++)
                {
                    var b = previewBonds[bi];
                    var names = b.characterIds.Select(id =>
                        previewParty.FirstOrDefault(u => u.Id == id)?.Name ?? id).ToList();
                    GUI.Label(new Rect(30 * s, bondY + 28 * s + bi * 22 * s, SW - 60 * s, 22 * s),
                        $"✅ {b.name} ({string.Join("+", names)})",
                        Theme.MakeLabel((int)(20 * s), FontStyle.Normal, Color.yellow));
                }
            }

            // ---- 底部按钮 ----
            float btnY2 = SH - 65 * s;
            GUI.backgroundColor = Theme.BgCard;
            if (GUI.Button(new Rect(24 * s, btnY2, 200 * s, 55 * s),
                "← 返回选关", Theme.MakeButton((int)(22 * s))))
            {
                AudioManager.PlaySfx(AudioManager.SfxClips.Click);
                GameManager.Instance.TransitionTo(GamePage.LevelSelect);
            }

            bool canConfirm = selected >= 1;
            GUI.enabled = canConfirm;
            GUI.backgroundColor = Theme.Primary;
            if (GUI.Button(new Rect(SW - 220 * s, btnY2, 200 * s, 55 * s),
                "确认阵容 →", Theme.MakeButton((int)(22 * s))))
            {
                AudioManager.PlaySfx(AudioManager.SfxClips.Click);
                ConfirmHeroSelection();
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
        }

        /// <summary>确认选人，进入装备调整</summary>
        private void ConfirmHeroSelection()
        {
            _playerParty.Clear();
            for (int i = 0; i < _heroPool.Count; i++)
            {
                if (_heroSelected[i])
                    _playerParty.Add(_heroPool[i]);
            }
            _selectedPartyIndex = 0;
            _showEquipList = false;
            GameManager.Instance.TransitionTo(GamePage.EquipSetup);
        }

        private Color GetClassColor(ClassType cls) => cls switch
        {
            ClassType.Cavalry => Theme.ClassCavalry,
            ClassType.Archer => Theme.ClassArcher,
            ClassType.Siege => Theme.ClassSiege,
            ClassType.Strategist => Theme.ClassStrategist,
            ClassType.HeavyInfantry => Theme.ClassHeavyInf,
            _ => Theme.ClassInfantry
        };

        // ========== 装备调整界面 (EquipSetup) ==========

        private void DrawEquipSetupUI()
        {
            float s = _uiScale;

            // 背景
            GUI.backgroundColor = Theme.BgDark;
            GUI.Box(Theme.ClampToScreen(new Rect(0, 0, SW, SH)), "");

            GUI.backgroundColor = Theme.Primary;
            GUI.Box(new Rect(0, 0, SW, 6 * s), "");

            var unit = _selectedPartyIndex >= 0 && _selectedPartyIndex < _playerParty.Count
                ? _playerParty[_selectedPartyIndex] : null;

            // 标题
            Theme.DrawTitle(new Rect(0, 15 * s, SW, 40 * s),
                $"⚔ 装备调整   {(unit != null ? unit.Name : "")}", (int)(32 * s));

            float panelY = 65 * s;
            float panelH = SH - 135 * s;

            // ---- 左面板：已选角色列表 ----
            float leftW = SW * 0.28f;
            Theme.DrawPanel(new Rect(10 * s, panelY, leftW, panelH), "出战武将");

            _partyScrollPos = GUI.BeginScrollView(
                new Rect(15 * s, panelY + 28 * s, leftW - 10 * s, panelH - 38 * s),
                _partyScrollPos,
                new Rect(0, 0, leftW - 20 * s, _playerParty.Count * 100 * s));

            for (int i = 0; i < _playerParty.Count; i++)
            {
                var u = _playerParty[i];
                bool sel = i == _selectedPartyIndex;
                float iy = i * 72 * s;

                GUI.backgroundColor = sel ? new Color(0.3f, 0.4f, 0.6f) : new Color(0.15f, 0.12f, 0.10f);
                GUI.Box(new Rect(0, iy, leftW - 20 * s, 100 * s), "");

                if (sel)
                {
                    GUI.backgroundColor = Theme.Primary;
                    GUI.Box(new Rect(0, iy, 4 * s, 100 * s), "");
                }

                GUI.Label(new Rect(12 * s, iy + 6 * s, leftW - 40 * s, 48 * s),
                    u.Name, Theme.MakeLabel((int)(44 * s), FontStyle.Bold,
                        sel ? Theme.Gold : Theme.TextLight));
                GUI.Label(new Rect(12 * s, iy + 56 * s, leftW - 40 * s, 22 * s),
                    $"{ClassData.GetName(u.UnitClass)} Lv.{u.Level}",
                    Theme.MakeLabel((int)(20 * s), FontStyle.Normal, Theme.TextDim));
                GUI.Label(new Rect(12 * s, iy + 78 * s, leftW - 40 * s, 20 * s),
                    $"HP {u.CurrentHp}/{u.MaxHp}  MP {u.CurrentMp}/{u.MaxMp}",
                    Theme.MakeLabel((int)(18 * s), FontStyle.Normal, Theme.TextDim));

                if (GUI.Button(new Rect(0, iy, leftW - 20 * s, 100 * s), "", GUIStyle.none))
                {
                    _selectedPartyIndex = i;
                    _showEquipList = false;
                }
            }
            GUI.EndScrollView();

            // ---- 右面板：装备详情 ----
            if (unit != null)
            {
                float rightX = leftW + 25 * s;
                float rightW = SW - leftW - 40 * s;
                float ry = panelY + 10 * s;

                // 角色概要
                string traitInfo = ClassData.GetTraitDescription(unit.UnitClass);
                GUI.Label(new Rect(rightX, ry, rightW, 30 * s),
                    unit.Name, Theme.MakeLabel((int)(24 * s), FontStyle.Bold, Theme.Gold));
                GUI.Label(new Rect(rightX + 160 * s, ry + 4 * s, rightW - 160 * s, 24 * s),
                    $"Lv.{unit.Level}  {ClassData.GetName(unit.UnitClass)}",
                    Theme.MakeLabel((int)(18 * s), FontStyle.Normal, Theme.TextDim));
                ry += 30 * s;

                // 五维对比三列
                DrawStatComparison(rightX, ref ry, rightW, unit, s);
                ry += 8 * s;

                // 装备卡 ×3
                DrawEquipCard(rightX, ref ry, rightW, unit, EquipmentType.Weapon, s);
                DrawEquipCard(rightX, ref ry, rightW, unit, EquipmentType.Armor, s);
                DrawEquipCard(rightX, ref ry, rightW, unit, EquipmentType.Trinket, s);
                ry += 6 * s;

                // 装备选择列表（点击某卡后展开）
                if (_showEquipList)
                {
                    DrawEquipSelectionList(rightX, ry, rightW, unit, s);
                }
                else
                {
                    // 被动
                    if (unit.PassiveIds.Count > 0)
                    {
                        var pnames = unit.PassiveIds.Select(id => PassiveSkillLibrary.Get(id))
                            .Where(p => p != null).Select(p => $"{p.name}({p.description})").ToList();
                        GUI.Label(new Rect(rightX, ry, rightW, 22 * s),
                            $"⚡ {string.Join("  ", pnames)}",
                            Theme.MakeLabel((int)(17 * s), FontStyle.Normal, Theme.BuffCyan));
                        ry += 24 * s;
                    }

                    // 羁绊
                    var bonds = BondSystem.CheckBonds(_playerParty);
                    foreach (var bd in bonds)
                    {
                        var bnames = bd.characterIds.Select(id =>
                            _playerParty.FirstOrDefault(u => u.Id == id)?.Name ?? id).ToList();
                        GUI.Label(new Rect(rightX, ry, rightW, 22 * s),
                            $"✦ {bd.name}: {string.Join("+", bnames)}",
                            Theme.MakeLabel((int)(17 * s), FontStyle.Normal, Theme.Gold));
                        ry += 22 * s;
                    }

                    // 最终属性
                    GUI.Label(new Rect(rightX, ry, rightW, 22 * s),
                        $"攻击范围: {unit.AttackRange}  移动力: {unit.MoveRange}",
                        Theme.MakeLabel((int)(17 * s), FontStyle.Normal, Theme.HpGreen));
                }
            }

            // ---- 底部按钮 ----
            float bY = SH - 60 * s;
            GUI.backgroundColor = Theme.Primary;
            if (GUI.Button(new Rect(SW / 2 - 150 * s, bY, 300 * s, 55 * s),
                "⚔  开 始 战 斗", Theme.MakeButton((int)(22 * s))))
            {
                AudioManager.PlaySfx(AudioManager.SfxClips.Click);
                StartBattle();
            }
            GUI.backgroundColor = Color.white;
        }

        /// <summary>五维对比三列（更紧凑排版，更大字号）</summary>
        private void DrawStatComparison(float x, ref float y, float w, BattleUnit unit, float s)
        {
            Theme.DrawPanel(new Rect(x, y, w, 95 * s), "五维");

            float colW = w / 3;
            float headerY = y + 16 * s;
            float valY = headerY + 18 * s;

            GUI.Label(new Rect(x + 5 * s, headerY, colW, 18 * s), "基础", Theme.MakeLabel((int)(16 * s), FontStyle.Bold, Theme.TextDim, TextAnchor.MiddleCenter));
            GUI.Label(new Rect(x + colW + 5 * s, headerY, colW, 18 * s), "装备加成", Theme.MakeLabel((int)(16 * s), FontStyle.Bold, Theme.TextDim, TextAnchor.MiddleCenter));
            GUI.Label(new Rect(x + colW * 2 + 5 * s, headerY, colW, 18 * s), "总计", Theme.MakeLabel((int)(16 * s), FontStyle.Bold, Theme.TextDim, TextAnchor.MiddleCenter));

            string[] statNames = { "武", "统", "智", "敏", "运" };
            int[] baseVals = { unit.BaseStrength, unit.BaseCommand, unit.BaseIntelligence, unit.BaseAgility, unit.BaseLuck };
            int[] totalVals = { unit.Strength, unit.Command, unit.Intelligence, unit.Agility, unit.Luck };

            for (int i = 0; i < 5; i++)
            {
                float rowY = valY + i * 15 * s;
                int bonus = totalVals[i] - baseVals[i];
                GUI.Label(new Rect(x + 5 * s, rowY, colW, 15 * s),
                    $"{statNames[i]}  {baseVals[i]}", Theme.MakeLabel((int)(16 * s), FontStyle.Normal, Theme.TextLight, TextAnchor.MiddleCenter));
                GUI.Label(new Rect(x + colW + 5 * s, rowY, colW, 15 * s),
                    bonus > 0 ? $"+{bonus}" : "—", Theme.MakeLabel((int)(16 * s), FontStyle.Normal,
                        bonus > 0 ? Theme.HpGreen : Theme.TextDim, TextAnchor.MiddleCenter));
                GUI.Label(new Rect(x + colW * 2 + 5 * s, rowY, colW, 15 * s),
                    $"{totalVals[i]}", Theme.MakeLabel((int)(18 * s), FontStyle.Bold, Theme.TextLight, TextAnchor.MiddleCenter));
            }

            y += 100 * s;
        }

        /// <summary>装备卡（压缩高度，可读字号）</summary>
        private void DrawEquipCard(float x, ref float y, float w, BattleUnit unit, EquipmentType slot, float s)
        {
            string slotName = slot == EquipmentType.Weapon ? "武器" :
                slot == EquipmentType.Armor ? "防具" : "饰品";
            string equipId = slot == EquipmentType.Weapon ? unit.WeaponId :
                slot == EquipmentType.Armor ? unit.ArmorId : unit.TrinketId;
            var equip = string.IsNullOrEmpty(equipId) ? null : EquipmentLibrary.Get(equipId);
            bool isEmpty = equip == null;

            float cardH = 145 * s;
            bool isEditing = _showEquipList && _editingSlot == slot;

            // 卡片背景
            Color border = isEditing ? Theme.Gold : (isEmpty ? new Color(0.3f, 0.3f, 0.3f) : Theme.BgCard);
            GUI.backgroundColor = border;
            Theme.DrawPanel(new Rect(x, y, w, cardH),
                isEmpty ? $"  {slotName}: 点击装备" : $"  {slotName}: {equip.name}");
            GUI.backgroundColor = Color.white;

            if (!isEmpty && equip != null)
            {
                // 稀有度色条
                Color rarityColor = equip.rarity switch
                {
                    EquipmentRarity.Common => Color.white,
                    EquipmentRarity.Uncommon => Color.green,
                    EquipmentRarity.Rare => Color.blue,
                    EquipmentRarity.Epic => new Color(0.7f, 0.2f, 1f),
                    _ => Color.white
                };
                GUI.backgroundColor = rarityColor;
                GUI.Box(new Rect(x + 4 * s, y + 4 * s, 4 * s, cardH - 8 * s), "");
                GUI.backgroundColor = Color.white;

                // 属性
                string statText = FormatEquipStats(equip);
                GUI.Label(new Rect(x + 16 * s, y + 22 * s, w - 80 * s, 22 * s),
                    statText, Theme.MakeLabel((int)(16 * s), FontStyle.Normal, Theme.TextLight));

                // 特效
                if (!string.IsNullOrEmpty(equip.effectDesc))
                {
                    GUI.Label(new Rect(x + 16 * s, y + 46 * s, w - 80 * s, 20 * s),
                        equip.effectDesc, Theme.MakeLabel((int)(15 * s), FontStyle.Normal, Theme.TextDim));
                }

                // 卸下
                if (GUI.Button(new Rect(x + w - 60 * s, y + 6 * s, 50 * s, 26 * s),
                    "卸下", Theme.MakeButton((int)(14 * s))))
                {
                    unit.Unequip(slot);
                    _showEquipList = false;
                }
            }

            // 点击 → 打开装备选择
            if (GUI.Button(new Rect(x, y, w - 60 * s, cardH), "", GUIStyle.none))
            {
                _editingSlot = slot;
                _showEquipList = true;
                _equipScrollPos = Vector2.zero;
            }

            y += cardH + 6 * s;
        }

        /// <summary>装备选择列表（重写：大行高+稀有度色）</summary>
        private void DrawEquipSelectionList(float x, float y, float w, BattleUnit unit, float s)
        {
            string slotLabel = _editingSlot == EquipmentType.Weapon ? "武器" :
                _editingSlot == EquipmentType.Armor ? "防具" : "饰品";
            Theme.DrawPanel(new Rect(x, y, w, Mathf.Min(280 * s, SH - y - 20 * s)),
                $"选择{slotLabel}");

            var allEquips = EquipmentLibrary.GetAll().Values
                .Where(e => e.type == _editingSlot && e.CanEquip(unit))
                .OrderBy(e => e.rarity).ThenBy(e => e.name).ToList();

            if (allEquips.Count == 0)
            {
                GUI.Label(new Rect(x + 20 * s, y + 40 * s, w - 40 * s, 30 * s),
                    "没有可用装备", Theme.MakeLabel((int)(16 * s), FontStyle.Normal, Theme.TextDim));
                if (GUI.Button(new Rect(x + w - 80 * s, y + 5 * s, 70 * s, 24 * s),
                    "关闭", Theme.MakeButton((int)(14 * s))))
                    _showEquipList = false;
                return;
            }

            float listH = Mathf.Min(240 * s, SH - y - 40 * s);
            bool isCur = IsEquipped(unit, allEquips[0].id);

            _equipScrollPos = GUI.BeginScrollView(
                new Rect(x + 5 * s, y + 28 * s, w - 15 * s, listH),
                _equipScrollPos,
                new Rect(0, 0, w - 25 * s, allEquips.Count * 52 * s));

            for (int i = 0; i < allEquips.Count; i++)
            {
                var e = allEquips[i];
                float iy = i * 52 * s;
                bool equipped = IsEquipped(unit, e.id);
                Color rareColor = e.rarity switch
                {
                    EquipmentRarity.Common => Color.white,
                    EquipmentRarity.Uncommon => Color.green,
                    EquipmentRarity.Rare => Color.blue,
                    EquipmentRarity.Epic => new Color(0.7f, 0.2f, 1f),
                    _ => Color.white
                };

                GUI.backgroundColor = equipped ? new Color(0.3f, 0.4f, 0.2f) : new Color(0.15f, 0.12f, 0.10f);
                GUI.Box(new Rect(0, iy, w - 25 * s, 48 * s), "");

                // 稀有度色条
                GUI.backgroundColor = rareColor;
                GUI.Box(new Rect(0, iy, 4 * s, 48 * s), "");
                GUI.backgroundColor = Color.white;

                // 装备名
                GUI.Label(new Rect(14 * s, iy + 4 * s, w - 60 * s, 24 * s),
                    e.name, Theme.MakeLabel((int)(17 * s), FontStyle.Bold, rareColor));

                // 属性
                string stats = FormatEquipStats(e);
                GUI.Label(new Rect(14 * s, iy + 28 * s, w - 60 * s, 18 * s),
                    stats, Theme.MakeLabel((int)(15 * s), FontStyle.Normal, Theme.TextLight));

                // ★ 标记
                if (equipped)
                {
                    GUI.Label(new Rect(w - 60 * s, iy + 10 * s, 40 * s, 28 * s),
                        "★", new GUIStyle { fontSize = (int)(20 * s),
                            normal = { textColor = Theme.Gold },
                            alignment = TextAnchor.MiddleCenter });
                }

                if (GUI.Button(new Rect(0, iy, w - 25 * s, 48 * s), "", GUIStyle.none))
                {
                    unit.Equip(e.id);
                    _showEquipList = false;
                }
            }
            GUI.EndScrollView();

            if (GUI.Button(new Rect(x + w - 80 * s, y + 5 * s, 70 * s, 24 * s),
                "关闭", Theme.MakeButton((int)(14 * s))))
                _showEquipList = false;
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
            string slotLabel = _editingSlot == EquipmentType.Weapon ? "武器" :
                _editingSlot == EquipmentType.Armor ? "防具" : "饰品";
            GUI.Box(new Rect(x, y, w, 200), $"选择{slotLabel}");

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

        // ========== 重叠诊断工具 ==========

        /// <summary>检测所有单位位置重叠，返回重叠详情</summary>
        private List<string> DetectOverlaps()
        {
            var overlaps = new List<string>();
            var posMap = new Dictionary<HexCoord, List<BattleUnit>>();
            foreach (var u in _allUnits.Where(u => u.IsAlive))
            {
                if (!posMap.ContainsKey(u.Position))
                    posMap[u.Position] = new List<BattleUnit>();
                posMap[u.Position].Add(u);
            }
            foreach (var kv in posMap)
            {
                if (kv.Value.Count > 1)
                {
                    var names = string.Join(", ", kv.Value.Select(u => $"{u.Name}[{u.Faction}]"));
                    overlaps.Add($"[重叠] 格子({kv.Key.q},{kv.Key.r}) 被 {kv.Value.Count} 个单位占据: {names}");
                }
            }
            return overlaps;
        }

        /// <summary>记录当前所有单位位置快照（用于对比）</summary>
        private string DumpUnitPositions(string label)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"===== {label} =====");
            foreach (var u in _allUnits.Where(u => u.IsAlive).OrderBy(u => u.Faction.ToString()).ThenBy(u => u.Name))
            {
                sb.AppendLine($"  [{u.Faction}] {u.Name} @({u.Position.q},{u.Position.r}) HP={u.CurrentHp}/{u.MaxHp} State={u.State} Moved={u.HasMovedThisTurn} Acted={u.HasActed}");
            }
            return sb.ToString();
        }

        /// <summary>在关键位置调用的重叠检查器——检测到重叠就输出详细日志</summary>
        private void CheckOverlapAt(string checkpoint)
        {
            var overlaps = DetectOverlaps();
            if (overlaps.Count > 0)
            {
                GameLogger.LogErrorFormat(LogCategory.Battle, "重叠检测|检查点={0}|数量={1}", checkpoint, overlaps.Count);
                foreach (var o in overlaps)
                    GameLogger.LogError(LogCategory.Battle, o);
                GameLogger.LogError(LogCategory.Battle, DumpUnitPositions($"重叠现场@{checkpoint}"));
            }
            else
            {
                GameLogger.LogInfoFormat(LogCategory.Battle, "重叠检测|检查点={0}|结果=无重叠", checkpoint);
            }
        }

        /// <summary>修复所有单位位置重叠：将重叠单位推到最近的空格</summary>
        private void ResolveUnitOverlaps()
        {
            var overlaps = DetectOverlaps();
            if (overlaps.Count > 0)
            {
                GameLogger.LogWarningFormat(LogCategory.Battle, "ResolveUnitOverlaps|发现重叠={0}", overlaps.Count);
                foreach (var o in overlaps)
                    GameLogger.LogWarning(LogCategory.Battle, o);
                GameLogger.LogError(LogCategory.Battle, DumpUnitPositions("修复前"));
            }

            var occupied = new HashSet<HexCoord>();
            int fixedCount = 0;
            foreach (var unit in _allUnits.Where(u => u.IsAlive).OrderBy(u => u.Faction))
            {
                if (occupied.Contains(unit.Position))
                {
                    HexCoord oldPos = unit.Position;
                    bool found = false;
                    for (int d = 1; d <= 6 && !found; d++)
                    {
                        foreach (var cell in unit.Position.Range(d))
                        {
                            if (_hexView != null && _hexView.Grid != null && _hexView.Grid.InBounds(cell)
                                && !occupied.Contains(cell))
                            {
                                unit.Position = cell;
                                found = true;
                                fixedCount++;
                                GameLogger.LogInfoFormat(LogCategory.Battle, "重叠修复|单位={0}|旧=({1},{2})|新=({3},{4})|距离={5}",
                                    unit.Name, oldPos.q, oldPos.r, cell.q, cell.r, d);
                                break;
                            }
                        }
                    }
                    if (!found)
                    {
                        GameLogger.LogErrorFormat(LogCategory.Battle, "重叠修复|单位={0}|位置=({1},{2})|结果=无法找到空位",
                            unit.Name, oldPos.q, oldPos.r);
                    }
                }
                occupied.Add(unit.Position);
            }

            if (fixedCount > 0)
            {
                GameLogger.LogError(LogCategory.Battle, DumpUnitPositions($"修复后（修复了{fixedCount}个）"));
                // 修复后再次检查
                var after = DetectOverlaps();
                if (after.Count > 0)
                {
                    GameLogger.LogErrorFormat(LogCategory.Battle, "ResolveUnitOverlaps|修复后仍有重叠={0}", after.Count);
                    foreach (var o in after)
                        GameLogger.LogError(LogCategory.Battle, o);
                }
                else
                {
                    GameLogger.LogInfo(LogCategory.Battle, "ResolveUnitOverlaps|所有重叠已修复");
                }
            }
        }

        /// <summary>从战前编组过渡到战斗</summary>
        private void StartBattle()
        {
            _showEquipList = false;
            // 注意：_gamePhase 延后到初始化完成后才设为 Battle，防止 Update 提前处理点击

            // 将玩家队伍设置为战斗单位
            _allUnits.Clear();
            _allUnits.AddRange(_playerParty);
            GameLogger.LegacyLog($"[StartBattle] 玩家队伍 {_playerParty.Count} 人，位置分布：{string.Join(", ", _playerParty.Select(u => $"{u.Name}@({u.Position.q},{u.Position.r})"))}");

            // 敌方单位
            CreateEnemyUnits();
            CheckOverlapAt("CreateEnemyUnits后");

            // 设置位置
            float startX = 1;
            float startY = 3;
            for (int i = 0; i < _playerParty.Count; i++)
            {
                _playerParty[i].Position = new HexCoord((int)(startX + i % 2), (int)(startY + i / 2));
                _playerParty[i].NewTurn();
            }
            GameLogger.LegacyLog($"[StartBattle] 玩家重新布置到起始区域：{string.Join(", ", _playerParty.Select(u => $"{u.Name}@({u.Position.q},{u.Position.r})"))}");
            CheckOverlapAt("玩家位置设置后");

            // 天气系统（从关卡数据读取）
            WeatherType weatherType = _currentLevel?.weather ?? WeatherType.Sunny;
            WindDirection windDir = _currentLevel?.wind ?? WindDirection.None;
            _weather = new WeatherSystem(weatherType, windDir);

            if (_hexView?.Grid == null)
            {
                GameLogger.LogError(LogCategory.Battle, "_hexView.Grid 为空，无法创建 SkillExecutor 与 AI。");
                return;
            }
            _skillExecutor = new SkillExecutor(_allUnits, _hexView.Grid, _weather);
            string diffStr = GameState.CurrentDifficulty switch
            {
                GameState.Difficulty.Easy => "easy",
                GameState.Difficulty.Hard => "hard",
                _ => "normal"
            };
            _aiTree = new AIBehaviorTree(_allUnits, _hexView.Grid, _weather, _skillExecutor, diffStr);

            // ★ 初始重叠修复：确保所有单位位置不重叠
            ResolveUnitOverlaps();
            CheckOverlapAt("ResolveUnitOverlaps后");

            // 创建可视化
            foreach (var unit in _allUnits)
            {
                var visual = UnitVisual.Create(unit, _hexView);
                _unitVisuals[unit] = visual;
            }

            // 回合管理器 + VictoryChecker
            _turnManager = new TurnManager(_allUnits);
            _victoryChecker = new VictoryChecker(_currentLevel, _allUnits, _turnManager);
            _turnManager.OnCustomVictoryCheck = OnCustomVictoryCheck;
            _turnManager.OnUnitTurnStart += OnUnitTurnStart;
            _turnManager.OnUnitTurnEnd += OnUnitTurnEnd;
            _turnManager.OnPhaseChanged += OnPhaseChanged;

            // 应用羁绊加成
            ApplyBondBonuses();

            // 难度缩放敌方属性
            ScaleEnemiesByDifficulty();

            // 重置回春使用记录（新关卡）
            SkillExecutor.ResetReviveTracker();

            _battleUI?.ShowTip("选中己方单位 → 点击移动/攻击 | 底部选择计策");
            _turnManager?.StartBattle();

            // 战斗BGM（Boss关使用boss音乐）
            bool isBossLevel = _currentLevel?.enemies.Any(e => e.isBoss) ?? false;
            AudioManager.PlayBgm(isBossLevel ? AudioManager.BgmClips.Boss : AudioManager.BgmClips.Battle);

            // ★ 所有初始化完成后再设为 Battle 阶段（防止 Update 提前处理点击）
            _gamePhase = GamePhase.Battle;

            // ★ 通知 GameManager 切换到战斗阶段（否则 EquipSetup UI 不会消失）
            if (GameManager.Instance != null)
                GameManager.Instance.TransitionTo(GamePage.Battle);
        }

        /// <summary>应用羁绊加成到出战队伍</summary>
        private void ApplyBondBonuses()
        {
            var bonds = BondSystem.CheckBonds(_playerParty);
            var playerIds = new HashSet<string>(_playerParty.Select(u => u.Id));

            foreach (var bond in bonds)
            {
                switch (bond.effectType)
                {
                    case "all_stats_pct":
                        // 全属性+% 给羁绊角色
                        foreach (var uid in bond.characterIds)
                        {
                            var unit = _playerParty.FirstOrDefault(u => u.Id == uid);
                            if (unit != null)
                            {
                                int bonus = (int)(unit.BaseStrength * bond.effectValue);
                                unit.BaseStrength += bonus;
                                unit.BaseCommand += (int)(unit.BaseCommand * bond.effectValue);
                                unit.BaseIntelligence += (int)(unit.BaseIntelligence * bond.effectValue);
                                unit.BaseAgility += (int)(unit.BaseAgility * bond.effectValue);
                                unit.BaseLuck += (int)(unit.BaseLuck * bond.effectValue);
                            }
                        }
                        break;

                    case "team_attack_pct":
                        // 全队攻击+%
                        foreach (var unit in _playerParty)
                        {
                            unit.BaseStrength += (int)(unit.BaseStrength * bond.effectValue);
                        }
                        break;

                    case "magic_damage_pct":
                        // 全队计策伤害+%
                        // 简单实现：给羁绊角色加智力
                        foreach (var uid in bond.characterIds)
                        {
                            var unit = _playerParty.FirstOrDefault(u => u.Id == uid);
                            if (unit != null)
                                unit.BaseIntelligence += (int)(unit.BaseIntelligence * bond.effectValue);
                        }
                        break;

                    case "cavalry_move":
                        // 骑兵移动力+1
                        foreach (var unit in _playerParty.Where(u => u.UnitClass == ClassType.Cavalry))
                        {
                            unit.BaseMoveRange += (int)bond.effectValue;
                        }
                        break;
                }
            }

            if (bonds.Count > 0)
                GameLogger.LogInfoFormat(LogCategory.Battle, "羁绊加成|数量={0}", bonds.Count);
        }

        /// <summary>根据难度缩放敌方属性</summary>
        private void ScaleEnemiesByDifficulty()
        {
            float mult = GameState.CurrentDifficulty switch
            {
                GameState.Difficulty.Easy => 0.80f,
                GameState.Difficulty.Normal => 1.00f,
                GameState.Difficulty.Hard => 1.20f,
                GameState.Difficulty.Hell => 1.45f,
                _ => 1.00f
            };
            // 极简模式：玩家方获得20%属性加成
            if (GameState.CurrentDifficulty == GameState.Difficulty.Easy)
            {
                foreach (var player in _allUnits.Where(u => u.Faction == Faction.Player))
                {
                    player.BaseStrength = (int)(player.BaseStrength * 1.20f);
                    player.BaseCommand = (int)(player.BaseCommand * 1.20f);
                    player.BaseAgility = (int)(player.BaseAgility * 1.10f);
                    player.MaxHp += 20;
                    player.CurrentHp = player.MaxHp;
                }
            }

            if (Mathf.Approximately(mult, 1f)) return;

            int addHp = GameState.CurrentDifficulty == GameState.Difficulty.Hell ? 20 : 0;

            foreach (var enemy in _allUnits.Where(u => u.Faction == Faction.Enemy))
            {
                enemy.BaseStrength = (int)(enemy.BaseStrength * mult);
                enemy.BaseCommand = (int)(enemy.BaseCommand * mult);
                enemy.BaseIntelligence = (int)(enemy.BaseIntelligence * mult);
                enemy.BaseAgility = (int)(enemy.BaseAgility * mult);
                enemy.BaseLuck = (int)(enemy.BaseLuck * mult);
                enemy.MaxHp = (int)(enemy.MaxHp * mult) + addHp;
                enemy.MaxMp = (int)(enemy.MaxMp * mult);
                enemy.CurrentHp = enemy.MaxHp;
                enemy.CurrentMp = enemy.MaxMp;
            }
        }

        /// <summary>从关卡数据创建敌方单位</summary>
        private void CreateEnemyUnits()
        {
            if (_currentLevel == null)
            {
                GameLogger.LogError(LogCategory.Battle, "没有关卡数据，无法创建敌人");
                return;
            }

            foreach (var cfg in _currentLevel.enemies)
            {
                var enemy = new BattleUnit(cfg.id, cfg.name, Faction.Enemy, cfg.unitClass,
                    cfg.str, cfg.cmd, cfg.@int, cfg.agi, cfg.luk,
                    cfg.hp, cfg.mp, cfg.move, cfg.attackRange,
                    Gender.Male)
                {
                    Position = cfg.position,
                    Level = cfg.level,
                    SkillIds = cfg.skillIds != null ? new List<string>(cfg.skillIds) : new List<string>(),
                    StrGrowth = Mathf.Max(1, cfg.level / 2),
                    CmdGrowth = Mathf.Max(1, cfg.level / 2),
                    IntGrowth = Mathf.Max(1, cfg.level / 3),
                    AgiGrowth = Mathf.Max(1, cfg.level / 3),
                    LukGrowth = Mathf.Max(1, cfg.level / 4)
                };
                _allUnits.Add(enemy);
            }
        }

        /// <summary>屏幕坐标 → 世界坐标 → 处理点击</summary>
        private void HandleScreenTap(Vector2 screenPos)
        {
            // Camera.main 安全检测
            if (Camera.main == null)
            {
                GameLogger.LogError(LogCategory.System, "Camera.main 为空");
                return;
            }

            // 检查 HexGridView 是否就绪
            if (_hexView == null || _hexView.Grid == null)
            {
                // 非战斗阶段点击忽略（如结算后/切换中）
                if (_gamePhase != GamePhase.Battle)
                    return;
                GameLogger.LogErrorFormat(LogCategory.System, "HexGridView 未初始化|phase={0}|page={1}", _gamePhase, GameManager.Instance?.CurrentPage);
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
            BattleUnit unitAtCell = _turnManager?.GetUnitAt(cell);

            // 如果点击的是自己的单位——选中它（只针对未行动的单位）
            if (unitAtCell != null && unitAtCell.Faction == Faction.Player
                && unitAtCell.State == UnitState.Ready && !unitAtCell.HasActed)
            {
                SelectUnit(unitAtCell);
                return;
            }

            if (_selectedUnit != null && _selectedUnit.State == UnitState.Ready)
            {
                // 计策优先
                if (!string.IsNullOrEmpty(_selectedSkillId))
                {
                    TryUseSkill(_selectedUnit, cell);
                    return;
                }

                // ========== 已移动但未攻击 = 攻击阶段 ==========
                if (_selectedUnit.HasMovedThisTurn && !_selectedUnit.HasActed)
                {
                    // 点击攻击范围内的敌人→攻击
                    if (unitAtCell != null && unitAtCell.Faction == Faction.Enemy
                        && _selectedUnit.Position.Distance(cell) <= _selectedUnit.AttackRange)
                    {
                        AttackUnit(_selectedUnit, unitAtCell);
                        return;
                    }
                    // 点击其他任何地方→待机
                    _battleUI?.ShowTip($"{_selectedUnit.Name} 待机");
                    DeselectUnit();
                    EndUnitAction();
                    return;
                }

                // ========== 未移动 = 移动/攻击阶段 ==========

                // 点击攻击范围内的敌人→攻击（原地攻击，不移动）
                if (unitAtCell != null && unitAtCell.Faction == Faction.Enemy
                    && _selectedUnit.Position.Distance(cell) <= _selectedUnit.AttackRange)
                {
                    AttackUnit(_selectedUnit, unitAtCell);
                    return;
                }

                // 点击友方→如果有治疗计策，尝试治疗
                if (unitAtCell != null && unitAtCell.Faction == Faction.Player && !_selectedUnit.HasActed)
                {
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

                // 点击空地→移动（排除其他单位占据的格子）
                var occ2 = new HashSet<HexCoord>(_allUnits.Where(u => u != _selectedUnit && u.IsAlive)
                    .Select(u => u.Position));
                if (_hexView?.PathFinder == null)
                {
                    GameLogger.LogError(LogCategory.Battle, "_hexView.PathFinder 为空，无法计算移动范围。");
                    return;
                }
                var range = _hexView.PathFinder.GetMoveRange(
                    _selectedUnit.Position, _selectedUnit.MoveRange, _selectedUnit.UnitClass, occ2);
                if (range.ContainsKey(cell))
                {
                    _selectedSkillId = null;
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
            var occ = new HashSet<HexCoord>(_allUnits.Where(u => u != unit && u.IsAlive).Select(u => u.Position));
            _hexView.ShowMoveRange(unit.Position, unit.MoveRange, unit.UnitClass, occ);

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

            // 被动技能信息
            string passiveInfo = "";
            if (unit.PassiveIds.Count > 0)
            {
                var pnames = unit.PassiveIds
                    .Select(id => PassiveSkillLibrary.Get(id))
                    .Where(p => p != null)
                    .Select(p => p.name);
                passiveInfo = " | 被动: " + string.Join(" ", pnames);
            }

            _battleUI?.ShowTip($"选中 Lv{unit.Level} {unit.Name} [HP:{unit.CurrentHp}/{unit.MaxHp} MP:{unit.CurrentMp}] 武{unit.Strength}统{unit.Command}智{unit.Intelligence}敏{unit.Agility}运{unit.Luck}{equipInfo}{skillInfo}{passiveInfo}");
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
            try
            {
                _hexView.ClearHighlights();
                DeselectUnit();

                // 计算路径（排除其他单位，并按当前兵种寻路）
                var occupied = new HashSet<HexCoord>(_allUnits
                    .Where(u => u != unit && u.IsAlive)
                    .Select(u => u.Position));
                var path = _hexView.PathFinder.FindPath(unit.Position, target, unit.UnitClass, occupied);
                if (path.Count < 2)
                    yield break;

                if (!_unitVisuals.TryGetValue(unit, out var visual) || visual == null)
                    yield break;

                // ★ 重叠保护：移动前确认目标格无其他单位
                var blockingUnit = _allUnits.FirstOrDefault(u => u != unit && u.IsAlive && u.Position == target);
                if (blockingUnit != null)
                {
                    GameLogger.LogWarningFormat(LogCategory.Battle, "移动目标被占据|单位={0}|目标=({1},{2})|阻挡={3}[{4}]",
                        unit.Name, target.q, target.r, blockingUnit.Name, blockingUnit.Faction);
                    _battleUI?.ShowTip($"目标格子被 {blockingUnit.Name} 占据！无法移动");
                    yield break;
                }

                GameLogger.LogInfoFormat(LogCategory.Battle, "单位移动|单位={0}|起点=({1},{2})|终点=({3},{4})|路径格={5}",
                    unit.Name, unit.Position.q, unit.Position.r, target.q, target.r, path.Count);
                CheckOverlapAt($"MoveUnitAnimation-{unit.Name}移动前");

                // 沿路径逐格移动
                float stepDuration = 0.15f;
                for (int i = 1; i < path.Count; i++)
                {
                    if (visual == null) yield break;
                    unit.Position = path[i];
                    Vector3 startPos = visual.transform.position;
                    Vector3 endPos = _hexView.HexToWorld(path[i]);

                    float elapsed = 0f;
                    while (elapsed < stepDuration)
                    {
                        if (visual == null) yield break;
                        elapsed += Time.deltaTime;
                        float t = elapsed / stepDuration;
                        t = t * t * (3f - 2f * t); // smoothstep
                        visual.transform.position = Vector3.Lerp(startPos, endPos, t);
                        yield return null;
                    }
                    if (visual != null)
                        visual.transform.position = endPos;
                }

                if (unit != null)
                {
                    unit.HasMovedThisTurn = true;
                    unit.Position = target;
                }
                GameLogger.LogInfoFormat(LogCategory.Battle, "单位到达|单位={0}|位置=({1},{2})", unit.Name, target.q, target.r);
                CheckOverlapAt($"MoveUnitAnimation-{unit.Name}移动后");
                _battleUI?.ShowTip($"{unit.Name} 移动到 ({target.q},{target.r})");

                // ★ 检查攻击范围内是否有敌人 → 保持选中让玩家选择攻击或待机
                bool hasTarget = _allUnits.Any(u => u.Faction == Faction.Enemy && u.IsAlive
                    && target.Distance(u.Position) <= unit.AttackRange);

                if (hasTarget)
                {
                    // 重新选中单位，显示攻击范围高亮
                    _selectedUnit = unit;
                    var enemyCells = _allUnits.Where(u => u.Faction == Faction.Enemy && u.IsAlive
                        && target.Distance(u.Position) <= unit.AttackRange)
                        .Select(u => u.Position).ToList();
                    _hexView.ShowAttackRange(target, unit.AttackRange, enemyCells);
                    if (_unitVisuals.TryGetValue(unit, out var vis))
                        vis.SetSelected(true);
                    _battleUI?.ShowTip($"{unit.Name} 选择攻击目标或点击空白处待机");
                }
                else
                {
                    _battleUI?.ShowTip($"{unit.Name} 到达目标位置，范围内无敌方");
                    EndUnitAction();
                }
            }
            finally
            {
                _isAnimating = false; // 确保任何情况下都重置
            }
        }

        // ========== 攻击 ==========

        private void AttackUnit(BattleUnit attacker, BattleUnit defender)
        {
            CheckOverlapAt($"AttackUnit-{attacker.Name}攻击前");
            GameLogger.LogInfoFormat(LogCategory.Battle, "攻击发起|攻={0}@({1},{2})|防={3}@({4},{5})",
                attacker.Name, attacker.Position.q, attacker.Position.r,
                defender.Name, defender.Position.q, defender.Position.r);
            // 检查攻击者和被攻击者是否在同一位置
            if (attacker.Position == defender.Position)
            {
                GameLogger.LogErrorFormat(LogCategory.Battle, "攻击发起时重叠|攻={0}|防={1}|位置=({2},{3})",
                    attacker.Name, defender.Name, attacker.Position.q, attacker.Position.r);
            }

            var attackerTerrain = _hexView.Grid.GetTerrain(attacker.Position);
            var result = CombatCalculator.CalcPhysicalDamage(attacker, defender, 0, 0, attackerTerrain);

            defender.TakeDamage(result.damage);

            // 攻击音效
            if (result.isHit)
                AudioManager.PlaySfx(result.isCrit ? AudioManager.SfxClips.Crit : AudioManager.SfxClips.Attack);
            AudioManager.PlaySfx(AudioManager.SfxClips.Hit);

            string critText = result.isCrit ? "【暴击】" : "";
            string hitText = result.isHit ?
                $"造成 {result.damage} 点伤害" : "未命中";
            string log = $"{attacker.Name} 攻击 {defender.Name}：{hitText}{critText}";

            _battleUI?.ShowTip(log);
            GameLogger.LogInfoFormat(LogCategory.Battle,
                "战斗结果|攻={0}|防={1}|伤害={2}|命中={3}|暴击={4}|防御者HP={5}/{6}",
                attacker.Name, defender.Name, result.damage, result.isHit, result.isCrit,
                defender.CurrentHp, defender.MaxHp);

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
                GameLogger.LogInfoFormat(LogCategory.Battle, "单位阵亡|单位={0}|阵营={1}", defender.Name, defender.Faction);
                AudioManager.PlaySfx(AudioManager.SfxClips.Death);
                if (_unitVisuals.TryGetValue(defender, out var deadVis))
                {
                    StartCoroutine(DeathAnimation(deadVis, defender));
                }

                // 经验分配
                int exp = CombatCalculator.CalcExp(attacker.Level - defender.Level, true, false, attacker);
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
                GameLogger.LogInfo(LogCategory.Battle, expLog);

                // ★ 阵亡后立即检查胜负（不需要等全队行动完毕）
                if (_victoryChecker != null && !_victoryChecker.IsVictory && !_victoryChecker.IsDefeat)
                {
                    _victoryChecker.Check();
                    if (_victoryChecker.IsVictory || _victoryChecker.IsDefeat)
                    {
                        _hexView.ClearHighlights();
                        _selectedUnit = null;
                        _turnManager.SetPhase(_victoryChecker.IsVictory
                            ? TurnManager.TurnPhase.Victory : TurnManager.TurnPhase.Defeat);
                        return;
                    }
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
            AudioManager.PlaySfx(skill.type == SkillType.Heal ? AudioManager.SfxClips.Heal : AudioManager.SfxClips.Skill);
            _battleUI?.ShowTip(log);
            GameLogger.LogInfo(LogCategory.Battle, log);

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

            // ★ 计策击杀后立即检查胜负
            var deadNow = _allUnits.Where(u => u.IsDead).ToList();
            if (deadNow.Count > 0 && _victoryChecker != null && !_victoryChecker.IsVictory && !_victoryChecker.IsDefeat)
            {
                _victoryChecker.Check();
                if (_victoryChecker.IsVictory || _victoryChecker.IsDefeat)
                {
                    _hexView.ClearHighlights();
                    _selectedSkillId = null;
                    _selectedUnit = null;
                    _isAnimating = false;
                    _turnManager.SetPhase(_victoryChecker.IsVictory
                        ? TurnManager.TurnPhase.Victory : TurnManager.TurnPhase.Defeat);
                    return;
                }
            }

            _hexView.ClearHighlights();
            _selectedSkillId = null;
            _selectedUnit = null;
            _isAnimating = false;
            EndUnitAction();
        }

        /// <summary>OnGUI 计策选择面板 + 单挑按钮 + 战前编组</summary>
        void OnGUI()
        {
            // 显示错误信息（如果有）
            if (!string.IsNullOrEmpty(_debugErr))
            {
                GUI.Box(new Rect(0, 0, SW, SH), "");
                GUI.Label(new Rect(30, 30, SW - 60, SH - 60),
                    _debugErr, new GUIStyle { fontSize = 28,
                        normal = { textColor = Color.red }, wordWrap = true });
                return;
            }

            // 小字阶段指示器（调试用，后续可移除）
            GUI.Label(new Rect(SW - 300, 0, 300, 24),
                $"P:{GameManager.Instance?.CurrentPage} PH:{_gamePhase}",
                Theme.MakeLabel(12, FontStyle.Normal, Color.gray));

            // 强制同步 GameManager 状态
            if (GameManager.Instance != null)
            {
                switch (GameManager.Instance.CurrentPage)
                {
                    case GamePage.LevelSelect: _gamePhase = GamePhase.LevelSelect; break;
                    case GamePage.HeroSelect: _gamePhase = GamePhase.HeroSelect; break;
                    case GamePage.EquipSetup: _gamePhase = GamePhase.EquipSetup; break;
                    case GamePage.Battle: _gamePhase = GamePhase.Battle; break;
                    case GamePage.Results: _gamePhase = GamePhase.Results; break;
                }
            }

            // 过渡保护 — 页面切换时禁止绘制
            if (GameManager.Instance != null && GameManager.Instance.IsTransitioning) return;

            // 对话期间不显示战斗UI（DialogueUI独立绘制）
            if (_waitingForDialogue || (GameManager.Instance != null &&
                GameManager.Instance.CurrentPage == GamePage.Story)) return;

            // 关卡选择
            if (_gamePhase == GamePhase.LevelSelect)
            {
                DrawLevelSelectUI();
                return;
            }

            // 选人界面
            if (_gamePhase == GamePhase.HeroSelect)
            {
                DrawHeroSelectUI();
                return;
            }

            // 装备调整界面
            if (_gamePhase == GamePhase.EquipSetup)
            {
                DrawEquipSetupUI();
                return;
            }

            // 结算界面
            if (_gamePhase == GamePhase.Results)
            {
                DrawResultsUI();
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

            float s = _uiScale;
            float btnW = 100 * s;
            float btnH = 55 * s;
            float pad = 8 * s;
            float startX = 10 * s;
            float startY = SH - 70 * s;

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

            // 战斗按钮样式（大字号）
            GUIStyle battleBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = (int)(14 * s),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Theme.TextLight },
                hover = { textColor = Theme.GoldLight }
            };

            // "普通攻击" 按钮
            if (string.IsNullOrEmpty(_selectedSkillId) && !canDuel)
                GUI.backgroundColor = Color.green;
            if (GUI.Button(new Rect(startX, startY, btnW, btnH), "⚔ 攻击", battleBtnStyle))
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
                if (GUI.Button(new Rect(x, startY, btnW, btnH), label, battleBtnStyle))
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
                AudioManager.PlaySfx(AudioManager.SfxClips.Click);
                ExecuteDuelRound(DuelAction.Attack);
            }
            btnX += 110;

            if (GUI.Button(new Rect(btnX, btnY, btnW, btnH2), "防御"))
            {
                AudioManager.PlaySfx(AudioManager.SfxClips.Click);
                ExecuteDuelRound(DuelAction.Defend);
            }
            btnX += 110;

            GUI.enabled = _duelSystem.PlayerSpecialGauge > 0;
            if (GUI.Button(new Rect(btnX, btnY, btnW, btnH2), "必杀"))
            {
                AudioManager.PlaySfx(AudioManager.SfxClips.Click);
                ExecuteDuelRound(DuelAction.Special);
            }
            GUI.enabled = true;
        }

        private void ExecuteDuelRound(DuelAction playerAction)
        {
            AudioManager.PlaySfx(AudioManager.SfxClips.Duel);

            // 敌方随机选择
            var enemyAction = (DuelAction)UnityEngine.Random.Range(0, 3);
            _duelSystem.ExecuteRound(playerAction, enemyAction);

            if (_duelSystem.IsFinished)
            {
                string resultLog = _duelSystem.ApplyResult();
                _battleUI?.ShowTip(resultLog);
                GameLogger.LogInfo(LogCategory.Battle, resultLog);

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

        // ========== 关卡选择 UI ==========

        private void DrawLevelSelectUI()
        {
            float s = _uiScale;
            // 背景（已裁剪到屏幕边界）
            GUI.backgroundColor = Theme.BgDark;
            GUI.Box(Theme.ClampToScreen(new Rect(0, 0, SW, SH)), "");
            GUI.backgroundColor = Color.white;

            // 顶部装饰
            GUI.backgroundColor = Theme.Primary;
            GUI.Box(new Rect(0, 0, SW, 8 * s), "");
            GUI.backgroundColor = Color.white;

            // 标题
            Theme.DrawTitle(new Rect(0, 20 * s, SW, 55 * s), "🏯 征战天下", (int)(42 * s));

            // 卡片
            float cardX = 40 * s;
            float cardW = SW - 80 * s;
            float cardH = 160 * s;
            float startY = 90 * s;
            float gap = 14 * s;

            for (int i = 0; i < _levelOrder.Count; i++)
            {
                string levelId = _levelOrder[i];
                var level = LevelLibrary.Get(levelId);
                if (level == null) continue;

                bool unlocked = GameState.IsLevelUnlocked(levelId);
                float y = startY + i * (cardH + gap);

                // 卡片背景（裁剪到屏幕边界）
                var cardRect = Theme.ClampToScreen(new Rect(cardX, y, cardW, cardH));
                GUI.backgroundColor = unlocked ? new Color(0.25f, 0.18f, 0.12f) : new Color(0.12f, 0.08f, 0.06f);
                GUI.Box(cardRect, "");
                GUI.backgroundColor = Color.white;

                // 左侧装饰条
                if (unlocked)
                {
                    GUI.backgroundColor = Theme.Primary;
                    GUI.Box(Theme.ClampToScreen(new Rect(cardX, y, 6 * s, cardH)), "");
                    GUI.backgroundColor = Color.white;
                }

                // 关卡名
                string levelTitle = $"第{i + 1}关  {level.name}";
                GUI.Label(Theme.ClampToScreen(new Rect(cardX, y + 10 * s, cardW, 48 * s)),
                    levelTitle,
                    Theme.MakeLabel((int)(38 * s), FontStyle.Bold, unlocked ? Theme.Gold : Theme.TextDim,
                                    TextAnchor.MiddleCenter));

                // 信息行
                string info = level.victoryType switch
                {
                    VictoryConditionType.DefeatAll => "全灭敌军",
                    VictoryConditionType.DefeatBoss => "击破主将",
                    VictoryConditionType.DefendTurns => $"坚守{level.defendTurns}回合",
                    _ => "未知"
                };
                GUI.Label(Theme.ClampToScreen(new Rect(cardX, y + 65 * s, cardW, 28 * s)),
                    $"{info}    敌方{level.enemies.Count}人",
                    Theme.MakeLabel((int)(26 * s), FontStyle.Normal, Theme.TextDim, TextAnchor.MiddleCenter));

                // 角色
                string roster = string.Join(" ", level.availableCharacters.Take(4)
                    .Select(id => CharacterDatabase.Get(id)?.Name ?? id));
                if (level.availableCharacters.Count > 4) roster += " …";
                GUI.Label(Theme.ClampToScreen(new Rect(cardX, y + 98 * s, cardW, 24 * s)),
                    $"出场: {roster}",
                    Theme.MakeLabel((int)(22 * s), FontStyle.Normal, new Color(0.7f, 0.9f, 0.7f), TextAnchor.MiddleCenter));

                // 锁定
                if (!unlocked)
                {
                    GUIStyle lockStyle = new GUIStyle { fontSize = (int)(48 * s), alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = Theme.TextDim } };
                    GUI.Label(Theme.ClampToScreen(new Rect(cardX + cardW - 80 * s, y + 30 * s, 60 * s, 60 * s)), "🔒", lockStyle);
                }

                // 点击
                if (unlocked && GUI.Button(cardRect, "", GUIStyle.none))
                {
                    AudioManager.PlaySfx(AudioManager.SfxClips.Click);
                    SelectLevel(levelId);
                }
            }
        }

        /// <summary>选择关卡，进入战前编组</summary>
        public void SelectLevel(string levelId)
        {
            _currentLevel = LevelLibrary.Get(levelId);
            if (_currentLevel == null) { _debugErr = "Level is null"; return; }
            _currentLevelIndex = _levelOrder.IndexOf(levelId);

            _hexView.RebuildFromLevelData(_currentLevel);
            try { InitHeroPool(); }
            catch (System.Exception ex) { _debugErr = "InitHeroPool: " + ex.Message; return; }

            // ★ 关前剧情：如有则先播放，再进入 HeroSelect
            string preStoryId = $"story_{levelId}_pre";
            if (StoryLibrary.Get(preStoryId) != null && _dialogueUI != null)
            {
                PlayLevelStory(preStoryId);
                return;
            }

            GameManager.Instance.TransitionTo(GamePage.HeroSelect);
        }
        private string _debugErr;

        private void PlayLevelStory(string storyId)
        {
            if (_dialogueUI == null) return;
            _waitingForDialogue = true;
            GameManager.Instance.EnterStory();
            _dialogueUI.PlayStory(storyId, () =>
            {
                _waitingForDialogue = false;
                GameManager.Instance.ExitStory();
                GameManager.Instance.TransitionTo(GamePage.HeroSelect);
            });
        }

        /// <summary>初始化英雄池（可选角色+必出角色，不自动填满）</summary>
        private List<BattleUnit> _heroPool = new();    // 所有可用角色（含必出）
        private List<bool> _heroSelected;              // 勾选状态
        private bool _initialSelectionDone;

        private void InitHeroPool()
        {
            _heroPool.Clear();
            _heroSelected = new List<bool>();
            _initialSelectionDone = false;

            var db = CharacterDatabase.GetAll();

            // 先加必出角色
            foreach (var charId in _currentLevel.requiredCharacters)
            {
                if (db.ContainsKey(charId))
                {
                    var unit = CharacterDatabase.CreateInstance(charId);
                    if (unit != null)
                    {
                        unit.Equip(GetDefaultWeapon(unit.UnitClass));
                        unit.Equip(unit.UnitClass == ClassType.Strategist || unit.UnitClass == ClassType.Archer ? "a003" : "a001");
                        _heroPool.Add(unit);
                        _heroSelected.Add(true); // 必出默认选中
                    }
                }
            }

            // 再加可选角色
            foreach (var charId in _currentLevel.availableCharacters)
            {
                if (_heroPool.Any(u => u.Id == charId)) continue;
                if (db.ContainsKey(charId))
                {
                    var unit = CharacterDatabase.CreateInstance(charId);
                    if (unit != null)
                    {
                        unit.Equip(GetDefaultWeapon(unit.UnitClass));
                        unit.Equip(unit.UnitClass == ClassType.Strategist || unit.UnitClass == ClassType.Archer ? "a003" : "a001");
                        // ★ 先算好勾选状态再Add，避免集合大小不一致
                        int currentSelected = _heroSelected.Count(v => v);
                        _heroPool.Add(unit);
                        _heroSelected.Add(currentSelected < 8);
                    }
                }
            }
        }

        private string GetDefaultWeapon(ClassType cls) => cls switch
        {
            ClassType.Cavalry => "w003",
            ClassType.Archer => "w004",
            ClassType.Siege => "w005",
            ClassType.Strategist => "w006",
            ClassType.HeavyInfantry => "w002",
            _ => "w001"
        };

        // ========== 结算界面 ==========

        private void DrawResultsUI()
        {
            float s = _uiScale;
            GUI.backgroundColor = Theme.BgDark;
            GUI.Box(new Rect(0, 0, SW, SH), "");
            GUI.backgroundColor = Color.white;

            // 结算面板 — 按缩放适配尺寸
            float pw = Mathf.Min(480 * s, SW - 40 * s);
            float ph = Mathf.Min(400 * s, SH - 40 * s);
            float px = (SW - pw) / 2;
            float py = (SH - ph) / 2;

            Theme.DrawPanel(new Rect(px, py, pw, ph));

            // 顶部装饰
            bool isVictory = _resultsTitle.Contains("胜利");
            Color titleColor = isVictory ? Theme.Gold : Theme.Primary;
            GUI.backgroundColor = titleColor;
            GUI.Box(new Rect(px, py, pw, 4 * s), "");
            GUI.backgroundColor = Color.white;

            // 标题
            Theme.DrawTitle(new Rect(px, py + 15 * s, pw, 45 * s), _resultsTitle, (int)(30 * s));

            // 结果信息
            GUI.Label(new Rect(px + 20 * s, py + 65 * s, pw - 40 * s, 30 * s),
                _resultsMessage,
                Theme.MakeLabel((int)(16 * s), FontStyle.Normal, Theme.TextLight, TextAnchor.MiddleCenter));

            // 战斗记录
            GUI.Label(new Rect(px + 20 * s, py + 100 * s, pw - 40 * s, 20 * s),
                "战斗记录:", Theme.MakeLabel((int)(13 * s), FontStyle.Normal, Theme.TextDim));

            float logAreaH = Mathf.Min(120 * s, ph - 220 * s);
            _resultsScroll = GUI.BeginScrollView(
                new Rect(px + 20 * s, py + 120 * s, pw - 40 * s, logAreaH),
                _resultsScroll,
                new Rect(0, 0, pw - 60 * s, _resultsLog.Count * 20 * s));

            for (int i = 0; i < _resultsLog.Count; i++)
            {
                Color logColor = _resultsLog[i].Contains("解锁") ? Theme.Gold :
                    _resultsLog[i].Contains("阵亡") ? Theme.HpRed : Theme.Parchment;
                GUI.Label(new Rect(5 * s, i * 20 * s, pw - 70 * s, 20 * s),
                    _resultsLog[i],
                    Theme.MakeLabel((int)(12 * s), FontStyle.Normal, logColor));
            }
            GUI.EndScrollView();

            // 按钮
            float btnY = py + ph - 55 * s;
            float btnW = 120 * s;
            float gap2 = 15 * s;
            float totalBtnW = btnW * 3 + gap2 * 2;
            float btnStartX = px + (pw - totalBtnW) / 2;

            // 重试（朱红）
            GUI.backgroundColor = Theme.PrimaryDark;
            if (GUI.Button(new Rect(btnStartX, btnY, btnW, 40 * s), "🔄 重试",
                Theme.MakeButton((int)(15 * s))))
            {
                AudioManager.PlaySfx(AudioManager.SfxClips.Click);
                RetryLevel();
            }
            GUI.backgroundColor = Color.white;

            // 下一关（金色，仅胜利且有关卡时）
            if (isVictory)
            {
                bool hasNext = _currentLevelIndex + 1 < _levelOrder.Count;
                GUI.enabled = hasNext;
                GUI.backgroundColor = Theme.Gold;
                if (GUI.Button(new Rect(btnStartX + btnW + gap2, btnY, btnW, 40 * s), "▶ 下一关",
                    Theme.MakeButton((int)(15 * s))))
                {
                    AudioManager.PlaySfx(AudioManager.SfxClips.Click);
                    string nextId = _levelOrder[_currentLevelIndex + 1];
                    GameState.UnlockLevel(nextId);
                    SelectLevel(nextId);
                }
                GUI.enabled = true;
            }

            // 返回关卡选择
            GUI.backgroundColor = Theme.BgCard;
            if (GUI.Button(new Rect(btnStartX + (btnW + gap2) * 2, btnY, btnW, 40 * s),
                "🏯 选关", Theme.MakeButton((int)(15 * s))))
            {
                AudioManager.PlaySfx(AudioManager.SfxClips.Click);
                _gamePhase = GamePhase.LevelSelect;
                CleanupBattle();
                if (GameManager.Instance != null)
                    GameManager.Instance.TransitionTo(GamePage.LevelSelect);
            }
            GUI.backgroundColor = Color.white;
        }

        private void RetryLevel()
        {
            string levelId = _currentLevel?.levelId;
            if (string.IsNullOrEmpty(levelId)) return;
            CleanupBattle();
            SelectLevel(levelId);
        }

        private void CleanupBattle()
        {
            // 销毁所有单位可视化
            foreach (var kv in _unitVisuals)
            {
                if (kv.Value != null)
                    Destroy(kv.Value.gameObject);
            }
            _unitVisuals.Clear();
            _allUnits.Clear();
            _turnManager = null;
            _victoryChecker = null;
            _selectedUnit = null;
            _selectedSkillId = null;
            _isAnimating = false;
            _inDuel = false;
            _duelSystem = null;
        }

        /// <summary>强制失败（暂停菜单→撤退时调用）</summary>
        public void ForceDefeat(string reason)
        {
            if (_turnManager == null) return;
            _victoryChecker ??= new VictoryChecker(_currentLevel, _allUnits, _turnManager);
            // 标记失败
            _turnManager.SetPhase(TurnManager.TurnPhase.Defeat);
        }

        // ========== 自定义胜负检查（VictoryChecker回调）==========

        private void OnCustomVictoryCheck()
        {
            _victoryChecker?.Check();
            if (_victoryChecker != null && _victoryChecker.IsVictory)
            {
                _turnManager.SetPhase(TurnManager.TurnPhase.Victory);
            }
            else if (_victoryChecker != null && _victoryChecker.IsDefeat)
            {
                _turnManager.SetPhase(TurnManager.TurnPhase.Defeat);
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
                GameLogger.LogInfo(LogCategory.Battle, splashLog);
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
                    int exp = CombatCalculator.CalcExp(attacker.Level - splashTarget.Level, true, false, attacker);
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
            if (unit.Faction == Faction.Player)
            {
                // ★ 被动：仁德（每回合恢复全队5%HP）
                if (unit.HasPassiveType("team_heal_pct"))
                {
                    float pct = unit.GetPassiveModifier("team_heal_pct");
                    foreach (var ally in _allUnits.Where(u => u.Faction == Faction.Player && u.IsAlive))
                    {
                        int heal = Mathf.Max(1, (int)(ally.MaxHp * pct));
                        ally.Heal(heal);
                    }
                    _battleUI?.ShowTip($"{unit.Name} 的【仁德】恢复全队HP");
                }
                // ★ 被动：再生（每回合恢复15%HP）
                if (unit.HasPassiveType("self_heal_pct"))
                {
                    int heal = Mathf.Max(1, (int)(unit.MaxHp * unit.GetPassiveModifier("self_heal_pct")));
                    unit.Heal(heal);
                }
                _battleUI?.ShowTip($"请操作 {unit.Name}");
            }
            else if (unit.Faction == Faction.Enemy)
            {
                // 敌方也触发再生
                if (unit.HasPassiveType("self_heal_pct"))
                {
                    int heal = Mathf.Max(1, (int)(unit.MaxHp * unit.GetPassiveModifier("self_heal_pct")));
                    unit.Heal(heal);
                }
                _battleUI?.ShowTip($"敌方 {unit.Name} 行动...");
                StartCoroutine(EnemyAI(unit));
            }
        }

        private void OnUnitTurnEnd(BattleUnit unit)
        {
            // ★ 被动：决断（20%概率再次行动）
            if (unit.Faction == Faction.Player && unit.HasPassiveType("extra_turn_chance"))
            {
                float chance = unit.GetPassiveModifier("extra_turn_chance");
                if (UnityEngine.Random.Range(0f, 1f) < chance)
                {
                    unit.State = UnitState.Ready;
                    unit.HasActed = false;
                    _battleUI?.ShowTip($"{unit.Name} 的【决断】触发！可以再次行动！");
                }
            }
        }

        private void OnPhaseChanged(TurnManager.TurnPhase phase)
        {
            switch (phase)
            {
                case TurnManager.TurnPhase.PlayerTurn:
                    // ★ 回合开始修复可能的重叠
                    CheckOverlapAt("PlayerTurn开始前");
                    ResolveUnitOverlaps();
                    CheckOverlapAt("PlayerTurn-ResolveUnitOverlaps后");
                    // 刷新所有单位的视觉位置
                    foreach (var kv in _unitVisuals)
                        kv.Value.UpdatePosition();
                    _battleUI?.UpdateTurnInfo(_turnManager.TurnNumber, "玩家回合");
                    // 回合开始时自动存档
                    if (_turnManager.TurnNumber == 1 || _turnManager.TurnNumber % 3 == 0)
                        AutoSaveGame();
                    break;
                case TurnManager.TurnPhase.EnemyTurn:
                    CheckOverlapAt("EnemyTurn开始前");
                    _battleUI?.UpdateTurnInfo(_turnManager.TurnNumber, "敌方回合");
                    break;
                case TurnManager.TurnPhase.Victory:
                    _battleUI?.UpdateTurnInfo(_turnManager.TurnNumber, "🎉 胜利！");
                    ShowResults(true);
                    break;
                case TurnManager.TurnPhase.Defeat:
                    _battleUI?.UpdateTurnInfo(_turnManager.TurnNumber, "💀 失败");
                    ShowResults(false);
                    break;
            }
        }

        /// <summary>显示结算界面（含存档+关后剧情）</summary>
        private void ShowResults(bool isVictory)
        {
            _resultsTitle = isVictory ? "🎉 胜利！" : "💀 战败";
            _resultsMessage = _victoryChecker?.ResultMessage
                ?? (isVictory ? "战斗结束" : "战斗失败");

            // 结算BGM
            AudioManager.PlayBgm(isVictory ? AudioManager.BgmClips.Victory : AudioManager.BgmClips.Defeat);

            if (isVictory)
            {
                // 胜利时自动存档
                AutoSaveGame();

                // 检查是否有关后剧情
                string postStoryId = $"story_{_currentLevel?.levelId}_post";
                if (StoryLibrary.Get(postStoryId) != null && _dialogueUI != null)
                {
                    _waitingForDialogue = true;
                    GameManager.Instance.EnterStory();
                    _dialogueUI.PlayStory(postStoryId, () =>
                    {
                        _waitingForDialogue = false;
                        GameManager.Instance.ExitStory();
                        ShowResultsScreen(isVictory);
                    });
                    return;
                }
            }

            ShowResultsScreen(isVictory);
        }

        /// <summary>显示结算界面（实际渲染）</summary>
        private void ShowResultsScreen(bool isVictory)
        {
            // 收集战斗记录（存活角色统计）
            _resultsLog.Clear();
            var alivePlayers = _allUnits.Where(u => u.Faction == Faction.Player && u.IsAlive).ToList();
            var deadPlayers = _allUnits.Where(u => u.Faction == Faction.Player && u.IsDead).ToList();

            _resultsLog.Add($"战斗结束于第 {_turnManager.TurnNumber} 回合");
            _resultsLog.Add($"存活: {alivePlayers.Count} 阵亡: {deadPlayers.Count}");

            foreach (var u in _allUnits.Where(u => u.Faction == Faction.Player))
            {
                string status = u.IsAlive ? $"HP:{u.CurrentHp}/{u.MaxHp}" : "💀 阵亡";
                _resultsLog.Add($"  {u.Name} Lv.{u.Level} {status} 武{u.Strength} 经验{u.Experience}/{u.ExpToNextLevel()}");
            }

            // 胜利时解锁下一关
            if (isVictory)
            {
                int nextIdx = _currentLevelIndex + 1;
                if (nextIdx < _levelOrder.Count)
                {
                    GameState.UnlockLevel(_levelOrder[nextIdx]);
                    string nextName = LevelLibrary.Get(_levelOrder[nextIdx])?.name ?? _levelOrder[nextIdx];
                    _resultsLog.Add($"");
                    _resultsLog.Add($"📢 解锁下一关: {nextName}");

                    // 设置下一关的关前剧情
                    string nextStoryId = $"story_{_levelOrder[nextIdx]}_pre";
                    if (StoryLibrary.Get(nextStoryId) != null)
                        GameState.PendingStoryId = nextStoryId;
                }
                else
                {
                    _resultsLog.Add("");
                    _resultsLog.Add($"🏆 已通关所有关卡！");
                }

                // 发放通关奖励
                ApplyLevelRewards();
            }

            _gamePhase = GamePhase.Results;
            if (GameManager.Instance != null)
                GameManager.Instance.TransitionTo(GamePage.Results);
        }

        /// <summary>发放关卡通关奖励（金币 + 装备自动分配）</summary>
        private void ApplyLevelRewards()
        {
            if (_currentLevel == null) return;

            var save = GameState.CurrentSave;
            if (save == null)
            {
                save = SaveData.CreateNew();
                GameState.CurrentSave = save;
            }
            if (save.inventoryEquipIds == null)
                save.inventoryEquipIds = new List<string>();

            // 金币奖励
            if (_currentLevel.rewardGold > 0)
            {
                save.gold += _currentLevel.rewardGold;
                _resultsLog.Add("");
                _resultsLog.Add($"💰 获得金钱：{_currentLevel.rewardGold}（持有：{save.gold}）");
            }

            // 装备奖励
            var alivePlayers = _allUnits.Where(u => u.Faction == Faction.Player && u.IsAlive).ToList();
            foreach (var equipId in _currentLevel.rewardEquipIds)
            {
                var equip = EquipmentLibrary.Get(equipId);
                if (equip == null)
                {
                    _resultsLog.Add($"⚠ 未知装备奖励：{equipId}");
                    continue;
                }

                // 优先尝试装备给主角李世民
                var target = alivePlayers.FirstOrDefault(u => u.Id == "lishimin" && CanAutoEquip(u, equip));
                // 否则找任意可装备且对应槽位空缺的单位
                if (target == null)
                    target = alivePlayers.FirstOrDefault(u => CanAutoEquip(u, equip));

                if (target != null)
                {
                    target.Equip(equipId);
                    _resultsLog.Add($"🎁 {target.Name} 装备 {equip.name}");
                }
                else
                {
                    save.inventoryEquipIds.Add(equipId);
                    _resultsLog.Add($"📦 获得未分配装备：{equip.name}（已存入仓库）");
                }
            }

            // 奖励后再次存档，确保金币和装备变更被记录
            AutoSaveGame();
        }

        private bool CanAutoEquip(BattleUnit unit, EquipmentData equip)
        {
            if (!equip.CanEquip(unit)) return false;
            return equip.type switch
            {
                EquipmentType.Weapon => string.IsNullOrEmpty(unit.WeaponId),
                EquipmentType.Armor => string.IsNullOrEmpty(unit.ArmorId),
                EquipmentType.Trinket => string.IsNullOrEmpty(unit.TrinketId),
                _ => false
            };
        }

        /// <summary>自动存档</summary>
        public void AutoSaveGame()
        {
            if (_currentLevel == null) return;
            var party = _playerParty.Count > 0 ? _playerParty :
                _allUnits.Where(u => u.Faction == Faction.Player).ToList();
            if (party.Count == 0) return;

            var saveData = SaveManager.BuildSaveData(
                _currentLevel.levelId,
                _currentLevelIndex,
                _currentLevel.name,
                party,
                GameState.GetAllUnlocked()
            );
            SaveManager.AutoSave(saveData);
            GameState.CurrentSave = saveData;
        }

        private void HighlightActingUnit(BattleUnit unit, bool selected)
        {
            if (_unitVisuals.TryGetValue(unit, out var vis))
                vis.SetSelected(selected);
        }

        // ========== AI行为树驱动 ==========

        private IEnumerator EnemyAI(BattleUnit enemy)
        {
            if (!enemy.IsAlive)
            {
                _turnManager.EndUnitAction();
                yield break;
            }

            // 锁定输入并高亮当前行动单位，让玩家看清是谁在行动
            _isAnimating = true;
            HighlightActingUnit(enemy, true);
            yield return new WaitForSeconds(0.5f);

            CheckOverlapAt($"EnemyAI-{enemy.Name}行动前");
            var action = _aiTree.Decide(enemy);
            // 结构化 AI 决策日志已在 AIBehaviorTree.Decide 中统一输出

            switch (action.Type)
            {
                case AIActionType.Attack:
                    yield return StartCoroutine(ExecuteAIAttack(enemy, action.TargetUnit));
                    break;

                case AIActionType.UseSkill:
                    yield return StartCoroutine(ExecuteAISkill(enemy, action));
                    break;

                case AIActionType.Move:
                case AIActionType.Retreat:
                    // ★ AI移动不与其他单位重叠
                    var blocker = _allUnits.FirstOrDefault(u => u != enemy && u.IsAlive && u.Position == action.TargetCell);
                    if (blocker != null)
                    {
                        GameLogger.LogWarningFormat(LogCategory.AI, "AI移动被阻挡|单位={0}|目标=({1},{2})|阻挡={3}[{4}]",
                            enemy.Name, action.TargetCell.q, action.TargetCell.r, blocker.Name, blocker.Faction);
                        _battleUI?.ShowTip($"{enemy.Name} 目标被占据，原地待命");
                        yield return new WaitForSeconds(0.4f);
                        break;
                    }
                    yield return StartCoroutine(MoveUnitAnimation(enemy, action.TargetCell));
                    _battleUI?.ShowTip($"{enemy.Name} {action.Reason}");
                    break;

                case AIActionType.Skip:
                    GameLogger.LogWarningFormat(LogCategory.AI, "AI跳过|单位={0}|原因={1}", enemy.Name, action.Reason);
                    yield return new WaitForSeconds(0.5f);
                    break;
            }

            HighlightActingUnit(enemy, false);
            CheckOverlapAt($"EnemyAI-{enemy.Name}行动后");
            _turnManager.EndUnitAction();
            _isAnimating = false;
        }

        private IEnumerator ExecuteAIAttack(BattleUnit attacker, BattleUnit defender)
        {
            if (defender == null || defender.IsDead) yield break;

            yield return new WaitForSeconds(0.3f);

            CheckOverlapAt($"ExecuteAIAttack-{attacker.Name}攻击前");
            GameLogger.LogInfoFormat(LogCategory.Battle, "AI攻击发起|攻={0}@({1},{2})|防={3}@({4},{5})",
                attacker.Name, attacker.Position.q, attacker.Position.r,
                defender.Name, defender.Position.q, defender.Position.r);
            // 检查攻击者和被攻击者是否在同一位置
            if (attacker.Position == defender.Position)
            {
                GameLogger.LogErrorFormat(LogCategory.Battle, "AI攻击发起时重叠|攻={0}|防={1}|位置=({2},{3})",
                    attacker.Name, defender.Name, attacker.Position.q, attacker.Position.r);
            }

            var attackerTerrain = _hexView.Grid.GetTerrain(attacker.Position);
            var result = CombatCalculator.CalcPhysicalDamage(attacker, defender, 0, 0, attackerTerrain);
            defender.TakeDamage(result.damage);

            string log = $"AI {attacker.Name} 攻击 {defender.Name}：{(result.isHit ? $"造成{result.damage}伤害" : "未命中")}";
            if (result.isCrit) log += "【暴击】";
            GameLogger.LogInfoFormat(LogCategory.Battle,
                "AI战斗结果|攻={0}|防={1}|伤害={2}|命中={3}|暴击={4}|防御者HP={5}/{6}",
                attacker.Name, defender.Name, result.damage, result.isHit, result.isCrit,
                defender.CurrentHp, defender.MaxHp);
            _battleUI?.ShowTip(log);

            if (_unitVisuals.TryGetValue(defender, out var defVis))
            {
                defVis.UpdateHpBar();
                yield return StartCoroutine(FlashEffect(defVis));
            }

            // ★ 器械AOE溅射（AI方也用）
            if (result.isHit && result.damage > 0)
            {
                ApplySiegeSplash(attacker, defender.Position, result.damage);
            }

            yield return new WaitForSeconds(0.3f);

            if (defender.IsDead)
            {
                GameLogger.LogInfoFormat(LogCategory.Battle, "单位阵亡|单位={0}|阵营={1}", defender.Name, defender.Faction);
                if (_unitVisuals.TryGetValue(defender, out var deadVis))
                    StartCoroutine(DeathAnimation(deadVis, defender));

                // AI 经验分配
                int exp = CombatCalculator.CalcExp(attacker.Level - defender.Level, true, false, attacker);
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
                GameLogger.LogInfo(LogCategory.Battle, expLog);

                // ★ AI击杀后立即检查胜负
                if (_victoryChecker != null && !_victoryChecker.IsVictory && !_victoryChecker.IsDefeat)
                {
                    _victoryChecker.Check();
                    if (_victoryChecker.IsDefeat || _victoryChecker.IsVictory)
                    {
                        _turnManager.SetPhase(_victoryChecker.IsVictory
                            ? TurnManager.TurnPhase.Victory : TurnManager.TurnPhase.Defeat);
                        yield break;
                    }
                }
            }
        }

        private IEnumerator ExecuteAISkill(BattleUnit caster, AIAction action)
        {
            if (action.Skill == null) yield break;

            yield return new WaitForSeconds(0.3f);

            string log = _skillExecutor.Execute(action.Skill, caster, action.TargetCell);
            GameLogger.LogInfoFormat(LogCategory.Battle, "AI计策|单位={0}|计策={1}|结果={2}", caster.Name, action.Skill.name, log);
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

            yield return new WaitForSeconds(0.4f);

            // 检查阵亡
            var deadUnits = _allUnits.Where(u => u.IsDead).ToList();
            foreach (var du in deadUnits)
            {
                if (_unitVisuals.TryGetValue(du, out var deadVis))
                {
                    StartCoroutine(DeathAnimation(deadVis, du));
                }
            }

            // ★ AI计策击杀后立即检查胜负
            if (deadUnits.Count > 0 && _victoryChecker != null && !_victoryChecker.IsVictory && !_victoryChecker.IsDefeat)
            {
                _victoryChecker.Check();
                if (_victoryChecker.IsVictory || _victoryChecker.IsDefeat)
                {
                    _turnManager.SetPhase(_victoryChecker.IsVictory
                        ? TurnManager.TurnPhase.Victory : TurnManager.TurnPhase.Defeat);
                    yield break;
                }
            }
        }
    }
}
