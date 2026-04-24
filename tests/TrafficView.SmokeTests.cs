using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
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
                TestMonitorSettingsRoundTripPreservesSkinSelection();
                TestTrafficUsageLogRoundTrip();
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
