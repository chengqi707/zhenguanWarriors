// ============================================================
// 全局设置持久化（不依赖 save.ts 的存档系统，仅 localStorage）
// ============================================================

const KEY_LANDSCAPE = 'zg_settings_landscape';

/** 是否开启 PC 横屏模式（战斗页右侧操作栏布局） */
export function isLandscapeMode(): boolean {
  try {
    return localStorage.getItem(KEY_LANDSCAPE) === '1';
  } catch {
    return false;
  }
}

export function setLandscapeMode(value: boolean): void {
  try {
    localStorage.setItem(KEY_LANDSCAPE, value ? '1' : '0');
  } catch {
    // ignore
  }
}
