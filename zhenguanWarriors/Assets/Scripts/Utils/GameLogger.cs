using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ZhenguanWarriors.Utils
{
    /// <summary>
    /// 游戏日志系统统一入口
    /// </summary>
    public static class GameLogger
    {
        private static readonly List<ILogOutput> _outputs = new List<ILogOutput>();
        private static LogSettings _settings = new LogSettings();
        private static bool _initialized = false;

        private static readonly string[] _categoryNames = new string[]
        {
            "System", "Battle", "AI", "Save", "Audio", "UI", "Level", "Network"
        };

        /// <summary>当前运行配置（修改后立即生效）</summary>
        public static LogSettings Settings
        {
            get => _settings;
            set => _settings = value ?? new LogSettings();
        }

        /// <summary>日志文件完整路径，便于调试面板显示</summary>
        public static string LogFilePath { get; private set; }

        /// <summary>初始化日志系统</summary>
        public static void Initialize(LogSettings settings = null)
        {
            if (_initialized) return;
            _initialized = true;

            _settings = settings?.Clone() ?? new LogSettings();

            string logDir = PathHelper.GetLogDirectory();
            LogFilePath = System.IO.Path.Combine(logDir, "game_log.txt");

            _outputs.Clear();

            if (_settings.unityOutputEnabled)
                _outputs.Add(new UnityLogOutput());

            if (_settings.logToFile)
                _outputs.Add(new FileLogOutput(logDir, _settings.maxFileSizeMB));
        }

        /// <summary>重新应用配置（例如从存档加载后）</summary>
        public static void Reconfigure(LogSettings settings)
        {
            if (settings == null) return;

            bool needReinit = _settings.logToFile != settings.logToFile
                           || _settings.maxFileSizeMB != settings.maxFileSizeMB;

            _settings = settings.Clone();

            if (needReinit)
            {
                Shutdown();
                Initialize(_settings);
            }
        }

        /// <summary>关闭所有输出</summary>
        public static void Shutdown()
        {
            foreach (var o in _outputs)
            {
                try { o.Flush(); o.Close(); } catch { }
            }
            _outputs.Clear();
            _initialized = false;
        }

        /// <summary>判断某分类和级别当前是否启用</summary>
        public static bool IsEnabled(LogCategory category, LogLevel level)
        {
            if (!_initialized) Initialize();
            if (level < _settings.globalMinLevel) return false;
            if ((_settings.enabledCategories & category) == 0) return false;
            return true;
        }

        #region 快捷方法（Info/Warning/Error 始终编译，运行时过滤）

        public static void LogInfo(LogCategory category, string message)
        {
            if (!IsEnabled(category, LogLevel.Info)) return;
            Write(category, LogLevel.Info, message);
        }

        public static void LogWarning(LogCategory category, string message)
        {
            if (!IsEnabled(category, LogLevel.Warning)) return;
            Write(category, LogLevel.Warning, message);
        }

        public static void LogError(LogCategory category, string message)
        {
            if (!IsEnabled(category, LogLevel.Error)) return;
            Write(category, LogLevel.Error, message);
        }

        public static void LogFatal(LogCategory category, string message)
        {
            if (!IsEnabled(category, LogLevel.Fatal)) return;
            Write(category, LogLevel.Fatal, message);
        }

        #endregion

        #region 参数化格式（避免过滤后仍进行 string.Format）

        public static void LogInfoFormat(LogCategory category, string format, params object[] args)
        {
            if (!IsEnabled(category, LogLevel.Info)) return;
            Write(category, LogLevel.Info, string.Format(format, args));
        }

        public static void LogWarningFormat(LogCategory category, string format, params object[] args)
        {
            if (!IsEnabled(category, LogLevel.Warning)) return;
            Write(category, LogLevel.Warning, string.Format(format, args));
        }

        public static void LogErrorFormat(LogCategory category, string format, params object[] args)
        {
            if (!IsEnabled(category, LogLevel.Error)) return;
            Write(category, LogLevel.Error, string.Format(format, args));
        }

        #endregion

        #region 编译期条件方法（Release 定义符号可 stripping）

        [System.Diagnostics.Conditional("ZGW_LOG_DEBUG")]
        [System.Diagnostics.Conditional("ZGW_LOG_VERBOSE")]
        public static void LogDebug(LogCategory category, string message)
        {
            if (!IsEnabled(category, LogLevel.Debug)) return;
            Write(category, LogLevel.Debug, message);
        }

        [System.Diagnostics.Conditional("ZGW_LOG_DEBUG")]
        [System.Diagnostics.Conditional("ZGW_LOG_VERBOSE")]
        public static void LogDebugFormat(LogCategory category, string format, params object[] args)
        {
            if (!IsEnabled(category, LogLevel.Debug)) return;
            Write(category, LogLevel.Debug, string.Format(format, args));
        }

        [System.Diagnostics.Conditional("ZGW_LOG_VERBOSE")]
        public static void LogVerbose(LogCategory category, string message)
        {
            if (!IsEnabled(category, LogLevel.Verbose)) return;
            Write(category, LogLevel.Verbose, message);
        }

        [System.Diagnostics.Conditional("ZGW_LOG_VERBOSE")]
        public static void LogVerboseFormat(LogCategory category, string format, params object[] args)
        {
            if (!IsEnabled(category, LogLevel.Verbose)) return;
            Write(category, LogLevel.Verbose, string.Format(format, args));
        }

        #endregion

        /// <summary>
        /// 兼容桥：自动识别旧日志前缀 `[战斗]`、`[存档]`、`[AI-Decide]` 等并路由到对应分类。
        /// 用于快速迁移旧代码，后续应逐步替换为具体分类。
        /// </summary>
        public static void LegacyLog(string rawMessage)
        {
            LogCategory cat = DetectCategory(rawMessage);
            if (!IsEnabled(cat, LogLevel.Info)) return;
            Write(cat, LogLevel.Info, rawMessage);
        }

        private static void Write(LogCategory category, LogLevel level, string message)
        {
            if (!_initialized) Initialize();

            string name = CategoryToName(category);
            foreach (var output in _outputs)
            {
                try
                {
                    output.Write(level, name, message);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameLogger] 输出失败: {e.Message}");
                }
            }
        }

        private static string CategoryToName(LogCategory category)
        {
            int bits = (int)category;
            if (bits == 0 || (bits & (bits - 1)) != 0) // 非 2 的幂或 0
                return "Multiple";

            int idx = (int)System.Math.Log(bits, 2);
            if (idx >= 0 && idx < _categoryNames.Length)
                return _categoryNames[idx];
            return "Unknown";
        }

        private static readonly Regex _prefixRegex = new Regex(@"^\[([^\]]+)\]", RegexOptions.Compiled);

        private static LogCategory DetectCategory(string raw)
        {
            var m = _prefixRegex.Match(raw);
            if (!m.Success) return LogCategory.System;

            string p = m.Groups[1].Value;
            switch (p)
            {
                case "战斗":
                case "StartBattle":
                case "MoveUnitAnimation":
                case "AttackUnit":
                case "EnemyAI":
                case "ExecuteAIAttack":
                case "ResolveUnitOverlaps":
                case "ExecuteAISkill":
                    return LogCategory.Battle;
                case "AI-Decide":
                case "CheckRetreat":
                case "CheckMoveTowards":
                case "PathFinder":
                    return LogCategory.AI;
                case "存档":
                    return LogCategory.Save;
                case "音频":
                    return LogCategory.Audio;
                case "UI":
                case "缩放":
                    return LogCategory.UI;
                case "关卡":
                    return LogCategory.Level;
                default:
                    return LogCategory.System;
            }
        }
    }
}
