# 贞观勇士 开发规范与教训

## 构建与调试
- **不要使用 uGUI**：在无 Unity Editor 环境下无法调试 uGUI（EventSystem/targetGraphic/Canvas 问题），所有 UI 使用 OnGUI
- **OnGUI 调试技巧**：调试信息直接画在屏幕上，不要依赖 `Debug.Log`（手机上看不到）
- **编译错误修复**：先确认花括号平衡（`grep -c "{"` vs `grep -c "}"`），再查 using 缺失

## 常见 Bug 模式

### 集合操作
- **先算后加**：`InitHeroPool` 中 `_heroPool.Add()` 之后计算 `_heroSelected` 会导致 index out of range。先在局部变量算出结果再 Add 到两个集合

### UI 布局
- **缩放基准**：竖屏用 `min(SW/1080, SH/1920)`，横屏用 `min(SW/1920, SH/1080)`。用错了会导致 UI 缩小到不可见
- **固定坐标**：OnGUI 用绝对坐标更可靠（`GUI.Box(new Rect(x, y, w, h), "")`），避免 ScrollView（坐标难调试）
- **GUI.Button 触控**：OnGUI 的 GUI.Button 自带触控支持，不需要额外设置

### 页面切换 (GameManager)
- **组件生命周期**：`SetAllEnabled(false)` 后切换页面，再用 `enabled = true` 恢复。在 `OnGUI()` 开头强制同步 `GameManager.CurrentPage` 防止阶段错乱
- **TransitionTo 失败**：检查调用方是否有异常被吞（用 try-catch 显示在屏幕上）
- **SetAllEnabled 覆盖所有组件**：新增组件必须加入 SetAllEnabled，否则页面切换后旧页面残留

### 编译错误
- **CS0106 (private invalid)**：花括号错位，类体内有孤立代码。检查 `grep -n "private void\|public void"` 是否是方法嵌套
- **CS0246 (type not found)**：`using` 缺失。添加对应 namespace
- **CS8967 (multiline interpolated string)**：C#9 不支持跨行 `$""`，拆成变量拼接到一行
- **CS1504 (source file not found)**：Windows 路径编码问题，用 PowerShell 编译替代 bash
