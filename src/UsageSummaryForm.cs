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
        private readonly int buttonMinHeight;
        private readonly int buttonRowMinHeight;
        private readonly int buttonVerticalInset;
        private readonly int buttonHorizontalInset;
        private readonly int buttonContentHorizontalPadding;
        private readonly int buttonContentVerticalPadding;
        private readonly int buttonSpacing;
        private readonly int exportButtonMinimumWidth;
        private readonly int clearButtonMinimumWidth;
        private readonly int okButtonMinimumWidth;
        private readonly int baseClientWidth;
        private readonly int baseClientHeight;
        private readonly int rowLabelMinimumWidth;
        private readonly int dailyColumnMinimumWidth;
        private readonly int weeklyColumnMinimumWidth;
        private readonly int monthlyColumnMinimumWidth;
        private readonly int mirroredSideMinimumWidth;
        private readonly int rowLabelTextHorizontalPadding;
        private readonly int dailyColumnTextHorizontalPadding;
        private readonly int weeklyColumnTextHorizontalPadding;
        private readonly int monthlyColumnTextHorizontalPadding;
        private readonly int adapterCaptionTextHorizontalPadding;
        private readonly int adapterValueTextHorizontalPadding;
        private readonly int adapterValueMinimumWidth;
        private readonly int adapterRowTextVerticalPadding;
        private readonly int buttonTextHorizontalPadding;
        private readonly int headerRowTextVerticalPadding;
        private readonly int dataRowTextVerticalPadding;
        private readonly int contentHorizontalPadding;
        private readonly int gridRightSpacing;
        private readonly int gridWidthReserve;
        private readonly int gridHeightReserve;
        private readonly int contentWidthReserve;
        private readonly int upperSectionHeightReserve;
        private readonly int minimumClientWidth;
        private readonly int minimumClientHeight;
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
            int sectionSpacing = Math.Max(10, this.Font.Height - 2);
            int compactSpacing = Math.Max(8, this.Font.Height / 2);
            int buttonSpacing = Math.Max(8, compactSpacing);
            int rowLabelLeftPadding = Math.Max(8, compactSpacing * 2);
            int cornerLogoHorizontalPadding = Math.Max(6, compactSpacing + 2);
            int cornerLogoVerticalPadding = Math.Max(2, compactSpacing / 2);
            this.rowLabelMinimumWidth = Math.Max(150, (this.Font.Height * 6) + 18);
            this.dailyColumnMinimumWidth = Math.Max(130, (this.Font.Height * 5) + 24);
            this.weeklyColumnMinimumWidth = Math.Max(164, (this.Font.Height * 6) + 32);
            this.monthlyColumnMinimumWidth = Math.Max(140, (this.Font.Height * 5) + 28);
            this.mirroredSideMinimumWidth = Math.Max(130, (this.Font.Height * 5) + 18);
            this.rowLabelTextHorizontalPadding = Math.Max(24, compactSpacing * 6);
            this.dailyColumnTextHorizontalPadding = Math.Max(24, compactSpacing * 6);
            this.weeklyColumnTextHorizontalPadding = Math.Max(52, compactSpacing * 10);
            this.monthlyColumnTextHorizontalPadding = Math.Max(28, compactSpacing * 6);
            this.adapterCaptionTextHorizontalPadding = Math.Max(18, compactSpacing * 4);
            this.adapterValueTextHorizontalPadding = Math.Max(24, compactSpacing * 6);
            this.adapterValueMinimumWidth = Math.Max(160, (this.Font.Height * 7) + 48);
            this.adapterRowTextVerticalPadding = Math.Max(10, compactSpacing * 2 + 2);
            this.buttonContentHorizontalPadding = Math.Max(10, compactSpacing + 4);
            this.buttonContentVerticalPadding = Math.Max(4, (compactSpacing / 2) + 1);
            this.buttonTextHorizontalPadding = Math.Max(28, (this.buttonContentHorizontalPadding * 2) + (compactSpacing * 3));
            this.headerRowTextVerticalPadding = Math.Max(12, compactSpacing * 3);
            this.dataRowTextVerticalPadding = Math.Max(14, (compactSpacing * 3) + 2);
            this.contentHorizontalPadding = Math.Max(10, compactSpacing + 2);
            this.gridRightSpacing = Math.Max(8, compactSpacing);
            this.gridWidthReserve = Math.Max(10, compactSpacing + 2);
            this.gridHeightReserve = Math.Max(5, Math.Max(1, compactSpacing / 2) + 1);
            this.contentWidthReserve = Math.Max(12, compactSpacing + 4);
            int baseContentWidthReserve = Math.Max(550, (this.Font.Height * 22) + 154);
            int baseContentHeightReserve = Math.Max(190, (this.Font.Height * 7) + 92);
            int headerCellMinHeight = Math.Max(34, this.Font.Height + 16);
            int dataCellMinHeight = Math.Max(40, this.Font.Height + 20);
            this.buttonMinHeight = Math.Max(36, this.Font.Height + (this.buttonContentVerticalPadding * 2) + 8);
            this.buttonVerticalInset = Math.Max(2, compactSpacing / 2);
            this.buttonHorizontalInset = Math.Max(8, compactSpacing);
            this.buttonSpacing = buttonSpacing;
            this.buttonRowMinHeight = this.buttonMinHeight + (this.buttonVerticalInset * 2);
            this.upperSectionHeightReserve = (sectionSpacing * 2) + this.buttonRowMinHeight;
            this.exportButtonMinimumWidth = Math.Max(180, (this.Font.Height * 8) + 72);
            this.clearButtonMinimumWidth = Math.Max(260, (this.Font.Height * 11) + 96);
            this.okButtonMinimumWidth = Math.Max(92, (this.Font.Height * 4) + 36);
            int initialGridWidth =
                this.rowLabelMinimumWidth +
                this.dailyColumnMinimumWidth +
                this.weeklyColumnMinimumWidth +
                this.monthlyColumnMinimumWidth +
                this.gridWidthReserve;
            int initialGridHeight = headerCellMinHeight + (dataCellMinHeight * 3) + this.gridHeightReserve;
            string adapterCaptionText = UiLanguage.Get("UsageWindow.AdapterCaption", "Internetverbindung:");
            int initialMirroredSideWidth = Math.Max(
                this.mirroredSideMinimumWidth,
                TextRenderer.MeasureText(
                    adapterCaptionText,
                    interfaceFont,
                    new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width + this.adapterCaptionTextHorizontalPadding);
            int initialAdapterValueWidth = Math.Max(
                this.adapterValueMinimumWidth,
                TextRenderer.MeasureText(
                    usageWindowData.AdapterDisplayName ?? string.Empty,
                    adapterValueFont,
                    new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width + this.adapterValueTextHorizontalPadding);
            int initialAdapterRowHeight = Math.Max(
                34,
                Math.Max(interfaceFont.Height, adapterValueFont.Height) + this.adapterRowTextVerticalPadding);
            int initialAdapterMinimumWidth = (initialMirroredSideWidth * 2) + initialAdapterValueWidth;
            int initialContentWidth = Math.Max(initialGridWidth + this.gridRightSpacing, initialAdapterMinimumWidth);
            this.baseClientWidth = Math.Max(570, (this.contentHorizontalPadding * 2) + baseContentWidthReserve);
            this.baseClientHeight = Math.Max(295, this.upperSectionHeightReserve + baseContentHeightReserve);
            this.minimumClientWidth = Math.Max(this.baseClientWidth, initialContentWidth + (this.contentHorizontalPadding * 2));
            this.minimumClientHeight = Math.Max(
                this.baseClientHeight,
                this.upperSectionHeightReserve + sectionSpacing + initialAdapterRowHeight + initialGridHeight);
            this.ClientSize = new Size(this.minimumClientWidth, this.minimumClientHeight);

            this.rootLayout = new TableLayoutPanel();
            this.rootLayout.Dock = DockStyle.Fill;
            this.rootLayout.Padding = new Padding(this.contentHorizontalPadding, sectionSpacing, this.contentHorizontalPadding, sectionSpacing);
            this.rootLayout.ColumnCount = 1;
            this.rootLayout.RowCount = 3;
            this.rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this.rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this.rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, this.buttonRowMinHeight + sectionSpacing));

            this.adapterLayout = new TableLayoutPanel();
            this.adapterLayout.Dock = DockStyle.Top;
            this.adapterLayout.AutoSize = false;
            this.adapterLayout.Height = initialAdapterRowHeight;
            this.adapterLayout.Margin = new Padding(0, 0, 0, compactSpacing);
            this.adapterLayout.Padding = new Padding(0);
            this.adapterLayout.ColumnCount = 3;
            this.adapterLayout.RowCount = 1;
            this.adapterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, initialMirroredSideWidth));
            this.adapterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.adapterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, initialMirroredSideWidth));
            this.adapterLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            this.adapterCaptionLabel = new Label();
            this.adapterCaptionLabel.Dock = DockStyle.Fill;
            this.adapterCaptionLabel.AutoSize = false;
            this.adapterCaptionLabel.Margin = new Padding(0);
            this.adapterCaptionLabel.Text = adapterCaptionText;
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
            this.usageGrid.Width = initialGridWidth;
            this.usageGrid.Height = initialGridHeight;
            this.usageGrid.Margin = new Padding(0, 0, this.gridRightSpacing, sectionSpacing);
            this.usageGrid.Padding = new Padding(0);
            this.usageGrid.CellBorderStyle = TableLayoutPanelCellBorderStyle.Single;
            this.usageGrid.ColumnStyles.Clear();
            this.usageGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, this.rowLabelMinimumWidth));
            this.usageGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, this.dailyColumnMinimumWidth));
            this.usageGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, this.weeklyColumnMinimumWidth));
            this.usageGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, this.monthlyColumnMinimumWidth));
            this.usageGrid.RowStyles.Clear();
            this.usageGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, headerCellMinHeight));
            this.usageGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, dataCellMinHeight));
            this.usageGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, dataCellMinHeight));
            this.usageGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, dataCellMinHeight));

            this.cornerHeaderControl = CreateCornerLogoControl(
                new Padding(
                    cornerLogoHorizontalPadding,
                    cornerLogoVerticalPadding,
                    cornerLogoHorizontalPadding,
                    cornerLogoVerticalPadding));
            this.usageGrid.Controls.Add(this.cornerHeaderControl, 0, 0);
            this.dailyHeaderLabel = AddCell(this.usageGrid, 1, 0, UiLanguage.Get("UsageWindow.ColumnDaily", "Täglich"), headerFont, ContentAlignment.MiddleCenter, new Padding(0), headerCellMinHeight);
            this.weeklyHeaderLabel = AddCell(this.usageGrid, 2, 0, UiLanguage.Get("UsageWindow.ColumnWeekly", "Wöchentlich"), headerFont, ContentAlignment.MiddleCenter, new Padding(0), headerCellMinHeight);
            this.monthlyHeaderLabel = AddCell(this.usageGrid, 3, 0, UiLanguage.Get("UsageWindow.ColumnMonthly", "Monatlich"), headerFont, ContentAlignment.MiddleCenter, new Padding(0), headerCellMinHeight);

            this.uploadRowLabel = AddCell(this.usageGrid, 0, 1, UiLanguage.Get("UsageWindow.RowUpload", "Upload"), rowFont, ContentAlignment.MiddleLeft, new Padding(rowLabelLeftPadding, 0, 0, 0), dataCellMinHeight);
            this.dailyUploadValueLabel = AddCell(this.usageGrid, 1, 1, string.Empty, valueFont, ContentAlignment.MiddleCenter, new Padding(0), dataCellMinHeight);
            this.weeklyUploadValueLabel = AddCell(this.usageGrid, 2, 1, string.Empty, valueFont, ContentAlignment.MiddleCenter, new Padding(0), dataCellMinHeight);
            this.monthlyUploadValueLabel = AddCell(this.usageGrid, 3, 1, string.Empty, valueFont, ContentAlignment.MiddleCenter, new Padding(0), dataCellMinHeight);

            this.downloadRowLabel = AddCell(this.usageGrid, 0, 2, UiLanguage.Get("UsageWindow.RowDownload", "Download"), rowFont, ContentAlignment.MiddleLeft, new Padding(rowLabelLeftPadding, 0, 0, 0), dataCellMinHeight);
            this.dailyDownloadValueLabel = AddCell(this.usageGrid, 1, 2, string.Empty, valueFont, ContentAlignment.MiddleCenter, new Padding(0), dataCellMinHeight);
            this.weeklyDownloadValueLabel = AddCell(this.usageGrid, 2, 2, string.Empty, valueFont, ContentAlignment.MiddleCenter, new Padding(0), dataCellMinHeight);
            this.monthlyDownloadValueLabel = AddCell(this.usageGrid, 3, 2, string.Empty, valueFont, ContentAlignment.MiddleCenter, new Padding(0), dataCellMinHeight);

            this.totalRowLabel = AddCell(this.usageGrid, 0, 3, UiLanguage.Get("UsageWindow.RowTotal", "Gesamt"), rowFont, ContentAlignment.MiddleLeft, new Padding(rowLabelLeftPadding, 0, 0, 0), dataCellMinHeight);
            this.dailyTotalValueLabel = AddCell(this.usageGrid, 1, 3, string.Empty, valueFont, ContentAlignment.MiddleCenter, new Padding(0), dataCellMinHeight);
            this.weeklyTotalValueLabel = AddCell(this.usageGrid, 2, 3, string.Empty, valueFont, ContentAlignment.MiddleCenter, new Padding(0), dataCellMinHeight);
            this.monthlyTotalValueLabel = AddCell(this.usageGrid, 3, 3, string.Empty, valueFont, ContentAlignment.MiddleCenter, new Padding(0), dataCellMinHeight);

            this.buttonPanel = new Panel();
            this.buttonPanel.Dock = DockStyle.Fill;
            this.buttonPanel.Margin = new Padding(0, sectionSpacing, 0, 0);
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
            int exportButtonWidth = Math.Max(this.exportButtonMinimumWidth, exportButtonTextSize.Width + this.buttonTextHorizontalPadding);
            this.exportButton.Size = new Size(exportButtonWidth, this.buttonMinHeight);
            this.exportButton.MinimumSize = new Size(exportButtonWidth, this.buttonMinHeight);
            this.exportButton.Padding = new Padding(
                this.buttonContentHorizontalPadding,
                this.buttonContentVerticalPadding,
                this.buttonContentHorizontalPadding,
                this.buttonContentVerticalPadding);
            this.exportButton.UseVisualStyleBackColor = true;
            this.exportButton.TextAlign = ContentAlignment.MiddleCenter;
            this.exportButton.Anchor = AnchorStyles.Left;
            this.exportButton.Location = new Point(this.buttonHorizontalInset, this.buttonVerticalInset);
            this.exportButton.Click += this.ExportButton_Click;

            this.clearButton = new Button();
            this.clearButton.Text = UiLanguage.Get("UsageWindow.ClearAll", "Verbrauchsdaten löschen");
            this.clearButton.AutoSize = false;
            this.clearButton.FlatStyle = FlatStyle.System;
            Size clearButtonTextSize = TextRenderer.MeasureText(
                this.clearButton.Text,
                this.Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            int clearButtonWidth = Math.Max(this.clearButtonMinimumWidth, clearButtonTextSize.Width + this.buttonTextHorizontalPadding);
            this.clearButton.Size = new Size(clearButtonWidth, this.buttonMinHeight);
            this.clearButton.MinimumSize = new Size(clearButtonWidth, this.buttonMinHeight);
            this.clearButton.Padding = new Padding(
                this.buttonContentHorizontalPadding,
                this.buttonContentVerticalPadding,
                this.buttonContentHorizontalPadding,
                this.buttonContentVerticalPadding);
            this.clearButton.UseVisualStyleBackColor = true;
            this.clearButton.TextAlign = ContentAlignment.MiddleCenter;
            this.clearButton.Anchor = AnchorStyles.Left;
            this.clearButton.Location = new Point(this.exportButton.Right + this.buttonSpacing, this.buttonVerticalInset);
            this.clearButton.Click += this.ClearButton_Click;

            this.okButton = new Button();
            this.okButton.Text = UiLanguage.Get("Common.Ok", "OK");
            this.okButton.DialogResult = DialogResult.OK;
            this.okButton.Font = this.Font;
            this.okButton.AutoSize = false;
            this.okButton.FlatStyle = FlatStyle.System;
            Size okButtonTextSize = TextRenderer.MeasureText(
                this.okButton.Text,
                this.okButton.Font ?? this.Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            int okButtonWidth = Math.Max(this.okButtonMinimumWidth, okButtonTextSize.Width + this.buttonTextHorizontalPadding);
            this.okButton.Size = new Size(okButtonWidth, this.buttonMinHeight);
            this.okButton.MinimumSize = this.okButton.Size;
            this.okButton.UseVisualStyleBackColor = true;
            this.okButton.TextAlign = ContentAlignment.MiddleCenter;
            this.okButton.Padding = new Padding(
                this.buttonContentHorizontalPadding,
                this.buttonContentVerticalPadding,
                this.buttonContentHorizontalPadding,
                this.buttonContentVerticalPadding);
            this.okButton.Margin = Padding.Empty;
            this.okButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.okButton.Location = new Point(
                Math.Max(this.buttonHorizontalInset, this.buttonPanel.Width - this.buttonHorizontalInset - this.okButton.Width),
                this.buttonVerticalInset);

            this.buttonPanel.Controls.Add(this.exportButton);
            this.buttonPanel.Controls.Add(this.clearButton);
            this.buttonPanel.Controls.Add(this.okButton);
            this.buttonPanel.Resize += delegate
            {
                this.exportButton.Location = new Point(
                    this.buttonHorizontalInset,
                    Math.Max(0, (this.buttonPanel.ClientSize.Height - this.exportButton.Height) / 2));
                this.clearButton.Location = new Point(
                    this.exportButton.Right + this.buttonSpacing,
                    Math.Max(0, (this.buttonPanel.ClientSize.Height - this.clearButton.Height) / 2));
                this.okButton.Location = new Point(
                    Math.Max(this.buttonHorizontalInset, this.buttonPanel.ClientSize.Width - this.buttonHorizontalInset - this.okButton.Width),
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
                saveFileDialog.Title = UiLanguage.Get("UsageWindow.ExportDialogTitle", "CSV exportieren...");
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
              int exportButtonTextWidth = TextRenderer.MeasureText(
                  this.exportButton.Text,
                  this.exportButton.Font ?? this.Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width;
            int exportButtonWidth = Math.Max(this.exportButtonMinimumWidth, exportButtonTextWidth + this.buttonTextHorizontalPadding);
            int exportButtonHeight = Math.Max(this.buttonMinHeight, this.exportButton.GetPreferredSize(Size.Empty).Height);
            this.exportButton.Size = new Size(exportButtonWidth, exportButtonHeight);
            this.exportButton.MinimumSize = this.exportButton.Size;

            int clearButtonTextWidth = TextRenderer.MeasureText(
                this.clearButton.Text,
                this.clearButton.Font ?? this.Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width;
            int clearButtonWidth = Math.Max(this.clearButtonMinimumWidth, clearButtonTextWidth + this.buttonTextHorizontalPadding);
            int clearButtonHeight = Math.Max(this.buttonMinHeight, this.clearButton.GetPreferredSize(Size.Empty).Height);
            this.clearButton.Size = new Size(clearButtonWidth, clearButtonHeight);
            this.clearButton.MinimumSize = this.clearButton.Size;

              int okButtonTextWidth = TextRenderer.MeasureText(
                  this.okButton.Text,
                  this.okButton.Font ?? this.Font,
                  new Size(int.MaxValue, int.MaxValue),
                  TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width;
              int okButtonWidth = Math.Max(this.okButtonMinimumWidth, okButtonTextWidth + this.buttonTextHorizontalPadding);
              int okButtonHeight = Math.Max(this.buttonMinHeight, this.okButton.GetPreferredSize(Size.Empty).Height);
              this.okButton.Size = new Size(okButtonWidth, okButtonHeight);
              this.okButton.MinimumSize = this.okButton.Size;
              int buttonRowHeight = Math.Max(
                  this.buttonRowMinHeight,
                  Math.Max(Math.Max(this.exportButton.Height, this.clearButton.Height), this.okButton.Height) + (this.buttonVerticalInset * 2));
              int buttonPanelOuterHeight = buttonRowHeight + this.buttonPanel.Margin.Vertical;
              this.rootLayout.RowStyles[2].Height = buttonPanelOuterHeight;
              int buttonsMinimumWidth =
                  this.buttonHorizontalInset +
                  this.exportButton.Width +
                  this.buttonSpacing +
                  this.clearButton.Width +
                  this.buttonSpacing +
                  this.okButton.Width +
                  this.buttonHorizontalInset;
              this.buttonPanel.MinimumSize = new Size(buttonsMinimumWidth, buttonRowHeight);
              this.buttonPanel.Height = buttonRowHeight;

            int rowLabelWidth = Math.Max(
                this.rowLabelMinimumWidth,
                Math.Max(
                    MeasureTextWidth(this.uploadRowLabel),
                    Math.Max(
                        MeasureTextWidth(this.downloadRowLabel),
                        MeasureTextWidth(this.totalRowLabel))) + this.rowLabelTextHorizontalPadding);

            int adapterValueWidth = MeasureTextWidth(this.adapterValueLabel) + this.adapterValueTextHorizontalPadding;
            int adapterRowHeight = Math.Max(
                34,
                Math.Max(
                    MeasureTextHeight(this.adapterCaptionLabel),
                    MeasureTextHeight(this.adapterValueLabel)) + this.adapterRowTextVerticalPadding);
            this.adapterLayout.RowStyles[0].Height = adapterRowHeight;
            this.adapterLayout.Height = adapterRowHeight;

            int dailyColumnWidth = Math.Max(
                MeasureTextWidth(this.dailyHeaderLabel),
                Math.Max(
                    MeasureTextWidth(this.dailyUploadValueLabel),
                    Math.Max(MeasureTextWidth(this.dailyDownloadValueLabel), MeasureTextWidth(this.dailyTotalValueLabel)))) + this.dailyColumnTextHorizontalPadding;
            int weeklyColumnWidth = Math.Max(
                MeasureTextWidth(this.weeklyHeaderLabel),
                Math.Max(
                    MeasureTextWidth(this.weeklyUploadValueLabel),
                    Math.Max(MeasureTextWidth(this.weeklyDownloadValueLabel), MeasureTextWidth(this.weeklyTotalValueLabel)))) + this.weeklyColumnTextHorizontalPadding;
            int monthlyColumnWidth = Math.Max(
                MeasureTextWidth(this.monthlyHeaderLabel),
                Math.Max(
                    MeasureTextWidth(this.monthlyUploadValueLabel),
                    Math.Max(MeasureTextWidth(this.monthlyDownloadValueLabel), MeasureTextWidth(this.monthlyTotalValueLabel)))) + this.monthlyColumnTextHorizontalPadding;

            dailyColumnWidth = Math.Max(this.dailyColumnMinimumWidth, dailyColumnWidth);
            weeklyColumnWidth = Math.Max(this.weeklyColumnMinimumWidth, weeklyColumnWidth);
            monthlyColumnWidth = Math.Max(this.monthlyColumnMinimumWidth, monthlyColumnWidth);

            int dataRowMinHeight = Math.Max(
                this.uploadRowLabel.MinimumSize.Height,
                Math.Max(
                    this.downloadRowLabel.MinimumSize.Height,
                    Math.Max(this.totalRowLabel.MinimumSize.Height, this.dailyUploadValueLabel.MinimumSize.Height)));

            int headerRowHeight = Math.Max(
                Math.Max(
                    this.dailyHeaderLabel.MinimumSize.Height,
                    Math.Max(this.weeklyHeaderLabel.MinimumSize.Height, this.monthlyHeaderLabel.MinimumSize.Height)),
                Math.Max(
                    MeasureTextHeight(this.dailyHeaderLabel),
                    Math.Max(MeasureTextHeight(this.monthlyHeaderLabel), MeasureTextHeight(this.weeklyHeaderLabel))) + this.headerRowTextVerticalPadding);
            int uploadRowHeight = Math.Max(
                MeasureTextHeight(this.uploadRowLabel),
                Math.Max(
                    MeasureTextHeight(this.dailyUploadValueLabel),
                    Math.Max(MeasureTextHeight(this.monthlyUploadValueLabel), MeasureTextHeight(this.weeklyUploadValueLabel)))) + this.dataRowTextVerticalPadding;
            int downloadRowHeight = Math.Max(
                MeasureTextHeight(this.downloadRowLabel),
                Math.Max(
                    MeasureTextHeight(this.dailyDownloadValueLabel),
                    Math.Max(MeasureTextHeight(this.monthlyDownloadValueLabel), MeasureTextHeight(this.weeklyDownloadValueLabel)))) + this.dataRowTextVerticalPadding;
            int totalRowHeight = Math.Max(
                MeasureTextHeight(this.totalRowLabel),
                Math.Max(
                    MeasureTextHeight(this.dailyTotalValueLabel),
                    Math.Max(MeasureTextHeight(this.monthlyTotalValueLabel), MeasureTextHeight(this.weeklyTotalValueLabel)))) + this.dataRowTextVerticalPadding;

            uploadRowHeight = Math.Max(dataRowMinHeight, uploadRowHeight);
            downloadRowHeight = Math.Max(dataRowMinHeight, downloadRowHeight);
            totalRowHeight = Math.Max(dataRowMinHeight, totalRowHeight);

            int minimumGridWidth = rowLabelWidth + dailyColumnWidth + weeklyColumnWidth + monthlyColumnWidth + this.gridWidthReserve;

              int mirroredSideWidth = Math.Max(
                  this.mirroredSideMinimumWidth,
                  MeasureTextWidth(this.adapterCaptionLabel) + this.adapterCaptionTextHorizontalPadding);
              this.adapterLayout.ColumnStyles[0].Width = mirroredSideWidth;
              this.adapterLayout.ColumnStyles[2].Width = mirroredSideWidth;

              int adapterMinimumWidth = (mirroredSideWidth * 2) + Math.Max(adapterValueWidth, this.adapterValueMinimumWidth);
            int minimumClientWidth = Math.Max(
                minimumGridWidth + this.contentWidthReserve,
                Math.Max(adapterMinimumWidth, buttonsMinimumWidth)) + contentPaddingWidth;
            int minimumClientHeight = contentPaddingHeight +
                this.adapterLayout.Height +
                this.adapterLayout.Margin.Bottom +
                this.usageGrid.Height +
                this.usageGrid.Margin.Bottom +
                buttonPanelOuterHeight;

            int availableGridWidth = Math.Max(
                minimumGridWidth,
                this.ClientSize.Width - contentPaddingWidth - this.usageGrid.Margin.Horizontal);
            int additionalGridWidth = Math.Max(0, availableGridWidth - minimumGridWidth);
            int extraDailyWidth = additionalGridWidth / 3;
            int extraWeeklyWidth = additionalGridWidth / 3;

            this.usageGrid.SuspendLayout();
            this.usageGrid.ColumnStyles[0].Width = rowLabelWidth;
            this.usageGrid.ColumnStyles[1].Width = dailyColumnWidth + extraDailyWidth;
            this.usageGrid.ColumnStyles[2].Width = weeklyColumnWidth + extraWeeklyWidth;
            this.usageGrid.ColumnStyles[3].Width = monthlyColumnWidth + additionalGridWidth - extraDailyWidth - extraWeeklyWidth;
            this.usageGrid.RowStyles[0].Height = headerRowHeight;
            this.usageGrid.RowStyles[1].Height = uploadRowHeight;
            this.usageGrid.RowStyles[2].Height = downloadRowHeight;
            this.usageGrid.RowStyles[3].Height = totalRowHeight;
            this.usageGrid.Width = availableGridWidth;
            this.usageGrid.Height = headerRowHeight + uploadRowHeight + downloadRowHeight + totalRowHeight + this.gridHeightReserve;
            this.usageGrid.ResumeLayout();

            this.rootLayout.PerformLayout();
            int requiredClientWidth = Math.Max(
                minimumClientWidth,
                this.rootLayout.GetPreferredSize(new Size(0, this.ClientSize.Height)).Width + this.rootLayout.Padding.Horizontal);
            if (requiredClientWidth > this.ClientSize.Width)
            {
                this.ClientSize = new Size(requiredClientWidth, this.ClientSize.Height);
            }
            int confirmedClientWidth = this.ClientSize.Width;
            int requiredClientHeight = Math.Max(
                minimumClientHeight,
                this.rootLayout.GetPreferredSize(new Size(confirmedClientWidth, 0)).Height + this.rootLayout.Padding.Vertical);
            this.MinimumSize = this.SizeFromClientSize(new Size(
                Math.Max(this.baseClientWidth, minimumClientWidth),
                Math.Max(this.baseClientHeight, requiredClientHeight)));

            int targetClientHeight = Math.Max(this.ClientSize.Height, Math.Max(this.baseClientHeight, requiredClientHeight));
            if (confirmedClientWidth != this.ClientSize.Width || targetClientHeight != this.ClientSize.Height)
            {
                this.ClientSize = new Size(confirmedClientWidth, targetClientHeight);
            }

            this.rootLayout.PerformLayout();
            int requiredWidthFromVisibleControls = Math.Max(
                Math.Max(
                    this.adapterLayout.Right + this.adapterLayout.Margin.Right,
                    this.usageGrid.Right + this.usageGrid.Margin.Right),
                this.buttonPanel.Right + this.buttonPanel.Margin.Right) + this.rootLayout.Padding.Right;
            if (requiredWidthFromVisibleControls > this.ClientSize.Width)
            {
                this.ClientSize = new Size(requiredWidthFromVisibleControls, this.ClientSize.Height);
                this.rootLayout.PerformLayout();
            }
            int requiredHeightFromVisibleControls = Math.Max(
                Math.Max(
                    this.adapterLayout.Bottom + this.adapterLayout.Margin.Bottom,
                    this.usageGrid.Bottom + this.usageGrid.Margin.Bottom),
                this.buttonPanel.Bottom + this.buttonPanel.Margin.Bottom) + this.rootLayout.Padding.Bottom;
            if (requiredHeightFromVisibleControls != this.ClientSize.Height)
            {
                this.ClientSize = new Size(this.ClientSize.Width, requiredHeightFromVisibleControls);
                this.rootLayout.PerformLayout();
            }

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
            Padding padding,
            int minimumHeight)
        {
            Label label = new Label();
            label.Dock = DockStyle.Fill;
            label.AutoSize = false;
            label.AutoEllipsis = false;
            label.MinimumSize = new Size(0, minimumHeight);
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

        private static Control CreateCornerLogoControl(Padding padding)
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.Margin = new Padding(0);
            panel.Padding = padding;

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
                System.Diagnostics.Trace.WriteLine("[TrafficView] UsageSummaryForm Logo konnte nicht geladen werden.");
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
                adapterName = UiLanguage.Get("Common.Adapter", "Internetverbindung");
            }

            return string.Format(
                "{0}_{1}_{2:yyyy-MM-dd}.csv",
                UiLanguage.Get("UsageWindow.Title", "Datenverbrauch"),
                adapterName,
                DateTime.Now);
        }
    }
}
