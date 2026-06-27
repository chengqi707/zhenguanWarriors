using System;

namespace ZhenguanWarriors.Utils
{
    /// <summary>
    /// 日志运行时配置，可序列化存到存档
    /// </summary>
    [Serializable]
    public class LogSettings
    {
        /// <summary>全局最低输出级别，低于此级别的日志不输出</summary>
        public LogLevel globalMinLevel = LogLevel.Info;

        /// <summary>是否启用文件日志</summary>
        public bool logToFile = true;

        /// <summary>单个日志文件大小上限（MB）</summary>
        public int maxFileSizeMB = 5;

        /// <summary>启用的分类位掩码</summary>
        public LogCategory enabledCategories = LogCategory.All;

        /// <summary>Unity Console / adb 是否输出</summary>
        public bool unityOutputEnabled = true;

        public LogSettings Clone()
        {
            return new LogSettings
            {
                globalMinLevel = this.globalMinLevel,
                logToFile = this.logToFile,
                maxFileSizeMB = this.maxFileSizeMB,
                enabledCategories = this.enabledCategories,
                unityOutputEnabled = this.unityOutputEnabled
            };
        }
    }
}
