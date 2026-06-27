using UnityEngine;

namespace ZhenguanWarriors.Utils
{
    /// <summary>
    /// Unity Console / adb logcat 输出
    /// </summary>
    public class UnityLogOutput : ILogOutput
    {
        public void Write(LogLevel level, string categoryName, string message)
        {
            string full = $"[ZGW][{categoryName}] {message}";
            switch (level)
            {
                case LogLevel.Fatal:
                case LogLevel.Error:
                    Debug.LogError(full);
                    break;
                case LogLevel.Warning:
                    Debug.LogWarning(full);
                    break;
                default:
                    Debug.Log(full);
                    break;
            }
        }

        public void Flush() { }
        public void Close() { }
    }
}
