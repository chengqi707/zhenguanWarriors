# 贞观勇士 文档索引

## 顶层文档
- [PRD.md](./PRD.md) —— 产品总览（目标、范围、优先级、风险；§15/§16 为 H5 迭代需求）
- [ARCHITECTURE.md](./ARCHITECTURE.md) —— 技术架构
- [ROADMAP.md](./ROADMAP.md) —— 4 阶段开发路线图

## H5 重制版（当前主线）
- [H5_ITERATION_SUMMARY.md](./H5_ITERATION_SUMMARY.md) —— **H5 开发迭代总结（全部需求与实现一览）**
- [H5_DESIGN.md](./H5_DESIGN.md) —— H5 技术设计与版本记录
- [EVALUATION_R2.md](./EVALUATION_R2.md) —— 数值机制评估与调整记录
- [PORTRAIT_PROMPTS.md](./PORTRAIT_PROMPTS.md) —— 15 人 AI 生图提示词包（立绘图片管线：`h5/public/portraits/`，有图用图、无图程序绘制）
- [h5/README.md](../h5/README.md) —— H5 工程使用（开发/构建/验证脚本）

## 专题细化文档（v0.2 启动）
| 文档 | 主题 | 状态 |
|---|---|---|
| [01-story.md](./01-story.md) | 剧情设计（八关脉络、人物弧线、对话风格、分支事件） | 🚧 草稿中 |
| [02-combat.md](./02-combat.md) | 战斗机制（手感、单挑、计策、回合节奏） | ⏳ 待写 |
| [03-character.md](./03-character.md) | 角色养成（觉醒/转职/羁绊/装备打造） | ⏳ 待写 |
| [04-levels.md](./04-levels.md) | 八关详细设计（地图、敌军、事件、胜负条件） | ⏳ 待写 |
| [05-systems.md](./05-systems.md) | 系统设计（大地图、商店、存档、设置） | ⏳ 待写 |
| [06-ui-ux.md](./06-ui-ux.md) | UI/UX 规范（视觉、动效、操作） | ⏳ 待写 |

## 设计决策依据

| 维度 | 决策 |
|---|---|
| 剧情忠实度 | **七实三虚** —— 主线遵循《旧唐书》《资治通鉴》，关键节点加戏剧化演绎 |
| 玄武门处理 | **前传序章** —— MVP 8 关全部发生在玄武门之前，留给 DLC |
| 机制聚焦 | **武将养成深度 + 战棋操作手感** |
| 时代范围 | 大业十三年（617）晋阳起兵 → 武德九年（626）玄武门前夜，共 9 年 |