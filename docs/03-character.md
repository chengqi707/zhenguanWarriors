# 03 角色系统设计

> 本文档定义全部 15 名可上阵角色的完整数据、成长曲线、羁绊关系与登场时机。所有数值为**普通难度基准**，其他难度通过全局乘子调整。

---

## 1. 设计原则

1. **主角绝对核心**：李世民五维无短板，作为玩家操作最频繁的单位必须手感最好。
2. **定位鲜明**：同一兵种内角色必须有差异，避免"同质化武将"。
3. **成长可期**：T0 角色起点高但成长慢，T2 角色起点低但某维成长快，给养成乐趣。
4. **羁绊有戏**：羁绊加成必须强到值得为触发它而调整编队，但不能强到不触发就玩不了。
5. **阵亡非永别**：MVP 中阵亡仅本关退场，不丢经验不扣忠诚。

---

## 2. 角色总览表

| 序 | 姓名 | 定位 | 兵种 | 评级 | 登场关卡 | 核心特色 |
|---|---|---|---|---|---|---|
| 1 | 李世民 | 君主 | 骑兵 | T0 | 第1关 | 全能光环，无短板 |
| 2 | 长孙无忌 | 谋士 | 谋士 | T1 | 第1关 | 全队回MP，计策强化 |
| 3 | 房玄龄 | 谋士 | 谋士 | T1 | 第3关 | 战前资金+，全队经验+10% |
| 4 | 杜如晦 | 谋士 | 谋士 | T1 | 第4关 | 控场专精，混乱/封锁 |
| 5 | 李靖 | 武将 | 骑兵 | T0 | 第4关 | 高统御，骑兵突击双倍距离 |
| 6 | 尉迟敬德 | 武将 | 重步 | T0 | 第5关(降) | 高HP高防，单挑必杀 |
| 7 | 秦琼 | 武将 | 骑兵 | T1 | 第6关(降) | 双锏暴击率高，反击率高 |
| 8 | 程咬金 | 武将 | 重步 | T1 | 第6关(降) | 三板斧：前3回合攻击翻倍 |
| 9 | 侯君集 | 武将 | 步兵 | T2 | 第6关 | 攻防均衡，地形适应强 |
| 10 | 段志玄 | 武将 | 骑兵 | T2 | 第2关 | 高移动力，可二次行动 |
| 11 | 刘弘基 | 武将 | 弓兵 | T1 | 第1关 | 远射距离+1，无地形遮挡 |
| 12 | 殷开山 | 武将 | 器械 | T2 | 第2关 | 攻城/破阵专精，AOE伤害 |
| 13 | 柴绍 | 武将 | 骑兵 | T2 | 第1关 | 与平阳同阵双方属性+15% |
| 14 | 平阳公主 | 女性 | 骑兵 | T1 | 第3关 | 娘子军：全场女性攻击+20% |
| 15 | 长孙皇后 | 女性 | 谋士 | T1 | 第5关(剧情) | 群体治疗，复活(每关1次) |

---

## 3. 完整角色数据

### 3.1 基础属性基准

| 属性 | 含义 | 影响 |
|---|---|---|
| STR 武力 | 物理攻击力 | 普攻伤害、单挑胜率 |
| CMD 统御 | 防御力/部曲数 | 物理防御、HP上限 |
| INT 智力 | 计策能力 | 计策伤害、计策抗性、命中率 |
| AGI 敏捷 | 机动性 | 命中/回避、行动顺序 |
| LUK 运气 | 变数 | 暴击率、抗暴、幸运事件 |

### 3.2 君主（1人）

#### 李世民（lishimin）
```json
{
  "id": "lishimin",
  "name": "李世民",
  "role": "monarch",
  "gender": "male",
  "class": "cavalry",
  "baseStats": { "str": 82, "cmd": 95, "int": 88, "agi": 78, "luk": 90 },
  "growth":   { "str": 4,  "cmd": 5,  "int": 4,  "agi": 3,  "luk": 4 },
  "hp": 120, "mp": 50, "move": 5, "attackRange": 1,
  "skills": ["rally", "insight", "charge"],
  "passive": "天策光环：同阵友军全属性+5%（不叠加）",
  "bonds": ["zhangsun_empress", "li_jing"],
  "unlockLevel": 1
}
```
- **特性**：天策光环——全场友军（含自身）五维+5%，不与其他君主光环叠加。
- **专属计策**：「秦王破阵」——LV15 习得，单体高伤+下回合自身攻防+20%。

