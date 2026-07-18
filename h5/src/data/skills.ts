// ============================================================
// 8 种主动计策——以 docs/02-combat.md §6.1 为准；文档未给出的
// 施放距离/威力等字段取 Unity SkillData.cs。冲突处见注释。
// 25 个被动——移植自 Unity PassiveSkillData.cs（仅 id/name/desc，
// 触发逻辑后续实现）。
// ============================================================
import type { PassiveDef, SkillDef } from '../core/types';

export const SKILLS: SkillDef[] = [
  {
    // MP/范围/点燃取文档；施放距离3取 Unity（文档未给）；power=25 即 25%×INT/80
    id: 'fire_attack',
    name: '火攻',
    mp: 25,
    range: 3,
    aoe: 'area3',
    kind: 'damage',
    power: 25,
    duration: 3,
    desc: '3×3范围HP-25%×INT/80，并点燃3回合（每回合-5%HP）；雨/雪天效果减半或无效，有风时沿风向扩散',
  },
  {
    // MP/直线4格取文档；施放距离3、威力70取 Unity（文档仅述"基于INT"）
    id: 'rock_slide',
    name: '落石',
    mp: 20,
    range: 3,
    aoe: 'line4',
    kind: 'damage',
    power: 70,
    desc: '直线4格全员物理伤害（基于智力）；山地地形伤害+50%，附近山地触发连环落石（山地关卡限定）',
  },
  {
    // MP/效果取文档；施放距离4取 Unity；文档范围为6×6，SkillAoe 无对应枚举，记于 desc
    id: 'water_attack',
    name: '水攻',
    mp: 35,
    range: 4,
    aoe: 'area3',
    kind: 'damage',
    power: 15,
    duration: 5,
    desc: '6×6范围内单位HP-15%，移动消耗+2，持续5回合；需邻近水域，低地可变为水域（李靖LV8习得）',
  },
  {
    // MP/锥形取文档；施放距离3、威力30取 Unity
    id: 'volley',
    name: '乱射',
    mp: 20,
    range: 3,
    aoe: 'cone',
    kind: 'damage',
    power: 30,
    desc: '90°锥形范围内全员弓箭伤害（弓兵LV5习得）',
  },
  {
    // 文档：3×3、攻击+15%/命中+10%、持续3回合；施放距离2取 Unity（Unity 持续2回合，以文档为准）
    id: 'rally',
    name: '鼓舞',
    mp: 15,
    range: 2,
    aoe: 'allyArea3',
    kind: 'buff',
    power: 15,
    duration: 3,
    desc: '3×3友军攻击+15%、命中+10%，持续3回合（君主/谋士默认）',
  },
  {
    // MP/30%取文档（Unity 为 MP12/power40，以文档为准）；施放距离3取 Unity
    id: 'heal',
    name: '医疗',
    mp: 15,
    range: 3,
    aoe: 'allySingle',
    kind: 'heal',
    power: 30,
    desc: '恢复单个友军30%HP，并解除中毒/燃烧（长孙皇后默认）',
  },
  {
    // MP/效果取文档（Unity 为 MP22/跳过回合，以文档为准）；施放距离3取 Unity
    // 注：文档计策表标"杜如晦专属"，但文档角色表中长孙无忌亦持有混乱，此处不限定持有者
    id: 'confuse',
    name: '混乱',
    mp: 30,
    range: 3,
    aoe: 'single',
    kind: 'debuff',
    power: 0,
    duration: 1,
    desc: '使敌方下回合行动随机（含攻击友军）',
  },
  {
    // 取文档（自身/全场/显示意图）；Unity 实装为"驱散友军负面状态"，与文档冲突，以文档为准
    id: 'insight',
    name: '洞察',
    mp: 10,
    range: 0,
    aoe: 'all',
    kind: 'buff',
    power: 0,
    duration: 1,
    desc: '显示所有敌方下回合行动意图，持续1回合（长孙无忌默认）',
  },
  {
    // 新增：谋士万能输出技，不受地形/天气影响，避免沙漠/雪地等场景无输出
    id: 'thunder_strike',
    name: '落雷',
    mp: 20,
    range: 4,
    aoe: 'single',
    kind: 'damage',
    power: 95,
    desc: '召唤天雷轰击单个敌人（INT×0.95），无视地形防御与天气影响',
  },
  {
    // 新增：地形计谋，在建筑/山地密集关卡提供范围输出
    id: 'earth_split',
    name: '地裂',
    mp: 25,
    range: 3,
    aoe: 'area3',
    kind: 'damage',
    power: 35,
    desc: '3×3范围引发地裂（INT×0.35）；山地/城墙/关隘/城池伤害+30%',
  },
];

// ---------- 被动（15 角色专属 + 10 通用，来自 PassiveSkillData.cs） ----------
export const PASSIVES: PassiveDef[] = [
  // ===== 角色专属（15） =====
  { id: 'ps_tiance', name: '天策上将', desc: '全队攻击+8%' },
  { id: 'ps_zhiji', name: '智计辅佐', desc: '计策伤害+20%' },
  { id: 'ps_zhizheng', name: '治政', desc: '战后经验获取+15%' },
  { id: 'ps_jueduan', name: '决断', desc: '行动后20%概率再次行动' },
  { id: 'ps_tongshuai', name: '统帅', desc: '骑兵突击距离+1' },
  { id: 'ps_danliao', name: '单挑达人', desc: '单挑胜率+20%' },
  { id: 'ps_shuangjian', name: '双锏', desc: '暴击率+15%，反击率+20%' },
  { id: 'ps_sanbanfu', name: '三板斧', desc: '前3回合攻击翻倍' },
  { id: 'ps_xiaojiang', name: '骁将', desc: '不受地形移动限制' },
  { id: 'ps_youqi', name: '游骑', desc: '移动力+1，可两次行动' },
  { id: 'ps_shenshe', name: '神射', desc: '远程伤害+15%' },
  { id: 'ps_gongcheng', name: '攻城', desc: '攻城时伤害+25%' },
  { id: 'ps_fuzhuo', name: '辅佐', desc: '与平阳公主同阵时双方属性+15%' },
  { id: 'ps_niangzijun', name: '娘子军', desc: '全女性单位攻击+20%' },
  { id: 'ps_rende', name: '仁德', desc: '每回合恢复全队5%HP' },
  // ===== 通用（10，装备/升级习得） =====
  { id: 'ps_ironwall', name: '铁壁', desc: '物理伤害减免+10%' },
  { id: 'ps_magicbarrier', name: '魔障', desc: '计策伤害减免+15%' },
  { id: 'ps_counter', name: '反击', desc: '受到攻击时30%概率反击' },
  { id: 'ps_berserk', name: '狂暴', desc: 'HP低于30%时攻击+25%' },
  { id: 'ps_regenerate', name: '再生', desc: '每回合恢复15%HP' },
  { id: 'ps_vanguard', name: '先锋', desc: '首次攻击伤害+30%' },
  { id: 'ps_guardian', name: '护卫', desc: '相邻友军受到伤害-15%' },
  { id: 'ps_assassin', name: '奇袭', desc: '从背后攻击时伤害+20%' },
  { id: 'ps_inspire', name: '鼓舞', desc: '击杀时全队攻击+10%（本关有效）' },
  { id: 'ps_fortitude', name: '坚韧', desc: '每减少10%HP，防御+3%' },
];
