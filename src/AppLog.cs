using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace TrafficView
{
    internal static class AppLog
    {
        private const string ApplicationDirectoryName = "TrafficView";
        private const string LogDirectoryName = "Logs";
        private const string LogFileName = "TrafficView.log";
        private const long MaxLogFileBytes = 256L * 1024L;
        private const int MaxLogBackupFiles = 3;
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

        public static string GetCurrentLogPath()
        {
            return GetLogPath();
        }

        public static string[] GetLogFilePathsForDiagnostics()
        {
            string logPath = GetLogPath();
            List<string> paths = new List<string>();
            if (!string.IsNullOrWhiteSpace(logPath))
            {
                paths.Add(logPath);
                for (int index = 1; index <= MaxLogBackupFiles; index++)
                {
                    paths.Add(GetBackupLogPath(logPath, index));
                }
            }

            return paths.ToArray();
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

                EnsurePortableLogPathAllowed(logPath);

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
            catch (Exception ex)
            {
                SafeTraceAppLogFailure("Write", level, message, ex);
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

            if (!string.IsNullOrWhiteSpace(exception.StackTrace))
            {
                parts.Add("StackTrace: " + exception.StackTrace);
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

            if (AppStorage.IsPortableMode)
            {
                baseDirectory = AppStorage.BaseDirectory;
            }
            else
            {
                try
                {
                    baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                }
                catch (Exception ex)
                {
                    SafeTraceAppLogFailure("GetLogPath", null, null, ex);
                    baseDirectory = string.Empty;
                }
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
                EnsurePortableLogPathAllowed(logPath);

                if (!File.Exists(logPath))
                {
                    return;
                }

                FileInfo info = new FileInfo(logPath);
                if (info.Length < MaxLogFileBytes)
                {
                    return;
                }

                string oldestBackupPath = GetBackupLogPath(logPath, MaxLogBackupFiles);
                EnsurePortableLogPathAllowed(oldestBackupPath);
                if (File.Exists(oldestBackupPath))
                {
                    File.Delete(oldestBackupPath);
                }

                for (int index = MaxLogBackupFiles - 1; index >= 1; index--)
                {
                    string sourceBackupPath = GetBackupLogPath(logPath, index);
                    string targetBackupPath = GetBackupLogPath(logPath, index + 1);
                    EnsurePortableLogPathAllowed(sourceBackupPath);
                    EnsurePortableLogPathAllowed(targetBackupPath);

                    if (File.Exists(sourceBackupPath))
                    {
                        File.Move(sourceBackupPath, targetBackupPath);
                    }
                }

                File.Move(logPath, GetBackupLogPath(logPath, 1));
            }
            catch (Exception ex)
            {
                SafeTraceAppLogFailure("RotateIfNeeded", null, null, ex);
            }
        }

        private static string GetBackupLogPath(string logPath, int index)
        {
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0}.{1}",
                logPath,
                Math.Max(1, index));
        }

        private static void EnsurePortableLogPathAllowed(string path)
        {
            if (!AppStorage.IsPortableMode)
            {
                return;
            }

            if (AppStorage.IsPathWithinBaseDirectory(path))
            {
                return;
            }

            throw new InvalidOperationException(
                string.Format(
                    "Der Log-Pfad liegt ausserhalb des Portable-Verzeichnisses: '{0}'.",
                    path ?? string.Empty));
        }

        private static void SafeTraceAppLogFailure(string method, string level, string message, Exception exception)
        {
            try
            {
                Trace.WriteLine(string.Format(
                    "[TrafficView] AppLog.{0} failed. Level={1} Message={2} Exception={3}",
                    method ?? "?",
                    level ?? "?",
                    (message ?? string.Empty).Replace("\r", " ").Replace("\n", " "),
                    exception != null ? exception.GetType().Name : "null"));
            }
            catch
            {
            }
        }
    }
}