# 贞观勇士 H5 版

Unity 版的 H5 重制：TypeScript + Canvas 2D（战斗）+ DOM（UI），零运行时依赖。
设计依据：`../docs/H5_DESIGN.md`。

## 开发

```bash
npm install
npm run dev        # http://localhost:5173
```

## 构建与本地预览

```bash
npm run build      # 产物在 dist/（纯静态，约 35KB gzip）
npm run preview    # http://localhost:4173
```

## 验证脚本

```bash
npm run sim                              # 无头仿真：AI 自动对战 8 关×3 种子（引擎逻辑验证）
node scripts/walkthrough.mjs             # 端到端走查：完整页面流截图（需先 npm run preview）
node scripts/autowin.mjs                 # 胜利链路：真实打第 1 关→结算→解锁下一关
node scripts/battle-flow.mjs             # 经典行动流：移动→行动菜单→物品
node scripts/attack-flow.mjs             # 攻击链路：多回合逼近→预览→确认攻击+胜利条件展示
node scripts/probe.mjs                   # 战斗画布布局探针（排障用）
```

走查/胜利链路脚本依赖本机 Chrome（playwright-core `channel: 'chrome'`），截图输出到 `shots/`。

## 目录

```
src/
├── core/     # 纯逻辑：hex/A*/战斗公式/回合机/AI/存档/仿真（零 DOM）
├── data/     # 数据表：15角色/30装备/8计策/25被动/6羁绊/8关/16场剧情
├── battle/   # Canvas 战斗：渲染/相机/动画/场景总控
├── ui/       # DOM 页面：菜单/选关/剧情/选人/配装/结算/设置
└── main.ts
```

## 部署

### 腾讯云 COS 一键部署（已配置）

1. 复制配置模板：
   ```bash
   cp .env.example .env.local
   ```
2. 编辑 `.env.local`，填入你的 `COS_SECRET_ID`、`COS_SECRET_KEY`、`COS_BUCKET`、`COS_REGION`。
3. 构建并上传：
   ```bash
   npm run build && npm run pack && npm run deploy:cos
   ```

> `.env.local` 已在 `.gitignore` 中，**绝不会提交到仓库**；`.env.example` 只包含模板，不含真实密钥。

### 其他托管

`dist/` 是纯静态文件，丢到任何静态托管即可（阿里云 OSS、Vercel、Nginx 等）。
注意 `vite.config.ts` 里 `base: './'`，可部署在任意子路径。
