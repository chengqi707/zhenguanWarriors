// ============================================================
// 剧情对话——文本移植自 Unity Core/Story/StoryData.cs（18 场：
// 序幕（§16.4 原创）+ 8 关 × 关前/关后）。Unity 原始 id 为
// story_01_pre 形式，H5 按 types.ts 约定改用 story_1_pre 形式。
// speakerId→portrait；选项分支（choices）按 H5 设计不做。
// ============================================================
import type { StoryScene } from '../core/types';

export const STORIES: StoryScene[] = [
  // ===== 序幕（§16.4，原创：615 年雁门救驾，无 Unity 对应文本） =====
  {
    id: 'story_0_pre', // 雁门救驾
    lines: [
      { speaker: '长孙无忌', portrait: 'zhangsun_wuji', text: '大业十一年秋，突厥始毕可汗率数十万骑南下，将天子围于雁门城中。' },
      { speaker: '长孙无忌', portrait: 'zhangsun_wuji', text: '城中粮草日尽，四方援军未至，满朝文武束手无策。' },
      { speaker: '李世民', portrait: 'lishimin', text: '我李世民年方十六，已随云定兴将军星夜驰援。突厥虽众，不过是骄兵！' },
      { speaker: '李世民', portrait: 'lishimin', text: '无忌，突厥不知我军虚实。我有一计——多张旗鼓、绵延数十里，伪作大军云集之象！' },
      { speaker: '长孙无忌', portrait: 'zhangsun_wuji', text: '疑兵之计！虚张声势，使突厥以为援军已至，必不敢恋战。公子此计大妙。' },
      { speaker: '李世民', portrait: 'lishimin', text: '正是。不过疑兵还需真刀真枪托底——前方突厥游骑已至，先破其前锋，壮我军声威！' },
      { speaker: '李世民', portrait: 'lishimin', text: '全军听令：击溃来敌，解雁门之围！' },
    ],
  },
  {
    id: 'story_0_post', // 疑兵退敌
    lines: [
      { speaker: '长孙无忌', portrait: 'zhangsun_wuji', text: '公子快看！突厥前锋已溃，始毕可汗见我军旗鼓蔽野，果然下令北撤了！' },
      { speaker: '李世民', portrait: 'lishimin', text: '哈哈！兵者，诡道也。突厥畏我援军之势，不战自退。雁门之围，解了！' },
      { speaker: '长孙无忌', portrait: 'zhangsun_wuji', text: '云定兴将军已将公子献计之功奏报天子。十六岁献疑兵退十万之敌，此战必传为佳话。' },
      { speaker: '李世民', portrait: 'lishimin', text: '此不过牛刀小试。天子虽脱困，然隋室气数已尽，民不聊生……' },
      { speaker: '李世民', portrait: 'lishimin', text: '无忌，你看着吧——这天下，很快就要变了。' },
      { speaker: '长孙无忌', portrait: 'zhangsun_wuji', text: '次年，天下大乱。李渊于晋阳起兵，一段新的传奇，就此开始……' },
    ],
  },
  // ===== 第1关（Unity story_01_pre/post） =====
  {
    id: 'story_1_pre', // 晋阳起兵
    lines: [
      { speaker: '李世民', portrait: 'lishimin', text: '隋炀帝无道，天下大乱。父亲李渊在晋阳起兵，正是英雄用武之时。' },
      { speaker: '李世民', portrait: 'lishimin', text: '我李世民愿率精兵，扫平群雄，开创太平盛世！' },
      { speaker: '李靖', portrait: 'li_jing', text: '世民兄，前方即是隋军校尉的营寨。你我联手，必能一鼓而下！' },
      { speaker: '李世民', portrait: 'lishimin', text: '好！此战乃我大唐开国第一战，只许胜，不许败！全军出击！' },
    ],
  },
  {
    id: 'story_1_post', // 首战告捷
    lines: [
      { speaker: '李世民', portrait: 'lishimin', text: '隋军不堪一击！看来天下唾手可得。' },
      { speaker: '李靖', portrait: 'li_jing', text: '世民兄不可轻敌。隋朝虽衰，各地军阀实力不可小觑。我听闻霍邑有宋老生驻守，兵精粮足。' },
      { speaker: '李世民', portrait: 'lishimin', text: '不错。整军备战，下一站——霍邑！' },
    ],
  },
  // ===== 第2关 =====
  {
    id: 'story_2_pre', // 霍邑攻坚
    lines: [
      { speaker: '李世民', portrait: 'lishimin', text: '霍邑城坚，宋老生善守。正面强攻不易，需用计取之。' },
      { speaker: '长孙无忌', portrait: 'zhangsun_wuji', text: '主公，天降大雨，正是用计良机。敌军火器难施，我军可趁机进攻。' },
      { speaker: '李世民', portrait: 'lishimin', text: '好！传令三军，冒雨攻城，拿下霍邑！' },
    ],
  },
  {
    id: 'story_2_post', // 霍邑大捷
    lines: [
      { speaker: '李世民', portrait: 'lishimin', text: '宋老生已败，霍邑城破！此乃天意助我大唐。' },
      { speaker: '段志玄', portrait: 'duan_zhixuan', text: '主公神机妙算，末将佩服！下一步是否直取长安？' },
      { speaker: '李世民', portrait: 'lishimin', text: '不错。关中乃龙兴之地，长安势在必得。整军，西进！' },
    ],
  },
  // ===== 第3关 =====
  {
    id: 'story_3_pre', // 直取长安
    lines: [
      { speaker: '李世民', portrait: 'lishimin', text: '长安乃隋都，城墙坚固，护城河深阔。' },
      { speaker: '平阳公主', portrait: 'pingyang_princess', text: '二弟放心！我已联络城内各方势力，届时里应外合，长安必破！' },
      { speaker: '李世民', portrait: 'lishimin', text: '三姐深谋远虑，世民佩服。好，攻取长安，定鼎关中！' },
    ],
  },
  {
    id: 'story_3_post', // 定鼎关中
    lines: [
      { speaker: '李世民', portrait: 'lishimin', text: '长安已下，关中平定。父亲称帝在即，我大唐基业初成。' },
      { speaker: '房玄龄', portrait: 'fang_xuanling', text: '主公，西秦薛举仍虎视眈眈，不可掉以轻心。' },
      { speaker: '李世民', portrait: 'lishimin', text: '玄龄所言甚是。传令整军，目标浅水原！' },
    ],
  },
  // ===== 第4关 =====
  {
    id: 'story_4_pre', // 浅水原之战
    lines: [
      { speaker: '李世民', portrait: 'lishimin', text: '薛举占据陇西，号称西秦霸王，兵锋正盛。' },
      { speaker: '李靖', portrait: 'li_jing', text: '薛举骑兵精锐，平原作战占尽优势。我军应诱敌深入，以计破之。' },
      { speaker: '李世民', portrait: 'lishimin', text: '此战关乎关中安危，诸位随我并肩一战！' },
    ],
  },
  {
    id: 'story_4_post', // 西秦溃败
    lines: [
      { speaker: '李世民', portrait: 'lishimin', text: '薛举败退，西秦再也无力东进。关中可保安稳了。' },
      { speaker: '秦琼', portrait: 'qin_qiong', text: '主公，北方刘武周勾结突厥，正在攻打柏壁。' },
      { speaker: '李世民', portrait: 'lishimin', text: '北方告急，不可迟疑。全军北上，救援柏壁！' },
    ],
  },
  // ===== 第5关 =====
  {
    id: 'story_5_pre', // 柏壁之战
    lines: [
      { speaker: '李世民', portrait: 'lishimin', text: '刘武周勾结突厥，攻陷柏壁，形势危急。' },
      { speaker: '尉迟敬德', portrait: 'yuchi_jingde', text: '末将愿率本部精兵，死守柏壁城！人在城在！' },
      { speaker: '李世民', portrait: 'lishimin', text: '敬德将军勇冠三军！此战只需守住十日，援军必到！' },
    ],
  },
  {
    id: 'story_5_post', // 柏壁坚守
    lines: [
      { speaker: '李世民', portrait: 'lishimin', text: '十日坚守，刘武周终于退兵。敬德将军，此战首功非你莫属。' },
      { speaker: '尉迟敬德', portrait: 'yuchi_jingde', text: '末将只是尽了本分。主公，何不趁势东进，夺取洛阳？' },
      { speaker: '李世民', portrait: 'lishimin', text: '正合我意。洛阳王世充，该算总账了。' },
    ],
  },
  // ===== 第6关 =====
  {
    id: 'story_6_pre', // 洛阳攻坚战
    lines: [
      { speaker: '李世民', portrait: 'lishimin', text: '洛阳乃中原腹心，王世充据城固守，此战非同小可。' },
      { speaker: '殷开山', portrait: 'yin_kaishan', text: '末将愿率器械营破城！洛阳城墙虽厚，也挡不住我军的投石车！' },
      { speaker: '李世民', portrait: 'lishimin', text: '好！殷将军攻城，李靖将军率骑兵截击援军，此战必胜！' },
    ],
  },
  {
    id: 'story_6_post', // 洛阳克复
    lines: [
      { speaker: '李世民', portrait: 'lishimin', text: '王世充已降，洛阳归我大唐。中原大半已定。' },
      { speaker: '李靖', portrait: 'li_jing', text: '主公，窦建德率十万大军东进虎牢关，意图救援王世充。' },
      { speaker: '李世民', portrait: 'lishimin', text: '来得正好。一并击破，天下可定！全军开赴虎牢关！' },
    ],
  },
  // ===== 第7关 =====
  {
    id: 'story_7_pre', // 虎牢关之战
    lines: [
      { speaker: '李世民', portrait: 'lishimin', text: '虎牢关乃洛阳东部门户，窦建德率十万大军来援王世充。' },
      { speaker: '秦琼', portrait: 'qin_qiong', text: '末将愿与敬德兄同守关隘！管他十万百万，叫他有来无回！' },
      { speaker: '尉迟敬德', portrait: 'yuchi_jingde', text: '俺也一样！' },
      { speaker: '李世民', portrait: 'lishimin', text: '哈哈！有二位虎将在，窦建德不足为惧！此战擒贼擒王，击破窦建德则敌军自溃！' },
    ],
  },
  {
    id: 'story_7_post', // 双雄归心
    lines: [
      { speaker: '李世民', portrait: 'lishimin', text: '窦建德已破，王世充孤立无援，中原大局已定！' },
      { speaker: '秦琼', portrait: 'qin_qiong', text: '恭喜主公！接下来便是——玄武门了。' },
      { speaker: '李世民', portrait: 'lishimin', text: '……是啊。玄武门。该来的终究要来。' },
    ],
  },
  // ===== 第8关 =====
  {
    id: 'story_8_pre', // 玄武门前夜
    lines: [
      { speaker: '李世民', portrait: 'lishimin', text: '……走到这一步，已无退路。建成、元吉，莫怪世民心狠。' },
      { speaker: '长孙无忌', portrait: 'zhangsun_wuji', text: '主公，当断不断反受其乱。禁军已经在玄武门集结，成败在此一举。' },
      { speaker: '李世民', portrait: 'lishimin', text: '好。明日玄武门，赌上我李世民的性命，赌上大唐的未来！' },
    ],
  },
  {
    id: 'story_8_post', // 贞观之治
    lines: [
      { speaker: '李世民', portrait: 'lishimin', text: '大唐建立，百废待兴。从今往后，我便是一国之君。' },
      { speaker: '长孙皇后', portrait: 'zhangsun_empress', text: '陛下英明神武，必能开创前所未有的盛世。' },
      { speaker: '李世民', portrait: 'lishimin', text: '愿与诸位君臣同心，共创贞观盛世！\n\n—— 全剧终 ——' },
    ],
  },
];
