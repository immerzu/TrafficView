using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace TrafficView
{
    internal static class RuntimeDiagnostics
    {
        private static readonly Stopwatch ProcessStopwatch = Stopwatch.StartNew();
        private static long startupCompletedMilliseconds = -1L;

        public static void MarkProcessStarted()
        {
            ProcessStopwatch.Start();
        }

        public static void MarkStartupCompleted()
        {
            Interlocked.CompareExchange(
                ref startupCompletedMilliseconds,
                ProcessStopwatch.ElapsedMilliseconds,
                -1L);
        }

        public static string CreateDiagnosticsText(string timerDiagnostics)
        {
            using (Process process = Process.GetCurrentProcess())
            {
                process.Refresh();

                return string.Format(
                    CultureInfo.InvariantCulture,
                    "Runtime uptime: {0}\r\nStartup ready: {1}\r\nWorking set: {2}\r\nPrivate memory: {3}\r\nManaged memory: {4}\r\nGC collections: gen0={5}, gen1={6}, gen2={7}\r\nThreads: {8}\r\n{9}",
                    FormatDuration(ProcessStopwatch.Elapsed),
                    FormatStartupCompletedMilliseconds(),
                    FormatBytes(process.WorkingSet64),
                    FormatBytes(process.PrivateMemorySize64),
                    FormatBytes(GC.GetTotalMemory(false)),
                    GC.CollectionCount(0),
                    GC.CollectionCount(1),
                    GC.CollectionCount(2),
                    process.Threads.Count,
                    string.IsNullOrWhiteSpace(timerDiagnostics) ? "Timers: unavailable" : timerDiagnostics);
            }
        }

        public static string FormatBytes(long bytes)
        {
            double safeBytes = Math.Max(0L, bytes);
            string[] units = new string[] { "B", "KB", "MB", "GB" };
            int unitIndex = 0;

            while (safeBytes >= 1024D && unitIndex < units.Length - 1)
            {
                safeBytes /= 1024D;
                unitIndex++;
            }

            if (unitIndex == 0)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0:0} {1}", safeBytes, units[unitIndex]);
            }

            return string.Format(CultureInfo.InvariantCulture, "{0:0.0} {1}", safeBytes, units[unitIndex]);
        }

        private static string FormatStartupCompletedMilliseconds()
        {
            long milliseconds = Interlocked.Read(ref startupCompletedMilliseconds);
            if (milliseconds < 0L)
            {
                return "pending";
            }

            return string.Format(CultureInfo.InvariantCulture, "{0} ms", milliseconds);
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalDays >= 1D)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:0}d {1:00}:{2:00}:{3:00}",
                    Math.Floor(duration.TotalDays),
                    duration.Hours,
                    duration.Minutes,
                    duration.Seconds);
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:00}:{1:00}:{2:00}",
                (int)Math.Floor(duration.TotalHours),
                duration.Minutes,
                duration.Seconds);
        }
    }
}
