using System;
using System.Collections.Generic;
using System.IO;

namespace TrafficView
{
    internal static class AppLog
    {
        private const string ApplicationDirectoryName = "TrafficView";
        private const string LogDirectoryName = "Logs";
        private const string LogFileName = "TrafficView.log";
        private const long MaxLogFileBytes = 256L * 1024L;
        private static readonly object SyncRoot = new object();
        private static readonly HashSet<string> LoggedOnceKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static void Info(string message)
        {
            Write("INFO", message, null);
        }

        public static void Warn(string message, Exception exception = null)
        {
            Write("WARN", message, exception);
        }

        public static void WarnOnce(string key, string message, Exception exception = null)
        {
            if (!TryMarkOnce(key))
            {
                return;
            }

            Write("WARN", message, exception);
        }

        public static void Error(string message, Exception exception = null)
        {
            Write("ERROR", message, exception);
        }

        private static bool TryMarkOnce(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return true;
            }

            lock (SyncRoot)
            {
                if (LoggedOnceKeys.Contains(key))
                {
                    return false;
                }

                LoggedOnceKeys.Add(key);
                return true;
            }
        }

        private static void Write(string level, string message, Exception exception)
        {
            try
            {
                string logPath = GetLogPath();
                if (string.IsNullOrWhiteSpace(logPath))
                {
                    return;
                }

                lock (SyncRoot)
                {
                    string directory = Path.GetDirectoryName(logPath);
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    RotateIfNeeded(logPath);

                    using (FileStream stream = new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    using (StreamWriter writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false)))
                    {
                        writer.WriteLine(CreateLogLine(level, message, exception));
                    }
                }
            }
            catch
            {
            }
        }

        private static string CreateLogLine(string level, string message, Exception exception)
        {
            string line = string.Format(
                "{0:yyyy-MM-dd HH:mm:ss.fff} [{1}] {2}",
                DateTime.Now,
                string.IsNullOrWhiteSpace(level) ? "INFO" : level,
                Sanitize(message));

            if (exception != null)
            {
                line += " | " + Sanitize(DescribeException(exception));
            }

            return line;
        }

        private static string DescribeException(Exception exception)
        {
            List<string> parts = new List<string>();
            Exception current = exception;
            int depth = 0;

            while (current != null && depth < 4)
            {
                parts.Add(current.GetType().Name + ": " + current.Message);
                current = current.InnerException;
                depth++;
            }

            return string.Join(" -> ", parts.ToArray());
        }

        private static string Sanitize(string value)
        {
            return (value ?? string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " ");
        }

        private static string GetLogPath()
        {
            string baseDirectory;

            try
            {
                baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }
            catch
            {
                baseDirectory = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            }

            return Path.Combine(baseDirectory, ApplicationDirectoryName, LogDirectoryName, LogFileName);
        }

        private static void RotateIfNeeded(string logPath)
        {
            try
            {
                if (!File.Exists(logPath))
                {
                    return;
                }

                FileInfo info = new FileInfo(logPath);
                if (info.Length < MaxLogFileBytes)
                {
                    return;
                }

                string backupPath = logPath + ".1";
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }

                File.Move(logPath, backupPath);
            }
            catch
            {
            }
        }
    }
}
