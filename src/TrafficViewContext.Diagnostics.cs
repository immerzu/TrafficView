using System;
using System.Text;
using System.Windows.Forms;

namespace TrafficView
{
    internal sealed partial class TrafficViewContext
    {
        private void AboutItem_Click(object sender, EventArgs e)
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

            MessageBox.Show(
                message.ToString(),
                UiLanguage.Get("Menu.About", "Über TrafficView"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}
