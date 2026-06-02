# 贞观勇士 UX 交互设计方案 v1.0

> 本文档定义完整的用户交互流程、页面状态管理、过渡动画，以及技术实现方案。
> 审查通过后进入开发。

---

## 1. 设计原则

1. **单页面原则** — 任何时刻屏幕上只显示一个"页面"的内容，禁止重叠
2. **状态驱动** — 所有页面切换由一个全局状态机控制，不在页面间直接互相创建/销毁
3. **过渡保护** — 页面切换期间（≤1帧）不绘制任何 UI，防止闪烁
4. **一致性** — 所有页面使用统一缩放、色板、字体规范（见 PRD 第9章）

---

## 2. 页面清单与导航图

```
                    ┌─────────────────────┐
                    │     启动 APP         │
                    │  GameBootstrapper    │
                    └─────────┬───────────┘
                              │ AfterSceneLoad
                    ┌─────────▼───────────┐
                    │    启动画面           │ 2.5s
                    │  GamePhase.Splash    │ 自动过渡
                    └─────────┬───────────┘
                              │ 2.5s后自动
                    ┌─────────▼───────────┐
                    │    主菜单             │
                    │  GamePhase.MainMenu  │
                    │                     │
                    │  ┌─────────────┐    │
                    │  │ 新游戏 ⚔   │──────┼──→ GamePhase.LevelSelect
                    │  ├─────────────┤    │
                    │  │ 继续游戏 💾 │──────┼──→ 加载存档 → GamePhase.LevelSelect
                    │  ├─────────────┤    │
                    │  │ 设置 ⚙     │──────┼──→ GamePhase.Settings → 返回 MainMenu
                    │  └─────────────┘    │
                    └─────────────────────┘
                              │
                    ┌─────────▼───────────┐
                    │    关卡选择           │
                    │  GamePhase.LevelSel  │
                    │  卡片列表×8          │
                    │  锁定关卡灰色+🔒     │
                    │  点击解锁关卡→       │
                    └─────────┬───────────┘
                              │ 点击关卡卡片
                    ┌─────────▼───────────┐
                    │  关前剧情（可选）      │
                    │  GamePhase.Story     │
                    │  自动播放→           │
                    └─────────┬───────────┘
                              │ 剧情结束
                    ┌─────────▼───────────┐
                    │  战前编组             │
                    │  GamePhase.PreBattle │
                    │  调整装备→开始战斗    │
                    └─────────┬───────────┘
                              │ 点击"开始战斗"
                    ┌─────────▼───────────┐
                    │  战斗画面             │
                    │  GamePhase.Battle    │
                    │  回合制→胜负判定      │
                    └─────────┬───────────┘
                              │ 胜利/失败
                    ┌─────────▼───────────┐
                    │  关后剧情（可选）      │
                    │  GamePhase.Story     │
                    └─────────┬───────────┘
                              │ 剧情结束
                    ┌─────────▼───────────┐
                    │  战斗结算             │
                    │  GamePhase.Results   │
                    │  [重试] [下一关] [选关]│
                    └──┬──────┬──────┬────┘
                       │      │      │
          ┌────────────┘      │      └────────────┐
          ▼                   ▼                   ▼
    GamePhase.Battle    GamePhase.LevelSel   GamePhase.LevelSel
    (重试本关)          (下一关，推进度)     (返回，不推进度)
```

---

## 3. 页面状态机设计

### 3.1 GamePhase 枚举

```csharp
public enum GamePhase
{
    Splash,         // 启动画面
    MainMenu,       // 主菜单
    Settings,       // 设置（主菜单的子页面）
    LevelSelect,    // 关卡选择
    Story,          // 剧情对话
    PreBattle,      // 战前编组
    Battle,         // 战斗中
    Results         // 战斗结算
}
```

### 3.2 状态转换规则

