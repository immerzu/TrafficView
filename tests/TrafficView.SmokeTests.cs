using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace TrafficView
{
    internal struct NetworkSnapshot
    {
        public static string ResolveAdapterKey(MonitorSettings settings)
        {
            if (settings == null)
            {
                return string.Empty;
            }

            if (settings.UsesAutomaticAdapterSelection())
            {
                return MonitorSettings.AutomaticAdapterId;
            }

            return (settings.AdapterId ?? string.Empty).Trim();
        }
    }

    internal static class SmokeTests
    {
        private static readonly string BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string PortableMarkerPath = Path.Combine(BaseDirectory, AppStorage.PortableMarkerFileName);
        private static readonly string ExportPath = Path.Combine(BaseDirectory, "usage-export.csv");

        public static int Main()
        {
            try
            {
                PreparePortableTestEnvironment();
                TestTrafficRateFormatter();
                TestTrafficRateSmoothing();
                TestTrafficUsageFormatter();
                TestPanelSkinCatalogPrefersDefaultFallback();
                TestSkinPathPolicyBlocksExternalPortableSkinPaths();
                TestPanelSkinCatalogIgnoresIncompleteSkins();
                TestMonitorSettingsNormalizesInvalidStoredValues();
                TestMonitorSettingsRecoversFromBackupWhenPrimaryIsInvalid();
                TestMonitorSettingsRoundTripPreservesSkinSelection();
                TestTrafficUsageLogRejectsEmptySamplesAndCountsPendingUsage();
                TestTrafficUsageLogHandlesLargeFilesAndInvalidRecords();
                TestTrafficUsageLogAppendsAfterPartialLine();
                TestTrafficUsageLogRoundTrip();
                TestAppLogRotatesLargeLogFile();
                TestDiagnosticsExportIncludesRotatedLogs();
                TestStorageDiagnosticsReportsWritableSettingsPath();
                TestRuntimeDiagnosticsReportsMemoryAndStartup();
                Console.WriteLine("Smoke tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }

        private static void PreparePortableTestEnvironment()
        {
            Directory.CreateDirectory(BaseDirectory);
            File.WriteAllText(PortableMarkerPath, string.Empty);
            CleanupFile(MonitorSettingsTestPaths.SettingsPath);
            CleanupFile(MonitorSettingsTestPaths.SettingsBackupPath);
            CleanupFile(TrafficUsageLog.GetUsageFilePath());
            CleanupFile(TrafficUsageLog.GetUsageArchiveFilePath());
            CleanupFile(ExportPath);
            CleanupDirectory(Path.Combine(BaseDirectory, "Skins"));
            CleanupDirectory(Path.Combine(BaseDirectory, "TrafficView"));
            PanelSkinCatalog.Reload();
        }

        private static void TestTrafficRateFormatter()
        {
            AssertEqual("0 B/s", TrafficRateFormatter.FormatSpeed(-1D), "Negative speeds should be clamped to zero.");
            AssertEqual("512 B/s", TrafficRateFormatter.FormatSpeed(512D), "Byte speeds should stay in bytes.");
            AssertEqual(string.Format("{0:0.0} KB/s", 1D), TrafficRateFormatter.FormatSpeed(1024D), "One kibibyte should use the KB/s unit.");
            AssertEqual("10 KB/s", TrafficRateFormatter.FormatSpeed(10D * 1024D), "Two-digit KB/s speeds should avoid unnecessary trailing decimals.");
            AssertEqual("100 KB/s", TrafficRateFormatter.FormatSpeed(100D * 1024D), "Three-digit KB/s speeds should be rounded without decimals.");
            AssertEqual(string.Format("{0:0.0} MB/s", 1D), TrafficRateFormatter.FormatSpeed(1024D * 1024D), "One mebibyte should use the MB/s unit.");
        }

        private static void TestTrafficRateSmoothing()
        {
            Queue<double> samples = new Queue<double>();
            double[] weights = new double[] { 0.15D, 0.30D, 0.55D };

            AssertEqual(0D, TrafficRateSmoothing.GetSmoothedRate(samples, weights), "Empty sample sets should smooth to zero.");

            TrafficRateSmoothing.AddSample(samples, -100D, 3);
            AssertEqual(0D, TrafficRateSmoothing.GetSmoothedRate(samples, weights), "Negative samples should be clamped to zero.");

            samples = new Queue<double>();
            TrafficRateSmoothing.AddSample(samples, 100D, 3);
            TrafficRateSmoothing.AddSample(samples, 200D, 3);
            AssertEqual(164.705882D, Math.Round(TrafficRateSmoothing.GetSmoothedRate(samples, weights), 6), "Recent samples should use the tail of the weight table.");

            TrafficRateSmoothing.AddSample(samples, 400D, 3);
            AssertEqual(3, samples.Count, "Smoothing should keep only the configured sample count.");
            AssertEqual(295D, Math.Round(TrafficRateSmoothing.GetSmoothedRate(samples, weights), 6), "Three samples should use all smoothing weights.");
        }

        private static void TestTrafficUsageFormatter()
        {
            DateTime timestamp = new DateTime(2026, 4, 23, 15, 45, 30);
            DayOfWeek firstDayOfWeek = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
            int weekOffset = (7 + (timestamp.DayOfWeek - firstDayOfWeek)) % 7;
            DateTime expectedWeekStart = timestamp.Date.AddDays(-weekOffset);

            AssertEqual("0 B", TrafficUsageFormatter.FormatAmount(-1L), "Negative usage amounts should be clamped to zero.");
            AssertEqual("512 B", TrafficUsageFormatter.FormatAmount(512L), "Byte usage amounts should stay in bytes.");
            AssertEqual(string.Format("{0:0.0} KB", 1D), TrafficUsageFormatter.FormatAmount(1024L), "One kibibyte should use the KB unit.");
            AssertEqual("100 MB", TrafficUsageFormatter.FormatAmount(100L * 1024L * 1024L), "Large megabyte amounts should be rounded without decimals.");
            AssertEqual("23.04.2026 00:00:00", TrafficUsageFormatter.FormatPeriodStart(timestamp, TrafficUsagePeriod.Daily), "Daily period should start at midnight.");
            AssertEqual(expectedWeekStart.ToString("dd.MM.yyyy HH:mm:ss"), TrafficUsageFormatter.FormatPeriodStart(timestamp, TrafficUsagePeriod.Weekly), "Weekly period should use the current culture's first day of week.");
            AssertEqual("01.04.2026 00:00:00", TrafficUsageFormatter.FormatPeriodStart(timestamp, TrafficUsagePeriod.Monthly), "Monthly period should start on the first day of the month.");
            AssertEqual("23.04.2026 15:45:30", TrafficUsageFormatter.FormatPeriodEnd(timestamp), "Period end should use the export timestamp.");
            AssertEqual("plain", TrafficUsageFormatter.EscapeCsvValue("plain"), "Plain CSV values should not be quoted.");
            AssertEqual("\"a;b\"\"c\"", TrafficUsageFormatter.EscapeCsvValue("a;b\"c"), "CSV values with separators or quotes should be quoted and escaped.");
        }

        private static void TestMonitorSettingsRoundTripPreservesSkinSelection()
        {
            CreateTestSkin("09", "ModernGreen");
            PanelSkinCatalog.Reload();

            MonitorSettings settings = new MonitorSettings(
                "adapter-1",
                "Ethernet",
                1200D,
                1300D,
                1400D,
                true,
                true,
                35,
                "en",
                true,
                12,
                34,
                125,
                "09",
                PopupDisplayMode.SimpleBlue,
                PopupSectionMode.LeftOnly,
                false,
                true,
                true,
                PopupSectionMode.RightOnly);

            AssertEqual("09", settings.PanelSkinId, "PanelSkinId should preserve the selected valid skin.");
            settings.Save();

            MonitorSettings loaded = MonitorSettings.Load();
            AssertEqual("adapter-1", loaded.AdapterId, "AdapterId should round-trip.");
            AssertEqual("Ethernet", loaded.AdapterName, "AdapterName should round-trip.");
            AssertEqual("en", loaded.LanguageCode, "Language should round-trip.");
            AssertEqual(125, loaded.PopupScalePercent, "Popup scale should round-trip.");
            AssertEqual(12, loaded.PopupLocationX, "PopupLocationX should round-trip.");
            AssertEqual(34, loaded.PopupLocationY, "PopupLocationY should round-trip.");
            AssertEqual(PopupDisplayMode.SimpleBlue, loaded.PopupDisplayMode, "Display mode should round-trip.");
            AssertEqual(PopupSectionMode.LeftOnly, loaded.PopupSectionMode, "Popup section mode should round-trip.");
            AssertEqual(PopupSectionMode.RightOnly, loaded.TaskbarPopupSectionMode, "Taskbar popup section mode should round-trip.");
            AssertEqual("09", loaded.PanelSkinId, "Saved PanelSkinId should not be forced back to the default skin.");
        }

        private static void TestMonitorSettingsNormalizesInvalidStoredValues()
        {
            CreateTestSkin(PanelSkinCatalog.DefaultSkinId, "DefaultSkin");
            PanelSkinCatalog.Reload();

            File.WriteAllLines(
                MonitorSettingsTestPaths.SettingsPath,
                new[]
                {
                    "AdapterId=adapter-invalid",
                    "AdapterName=Invalid Adapter",
                    "CalibrationPeakBytesPerSecond=-50",
                    "CalibrationDownloadPeakBytesPerSecond=NaN",
                    "CalibrationUploadPeakBytesPerSecond=Infinity",
                    "TransparencyPercent=150",
                    "LanguageCode=missing-language",
                    "PopupScalePercent=123",
                    "PanelSkinId=missing-skin",
                    "PopupDisplayMode=unknown-mode",
                    "PopupSectionMode=LeftOnly",
                    "TaskbarPopupSectionMode=unknown-section"
                });

            MonitorSettings loaded = MonitorSettings.Load();
            AssertEqual("adapter-invalid", loaded.AdapterId, "Recognized settings should load the adapter id.");
            AssertEqual(0D, loaded.CalibrationPeakBytesPerSecond, "Negative calibration peaks should normalize to zero.");
            AssertEqual(0D, loaded.CalibrationDownloadPeakBytesPerSecond, "NaN download peaks should normalize to zero.");
            AssertEqual(0D, loaded.CalibrationUploadPeakBytesPerSecond, "Infinite upload peaks should normalize to zero.");
            AssertEqual(100, loaded.TransparencyPercent, "Transparency should be clamped to the supported range.");
            AssertEqual("de", loaded.LanguageCode, "Unknown language codes should fall back to German.");
            AssertEqual(125, loaded.PopupScalePercent, "Unsupported popup scales should normalize to the nearest supported value.");
            AssertEqual(PanelSkinCatalog.DefaultSkinId, loaded.PanelSkinId, "Unknown skins should fall back to the default skin.");
            AssertEqual(PopupDisplayMode.Standard, loaded.PopupDisplayMode, "Unknown display modes should fall back to Standard.");
            AssertEqual(PopupSectionMode.LeftOnly, loaded.PopupSectionMode, "Valid section modes should still be honored.");
            AssertEqual(PopupSectionMode.RightOnly, loaded.TaskbarPopupSectionMode, "Unknown taskbar section modes should fall back to the taskbar default.");
        }

        private static void TestMonitorSettingsRecoversFromBackupWhenPrimaryIsInvalid()
        {
            CreateTestSkin(PanelSkinCatalog.DefaultSkinId, "DefaultSkin");
            PanelSkinCatalog.Reload();

            File.WriteAllLines(
                MonitorSettingsTestPaths.SettingsPath,
                new[]
                {
                    "this is not a settings file",
                    "still invalid"
                });

            File.WriteAllLines(
                MonitorSettingsTestPaths.SettingsBackupPath,
                new[]
                {
                    "AdapterId=adapter-backup",
                    "AdapterName=Recovered Adapter",
                    "CalibrationPeakBytesPerSecond=4096",
                    "LanguageCode=en",
                    "PanelSkinId=" + PanelSkinCatalog.DefaultSkinId
                });

            MonitorSettings loaded = MonitorSettings.Load();
            AssertEqual("adapter-backup", loaded.AdapterId, "Backup settings should be used when the primary settings file is invalid.");
            AssertEqual("Recovered Adapter", loaded.AdapterName, "Backup settings should preserve adapter details.");
            AssertEqual("en", loaded.LanguageCode, "Backup settings should preserve language.");

            string restoredPrimaryText = File.ReadAllText(MonitorSettingsTestPaths.SettingsPath);
            AssertTrue(
                restoredPrimaryText.IndexOf("AdapterId=adapter-backup", StringComparison.OrdinalIgnoreCase) >= 0,
                "Recovered backup settings should be written back to the primary settings file.");
        }

        private static void TestPanelSkinCatalogPrefersDefaultFallback()
        {
            CreateTestSkin("07", "OlderSkin");
            CreateTestSkin(PanelSkinCatalog.DefaultSkinId, "DefaultSkin");
            PanelSkinCatalog.Reload();

            AssertEqual(
                PanelSkinCatalog.DefaultSkinId,
                PanelSkinCatalog.NormalizeSkinId(string.Empty),
                "Empty skin selections should fall back to the default skin when it exists.");
            AssertEqual(
                PanelSkinCatalog.DefaultSkinId,
                PanelSkinCatalog.NormalizeSkinId("missing"),
                "Unknown skin selections should fall back to the default skin when it exists.");

            PanelSkinDefinition fallbackSkin = PanelSkinCatalog.GetSkinById("missing");
            AssertTrue(fallbackSkin != null, "Missing skin lookup should return a fallback skin.");
            AssertEqual(
                PanelSkinCatalog.DefaultSkinId,
                fallbackSkin.Id,
                "Missing skin lookup should return the default skin when it exists.");
        }

        private static void TestSkinPathPolicyBlocksExternalPortableSkinPaths()
        {
            string outsideDirectoryPath = Path.Combine(Path.GetTempPath(), "TrafficViewExternalSkinSmokeTest");
            string errorMessage;

            AssertTrue(
                !PanelSkinCatalog.TryValidateSkinDirectory(outsideDirectoryPath, out errorMessage),
                "Portable skin validation should reject paths outside the app directory.");
            AssertTrue(
                errorMessage.IndexOf("ausserhalb des Portable-Verzeichnisses", StringComparison.OrdinalIgnoreCase) >= 0,
                "External portable skin paths should produce a clear path boundary error.");
        }

        private static void TestPanelSkinCatalogIgnoresIncompleteSkins()
        {
            CreateTestSkin(PanelSkinCatalog.DefaultSkinId, "DefaultSkin");
            string brokenSkinDirectoryPath = Path.Combine(BaseDirectory, "Skins", "BrokenSkin");
            Directory.CreateDirectory(brokenSkinDirectoryPath);
            File.WriteAllLines(
                Path.Combine(brokenSkinDirectoryPath, "skin.ini"),
                new[]
                {
                    "Id=99",
                    "DisplayNameFallback=BrokenSkin",
                    "SurfaceEffect=none"
                });

            string validationError;
            AssertTrue(
                !PanelSkinCatalog.TryValidateSkinDirectory(brokenSkinDirectoryPath, out validationError),
                "Incomplete skin directories should fail validation.");
            AssertTrue(
                validationError.IndexOf("fehlt", StringComparison.OrdinalIgnoreCase) >= 0,
                "Incomplete skin validation should describe missing files.");

            PanelSkinCatalog.Reload();
            AssertEqual(
                PanelSkinCatalog.DefaultSkinId,
                PanelSkinCatalog.NormalizeSkinId("99"),
                "Incomplete skins should be ignored and the default skin should remain available.");
        }

        private static void TestTrafficUsageLogRoundTrip()
        {
            MonitorSettings settings = new MonitorSettings(
                "adapter-usage",
                "Fiber",
                900D,
                panelSkinId: "09");

            TrafficUsageLog log = new TrafficUsageLog();
            AssertTrue(log.QueueUsage(settings, 1024L, 2048L), "First usage sample should be queued.");
            AssertTrue(log.QueueUsage(settings, 512L, 256L), "Second usage sample should be queued.");
            AssertTrue(log.FlushPending(), "Queued usage samples should flush successfully.");

            TrafficUsageSummaries summaries = log.GetSummaries(settings);
            AssertEqual(1536L, summaries.Daily.DownloadBytes, "Daily download sum should match flushed samples.");
            AssertEqual(2304L, summaries.Daily.UploadBytes, "Daily upload sum should match flushed samples.");
            AssertEqual(3840L, summaries.Daily.TotalBytes, "Daily total should match flushed samples.");
            AssertEqual(1536L, summaries.Weekly.DownloadBytes, "Weekly download sum should match flushed samples.");
            AssertEqual(2304L, summaries.Monthly.UploadBytes, "Monthly upload sum should match flushed samples.");

            AssertTrue(log.ExportCsv(settings, "Fiber", ExportPath), "CSV export should succeed.");
            string[] csvLines = File.ReadAllLines(ExportPath);
            AssertEqual("TimestampUtc;AdapterId;AdapterName;DownloadBytes;UploadBytes", csvLines[0], "CSV header should match the export format.");
            AssertEqual(3, csvLines.Length, "CSV export should contain one header row and two data rows.");
            AssertTrue(csvLines.Skip(1).All(line => line.Contains(";adapter-usage;Fiber;")), "CSV rows should reference the selected adapter.");

            AssertTrue(log.ClearAll(), "Usage data should be clearable.");
            TrafficUsageSummaries clearedSummaries = log.GetSummaries(settings);
            AssertEqual(0L, clearedSummaries.Daily.TotalBytes, "Daily total should be zero after clearing.");
            AssertTrue(!File.Exists(TrafficUsageLog.GetUsageFilePath()), "Active usage file should be gone after clearing.");
            AssertTrue(!File.Exists(TrafficUsageLog.GetUsageArchiveFilePath()), "Archive usage file should be gone after clearing.");
        }

        private static void TestTrafficUsageLogRejectsEmptySamplesAndCountsPendingUsage()
        {
            CleanupFile(TrafficUsageLog.GetUsageFilePath());
            CleanupFile(TrafficUsageLog.GetUsageArchiveFilePath());

            MonitorSettings settings = new MonitorSettings(
                "adapter-pending",
                "Pending Adapter",
                900D,
                panelSkinId: PanelSkinCatalog.DefaultSkinId);

            TrafficUsageLog log = new TrafficUsageLog();
            AssertTrue(!log.QueueUsage(null, 1L, 1L), "Null settings should not queue usage.");
            AssertTrue(!log.QueueUsage(settings, 0L, 0L), "Empty usage samples should not be queued.");
            AssertTrue(!log.QueueUsage(settings, -1L, -2L), "Fully negative samples should not be queued.");
            AssertEqual(0, log.PendingRecordCount, "Rejected usage samples should not remain pending.");

            AssertTrue(log.QueueUsage(settings, -10L, 20L), "Partially valid samples should be queued after clamping.");
            AssertEqual(1, log.PendingRecordCount, "The valid clamped usage sample should be pending.");

            TrafficUsageSummaries pendingSummaries = log.GetSummaries(settings);
            AssertEqual(0L, pendingSummaries.Daily.DownloadBytes, "Negative pending download bytes should be clamped to zero.");
            AssertEqual(20L, pendingSummaries.Daily.UploadBytes, "Pending upload bytes should be included in daily summaries.");
            AssertEqual(20L, pendingSummaries.Daily.TotalBytes, "Pending totals should include clamped values.");

            AssertTrue(log.FlushPending(), "Pending usage should flush successfully.");
            AssertEqual(0, log.PendingRecordCount, "Flushed usage samples should be removed from the pending queue.");

            File.AppendAllLines(
                TrafficUsageLog.GetUsageFilePath(),
                new[]
                {
                    "not-a-valid-record",
                    string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "{0}|other-adapter|900|900",
                        DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture))
                });

            TrafficUsageSummaries flushedSummaries = log.GetSummaries(settings);
            AssertEqual(0L, flushedSummaries.Daily.DownloadBytes, "Invalid and other-adapter records should not affect download totals.");
            AssertEqual(20L, flushedSummaries.Daily.UploadBytes, "Invalid and other-adapter records should not affect upload totals.");

            AssertTrue(log.ClearAll(), "Usage data should be clearable after invalid records are ignored.");
        }

        private static void TestTrafficUsageLogHandlesLargeFilesAndInvalidRecords()
        {
            CleanupFile(TrafficUsageLog.GetUsageFilePath());
            CleanupFile(TrafficUsageLog.GetUsageArchiveFilePath());

            MonitorSettings settings = new MonitorSettings(
                "adapter-large",
                "Large Adapter",
                900D,
                panelSkinId: PanelSkinCatalog.DefaultSkinId);

            List<string> lines = new List<string>();
            string timestamp = DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
            for (int i = 0; i < 5000; i++)
            {
                lines.Add(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{0}|adapter-large|1|2",
                    timestamp));
            }

            lines.Add(string.Empty);
            lines.Add("invalid|adapter-large|x|y");
            lines.Add(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0}|other-adapter|100|100",
                timestamp));
            File.WriteAllLines(TrafficUsageLog.GetUsageFilePath(), lines.ToArray());

            TrafficUsageLog log = new TrafficUsageLog();
            TrafficUsageSummaries summaries = log.GetSummaries(settings);
            AssertEqual(5000L, summaries.Daily.DownloadBytes, "Large usage files should preserve valid download totals.");
            AssertEqual(10000L, summaries.Daily.UploadBytes, "Large usage files should preserve valid upload totals.");

            AssertTrue(log.QueueUsage(settings, 3L, 4L), "A new sample should still be queued after reading a large usage file.");
            AssertTrue(log.FlushPending(), "Atomic usage flush should succeed after reading a large usage file.");

            TrafficUsageSummaries updatedSummaries = log.GetSummaries(settings);
            AssertEqual(5003L, updatedSummaries.Daily.DownloadBytes, "Atomic append should keep existing valid records.");
            AssertEqual(10004L, updatedSummaries.Daily.UploadBytes, "Atomic append should add new records exactly once.");

            AssertTrue(log.ClearAll(), "Large usage data should be clearable.");
        }

        private static void TestTrafficUsageLogAppendsAfterPartialLine()
        {
            CleanupFile(TrafficUsageLog.GetUsageFilePath());
            CleanupFile(TrafficUsageLog.GetUsageArchiveFilePath());

            MonitorSettings settings = new MonitorSettings(
                "adapter-partial",
                "Partial Adapter",
                900D,
                panelSkinId: PanelSkinCatalog.DefaultSkinId);

            File.WriteAllText(TrafficUsageLog.GetUsageFilePath(), "crash-partial-line-without-newline");

            TrafficUsageLog log = new TrafficUsageLog();
            AssertTrue(log.QueueUsage(settings, 7L, 8L), "A sample after a partial usage line should be queued.");
            AssertTrue(log.FlushPending(), "A sample after a partial usage line should flush successfully.");

            TrafficUsageSummaries summaries = log.GetSummaries(settings);
            AssertEqual(7L, summaries.Daily.DownloadBytes, "Appended usage should not be glued to a partial invalid line.");
            AssertEqual(8L, summaries.Daily.UploadBytes, "Appended usage should remain parseable after a partial invalid line.");

            AssertTrue(log.ClearAll(), "Usage data with a partial line should be clearable.");
        }

        private static void TestAppLogRotatesLargeLogFile()
        {
            string logPath = AppLog.GetCurrentLogPath();
            string logDirectory = Path.GetDirectoryName(logPath);
            Directory.CreateDirectory(logDirectory);
            CleanupFile(logPath);
            CleanupFile(logPath + ".1");
            CleanupFile(logPath + ".2");
            CleanupFile(logPath + ".3");
            CleanupFile(logPath + ".4");

            File.WriteAllText(logPath, new string('x', 300 * 1024));
            File.WriteAllText(logPath + ".1", "old-one");
            File.WriteAllText(logPath + ".2", "old-two");
            File.WriteAllText(logPath + ".3", "old-three");
            AppLog.Info("rotation smoke test");

            AssertTrue(File.Exists(logPath + ".1"), "Large log files should rotate to a .1 backup.");
            AssertTrue(File.Exists(logPath + ".2"), "Existing .1 log backup should rotate to .2.");
            AssertTrue(File.Exists(logPath + ".3"), "Existing .2 log backup should rotate to .3.");
            AssertTrue(!File.Exists(logPath + ".4"), "Log rotation should keep only the configured backup count.");
            AssertTrue(File.Exists(logPath), "A new current log file should be created after rotation.");
            AssertEqual("old-one", File.ReadAllText(logPath + ".2"), "The previous .1 backup should move to .2.");
            AssertEqual("old-two", File.ReadAllText(logPath + ".3"), "The previous .2 backup should move to .3.");

            FileInfo currentLog = new FileInfo(logPath);
            AssertTrue(currentLog.Length < 32 * 1024, "The current log file should only contain new entries after rotation.");
        }

        private static void TestDiagnosticsExportIncludesRotatedLogs()
        {
            string logPath = AppLog.GetCurrentLogPath();
            string diagnosticsZipPath = Path.Combine(BaseDirectory, "diagnostics-export.zip");
            CleanupFile(diagnosticsZipPath);
            CleanupFile(logPath);
            CleanupFile(logPath + ".1");
            CleanupFile(logPath + ".2");
            CleanupFile(logPath + ".3");

            Directory.CreateDirectory(Path.GetDirectoryName(logPath));
            File.WriteAllText(logPath, "current-log");
            File.WriteAllText(logPath + ".1", "backup-one");
            File.WriteAllText(logPath + ".2", "backup-two");
            File.WriteAllText(logPath + ".3", "backup-three");

            DiagnosticsExport.WriteZip(diagnosticsZipPath, "diagnostics-body");

            using (ZipArchive archive = ZipFile.OpenRead(diagnosticsZipPath))
            {
                AssertTrue(archive.GetEntry("diagnostics.txt") != null, "Diagnostics ZIP should contain diagnostics text.");
                AssertTrue(archive.GetEntry("TrafficView.log") != null, "Diagnostics ZIP should contain the current log.");
                AssertTrue(archive.GetEntry("TrafficView.log.1") != null, "Diagnostics ZIP should contain the first rotated log.");
                AssertTrue(archive.GetEntry("TrafficView.log.2") != null, "Diagnostics ZIP should contain the second rotated log.");
                AssertTrue(archive.GetEntry("TrafficView.log.3") != null, "Diagnostics ZIP should contain the third rotated log.");
            }

            CleanupFile(diagnosticsZipPath);
        }

        private static void TestStorageDiagnosticsReportsWritableSettingsPath()
        {
            string diagnostics = AppStorage.CreateStorageDiagnosticsText();
            AssertTrue(
                diagnostics.IndexOf("Settings path writable: yes", StringComparison.OrdinalIgnoreCase) >= 0,
                "Storage diagnostics should report the portable test settings path as writable.");
        }

        private static void TestRuntimeDiagnosticsReportsMemoryAndStartup()
        {
            RuntimeDiagnostics.MarkStartupCompleted();
            string diagnostics = RuntimeDiagnostics.CreateDiagnosticsText("Timers: smoke");

            AssertTrue(
                diagnostics.IndexOf("Startup ready:", StringComparison.OrdinalIgnoreCase) >= 0,
                "Runtime diagnostics should report startup readiness.");
            AssertTrue(
                diagnostics.IndexOf("Working set:", StringComparison.OrdinalIgnoreCase) >= 0,
                "Runtime diagnostics should report process memory.");
            AssertTrue(
                diagnostics.IndexOf("Timers: smoke", StringComparison.OrdinalIgnoreCase) >= 0,
                "Runtime diagnostics should include timer details.");
            AssertEqual("1.0 KB", RuntimeDiagnostics.FormatBytes(1024L), "Byte formatting should use binary units.");
        }

        private static void CreateTestSkin(string id, string directoryName)
        {
            string skinsDirectoryPath = Path.Combine(BaseDirectory, "Skins");
            string skinDirectoryPath = Path.Combine(skinsDirectoryPath, directoryName);
            Directory.CreateDirectory(skinDirectoryPath);

            File.WriteAllLines(
                Path.Combine(skinDirectoryPath, "skin.ini"),
                new[]
                {
                    "Id=" + id,
                    "DisplayNameFallback=" + directoryName,
                    "SurfaceEffect=none"
                });

            CreatePng(Path.Combine(skinDirectoryPath, "TrafficView.panel.90.png"), 92, 50);
            CreatePng(Path.Combine(skinDirectoryPath, "TrafficView.panel.png"), 102, 56);
            CreatePng(Path.Combine(skinDirectoryPath, "TrafficView.panel.110.png"), 112, 62);
            CreatePng(Path.Combine(skinDirectoryPath, "TrafficView.panel.125.png"), 128, 70);
            CreatePng(Path.Combine(skinDirectoryPath, "TrafficView.panel.150.png"), 153, 84);
        }

        private static void CreatePng(string path, int width, int height)
        {
            using (Bitmap bitmap = new Bitmap(width, height))
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.FromArgb(12, 48, 12));
                }

                bitmap.Save(path, ImageFormat.Png);
            }
        }

        private static void CleanupFile(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static void CleanupDirectory(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!object.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    string.Format("{0} Expected='{1}' Actual='{2}'", message, expected, actual));
            }
        }

        private static class MonitorSettingsTestPaths
        {
            public static readonly string SettingsPath = Path.Combine(BaseDirectory, "TrafficView.settings.ini");
            public static readonly string SettingsBackupPath = Path.Combine(BaseDirectory, "TrafficView.settings.ini_");
        }
    }
}
