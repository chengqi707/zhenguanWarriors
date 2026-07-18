// ============================================================
// 音频模块对外 API——UI/战斗代码统一从这里 import。
// ============================================================
export {
  Bgm,
  isMusicOn, setMusicOn,
  isSfxOn, setSfxOn,
  setMusicVolume, setSfxVolume,
  type BgmName,
} from './engine';
export { Sfx, bindGlobalClickSfx, type SfxName } from './sfx';
