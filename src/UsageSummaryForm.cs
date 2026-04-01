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
        private readonly TableLayoutPanel usageGrid;
        private readonly Label dailyHeaderLabel;
        private readonly Label monthlyHeaderLabel;
        private readonly Label weeklyHeaderLabel;
        private readonly Label uploadRowLabel;
        private readonly Label downloadRowLabel;
        private readonly Label adapterValueLabel;
        private readonly Label adapterCaptionLabel;
        private readonly Label dailyUploadValueLabel;
        private readonly Label monthlyUploadValueLabel;
        private readonly Label weeklyUploadValueLabel;
        private readonly Label dailyDownloadValueLabel;
        private readonly Label monthlyDownloadValueLabel;
        private readonly Label weeklyDownloadValueLabel;
        private readonly Button clearButton;
        private readonly Button okButton;

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
            this.StartPosition = FormStartPosition.CenterScreen;
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

            this.adapterCaptionLabel = new Label();
            this.adapterCaptionLabel.Dock = DockStyle.Fill;
            this.adapterCaptionLabel.AutoSize = false;
            this.adapterCaptionLabel.Margin = new Padding(0);
            this.adapterCaptionLabel.Text = UiLanguage.Get("UsageWindow.AdapterCaption", "Schnittstelle:");
            this.adapterCaptionLabel.Font = interfaceFont;
            this.adapterCaptionLabel.TextAlign = ContentAlignment.MiddleLeft;

            this.adapterValueLabel = new Label();
            this.adapterValueLabel.Dock = DockStyle.Fill;
            this.adapterValueLabel.AutoSize = false;
            this.adapterValueLabel.Margin = new Padding(0);
            this.adapterValueLabel.Font = adapterValueFont;
            this.adapterValueLabel.TextAlign = ContentAlignment.MiddleCenter;

            adapterLayout.Controls.Add(this.adapterCaptionLabel, 0, 0);
            adapterLayout.Controls.Add(this.adapterValueLabel, 1, 0);
            adapterLayout.Controls.Add(new Panel() { Dock = DockStyle.Fill, Margin = new Padding(0) }, 2, 0);

            this.usageGrid = new TableLayoutPanel();
            this.usageGrid.Dock = DockStyle.Top;
            this.usageGrid.AutoSize = false;
            this.usageGrid.ColumnCount = 4;
            this.usageGrid.RowCount = 3;
            this.usageGrid.Width = 530;
            this.usageGrid.Height = 118;
            this.usageGrid.Margin = new Padding(0, 0, 0, 12);
            this.usageGrid.Padding = new Padding(0);
            this.usageGrid.CellBorderStyle = TableLayoutPanelCellBorderStyle.Single;
            this.usageGrid.ColumnStyles.Clear();
            this.usageGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            this.usageGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125F));
            this.usageGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125F));
            this.usageGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125F));
            this.usageGrid.RowStyles.Clear();
            this.usageGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            this.usageGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            this.usageGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            AddCell(this.usageGrid, 0, 0, string.Empty, headerFont, ContentAlignment.MiddleCenter, new Padding(0));
            this.dailyHeaderLabel = AddCell(this.usageGrid, 1, 0, UiLanguage.Get("UsageWindow.ColumnDaily", "Täglich"), headerFont, ContentAlignment.MiddleCenter, new Padding(0));
            this.monthlyHeaderLabel = AddCell(this.usageGrid, 2, 0, UiLanguage.Get("UsageWindow.ColumnMonthly", "Monatlich"), headerFont, ContentAlignment.MiddleCenter, new Padding(0));
            this.weeklyHeaderLabel = AddCell(this.usageGrid, 3, 0, UiLanguage.Get("UsageWindow.ColumnWeekly", "Wöchentlich"), headerFont, ContentAlignment.MiddleCenter, new Padding(0));

            this.uploadRowLabel = AddCell(this.usageGrid, 0, 1, UiLanguage.Get("UsageWindow.RowUpload", "Upload"), rowFont, ContentAlignment.MiddleLeft, new Padding(8, 0, 0, 0));
            this.dailyUploadValueLabel = AddCell(this.usageGrid, 1, 1, string.Empty, valueFont, ContentAlignment.MiddleCenter, new Padding(0));
            this.monthlyUploadValueLabel = AddCell(this.usageGrid, 2, 1, string.Empty, valueFont, ContentAlignment.MiddleCenter, new Padding(0));
            this.weeklyUploadValueLabel = AddCell(this.usageGrid, 3, 1, string.Empty, valueFont, ContentAlignment.MiddleCenter, new Padding(0));

            this.downloadRowLabel = AddCell(this.usageGrid, 0, 2, UiLanguage.Get("UsageWindow.RowDownload", "Download"), rowFont, ContentAlignment.MiddleLeft, new Padding(8, 0, 0, 0));
            this.dailyDownloadValueLabel = AddCell(this.usageGrid, 1, 2, string.Empty, valueFont, ContentAlignment.MiddleCenter, new Padding(0));
            this.monthlyDownloadValueLabel = AddCell(this.usageGrid, 2, 2, string.Empty, valueFont, ContentAlignment.MiddleCenter, new Padding(0));
            this.weeklyDownloadValueLabel = AddCell(this.usageGrid, 3, 2, string.Empty, valueFont, ContentAlignment.MiddleCenter, new Padding(0));

            Panel buttonPanel = new Panel();
            buttonPanel.Dock = DockStyle.Fill;
            buttonPanel.Margin = new Padding(0, 12, 0, 0);
            buttonPanel.Padding = new Padding(0);

            this.clearButton = new Button();
            this.clearButton.Text = UiLanguage.Get("UsageWindow.ClearAll", "Datenverbrauch löschen");
            this.clearButton.AutoSize = false;
            this.clearButton.FlatStyle = FlatStyle.System;
            Size clearButtonTextSize = TextRenderer.MeasureText(
                this.clearButton.Text,
                this.Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            int clearButtonWidth = Math.Max(260, clearButtonTextSize.Width + 28);
            this.clearButton.Size = new Size(clearButtonWidth, 36);
            this.clearButton.MinimumSize = new Size(clearButtonWidth, 36);
            this.clearButton.Anchor = AnchorStyles.Left;
            this.clearButton.Location = new Point(0, 2);
            this.clearButton.Click += this.ClearButton_Click;

            this.okButton = new Button();
            this.okButton.Text = UiLanguage.Get("Common.Ok", "OK");
            this.okButton.DialogResult = DialogResult.OK;
            this.okButton.Font = this.Font;
            this.okButton.AutoSize = false;
            this.okButton.FlatStyle = FlatStyle.System;
            this.okButton.Size = new Size(92, 32);
            this.okButton.MinimumSize = new Size(92, 32);
            this.okButton.UseVisualStyleBackColor = true;
            this.okButton.TextAlign = ContentAlignment.MiddleCenter;
            this.okButton.Padding = Padding.Empty;
            this.okButton.Margin = Padding.Empty;
            this.okButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.okButton.Location = new Point(buttonPanel.Width - this.okButton.Width, 2);

            buttonPanel.Controls.Add(this.clearButton);
            buttonPanel.Controls.Add(this.okButton);
            buttonPanel.Resize += delegate
            {
                this.clearButton.Location = new Point(
                    0,
                    Math.Max(0, (buttonPanel.ClientSize.Height - this.clearButton.Height) / 2));
                this.okButton.Location = new Point(
                    Math.Max(0, buttonPanel.ClientSize.Width - this.okButton.Width),
                    Math.Max(0, (buttonPanel.ClientSize.Height - this.okButton.Height) / 2));
            };

            rootLayout.Controls.Add(adapterLayout, 0, 0);
            rootLayout.Controls.Add(this.usageGrid, 0, 1);
            rootLayout.Controls.Add(buttonPanel, 0, 2);

            this.AcceptButton = this.okButton;
            this.CancelButton = this.okButton;
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
            this.UpdateDynamicLayout();
        }

        private void UpdateDynamicLayout()
        {
            int rowLabelWidth = Math.Max(
                150,
                Math.Max(
                    MeasureTextWidth(this.uploadRowLabel),
                    MeasureTextWidth(this.downloadRowLabel)) + 24);

            int adapterCaptionWidth = MeasureTextWidth(this.adapterCaptionLabel) + 8;
            int adapterValueWidth = MeasureTextWidth(this.adapterValueLabel) + 16;

            int dailyColumnWidth = Math.Max(
                MeasureTextWidth(this.dailyHeaderLabel),
                Math.Max(MeasureTextWidth(this.dailyUploadValueLabel), MeasureTextWidth(this.dailyDownloadValueLabel))) + 24;
            int monthlyColumnWidth = Math.Max(
                MeasureTextWidth(this.monthlyHeaderLabel),
                Math.Max(MeasureTextWidth(this.monthlyUploadValueLabel), MeasureTextWidth(this.monthlyDownloadValueLabel))) + 24;
            int weeklyColumnWidth = Math.Max(
                MeasureTextWidth(this.weeklyHeaderLabel),
                Math.Max(MeasureTextWidth(this.weeklyUploadValueLabel), MeasureTextWidth(this.weeklyDownloadValueLabel))) + 24;

            dailyColumnWidth = Math.Max(125, dailyColumnWidth);
            monthlyColumnWidth = Math.Max(125, monthlyColumnWidth);
            weeklyColumnWidth = Math.Max(125, weeklyColumnWidth);

            int headerRowHeight = Math.Max(34, Math.Max(
                MeasureTextHeight(this.dailyHeaderLabel),
                Math.Max(MeasureTextHeight(this.monthlyHeaderLabel), MeasureTextHeight(this.weeklyHeaderLabel))) + 12);
            int uploadRowHeight = Math.Max(
                MeasureTextHeight(this.uploadRowLabel),
                Math.Max(
                    MeasureTextHeight(this.dailyUploadValueLabel),
                    Math.Max(MeasureTextHeight(this.monthlyUploadValueLabel), MeasureTextHeight(this.weeklyUploadValueLabel)))) + 14;
            int downloadRowHeight = Math.Max(
                MeasureTextHeight(this.downloadRowLabel),
                Math.Max(
                    MeasureTextHeight(this.dailyDownloadValueLabel),
                    Math.Max(MeasureTextHeight(this.monthlyDownloadValueLabel), MeasureTextHeight(this.weeklyDownloadValueLabel)))) + 14;

            uploadRowHeight = Math.Max(40, uploadRowHeight);
            downloadRowHeight = Math.Max(40, downloadRowHeight);

            this.usageGrid.SuspendLayout();
            this.usageGrid.ColumnStyles[0].Width = rowLabelWidth;
            this.usageGrid.ColumnStyles[1].Width = dailyColumnWidth;
            this.usageGrid.ColumnStyles[2].Width = monthlyColumnWidth;
            this.usageGrid.ColumnStyles[3].Width = weeklyColumnWidth;
            this.usageGrid.RowStyles[0].Height = headerRowHeight;
            this.usageGrid.RowStyles[1].Height = uploadRowHeight;
            this.usageGrid.RowStyles[2].Height = downloadRowHeight;
            this.usageGrid.Width = rowLabelWidth + dailyColumnWidth + monthlyColumnWidth + weeklyColumnWidth + 5;
            this.usageGrid.Height = headerRowHeight + uploadRowHeight + downloadRowHeight + 4;
            this.usageGrid.ResumeLayout();

            int adapterMinimumWidth = adapterCaptionWidth + Math.Max(adapterValueWidth, 140) + 150;
            int buttonsMinimumWidth = this.clearButton.Width + this.okButton.Width + 56;
            int minimumClientWidth = Math.Max(this.usageGrid.Width + 40, Math.Max(adapterMinimumWidth, buttonsMinimumWidth));
            int minimumClientHeight = this.usageGrid.Height + 177;
            this.ClientSize = new Size(
                Math.Max(570, minimumClientWidth),
                Math.Max(295, minimumClientHeight));
        }

        private static int MeasureTextWidth(Label label)
        {
            return TextRenderer.MeasureText(
                label.Text ?? string.Empty,
                label.Font ?? Control.DefaultFont,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width;
        }

        private static int MeasureTextHeight(Label label)
        {
            return TextRenderer.MeasureText(
                label.Text ?? string.Empty,
                label.Font ?? Control.DefaultFont,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Height;
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
