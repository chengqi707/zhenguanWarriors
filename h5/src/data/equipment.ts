// ============================================================
// 30 件装备——移植自 Unity Core/Combat/EquipmentData.cs。
// 价格：Unity 中 basePrice 未填，按 ShopCatalog.CalculatePrice
// 公式实算（稀有度基数 100/250/600/1200 + 五维×5 + HP/MP×2
// + 移动/射程×80 + 百分比×10）。docs/05-systems.md 仅给出稀有度
// 价格区间（白200-400/绿500-800/蓝1000-1500/紫不可购买），
// 与公式结果不一致，此处以 Unity 实装公式为准，冲突见各条注释。
// 与文档冲突的字段以文档为准并加注。
// ============================================================
import type { EquipmentDef } from '../core/types';

export const EQUIPMENT: EquipmentDef[] = [
  // ========== 武器（12件） ==========
  { id: 'w001', name: '环首刀', slot: 'weapon', rarity: 'white', statBonus: { str: 8 }, price: 140 },
  { id: 'w002', name: '铁枪', slot: 'weapon', rarity: 'white', classes: ['infantry', 'heavy'], statBonus: { str: 12 }, price: 160 },
  { id: 'w003', name: '马槊', slot: 'weapon', rarity: 'white', classes: ['cavalry'], statBonus: { str: 14 }, moveBonus: 1, price: 250 },
  { id: 'w004', name: '长弓', slot: 'weapon', rarity: 'white', classes: ['archer'], statBonus: { str: 10 }, rangeBonus: 1, price: 230 },
  // 特效取文档（对城墙+20%）；Unity 无 effectDesc
  { id: 'w005', name: '攻城锤', slot: 'weapon', rarity: 'white', classes: ['siege'], statBonus: { str: 15 }, effectDesc: '对城墙伤害+20%', price: 175 },
  // 特效取文档（计策伤害+10%）；Unity 实装为 intPercent=10（智力+10%）
  { id: 'w006', name: '羽扇', slot: 'weapon', rarity: 'white', classes: ['strategist'], statBonus: { str: 5 }, effectDesc: '计策伤害+10%', price: 225 },
  // 特效取文档（暴击率+5%→critBonus）；Unity 实装为 lukBonus=5
  { id: 'w007', name: '精钢剑', slot: 'weapon', rarity: 'green', statBonus: { str: 18 }, critBonus: 5, price: 365 },
  { id: 'w008', name: '破甲枪', slot: 'weapon', rarity: 'green', classes: ['infantry', 'heavy'], statBonus: { str: 22 }, effectDesc: '无视15%防御', price: 360 },
  { id: 'w009', name: '饮血刀', slot: 'weapon', rarity: 'green', classes: ['cavalry'], statBonus: { str: 20 }, effectDesc: '击杀恢复10%HP', price: 350 },
  { id: 'w010', name: '连弩', slot: 'weapon', rarity: 'green', classes: ['archer'], statBonus: { str: 16 }, effectDesc: '可攻击2次（伤害-30%）', price: 330 },
  // 限制取文档（李世民专属）；Unity 实装为 骑兵+男性限定。strPercent=15→attackPct
  { id: 'w011', name: '秦王剑', slot: 'weapon', rarity: 'purple', charId: 'lishimin', statBonus: { str: 30 }, attackPct: 15, effectDesc: '攻击+15%，光环范围+1', price: 1500 },
  // 限制取文档（秦琼专属）；Unity 实装为 骑兵限定
  { id: 'w012', name: '门神锏', slot: 'weapon', rarity: 'purple', charId: 'qin_qiong', statBonus: { str: 28 }, effectDesc: '反击伤害+50%', price: 1340 },

  // ========== 防具（10件） ==========
  { id: 'a001', name: '皮甲', slot: 'armor', rarity: 'white', statBonus: { cmd: 5 }, hpBonus: 10, price: 145 },
  { id: 'a002', name: '铁甲', slot: 'armor', rarity: 'white', classes: ['infantry', 'heavy', 'cavalry'], statBonus: { cmd: 10 }, hpBonus: 20, moveBonus: -1, price: 110 },
  { id: 'a003', name: '轻甲', slot: 'armor', rarity: 'white', classes: ['archer', 'strategist'], statBonus: { cmd: 6 }, hpBonus: 15, price: 160 },
  { id: 'a004', name: '藤甲', slot: 'armor', rarity: 'green', statBonus: { cmd: 12 }, hpBonus: 25, effectDesc: '火攻伤害+20%（负面）', price: 360 },
  { id: 'a005', name: '明光铠', slot: 'armor', rarity: 'green', classes: ['infantry', 'heavy'], statBonus: { cmd: 18 }, hpBonus: 35, effectDesc: '被暴击率-10%', price: 410 },
  { id: 'a006', name: '锁子甲', slot: 'armor', rarity: 'green', classes: ['cavalry'], statBonus: { cmd: 15 }, hpBonus: 30, effectDesc: '骑兵突击伤害+10%', price: 385 },
  { id: 'a007', name: '锦袍', slot: 'armor', rarity: 'green', classes: ['strategist'], statBonus: { cmd: 8 }, hpBonus: 20, mpBonus: 20, price: 370 },
  { id: 'a008', name: '乌铁甲', slot: 'armor', rarity: 'blue', statBonus: { cmd: 22 }, hpBonus: 45, effectDesc: '物理伤害-10%', price: 800 },
  // 限制取文档（兵种"君主"，即李世民）；Unity 实装为 骑兵+男性限定
  { id: 'a009', name: '金鳞甲', slot: 'armor', rarity: 'blue', charId: 'lishimin', statBonus: { cmd: 25 }, hpBonus: 50, effectDesc: '全队防御+5%', price: 825 },
  { id: 'a010', name: '铁壁盾', slot: 'armor', rarity: 'blue', classes: ['heavy'], statBonus: { cmd: 20 }, hpBonus: 40, effectDesc: '正面伤害-20%', price: 780 },

  // ========== 饰品（8件） ==========
  { id: 't001', name: '玉佩', slot: 'trinket', rarity: 'white', statBonus: { luk: 5 }, price: 125 },
  { id: 't002', name: '护心镜', slot: 'trinket', rarity: 'white', statBonus: { luk: 5 }, effectDesc: '被暴击率-5%', price: 125 },
  { id: 't003', name: '香囊', slot: 'trinket', rarity: 'green', gender: 'female', hpBonus: 15, price: 280 },
  { id: 't004', name: '兵书', slot: 'trinket', rarity: 'green', classes: ['strategist'], statBonus: { int: 8 }, price: 290 },
  { id: 't005', name: '酒壶', slot: 'trinket', rarity: 'green', effectDesc: '每关开始HP+10%', price: 250 },
  { id: 't006', name: '战鼓', slot: 'trinket', rarity: 'blue', effectDesc: '同阵友军攻击+3%', price: 600 },
  { id: 't007', name: '令箭', slot: 'trinket', rarity: 'blue', moveBonus: 1, price: 680 },
  { id: 't008', name: '虎符', slot: 'trinket', rarity: 'purple', statBonus: { cmd: 10 }, effectDesc: '统御+10，全队命中+5%', price: 1250 },
];
