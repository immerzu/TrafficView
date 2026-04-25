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

        private static string CreateDiagnosticsText()
        {
            StringBuilder message = new StringBuilder();
            message.AppendLine("TrafficView");
            message.AppendLine();
            message.AppendLine("Version: " + typeof(Program).Assembly.GetName().Version);
            message.AppendLine("Portable: " + (AppStorage.IsPortableMode ? "yes" : "no"));
            message.AppendLine("Base path: " + AppStorage.BaseDirectory);
            message.AppendLine("Settings path: " + MonitorSettings.GetCurrentSettingsPath());
            message.AppendLine("Usage path: " + TrafficUsageLog.GetUsageFilePath());
            message.AppendLine("Log path: " + AppLog.GetCurrentLogPath());
            message.AppendLine();
            message.AppendLine(AppStorage.CreateStorageDiagnosticsText());
            message.AppendLine();
            message.AppendLine(RuntimeDiagnostics.CreateDiagnosticsText(TrafficPopupForm.CreateTimerDiagnosticsText()));
            return message.ToString();
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
    }
}
