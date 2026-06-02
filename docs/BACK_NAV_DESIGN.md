# 贞观勇士 返回导航设计方案 v1.0

> 本文档定义 Android 返回键在各页面的行为、返回导航规则、以及暂停菜单设计。
> 审查通过后进入开发。

---

## 1. 设计原则

1. **不丢失进度** — 返回/退出时必须自动存档，确保下次继续时无缝衔接
2. **二次确认** — 涉及退出关卡/放弃进度的操作必须弹确认框
3. **可预测** — 每个页面的返回行为符合用户直觉（从哪来回哪去）
4. **统一管理** — 所有返回逻辑由 GameManager 统一处理，不在各页面分散

---

## 2. 各页面返回行为

### 2.1 总览表

| 页面 | 返回键行为 | 是否需要确认 | 存档行为 |
|------|-----------|-------------|---------|
| 启动画面 | 无反应（2.5s后自动转场） | — | — |
| 主菜单 | 弹窗"确认退出游戏？" | ✅ 确认框 | — |
| 设置 | 返回主菜单 | ❌ 直接返回 | — |
| 关卡选择 | 显示返回菜单（回主菜单/继续选关） | ✅ 底部弹窗 | — |
| 战前编组 | 返回关卡选择 | ❌ 直接返回 | — |
| 剧情对话 | 快进/跳过当前对话 | ❌ 直接跳过 | — |
| **战斗中** | **显示暂停菜单** 见第3节 | — | ⏺ 自动存档 |
| 战斗结算 | 无反应（已有按钮） | — | — |

### 2.2 各页面详细设计

#### 启动画面 (Splash)
- 返回键：**忽略**（防止用户在加载时误触退出）
- 屏幕触摸：同样忽略（自动转场）

#### 主菜单 (MainMenu)
- 返回键：弹出确认框 → "确认退出游戏？"
- 确认框样式：
  ```
  ┌─────────────────────────────┐
  │    退出游戏？                │
  │                             │
  │  退出后进度不会丢失，         │
  │  下次可从主菜单继续。        │
  │                             │
  │    [取消]     [确认退出]     │
  └─────────────────────────────┘
  ```
- 确认：`Application.Quit()`（编辑器下 `Debug.Break()`）

#### 设置 (Settings)
- 返回键：直接回到主菜单（等同于当前"← 返回"按钮）
- 无需确认

#### 关卡选择 (LevelSelect)
- 返回键：弹底部菜单
  ```
  ┌─────────────────────────────────────────┐
  │                                         │
  │    📋 选关菜单                          │
  │                                         │
  │    [▶ 继续选关]                         │
  │    [🏯 返回主菜单]                      │
  │    [💾 保存当前进度]                    │
  │                                         │
  └─────────────────────────────────────────┘
  ```
- 返回主菜单前自动存档（保存当前解锁进度）
- 继续选关 → 关闭弹窗

#### 战前编组 (PreBattle)
- 返回键：直接返回关卡选择
- 不做存档（还没开始战斗）
- 如果已调整装备 → 自动记住装备选择（不用重新配）

#### 剧情对话 (Story)
- 返回键：**快进到当前对话结束**
  - 非跳过整个剧情，而是加速文本显示
  - 如果有选项 → 停在选项处等待选择
- 设计考量：玩家可能已看过剧情想跳过，但不应完全跳过导致错过关键选项

#### 战斗结算 (Results)
- 返回键：**忽略**（已经有"重试/下一关/选关"三个按钮，不需要额外返回操作）

---

## 3. 战斗中暂停菜单（核心设计）

### 3.1 触发方式

| 平台 | 触发 |
|------|------|
| Android | 点击返回键(ESC) |
| PC(调试) | ESC键 或 右上角暂停按钮 |

### 3.2 暂停菜单界面

战斗画面正常渲染，上方叠加半透明遮罩 + 暂停菜单：

