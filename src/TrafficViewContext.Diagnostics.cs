using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace TrafficView
{
    internal sealed partial class TrafficViewContext
    {
        private void AboutItem_Click(object sender, EventArgs e)
        {
            string diagnosticsText = CreateDiagnosticsText();

            DialogResult result = MessageBox.Show(
                diagnosticsText + "\r\nDiagnosebericht speichern?",
                UiLanguage.Get("Menu.About", "Über TrafficView"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (result == DialogResult.Yes)
            {
                this.ExportDiagnostics(diagnosticsText);
            }
        }

        private string CreateDiagnosticsText()
        {
            StringBuilder message = new StringBuilder();
            message.AppendLine("TrafficView");
            message.AppendLine();
            message.AppendLine("Version: " + typeof(Program).Assembly.GetName().Version);
            message.AppendLine("Portable: " + (AppStorage.IsPortableMode ? "yes" : "no"));
            message.AppendLine("Base path: " + AnonymizeDiagnosticsPath(AppStorage.BaseDirectory));
            message.AppendLine("Settings path: " + AnonymizeDiagnosticsPath(MonitorSettings.GetCurrentSettingsPath()));
            message.AppendLine("Usage path: " + AnonymizeDiagnosticsPath(TrafficUsageLog.GetUsageFilePath()));
            message.AppendLine("Log path: " + AnonymizeDiagnosticsPath(AppLog.GetCurrentLogPath()));
            message.AppendLine();
            message.AppendLine(AnonymizeDiagnosticsText(AppStorage.CreateStorageDiagnosticsText()));
            message.AppendLine();
            message.AppendLine(CreateMaintenanceSummaryText());
            message.AppendLine();
            message.AppendLine(this.CreateSettingsDiagnosticsText());
            message.AppendLine();
            message.AppendLine(this.CreateMonitoringDiagnosticsText());
            message.AppendLine();
            message.AppendLine(CreateBaselinePolicyDiagnosticsText());
            message.AppendLine();
            message.AppendLine(RuntimeDiagnostics.CreateDiagnosticsText(TrafficPopupForm.CreateTimerDiagnosticsText()));
            return message.ToString();
        }

        private string CreateSettingsDiagnosticsText()
        {
            if (this.settings == null)
            {
                return "Settings diagnostics:\r\nSettings diagnostics: n/a";
            }

            MonitorSettingsDiagnostics diag = this.settings.CreateDiagnostics();

            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "Settings diagnostics:\r\nAdapter display name: {0}\r\nUses automatic adapter selection: {1}\r\nCalibration peak: {2}\r\nCalibration download peak: {3}\r\nCalibration upload peak: {4}\r\nTransparency percent: {5}\r\nLanguage code: {6}\r\nHas saved popup location: {7}\r\nPopup location: {8},{9}\r\nPopup scale percent: {10}\r\nPanel skin id: {11}\r\nPopup display mode: {12}\r\nPopup section mode: {13}\r\nRotating meter gloss enabled: {14}\r\nTaskbar integration enabled: {15}\r\nActivity border glow enabled: {16}\r\nTaskbar popup section mode: {17}\r\nCalibration useful threshold: {18}\r\nCalibration peak useful: {19}\r\nCalibration download peak useful: {20}\r\nCalibration upload peak useful: {21}",
                diag.AdapterDisplayName,
                diag.UsesAutomaticAdapterSelection ? "yes" : "no",
                TrafficRateFormatter.FormatSpeed(diag.CalibrationPeakBytesPerSecond),
                TrafficRateFormatter.FormatSpeed(diag.CalibrationDownloadPeakBytesPerSecond),
                TrafficRateFormatter.FormatSpeed(diag.CalibrationUploadPeakBytesPerSecond),
                diag.TransparencyPercent,
                diag.LanguageCode,
                diag.HasSavedPopupLocation ? "yes" : "no",
                diag.PopupLocationX,
                diag.PopupLocationY,
                diag.PopupScalePercent,
                diag.PanelSkinId,
                diag.PopupDisplayMode,
                diag.PopupSectionMode,
                diag.RotatingMeterGlossEnabled ? "yes" : "no",
                diag.TaskbarIntegrationEnabled ? "yes" : "no",
                diag.ActivityBorderGlowEnabled ? "yes" : "no",
                diag.TaskbarPopupSectionMode,
                TrafficRateFormatter.FormatSpeed(diag.MinimumUsefulCalibrationBytesPerSecond),
                diag.HasUsefulCalibrationPeak ? "yes" : "no",
                diag.HasUsefulDownloadCalibrationPeak ? "yes" : "no",
                diag.HasUsefulUploadCalibrationPeak ? "yes" : "no");
        }

        private string CreateMonitoringDiagnosticsText()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(this.popupForm != null && !this.popupForm.IsDisposed
                ? this.popupForm.CreateMonitoringDiagnosticsText()
                : "Monitoring diagnostics:\r\nAdapter selection mode: unknown\r\nConfigured adapter: unknown\r\nLast snapshot adapter: none\r\nLast successful sample UTC: none\r\nCurrent rates: DL 0 B/s / UL 0 B/s\r\nCapture active: no");

            AutomaticAdapterSelectionDiagnostics autoDiagnostics = NetworkSnapshot.GetAutomaticAdapterSelectionDiagnostics();
            builder.AppendLine("Automatic adapter confirmed: " + FormatDiagnosticsNone(autoDiagnostics.ConfirmedAdapterKey));
            builder.AppendLine("Automatic adapter pending: " + FormatDiagnosticsNone(autoDiagnostics.PendingAdapterKey));
            builder.AppendLine(
                "Automatic adapter pending confirmations: " +
                autoDiagnostics.PendingConfirmationCount.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                "/" +
                autoDiagnostics.RequiredConfirmationSamples.ToString(System.Globalization.CultureInfo.InvariantCulture));

            TrafficUsageLogDiagnostics usageDiagnostics = this.trafficUsageLog != null
                ? this.trafficUsageLog.CreateDiagnostics()
                : null;

            if (usageDiagnostics != null)
            {
                builder.AppendLine("Pending usage records: " + usageDiagnostics.PendingRecordCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
                builder.AppendLine("Usage last maintenance UTC: " + FormatDiagnosticsUtc(usageDiagnostics.LastMaintenanceUtc));
                builder.AppendLine("Usage active file exists: " + (usageDiagnostics.ActiveFileExists ? "yes" : "no"));
                builder.AppendLine("Usage active file bytes: " + usageDiagnostics.ActiveFileBytes.ToString(System.Globalization.CultureInfo.InvariantCulture));
                builder.AppendLine("Usage active file limit bytes: " + usageDiagnostics.MaxActiveFileBytes.ToString(System.Globalization.CultureInfo.InvariantCulture));
                builder.AppendLine("Usage active file over limit: " + (usageDiagnostics.ActiveFileBytes >= usageDiagnostics.MaxActiveFileBytes ? "yes" : "no"));
            }
            else
            {
                builder.AppendLine("Pending usage records: n/a");
                builder.AppendLine("Usage diagnostics: n/a");
            }

            return builder.ToString();
        }

        private static string FormatDiagnosticsUtc(DateTime value)
        {
            if (value == DateTime.MinValue)
            {
                return "none";
            }

            DateTime utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
            return utc.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + " UTC";
        }

        private static string CreateBaselinePolicyDiagnosticsText()
        {
            try
            {
                TrafficSnapshotBaselinePolicySelfTestResult result = TrafficSnapshotBaselinePolicy.RunSelfTest();
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("Baseline policy diagnostics:");
                builder.AppendLine("Baseline policy self-test overall: " + (result.AllPassed ? "pass" : "fail"));
                builder.Append(TrafficSnapshotBaselinePolicy.CreateSelfTestReport());
                return builder.ToString();
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce(
                    "baseline-policy-diagnostics-failed",
                    "Baseline policy diagnostics failed.",
                    ex);

                return "Baseline policy diagnostics:\r\nBaseline policy self-test overall: n/a\r\nTrafficSnapshotBaselinePolicy self-test: n/a";
            }
        }

        private static string FormatDiagnosticsNone(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "none" : value;
        }

        private static string AnonymizeDiagnosticsPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value ?? string.Empty;
            }

            string result = value;

            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrWhiteSpace(userProfile))
                {
                    result = ReplacePathPrefix(result, userProfile, "%USERPROFILE%");
                }

                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrWhiteSpace(localAppData))
                {
                    result = ReplacePathPrefix(result, localAppData, "%LOCALAPPDATA%");
                }

                string roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (!string.IsNullOrWhiteSpace(roamingAppData))
                {
                    result = ReplacePathPrefix(result, roamingAppData, "%APPDATA%");
                }

                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                if (!string.IsNullOrWhiteSpace(desktop))
                {
                    result = ReplacePathPrefix(result, desktop, "%DESKTOP%");
                }

                string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (!string.IsNullOrWhiteSpace(documents))
                {
                    result = ReplacePathPrefix(result, documents, "%DOCUMENTS%");
                }
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce(
                    "diagnostics-path-anonymize-failed",
                    "Diagnostics path anonymization failed.",
                    ex);
            }

            return result;
        }

        private static string ReplacePathPrefix(string value, string pathPrefix, string replacement)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                string.IsNullOrWhiteSpace(pathPrefix) ||
                replacement == null)
            {
                return value ?? string.Empty;
            }

            string normalizedPrefix = pathPrefix.TrimEnd(
                System.IO.Path.DirectorySeparatorChar,
                System.IO.Path.AltDirectorySeparatorChar);

            if (string.IsNullOrWhiteSpace(normalizedPrefix))
            {
                return value;
            }

            if (value.Length == normalizedPrefix.Length &&
                string.Equals(value, normalizedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return replacement;
            }

            if (value.Length > normalizedPrefix.Length &&
                value.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                char separator = value[normalizedPrefix.Length];
                if (separator == System.IO.Path.DirectorySeparatorChar ||
                    separator == System.IO.Path.AltDirectorySeparatorChar)
                {
                    return replacement + value.Substring(normalizedPrefix.Length);
                }
            }

            return value;
        }

        private static string AnonymizeDiagnosticsText(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value ?? string.Empty;
            }

            string result = value;

            try
            {
                string[] prefixes = new string[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                };

                string[] replacements = new string[]
                {
                    "%USERPROFILE%",
                    "%LOCALAPPDATA%",
                    "%APPDATA%",
                    "%DESKTOP%",
                    "%DOCUMENTS%"
                };

                for (int i = 0; i < prefixes.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(prefixes[i]))
                    {
                        result = result.Replace(prefixes[i], replacements[i]);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce(
                    "diagnostics-text-anonymize-failed",
                    "Diagnostics text anonymization failed.",
                    ex);
            }

            return result;
        }

        private void ExportDiagnostics(string diagnosticsText)
        {
            try
            {
                using (SaveFileDialog dialog = new SaveFileDialog())
                {
                    dialog.Title = "TrafficView Diagnosebericht speichern";
                    dialog.Filter = "ZIP-Archiv (*.zip)|*.zip|Textdatei (*.txt)|*.txt";
                    dialog.FileName = string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "TrafficView-Diagnose-{0:yyyyMMdd-HHmmss}.zip",
                        DateTime.Now);
                    dialog.OverwritePrompt = true;

                    if (dialog.ShowDialog() != DialogResult.OK)
                    {
                        return;
                    }

                    DialogResult confirmResult = MessageBox.Show(
                        "Die Diagnose-ZIP kann Logs, lokale Pfade und Adapterinformationen enthalten.",
                        "TrafficView",
                        MessageBoxButtons.OKCancel,
                        MessageBoxIcon.Warning);

                    if (confirmResult != DialogResult.OK)
                    {
                        return;
                    }

                    if (string.Equals(Path.GetExtension(dialog.FileName), ".txt", StringComparison.OrdinalIgnoreCase))
                    {
                        File.WriteAllText(dialog.FileName, diagnosticsText, new UTF8Encoding(true));
                    }
                    else
                    {
                        DiagnosticsExport.WriteZip(dialog.FileName, diagnosticsText);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce(
                    "diagnostics-export-failed",
                    "Diagnosebericht konnte nicht gespeichert werden.",
                    ex);
                MessageBox.Show(
                    "Diagnosebericht konnte nicht gespeichert werden.",
                    "TrafficView",
                    MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            }
        }

        private static string CreateMaintenanceSummaryText()
        {
            StringBuilder builder = new StringBuilder();

            builder.AppendLine("Maintenance summary:");
            builder.AppendLine("Capture lifecycle hardening: enabled");
            builder.AppendLine("Capture flag interlocked guards: enabled");
            builder.AppendLine("File retry policy: enabled");
            builder.AppendLine("Log rotation trace retry: enabled");
            builder.AppendLine("Traffic counter reset baseline policy: enabled");
            builder.AppendLine("Automatic adapter hysteresis: enabled");
            builder.AppendLine("Usage append-only flush: enabled");
            builder.AppendLine("Usage size-based maintenance: enabled");
            builder.AppendLine("Settings normalization before save: enabled");
            builder.AppendLine("Diagnostics path anonymization: enabled");
            builder.AppendLine("Calibration useful-threshold validation: enabled");
            builder.AppendLine("Baseline policy self-test diagnostics: enabled");

            return builder.ToString();
        }
    }
}
