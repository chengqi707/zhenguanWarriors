using System.IO;
using UnityEngine;
using ZhenguanWarriors.Utils;

namespace ZhenguanWarriors.View.BattleView
{
    /// <summary>
    /// 调试日志面板——OnGUI 实现，从暂停菜单进入
    /// </summary>
    public class DebugPanel : MonoBehaviour
    {
        private bool _isOpen;
        private float _scale;
        private Vector2 _scrollPos;
        private Vector2 _logScrollPos;
        private string[] _levelNames = { "Verbose", "Debug", "Info", "Warning", "Error", "Fatal" };

        public bool IsOpen => _isOpen;

        void Start()
        {
            _scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            if (_scale < 0.5f) _scale = 0.5f;
            if (_scale > 2.0f) _scale = 2.0f;
        }

        public void Show() => _isOpen = true;
        public void Hide() => _isOpen = false;

        void OnGUI()
        {
            if (!_isOpen) return;
            float s = _scale;

            // 半透明遮罩
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");

            // 面板
            float pw = 720 * s, ph = 640 * s;
            float px = (Screen.width - pw) / 2, py = (Screen.height - ph) / 2;

            GUI.backgroundColor = Theme.BgPanel;
            GUI.Box(new Rect(px, py, pw, ph), "");
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = Theme.Gold;
            GUI.Box(new Rect(px, py, pw, 4 * s), "");

            // 标题
            GUI.Label(new Rect(px, py + 16 * s, pw, 36 * s),
                "🐛 调 试 日 志",
                Theme.MakeLabel((int)(24 * s), FontStyle.Bold, Theme.Gold, TextAnchor.MiddleCenter));

            float margin = 24 * s;
            float contentX = px + margin;
            float contentY = py + 60 * s;
            float contentW = pw - margin * 2;

            var settings = GameLogger.Settings;

            // 全局级别
            GUI.Label(new Rect(contentX, contentY, 120 * s, 28 * s),
                "最低级别", Theme.MakeLabel((int)(16 * s), FontStyle.Bold));
            int curLevel = (int)settings.globalMinLevel;
            int newLevel = GUI.SelectionGrid(new Rect(contentX + 130 * s, contentY, 420 * s, 28 * s),
                curLevel, _levelNames, 6);
            if (newLevel != curLevel)
                settings.globalMinLevel = (LogLevel)newLevel;
            contentY += 40 * s;

            // 分类开关
            GUI.Label(new Rect(contentX, contentY, contentW, 28 * s),
                "分类过滤", Theme.MakeLabel((int)(16 * s), FontStyle.Bold));
            contentY += 32 * s;

            _scrollPos = GUI.BeginScrollView(
                new Rect(contentX, contentY, contentW, 120 * s),
                _scrollPos,
                new Rect(0, 0, contentW - 20 * s, 160 * s));

            float cx = 0, cy = 0;
            foreach (LogCategory cat in new[] {
                LogCategory.System, LogCategory.Battle, LogCategory.AI, LogCategory.Save,
                LogCategory.Audio, LogCategory.UI, LogCategory.Level, LogCategory.Network
            })
            {
                bool enabled = (settings.enabledCategories & cat) != 0;
                bool newEnabled = GUI.Toggle(new Rect(cx, cy, 150 * s, 28 * s), enabled, cat.ToString());
                if (newEnabled != enabled)
                {
                    if (newEnabled) settings.enabledCategories |= cat;
                    else settings.enabledCategories &= ~cat;
                }
                cx += 160 * s;
                if (cx > contentW - 160 * s)
                {
                    cx = 0;
                    cy += 32 * s;
                }
            }
            GUI.EndScrollView();
            contentY += 130 * s;

            // 输出开关
            bool fileLog = settings.logToFile;
            bool newFileLog = GUI.Toggle(new Rect(contentX, contentY, 180 * s, 28 * s), fileLog, "文件日志");
            if (newFileLog != fileLog)
            {
                settings.logToFile = newFileLog;
                GameLogger.Reconfigure(settings);
            }

            bool unityOut = settings.unityOutputEnabled;
            bool newUnityOut = GUI.Toggle(new Rect(contentX + 200 * s, contentY, 200 * s, 28 * s), unityOut, "Unity/adb 输出");
            if (newUnityOut != unityOut)
                settings.unityOutputEnabled = newUnityOut;
            contentY += 36 * s;

            // 操作按钮
            float btnY = contentY;
            GUI.backgroundColor = Theme.Primary;
            if (GUI.Button(new Rect(contentX, btnY, 120 * s, 36 * s), "清除日志", Theme.MakeButton((int)(14 * s))))
            {
                ClearLogFiles();
            }
            GUI.backgroundColor = Theme.PrimaryDark;
            if (GUI.Button(new Rect(contentX + 130 * s, btnY, 120 * s, 36 * s), "导出日志", Theme.MakeButton((int)(14 * s))))
            {
                ShareLogFile();
            }
            GUI.backgroundColor = Theme.BgCard;
            if (GUI.Button(new Rect(contentX + 260 * s, btnY, 120 * s, 36 * s), "← 返回", Theme.MakeButton((int)(14 * s))))
            {
                Hide();
            }
            GUI.backgroundColor = Color.white;
            contentY += 50 * s;

            // 日志路径
            GUI.Label(new Rect(contentX, contentY, contentW, 20 * s),
                $"日志文件: {GameLogger.LogFilePath}",
                Theme.MakeLabel((int)(12 * s), FontStyle.Normal, Theme.TextDim));
            contentY += 26 * s;

            // 最近日志
            GUI.Label(new Rect(contentX, contentY, contentW, 24 * s),
                "最近日志", Theme.MakeLabel((int)(16 * s), FontStyle.Bold));
            contentY += 26 * s;

            var logs = GameLogger.RecentLogs;
            float logH = ph - (contentY - py) - 20 * s;
            float innerH = Mathf.Max(logH, logs.Count * 20 * s);
            _logScrollPos = GUI.BeginScrollView(
                new Rect(contentX, contentY, contentW, logH),
                _logScrollPos,
                new Rect(0, 0, contentW - 20 * s, innerH));

            float ly = 0;
            for (int i = logs.Count - 1; i >= 0; i--)
            {
                GUI.Label(new Rect(0, ly, contentW - 20 * s, 20 * s), logs[i],
                    Theme.MakeLabel((int)(12 * s), FontStyle.Normal, Theme.TextDim));
                ly += 20 * s;
            }
            GUI.EndScrollView();
        }

