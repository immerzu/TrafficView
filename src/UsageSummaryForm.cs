using System;
using System.Drawing;
using System.Windows.Forms;

namespace TrafficView
{
    internal sealed class UsageWindowData
    {
        public UsageWindowData(
            string adapterDisplayName,
            TrafficUsageSummary dailySummary,
            TrafficUsageSummary monthlySummary,
            TrafficUsageSummary weeklySummary)
        {
            this.AdapterDisplayName = adapterDisplayName ?? string.Empty;
            this.DailySummary = dailySummary ?? new TrafficUsageSummary();
            this.MonthlySummary = monthlySummary ?? new TrafficUsageSummary();
            this.WeeklySummary = weeklySummary ?? new TrafficUsageSummary();
        }

        public string AdapterDisplayName { get; private set; }

        public TrafficUsageSummary DailySummary { get; private set; }

        public TrafficUsageSummary MonthlySummary { get; private set; }

        public TrafficUsageSummary WeeklySummary { get; private set; }
    }

    internal sealed class UsageSummaryForm : Form
    {
        private readonly Func<UsageWindowData> loadUsageWindowData;
        private readonly Func<bool> clearUsageData;
        private readonly Func<long, string> formatUsageAmount;
        private readonly Label adapterValueLabel;
        private readonly Label dailyUploadValueLabel;
        private readonly Label monthlyUploadValueLabel;
        private readonly Label weeklyUploadValueLabel;
        private readonly Label dailyDownloadValueLabel;
        private readonly Label monthlyDownloadValueLabel;
        private readonly Label weeklyDownloadValueLabel;

        public UsageSummaryForm(
            Func<UsageWindowData> loadUsageWindowData,
            Func<bool> clearUsageData,
            Func<long, string> formatUsageAmount)
        {
            if (loadUsageWindowData == null)
            {
                throw new ArgumentNullException("loadUsageWindowData");
            }

            if (clearUsageData == null)
            {
                throw new ArgumentNullException("clearUsageData");
            }

            if (formatUsageAmount == null)
            {
                throw new ArgumentNullException("formatUsageAmount");
            }

            this.loadUsageWindowData = loadUsageWindowData;
            this.clearUsageData = clearUsageData;
            this.formatUsageAmount = formatUsageAmount;

            UsageWindowData usageWindowData = this.loadUsageWindowData();
            if (usageWindowData == null)
            {
                throw new InvalidOperationException("loadUsageWindowData returned null.");
            }

            this.Text = UiLanguage.Get("UsageWindow.Title", "Datenverbrauch");
            Font baseFont = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            Font interfaceFont = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            Font adapterValueFont = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            Font headerFont = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            Font rowFont = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            Font valueFont = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);

            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.Font = baseFont;
            this.TopMost = true;
            this.ClientSize = new Size(570, 295);