```
┌────────────────────────────────────────────┐
│░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░│
│░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░│
│░░░░░░░░░░  ⏸ 战 斗 暂 停  ░░░░░░░░░░░░░░░│
│░░░░░░░░░░                          ░░░░░░░░│
│░░░░░░░░░░  [▶ 继续战斗]             ░░░░░░░░│
│░░░░░░░░░░  [💾 保存并退出]          ░░░░░░░░│
│░░░░░░░░░░  [🏳 撤退（失败）]        ░░░░░░░░│
│░░░░░░░░░░  [🏯 返回主菜单]          ░░░░░░░░│
│░░░░░░░░░░                          ░░░░░░░░│
│░░░░░░░░░░  关卡: 晋阳举义 第5回合    ░░░░░░░░│
│░░░░░░░░░░                          ░░░░░░░░│
└────────────────────────────────────────────┘
```

### 3.3 各菜单项行为

| 按钮 | 行为 | 存档 |
|------|------|------|
| **继续战斗** | 关闭暂停菜单，恢复游戏 | — |
| **保存并退出** | 自动存档当前状态 → 返回主菜单 | ✅ 保留战场状态 |
| **撤退（失败）** | 弹窗确认后以失败结算 | ⏺ 不存档 |
| **返回主菜单** | 弹窗确认→返回主菜单（进度丢失） | ❌ 不存档 |

### 3.4 撤退/失败的收益设计

**原则**：撤退应有收益但不足以替代胜利，避免玩家通过反复撤退刷资源。

| 项目 | 撤退/失败时 | 胜利时 |
|------|-----------|-------|
| 已击杀经验 | ✅ **保留 50%**（角色已获得的经验不清零） | ✅ 100% 保留 |
| 已升级等级 | ✅ **保留**（降级体验极差，不做） | ✅ 保留 |
| 关卡解锁 | ❌ 不解锁下一关 | ✅ 解锁 |
| 角色状态 | ✅ 恢复到战前状态（不扣资源） | ✅ 保留战后状态 |
| 关卡进度 | ❌ 标记为未完成（可重打） | ✅ 标记为已完成 |

**设计理由**：
- 保留经验/等级：尊重玩家投入的时间，符合 PRD"失败惩罚低"原则
- 不解锁关卡：维持正常的难度曲线，防止跳关
- 恢复到战前状态：避免"残血撤退"反复刷经验的漏洞

### 3.4 保存并退出的存档内容

存档数据包含：
- 当前关卡 ID 和回合数
- 所有单位当前位置、HP/MP、状态
- 角色等级/经验/装备
- 已解锁关卡列表

### 3.5 继续游戏 → 恢复战场状态（精确恢复）

从"保存并退出"存档继续时：
1. 主菜单 → 继续游戏 → 加载存档
2. 检测到存档标记 `isInBattle = true`（有战场状态数据）
3. 自动进入该关卡，**完全恢复存档时的战场状态**：
   - 关卡、网格、地形 → 重建
   - 所有单位位置 → 还原
   - 所有单位 HP/MP/状态 → 精确恢复
   - 回合数 → 还原
   - 天气、风向 → 还原
4. 玩家从存档时的状态继续战斗

**数据需求**：扩展 `SaveData` 增加以下战场状态字段：
```csharp
public bool isInBattle;           // 是否在战斗中存档
public int turnNumber;            // 当前回合数
public WeatherType weather;       // 天气
public WindDirection wind;        // 风向
public List<BattleUnitSaveData> units;  // 所有单位完整状态
public List<TerrainChangeSave> terrainChanges; // 地形变化记录
```

---

## 4. 技术实现方案

### 4.1 Android 返回键检测

```csharp
// 在 GameManager.Update() 中统一检测
void Update()
{
    if (Input.GetKeyDown(KeyCode.Escape))
    {
        HandleBackButton();
    }
}
```

### 4.2 返回键分发

GameManager 根据 `CurrentPage` 分发到不同处理逻辑：

