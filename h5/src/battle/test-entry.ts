// ============================================================
// 战斗自检入口（h5/battle-test.html 专用，不进主构建）：
// new BattleScene 直接打第 1 关（新手引导开），胜负后显示结果
// 并可一键重打。vite dev 下访问 /battle-test.html；
// 可用 ?level=N 指定关卡（视觉自测截图用，新手引导仅第 1 关开）。
// ============================================================
import type { BattleOutcome } from '../core/types';
import { getLevel } from '../data';
import { BattleScene } from './battleScene';

const app = document.getElementById('app');
if (!app) throw new Error('缺少 #app 容器');

let scene: BattleScene | null = null;

function start(): void {
  const levelId = Number(new URLSearchParams(location.search).get('level') ?? '1');
  const level = getLevel(levelId);
  if (!level) throw new Error(`第 ${levelId} 关数据缺失`);
  app!.innerHTML = '';
  scene?.destroy();
  scene = new BattleScene(app!, {
    level,
    // available 全员出战，等级与 sim 口径一致（levelId×2+1）
    party: [
      { charId: 'lishimin', level: levelId * 2 + 1, equipment: {} },
      { charId: 'zhangsun_wuji', level: levelId * 2 + 1, equipment: {} },
      { charId: 'chai_shao', level: levelId * 2 + 1, equipment: {} },
      { charId: 'liu_hongji', level: levelId * 2 + 1, equipment: {} },
      { charId: 'li_jing', level: levelId * 2 + 1, equipment: {} },
    ],
    difficulty: 'normal',
    items: { jinchuang: 2, qingxin: 1, shiqi: 1 }, // 自检：经典行动流「物品」入口
    tutorial: levelId === 1,
    onFinish: (outcome: BattleOutcome) => showResult(outcome),
  });
  // 调试/自动化测试钩子（与 battlePage 一致）
  (window as unknown as Record<string, unknown>).__scene = scene;
}

function showResult(outcome: BattleOutcome): void {
  scene?.destroy();
  scene = null;
  app!.innerHTML = '';
  const wrap = document.createElement('div');
  wrap.className = 'test-result';
  const h = document.createElement('h1');
  h.textContent = outcome === 'win' ? '🎉 胜利（win）' : '💀 战败（lose）';
  const p = document.createElement('p');
  p.textContent = 'onFinish 回调正常，战斗场景已销毁。';
  const btn = document.createElement('button');
  btn.textContent = '重打本关';
  btn.addEventListener('click', start);
  wrap.appendChild(h);
  wrap.appendChild(p);
  wrap.appendChild(btn);
  app!.appendChild(wrap);
}

start();
