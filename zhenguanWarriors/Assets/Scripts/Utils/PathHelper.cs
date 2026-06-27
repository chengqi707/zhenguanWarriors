using UnityEngine;

namespace ZhenguanWarriors.Utils
{
    /// <summary>
    /// 路径工具类
    /// </summary>
    public static class PathHelper
    {
        /// <summary>日志目录</summary>
        public static string GetLogDirectory()
        {
            return System.IO.Path.Combine(Application.persistentDataPath, "logs");
        }
    }
}
