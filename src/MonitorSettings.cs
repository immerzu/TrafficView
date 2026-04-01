using System;
using System.Drawing;
using System.IO;

namespace TrafficView
{
    internal sealed class MonitorSettings
    {
        private const string SettingsFileName = "TrafficView.settings.ini";
        private static readonly int[] SupportedPopupScalePercents = new int[] { 90, 100, 110, 125, 150 };
        public MonitorSettings(
            string adapterId,
            string adapterName,
            double calibrationPeakBytesPerSecond,
            double calibrationDownloadPeakBytesPerSecond = 0D,
            double calibrationUploadPeakBytesPerSecond = 0D,
            bool initialCalibrationPromptHandled = false,
            bool initialLanguagePromptHandled = false,
            int transparencyPercent = 0,
            string languageCode = "de",
            bool hasSavedPopupLocation = false,
            int popupLocationX = 0,
            int popupLocationY = 0,
            int popupScalePercent = 100,
            string panelSkinId = "08")
        {
            this.AdapterId = adapterId ?? string.Empty;
            this.AdapterName = adapterName ?? string.Empty;
            this.CalibrationPeakBytesPerSecond = Math.Max(0D, calibrationPeakBytesPerSecond);
            this.CalibrationDownloadPeakBytesPerSecond = Math.Max(0D, calibrationDownloadPeakBytesPerSecond);
            this.CalibrationUploadPeakBytesPerSecond = Math.Max(0D, calibrationUploadPeakBytesPerSecond);
            this.InitialCalibrationPromptHandled = initialCalibrationPromptHandled;
            this.InitialLanguagePromptHandled = initialLanguagePromptHandled;
            this.TransparencyPercent = ClampTransparencyPercent(transparencyPercent);
            this.LanguageCode = UiLanguage.NormalizeLanguageCode(languageCode);
            this.HasSavedPopupLocation = hasSavedPopupLocation;
            this.PopupLocationX = popupLocationX;
            this.PopupLocationY = popupLocationY;
            this.PopupScalePercent = NormalizePopupScalePercent(popupScalePercent);
            this.PanelSkinId = NormalizePanelSkinId(panelSkinId);
        }

        public string AdapterId { get; private set; }

        public string AdapterName { get; private set; }

        public double CalibrationPeakBytesPerSecond { get; private set; }

        public double CalibrationDownloadPeakBytesPerSecond { get; private set; }

        public double CalibrationUploadPeakBytesPerSecond { get; private set; }

        public bool InitialCalibrationPromptHandled { get; private set; }

        public bool InitialLanguagePromptHandled { get; private set; }

        public int TransparencyPercent { get; private set; }

        public string LanguageCode { get; private set; }

        public bool HasSavedPopupLocation { get; private set; }

        public int PopupLocationX { get; private set; }

        public int PopupLocationY { get; private set; }

        public Point PopupLocation
        {
            get
            {
                return new Point(this.PopupLocationX, this.PopupLocationY);
            }
        }

        public int PopupScalePercent { get; private set; }

        public string PanelSkinId { get; private set; }

        public MonitorSettings Clone()
        {
            return new MonitorSettings(
                this.AdapterId,
                this.AdapterName,
                this.CalibrationPeakBytesPerSecond,
                this.CalibrationDownloadPeakBytesPerSecond,
                this.CalibrationUploadPeakBytesPerSecond,
                this.InitialCalibrationPromptHandled,
                this.InitialLanguagePromptHandled,
                this.TransparencyPercent,
                this.LanguageCode,
                this.HasSavedPopupLocation,
                this.PopupLocationX,
                this.PopupLocationY,
                this.PopupScalePercent,
                this.PanelSkinId);
        }

