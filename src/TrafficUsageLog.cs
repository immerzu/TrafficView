using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace TrafficView
{
    internal enum TrafficUsagePeriod
    {
        Daily,
        Weekly,
        Monthly
    }

    internal sealed class TrafficUsageSummary
    {
        public long DownloadBytes { get; set; }

        public long UploadBytes { get; set; }

        public long TotalBytes
        {
            get { return SafeAdd(this.DownloadBytes, this.UploadBytes); }
        }

        private static long SafeAdd(long left, long right)
        {
            if (left > long.MaxValue - right)
            {
                return long.MaxValue;
            }

            return Math.Max(0L, left) + Math.Max(0L, right);
        }
    }

    internal sealed class TrafficUsageLog
    {
        private const string UsageFileName = "Verbrauch.txt";
        private readonly object syncRoot = new object();
        private readonly List<string> pendingLines = new List<string>();

        public int PendingRecordCount
        {
            get
            {
                lock (this.syncRoot)
                {
                    return this.pendingLines.Count;
                }
            }
        }

        public bool QueueUsage(MonitorSettings settings, long downloadBytes, long uploadBytes)
        {
            if (settings == null)
            {
                return false;
            }

            long safeDownloadBytes = Math.Max(0L, downloadBytes);
            long safeUploadBytes = Math.Max(0L, uploadBytes);
            if (safeDownloadBytes <= 0L && safeUploadBytes <= 0L)
            {
                return false;
            }

            string adapterKey = GetAdapterKey(settings);
            string line = string.Format(
                CultureInfo.InvariantCulture,
                "{0}|{1}|{2}|{3}",
                DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                adapterKey,
                safeDownloadBytes,
                safeUploadBytes);

            lock (this.syncRoot)
            {
                this.pendingLines.Add(line);
            }

            return true;
        }

        public void FlushPending()
        {
            string[] linesToWrite;
            lock (this.syncRoot)
            {
                if (this.pendingLines.Count == 0)
                {
                    return;
                }

                linesToWrite = this.pendingLines.ToArray();
            }

            string path = GetUsageFilePath();

            try
            {
                File.AppendAllLines(path, linesToWrite);

                lock (this.syncRoot)
                {
                    if (this.pendingLines.Count >= linesToWrite.Length)
                    {
                        this.pendingLines.RemoveRange(0, linesToWrite.Length);
                    }
                    else
                    {
                        this.pendingLines.Clear();
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce(
                    "traffic-usage-flush-failed",
                    string.Format("Verbrauchsdaten konnten nicht nach '{0}' geschrieben werden.", path),
                    ex);
            }
        }

        public bool ClearAll()
        {
            lock (this.syncRoot)
            {
                this.pendingLines.Clear();
            }

            string path = GetUsageFilePath();
            if (!File.Exists(path))
            {
                return true;
            }

            try
            {
                File.Delete(path);
                return true;
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce(
                    "traffic-usage-clear-failed",
                    string.Format("Verbrauchsdaten konnten nicht aus '{0}' geloescht werden.", path),
                    ex);
                return false;
            }
        }

        public TrafficUsageSummary GetSummary(MonitorSettings settings, TrafficUsagePeriod period)
        {
            string adapterKey = GetAdapterKey(settings);
            TrafficUsageSummary summary = new TrafficUsageSummary();
            DateTime nowLocal = DateTime.Now;
            DateTime currentWeekStart = GetStartOfWeek(nowLocal);

            try
            {
                string path = GetUsageFilePath();
                if (File.Exists(path))
                {
                    foreach (string line in File.ReadLines(path))
                    {
                        TrafficUsageRecord record;
                        if (!TryParseRecord(line, out record))
                        {
                            continue;
                        }

                        if (!string.Equals(record.AdapterKey, adapterKey, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (!MatchesPeriod(record.TimestampUtc, period, nowLocal, currentWeekStart))
                        {
                            continue;
                        }

                        summary.DownloadBytes = SafeAdd(summary.DownloadBytes, record.DownloadBytes);
                        summary.UploadBytes = SafeAdd(summary.UploadBytes, record.UploadBytes);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce(
                    "traffic-usage-read-failed",
                    "Verbrauch.txt konnte nicht gelesen werden; Verbrauchssummen werden eventuell unvollstaendig angezeigt.",
                    ex);
            }

            lock (this.syncRoot)
            {
                for (int i = 0; i < this.pendingLines.Count; i++)
                {
                    TrafficUsageRecord record;
                    if (!TryParseRecord(this.pendingLines[i], out record))
                    {
                        continue;
                    }

                    if (!string.Equals(record.AdapterKey, adapterKey, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!MatchesPeriod(record.TimestampUtc, period, nowLocal, currentWeekStart))
                    {
                        continue;
                    }

                    summary.DownloadBytes = SafeAdd(summary.DownloadBytes, record.DownloadBytes);
                    summary.UploadBytes = SafeAdd(summary.UploadBytes, record.UploadBytes);
                }
            }

            return summary;
        }

        public static string GetUsageFilePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, UsageFileName);
        }

        public static string GetAdapterKey(MonitorSettings settings)
        {
            if (settings == null || string.IsNullOrWhiteSpace(settings.AdapterId))
            {
                return "automatic";
            }

            return settings.AdapterId.Trim();
        }

        private static bool TryParseRecord(string line, out TrafficUsageRecord record)
        {
            record = default(TrafficUsageRecord);

            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            string[] parts = line.Split('|');
            if (parts.Length != 4)
            {
                return false;
            }

            DateTime timestampUtc;
            if (!DateTime.TryParse(
                parts[0].Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out timestampUtc))
            {
                return false;
            }

            long downloadBytes;
            if (!long.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out downloadBytes))
            {
                return false;
            }

            long uploadBytes;
            if (!long.TryParse(parts[3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out uploadBytes))
            {
                return false;
            }

            record = new TrafficUsageRecord(
                timestampUtc,
                parts[1].Trim(),
                Math.Max(0L, downloadBytes),
                Math.Max(0L, uploadBytes));
            return true;
        }

        private static bool MatchesPeriod(
            DateTime timestampUtc,
            TrafficUsagePeriod period,
            DateTime nowLocal,
            DateTime currentWeekStart)
        {
            DateTime localTimestamp = timestampUtc.ToLocalTime();

            switch (period)
            {
                case TrafficUsagePeriod.Daily:
                    return localTimestamp.Date == nowLocal.Date;
                case TrafficUsagePeriod.Weekly:
                    return GetStartOfWeek(localTimestamp) == currentWeekStart;
                case TrafficUsagePeriod.Monthly:
                    return localTimestamp.Year == nowLocal.Year &&
                        localTimestamp.Month == nowLocal.Month;
                default:
                    return false;
            }
        }

        private static DateTime GetStartOfWeek(DateTime dateTime)
        {
            DayOfWeek firstDayOfWeek = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
            int offset = (7 + (dateTime.DayOfWeek - firstDayOfWeek)) % 7;
            return dateTime.Date.AddDays(-offset);
        }

        private static long SafeAdd(long left, long right)
        {
            if (left > long.MaxValue - right)
            {
                return long.MaxValue;
            }

            return Math.Max(0L, left) + Math.Max(0L, right);
        }

        private struct TrafficUsageRecord
        {
            public readonly DateTime TimestampUtc;
            public readonly string AdapterKey;
            public readonly long DownloadBytes;
            public readonly long UploadBytes;

            public TrafficUsageRecord(DateTime timestampUtc, string adapterKey, long downloadBytes, long uploadBytes)
            {
                this.TimestampUtc = timestampUtc;
                this.AdapterKey = adapterKey ?? string.Empty;
                this.DownloadBytes = downloadBytes;
                this.UploadBytes = uploadBytes;
            }
        }
    }
}