---

### 3.3 谋士（4人）

谋士统一特征：HP 低（70-90），MP 高（50-70），移动力 5，普攻范围 2，兵种克制：克器械、被骑兵克。

#### 长孙无忌（zhangsun_wuji）
```json
{
  "id": "zhangsun_wuji",
  "name": "长孙无忌",
  "role": "strategist",
  "gender": "male",
  "class": "strategist",
  "baseStats": { "str": 30, "cmd": 60, "int": 92, "agi": 65, "luk": 85 },
  "growth":   { "str": 1,  "cmd": 3,  "int": 5,  "agi": 3,  "luk": 4 },
  "hp": 80, "mp": 65, "move": 5, "attackRange": 2,
  "skills": ["insight", "fire_attack", "confuse"],
  "passive": "谋主：回合开始时，全队恢复 5% MP",
  "unlockLevel": 1
}
```
- **特性**：谋主光环——每回合开始，所有友军恢复 5% 最大 MP（含自身）。

#### 房玄龄（fang_xuanling）
```json
{
  "id": "fang_xuanling",
  "name": "房玄龄",
  "role": "strategist",
  "gender": "male",
  "class": "strategist",
  "baseStats": { "str": 25, "cmd": 70, "int": 92, "agi": 60, "luk": 80 },
  "growth":   { "str": 1,  "cmd": 3,  "int": 5,  "agi": 2,  "luk": 3 },
  "hp": 75, "mp": 60, "move": 5, "attackRange": 2,
  "skills": ["rally", "heal", "logistics"],
  "passive": "治政：战前资金+20%，全队本关经验获取+10%",
  "unlockLevel": 3
}
```
- **特性**：后勤型谋士，不直接增强伤害，但提升全局资源。

#### 杜如晦（du_ruhui）
```json
{
  "id": "du_ruhui",
  "name": "杜如晦",
  "role": "strategist",
  "gender": "male",
  "class": "strategist",
  "baseStats": { "str": 28, "cmd": 68, "int": 90, "agi": 62, "luk": 78 },
  "growth":   { "str": 1,  "cmd": 2,  "int": 5,  "agi": 3,  "luk": 3 },
  "hp": 78, "mp": 60, "move": 5, "attackRange": 2,
  "skills": ["confuse", "rock_slide", "insight"],
  "passive": "决断：对混乱/减速状态的敌人伤害+25%",
  "unlockLevel": 4
}
```
- **特性**：控场专精，混乱计策成功率比其他谋士高 10%。

#### 长孙皇后（zhangsun_empress）
```json
{
  "id": "zhangsun_empress",
  "name": "长孙皇后",
  "role": "female",
  "gender": "female",
  "class": "strategist",
  "baseStats": { "str": 20, "cmd": 55, "int": 88, "agi": 60, "luk": 90 },
  "growth":   { "str": 1,  "cmd": 2,  "int": 4,  "agi": 2,  "luk": 5 },
  "hp": 70, "mp": 70, "move": 5, "attackRange": 2,
  "skills": ["heal", "revive", "rally"],
  "passive": "母仪：医疗计策效果+20%，复活后目标额外恢复 10% HP",
  "unlockLevel": 5
}
```
- **特性**：唯一拥有「回春(复活)」的角色，每关限用 1 次。
- **限制**：第 5 关后才通过剧情解锁，前期不可用。

---

### 3.4 武将（8人）

