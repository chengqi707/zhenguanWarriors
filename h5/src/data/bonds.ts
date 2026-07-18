// ============================================================
// 6 组羁绊——以 docs/03-character.md §4.1 为准。
// Unity BondSystem.cs 仅实装 5 组且无「瓦岗三杰·齐」「兄妹」，
// 另有文档不存在的「天策府将」（bond_tiance）未收录——均以文档为准。
// ============================================================
import type { BondDef } from '../core/types';

export const BONDS: BondDef[] = [
  {
    id: 'bond_emperor',
    name: '帝后同心',
    members: ['lishimin', 'zhangsun_empress'],
    minCount: 2,
    desc: '双方全属性+10%；长孙皇后医疗范围+1',
  },
  {
    // 文档名「夫妻同阵」（Unity 作「夫妻并肩」，以文档为准）
    id: 'bond_couple',
    name: '夫妻同阵',
    members: ['chai_shao', 'pingyang_princess'],
    minCount: 2,
    desc: '双方全属性+15%；相邻时互相替对方承受1次致命伤害（每关1次）',
  },
  {
    // 文档：任意两人同阵即触发（Unity 实装需三人齐出且效果为全队攻击+12%，以文档为准）
    id: 'bond_wagang',
    name: '瓦岗三杰',
    members: ['qin_qiong', 'cheng_yaojin', 'yuchi_jingde'],
    minCount: 2,
    desc: '任意两人同阵：两人攻击力+10%、暴击率+5%',
  },
  {
    id: 'bond_wagang_full',
    name: '瓦岗三杰·齐',
    members: ['qin_qiong', 'cheng_yaojin', 'yuchi_jingde'],
    minCount: 3,
    desc: '三人同阵：攻击力+10%、暴击率+5%，且全员获得「同袍」——受到致命伤害时保留1HP（每关1次/人）',
  },
  {
    // 文档：智力+10、MP消耗-20%（Unity 实装为计策伤害+15%，以文档为准）
    id: 'bond_chancellor',
    name: '房谋杜断',
    members: ['fang_xuanling', 'du_ruhui'],
    minCount: 2,
    desc: '两人智力+10，计策MP消耗-20%',
  },
  {
    id: 'bond_siblings',
    name: '兄妹',
    members: ['zhangsun_wuji', 'zhangsun_empress'],
    minCount: 2,
    desc: '两人MP上限+15，回合开始互相恢复10MP',
  },
];
