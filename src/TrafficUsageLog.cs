using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

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

    internal sealed class TrafficUsageSummaries
    {
        public TrafficUsageSummaries()
        {
            this.Daily = new TrafficUsageSummary();
            this.Weekly = new TrafficUsageSummary();
            this.Monthly = new TrafficUsageSummary();
        }

        public TrafficUsageSummary Daily { get; private set; }

        public TrafficUsageSummary Weekly { get; private set; }

        public TrafficUsageSummary Monthly { get; private set; }
    }

    internal sealed class TrafficUsageLog
    {
        private const string UsageFileName = "Verbrauch.txt";
        private const string UsageArchiveFileName = "Verbrauch.archiv.txt";
        private readonly object syncRoot = new object();
        private readonly List<string> pendingLines = new List<string>();
        private DateTime lastMaintenanceUtc = DateTime.MinValue;

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
            this.RunMaintenanceIfNeeded();

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

                this.RunMaintenanceIfNeeded();
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
            List<string> pendingBackup;
            lock (this.syncRoot)
            {
                pendingBackup = new List<string>(this.pendingLines);
                this.pendingLines.Clear();
            }

            try
            {
                string activePath = GetUsageFilePath();
                string archivePath = GetUsageArchiveFilePath();
                string activeBackupPath = CreateDeleteBackupPath(activePath);
                string archiveBackupPath = CreateDeleteBackupPath(archivePath);
                bool activeMoved = false;
                bool archiveMoved = false;

                try
                {
                    if (File.Exists(activePath))
                    {
                        File.Move(activePath, activeBackupPath);
                        activeMoved = true;
                    }

                    if (File.Exists(archivePath))
                    {
                        File.Move(archivePath, archiveBackupPath);
                        archiveMoved = true;
                    }
                }
                catch
                {
                    RestoreDeleteBackup(activeBackupPath, activePath, activeMoved);
                    RestoreDeleteBackup(archiveBackupPath, archivePath, archiveMoved);
                    throw;
                }

                try
                {
                    DeleteIfExists(activeBackupPath);
                    DeleteIfExists(archiveBackupPath);
                }
                catch
                {
                    RestoreDeleteBackup(activeBackupPath, activePath, activeMoved);
                    RestoreDeleteBackup(archiveBackupPath, archivePath, archiveMoved);
                    throw;
                }

                return true;
            }
            catch (Exception ex)
            {
                lock (this.syncRoot)
                {
                    this.pendingLines.InsertRange(0, pendingBackup);
                }

                AppLog.WarnOnce(
                    "traffic-usage-clear-failed",
                    string.Format(
                        "Verbrauchsdaten konnten nicht aus '{0}' bzw. '{1}' geloescht werden.",
                        GetUsageFilePath(),
                        GetUsageArchiveFilePath()),
                    ex);
                return false;
            }
        }

        public TrafficUsageSummaries GetSummaries(MonitorSettings settings)
        {
            this.RunMaintenanceIfNeeded();

            string adapterKey = GetAdapterKey(settings);
            TrafficUsageSummaries summaries = new TrafficUsageSummaries();
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

                        AccumulateSummaries(record, nowLocal, currentWeekStart, summaries);
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

                    AccumulateSummaries(record, nowLocal, currentWeekStart, summaries);
                }
            }

            return summaries;
        }

        public TrafficUsageSummary GetSummary(MonitorSettings settings, TrafficUsagePeriod period)
        {
            TrafficUsageSummaries summaries = this.GetSummaries(settings);

            switch (period)
            {
                case TrafficUsagePeriod.Daily:
                    return summaries.Daily;
                case TrafficUsagePeriod.Weekly:
                    return summaries.Weekly;
                case TrafficUsagePeriod.Monthly:
                    return summaries.Monthly;
                default:
                    return new TrafficUsageSummary();
            }
        }

        public bool ExportCsv(MonitorSettings settings, string adapterDisplayName, string targetPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return false;
            }

            string adapterKey = GetAdapterKey(settings);
            List<string> lines = new List<string>();
            lines.Add("TimestampUtc;AdapterId;AdapterName;DownloadBytes;UploadBytes");

            try
            {
                this.RunMaintenanceIfNeeded();
                AppendCsvLinesFromFile(GetUsageArchiveFilePath(), adapterKey, adapterDisplayName, lines);
                AppendCsvLinesFromFile(GetUsageFilePath(), adapterKey, adapterDisplayName, lines);

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

                        lines.Add(ToCsvLine(record, adapterDisplayName));
                    }
                }

                string directoryPath = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                using (StreamWriter writer = new StreamWriter(targetPath, false, new UTF8Encoding(true)))
                {
                    for (int i = 0; i < lines.Count; i++)
                    {
                        writer.WriteLine(lines[i]);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce(
                    "traffic-usage-export-failed",
                    string.Format("Verbrauchsdaten konnten nicht nach '{0}' exportiert werden.", targetPath),
                    ex);
                return false;
            }
        }

        public static string GetUsageFilePath()
        {
            return Path.Combine(AppStorage.BaseDirectory, UsageFileName);
        }

        public static string GetUsageArchiveFilePath()
        {
            return Path.Combine(AppStorage.BaseDirectory, UsageArchiveFileName);
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

        private void RunMaintenanceIfNeeded()
        {
            DateTime nowUtc = DateTime.UtcNow;
            if (this.lastMaintenanceUtc != DateTime.MinValue &&
                (nowUtc - this.lastMaintenanceUtc).TotalHours < 12D)
            {
                return;
            }

            this.lastMaintenanceUtc = nowUtc;
            this.RotateActiveUsageFile();
        }

        private void RotateActiveUsageFile()
        {
            string activePath = GetUsageFilePath();
            if (!File.Exists(activePath))
            {
                return;
            }

            DateTime retentionStartLocal = GetRetentionStartLocal(DateTime.Now);
            List<string> retainedLines = new List<string>();
            List<string> archivedLines = new List<string>();

            try
            {
                foreach (string line in File.ReadLines(activePath))
                {
                    TrafficUsageRecord record;
                    if (!TryParseRecord(line, out record))
                    {
                        retainedLines.Add(line);
                        continue;
                    }

                    if (record.TimestampUtc.ToLocalTime() < retentionStartLocal)
                    {
                        archivedLines.Add(line);
                    }
                    else
                    {
                        retainedLines.Add(line);
                    }
                }

                if (archivedLines.Count == 0)
                {
                    return;
                }

                File.AppendAllLines(GetUsageArchiveFilePath(), archivedLines);
                File.WriteAllLines(activePath, retainedLines);
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce(
                    "traffic-usage-maintenance-failed",
                    string.Format("Verbrauchslog konnte nicht rotiert werden. Datei='{0}'.", activePath),
                    ex);
            }
        }

        private static void AppendCsvLinesFromFile(
            string path,
            string adapterKey,
            string adapterDisplayName,
            List<string> lines)
        {
            if (lines == null || !File.Exists(path))
            {
                return;
            }

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

                lines.Add(ToCsvLine(record, adapterDisplayName));
            }
        }

        private static void AccumulateSummaries(
            TrafficUsageRecord record,
            DateTime nowLocal,
            DateTime currentWeekStart,
            TrafficUsageSummaries summaries)
        {
            if (summaries == null)
            {
                return;
            }

            DateTime localTimestamp = record.TimestampUtc.ToLocalTime();

            if (localTimestamp.Date == nowLocal.Date)
            {
                summaries.Daily.DownloadBytes = SafeAdd(summaries.Daily.DownloadBytes, record.DownloadBytes);
                summaries.Daily.UploadBytes = SafeAdd(summaries.Daily.UploadBytes, record.UploadBytes);
            }

            if (GetStartOfWeek(localTimestamp) == currentWeekStart)
            {
                summaries.Weekly.DownloadBytes = SafeAdd(summaries.Weekly.DownloadBytes, record.DownloadBytes);
                summaries.Weekly.UploadBytes = SafeAdd(summaries.Weekly.UploadBytes, record.UploadBytes);
            }

            if (localTimestamp.Year == nowLocal.Year && localTimestamp.Month == nowLocal.Month)
            {
                summaries.Monthly.DownloadBytes = SafeAdd(summaries.Monthly.DownloadBytes, record.DownloadBytes);
                summaries.Monthly.UploadBytes = SafeAdd(summaries.Monthly.UploadBytes, record.UploadBytes);
            }
        }

        private static string ToCsvLine(TrafficUsageRecord record, string adapterDisplayName)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0};{1};{2};{3};{4}",
                EscapeCsvValue(record.TimestampUtc.ToString("o", CultureInfo.InvariantCulture)),
                EscapeCsvValue(record.AdapterKey),
                EscapeCsvValue(adapterDisplayName ?? string.Empty),
                record.DownloadBytes,
                record.UploadBytes);
        }

        private static string EscapeCsvValue(string value)
        {
            string safeValue = value ?? string.Empty;
            bool mustQuote = safeValue.IndexOfAny(new char[] { ';', '"', '\r', '\n' }) >= 0;
            if (!mustQuote)
            {
                return safeValue;
            }

            return "\"" + safeValue.Replace("\"", "\"\"") + "\"";
        }

        private static DateTime GetRetentionStartLocal(DateTime nowLocal)
        {
            DateTime currentMonthStart = new DateTime(nowLocal.Year, nowLocal.Month, 1);
            return currentMonthStart.AddDays(-7);
        }

        private static void DeleteIfExists(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }

            File.Delete(path);
        }

        private static string CreateDeleteBackupPath(string originalPath)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}.{1}.delete",
                originalPath,
                Guid.NewGuid().ToString("N"));
        }

        private static void RestoreDeleteBackup(string backupPath, string originalPath, bool wasMoved)
        {
            if (!wasMoved || !File.Exists(backupPath))
            {
                return;
            }

            if (File.Exists(originalPath))
            {
                return;
            }

            File.Move(backupPath, originalPath);
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
