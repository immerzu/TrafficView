using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
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
        private const string CompressedArchiveFileNamePrefix = "Verbrauch.archiv.";
        private const string CompressedArchiveFileNameSuffix = ".txt.gz";
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

        public bool FlushPending()
        {
            this.RunMaintenanceIfNeeded();

            List<string> linesToWrite;
            lock (this.syncRoot)
            {
                if (this.pendingLines.Count == 0)
                {
                    return true;
                }

                linesToWrite = new List<string>(this.pendingLines);
                this.pendingLines.Clear();
            }

            string path = GetUsageFilePath();

            try
            {
                EnsureUsageDirectoryExists();
                AtomicAppendAllLines(path, linesToWrite.ToArray());

                this.RunMaintenanceIfNeeded();
                return true;
            }
            catch (Exception ex)
            {
                lock (this.syncRoot)
                {
                    this.pendingLines.InsertRange(0, linesToWrite);
                }

                AppLog.WarnOnce(
                    "traffic-usage-flush-failed",
                    string.Format("Verbrauchsdaten konnten nicht nach '{0}' geschrieben werden.", path),
                    ex);
                return false;
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
                List<DeleteBackupEntry> deleteBackups = new List<DeleteBackupEntry>();

                try
                {
                    MoveToDeleteBackupIfExists(activePath, deleteBackups);
                    MoveToDeleteBackupIfExists(archivePath, deleteBackups);
                    foreach (string compressedArchivePath in EnumerateCompressedArchiveFilePaths())
                    {
                        MoveToDeleteBackupIfExists(compressedArchivePath, deleteBackups);
                    }
                }
                catch
                {
                    RestoreDeleteBackups(deleteBackups);
                    throw;
                }

                try
                {
                    DeleteDeleteBackups(deleteBackups);
                }
                catch
                {
                    RestoreDeleteBackups(deleteBackups);
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
                AppendSummariesFromCompressedArchives(adapterKey, nowLocal, currentWeekStart, summaries);

                string archivePath = GetUsageArchiveFilePath();
                if (File.Exists(archivePath))
                {
                    AppendSummariesFromFile(archivePath, adapterKey, nowLocal, currentWeekStart, summaries);
                }

                string path = GetUsageFilePath();
                if (File.Exists(path))
                {
                    AppendSummariesFromFile(path, adapterKey, nowLocal, currentWeekStart, summaries);
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

            if (ArePathsEqual(targetPath, GetUsageFilePath()) ||
                ArePathsEqual(targetPath, GetUsageArchiveFilePath()))
            {
                AppLog.WarnOnce(
                    "traffic-usage-export-target-conflicts-with-usage-storage",
                    string.Format(
                        "Verbrauchsdaten-Export nach '{0}' wurde blockiert, weil das Ziel mit einer internen Verbrauchsdatei kollidiert.",
                        targetPath));
                return false;
            }

            string adapterKey = GetAdapterKey(settings);
            List<string> lines = new List<string>();
            lines.Add("TimestampUtc;AdapterId;AdapterName;DownloadBytes;UploadBytes");

            try
            {
                this.RunMaintenanceIfNeeded();
                AppendCsvLinesFromCompressedArchives(adapterKey, adapterDisplayName, lines);
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
            return Path.Combine(GetUsageBaseDirectory(), UsageFileName);
        }

        public static string GetUsageArchiveFilePath()
        {
            return Path.Combine(GetUsageBaseDirectory(), UsageArchiveFileName);
        }

        public static string GetAdapterKey(MonitorSettings settings)
        {
            return NetworkSnapshot.ResolveAdapterKey(settings);
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

            if (this.RotateActiveUsageFile() && this.CompressArchiveUsageFiles())
            {
                this.lastMaintenanceUtc = nowUtc;
            }
        }

        private bool RotateActiveUsageFile()
        {
            string activePath = GetUsageFilePath();
            if (!File.Exists(activePath))
            {
                return true;
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
                    return true;
                }

                EnsureUsageDirectoryExists();
                string archivePath = GetUsageArchiveFilePath();
                string activeBackupPath = CreateDeleteBackupPath(activePath);
                string archiveBackupPath = CreateDeleteBackupPath(archivePath);
                bool archiveExisted = File.Exists(archivePath);

                File.Copy(activePath, activeBackupPath, true);
                if (archiveExisted)
                {
                    File.Copy(archivePath, archiveBackupPath, true);
                }

                try
                {
                    AtomicAppendAllLines(archivePath, archivedLines);
                    AtomicWriteAllLines(activePath, retainedLines);
                    return true;
                }
                catch
                {
                    File.Copy(activeBackupPath, activePath, true);

                    if (archiveExisted)
                    {
                        File.Copy(archiveBackupPath, archivePath, true);
                    }
                    else
                    {
                        DeleteIfExists(archivePath);
                    }

                    throw;
                }
                finally
                {
                    DeleteIfExists(activeBackupPath);
                    DeleteIfExists(archiveBackupPath);
                }
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce(
                    "traffic-usage-maintenance-failed",
                    string.Format("Verbrauchslog konnte nicht rotiert werden. Datei='{0}'.", activePath),
                    ex);
                return false;
            }
        }

        private bool CompressArchiveUsageFiles()
        {
            string archivePath = GetUsageArchiveFilePath();
            if (!File.Exists(archivePath))
            {
                return true;
            }

            Dictionary<string, List<string>> monthlyLinesByTargetPath = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            List<string> retainedArchiveLines = new List<string>();

            try
            {
                foreach (string line in File.ReadLines(archivePath))
                {
                    TrafficUsageRecord record;
                    if (!TryParseRecord(line, out record))
                    {
                        retainedArchiveLines.Add(line);
                        continue;
                    }

                    string targetPath = GetCompressedArchiveFilePath(record.TimestampUtc.ToLocalTime());
                    List<string> targetLines;
                    if (!monthlyLinesByTargetPath.TryGetValue(targetPath, out targetLines))
                    {
                        targetLines = new List<string>();
                        monthlyLinesByTargetPath[targetPath] = targetLines;
                    }

                    targetLines.Add(line);
                }

                if (monthlyLinesByTargetPath.Count == 0)
                {
                    return true;
                }

                EnsureUsageDirectoryExists();

                Dictionary<string, string> backupPathsByTargetPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, bool> targetExistedByPath = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                string archiveBackupPath = CreateDeleteBackupPath(archivePath);

                try
                {
                    File.Copy(archivePath, archiveBackupPath, true);

                    foreach (KeyValuePair<string, List<string>> entry in monthlyLinesByTargetPath)
                    {
                        string targetPath = entry.Key;
                        bool targetExists = File.Exists(targetPath);
                        targetExistedByPath[targetPath] = targetExists;

                        string backupPath = CreateDeleteBackupPath(targetPath);
                        backupPathsByTargetPath[targetPath] = backupPath;

                        if (targetExists)
                        {
                            File.Copy(targetPath, backupPath, true);
                        }
                    }

                    foreach (KeyValuePair<string, List<string>> entry in monthlyLinesByTargetPath)
                    {
                        List<string> existingLines = ReadCompressedArchiveLines(entry.Key);
                        List<string> mergedLines = MergeUniqueLines(existingLines, entry.Value);
                        WriteCompressedArchiveLines(entry.Key, mergedLines);
                    }

                    if (retainedArchiveLines.Count > 0)
                    {
                        AtomicWriteAllLines(archivePath, retainedArchiveLines);
                    }
                    else
                    {
                        DeleteIfExists(archivePath);
                    }

                    return true;
                }
                catch
                {
                    if (File.Exists(archiveBackupPath))
                    {
                        File.Copy(archiveBackupPath, archivePath, true);
                    }

                    foreach (KeyValuePair<string, string> entry in backupPathsByTargetPath)
                    {
                        bool targetExisted;
                        if (targetExistedByPath.TryGetValue(entry.Key, out targetExisted) && targetExisted)
                        {
                            if (File.Exists(entry.Value))
                            {
                                File.Copy(entry.Value, entry.Key, true);
                            }
                        }
                        else
                        {
                            DeleteIfExists(entry.Key);
                        }
                    }

                    throw;
                }
                finally
                {
                    DeleteIfExists(archiveBackupPath);

                    foreach (string backupPath in backupPathsByTargetPath.Values)
                    {
                        DeleteIfExists(backupPath);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce(
                    "traffic-usage-archive-compress-failed",
                    string.Format("Verbrauchsarchiv konnte nicht komprimiert werden. Datei='{0}'.", archivePath),
                    ex);
                return false;
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

            int invalidLineCount = 0;
            foreach (string line in File.ReadLines(path))
            {
                TrafficUsageRecord record;
                if (!TryParseRecord(line, out record))
                {
                    invalidLineCount++;
                    continue;
                }

                if (!string.Equals(record.AdapterKey, adapterKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                lines.Add(ToCsvLine(record, adapterDisplayName));
            }

            WarnIfUsageFileContainsInvalidRecords(path, invalidLineCount);
        }

        private static void AppendCsvLinesFromCompressedArchives(
            string adapterKey,
            string adapterDisplayName,
            List<string> lines)
        {
            foreach (string path in EnumerateCompressedArchiveFilePaths())
            {
                foreach (string line in ReadCompressedArchiveLines(path))
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
        }

        private static void AppendSummariesFromFile(
            string path,
            string adapterKey,
            DateTime nowLocal,
            DateTime currentWeekStart,
            TrafficUsageSummaries summaries)
        {
            int invalidLineCount = 0;
            foreach (string line in File.ReadLines(path))
            {
                TrafficUsageRecord record;
                if (!TryParseRecord(line, out record))
                {
                    invalidLineCount++;
                    continue;
                }

                if (!string.Equals(record.AdapterKey, adapterKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                AccumulateSummaries(record, nowLocal, currentWeekStart, summaries);
            }

            WarnIfUsageFileContainsInvalidRecords(path, invalidLineCount);
        }

        private static void WarnIfUsageFileContainsInvalidRecords(string path, int invalidLineCount)
        {
            if (string.IsNullOrWhiteSpace(path) || invalidLineCount <= 0)
            {
                return;
            }

            AppLog.WarnOnce(
                "traffic-usage-invalid-lines-" + path,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Verbrauchsdatei enthaelt {0} ungueltige Zeile(n), gueltige Zeilen werden weiter verwendet. Datei='{1}'.",
                    invalidLineCount,
                    path));
        }

        private static void AppendSummariesFromCompressedArchives(
            string adapterKey,
            DateTime nowLocal,
            DateTime currentWeekStart,
            TrafficUsageSummaries summaries)
        {
            foreach (string path in EnumerateCompressedArchiveFilePaths())
            {
                foreach (string line in ReadCompressedArchiveLines(path))
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
            if (safeValue.Length > 0 && (safeValue[0] == '=' || safeValue[0] == '+' || safeValue[0] == '-' || safeValue[0] == '@'))
            {
                safeValue = "'" + safeValue;
            }

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

        private static string GetCompressedArchiveFilePath(DateTime localTimestamp)
        {
            string fileName = string.Format(
                CultureInfo.InvariantCulture,
                "{0}{1:yyyy-MM}{2}",
                CompressedArchiveFileNamePrefix,
                localTimestamp,
                CompressedArchiveFileNameSuffix);
            return Path.Combine(GetUsageBaseDirectory(), fileName);
        }

        private static IEnumerable<string> EnumerateCompressedArchiveFilePaths()
        {
            string directoryPath = GetUsageBaseDirectory();
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            {
                yield break;
            }

            string[] paths = Directory.GetFiles(
                directoryPath,
                CompressedArchiveFileNamePrefix + "*" + CompressedArchiveFileNameSuffix,
                SearchOption.TopDirectoryOnly);
            Array.Sort(paths, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < paths.Length; i++)
            {
                yield return paths[i];
            }
        }

        private static List<string> ReadCompressedArchiveLines(string path)
        {
            List<string> lines = new List<string>();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return lines;
            }

            using (FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (GZipStream gzipStream = new GZipStream(fileStream, CompressionMode.Decompress))
            using (StreamReader reader = new StreamReader(gzipStream, Encoding.UTF8, true))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                }
            }

            return lines;
        }

        private static void WriteCompressedArchiveLines(string path, List<string> lines)
        {
            EnsurePortablePathAllowed(path, "Komprimiertes Verbrauchsarchiv");

            string tempPath = string.Format(
                CultureInfo.InvariantCulture,
                "{0}.{1}.tmp",
                path,
                Guid.NewGuid().ToString("N"));

            try
            {
                using (FileStream fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (GZipStream gzipStream = new GZipStream(fileStream, CompressionMode.Compress))
                using (StreamWriter writer = new StreamWriter(gzipStream, new UTF8Encoding(false)))
                {
                    for (int i = 0; i < lines.Count; i++)
                    {
                        writer.WriteLine(lines[i]);
                    }
                }

                AtomicReplaceFile(tempPath, path);
            }
            finally
            {
                DeleteIfExists(tempPath);
            }
        }

        private static void AtomicAppendAllLines(string path, IEnumerable<string> appendedLines)
        {
            if (string.IsNullOrWhiteSpace(path) || appendedLines == null)
            {
                return;
            }

            EnsurePortablePathAllowed(path, "Verbrauchsdatei");

            string directoryPath = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            string tempPath = string.Format(
                CultureInfo.InvariantCulture,
                "{0}.{1}.tmp",
                path,
                Guid.NewGuid().ToString("N"));

            try
            {
                using (FileStream tempStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    bool appendLineBreakBeforeNewLines = false;

                    if (File.Exists(path))
                    {
                        using (FileStream sourceStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            appendLineBreakBeforeNewLines = DoesStreamNeedTrailingLineBreak(sourceStream);
                            sourceStream.CopyTo(tempStream);
                        }
                    }

                    using (StreamWriter writer = new StreamWriter(tempStream, new UTF8Encoding(false)))
                    {
                        if (appendLineBreakBeforeNewLines)
                        {
                            writer.WriteLine();
                        }

                        foreach (string line in appendedLines)
                        {
                            writer.WriteLine(line ?? string.Empty);
                        }
                    }
                }

                AtomicReplaceFile(tempPath, path);
            }
            finally
            {
                DeleteIfExists(tempPath);
            }
        }

        private static bool DoesStreamNeedTrailingLineBreak(FileStream stream)
        {
            if (stream == null || stream.Length == 0L)
            {
                return false;
            }

            long originalPosition = stream.Position;
            try
            {
                stream.Seek(-1L, SeekOrigin.End);
                int lastByte = stream.ReadByte();
                return lastByte != '\n' && lastByte != '\r';
            }
            finally
            {
                stream.Seek(originalPosition, SeekOrigin.Begin);
            }
        }

        private static void AtomicWriteAllLines(string path, IEnumerable<string> lines)
        {
            if (string.IsNullOrWhiteSpace(path) || lines == null)
            {
                return;
            }

            EnsurePortablePathAllowed(path, "Verbrauchsdatei");

            string directoryPath = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            string tempPath = string.Format(
                CultureInfo.InvariantCulture,
                "{0}.{1}.tmp",
                path,
                Guid.NewGuid().ToString("N"));

            try
            {
                using (FileStream fileStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (StreamWriter writer = new StreamWriter(fileStream, new UTF8Encoding(false)))
                {
                    foreach (string line in lines)
                    {
                        writer.WriteLine(line ?? string.Empty);
                    }
                }

                AtomicReplaceFile(tempPath, path);
            }
            finally
            {
                DeleteIfExists(tempPath);
            }
        }

        private static void AtomicReplaceFile(string tempPath, string targetPath)
        {
            if (string.IsNullOrWhiteSpace(tempPath) || string.IsNullOrWhiteSpace(targetPath))
            {
                return;
            }

            EnsurePortablePathAllowed(targetPath, "Verbrauchsdatei");
            EnsurePortablePathAllowed(tempPath, "Temporaere Verbrauchsdatei");

            if (File.Exists(targetPath))
            {
                File.Replace(tempPath, targetPath, null, true);
            }
            else
            {
                File.Move(tempPath, targetPath);
            }
        }

        private static List<string> MergeUniqueLines(List<string> existingLines, List<string> newLines)
        {
            List<string> mergedLines = new List<string>();
            HashSet<string> seenLines = new HashSet<string>(StringComparer.Ordinal);

            if (existingLines != null)
            {
                for (int i = 0; i < existingLines.Count; i++)
                {
                    string line = existingLines[i];
                    if (seenLines.Add(line))
                    {
                        mergedLines.Add(line);
                    }
                }
            }

            if (newLines != null)
            {
                for (int i = 0; i < newLines.Count; i++)
                {
                    string line = newLines[i];
                    if (seenLines.Add(line))
                    {
                        mergedLines.Add(line);
                    }
                }
            }

            return mergedLines;
        }

        private static string GetUsageBaseDirectory()
        {
            string path = AppStorage.GetSettingsDirectoryPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                return AppStorage.BaseDirectory;
            }

            return path;
        }

        private static void EnsureUsageDirectoryExists()
        {
            string directoryPath = GetUsageBaseDirectory();
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return;
            }

            EnsurePortablePathAllowed(directoryPath, "Verbrauchsverzeichnis");

            Directory.CreateDirectory(directoryPath);
        }

        private static void DeleteIfExists(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                if (!File.Exists(path))
                {
                    return;
                }

                File.Delete(path);
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce(
                    "traffic-usage-delete-if-exists-failed-" + path,
                    string.Format("Datei konnte nicht entfernt werden: '{0}'.", path),
                    ex);
            }
        }

        private static string CreateDeleteBackupPath(string originalPath)
        {
            EnsurePortablePathAllowed(originalPath, "Verbrauchsdatei");

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}.{1}.delete",
                originalPath,
                Guid.NewGuid().ToString("N"));
        }

        private static void MoveToDeleteBackupIfExists(string originalPath, List<DeleteBackupEntry> deleteBackups)
        {
            if (string.IsNullOrWhiteSpace(originalPath) || !File.Exists(originalPath))
            {
                return;
            }

            string backupPath = CreateDeleteBackupPath(originalPath);
            File.Move(originalPath, backupPath);
            deleteBackups.Add(new DeleteBackupEntry(originalPath, backupPath));
        }

        private static void DeleteDeleteBackups(List<DeleteBackupEntry> deleteBackups)
        {
            if (deleteBackups == null)
            {
                return;
            }

            for (int i = 0; i < deleteBackups.Count; i++)
            {
                DeleteIfExists(deleteBackups[i].BackupPath);
            }
        }

        private static void RestoreDeleteBackups(List<DeleteBackupEntry> deleteBackups)
        {
            if (deleteBackups == null)
            {
                return;
            }

            for (int i = deleteBackups.Count - 1; i >= 0; i--)
            {
                RestoreDeleteBackup(deleteBackups[i].BackupPath, deleteBackups[i].OriginalPath, true);
            }
        }

        private static void EnsurePortablePathAllowed(string path, string pathLabel)
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
                    CultureInfo.InvariantCulture,
                    "{0} liegt ausserhalb des Portable-Verzeichnisses: '{1}'.",
                    string.IsNullOrWhiteSpace(pathLabel) ? "Pfad" : pathLabel,
                    path ?? string.Empty));
        }

        private static bool ArePathsEqual(string left, string right)
        {
            string normalizedLeft = NormalizePathForComparison(left);
            string normalizedRight = NormalizePathForComparison(right);

            if (normalizedLeft == null || normalizedRight == null)
            {
                return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePathForComparison(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
            catch (PathTooLongException)
            {
                return null;
            }
        }

        private static void RestoreDeleteBackup(string backupPath, string originalPath, bool wasMoved)
        {
            if (!wasMoved || string.IsNullOrWhiteSpace(backupPath) || string.IsNullOrWhiteSpace(originalPath))
            {
                return;
            }

            try
            {
                if (!File.Exists(backupPath))
                {
                    return;
                }

                if (File.Exists(originalPath))
                {
                    return;
                }

                File.Move(backupPath, originalPath);
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce(
                    "traffic-usage-restore-delete-backup-failed-" + originalPath,
                    string.Format(
                        "Loesch-Backup konnte nicht nach '{0}' wiederhergestellt werden.",
                        originalPath),
                    ex);
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

        private struct DeleteBackupEntry
        {
            public DeleteBackupEntry(string originalPath, string backupPath)
            {
                this.OriginalPath = originalPath;
                this.BackupPath = backupPath;
            }

            public readonly string OriginalPath;
            public readonly string BackupPath;
        }
    }
}
