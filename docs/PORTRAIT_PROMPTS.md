# 《贞观勇士》角色立绘 AI 生成提示词包（15 人）

> 用途：生成 H5 版角色立绘。生成后按命名规范放入 `h5/public/portraits/`，游戏自动使用图片版（缺图角色回退程序绘制）。
> 使用方式：把「统一风格基底 + 角色描述」拼接为完整提示词，逐张生成；中文平台用中文提示词，MJ/SD 用英文提示词。

## 统一规格（重要，保证 15 张风格一致）

- **尺寸**：512×512 或 768×768，PNG（或 JPG），正方形
- **构图**：胸部以上半身像，人物居中，面部朝向正前方或微侧
- **背景**：深褐/暗色渐变纯色背景（避免复杂场景，方便与游戏 UI 融合）
- **风格**：国风写实插画（半厚涂），唐代武将/文士，服饰有金属铠甲质感
- **禁止**：文字、水印、边框、多人、全身像、Q 版/卡通脸

## 统一风格基底提示词

**中文（即梦/通义万相/文心一格）**：
> 国风写实插画，唐代历史人物半身像，胸部以上构图，人物居中，深色渐变纯色背景，半厚涂风格，金属铠甲质感细腻，面部写实有神采，光线从左上方照射，无文字无水印，512×512

**English（Midjourney / Stable Diffusion）**：
> Chinese style realistic illustration, bust portrait of a Tang dynasty figure, chest-up composition, centered, dark gradient plain background, semi-realistic digital painting, detailed metal armor texture, expressive realistic face, lit from upper left, no text, no watermark --ar 1:1

## 15 人角色描述（拼在基底后）

| 文件命名 | 角色 | 中文描述 | English |
|---|---|---|---|
| `lishimin.png` | 李世民 | 20 岁年轻英武君主，剑眉星目，束发金冠，红金色明光铠甲，气质英挺自信，短须 | Young heroic Chinese emperor, age 20, sword-like eyebrows, golden hair crown, red-gold armor, confident |
| `zhangsun_wuji.png` | 长孙无忌 | 中年文士，清瘦，浓黑山羊须，高冠文士巾，紫色长袍，气质沉稳 | Middle-aged scholar-official, slim, black goatee, tall scholar hat, purple robe, calm |
| `fang_xuanling.png` | 房玄龄 | 年长文士，花白山羊须，文士高冠，深色长袍，儒雅睿智 | Elderly scholar, gray-white goatee, scholar hat, dark robe, wise |
| `du_ruhui.png` | 杜如晦 | 中年文士，深棕山羊须，文士冠，深蓝长袍，目光锐利果断 | Middle-aged scholar, dark brown goatee, sharp decisive eyes, deep blue robe |
| `li_jing.png` | 李靖 | 中年儒将，山羊须，铜褐色武巾，铠甲外罩战袍，沉稳威严 | Middle-aged Confucian general, goatee, bronze-brown headwrap, armor with war robe, composed |
| `yuchi_jingde.png` | 尉迟敬德 | 黑面虬髯猛将，怒目倒竖眉，铁兜鍪头盔，黑铁重甲，极其威猛 | Fierce dark-faced general with curly beard, glaring eyes, iron helmet, black heavy armor, intimidating |
| `qin_qiong.png` | 秦琼 | 端正威严武将，金色凤翅盔，铠甲，肩后露出双锏柄，正气凛然 | Dignified righteous general, golden winged phoenix helmet, armor, twin mace handles behind shoulders |
| `cheng_yaojin.png` | 程咬金 | 阔脸环眼，豪爽大笑，虬髯，铁盔，兽面重甲，粗犷 | Broad face with round eyes, hearty laughing warrior, curly beard, iron helmet, beast-faced heavy armor |
| `hou_junji.png` | 侯君集 | 壮年武将，皮帻帽，绿色战袍铠甲，表情坚毅 | Middle-aged warrior, leather cap, green war robe armor, resolute |
| `duan_zhixuan.png` | 段志玄 | 年轻游骑将领，束发抹额，轻甲，英气干练 | Young light cavalry officer, hair band, light armor, sharp and capable |
| `liu_hongji.png` | 刘弘基 | 弓手，束发带，青绿色衣甲，背负箭囊，目光专注 | Archer, hair band, teal armor, quiver on back, focused eyes |
| `yin_kaishan.png` | 殷开山 | 工匠气质武将，软布帽，褐色衣甲，朴实敦厚 | Engineer-like officer, soft cloth cap, brown armor, honest and sturdy |
| `chai_shao.png` | 柴绍 | 青年将领，凤翅盔，银白铠甲，英俊稳重 | Young handsome general, winged helmet, silver-white armor, steady |
| `pingyang_princess.png` | 平阳公主 | 英气女将，高束发髻配红缨，戎装铠甲，英姿飒爽 | Heroic female general, high hair bun with red tassel, battle armor, valiant |
| `zhangsun_empress.png` | 长孙皇后 | 温婉端庄的皇后，高髻金钗，额间花钿，华丽宫装，耳坠 | Gentle elegant empress, high bun with golden hairpin, forehead ornament, palace dress, earrings |

## 操作流程

1. 逐张生成 → 挑最满意的一张（风格一致性 > 单张完美度）
2. 裁为正方形 → 重命名为上表文件名
3. 放入 `h5/public/portraits/`
4. 网页版：`npm run build` 后刷新即生效；单文件版：`npm run build && npm run pack` 后图片会内嵌进 HTML
5. 生成几张放几张即可，缺图角色自动用程序绘制版

## 提示

- Midjourney 可加 `--style raw --s 200` 提升一致性；SD 建议固定 seed 区间 + 同一底模
- 若生成图带复杂背景，可用 remove.bg 去底后填深色渐变底
- 战场小人/地形素材后续如需同步替换，找我从这里扩展管线
