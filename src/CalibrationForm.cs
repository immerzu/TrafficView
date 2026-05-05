using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace TrafficView
{
    internal sealed class CalibrationForm : Form
    {
        private const int CalibrationDurationSeconds = 30;

        private readonly ComboBox adapterComboBox;
        private readonly ProgressBar progressBar;
        private readonly Label statusLabel;
        private readonly Label infoLabel;
        private readonly Label speedtestHintLabel;
        private readonly Button startButton;
        private readonly Button saveAdapterButton;
        private readonly Button saveButton;
        private readonly Button cancelButton;
        private readonly LinkLabel speedtestNetLinkLabel;
        private readonly LinkLabel wieIstMeineIpLinkLabel;
        private readonly FlowLayoutPanel speedtestLinksPanel;
        private readonly Control calibrationLogoControl;
        private readonly TableLayoutPanel calibrationButtonPanel;
        private readonly TableLayoutPanel calibrationSpeedtestPanel;
        private readonly int calibrationButtonHorizontalPadding;
        private readonly int calibrationButtonVerticalPadding;
        private readonly int calibrationButtonHeightMinimum;
        private readonly int calibrationButtonHeightReserve;
        private readonly int calibrationButtonMinimumWidth;
        private readonly int calibrationButtonTextWidthReserve;
        private readonly int calibrationContentMinimumWidth;
        private readonly int calibrationDialogWidthReserve;
        private readonly int calibrationDialogHeightReserve;
        private readonly int calibrationFooterWrapMinimumWidth;
        private readonly int calibrationFooterContentMinimumWidth;
        private readonly int calibrationFooterLinkSpacing;
        private readonly int calibrationFooterLogoHeight;
        private readonly int calibrationFooterLogoMinimumWidth;
        private readonly int calibrationFooterLogoMaximumWidth;
        private readonly Timer calibrationTimer;
        private readonly TableLayoutPanel rootLayout;
        private readonly bool isInitialCalibrationRequired;
        private bool selectedAdapterAvailable = true;
        private MonitorSettings currentSettings;
        private MonitorSettings activeSettings;
        private NetworkSnapshot lastSnapshot;
        private DateTime lastSampleUtc;
        private double peakBytesPerSecond;
        private double peakDownloadBytesPerSecond;
        private double peakUploadBytesPerSecond;
        private int elapsedSeconds;
        private bool calibrationDialogSizeLocked;
        private bool allowClose;
        private bool isCapturing;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint uFlags);

        private const uint CalibrationSwpShowWindow = 0x0040;
        private static readonly IntPtr CalibrationHwndTopMost = new IntPtr(-1);

        public CalibrationForm(MonitorSettings settings)
        {
            this.currentSettings = settings.Clone();
            this.SelectedSettings = null;
            this.isInitialCalibrationRequired = !this.currentSettings.InitialCalibrationPromptHandled;

            this.Text = UiLanguage.Get("Calibration.Title", "Kalibration");
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ShowInTaskbar = false;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ControlBox = false;
            this.AutoScaleMode = AutoScaleMode.Font;
            this.Font = SystemFonts.MessageBoxFont;
            this.TopMost = true;
            this.AutoScroll = false;
            int sectionSpacing = Math.Max(6, this.Font.Height - 6);
            int compactSpacing = Math.Max(4, this.Font.Height / 3);
            int progressBarHeight = Math.Max(14, this.Font.Height + 1);
            int buttonSpacing = Math.Max(6, compactSpacing + 1);
            int linkSpacing = Math.Max(6, compactSpacing + 1);
            int logoHeight = Math.Max(22, (this.Font.Height * 2) - 2);
            int horizontalPadding = Math.Max(18, this.Font.Height + 6);
            int verticalPadding = Math.Max(8, this.Font.Height - 2);
            int topPadding = Math.Max(1, verticalPadding - 7);
            int bottomPadding = Math.Max(0, verticalPadding - 10);
            int dialogHeightReserve = Math.Max(1, compactSpacing - 3);
            int contentMinimumHeight = Math.Max(116, this.Font.Height * 6);
            int baseContentWidth = Math.Max(548, this.Font.Height * 29);
            int contentMinimumWidth = Math.Max(440, this.Font.Height * 25);
            int baseClientWidth = Math.Max(620, (horizontalPadding * 2) + baseContentWidth);
            int baseClientHeight = Math.Max(152, dialogHeightReserve + topPadding + bottomPadding + contentMinimumHeight);
            int baseMinimumClientWidth = Math.Max(580, (horizontalPadding * 2) + contentMinimumWidth);
            int baseMinimumClientHeight = Math.Max(144, topPadding + bottomPadding + contentMinimumHeight);
            this.ClientSize = new Size(baseClientWidth, baseClientHeight);
            this.MinimumSize = new Size(baseMinimumClientWidth, baseMinimumClientHeight);
            this.calibrationButtonHorizontalPadding = Math.Max(8, compactSpacing + 2);
            this.calibrationButtonVerticalPadding = Math.Max(1, (compactSpacing / 2) - 1);
            this.calibrationButtonHeightMinimum = Math.Max(36, this.Font.Height + 14);
            this.calibrationButtonHeightReserve = Math.Max(16, (compactSpacing * 3) - 2);
            this.calibrationButtonMinimumWidth = Math.Max(96, (this.Font.Height * 5) + 24);
            this.calibrationButtonTextWidthReserve = Math.Max(12, compactSpacing * 3);
            this.calibrationContentMinimumWidth = contentMinimumWidth;
            this.calibrationDialogWidthReserve = Math.Max(10, horizontalPadding - sectionSpacing);
            this.calibrationDialogHeightReserve = dialogHeightReserve;
            this.calibrationFooterWrapMinimumWidth = Math.Max(340, this.Font.Height * 20);
            this.calibrationFooterContentMinimumWidth = Math.Max(250, this.Font.Height * 13);
            this.calibrationFooterLinkSpacing = linkSpacing;
            this.calibrationFooterLogoHeight = logoHeight;
            this.calibrationFooterLogoMinimumWidth = Math.Max(96, logoHeight + Math.Max(24, compactSpacing * 4));
            this.calibrationFooterLogoMaximumWidth = Math.Max(180, logoHeight * 3);
            int compactButtonHeight = this.GetCalibrationButtonHeight();

            this.rootLayout = new TableLayoutPanel();
            this.rootLayout.ColumnCount = 1;
            this.rootLayout.RowCount = 6;
            this.rootLayout.Dock = DockStyle.Fill;
            this.rootLayout.Padding = new Padding(horizontalPadding, topPadding, horizontalPadding, bottomPadding);
            this.rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this.rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this.rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this.rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this.rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this.rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this.Controls.Add(this.rootLayout);

            this.infoLabel = new Label();
            this.infoLabel.AutoSize = true;
            this.infoLabel.Dock = DockStyle.Fill;
            this.infoLabel.Margin = new Padding(0, 0, 0, Math.Max(2, compactSpacing - 2));
            this.infoLabel.Text = UiLanguage.Get(
                "Calibration.Info",
                "Wähle die Internetverbindung für Kalibration und Überwachung. Die Messung läuft ca. 30 Sekunden. Das Fenster bleibt bis zum Speichern oder Abbrechen geöffnet.");
            this.rootLayout.Controls.Add(this.infoLabel, 0, 0);

            TableLayoutPanel adapterLayout = new TableLayoutPanel();
            adapterLayout.ColumnCount = 1;
            adapterLayout.RowCount = 2;
            adapterLayout.Dock = DockStyle.Fill;
            adapterLayout.Margin = new Padding(0, 0, 0, Math.Max(2, compactSpacing - 2));
            adapterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            adapterLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            adapterLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this.rootLayout.Controls.Add(adapterLayout, 0, 1);

            Label adapterLabel = new Label();
            adapterLabel.AutoSize = true;
            adapterLabel.Dock = DockStyle.Fill;
            adapterLabel.Margin = new Padding(0, 0, 0, Math.Max(1, compactSpacing - 2));
            adapterLabel.Text = UiLanguage.Get("Calibration.AdapterLabel", "Internetverbindung");
            adapterLayout.Controls.Add(adapterLabel, 0, 0);

            this.adapterComboBox = new ComboBox();
            this.adapterComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            this.adapterComboBox.Dock = DockStyle.Top;
            this.adapterComboBox.IntegralHeight = false;
            this.adapterComboBox.Margin = new Padding(0);
            this.adapterComboBox.MinimumSize = new Size(0, Math.Max(28, this.adapterComboBox.PreferredHeight + 4));
            this.adapterComboBox.SelectedIndexChanged += this.AdapterComboBox_SelectedIndexChanged;
            adapterLayout.Controls.Add(this.adapterComboBox, 0, 1);

            this.progressBar = new ProgressBar();
            this.progressBar.Dock = DockStyle.Top;
            this.progressBar.Margin = new Padding(0, 0, 0, Math.Max(1, compactSpacing - 2));
            this.progressBar.Height = progressBarHeight;
            this.progressBar.Minimum = 0;
            this.progressBar.Maximum = CalibrationDurationSeconds;
            this.rootLayout.Controls.Add(this.progressBar, 0, 2);

            this.statusLabel = new Label();
            this.statusLabel.AutoSize = true;
            this.statusLabel.Dock = DockStyle.Fill;
            this.statusLabel.Margin = new Padding(0, 0, 0, Math.Max(1, compactSpacing - 2));
            this.statusLabel.Text = UiLanguage.Get(
                "Calibration.ReadyStatus",
                "Bereit für die Kalibration. Bitte mit 'Starten' beginnen und später mit 'Speichern' bestätigen oder mit 'Abbrechen' schließen.");
            this.statusLabel.MinimumSize = new Size(0, Math.Max(20, this.Font.Height + Math.Max(1, compactSpacing - 3)));
            this.rootLayout.Controls.Add(this.statusLabel, 0, 3);

            this.calibrationButtonPanel = new TableLayoutPanel();
            TableLayoutPanel buttonPanel = this.calibrationButtonPanel;
            buttonPanel.ColumnCount = 2;
            buttonPanel.RowCount = 2;
            buttonPanel.AutoSize = true;
            buttonPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            buttonPanel.Dock = DockStyle.None;
            buttonPanel.Anchor = AnchorStyles.Top;
            buttonPanel.Margin = new Padding(0);
            buttonPanel.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            buttonPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            buttonPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this.rootLayout.Controls.Add(buttonPanel, 0, 4);

            this.startButton = new Button();
            string startButtonText = UiLanguage.Get("Calibration.Start", "Starten");
            string remeasureButtonText = UiLanguage.Get("Calibration.Remeasure", "Neu messen");
            this.startButton.Text = startButtonText;
            this.startButton.Margin = new Padding(0, 0, 0, buttonSpacing);
            this.ConfigureDialogButton(this.startButton);
            this.startButton.Width = Math.Max(
                this.GetCalibrationButtonWidth(startButtonText),
                this.GetCalibrationButtonWidth(remeasureButtonText));
            this.startButton.Click += this.StartButton_Click;
            buttonPanel.Controls.Add(this.startButton, 1, 0);

            this.saveAdapterButton = new Button();
            this.saveAdapterButton.Text = UiLanguage.Get("Calibration.SaveAdapter", "Internetverbindung speichern");
            this.saveAdapterButton.Margin = new Padding(0, 0, buttonSpacing, buttonSpacing);
            this.ConfigureDialogButton(this.saveAdapterButton);
            this.saveAdapterButton.Click += this.SaveAdapterButton_Click;
            buttonPanel.Controls.Add(this.saveAdapterButton, 0, 0);

            this.saveButton = new Button();
            this.saveButton.Text = UiLanguage.Get("Calibration.Save", "Speichern");
            this.saveButton.Margin = new Padding(0, 0, buttonSpacing, 0);
            this.ConfigureDialogButton(this.saveButton);
            this.saveButton.Enabled = false;
            this.saveButton.Click += this.SaveButton_Click;
            buttonPanel.Controls.Add(this.saveButton, 0, 1);

            this.cancelButton = new Button();
            this.cancelButton.Text = UiLanguage.Get("Calibration.Cancel", "Abbrechen");
            this.cancelButton.Margin = new Padding(0);
            this.ConfigureDialogButton(this.cancelButton);
            this.cancelButton.Click += this.CancelButton_Click;
            buttonPanel.Controls.Add(this.cancelButton, 1, 1);

            int initialButtonPanelWidth = buttonPanel.GetPreferredSize(Size.Empty).Width;
            int initialClientWidth = Math.Max(
                this.ClientSize.Width,
                initialButtonPanelWidth + this.rootLayout.Padding.Horizontal + this.calibrationDialogWidthReserve);
            int initialMinimumClientWidth = Math.Max(
                baseMinimumClientWidth,
                initialButtonPanelWidth + this.rootLayout.Padding.Horizontal + this.calibrationDialogWidthReserve);
            this.ClientSize = new Size(initialClientWidth, this.ClientSize.Height);
            this.MinimumSize = this.SizeFromClientSize(
                new Size(initialMinimumClientWidth, Math.Max(baseMinimumClientHeight, this.ClientSize.Height)));

            this.calibrationSpeedtestPanel = new TableLayoutPanel();
            TableLayoutPanel speedtestPanel = this.calibrationSpeedtestPanel;
            speedtestPanel.AutoSize = true;
            speedtestPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            speedtestPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            speedtestPanel.Margin = new Padding(0, 0, 0, 0);
            speedtestPanel.ColumnCount = 2;
            speedtestPanel.RowCount = 2;
            speedtestPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            speedtestPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            speedtestPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            speedtestPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this.rootLayout.Controls.Add(speedtestPanel, 0, 5);

            this.calibrationLogoControl = this.CreateCalibrationLogoControl(
                this.calibrationFooterLogoHeight,
                this.calibrationFooterLogoMinimumWidth,
                this.calibrationFooterLogoMaximumWidth);
            if (this.calibrationLogoControl != null)
            {
                this.calibrationLogoControl.Margin = new Padding(0, 0, Math.Max(4, compactSpacing - 2), 0);
                speedtestPanel.Controls.Add(this.calibrationLogoControl, 0, 0);
                speedtestPanel.SetRowSpan(this.calibrationLogoControl, 2);
            }

            this.speedtestHintLabel = new Label();
            this.speedtestHintLabel.AutoSize = true;
            this.speedtestHintLabel.Margin = new Padding(0, 0, 0, 0);
            this.speedtestHintLabel.TextAlign = ContentAlignment.MiddleLeft;
            this.speedtestHintLabel.Text = UiLanguage.Get(
                "Calibration.SpeedtestHint",
                "Zum Erzeugen von Datenverkehr kann ein Speedtest geöffnet werden:");
            speedtestPanel.Controls.Add(this.speedtestHintLabel, 1, 0);

            this.speedtestLinksPanel = new FlowLayoutPanel();
            this.speedtestLinksPanel.AutoSize = true;
            this.speedtestLinksPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.speedtestLinksPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            this.speedtestLinksPanel.FlowDirection = FlowDirection.LeftToRight;
            this.speedtestLinksPanel.WrapContents = true;
            this.speedtestLinksPanel.Margin = new Padding(0);
            speedtestPanel.Controls.Add(this.speedtestLinksPanel, 1, 1);

            this.speedtestNetLinkLabel = new LinkLabel();
            this.speedtestNetLinkLabel.AutoSize = true;
            this.speedtestNetLinkLabel.Margin = new Padding(0, 0, this.calibrationFooterLinkSpacing, 0);
            this.speedtestNetLinkLabel.Text = UiLanguage.Get("Calibration.SpeedtestNet", "Speedtest.net öffnen");
            this.speedtestNetLinkLabel.Tag = "https://www.speedtest.net";
            this.speedtestNetLinkLabel.LinkClicked += this.SpeedtestLinkLabel_LinkClicked;
            this.speedtestLinksPanel.Controls.Add(this.speedtestNetLinkLabel);

            this.wieIstMeineIpLinkLabel = new LinkLabel();
            this.wieIstMeineIpLinkLabel.AutoSize = true;
            this.wieIstMeineIpLinkLabel.Margin = new Padding(0, 0, 0, 0);
            this.wieIstMeineIpLinkLabel.Text = UiLanguage.Get("Calibration.SpeedtestWieIstMeineIp", "wieistmeineip.de Speedtest öffnen");
            this.wieIstMeineIpLinkLabel.Tag = "https://www.wieistmeineip.de/speedtest/";
            this.wieIstMeineIpLinkLabel.LinkClicked += this.SpeedtestLinkLabel_LinkClicked;
            this.speedtestLinksPanel.Controls.Add(this.wieIstMeineIpLinkLabel);

            this.AcceptButton = this.startButton;
            this.CancelButton = this.cancelButton;

            this.calibrationTimer = new Timer();
            this.calibrationTimer.Interval = 1000;
            this.calibrationTimer.Tick += this.CalibrationTimer_Tick;

            this.LoadAdapterItems();
            this.UpdateAdapterAvailabilityUi();
            this.Load += this.CalibrationForm_Load;
            this.Shown += this.CalibrationForm_Shown;
        }

        public MonitorSettings SelectedSettings { get; private set; }
        public MonitorSettings SavedAdapterSettings { get; private set; }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!this.allowClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.statusLabel.Text = this.saveButton.Enabled
                    ? UiLanguage.Get("Calibration.CloseHintDone", "Kalibration abgeschlossen. Bitte mit 'Speichern' bestätigen oder mit 'Abbrechen' schließen.")
                    : UiLanguage.Get("Calibration.CloseHintWaiting", "Bitte 'Abbrechen' verwenden oder die Kalibration abschließen und mit 'Speichern' bestätigen.");
                return;
            }

            if (this.calibrationTimer.Enabled)
            {
                this.calibrationTimer.Stop();
            }

            base.OnFormClosing(e);
        }

        private void CalibrationForm_Load(object sender, EventArgs e)
        {
            this.AdjustDialogLayout();
            this.LockCalibrationDialogSize();
        }

        private void CalibrationForm_Shown(object sender, EventArgs e)
        {
            SetWindowPos(this.Handle, CalibrationHwndTopMost, this.Left, this.Top, this.Width, this.Height, CalibrationSwpShowWindow);
            this.BringToFront();
            this.Activate();
            this.ActiveControl = this.startButton;
            SetForegroundWindow(this.Handle);
        }

        private void ConfigureDialogButton(Button button)
        {
            int compactButtonHeight = this.GetCalibrationButtonHeight();
            button.AutoSize = false;
            button.Dock = DockStyle.Fill;
            button.Padding = new Padding(
                this.calibrationButtonHorizontalPadding,
                this.calibrationButtonVerticalPadding,
                this.calibrationButtonHorizontalPadding,
                this.calibrationButtonVerticalPadding);
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Width = this.GetCalibrationButtonWidth(button.Text);
            button.MinimumSize = new Size(0, compactButtonHeight);
            button.MaximumSize = new Size(int.MaxValue, compactButtonHeight);
            button.Height = compactButtonHeight;
        }

        private int GetCalibrationButtonHeight()
        {
            return Math.Max(this.calibrationButtonHeightMinimum, this.Font.Height + this.calibrationButtonHeightReserve);
        }

        private int GetCalibrationButtonWidth(string text)
        {
            Size textSize = TextRenderer.MeasureText(
                text ?? string.Empty,
                this.Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            int horizontalPadding = this.calibrationButtonHorizontalPadding * 2;
            return Math.Max(this.calibrationButtonMinimumWidth, textSize.Width + horizontalPadding + this.calibrationButtonTextWidthReserve);
        }

        private Control CreateCalibrationLogoControl(int logoHeight, int minimumWidth, int maximumWidth)
        {
            string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LOLO-SOFT_00_SW.png");
            if (!File.Exists(logoPath))
            {
                return null;
            }

            try
            {
                Bitmap logoImage;
                using (FileStream stream = new FileStream(logoPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (Image image = Image.FromStream(stream))
                {
                    logoImage = new Bitmap(image);
                }

                int logoWidth = Math.Max(minimumWidth, (int)Math.Round((double)logoImage.Width * logoHeight / Math.Max(1, logoImage.Height)));
                logoWidth = Math.Min(maximumWidth, logoWidth);

                Panel logoPanel = new Panel();
                logoPanel.Size = new Size(logoWidth, logoHeight);
                logoPanel.Margin = Padding.Empty;
                logoPanel.Padding = Padding.Empty;
                logoPanel.Anchor = AnchorStyles.Left | AnchorStyles.Top;
                logoPanel.BackColor = Color.Transparent;

                PictureBox pictureBox = new PictureBox();
                pictureBox.Dock = DockStyle.Fill;
                pictureBox.Margin = Padding.Empty;
                pictureBox.Padding = Padding.Empty;
                pictureBox.TabStop = false;
                pictureBox.Image = logoImage;
                pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
                pictureBox.BackColor = Color.Transparent;
                logoPanel.Controls.Add(pictureBox);
                logoPanel.Disposed += delegate
                {
                    pictureBox.Image.Dispose();
                };

                return logoPanel;
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce(
                    "calibration-logo-load-failed",
                    string.Format("Kalibrationslogo konnte nicht aus '{0}' geladen werden.", logoPath),
                    ex);
                return null;
            }
        }

        private void SetCalibrationButtonText(Button button, string text)
        {
            if (button == null)
            {
                return;
            }

            button.Text = text ?? string.Empty;
            button.Width = Math.Max(button.Width, this.GetCalibrationButtonWidth(button.Text));
        }

        private void LockCalibrationDialogSize()
        {
            if (this.calibrationDialogSizeLocked)
            {
                return;
            }

            Size fixedSize = this.SizeFromClientSize(this.ClientSize);
            this.MinimumSize = fixedSize;
            this.MaximumSize = fixedSize;
            this.AutoScrollMinSize = Size.Empty;
            this.calibrationDialogSizeLocked = true;
        }

        private void SpeedtestLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            LinkLabel linkLabel = sender as LinkLabel;
            string targetUrl = linkLabel != null ? linkLabel.Tag as string : null;
            if (string.IsNullOrWhiteSpace(targetUrl))
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(targetUrl)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce(
                    "calibration-speedtest-open-failed-" + targetUrl,
                    string.Format("Speedtest-Link konnte nicht geöffnet werden: {0}", targetUrl),
                    ex);
            }
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            if (this.calibrationTimer.Enabled)
            {
                return;
            }

            if (!this.selectedAdapterAvailable)
            {
                this.UpdateAdapterAvailabilityUi();
                return;
            }

            AdapterListItem selectedItem = this.adapterComboBox.SelectedItem as AdapterListItem;
            string adapterId = string.Empty;
            string adapterName = string.Empty;

            if (selectedItem != null)
            {
                adapterId = selectedItem.Id;
                adapterName = selectedItem.Name;
            }

            this.activeSettings = new MonitorSettings(
                adapterId,
                adapterName,
                this.currentSettings.CalibrationPeakBytesPerSecond,
                this.currentSettings.CalibrationDownloadPeakBytesPerSecond,
                this.currentSettings.CalibrationUploadPeakBytesPerSecond,
                this.currentSettings.InitialCalibrationPromptHandled,
                this.currentSettings.InitialLanguagePromptHandled,
                this.currentSettings.TransparencyPercent,
                this.currentSettings.LanguageCode,
                this.currentSettings.HasSavedPopupLocation,
                this.currentSettings.PopupLocationX,
                this.currentSettings.PopupLocationY,
                this.currentSettings.PopupScalePercent,
                this.currentSettings.PanelSkinId,
                this.currentSettings.PopupDisplayMode,
                this.currentSettings.PopupSectionMode,
                this.currentSettings.RotatingMeterGlossEnabled,
                this.currentSettings.TaskbarIntegrationEnabled,
                this.currentSettings.ActivityBorderGlowEnabled,
                this.currentSettings.TaskbarPopupSectionMode);

            this.lastSnapshot = NetworkSnapshot.Capture(this.activeSettings);
            if (!this.lastSnapshot.HasAdapters)
            {
                AppLog.WarnOnce(
                    "calibration-start-without-adapters-" + GetCalibrationAdapterLogKey(this.activeSettings),
                    string.Format(
                        "Calibration started without an initial adapter snapshot for '{0}'. Measurement will continue and may recover if the adapter becomes available.",
                        this.activeSettings.GetAdapterDisplayName()));
            }
            this.lastSampleUtc = DateTime.UtcNow;
            this.peakBytesPerSecond = 0D;
            this.peakDownloadBytesPerSecond = 0D;
            this.peakUploadBytesPerSecond = 0D;
            this.elapsedSeconds = 0;
            this.SelectedSettings = null;
            this.allowClose = false;
            this.progressBar.Value = 0;
            this.statusLabel.Text = UiLanguage.Get("Calibration.RunningInitial", "Kalibration läuft... 0 / 30 s");
            this.startButton.Enabled = false;
            this.SetCalibrationButtonText(this.startButton, UiLanguage.Get("Calibration.Start", "Starten"));
            this.saveButton.Enabled = false;
            this.saveAdapterButton.Enabled = false;
            this.adapterComboBox.Enabled = false;
            this.AcceptButton = this.startButton;
            this.calibrationTimer.Start();
        }

        private void SaveAdapterButton_Click(object sender, EventArgs e)
        {
            if (!this.selectedAdapterAvailable)
            {
                this.UpdateAdapterAvailabilityUi();
                return;
            }

            this.currentSettings = this.CreateSettingsFromSelectedAdapter(
                this.currentSettings.CalibrationPeakBytesPerSecond,
                this.currentSettings.CalibrationDownloadPeakBytesPerSecond,
                this.currentSettings.CalibrationUploadPeakBytesPerSecond);
            this.currentSettings.Save();
            this.SavedAdapterSettings = this.currentSettings.Clone();

            if (this.isInitialCalibrationRequired && !this.currentSettings.HasCalibrationData())
            {
                this.SelectedSettings = null;
                this.allowClose = false;
                this.statusLabel.Text = UiLanguage.Format(
                    "Calibration.AdapterSavedStatus",
                    "Internetverbindung gespeichert: {0}. Bitte Kalibration abschließen, mit 'Speichern' bestätigen oder mit 'Abbrechen' schließen.",
                    this.currentSettings.GetAdapterDisplayName());
                this.startButton.Enabled = this.selectedAdapterAvailable;
                this.saveAdapterButton.Enabled = this.selectedAdapterAvailable;
                this.saveButton.Enabled = false;
                this.adapterComboBox.Enabled = true;
                this.AcceptButton = this.startButton;
                this.ActiveControl = this.startButton;
                return;
            }

            this.SelectedSettings = this.currentSettings.Clone();
            this.allowClose = false;
            this.statusLabel.Text = UiLanguage.Format(
                "Calibration.AdapterSavedStatus",
                "Internetverbindung gespeichert: {0}. Das Fenster bleibt für Kalibration, Speichern oder Abbrechen geöffnet.",
                this.currentSettings.GetAdapterDisplayName());
            this.startButton.Enabled = this.selectedAdapterAvailable && !this.calibrationTimer.Enabled;
            this.saveAdapterButton.Enabled = this.selectedAdapterAvailable && !this.calibrationTimer.Enabled;
            this.adapterComboBox.Enabled = !this.calibrationTimer.Enabled;
            this.AcceptButton = this.saveButton.Enabled ? this.saveButton : this.startButton;
            this.ActiveControl = this.saveButton.Enabled ? (Control)this.saveButton : this.startButton;
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            this.SelectedSettings = null;
            this.allowClose = true;
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            if (this.SelectedSettings == null)
            {
                return;
            }

            this.allowClose = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        private void CalibrationTimer_Tick(object sender, EventArgs e)
        {
            if (this.activeSettings == null)
            {
                AppLog.WarnOnce(
                    "calibration-tick-without-active-settings",
                    "Calibration tick occurred without active settings. Calibration was stopped and reset to a ready state.");
                this.calibrationTimer.Stop();
                this.lastSampleUtc = DateTime.MinValue;
                this.progressBar.Value = 0;
                this.statusLabel.Text = UiLanguage.Get(
                    "Calibration.ReadyStatus",
                    "Bereit für die Kalibration. Bitte mit Starten beginnen und später mit Speichern bestätigen oder mit Abbrechen schließen.");
                this.startButton.Enabled = true;
                this.SetCalibrationButtonText(this.startButton, UiLanguage.Get("Calibration.Start", "Starten"));
                this.saveButton.Enabled = false;
                this.saveAdapterButton.Enabled = true;
                this.adapterComboBox.Enabled = true;
                this.AcceptButton = this.startButton;
                this.ActiveControl = this.startButton;
                return;
            }

            this.elapsedSeconds++;
            this.progressBar.Value = Math.Min(CalibrationDurationSeconds, this.elapsedSeconds);
            this.statusLabel.Text = UiLanguage.Format(
                "Calibration.RunningStatus",
                "Kalibration laeuft... {0} / 30 s | DL {1} | UL {2}",
                this.elapsedSeconds,
                TrafficRateFormatter.FormatSpeed(this.peakDownloadBytesPerSecond),
                TrafficRateFormatter.FormatSpeed(this.peakUploadBytesPerSecond));

            if (this.elapsedSeconds >= CalibrationDurationSeconds)
            {
                this.FinishCalibration();
                return;
            }

            if (this.isCapturing)
            {
                return;
            }

            MonitorSettings captureSettings = this.activeSettings.Clone();
            this.isCapturing = true;
            Task.Run(() =>
            {
                try
                {
                    NetworkSnapshot snapshot = NetworkSnapshot.Capture(captureSettings);
                    DateTime nowUtc = DateTime.UtcNow;
                    this.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (!snapshot.HasAdapters)
                            {
                                AppLog.WarnOnce(
                                    "calibration-snapshot-unavailable-" + GetCalibrationAdapterLogKey(captureSettings),
                                    string.Format(
                                        "Calibration snapshot is temporarily unavailable for {0}. Existing measurement state will be preserved.",
                                        captureSettings.GetAdapterDisplayName()));
                            }

                            if (this.lastSampleUtc != DateTime.MinValue && this.lastSnapshot.HasAdapters && snapshot.HasAdapters)
                            {
                                double elapsed = (nowUtc - this.lastSampleUtc).TotalSeconds;
                                if (elapsed > 0.1D)
                                {
                                    long downloadDiff = snapshot.BytesReceived - this.lastSnapshot.BytesReceived;
                                    long uploadDiff = snapshot.BytesSent - this.lastSnapshot.BytesSent;
                                    long totalDiff = snapshot.TotalBytes - this.lastSnapshot.TotalBytes;
                                    double downloadBytesPerSecond = Math.Max(0L, downloadDiff) / elapsed;
                                    double uploadBytesPerSecond = Math.Max(0L, uploadDiff) / elapsed;
                                    double totalBytesPerSecond = Math.Max(0L, totalDiff) / elapsed;
                                    if (downloadBytesPerSecond > this.peakDownloadBytesPerSecond)
                                    {
                                        this.peakDownloadBytesPerSecond = downloadBytesPerSecond;
                                    }
                                    if (uploadBytesPerSecond > this.peakUploadBytesPerSecond)
                                    {
                                        this.peakUploadBytesPerSecond = uploadBytesPerSecond;
                                    }
                                    if (totalBytesPerSecond > this.peakBytesPerSecond)
                                    {
                                        this.peakBytesPerSecond = totalBytesPerSecond;
                                    }
                                }
                            }

                            this.lastSnapshot = snapshot;
                            this.lastSampleUtc = nowUtc;
                            this.statusLabel.Text = UiLanguage.Format(
                                "Calibration.RunningStatus",
                                "Kalibration laeuft... {0} / 30 s | DL {1} | UL {2}",
                                this.elapsedSeconds,
                                TrafficRateFormatter.FormatSpeed(this.peakDownloadBytesPerSecond),
                                TrafficRateFormatter.FormatSpeed(this.peakUploadBytesPerSecond));
                        }
                        catch
                        {
                            System.Diagnostics.Trace.WriteLine("[TrafficView] CalibrationForm Capture-Task-Verarbeitung schlug fehl.");
                        }
                        finally
                        {
                            this.isCapturing = false;
                        }
                    }));
                }
                catch
                {
                    this.BeginInvoke(new Action(() => { this.isCapturing = false; }));
                }
            });
        }
        private void FinishCalibration()
        {
            this.calibrationTimer.Stop();

            double storedPeak = NormalizeCalibrationPeak(this.peakBytesPerSecond);
            if (storedPeak <= 0D)
            {
                storedPeak = NormalizeCalibrationPeak(this.currentSettings.CalibrationPeakBytesPerSecond);
            }

            double storedDownloadPeak = NormalizeCalibrationPeak(this.peakDownloadBytesPerSecond);
            if (storedDownloadPeak <= 0D)
            {
                storedDownloadPeak = NormalizeCalibrationPeak(this.currentSettings.CalibrationDownloadPeakBytesPerSecond);
                if (storedDownloadPeak <= 0D)
                {
                    storedDownloadPeak = storedPeak;
                }
            }

            double storedUploadPeak = NormalizeCalibrationPeak(this.peakUploadBytesPerSecond);
            if (storedUploadPeak <= 0D)
            {
                storedUploadPeak = NormalizeCalibrationPeak(this.currentSettings.CalibrationUploadPeakBytesPerSecond);
                if (storedUploadPeak <= 0D)
                {
                    storedUploadPeak = storedPeak;
                }
            }

            if (storedPeak <= 0D && storedDownloadPeak <= 0D && storedUploadPeak <= 0D)
            {
                AppLog.WarnOnce(
                    "calibration-finished-without-traffic-" + GetCalibrationAdapterLogKey(this.activeSettings),
                    string.Format(
                        "Calibration finished without measurable traffic for '{0}'. Saving would keep zero-valued calibration data.",
                        this.activeSettings != null
                            ? this.activeSettings.GetAdapterDisplayName()
                            : this.currentSettings.GetAdapterDisplayName()));
            }

            this.SelectedSettings = new MonitorSettings(
                this.activeSettings != null ? this.activeSettings.AdapterId : this.currentSettings.AdapterId,
                this.activeSettings != null ? this.activeSettings.AdapterName : this.currentSettings.AdapterName,
                storedPeak,
                storedDownloadPeak,
                storedUploadPeak,
                this.currentSettings.InitialCalibrationPromptHandled,
                this.currentSettings.InitialLanguagePromptHandled,
                this.currentSettings.TransparencyPercent,
                this.currentSettings.LanguageCode,
                this.currentSettings.HasSavedPopupLocation,
                this.currentSettings.PopupLocationX,
                this.currentSettings.PopupLocationY,
                this.currentSettings.PopupScalePercent,
                this.currentSettings.PanelSkinId,
                this.currentSettings.PopupDisplayMode,
                this.currentSettings.PopupSectionMode,
                this.currentSettings.RotatingMeterGlossEnabled,
                this.currentSettings.TaskbarIntegrationEnabled,
                this.currentSettings.ActivityBorderGlowEnabled,
                this.currentSettings.TaskbarPopupSectionMode);
            this.statusLabel.Text = UiLanguage.Format(
                "Calibration.CompletedStatus",
                "Kalibration abgeschlossen. DL {0} | UL {1}. Mit 'Speichern' bestätigen.",
                TrafficRateFormatter.FormatSpeed(storedDownloadPeak),
                TrafficRateFormatter.FormatSpeed(storedUploadPeak));
            this.startButton.Enabled = true;
            this.SetCalibrationButtonText(this.startButton, UiLanguage.Get("Calibration.Remeasure", "Neu messen"));
            this.saveAdapterButton.Enabled = true;
            this.adapterComboBox.Enabled = true;
            this.saveButton.Enabled = true;
            this.rootLayout.PerformLayout();
            this.AcceptButton = this.saveButton;
            this.ActiveControl = this.saveButton;
        }

        private static double NormalizeCalibrationPeak(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0D)
            {
                return 0D;
            }

            return value;
        }

        private static string GetCalibrationAdapterLogKey(MonitorSettings settings)
        {
            if (settings == null)
            {
                return "unknown";
            }

            if (!string.IsNullOrWhiteSpace(settings.AdapterId))
            {
                return settings.AdapterId;
            }

            if (!string.IsNullOrWhiteSpace(settings.AdapterName))
            {
                return settings.AdapterName;
            }

            return "automatic";
        }

        private void AdjustDialogLayout()
        {
            Rectangle workingArea = Screen.FromControl(this).WorkingArea;
            int layoutScreenMargin = Math.Max(80, this.Font.Height * 4);
            int widthReserve = this.calibrationDialogWidthReserve;
            int heightReserve = this.calibrationDialogHeightReserve;
            int minimumContentWidth = this.calibrationContentMinimumWidth;
            int wrapMinimumWidth = this.calibrationFooterWrapMinimumWidth;
            int speedtestMinimumContentWidth = this.calibrationFooterContentMinimumWidth;
            int minimumClientWidth = Math.Max(580, this.rootLayout.Padding.Horizontal + minimumContentWidth);
            int minimumClientHeight = Math.Max(92, this.rootLayout.Padding.Vertical + (this.Font.Height * 4));
            int maxClientWidth = Math.Max(520, workingArea.Width - layoutScreenMargin);
            int maxClientHeight = Math.Max(192, workingArea.Height - layoutScreenMargin);
            int targetWidth = Math.Min(Math.Max(minimumClientWidth, this.ClientSize.Width), maxClientWidth);
            string currentStatusText = this.statusLabel.Text;
            this.statusLabel.Text = this.GetCalibrationMeasurementStatusText();
            Size preferred = Size.Empty;
            int buttonPanelWidth = 0;

            for (int pass = 0; pass < 2; pass++)
            {
                int wrapWidth = Math.Max(wrapMinimumWidth, targetWidth - this.rootLayout.Padding.Horizontal);
                int speedtestContentWidth = wrapWidth;
                if (this.calibrationLogoControl != null)
                {
                    speedtestContentWidth = Math.Max(
                        speedtestMinimumContentWidth,
                        wrapWidth - this.calibrationLogoControl.Width - this.calibrationLogoControl.Margin.Horizontal);
                }
                this.infoLabel.MaximumSize = new Size(wrapWidth, 0);
                this.statusLabel.MaximumSize = new Size(wrapWidth, 0);
                this.speedtestHintLabel.MaximumSize = new Size(speedtestContentWidth, 0);
                this.speedtestLinksPanel.MaximumSize = new Size(speedtestContentWidth, int.MaxValue);
                this.speedtestNetLinkLabel.MaximumSize = new Size(speedtestContentWidth, 0);
                this.wieIstMeineIpLinkLabel.MaximumSize = new Size(speedtestContentWidth, 0);
                this.rootLayout.PerformLayout();

                preferred = this.rootLayout.GetPreferredSize(new Size(targetWidth, 0));
                buttonPanelWidth = this.calibrationButtonPanel.GetPreferredSize(Size.Empty).Width;
                int requiredWidth = Math.Min(
                    maxClientWidth,
                    Math.Max(
                        minimumClientWidth,
                        Math.Max(
                            preferred.Width + (widthReserve * 2),
                            buttonPanelWidth + this.rootLayout.Padding.Horizontal + widthReserve)));
                if (requiredWidth <= targetWidth)
                {
                    break;
                }

                targetWidth = requiredWidth;
            }

            int targetHeight = Math.Min(
                Math.Max(this.rootLayout.Padding.Vertical, preferred.Height + heightReserve),
                maxClientHeight);

            this.ClientSize = new Size(targetWidth, targetHeight);
            this.rootLayout.PerformLayout();
            int confirmedPreferredWidth = this.rootLayout.GetPreferredSize(new Size(0, this.ClientSize.Height)).Width;
            int confirmedPreferredHeight = this.rootLayout.GetPreferredSize(new Size(targetWidth, 0)).Height;
            int requiredClientWidthFromLayout = Math.Min(
                maxClientWidth,
                Math.Max(
                    minimumClientWidth,
                    Math.Max(
                        confirmedPreferredWidth + this.rootLayout.Padding.Horizontal + widthReserve,
                        buttonPanelWidth + this.rootLayout.Padding.Horizontal + widthReserve)));
            if (requiredClientWidthFromLayout > this.ClientSize.Width)
            {
                this.ClientSize = new Size(requiredClientWidthFromLayout, this.ClientSize.Height);
            }

            int confirmedClientWidth = this.ClientSize.Width;
            int requiredClientHeightFromLayout = Math.Min(
                maxClientHeight,
                Math.Max(
                    minimumClientHeight,
                    this.rootLayout.GetPreferredSize(new Size(confirmedClientWidth, 0)).Height + heightReserve));
            if (requiredClientHeightFromLayout > this.ClientSize.Height)
            {
                this.ClientSize = new Size(this.ClientSize.Width, requiredClientHeightFromLayout);
            }

            this.rootLayout.PerformLayout();
            int requiredClientWidthFromVisibleControls = Math.Max(
                minimumClientWidth,
                Math.Max(
                    this.calibrationButtonPanel.Right + this.calibrationButtonPanel.Margin.Right,
                    this.calibrationSpeedtestPanel.Right + this.calibrationSpeedtestPanel.Margin.Right) + this.rootLayout.Padding.Right);
            if (requiredClientWidthFromVisibleControls > this.ClientSize.Width)
            {
                this.ClientSize = new Size(requiredClientWidthFromVisibleControls, this.ClientSize.Height);
                this.rootLayout.PerformLayout();
            }

            int requiredClientHeightFromVisibleControls = Math.Max(
                minimumClientHeight,
                Math.Max(
                    this.calibrationButtonPanel.Bottom + this.calibrationButtonPanel.Margin.Bottom,
                    this.calibrationSpeedtestPanel.Bottom + this.calibrationSpeedtestPanel.Margin.Bottom) + this.rootLayout.Padding.Bottom);
            if (requiredClientHeightFromVisibleControls != this.ClientSize.Height)
            {
                this.ClientSize = new Size(this.ClientSize.Width, requiredClientHeightFromVisibleControls);
            }

            this.AutoScrollMinSize = Size.Empty;
            this.statusLabel.Text = currentStatusText;
            this.rootLayout.PerformLayout();
        }

        private string GetCalibrationMeasurementStatusText()
        {
            string adapterDisplayName = this.currentSettings != null
                ? this.currentSettings.GetAdapterDisplayName()
                : UiLanguage.Get("Monitoring.AdapterAutomatic", "Automatisch");

            string[] samples = new string[]
            {
                UiLanguage.Get(
                    "Calibration.ReadyStatus",
                    "Bereit für die Kalibration. Bitte mit 'Starten' beginnen und später mit 'Speichern' bestätigen oder mit 'Abbrechen' schließen."),
                UiLanguage.Format(
                    "Calibration.AdapterSavedStatus",
                    "Internetverbindung gespeichert: {0}. Bitte Kalibration abschließen, mit 'Speichern' bestätigen oder mit 'Abbrechen' schließen.",
                    adapterDisplayName),
                UiLanguage.Format(
                    "Calibration.RunningStatus",
                    "Kalibration läuft... {0} / 30 s | DL {1} | UL {2}",
                    CalibrationDurationSeconds,
                    "999,9 MB/s",
                    "999,9 MB/s"),
                UiLanguage.Format(
                    "Calibration.CompletedStatus",
                    "Kalibration abgeschlossen. DL {0} | UL {1}. Mit 'Speichern' bestätigen.",
                    "999,9 MB/s",
                    "999,9 MB/s")
            };

            string longest = samples[0];
            for (int i = 1; i < samples.Length; i++)
            {
                if (samples[i].Length > longest.Length)
                {
                    longest = samples[i];
                }
            }

            return longest;
        }

        private MonitorSettings CreateSettingsFromSelectedAdapter(
            double calibrationPeakBytesPerSecond,
            double calibrationDownloadPeakBytesPerSecond,
            double calibrationUploadPeakBytesPerSecond)
        {
            AdapterListItem selectedItem = this.adapterComboBox.SelectedItem as AdapterListItem;
            if (selectedItem == null)
            {
                return this.currentSettings;
            }

            string adapterId = string.Empty;
            string adapterName = string.Empty;

            adapterId = selectedItem.Id;
            adapterName = selectedItem.Name;

            return new MonitorSettings(
                adapterId,
                adapterName,
                calibrationPeakBytesPerSecond,
                calibrationDownloadPeakBytesPerSecond,
                calibrationUploadPeakBytesPerSecond,
                this.currentSettings.InitialCalibrationPromptHandled,
                this.currentSettings.InitialLanguagePromptHandled,
                this.currentSettings.TransparencyPercent,
                this.currentSettings.LanguageCode,
                this.currentSettings.HasSavedPopupLocation,
                this.currentSettings.PopupLocationX,
                this.currentSettings.PopupLocationY,
                this.currentSettings.PopupScalePercent,
                this.currentSettings.PanelSkinId,
                this.currentSettings.PopupDisplayMode,
                this.currentSettings.PopupSectionMode,
                this.currentSettings.RotatingMeterGlossEnabled,
                this.currentSettings.TaskbarIntegrationEnabled,
                this.currentSettings.ActivityBorderGlowEnabled,
                this.currentSettings.TaskbarPopupSectionMode);
        }

        private void LoadAdapterItems()
        {
            List<AdapterListItem> items = NetworkSnapshot.GetAdapterItems();
            AdapterListItem automaticItem = new AdapterListItem(
                MonitorSettings.AutomaticAdapterId,
                string.Empty,
                UiLanguage.Get("Calibration.AdapterAutomatic", "Automatisch (bevorzugt LAN, sonst WLAN)"),
                true);
            this.adapterComboBox.Items.Add(automaticItem);

            int selectedIndex = this.currentSettings.UsesAutomaticAdapterSelection() ? 0 : -1;
            bool selectedAdapterFound = this.currentSettings.UsesAutomaticAdapterSelection();

            for (int i = 0; i < items.Count; i++)
            {
                AdapterListItem item = items[i];
                this.adapterComboBox.Items.Add(item);

                if (!string.IsNullOrEmpty(this.currentSettings.AdapterId) &&
                    string.Equals(item.Id, this.currentSettings.AdapterId, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i + 1;
                    selectedAdapterFound = true;
                }
            }

            if (!selectedAdapterFound &&
                !string.IsNullOrEmpty(this.currentSettings.AdapterId))
            {
                AdapterListItem missingItem = new AdapterListItem(
                    this.currentSettings.AdapterId,
                    this.currentSettings.AdapterName,
                    UiLanguage.Format(
                        "Calibration.AdapterMissingItem",
                        "{0} (nicht verfügbar)",
                        this.currentSettings.GetAdapterDisplayName()),
                    false);
                this.adapterComboBox.Items.Add(missingItem);
                selectedIndex = this.adapterComboBox.Items.Count - 1;
            }
            else if (!selectedAdapterFound && this.adapterComboBox.Items.Count > 0 && selectedIndex < 0)
            {
                selectedIndex = 0;
            }

            if (selectedIndex >= 0 && selectedIndex < this.adapterComboBox.Items.Count)
            {
                this.adapterComboBox.SelectedIndex = selectedIndex;
            }
            else
            {
                this.adapterComboBox.SelectedIndex = -1;
            }
        }

        private void AdapterComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.UpdateAdapterAvailabilityUi();
        }

        private void UpdateAdapterAvailabilityUi()
        {
            AdapterListItem selectedItem = this.adapterComboBox.SelectedItem as AdapterListItem;
            this.selectedAdapterAvailable = selectedItem != null && selectedItem.IsAvailable;

            if (this.calibrationTimer.Enabled)
            {
                return;
            }

            this.startButton.Enabled = this.selectedAdapterAvailable;
            this.saveAdapterButton.Enabled = this.selectedAdapterAvailable;

            if (selectedItem == null)
            {
                this.statusLabel.Text = UiLanguage.Get(
                    "Calibration.AdapterSelectionRequired",
                    "Bitte eine aktive Internetverbindung auswählen.");
                return;
            }

            if (!selectedItem.IsAvailable)
            {
                this.statusLabel.Text = UiLanguage.Format(
                    "Calibration.AdapterUnavailableStatus",
                    "Die gespeicherte Internetverbindung '{0}' ist derzeit nicht verfügbar. Bitte eine aktive Internetverbindung oder 'Automatisch' auswählen.",
                    this.currentSettings.GetAdapterDisplayName());
                return;
            }

            if (!this.saveButton.Enabled)
            {
                this.statusLabel.Text = UiLanguage.Get(
                    "Calibration.ReadyStatus",
                    "Bereit für die Kalibration. Bitte mit 'Starten' beginnen und später mit 'Speichern' bestätigen oder mit 'Abbrechen' schließen.");
            }
        }

    }

}

