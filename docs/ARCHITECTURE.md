# 贞观勇士 技术架构文档 v0.1（MVP）

## 1. 技术选型

| 层面 | 选型 | 理由 |
|---|---|---|
| 引擎 | Unity 2022.3 LTS | 2D战棋案例丰富、Asset Store素材多、Android打包成熟 |
| 语言 | C# | Unity标准、性能好、AI辅助开发友好 |
| 渲染 | URP 2D | 像素/低多边形风格渲染效率高 |
| 数据存储 | JSON + PlayerPrefs | 轻量，无需后端；存档用JSON序列化 |
| 地图编辑 | Tiled Map Editor | 开源、Unity插件Tiled2Unity无缝对接 |
| 版本管理 | Git + Git LFS | LFS管理二进制资源（贴图/音效） |
| CI/CD | GitHub Actions | 自动打包APK、自动测试 |

## 2. 架构分层

```
┌──────────────────────────────────────┐
│            表现层 (View)              │  Unity MonoBehaviour / UI Canvas
├──────────────────────────────────────┤
│           逻辑层 (Logic)              │  纯C#类，无Unity依赖，可单元测试
├──────────────────────────────────────┤
│           数据层 (Data)               │  JSON配置表 + 存档序列化
├──────────────────────────────────────┤
│           引擎层 (Engine)             │  Unity API封装、资源加载、音频
└──────────────────────────────────────┘
```

核心原则：**逻辑层零Unity依赖**，所有MonoBehaviour仅做"胶水"——接收逻辑层事件，驱动表现层。

## 3. 核心模块

### 3.1 战棋引擎 (BattleEngine)
- HexGrid：六边形网格数据结构，坐标用Cube坐标系
- PathFinder：A*寻路，支持地形消耗、友军阻挡
- TurnManager：回合控制器（己方→敌方→事件→胜利判定）
- BattleUnit：战斗单位状态机（待机/移动/攻击/计策/休息）
- TerrainSystem：地形效果（移动力消耗、防御加成、命中率修正）

### 3.2 战斗系统 (CombatSystem)
- DamageCalculator：伤害公式、暴击、兵种克制倍率
- HitCalculator：命中率、回避率、地形修正
- SkillExecutor：计策执行（AOE选格、效果施加）
- DuelSystem：单挑判定（武力对比 + 随机事件）
- StatusEffectManager：增益/减益状态管理

### 3.3 AI系统 (AISystem)
- AIController：每回合决策入口
- BehaviorTree：行为树（攻击优先/占位/撤退/计策）
- ThreatMap：威胁热力图，评估移动目标价值
- DifficultyAdjuster：难度参数（MVP仅Normal）

### 3.4 角色系统 (CharacterSystem)
- CharacterData：角色基础属性（武/统/智/敏/运 + 性别 + 阵营类型）
- ClassType：兵种（步兵/重步/骑兵/弓兵/器械/谋士），转职树
- CharacterRole：角色定位（君主/武将/谋士/女性），影响 UI 和事件触发
- Equipment：武器/防具/饰品，部分装备性别/职业限定
- SkillTree：技能习得（等级触发 + 选择分支）
- ExperienceSystem：经验计算、升级属性成长
- BondSystem：羁绊系统（多角色同阵触发额外属性加成）

### 3.5 剧情系统 (StorySystem)
- DialogPlayer：对话播放器（立绘 + 文本 + 选项）
- EventTrigger：关卡内事件触发（回合数/位置/HP阈值）
- StoryBranch：分支记录（影响后续关卡走向）
- Cinematic：简易过场（平移 + 淡入淡出）

### 3.6 关卡系统 (LevelSystem)
- LevelData：关卡配置（地图、敌军部署，胜利/失败条件）
- LevelLoader：从Tiled导出数据加载地图
- DeploymentPhase：战前部署（选将/摆位）
- VictoryChecker：胜负条件实时判定

### 3.7 存档系统 (SaveSystem)
- SaveData：全局存档（进度、武将状态、装备、剧情分支）
- AutoSave：每关结束自动存档
- ManualSave：5个手动槽位
- SaveManager：序列化/反序列化 + 版本迁移

### 3.8 资源管理 (ResourceManager)
- AssetLoader：Addressables异步加载（贴图/预制体/音效）
- SpriteManager：精灵图集管理（武将立绘/兵种小人/地形块）
- AudioPlayer：BGM/SFX播放（对象池复用AudioSource）

## 4. 数据驱动设计

所有游戏数据存JSON配置表，代码只读不写：

```
Assets/
├── Data/
│   ├── Characters/      # 武将属性表
│   ├── Classes/         # 兵种定义
│   ├── Equipment/       # 装备表
│   ├── Skills/          # 技能表
│   ├── Levels/          # 关卡配置
│   ├── Terrain/         # 地形效果
│   └── Story/           # 剧情脚本
├── Maps/                # Tiled地图文件
├── Sprites/             # 精灵图集
└── Audio/               # 音效/BGM
```

示例 - 武将配置 `Characters/lishimin.json`：
```json
{
  "id": "lishimin",
  "name": "李世民",
  "role": "monarch",
  "gender": "male",
  "class": "cavalry",
  "baseStats": { "str": 82, "cmd": 95, "int": 88, "agi": 78, "luk": 90 },
  "growth":  { "str": 4,  "cmd": 5,  "int": 4,  "agi": 3,  "luk": 4  },
  "skills": ["charge", "rally", "ambush"],
  "bonds": ["zhangsun_empress", "li_jing"],
  "portrait": "Sprites/Portraits/lishimin"
}
```

