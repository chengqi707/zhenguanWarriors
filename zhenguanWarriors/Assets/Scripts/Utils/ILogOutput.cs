namespace ZhenguanWarriors.Utils
{
    /// <summary>
    /// 日志输出目标接口
    /// </summary>
    public interface ILogOutput
    {
        /// <summary>输出一条日志</summary>
        void Write(LogLevel level, string categoryName, string message);

        /// <summary>立即刷新缓冲区</summary>
        void Flush();

        /// <summary>关闭输出</summary>
        void Close();
    }
}