#### 李靖（li_jing）
```json
{
  "id": "li_jing",
  "name": "李靖",
  "role": "general",
  "gender": "male",
  "class": "cavalry",
  "baseStats": { "str": 90, "cmd": 85, "int": 75, "agi": 70, "luk": 75 },
  "growth":   { "str": 5,  "cmd": 4,  "int": 3,  "agi": 3,  "luk": 3 },
  "hp": 105, "mp": 35, "move": 6, "attackRange": 1,
  "skills": ["fire_attack", "water_attack"],
  "passive": "统帅：骑兵突击时移动距离+1（可达7格）",
  "unlockLevel": 4
}
```
- **特性**：骑兵中的统帅，移动力 6（比其他骑兵多 1），突击范围极远。
- **水攻**：LV8 习得，需在邻近水域关卡才能发挥最大效果。

#### 尉迟敬德（yuchi_jingde）
```json
{
  "id": "yuchi_jingde",
  "name": "尉迟敬德",
  "role": "general",
  "gender": "male",
  "class": "heavy_infantry",
  "baseStats": { "str": 92, "cmd": 88, "int": 45, "agi": 65, "luk": 60 },
  "growth":   { "str": 5,  "cmd": 5,  "int": 1,  "agi": 2,  "luk": 2 },
  "hp": 130, "mp": 20, "move": 4, "attackRange": 1,
  "skills": ["duel_slash"],
  "passive": "铁壁：受到物理伤害-15%，被暴击率-10%",
  "unlockLevel": 5
}
```
- **特性**：全游戏最高 HP/CMD，人形城墙。
- **单挑必杀**：「夺槊」——单挑中必杀槽消耗-1。
- **登场**：第 5 关为敌方，剧情中段招降后永久加入。

#### 秦琼（qin_qiong）
```json
{
  "id": "qin_qiong",
  "name": "秦琼",
  "role": "general",
  "gender": "male",
  "class": "cavalry",
  "baseStats": { "str": 88, "cmd": 80, "int": 55, "agi": 72, "luk": 70 },
  "growth":   { "str": 4,  "cmd": 4,  "int": 2,  "agi": 3,  "luk": 3 },
  "hp": 100, "mp": 25, "move": 5, "attackRange": 1,
  "skills": ["charge", "counter_stance"],
  "passive": "门神：反击伤害+30%，反击率+15%",
  "unlockLevel": 6
}
```
- **特性**：反击流核心，敌人攻击他时往往得不偿失。

#### 程咬金（cheng_yaojin）
```json
{
  "id": "cheng_yaojin",
  "name": "程咬金",
  "role": "general",
  "gender": "male",
  "class": "heavy_infantry",
  "baseStats": { "str": 85, "cmd": 75, "int": 40, "agi": 60, "luk": 85 },
  "growth":   { "str": 4,  "cmd": 3,  "int": 1,  "agi": 2,  "luk": 5 },
  "hp": 115, "mp": 15, "move": 4, "attackRange": 1,
  "skills": ["axe_slash"],
  "passive": "三板斧：每关前 3 回合，攻击力翻倍（第4回合起恢复）",
  "unlockLevel": 6
}
```
- **特性**：开场爆发型，前3回合全游戏最高物理输出。
- **运气极高**：暴击率和幸运事件触发率都很可观。

#### 侯君集（hou_junji）
```json
{
  "id": "hou_junji",
  "name": "侯君集",
  "role": "general",
  "gender": "male",
  "class": "infantry",
  "baseStats": { "str": 78, "cmd": 72, "int": 65, "agi": 68, "luk": 60 },
  "growth":   { "str": 3,  "cmd": 3,  "int": 3,  "agi": 3,  "luk": 2 },
  "hp": 100, "mp": 20, "move": 5, "attackRange": 1,
  "skills": [],
  "passive": "均衡：在所有地形上移动消耗视为平原（不受负面地形影响）",
  "unlockLevel": 6
}
```
- **特性**：没有突出长板，但地形适应全游戏最强，任何图都能稳定发挥。