        public MonitorSettings WithInitialCalibrationPromptHandled(bool handled)
        {
            return new MonitorSettings(
                this.AdapterId,
                this.AdapterName,
                this.CalibrationPeakBytesPerSecond,
                this.CalibrationDownloadPeakBytesPerSecond,
                this.CalibrationUploadPeakBytesPerSecond,
                handled,
                this.InitialLanguagePromptHandled,
                this.TransparencyPercent,
                this.LanguageCode,
                this.HasSavedPopupLocation,
                this.PopupLocationX,
                this.PopupLocationY,
                this.PopupScalePercent,
                this.PanelSkinId);
        }

        public MonitorSettings WithInitialLanguagePromptHandled(bool handled)
        {
            return new MonitorSettings(
                this.AdapterId,
                this.AdapterName,
                this.CalibrationPeakBytesPerSecond,
                this.CalibrationDownloadPeakBytesPerSecond,
                this.CalibrationUploadPeakBytesPerSecond,
                this.InitialCalibrationPromptHandled,
                handled,
                this.TransparencyPercent,
                this.LanguageCode,
                this.HasSavedPopupLocation,
                this.PopupLocationX,
                this.PopupLocationY,
                this.PopupScalePercent,
                this.PanelSkinId);
        }

        public MonitorSettings WithTransparencyPercent(int transparencyPercent)
        {
            return new MonitorSettings(
                this.AdapterId,
                this.AdapterName,
                this.CalibrationPeakBytesPerSecond,
                this.CalibrationDownloadPeakBytesPerSecond,
                this.CalibrationUploadPeakBytesPerSecond,
                this.InitialCalibrationPromptHandled,
                this.InitialLanguagePromptHandled,
                transparencyPercent,
                this.LanguageCode,
                this.HasSavedPopupLocation,
                this.PopupLocationX,
                this.PopupLocationY,
                this.PopupScalePercent,
                this.PanelSkinId);
        }

        public MonitorSettings WithLanguageCode(string languageCode)
        {
            return new MonitorSettings(
                this.AdapterId,
                this.AdapterName,
                this.CalibrationPeakBytesPerSecond,
                this.CalibrationDownloadPeakBytesPerSecond,
                this.CalibrationUploadPeakBytesPerSecond,
                this.InitialCalibrationPromptHandled,
                this.InitialLanguagePromptHandled,
                this.TransparencyPercent,
                languageCode,
                this.HasSavedPopupLocation,
                this.PopupLocationX,
                this.PopupLocationY,
                this.PopupScalePercent,
                this.PanelSkinId);
        }

        public MonitorSettings WithPopupLocation(Point popupLocation)
        {
            return new MonitorSettings(
                this.AdapterId,
                this.AdapterName,
                this.CalibrationPeakBytesPerSecond,
                this.CalibrationDownloadPeakBytesPerSecond,
                this.CalibrationUploadPeakBytesPerSecond,
                this.InitialCalibrationPromptHandled,
                this.InitialLanguagePromptHandled,
                this.TransparencyPercent,
                this.LanguageCode,
                true,
                popupLocation.X,
                popupLocation.Y,
                this.PopupScalePercent,
                this.PanelSkinId);
        }

        public MonitorSettings WithPopupScalePercent(int popupScalePercent)
        {
            return new MonitorSettings(
                this.AdapterId,
                this.AdapterName,
                this.CalibrationPeakBytesPerSecond,
                this.CalibrationDownloadPeakBytesPerSecond,
                this.CalibrationUploadPeakBytesPerSecond,
                this.InitialCalibrationPromptHandled,
                this.InitialLanguagePromptHandled,
                this.TransparencyPercent,
                this.LanguageCode,
                this.HasSavedPopupLocation,
                this.PopupLocationX,
                this.PopupLocationY,
                popupScalePercent,
                this.PanelSkinId);
        }

