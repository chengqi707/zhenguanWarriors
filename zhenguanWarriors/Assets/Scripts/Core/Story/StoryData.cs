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