示例 - 谋士配置 `Characters/fang_xuanling.json`：
```json
{
  "id": "fang_xuanling",
  "name": "房玄龄",
  "role": "strategist",
  "gender": "male",
  "class": "strategist",
  "baseStats": { "str": 25, "cmd": 70, "int": 92, "agi": 60, "luk": 80 },
  "growth":  { "str": 1,  "cmd": 3,  "int": 5,  "agi": 3,  "luk": 4  },
  "skills": ["fire_attack", "rally", "insight", "logistics"],
  "passive": "战前增加资金20%，全队经验+10%",
  "portrait": "Sprites/Portraits/fang_xuanling"
}
```

示例 - 女性角色配置 `Characters/pingyang_princess.json`：
```json
{
  "id": "pingyang_princess",
  "name": "平阳公主",
  "role": "female",
  "gender": "female",
  "class": "cavalry",
  "baseStats": { "str": 70, "cmd": 85, "int": 75, "agi": 80, "luk": 85 },
  "growth":  { "str": 4,  "cmd": 4,  "int": 3,  "agi": 4,  "luk": 4  },
  "skills": ["charge", "niangzi_army", "rally"],
  "passive": "娘子军：全场女性角色攻击+20%",
  "bonds": ["chai_shao"],
  "portrait": "Sprites/Portraits/pingyang"
}
```

## 5. 项目目录结构（Unity）

```
zhenguanWarriors/
├── Assets/
│   ├── Scripts/
│   │   ├── Core/              # 逻辑层（纯C#，无Unity依赖）
│   │   │   ├── Battle/        # 战棋引擎
│   │   │   ├── Combat/        # 战斗计算
│   │   │   ├── AI/            # 人工智能
│   │   │   ├── Character/     # 角色系统
│   │   │   ├── Story/         # 剧情系统
│   │   │   ├── Level/         # 关卡系统
│   │   │   └── Save/          # 存档系统
│   │   ├── View/              # 表现层（MonoBehaviour）
│   │   │   ├── BattleView/    # 战斗场景UI
│   │   │   ├── MapView/       # 大地图场景
│   │   │   ├── MenuView/      # 主菜单
│   │   │   └── DialogView/    # 对话UI
│   │   ├── Engine/            # 引擎封装层
│   │   │   ├── AssetLoader.cs
│   │   │   ├── AudioPlayer.cs
│   │   │   └── SpriteManager.cs
│   │   └── Utils/             # 工具类
│   ├── Data/                  # JSON配置表
│   ├── Maps/                  # Tiled地图
│   ├── Sprites/               # 精灵图集
│   ├── Audio/                 # 音效BGM
│   ├── Prefabs/               # 预制体
│   └── Scenes/                # 场景文件
│       ├── MainMenu.unity
│       ├── WorldMap.unity
│       └── Battle.unity
├── Packages/
└── ProjectSettings/
```

## 6. 场景设计

仅 3 个场景，通过脚本控制流程：

| 场景 | 职责 |
|---|---|
| MainMenu | 主菜单、设置、存档选择 |
| WorldMap | 大地图选关、武将编组、装备调整 |
| Battle | 战斗场景（加载不同关卡数据复用） |

## 7. 关键技术方案

### 7.1 六边形网格
- 坐标系：Cube (q, r, s)，q+r+s=0
- 像素转换：flat-top六边形，size=32px
- 邻居计算：6方向偏移表
- 距离：max(abs(q1-q2), abs(r1-r2), abs(s1-s2))

### 7.2 A* 寻路
- 开放列表：优先队列（最小堆）
- 启发函数：六边形距离
- 代价：基础1 + 地形额外消耗
- 友军阻挡：不可穿越；敌方：可穿越但消耗+2

### 7.3 战斗AI行为树
```
Selector（根）
├── Sequence（危急撤退）
│   ├── HP<25%?
│   └── 移向最近友军
├── Sequence（计策优先）
│   ├── 有高价值AOE目标?
│   └── 施放计策
├── Sequence（攻击最弱）
│   ├── 范围内有敌人?
│   └── 攻击HP最低目标
└── Sequence（向目标移动）
    ├── 距离目标>攻击范围?
    └── 移向目标
```

### 7.4 存档序列化
- 使用 Newtonsoft.Json（Unity内置）
- 存档结构：版本号 + 全局状态 + 各武将状态 + 剧情分支标记
- 向后兼容：读取时按版本号逐步迁移

## 8. 性能目标

| 指标 | 目标值 |
|---|---|
| 帧率 | 中端机稳定60fps |
| 战斗场景Draw Call | ≤50 |
| 内存占用 | ≤300MB |
| 包体大小 | ≤150MB |
| 场景切换 | ≤2秒 |
| 存档读写 | ≤500ms |

## 9. 第三方依赖（MVP）

| 库 | 用途 | 许可证 |
|---|---|---|
| Tiled2Unity | Tiled地图导入 | MIT |
| Newtonsoft.Json | JSON序列化 | MIT |
| DOTween | 动画缓动 | MIT |
| Unity Addressables | 资源异步加载 | Unity Compose |

## 10. 开发环境

- Unity 2022.3 LTS + Visual Studio / VS Code
- Tiled Map Editor 1.10+
- Git + Git LFS
- Android SDK 33+、Min API 24（Android 7.0）
- 目标分辨率：1080×2400（横屏适配 1920×1080）

## 11. 构建与发布

- Debug包：Development Build + Script Debugging
- Release包：IL2CPP + Strip Engine Code + ProGuard
- 签名：Release keystore
- 渠道：先APK直装，后期考虑TapTap/Google Play

## 12. 版本记录
- v0.1（2026-05-31）初稿，对应 MVP 架构
- v0.2（2026-05-31）新增谋士兵种、角色定位（君主/武将/谋士/女性）、羁绊系统