| 当前状态 | 触发条件 | 下一状态 | 过渡动作 |
|---------|---------|---------|---------|
| Splash | 2.5s 倒计时结束 | MainMenu | 清除启动画面 |
| MainMenu | 点击"新游戏" | LevelSelect | 重置存档+关卡进度 |
| MainMenu | 点击"继续游戏" | LevelSelect | 加载存档 |
| MainMenu | 点击"设置" | Settings | — |
| Settings | 点击"返回" | MainMenu | — |
| LevelSelect | 点击解锁关卡 | Story (如有) / PreBattle | 重建网格+加载角色 |
| Story (关前) | 对话结束 | PreBattle | — |
| PreBattle | 点击"开始战斗" | Battle | 初始化战斗+AI+天气 |
| Battle | 胜负判定 | Story (如有) / Results | 自动存档 |
| Story (关后) | 对话结束 | Results | — |
| Results | 点击"重试" | PreBattle | 清理战斗状态 |
| Results | 点击"下一关" | LevelSelect | 解锁下一关+跳转 |
| Results | 点击"选关" | LevelSelect | 清理战斗状态 |

### 3.3 禁止的状态转换

- ❌ LevelSelect → MainMenu（关卡选择没有返回主菜单的按钮）
- ❌ Battle → PreBattle（战斗中不能回编组）
- ❌ Results → Battle（结算后不能直接回战斗）
- ❌ Story 期间不能切换状态（必须等到对话结束回调）

---

## 4. 当前问题分析

### 4.1 问题：页面重叠

**现象**：主菜单和关卡选择画面同时显示。

**根因**：页面切换时使用了 `Destroy(gameObject)` + `new GameObject(...)` 的模式。

```
当前代码：
  MainMenuController.SwitchToBattle()
    → Destroy(gameObject)        // Unity延迟到帧末才真正销毁
    → new GameObject("BattleScene")  // 立即创建
    → BattleSceneStarter.Awake()     // 立即附加组件
    → BattleTestController.Start()   // 下一帧执行

  同一帧内 OnGUI() 调用顺序：
    1. MainMenuController.OnGUI()  ← 虽然标记了Destroy，但帧末才移除
    2. BattleTestController.OnGUI() ← 新组件已经绘制
    → 两个页面同时渲染！
```

### 4.2 问题：多 GameObject 分散管理

当前代码中，不同页面分布在不同的 GameObject 上：
- 启动画面 → `GameObject("GameRoot")`
- 主菜单 → `GameObject("MainMenu")`
- 战斗场景 → `GameObject("BattleSystem")`

跨 GameObject 的组件通信困难，状态管理分散，容易产生悬挂引用。

### 4.3 问题：OnGUI 无统一入口

每个 MonoBehaviour 都有自己的 `OnGUI()`，Unity 按任意顺序调用它们。页面数量增加后无法保证绘制顺序正确。

---

## 5. 技术实现方案

### 5.1 统一 GameRoot 架构

```
GameObject("GameRoot")          ← 只存在一个根对象
├── SplashScreen                ← 启动画面（显示2.5s后自关闭）
├── MainMenuController          ← 主菜单（含设置页面）
├── BattleSceneStarter          ← 战斗场景
│   ├── HexGridView
│   ├── BattleTestController    ← 关卡选择/编组/战斗/结算
│   └── DialogueUI
└── AudioManager                ← 全局音频
```

所有组件**同时存在**，但通过 `enabled = true/false` 控制激活状态。

### 5.2 统一状态机

```csharp
// 在 GameRoot 上挂载一个 GameManager 组件，持有 GamePhase 状态
public class GameManager : MonoBehaviour
{
    public GamePhase CurrentPhase { get; private set; }

    public void TransitionTo(GamePhase newPhase)
    {
        _isTransitioning = true;      // 过渡锁
        CurrentPhase = newPhase;
        UpdateComponentStates();
        _isTransitioning = false;
    }
}
```

### 5.3 按状态开关组件

```csharp
void UpdateComponentStates()
{
    splashScreen.enabled = (phase == GamePhase.Splash);
    mainMenuController.enabled = (phase == GamePhase.MainMenu || phase == GamePhase.Settings);
    battleTestController.enabled = (phase >= GamePhase.LevelSelect);
    dialogueUI.enabled = (phase == GamePhase.Story);
    
    // 特定状态下禁用输入
    battleTestController.EnableInput = (phase == GamePhase.Battle);
}
```

