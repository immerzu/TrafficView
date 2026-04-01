using System;
using System.Drawing;
using System.IO;
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
        private readonly Func<string, bool> exportUsageData;
        private readonly TableLayoutPanel rootLayout;
        private readonly TableLayoutPanel adapterLayout;
        private readonly TableLayoutPanel usageGrid;
        private readonly Panel adapterRightSpacerPanel;
        private readonly Panel buttonPanel;
        private readonly Control cornerHeaderControl;
        private readonly Label dailyHeaderLabel;
        private readonly Label weeklyHeaderLabel;
        private readonly Label monthlyHeaderLabel;
        private readonly Label uploadRowLabel;
        private readonly Label downloadRowLabel;
        private readonly Label totalRowLabel;
        private readonly Label adapterValueLabel;
        private readonly Label adapterCaptionLabel;
        private readonly Label dailyUploadValueLabel;
        private readonly Label weeklyUploadValueLabel;
        private readonly Label monthlyUploadValueLabel;
        private readonly Label dailyDownloadValueLabel;
        private readonly Label weeklyDownloadValueLabel;
        private readonly Label monthlyDownloadValueLabel;
        private readonly Label dailyTotalValueLabel;
        private readonly Label weeklyTotalValueLabel;
        private readonly Label monthlyTotalValueLabel;
        private readonly Button exportButton;
        private readonly Button clearButton;
        private readonly Button okButton;
        private bool isUpdatingLayout;

        public UsageSummaryForm(
            Func<UsageWindowData> loadUsageWindowData,
            Func<bool> clearUsageData,
            Func<long, string> formatUsageAmount,
            Func<string, bool> exportUsageData)
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

            if (exportUsageData == null)
            {
                throw new ArgumentNullException("exportUsageData");
            }

            this.loadUsageWindowData = loadUsageWindowData;
            this.clearUsageData = clearUsageData;
            this.formatUsageAmount = formatUsageAmount;
            this.exportUsageData = exportUsageData;

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

            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ShowInTaskbar = false;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.Font = baseFont;
            this.TopMost = true;
            this.ClientSize = new Size(570, 295);

            this.rootLayout = new TableLayoutPanel();
            this.rootLayout.Dock = DockStyle.Fill;
            this.rootLayout.Padding = new Padding(10);
            this.rootLayout.ColumnCount = 1;
            this.rootLayout.RowCount = 3;
            this.rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this.rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this.rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));

            this.adapterLayout = new TableLayoutPanel();
            this.adapterLayout.Dock = DockStyle.Top;
            this.adapterLayout.AutoSize = false;
            this.adapterLayout.Height = 34;
            this.adapterLayout.Margin = new Padding(0, 0, 0, 8);
            this.adapterLayout.Padding = new Padding(0);
            this.adapterLayout.ColumnCount = 3;
            this.adapterLayout.RowCount = 1;
            this.adapterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
            this.adapterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.adapterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
            this.adapterLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

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
            this.adapterValueLabel.AutoEllipsis = true;
            this.adapterValueLabel.Font = adapterValueFont;
            this.adapterValueLabel.TextAlign = ContentAlignment.MiddleCenter;

            this.adapterRightSpacerPanel = new Panel();
            this.adapterRightSpacerPanel.Dock = DockStyle.Fill;
            this.adapterRightSpacerPanel.Margin = new Padding(0);

            this.adapterLayout.Controls.Add(this.adapterCaptionLabel, 0, 0);
            this.adapterLayout.Controls.Add(this.adapterValueLabel, 1, 0);
            this.adapterLayout.Controls.Add(this.adapterRightSpacerPanel, 2, 0);

            this.usageGrid = new TableLayoutPanel();
            this.usageGrid.Dock = DockStyle.Top;
            this.usageGrid.AutoSize = false;
            this.usageGrid.ColumnCount = 4;
            this.usageGrid.RowCount = 4;
            this.usageGrid.Width = 530;
            this.usageGrid.Height = 159;
            this.usageGrid.Margin = new Padding(0, 0, 8, 12);
            this.usageGrid.Padding = new Padding(0);
            this.usageGrid.CellBorderStyle = TableLayoutPanelCellBorderStyle.Single;
            this.usageGrid.ColumnStyles.Clear();
            this.usageGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            this.usageGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
            this.usageGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            this.usageGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
            this.usageGrid.RowStyles.Clear();
            this.usageGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            this.usageGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            this.usageGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            this.usageGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            this.cornerHeaderControl = CreateCornerLogoControl();
            this.usageGrid.Controls.Add(this.cornerHeaderControl, 0, 0);
            this.dailyHeaderLabel = AddCell(this.usageGrid, 1, 0, UiLanguage.Get("UsageWindow.ColumnDaily", "Täglich"), headerFont, ContentAlignment.MiddleCenter, new Padding(0));
            this.weeklyHeaderLabel = AddCell(this.usageGrid, 2, 0, UiLanguage.Get("UsageWindow.ColumnWeekly", "Wöchentlich"), headerFont, ContentAlignment.MiddleCenter, new Padding(0));
            this.monthlyHeaderLabel = AddCell(this.usageGrid, 3, 0, UiLanguage.Get("UsageWindow.ColumnMonthly", "Monatlich"), headerFont, ContentAlignment.MiddleCenter, new Padding(0));

            this.uploadRowLabel = AddCell(this.usageGrid, 0, 1, UiLanguage.Get("UsageWindow.RowUpload", "Upload"), rowFont, ContentAlignment.MiddleLeft, new Padding(8, 0, 0, 0));
            this.dailyUploadValueLabel = AddCell(this.usageGrid, 1, 1, string.Empty, valueFont, ContentAlignment.MiddleCenter, new Padding(0));
            this.weeklyUploadValueLabel = AddCell(this.usageGrid, 2, 1, string.Empty, valueFont, ContentAlignment.MiddleCenter, new Padding(0));
            this.monthlyUploadValueLabel = AddCell(this.usageGrid, 3, 1, string.Empty, valueFont, ContentAlignment.MiddleCenter, new Padding(0));

            this.downloadRowLabel = AddCell(this.usageGrid, 0, 2, UiLanguage.Get("UsageWindow.RowDownload", "Download"), rowFont, ContentAlignment.MiddleLeft, new Padding(8, 0, 0, 0));
            this.dailyDownloadValueLabel = AddCell(this.usageGrid, 1, 2, string.Empty, valueFont, ContentAlignment.MiddleCenter, new Padding(0));
            this.weeklyDownloadValueLabel = AddCell(this.usageGrid, 2, 2, string.Empty, valueFont, ContentAlignment.MiddleCenter, new Padding(0));
            this.monthlyDownloadValueLabel = AddCell(this.usageGrid, 3, 2, string.Empty, valueFont, ContentAlignment.MiddleCenter, new Padding(0));

            this.totalRowLabel = AddCell(this.usageGrid, 0, 3, UiLanguage.Get("UsageWindow.RowTotal", "Gesamt"), rowFont, ContentAlignment.MiddleLeft, new Padding(8, 0, 0, 0));
            this.dailyTotalValueLabel = AddCell(this.usageGrid, 1, 3, string.Empty, valueFont, ContentAlignment.MiddleCenter, new Padding(0));
            this.weeklyTotalValueLabel = AddCell(this.usageGrid, 2, 3, string.Empty, valueFont, ContentAlignment.MiddleCenter, new Padding(0));
            this.monthlyTotalValueLabel = AddCell(this.usageGrid, 3, 3, string.Empty, valueFont, ContentAlignment.MiddleCenter, new Padding(0));

            this.buttonPanel = new Panel();
            this.buttonPanel.Dock = DockStyle.Fill;
            this.buttonPanel.Margin = new Padding(0, 12, 0, 0);
            this.buttonPanel.Padding = new Padding(0);

            this.exportButton = new Button();
            this.exportButton.Text = UiLanguage.Get("UsageWindow.ExportCsv", "CSV exportieren...");
            this.exportButton.AutoSize = false;
            this.exportButton.FlatStyle = FlatStyle.System;
            Size exportButtonTextSize = TextRenderer.MeasureText(
                this.exportButton.Text,
                this.Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            int exportButtonWidth = Math.Max(180, exportButtonTextSize.Width + 28);
            this.exportButton.Size = new Size(exportButtonWidth, 36);
            this.exportButton.MinimumSize = new Size(exportButtonWidth, 36);
            this.exportButton.Anchor = AnchorStyles.Left;
            this.exportButton.Location = new Point(0, 2);
            this.exportButton.Click += this.ExportButton_Click;

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
            this.clearButton.Location = new Point(this.exportButton.Right + 8, 2);
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
            this.okButton.Location = new Point(this.buttonPanel.Width - this.okButton.Width, 2);

            this.buttonPanel.Controls.Add(this.exportButton);
            this.buttonPanel.Controls.Add(this.clearButton);
            this.buttonPanel.Controls.Add(this.okButton);
            this.buttonPanel.Resize += delegate
            {
                this.exportButton.Location = new Point(
                    0,
                    Math.Max(0, (this.buttonPanel.ClientSize.Height - this.exportButton.Height) / 2));
                this.clearButton.Location = new Point(
                    this.exportButton.Right + 8,
                    Math.Max(0, (this.buttonPanel.ClientSize.Height - this.clearButton.Height) / 2));
                this.okButton.Location = new Point(
                    Math.Max(0, this.buttonPanel.ClientSize.Width - this.okButton.Width),
                    Math.Max(0, (this.buttonPanel.ClientSize.Height - this.okButton.Height) / 2));
            };

            this.rootLayout.Controls.Add(this.adapterLayout, 0, 0);
            this.rootLayout.Controls.Add(this.usageGrid, 0, 1);
            this.rootLayout.Controls.Add(this.buttonPanel, 0, 2);

            this.AcceptButton = this.okButton;
            this.CancelButton = this.okButton;
            this.Controls.Add(this.rootLayout);
            this.Resize += this.UsageSummaryForm_Resize;
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

        private void ExportButton_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Title = UiLanguage.Get("UsageWindow.ExportDialogTitle", "CSV exportieren");
                saveFileDialog.Filter = UiLanguage.Get(
                    "UsageWindow.ExportDialogFilter",
                    "CSV-Datei (*.csv)|*.csv|Alle Dateien (*.*)|*.*");
                saveFileDialog.DefaultExt = "csv";
                saveFileDialog.AddExtension = true;
                saveFileDialog.OverwritePrompt = true;
                saveFileDialog.InitialDirectory = AppStorage.BaseDirectory;
                saveFileDialog.FileName = this.GetDefaultExportFileName();

                if (saveFileDialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                if (!this.exportUsageData(saveFileDialog.FileName))
                {
                    MessageBox.Show(
                        this,
                        UiLanguage.Get(
                            "UsageWindow.ExportFailed",
                            "Die Verbrauchsdaten konnten nicht exportiert werden."),
                        this.Text,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                MessageBox.Show(
                    this,
                    UiLanguage.Get("UsageWindow.ExportSucceededPrefix", "CSV-Export gespeichert:") +
                        Environment.NewLine +
                        saveFileDialog.FileName,
                    this.Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private void ApplyUsageData(UsageWindowData usageWindowData)
        {
            this.adapterValueLabel.Text = usageWindowData.AdapterDisplayName ?? string.Empty;
            this.dailyUploadValueLabel.Text = this.formatUsageAmount(usageWindowData.DailySummary.UploadBytes);
            this.weeklyUploadValueLabel.Text = this.formatUsageAmount(usageWindowData.WeeklySummary.UploadBytes);
            this.monthlyUploadValueLabel.Text = this.formatUsageAmount(usageWindowData.MonthlySummary.UploadBytes);
            this.dailyDownloadValueLabel.Text = this.formatUsageAmount(usageWindowData.DailySummary.DownloadBytes);
            this.weeklyDownloadValueLabel.Text = this.formatUsageAmount(usageWindowData.WeeklySummary.DownloadBytes);
            this.monthlyDownloadValueLabel.Text = this.formatUsageAmount(usageWindowData.MonthlySummary.DownloadBytes);
            this.dailyTotalValueLabel.Text = this.formatUsageAmount(usageWindowData.DailySummary.TotalBytes);
            this.weeklyTotalValueLabel.Text = this.formatUsageAmount(usageWindowData.WeeklySummary.TotalBytes);
            this.monthlyTotalValueLabel.Text = this.formatUsageAmount(usageWindowData.MonthlySummary.TotalBytes);
            this.UpdateDynamicLayout();
        }

        private void UsageSummaryForm_Resize(object sender, EventArgs e)
        {
            this.UpdateDynamicLayout();
        }

        private void UpdateDynamicLayout()
        {
            if (this.isUpdatingLayout)
            {
                return;
            }

            this.isUpdatingLayout = true;

            try
            {
            int contentPaddingWidth = this.rootLayout.Padding.Horizontal;
            int contentPaddingHeight = this.rootLayout.Padding.Vertical;
            int headerSpacing = this.adapterLayout.Margin.Bottom;
            int buttonSpacing = this.buttonPanel.Margin.Top;
            int buttonRowHeight = Math.Max(
                48,
                Math.Max(Math.Max(this.exportButton.Height, this.clearButton.Height), this.okButton.Height) + 8);

            int exportButtonTextWidth = TextRenderer.MeasureText(
                this.exportButton.Text,
                this.exportButton.Font ?? this.Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width;
            int exportButtonWidth = Math.Max(180, exportButtonTextWidth + 28);
            this.exportButton.Size = new Size(exportButtonWidth, Math.Max(36, this.exportButton.Height));
            this.exportButton.MinimumSize = this.exportButton.Size;

            int clearButtonTextWidth = TextRenderer.MeasureText(
                this.clearButton.Text,
                this.clearButton.Font ?? this.Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width;
            int clearButtonWidth = Math.Max(260, clearButtonTextWidth + 28);
            this.clearButton.Size = new Size(clearButtonWidth, Math.Max(36, this.clearButton.Height));
            this.clearButton.MinimumSize = this.clearButton.Size;

            int rowLabelWidth = Math.Max(
                150,
                Math.Max(
                    MeasureTextWidth(this.uploadRowLabel),
                    Math.Max(
                        MeasureTextWidth(this.downloadRowLabel),
                        MeasureTextWidth(this.totalRowLabel))) + 24);

            int adapterCaptionWidth = MeasureTextWidth(this.adapterCaptionLabel) + 8;
            int adapterValueWidth = MeasureTextWidth(this.adapterValueLabel) + 16;

            int dailyColumnWidth = Math.Max(
                MeasureTextWidth(this.dailyHeaderLabel),
                Math.Max(
                    MeasureTextWidth(this.dailyUploadValueLabel),
                    Math.Max(MeasureTextWidth(this.dailyDownloadValueLabel), MeasureTextWidth(this.dailyTotalValueLabel)))) + 24;
            int weeklyColumnWidth = Math.Max(
                MeasureTextWidth(this.weeklyHeaderLabel),
                Math.Max(
                    MeasureTextWidth(this.weeklyUploadValueLabel),
                    Math.Max(MeasureTextWidth(this.weeklyDownloadValueLabel), MeasureTextWidth(this.weeklyTotalValueLabel)))) + 52;
            int monthlyColumnWidth = Math.Max(
                MeasureTextWidth(this.monthlyHeaderLabel),
                Math.Max(
                    MeasureTextWidth(this.monthlyUploadValueLabel),
                    Math.Max(MeasureTextWidth(this.monthlyDownloadValueLabel), MeasureTextWidth(this.monthlyTotalValueLabel)))) + 28;

            dailyColumnWidth = Math.Max(130, dailyColumnWidth);
            weeklyColumnWidth = Math.Max(164, weeklyColumnWidth);
            monthlyColumnWidth = Math.Max(140, monthlyColumnWidth);

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
            int totalRowHeight = Math.Max(
                MeasureTextHeight(this.totalRowLabel),
                Math.Max(
                    MeasureTextHeight(this.dailyTotalValueLabel),
                    Math.Max(MeasureTextHeight(this.monthlyTotalValueLabel), MeasureTextHeight(this.weeklyTotalValueLabel)))) + 14;

            uploadRowHeight = Math.Max(40, uploadRowHeight);
            downloadRowHeight = Math.Max(40, downloadRowHeight);
            totalRowHeight = Math.Max(40, totalRowHeight);

            int minimumGridWidth = rowLabelWidth + dailyColumnWidth + weeklyColumnWidth + monthlyColumnWidth + 10;

            int mirroredSideWidth = Math.Max(130, adapterCaptionWidth + 18);
            this.adapterLayout.ColumnStyles[0].Width = mirroredSideWidth;
            this.adapterLayout.ColumnStyles[2].Width = mirroredSideWidth;

            int adapterMinimumWidth = (mirroredSideWidth * 2) + Math.Max(adapterValueWidth + 24, 160);
            int buttonsMinimumWidth = this.exportButton.Width + this.clearButton.Width + this.okButton.Width + 64;
            int minimumContentWidth = Math.Max(minimumGridWidth + 12, Math.Max(adapterMinimumWidth, buttonsMinimumWidth));
            int minimumClientWidth = minimumContentWidth + contentPaddingWidth;
            int minimumClientHeight = contentPaddingHeight +
                this.adapterLayout.Height +
                headerSpacing +
                (headerRowHeight + uploadRowHeight + downloadRowHeight + totalRowHeight + 5) +
                buttonSpacing +
                buttonRowHeight;

            this.MinimumSize = this.SizeFromClientSize(new Size(
                Math.Max(570, minimumClientWidth),
                Math.Max(295, minimumClientHeight)));

            int targetClientWidth = Math.Max(this.ClientSize.Width, Math.Max(570, minimumClientWidth));
            int targetClientHeight = Math.Max(this.ClientSize.Height, Math.Max(295, minimumClientHeight));
            if (targetClientWidth != this.ClientSize.Width || targetClientHeight != this.ClientSize.Height)
            {
                this.ClientSize = new Size(targetClientWidth, targetClientHeight);
            }

            int availableGridWidth = Math.Max(
                minimumGridWidth,
                this.ClientSize.Width - contentPaddingWidth - this.usageGrid.Margin.Horizontal);
            int additionalGridWidth = Math.Max(0, availableGridWidth - minimumGridWidth);
            int extraDailyWidth = additionalGridWidth / 3;
            int extraWeeklyWidth = additionalGridWidth / 3;
            int extraMonthlyWidth = additionalGridWidth - extraDailyWidth - extraWeeklyWidth;

            this.usageGrid.SuspendLayout();
            this.usageGrid.ColumnStyles[0].Width = rowLabelWidth;
            this.usageGrid.ColumnStyles[1].Width = dailyColumnWidth + extraDailyWidth;
            this.usageGrid.ColumnStyles[2].Width = weeklyColumnWidth + extraWeeklyWidth;
            this.usageGrid.ColumnStyles[3].Width = monthlyColumnWidth + extraMonthlyWidth;
            this.usageGrid.RowStyles[0].Height = headerRowHeight;
            this.usageGrid.RowStyles[1].Height = uploadRowHeight;
            this.usageGrid.RowStyles[2].Height = downloadRowHeight;
            this.usageGrid.RowStyles[3].Height = totalRowHeight;
            this.usageGrid.Width = availableGridWidth;
            this.usageGrid.Height = headerRowHeight + uploadRowHeight + downloadRowHeight + totalRowHeight + 5;
            this.usageGrid.ResumeLayout();

            this.buttonPanel.PerformLayout();
            }
            finally
            {
                this.isUpdatingLayout = false;
            }
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

        private static Control CreateCornerLogoControl()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.Margin = new Padding(0);
            panel.Padding = new Padding(6, 2, 6, 2);

            Image logoImage = TryLoadCornerLogo();
            if (logoImage == null)
            {
                return panel;
            }

            PictureBox pictureBox = new PictureBox();
            pictureBox.Dock = DockStyle.Fill;
            pictureBox.Margin = new Padding(0);
            pictureBox.BackColor = Color.Transparent;
            pictureBox.Image = logoImage;
            pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            panel.Controls.Add(pictureBox);
            panel.Disposed += delegate
            {
                pictureBox.Image.Dispose();
            };

            return panel;
        }

        private static Image TryLoadCornerLogo()
        {
            try
            {
                string logoPath = Path.Combine(AppStorage.BaseDirectory, "LOLO-SOFT_00_SW.png");
                if (!File.Exists(logoPath))
                {
                    return null;
                }

                using (FileStream stream = new FileStream(logoPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (Image original = Image.FromStream(stream))
                {
                    return new Bitmap(original);
                }
            }
            catch
            {
                return null;
            }
        }

        private string GetDefaultExportFileName()
        {
            string adapterName = this.adapterValueLabel.Text ?? string.Empty;
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                adapterName = adapterName.Replace(invalidChar, '_');
            }

            if (string.IsNullOrWhiteSpace(adapterName))
            {
                adapterName = "Adapter";
            }

            return string.Format(
                "{0}_{1}_{2:yyyy-MM-dd}.csv",
                "Verbrauch",
                adapterName,
                DateTime.Now);
        }
    }
}
