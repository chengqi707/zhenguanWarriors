using System.Collections;
using UnityEngine;
using ZhenguanWarriors.Core.UI;

namespace ZhenguanWarriors.View.BattleView
{
    /// <summary>
    /// 全屏加载页：关卡切换时显示进度条与提示，避免黑屏/卡帧
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        public static LoadingScreen Instance { get; private set; }

        private bool _isOpen;
        private float _progress;
        private string _tip;
        private Coroutine _loadCoroutine;

        private static readonly string[] TIPS = new string[]
        {
            "地形会影响兵种战斗力，平原利于骑兵冲锋。",
            "谋士智力高，释放计策时注意保持安全距离。",
            "装备可通过战后商店购买或出售。",
            "弓兵占据高地可发挥最大射程优势。",
            "武将单挑可能直接斩杀敌方将领。",
            "羁绊同场可激活额外属性加成。",
            "残血单位可退入树林获得防御加成。",
            "每日观看广告可领取额外金币奖励。"
        };

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>
        /// 显示加载页并执行加载协程
        /// </summary>
        /// <param name="loadOperation">接收进度回调的加载步骤</param>
        /// <param name="onComplete">加载完成后回调</param>
        public void Show(System.Func<System.Action<float>, IEnumerator> loadOperation, System.Action onComplete)
        {
            if (_isOpen) return;
            _isOpen = true;
            _progress = 0f;
            _tip = TIPS[Random.Range(0, TIPS.Length)];

            if (_loadCoroutine != null) StopCoroutine(_loadCoroutine);
            _loadCoroutine = StartCoroutine(RunLoad(loadOperation, onComplete));
        }

        private IEnumerator RunLoad(System.Func<System.Action<float>, IEnumerator> loadOperation, System.Action onComplete)
        {
            GameManager.Instance?.SetTransitioning(true);

            // 先渲染一帧 0% 进度，再执行实际加载
            yield return null;

            if (loadOperation != null)
                yield return StartCoroutine(loadOperation(p => _progress = Mathf.Clamp01(p)));
            else
                _progress = 1f;

            yield return null;

            _isOpen = false;
            GameManager.Instance?.SetTransitioning(false);
            onComplete?.Invoke();
        }

        void OnGUI()
        {
            if (!_isOpen) return;

            float s = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            s = Mathf.Clamp(s, 0.6f, 1.5f);

            // 半透明背景
            GUI.backgroundColor = new Color(0.05f, 0.05f, 0.05f, 0.95f);
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");
            GUI.backgroundColor = Color.white;

            float pw = 700 * s, ph = 280 * s;
            float px = (Screen.width - pw) / 2, py = (Screen.height - ph) / 2;

            Theme.DrawPanel(new Rect(px, py, pw, ph));

            // 标题
            Theme.DrawTitle(new Rect(px, py + 20 * s, pw, 50 * s), "⚔ 加载中", (int)(36 * s));

            // 提示
            GUI.Label(new Rect(px + 40 * s, py + 90 * s, pw - 80 * s, 60 * s),
                _tip, Theme.MakeLabel((int)(18 * s), FontStyle.Normal, Theme.Parchment, TextAnchor.MiddleCenter));

            // 进度条底
            float barX = px + 60 * s, barY = py + 170 * s;
            float barW = pw - 120 * s, barH = 24 * s;
            GUI.backgroundColor = Theme.BgCard;
            GUI.Box(new Rect(barX, barY, barW, barH), "");

            // 进度条填充
            float fillW = Mathf.Max(4 * s, barW * _progress);
            GUI.backgroundColor = Theme.Primary;
            GUI.Box(new Rect(barX, barY, fillW, barH), "");
            GUI.backgroundColor = Color.white;

            // 百分比
            GUI.Label(new Rect(barX, barY + barH + 8 * s, barW, 24 * s),
                $"{Mathf.RoundToInt(_progress * 100)}%", Theme.MakeLabel((int)(16 * s), FontStyle.Bold, Theme.Gold, TextAnchor.MiddleCenter));
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