### 5.4 OnGUI 过渡保护

所有 `OnGUI()` 方法添加：

```csharp
void OnGUI()
{
    if (GameManager.Instance.IsTransitioning) return; // 过渡期间不绘制
    if (!isActiveAndEnabled) return;  // 组件禁用时不绘制
    // ... 正常绘制代码
}
```

### 5.5 改造后的启动流程

```
GameBootstrapper
  → new GameObject("GameRoot")
  → GameManager (持有所有子组件引用)
  → 添加所有子组件（初始全部 disabled）
  → TransitionTo(Splash)

Splash (2.5s)
  → TransitionTo(MainMenu)

MainMenu → "新游戏"
  → SaveManager.ResetNewGame()
  → BattleTestController.InitLevelSelect()
  → TransitionTo(LevelSelect)

MainMenu → "继续游戏"
  → SaveManager.LoadLatest()
  → BattleTestController.InitLevelSelect()
  → TransitionTo(LevelSelect)

LevelSelect → 点击关卡
  → BattleTestController.SelectLevel(levelId)
  → 检查关前剧情→TransitionTo(Story) 或 TransitionTo(PreBattle)

Story → 结束回调
  → BattleTestController.InitPreBattle() 或 StartBattle()
  → TransitionTo(PreBattle) 或 TransitionTo(Battle)

PreBattle → 开始战斗
  → BattleTestController.StartBattle()
  → TransitionTo(Battle)

Battle → 胜负判定
  → AutoSaveGame()
  → 检查关后剧情→TransitionTo(Story) 或 TransitionTo(Results)

Results → 下一关/重试/选关
  → 相应清理 + TransitionTo(LevelSelect)
```

### 5.6 原有代码修改清单

| 文件 | 修改内容 |
|------|---------|
| `GameBootstrapper.cs` | 改为创建 GameRoot + GameManager，不再直接创任何页面 |
| `SplashScreen.cs` | 移除自动创建 MainMenu 的逻辑，改为回调 GameManager 切换状态 |
| `MainMenuController.cs` | 移除 `SwitchToBattle()` 和 `Destroy()`，改为调用 GameManager.TransitionTo() |
| `BattleTestController.cs` | 改为通过 enabled 控制激活，移除所有组件创建/销毁逻辑 |
| `BattleSceneStarter.cs` | 合并到 BattleTestController 或移除 |
| `GameManager.cs` | **新建**：统一状态机 + 组件生命周期管理 |

---

## 6. 实现步骤（待审查后执行）

1. **新建** `GameManager.cs` — 状态机 + 组件引用管理
2. **重构** `GameBootstrapper.cs` — 创建 GameRoot，挂载所有组件，初始 Splash
3. **重构** `SplashScreen.cs` — 移除自建 MainMenu，改用状态机回调
4. **重构** `MainMenuController.cs` — 移除 SwitchToBattle，调用 GameManager
5. **重构** `BattleTestController.cs` — 分离 GamePhase 依赖，去除自建组件逻辑
6. **移除** `BattleSceneStarter.cs` — 功能合并到 GameManager
7. **添加** 过渡保护 `_isTransitioning` 到所有 OnGUI
8. **测试** 全流程无重叠切换

---

## 7. 待审查问题

1. **一键启动 vs 场景分离**：当前方案保持单场景 + 组件 enabled 切换。如果未来需要多场景（主菜单场景、战斗场景），需要改为 SceneManager.LoadScene。建议 MVP 阶段保持单场景，上架前再评估。
2. **加载状态**：当前所有资源在内存中，切换无延迟。后续添加 BGM/立绘后可能需要 loading 界面。
3. **返回键**：Android 返回键当前未处理。建议在 GameManager 中拦截，根据当前状态决定行为（如战斗中提示"是否退出"）。
