// ============================================================
// 相机——平移 + 缩放（双指捏合/滚轮），带边界钳制。
// 坐标系：世界坐标 = 战场像素（六边形布局），屏幕坐标 = CSS px。
//   screen = world * scale + (x, y)
// 缩放范围 0.6–1.6×（相对初始适配比例，见 H5_DESIGN §6）。
// ============================================================

export class Camera {
  x = 0;
  y = 0;
  scale = 1;

  private minScale = 0.3;
  private maxScale = 2;
  private fitScale = 1;
  private mapW = 1;
  private mapH = 1;
  private viewW = 1;
  private viewH = 1;
  /** 用户是否手动缩放过（是则窗口变化时不再重新适配） */
  private userZoomed = false;

  /** 设置战场世界尺寸（px） */
  setMap(w: number, h: number): void {
    this.mapW = Math.max(1, w);
    this.mapH = Math.max(1, h);
    if (!this.userZoomed) this.fit();
    else this.clampOffset();
  }

  /** 设置可视区域尺寸（CSS px） */
  setView(w: number, h: number): void {
    this.viewW = Math.max(1, w);
    this.viewH = Math.max(1, h);
    if (!this.userZoomed) this.fit();
    else this.clampOffset();
  }

  /** 初始适配：整图居中（缩放范围 = 适配比例 × [0.6, 1.6]） */
  fit(): void {
    this.fitScale = Math.min(this.viewW / this.mapW, this.viewH / this.mapH);
    this.minScale = this.fitScale * 0.6;
    this.maxScale = this.fitScale * 1.6;
    this.scale = this.fitScale;
    this.userZoomed = false;
    this.clampOffset();
  }

  /** 边界钳制：图小于视口则居中，否则不允许拖出边缘（留 40px 余量） */
  clampOffset(): void {
    const m = 40;
    const w = this.mapW * this.scale;
    const h = this.mapH * this.scale;
    if (w <= this.viewW) this.x = (this.viewW - w) / 2;
    else this.x = Math.min(m, Math.max(this.viewW - w - m, this.x));
    if (h <= this.viewH) this.y = (this.viewH - h) / 2;
    else this.y = Math.min(m, Math.max(this.viewH - h - m, this.y));
  }

  /** 平移（屏幕坐标增量） */
  panBy(dx: number, dy: number): void {
    this.x += dx;
    this.y += dy;
    this.clampOffset();
  }

  /** 以屏幕上 (sx, sy) 为锚点缩放 factor 倍 */
  zoomAt(sx: number, sy: number, factor: number): void {
    const ns = Math.min(this.maxScale, Math.max(this.minScale, this.scale * factor));
    if (ns === this.scale) return;
    const k = ns / this.scale;
    this.x = sx - (sx - this.x) * k;
    this.y = sy - (sy - this.y) * k;
    this.scale = ns;
    this.userZoomed = true;
    this.clampOffset();
  }

  /** 屏幕坐标 → 世界坐标 */
  screenToWorld(sx: number, sy: number): { x: number; y: number } {
    return { x: (sx - this.x) / this.scale, y: (sy - this.y) / this.scale };
  }

  /** 世界坐标 → 屏幕坐标 */
  worldToScreen(wx: number, wy: number): { x: number; y: number } {
    return { x: wx * this.scale + this.x, y: wy * this.scale + this.y };
  }
}