#### 段志玄（duan_zhixuan）
```json
{
  "id": "duan_zhixuan",
  "name": "段志玄",
  "role": "general",
  "gender": "male",
  "class": "cavalry",
  "baseStats": { "str": 75, "cmd": 70, "int": 60, "agi": 85, "luk": 65 },
  "growth":   { "str": 3,  "cmd": 3,  "int": 2,  "agi": 5,  "luk": 2 },
  "hp": 95, "mp": 20, "move": 6, "attackRange": 1,
  "skills": ["charge"],
  "passive": "游骑：每 5 回合可额外行动一次（第5、10、15...回合）",
  "unlockLevel": 2
}
```
- **特性**：高 AGI 带来高命中/回避，游骑被动在持久战中价值巨大。

#### 刘弘基（liu_hongji）
```json
{
  "id": "liu_hongji",
  "name": "刘弘基",
  "role": "general",
  "gender": "male",
  "class": "archer",
  "baseStats": { "str": 80, "cmd": 68, "int": 55, "agi": 70, "luk": 55 },
  "growth":   { "str": 4,  "cmd": 3,  "int": 2,  "agi": 3,  "luk": 2 },
  "hp": 90, "mp": 25, "move": 4, "attackRange": 3,
  "skills": ["volley"],
  "passive": "神射：弓箭射程+1（可达3格），无视森林地形命中减益",
  "unlockLevel": 1
}
```
- **特性**：全游戏最远的普攻射程（3格），森林战不损失命中。

#### 殷开山（yin_kaishan）
```json
{
  "id": "yin_kaishan",
  "name": "殷开山",
  "role": "general",
  "gender": "male",
  "class": "siege",
  "baseStats": { "str": 70, "cmd": 75, "int": 60, "agi": 55, "luk": 50 },
  "growth":   { "str": 3,  "cmd": 4,  "int": 3,  "agi": 1,  "luk": 2 },
  "hp": 95, "mp": 20, "move": 3, "attackRange": 2,
  "skills": ["rock_slide"],
  "passive": "破阵：对城墙/城池地形上的敌人伤害+25%，可破坏城墙（变为废墟）",
  "unlockLevel": 2
}
```
- **特性**：攻城战必备，能破坏城墙创造通路。

#### 柴绍（chai_shao）
```json
{
  "id": "chai_shao",
  "name": "柴绍",
  "role": "general",
  "gender": "male",
  "class": "cavalry",
  "baseStats": { "str": 72, "cmd": 70, "int": 68, "agi": 72, "luk": 70 },
  "growth":   { "str": 3,  "cmd": 3,  "int": 3,  "agi": 3,  "luk": 3 },
  "hp": 98, "mp": 25, "move": 5, "attackRange": 1,
  "skills": ["rally"],
  "passive": "辅佐：与平阳公主同阵时，双方全属性+15%",
  "unlockLevel": 1
}
```
- **特性**：单独使用中规中矩，但与平阳绑定后两人都能达到 T1 水准。

---

### 3.5 女性（2人）

#### 平阳公主（pingyang_princess）
```json
{
  "id": "pingyang_princess",
  "name": "平阳公主",
  "role": "female",
  "gender": "female",
  "class": "cavalry",
  "baseStats": { "str": 80, "cmd": 82, "int": 75, "agi": 80, "luk": 78 },
  "growth":   { "str": 4,  "cmd": 4,  "int": 3,  "agi": 4,  "luk": 3 },
  "hp": 100, "mp": 30, "move": 5, "attackRange": 1,
  "skills": ["charge", "niangzi_army"],
  "passive": "娘子军：全场所有女性角色（含自身）攻击力+20%",
  "unlockLevel": 3
}
```
- **特性**：女性核心，与长孙皇后同阵时两人互相加成极高。
- **娘子军**：被动全场生效，不需要相邻。

---

## 4. 羁绊系统

羁绊在**战前编组界面**确认触发，战斗中实时生效。

### 4.1 羁绊一览

