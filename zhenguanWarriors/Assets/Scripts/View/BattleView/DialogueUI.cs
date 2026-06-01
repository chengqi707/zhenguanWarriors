using System.Collections.Generic;
using UnityEngine;
using ZhenguanWarriors.Core.Story;
using ZhenguanWarriors.Core.Character;
using ZhenguanWarriors.Core.Save;

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
                Debug.LogWarning($"[对话] 剧情不存在: {storyId}");
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
                Debug.LogWarning($"[对话] 剧情 {story.storyId} 无起始节点");
                FinishDialogue();
            }
        }

        /// <summary>是否正在播放对话</summary>
        public bool IsActive => _isActive;

        private void DrawDialogue()
        {
            // 半透明遮罩
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");

            float w = Screen.width * 0.8f;
            float h = 220;
            float x = (Screen.width - w) / 2;
            float y = Screen.height - h - 20;

            // 对话背景
            GUI.Box(new Rect(x, y, w, h), "");

            // 头像占位（圆形区域）
            float avatarSize = 80;
            float avatarX = x + 20;
            float avatarY = y + 20;
            GUI.Box(new Rect(avatarX, avatarY, avatarSize, avatarSize),
                GetSpeakerAvatar(_currentNode.speakerId));

            // 说话人名字
            float textX = avatarX + avatarSize + 20;
            float textW = w - avatarSize - 50;
            GUI.Label(new Rect(textX, y + 15, textW, 25),
                _currentNode.speaker,
                new GUIStyle { fontSize = 18, fontStyle = FontStyle.Bold,
                    normal = { textColor = NameColor } });

            // 对白文本（支持多行）
            float textY = y + 45;
            float textH = 80;
            GUI.Label(new Rect(textX, textY, textW, textH),
                _currentNode.text,
                new GUIStyle { fontSize = 15, wordWrap = true,
                    normal = { textColor = TextColor } });

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