        public MonitorSettings WithPanelSkinId(string panelSkinId)
        {
            return new MonitorSettings(
                this.AdapterId,
                this.AdapterName,
                this.CalibrationPeakBytesPerSecond,
                this.CalibrationDownloadPeakBytesPerSecond,
                this.CalibrationUploadPeakBytesPerSecond,
                this.InitialCalibrationPromptHandled,
                this.InitialLanguagePromptHandled,
                this.TransparencyPercent,
                this.LanguageCode,
                this.HasSavedPopupLocation,
                this.PopupLocationX,
                this.PopupLocationY,
                this.PopupScalePercent,
                panelSkinId);
        }

        public double GetDownloadVisualizationPeak()
        {
            if (this.CalibrationDownloadPeakBytesPerSecond > 0D)
            {
                return this.CalibrationDownloadPeakBytesPerSecond;
            }

            return this.CalibrationPeakBytesPerSecond;
        }

        public double GetUploadVisualizationPeak()
        {
            if (this.CalibrationUploadPeakBytesPerSecond > 0D)
            {
                return this.CalibrationUploadPeakBytesPerSecond;
            }

            return this.CalibrationPeakBytesPerSecond;
        }

        public string GetAdapterDisplayName()
        {
            if (string.IsNullOrEmpty(this.AdapterId))
            {
                return UiLanguage.Get("Common.Automatic", "Automatisch");
            }

            if (string.IsNullOrEmpty(this.AdapterName))
            {
                return UiLanguage.Get("Common.Adapter", "Adapter");
            }

            return this.AdapterName;
        }

        public bool HasAdapterSelection()
        {
            return !string.IsNullOrEmpty(this.AdapterId) || !string.IsNullOrEmpty(this.AdapterName);
        }

        public bool HasCalibrationData()
        {
            return this.CalibrationPeakBytesPerSecond > 0D ||
                this.CalibrationDownloadPeakBytesPerSecond > 0D ||
                this.CalibrationUploadPeakBytesPerSecond > 0D;
        }

        public void Save()
        {
            string settingsPath = GetSettingsPath();
            if (!TryWriteSettingsFile(settingsPath, this.CreateSerializedLines()))
            {
                AppLog.WarnOnce(
                    "settings-save-failed-" + settingsPath,
                    string.Format("Settings save failed; current values could not be persisted to '{0}'.", settingsPath));
            }
        }

        public static MonitorSettings Load()
        {
            MonitorSettings settings = new MonitorSettings(string.Empty, string.Empty, 0D);
            string settingsPath = GetSettingsPath();
            string[] lines;
            bool primarySettingsFileExists = File.Exists(settingsPath);

            if (TryReadSettingsLines(settingsPath, out lines))
            {
                return LoadFromLines(lines, settings);
            }

            string legacySettingsPath = GetLegacySettingsPath();
            bool legacySettingsFileExists = !ArePathsEqual(settingsPath, legacySettingsPath) &&
                File.Exists(legacySettingsPath);
            if (!ArePathsEqual(settingsPath, legacySettingsPath) &&
                !File.Exists(settingsPath) &&
                TryReadSettingsLines(legacySettingsPath, out lines))
            {
                MonitorSettings migratedSettings = LoadFromLines(lines, settings);
                AppLog.WarnOnce(
                    "settings-legacy-migration-" + legacySettingsPath,
                    string.Format(
                        "Legacy settings path '{0}' was used and migration to '{1}' was attempted.",
                        legacySettingsPath,
                        settingsPath));
                TryWriteSettingsFile(settingsPath, migratedSettings.CreateSerializedLines());
                return migratedSettings;
            }

            if (primarySettingsFileExists || legacySettingsFileExists)
            {
                AppLog.WarnOnce(
                    "settings-load-defaults",
                    string.Format(
                        "Settings could not be loaded from available path(s); defaults are being used. Primary='{0}', Legacy='{1}'.",
                        settingsPath,
                        legacySettingsPath));
            }

            return settings;
        }

        public static bool SettingsFileExists()
        {
            return File.Exists(GetSettingsPath()) || File.Exists(GetLegacySettingsPath());
        }

