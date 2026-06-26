using System.Collections.Generic;

namespace ZhenguanWarriors.Core.Story
{
    /// <summary>
    /// 剧情对话数据类型
    /// </summary>
    public enum StoryTriggerType
    {
        BeforeLevel,    // 关前剧情
        AfterLevel,     // 关后剧情
        MidLevel,       // 关内剧情（特定回合触发）
        OnVisit         // 访问特定格子触发
    }

    /// <summary>
    /// 对话中的选项
    /// </summary>
    public class StoryChoice
    {
        public string text;          // 选项文本
        public int nextNode;         // 跳转到哪个节点（-1 = 结束）
        public string effectDesc;    // 效果描述（"攻击+5"等）
    }

    /// <summary>
    /// 对话节点（一个说话者的一段话）
    /// </summary>
    public class StoryNode
    {
        public int id;                      // 节点ID
        public string speaker;              // 说话人
        public string speakerId;            // 说话人角色ID（用于头像）
        public string text;                 // 对白文本
        public int nextNode;                // 下一节点ID（-1 = 结束）
        public List<StoryChoice> choices;   // 选项（null = 自动推进）
        public string onEnterEffect;        // 进入该节点时触发效果（"unlock_xx"等）
    }

    /// <summary>
    /// 剧情对话数据——一场完整的对话序列
    /// </summary>
    public class StoryData
    {
        public string storyId;                  // 剧情唯一ID
        public string title;                    // 剧情标题（用于保存进度）
        public StoryTriggerType triggerType;    // 触发类型
        public string levelId;                  // 所属关卡
        public int triggerTurn;                 // 触发回合（MidLevel用）
        public Dictionary<int, StoryNode> nodes; // 节点字典
        public int startNode;                   // 起始节点ID

        public StoryData()
        {
            nodes = new Dictionary<int, StoryNode>();
        }
    }

    /// <summary>
    /// 故事库——预定义全部剧情对话
    /// </summary>
    public static class StoryLibrary
    {
        private static Dictionary<string, StoryData> _stories;

        public static StoryData Get(string id)
        {
            if (_stories == null) Build();
            return _stories.TryGetValue(id, out var s) ? s : null;
        }

        public static Dictionary<string, StoryData> GetAll()
        {
            if (_stories == null) Build();
            return _stories;
        }