| 羁绊名 | 成员 | 触发条件 | 效果 |
|---|---|---|---|
| 帝后同心 | 李世民 + 长孙皇后 | 同阵（均上场） | 双方全属性+10%；长孙皇后医疗范围+1 |
| 夫妻同阵 | 柴绍 + 平阳公主 | 同阵 | 双方全属性+15%；相邻时互相替对方承受1次致命伤害（每关1次） |
| 瓦岗三杰 | 秦琼 + 程咬金 + 尉迟敬德 | 任意两人同阵 | 两人攻击力+10%、暴击率+5% |
| 瓦岗三杰·齐 | 秦琼 + 程咬金 + 尉迟敬德 | 三人同阵 | 以上效果+全员获得「同袍」：受到致命伤害时保留1HP（每关1次/人） |
| 房谋杜断 | 房玄龄 + 杜如晦 | 同阵 | 两人智力+10，计策MP消耗-20% |
| 兄妹 | 长孙无忌 + 长孙皇后 | 同阵 | 两人MP上限+15，回合开始互相恢复10MP |

### 4.2 羁绊提示
- 编组界面中，已触发羁绊的角色头像旁显示金色连线。
- 战斗中首次触发羁绊效果时，弹出小横幅「帝后同心，攻击力提升！」

---

## 5. 经验与成长

### 5.1 升级曲线

| 等级 | 所需累计经验 | 备注 |
|---|---|---|
| 1 | 0 | 初始 |
| 2 | 100 | |
| 3 | 220 | |
| 4 | 360 | |
| 5 | 520 | 第1关结束约LV3-4 |
| 10 | 1800 | 第3关结束约LV8-10 |
| 15 | 4200 | 第5关结束约LV13-15 |
| 20 | 7800 | 第6关结束约LV17-19 |
| 25 | 13000 | 第7关结束约LV22-24 |
| 30 | 20000 | 通关目标 |

每升1级：五维各增加 `growth` 值（小数向下取整）。

### 5.2 经验获取

- 参战并行动：100% 基础经验
- 参战但未行动：50%
- 未参战：0%（ bench 不获得）
- 击杀：额外+30%
- 击杀Boss：额外+50%

### 5.3 等级上限与转职

- MVP 不设转职系统，30级为上限。
- 部分角色在特定等级习得新计策（如李靖 LV8 习得水攻）。

---

## 6. 忠诚度（轻量）

- 范围 0-100，默认 80。
- 影响：关卡末「武将自言自语」事件触发率；隐藏单挑必胜条件。
- 不会脱离阵营（MVP 简化）。
- 提升途径：关卡胜利+5，羁绊触发+3，剧情选项符合武将性格+5。
- 降低途径：阵亡-5，剧情选项违背性格-5，被玩家「弃置不用」连续3关-10。

---

## 7. 阵亡与退场

- **本关阵亡**：角色变为「撤退」状态，本关无法再次行动。
- **下关恢复**：满血满MP回归，不损失等级/装备/经验。
- **阵亡台词**：每名角色有 2 条专属退场台词，随机播放 1 条。
- **困难模式可选**：开启「重伤系统」，阵亡后下关 HP 上限-20%（持续1关）。

---

## 8. 角色解锁节奏

| 关卡 | 新解锁角色 | 解锁方式 |
|---|---|---|
| 第1关 晋阳 | 李世民、长孙无忌、柴绍、刘弘基 | 初始可用 |
| 第2关 霍邑 | 殷开山、段志玄 | 剧情加入 |
| 第3关 长安 | 平阳公主、房玄龄 | 剧情加入 |
| 第4关 浅水原 | 李靖、杜如晦 | 剧情投奔 |
| 第5关 柏壁 | 尉迟敬德、长孙皇后 | 尉迟中段招降；皇后远程书信 |
| 第6关 洛阳 | 秦琼、程咬金、侯君集 | 剧情加入 |
| 第7关 虎牢 | — | 全员可用 |
| 第8关 玄武门 | — | 全员可用 |

---

## 9. 与其他文档关联

- **01-story.md**：本文档定义"角色能做什么"，剧情文档定义"角色是什么人"。
- **02-combat.md**：本文档提供角色数值，战斗文档定义"数值如何转化为伤害"。
- **04-levels.md**：本文档定义角色数据，关卡文档定义"每关谁可用、敌军是谁"。
- **05-systems.md**：装备/经验/存档系统影响角色养成方式。

## 版本记录
- v0.1（2026-06-01）初稿——15人完整五维/成长/HPMP/计策/被动/羁绊/忠诚度/阵亡规则
