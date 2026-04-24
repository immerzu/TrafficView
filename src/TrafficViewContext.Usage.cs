using System;
using System.Collections.Generic;
using System.IO;

namespace TrafficView
{
    internal sealed partial class TrafficViewContext
    {
        private UsageWindowData CreateUsageWindowData()
        {
            if (this.trafficUsageLog.PendingRecordCount > 0 &&
                this.trafficUsageLog.FlushPending())
            {
                this.lastTrafficUsageFlushUtc = DateTime.UtcNow;
            }

            TrafficUsageSummaries summaries = this.trafficUsageLog.GetSummaries(this.settings);

            return new UsageWindowData(
                this.settings.GetAdapterDisplayName(),
                summaries.Daily,
                summaries.Monthly,
                summaries.Weekly);
        }

        private bool ClearUsageData()
        {
            bool cleared = this.trafficUsageLog.ClearAll();
            if (cleared)
            {
                this.lastTrafficUsageFlushUtc = DateTime.UtcNow;
            }

            return cleared;
        }

        private bool ExportUsageData(string targetPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return false;
            }

            try
            {
                UsageWindowData usageWindowData = this.CreateUsageWindowData();
                DateTime exportTimestampLocal = DateTime.Now;
                List<string> csvLines = new List<string>();
                string adapterDisplayName = usageWindowData.AdapterDisplayName ?? string.Empty;

                csvLines.Add(string.Join(";",
                    TrafficUsageFormatter.EscapeCsvValue(UiLanguage.Get("UsageWindow.AdapterCaption", "Internetverbindung:").TrimEnd(':')),
                    TrafficUsageFormatter.EscapeCsvValue(adapterDisplayName)));
                csvLines.Add(string.Empty);
                csvLines.Add(string.Join(";",
                    string.Empty,
                    TrafficUsageFormatter.EscapeCsvValue(UiLanguage.Get("UsageWindow.ColumnDaily", "Täglich")),
                    TrafficUsageFormatter.EscapeCsvValue(UiLanguage.Get("UsageWindow.ColumnWeekly", "Wöchentlich")),
                    TrafficUsageFormatter.EscapeCsvValue(UiLanguage.Get("UsageWindow.ColumnMonthly", "Monatlich"))));
                csvLines.Add(string.Join(";",
                    TrafficUsageFormatter.EscapeCsvValue(UiLanguage.Get("UsageWindow.RowPeriodStart", "Beginn")),
                    TrafficUsageFormatter.EscapeCsvValue(TrafficUsageFormatter.FormatPeriodStart(exportTimestampLocal, TrafficUsagePeriod.Daily)),
                    TrafficUsageFormatter.EscapeCsvValue(TrafficUsageFormatter.FormatPeriodStart(exportTimestampLocal, TrafficUsagePeriod.Weekly)),
                    TrafficUsageFormatter.EscapeCsvValue(TrafficUsageFormatter.FormatPeriodStart(exportTimestampLocal, TrafficUsagePeriod.Monthly))));
                csvLines.Add(string.Join(";",
                    TrafficUsageFormatter.EscapeCsvValue(UiLanguage.Get("UsageWindow.RowPeriodEnd", "Ende")),
                    TrafficUsageFormatter.EscapeCsvValue(TrafficUsageFormatter.FormatPeriodEnd(exportTimestampLocal)),
                    TrafficUsageFormatter.EscapeCsvValue(TrafficUsageFormatter.FormatPeriodEnd(exportTimestampLocal)),
                    TrafficUsageFormatter.EscapeCsvValue(TrafficUsageFormatter.FormatPeriodEnd(exportTimestampLocal))));
                csvLines.Add(string.Join(";",
                    TrafficUsageFormatter.EscapeCsvValue(UiLanguage.Get("UsageWindow.RowUpload", "Upload")),
                    TrafficUsageFormatter.EscapeCsvValue(TrafficUsageFormatter.FormatAmount(usageWindowData.DailySummary.UploadBytes)),
                    TrafficUsageFormatter.EscapeCsvValue(TrafficUsageFormatter.FormatAmount(usageWindowData.WeeklySummary.UploadBytes)),
                    TrafficUsageFormatter.EscapeCsvValue(TrafficUsageFormatter.FormatAmount(usageWindowData.MonthlySummary.UploadBytes))));
                csvLines.Add(string.Join(";",
                    TrafficUsageFormatter.EscapeCsvValue(UiLanguage.Get("UsageWindow.RowDownload", "Download")),
                    TrafficUsageFormatter.EscapeCsvValue(TrafficUsageFormatter.FormatAmount(usageWindowData.DailySummary.DownloadBytes)),
                    TrafficUsageFormatter.EscapeCsvValue(TrafficUsageFormatter.FormatAmount(usageWindowData.WeeklySummary.DownloadBytes)),
                    TrafficUsageFormatter.EscapeCsvValue(TrafficUsageFormatter.FormatAmount(usageWindowData.MonthlySummary.DownloadBytes))));
                csvLines.Add(string.Join(";",
                    TrafficUsageFormatter.EscapeCsvValue(UiLanguage.Get("UsageWindow.RowTotal", "Gesamt")),
                    TrafficUsageFormatter.EscapeCsvValue(TrafficUsageFormatter.FormatAmount(usageWindowData.DailySummary.TotalBytes)),
                    TrafficUsageFormatter.EscapeCsvValue(TrafficUsageFormatter.FormatAmount(usageWindowData.WeeklySummary.TotalBytes)),
                    TrafficUsageFormatter.EscapeCsvValue(TrafficUsageFormatter.FormatAmount(usageWindowData.MonthlySummary.TotalBytes))));

                string directoryPath = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                using (StreamWriter writer = new StreamWriter(targetPath, false, new System.Text.UTF8Encoding(true)))
                {
                    for (int i = 0; i < csvLines.Count; i++)
                    {
                        writer.WriteLine(csvLines[i]);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce(
                    "traffic-usage-window-export-failed",
                    string.Format("Verbrauchsdaten konnten nicht nach '{0}' exportiert werden.", targetPath),
                    ex);
                return false;
            }
        }

        private void ShowUsageWindow()
        {
            bool pausePopupTopMost = this.popupForm.Visible;
            if (pausePopupTopMost)
            {
                this.popupForm.SuspendTopMostEnforcement();
            }

            try
            {
                using (UsageSummaryForm usageSummaryForm = new UsageSummaryForm(
                    new Func<UsageWindowData>(this.CreateUsageWindowData),
                    new Func<bool>(this.ClearUsageData),
                    new Func<long, string>(TrafficUsageFormatter.FormatAmount),
                    new Func<string, bool>(this.ExportUsageData)))
                {
                    if (this.popupForm.Visible)
                    {
                        usageSummaryForm.ShowDialog(this.popupForm);
                    }
                    else
                    {
                        usageSummaryForm.ShowDialog();
                    }
                }
            }
            finally
            {
                if (pausePopupTopMost)
                {
                    this.popupForm.ResumeTopMostEnforcement(false);
                }
            }
        }
    }
}