        private static MonitorSettings LoadFromLines(string[] lines, MonitorSettings defaults)
        {
            defaults = defaults ?? new MonitorSettings(string.Empty, string.Empty, 0D);

            if (lines == null || lines.Length == 0)
            {
                return defaults;
            }

            string adapterId = defaults.AdapterId;
            string adapterName = defaults.AdapterName;
            double calibrationPeak = defaults.CalibrationPeakBytesPerSecond;
            double calibrationDownloadPeak = defaults.CalibrationDownloadPeakBytesPerSecond;
            double calibrationUploadPeak = defaults.CalibrationUploadPeakBytesPerSecond;
            bool initialCalibrationPromptHandled = defaults.InitialCalibrationPromptHandled;
            bool initialLanguagePromptHandled = defaults.InitialLanguagePromptHandled;
            int transparencyPercent = defaults.TransparencyPercent;
            string languageCode = defaults.LanguageCode;
            bool hasSavedPopupLocation = defaults.HasSavedPopupLocation;
            int popupLocationX = defaults.PopupLocationX;
            int popupLocationY = defaults.PopupLocationY;
            int popupScalePercent = defaults.PopupScalePercent;
            string panelSkinId = defaults.PanelSkinId;

            foreach (string line in lines)
            {
                int splitIndex = line.IndexOf('=');
                if (splitIndex <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, splitIndex).Trim();
                string value = line.Substring(splitIndex + 1).Trim();

                if (string.Equals(key, "AdapterId", StringComparison.OrdinalIgnoreCase))
                {
                    adapterId = value;
                    continue;
                }

                if (string.Equals(key, "AdapterName", StringComparison.OrdinalIgnoreCase))
                {
                    adapterName = value;
                    continue;
                }

                if (string.Equals(key, "CalibrationPeakBytesPerSecond", StringComparison.OrdinalIgnoreCase))
                {
                    double parsedValue;
                    if (double.TryParse(
                        value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out parsedValue))
                    {
                        calibrationPeak = parsedValue;
                    }

                    continue;
                }

                if (string.Equals(key, "CalibrationDownloadPeakBytesPerSecond", StringComparison.OrdinalIgnoreCase))
                {
                    double parsedValue;
                    if (double.TryParse(
                        value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out parsedValue))
                    {
                        calibrationDownloadPeak = parsedValue;
                    }

                    continue;
                }

                if (string.Equals(key, "CalibrationUploadPeakBytesPerSecond", StringComparison.OrdinalIgnoreCase))
                {
                    double parsedValue;
                    if (double.TryParse(
                        value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out parsedValue))
                    {
                        calibrationUploadPeak = parsedValue;
                    }

                    continue;
                }

                if (string.Equals(key, "InitialCalibrationPromptHandled", StringComparison.OrdinalIgnoreCase))
                {
                    initialCalibrationPromptHandled = string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (string.Equals(key, "InitialLanguagePromptHandled", StringComparison.OrdinalIgnoreCase))
                {
                    initialLanguagePromptHandled = string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (string.Equals(key, "TransparencyPercent", StringComparison.OrdinalIgnoreCase))
                {
                    int parsedValue;
                    if (int.TryParse(value, out parsedValue))
                    {
                        transparencyPercent = ClampTransparencyPercent(parsedValue);
                    }

                    continue;
                }

                if (string.Equals(key, "LanguageCode", StringComparison.OrdinalIgnoreCase))
                {
                    languageCode = UiLanguage.NormalizeLanguageCode(value);
                    continue;
                }

                if (string.Equals(key, "HasSavedPopupLocation", StringComparison.OrdinalIgnoreCase))
                {
                    hasSavedPopupLocation = string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (string.Equals(key, "PopupLocationX", StringComparison.OrdinalIgnoreCase))
                {
                    int parsedValue;
                    if (int.TryParse(value, out parsedValue))
                    {
                        popupLocationX = parsedValue;
                    }

                    continue;
                }

                if (string.Equals(key, "PopupLocationY", StringComparison.OrdinalIgnoreCase))
                {
                    int parsedValue;
                    if (int.TryParse(value, out parsedValue))
                    {
                        popupLocationY = parsedValue;
                    }

                    continue;
                }

                if (string.Equals(key, "PopupScalePercent", StringComparison.OrdinalIgnoreCase))
                {
                    int parsedValue;
                    if (int.TryParse(value, out parsedValue))
                    {
                        popupScalePercent = NormalizePopupScalePercent(parsedValue);
                    }

                    continue;
                }

                if (string.Equals(key, "PanelSkinId", StringComparison.OrdinalIgnoreCase))
                {
                    panelSkinId = NormalizePanelSkinId(value);
                }
            }

            return new MonitorSettings(
                adapterId,
                adapterName,
                calibrationPeak,
                calibrationDownloadPeak,
                calibrationUploadPeak,
                initialCalibrationPromptHandled,
                initialLanguagePromptHandled,
                transparencyPercent,
                languageCode,
                hasSavedPopupLocation,
                popupLocationX,
                popupLocationY,
                popupScalePercent,
                panelSkinId);
        }

        private string[] CreateSerializedLines()
        {
            return new string[]
            {
                string.Format("AdapterId={0}", this.AdapterId),
                string.Format("AdapterName={0}", this.AdapterName),
                string.Format(
                    "CalibrationPeakBytesPerSecond={0}",
                    this.CalibrationPeakBytesPerSecond.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                string.Format(
                    "CalibrationDownloadPeakBytesPerSecond={0}",
                    this.CalibrationDownloadPeakBytesPerSecond.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                string.Format(
                    "CalibrationUploadPeakBytesPerSecond={0}",
                    this.CalibrationUploadPeakBytesPerSecond.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                string.Format("InitialCalibrationPromptHandled={0}", this.InitialCalibrationPromptHandled ? "1" : "0"),
                string.Format("InitialLanguagePromptHandled={0}", this.InitialLanguagePromptHandled ? "1" : "0"),
                string.Format("TransparencyPercent={0}", this.TransparencyPercent),
                string.Format("LanguageCode={0}", this.LanguageCode),
                string.Format("HasSavedPopupLocation={0}", this.HasSavedPopupLocation ? "1" : "0"),
                string.Format("PopupLocationX={0}", this.PopupLocationX),
                string.Format("PopupLocationY={0}", this.PopupLocationY),
                string.Format("PopupScalePercent={0}", this.PopupScalePercent),
                string.Format("PanelSkinId={0}", this.PanelSkinId)
            };
        }

        public static int[] GetSupportedPopupScalePercents()
        {
            return (int[])SupportedPopupScalePercents.Clone();
        }

        public static string[] GetSupportedPanelSkinIds()
        {
            return PanelSkinCatalog.GetSupportedSkinIds();
        }

        private static string GetSettingsPath()
        {
            return Path.Combine(GetSettingsDirectoryPath(), SettingsFileName);
        }

        private static string GetLegacySettingsPath()
        {
            return Path.Combine(AppStorage.BaseDirectory, SettingsFileName);
        }

        private static string GetSettingsDirectoryPath()
        {
            return AppStorage.GetSettingsDirectoryPath();
        }

        private static bool TryReadSettingsLines(string path, out string[] lines)
        {
            lines = null;

            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                if (!File.Exists(path))
                {
                    return false;
                }

                lines = File.ReadAllLines(path);
                return true;
            }
            catch (IOException)
            {
                AppLog.WarnOnce(
                    "settings-read-io-" + (path ?? string.Empty),
                    string.Format("Settings file could not be read: {0}", path ?? string.Empty));
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                AppLog.WarnOnce(
                    "settings-read-access-" + (path ?? string.Empty),
                    string.Format("Settings file is not accessible: {0}", path ?? string.Empty));
                return false;
            }
            catch (System.Security.SecurityException)
            {
                AppLog.WarnOnce(
                    "settings-read-security-" + (path ?? string.Empty),
                    string.Format("Settings file access was denied by security policy: {0}", path ?? string.Empty));
                return false;
            }
            catch (NotSupportedException)
            {
                AppLog.WarnOnce(
                    "settings-read-path-" + (path ?? string.Empty),
                    string.Format("Settings file path is not supported: {0}", path ?? string.Empty));
                return false;
            }
        }

        private static bool TryWriteSettingsFile(string path, string[] lines)
        {
            if (string.IsNullOrWhiteSpace(path) || lines == null)
            {
                return false;
            }

            string tempPath = null;

            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllLines(tempPath, lines);

                if (File.Exists(path))
                {
                    File.Replace(tempPath, path, null, true);
                }
                else
                {
                    File.Move(tempPath, path);
                }

                return true;
            }
            catch (IOException)
            {
                AppLog.WarnOnce(
                    "settings-write-io-" + (path ?? string.Empty),
                    string.Format("Settings file could not be written: {0}", path ?? string.Empty));
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                AppLog.WarnOnce(
                    "settings-write-access-" + (path ?? string.Empty),
                    string.Format("Settings file is not writable: {0}", path ?? string.Empty));
                return false;
            }
            catch (System.Security.SecurityException)
            {
                AppLog.WarnOnce(
                    "settings-write-security-" + (path ?? string.Empty),
                    string.Format("Settings file write was denied by security policy: {0}", path ?? string.Empty));
                return false;
            }
            catch (NotSupportedException)
            {
                AppLog.WarnOnce(
                    "settings-write-path-" + (path ?? string.Empty),
                    string.Format("Settings file path is not supported for writing: {0}", path ?? string.Empty));
                return false;
            }
            finally
            {
                TryDeleteFile(tempPath);
            }
        }

        private static void TryDeleteFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
                AppLog.WarnOnce(
                    "settings-temp-delete-io-" + (path ?? string.Empty),
                    string.Format("Temporary settings file could not be deleted: {0}", path ?? string.Empty));
            }
            catch (UnauthorizedAccessException)
            {
                AppLog.WarnOnce(
                    "settings-temp-delete-access-" + (path ?? string.Empty),
                    string.Format("Temporary settings file is not deletable: {0}", path ?? string.Empty));
            }
            catch (System.Security.SecurityException)
            {
                AppLog.WarnOnce(
                    "settings-temp-delete-security-" + (path ?? string.Empty),
                    string.Format("Temporary settings file deletion was denied by security policy: {0}", path ?? string.Empty));
            }
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

        public static double ToOpacityValue(int transparencyPercent)
        {
            int clamped = ClampTransparencyPercent(transparencyPercent);
            return Math.Max(0D, Math.Min(1D, 1D - (clamped / 100D)));
        }

        public static byte ToOpacityByte(int transparencyPercent)
        {
            return (byte)Math.Round(ToOpacityValue(transparencyPercent) * 255D);
        }

        private static int ClampTransparencyPercent(int transparencyPercent)
        {
            return Math.Max(0, Math.Min(100, transparencyPercent));
        }

        private static int NormalizePopupScalePercent(int popupScalePercent)
        {
            int normalized = SupportedPopupScalePercents[0];
            int bestDistance = Math.Abs(popupScalePercent - normalized);

            for (int i = 1; i < SupportedPopupScalePercents.Length; i++)
            {
                int candidate = SupportedPopupScalePercents[i];
                int distance = Math.Abs(popupScalePercent - candidate);
                if (distance < bestDistance)
                {
                    normalized = candidate;
                    bestDistance = distance;
                }
            }

            return normalized;
        }

        private static string NormalizePanelSkinId(string panelSkinId)
        {
            return PanelSkinCatalog.NormalizeSkinId(panelSkinId);
        }
    }
}