```csharp
void HandleBackButton()
{
    if (IsTransitioning) return; // 过渡中不处理

    switch (CurrentPage)
    {
        case GamePage.Splash:     break; // 忽略
        case GamePage.MainMenu:   ShowExitConfirm(); break;
        case GamePage.Settings:   TransitionTo(MainMenu); break;
        case GamePage.LevelSelect: ShowLevelSelectMenu(); break;
        case GamePage.PreBattle:  TransitionTo(LevelSelect); break;
        case GamePage.Story:      FastForwardDialogue(); break;
        case GamePage.Battle:     ShowPauseMenu(); break;
        case GamePage.Results:    break; // 忽略
    }
}
```

### 4.3 暂停菜单（战斗专用）

战斗中的暂停是一个**叠加层**，不影响 BattleTestController 的组件状态：

```csharp
public class PauseMenu : MonoBehaviour
{
    private bool _isOpen;
    public bool IsOpen => _isOpen;

    public void Open() { _isOpen = true; Time.timeScale = 0f; }
    public void Close() { _isOpen = false; Time.timeScale = 1f; }

    void OnGUI()
    {
        if (!_isOpen) return;
        // 绘制半透明遮罩 + 暂停菜单按钮
    }
}
```

注意：使用 `Time.timeScale = 0` 暂停所有协程和动画，但 OnGUI 仍然正常工作。

### 4.4 确认框组件

```csharp
public class ConfirmDialog : MonoBehaviour
{
    private string _title;
    private string _message;
    private string _confirmText;
    private string _cancelText;
    private System.Action _onConfirm;
    private System.Action _onCancel;
    private bool _isOpen;

    public void Show(string title, string message, 
        System.Action onConfirm, System.Action onCancel = null,
        string confirmText = "确认", string cancelText = "取消")
    { ... }

    void OnGUI()
    {
        if (!_isOpen) return;
        // 半透明遮罩 + 居中确认框
    }
}
```

### 4.5 暂停菜单的输入锁

暂停菜单打开时：
- `BattleTestController.Update()` 应跳过（`_isPaused` 检查）
- `BattleTestController.OnGUI()` 应跳过（暂停菜单独占渲染或叠加渲染）
- 实现方式：在 GameManager 中设置 `_isPaused = true`，BattleTestController 的 Update 和 OnGUI 检查此标志

---

## 5. 页面导航规则（更新版）

| 当前页面 | 返回行为 | 目标页面 | 存档 |
|---------|---------|---------|------|
| 主菜单 | 退出确认 | 退出APP | — |
| 设置 | 返回 | 主菜单 | — |
| 关卡选择 | 选关菜单 | 主菜单/继续 | ✅ |
| 战前编组 | 返回 | 关卡选择 | — |
| 剧情对话 | 快进 | 同一页 | — |
| 战斗中 | 暂停菜单 | 暂停/存档退出/撤退/回主菜单 | ✅/— |
| 结算 | 忽略 | — | — |

---

## 6. 实现步骤（待审查后执行）

1. **GameManager 新增**：
   - `Update()` 中检测 `KeyCode.Escape`
   - `HandleBackButton()` 分发逻辑
   - `_isPaused` 标志（战斗中暂停用）

2. **新建** `PauseMenu.cs`：
   - 暂停菜单 UI
   - `Time.timeScale` 控制
   - 继续/保存退出/撤退/回主菜单 四个按钮

3. **新建** `ConfirmDialog.cs`：
   - 通用确认框
   - 可配置标题/内容/按钮文字/回调

4. **修改 BattleTestController.cs**：
   - `Update()` 开头检查 `_isPaused`
   - `OnGUI()` 暂停时跳过战斗UI渲染

5. **修改 LevelSelectUI**：
   - 点击返回 → 显示选关菜单弹窗

---

## 7. 待审查问题

1. **战斗中保存进度策略**：当前方案为"保存关卡进度但回到战前编组重新打"，是否接受？
2. **暂停菜单使用 Time.timeScale = 0**：会影响 Animator 和 ParticleSystem，当前项目没有用到这些，可行。
3. **撤退的失败判定**：撤退后触发正常的失败结算流程（显示失败界面，可选择重试/选关）。
4. **剧情快进**：是直接跳到剧情结束，还是加速文本显示？建议加速显示，防止错过选项。
