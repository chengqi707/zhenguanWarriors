// ============================================================
// H5 入口——挂载全局样式并启动页面状态机。
// 启动错误/关键步骤会写入 #boot-log，方便在飞书/微信等
// 受限内置浏览器中直接看到报错，无需 adb。
// ============================================================
import './styles/main.css';
import { Game } from './ui/game';
import { loadPortraitImage } from './ui/portraits';
import { CHARACTERS } from './data';

const logs: string[] = [];
function bootLog(msg: string): void {
  logs.push(msg);
  const el = document.getElementById('boot-log');
  if (el) el.textContent = logs.join('\n');
}

window.onerror = (msg, _url, line, col, err) => {
  bootLog(`[ERR] ${msg} @${line}:${col} ${err?.stack ?? ''}`);
  return false;
};
window.onunhandledrejection = (e: PromiseRejectionEvent) => {
  bootLog(`[REJ] ${e.reason?.stack ?? e.reason}`);
};

// 预加载外部立绘图片（失败自动回退程序绘制，避免白屏阻塞）
(async () => {
  try {
    bootLog('boot: 开始加载');
    bootLog(`ua: ${navigator.userAgent.slice(0, 80)}`);
    bootLog(`screen: ${window.innerWidth}x${window.innerHeight}`);
    let ls = '?';
    try { ls = typeof localStorage !== 'undefined' ? 'yes' : 'no'; localStorage.getItem('__t__'); } catch (e) { ls = 'err'; }
    bootLog(`localStorage: ${ls}`);
    await Promise.all(CHARACTERS.map(c => loadPortraitImage(c.id)));
    bootLog('boot: 立绘加载完成');
    const app = document.getElementById('app');
    if (!app) { bootLog('boot: 找不到 #app'); return; }
    new Game(app).start();
    bootLog('boot: 游戏已启动');
  } catch (e) {
    bootLog(`[FATAL] ${e instanceof Error ? e.stack ?? e.message : String(e)}`);
  }
})();