            TableLayoutPanel rootLayout = new TableLayoutPanel();
            rootLayout.Dock = DockStyle.Fill;
            rootLayout.Padding = new Padding(10);
            rootLayout.ColumnCount = 1;
            rootLayout.RowCount = 3;
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));

            TableLayoutPanel adapterLayout = new TableLayoutPanel();
            adapterLayout.Dock = DockStyle.Top;
            adapterLayout.AutoSize = false;
            adapterLayout.Height = 32;
            adapterLayout.Margin = new Padding(0, 0, 0, 8);
            adapterLayout.Padding = new Padding(0);
            adapterLayout.ColumnCount = 3;
            adapterLayout.RowCount = 1;
            adapterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
            adapterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            adapterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
            adapterLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            Label adapterCaptionLabel = new Label();
            adapterCaptionLabel.Dock = DockStyle.Fill;
            adapterCaptionLabel.AutoSize = false;
            adapterCaptionLabel.Margin = new Padding(0);
            adapterCaptionLabel.Text = UiLanguage.Get("UsageWindow.AdapterCaption", "Schnittstelle:");
            adapterCaptionLabel.Font = interfaceFont;
            adapterCaptionLabel.TextAlign = ContentAlignment.MiddleLeft;

            this.adapterValueLabel = new Label();
            this.adapterValueLabel.Dock = DockStyle.Fill;
            this.adapterValueLabel.AutoSize = false;
            this.adapterValueLabel.Margin = new Padding(0);
            this.adapterValueLabel.Font = adapterValueFont;
            this.adapterValueLabel.TextAlign = ContentAlignment.MiddleCenter;

            adapterLayout.Controls.Add(adapterCaptionLabel, 0, 0);
            adapterLayout.Controls.Add(this.adapterValueLabel, 1, 0);
            adapterLayout.Controls.Add(new Panel() { Dock = DockStyle.Fill, Margin = new Padding(0) }, 2, 0);

            TableLayoutPanel grid = new TableLayoutPanel();
            grid.Dock = DockStyle.Top;
            grid.AutoSize = false;
            grid.ColumnCount = 4;
            grid.RowCount = 3;
            grid.Width = 525;
            grid.Height = 116;
            grid.Margin = new Padding(0, 0, 0, 12);
            grid.Padding = new Padding(0);
            grid.CellBorderStyle = TableLayoutPanelCellBorderStyle.Single;
            grid.ColumnStyles.Clear();
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125F));
            grid.RowStyles.Clear();
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            AddCell(grid, 0, 0, string.Empty, headerFont, ContentAlignment.MiddleCenter, new Padding(0));
            AddCell(grid, 1, 0, UiLanguage.Get("UsageWindow.ColumnDaily", "Täglich"), headerFont, ContentAlignment.MiddleCenter, new Padding(0));
            AddCell(grid, 2, 0, UiLanguage.Get("UsageWindow.ColumnMonthly", "Monatlich"), headerFont, ContentAlignment.MiddleCenter, new Padding(0));
            AddCell(grid, 3, 0, UiLanguage.Get("UsageWindow.ColumnWeekly", "Wöchentlich"), headerFont, ContentAlignment.MiddleCenter, new Padding(0));

            AddCell(grid, 0, 1, UiLanguage.Get("UsageWindow.RowUpload", "Upload"), rowFont, ContentAlignment.MiddleLeft, new Padding(8, 0, 0, 0));
            this.dailyUploadValueLabel = AddCell(grid, 1, 1, string.Empty, valueFont, ContentAlignment.MiddleCenter, new Padding(0));
            this.monthlyUploadValueLabel = AddCell(grid, 2, 1, string.Empty, valueFont, ContentAlignment.MiddleCenter, new Padding(0));
            this.weeklyUploadValueLabel = AddCell(grid, 3, 1, string.Empty, valueFont, ContentAlignment.MiddleCenter, new Padding(0));

            AddCell(grid, 0, 2, UiLanguage.Get("UsageWindow.RowDownload", "Download"), rowFont, ContentAlignment.MiddleLeft, new Padding(8, 0, 0, 0));
            this.dailyDownloadValueLabel = AddCell(grid, 1, 2, string.Empty, valueFont, ContentAlignment.MiddleCenter, new Padding(0));
            this.monthlyDownloadValueLabel = AddCell(grid, 2, 2, string.Empty, valueFont, ContentAlignment.MiddleCenter, new Padding(0));
            this.weeklyDownloadValueLabel = AddCell(grid, 3, 2, string.Empty, valueFont, ContentAlignment.MiddleCenter, new Padding(0));

            Panel buttonPanel = new Panel();
            buttonPanel.Dock = DockStyle.Fill;
            buttonPanel.Margin = new Padding(0, 12, 0, 0);
            buttonPanel.Padding = new Padding(0);

            Button clearButton = new Button();
            clearButton.Text = UiLanguage.Get("UsageWindow.ClearAll", "Datenverbrauch löschen");
            clearButton.AutoSize = false;
            clearButton.FlatStyle = FlatStyle.System;
            Size clearButtonTextSize = TextRenderer.MeasureText(
                clearButton.Text,
                this.Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            int clearButtonWidth = Math.Max(260, clearButtonTextSize.Width + 28);
            clearButton.Size = new Size(clearButtonWidth, 36);
            clearButton.MinimumSize = new Size(clearButtonWidth, 36);
            clearButton.Anchor = AnchorStyles.Left;
            clearButton.Location = new Point(0, 2);
            clearButton.Click += this.ClearButton_Click;

            Button okButton = new Button();
            okButton.Text = UiLanguage.Get("Common.Ok", "OK");
            okButton.DialogResult = DialogResult.OK;
            okButton.Font = this.Font;
            okButton.AutoSize = false;
            okButton.FlatStyle = FlatStyle.System;
            okButton.Size = new Size(92, 32);
            okButton.MinimumSize = new Size(92, 32);
            okButton.UseVisualStyleBackColor = true;
            okButton.TextAlign = ContentAlignment.MiddleCenter;
            okButton.Padding = Padding.Empty;
            okButton.Margin = Padding.Empty;
            okButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            okButton.Location = new Point(buttonPanel.Width - okButton.Width, 2);

            buttonPanel.Controls.Add(clearButton);
            buttonPanel.Controls.Add(okButton);
            buttonPanel.Resize += delegate
            {
                clearButton.Location = new Point(
                    0,
                    Math.Max(0, (buttonPanel.ClientSize.Height - clearButton.Height) / 2));
                okButton.Location = new Point(
                    Math.Max(0, buttonPanel.ClientSize.Width - okButton.Width),
                    Math.Max(0, (buttonPanel.ClientSize.Height - okButton.Height) / 2));
            };

            rootLayout.Controls.Add(adapterLayout, 0, 0);
            rootLayout.Controls.Add(grid, 0, 1);
            rootLayout.Controls.Add(buttonPanel, 0, 2);

            this.AcceptButton = okButton;
            this.CancelButton = okButton;
            this.Controls.Add(rootLayout);
            this.ApplyUsageData(usageWindowData);
        }

        private void ClearButton_Click(object sender, EventArgs e)
        {
            if (!this.clearUsageData())
            {
                MessageBox.Show(
                    this,
                    UiLanguage.Get(
                        "UsageWindow.ClearFailed",
                        "Die aufgezeichneten Verbrauchsdaten konnten nicht gelöscht werden."),
                    this.Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            UsageWindowData usageWindowData = this.loadUsageWindowData();
            if (usageWindowData != null)
            {
                this.ApplyUsageData(usageWindowData);
            }
        }

        private void ApplyUsageData(UsageWindowData usageWindowData)
        {
            this.adapterValueLabel.Text = usageWindowData.AdapterDisplayName ?? string.Empty;
            this.dailyUploadValueLabel.Text = this.formatUsageAmount(usageWindowData.DailySummary.UploadBytes);
            this.monthlyUploadValueLabel.Text = this.formatUsageAmount(usageWindowData.MonthlySummary.UploadBytes);
            this.weeklyUploadValueLabel.Text = this.formatUsageAmount(usageWindowData.WeeklySummary.UploadBytes);
            this.dailyDownloadValueLabel.Text = this.formatUsageAmount(usageWindowData.DailySummary.DownloadBytes);
            this.monthlyDownloadValueLabel.Text = this.formatUsageAmount(usageWindowData.MonthlySummary.DownloadBytes);
            this.weeklyDownloadValueLabel.Text = this.formatUsageAmount(usageWindowData.WeeklySummary.DownloadBytes);
        }

        private static Label AddCell(
            TableLayoutPanel grid,
            int column,
            int row,
            string text,
            Font font,
            ContentAlignment textAlign,
            Padding padding)
        {
            Label label = new Label();
            label.Dock = DockStyle.Fill;
            label.AutoSize = false;
            label.AutoEllipsis = false;
            label.MinimumSize = new Size(0, 34);
            label.Margin = new Padding(0);
            label.Padding = padding;
            label.Text = text ?? string.Empty;
            label.TextAlign = textAlign;
            if (font != null)
            {
                label.Font = font;
            }

            grid.Controls.Add(label, column, row);
            return label;
        }
    }
}
