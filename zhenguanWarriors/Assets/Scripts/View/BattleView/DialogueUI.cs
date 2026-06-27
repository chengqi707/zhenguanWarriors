using System.Collections.Generic;
using UnityEngine;
using ZhenguanWarriors.Core.Story;
using ZhenguanWarriors.Core.Character;
using ZhenguanWarriors.Core.UI;
using ZhenguanWarriors.Utils;

namespace ZhenguanWarriors.View.BattleView
{
    /// <summary>
    /// 对话UI控制器——渲染剧情对白覆盖层
    /// </summary>
    public class DialogueUI : MonoBehaviour
    {
        private StoryData _currentStory;
        private StoryNode _currentNode;
        private bool _isActive;
        private System.Action _onFinish;

        // 头像颜色缓存
        private static readonly Color SpeakerColor = new Color(0.9f, 0.7f, 0.2f);
        private static readonly Color TextColor = Color.white;
        private static readonly Color NameColor = new Color(0.4f, 0.8f, 1.0f);

        void OnGUI()
        {
            if (!_isActive || _currentNode == null) return;
            DrawDialogue();
        }

        /// <summary>开始播放剧情</summary>
        public void PlayStory(string storyId, System.Action onFinish = null)
        {
            var story = StoryLibrary.Get(storyId);
            if (story == null)
            {
                GameLogger.LogWarningFormat(LogCategory.UI, "剧情不存在|storyId={0}", storyId);
                onFinish?.Invoke();
                return;
            }
            PlayStory(story, onFinish);
        }

        /// <summary>开始播放剧情</summary>
        public void PlayStory(StoryData story, System.Action onFinish = null)
        {
            _currentStory = story;
            _onFinish = onFinish;
            _isActive = true;

            // 跳转到起始节点
            if (story.nodes.TryGetValue(story.startNode, out var startNode))
            {
                _currentNode = startNode;
            }
            else
            {
                GameLogger.LogWarningFormat(LogCategory.UI, "剧情无起始节点|storyId={0}", story.storyId);
                FinishDialogue();
            }
        }

        /// <summary>是否正在播放对话</summary>
        public bool IsActive => _isActive;

        /// <summary>快进/跳过当前对话（返回键触发）</summary>
        public void FastForward()
        {
            if (!_isActive) return;
            // 直接跳到对话结束
            FinishDialogue();
        }

        private void DrawDialogue()
        {
            // 半透明遮罩（深褐色）
            GUI.backgroundColor = Theme.BgDark;
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");
            GUI.backgroundColor = Color.white;

            float w = Screen.width * 0.8f;
            float h = 240;
            float x = (Screen.width - w) / 2;
            float y = Screen.height - h - 20;

            // 对话背景（唐风面板）
            Theme.DrawPanel(new Rect(x, y, w, h));

            // 顶部朱红装饰线
            GUI.backgroundColor = Theme.Primary;
            GUI.Box(new Rect(x, y, w, 3), "");
            GUI.backgroundColor = Color.white;

            // 头像（生成的角色头像）
            float avatarSize = 80;
            float avatarX = x + 25;
            float avatarY = y + 25;
            var portrait = PortraitGenerator.GetPortrait(_currentNode.speakerId);
            if (portrait != null)
                GUI.DrawTexture(new Rect(avatarX, avatarY, avatarSize, avatarSize), portrait);

            // 头像边框
            GUI.backgroundColor = Theme.Gold;
            GUI.Box(new Rect(avatarX - 2, avatarY - 2, avatarSize + 4, avatarSize + 4), "");
            GUI.backgroundColor = Color.white;

            // 说话人名字
            float textX = avatarX + avatarSize + 25;
            float textW = w - avatarSize - 60;
            GUI.Label(new Rect(textX, y + 20, textW, 28),
                _currentNode.speaker,
                Theme.MakeLabel(20, FontStyle.Bold, Theme.Gold));

            // 对白文本（支持多行）
            float textY = y + 55;
            float textH = 90;
            GUI.Label(new Rect(textX, textY, textW, textH),
                _currentNode.text,
                Theme.MakeLabel(16, FontStyle.Normal, Theme.TextLight));
            // 多行支持
            GUI.Label(new Rect(textX, textY, textW, textH),
                _currentNode.text,
                new GUIStyle { fontSize = 16, wordWrap = true,
                    normal = { textColor = Theme.TextLight } });

            // 选项 / 点击继续
            float btnY = y + h - 40;

            if (_currentNode.choices != null && _currentNode.choices.Count > 0)
            {
                // 显示选项按钮
                float choiceStartX = x + 50;
                for (int i = 0; i < _currentNode.choices.Count; i++)
                {
                    var choice = _currentNode.choices[i];
                    float btnW2 = 300;
                    if (GUI.Button(new Rect(choiceStartX + i * (btnW2 + 10), btnY, btnW2, 30),
                        $"{choice.text}{(string.IsNullOrEmpty(choice.effectDesc) ? "" : $" ({choice.effectDesc})")}"))
                    {
                        SelectChoice(i);
                    }
                }
            }
            else
            {
                // "点击继续" 提示
                GUI.Label(new Rect(x + w - 120, btnY, 110, 25),
                    "点击任意处继续 →",
                    new GUIStyle { fontSize = 12, normal = { textColor = Color.gray },
                        alignment = TextAnchor.MiddleRight });

                // 检测任意点击
                if (Event.current.type == EventType.MouseDown || Event.current.type == EventType.TouchDown)
                {
                    AdvanceDialogue();
                }
            }

            // 剧情标题（右上角）
            GUI.Label(new Rect(x + w - 200, y + 5, 190, 20),
                _currentStory?.title ?? "",
                new GUIStyle { fontSize = 11, normal = { textColor = Color.gray },
                    alignment = TextAnchor.MiddleRight });
        }

        private void AdvanceDialogue()
        {
            if (_currentNode.nextNode < 0)
            {
                FinishDialogue();
                return;
            }

            if (_currentStory.nodes.TryGetValue(_currentNode.nextNode, out var nextNode))
            {
                _currentNode = nextNode;
            }
            else
            {
                FinishDialogue();
            }
        }

        private void SelectChoice(int index)
        {
            if (_currentNode.choices == null || index >= _currentNode.choices.Count) return;

            var choice = _currentNode.choices[index];
            if (choice.nextNode < 0)
            {
                FinishDialogue();
                return;
            }

            if (_currentStory.nodes.TryGetValue(choice.nextNode, out var nextNode))
            {
                _currentNode = nextNode;
            }
            else
            {
                FinishDialogue();
            }
        }

        private void FinishDialogue()
        {
            _isActive = false;
            _currentStory = null;
            _currentNode = null;

            var cb = _onFinish;
            _onFinish = null;
            cb?.Invoke();
        }

        /// <summary>获取说话人头像（角色名缩写）</summary>
        private string GetSpeakerAvatar(string speakerId)
        {
            if (string.IsNullOrEmpty(speakerId)) return "?";
            var charData = CharacterDatabase.Get(speakerId);
            if (charData != null)
            {
                string name = charData.Name;
                if (name.Length >= 2) return name.Substring(0, 2);
                return name;
            }
            // 按角色名映射
            return _currentNode?.speaker != null && _currentNode.speaker.Length >= 2
                ? _currentNode.speaker.Substring(0, 2)
                : "??";
        }
    }
}
