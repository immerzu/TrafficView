using System;
using System.Drawing;
using System.IO;

namespace TrafficView
{
    internal sealed class MonitorSettings
    {
        private const string SettingsFileName = "TrafficView.settings.ini";
        private const string SettingsBackupFileName = "TrafficView.settings.ini_";
        public const string AutomaticAdapterId = "__AUTO__";
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
            string panelSkinId = "08",
            PopupDisplayMode popupDisplayMode = PopupDisplayMode.Standard,
            PopupSectionMode popupSectionMode = PopupSectionMode.Both,
            bool rotatingMeterGlossEnabled = true,
            bool taskbarIntegrationEnabled = false,
            bool activityBorderGlowEnabled = false,
            PopupSectionMode taskbarPopupSectionMode = PopupSectionMode.RightOnly)
        {
            this.AdapterId = adapterId ?? string.Empty;
            this.AdapterName = adapterName ?? string.Empty;
            this.CalibrationPeakBytesPerSecond = NormalizeCalibrationBytesPerSecond(calibrationPeakBytesPerSecond);
            this.CalibrationDownloadPeakBytesPerSecond = NormalizeCalibrationBytesPerSecond(calibrationDownloadPeakBytesPerSecond);
            this.CalibrationUploadPeakBytesPerSecond = NormalizeCalibrationBytesPerSecond(calibrationUploadPeakBytesPerSecond);
            this.InitialCalibrationPromptHandled = initialCalibrationPromptHandled;
            this.InitialLanguagePromptHandled = initialLanguagePromptHandled;
            this.TransparencyPercent = ClampTransparencyPercent(transparencyPercent);
            this.LanguageCode = UiLanguage.NormalizeLanguageCode(languageCode);
            this.HasSavedPopupLocation = hasSavedPopupLocation;
            this.PopupLocationX = popupLocationX;
            this.PopupLocationY = popupLocationY;
            this.PopupScalePercent = NormalizePopupScalePercent(popupScalePercent);
            this.PanelSkinId = NormalizePanelSkinId(panelSkinId);
            this.PopupDisplayMode = NormalizePopupDisplayMode(popupDisplayMode);
            this.PopupSectionMode = NormalizePopupSectionMode(popupSectionMode);
            this.TaskbarPopupSectionMode = NormalizePopupSectionMode(taskbarPopupSectionMode);
            this.RotatingMeterGlossEnabled = rotatingMeterGlossEnabled;
            this.ActivityBorderGlowEnabled = activityBorderGlowEnabled;
            this.TaskbarIntegrationEnabled = taskbarIntegrationEnabled;
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

        public PopupDisplayMode PopupDisplayMode { get; private set; }

        public PopupSectionMode PopupSectionMode { get; private set; }

        public PopupSectionMode TaskbarPopupSectionMode { get; private set; }

        public bool RotatingMeterGlossEnabled { get; private set; }

        public bool ActivityBorderGlowEnabled { get; private set; }

        public bool TaskbarIntegrationEnabled { get; private set; }

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
                this.PanelSkinId,
                this.PopupDisplayMode,
                this.PopupSectionMode,
                this.RotatingMeterGlossEnabled,
                this.TaskbarIntegrationEnabled,
                this.ActivityBorderGlowEnabled,
                this.TaskbarPopupSectionMode);
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
                this.PanelSkinId,
                this.PopupDisplayMode,
                this.PopupSectionMode,
                this.RotatingMeterGlossEnabled,
                this.TaskbarIntegrationEnabled,
                this.ActivityBorderGlowEnabled,
                this.TaskbarPopupSectionMode);
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
                this.PanelSkinId,
                this.PopupDisplayMode,
                this.PopupSectionMode,
                this.RotatingMeterGlossEnabled,
                this.TaskbarIntegrationEnabled,
                this.ActivityBorderGlowEnabled,
                this.TaskbarPopupSectionMode);
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
                this.PanelSkinId,
                this.PopupDisplayMode,
                this.PopupSectionMode,
                this.RotatingMeterGlossEnabled,
                this.TaskbarIntegrationEnabled,
                this.ActivityBorderGlowEnabled,
                this.TaskbarPopupSectionMode);
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
                this.PanelSkinId,
                this.PopupDisplayMode,
                this.PopupSectionMode,
                this.RotatingMeterGlossEnabled,
                this.TaskbarIntegrationEnabled,
                this.ActivityBorderGlowEnabled,
                this.TaskbarPopupSectionMode);
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
                this.PanelSkinId,
                this.PopupDisplayMode,
                this.PopupSectionMode,
                this.RotatingMeterGlossEnabled,
                this.TaskbarIntegrationEnabled,
                this.ActivityBorderGlowEnabled,
                this.TaskbarPopupSectionMode);
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
                this.PanelSkinId,
                this.PopupDisplayMode,
                this.PopupSectionMode,
                this.RotatingMeterGlossEnabled,
                this.TaskbarIntegrationEnabled,
                this.ActivityBorderGlowEnabled,
                this.TaskbarPopupSectionMode);
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
                panelSkinId,
                this.PopupDisplayMode,
                this.PopupSectionMode,
                this.RotatingMeterGlossEnabled,
                this.TaskbarIntegrationEnabled,
                this.ActivityBorderGlowEnabled,
                this.TaskbarPopupSectionMode);
        }

        public MonitorSettings WithPopupDisplayMode(PopupDisplayMode popupDisplayMode)
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
                this.PanelSkinId,
                popupDisplayMode,
                this.PopupSectionMode,
                this.RotatingMeterGlossEnabled,
                this.TaskbarIntegrationEnabled,
                this.ActivityBorderGlowEnabled,
                this.TaskbarPopupSectionMode);
        }

        public MonitorSettings WithPopupSectionMode(PopupSectionMode popupSectionMode)
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
                this.PanelSkinId,
                this.PopupDisplayMode,
                popupSectionMode,
                this.RotatingMeterGlossEnabled,
                this.TaskbarIntegrationEnabled,
                this.ActivityBorderGlowEnabled,
                this.TaskbarPopupSectionMode);
        }

        public MonitorSettings WithTaskbarPopupSectionMode(PopupSectionMode taskbarPopupSectionMode)
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
                this.PanelSkinId,
                this.PopupDisplayMode,
                this.PopupSectionMode,
                this.RotatingMeterGlossEnabled,
                this.TaskbarIntegrationEnabled,
                this.ActivityBorderGlowEnabled,
                taskbarPopupSectionMode);
        }

        public MonitorSettings WithRotatingMeterGlossEnabled(bool rotatingMeterGlossEnabled)
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
                this.PanelSkinId,
                this.PopupDisplayMode,
                this.PopupSectionMode,
                rotatingMeterGlossEnabled,
                this.TaskbarIntegrationEnabled,
                this.ActivityBorderGlowEnabled,
                this.TaskbarPopupSectionMode);
        }

        public MonitorSettings WithActivityBorderGlowEnabled(bool activityBorderGlowEnabled)
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
                this.PanelSkinId,
                this.PopupDisplayMode,
                this.PopupSectionMode,
                this.RotatingMeterGlossEnabled,
                this.TaskbarIntegrationEnabled,
                activityBorderGlowEnabled,
                this.TaskbarPopupSectionMode);
        }

        public MonitorSettings WithTaskbarIntegrationEnabled(bool taskbarIntegrationEnabled)
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
                this.PanelSkinId,
                this.PopupDisplayMode,
                this.PopupSectionMode,
                this.RotatingMeterGlossEnabled,
                taskbarIntegrationEnabled,
                this.ActivityBorderGlowEnabled,
                this.TaskbarPopupSectionMode);
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
                return UiLanguage.Get("Common.NoAdapter", "Keine Internetverbindung");
            }

            if (this.UsesAutomaticAdapterSelection())
            {
                return UiLanguage.Get("Common.Automatic", "Automatisch");
            }

            if (string.IsNullOrEmpty(this.AdapterName))
            {
                return UiLanguage.Get("Common.Adapter", "Internetverbindung");
            }

            return this.AdapterName;
        }

        public bool HasAdapterSelection()
        {
            return !string.IsNullOrWhiteSpace(this.AdapterId);
        }

        public bool UsesAutomaticAdapterSelection()
        {
            return string.Equals(this.AdapterId, AutomaticAdapterId, StringComparison.OrdinalIgnoreCase);
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
            string backupSettingsPath = GetSettingsBackupPath();
            string[] serializedLines = this.CreateSerializedLines();

            if (!TryWriteSettingsFile(settingsPath, serializedLines))
            {
                AppLog.WarnOnce(
                    "settings-save-failed-" + settingsPath,
                    string.Format("Settings save failed; current values could not be persisted to '{0}'.", settingsPath));
                return;
            }

            if (!TryWriteSettingsFile(backupSettingsPath, serializedLines))
            {
                AppLog.WarnOnce(
                    "settings-backup-save-failed-" + backupSettingsPath,
                    string.Format("Settings backup could not be persisted to '{0}'.", backupSettingsPath));
            }
        }

        public static MonitorSettings Load()
        {
            MonitorSettings settings = new MonitorSettings(string.Empty, string.Empty, 0D);
            string settingsPath = GetSettingsPath();
            string backupSettingsPath = GetSettingsBackupPath();
            string[] lines;
            bool primarySettingsFileExists = File.Exists(settingsPath);
            bool backupSettingsFileExists = !ArePathsEqual(settingsPath, backupSettingsPath) &&
                File.Exists(backupSettingsPath);
            string[] primaryLines = null;
            bool primaryHasRecognizedSettings = false;
            string[] backupLines = null;
            bool backupHasRecognizedSettings = false;

            if (TryReadSettingsLines(settingsPath, out lines))
            {
                if (ContainsRecognizedStoredSetting(lines))
                {
                    primaryLines = lines;
                    primaryHasRecognizedSettings = true;
                }
            }

            if (!ArePathsEqual(settingsPath, backupSettingsPath) &&
                TryReadSettingsLines(backupSettingsPath, out lines) &&
                ContainsRecognizedStoredSetting(lines))
            {
                backupLines = lines;
                backupHasRecognizedSettings = true;
            }

            string legacySettingsPath = GetLegacySettingsPath();
            bool legacySettingsFileExists = !ArePathsEqual(settingsPath, legacySettingsPath) &&
                File.Exists(legacySettingsPath);
            string[] legacyLines = null;
            bool legacyHasRecognizedSettings = false;
            if (!ArePathsEqual(settingsPath, legacySettingsPath) &&
                TryReadSettingsLines(legacySettingsPath, out lines) &&
                ContainsRecognizedStoredSetting(lines))
            {
                legacyLines = lines;
                legacyHasRecognizedSettings = true;
            }

            if (primaryHasRecognizedSettings)
            {
                MonitorSettings primarySettings = LoadFromLines(primaryLines, settings);
                if (!ContainsStoredValidLanguageSetting(primaryLines) &&
                    legacyHasRecognizedSettings &&
                    ContainsStoredValidLanguageSetting(legacyLines))
                {
                    MonitorSettings legacySettings = LoadFromLines(legacyLines, settings);
                    primarySettings = primarySettings.WithLanguageCode(legacySettings.LanguageCode);
                }

                return primarySettings;
            }

            if (backupHasRecognizedSettings)
            {
                MonitorSettings backupSettings = LoadFromLines(backupLines, settings);
                AppLog.WarnOnce(
                    "settings-backup-recovery-" + backupSettingsPath,
                    string.Format(
                        "Settings backup path '{0}' was used because the primary settings were unavailable or invalid.",
                        backupSettingsPath));
                if (!TryWriteSettingsFile(settingsPath, backupSettings.CreateSerializedLines()))
                {
                    AppLog.WarnOnce(
                        "settings-backup-recovery-save-failed-" + settingsPath,
                        string.Format(
                            "Recovered backup settings could not be persisted to '{0}'.",
                            settingsPath));
                }
                return backupSettings;
            }

            if (legacyHasRecognizedSettings)
            {
                MonitorSettings migratedSettings = LoadFromLines(legacyLines, settings);
                AppLog.WarnOnce(
                    "settings-legacy-migration-" + legacySettingsPath,
                    string.Format(
                        "Legacy settings path '{0}' was used and migration to '{1}' was attempted.",
                        legacySettingsPath,
                        settingsPath));
                if (!TryWriteSettingsFile(settingsPath, migratedSettings.CreateSerializedLines()))
                {
                    AppLog.WarnOnce(
                        "settings-legacy-migration-save-failed-" + settingsPath,
                        string.Format(
                            "Legacy settings were loaded from '{0}', but migration could not be persisted to '{1}'.",
                            legacySettingsPath,
                            settingsPath));
                }
                return migratedSettings;
            }

            if (primarySettingsFileExists || backupSettingsFileExists || legacySettingsFileExists)
            {
                AppLog.WarnOnce(
                    "settings-load-defaults",
                    string.Format(
                        "Settings could not be loaded from available path(s); defaults are being used. Primary='{0}', Backup='{1}', Legacy='{2}'.",
                        settingsPath,
                        backupSettingsPath,
                        legacySettingsPath));
            }

            return settings;
        }

        public static bool SettingsFileExists()
        {
            return File.Exists(GetSettingsPath()) ||
                File.Exists(GetSettingsBackupPath()) ||
                File.Exists(GetLegacySettingsPath());
        }

        public static bool HasStoredLanguageSetting()
        {
            string[] lines;
            string settingsPath = GetSettingsPath();
            string backupSettingsPath = GetSettingsBackupPath();
            if (TryReadSettingsLines(settingsPath, out lines))
            {
                if (ContainsRecognizedStoredSetting(lines))
                {
                    if (ContainsStoredValidLanguageSetting(lines))
                    {
                        return true;
                    }
                }
            }

            if (!ArePathsEqual(settingsPath, backupSettingsPath) &&
                TryReadSettingsLines(backupSettingsPath, out lines))
            {
                if (ContainsRecognizedStoredSetting(lines) &&
                    ContainsStoredValidLanguageSetting(lines))
                {
                    return true;
                }
            }

            string legacySettingsPath = GetLegacySettingsPath();
            if (!ArePathsEqual(settingsPath, legacySettingsPath) &&
                TryReadSettingsLines(legacySettingsPath, out lines) &&
                ContainsStoredValidLanguageSetting(lines))
            {
                return true;
            }

            return false;
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
            PopupDisplayMode popupDisplayMode = defaults.PopupDisplayMode;
            PopupSectionMode popupSectionMode = defaults.PopupSectionMode;
            PopupSectionMode taskbarPopupSectionMode = defaults.TaskbarPopupSectionMode;
            bool hasStoredTaskbarPopupSectionMode = false;
            bool rotatingMeterGlossEnabled = defaults.RotatingMeterGlossEnabled;
            bool taskbarIntegrationEnabled = defaults.TaskbarIntegrationEnabled;
            bool activityBorderGlowEnabled = defaults.ActivityBorderGlowEnabled;

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
                    continue;
                }

                if (string.Equals(key, "PopupDisplayMode", StringComparison.OrdinalIgnoreCase))
                {
                    popupDisplayMode = ParsePopupDisplayMode(value, defaults.PopupDisplayMode);
                    continue;
                }

                if (string.Equals(key, "PopupSectionMode", StringComparison.OrdinalIgnoreCase))
                {
                    popupSectionMode = ParsePopupSectionMode(value, defaults.PopupSectionMode);
                    continue;
                }

                if (string.Equals(key, "TaskbarPopupSectionMode", StringComparison.OrdinalIgnoreCase))
                {
                    hasStoredTaskbarPopupSectionMode = true;
                    taskbarPopupSectionMode = ParsePopupSectionMode(value, defaults.TaskbarPopupSectionMode);
                    continue;
                }

                if (string.Equals(key, "RotatingMeterGlossEnabled", StringComparison.OrdinalIgnoreCase))
                {
                    rotatingMeterGlossEnabled = string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (string.Equals(key, "ActivityBorderGlowEnabled", StringComparison.OrdinalIgnoreCase))
                {
                    activityBorderGlowEnabled = string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (string.Equals(key, "TaskbarIntegrationEnabled", StringComparison.OrdinalIgnoreCase))
                {
                    taskbarIntegrationEnabled = string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

            }

            if (!hasStoredTaskbarPopupSectionMode && taskbarIntegrationEnabled)
            {
                taskbarPopupSectionMode = popupSectionMode;
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
                panelSkinId,
                popupDisplayMode,
                popupSectionMode,
                rotatingMeterGlossEnabled,
                taskbarIntegrationEnabled,
                activityBorderGlowEnabled,
                taskbarPopupSectionMode);
        }

        private static bool ContainsStoredValidLanguageSetting(string[] lines)
        {
            string value;
            return TryGetStoredSettingValue(lines, "LanguageCode", out value) &&
                IsRecognizedStoredLanguageCode(value);
        }

        private static bool ContainsRecognizedStoredSetting(string[] lines)
        {
            if (lines == null || lines.Length == 0)
            {
                return false;
            }

            if (ContainsStoredSavedPopupLocation(lines))
            {
                return true;
            }

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                int splitIndex = line.IndexOf('=');
                if (splitIndex <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, splitIndex).Trim();
                string value = line.Substring(splitIndex + 1).Trim();
                if (!IsRecognizedStoredSettingValue(key, value))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static bool ContainsStoredSetting(string[] lines, string keyName)
        {
            string ignored;
            return TryGetStoredSettingValue(lines, keyName, out ignored);
        }

        private static bool IsRecognizedStoredSettingValue(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (string.Equals(key, "AdapterId", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(key, "CalibrationPeakBytesPerSecond", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "CalibrationDownloadPeakBytesPerSecond", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "CalibrationUploadPeakBytesPerSecond", StringComparison.OrdinalIgnoreCase))
            {
                double parsedValue;
                return double.TryParse(
                    value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out parsedValue) &&
                    NormalizeCalibrationBytesPerSecond(parsedValue) > 0D;
            }

            if (string.Equals(key, "TransparencyPercent", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "PopupScalePercent", StringComparison.OrdinalIgnoreCase))
            {
                int parsedValue;
                return int.TryParse(value, out parsedValue);
            }

            if (string.Equals(key, "LanguageCode", StringComparison.OrdinalIgnoreCase))
            {
                return IsRecognizedStoredLanguageCode(value);
            }

            if (string.Equals(key, "PanelSkinId", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(
                    PanelSkinCatalog.NormalizeSkinId(value),
                    value.Trim(),
                    StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(key, "PopupDisplayMode", StringComparison.OrdinalIgnoreCase))
            {
                PopupDisplayMode parsedMode;
                return TryParsePopupDisplayMode(value, out parsedMode);
            }

            if (string.Equals(key, "PopupSectionMode", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "TaskbarPopupSectionMode", StringComparison.OrdinalIgnoreCase))
            {
                PopupSectionMode parsedMode;
                return TryParsePopupSectionMode(value, out parsedMode);
            }

            if (string.Equals(key, "RotatingMeterGlossEnabled", StringComparison.OrdinalIgnoreCase))
            {
                bool parsedValue;
                return TryParseStoredBoolean(value, out parsedValue);
            }

            if (string.Equals(key, "ActivityBorderGlowEnabled", StringComparison.OrdinalIgnoreCase))
            {
                bool parsedValue;
                return TryParseStoredBoolean(value, out parsedValue);
            }

            if (string.Equals(key, "TaskbarIntegrationEnabled", StringComparison.OrdinalIgnoreCase))
            {
                bool parsedValue;
                return TryParseStoredBoolean(value, out parsedValue);
            }

            return false;
        }

        private static bool ContainsStoredSavedPopupLocation(string[] lines)
        {
            string hasSavedPopupLocationValue;
            if (!TryGetStoredSettingValue(lines, "HasSavedPopupLocation", out hasSavedPopupLocationValue))
            {
                return false;
            }

            bool hasSavedPopupLocation;
            if (!TryParseStoredBoolean(hasSavedPopupLocationValue, out hasSavedPopupLocation) ||
                !hasSavedPopupLocation)
            {
                return false;
            }

            string popupLocationXValue;
            string popupLocationYValue;
            int popupLocationX;
            int popupLocationY;
            return TryGetStoredSettingValue(lines, "PopupLocationX", out popupLocationXValue) &&
                int.TryParse(popupLocationXValue, out popupLocationX) &&
                TryGetStoredSettingValue(lines, "PopupLocationY", out popupLocationYValue) &&
                int.TryParse(popupLocationYValue, out popupLocationY);
        }

        private static bool TryGetStoredSettingValue(string[] lines, string keyName, out string value)
        {
            value = string.Empty;

            if (lines == null || lines.Length == 0 || string.IsNullOrWhiteSpace(keyName))
            {
                return false;
            }

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                int splitIndex = line.IndexOf('=');
                if (splitIndex <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, splitIndex).Trim();
                if (!string.Equals(key, keyName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                value = line.Substring(splitIndex + 1).Trim();
                return !string.IsNullOrWhiteSpace(value);
            }

            return false;
        }

        private static bool TryParseStoredBoolean(string value, out bool parsedValue)
        {
            parsedValue = false;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = value.Trim();
            if (string.Equals(normalized, "1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "yes", StringComparison.OrdinalIgnoreCase))
            {
                parsedValue = true;
                return true;
            }

            if (string.Equals(normalized, "0", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "false", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "no", StringComparison.OrdinalIgnoreCase))
            {
                parsedValue = false;
                return true;
            }

            return false;
        }

        private static bool IsRecognizedStoredLanguageCode(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
            {
                return false;
            }

            string normalized = languageCode.Trim();
            LanguageOption[] supportedLanguages = UiLanguage.GetSupportedLanguages();

            for (int i = 0; i < supportedLanguages.Length; i++)
            {
                if (string.Equals(supportedLanguages[i].Code, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return string.Equals(normalized, "zh", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "zh-cn", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "zh-hans", StringComparison.OrdinalIgnoreCase);
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
                string.Format("PanelSkinId={0}", this.PanelSkinId),
                string.Format("PopupDisplayMode={0}", this.PopupDisplayMode),
                string.Format("PopupSectionMode={0}", this.PopupSectionMode),
                string.Format("TaskbarPopupSectionMode={0}", this.TaskbarPopupSectionMode),
                string.Format("RotatingMeterGlossEnabled={0}", this.RotatingMeterGlossEnabled ? "1" : "0"),
                string.Format("ActivityBorderGlowEnabled={0}", this.ActivityBorderGlowEnabled ? "1" : "0"),
                string.Format("TaskbarIntegrationEnabled={0}", this.TaskbarIntegrationEnabled ? "1" : "0")
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

        private static string GetSettingsBackupPath()
        {
            return Path.Combine(GetSettingsDirectoryPath(), SettingsBackupFileName);
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
                EnsurePortableSettingsPathAllowed(path);

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
                EnsurePortableSettingsPathAllowed(path);

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
                EnsurePortableSettingsPathAllowed(path);

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

        private static void EnsurePortableSettingsPathAllowed(string path)
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
                    "Settings-Pfad liegt ausserhalb des Portable-Verzeichnisses: '{0}'.",
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

        private static double NormalizeCalibrationBytesPerSecond(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0D)
            {
                return 0D;
            }

            return value;
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
            return PanelSkinCatalog.DefaultSkinId;
        }

        private static PopupDisplayMode NormalizePopupDisplayMode(PopupDisplayMode popupDisplayMode)
        {
            if (popupDisplayMode == PopupDisplayMode.MiniGraph)
            {
                return PopupDisplayMode.MiniGraph;
            }

            if (popupDisplayMode == PopupDisplayMode.MiniSoft)
            {
                return PopupDisplayMode.MiniSoft;
            }

            if (popupDisplayMode == PopupDisplayMode.Simple)
            {
                return PopupDisplayMode.Simple;
            }

            if (popupDisplayMode == PopupDisplayMode.SimpleBlue)
            {
                return PopupDisplayMode.SimpleBlue;
            }

            return PopupDisplayMode.Standard;
        }

        private static PopupSectionMode NormalizePopupSectionMode(PopupSectionMode popupSectionMode)
        {
            if (popupSectionMode == PopupSectionMode.LeftOnly)
            {
                return PopupSectionMode.LeftOnly;
            }

            if (popupSectionMode == PopupSectionMode.RightOnly)
            {
                return PopupSectionMode.RightOnly;
            }

            return PopupSectionMode.Both;
        }

        private static PopupDisplayMode ParsePopupDisplayMode(string value, PopupDisplayMode fallback)
        {
            PopupDisplayMode parsedMode;
            return TryParsePopupDisplayMode(value, out parsedMode)
                ? parsedMode
                : NormalizePopupDisplayMode(fallback);
        }

        private static PopupSectionMode ParsePopupSectionMode(string value, PopupSectionMode fallback)
        {
            PopupSectionMode parsedMode;
            return TryParsePopupSectionMode(value, out parsedMode)
                ? parsedMode
                : NormalizePopupSectionMode(fallback);
        }

        private static bool TryParsePopupDisplayMode(string value, out PopupDisplayMode popupDisplayMode)
        {
            popupDisplayMode = PopupDisplayMode.Standard;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = value.Trim();
            if (string.Equals(normalized, "MiniGraph", StringComparison.OrdinalIgnoreCase))
            {
                popupDisplayMode = PopupDisplayMode.MiniGraph;
                return true;
            }

            if (string.Equals(normalized, "MiniSoft", StringComparison.OrdinalIgnoreCase))
            {
                popupDisplayMode = PopupDisplayMode.MiniSoft;
                return true;
            }

            if (string.Equals(normalized, "Simple", StringComparison.OrdinalIgnoreCase))
            {
                popupDisplayMode = PopupDisplayMode.Simple;
                return true;
            }

            if (string.Equals(normalized, "SimpleBlue", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "Simple blue", StringComparison.OrdinalIgnoreCase))
            {
                popupDisplayMode = PopupDisplayMode.SimpleBlue;
                return true;
            }

            if (string.Equals(normalized, "Standard", StringComparison.OrdinalIgnoreCase))
            {
                popupDisplayMode = PopupDisplayMode.Standard;
                return true;
            }

            return false;
        }

        private static bool TryParsePopupSectionMode(string value, out PopupSectionMode popupSectionMode)
        {
            popupSectionMode = PopupSectionMode.Both;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = value.Trim();
            if (string.Equals(normalized, "LeftOnly", StringComparison.OrdinalIgnoreCase))
            {
                popupSectionMode = PopupSectionMode.LeftOnly;
                return true;
            }

            if (string.Equals(normalized, "RightOnly", StringComparison.OrdinalIgnoreCase))
            {
                popupSectionMode = PopupSectionMode.RightOnly;
                return true;
            }

            if (string.Equals(normalized, "Both", StringComparison.OrdinalIgnoreCase))
            {
                popupSectionMode = PopupSectionMode.Both;
                return true;
            }

            return false;
        }
    }
}
