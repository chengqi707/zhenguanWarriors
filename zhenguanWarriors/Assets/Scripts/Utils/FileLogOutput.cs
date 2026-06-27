using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace ZhenguanWarriors.Utils
{
    /// <summary>
    /// 文件日志输出，支持按大小轮转
    /// </summary>
    public class FileLogOutput : ILogOutput
    {
        private readonly string _basePath;
        private readonly int _maxSizeMB;
        private readonly int _maxBackups = 2;
        private readonly object _lock = new object();

        private StreamWriter _writer;
        private string _currentFile;
        private long _currentSize;

        public FileLogOutput(string basePath, int maxSizeMB)
        {
            _basePath = basePath;
            _maxSizeMB = Mathf.Clamp(maxSizeMB, 1, 50);
            _currentFile = Path.Combine(_basePath, "game_log.txt");

            try
            {
                Directory.CreateDirectory(_basePath);
                RotateIfNeeded();
                OpenWriter();
            }
            catch (Exception e)
            {
                Debug.LogError($"[FileLogOutput] 初始化失败: {e.Message}");
            }
        }

        public void Write(LogLevel level, string categoryName, string message)
        {
            lock (_lock)
            {
                if (_writer == null) return;

                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] [{categoryName}] {message}";
                _writer.WriteLine(line);

                // 简单估算大小，避免每次写都查文件
                _currentSize += Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;
                if (_currentSize > _maxSizeMB * 1024L * 1024L)
                {
                    _writer.Flush();
                    Rotate();
                }
            }
        }

        public void Flush()
        {
            lock (_lock)
            {
                _writer?.Flush();
            }
        }

        public void Close()
        {
            lock (_lock)
            {
                _writer?.Close();
                _writer = null;
            }
        }

        private void OpenWriter()
        {
            try
            {
                _writer = new StreamWriter(_currentFile, append: true, encoding: Encoding.UTF8);
                _writer.AutoFlush = false;
                var info = new FileInfo(_currentFile);
                _currentSize = info.Exists ? info.Length : 0;
            }
            catch (Exception e)
            {
                Debug.LogError($"[FileLogOutput] 打开文件失败: {e.Message}");
                _writer = null;
            }
        }

        private void RotateIfNeeded()
        {
            if (!File.Exists(_currentFile)) return;
            var info = new FileInfo(_currentFile);
            if (info.Length > _maxSizeMB * 1024L * 1024L)
                Rotate();
        }

        private void Rotate()
        {
            Close();

            string path2 = Path.Combine(_basePath, "game_log_2.txt");
            string path1 = Path.Combine(_basePath, "game_log_1.txt");

            try
            {
                if (File.Exists(path2)) File.Delete(path2);
                if (File.Exists(path1)) File.Move(path1, path2);
                if (File.Exists(_currentFile)) File.Move(_currentFile, path1);
            }
            catch (Exception e)
            {
                Debug.LogError($"[FileLogOutput] 轮转失败: {e.Message}");
            }

            OpenWriter();
        }
    }
}