        private static void Build()
        {
            _stories = new Dictionary<string, StoryData>();

            // ===== 第1关·关前：晋阳起兵 =====
            _stories["story_01_pre"] = new StoryData
            {
                storyId = "story_01_pre",
                title = "晋阳起兵",
                triggerType = StoryTriggerType.BeforeLevel,
                levelId = "level_01",
                startNode = 1,
                nodes = new Dictionary<int, StoryNode>
                {
                    { 1, new StoryNode { id = 1, speaker = "李世民", speakerId = "lishimin", text = "隋炀帝无道，天下大乱。父亲李渊在晋阳起兵，正是英雄用武之时。", nextNode = 2 } },
                    { 2, new StoryNode { id = 2, speaker = "李世民", speakerId = "lishimin", text = "我李世民愿率精兵，扫平群雄，开创太平盛世！", nextNode = 3 } },
                    { 3, new StoryNode { id = 3, speaker = "李靖", speakerId = "li_jing", text = "世民兄，前方即是隋军校尉的营寨。你我联手，必能一鼓而下！", nextNode = 4 } },
                    { 4, new StoryNode { id = 4, speaker = "李世民", speakerId = "lishimin", text = "好！此战乃我大唐开国第一战，只许胜，不许败！全军出击！", nextNode = -1 } }
                }
            };

            // ===== 第1关·关后：首战告捷 =====
            _stories["story_01_post"] = new StoryData
            {
                storyId = "story_01_post",
                title = "首战告捷",
                triggerType = StoryTriggerType.AfterLevel,
                levelId = "level_01",
                startNode = 1,
                nodes = new Dictionary<int, StoryNode>
                {
                    { 1, new StoryNode { id = 1, speaker = "李世民", speakerId = "lishimin", text = "隋军不堪一击！看来天下唾手可得。", nextNode = 2 } },
                    { 2, new StoryNode { id = 2, speaker = "李靖", speakerId = "li_jing", text = "世民兄不可轻敌。隋朝虽衰，各地军阀实力不可小觑。我听闻霍邑有宋老生驻守，兵精粮足。", nextNode = 3 } },
                    { 3, new StoryNode { id = 3, speaker = "李世民", speakerId = "lishimin", text = "不错。整军备战，下一站——霍邑！", nextNode = -1 } }
                }
            };

            // ===== 第2关·关前：霍邑攻坚 =====
            _stories["story_02_pre"] = new StoryData
            {
                storyId = "story_02_pre",
                title = "霍邑攻坚",
                triggerType = StoryTriggerType.BeforeLevel,
                levelId = "level_02",
                startNode = 1,
                nodes = new Dictionary<int, StoryNode>
                {
                    { 1, new StoryNode { id = 1, speaker = "李世民", speakerId = "lishimin", text = "霍邑城坚，宋老生善守。正面强攻不易，需用计取之。", nextNode = 2 } },
                    { 2, new StoryNode { id = 2, speaker = "长孙无忌", speakerId = "zhangsun_wuji", text = "主公，天降大雨，正是用计良机。敌军火器难施，我军可趁机进攻。", nextNode = 3 } },
                    { 3, new StoryNode { id = 3, speaker = "李世民", speakerId = "lishimin", text = "好！传令三军，冒雨攻城，拿下霍邑！", nextNode = -1 } }
                }
            };

            // ===== 第2关·关后：霍邑大捷 =====
            _stories["story_02_post"] = new StoryData
            {
                storyId = "story_02_post",
                title = "霍邑大捷",
                triggerType = StoryTriggerType.AfterLevel,
                levelId = "level_02",
                startNode = 1,
                nodes = new Dictionary<int, StoryNode>
                {
                    { 1, new StoryNode { id = 1, speaker = "李世民", speakerId = "lishimin", text = "宋老生已败，霍邑城破！此乃天意助我大唐。", nextNode = 2 } },
                    { 2, new StoryNode { id = 2, speaker = "段志玄", speakerId = "duan_zhixuan", text = "主公神机妙算，末将佩服！下一步是否直取长安？", nextNode = 3 } },
                    { 3, new StoryNode { id = 3, speaker = "李世民", speakerId = "lishimin", text = "不错。关中乃龙兴之地，长安势在必得。整军，西进！", nextNode = -1 } }
                }
            };

            // ===== 第3关·关前：直取长安 =====
            _stories["story_03_pre"] = new StoryData
            {
                storyId = "story_03_pre",
                title = "直取长安",
                triggerType = StoryTriggerType.BeforeLevel,
                levelId = "level_03",
                startNode = 1,
                nodes = new Dictionary<int, StoryNode>
                {
                    { 1, new StoryNode { id = 1, speaker = "李世民", speakerId = "lishimin", text = "长安乃隋都，城墙坚固，护城河深阔。", nextNode = 2 } },
                    { 2, new StoryNode { id = 2, speaker = "平阳公主", speakerId = "pingyang_princess", text = "二弟放心！我已联络城内各方势力，届时里应外合，长安必破！", nextNode = 3 } },
                    { 3, new StoryNode { id = 3, speaker = "李世民", speakerId = "lishimin", text = "三姐深谋远虑，世民佩服。好，攻取长安，定鼎关中！", nextNode = -1 } }
                }
            };

            // ===== 第3关·关后：定鼎关中 =====
            _stories["story_03_post"] = new StoryData
            {
                storyId = "story_03_post",
                title = "定鼎关中",
                triggerType = StoryTriggerType.AfterLevel,
                levelId = "level_03",
                startNode = 1,
                nodes = new Dictionary<int, StoryNode>
                {
                    { 1, new StoryNode { id = 1, speaker = "李世民", speakerId = "lishimin", text = "长安已下，关中平定。父亲称帝在即，我大唐基业初成。", nextNode = 2 } },
                    { 2, new StoryNode { id = 2, speaker = "房玄龄", speakerId = "fang_xuanling", text = "主公，西秦薛举仍虎视眈眈，不可掉以轻心。", nextNode = 3 } },
                    { 3, new StoryNode { id = 3, speaker = "李世民", speakerId = "lishimin", text = "玄龄所言甚是。传令整军，目标浅水原！", nextNode = -1 } }
                }
            };

            // ===== 第4关·关前：浅水原 =====
            _stories["story_04_pre"] = new StoryData
            {
                storyId = "story_04_pre",
                title = "浅水原之战",
                triggerType = StoryTriggerType.BeforeLevel,
                levelId = "level_04",
                startNode = 1,
                nodes = new Dictionary<int, StoryNode>
                {
                    { 1, new StoryNode { id = 1, speaker = "李世民", speakerId = "lishimin", text = "薛举占据陇西，号称西秦霸王，兵锋正盛。", nextNode = 2 } },
                    { 2, new StoryNode { id = 2, speaker = "李靖", speakerId = "li_jing", text = "薛举骑兵精锐，平原作战占尽优势。我军应诱敌深入，以计破之。", nextNode = 3 } },
                    { 3, new StoryNode { id = 3, speaker = "李世民", speakerId = "lishimin", text = "此战关乎关中安危，诸位随我并肩一战！", nextNode = -1 } }
                }
            };

            // ===== 第4关·关后：西秦溃败 =====
            _stories["story_04_post"] = new StoryData
            {
                storyId = "story_04_post",
                title = "西秦溃败",
                triggerType = StoryTriggerType.AfterLevel,
                levelId = "level_04",
                startNode = 1,
                nodes = new Dictionary<int, StoryNode>
                {
                    { 1, new StoryNode { id = 1, speaker = "李世民", speakerId = "lishimin", text = "薛举败退，西秦再也无力东进。关中可保安稳了。", nextNode = 2 } },
                    { 2, new StoryNode { id = 2, speaker = "秦琼", speakerId = "qin_qiong", text = "主公，北方刘武周勾结突厥，正在攻打柏壁。", nextNode = 3 } },
                    { 3, new StoryNode { id = 3, speaker = "李世民", speakerId = "lishimin", text = "北方告急，不可迟疑。全军北上，救援柏壁！", nextNode = -1 } }
                }
            };

            // ===== 第5关·关前：柏壁 =====
            _stories["story_05_pre"] = new StoryData
            {
                storyId = "story_05_pre",
                title = "柏壁之战",
                triggerType = StoryTriggerType.BeforeLevel,
                levelId = "level_05",
                startNode = 1,
                nodes = new Dictionary<int, StoryNode>
                {
                    { 1, new StoryNode { id = 1, speaker = "李世民", speakerId = "lishimin", text = "刘武周勾结突厥，攻陷柏壁，形势危急。", nextNode = 2 } },
                    { 2, new StoryNode { id = 2, speaker = "尉迟敬德", speakerId = "yuchi_jingde", text = "末将愿率本部精兵，死守柏壁城！人在城在！", nextNode = 3 } },
                    { 3, new StoryNode { id = 3, speaker = "李世民", speakerId = "lishimin", text = "敬德将军勇冠三军！此战只需守住十日，援军必到！", nextNode = -1 } }
                }
            };

            // ===== 第5关·关后：柏壁坚守 =====
            _stories["story_05_post"] = new StoryData
            {
                storyId = "story_05_post",
                title = "柏壁坚守",
                triggerType = StoryTriggerType.AfterLevel,
                levelId = "level_05",
                startNode = 1,
                nodes = new Dictionary<int, StoryNode>
                {
                    { 1, new StoryNode { id = 1, speaker = "李世民", speakerId = "lishimin", text = "十日坚守，刘武周终于退兵。敬德将军，此战首功非你莫属。", nextNode = 2 } },
                    { 2, new StoryNode { id = 2, speaker = "尉迟敬德", speakerId = "yuchi_jingde", text = "末将只是尽了本分。主公，何不趁势东进，夺取洛阳？", nextNode = 3 } },
                    { 3, new StoryNode { id = 3, speaker = "李世民", speakerId = "lishimin", text = "正合我意。洛阳王世充，该算总账了。", nextNode = -1 } }
                }
            };

            // ===== 第8关·关前：玄武门 =====
            _stories["story_08_pre"] = new StoryData
            {
                storyId = "story_08_pre",
                title = "玄武门前夜",
                triggerType = StoryTriggerType.BeforeLevel,
                levelId = "level_08",
                startNode = 1,
                nodes = new Dictionary<int, StoryNode>
                {
                    { 1, new StoryNode { id = 1, speaker = "李世民", speakerId = "lishimin", text = "……走到这一步，已无退路。建成、元吉，莫怪世民心狠。", nextNode = 2 } },
                    { 2, new StoryNode { id = 2, speaker = "长孙无忌", speakerId = "zhangsun_wuji", text = "主公，当断不断反受其乱。禁军已经在玄武门集结，成败在此一举。", nextNode = 3 } },
                    { 3, new StoryNode { id = 3, speaker = "李世民", speakerId = "lishimin", text = "好。明日玄武门，赌上我李世民的性命，赌上大唐的未来！", nextNode = -1 } }
                }
            };

            // ===== 第6关·关前：洛阳攻坚战 =====
            _stories["story_06_pre"] = new StoryData
            {
                storyId = "story_06_pre",
                title = "洛阳攻坚战",
                triggerType = StoryTriggerType.BeforeLevel,
                levelId = "level_06",
                startNode = 1,
                nodes = new Dictionary<int, StoryNode>
                {
                    { 1, new StoryNode { id = 1, speaker = "李世民", speakerId = "lishimin", text = "洛阳乃中原腹心，王世充据城固守，此战非同小可。", nextNode = 2 } },
                    { 2, new StoryNode { id = 2, speaker = "殷开山", speakerId = "yin_kaishan", text = "末将愿率器械营破城！洛阳城墙虽厚，也挡不住我军的投石车！", nextNode = 3 } },
                    { 3, new StoryNode { id = 3, speaker = "李世民", speakerId = "lishimin", text = "好！殷将军攻城，李靖将军率骑兵截击援军，此战必胜！", nextNode = -1 } }
                }
            };

            // ===== 第6关·关后：洛阳克复 =====
            _stories["story_06_post"] = new StoryData
            {
                storyId = "story_06_post",
                title = "洛阳克复",
                triggerType = StoryTriggerType.AfterLevel,
                levelId = "level_06",
                startNode = 1,
                nodes = new Dictionary<int, StoryNode>
                {
                    { 1, new StoryNode { id = 1, speaker = "李世民", speakerId = "lishimin", text = "王世充已降，洛阳归我大唐。中原大半已定。", nextNode = 2 } },
                    { 2, new StoryNode { id = 2, speaker = "李靖", speakerId = "li_jing", text = "主公，窦建德率十万大军东进虎牢关，意图救援王世充。", nextNode = 3 } },
                    { 3, new StoryNode { id = 3, speaker = "李世民", speakerId = "lishimin", text = "来得正好。一并击破，天下可定！全军开赴虎牢关！", nextNode = -1 } }
                }
            };

            // ===== 第7关·关前：虎牢关之战 =====
            _stories["story_07_pre"] = new StoryData
            {
                storyId = "story_07_pre",
                title = "虎牢关之战",
                triggerType = StoryTriggerType.BeforeLevel,
                levelId = "level_07",
                startNode = 1,
                nodes = new Dictionary<int, StoryNode>
                {
                    { 1, new StoryNode { id = 1, speaker = "李世民", speakerId = "lishimin", text = "虎牢关乃洛阳东部门户，窦建德率十万大军来援王世充。", nextNode = 2 } },
                    { 2, new StoryNode { id = 2, speaker = "秦琼", speakerId = "qin_qiong", text = "末将愿与敬德兄同守关隘！管他十万百万，叫他有来无回！", nextNode = 3 } },
                    { 3, new StoryNode { id = 3, speaker = "尉迟敬德", speakerId = "yuchi_jingde", text = "俺也一样！", nextNode = 4 } },
                    { 4, new StoryNode { id = 4, speaker = "李世民", speakerId = "lishimin", text = "哈哈！有二位虎将在，窦建德不足为惧！此战擒贼擒王，击破窦建德则敌军自溃！", nextNode = -1 } }
                }
            };

            // ===== 第7关·关后：双雄归心 =====
            _stories["story_07_post"] = new StoryData
            {
                storyId = "story_07_post",
                title = "双雄归心",
                triggerType = StoryTriggerType.AfterLevel,
                levelId = "level_07",
                startNode = 1,
                nodes = new Dictionary<int, StoryNode>
                {
                    { 1, new StoryNode { id = 1, speaker = "李世民", speakerId = "lishimin", text = "窦建德已破，王世充孤立无援，中原大局已定！", nextNode = 2 } },
                    { 2, new StoryNode { id = 2, speaker = "秦琼", speakerId = "qin_qiong", text = "恭喜主公！接下来便是——玄武门了。", nextNode = 3 } },
                    { 3, new StoryNode { id = 3, speaker = "李世民", speakerId = "lishimin", text = "……是啊。玄武门。该来的终究要来。", nextNode = -1 } }
                }
            };

            // ===== 第8关·关后：贞观之治 =====
            _stories["story_08_post"] = new StoryData
            {
                storyId = "story_08_post",
                title = "贞观之治",
                triggerType = StoryTriggerType.AfterLevel,
                levelId = "level_08",
                startNode = 1,
                nodes = new Dictionary<int, StoryNode>
                {
                    { 1, new StoryNode { id = 1, speaker = "李世民", speakerId = "lishimin", text = "大唐建立，百废待兴。从今往后，我便是一国之君。", nextNode = 2 } },
                    { 2, new StoryNode { id = 2, speaker = "长孙皇后", speakerId = "zhangsun_empress", text = "陛下英明神武，必能开创前所未有的盛世。", nextNode = 3 } },
                    { 3, new StoryNode { id = 3, speaker = "李世民", speakerId = "lishimin", text = "愿与诸位君臣同心，共创贞观盛世！\n\n—— 全剧终 ——", nextNode = -1 } }
                }
            };
        }
    }
}
