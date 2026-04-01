using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: AssemblyTitle("TrafficView")]
[assembly: AssemblyDescription("Kleines Trayfenster fuer Upload- und Download-Werte.")]
[assembly: AssemblyCompany("Codex")]
[assembly: AssemblyProduct("TrafficView")]
[assembly: AssemblyCopyright("Copyright (c) 2026")]
[assembly: AssemblyVersion("1.4.2.0")]
[assembly: AssemblyFileVersion("1.4.2.0")]

namespace TrafficView
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            try
            {
                AppLog.Info(string.Format(
                    "Session started. Version={0}; OS={1}; 64BitOS={2}; Machine={3}",
                    typeof(Program).Assembly.GetName().Version,
                    Environment.OSVersion.VersionString,
                    Environment.Is64BitOperatingSystem ? "yes" : "no",
                    Environment.MachineName));
                DpiHelper.EnableHighDpiSupport();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrafficViewContext());
            }
            catch (Exception ex)
            {
                AppLog.Error("Application terminated during startup/bootstrap.", ex);
                throw;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public Rectangle ToRectangle()
        {
            return Rectangle.FromLTRB(this.Left, this.Top, this.Right, this.Bottom);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativePoint
    {
        public int X;
        public int Y;

        public NativePoint(int x, int y)
        {
            this.X = x;
            this.Y = y;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeSize
    {
        public int CX;
        public int CY;

        public NativeSize(int cx, int cy)
        {
            this.CX = cx;
            this.CY = cy;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct BlendFunction
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    internal sealed class TrafficViewContext : ApplicationContext
    {
        private enum SharedMenuOpenSource
        {
            None,
            OverlayLeftClick,
            TrayRightClick
        }

        private readonly TrafficPopupForm popupForm;
        private readonly NotifyIcon notifyIcon;
        private readonly ToolStripMenuItem toggleItem;
        private readonly ToolStripMenuItem calibrationStatusItem;
        private readonly ToolStripMenuItem calibrationItem;
        private readonly ToolStripMenuItem transparencyItem;
        private readonly ToolStripMenuItem sizeItem;
        private readonly ToolStripMenuItem languageItem;
        private readonly ToolStripMenuItem exitItem;
        private readonly ContextMenuStrip sharedMenu;
        private readonly Icon notifyIconHandle;
        private readonly Dictionary<string, ToolStripMenuItem> languageMenuItems;
        private readonly Dictionary<int, ToolStripMenuItem> popupScaleMenuItems;
        private SharedMenuOpenSource sharedMenuOpenSource;
        private MonitorSettings settings;

        public TrafficViewContext()
        {
            this.settings = MonitorSettings.Load();
            UiLanguage.Initialize(this.settings.LanguageCode);
            this.popupForm = new TrafficPopupForm(this.settings);
            this.popupForm.RatesUpdated += this.PopupForm_RatesUpdated;
            this.popupForm.OverlayMenuRequested += this.PopupForm_OverlayMenuRequested;
            this.popupForm.OverlayLocationCommitted += this.PopupForm_OverlayLocationCommitted;
            this.languageMenuItems = new Dictionary<string, ToolStripMenuItem>(StringComparer.OrdinalIgnoreCase);
            this.popupScaleMenuItems = new Dictionary<int, ToolStripMenuItem>();

            this.sharedMenu = new ContextMenuStrip();
            this.sharedMenu.RenderMode = ToolStripRenderMode.System;
            this.sharedMenu.ShowImageMargin = false;
            this.sharedMenu.Font = new Font(
                SystemFonts.MenuFont.FontFamily,
                Math.Max(10F, SystemFonts.MenuFont.Size),
                FontStyle.Regular,
                GraphicsUnit.Point);
            this.toggleItem = new ToolStripMenuItem(string.Empty, null, this.ToggleItem_Click);
            this.calibrationStatusItem = new ToolStripMenuItem(string.Empty);
            this.calibrationStatusItem.Enabled = false;
            this.calibrationItem = new ToolStripMenuItem(string.Empty, null, this.CalibrationItem_Click);
            this.transparencyItem = new ToolStripMenuItem(string.Empty, null, this.TransparencyItem_Click);
            this.sizeItem = new ToolStripMenuItem(string.Empty);
            this.languageItem = new ToolStripMenuItem(string.Empty);
            this.exitItem = new ToolStripMenuItem(string.Empty, null, this.ExitItem_Click);

            LanguageOption[] languages = UiLanguage.GetSupportedLanguages();
            for (int i = 0; i < languages.Length; i++)
            {
                ToolStripMenuItem item = new ToolStripMenuItem(languages[i].DisplayName, null, this.LanguageMenuItem_Click);
                item.Tag = languages[i].Code;
                this.languageMenuItems[languages[i].Code] = item;
                this.languageItem.DropDownItems.Add(item);
            }

            int[] popupScalePercents = MonitorSettings.GetSupportedPopupScalePercents();
            for (int i = 0; i < popupScalePercents.Length; i++)
            {
                ToolStripMenuItem item = new ToolStripMenuItem(string.Empty, null, this.PopupScaleMenuItem_Click);
                item.Tag = popupScalePercents[i];
                this.popupScaleMenuItems[popupScalePercents[i]] = item;
                this.sizeItem.DropDownItems.Add(item);
            }

            this.sharedMenu.Items.Add(this.toggleItem);
            this.sharedMenu.Items.Add(this.calibrationStatusItem);
            this.sharedMenu.Items.Add(this.calibrationItem);
            this.sharedMenu.Items.Add(this.transparencyItem);
            this.sharedMenu.Items.Add(this.sizeItem);
            this.sharedMenu.Items.Add(this.languageItem);
            this.sharedMenu.Items.Add(new ToolStripSeparator());
            this.sharedMenu.Items.Add(this.exitItem);
            this.sharedMenu.Opening += this.Menu_Opening;
            this.sharedMenu.Closed += this.Menu_Closed;
            this.UpdateMenuState();

            this.notifyIconHandle = AppIconFactory.CreateAppIcon();
            this.notifyIcon = new NotifyIcon();
            this.notifyIcon.Icon = this.notifyIconHandle;
            this.notifyIcon.Visible = true;
            this.notifyIcon.Text = "TrafficView";
            this.notifyIcon.MouseUp += this.NotifyIcon_MouseUp;

            EventHandler showOnStartup = null;
            showOnStartup = delegate(object sender, EventArgs e)
            {
                Application.Idle -= showOnStartup;
                try
                {
                    this.PromptInitialLanguageOnFirstStart();
                    this.BringPopupToFront();
                    this.PromptInitialCalibrationOnFirstStart();
                    this.BringPopupToFront();
                }
                catch (Exception ex)
                {
                    AppLog.Error("Startup prompt sequence failed; continuing with degraded startup behavior.", ex);
                }
            };

            Application.Idle += showOnStartup;
        }

        protected override void ExitThreadCore()
        {
            this.notifyIcon.Visible = false;
            this.notifyIcon.Dispose();
            this.sharedMenu.Dispose();
            this.popupForm.Dispose();
            this.notifyIconHandle.Dispose();
            base.ExitThreadCore();
        }

        private void Menu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (this.sharedMenuOpenSource == SharedMenuOpenSource.None)
            {
                e.Cancel = true;
                return;
            }

            this.popupForm.SuspendTopMostEnforcement();
            this.UpdateMenuState();
        }

        private void Menu_Closed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            this.sharedMenuOpenSource = SharedMenuOpenSource.None;
            this.popupForm.ResumeTopMostEnforcement(false);
        }

        private void ToggleItem_Click(object sender, EventArgs e)
        {
            this.ToggleWindow();
        }

        private void CalibrationItem_Click(object sender, EventArgs e)
        {
            this.ShowCalibrationDialog(this.popupForm.Visible);
        }

        private void TransparencyItem_Click(object sender, EventArgs e)
        {
            using (TransparencyForm transparencyForm = new TransparencyForm(
                this.settings.TransparencyPercent,
                this.ApplyTransparencySetting))
            {
                bool pausePopupTopMost = this.popupForm.Visible;
                if (pausePopupTopMost)
                {
                    this.popupForm.SuspendTopMostEnforcement();
                }

                try
                {
                    transparencyForm.ShowDialog();
                }
                finally
                {
                    if (pausePopupTopMost)
                    {
                        this.popupForm.ResumeTopMostEnforcement(false);
                    }
                }

                if (this.popupForm.Visible)
                {
                    this.popupForm.BringToFrontOnly();
                }
            }
        }

        private void ApplyTransparencySetting(int transparencyPercent)
        {
            this.settings = this.settings.WithTransparencyPercent(transparencyPercent);
            this.settings.Save();
            this.popupForm.ApplySettings(this.settings);
            this.UpdateMenuState();
        }

        private void PopupScaleMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            if (item == null || item.Tag == null)
            {
                return;
            }

            int popupScalePercent;
            if (!int.TryParse(item.Tag.ToString(), out popupScalePercent))
            {
                return;
            }

            this.ApplyPopupScalePercent(popupScalePercent);
        }

        private void ApplyPopupScalePercent(int popupScalePercent)
        {
            if (this.settings.PopupScalePercent == popupScalePercent)
            {
                return;
            }

            this.settings = this.settings.WithPopupScalePercent(popupScalePercent);
            this.popupForm.ApplySettings(this.settings);

            if (this.popupForm.Visible)
            {
                this.settings = this.settings.WithPopupLocation(this.popupForm.Location);
                this.popupForm.BringToFrontOnly();
            }

            this.settings.Save();
            this.UpdateMenuState();
        }

        private void LanguageMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            string languageCode = item != null ? item.Tag as string : null;
            if (string.IsNullOrEmpty(languageCode))
            {
                return;
            }

            this.settings = this.settings.WithLanguageCode(languageCode);
            this.settings.Save();
            UiLanguage.SetLanguage(this.settings.LanguageCode);
            this.UpdateMenuState();
        }

        private void ExitItem_Click(object sender, EventArgs e)
        {
            this.ExitThread();
        }

        private void NotifyIcon_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                this.ToggleWindow(true);
                return;
            }

            if (e.Button == MouseButtons.Right)
            {
                this.ShowSharedMenuFromTrayRightClick();
            }
        }

        private void PopupForm_OverlayMenuRequested(object sender, EventArgs e)
        {
            this.ShowSharedMenuFromOverlayLeftClick();
        }

        private void PopupForm_OverlayLocationCommitted(object sender, EventArgs e)
        {
            Point popupLocation = this.popupForm.Location;
            if (this.settings.HasSavedPopupLocation &&
                this.settings.PopupLocation == popupLocation)
            {
                return;
            }

            this.settings = this.settings.WithPopupLocation(popupLocation);
            this.settings.Save();
        }

        private void ShowSharedMenuFromOverlayLeftClick()
        {
            if (this.sharedMenu == null || this.sharedMenu.Visible)
            {
                return;
            }

            this.sharedMenuOpenSource = SharedMenuOpenSource.OverlayLeftClick;
            this.sharedMenu.Show(Cursor.Position);
        }

        private void ShowSharedMenuFromTrayRightClick()
        {
            if (this.sharedMenu == null || this.sharedMenu.Visible)
            {
                return;
            }

            this.sharedMenuOpenSource = SharedMenuOpenSource.TrayRightClick;
            this.sharedMenu.Show(Cursor.Position);
        }

        private void ToggleWindow(bool fromTrayLeftClick = false)
        {
            if (this.popupForm.Visible)
            {
                this.popupForm.Hide();
                return;
            }

            if (fromTrayLeftClick)
            {
                this.popupForm.SuppressMenuTemporarily(350);
            }

            this.ShowPopupAtPreferredOrSavedLocation(null, true);
        }

        private void ShowCalibrationDialog(bool keepPopupVisible = false)
        {
            bool popupWasVisible = this.popupForm.Visible;
            Point popupRestoreLocation = this.popupForm.Location;
            bool shouldRestorePopup = popupWasVisible || keepPopupVisible;
            bool pausePopupTopMost = popupWasVisible && keepPopupVisible;
            MonitorSettings selectedSettings = null;

            if (popupWasVisible && !keepPopupVisible)
            {
                this.popupForm.Hide();
            }

            if (pausePopupTopMost)
            {
                this.popupForm.SuspendTopMostEnforcement();
            }

            try
            {
                using (CalibrationForm calibrationForm = new CalibrationForm(this.settings))
                {
                    if (calibrationForm.ShowDialog() == DialogResult.OK && calibrationForm.SelectedSettings != null)
                    {
                        selectedSettings = calibrationForm.SelectedSettings.Clone();
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

            if (selectedSettings != null)
            {
                this.settings = selectedSettings;
                this.settings = this.settings.WithInitialCalibrationPromptHandled(true);
                this.settings.Save();
                this.popupForm.ApplySettings(this.settings);
                this.UpdateMenuState();
            }

            if (shouldRestorePopup)
            {
                this.BringPopupToFront(popupWasVisible ? (Point?)popupRestoreLocation : null);
            }
        }

        private void BringPopupToFront(Point? restoreLocation = null)
        {
            this.ShowPopupAtPreferredOrSavedLocation(restoreLocation, false);

            this.popupForm.BeginInvoke(new Action(this.popupForm.BringToFrontOnly));
            this.SchedulePopupFrontRefresh(180, restoreLocation);
            this.SchedulePopupFrontRefresh(420, restoreLocation);
        }

        private void SchedulePopupFrontRefresh(int delayMilliseconds, Point? restoreLocation = null)
        {
            Timer timer = new Timer();
            timer.Interval = Math.Max(60, delayMilliseconds);
            EventHandler tickHandler = null;
            tickHandler = delegate(object sender, EventArgs e)
            {
                timer.Tick -= tickHandler;
                timer.Stop();
                timer.Dispose();

                if (this.popupForm.IsDisposed)
                {
                    return;
                }

                this.ShowPopupAtPreferredOrSavedLocation(restoreLocation, false);
            };
            timer.Tick += tickHandler;
            timer.Start();
        }

        private void ShowPopupAtPreferredOrSavedLocation(Point? restoreLocation, bool activateWindow)
        {
            Point? effectiveLocation = restoreLocation;
            if (!effectiveLocation.HasValue &&
                this.settings.HasSavedPopupLocation)
            {
                effectiveLocation = this.settings.PopupLocation;
            }

            if (effectiveLocation.HasValue)
            {
                this.popupForm.ShowAtLocation(effectiveLocation.Value, activateWindow);
                return;
            }

            this.popupForm.ShowNearTray(activateWindow);
        }

        private void PromptInitialCalibrationOnFirstStart()
        {
            if (this.settings.InitialCalibrationPromptHandled)
            {
                return;
            }

            this.ShowCalibrationDialog(true);
        }

        private void PromptInitialLanguageOnFirstStart()
        {
            if (this.settings.InitialLanguagePromptHandled)
            {
                return;
            }

            using (LanguageSelectionForm languageForm = new LanguageSelectionForm(this.settings.LanguageCode))
            {
                if (languageForm.ShowDialog() == DialogResult.OK)
                {
                    this.settings = this.settings
                        .WithLanguageCode(languageForm.SelectedLanguageCode)
                        .WithInitialLanguagePromptHandled(true);
                    this.settings.Save();
                    UiLanguage.SetLanguage(this.settings.LanguageCode);
                    this.popupForm.ApplySettings(this.settings);
                    this.UpdateMenuState();
                }
            }
        }

        private void PopupForm_RatesUpdated(object sender, RatesUpdatedEventArgs e)
        {
            string text = string.Format(
                UiLanguage.Get("Notify.Rates", "DL {0} | UL {1}"),
                TrafficPopupForm.FormatSpeed(e.DownloadBytesPerSecond),
                TrafficPopupForm.FormatSpeed(e.UploadBytesPerSecond));

            if (text.Length > 63)
            {
                text = text.Substring(0, 63);
            }

            this.notifyIcon.Text = text;
        }

        private void UpdateMenuState()
        {
            this.toggleItem.Text = this.popupForm.Visible
                ? UiLanguage.Get("Menu.Hide", "Ausblenden")
                : UiLanguage.Get("Menu.Show", "Anzeigen");
            this.calibrationItem.Text = UiLanguage.Get("Menu.Calibration", "Kalibration (30 s)...");
            this.transparencyItem.Text = UiLanguage.Format(
                "Menu.TransparencyFormat",
                "Transparenz ({0} %)",
                this.settings.TransparencyPercent);
            this.sizeItem.Text = UiLanguage.Format(
                "Menu.SizeFormat",
                "Groesse ({0} %)",
                this.settings.PopupScalePercent);
            this.languageItem.Text = UiLanguage.Get("Menu.Language", "Sprache");
            this.exitItem.Text = UiLanguage.Get("Menu.Exit", "Beenden");

            foreach (KeyValuePair<string, ToolStripMenuItem> pair in this.languageMenuItems)
            {
                pair.Value.Checked = string.Equals(pair.Key, this.settings.LanguageCode, StringComparison.OrdinalIgnoreCase);
            }

            foreach (KeyValuePair<int, ToolStripMenuItem> pair in this.popupScaleMenuItems)
            {
                pair.Value.Text = string.Format("{0} %", pair.Key);
                pair.Value.Checked = pair.Key == this.settings.PopupScalePercent;
            }

            if (this.settings.HasCalibrationData())
            {
                this.calibrationStatusItem.Text = UiLanguage.Get(
                    "Menu.CalibrationStatusSaved",
                    "Kalibrationsstatus: gespeichert");
                return;
            }

            if (this.settings.HasAdapterSelection())
            {
                this.calibrationStatusItem.Text = UiLanguage.Get(
                    "Menu.CalibrationStatusAdapterSelected",
                    "Kalibrationsstatus: Adapter gewaehlt");
                return;
            }

            this.calibrationStatusItem.Text = UiLanguage.Get(
                "Menu.CalibrationStatusOpen",
                "Kalibrationsstatus: offen");
        }
    }

    internal sealed class OverlayInputLabel : Label
    {
        private const int WmContextMenu = 0x007B;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmContextMenu)
            {
                m.Result = IntPtr.Zero;
                return;
            }

            base.WndProc(ref m);
        }
    }

    internal sealed class TrafficPopupForm : Form
    {
        private const int BaseClientWidth = 102;
        private const int BaseClientHeight = 56;
        private const int BaseWindowCornerRadius = 14;
        private const int BaseOuterInset = 2;
        private const int BaseSeparatorY = 28;
        private const int BaseSeparatorInset = 14;
        private const int BasePopupMargin = 10;
        private const int BaseCaptionX = 6;
        private const int BaseDownloadCaptionY = 9;
        private const int BaseDownloadValueX = 16;
        private const int BaseDownloadValueY = 7;
        private const int BaseUploadCaptionY = 32;
        private const int BaseUploadValueX = 16;
        private const int BaseUploadValueY = 30;
        private const int BaseCaptionWidth = 12;
        private const int BaseCaptionHeight = 8;
        private const int BaseValueWidth = 42;
        private const int BaseValueHeight = 14;
        private const int BaseMeterDiameter = 39;
        private const int BaseMeterRightInset = 5;
        private const int BaseDragThreshold = 4;
        private const int BasePopupVisibleMargin = 8;
        private const float BaseFormFontSize = 7.0F;
        private const float BaseCaptionFontSize = 6.0F;
        private const float BaseValueFontSize = 10.4F;
        private const int WmNclButtonDown = 0xA1;
        private const int HtCaption = 0x2;
        private const int WmContextMenu = 0x007B;
        private const int WmDpiChanged = 0x02E0;
        private const int WmDisplayChange = 0x007E;
        private const int WmSettingChange = 0x001A;
        private const int WsExLayered = 0x00080000;
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpShowWindow = 0x0040;
        private const uint SwpNoActivate = 0x0010;
        private const uint LwaAlpha = 0x2;
        private const int UlwAlpha = 0x2;
        private const byte AcSrcOver = 0x00;
        private const byte AcSrcAlpha = 0x01;
        private const int KeepTopMostRefreshIntervalMs = 1600;
        private static readonly IntPtr HwndTopMost = new IntPtr(-1);
        private static readonly Color BackgroundBlue = Color.FromArgb(18, 34, 82);
        private static readonly Color BorderColor = Color.FromArgb(18, 34, 82);
        private static readonly Color EdgeSmoothingColor = Color.FromArgb(120, 56, 84, 146);
        private static readonly Color BorderBevelHighlightColor = Color.FromArgb(132, 152, 184, 242);
        private static readonly Color BorderBevelShadowColor = Color.FromArgb(132, 8, 18, 46);
        private static readonly Color PanelSurfaceHighlightColor = Color.FromArgb(52, 128, 158, 220);
        private static readonly Color PanelSurfaceShadowColor = Color.FromArgb(42, 8, 16, 42);
        private static readonly Color PanelInnerEdgeHighlightColor = Color.FromArgb(76, 182, 208, 248);
        private static readonly Color PanelInnerEdgeShadowColor = Color.FromArgb(64, 8, 16, 40);
        private static readonly Color DividerColor = Color.FromArgb(46, 66, 116);
        private static readonly Color DownloadCaptionColor = Color.FromArgb(255, 220, 118);
        private static readonly Color DownloadValueColor = Color.FromArgb(255, 182, 28);
        private static readonly Color DownloadRingLowColor = Color.FromArgb(246, 144, 0);
        private static readonly Color DownloadRingHighColor = Color.FromArgb(255, 222, 84);
        private static readonly Color UploadCaptionColor = Color.FromArgb(170, 255, 164);
        private static readonly Color UploadValueColor = Color.FromArgb(72, 255, 96);
        private static readonly Color UploadRingLowColor = Color.FromArgb(88, 255, 116);
        private static readonly Color UploadRingHighColor = Color.FromArgb(214, 255, 214);
        private static readonly Color MeterTrackColor = Color.FromArgb(64, 94, 148);
        private static readonly Color MeterTrackInnerColor = Color.FromArgb(82, 112, 166);
        private static readonly Color MeterCenterColor = Color.FromArgb(11, 24, 60);
        private static readonly Color MeterCenterBorderColor = Color.FromArgb(84, 132, 196);
        private static readonly Color MeterDepthHighlightColor = Color.FromArgb(92, 228, 236, 255);
        private static readonly Color MeterDepthShadowColor = Color.FromArgb(88, 8, 16, 42);
        private static readonly Color MeterCenterHighlightColor = Color.FromArgb(88, 116, 146, 210);
        private static readonly Color MeterCenterShadowColor = Color.FromArgb(84, 4, 12, 28);
        private static readonly Color DownloadArrowBaseColor = Color.FromArgb(255, 188, 38);
        private static readonly Color DownloadArrowHighColor = Color.FromArgb(255, 228, 116);
        private static readonly Color UploadArrowBaseColor = Color.FromArgb(80, 255, 64);
        private static readonly Color UploadArrowHighColor = Color.FromArgb(170, 255, 140);
        private static readonly Color SparklineGuideColor = Color.FromArgb(74, 122, 156);
        private static readonly Color SparklineDownloadColor = Color.FromArgb(255, 178, 58);
        private static readonly Color SparklineUploadColor = Color.FromArgb(90, 255, 120);
        private static readonly StringFormat TrafficTextStringFormat = CreateTrafficTextFormat(false);
        private static readonly StringFormat TrafficEllipsisTextFormat = CreateTrafficTextFormat(true);
        private const double LowTrafficVisualizationExponent = 0.72D;
        private const double ArrowMotionDeadZoneRatio = 0.05D;
        private const double ArrowMotionFullRatio = 0.30D;
        private const int RingSegmentCount = 18;
        private const float RingSegmentGapDegrees = 4.2F;
        private const float MinimumVisibleSegmentSweepDegrees = 0.9F;
        private const double RingDisplayRiseSmoothingFactor = 0.72D;
        private const double RingDisplayFallSmoothingFactor = 0.42D;
        private const double RingDisplayNoiseFloorBytesPerSecond = 8D * 1024D;
        private const int DisplaySmoothingSampleCount = 3;
        private const int TrafficHistorySampleCount = 60;
        private const int OverlaySparklinePointCount = 24;
        private const string PanelBackgroundAssetFileName = "TrafficView.panel.png";
        private const string PanelBackgroundScaledAssetFileNameFormat = "TrafficView.panel.{0}.png";
        private static readonly double[] DisplaySmoothingWeights = new double[] { 0.15D, 0.30D, 0.55D };
        private static readonly object PanelBackgroundAssetSync = new object();
        private static readonly int[] PanelBackgroundPreparedScalePercents = new int[] { 90, 100, 110, 125, 150 };
        private static Dictionary<string, Bitmap> cachedPanelBackgroundAssets;
        private static bool panelBackgroundAssetLoadAttempted;

        private readonly Timer refreshTimer;
        private readonly Timer animationTimer;
        private readonly Timer topMostGuardTimer;
        private readonly Label downloadCaptionLabel;
        private readonly Label uploadCaptionLabel;
        private readonly Label downloadValueLabel;
        private readonly Label uploadValueLabel;
        private MonitorSettings settings;
        private long lastReceivedBytes;
        private long lastSentBytes;
        private DateTime lastSampleUtc;
        private int currentDpi;
        private Font captionFont;
        private Font valueFont;
        private Font formFont;
        private double latestDownloadBytesPerSecond;
        private double latestUploadBytesPerSecond;
        private double ringDisplayDownloadBytesPerSecond;
        private double ringDisplayUploadBytesPerSecond;
        private double visualDownloadPeakBytesPerSecond;
        private double visualUploadPeakBytesPerSecond;
        private readonly Queue<double> recentDownloadSamples;
        private readonly Queue<double> recentUploadSamples;
        private readonly Queue<TrafficHistorySample> trafficHistory;
        private Control dragControl;
        private Point dragStartCursor;
        private Point dragStartLocation;
        private bool leftMousePressed;
        private bool dragMoved;
        private Bitmap staticSurfaceBitmap;
        private Bitmap composedSurfaceBitmap;
        private bool staticSurfaceDirty = true;
        private int lastRenderedAnimationFrame = -1;
        private string lastRenderedDownloadText = string.Empty;
        private string lastRenderedUploadText = string.Empty;
        private double lastRenderedDownloadFillRatio = double.NaN;
        private double lastRenderedUploadFillRatio = double.NaN;
        private int lastRenderedTrafficHistoryVersion = -1;
        private Point lastPresentedLocation = new Point(int.MinValue, int.MinValue);
        private Size lastPresentedSize = Size.Empty;
        private int topMostPauseDepth;
        private int trafficHistoryVersion;
        private int cachedOverlaySparklineHistoryVersion = -1;
        private TrafficHistorySample[] cachedOverlaySparklineSamples = Array.Empty<TrafficHistorySample>();
        private DateTime suppressMenuUntilUtc = DateTime.MinValue;
        
        public event EventHandler OverlayMenuRequested;
        public event EventHandler OverlayLocationCommitted;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetLayeredWindowAttributes(
            IntPtr hwnd,
            uint crKey,
            byte bAlpha,
            uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(
            IntPtr hwnd,
            IntPtr hdcDst,
            ref NativePoint pptDst,
            ref NativeSize psize,
            IntPtr hdcSrc,
            ref NativePoint pptSrc,
            int crKey,
            ref BlendFunction pblend,
            int dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hDc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteDC(IntPtr hDc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr SelectObject(IntPtr hDc, IntPtr hObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        public event EventHandler<RatesUpdatedEventArgs> RatesUpdated;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams createParams = base.CreateParams;
                createParams.ExStyle |= WsExLayered;
                return createParams;
            }
        }

        public TrafficPopupForm(MonitorSettings initialSettings)
        {
            this.settings = initialSettings.Clone();
            this.currentDpi = DpiHelper.GetDesktopDpi();
            this.recentDownloadSamples = new Queue<double>();
            this.recentUploadSamples = new Queue<double>();
            this.trafficHistory = new Queue<TrafficHistorySample>();

            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.Text = "TrafficView";
            this.BackColor = BackgroundBlue;
            this.AutoScaleMode = AutoScaleMode.None;
            this.DoubleBuffered = true;
            this.SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);

            this.downloadCaptionLabel = CreateCaptionLabel(
                "DL",
                DownloadCaptionColor);
            this.Controls.Add(this.downloadCaptionLabel);
            this.downloadCaptionLabel.Visible = false;

            this.downloadValueLabel = CreateValueLabel(
                DownloadValueColor);
            this.Controls.Add(this.downloadValueLabel);
            this.downloadValueLabel.Visible = false;

            this.uploadCaptionLabel = CreateCaptionLabel(
                "UL",
                UploadCaptionColor);
            this.Controls.Add(this.uploadCaptionLabel);
            this.uploadCaptionLabel.Visible = false;

            this.uploadValueLabel = CreateValueLabel(
                UploadValueColor);
            this.Controls.Add(this.uploadValueLabel);
            this.uploadValueLabel.Visible = false;

            this.SuppressOverlayContextMenus();
            this.WireSurface(this);

            this.refreshTimer = new Timer();
            this.refreshTimer.Interval = 1000;
            this.refreshTimer.Tick += this.RefreshTimer_Tick;
            this.refreshTimer.Start();

            this.animationTimer = new Timer();
            this.animationTimer.Interval = 180;
            this.animationTimer.Tick += this.AnimationTimer_Tick;
            this.animationTimer.Enabled = false;

            this.topMostGuardTimer = new Timer();
            this.topMostGuardTimer.Interval = KeepTopMostRefreshIntervalMs;
            this.topMostGuardTimer.Tick += this.TopMostGuardTimer_Tick;
            this.topMostGuardTimer.Enabled = false;

            this.ApplyDpiLayout(this.currentDpi);
            this.ApplySettings(this.settings);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            int windowDpi = DpiHelper.GetWindowDpi(this.Handle);
            if (windowDpi != this.currentDpi)
            {
                this.ApplyDpiLayout(windowDpi);
            }

            this.ApplyWindowTransparency();
            this.EnsureTopMostPlacement(false);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            this.UpdateAnimationTimerState();
            this.UpdateTopMostGuardState();

            if (this.Visible)
            {
                this.lastRenderedAnimationFrame = -1;
                this.lastPresentedLocation = new Point(int.MinValue, int.MinValue);
                this.EnsureVisiblePopupLocation(null, null);
                this.EnsureTopMostPlacement(false);
                this.RefreshVisualSurface();
            }
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);

            if (this.Visible)
            {
                this.BeginInvoke(new Action(delegate
                {
                    this.EnsureTopMostPlacement(false);
                }));
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                return;
            }

            base.OnFormClosing(e);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                this.Hide();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            this.UpdateWindowRegion();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmContextMenu)
            {
                m.Result = IntPtr.Zero;
                return;
            }

            if (m.Msg == WmDpiChanged)
            {
                int newDpi = DpiHelper.HiWord(m.WParam);
                Point newLocation = this.Location;

                if (m.LParam != IntPtr.Zero)
                {
                    NativeRect suggestedRect = (NativeRect)Marshal.PtrToStructure(m.LParam, typeof(NativeRect));
                    newLocation = new Point(suggestedRect.Left, suggestedRect.Top);
                }

                this.ApplyDpiLayout(newDpi);
                this.Location = this.GetVisiblePopupLocation(
                    newLocation,
                    null,
                    "popup-location-dpi-clamped",
                    "Popup-Position wurde nach einer DPI-Aenderung in einen sichtbaren Arbeitsbereich verschoben.");
                return;
            }

            if (m.Msg == WmDisplayChange || m.Msg == WmSettingChange)
            {
                base.WndProc(ref m);

                if (this.Visible && this.IsHandleCreated)
                {
                    this.BeginInvoke(new Action(delegate
                    {
                        if (!this.IsDisposed && this.Visible)
                        {
                            this.EnsureVisiblePopupLocation(
                                "popup-location-workarea-clamped",
                                "Popup-Position wurde nach einer Monitor- oder Arbeitsbereichsaenderung in einen sichtbaren Bereich verschoben.");
                        }
                    }));
                }

                return;
            }

            base.WndProc(ref m);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            this.RenderPopupSurface(e.Graphics);
        }

        private void RenderPopupSurface(Graphics graphics)
        {
            graphics.Clear(Color.Transparent);
            this.RenderStaticPopupSurface(graphics);
            double downloadFillRatio = this.GetCurrentDownloadFillRatio();
            double uploadFillRatio = this.GetCurrentUploadFillRatio();
            this.RenderDynamicPopupSurface(
                graphics,
                downloadFillRatio,
                uploadFillRatio,
                GetVisualizedFillRatio(downloadFillRatio),
                GetVisualizedFillRatio(uploadFillRatio));
        }

        private void RenderStaticPopupSurface(Graphics graphics)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.CompositingQuality = CompositingQuality.HighQuality;

            int outerInset = this.ScaleValue(BaseOuterInset);
            int separatorY = this.ScaleValue(BaseSeparatorY);
            int separatorInset = this.ScaleValue(BaseSeparatorInset);
            int cornerRadius = this.ScaleValue(BaseWindowCornerRadius);
            float strokeWidth = Math.Max(1F, this.ScaleFloat(1F));
            float sharedRingWidth = Math.Max(2F, this.ScaleFloat(6.2F));
            float centerInset = Math.Max(1F, this.ScaleFloat(2.3F));
            byte backgroundAlpha = MonitorSettings.ToOpacityByte(this.settings.TransparencyPercent);

            float strokeInset = strokeWidth / 2F;
            RectangleF outerBounds = new RectangleF(
                strokeInset,
                strokeInset,
                Math.Max(1F, this.Width - strokeWidth),
                Math.Max(1F, this.Height - strokeWidth));
            RectangleF innerBounds = new RectangleF(
                outerInset + strokeInset,
                outerInset + strokeInset,
                Math.Max(1F, this.Width - (outerInset * 2F) - strokeWidth),
                Math.Max(1F, this.Height - (outerInset * 2F) - strokeWidth));
            Rectangle meterBounds = this.GetDownloadMeterBounds();
            RectangleF sharedRingBounds = this.CreateInsetBounds(meterBounds, sharedRingWidth / 2F);
            RectangleF centerBounds = this.CreateInsetBounds(meterBounds, sharedRingWidth + centerInset);

            using (SolidBrush meterCenterBrush = new SolidBrush(MeterCenterColor))
            using (Pen meterCenterPen = new Pen(MeterCenterBorderColor, strokeWidth))
            {
                if (!this.TryDrawPanelBackgroundAsset(graphics, backgroundAlpha))
                {
                    using (GraphicsPath outerFillPath = CreateRoundedPath(outerBounds, cornerRadius))
                    using (GraphicsPath innerFillPath = CreateRoundedPath(innerBounds, Math.Max(2F, cornerRadius - outerInset)))
                    using (GraphicsPath outerStrokePath = CreateRoundedPath(outerBounds, cornerRadius))
                    using (GraphicsPath innerStrokePath = CreateRoundedPath(
                        innerBounds,
                        Math.Max(2F, cornerRadius - outerInset)))
                    using (SolidBrush borderBrush = new SolidBrush(ApplyAlpha(BorderColor, backgroundAlpha)))
                    using (SolidBrush fillBrush = new SolidBrush(ApplyAlpha(BackgroundBlue, backgroundAlpha)))
                    using (Pen outerPen = new Pen(ApplyAlpha(EdgeSmoothingColor, backgroundAlpha), strokeWidth))
                    using (Pen innerPen = new Pen(ApplyAlpha(Color.FromArgb(60, 86, 144), backgroundAlpha), strokeWidth))
                    {
                        outerPen.LineJoin = LineJoin.Round;
                        innerPen.LineJoin = LineJoin.Round;
                        graphics.FillPath(borderBrush, outerFillPath);
                        graphics.FillPath(fillBrush, innerFillPath);
                        this.DrawPanelDepthSurface(
                            graphics,
                            innerBounds,
                            Math.Max(2F, cornerRadius - outerInset),
                            backgroundAlpha);
                        graphics.DrawPath(outerPen, outerStrokePath);
                        graphics.DrawPath(innerPen, innerStrokePath);
                    }
                }

                this.DrawPanelSeparator(
                    graphics,
                    separatorInset,
                    separatorY,
                    Math.Max(separatorInset, meterBounds.Left - this.ScaleValue(5)),
                    backgroundAlpha);

                this.DrawInterleavedTrafficRing(
                    graphics,
                    sharedRingBounds,
                    sharedRingWidth,
                    0D,
                    0D,
                    MeterTrackColor,
                    MeterTrackInnerColor,
                    DownloadRingLowColor,
                    DownloadRingLowColor,
                    UploadRingLowColor,
                    UploadRingLowColor,
                    true);

                graphics.FillEllipse(meterCenterBrush, centerBounds);
                this.DrawMeterCenterDepth(graphics, centerBounds);
                graphics.DrawEllipse(meterCenterPen, centerBounds);
            }
        }

        private bool TryDrawPanelBackgroundAsset(Graphics graphics, byte backgroundAlpha)
        {
            Bitmap panelBackgroundAsset = GetPanelBackgroundAsset(this.ClientSize);
            if (panelBackgroundAsset == null)
            {
                return false;
            }

            GraphicsState state = graphics.Save();

            try
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;

                using (ImageAttributes imageAttributes = CreateAlphaImageAttributes(backgroundAlpha))
                {
                    if (panelBackgroundAsset.Width == this.ClientSize.Width &&
                        panelBackgroundAsset.Height == this.ClientSize.Height)
                    {
                        graphics.DrawImage(
                            panelBackgroundAsset,
                            new Rectangle(0, 0, panelBackgroundAsset.Width, panelBackgroundAsset.Height),
                            0,
                            0,
                            panelBackgroundAsset.Width,
                            panelBackgroundAsset.Height,
                            GraphicsUnit.Pixel,
                            imageAttributes);
                    }
                    else
                    {
                        graphics.DrawImage(
                            panelBackgroundAsset,
                            new Rectangle(0, 0, this.ClientSize.Width, this.ClientSize.Height),
                            0,
                            0,
                            panelBackgroundAsset.Width,
                            panelBackgroundAsset.Height,
                            GraphicsUnit.Pixel,
                            imageAttributes);
                    }
                }

                return true;
            }
            catch (ExternalException ex)
            {
                AppLog.WarnOnce(
                    "panel-background-asset-draw-failed",
                    "The panel background asset could not be drawn. Falling back to procedural panel rendering.",
                    ex);
                return false;
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private void RenderDynamicPopupSurface(
            Graphics graphics,
            double downloadFillRatio,
            double uploadFillRatio,
            double visualDownloadFillRatio,
            double visualUploadFillRatio)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.CompositingQuality = CompositingQuality.HighQuality;

            float sharedRingWidth = Math.Max(2F, this.ScaleFloat(6.2F));
            float centerInset = Math.Max(1F, this.ScaleFloat(2.3F));
            float iconInset = Math.Max(1F, this.ScaleFloat(2.3F));

            Rectangle meterBounds = this.GetDownloadMeterBounds();
            RectangleF sharedRingBounds = this.CreateInsetBounds(meterBounds, sharedRingWidth / 2F);
            RectangleF centerBounds = this.CreateInsetBounds(meterBounds, sharedRingWidth + centerInset);
            RectangleF iconBounds = this.CreateInsetBounds(meterBounds, sharedRingWidth + iconInset);

            Color downloadRingEndColor = GetInterpolatedColor(
                DownloadRingLowColor,
                DownloadRingHighColor,
                SmoothStep(visualDownloadFillRatio));
            Color uploadRingEndColor = GetInterpolatedColor(
                UploadRingLowColor,
                UploadRingHighColor,
                SmoothStep(visualUploadFillRatio));

            this.DrawMeterValueBalanceSupport(graphics, meterBounds);
            this.DrawTrafficTexts(graphics);
            this.DrawMiniTrafficSparkline(graphics, meterBounds);
            this.DrawInterleavedTrafficRing(
                graphics,
                sharedRingBounds,
                sharedRingWidth,
                visualDownloadFillRatio,
                visualUploadFillRatio,
                MeterTrackColor,
                MeterTrackInnerColor,
                DownloadRingLowColor,
                downloadRingEndColor,
                UploadRingLowColor,
                uploadRingEndColor,
                false);
            this.DrawCenterTrafficArrows(
                graphics,
                centerBounds,
                iconBounds,
                downloadFillRatio,
                uploadFillRatio,
                visualDownloadFillRatio,
                visualUploadFillRatio);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.refreshTimer != null)
                {
                    this.refreshTimer.Dispose();
                }

                if (this.animationTimer != null)
                {
                    this.animationTimer.Dispose();
                }

                if (this.topMostGuardTimer != null)
                {
                    this.topMostGuardTimer.Dispose();
                }

                this.DisposeSurfaceBitmaps();

                DisposeFont(this.captionFont);
                DisposeFont(this.valueFont);
                DisposeFont(this.formFont);
            }

            base.Dispose(disposing);
        }

        public void ApplySettings(MonitorSettings newSettings)
        {
            bool popupScaleChanged = this.settings.PopupScalePercent != newSettings.PopupScalePercent;
            Point previousLocation = this.Location;
            this.settings = newSettings.Clone();

            if (popupScaleChanged)
            {
                this.ApplyDpiLayout(this.currentDpi);
                this.Location = this.GetVisiblePopupLocation(
                    previousLocation,
                    null,
                    "popup-scale-clamped",
                    "Popup-Position wurde nach einer Groessenaenderung auf einen sichtbaren Arbeitsbereich begrenzt.");
            }

            this.staticSurfaceDirty = true;
            this.ApplyWindowTransparency();
            this.lastSampleUtc = DateTime.MinValue;
            this.lastReceivedBytes = 0L;
            this.lastSentBytes = 0L;
            this.latestDownloadBytesPerSecond = 0D;
            this.latestUploadBytesPerSecond = 0D;
            this.ringDisplayDownloadBytesPerSecond = 0D;
            this.ringDisplayUploadBytesPerSecond = 0D;
            this.ResetDisplayedRateSmoothing();
            this.trafficHistory.Clear();
            this.trafficHistoryVersion++;
            this.visualDownloadPeakBytesPerSecond = Math.Max(this.settings.GetDownloadVisualizationPeak(), 256D * 1024D);
            this.visualUploadPeakBytesPerSecond = Math.Max(this.settings.GetUploadVisualizationPeak(), 256D * 1024D);
            this.RefreshTraffic();
        }

        private void ApplyWindowTransparency()
        {
            this.Opacity = 1D;
            this.downloadCaptionLabel.ForeColor = DownloadCaptionColor;
            this.downloadValueLabel.ForeColor = DownloadValueColor;
            this.uploadCaptionLabel.ForeColor = UploadCaptionColor;
            this.uploadValueLabel.ForeColor = UploadValueColor;
            this.staticSurfaceDirty = true;

            if (!this.IsHandleCreated)
            {
                return;
            }

            if (this.Visible)
            {
                this.RefreshVisualSurface();
            }
        }

        public void ShowNearTray(bool activateWindow = true)
        {
            Rectangle workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
            int popupMargin = this.ScaleValue(BasePopupMargin);
            Point preferredLocation = new Point(
                workingArea.Right - this.Width - popupMargin,
                workingArea.Bottom - this.Height - popupMargin);
            this.Location = this.GetVisiblePopupLocation(preferredLocation, Cursor.Position, null, null);

            if (!this.Visible)
            {
                this.Show();
            }

            this.WindowState = FormWindowState.Normal;
            this.EnsureTopMostPlacement(activateWindow);
        }

        public void ShowAtLocation(Point preferredLocation, bool activateWindow)
        {
            this.Location = this.GetVisiblePopupLocation(
                preferredLocation,
                null,
                "popup-location-restore-clamped",
                "Popup-Position wurde fuer die Wiederherstellung auf einen sichtbaren Arbeitsbereich begrenzt.");

            if (!this.Visible)
            {
                this.Show();
            }

            this.WindowState = FormWindowState.Normal;
            this.EnsureTopMostPlacement(activateWindow);
        }

        public void SuppressMenuTemporarily(int milliseconds)
        {
            if (milliseconds <= 0)
            {
                return;
            }

            this.suppressMenuUntilUtc = DateTime.UtcNow.AddMilliseconds(milliseconds);
        }

        public void BringToFrontOnly()
        {
            if (!this.Visible)
            {
                return;
            }

            this.WindowState = FormWindowState.Normal;
            this.EnsureTopMostPlacement(false);
        }

        private void UpdateTopMostGuardState()
        {
            if (this.topMostGuardTimer == null)
            {
                return;
            }

            this.topMostGuardTimer.Enabled = this.Visible && !this.IsTopMostEnforcementPaused;
        }

        public void SuspendTopMostEnforcement()
        {
            this.topMostPauseDepth++;
            this.UpdateTopMostGuardState();
        }

        public void ResumeTopMostEnforcement(bool activateWindow)
        {
            if (this.topMostPauseDepth > 0)
            {
                this.topMostPauseDepth--;
            }

            this.UpdateTopMostGuardState();

            if (this.IsTopMostEnforcementPaused || !this.Visible)
            {
                return;
            }

            this.EnsureTopMostPlacement(activateWindow);
        }

        private void EnsureTopMostPlacement(bool activateWindow)
        {
            if (!this.IsHandleCreated || this.IsTopMostEnforcementPaused)
            {
                return;
            }

            this.TopMost = true;

            uint flags = SwpShowWindow | SwpNoMove | SwpNoSize;
            if (!activateWindow)
            {
                flags |= SwpNoActivate;
            }

            if (!SetWindowPos(this.Handle, HwndTopMost, this.Left, this.Top, this.Width, this.Height, flags))
            {
                AppLog.WarnOnce(
                    "overlay-topmost-setwindowpos",
                    string.Format(
                        "SetWindowPos failed while enforcing top-most overlay placement. Win32={0}",
                        Marshal.GetLastWin32Error()));
            }

            if (activateWindow)
            {
                this.TryActivatePopupWindow();
            }
        }

        private void TryActivatePopupWindow()
        {
            IntPtr foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == this.Handle)
            {
                return;
            }

            this.BringToFront();

            if (Form.ActiveForm != this)
            {
                try
                {
                    this.Activate();
                }
                catch (InvalidOperationException ex)
                {
                    AppLog.WarnOnce(
                        "popup-activate-invalid-operation",
                        "Popup-Aktivierung konnte nicht ausgefuehrt werden.",
                        ex);
                }
            }

            if (GetForegroundWindow() == this.Handle)
            {
                return;
            }

            if (!SetForegroundWindow(this.Handle))
            {
                AppLog.WarnOnce(
                    "popup-setforegroundwindow-failed",
                    "SetForegroundWindow konnte das Popup nicht in den Vordergrund holen; TopMost bleibt aktiv.");
            }
        }

        private void EnsureVisiblePopupLocation(string logKey, string logMessage)
        {
            Point adjustedLocation = this.GetVisiblePopupLocation(this.Location, null, logKey, logMessage);
            if (adjustedLocation != this.Location)
            {
                this.Location = adjustedLocation;
            }
        }

        private Point GetVisiblePopupLocation(Point preferredLocation, Point? anchorPoint, string logKey, string logMessage)
        {
            Rectangle workingArea = this.GetBestWorkingArea(preferredLocation, anchorPoint);
            Point adjustedLocation = ClampLocationToWorkingArea(preferredLocation, this.Size, workingArea);

            if (!string.IsNullOrWhiteSpace(logKey) &&
                adjustedLocation != preferredLocation)
            {
                AppLog.WarnOnce(
                    logKey,
                    string.Format(
                        "{0} Angefordert=({1},{2}), angewendet=({3},{4}), Arbeitsbereich=({5},{6},{7},{8}).",
                        logMessage,
                        preferredLocation.X,
                        preferredLocation.Y,
                        adjustedLocation.X,
                        adjustedLocation.Y,
                        workingArea.Left,
                        workingArea.Top,
                        workingArea.Width,
                        workingArea.Height));
            }

            return adjustedLocation;
        }

        private Rectangle GetBestWorkingArea(Point preferredLocation, Point? anchorPoint)
        {
            if (anchorPoint.HasValue)
            {
                return Screen.FromPoint(anchorPoint.Value).WorkingArea;
            }

            Rectangle preferredBounds = new Rectangle(preferredLocation, this.Size);
            Point preferredCenter = GetRectangleCenter(preferredBounds);
            Rectangle bestWorkingArea = Screen.FromPoint(preferredLocation).WorkingArea;
            long bestDistance = GetDistanceSquaredToRectangle(bestWorkingArea, preferredCenter);
            int bestIntersectionArea = GetIntersectionArea(preferredBounds, bestWorkingArea);

            foreach (Screen screen in Screen.AllScreens)
            {
                Rectangle candidateArea = screen.WorkingArea;
                int intersectionArea = GetIntersectionArea(preferredBounds, candidateArea);

                if (intersectionArea > bestIntersectionArea)
                {
                    bestWorkingArea = candidateArea;
                    bestIntersectionArea = intersectionArea;
                    bestDistance = GetDistanceSquaredToRectangle(candidateArea, preferredCenter);
                    continue;
                }

                if (intersectionArea == bestIntersectionArea)
                {
                    long candidateDistance = GetDistanceSquaredToRectangle(candidateArea, preferredCenter);
                    if (candidateDistance < bestDistance)
                    {
                        bestWorkingArea = candidateArea;
                        bestDistance = candidateDistance;
                    }
                }
            }

            return bestWorkingArea;
        }

        private Point ClampLocationToWorkingArea(Point preferredLocation, Size windowSize, Rectangle workingArea)
        {
            int minimumVisibleMargin = Math.Max(1, this.ScaleValue(BasePopupVisibleMargin));
            int x = ClampAxisToRange(
                preferredLocation.X,
                windowSize.Width,
                workingArea.Left,
                workingArea.Right,
                minimumVisibleMargin);
            int y = ClampAxisToRange(
                preferredLocation.Y,
                windowSize.Height,
                workingArea.Top,
                workingArea.Bottom,
                minimumVisibleMargin);
            return new Point(x, y);
        }

        private static int ClampAxisToRange(
            int preferredStart,
            int elementSize,
            int rangeStart,
            int rangeEndExclusive,
            int minimumVisibleMargin)
        {
            elementSize = Math.Max(1, elementSize);
            minimumVisibleMargin = Math.Max(1, minimumVisibleMargin);

            int availableSize = Math.Max(0, rangeEndExclusive - rangeStart);
            int minimumStart = rangeStart;
            int maximumStart = rangeEndExclusive - elementSize;

            if (elementSize > availableSize)
            {
                minimumStart = rangeStart - Math.Max(0, elementSize - minimumVisibleMargin);
                maximumStart = rangeEndExclusive - minimumVisibleMargin;
            }

            if (maximumStart < minimumStart)
            {
                maximumStart = minimumStart;
            }

            if (preferredStart < minimumStart)
            {
                return minimumStart;
            }

            if (preferredStart > maximumStart)
            {
                return maximumStart;
            }

            return preferredStart;
        }

        private static int GetIntersectionArea(Rectangle first, Rectangle second)
        {
            Rectangle intersection = Rectangle.Intersect(first, second);
            if (intersection.IsEmpty)
            {
                return 0;
            }

            return intersection.Width * intersection.Height;
        }

        private static Point GetRectangleCenter(Rectangle bounds)
        {
            return new Point(
                bounds.Left + (bounds.Width / 2),
                bounds.Top + (bounds.Height / 2));
        }

        private static long GetDistanceSquaredToRectangle(Rectangle rectangle, Point point)
        {
            int dx = 0;
            if (point.X < rectangle.Left)
            {
                dx = rectangle.Left - point.X;
            }
            else if (point.X >= rectangle.Right)
            {
                dx = point.X - rectangle.Right;
            }

            int dy = 0;
            if (point.Y < rectangle.Top)
            {
                dy = rectangle.Top - point.Y;
            }
            else if (point.Y >= rectangle.Bottom)
            {
                dy = point.Y - rectangle.Bottom;
            }

            return ((long)dx * dx) + ((long)dy * dy);
        }

        public static string FormatSpeed(double bytesPerSecond)
        {
            string[] units = new string[] { "B/s", "KB/s", "MB/s", "GB/s" };
            double value = Math.Max(0D, bytesPerSecond);
            int unitIndex = 0;

            while (value >= 1024D && unitIndex < units.Length - 1)
            {
                value /= 1024D;
                unitIndex++;
            }

            if (unitIndex == 0)
            {
                return string.Format("{0:0} {1}", value, units[unitIndex]);
            }

            string format = value >= 100D ? "0" : (value >= 10D ? "0.#" : "0.0");
            return string.Format("{0:" + format + "} {1}", value, units[unitIndex]);
        }

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            this.RefreshTraffic();
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            if (!this.Visible || this.IsDisposed)
            {
                return;
            }

            this.RefreshVisualSurface();
        }

        private void TopMostGuardTimer_Tick(object sender, EventArgs e)
        {
            if (!this.Visible || this.IsDisposed || this.IsTopMostEnforcementPaused)
            {
                return;
            }

            this.EnsureTopMostPlacement(false);
        }

        private bool IsTopMostEnforcementPaused
        {
            get { return this.topMostPauseDepth > 0; }
        }

        private void RefreshTraffic()
        {
            NetworkSnapshot snapshot = NetworkSnapshot.Capture(this.settings);

            if (!snapshot.HasAdapters)
            {
                this.lastSampleUtc = DateTime.MinValue;
                this.lastReceivedBytes = 0L;
                this.lastSentBytes = 0L;
                this.latestDownloadBytesPerSecond = 0D;
                this.latestUploadBytesPerSecond = 0D;
                this.ringDisplayDownloadBytesPerSecond = 0D;
                this.ringDisplayUploadBytesPerSecond = 0D;
                this.ResetDisplayedRateSmoothing();
                this.trafficHistory.Clear();
                this.trafficHistoryVersion++;
                this.downloadValueLabel.Text = "0 B/s";
                this.uploadValueLabel.Text = "0 B/s";
                this.UpdateAnimationTimerState();
                if (this.Visible)
                {
                    this.RefreshVisualSurface();
                }
                this.OnRatesUpdated(0D, 0D);
                return;
            }

            DateTime nowUtc = DateTime.UtcNow;
            double downloadBytesPerSecond = 0D;
            double uploadBytesPerSecond = 0D;

            if (this.lastSampleUtc != DateTime.MinValue)
            {
                double elapsedSeconds = (nowUtc - this.lastSampleUtc).TotalSeconds;
                if (elapsedSeconds > 0.1D)
                {
                    long receivedDiff = snapshot.BytesReceived - this.lastReceivedBytes;
                    long sentDiff = snapshot.BytesSent - this.lastSentBytes;
                    downloadBytesPerSecond = Math.Max(0L, receivedDiff) / elapsedSeconds;
                    uploadBytesPerSecond = Math.Max(0L, sentDiff) / elapsedSeconds;
                }
            }

            this.lastSampleUtc = nowUtc;
            this.lastReceivedBytes = snapshot.BytesReceived;
            this.lastSentBytes = snapshot.BytesSent;
            this.latestDownloadBytesPerSecond = downloadBytesPerSecond;
            this.latestUploadBytesPerSecond = uploadBytesPerSecond;
            this.UpdateRingDisplayRates(downloadBytesPerSecond, uploadBytesPerSecond);
            this.visualDownloadPeakBytesPerSecond = this.GetVisualizationPeak(
                downloadBytesPerSecond,
                this.visualDownloadPeakBytesPerSecond,
                this.settings.GetDownloadVisualizationPeak());
            this.visualUploadPeakBytesPerSecond = this.GetVisualizationPeak(
                uploadBytesPerSecond,
                this.visualUploadPeakBytesPerSecond,
                this.settings.GetUploadVisualizationPeak());

            AddDisplaySample(this.recentDownloadSamples, downloadBytesPerSecond);
            AddDisplaySample(this.recentUploadSamples, uploadBytesPerSecond);
            double smoothedDownloadBytesPerSecond = GetSmoothedDisplayRate(this.recentDownloadSamples);
            double smoothedUploadBytesPerSecond = GetSmoothedDisplayRate(this.recentUploadSamples);

            this.downloadValueLabel.Text = FormatSpeed(smoothedDownloadBytesPerSecond);
            this.uploadValueLabel.Text = FormatSpeed(smoothedUploadBytesPerSecond);
            this.AddTrafficHistorySample(smoothedDownloadBytesPerSecond, smoothedUploadBytesPerSecond);
            this.UpdateAnimationTimerState();
            if (this.Visible)
            {
                this.RefreshVisualSurface();
            }

            this.OnRatesUpdated(smoothedDownloadBytesPerSecond, smoothedUploadBytesPerSecond);
        }

        private void ResetDisplayedRateSmoothing()
        {
            this.recentDownloadSamples.Clear();
            this.recentUploadSamples.Clear();
        }

        private static void AddDisplaySample(Queue<double> samples, double value)
        {
            while (samples.Count >= DisplaySmoothingSampleCount)
            {
                samples.Dequeue();
            }

            samples.Enqueue(Math.Max(0D, value));
        }

        private static double GetSmoothedDisplayRate(Queue<double> samples)
        {
            if (samples.Count == 0)
            {
                return 0D;
            }

            double[] values = samples.ToArray();
            double weightedSum = 0D;
            double totalWeight = 0D;
            int weightOffset = DisplaySmoothingWeights.Length - values.Length;

            for (int i = 0; i < values.Length; i++)
            {
                double weight = DisplaySmoothingWeights[weightOffset + i];
                weightedSum += values[i] * weight;
                totalWeight += weight;
            }

            if (totalWeight <= 0D)
            {
                return values[values.Length - 1];
            }

            return weightedSum / totalWeight;
        }

        private void UpdateRingDisplayRates(double downloadBytesPerSecond, double uploadBytesPerSecond)
        {
            this.ringDisplayDownloadBytesPerSecond = UpdateRingDisplayRate(
                this.ringDisplayDownloadBytesPerSecond,
                downloadBytesPerSecond);
            this.ringDisplayUploadBytesPerSecond = UpdateRingDisplayRate(
                this.ringDisplayUploadBytesPerSecond,
                uploadBytesPerSecond);
        }

        private static double UpdateRingDisplayRate(double currentBytesPerSecond, double targetBytesPerSecond)
        {
            double current = Math.Max(0D, currentBytesPerSecond);
            double target = Math.Max(0D, targetBytesPerSecond);

            if (current <= RingDisplayNoiseFloorBytesPerSecond)
            {
                current = 0D;
            }

            if (target <= RingDisplayNoiseFloorBytesPerSecond)
            {
                target = 0D;
            }

            double smoothingFactor = target >= current
                ? RingDisplayRiseSmoothingFactor
                : RingDisplayFallSmoothingFactor;
            double next = current + ((target - current) * smoothingFactor);

            if (Math.Abs(next - target) <= (RingDisplayNoiseFloorBytesPerSecond * 0.25D))
            {
                return target;
            }

            return Math.Max(0D, next);
        }

        private void AddTrafficHistorySample(double downloadBytesPerSecond, double uploadBytesPerSecond)
        {
            while (this.trafficHistory.Count >= TrafficHistorySampleCount)
            {
                this.trafficHistory.Dequeue();
            }

            this.trafficHistory.Enqueue(new TrafficHistorySample(
                DateTime.UtcNow,
                Math.Max(0D, downloadBytesPerSecond),
                Math.Max(0D, uploadBytesPerSecond)));
            this.trafficHistoryVersion++;
        }

        private TrafficHistorySample[] GetTrafficHistorySnapshot()
        {
            return this.trafficHistory.ToArray();
        }

        private void OnRatesUpdated(double downloadBytesPerSecond, double uploadBytesPerSecond)
        {
            EventHandler<RatesUpdatedEventArgs> handler = this.RatesUpdated;
            if (handler != null)
            {
                handler(this, new RatesUpdatedEventArgs(downloadBytesPerSecond, uploadBytesPerSecond));
            }
        }

        private double GetVisualizationPeak(
            double currentTrafficBytesPerSecond,
            double previousPeakBytesPerSecond,
            double configuredPeakBytesPerSecond)
        {
            if (configuredPeakBytesPerSecond > 0D)
            {
                return configuredPeakBytesPerSecond;
            }

            double minimumPeak = 256D * 1024D;
            double currentPeak = Math.Max(minimumPeak, currentTrafficBytesPerSecond * 1.15D);
            if (previousPeakBytesPerSecond <= 0D)
            {
                return currentPeak;
            }

            return Math.Max(currentPeak, previousPeakBytesPerSecond * 0.96D);
        }

        private double GetTrafficFillRatio(double bytesPerSecond, double configuredPeakBytesPerSecond, double visualPeakBytesPerSecond)
        {
            double peak = configuredPeakBytesPerSecond > 0D
                ? configuredPeakBytesPerSecond
                : visualPeakBytesPerSecond;

            if (peak <= 0D)
            {
                return 0D;
            }

            return Math.Max(0D, Math.Min(1D, bytesPerSecond / peak));
        }

        private static double GetVisualizedFillRatio(double fillRatio)
        {
            double clamped = Math.Max(0D, Math.Min(1D, fillRatio));

            if (clamped <= 0D || clamped >= 1D)
            {
                return clamped;
            }

            // A gentle gamma curve makes low traffic more visible without
            // overdriving medium and high values too aggressively.
            return Math.Pow(clamped, LowTrafficVisualizationExponent);
        }

        private Rectangle GetDownloadMeterBounds()
        {
            int diameter = this.ScaleValue(BaseMeterDiameter);
            int rightInset = this.ScaleValue(BaseMeterRightInset);
            int x = this.ClientSize.Width - diameter - rightInset;
            int y = Math.Max(this.ScaleValue(6), (this.ClientSize.Height - diameter) / 2);
            return new Rectangle(x, y, diameter, diameter);
        }

        private RectangleF CreateInsetBounds(Rectangle outerBounds, float inset)
        {
            float width = Math.Max(2F, outerBounds.Width - (inset * 2F));
            float height = Math.Max(2F, outerBounds.Height - (inset * 2F));
            return new RectangleF(
                outerBounds.Left + inset,
                outerBounds.Top + inset,
                width,
                height);
        }

        private void DrawTrafficRing(
            Graphics graphics,
            RectangleF bounds,
            float strokeWidth,
            double fillRatio,
            Color trackColor,
            Color progressColor)
        {
            double clampedRatio = Math.Max(0D, Math.Min(1D, fillRatio));

            using (Pen trackPen = CreateRingPen(trackColor, strokeWidth, false))
            using (Pen glowPen = CreateRingPen(Color.FromArgb(96, progressColor), strokeWidth + this.ScaleFloat(1.5F), true))
            using (Pen progressPen = CreateRingPen(progressColor, strokeWidth, true))
            {
                graphics.DrawEllipse(trackPen, bounds);

                if (clampedRatio <= 0.001D)
                {
                    return;
                }

                float sweepAngle = this.GetDisplayedSweepAngle(clampedRatio);
                graphics.DrawArc(glowPen, bounds, -90F, sweepAngle);
                graphics.DrawArc(progressPen, bounds, -90F, sweepAngle);
            }
        }

        private void DrawInterleavedTrafficRing(
            Graphics graphics,
            RectangleF bounds,
            float strokeWidth,
            double downloadFillRatio,
            double uploadFillRatio,
            Color downloadTrackColor,
            Color uploadTrackColor,
            Color downloadStartColor,
            Color downloadEndColor,
            Color uploadStartColor,
            Color uploadEndColor,
            bool drawTracks)
        {
            int segmentCount = RingSegmentCount;
            float slotSweep = 360F / segmentCount;
            float gapAngle = Math.Min(slotSweep * 0.48F, RingSegmentGapDegrees);
            float downloadSweep = Math.Max(
                MinimumVisibleSegmentSweepDegrees,
                slotSweep - gapAngle);
            float uploadSweep = Math.Max(
                MinimumVisibleSegmentSweepDegrees,
                gapAngle * 0.82F);
            float uploadOffset = downloadSweep + ((gapAngle - uploadSweep) / 2F);

            this.DrawSegmentedProgressSet(
                graphics,
                bounds,
                strokeWidth,
                segmentCount,
                slotSweep,
                0F,
                downloadSweep,
                downloadFillRatio,
                downloadTrackColor,
                downloadStartColor,
                downloadEndColor,
                drawTracks);
            this.DrawSegmentedProgressSet(
                graphics,
                bounds,
                strokeWidth,
                segmentCount,
                slotSweep,
                uploadOffset,
                uploadSweep,
                uploadFillRatio,
                uploadTrackColor,
                uploadStartColor,
                uploadEndColor,
                drawTracks);
        }

        private void DrawSegmentedProgressSet(
            Graphics graphics,
            RectangleF bounds,
            float strokeWidth,
            int segmentCount,
            float slotSweep,
            float segmentOffset,
            float segmentSweep,
            double fillRatio,
            Color trackColor,
            Color startColor,
            Color endColor,
            bool drawTracks)
        {
            double clampedRatio = Math.Max(0D, Math.Min(1D, fillRatio));
            double activeSegments = clampedRatio * segmentCount;
            int fullyLitSegments = (int)Math.Floor(activeSegments);
            double partialSegmentRatio = activeSegments - fullyLitSegments;
            Color activeEndColor = GetInterpolatedColor(
                startColor,
                endColor,
                SmoothStep(clampedRatio));

            for (int index = 0; index < segmentCount; index++)
            {
                float segmentStartAngle = -90F + segmentOffset + (index * slotSweep);
                if (drawTracks)
                {
                    this.DrawRingSegment(
                        graphics,
                        bounds,
                        strokeWidth,
                        segmentStartAngle,
                        segmentSweep,
                        trackColor,
                        0F);
                }

                if (index < fullyLitSegments)
                {
                    double colorRatio = (index + 1D) / Math.Max(1D, activeSegments);
                    Color activeColor = GetInterpolatedColor(
                        startColor,
                        activeEndColor,
                        SmoothStep(Math.Max(0D, Math.Min(1D, colorRatio))));
                    this.DrawRingSegment(
                        graphics,
                        bounds,
                        strokeWidth,
                        segmentStartAngle,
                        segmentSweep,
                        activeColor,
                        this.ScaleFloat(1.8F));
                }
                else if (index == fullyLitSegments && partialSegmentRatio > 0D)
                {
                    float partialSweep = (float)(segmentSweep * partialSegmentRatio);

                    if (partialSweep >= MinimumVisibleSegmentSweepDegrees)
                    {
                        Color activeColor = GetInterpolatedColor(
                            startColor,
                            activeEndColor,
                            SmoothStep(clampedRatio));
                        this.DrawRingSegment(
                            graphics,
                            bounds,
                            strokeWidth,
                            segmentStartAngle,
                            partialSweep,
                            activeColor,
                            this.ScaleFloat(1.8F));
                    }
                }
            }
        }

        private void DrawRingSegment(
            Graphics graphics,
            RectangleF bounds,
            float strokeWidth,
            float startAngle,
            float sweepAngle,
            Color color,
            float glowWidth)
        {
            if (sweepAngle <= 0.05F)
            {
                return;
            }

            float stableStrokeWidth = NormalizeStrokeWidth(strokeWidth);
            RectangleF stableBounds = GetStableArcBounds(bounds);
            RectangleF accentBounds = GetStableArcBounds(
                InflateRectangle(
                    stableBounds,
                    -Math.Max(0.2F, stableStrokeWidth * 0.03F)));
            float stableStartAngle = NormalizeArcAngle(startAngle);
            float stableSweepAngle = NormalizeArcSweepAngle(sweepAngle);
            GraphicsState state = graphics.Save();

            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;

                if (glowWidth > 0.05F)
                {
                    using (Pen glowPen = new Pen(
                        Color.FromArgb(120, color),
                        NormalizeStrokeWidth(stableStrokeWidth + glowWidth)))
                    {
                        glowPen.Alignment = PenAlignment.Center;
                        glowPen.LineJoin = LineJoin.MiterClipped;
                        glowPen.StartCap = LineCap.Flat;
                        glowPen.EndCap = LineCap.Flat;
                        graphics.DrawArc(glowPen, stableBounds, stableStartAngle, stableSweepAngle);
                    }
                }

                using (Pen segmentPen = new Pen(color, stableStrokeWidth))
                {
                    segmentPen.Alignment = PenAlignment.Center;
                    segmentPen.LineJoin = LineJoin.MiterClipped;
                    segmentPen.StartCap = LineCap.Flat;
                    segmentPen.EndCap = LineCap.Flat;
                    graphics.DrawArc(segmentPen, stableBounds, stableStartAngle, stableSweepAngle);
                }

                float accentWidth = NormalizeStrokeWidth(Math.Max(0.6F, stableStrokeWidth * 0.14F));
                float highlightSweep = NormalizeArcSweepAngle(Math.Max(0.1F, stableSweepAngle * 0.42F));
                float shadowStartAngle = NormalizeArcAngle(stableStartAngle + (stableSweepAngle * 0.44F));
                float shadowSweep = NormalizeArcSweepAngle(Math.Max(0.1F, stableSweepAngle * 0.46F));
                using (Pen highlightPen = new Pen(
                    ApplyAlpha(GetInterpolatedColor(color, Color.FromArgb(255, 245, 248, 255), 0.12D), 84),
                    accentWidth))
                using (Pen shadowPen = new Pen(
                    ApplyAlpha(GetInterpolatedColor(color, Color.FromArgb(255, 8, 14, 28), 0.16D), 92),
                    accentWidth))
                {
                    highlightPen.Alignment = PenAlignment.Center;
                    highlightPen.LineJoin = LineJoin.MiterClipped;
                    highlightPen.StartCap = LineCap.Flat;
                    highlightPen.EndCap = LineCap.Flat;
                    shadowPen.Alignment = PenAlignment.Center;
                    shadowPen.LineJoin = LineJoin.MiterClipped;
                    shadowPen.StartCap = LineCap.Flat;
                    shadowPen.EndCap = LineCap.Flat;
                    graphics.DrawArc(
                        highlightPen,
                        accentBounds,
                        stableStartAngle,
                        highlightSweep);
                    graphics.DrawArc(
                        shadowPen,
                        accentBounds,
                        shadowStartAngle,
                        shadowSweep);
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private void DrawCenterTrafficArrows(
            Graphics graphics,
            RectangleF centerBounds,
            RectangleF iconBounds,
            double downloadFillRatio,
            double uploadFillRatio,
            double visualDownloadFillRatio,
            double visualUploadFillRatio)
        {
            double animationSeconds = DateTime.UtcNow.TimeOfDay.TotalSeconds;
            float arrowWidth = Math.Max(2.2F, (iconBounds.Width * 0.24F) - this.ScaleFloat(1.2F));
            float arrowHeight = Math.Max(5.8F, (iconBounds.Height * 0.58F) - this.ScaleFloat(1F));
            float shaftWidth = Math.Max(1.2F, arrowWidth * 0.34F);
            float glowBaseWidth = Math.Max(1.6F, this.ScaleFloat(1.8F));
            float bobAmplitude = Math.Max(0.5F, this.ScaleFloat(0.85F));
            float horizontalOffset = iconBounds.Width * 0.19F;
            float centerX = centerBounds.Left + (centerBounds.Width / 2F);
            float centerY = centerBounds.Top + (centerBounds.Height / 2F);
            float rawDownloadPulse = 0.5F + (0.5F * (float)Math.Sin(animationSeconds * 5.4D));
            float rawUploadPulse = 0.5F + (0.5F * (float)Math.Sin((animationSeconds * 5.4D) + Math.PI));
            float downloadMotionRatio = (float)GetArrowMotionRatio(downloadFillRatio);
            float uploadMotionRatio = (float)GetArrowMotionRatio(uploadFillRatio);
            float downloadPulse = 0.5F + ((rawDownloadPulse - 0.5F) * downloadMotionRatio);
            float uploadPulse = 0.5F + ((rawUploadPulse - 0.5F) * uploadMotionRatio);
            float downloadYOffset = ((downloadPulse - 0.5F) * bobAmplitude * 1.7F) + this.ScaleFloat(0.45F);
            float uploadYOffset = -((uploadPulse - 0.5F) * bobAmplitude * 1.7F) - this.ScaleFloat(0.45F);
            Color downloadColor = GetInterpolatedColor(
                DownloadArrowBaseColor,
                DownloadArrowHighColor,
                SmoothStep(visualDownloadFillRatio));
            Color uploadColor = GetInterpolatedColor(
                UploadArrowBaseColor,
                UploadArrowHighColor,
                SmoothStep(visualUploadFillRatio));

            using (GraphicsPath clipPath = new GraphicsPath())
            {
                clipPath.AddEllipse(centerBounds);
                GraphicsState state = graphics.Save();
                graphics.SetClip(clipPath, CombineMode.Intersect);

                this.DrawAnimatedArrow(
                    graphics,
                    new PointF(centerX - horizontalOffset, centerY + downloadYOffset),
                    arrowWidth,
                    arrowHeight,
                    shaftWidth,
                    false,
                    downloadColor,
                    downloadPulse,
                    glowBaseWidth,
                    visualDownloadFillRatio);
                this.DrawAnimatedArrow(
                    graphics,
                    new PointF(centerX + horizontalOffset, centerY + uploadYOffset),
                    arrowWidth,
                    arrowHeight,
                    shaftWidth,
                    true,
                    uploadColor,
                    uploadPulse,
                    glowBaseWidth,
                    visualUploadFillRatio);

                graphics.Restore(state);
            }
        }

        private void DrawAnimatedArrow(
            Graphics graphics,
            PointF center,
            float width,
            float height,
            float shaftWidth,
            bool pointsUp,
            Color bodyColor,
            float pulse,
            float glowBaseWidth,
            double intensity)
        {
            float scale = 0.92F + ((float)intensity * 0.10F) + (pulse * 0.04F);
            PointF stableCenter = GetStableArrowCenter(center);
            float scaledWidth = NormalizeArrowDimension(width * scale);
            float scaledHeight = NormalizeArrowDimension(height * scale);
            float scaledShaftWidth = NormalizeArrowDimension(shaftWidth * Math.Max(0.92F, scale));
            float glowWidth = glowBaseWidth + (pulse * this.ScaleFloat(0.85F)) + ((float)intensity * this.ScaleFloat(0.75F));
            int glowAlpha = 96 + (int)Math.Round((pulse * 36F) + (float)intensity * 54F, MidpointRounding.AwayFromZero);
            Color glowColor = Color.FromArgb(
                Math.Max(0, Math.Min(220, glowAlpha)),
                bodyColor.R,
                bodyColor.G,
                bodyColor.B);
            Color outlineColor = GetInterpolatedColor(bodyColor, Color.FromArgb(255, 250, 248, 236), 0.22D + (0.10D * pulse));
            GraphicsState state = graphics.Save();

            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;

            using (GraphicsPath arrowPath = CreateArrowPath(stableCenter, scaledWidth, scaledHeight, scaledShaftWidth, pointsUp))
            using (Pen glowPen = new Pen(glowColor, glowWidth))
            using (SolidBrush arrowBrush = new SolidBrush(bodyColor))
            using (Pen outlinePen = new Pen(outlineColor, Math.Max(0.8F, this.ScaleFloat(0.8F))))
            {
                glowPen.LineJoin = LineJoin.Round;
                glowPen.Alignment = PenAlignment.Center;
                outlinePen.LineJoin = LineJoin.Round;
                outlinePen.Alignment = PenAlignment.Center;
                graphics.DrawPath(glowPen, arrowPath);
                graphics.FillPath(arrowBrush, arrowPath);
                graphics.DrawPath(outlinePen, arrowPath);
            }

            graphics.Restore(state);
        }

        private static GraphicsPath CreateArrowPath(
            PointF center,
            float width,
            float height,
            float shaftWidth,
            bool pointsUp)
        {
            float halfWidth = width / 2F;
            float halfHeight = height / 2F;
            float halfShaftWidth = shaftWidth / 2F;
            float headHeight = height * 0.42F;
            float top = center.Y - halfHeight;
            float bottom = center.Y + halfHeight;
            float headBaseY = pointsUp ? (top + headHeight) : (bottom - headHeight);
            PointF[] points;

            if (pointsUp)
            {
                points = new PointF[]
                {
                    CreateAlignedPoint(center.X, top),
                    CreateAlignedPoint(center.X + halfWidth, headBaseY),
                    CreateAlignedPoint(center.X + halfShaftWidth, headBaseY),
                    CreateAlignedPoint(center.X + halfShaftWidth, bottom),
                    CreateAlignedPoint(center.X - halfShaftWidth, bottom),
                    CreateAlignedPoint(center.X - halfShaftWidth, headBaseY),
                    CreateAlignedPoint(center.X - halfWidth, headBaseY)
                };
            }
            else
            {
                points = new PointF[]
                {
                    CreateAlignedPoint(center.X + halfShaftWidth, top),
                    CreateAlignedPoint(center.X + halfShaftWidth, headBaseY),
                    CreateAlignedPoint(center.X + halfWidth, headBaseY),
                    CreateAlignedPoint(center.X, bottom),
                    CreateAlignedPoint(center.X - halfWidth, headBaseY),
                    CreateAlignedPoint(center.X - halfShaftWidth, headBaseY),
                    CreateAlignedPoint(center.X - halfShaftWidth, top)
                };
            }

            GraphicsPath path = new GraphicsPath();
            path.AddPolygon(points);
            path.CloseFigure();
            return path;
        }

        private void DrawPanelDepthSurface(
            Graphics graphics,
            RectangleF innerBounds,
            float innerCornerRadius,
            byte backgroundAlpha)
        {
            float shadingInset = Math.Max(2.4F, this.ScaleFloat(2.8F));
            RectangleF shadingBounds = InflateRectangle(innerBounds, -shadingInset);
            float shadingCornerRadius = Math.Max(2F, innerCornerRadius - shadingInset + this.ScaleFloat(0.5F));

            using (GraphicsPath shadingPath = CreateRoundedPath(shadingBounds, shadingCornerRadius))
            using (PathGradientBrush shadingBrush = new PathGradientBrush(shadingPath))
            {
                Color convexCenterColor = ApplyAlpha(
                    GetInterpolatedColor(BackgroundBlue, Color.FromArgb(104, 136, 194), 0.07D),
                    backgroundAlpha);
                Color convexEdgeColor = Color.FromArgb(0, BackgroundBlue.R, BackgroundBlue.G, BackgroundBlue.B);
                Color[] surroundColors = new Color[Math.Max(1, shadingPath.PointCount)];

                for (int i = 0; i < surroundColors.Length; i++)
                {
                    surroundColors[i] = convexEdgeColor;
                }

                shadingBrush.CenterPoint = new PointF(
                    shadingBounds.Left + (shadingBounds.Width * 0.50F),
                    shadingBounds.Top + (shadingBounds.Height * 0.46F));
                shadingBrush.FocusScales = new PointF(0.91F, 0.86F);
                shadingBrush.CenterColor = convexCenterColor;
                shadingBrush.SurroundColors = surroundColors;
                shadingBrush.WrapMode = WrapMode.Clamp;
                graphics.FillPath(shadingBrush, shadingPath);
            }
        }

        private static Bitmap GetPanelBackgroundAsset(Size targetSize)
        {
            lock (PanelBackgroundAssetSync)
            {
                if (panelBackgroundAssetLoadAttempted)
                {
                    return SelectBestPanelBackgroundAsset(targetSize);
                }

                panelBackgroundAssetLoadAttempted = true;
                cachedPanelBackgroundAssets = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);

                string[] assetPaths = GetPanelBackgroundAssetPaths();
                for (int i = 0; i < assetPaths.Length; i++)
                {
                    string assetPath = assetPaths[i];
                    if (!File.Exists(assetPath))
                    {
                        continue;
                    }

                    try
                    {
                        using (Image image = Image.FromFile(assetPath))
                        {
                            cachedPanelBackgroundAssets[assetPath] = new Bitmap(image);
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLog.WarnOnce(
                            "panel-background-asset-load-failed-" + assetPath,
                            string.Format(
                                "The panel background asset could not be loaded from '{0}'. Procedural panel rendering will be used for missing sizes.",
                                assetPath),
                            ex);
                    }
                }

                if (cachedPanelBackgroundAssets.Count == 0)
                {
                    AppLog.WarnOnce(
                        "panel-background-asset-missing",
                        string.Format(
                            "No panel background assets were found in '{0}'. Procedural panel rendering will be used.",
                            AppDomain.CurrentDomain.BaseDirectory));
                    return null;
                }

                return SelectBestPanelBackgroundAsset(targetSize);
            }
        }

        private static string[] GetPanelBackgroundAssetPaths()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            List<string> assetPaths = new List<string>();

            for (int i = 0; i < PanelBackgroundPreparedScalePercents.Length; i++)
            {
                int scalePercent = PanelBackgroundPreparedScalePercents[i];
                string fileName = scalePercent == 100
                    ? PanelBackgroundAssetFileName
                    : string.Format(PanelBackgroundScaledAssetFileNameFormat, scalePercent);
                assetPaths.Add(Path.Combine(baseDirectory, fileName));
            }

            return assetPaths.ToArray();
        }

        private static Bitmap SelectBestPanelBackgroundAsset(Size targetSize)
        {
            if (cachedPanelBackgroundAssets == null || cachedPanelBackgroundAssets.Count == 0)
            {
                return null;
            }

            Bitmap bestAsset = null;
            long bestScore = long.MaxValue;

            foreach (KeyValuePair<string, Bitmap> pair in cachedPanelBackgroundAssets)
            {
                Bitmap candidate = pair.Value;
                long score = GetPanelBackgroundAssetMatchScore(candidate.Size, targetSize);
                if (score < bestScore)
                {
                    bestAsset = candidate;
                    bestScore = score;
                }
            }

            return bestAsset;
        }

        private static long GetPanelBackgroundAssetMatchScore(Size assetSize, Size targetSize)
        {
            long widthDelta = Math.Abs(assetSize.Width - targetSize.Width);
            long heightDelta = Math.Abs(assetSize.Height - targetSize.Height);
            long areaDelta = Math.Abs((assetSize.Width * assetSize.Height) - (targetSize.Width * targetSize.Height));
            return (widthDelta * widthDelta) + (heightDelta * heightDelta) + areaDelta;
        }

        private void DrawMeterCenterDepth(Graphics graphics, RectangleF centerBounds)
        {
            GraphicsState state = graphics.Save();

            try
            {
                using (GraphicsPath centerPath = new GraphicsPath())
                {
                    centerPath.AddEllipse(centerBounds);
                    graphics.SetClip(centerPath, CombineMode.Intersect);

                    using (LinearGradientBrush centerBrush = new LinearGradientBrush(
                        new PointF(centerBounds.Left, centerBounds.Top),
                        new PointF(centerBounds.Right, centerBounds.Bottom),
                        GetInterpolatedColor(MeterCenterColor, Color.FromArgb(48, 78, 132), 0.32D),
                        GetInterpolatedColor(MeterCenterColor, Color.FromArgb(4, 10, 24), 0.42D)))
                    {
                        ColorBlend blend = new ColorBlend();
                        blend.Positions = new float[] { 0F, 0.32F, 0.68F, 1F };
                        blend.Colors = new Color[]
                        {
                            GetInterpolatedColor(MeterCenterColor, Color.FromArgb(66, 96, 154), 0.36D),
                            GetInterpolatedColor(MeterCenterColor, Color.FromArgb(30, 52, 98), 0.20D),
                            MeterCenterColor,
                            GetInterpolatedColor(MeterCenterColor, Color.FromArgb(2, 8, 18), 0.50D)
                        };
                        centerBrush.InterpolationColors = blend;
                        graphics.FillEllipse(centerBrush, centerBounds);
                    }

                    RectangleF highlightBounds = new RectangleF(
                        centerBounds.Left + (centerBounds.Width * 0.12F),
                        centerBounds.Top + (centerBounds.Height * 0.10F),
                        centerBounds.Width * 0.58F,
                        centerBounds.Height * 0.34F);
                    using (GraphicsPath highlightPath = CreateRoundedPath(
                        highlightBounds,
                        Math.Max(2, (int)Math.Round(Math.Min(highlightBounds.Width, highlightBounds.Height) * 0.45F, MidpointRounding.AwayFromZero))))
                    using (LinearGradientBrush highlightBrush = new LinearGradientBrush(
                        new PointF(highlightBounds.Left, highlightBounds.Top),
                        new PointF(highlightBounds.Left, highlightBounds.Bottom),
                        Color.FromArgb(54, 232, 240, 255),
                        Color.FromArgb(0, 232, 240, 255)))
                    {
                        graphics.FillPath(highlightBrush, highlightPath);
                    }

                    float rimWidth = Math.Max(0.8F, this.ScaleFloat(1F));
                    using (Pen innerHighlightPen = new Pen(Color.FromArgb(76, 176, 204, 248), rimWidth))
                    using (Pen innerShadowPen = new Pen(Color.FromArgb(84, 6, 10, 24), rimWidth))
                    {
                        RectangleF rimBounds = InflateRectangle(centerBounds, -Math.Max(1F, this.ScaleFloat(1.4F)));
                        innerHighlightPen.Alignment = PenAlignment.Center;
                        innerShadowPen.Alignment = PenAlignment.Center;
                        graphics.DrawArc(innerHighlightPen, rimBounds, 214F, 108F);
                        graphics.DrawArc(innerShadowPen, rimBounds, 26F, 120F);
                    }
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private float GetDisplayedSweepAngle(double clampedRatio)
        {
            float sweepAngle = (float)(clampedRatio * 360D);
            return Math.Min(359.5F, Math.Max(sweepAngle, 8F));
        }

        private static Pen CreateRingPen(Color color, float width, bool roundedCaps)
        {
            Pen pen = new Pen(color, width);
            pen.Alignment = PenAlignment.Center;
            pen.LineJoin = LineJoin.Round;
            pen.StartCap = roundedCaps ? LineCap.Round : LineCap.Flat;
            pen.EndCap = roundedCaps ? LineCap.Round : LineCap.Flat;
            return pen;
        }

        private static RectangleF GetStableArcBounds(RectangleF bounds)
        {
            float left = AlignToHalfPixel(bounds.Left);
            float top = AlignToHalfPixel(bounds.Top);
            float right = AlignToHalfPixel(bounds.Right);
            float bottom = AlignToHalfPixel(bounds.Bottom);
            return new RectangleF(
                left,
                top,
                Math.Max(1F, right - left),
                Math.Max(1F, bottom - top));
        }

        private static float NormalizeArcAngle(float angle)
        {
            return (float)(Math.Round(angle * 4F, MidpointRounding.AwayFromZero) / 4D);
        }

        private static float NormalizeArcSweepAngle(float sweepAngle)
        {
            return Math.Max(0.05F, (float)(Math.Round(sweepAngle * 4F, MidpointRounding.AwayFromZero) / 4D));
        }

        private static float AlignToHalfPixel(float value)
        {
            return (float)(Math.Round(value * 2F, MidpointRounding.AwayFromZero) / 2D);
        }

        private static float NormalizeStrokeWidth(float value)
        {
            return Math.Max(0.5F, AlignToHalfPixel(value));
        }

        private static RectangleF GetStableTextBounds(Rectangle bounds)
        {
            return new RectangleF(
                AlignToHalfPixel(bounds.Left),
                AlignToHalfPixel(bounds.Top),
                Math.Max(1F, bounds.Width),
                Math.Max(1F, bounds.Height));
        }

        private static PointF GetStableArrowCenter(PointF center)
        {
            return new PointF(
                AlignToHalfPixel(center.X),
                AlignToHalfPixel(center.Y));
        }

        private static float NormalizeArrowDimension(float value)
        {
            return Math.Max(1F, AlignToHalfPixel(value));
        }

        private static PointF CreateAlignedPoint(float x, float y)
        {
            return new PointF(
                AlignToHalfPixel(x),
                AlignToHalfPixel(y));
        }

        private static RectangleF OffsetRectangle(RectangleF bounds, float offsetX, float offsetY)
        {
            return new RectangleF(
                bounds.Left + offsetX,
                bounds.Top + offsetY,
                bounds.Width,
                bounds.Height);
        }

        private void DrawPanelSeparator(
            Graphics graphics,
            int separatorInset,
            int separatorY,
            int separatorEndX,
            byte backgroundAlpha)
        {
            float startX = AlignToHalfPixel(separatorInset);
            float endX = AlignToHalfPixel(Math.Max(separatorInset, separatorEndX));
            float lineY = AlignToHalfPixel(separatorY);
            float accentWidth = Math.Max(1F, this.ScaleFloat(1F));
            float transitionOffset = Math.Max(1F, this.ScaleFloat(1F));

            using (Pen transitionPen = new Pen(
                ApplyAlpha(GetInterpolatedColor(BackgroundBlue, DividerColor, 0.20D), Math.Min(backgroundAlpha, (byte)52)),
                accentWidth))
            using (Pen separatorPen = new Pen(
                ApplyAlpha(GetInterpolatedColor(DividerColor, BackgroundBlue, 0.14D), Math.Min(backgroundAlpha, (byte)88)),
                accentWidth))
            {
                transitionPen.StartCap = LineCap.Flat;
                transitionPen.EndCap = LineCap.Flat;
                separatorPen.StartCap = LineCap.Flat;
                separatorPen.EndCap = LineCap.Flat;

                graphics.DrawLine(transitionPen, startX, lineY - transitionOffset, endX, lineY - transitionOffset);
                graphics.DrawLine(separatorPen, startX, lineY, endX, lineY);
                graphics.DrawLine(transitionPen, startX, lineY + transitionOffset, endX, lineY + transitionOffset);
            }
        }

        private static StringFormat CreateTrafficTextFormat(bool allowEllipsis)
        {
            StringFormat stringFormat = new StringFormat(StringFormat.GenericTypographic);
            stringFormat.Alignment = StringAlignment.Near;
            stringFormat.LineAlignment = StringAlignment.Center;
            stringFormat.FormatFlags = StringFormatFlags.NoWrap;
            stringFormat.Trimming = allowEllipsis
                ? StringTrimming.EllipsisCharacter
                : StringTrimming.None;
            return stringFormat;
        }

        private static double SmoothStep(double value)
        {
            double clamped = Math.Max(0D, Math.Min(1D, value));
            return clamped * clamped * (3D - (2D * clamped));
        }

        private static Color ApplyAlpha(Color color, byte alpha)
        {
            int effectiveAlpha = (color.A * alpha) / 255;
            return Color.FromArgb(effectiveAlpha, color.R, color.G, color.B);
        }

        private static Color GetInterpolatedColor(Color fromColor, Color toColor, double ratio)
        {
            double clamped = Math.Max(0D, Math.Min(1D, ratio));
            int a = InterpolateChannel(fromColor.A, toColor.A, clamped);
            int r = InterpolateChannel(fromColor.R, toColor.R, clamped);
            int g = InterpolateChannel(fromColor.G, toColor.G, clamped);
            int b = InterpolateChannel(fromColor.B, toColor.B, clamped);
            return Color.FromArgb(a, r, g, b);
        }

        private static int InterpolateChannel(int fromValue, int toValue, double ratio)
        {
            return (int)Math.Round(fromValue + ((toValue - fromValue) * ratio), MidpointRounding.AwayFromZero);
        }

        private static RectangleF InflateRectangle(RectangleF bounds, float amount)
        {
            return new RectangleF(
                bounds.Left - amount,
                bounds.Top - amount,
                Math.Max(1F, bounds.Width + (amount * 2F)),
                Math.Max(1F, bounds.Height + (amount * 2F)));
        }

        private static ImageAttributes CreateAlphaImageAttributes(byte alpha)
        {
            ImageAttributes imageAttributes = new ImageAttributes();
            ColorMatrix colorMatrix = new ColorMatrix();
            colorMatrix.Matrix33 = alpha / 255F;
            imageAttributes.SetColorMatrix(colorMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
            return imageAttributes;
        }

        private static Label CreateCaptionLabel(string text, Color color)
        {
            Label label = new OverlayInputLabel();
            label.AutoSize = false;
            label.BackColor = Color.Transparent;
            label.ForeColor = color;
            label.Text = text;
            label.TextAlign = ContentAlignment.MiddleLeft;
            return label;
        }

        private static Label CreateValueLabel(Color color)
        {
            Label label = new OverlayInputLabel();
            label.AutoSize = false;
            label.AutoEllipsis = true;
            label.BackColor = Color.Transparent;
            label.ForeColor = color;
            label.Text = "0 B/s";
            label.TextAlign = ContentAlignment.MiddleLeft;
            return label;
        }

        private void DrawTrafficTexts(Graphics graphics)
        {
            DrawTrafficText(
                graphics,
                this.downloadCaptionLabel.Text,
                this.captionFont,
                DownloadCaptionColor,
                this.downloadCaptionLabel.Bounds,
                false,
                false);
            DrawTrafficText(
                graphics,
                this.downloadValueLabel.Text,
                this.valueFont,
                DownloadValueColor,
                this.downloadValueLabel.Bounds,
                true,
                true);
            DrawTrafficText(
                graphics,
                this.uploadCaptionLabel.Text,
                this.captionFont,
                UploadCaptionColor,
                this.uploadCaptionLabel.Bounds,
                false,
                false);
            DrawTrafficText(
                graphics,
                this.uploadValueLabel.Text,
                this.valueFont,
                UploadValueColor,
                this.uploadValueLabel.Bounds,
                true,
                true);
        }

        private void DrawMeterValueBalanceSupport(Graphics graphics, Rectangle meterBounds)
        {
            int valueRight = Math.Max(this.downloadValueLabel.Bounds.Right, this.uploadValueLabel.Bounds.Right);
            int supportLeft = Math.Max(this.ScaleValue(40), valueRight - this.ScaleValue(8));
            int supportRight = meterBounds.Left + this.ScaleValue(2);
            int supportTop = Math.Max(0, this.downloadCaptionLabel.Bounds.Top - this.ScaleValue(2));
            int supportBottom = Math.Min(this.ClientSize.Height, this.uploadValueLabel.Bounds.Bottom + this.ScaleValue(2));

            if (supportRight - supportLeft < this.ScaleValue(6) || supportBottom - supportTop < this.ScaleValue(10))
            {
                return;
            }

            RectangleF supportBounds = new RectangleF(
                AlignToHalfPixel(supportLeft),
                AlignToHalfPixel(supportTop),
                Math.Max(1F, supportRight - supportLeft),
                Math.Max(1F, supportBottom - supportTop));
            GraphicsState state = graphics.Save();

            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;

                Color supportColor = GetInterpolatedColor(BackgroundBlue, MeterTrackInnerColor, 0.14D);
                using (LinearGradientBrush supportBrush = new LinearGradientBrush(
                    new PointF(supportBounds.Left, supportBounds.Top),
                    new PointF(supportBounds.Right, supportBounds.Top),
                    Color.Transparent,
                    Color.Transparent))
                {
                    ColorBlend blend = new ColorBlend();
                    blend.Positions = new float[] { 0F, 0.30F, 0.74F, 1F };
                    blend.Colors = new Color[]
                    {
                        Color.Transparent,
                        ApplyAlpha(supportColor, 10),
                        ApplyAlpha(supportColor, 22),
                        ApplyAlpha(supportColor, 8)
                    };
                    supportBrush.InterpolationColors = blend;
                    graphics.FillRectangle(supportBrush, supportBounds);
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private void DrawMiniTrafficSparkline(Graphics graphics, Rectangle meterBounds)
        {
            Rectangle bounds = this.GetSparklineBounds(meterBounds);
            if (bounds.Width < 12 || bounds.Height < 4)
            {
                return;
            }

            GraphicsState state = graphics.Save();
            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;

                using (Pen guidePen = new Pen(Color.FromArgb(90, SparklineGuideColor), Math.Max(1F, this.ScaleFloat(1F))))
                {
                    graphics.DrawLine(guidePen, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
                    graphics.DrawLine(guidePen, bounds.Left, bounds.Top + 1, bounds.Right, bounds.Top + 1);
                }

                TrafficHistorySample[] samples = this.GetOverlaySparklineSamples();
                if (samples.Length < 2)
                {
                    return;
                }

                double peak = 1D;
                for (int i = 0; i < samples.Length; i++)
                {
                    peak = Math.Max(peak, samples[i].DownloadBytesPerSecond);
                    peak = Math.Max(peak, samples[i].UploadBytesPerSecond);
                }

                PointF[] downloadPoints = CreateSparklinePoints(samples, bounds, peak, true);
                PointF[] uploadPoints = CreateSparklinePoints(samples, bounds, peak, false);

                using (Pen downloadPen = new Pen(Color.FromArgb(220, SparklineDownloadColor), Math.Max(1.15F, this.ScaleFloat(1.15F))))
                using (Pen uploadPen = new Pen(Color.FromArgb(220, SparklineUploadColor), Math.Max(1.15F, this.ScaleFloat(1.15F))))
                {
                    downloadPen.LineJoin = LineJoin.Round;
                    downloadPen.StartCap = LineCap.Round;
                    downloadPen.EndCap = LineCap.Round;
                    uploadPen.LineJoin = LineJoin.Round;
                    uploadPen.StartCap = LineCap.Round;
                    uploadPen.EndCap = LineCap.Round;

                    if (downloadPoints.Length >= 2)
                    {
                        graphics.DrawLines(downloadPen, downloadPoints);
                    }

                    if (uploadPoints.Length >= 2)
                    {
                        graphics.DrawLines(uploadPen, uploadPoints);
                    }
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private Rectangle GetSparklineBounds(Rectangle meterBounds)
        {
            int left = this.ScaleValue(6);
            int top = this.ScaleValue(46);
            int width = Math.Max(12, meterBounds.Left - left - this.ScaleValue(4));
            int height = Math.Max(4, this.ScaleValue(7));
            return new Rectangle(left, top, width, height);
        }

        private TrafficHistorySample[] GetOverlaySparklineSamples()
        {
            if (this.cachedOverlaySparklineHistoryVersion == this.trafficHistoryVersion &&
                this.cachedOverlaySparklineSamples != null)
            {
                return this.cachedOverlaySparklineSamples;
            }

            TrafficHistorySample[] history = this.GetTrafficHistorySnapshot();
            TrafficHistorySample[] snapshot;
            if (history.Length <= 0)
            {
                snapshot = Array.Empty<TrafficHistorySample>();
            }
            else if (history.Length <= OverlaySparklinePointCount)
            {
                snapshot = history;
            }
            else
            {
                snapshot = new TrafficHistorySample[OverlaySparklinePointCount];
                Array.Copy(
                    history,
                    history.Length - OverlaySparklinePointCount,
                    snapshot,
                    0,
                    OverlaySparklinePointCount);
            }

            this.cachedOverlaySparklineHistoryVersion = this.trafficHistoryVersion;
            this.cachedOverlaySparklineSamples = snapshot;
            return snapshot;
        }

        private static PointF[] CreateSparklinePoints(
            TrafficHistorySample[] samples,
            Rectangle bounds,
            double peakBytesPerSecond,
            bool useDownload)
        {
            if (samples == null || samples.Length == 0)
            {
                return Array.Empty<PointF>();
            }

            PointF[] points = new PointF[samples.Length];
            float width = Math.Max(1F, bounds.Width - 1F);
            float height = Math.Max(1F, bounds.Height - 1F);
            double peak = Math.Max(1D, peakBytesPerSecond);

            for (int i = 0; i < samples.Length; i++)
            {
                double value = useDownload
                    ? samples[i].DownloadBytesPerSecond
                    : samples[i].UploadBytesPerSecond;
                double ratio = Math.Max(0D, Math.Min(1D, value / peak));
                float x = bounds.Left + ((samples.Length == 1)
                    ? 0F
                    : (width * i) / Math.Max(1, samples.Length - 1));
                float y = bounds.Bottom - 1 - (float)(ratio * height);
                points[i] = new PointF(
                    AlignToHalfPixel(x),
                    AlignToHalfPixel(y));
            }

            return RemoveConsecutiveDuplicatePoints(points);
        }

        private static PointF[] RemoveConsecutiveDuplicatePoints(PointF[] points)
        {
            if (points == null || points.Length <= 1)
            {
                return points ?? Array.Empty<PointF>();
            }

            List<PointF> filteredPoints = new List<PointF>(points.Length);
            PointF previousPoint = points[0];
            filteredPoints.Add(previousPoint);

            for (int index = 1; index < points.Length; index++)
            {
                PointF currentPoint = points[index];
                if (currentPoint != previousPoint)
                {
                    filteredPoints.Add(currentPoint);
                    previousPoint = currentPoint;
                }
            }

            return filteredPoints.Count == points.Length
                ? points
                : filteredPoints.ToArray();
        }

        private static void DrawTrafficText(
            Graphics graphics,
            string text,
            Font font,
            Color color,
            Rectangle bounds,
            bool allowEllipsis,
            bool isPrimaryValue)
        {
            GraphicsState state = graphics.Save();
            RectangleF stableBounds = GetStableTextBounds(bounds);
            RectangleF contrastBounds = OffsetRectangle(
                stableBounds,
                0F,
                0.5F);

            using (SolidBrush contrastBrush = new SolidBrush(Color.FromArgb(
                isPrimaryValue ? 32 : 20,
                BackgroundBlue.R,
                BackgroundBlue.G,
                BackgroundBlue.B)))
            using (SolidBrush textBrush = new SolidBrush(color))
            {
                graphics.PixelOffsetMode = PixelOffsetMode.Half;
                graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                graphics.DrawString(
                    text,
                    font,
                    contrastBrush,
                    contrastBounds,
                    allowEllipsis
                        ? TrafficEllipsisTextFormat
                        : TrafficTextStringFormat);
                graphics.DrawString(
                    text,
                    font,
                    textBrush,
                    stableBounds,
                    allowEllipsis
                        ? TrafficEllipsisTextFormat
                        : TrafficTextStringFormat);
            }

            graphics.Restore(state);
        }

        private double GetVisualizedFillRatioForCurrentDownload()
        {
            return GetVisualizedFillRatio(this.GetCurrentDownloadFillRatio());
        }

        private double GetVisualizedFillRatioForCurrentUpload()
        {
            return GetVisualizedFillRatio(this.GetCurrentUploadFillRatio());
        }

        private double GetCurrentDownloadFillRatio()
        {
            return this.GetTrafficFillRatio(
                this.ringDisplayDownloadBytesPerSecond,
                this.settings.GetDownloadVisualizationPeak(),
                this.visualDownloadPeakBytesPerSecond);
        }

        private double GetCurrentUploadFillRatio()
        {
            return this.GetTrafficFillRatio(
                this.ringDisplayUploadBytesPerSecond,
                this.settings.GetUploadVisualizationPeak(),
                this.visualUploadPeakBytesPerSecond);
        }

        private static double GetArrowMotionRatio(double fillRatio)
        {
            double clamped = Math.Max(0D, Math.Min(1D, fillRatio));

            if (clamped <= ArrowMotionDeadZoneRatio)
            {
                return 0D;
            }

            double normalized = (clamped - ArrowMotionDeadZoneRatio) /
                Math.Max(0.0001D, ArrowMotionFullRatio - ArrowMotionDeadZoneRatio);
            return SmoothStep(Math.Max(0D, Math.Min(1D, normalized)));
        }

        private bool ShouldAnimateCenterArrows()
        {
            return GetArrowMotionRatio(this.GetCurrentDownloadFillRatio()) > 0.001D ||
                GetArrowMotionRatio(this.GetCurrentUploadFillRatio()) > 0.001D;
        }

        private void UpdateAnimationTimerState()
        {
            if (this.animationTimer == null)
            {
                return;
            }

            bool shouldAnimate = this.Visible && this.ShouldAnimateCenterArrows();
            if (this.animationTimer.Enabled == shouldAnimate)
            {
                return;
            }

            this.animationTimer.Enabled = shouldAnimate;
            this.lastRenderedAnimationFrame = -1;
        }

        private int GetAnimationFrameIndex()
        {
            if (!this.ShouldAnimateCenterArrows())
            {
                return 0;
            }

            int interval = Math.Max(1, this.animationTimer != null ? this.animationTimer.Interval : 180);
            double milliseconds = DateTime.UtcNow.TimeOfDay.TotalMilliseconds;
            return (int)Math.Floor(milliseconds / interval);
        }

        private static bool AreNearlyEqual(double left, double right)
        {
            if (double.IsNaN(left) && double.IsNaN(right))
            {
                return true;
            }

            return Math.Abs(left - right) < 0.0001D;
        }

        private void RefreshVisualSurface()
        {
            if (!this.IsHandleCreated || this.Width <= 0 || this.Height <= 0)
            {
                return;
            }

            try
            {
                this.UpdateLayeredWindowContent();
            }
            catch (ExternalException ex)
            {
                this.HandleOverlayRenderFailure(
                    "overlay-refresh-external-exception",
                    "Overlay refresh failed because of a GDI/native rendering exception.",
                    ex);
            }
            catch (ArgumentException ex)
            {
                this.HandleOverlayRenderFailure(
                    "overlay-refresh-argument-exception",
                    "Overlay refresh failed because of an invalid rendering argument.",
                    ex);
            }
            catch (InvalidOperationException ex)
            {
                this.HandleOverlayRenderFailure(
                    "overlay-refresh-invalid-operation",
                    "Overlay refresh failed because the rendering state was not valid.",
                    ex);
            }
        }

        private void UpdateLayeredWindowContent()
        {
            if (!this.Visible)
            {
                return;
            }

            if (!this.EnsureSurfaceBitmaps())
            {
                return;
            }

            bool staticWasDirty = this.staticSurfaceDirty;
            if (!this.RebuildStaticSurfaceIfNeeded())
            {
                return;
            }

            double downloadFillRatio = this.GetCurrentDownloadFillRatio();
            double uploadFillRatio = this.GetCurrentUploadFillRatio();
            double visualDownloadFillRatio = GetVisualizedFillRatio(downloadFillRatio);
            double visualUploadFillRatio = GetVisualizedFillRatio(uploadFillRatio);
            int animationFrame = this.GetAnimationFrameIndex();
            bool needCompose =
                staticWasDirty ||
                this.composedSurfaceBitmap == null ||
                this.lastRenderedAnimationFrame != animationFrame ||
                !string.Equals(this.lastRenderedDownloadText, this.downloadValueLabel.Text, StringComparison.Ordinal) ||
                !string.Equals(this.lastRenderedUploadText, this.uploadValueLabel.Text, StringComparison.Ordinal) ||
                this.lastRenderedTrafficHistoryVersion != this.trafficHistoryVersion ||
                !AreNearlyEqual(this.lastRenderedDownloadFillRatio, visualDownloadFillRatio) ||
                !AreNearlyEqual(this.lastRenderedUploadFillRatio, visualUploadFillRatio);

            if (needCompose)
            {
                try
                {
                    using (Graphics graphics = Graphics.FromImage(this.composedSurfaceBitmap))
                    {
                        graphics.Clear(Color.Transparent);
                        graphics.DrawImageUnscaled(this.staticSurfaceBitmap, 0, 0);
                        this.RenderDynamicPopupSurface(
                            graphics,
                            downloadFillRatio,
                            uploadFillRatio,
                            visualDownloadFillRatio,
                            visualUploadFillRatio);
                    }
                }
                catch (ExternalException ex)
                {
                    this.HandleOverlayRenderFailure(
                        "overlay-compose-external-exception",
                        "Overlay composition failed because of a GDI/native rendering exception.",
                        ex);
                    return;
                }
                catch (ArgumentException ex)
                {
                    this.HandleOverlayRenderFailure(
                        "overlay-compose-argument-exception",
                        "Overlay composition failed because of an invalid rendering argument.",
                        ex);
                    return;
                }

                this.lastRenderedAnimationFrame = animationFrame;
                this.lastRenderedDownloadText = this.downloadValueLabel.Text;
                this.lastRenderedUploadText = this.uploadValueLabel.Text;
                this.lastRenderedTrafficHistoryVersion = this.trafficHistoryVersion;
                this.lastRenderedDownloadFillRatio = visualDownloadFillRatio;
                this.lastRenderedUploadFillRatio = visualUploadFillRatio;
            }

            if (needCompose ||
                this.lastPresentedLocation != this.Location ||
                this.lastPresentedSize != this.Size)
            {
                if (this.PresentLayeredBitmap(this.composedSurfaceBitmap))
                {
                    this.lastPresentedLocation = this.Location;
                    this.lastPresentedSize = this.Size;
                }
            }
        }

        private bool EnsureSurfaceBitmaps()
        {
            if (this.staticSurfaceBitmap != null &&
                this.staticSurfaceBitmap.Width == this.Width &&
                this.staticSurfaceBitmap.Height == this.Height &&
                this.composedSurfaceBitmap != null &&
                this.composedSurfaceBitmap.Width == this.Width &&
                this.composedSurfaceBitmap.Height == this.Height)
            {
                return true;
            }

            this.DisposeSurfaceBitmaps();

            try
            {
                this.staticSurfaceBitmap = new Bitmap(this.Width, this.Height, PixelFormat.Format32bppArgb);
                this.composedSurfaceBitmap = new Bitmap(this.Width, this.Height, PixelFormat.Format32bppArgb);
                this.staticSurfaceDirty = true;
                this.lastRenderedAnimationFrame = -1;
                this.lastRenderedTrafficHistoryVersion = -1;
                return true;
            }
            catch (ExternalException ex)
            {
                this.HandleOverlayRenderFailure(
                    "overlay-surface-bitmap-external-exception",
                    "Overlay surface bitmap creation failed because of a GDI/native rendering exception.",
                    ex);
            }
            catch (ArgumentException ex)
            {
                this.HandleOverlayRenderFailure(
                    "overlay-surface-bitmap-argument-exception",
                    "Overlay surface bitmap creation failed because of invalid dimensions or pixel format.",
                    ex);
            }
            catch (OutOfMemoryException ex)
            {
                this.HandleOverlayRenderFailure(
                    "overlay-surface-bitmap-oom",
                    "Overlay surface bitmap creation failed because memory was not available.",
                    ex);
            }

            return false;
        }

        private bool RebuildStaticSurfaceIfNeeded()
        {
            if (!this.staticSurfaceDirty || this.staticSurfaceBitmap == null)
            {
                return true;
            }

            try
            {
                using (Graphics graphics = Graphics.FromImage(this.staticSurfaceBitmap))
                {
                    graphics.Clear(Color.Transparent);
                    this.RenderStaticPopupSurface(graphics);
                }
            }
            catch (ExternalException ex)
            {
                this.HandleOverlayRenderFailure(
                    "overlay-static-surface-external-exception",
                    "Overlay static surface rebuild failed because of a GDI/native rendering exception.",
                    ex);
                return false;
            }
            catch (ArgumentException ex)
            {
                this.HandleOverlayRenderFailure(
                    "overlay-static-surface-argument-exception",
                    "Overlay static surface rebuild failed because of an invalid rendering argument.",
                    ex);
                return false;
            }

            this.staticSurfaceDirty = false;
            return true;
        }

        private bool PresentLayeredBitmap(Bitmap bitmap)
        {
            if (bitmap == null)
            {
                return false;
            }

            IntPtr screenDc = IntPtr.Zero;
            IntPtr memoryDc = IntPtr.Zero;
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;

            try
            {
                screenDc = GetDC(IntPtr.Zero);
                if (screenDc == IntPtr.Zero)
                {
                    AppLog.WarnOnce(
                        "overlay-getdc-failed",
                        string.Format(
                            "GetDC failed for layered overlay rendering. Win32={0}",
                            Marshal.GetLastWin32Error()));
                    return false;
                }

                memoryDc = CreateCompatibleDC(screenDc);
                if (memoryDc == IntPtr.Zero)
                {
                    AppLog.WarnOnce(
                        "overlay-createcompatibledc-failed",
                        string.Format(
                            "CreateCompatibleDC failed for layered overlay rendering. Win32={0}",
                            Marshal.GetLastWin32Error()));
                    return false;
                }

                hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
                if (hBitmap == IntPtr.Zero)
                {
                    AppLog.WarnOnce(
                        "overlay-gethbitmap-failed",
                        string.Format(
                            "GetHbitmap failed for layered overlay rendering. Win32={0}",
                            Marshal.GetLastWin32Error()));
                    return false;
                }

                oldBitmap = SelectObject(memoryDc, hBitmap);
                if (IsInvalidSelectObjectResult(oldBitmap))
                {
                    AppLog.WarnOnce(
                        "overlay-selectobject-failed",
                        string.Format(
                            "SelectObject failed for layered overlay rendering. Win32={0}",
                            Marshal.GetLastWin32Error()));
                    return false;
                }

                NativeSize size = new NativeSize(bitmap.Width, bitmap.Height);
                NativePoint sourcePoint = new NativePoint(0, 0);
                NativePoint topPosition = new NativePoint(this.Left, this.Top);
                BlendFunction blend = new BlendFunction();
                blend.BlendOp = AcSrcOver;
                blend.SourceConstantAlpha = 255;
                blend.AlphaFormat = AcSrcAlpha;

                if (!UpdateLayeredWindow(
                    this.Handle,
                    screenDc,
                    ref topPosition,
                    ref size,
                    memoryDc,
                    ref sourcePoint,
                    0,
                    ref blend,
                    UlwAlpha))
                {
                    AppLog.WarnOnce(
                        "overlay-updatelayeredwindow-failed",
                        string.Format(
                            "UpdateLayeredWindow failed for overlay presentation. Win32={0}",
                            Marshal.GetLastWin32Error()));
                    return false;
                }

                return true;
            }
            catch (ExternalException ex)
            {
                AppLog.WarnOnce("overlay-gdi-external-exception", "GDI/bitmap operation failed during overlay presentation.", ex);
                return false;
            }
            finally
            {
                if (oldBitmap != IntPtr.Zero)
                {
                    IntPtr restoreResult = SelectObject(memoryDc, oldBitmap);
                    if (IsInvalidSelectObjectResult(restoreResult))
                    {
                        AppLog.WarnOnce(
                            "overlay-selectobject-restore-failed",
                            string.Format(
                                "SelectObject failed while restoring the previous overlay bitmap object. Win32={0}",
                                Marshal.GetLastWin32Error()));
                    }
                }

                if (hBitmap != IntPtr.Zero)
                {
                    if (!DeleteObject(hBitmap))
                    {
                        AppLog.WarnOnce(
                            "overlay-deleteobject-failed",
                            string.Format(
                                "DeleteObject failed while releasing overlay bitmap resources. Win32={0}",
                                Marshal.GetLastWin32Error()));
                    }
                }

                if (memoryDc != IntPtr.Zero)
                {
                    if (!DeleteDC(memoryDc))
                    {
                        AppLog.WarnOnce(
                            "overlay-deletedc-failed",
                            string.Format(
                                "DeleteDC failed while releasing overlay memory DC. Win32={0}",
                                Marshal.GetLastWin32Error()));
                    }
                }

                if (screenDc != IntPtr.Zero)
                {
                    if (ReleaseDC(IntPtr.Zero, screenDc) == 0)
                    {
                        AppLog.WarnOnce(
                            "overlay-releasedc-failed",
                            string.Format(
                                "ReleaseDC failed while releasing overlay screen DC. Win32={0}",
                                Marshal.GetLastWin32Error()));
                    }
                }
            }
        }

        private void HandleOverlayRenderFailure(string key, string message, Exception exception)
        {
            this.DisposeSurfaceBitmaps();
            this.staticSurfaceDirty = true;
            this.lastRenderedAnimationFrame = -1;
            this.lastRenderedTrafficHistoryVersion = -1;
            this.lastPresentedLocation = new Point(int.MinValue, int.MinValue);
            this.lastPresentedSize = Size.Empty;
            AppLog.WarnOnce(key, message, exception);
        }

        private static bool IsInvalidSelectObjectResult(IntPtr result)
        {
            return result == IntPtr.Zero || result == new IntPtr(-1);
        }

        private void DisposeSurfaceBitmaps()
        {
            if (this.staticSurfaceBitmap != null)
            {
                this.staticSurfaceBitmap.Dispose();
                this.staticSurfaceBitmap = null;
            }

            if (this.composedSurfaceBitmap != null)
            {
                this.composedSurfaceBitmap.Dispose();
                this.composedSurfaceBitmap = null;
            }
        }

        private void ApplyDpiLayout(int dpi)
        {
            dpi = DpiHelper.NormalizeDpi(dpi);
            this.currentDpi = dpi;

            Font newFormFont = new Font("Segoe UI", this.ScaleFloat(BaseFormFontSize), FontStyle.Regular, GraphicsUnit.Pixel);
            Font newCaptionFont = new Font("Segoe UI", this.ScaleFloat(BaseCaptionFontSize), FontStyle.Bold, GraphicsUnit.Pixel);
            Font newValueFont = new Font("Segoe UI Semibold", this.ScaleFloat(BaseValueFontSize), FontStyle.Bold, GraphicsUnit.Pixel);

            Font previousFormFont = this.formFont;
            Font previousCaptionFont = this.captionFont;
            Font previousValueFont = this.valueFont;

            this.formFont = newFormFont;
            this.captionFont = newCaptionFont;
            this.valueFont = newValueFont;

            this.SuspendLayout();

            Size clientSize = new Size(this.ScaleValue(BaseClientWidth), this.ScaleValue(BaseClientHeight));
            this.ClientSize = clientSize;
            this.MinimumSize = clientSize;
            this.MaximumSize = clientSize;
            this.Font = this.formFont;

            this.downloadCaptionLabel.Font = this.captionFont;
            this.downloadCaptionLabel.Location = new Point(this.ScaleValue(BaseCaptionX), this.ScaleValue(BaseDownloadCaptionY));
            this.downloadCaptionLabel.Size = new Size(this.ScaleValue(BaseCaptionWidth), this.ScaleValue(BaseCaptionHeight));

            this.downloadValueLabel.Font = this.valueFont;
            this.downloadValueLabel.Location = new Point(this.ScaleValue(BaseDownloadValueX), this.ScaleValue(BaseDownloadValueY));
            this.downloadValueLabel.Size = new Size(this.ScaleValue(BaseValueWidth), this.ScaleValue(BaseValueHeight));

            this.uploadCaptionLabel.Font = this.captionFont;
            this.uploadCaptionLabel.Location = new Point(this.ScaleValue(BaseCaptionX), this.ScaleValue(BaseUploadCaptionY));
            this.uploadCaptionLabel.Size = new Size(this.ScaleValue(BaseCaptionWidth), this.ScaleValue(BaseCaptionHeight));

            this.uploadValueLabel.Font = this.valueFont;
            this.uploadValueLabel.Location = new Point(this.ScaleValue(BaseUploadValueX), this.ScaleValue(BaseUploadValueY));
            this.uploadValueLabel.Size = new Size(this.ScaleValue(BaseValueWidth), this.ScaleValue(BaseValueHeight));

            this.ResumeLayout(false);
            this.UpdateWindowRegion();
            this.staticSurfaceDirty = true;

            if (this.Visible)
            {
                this.RefreshVisualSurface();
            }

            DisposeFont(previousFormFont);
            DisposeFont(previousCaptionFont);
            DisposeFont(previousValueFont);
        }

        private int ScaleValue(int value)
        {
            return Math.Max(
                1,
                (int)Math.Round(
                    DpiHelper.Scale(value, this.currentDpi) * this.GetPopupScaleFactor(),
                    MidpointRounding.AwayFromZero));
        }

        private float ScaleFloat(float value)
        {
            return DpiHelper.Scale(value, this.currentDpi) * (float)this.GetPopupScaleFactor();
        }

        private double GetPopupScaleFactor()
        {
            return Math.Max(0.5D, this.settings.PopupScalePercent / 100D);
        }

        private void UpdateWindowRegion()
        {
            if (this.Width <= 0 || this.Height <= 0)
            {
                return;
            }

            Region previousRegion = this.Region;
            using (GraphicsPath path = CreateRoundedPath(
                new Rectangle(0, 0, this.Width, this.Height),
                this.ScaleValue(BaseWindowCornerRadius)))
            {
                this.Region = new Region(path);
            }

            this.staticSurfaceDirty = true;
            this.lastPresentedLocation = new Point(int.MinValue, int.MinValue);
            this.lastPresentedSize = Size.Empty;

            if (previousRegion != null)
            {
                previousRegion.Dispose();
            }
        }

        private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = Math.Max(2, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
            Rectangle arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

            path.AddArc(arc, 180F, 90F);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270F, 90F);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0F, 90F);
            arc.X = bounds.Left;
            path.AddArc(arc, 90F, 90F);
            path.CloseFigure();
            return path;
        }

        private static GraphicsPath CreateRoundedPath(RectangleF bounds, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float diameter = Math.Max(2F, Math.Min(radius * 2F, Math.Min(bounds.Width, bounds.Height)));
            RectangleF arc = new RectangleF(bounds.Location, new SizeF(diameter, diameter));

            path.AddArc(arc, 180F, 90F);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270F, 90F);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0F, 90F);
            arc.X = bounds.Left;
            path.AddArc(arc, 90F, 90F);
            path.CloseFigure();
            return path;
        }

        private void WireSurface(Control control)
        {
            control.MouseDown += this.Control_MouseDown;
            control.MouseMove += this.Control_MouseMove;
            control.MouseUp += this.Control_MouseUp;
        }

        private void SuppressOverlayContextMenus()
        {
            SuppressContextMenu(this);
            SuppressContextMenu(this.downloadCaptionLabel);
            SuppressContextMenu(this.downloadValueLabel);
            SuppressContextMenu(this.uploadCaptionLabel);
            SuppressContextMenu(this.uploadValueLabel);
        }

        private static void SuppressContextMenu(Control control)
        {
            if (control != null)
            {
                control.ContextMenuStrip = null;
            }
        }

        private void Control_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == GetOverlayDragMouseButton())
            {
                this.leftMousePressed = true;
                this.dragMoved = false;
                this.dragStartCursor = Cursor.Position;
                this.dragStartLocation = this.Location;
                this.dragControl = sender as Control;
                if (this.dragControl != null)
                {
                    this.dragControl.Capture = true;
                }

                return;
            }

            if (e.Button == MouseButtons.Middle)
            {
                ReleaseCapture();
                SendMessage(this.Handle, WmNclButtonDown, HtCaption, 0);
            }
        }

        private void Control_MouseMove(object sender, MouseEventArgs e)
        {
            if (!this.leftMousePressed)
            {
                return;
            }

            Point cursorPosition = Cursor.Position;
            int deltaX = cursorPosition.X - this.dragStartCursor.X;
            int deltaY = cursorPosition.Y - this.dragStartCursor.Y;

            if (!this.dragMoved)
            {
                int dragThreshold = this.ScaleValue(BaseDragThreshold);
                if (Math.Abs(deltaX) < dragThreshold && Math.Abs(deltaY) < dragThreshold)
                {
                    return;
                }

                this.dragMoved = true;
            }

            Point preferredLocation = new Point(
                this.dragStartLocation.X + deltaX,
                this.dragStartLocation.Y + deltaY);
            this.Location = this.GetVisiblePopupLocation(preferredLocation, cursorPosition, null, null);
        }

        private void Control_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == GetOverlayMenuMouseButton())
            {
                bool shouldShowMenu = !this.dragMoved && DateTime.UtcNow >= this.suppressMenuUntilUtc;
                this.ResetOverlayDragState();

                if (shouldShowMenu)
                {
                    this.OnOverlayMenuRequested();
                }

                return;
            }

            if (e.Button == GetOverlayDragMouseButton())
            {
                bool shouldCommitLocation = this.dragMoved;
                this.ResetOverlayDragState();

                if (shouldCommitLocation)
                {
                    this.OnOverlayLocationCommitted();
                }
            }
        }

        private void OnOverlayMenuRequested()
        {
            EventHandler handler = this.OverlayMenuRequested;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void OnOverlayLocationCommitted()
        {
            EventHandler handler = this.OverlayLocationCommitted;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void ResetOverlayDragState()
        {
            this.leftMousePressed = false;
            this.dragMoved = false;

            if (this.dragControl != null)
            {
                this.dragControl.Capture = false;
                this.dragControl = null;
            }
        }

        private static MouseButtons GetOverlayMenuMouseButton()
        {
            return SystemInformation.MouseButtonsSwapped
                ? MouseButtons.Right
                : MouseButtons.Left;
        }

        private static MouseButtons GetOverlayDragMouseButton()
        {
            return MouseButtons.Left;
        }

        private static void DisposeFont(Font font)
        {
            if (font != null)
            {
                font.Dispose();
            }
        }
    }

    internal sealed class CalibrationForm : Form
    {
        private const int CalibrationDurationSeconds = 30;

        private readonly ComboBox adapterComboBox;
        private readonly ProgressBar progressBar;
        private readonly Label statusLabel;
        private readonly Label infoLabel;
        private readonly Button startButton;
        private readonly Button saveAdapterButton;
        private readonly Button saveButton;
        private readonly Button cancelButton;
        private readonly Timer calibrationTimer;
        private readonly TableLayoutPanel rootLayout;
        private MonitorSettings currentSettings;
        private MonitorSettings activeSettings;
        private NetworkSnapshot lastSnapshot;
        private DateTime lastSampleUtc;
        private double peakBytesPerSecond;
        private double peakDownloadBytesPerSecond;
        private double peakUploadBytesPerSecond;
        private int elapsedSeconds;
        private bool allowClose;

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
            this.AdapterSelectionSavedOnly = false;

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
            this.AutoScroll = true;
            this.ClientSize = new Size(620, 340);
            this.MinimumSize = new Size(600, 320);
            int compactButtonHeight = this.GetCalibrationButtonHeight();

            this.rootLayout = new TableLayoutPanel();
            this.rootLayout.ColumnCount = 1;
            this.rootLayout.RowCount = 5;
            this.rootLayout.Dock = DockStyle.Fill;
            this.rootLayout.Padding = new Padding(18, 16, 18, 16);
            this.rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this.rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this.rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this.rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this.rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this.Controls.Add(this.rootLayout);

            this.infoLabel = new Label();
            this.infoLabel.AutoSize = true;
            this.infoLabel.Dock = DockStyle.Fill;
            this.infoLabel.Margin = new Padding(0, 0, 0, 12);
            this.infoLabel.Text = UiLanguage.Get(
                "Calibration.Info",
                "Waehle den Netzwerkadapter fuer die Kalibration. Die Messung laeuft ca. 30 Sekunden. Das Fenster bleibt bis zum Speichern oder Abbrechen geoeffnet.");
            this.rootLayout.Controls.Add(this.infoLabel, 0, 0);

            TableLayoutPanel adapterLayout = new TableLayoutPanel();
            adapterLayout.ColumnCount = 1;
            adapterLayout.RowCount = 2;
            adapterLayout.Dock = DockStyle.Fill;
            adapterLayout.Margin = new Padding(0, 0, 0, 12);
            adapterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            adapterLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            adapterLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this.rootLayout.Controls.Add(adapterLayout, 0, 1);

            Label adapterLabel = new Label();
            adapterLabel.AutoSize = true;
            adapterLabel.Dock = DockStyle.Fill;
            adapterLabel.Margin = new Padding(0, 0, 0, 6);
            adapterLabel.Text = UiLanguage.Get("Calibration.AdapterLabel", "Adapter");
            adapterLayout.Controls.Add(adapterLabel, 0, 0);

            this.adapterComboBox = new ComboBox();
            this.adapterComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            this.adapterComboBox.Dock = DockStyle.Top;
            this.adapterComboBox.IntegralHeight = false;
            this.adapterComboBox.Margin = new Padding(0);
            this.adapterComboBox.MinimumSize = new Size(0, Math.Max(30, this.adapterComboBox.PreferredHeight + 6));
            adapterLayout.Controls.Add(this.adapterComboBox, 0, 1);

            this.progressBar = new ProgressBar();
            this.progressBar.Dock = DockStyle.Top;
            this.progressBar.Margin = new Padding(0, 0, 0, 8);
            this.progressBar.Height = 18;
            this.progressBar.Minimum = 0;
            this.progressBar.Maximum = CalibrationDurationSeconds;
            this.rootLayout.Controls.Add(this.progressBar, 0, 2);

            this.statusLabel = new Label();
            this.statusLabel.AutoSize = true;
            this.statusLabel.Dock = DockStyle.Fill;
            this.statusLabel.Margin = new Padding(0, 0, 0, 12);
            this.statusLabel.Text = UiLanguage.Get(
                "Calibration.ReadyStatus",
                "Bereit fuer die Kalibration. Bitte mit 'Starten' beginnen und spaeter mit 'Speichern' uebernehmen oder mit 'Abbrechen' schliessen.");
            this.rootLayout.Controls.Add(this.statusLabel, 0, 3);

            TableLayoutPanel buttonPanel = new TableLayoutPanel();
            buttonPanel.ColumnCount = 4;
            buttonPanel.RowCount = 1;
            buttonPanel.AutoSize = true;
            buttonPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            buttonPanel.Dock = DockStyle.None;
            buttonPanel.Anchor = AnchorStyles.Top;
            buttonPanel.Margin = new Padding(0);
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            buttonPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, compactButtonHeight));
            this.rootLayout.Controls.Add(buttonPanel, 0, 4);

            this.startButton = new Button();
            this.startButton.Text = UiLanguage.Get("Calibration.Start", "Starten");
            this.startButton.Margin = new Padding(0, 0, 8, 0);
            this.ConfigureDialogButton(this.startButton);
            this.startButton.Click += this.StartButton_Click;
            buttonPanel.Controls.Add(this.startButton, 0, 0);

            this.saveAdapterButton = new Button();
            this.saveAdapterButton.Text = UiLanguage.Get("Calibration.SaveAdapter", "Adapter speichern");
            this.saveAdapterButton.Margin = new Padding(0, 0, 8, 0);
            this.ConfigureDialogButton(this.saveAdapterButton);
            this.saveAdapterButton.Click += this.SaveAdapterButton_Click;
            buttonPanel.Controls.Add(this.saveAdapterButton, 1, 0);

            this.saveButton = new Button();
            this.saveButton.Text = UiLanguage.Get("Calibration.Save", "Speichern");
            this.saveButton.Margin = new Padding(8, 0, 8, 0);
            this.ConfigureDialogButton(this.saveButton);
            this.saveButton.Enabled = false;
            this.saveButton.Click += this.SaveButton_Click;
            buttonPanel.Controls.Add(this.saveButton, 2, 0);

            this.cancelButton = new Button();
            this.cancelButton.Text = UiLanguage.Get("Calibration.Cancel", "Abbrechen");
            this.cancelButton.Margin = new Padding(0);
            this.ConfigureDialogButton(this.cancelButton);
            this.cancelButton.Click += this.CancelButton_Click;
            buttonPanel.Controls.Add(this.cancelButton, 3, 0);

            this.AcceptButton = this.startButton;
            this.CancelButton = this.cancelButton;

            this.calibrationTimer = new Timer();
            this.calibrationTimer.Interval = 1000;
            this.calibrationTimer.Tick += this.CalibrationTimer_Tick;

            this.LoadAdapterItems();
            this.Load += this.CalibrationForm_Load;
            this.Shown += this.CalibrationForm_Shown;
        }

        public MonitorSettings SelectedSettings { get; private set; }

        public bool AdapterSelectionSavedOnly { get; private set; }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!this.allowClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.statusLabel.Text = this.saveButton.Enabled
                    ? UiLanguage.Get("Calibration.CloseHintDone", "Kalibration fertig. Bitte mit 'Speichern' uebernehmen oder mit 'Abbrechen' schliessen.")
                    : UiLanguage.Get("Calibration.CloseHintWaiting", "Bitte 'Abbrechen' verwenden oder die Kalibration abschliessen und mit 'Speichern' uebernehmen.");
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
        }

        private void CalibrationForm_Shown(object sender, EventArgs e)
        {
            this.AdjustDialogLayout();
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
            button.Padding = new Padding(8, 2, 8, 2);
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Width = this.GetCalibrationButtonWidth(button.Text);
            button.MinimumSize = new Size(0, compactButtonHeight);
            button.MaximumSize = new Size(int.MaxValue, compactButtonHeight);
            button.Height = compactButtonHeight;
        }

        private int GetCalibrationButtonHeight()
        {
            return Math.Max(38, this.Font.Height + 18);
        }

        private int GetCalibrationButtonWidth(string text)
        {
            Size textSize = TextRenderer.MeasureText(
                text ?? string.Empty,
                this.Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            return Math.Max(96, textSize.Width + 28);
        }

        private void SetCalibrationButtonText(Button button, string text)
        {
            if (button == null)
            {
                return;
            }

            button.Text = text ?? string.Empty;
            button.Width = this.GetCalibrationButtonWidth(button.Text);
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            if (this.calibrationTimer.Enabled)
            {
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
                this.currentSettings.LanguageCode);

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
            this.AdapterSelectionSavedOnly = false;
            this.allowClose = false;
            this.progressBar.Value = 0;
            this.statusLabel.Text = UiLanguage.Get("Calibration.RunningInitial", "Kalibration laeuft... 0 / 30 s");
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
            this.currentSettings = this.CreateSettingsFromSelectedAdapter(
                this.currentSettings.CalibrationPeakBytesPerSecond,
                this.currentSettings.CalibrationDownloadPeakBytesPerSecond,
                this.currentSettings.CalibrationUploadPeakBytesPerSecond);
            this.currentSettings.Save();
            this.SelectedSettings = this.currentSettings.Clone();
            this.AdapterSelectionSavedOnly = true;
            this.statusLabel.Text = UiLanguage.Format(
                "Calibration.AdapterSavedStatus",
                "Adapter gespeichert: {0}. Bitte Kalibration abschliessen, mit 'Speichern' uebernehmen oder mit 'Abbrechen' schliessen.",
                this.currentSettings.GetAdapterDisplayName());
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            this.SelectedSettings = null;
            this.AdapterSelectionSavedOnly = false;
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

            this.AdapterSelectionSavedOnly = false;
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
                    "Bereit fuer die Kalibration. Bitte mit 'Starten' beginnen und spaeter mit 'Speichern' uebernehmen oder mit 'Abbrechen' schliessen.");
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

            NetworkSnapshot snapshot = NetworkSnapshot.Capture(this.activeSettings);
            DateTime nowUtc = DateTime.UtcNow;

            if (!snapshot.HasAdapters)
            {
                AppLog.WarnOnce(
                    "calibration-snapshot-unavailable-" + GetCalibrationAdapterLogKey(this.activeSettings),
                    string.Format(
                        "Calibration snapshot is temporarily unavailable for '{0}'. Existing measurement state will be preserved.",
                        this.activeSettings.GetAdapterDisplayName()));
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
            this.progressBar.Value = Math.Min(CalibrationDurationSeconds, this.elapsedSeconds);
            this.statusLabel.Text = UiLanguage.Format(
                "Calibration.RunningStatus",
                "Kalibration laeuft... {0} / 30 s | DL {1} | UL {2}",
                this.elapsedSeconds,
                TrafficPopupForm.FormatSpeed(this.peakDownloadBytesPerSecond),
                TrafficPopupForm.FormatSpeed(this.peakUploadBytesPerSecond));

            if (this.elapsedSeconds >= CalibrationDurationSeconds)
            {
                this.FinishCalibration();
            }
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
                this.currentSettings.LanguageCode);
            this.statusLabel.Text = UiLanguage.Format(
                "Calibration.CompletedStatus",
                "Kalibration abgeschlossen. DL {0} | UL {1}. Mit 'Speichern' uebernehmen.",
                TrafficPopupForm.FormatSpeed(storedDownloadPeak),
                TrafficPopupForm.FormatSpeed(storedUploadPeak));
            this.startButton.Enabled = true;
            this.SetCalibrationButtonText(this.startButton, UiLanguage.Get("Calibration.Remeasure", "Neu messen"));
            this.saveAdapterButton.Enabled = true;
            this.adapterComboBox.Enabled = true;
            this.saveButton.Enabled = true;
            this.AdjustDialogLayout();
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
            int maxClientWidth = Math.Max(520, workingArea.Width - 80);
            int maxClientHeight = Math.Max(320, workingArea.Height - 80);
            int targetWidth = Math.Min(Math.Max(540, this.ClientSize.Width), maxClientWidth);
            Size preferred = Size.Empty;

            for (int pass = 0; pass < 2; pass++)
            {
                int wrapWidth = Math.Max(320, targetWidth - this.rootLayout.Padding.Horizontal);
                this.infoLabel.MaximumSize = new Size(wrapWidth, 0);
                this.statusLabel.MaximumSize = new Size(wrapWidth, 0);
                this.rootLayout.PerformLayout();

                preferred = this.rootLayout.GetPreferredSize(new Size(targetWidth, 0));
                int requiredWidth = Math.Min(maxClientWidth, Math.Max(540, preferred.Width + 24));
                if (requiredWidth <= targetWidth)
                {
                    break;
                }

                targetWidth = requiredWidth;
            }

            int targetHeight = Math.Min(
                Math.Max(320, preferred.Height + this.rootLayout.Padding.Vertical + 16),
                maxClientHeight);

            this.ClientSize = new Size(targetWidth, targetHeight);
            this.AutoScrollMinSize = new Size(preferred.Width, preferred.Height + 8);
        }

        private MonitorSettings CreateSettingsFromSelectedAdapter(
            double calibrationPeakBytesPerSecond,
            double calibrationDownloadPeakBytesPerSecond,
            double calibrationUploadPeakBytesPerSecond)
        {
            AdapterListItem selectedItem = this.adapterComboBox.SelectedItem as AdapterListItem;
            string adapterId = string.Empty;
            string adapterName = string.Empty;

            if (selectedItem != null)
            {
                adapterId = selectedItem.Id;
                adapterName = selectedItem.Name;
            }

            return new MonitorSettings(
                adapterId,
                adapterName,
                calibrationPeakBytesPerSecond,
                calibrationDownloadPeakBytesPerSecond,
                calibrationUploadPeakBytesPerSecond,
                this.currentSettings.InitialCalibrationPromptHandled,
                this.currentSettings.InitialLanguagePromptHandled,
                this.currentSettings.TransparencyPercent,
                this.currentSettings.LanguageCode);
        }

        private void LoadAdapterItems()
        {
            List<AdapterListItem> items = NetworkSnapshot.GetAdapterItems();
            AdapterListItem automaticItem = new AdapterListItem(
                string.Empty,
                string.Empty,
                UiLanguage.Get("Calibration.AdapterAutomatic", "Automatisch (alle aktiven Adapter)"));
            this.adapterComboBox.Items.Add(automaticItem);

            int selectedIndex = 0;

            for (int i = 0; i < items.Count; i++)
            {
                AdapterListItem item = items[i];
                this.adapterComboBox.Items.Add(item);

                if (!string.IsNullOrEmpty(this.currentSettings.AdapterId) &&
                    string.Equals(item.Id, this.currentSettings.AdapterId, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i + 1;
                }
            }

            this.adapterComboBox.SelectedIndex = selectedIndex;
        }
    }

    internal sealed class RatesUpdatedEventArgs : EventArgs
    {
        public RatesUpdatedEventArgs(double downloadBytesPerSecond, double uploadBytesPerSecond)
        {
            this.DownloadBytesPerSecond = downloadBytesPerSecond;
            this.UploadBytesPerSecond = uploadBytesPerSecond;
        }

        public double DownloadBytesPerSecond { get; private set; }

        public double UploadBytesPerSecond { get; private set; }
    }

    internal struct TrafficHistorySample
    {
        public readonly DateTime TimestampUtc;
        public readonly double DownloadBytesPerSecond;
        public readonly double UploadBytesPerSecond;

        public TrafficHistorySample(DateTime timestampUtc, double downloadBytesPerSecond, double uploadBytesPerSecond)
        {
            this.TimestampUtc = timestampUtc;
            this.DownloadBytesPerSecond = downloadBytesPerSecond;
            this.UploadBytesPerSecond = uploadBytesPerSecond;
        }
    }

    internal sealed class LanguageSelectionForm : Form
    {
        private readonly ComboBox languageComboBox;
        private readonly Button saveButton;
        private bool allowClose;

        public LanguageSelectionForm(string selectedLanguageCode)
        {
            this.Text = UiLanguage.Get("StartupLanguage.Title", "Programmsprache");
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ShowInTaskbar = false;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ControlBox = false;
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.Font = SystemFonts.MessageBoxFont;
            this.TopMost = true;
            this.ClientSize = new Size(420, 190);

            Label infoLabel = new Label();
            infoLabel.AutoSize = false;
            infoLabel.Text = UiLanguage.Get(
                "StartupLanguage.Info",
                "Bitte waehle zuerst die Programmsprache aus. Nach dem Speichern wird das Fenster geschlossen und das Programm startet weiter.");
            infoLabel.SetBounds(16, 14, 388, 48);

            Label languageLabel = new Label();
            languageLabel.AutoSize = false;
            languageLabel.Text = UiLanguage.Get("StartupLanguage.Label", "Sprache");
            languageLabel.SetBounds(16, 72, 120, 24);

            this.languageComboBox = new ComboBox();
            this.languageComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            this.languageComboBox.SetBounds(16, 98, 388, 30);

            LanguageOption[] languages = UiLanguage.GetSupportedLanguages();
            int selectedIndex = 0;
            for (int i = 0; i < languages.Length; i++)
            {
                this.languageComboBox.Items.Add(languages[i]);
                if (string.Equals(languages[i].Code, selectedLanguageCode, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i;
                }
            }

            if (this.languageComboBox.Items.Count > 0)
            {
                this.languageComboBox.SelectedIndex = selectedIndex;
            }

            this.saveButton = new Button();
            this.saveButton.AutoSize = true;
            this.saveButton.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.saveButton.Text = UiLanguage.Get("StartupLanguage.Save", "Speichern");
            this.saveButton.Padding = new Padding(10, 4, 10, 4);
            this.saveButton.MinimumSize = new Size(112, 32);
            this.saveButton.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            Size saveButtonSize = this.saveButton.GetPreferredSize(Size.Empty);
            int saveButtonWidth = Math.Max(this.saveButton.MinimumSize.Width, saveButtonSize.Width);
            int saveButtonHeight = Math.Max(this.saveButton.MinimumSize.Height, saveButtonSize.Height);
            this.saveButton.SetBounds(
                this.ClientSize.Width - 16 - saveButtonWidth,
                this.ClientSize.Height - 16 - saveButtonHeight,
                saveButtonWidth,
                saveButtonHeight);
            this.saveButton.Click += this.SaveButton_Click;

            this.AcceptButton = this.saveButton;
            this.Controls.Add(infoLabel);
            this.Controls.Add(languageLabel);
            this.Controls.Add(this.languageComboBox);
            this.Controls.Add(this.saveButton);
        }

        public string SelectedLanguageCode
        {
            get
            {
                LanguageOption option = this.languageComboBox.SelectedItem as LanguageOption;
                return option != null ? option.Code : "de";
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!this.allowClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                return;
            }

            base.OnFormClosing(e);
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            this.allowClose = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }

    internal sealed class TransparencyForm : Form
    {
        private readonly TrackBar transparencyTrackBar;
        private readonly Label valueLabel;
        private readonly Action<int> previewTransparency;

        public TransparencyForm(int transparencyPercent, Action<int> previewTransparency)
        {
            this.previewTransparency = previewTransparency;
            this.Text = UiLanguage.Get("Transparency.Title", "Transparenz");
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.ClientSize = new Size(320, 160);
            this.Font = SystemFonts.MessageBoxFont;
            this.TopMost = true;

            Label infoLabel = new Label();
            infoLabel.AutoSize = false;
            infoLabel.Text = UiLanguage.Get(
                "Transparency.Info",
                "Stelle die Transparenz des gesamten Anzeigefelds von 0 % bis 100 % ein.");
            infoLabel.SetBounds(16, 14, 288, 34);

            this.valueLabel = new Label();
            this.valueLabel.AutoSize = false;
            this.valueLabel.TextAlign = ContentAlignment.MiddleRight;
            this.valueLabel.SetBounds(214, 52, 90, 24);

            this.transparencyTrackBar = new TrackBar();
            this.transparencyTrackBar.Minimum = 0;
            this.transparencyTrackBar.Maximum = 100;
            this.transparencyTrackBar.TickFrequency = 10;
            this.transparencyTrackBar.SmallChange = 1;
            this.transparencyTrackBar.LargeChange = 10;
            this.transparencyTrackBar.Value = Math.Max(0, Math.Min(100, transparencyPercent));
            this.transparencyTrackBar.SetBounds(16, 74, 288, 40);
            this.transparencyTrackBar.ValueChanged += this.TransparencyTrackBar_ValueChanged;

            Button saveButton = new Button();
            saveButton.Text = UiLanguage.Get("Transparency.Save", "Speichern");
            saveButton.DialogResult = DialogResult.OK;
            saveButton.SetBounds(142, 120, 78, 28);
            saveButton.Click += delegate
            {
                if (this.previewTransparency != null)
                {
                    this.previewTransparency(this.transparencyTrackBar.Value);
                }
            };

            Button cancelButton = new Button();
            cancelButton.Text = UiLanguage.Get("Transparency.Close", "Schliessen");
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.SetBounds(226, 120, 78, 28);

            this.AcceptButton = saveButton;
            this.CancelButton = cancelButton;
            this.Controls.Add(infoLabel);
            this.Controls.Add(this.valueLabel);
            this.Controls.Add(this.transparencyTrackBar);
            this.Controls.Add(saveButton);
            this.Controls.Add(cancelButton);
            this.UpdateValueLabel();
            if (this.previewTransparency != null)
            {
                this.previewTransparency(this.transparencyTrackBar.Value);
            }
        }

        public int SelectedTransparencyPercent
        {
            get
            {
                return this.transparencyTrackBar.Value;
            }
        }

        private void TransparencyTrackBar_ValueChanged(object sender, EventArgs e)
        {
            this.UpdateValueLabel();
            if (this.previewTransparency != null)
            {
                this.previewTransparency(this.transparencyTrackBar.Value);
            }
        }

        private void UpdateValueLabel()
        {
            this.valueLabel.Text = UiLanguage.Format(
                "Transparency.ValueFormat",
                "{0} %",
                this.transparencyTrackBar.Value);
        }
    }

    internal sealed class AdapterListItem
    {
        public AdapterListItem(string id, string name, string displayText)
        {
            this.Id = id ?? string.Empty;
            this.Name = name ?? string.Empty;
            this.DisplayText = displayText ?? string.Empty;
        }

        public string Id { get; private set; }

        public string Name { get; private set; }

        public string DisplayText { get; private set; }

        public override string ToString()
        {
            return this.DisplayText;
        }
    }

    internal struct NetworkSnapshot
    {
        private const int IfMaxStringSize = 256;
        private const int IfMaxPhysAddressLength = 32;
        private const uint NoError = 0U;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MibIfRow2
        {
            public ulong InterfaceLuid;
            public uint InterfaceIndex;
            public Guid InterfaceGuid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = IfMaxStringSize + 1)]
            public string Alias;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = IfMaxStringSize + 1)]
            public string Description;
            public uint PhysicalAddressLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = IfMaxPhysAddressLength)]
            public byte[] PhysicalAddress;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = IfMaxPhysAddressLength)]
            public byte[] PermanentPhysicalAddress;
            public uint Mtu;
            public uint Type;
            public uint TunnelType;
            public uint MediaType;
            public uint PhysicalMediumType;
            public uint AccessType;
            public uint DirectionType;
            public byte InterfaceAndOperStatusFlags;
            public uint OperStatus;
            public uint AdminStatus;
            public uint MediaConnectState;
            public Guid NetworkGuid;
            public uint ConnectionType;
            public ulong TransmitLinkSpeed;
            public ulong ReceiveLinkSpeed;
            public ulong InOctets;
            public ulong InUcastPkts;
            public ulong InNUcastPkts;
            public ulong InDiscards;
            public ulong InErrors;
            public ulong InUnknownProtos;
            public ulong InUcastOctets;
            public ulong InMulticastOctets;
            public ulong InBroadcastOctets;
            public ulong OutOctets;
            public ulong OutUcastPkts;
            public ulong OutNUcastPkts;
            public ulong OutDiscards;
            public ulong OutErrors;
            public ulong OutUcastOctets;
            public ulong OutMulticastOctets;
            public ulong OutBroadcastOctets;
            public ulong OutQLen;
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetIfEntry2(ref MibIfRow2 row);

        public NetworkSnapshot(long bytesReceived, long bytesSent, int adapterCount, string displayName)
        {
            this.BytesReceived = bytesReceived;
            this.BytesSent = bytesSent;
            this.AdapterCount = adapterCount;
            this.DisplayName = displayName ?? string.Empty;
        }

        public long BytesReceived;
        public long BytesSent;
        public int AdapterCount;
        public string DisplayName;

        public bool HasAdapters
        {
            get { return this.AdapterCount > 0; }
        }

        public long TotalBytes
        {
            get { return this.BytesReceived + this.BytesSent; }
        }

        public static List<AdapterListItem> GetAdapterItems()
        {
            List<AdapterListItem> items = new List<AdapterListItem>();
            NetworkInterface[] interfaces;

            try
            {
                interfaces = NetworkInterface.GetAllNetworkInterfaces();
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce("network-getallnetworkinterfaces-adapterlist", "Failed to enumerate network interfaces for adapter list.", ex);
                return items;
            }

            foreach (NetworkInterface networkInterface in interfaces)
            {
                if (!IsSelectable(networkInterface))
                {
                    continue;
                }

                OperationalStatus operationalStatus;
                string stateText = TryGetOperationalStatus(networkInterface, out operationalStatus) &&
                    operationalStatus == OperationalStatus.Up
                    ? "aktiv"
                    : "inaktiv";
                string displayText = string.Format("{0} ({1})", networkInterface.Name, stateText);
                items.Add(new AdapterListItem(networkInterface.Id, networkInterface.Name, displayText));
            }

            items.Sort(
                delegate(AdapterListItem left, AdapterListItem right)
                {
                    return string.Compare(left.DisplayText, right.DisplayText, StringComparison.CurrentCultureIgnoreCase);
                });

            return items;
        }

        public static NetworkSnapshot Capture(MonitorSettings settings)
        {
            NetworkInterface[] interfaces;

            try
            {
                interfaces = NetworkInterface.GetAllNetworkInterfaces();
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce("network-getallnetworkinterfaces-capture", "Failed to enumerate network interfaces for capture.", ex);
                return new NetworkSnapshot(0L, 0L, 0, string.Empty);
            }

            if (settings != null && !string.IsNullOrEmpty(settings.AdapterId))
            {
                return CaptureSelectedAdapter(interfaces, settings);
            }

            return CaptureAutomatic(interfaces);
        }

        private static NetworkSnapshot CaptureAutomatic(NetworkInterface[] interfaces)
        {
            long received = 0L;
            long sent = 0L;
            int adapterCount = 0;
            string primaryAdapterName = string.Empty;
            long fastestAdapterSpeed = long.MinValue;

            foreach (NetworkInterface networkInterface in interfaces)
            {
                if (!IsCapturable(networkInterface))
                {
                    continue;
                }

                long bytesReceived;
                long bytesSent;
                if (!TryReadStatistics(networkInterface, out bytesReceived, out bytesSent))
                {
                    continue;
                }

                long speed;
                TryGetInterfaceSpeed(networkInterface, out speed);
                received += bytesReceived;
                sent += bytesSent;
                adapterCount++;

                if (string.IsNullOrEmpty(primaryAdapterName) || speed > fastestAdapterSpeed)
                {
                    primaryAdapterName = networkInterface.Name;
                    fastestAdapterSpeed = speed;
                }
            }

            return new NetworkSnapshot(received, sent, adapterCount, primaryAdapterName);
        }

        private static NetworkSnapshot CaptureSelectedAdapter(NetworkInterface[] interfaces, MonitorSettings settings)
        {
            foreach (NetworkInterface networkInterface in interfaces)
            {
                if (!IsSelectable(networkInterface))
                {
                    continue;
                }

                if (!string.Equals(networkInterface.Id, settings.AdapterId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!IsCapturable(networkInterface))
                {
                    return new NetworkSnapshot(0L, 0L, 0, networkInterface.Name);
                }

                long bytesReceived;
                long bytesSent;
                if (!TryReadStatistics(networkInterface, out bytesReceived, out bytesSent))
                {
                    return new NetworkSnapshot(0L, 0L, 0, networkInterface.Name);
                }

                return new NetworkSnapshot(bytesReceived, bytesSent, 1, networkInterface.Name);
            }

            return new NetworkSnapshot(0L, 0L, 0, settings.AdapterName);
        }

        private static bool IsSelectable(NetworkInterface networkInterface)
        {
            if (networkInterface == null)
            {
                return false;
            }

            NetworkInterfaceType type;
            try
            {
                type = networkInterface.NetworkInterfaceType;
            }
            catch (NetworkInformationException)
            {
                return false;
            }

            if (type == NetworkInterfaceType.Loopback ||
                type == NetworkInterfaceType.Tunnel ||
                type == NetworkInterfaceType.Unknown)
            {
                return false;
            }

            try
            {
                if (networkInterface.IsReceiveOnly)
                {
                    return false;
                }
            }
            catch (NetworkInformationException)
            {
                return false;
            }

            return SupportsTrafficProtocols(networkInterface);
        }

        private static bool IsCapturable(NetworkInterface networkInterface)
        {
            OperationalStatus operationalStatus;
            return IsSelectable(networkInterface) &&
                TryGetOperationalStatus(networkInterface, out operationalStatus) &&
                operationalStatus == OperationalStatus.Up &&
                HasUsableUnicastAddress(networkInterface);
        }

        private static bool TryGetInterfaceSpeed(NetworkInterface networkInterface, out long speed)
        {
            speed = 0L;

            if (networkInterface == null)
            {
                return false;
            }

            try
            {
                speed = Math.Max(0L, networkInterface.Speed);
                return true;
            }
            catch (NetworkInformationException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }

        private static bool TryReadStatistics(
            NetworkInterface networkInterface,
            out long bytesReceived,
            out long bytesSent)
        {
            bytesReceived = 0L;
            bytesSent = 0L;

            if (networkInterface == null)
            {
                return false;
            }

            if (TryReadInterfaceOctetStatistics(networkInterface, out bytesReceived, out bytesSent))
            {
                return true;
            }

            AppLog.WarnOnce(
                "network-stats-fallback-ipv4-" + (networkInterface.Id ?? string.Empty),
                string.Format(
                    "Falling back to IPv4 statistics for adapter '{0}' after interface-counter measurement was unavailable.",
                    networkInterface.Name ?? string.Empty));

            try
            {
                IPv4InterfaceStatistics statistics = networkInterface.GetIPv4Statistics();
                bytesReceived = statistics.BytesReceived;
                bytesSent = statistics.BytesSent;
                return true;
            }
            catch (NetworkInformationException)
            {
                AppLog.WarnOnce(
                    "network-ipv4stats-failed-" + (networkInterface.Id ?? string.Empty),
                    string.Format(
                        "IPv4 statistics are unavailable for adapter '{0}'.",
                        networkInterface.Name ?? string.Empty));
                return false;
            }
            catch (NotImplementedException)
            {
                AppLog.WarnOnce(
                    "network-ipv4stats-notimplemented-" + (networkInterface.Id ?? string.Empty),
                    string.Format(
                        "IPv4 statistics are not implemented for adapter '{0}'.",
                        networkInterface.Name ?? string.Empty));
                return false;
            }
        }

        private static bool TryReadInterfaceOctetStatistics(
            NetworkInterface networkInterface,
            out long bytesReceived,
            out long bytesSent)
        {
            bytesReceived = 0L;
            bytesSent = 0L;

            uint interfaceIndex;
            if (!TryGetInterfaceIndex(networkInterface, out interfaceIndex) || interfaceIndex == 0U)
            {
                return false;
            }

            try
            {
                MibIfRow2 row = CreateEmptyMibIfRow2();
                row.InterfaceIndex = interfaceIndex;
                uint result = GetIfEntry2(ref row);
                if (result != NoError)
                {
                    AppLog.WarnOnce(
                        "network-getifentry2-result-" + interfaceIndex.ToString(),
                        string.Format(
                            "GetIfEntry2 failed for interface index {0} with result {1}; falling back if possible.",
                            interfaceIndex,
                            result));
                    return false;
                }

                bytesReceived = ConvertUnsignedCounter(row.InOctets);
                bytesSent = ConvertUnsignedCounter(row.OutOctets);
                return true;
            }
            catch (DllNotFoundException)
            {
                AppLog.WarnOnce(
                    "network-getifentry2-dllmissing",
                    "iphlpapi.dll is not available; falling back from interface octet counters.");
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                AppLog.WarnOnce(
                    "network-getifentry2-entrypoint",
                    "GetIfEntry2 is not available; falling back from interface octet counters.");
                return false;
            }
            catch (TypeLoadException)
            {
                AppLog.WarnOnce(
                    "network-getifentry2-typeload",
                    "Native interface-counter structure could not be loaded; falling back from interface octet counters.");
                return false;
            }
        }

        private static bool TryGetInterfaceIndex(NetworkInterface networkInterface, out uint interfaceIndex)
        {
            interfaceIndex = 0U;

            IPInterfaceProperties properties;
            if (!TryGetIpProperties(networkInterface, out properties) || properties == null)
            {
                return false;
            }

            try
            {
                IPv4InterfaceProperties ipv4Properties = properties.GetIPv4Properties();
                if (ipv4Properties != null && ipv4Properties.Index > 0)
                {
                    interfaceIndex = (uint)ipv4Properties.Index;
                    return true;
                }
            }
            catch (NetworkInformationException)
            {
            }

            try
            {
                IPv6InterfaceProperties ipv6Properties = properties.GetIPv6Properties();
                if (ipv6Properties != null && ipv6Properties.Index > 0)
                {
                    interfaceIndex = (uint)ipv6Properties.Index;
                    return true;
                }
            }
            catch (NetworkInformationException)
            {
            }

            return false;
        }

        private static bool SupportsTrafficProtocols(NetworkInterface networkInterface)
        {
            try
            {
                return networkInterface.Supports(NetworkInterfaceComponent.IPv4) ||
                    networkInterface.Supports(NetworkInterfaceComponent.IPv6);
            }
            catch (NetworkInformationException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }

        private static bool TryGetOperationalStatus(NetworkInterface networkInterface, out OperationalStatus operationalStatus)
        {
            operationalStatus = OperationalStatus.Unknown;

            if (networkInterface == null)
            {
                return false;
            }

            try
            {
                operationalStatus = networkInterface.OperationalStatus;
                return true;
            }
            catch (NetworkInformationException)
            {
                return false;
            }
        }

        private static bool HasUsableUnicastAddress(NetworkInterface networkInterface)
        {
            IPInterfaceProperties properties;
            if (!TryGetIpProperties(networkInterface, out properties) || properties == null)
            {
                return false;
            }

            foreach (UnicastIPAddressInformation addressInformation in properties.UnicastAddresses)
            {
                if (addressInformation == null || addressInformation.Address == null)
                {
                    continue;
                }

                if (System.Net.IPAddress.IsLoopback(addressInformation.Address))
                {
                    continue;
                }

                if (addressInformation.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ||
                    addressInformation.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                {
                    return true;
                }
            }

            return false;
        }


        private static bool TryGetIpProperties(NetworkInterface networkInterface, out IPInterfaceProperties properties)
        {
            properties = null;

            if (networkInterface == null)
            {
                return false;
            }

            try
            {
                properties = networkInterface.GetIPProperties();
                return properties != null;
            }
            catch (NetworkInformationException)
            {
                return false;
            }
        }

        private static MibIfRow2 CreateEmptyMibIfRow2()
        {
            MibIfRow2 row = new MibIfRow2();
            row.Alias = string.Empty;
            row.Description = string.Empty;
            row.PhysicalAddress = new byte[IfMaxPhysAddressLength];
            row.PermanentPhysicalAddress = new byte[IfMaxPhysAddressLength];
            return row;
        }

        private static long ConvertUnsignedCounter(ulong value)
        {
            return value > long.MaxValue ? long.MaxValue : (long)value;
        }
    }

    internal static class AppIconFactory
    {
        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);

        public static Icon CreateAppIcon()
        {
            Bitmap bitmap = new Bitmap(64, 64);

            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (SolidBrush panelBrush = new SolidBrush(Color.FromArgb(18, 34, 82)))
            using (Pen panelPen = new Pen(Color.FromArgb(240, 246, 252), 3F))
            using (SolidBrush orangeBrush = new SolidBrush(Color.FromArgb(255, 155, 0)))
            using (SolidBrush greenBrush = new SolidBrush(Color.FromArgb(64, 255, 96)))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);

                Rectangle panelBounds = new Rectangle(6, 6, 52, 52);
                using (GraphicsPath path = CreateRoundedPath(panelBounds, 14))
                {
                    graphics.FillPath(panelBrush, path);
                    graphics.DrawPath(panelPen, path);
                }

                FillArrow(graphics, new Rectangle(14, 16, 14, 28), orangeBrush, false);
                FillArrow(graphics, new Rectangle(36, 16, 14, 28), greenBrush, true);
            }

            IntPtr handle = bitmap.GetHicon();

            try
            {
                using (Icon temporary = Icon.FromHandle(handle))
                {
                    return (Icon)temporary.Clone();
                }
            }
            finally
            {
                DestroyIcon(handle);
                bitmap.Dispose();
            }
        }

        private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = Math.Max(2, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
            Rectangle arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

            path.AddArc(arc, 180F, 90F);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270F, 90F);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0F, 90F);
            arc.X = bounds.Left;
            path.AddArc(arc, 90F, 90F);
            path.CloseFigure();
            return path;
        }

        private static void FillArrow(Graphics graphics, Rectangle bounds, Brush brush, bool upward)
        {
            Point[] points;

            if (upward)
            {
                points = new Point[]
                {
                    new Point(bounds.Left + (bounds.Width / 2), bounds.Top),
                    new Point(bounds.Right, bounds.Top + 10),
                    new Point(bounds.Left + 9, bounds.Top + 10),
                    new Point(bounds.Left + 9, bounds.Bottom),
                    new Point(bounds.Left + 5, bounds.Bottom),
                    new Point(bounds.Left + 5, bounds.Top + 10),
                    new Point(bounds.Left, bounds.Top + 10)
                };
            }
            else
            {
                points = new Point[]
                {
                    new Point(bounds.Left + 5, bounds.Top),
                    new Point(bounds.Left + 9, bounds.Top),
                    new Point(bounds.Left + 9, bounds.Bottom - 10),
                    new Point(bounds.Right, bounds.Bottom - 10),
                    new Point(bounds.Left + (bounds.Width / 2), bounds.Bottom),
                    new Point(bounds.Left, bounds.Bottom - 10),
                    new Point(bounds.Left + 5, bounds.Bottom - 10)
                };
            }

            graphics.FillPolygon(brush, points);
        }
    }
}