        private void ClearLogFiles()
        {
            try
            {
                string dir = PathHelper.GetLogDirectory();
                if (Directory.Exists(dir))
                {
                    foreach (var f in Directory.GetFiles(dir, "game_log*.txt"))
                        File.Delete(f);
                }
                GameLogger.Reconfigure(GameLogger.Settings);
                GameLogger.LogInfo(LogCategory.System, "日志文件已清除");
            }
            catch (System.Exception e)
            {
                GameLogger.LogErrorFormat(LogCategory.System, "清除日志失败|原因={0}", e.Message);
            }
        }

        private void ShareLogFile()
        {
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                string path = GameLogger.LogFilePath;
                if (!File.Exists(path))
                {
                    GameLogger.LogWarning(LogCategory.System, "日志文件不存在，无法导出");
                    return;
                }

                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.SEND"))
                using (var file = new AndroidJavaObject("java.io.File", path))
                {
                    string packageName = activity.Call<string>("getPackageName");
                    string authority = packageName + ".fileprovider";
                    using (var uriClass = new AndroidJavaClass("androidx.core.content.FileProvider"))
                    using (var uri = uriClass.CallStatic<AndroidJavaObject>("getUriForFile", activity, authority, file))
                    {
                        intent.Call<AndroidJavaObject>("setType", "text/plain");
                        intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.STREAM", uri);
                        intent.Call<AndroidJavaObject>("addFlags", 1); // FLAG_GRANT_READ_URI_PERMISSION
                        var chooser = new AndroidJavaObject("android.content.Intent", "android.intent.action.CHOOSER");
                        chooser.Call<AndroidJavaObject>("putExtra", "android.intent.extra.INTENT", intent);
                        activity.Call("startActivity", chooser);
                    }
                }
                GameLogger.LogInfo(LogCategory.System, "日志导出已触发");
#else
                GameLogger.LogInfoFormat(LogCategory.System, "日志文件路径: {0}", GameLogger.LogFilePath);
#endif
            }
            catch (System.Exception e)
            {
                GameLogger.LogErrorFormat(LogCategory.System, "导出日志失败|原因={0}", e.Message);
            }
        }
    }
}
