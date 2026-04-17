using System;
using System.Collections.Generic;
using System.Diagnostics;
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
[assembly: AssemblyVersion("1.4.23.0")]
[assembly: AssemblyFileVersion("1.4.23.0")]

namespace TrafficView
{
    internal enum AdapterAvailabilityState
    {
        Automatic,
        Available,
        Inactive,
        Missing
    }

    internal enum PopupDisplayMode
    {
        Standard,
        MiniGraph,
        MiniSoft
    }

    internal enum PopupSectionMode
    {
        Both,
        LeftOnly,
        RightOnly
    }

    internal enum AppBarEdge : uint
    {
        Left = 0,
        Top = 1,
        Right = 2,
        Bottom = 3
    }

    internal static class Program
    {
        private const string SingleInstanceMutexName = @"Local\TrafficView.SingleInstance";

        [STAThread]
        private static void Main()
        {
            System.Threading.Mutex singleInstanceMutex = null;
            bool ownsSingleInstanceMutex = false;

            try
            {
                singleInstanceMutex = new System.Threading.Mutex(false, SingleInstanceMutexName);

                try
                {
                    ownsSingleInstanceMutex = singleInstanceMutex.WaitOne(0, false);
                }
                catch (System.Threading.AbandonedMutexException)
                {
                    ownsSingleInstanceMutex = true;
                }

                if (!ownsSingleInstanceMutex)
                {
                    AppLog.WarnOnce(
                        "single-instance-already-running",
                        "Ein zweiter Start von TrafficView wurde blockiert, weil bereits eine Instanz aktiv ist.");
                    MessageBox.Show(
                        "TrafficView ist bereits gestartet.\r\n\r\nBitte verwende die bereits laufende Instanz im Infobereich von Windows.",
                        "TrafficView",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

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
            finally
            {
                if (singleInstanceMutex != null)
                {
                    if (ownsSingleInstanceMutex)
                    {
                        try
                        {
                            singleInstanceMutex.ReleaseMutex();
                        }
                        catch
                        {
                        }
                    }

                    singleInstanceMutex.Dispose();
                }
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

    [StructLayout(LayoutKind.Sequential)]
    internal struct AppBarData
    {
        public int CbSize;
        public IntPtr HWnd;
        public uint CallbackMessage;
        public AppBarEdge UEdge;
        public NativeRect Rc;
        public IntPtr LParam;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct BlendFunction
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    internal struct TaskbarVisualTheme
    {
        public Color TaskbarColor;
        public Color BaseColor;
        public Color BorderColor;
        public Color DividerColor;
        public byte OverlayAlpha;
    }

    internal sealed class TaskbarIntegrationSnapshot
    {
        public IntPtr TaskbarHandle { get; set; }

        public IntPtr TaskbarZOrderAnchorHandle { get; set; }

        public AppBarEdge Edge { get; set; }

        public Rectangle Bounds { get; set; }

        public Rectangle ScreenBounds { get; set; }

        public Rectangle[] OccupiedBounds { get; set; }

        public bool AutoHide { get; set; }

        public bool IsHidden { get; set; }

        public bool UsesCustomTaskListHeuristic { get; set; }

        public TaskbarVisualTheme Theme { get; set; }

        public bool IsVertical
        {
            get { return this.Edge == AppBarEdge.Left || this.Edge == AppBarEdge.Right; }
        }
    }

    internal sealed class TrafficViewContext : ApplicationContext
    {
        private const string CompanyLogoFileName = "LOLO-SOFT_00_SW.png";

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
        private readonly ToolStripMenuItem dataUsageItem;
        private readonly ToolStripMenuItem transparencyItem;
        private readonly ToolStripMenuItem sizeItem;
        private readonly ToolStripMenuItem displayModeItem;
        private readonly ToolStripMenuItem sectionModeItem;
        private readonly ToolStripMenuItem taskbarIntegrationItem;
        private readonly ToolStripMenuItem taskbarIntegrationOnItem;
        private readonly ToolStripMenuItem taskbarIntegrationOffItem;
        private readonly ToolStripMenuItem rotatingGlossItem;
        private readonly ToolStripMenuItem activityBorderGlowItem;
        private readonly ToolStripMenuItem skinItem;
        private readonly ToolStripMenuItem deleteSkinItem;
        private readonly ToolStripMenuItem languageItem;
        private readonly ToolStripMenuItem exitItem;
        private readonly ContextMenuStrip sharedMenu;
        private readonly Icon notifyIconHandle;
        private readonly Image companyLogoImage;
        private readonly ToolStripControlHost companyLogoHost;
        private readonly string menuVersionNumber;
        private Label menuVersionLabel;
        private readonly Dictionary<string, ToolStripMenuItem> languageMenuItems;
        private readonly Dictionary<int, ToolStripMenuItem> popupScaleMenuItems;
        private readonly Dictionary<PopupDisplayMode, ToolStripMenuItem> displayModeMenuItems;
        private readonly Dictionary<PopupSectionMode, ToolStripMenuItem> sectionModeMenuItems;
        private readonly Dictionary<string, ToolStripMenuItem> panelSkinMenuItems;
        private SharedMenuOpenSource sharedMenuOpenSource;
        private MonitorSettings settings;
        private readonly TrafficUsageLog trafficUsageLog;
        private DateTime lastTrafficUsageFlushUtc = DateTime.MinValue;

        public TrafficViewContext()
        {
            this.settings = MonitorSettings.Load();
            this.PersistCurrentSettings(false);
            this.trafficUsageLog = new TrafficUsageLog();
            UiLanguage.Initialize(this.settings.LanguageCode);
            this.popupForm = new TrafficPopupForm(this.settings);
            this.popupForm.RatesUpdated += this.PopupForm_RatesUpdated;
            this.popupForm.TrafficUsageMeasured += this.PopupForm_TrafficUsageMeasured;
            this.popupForm.OverlayMenuRequested += this.PopupForm_OverlayMenuRequested;
            this.popupForm.OverlayLocationCommitted += this.PopupForm_OverlayLocationCommitted;
            this.popupForm.TaskbarIntegrationNoSpaceAcknowledged += this.PopupForm_TaskbarIntegrationNoSpaceAcknowledged;
            this.languageMenuItems = new Dictionary<string, ToolStripMenuItem>(StringComparer.OrdinalIgnoreCase);
            this.popupScaleMenuItems = new Dictionary<int, ToolStripMenuItem>();
            this.displayModeMenuItems = new Dictionary<PopupDisplayMode, ToolStripMenuItem>();
            this.sectionModeMenuItems = new Dictionary<PopupSectionMode, ToolStripMenuItem>();
            this.panelSkinMenuItems = new Dictionary<string, ToolStripMenuItem>(StringComparer.OrdinalIgnoreCase);
            this.menuVersionNumber = GetMenuVersionNumber();

            this.sharedMenu = new ContextMenuStrip();
            this.sharedMenu.RenderMode = ToolStripRenderMode.System;
            this.sharedMenu.ShowImageMargin = false;
            this.sharedMenu.Padding = Padding.Empty;
            this.sharedMenu.Font = new Font(
                SystemFonts.MenuFont.FontFamily,
                Math.Max(10F, SystemFonts.MenuFont.Size),
                FontStyle.Regular,
                GraphicsUnit.Point);
            this.companyLogoImage = LoadCompanyLogoImage();
            this.companyLogoHost = this.CreateCompanyLogoHost(this.companyLogoImage, this.GetMenuVersionText());
            this.toggleItem = new ToolStripMenuItem(string.Empty, null, this.ToggleItem_Click);
            this.calibrationStatusItem = new ToolStripMenuItem(string.Empty);
            this.calibrationStatusItem.Enabled = false;
            this.calibrationItem = new ToolStripMenuItem(string.Empty, null, this.CalibrationItem_Click);
            this.dataUsageItem = new ToolStripMenuItem(string.Empty, null, this.DataUsageItem_Click);
            this.transparencyItem = new ToolStripMenuItem(string.Empty, null, this.TransparencyItem_Click);
            this.sizeItem = new ToolStripMenuItem(string.Empty);
            this.displayModeItem = new ToolStripMenuItem(string.Empty);
            this.sectionModeItem = new ToolStripMenuItem(string.Empty);
            this.taskbarIntegrationItem = new ToolStripMenuItem(string.Empty);
            this.taskbarIntegrationOnItem = new ToolStripMenuItem(string.Empty, null, this.TaskbarIntegrationMenuItem_Click);
            this.taskbarIntegrationOffItem = new ToolStripMenuItem(string.Empty, null, this.TaskbarIntegrationMenuItem_Click);
            this.taskbarIntegrationOnItem.Tag = true;
            this.taskbarIntegrationOffItem.Tag = false;
            this.taskbarIntegrationItem.DropDownItems.Add(this.taskbarIntegrationOnItem);
            this.taskbarIntegrationItem.DropDownItems.Add(this.taskbarIntegrationOffItem);
            this.rotatingGlossItem = new ToolStripMenuItem(string.Empty, null, this.RotatingGlossItem_Click);
            this.activityBorderGlowItem = new ToolStripMenuItem(string.Empty, null, this.ActivityBorderGlowItem_Click);
            this.skinItem = new ToolStripMenuItem(string.Empty);
            this.deleteSkinItem = new ToolStripMenuItem(string.Empty, null, this.DeleteSkinItem_Click);
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

            PopupDisplayMode[] popupDisplayModes = new PopupDisplayMode[]
            {
                PopupDisplayMode.Standard,
                PopupDisplayMode.MiniGraph,
                PopupDisplayMode.MiniSoft
            };
            for (int i = 0; i < popupDisplayModes.Length; i++)
            {
                ToolStripMenuItem item = new ToolStripMenuItem(string.Empty, null, this.DisplayModeMenuItem_Click);
                item.Tag = popupDisplayModes[i];
                this.displayModeMenuItems[popupDisplayModes[i]] = item;
                this.displayModeItem.DropDownItems.Add(item);
            }
            this.displayModeItem.DropDownItems.Add(new ToolStripSeparator());
            this.displayModeItem.DropDownItems.Add(this.rotatingGlossItem);
            this.displayModeItem.DropDownItems.Add(this.activityBorderGlowItem);

            PopupSectionMode[] popupSectionModes = new PopupSectionMode[]
            {
                PopupSectionMode.Both,
                PopupSectionMode.LeftOnly,
                PopupSectionMode.RightOnly
            };
            for (int i = 0; i < popupSectionModes.Length; i++)
            {
                ToolStripMenuItem item = new ToolStripMenuItem(string.Empty, null, this.SectionModeMenuItem_Click);
                item.Tag = popupSectionModes[i];
                this.sectionModeMenuItems[popupSectionModes[i]] = item;
                this.sectionModeItem.DropDownItems.Add(item);
            }

            if (this.companyLogoHost != null)
            {
                this.sharedMenu.Items.Add(this.companyLogoHost);
                this.sharedMenu.Items.Add(new ToolStripSeparator());
            }

            this.sharedMenu.Items.Add(this.toggleItem);
            this.sharedMenu.Items.Add(this.calibrationStatusItem);
            this.sharedMenu.Items.Add(this.calibrationItem);
            this.sharedMenu.Items.Add(this.dataUsageItem);
            this.sharedMenu.Items.Add(this.transparencyItem);
            this.sharedMenu.Items.Add(this.sizeItem);
            this.sharedMenu.Items.Add(this.displayModeItem);
            this.sharedMenu.Items.Add(this.sectionModeItem);
            this.sharedMenu.Items.Add(this.taskbarIntegrationItem);
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
            this.PersistCurrentSettings(true);
            this.trafficUsageLog.FlushPending();
            this.notifyIcon.Visible = false;
            this.notifyIcon.Dispose();
            this.sharedMenu.Dispose();
            if (this.companyLogoImage != null)
            {
                this.companyLogoImage.Dispose();
            }
            this.popupForm.Dispose();
            this.notifyIconHandle.Dispose();
            base.ExitThreadCore();
        }

        private void PersistCurrentSettings(bool includePopupLocation)
        {
            if (this.settings == null)
            {
                return;
            }

            try
            {
                if (!this.settings.TaskbarIntegrationEnabled &&
                    includePopupLocation &&
                    this.popupForm != null &&
                    !this.popupForm.IsDisposed)
                {
                    this.settings = this.settings.WithPopupLocation(this.popupForm.Location);
                }

                this.settings.Save();
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce(
                    includePopupLocation
                        ? "settings-persist-on-exit-failed"
                        : "settings-persist-on-startup-failed",
                    includePopupLocation
                        ? "Die aktuellen Einstellungen konnten beim Beenden nicht gesichert werden."
                        : "Die aktuellen Einstellungen konnten beim Start nicht materialisiert werden.",
                    ex);
            }
        }

        private static Image LoadCompanyLogoImage()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, CompanyLogoFileName);
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (Image image = Image.FromStream(stream))
                {
                    return new Bitmap(image);
                }
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce(
                    "company-logo-load-failed",
                    string.Format("Firmenlogo konnte nicht aus '{0}' geladen werden.", path),
                    ex);
                return null;
            }
        }

        private static string GetMenuVersionNumber()
        {
            Version version = typeof(Program).Assembly.GetName().Version;
            if (version == null)
            {
                return "?";
            }

            return string.Format(
                "{0}.{1}.{2:00}",
                version.Major,
                version.Minor,
                version.Build);
        }

        private string GetMenuVersionText()
        {
            return UiLanguage.Format(
                "Menu.VersionFormat",
                "Version {0}",
                this.menuVersionNumber);
        }

        private ToolStripControlHost CreateCompanyLogoHost(Image logoImage, string versionText)
        {
            if (logoImage == null)
            {
                return null;
            }

            Image trimmedLogoImage = TrimMenuLogoImage(logoImage);
            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Size = new Size(286, 72);
            panel.Margin = Padding.Empty;
            panel.Padding = Padding.Empty;
            panel.BackColor = SystemColors.Menu;
            panel.ColumnCount = 2;
            panel.RowCount = 1;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            Panel logoPanel = new Panel();
            logoPanel.Dock = DockStyle.Fill;
            logoPanel.Margin = Padding.Empty;
            logoPanel.Padding = Padding.Empty;
            logoPanel.BackColor = SystemColors.Menu;
            logoPanel.Cursor = Cursors.Hand;
            logoPanel.Click += this.CompanyLogo_Click;

            int logoWidth = Math.Min(82, Math.Max(1, (int)Math.Round(72D * trimmedLogoImage.Width / trimmedLogoImage.Height)));
            PictureBox pictureBox = new PictureBox();
            pictureBox.Location = new Point(0, 0);
            pictureBox.Size = new Size(logoWidth, 72);
            pictureBox.Margin = Padding.Empty;
            pictureBox.Padding = Padding.Empty;
            pictureBox.TabStop = false;
            pictureBox.Image = trimmedLogoImage;
            pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox.BackColor = SystemColors.Menu;
            pictureBox.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            pictureBox.Cursor = Cursors.Hand;
            pictureBox.Click += this.CompanyLogo_Click;
            logoPanel.Controls.Add(pictureBox);

            Label versionLabel = new Label();
            versionLabel.Dock = DockStyle.Fill;
            versionLabel.Margin = Padding.Empty;
            versionLabel.Padding = new Padding(0, 0, 8, 0);
            versionLabel.AutoSize = false;
            versionLabel.Text = string.IsNullOrWhiteSpace(versionText) ? "?" : versionText;
            versionLabel.TextAlign = ContentAlignment.MiddleCenter;
            versionLabel.Font = new Font(
                this.sharedMenu.Font.FontFamily,
                this.sharedMenu.Font.Size + 0.5F,
                FontStyle.Bold,
                GraphicsUnit.Point);
            versionLabel.BackColor = SystemColors.Menu;
            this.menuVersionLabel = versionLabel;

            panel.Controls.Add(logoPanel, 0, 0);
            panel.Controls.Add(versionLabel, 1, 0);

            ToolStripControlHost host = new ToolStripControlHost(panel);
            host.AutoSize = false;
            host.Size = panel.Size;
            host.Margin = Padding.Empty;
            host.Padding = Padding.Empty;
            return host;
        }

        private void CompanyLogo_Click(object sender, EventArgs e)
        {
            if (this.sharedMenu != null && this.sharedMenu.Visible)
            {
                this.sharedMenu.Close(ToolStripDropDownCloseReason.ItemClicked);
            }

            this.ShowCompanyLogoWindow();
        }

        private void ShowCompanyLogoWindow()
        {
            if (this.companyLogoImage == null)
            {
                return;
            }

            bool pausePopupTopMost = this.popupForm.Visible;
            if (pausePopupTopMost)
            {
                this.popupForm.SuspendTopMostEnforcement();
            }

            try
            {
                Rectangle workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
                int clientWidth = Math.Min(this.companyLogoImage.Width, Math.Max(320, workingArea.Width - 80));
                int clientHeight = Math.Min(this.companyLogoImage.Height, Math.Max(240, workingArea.Height - 80));

                using (Form logoForm = new Form())
                using (Panel containerPanel = new Panel())
                using (PictureBox pictureBox = new PictureBox())
                {
                    logoForm.Text = "LOLO-SOFT";
                    logoForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                    logoForm.StartPosition = FormStartPosition.CenterScreen;
                    logoForm.ShowInTaskbar = false;
                    logoForm.MaximizeBox = false;
                    logoForm.MinimizeBox = false;
                    logoForm.AutoScaleMode = AutoScaleMode.Dpi;
                    logoForm.Font = this.sharedMenu.Font;
                    logoForm.ClientSize = new Size(clientWidth, clientHeight);
                    logoForm.TopMost = true;

                    containerPanel.Dock = DockStyle.Fill;
                    containerPanel.AutoScroll = true;
                    containerPanel.BackColor = Color.White;
                    containerPanel.Padding = Padding.Empty;

                    pictureBox.Location = new Point(0, 0);
                    pictureBox.Size = this.companyLogoImage.Size;
                    pictureBox.Margin = Padding.Empty;
                    pictureBox.Padding = Padding.Empty;
                    pictureBox.Image = this.companyLogoImage;
                    pictureBox.SizeMode = PictureBoxSizeMode.Normal;
                    pictureBox.BackColor = Color.White;

                    containerPanel.Controls.Add(pictureBox);
                    logoForm.Controls.Add(containerPanel);

                    if (this.popupForm.Visible)
                    {
                        logoForm.ShowDialog(this.popupForm);
                    }
                    else
                    {
                        logoForm.ShowDialog();
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

        private static Image TrimMenuLogoImage(Image logoImage)
        {
            Bitmap sourceBitmap = logoImage as Bitmap;
            if (sourceBitmap == null)
            {
                return logoImage;
            }

            int minX = sourceBitmap.Width;
            int minY = sourceBitmap.Height;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < sourceBitmap.Height; y++)
            {
                for (int x = 0; x < sourceBitmap.Width; x++)
                {
                    Color pixel = sourceBitmap.GetPixel(x, y);
                    if (pixel.A <= 8)
                    {
                        continue;
                    }

                    if (pixel.R >= 245 && pixel.G >= 245 && pixel.B >= 245)
                    {
                        continue;
                    }

                    if (x < minX)
                    {
                        minX = x;
                    }

                    if (y < minY)
                    {
                        minY = y;
                    }

                    if (x > maxX)
                    {
                        maxX = x;
                    }

                    if (y > maxY)
                    {
                        maxY = y;
                    }
                }
            }

            if (maxX < minX || maxY < minY)
            {
                return logoImage;
            }

            Rectangle cropBounds = Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
            return sourceBitmap.Clone(cropBounds, sourceBitmap.PixelFormat);
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

        private void DataUsageItem_Click(object sender, EventArgs e)
        {
            this.ShowUsageWindow();
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

        private void DisplayModeMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            if (item == null || !(item.Tag is PopupDisplayMode))
            {
                return;
            }

            PopupDisplayMode popupDisplayMode = (PopupDisplayMode)item.Tag;
            if (this.settings.PopupDisplayMode == popupDisplayMode)
            {
                return;
            }

            this.settings = this.settings.WithPopupDisplayMode(popupDisplayMode);
            this.settings.Save();
            this.popupForm.ApplySettings(this.settings);
            this.UpdateMenuState();
        }

        private void SectionModeMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            if (item == null || !(item.Tag is PopupSectionMode))
            {
                return;
            }

            PopupSectionMode popupSectionMode = (PopupSectionMode)item.Tag;
            if (this.settings.PopupSectionMode == popupSectionMode)
            {
                return;
            }

            if (this.settings.TaskbarIntegrationEnabled)
            {
                this.popupForm.ClearTaskbarDefaultSectionModeOverride();
            }

            this.settings = this.settings.WithPopupSectionMode(popupSectionMode);
            this.settings.Save();
            this.popupForm.ApplySettings(this.settings);

            if (this.popupForm.Visible)
            {
                if (!this.settings.TaskbarIntegrationEnabled)
                {
                    this.settings = this.settings.WithPopupLocation(this.popupForm.Location);
                    this.settings.Save();
                }
                this.popupForm.BringToFrontOnly();
            }

            this.UpdateMenuState();
        }

        private void RotatingGlossItem_Click(object sender, EventArgs e)
        {
            bool nextValue = !this.settings.RotatingMeterGlossEnabled;
            this.settings = this.settings.WithRotatingMeterGlossEnabled(nextValue);
            this.settings.Save();
            this.popupForm.ApplySettings(this.settings);
            this.UpdateMenuState();
        }

        private void ActivityBorderGlowItem_Click(object sender, EventArgs e)
        {
            bool nextValue = !this.settings.ActivityBorderGlowEnabled;
            this.settings = this.settings.WithActivityBorderGlowEnabled(nextValue);
            this.settings.Save();
            this.popupForm.ApplySettings(this.settings);
            this.UpdateMenuState();
        }

        private void TaskbarIntegrationMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            if (item == null || !(item.Tag is bool))
            {
                return;
            }

            bool enableTaskbarIntegration = (bool)item.Tag;
            this.SetTaskbarIntegrationEnabled(enableTaskbarIntegration);
        }

        private void PopupForm_TaskbarIntegrationNoSpaceAcknowledged(object sender, EventArgs e)
        {
            this.SetTaskbarIntegrationEnabled(false, true);
        }

        private void SetTaskbarIntegrationEnabled(bool enableTaskbarIntegration)
        {
            this.SetTaskbarIntegrationEnabled(enableTaskbarIntegration, true);
        }

        private void SetTaskbarIntegrationEnabled(bool enableTaskbarIntegration, bool restorePopupAfterChange)
        {
            if (this.settings.TaskbarIntegrationEnabled == enableTaskbarIntegration)
            {
                return;
            }

            if (enableTaskbarIntegration)
            {
                this.popupForm.ClearTaskbarIntegrationPreferredLocation();
            }

            this.settings = this.settings.WithTaskbarIntegrationEnabled(enableTaskbarIntegration);
            this.settings.Save();
            this.popupForm.ApplySettings(this.settings);
            if (enableTaskbarIntegration)
            {
                this.popupForm.ApplyDefaultTaskbarSectionModeOverride();
            }

            if (restorePopupAfterChange &&
                (this.popupForm.Visible || this.popupForm.HasDeferredVisibilityRequest))
            {
                this.ShowPopupAtPreferredOrSavedLocation(null, false);
            }

            this.UpdateMenuState();
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
                if (!this.settings.TaskbarIntegrationEnabled)
                {
                    this.settings = this.settings.WithPopupLocation(this.popupForm.Location);
                }
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

            this.settings = this.settings
                .WithLanguageCode(languageCode)
                .WithInitialLanguagePromptHandled(true);
            this.settings.Save();
            UiLanguage.SetLanguage(this.settings.LanguageCode);
            this.popupForm.ApplySettings(this.settings);
            this.UpdateMenuState();
        }

        private void PanelSkinMenuItem_Click(object sender, EventArgs e)
        {
            this.EnsureValidSelectedSkin();

            ToolStripMenuItem item = sender as ToolStripMenuItem;
            string panelSkinId = item != null ? item.Tag as string : null;
            if (string.IsNullOrEmpty(panelSkinId))
            {
                return;
            }

            this.ApplyPanelSkin(panelSkinId);
        }

        private void DeleteSkinItem_Click(object sender, EventArgs e)
        {
            if (this.TryBeginInvokePopupForm(new Action(this.ConfirmAndDeleteCurrentSkin)))
            {
                return;
            }

            this.ConfirmAndDeleteCurrentSkin();
        }

        private void ConfirmAndDeleteCurrentSkin()
        {
            PanelSkinDefinition currentSkin = PanelSkinCatalog.GetSkinById(this.settings.PanelSkinId);
            if (currentSkin == null)
            {
                return;
            }

            bool pausePopupTopMost = this.popupForm.Visible;
            if (pausePopupTopMost)
            {
                this.popupForm.SuspendTopMostEnforcement();
            }

            try
            {
                this.ConfirmAndDeleteCurrentSkinCore(currentSkin);
            }
            finally
            {
                if (pausePopupTopMost)
                {
                    this.popupForm.ResumeTopMostEnforcement(false);
                }
            }
        }

        private void ConfirmAndDeleteCurrentSkinCore(PanelSkinDefinition currentSkin)
        {
            string originalSkinId = currentSkin.Id;
            IWin32Window owner = this.popupForm.Visible && !this.popupForm.IsDisposed
                ? (IWin32Window)this.popupForm
                : null;

            if (PanelSkinCatalog.IsProtectedSkinId(currentSkin.Id))
            {
                ShowMessageBox(
                    owner,
                    UiLanguage.Get(
                        "Menu.DeleteSkinProtected",
                        "Der Standardskin 'Normal' kann nicht gelöscht werden."),
                    UiLanguage.Get("Menu.DeleteSkin", "Skin löschen"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                this.RebuildPanelSkinMenuItems();
                this.UpdateMenuState();
                return;
            }

            PanelSkinDefinition[] availableSkins = PanelSkinCatalog.GetAvailableSkins();
            if (availableSkins.Length <= 1)
            {
                ShowMessageBox(
                    owner,
                    UiLanguage.Get(
                        "Menu.DeleteSkinLastBlocked",
                        "Der letzte verbliebene Skin kann nicht gelöscht werden."),
                    UiLanguage.Get("Menu.DeleteSkin", "Skin löschen"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            string currentSkinName = this.GetPanelSkinDisplayName(currentSkin.Id);
            DialogResult confirmation = ShowMessageBox(
                owner,
                UiLanguage.Format(
                    "Menu.DeleteSkinConfirm",
                    "Soll der aktuelle Skin '{0}' wirklich gelöscht werden?",
                    currentSkinName),
                UiLanguage.Get("Menu.DeleteSkin", "Skin löschen"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);

            if (confirmation != DialogResult.Yes)
            {
                return;
            }

            string fallbackSkinId = PanelSkinCatalog.GetDefaultOrFirstSkinId();
            if (!string.Equals(this.settings.PanelSkinId, fallbackSkinId, StringComparison.OrdinalIgnoreCase))
            {
                this.settings = this.settings.WithPanelSkinId(fallbackSkinId);
                this.settings.Save();
                this.popupForm.ApplySettings(this.settings);
                this.UpdateMenuState();
                Application.DoEvents();
            }

            TrafficPopupForm.ReleasePanelBackgroundAssetCache(currentSkin.DirectoryPath);
            string errorMessage;
            if (!PanelSkinCatalog.TryDeleteSkin(currentSkin.Id, out errorMessage))
            {
                string restoredSkinId = fallbackSkinId;
                PanelSkinDefinition restoredSkin = PanelSkinCatalog.GetSkinById(originalSkinId);
                if (restoredSkin != null &&
                    string.Equals(restoredSkin.Id, originalSkinId, StringComparison.OrdinalIgnoreCase))
                {
                    restoredSkinId = restoredSkin.Id;
                }

                ShowMessageBox(
                    owner,
                    string.IsNullOrWhiteSpace(errorMessage)
                        ? UiLanguage.Get("Menu.DeleteSkinFailed", "Der Skin konnte nicht gelöscht werden.")
                        : errorMessage,
                    UiLanguage.Get("Menu.DeleteSkin", "Skin löschen"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                this.settings = this.settings.WithPanelSkinId(restoredSkinId);
                this.settings.Save();
                this.popupForm.ApplySettings(this.settings);
                this.RebuildPanelSkinMenuItems();
                this.UpdateMenuState();
                return;
            }

            this.settings = this.settings.WithPanelSkinId(fallbackSkinId);
            this.settings.Save();
            this.popupForm.ApplySettings(this.settings);
            this.RebuildPanelSkinMenuItems();
            this.UpdateMenuState();
        }

        private static DialogResult ShowMessageBox(
            IWin32Window owner,
            string text,
            string caption,
            MessageBoxButtons buttons,
            MessageBoxIcon icon,
            MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
        {
            return owner == null
                ? MessageBox.Show(text, caption, buttons, icon, defaultButton)
                : MessageBox.Show(owner, text, caption, buttons, icon, defaultButton);
        }

        private void ApplyPanelSkin(string panelSkinId)
        {
            string normalizedPanelSkinId = PanelSkinCatalog.NormalizeSkinId(panelSkinId);
            if (string.Equals(this.settings.PanelSkinId, normalizedPanelSkinId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            this.settings = this.settings.WithPanelSkinId(normalizedPanelSkinId);
            this.settings.Save();
            this.popupForm.ApplySettings(this.settings);
            this.UpdateMenuState();
        }

        private bool EnsureValidSelectedSkin()
        {
            string normalizedPanelSkinId = PanelSkinCatalog.NormalizeSkinId(this.settings.PanelSkinId);
            if (string.Equals(this.settings.PanelSkinId, normalizedPanelSkinId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            this.settings = this.settings.WithPanelSkinId(normalizedPanelSkinId);
            this.settings.Save();
            this.popupForm.ApplySettings(this.settings);
            return true;
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
            bool enableTaskbarIntegration;
            Point desktopLocation;
            if (this.popupForm.TryGetAutomaticTaskbarIntegrationStateChange(out enableTaskbarIntegration, out desktopLocation))
            {
                if (enableTaskbarIntegration)
                {
                    this.SetTaskbarIntegrationEnabled(true, false);
                    this.popupForm.ShowAtRightBiasedTaskbarPlacement(false, true);
                    return;
                }

                this.SetTaskbarIntegrationEnabled(false, false);
                this.popupForm.ShowAtLocation(desktopLocation, false);

                if (!this.settings.HasSavedPopupLocation ||
                    this.settings.PopupLocation != this.popupForm.Location)
                {
                    this.settings = this.settings.WithPopupLocation(this.popupForm.Location);
                    this.settings.Save();
                }

                return;
            }

            if (this.settings.TaskbarIntegrationEnabled)
            {
                return;
            }

            Point popupLocation = this.popupForm.Location;
            if (this.settings.HasSavedPopupLocation &&
                this.settings.PopupLocation == popupLocation)
            {
                return;
            }

            this.settings = this.settings.WithPopupLocation(popupLocation);
            this.settings.Save();
        }

        private void PopupForm_TrafficUsageMeasured(object sender, TrafficUsageMeasuredEventArgs e)
        {
            if (!this.trafficUsageLog.QueueUsage(this.settings, e.DownloadBytes, e.UploadBytes))
            {
                return;
            }

            if (this.trafficUsageLog.PendingRecordCount >= 8 ||
                this.lastTrafficUsageFlushUtc == DateTime.MinValue ||
                (DateTime.UtcNow - this.lastTrafficUsageFlushUtc).TotalSeconds >= 15D)
            {
                if (this.trafficUsageLog.FlushPending())
                {
                    this.lastTrafficUsageFlushUtc = DateTime.UtcNow;
                }
            }
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
            if (this.popupForm.Visible || this.popupForm.HasDeferredVisibilityRequest)
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
            bool popupAvailable = this.HasUsablePopupForm();
            bool popupWasVisible = popupAvailable && this.popupForm.Visible;
            Point popupRestoreLocation = popupAvailable ? this.popupForm.Location : Point.Empty;
            bool shouldRestorePopup = popupWasVisible || keepPopupVisible;
            bool pausePopupTopMost = popupWasVisible && keepPopupVisible;
            MonitorSettings selectedSettings = null;
            bool calibrationConfirmed = false;

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
                    calibrationForm.ShowDialog();

                    if (calibrationForm.SelectedSettings != null)
                    {
                        selectedSettings = calibrationForm.SelectedSettings.Clone();
                        calibrationConfirmed = true;
                    }
                    else if (calibrationForm.SavedAdapterSettings != null)
                    {
                        selectedSettings = calibrationForm.SavedAdapterSettings.Clone();
                    }
                }
            }
            finally
            {
                if (pausePopupTopMost && this.HasUsablePopupForm())
                {
                    this.popupForm.ResumeTopMostEnforcement(false);
                }
            }

            if (selectedSettings != null)
            {
                this.settings = selectedSettings;
                if (calibrationConfirmed)
                {
                    this.settings = this.settings.WithInitialCalibrationPromptHandled(true);
                }
                this.settings.Save();
                if (this.HasUsablePopupForm())
                {
                    this.popupForm.ApplySettings(this.settings);
                }
                this.UpdateMenuState();
            }

            if (shouldRestorePopup)
            {
                this.BringPopupToFront(popupWasVisible ? (Point?)popupRestoreLocation : null);
            }
        }

        private void BringPopupToFront(Point? restoreLocation = null)
        {
            if (!this.HasUsablePopupForm())
            {
                return;
            }

            this.ShowPopupAtPreferredOrSavedLocation(restoreLocation, false);

            if (this.TryBeginInvokePopupForm(new Action(this.popupForm.BringToFrontOnly)))
            {
            }
            else if (this.HasUsablePopupForm())
            {
                this.popupForm.BringToFrontOnly();
            }

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

                if (!this.HasUsablePopupForm())
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
            if (!this.HasUsablePopupForm())
            {
                return;
            }

            Point? effectiveLocation = restoreLocation;
            if (!this.settings.TaskbarIntegrationEnabled &&
                !effectiveLocation.HasValue &&
                this.settings.HasSavedPopupLocation)
            {
                effectiveLocation = this.settings.PopupLocation;
            }

            if (effectiveLocation.HasValue && !this.settings.TaskbarIntegrationEnabled)
            {
                this.popupForm.ShowAtLocation(effectiveLocation.Value, activateWindow);
                if (!this.settings.TaskbarIntegrationEnabled &&
                    this.settings.HasSavedPopupLocation &&
                    this.popupForm.Location != this.settings.PopupLocation)
                {
                    this.settings = this.settings.WithPopupLocation(this.popupForm.Location);
                    this.settings.Save();
                }

                return;
            }

            this.popupForm.ShowNearTray(activateWindow);
        }

        private void PromptInitialCalibrationOnFirstStart()
        {
            if (this.settings.InitialCalibrationPromptHandled &&
                this.settings.HasCalibrationData())
            {
                return;
            }

            if (this.settings.HasCalibrationData())
            {
                this.settings = this.settings.WithInitialCalibrationPromptHandled(true);
                this.settings.Save();
                return;
            }

            this.ShowCalibrationDialog(true);
        }

        private void PromptInitialLanguageOnFirstStart()
        {
            bool hasStoredLanguageSetting = MonitorSettings.HasStoredLanguageSetting();
            if (this.settings.InitialLanguagePromptHandled && hasStoredLanguageSetting)
            {
                return;
            }

            if (hasStoredLanguageSetting)
            {
                this.settings = this.settings.WithInitialLanguagePromptHandled(true);
                this.settings.Save();
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
                    if (this.HasUsablePopupForm())
                    {
                        this.popupForm.ApplySettings(this.settings);
                    }
                    this.UpdateMenuState();
                }
            }
        }

        private bool HasUsablePopupForm()
        {
            return this.popupForm != null && !this.popupForm.IsDisposed;
        }

        private bool CanInvokePopupForm()
        {
            return this.HasUsablePopupForm() && this.popupForm.IsHandleCreated;
        }

        private bool TryBeginInvokePopupForm(Action action)
        {
            if (action == null || !this.CanInvokePopupForm())
            {
                return false;
            }

            try
            {
                this.popupForm.BeginInvoke(action);
                return true;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
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
            this.toggleItem.Text = (this.popupForm.Visible || this.popupForm.HasDeferredVisibilityRequest)
                ? UiLanguage.Get("Menu.Hide", "Ausblenden")
                : UiLanguage.Get("Menu.Show", "Anzeigen");
            this.calibrationItem.Text = UiLanguage.Get("Menu.Calibration", "Kalibration (30 s)...");
            this.dataUsageItem.Text = UiLanguage.Get("Menu.DataUsage", "Datenverbrauch");
            this.transparencyItem.Text = UiLanguage.Format(
                "Menu.TransparencyFormat",
                "Transparenz ({0} %)",
                this.settings.TransparencyPercent);
            this.sizeItem.Text = UiLanguage.Format(
                "Menu.SizeFormat",
                "Göße ({0} %)",
                this.settings.PopupScalePercent);
            this.displayModeItem.Text = UiLanguage.Get(
                "Menu.DisplayMode",
                "Anzeige");
            this.sectionModeItem.Text = UiLanguage.Get(
                "Menu.SectionMode",
                "Bereich");
            this.taskbarIntegrationItem.Text = UiLanguage.Get(
                "Menu.TaskbarIntegration",
                "Taskleistenintegration");
            this.taskbarIntegrationOnItem.Text = UiLanguage.Get(
                "Menu.TaskbarIntegrationOn",
                "ein");
            this.taskbarIntegrationOffItem.Text = UiLanguage.Get(
                "Menu.TaskbarIntegrationOff",
                "aus");
            this.taskbarIntegrationOnItem.Checked = this.settings.TaskbarIntegrationEnabled;
            this.taskbarIntegrationOffItem.Checked = !this.settings.TaskbarIntegrationEnabled;
            this.languageItem.Text = UiLanguage.Get("Menu.Language", "Sprache");
            this.exitItem.Text = UiLanguage.Get("Menu.Exit", "Beenden");
            if (this.menuVersionLabel != null)
            {
                this.menuVersionLabel.Text = this.GetMenuVersionText();
            }

            foreach (KeyValuePair<string, ToolStripMenuItem> pair in this.languageMenuItems)
            {
                pair.Value.Checked = string.Equals(pair.Key, this.settings.LanguageCode, StringComparison.OrdinalIgnoreCase);
            }

            foreach (KeyValuePair<int, ToolStripMenuItem> pair in this.popupScaleMenuItems)
            {
                pair.Value.Text = string.Format("{0} %", pair.Key);
                pair.Value.Checked = pair.Key == this.settings.PopupScalePercent;
            }

            foreach (KeyValuePair<PopupDisplayMode, ToolStripMenuItem> pair in this.displayModeMenuItems)
            {
                pair.Value.Text = this.GetPopupDisplayModeDisplayName(pair.Key);
                pair.Value.Checked = pair.Key == this.settings.PopupDisplayMode;
            }

            foreach (KeyValuePair<PopupSectionMode, ToolStripMenuItem> pair in this.sectionModeMenuItems)
            {
                pair.Value.Text = this.GetPopupSectionModeDisplayName(pair.Key);
                pair.Value.Checked = pair.Key == this.settings.PopupSectionMode;
            }

            this.rotatingGlossItem.Text = UiLanguage.Get(
                "Menu.RotatingGloss",
                "Rotierender Kernschimmer");
            this.rotatingGlossItem.Checked = this.settings.RotatingMeterGlossEnabled;
            this.activityBorderGlowItem.Text = UiLanguage.Get(
                "Menu.ActivityBorderGlow",
                "Leuchtrand");
            this.activityBorderGlowItem.Checked = this.settings.ActivityBorderGlowEnabled;
            this.activityBorderGlowItem.Enabled = !this.settings.TaskbarIntegrationEnabled;

            AdapterAvailabilityState adapterAvailabilityState = GetAdapterAvailabilityState(this.settings);

            if (this.settings.HasCalibrationData() &&
                adapterAvailabilityState != AdapterAvailabilityState.Missing &&
                adapterAvailabilityState != AdapterAvailabilityState.Inactive)
            {
                this.calibrationStatusItem.Text = UiLanguage.Get(
                    "Menu.CalibrationStatusSaved",
                    "Kalibrationsstatus: gespeichert");
                return;
            }

            if (this.settings.HasAdapterSelection())
            {
                if (adapterAvailabilityState == AdapterAvailabilityState.Missing)
                {
                    this.calibrationStatusItem.Text = UiLanguage.Get(
                        "Menu.CalibrationStatusAdapterMissing",
                        "Kalibrationsstatus: Internetverbindung nicht gefunden");
                    return;
                }

                if (adapterAvailabilityState == AdapterAvailabilityState.Inactive)
                {
                    this.calibrationStatusItem.Text = UiLanguage.Get(
                        "Menu.CalibrationStatusAdapterInactive",
                        "Kalibrationsstatus: Internetverbindung inaktiv");
                    return;
                }

                this.calibrationStatusItem.Text = UiLanguage.Get(
                    this.settings.HasCalibrationData()
                        ? "Menu.CalibrationStatusSaved"
                        : "Menu.CalibrationStatusAdapterSelected",
                    this.settings.HasCalibrationData()
                        ? "Kalibrationsstatus: abgeschlossen"
                        : "Kalibrationsstatus: Internetverbindung gewählt");
                return;
            }

            this.calibrationStatusItem.Text = UiLanguage.Get(
                "Menu.CalibrationStatusOpen",
                "Kalibrationsstatus: nicht abgeschlossen");
        }

        private string GetPanelSkinDisplayName(string panelSkinId)
        {
            PanelSkinDefinition definition = PanelSkinCatalog.GetSkinById(panelSkinId);
            if (definition == null)
            {
                return UiLanguage.Get("Menu.Skins", "Skins");
            }

            if (string.IsNullOrWhiteSpace(definition.DisplayNameKey))
            {
                return string.IsNullOrWhiteSpace(definition.DisplayNameFallback)
                    ? definition.Id
                    : definition.DisplayNameFallback;
            }

            string displayName = UiLanguage.Get(
                definition.DisplayNameKey,
                string.IsNullOrWhiteSpace(definition.DisplayNameFallback) ? definition.Id : definition.DisplayNameFallback);

            if (PanelSkinCatalog.IsProtectedSkinId(definition.Id))
            {
                displayName += UiLanguage.Get(
                    "Menu.SkinProtectedSuffix",
                    " (nicht löschbar)");
            }

            return displayName;
        }

        private string GetPopupDisplayModeDisplayName(PopupDisplayMode popupDisplayMode)
        {
            switch (popupDisplayMode)
            {
                case PopupDisplayMode.MiniGraph:
                    return UiLanguage.Get("Menu.DisplayModeMiniGraph", "MiniGraph");
                case PopupDisplayMode.MiniSoft:
                    return UiLanguage.Get("Menu.DisplayModeMiniSoft", "MiniSoft");
                default:
                    return UiLanguage.Get("Menu.DisplayModeStandard", "Standard");
            }
        }

        private string GetPopupSectionModeDisplayName(PopupSectionMode popupSectionMode)
        {
            switch (popupSectionMode)
            {
                case PopupSectionMode.LeftOnly:
                    return UiLanguage.Get("Menu.SectionModeLeftOnly", "Nur links");
                case PopupSectionMode.RightOnly:
                    return UiLanguage.Get("Menu.SectionModeRightOnly", "Nur rechts");
                default:
                    return UiLanguage.Get("Menu.SectionModeBoth", "Beide Teile");
            }
        }

        private void RebuildPanelSkinMenuItems()
        {
            this.skinItem.DropDownItems.Clear();
            this.panelSkinMenuItems.Clear();

            string[] panelSkinIds = MonitorSettings.GetSupportedPanelSkinIds();
            for (int i = 0; i < panelSkinIds.Length; i++)
            {
                ToolStripMenuItem item = new ToolStripMenuItem(string.Empty, null, this.PanelSkinMenuItem_Click);
                item.Tag = panelSkinIds[i];
                this.panelSkinMenuItems[panelSkinIds[i]] = item;
                this.skinItem.DropDownItems.Add(item);
            }

            if (this.panelSkinMenuItems.Count > 0)
            {
                this.skinItem.DropDownItems.Add(new ToolStripSeparator());
            }

            this.skinItem.DropDownItems.Add(this.deleteSkinItem);
        }

        private string GetFallbackSkinId(string deletedSkinId)
        {
            PanelSkinDefinition[] definitions = PanelSkinCatalog.GetAvailableSkins();

            for (int i = 0; i < definitions.Length; i++)
            {
                if (!string.Equals(definitions[i].Id, deletedSkinId, StringComparison.OrdinalIgnoreCase))
                {
                    return definitions[i].Id;
                }
            }

            return "08";
        }

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
                    EscapeCsvValue(UiLanguage.Get("UsageWindow.AdapterCaption", "Internetverbindung:").TrimEnd(':')),
                    EscapeCsvValue(adapterDisplayName)));
                csvLines.Add(string.Empty);
                csvLines.Add(string.Join(";",
                    string.Empty,
                    EscapeCsvValue(UiLanguage.Get("UsageWindow.ColumnDaily", "Täglich")),
                    EscapeCsvValue(UiLanguage.Get("UsageWindow.ColumnWeekly", "Wöchentlich")),
                    EscapeCsvValue(UiLanguage.Get("UsageWindow.ColumnMonthly", "Monatlich"))));
                csvLines.Add(string.Join(";",
                    EscapeCsvValue(UiLanguage.Get("UsageWindow.RowPeriodStart", "Beginn")),
                    EscapeCsvValue(this.FormatUsagePeriodStart(exportTimestampLocal, TrafficUsagePeriod.Daily)),
                    EscapeCsvValue(this.FormatUsagePeriodStart(exportTimestampLocal, TrafficUsagePeriod.Weekly)),
                    EscapeCsvValue(this.FormatUsagePeriodStart(exportTimestampLocal, TrafficUsagePeriod.Monthly))));
                csvLines.Add(string.Join(";",
                    EscapeCsvValue(UiLanguage.Get("UsageWindow.RowPeriodEnd", "Ende")),
                    EscapeCsvValue(this.FormatUsagePeriodEnd(exportTimestampLocal)),
                    EscapeCsvValue(this.FormatUsagePeriodEnd(exportTimestampLocal)),
                    EscapeCsvValue(this.FormatUsagePeriodEnd(exportTimestampLocal))));
                csvLines.Add(string.Join(";",
                    EscapeCsvValue(UiLanguage.Get("UsageWindow.RowUpload", "Upload")),
                    EscapeCsvValue(FormatUsageAmount(usageWindowData.DailySummary.UploadBytes)),
                    EscapeCsvValue(FormatUsageAmount(usageWindowData.WeeklySummary.UploadBytes)),
                    EscapeCsvValue(FormatUsageAmount(usageWindowData.MonthlySummary.UploadBytes))));
                csvLines.Add(string.Join(";",
                    EscapeCsvValue(UiLanguage.Get("UsageWindow.RowDownload", "Download")),
                    EscapeCsvValue(FormatUsageAmount(usageWindowData.DailySummary.DownloadBytes)),
                    EscapeCsvValue(FormatUsageAmount(usageWindowData.WeeklySummary.DownloadBytes)),
                    EscapeCsvValue(FormatUsageAmount(usageWindowData.MonthlySummary.DownloadBytes))));
                csvLines.Add(string.Join(";",
                    EscapeCsvValue(UiLanguage.Get("UsageWindow.RowTotal", "Gesamt")),
                    EscapeCsvValue(FormatUsageAmount(usageWindowData.DailySummary.TotalBytes)),
                    EscapeCsvValue(FormatUsageAmount(usageWindowData.WeeklySummary.TotalBytes)),
                    EscapeCsvValue(FormatUsageAmount(usageWindowData.MonthlySummary.TotalBytes))));

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
                    new Func<long, string>(FormatUsageAmount),
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

        private static string FormatUsageAmount(long bytes)
        {
            string[] units = new string[] { "B", "KB", "MB", "GB", "TB" };
            double value = Math.Max(0L, bytes);
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

        private string FormatUsagePeriodStart(DateTime exportTimestampLocal, TrafficUsagePeriod period)
        {
            DateTime periodStartLocal;
            switch (period)
            {
                case TrafficUsagePeriod.Daily:
                    periodStartLocal = exportTimestampLocal.Date;
                    break;
                case TrafficUsagePeriod.Weekly:
                    periodStartLocal = GetStartOfWeek(exportTimestampLocal);
                    break;
                case TrafficUsagePeriod.Monthly:
                    periodStartLocal = new DateTime(exportTimestampLocal.Year, exportTimestampLocal.Month, 1);
                    break;
                default:
                    periodStartLocal = exportTimestampLocal;
                    break;
            }

            return periodStartLocal.ToString("dd.MM.yyyy HH:mm:ss");
        }

        private string FormatUsagePeriodEnd(DateTime exportTimestampLocal)
        {
            return exportTimestampLocal.ToString("dd.MM.yyyy HH:mm:ss");
        }

        private static DateTime GetStartOfWeek(DateTime dateTime)
        {
            DayOfWeek firstDayOfWeek = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
            int offset = (7 + (dateTime.DayOfWeek - firstDayOfWeek)) % 7;
            return dateTime.Date.AddDays(-offset);
        }

        private static string EscapeCsvValue(string value)
        {
            string safeValue = value ?? string.Empty;
            if (safeValue.IndexOfAny(new char[] { ';', '"', '\r', '\n' }) < 0)
            {
                return safeValue;
            }

            return "\"" + safeValue.Replace("\"", "\"\"") + "\"";
        }

        private static AdapterAvailabilityState GetAdapterAvailabilityState(MonitorSettings settings)
        {
            if (settings == null || !settings.HasAdapterSelection())
            {
                return AdapterAvailabilityState.Missing;
            }

            return NetworkSnapshot.GetAdapterAvailabilityState(settings);
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
        private const int BaseLeftOnlyClientWidth = 62;
        private const int BaseRightOnlyClientWidth = 58;
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
        private const int MiniGraphMeterDiameter = 46;
        private const int MiniGraphMeterRightInset = 3;
        private const int BaseDragThreshold = 4;
        private const int BasePopupVisibleMargin = 8;
        private const int TaskbarMonitorIntervalMs = 350;
        private const int TaskbarRefreshDebounceMs = 220;
        private const int TaskbarInsetThickness = 2;
        private const int MinimumVisibleTaskbarThickness = 8;
        private const int TaskbarPlacementMargin = 2;
        private const int TaskbarOccupiedSafetyPadding = 4;
        private const int TaskbarProtectedEdgeProbe = 96;
        private const int TaskbarCompactRestoreHysteresis = 12;
        private const int TaskbarSectionToggleDragThreshold = 10;
        private const int TaskbarDesktopSnapHoldDistance = 18;
        private const int TaskbarDragSnapDistance = 36;
        private const int TaskbarDragBreakThroughDepth = 60;
        private const int NoSpaceMessageCooldownMs = 1800;
        private const int TaskbarTransientFailureGraceMs = 900;
        private const int DesktopToTaskbarBlinkSuppressionMs = 1200;
        private const byte TaskbarIntegratedPanelMaxBackgroundAlpha = 122;
        private const byte TaskbarIntegratedPanelTintAlpha = 34;
        private const byte TaskbarIntegratedPanelInnerOpacityBoostAlpha = 108;
        private const byte TaskbarIntegratedInfoPlateFillAlpha = 112;
        private const byte TaskbarIntegratedInfoPlateHighlightAlpha = 44;
        private const byte TaskbarIntegratedEdgeGradientOuterAlpha = 255;
        private const byte TaskbarIntegratedEdgeGradientLipAlpha = 255;
        private const float TaskbarIntegratedEdgeGradientWidth = 10.0F;
        private const float TaskbarIntegratedEdgeGradientLipWidth = 4.0F;
        private const float BaseFormFontSize = 7.0F;
        private const float BaseCaptionFontSize = 6.0F;
        private const float BaseValueFontSize = 10.4F;
        private const int WmNclButtonDown = 0xA1;
        private const int HtCaption = 0x2;
        private const int WmContextMenu = 0x007B;
        private const int WmNcHitTest = 0x0084;
        private const int WmDpiChanged = 0x02E0;
        private const int WmDisplayChange = 0x007E;
        private const int WmSettingChange = 0x001A;
        private const int HtTransparent = -1;
        private const int WsExLayered = 0x00080000;
        private const uint AbmGetTaskbarPos = 0x00000005;
        private const uint AbmGetState = 0x00000004;
        private const int AbsAutoHide = 0x1;
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpShowWindow = 0x0040;
        private const uint SwpNoActivate = 0x0010;
        private const uint SwpNoRedraw = 0x0008;
        private const uint SwpNoOwnerZOrder = 0x0200;
        private const uint SwpNoSendChanging = 0x0400;
        private const uint GwHwndNext = 2;
        private const uint GwHwndPrev = 3;
        private const int GwlStyle = -16;
        private const uint LwaAlpha = 0x2;
        private const int UlwAlpha = 0x2;
        private const byte AcSrcOver = 0x00;
        private const byte AcSrcAlpha = 0x01;
        private const int KeepTopMostRefreshIntervalMs = 1600;
        private static readonly IntPtr HwndTop = IntPtr.Zero;
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
        private const double RingDisplayRiseSmoothingFactor = 0.28D;
        private const double RingDisplayFallSmoothingFactor = 0.14D;
        private const double RingDisplayNoiseFloorBytesPerSecond = 8D * 1024D;
        private const int DisplaySmoothingSampleCount = 3;
        private const int TrafficHistorySampleCount = 60;
        private const int OverlaySparklinePointCount = 24;
        private const int MiniGraphOverlaySparklinePointCount = 40;
        private const int MiniGraphRingSegmentCount = 16;
        private const float MiniGraphRingSegmentGapDegrees = 3.2F;
        private const float MiniGraphDownloadRingSegmentGapDegrees = 0.7F;
        private const float MiniGraphUploadRingSegmentGapDegrees = 1.2F;
        private const float MiniGraphDualRingInnerGapFactor = 0.36F;
        private const float MiniSoftDualRingInnerGapFactor = 0.46F;
        private const float TaskbarIntegratedDownloadRingOutwardExpansion = 1.5F;
        private const double MiniGraphRingDisplayNoiseFloorBytesPerSecond = 2D * 1024D;
        private const double MiniGraphDownloadLowTrafficVisualizationExponent = 0.50D;
        private const double MiniGraphUploadLowTrafficVisualizationExponent = 0.58D;
        private const double MiniGraphDownloadMinimumVisualizationPeakBytesPerSecond = 96D * 1024D;
        private const double MiniGraphUploadMinimumVisualizationPeakBytesPerSecond = 128D * 1024D;
        private const float MiniGraphSparklineLeft = 8F;
        private const float MiniGraphSparklineTop = 46F;
        private const float MiniGraphSparklineHeight = 8F;
        private const float MiniGraphSparklineContentLeftInset = 0F;
        private const float PeakHoldMarkerSweepDegrees = 8.5F;
        private const double PeakHoldReleaseDelaySeconds = 0.70D;
        private const double PeakHoldDecayPerSecond = 0.42D;
        private const double MeterGlossAnimationThresholdRatio = 0.02D;
        private const double MeterGlossClockwiseMaxRotationDegreesPerSecond = 138D;
        private const double MeterGlossCounterClockwiseMaxRotationDegreesPerSecond = 110D;
        private const double ActivityBorderFadeInStartRatio = 0.008D;
        private const double ActivityBorderFadeInFullRatio = 0.165D;
        private const double ActivityBorderAnimationThresholdRatio = 0.006D;
        private const double ActivityBorderBaseRotationDegreesPerSecond = 56D;
        private const double ActivityBorderMaxRotationDegreesPerSecond = 328D;
        private const double ActivityBorderTravelPulseWidth = 0.105D;
        private const double ActivityBorderTravelTailWidth = 0.245D;
        private const float StandardMeterGlossExtraInset = 2.3F;
        private const float MiniGraphDownloadRingWeight = 1.12F;
        private const float MiniGraphUploadRingWeight = 1.00F;
        private const float MiniGraphDownloadSparklineWidthScale = 1.08F;
        private const float MiniGraphUploadSparklineWidthScale = 1.00F;
        private const int MiniGraphDownloadSparklineAreaAlpha = 38;
        private const int MiniGraphUploadSparklineAreaAlpha = 30;
        private const int MiniGraphDownloadPeakMarkerAlpha = 188;
        private const int MiniGraphUploadPeakMarkerAlpha = 176;
        private const string PanelBackgroundAssetFileName = "TrafficView.panel.png";
        private const string PanelBackgroundScaledAssetFileNameFormat = "TrafficView.panel.{0}.png";
        private static readonly double[] DisplaySmoothingWeights = new double[] { 0.15D, 0.30D, 0.55D };
        private static readonly object PanelBackgroundAssetSync = new object();
        private static readonly int[] PanelBackgroundPreparedScalePercents = new int[] { 90, 100, 110, 125, 150 };
        private static readonly Dictionary<string, Dictionary<string, Bitmap>> CachedPanelBackgroundAssetsByDirectory =
            new Dictionary<string, Dictionary<string, Bitmap>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, bool> PanelBackgroundAssetLoadAttemptedByDirectory =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private static readonly object MeterCenterAssetSync = new object();
        private static string cachedMeterCenterAssetPath = string.Empty;
        private static Bitmap cachedMeterCenterAsset;

        private readonly Timer refreshTimer;
        private readonly Timer animationTimer;
        private readonly Timer topMostGuardTimer;
        private readonly Timer taskbarMonitorTimer;
        private readonly Timer taskbarRefreshDebounceTimer;
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
        private double displayedDownloadBytesPerSecond;
        private double displayedUploadBytesPerSecond;
        private double ringDisplayDownloadBytesPerSecond;
        private double ringDisplayUploadBytesPerSecond;
        private double peakHoldDownloadBytesPerSecond;
        private double peakHoldUploadBytesPerSecond;
        private double visualDownloadPeakBytesPerSecond;
        private double visualUploadPeakBytesPerSecond;
        private DateTime peakHoldDownloadCapturedUtc = DateTime.MinValue;
        private DateTime peakHoldUploadCapturedUtc = DateTime.MinValue;
        private DateTime lastAnimationAdvanceUtc = DateTime.MinValue;
        private double meterGlossRotationDegrees;
        private double activityBorderRotationDegrees;
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
        private bool taskbarIntegrationDisplayRequested;
        private bool taskbarIntegrationVisibilityChange;
        private bool taskbarNoSpaceMessageShown;
        private bool taskbarNoSpaceMessageVisible;
        private bool taskbarIntegrationRefreshInProgress;
        private bool taskbarIntegrationRefreshPending;
        private bool taskbarIntegrationDebouncedRefreshPending;
        private bool taskbarIntegrationPendingActivateWindow;
        private bool taskbarIntegrationPendingShowNoSpaceMessage;
        private bool taskbarLocalZOrderRepairPending = true;
        private bool taskbarIntegrationForceRightOnlySection;
        private bool taskbarIntegrationStickyRightOnlySection;
        private Point? taskbarIntegrationPreferredLocation;
        private DateTime lastNoSpaceMessageUtc = DateTime.MinValue;
        private DateTime lastDesktopShellForegroundUtc = DateTime.MinValue;
        private DateTime lastTaskbarIntegrationRefreshUtc = DateTime.MinValue;
        private DateTime lastSuccessfulTaskbarPlacementUtc = DateTime.MinValue;
        private Rectangle lastSuccessfulTaskbarPlacementBounds = Rectangle.Empty;
        private int lastAppliedTaskbarThickness = -1;
        private TaskbarIntegrationSnapshot activeTaskbarSnapshot;
        private IntPtr lastTaskbarLocalZOrderAnchorHandle = IntPtr.Zero;
        private IntPtr taskbarIntegrationHostHandle = IntPtr.Zero;
        
        public event EventHandler OverlayMenuRequested;
        public event EventHandler OverlayLocationCommitted;
        public event EventHandler TaskbarIntegrationNoSpaceAcknowledged;
        public event EventHandler<TrafficUsageMeasuredEventArgs> TrafficUsageMeasured;

        public bool HasDeferredVisibilityRequest
        {
            get { return this.taskbarIntegrationDisplayRequested; }
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint uFlags);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);


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

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
            {
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            }

            return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 8)
            {
                return GetWindowLongPtr64(hWnd, nIndex);
            }

            return new IntPtr(GetWindowLong32(hWnd, nIndex));
        }

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

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern uint SHAppBarMessage(uint dwMessage, ref AppBarData pData);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmGetColorizationColor(out uint pcrColorization, out bool pfOpaqueBlend);

        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

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

        protected override bool ShowWithoutActivation
        {
            get { return this.ShouldUsePassiveDesktopOverlayBehavior() || this.IsTaskbarIntegratedMode(); }
        }

        private bool ShouldUsePassiveDesktopOverlayBehavior()
        {
            return false;
        }

        private bool ShouldPassMouseToUnderlyingDesktop()
        {
            return this.ShouldUsePassiveDesktopOverlayBehavior() &&
                (Control.ModifierKeys & Keys.Shift) != Keys.Shift;
        }

        private bool IsTaskbarIntegratedMode()
        {
            return this.settings != null && this.settings.TaskbarIntegrationEnabled;
        }

        private bool ShouldUseGlobalTopMost()
        {
            return this.ShouldUseDesktopGlobalTopMost();
        }

        private bool ShouldUseDesktopGlobalTopMost()
        {
            return !this.IsTaskbarIntegratedMode();
        }

        private bool ShouldUseTaskbarLocalZOrder()
        {
            return this.IsTaskbarIntegratedMode();
        }

        private void ApplyWindowZOrderMode()
        {
            if (this.ShouldUseDesktopGlobalTopMost() && !this.TopMost)
            {
                this.TopMost = true;
            }
            else if (this.ShouldUseTaskbarLocalZOrder() && this.TopMost)
            {
                this.TopMost = false;
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
            this.TopMost = this.ShouldUseGlobalTopMost();
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
            this.animationTimer.Interval = 140;
            this.animationTimer.Tick += this.AnimationTimer_Tick;
            this.animationTimer.Enabled = false;

            this.topMostGuardTimer = new Timer();
            this.topMostGuardTimer.Interval = KeepTopMostRefreshIntervalMs;
            this.topMostGuardTimer.Tick += this.TopMostGuardTimer_Tick;
            this.topMostGuardTimer.Enabled = this.Visible &&
                !this.IsTopMostEnforcementPaused &&
                this.ShouldUseGlobalTopMost();

            this.taskbarMonitorTimer = new Timer();
            this.taskbarMonitorTimer.Interval = TaskbarMonitorIntervalMs;
            this.taskbarMonitorTimer.Tick += this.TaskbarMonitorTimer_Tick;
            this.taskbarMonitorTimer.Enabled = false;

            this.taskbarRefreshDebounceTimer = new Timer();
            this.taskbarRefreshDebounceTimer.Interval = TaskbarRefreshDebounceMs;
            this.taskbarRefreshDebounceTimer.Tick += this.TaskbarRefreshDebounceTimer_Tick;
            this.taskbarRefreshDebounceTimer.Enabled = false;

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
            this.UpdateTaskbarMonitorState();

            if (!this.taskbarIntegrationVisibilityChange)
            {
                this.taskbarIntegrationDisplayRequested = this.Visible;
            }

            if (this.Visible)
            {
                this.lastRenderedAnimationFrame = -1;
                this.lastPresentedLocation = new Point(int.MinValue, int.MinValue);

                if (this.settings.TaskbarIntegrationEnabled)
                {
                    this.RefreshTaskbarIntegration(false, false);
                    return;
                }

                this.EnsureVisiblePopupLocation(null, null);
                this.EnsureTopMostPlacement(false);
                this.RefreshVisualSurface();
            }
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);

            if (!this.Visible)
            {
                return;
            }

            if (this.settings.TaskbarIntegrationEnabled)
            {
                this.TryBeginInvokeSafely(new Action(delegate
                {
                    if (!this.IsDisposed && this.IsDesktopShellForegroundWindow())
                    {
                        this.lastDesktopShellForegroundUtc = DateTime.UtcNow;
                    }
                }));
                return;
            }

            this.TryBeginInvokeSafely(new Action(delegate
            {
                this.EnsureTopMostPlacement(false);
            }));
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
            if (m.Msg == WmNcHitTest && this.ShouldPassMouseToUnderlyingDesktop())
            {
                m.Result = new IntPtr(HtTransparent);
                return;
            }

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
                this.OnOverlayLocationCommitted();
                return;
            }

            if (m.Msg == WmDisplayChange || m.Msg == WmSettingChange)
            {
                base.WndProc(ref m);

                if (this.IsHandleCreated &&
                    (this.Visible || this.taskbarIntegrationDisplayRequested))
                {
                    this.TryBeginInvokeSafely(new Action(delegate
                    {
                        if (this.IsDisposed)
                        {
                            return;
                        }

                        if (this.settings.TaskbarIntegrationEnabled)
                        {
                            this.RefreshTaskbarIntegration(false, false);
                        }
                        else if (this.Visible)
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

        private bool TryBeginInvokeSafely(Action action)
        {
            if (action == null || this.IsDisposed || !this.IsHandleCreated)
            {
                return false;
            }

            try
            {
                this.BeginInvoke(action);
                return true;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
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
                this.GetVisualizedFillRatio(downloadFillRatio, true),
                this.GetVisualizedFillRatio(uploadFillRatio, false));
        }

        private void RenderStaticPopupSurface(Graphics graphics)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.CompositingQuality = CompositingQuality.HighQuality;

            int outerInset = this.ScaleValue(this.GetPanelOuterInset());
            int separatorY = this.ScaleValue(BaseSeparatorY);
            int separatorInset = this.ScaleValue(BaseSeparatorInset);
            int cornerRadius = this.ScaleValue(BaseWindowCornerRadius);
            float strokeWidth = Math.Max(1F, this.ScaleFloat(1F));
            float sharedRingWidth = Math.Max(2F, this.ScaleFloat(6.2F));
            float centerInset = Math.Max(1F, this.ScaleFloat(this.IsMiniSoftDisplayMode() ? 4.6F : 2.3F));
            byte backgroundAlpha = this.GetStaticPanelBackgroundAlpha();
            Color panelBackgroundColor = this.GetPanelBackgroundBaseColor();
            Color panelBorderColor = this.GetPanelBorderBaseColor();

            if (!this.ShouldDrawStaticBackgroundLayer())
            {
                return;
            }

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
            bool drawRightSection = this.IsRightSectionVisible();
            Rectangle meterBounds = drawRightSection
                ? this.GetDownloadMeterBounds()
                : Rectangle.Empty;
            RectangleF sharedRingBounds = drawRightSection
                ? this.CreateInsetBounds(meterBounds, sharedRingWidth / 2F)
                : RectangleF.Empty;
            RectangleF centerBounds = drawRightSection
                ? this.CreateInsetBounds(meterBounds, sharedRingWidth + centerInset)
                : RectangleF.Empty;

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
                    using (SolidBrush borderBrush = new SolidBrush(ApplyAlpha(panelBorderColor, backgroundAlpha)))
                    using (SolidBrush fillBrush = new SolidBrush(ApplyAlpha(panelBackgroundColor, backgroundAlpha)))
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

                byte overlayAlpha = this.GetTaskbarBackgroundOverlayAlpha();
                if (overlayAlpha > 0)
                {
                    using (GraphicsPath tintPath = CreateRoundedPath(innerBounds, Math.Max(2F, cornerRadius - outerInset)))
                    using (SolidBrush tintBrush = new SolidBrush(ApplyAlpha(panelBackgroundColor, overlayAlpha)))
                    {
                        graphics.FillPath(tintBrush, tintPath);
                    }
                }

                if (this.IsGlassPanelSkinEnabled())
                {
                    this.DrawPanelGlassSurface(
                        graphics,
                        innerBounds,
                        Math.Max(2F, cornerRadius - outerInset),
                        backgroundAlpha);
                }

                if (this.IsTaskbarIntegrationActive())
                {
                    this.DrawTaskbarIntegratedInnerOpacityBoost(
                        graphics,
                        innerBounds,
                        Math.Max(2F, cornerRadius - outerInset),
                        drawRightSection);
                    this.DrawTaskbarIntegratedInfoOpacityPlate(graphics, meterBounds);
                }

                if (this.IsTaskbarIntegrationActive())
                {
                    RectangleF fullPanelBounds = new RectangleF(0F, 0F, this.Width, this.Height);
                    this.DrawTaskbarIntegratedPanelEdgeGradient(
                        graphics,
                        fullPanelBounds,
                        cornerRadius,
                        backgroundAlpha);
                }

                if (this.IsBothSectionsVisible())
                {
                    this.DrawPanelSeparator(
                        graphics,
                        separatorInset,
                        separatorY,
                        Math.Max(separatorInset, meterBounds.Left - this.ScaleValue(5)),
                        backgroundAlpha);
                }

                if (drawRightSection)
                {
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
        }

        private bool IsGlassPanelSkinEnabled()
        {
            PanelSkinDefinition definition = PanelSkinCatalog.GetSkinById(this.settings.PanelSkinId);
            return definition != null && definition.HasGlassSurfaceEffect;
        }

        private bool IsReadableInfoPanelSkinEnabled()
        {
            PanelSkinDefinition definition = PanelSkinCatalog.GetSkinById(this.settings.PanelSkinId);
            return definition != null && definition.HasReadableInfoPlateEffect;
        }

        private PanelSkinDefinition GetCurrentPanelSkinDefinition()
        {
            return PanelSkinCatalog.GetSkinById(this.settings.PanelSkinId);
        }

        private string GetCurrentMeterCenterAssetPath()
        {
            PanelSkinDefinition definition = this.GetCurrentPanelSkinDefinition();
            if (definition == null || string.IsNullOrWhiteSpace(definition.DirectoryPath))
            {
                return null;
            }

            string assetPath = Path.Combine(definition.DirectoryPath, "TrafficView.center_core.png");
            return File.Exists(assetPath) ? assetPath : null;
        }

        private bool IsHudOnlyTransparencyMode()
        {
            return this.settings != null && this.settings.TransparencyPercent >= 100;
        }

        private byte GetStaticPanelBackgroundAlpha()
        {
            byte configuredAlpha = MonitorSettings.ToOpacityByte(this.settings.TransparencyPercent);
            return this.IsTaskbarIntegrationActive()
                ? Math.Min(configuredAlpha, TaskbarIntegratedPanelMaxBackgroundAlpha)
                : configuredAlpha;
        }

        private bool ShouldDrawStaticBackgroundLayer()
        {
            return !this.IsHudOnlyTransparencyMode()
                && this.GetStaticPanelBackgroundAlpha() > 0;
        }

        private bool IsLeftSectionVisible()
        {
            PopupSectionMode popupSectionMode = this.GetEffectivePopupSectionMode();
            return popupSectionMode == PopupSectionMode.Both ||
                popupSectionMode == PopupSectionMode.LeftOnly;
        }

        private bool IsRightSectionVisible()
        {
            PopupSectionMode popupSectionMode = this.GetEffectivePopupSectionMode();
            return popupSectionMode == PopupSectionMode.Both ||
                popupSectionMode == PopupSectionMode.RightOnly;
        }

        private bool IsBothSectionsVisible()
        {
            return this.GetEffectivePopupSectionMode() == PopupSectionMode.Both;
        }

        private PopupSectionMode GetConfiguredPopupSectionMode()
        {
            return this.settings != null
                ? this.settings.PopupSectionMode
                : PopupSectionMode.Both;
        }

        private PopupSectionMode GetEffectivePopupSectionMode()
        {
            if ((this.taskbarIntegrationForceRightOnlySection ||
                this.taskbarIntegrationStickyRightOnlySection) &&
                this.settings != null &&
                this.settings.TaskbarIntegrationEnabled)
            {
                return PopupSectionMode.RightOnly;
            }

            return this.GetConfiguredPopupSectionMode();
        }

        private bool ShouldDrawDynamicRing()
        {
            PanelSkinDefinition definition = this.GetCurrentPanelSkinDefinition();
            return this.IsRightSectionVisible() &&
                (definition == null || definition.DrawDynamicRing);
        }

        private bool ShouldDrawCenterTrafficArrows()
        {
            PanelSkinDefinition definition = this.GetCurrentPanelSkinDefinition();
            return this.IsRightSectionVisible() &&
                (definition == null || definition.DrawCenterArrows);
        }

        private bool ShouldDrawSparkline()
        {
            PanelSkinDefinition definition = this.GetCurrentPanelSkinDefinition();
            return this.IsLeftSectionVisible() &&
                (definition == null || definition.DrawSparkline);
        }

        private bool ShouldDrawMeterValueSupport()
        {
            PanelSkinDefinition definition = this.GetCurrentPanelSkinDefinition();
            return this.IsLeftSectionVisible() &&
                (definition == null || definition.DrawMeterValueSupport);
        }

        private Rectangle ScaleSkinRectangle(Rectangle bounds)
        {
            return new Rectangle(
                this.ScaleValue(bounds.X),
                this.ScaleValue(bounds.Y),
                this.ScaleValue(bounds.Width),
                this.ScaleValue(bounds.Height));
        }

        private Size ScaleSkinSize(Size size)
        {
            return new Size(
                this.ScaleValue(size.Width),
                this.ScaleValue(size.Height));
        }

        private Rectangle GetScaledSkinBounds(Rectangle defaultBounds, Rectangle? overrideBounds)
        {
            return this.ScaleSkinRectangle(overrideBounds ?? defaultBounds);
        }

        private bool TryDrawPanelBackgroundAsset(Graphics graphics, byte backgroundAlpha)
        {
            Bitmap panelBackgroundAsset = GetPanelBackgroundAsset(this.settings.PanelSkinId, this.ClientSize);
            if (panelBackgroundAsset == null)
            {
                return false;
            }

            GraphicsState state = graphics.Save();

            try
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;
                using (Bitmap adjustedAsset = CreateSelectiveTransparencyBitmap(panelBackgroundAsset, backgroundAlpha))
                {
                    if (adjustedAsset.Width == this.ClientSize.Width &&
                        adjustedAsset.Height == this.ClientSize.Height)
                    {
                        graphics.DrawImage(
                            adjustedAsset,
                            new Rectangle(0, 0, adjustedAsset.Width, adjustedAsset.Height),
                            0,
                            0,
                            adjustedAsset.Width,
                            adjustedAsset.Height,
                            GraphicsUnit.Pixel);
                    }
                    else
                    {
                        graphics.DrawImage(
                            adjustedAsset,
                            new Rectangle(0, 0, this.ClientSize.Width, this.ClientSize.Height),
                            0,
                            0,
                            adjustedAsset.Width,
                            adjustedAsset.Height,
                            GraphicsUnit.Pixel);
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
            float centerInset = Math.Max(1F, this.ScaleFloat(this.IsMiniSoftDisplayMode() ? 4.6F : 2.3F));
            float iconInset = Math.Max(1F, this.ScaleFloat(this.IsMiniSoftDisplayMode() ? 4.6F : 2.3F));

            bool drawLeftSection = this.IsLeftSectionVisible();
            bool drawRightSection = this.IsRightSectionVisible();
            Rectangle meterBounds = drawRightSection
                ? this.GetDownloadMeterBounds()
                : Rectangle.Empty;
            RectangleF sharedRingBounds = drawRightSection
                ? this.CreateInsetBounds(meterBounds, sharedRingWidth / 2F)
                : RectangleF.Empty;
            RectangleF centerBounds = drawRightSection
                ? this.CreateInsetBounds(meterBounds, sharedRingWidth + centerInset)
                : RectangleF.Empty;
            RectangleF iconBounds = drawRightSection
                ? this.CreateInsetBounds(meterBounds, sharedRingWidth + iconInset)
                : RectangleF.Empty;

            Color downloadRingEndColor = GetInterpolatedColor(
                DownloadRingLowColor,
                DownloadRingHighColor,
                SmoothStep(visualDownloadFillRatio));
            Color uploadRingEndColor = GetInterpolatedColor(
                UploadRingLowColor,
                UploadRingHighColor,
                SmoothStep(visualUploadFillRatio));

            if (drawLeftSection &&
                this.IsReadableInfoPanelSkinEnabled() &&
                !this.IsHudOnlyTransparencyMode())
            {
                this.DrawReadableTrafficInfoPanel(graphics, meterBounds);
            }
            else if (drawLeftSection && !this.IsHudOnlyTransparencyMode())
            {
                this.DrawTransparencyAwareInfoPanel(graphics, meterBounds);
            }

            if (drawLeftSection && !this.IsHudOnlyTransparencyMode() && this.ShouldDrawMeterValueSupport())
            {
                this.DrawMeterValueBalanceSupport(graphics, meterBounds);
            }

            if (drawLeftSection)
            {
                this.DrawTrafficTexts(graphics);
            }

            if (this.ShouldDrawSparkline())
            {
                this.DrawMiniTrafficSparkline(graphics, meterBounds);
            }

            if (drawRightSection && this.IsHudOnlyTransparencyMode())
            {
                this.DrawHudOnlyMeterGuideCircles(graphics, sharedRingBounds, sharedRingWidth);
            }

            if (this.ShouldDrawDynamicRing())
            {
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
            }

            if (drawRightSection)
            {
                RectangleF glossBounds = centerBounds;
                if (!this.IsMiniSoftDisplayMode())
                {
                    float glossInset = this.ScaleFloat(StandardMeterGlossExtraInset);
                    glossBounds = new RectangleF(
                        centerBounds.Left + glossInset,
                        centerBounds.Top + glossInset,
                        Math.Max(2F, centerBounds.Width - (glossInset * 2F)),
                        Math.Max(2F, centerBounds.Height - (glossInset * 2F)));
                }

                this.DrawRotatingMeterGloss(
                    graphics,
                    glossBounds,
                    visualDownloadFillRatio,
                    visualUploadFillRatio);
            }

            if (this.ShouldDrawCenterTrafficArrows())
            {
                this.DrawCenterTrafficArrows(
                    graphics,
                    centerBounds,
                    iconBounds,
                    downloadFillRatio,
                    uploadFillRatio,
                    visualDownloadFillRatio,
                    visualUploadFillRatio);
            }

            this.DrawActivityBorderLights(
                graphics,
                visualDownloadFillRatio,
                visualUploadFillRatio);
        }

        private void DrawHudOnlyMeterGuideCircles(
            Graphics graphics,
            RectangleF sharedRingBounds,
            float sharedRingWidth)
        {
            GraphicsState state = graphics.Save();

            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;

                float outlineWidth = 1F;
                float stableRingWidth = NormalizeStrokeWidth(sharedRingWidth);
                float edgeOffset = Math.Max(0F, (stableRingWidth - outlineWidth) / 2F);
                RectangleF baseBounds = GetStableArcBounds(sharedRingBounds);
                RectangleF outerBounds = GetStableArcBounds(
                    InflateRectangle(baseBounds, edgeOffset + 2F));
                RectangleF innerBounds = GetStableArcBounds(
                    InflateRectangle(baseBounds, -(edgeOffset + 2F)));

                Color outerColor = Color.FromArgb(220, 164, 228, 255);
                Color innerColor = Color.FromArgb(212, 132, 210, 255);

                using (Pen outerPen = new Pen(outerColor, outlineWidth))
                using (Pen innerPen = new Pen(innerColor, outlineWidth))
                {
                    outerPen.Alignment = PenAlignment.Center;
                    innerPen.Alignment = PenAlignment.Center;
                    graphics.DrawEllipse(outerPen, outerBounds);
                    graphics.DrawEllipse(innerPen, innerBounds);
                }
            }
            finally
            {
                graphics.Restore(state);
            }
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

                if (this.taskbarMonitorTimer != null)
                {
                    this.taskbarMonitorTimer.Dispose();
                }

                if (this.taskbarRefreshDebounceTimer != null)
                {
                    this.taskbarRefreshDebounceTimer.Dispose();
                }

                this.DisposeSurfaceBitmaps();
                ReleaseCachedMeterCenterAsset();

                DisposeFont(this.captionFont);
                DisposeFont(this.valueFont);
                DisposeFont(this.formFont);
            }

            base.Dispose(disposing);
        }

        public void ApplySettings(MonitorSettings newSettings)
        {
            bool popupScaleChanged = this.settings.PopupScalePercent != newSettings.PopupScalePercent;
            bool sectionModeChanged = this.settings.PopupSectionMode != newSettings.PopupSectionMode;
            bool taskbarIntegrationChanged = this.settings.TaskbarIntegrationEnabled != newSettings.TaskbarIntegrationEnabled;
            Rectangle previousBounds = new Rectangle(this.Location, this.Size);
            this.settings = newSettings.Clone();
            this.ApplyWindowZOrderMode();
            this.UpdateTopMostGuardState();
            if (taskbarIntegrationChanged && this.settings.TaskbarIntegrationEnabled)
            {
                this.taskbarLocalZOrderRepairPending = true;
                this.lastTaskbarLocalZOrderAnchorHandle = IntPtr.Zero;
            }

            if (!this.settings.TaskbarIntegrationEnabled)
            {
                this.activeTaskbarSnapshot = null;
                this.taskbarNoSpaceMessageShown = false;
                this.taskbarIntegrationForceRightOnlySection = false;
                this.taskbarIntegrationStickyRightOnlySection = false;
                this.lastAppliedTaskbarThickness = -1;
                this.taskbarLocalZOrderRepairPending = true;
                this.lastTaskbarLocalZOrderAnchorHandle = IntPtr.Zero;
                this.ClearTaskbarIntegrationRefreshDebounce();
            }

            if (popupScaleChanged || sectionModeChanged || taskbarIntegrationChanged)
            {
                this.ApplyDpiLayout(this.currentDpi, false);

                if (this.Visible && !this.settings.TaskbarIntegrationEnabled)
                {
                    Point preferredLocation = this.GetPopupScaleAdjustedLocation(previousBounds);
                    this.Location = this.GetVisiblePopupLocation(
                        preferredLocation,
                        GetRectangleCenter(previousBounds),
                        "popup-scale-clamped",
                        "Popup-Position wurde nach einer Groessenaenderung auf einen sichtbaren Arbeitsbereich begrenzt.");
                }

                this.lastPresentedLocation = new Point(int.MinValue, int.MinValue);
                this.lastPresentedSize = Size.Empty;
            }

            this.staticSurfaceDirty = true;
            this.ApplyWindowTransparency();
            this.lastSampleUtc = DateTime.MinValue;
            this.lastReceivedBytes = 0L;
            this.lastSentBytes = 0L;
            this.latestDownloadBytesPerSecond = 0D;
            this.latestUploadBytesPerSecond = 0D;
            this.displayedDownloadBytesPerSecond = 0D;
            this.displayedUploadBytesPerSecond = 0D;
            this.ringDisplayDownloadBytesPerSecond = 0D;
            this.ringDisplayUploadBytesPerSecond = 0D;
            this.peakHoldDownloadBytesPerSecond = 0D;
            this.peakHoldUploadBytesPerSecond = 0D;
            this.peakHoldDownloadCapturedUtc = DateTime.MinValue;
            this.peakHoldUploadCapturedUtc = DateTime.MinValue;
            this.lastAnimationAdvanceUtc = DateTime.MinValue;
            this.ResetDisplayedRateSmoothing();
            this.trafficHistory.Clear();
            this.trafficHistoryVersion++;
            this.visualDownloadPeakBytesPerSecond = Math.Max(
                this.settings.GetDownloadVisualizationPeak(),
                this.GetMinimumVisualizationPeakBytesPerSecond(true));
            this.visualUploadPeakBytesPerSecond = Math.Max(
                this.settings.GetUploadVisualizationPeak(),
                this.GetMinimumVisualizationPeakBytesPerSecond(false));
            this.RefreshTraffic();
            this.UpdateTaskbarMonitorState();

            if (this.settings.TaskbarIntegrationEnabled &&
                (this.Visible || this.taskbarIntegrationDisplayRequested))
            {
                this.taskbarIntegrationDisplayRequested = true;
                this.RefreshTaskbarIntegration(false, false);
            }
        }

        private void ApplyWindowTransparency()
        {
            this.Opacity = 1D;
            this.BackColor = this.GetPanelBackgroundBaseColor();
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

        private Color GetPanelBackgroundBaseColor()
        {
            return this.activeTaskbarSnapshot != null
                ? this.activeTaskbarSnapshot.Theme.BaseColor
                : BackgroundBlue;
        }

        private Color GetPanelBorderBaseColor()
        {
            return this.activeTaskbarSnapshot != null
                ? this.activeTaskbarSnapshot.Theme.BorderColor
                : BorderColor;
        }

        private Color GetPanelDividerBaseColor()
        {
            return this.activeTaskbarSnapshot != null
                ? this.activeTaskbarSnapshot.Theme.DividerColor
                : DividerColor;
        }

        private byte GetTaskbarBackgroundOverlayAlpha()
        {
            return this.activeTaskbarSnapshot != null
                ? this.activeTaskbarSnapshot.Theme.OverlayAlpha
                : (byte)0;
        }

        public void ShowNearTray(bool activateWindow = true)
        {
            if (this.settings.TaskbarIntegrationEnabled)
            {
                this.taskbarIntegrationDisplayRequested = true;
                this.RefreshTaskbarIntegration(activateWindow, true);
                return;
            }

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
            if (this.settings.TaskbarIntegrationEnabled)
            {
                this.taskbarIntegrationDisplayRequested = true;
                this.RefreshTaskbarIntegration(activateWindow, true);
                return;
            }

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
            if (this.settings.TaskbarIntegrationEnabled)
            {
                this.taskbarIntegrationDisplayRequested = true;
                this.RefreshTaskbarIntegration(false, false);
                return;
            }

            if (!this.Visible)
            {
                return;
            }

            this.WindowState = FormWindowState.Normal;
            this.EnsureTopMostPlacement(false);
        }

        public bool TryGetAutomaticTaskbarIntegrationStateChange(out bool enableTaskbarIntegration, out Point desktopLocation)
        {
            enableTaskbarIntegration = false;
            desktopLocation = Point.Empty;

            TaskbarIntegrationSnapshot snapshot;
            if (!this.TryCaptureTaskbarIntegrationSnapshot(out snapshot) || snapshot.IsHidden)
            {
                if (this.settings.TaskbarIntegrationEnabled &&
                    this.taskbarIntegrationPreferredLocation.HasValue &&
                    this.ShouldDetachFromTaskbarWithoutSnapshot(this.taskbarIntegrationPreferredLocation.Value))
                {
                    Point preferredLocationWithoutSnapshot = this.taskbarIntegrationPreferredLocation.Value;
                    Rectangle preferredBoundsWithoutSnapshot = new Rectangle(preferredLocationWithoutSnapshot, this.Size);
                    this.taskbarIntegrationPreferredLocation = null;
                    desktopLocation = this.GetVisiblePopupLocation(
                        preferredLocationWithoutSnapshot,
                        GetRectangleCenter(preferredBoundsWithoutSnapshot),
                        null,
                        null);
                    return true;
                }

                return false;
            }

            if (this.settings.TaskbarIntegrationEnabled)
            {
                Point preferredLocation = this.taskbarIntegrationPreferredLocation ?? this.Location;
                Rectangle preferredBounds = new Rectangle(preferredLocation, this.Size);
                if (this.ShouldAutoIntegrateWithTaskbar(preferredBounds, snapshot))
                {
                    return false;
                }

                this.taskbarIntegrationPreferredLocation = null;
                desktopLocation = this.GetVisiblePopupLocation(
                    preferredLocation,
                    GetRectangleCenter(preferredBounds),
                    null,
                    null);
                return true;
            }

            Rectangle popupBounds = new Rectangle(this.Location, this.Size);
            if (!this.ShouldAutoIntegrateWithTaskbar(popupBounds, snapshot))
            {
                return false;
            }

            enableTaskbarIntegration = true;
            // When the popup is dropped onto the taskbar from the desktop,
            // start with the deterministic right-biased placement instead of
            // preserving the transient drag location inside the free band.
            this.taskbarIntegrationPreferredLocation = null;
            return true;
        }

        private bool ShouldDetachFromTaskbarWithoutSnapshot(Point preferredLocation)
        {
            Rectangle anchorBounds = this.lastSuccessfulTaskbarPlacementBounds.Width > 0 &&
                this.lastSuccessfulTaskbarPlacementBounds.Height > 0
                ? this.lastSuccessfulTaskbarPlacementBounds
                : this.GetCurrentPopupScreenBounds();
            Rectangle preferredBounds = new Rectangle(preferredLocation, this.Size);

            int horizontalDistance = Math.Abs(preferredBounds.Left - anchorBounds.Left);
            int verticalDistance = Math.Abs(preferredBounds.Top - anchorBounds.Top);
            int detachThreshold = Math.Max(
                this.ScaleValue(TaskbarDragSnapDistance),
                this.ScaleValue(TaskbarDesktopSnapHoldDistance));

            return horizontalDistance >= detachThreshold || verticalDistance >= detachThreshold;
        }

        private void UpdateTaskbarMonitorState()
        {
            if (this.taskbarMonitorTimer == null)
            {
                return;
            }

            this.taskbarMonitorTimer.Enabled = this.settings != null &&
                this.settings.TaskbarIntegrationEnabled &&
                (this.Visible || this.taskbarIntegrationDisplayRequested);
        }

        private void UpdateTopMostGuardState()
        {
            if (this.topMostGuardTimer == null)
            {
                return;
            }

            this.topMostGuardTimer.Enabled = this.Visible &&
                this.ShouldUseGlobalTopMost() &&
                !this.IsTopMostEnforcementPaused;
        }

        public void ClearTaskbarIntegrationPreferredLocation()
        {
            this.taskbarIntegrationPreferredLocation = null;
        }

        public void ApplyDefaultTaskbarSectionModeOverride()
        {
            if (this.settings == null || !this.settings.TaskbarIntegrationEnabled)
            {
                return;
            }

            this.SetTaskbarIntegrationStickyRightOnly(false);
            this.SetTaskbarIntegrationForcedRightOnly(true);
        }

        public void ClearTaskbarDefaultSectionModeOverride()
        {
            this.SetTaskbarIntegrationStickyRightOnly(false);
            this.SetTaskbarIntegrationForcedRightOnly(false);
        }

        public void ShowAtRightBiasedTaskbarPlacement(bool activateWindow, bool showNoSpaceMessage)
        {
            if (!this.settings.TaskbarIntegrationEnabled)
            {
                this.ShowNearTray(activateWindow);
                return;
            }

            this.taskbarIntegrationDisplayRequested = true;
            this.taskbarIntegrationPreferredLocation = null;
            this.lastDesktopShellForegroundUtc = DateTime.MinValue;

            TaskbarIntegrationSnapshot snapshot;
            if (!this.TryCaptureTaskbarIntegrationSnapshot(out snapshot))
            {
                this.RefreshTaskbarIntegration(activateWindow, showNoSpaceMessage);
                return;
            }

            this.TrackTaskbarLocalZOrderAnchor(snapshot);
            this.activeTaskbarSnapshot = snapshot;
            this.ApplyTaskbarHostBinding(IntPtr.Zero);
            if (this.NeedsTaskbarIntegrationLayoutRefresh(snapshot) || this.NeedsCurrentDpiLayout())
            {
                this.ApplyDpiLayout(this.currentDpi, false);
            }

            Rectangle placementBounds;
            if (this.TryGetTaskbarPlacementBoundsWithCompactFallback(snapshot, out placementBounds))
            {
                this.taskbarNoSpaceMessageShown = false;
                this.lastSuccessfulTaskbarPlacementUtc = DateTime.UtcNow;
                this.lastSuccessfulTaskbarPlacementBounds = placementBounds;
                this.ShowAtTaskbarPlacement(placementBounds, activateWindow);
                this.UpdateTaskbarMonitorState();
                return;
            }

            this.HideForTaskbarIntegrationCondition();
            this.ShowNoSpaceMessageIfNeeded(showNoSpaceMessage);
            this.UpdateTaskbarMonitorState();
        }

        private void TaskbarMonitorTimer_Tick(object sender, EventArgs e)
        {
            if (!this.settings.TaskbarIntegrationEnabled ||
                (!this.Visible && !this.taskbarIntegrationDisplayRequested) ||
                this.IsDisposed)
            {
                this.UpdateTaskbarMonitorState();
                return;
            }

            if (this.IsOverlayDragInProgress())
            {
                return;
            }

            if (this.IsDesktopShellForegroundWindow())
            {
                this.TryRestoreTaskbarPresenceDuringDesktopShell();
                return;
            }

            if (this.IsTaskbarForegroundWindow())
            {
                if (this.TryRefreshTaskbarPlacementDuringTaskbarFocus())
                {
                    this.EnsurePassiveTaskbarPresence();
                }

                return;
            }

            this.RefreshTaskbarIntegration(false, false);
        }

        private void TryRestoreTaskbarPresenceDuringDesktopShell()
        {
            if (!this.taskbarIntegrationDisplayRequested ||
                this.lastSuccessfulTaskbarPlacementBounds.Width <= 0 ||
                this.lastSuccessfulTaskbarPlacementBounds.Height <= 0)
            {
                return;
            }

            if (!this.Visible)
            {
                this.ShowAtTaskbarPlacement(this.lastSuccessfulTaskbarPlacementBounds, false);
                return;
            }

            if (this.GetCurrentPopupScreenBounds() != this.lastSuccessfulTaskbarPlacementBounds)
            {
                this.ShowAtTaskbarPlacement(this.lastSuccessfulTaskbarPlacementBounds, false);
                return;
            }

            this.EnsurePassiveTaskbarPresence();
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

            if (!this.ShouldUseGlobalTopMost())
            {
                this.ApplyWindowZOrderMode();
                return;
            }

            this.EnsureTopMostPlacement(activateWindow);
        }

        private void RefreshTaskbarIntegration(bool activateWindow, bool showNoSpaceMessage)
        {
            this.RefreshTaskbarIntegration(activateWindow, showNoSpaceMessage, false);
        }

        private void RefreshTaskbarIntegration(bool activateWindow, bool showNoSpaceMessage, bool bypassDebounce)
        {
            if (this.IsTaskbarIntegratedMode())
            {
                activateWindow = false;
                if (!bypassDebounce &&
                    !showNoSpaceMessage &&
                    this.TryDebounceTaskbarIntegrationRefresh())
                {
                    return;
                }
            }

            if (this.taskbarIntegrationRefreshInProgress)
            {
                this.taskbarIntegrationRefreshPending = true;
                this.taskbarIntegrationPendingActivateWindow = this.taskbarIntegrationPendingActivateWindow || activateWindow;
                this.taskbarIntegrationPendingShowNoSpaceMessage = this.taskbarIntegrationPendingShowNoSpaceMessage || showNoSpaceMessage;
                return;
            }

            if (this.IsTaskbarIntegratedMode())
            {
                this.lastTaskbarIntegrationRefreshUtc = DateTime.UtcNow;
            }

            this.taskbarIntegrationRefreshInProgress = true;
            try
            {
                if (!this.settings.TaskbarIntegrationEnabled)
                {
                    this.ApplyTaskbarHostBinding(IntPtr.Zero);
                    this.activeTaskbarSnapshot = null;
                    this.taskbarNoSpaceMessageShown = false;
                    this.taskbarIntegrationStickyRightOnlySection = false;
                    this.SetTaskbarIntegrationForcedRightOnly(false);
                    this.taskbarIntegrationPreferredLocation = null;
                    this.lastAppliedTaskbarThickness = -1;
                    this.lastSuccessfulTaskbarPlacementUtc = DateTime.MinValue;
                    this.lastSuccessfulTaskbarPlacementBounds = Rectangle.Empty;
                    this.ClearTaskbarIntegrationRefreshDebounce();
                    this.UpdateTaskbarMonitorState();
                    return;
                }

                if (!this.taskbarIntegrationDisplayRequested && !this.Visible)
                {
                    this.UpdateTaskbarMonitorState();
                    return;
                }

                TaskbarIntegrationSnapshot snapshot;
                if (!this.TryCaptureTaskbarIntegrationSnapshot(out snapshot))
                {
                    this.activeTaskbarSnapshot = null;
                    if (this.TryPreserveTaskbarPlacement(activateWindow, !showNoSpaceMessage))
                    {
                        this.UpdateTaskbarMonitorState();
                        return;
                    }

                    this.HideForTaskbarIntegrationCondition();
                    this.UpdateTaskbarMonitorState();
                    return;
                }

                this.TrackTaskbarLocalZOrderAnchor(snapshot);
                this.activeTaskbarSnapshot = snapshot;
                this.ApplyTaskbarHostBinding(IntPtr.Zero);
                if (this.NeedsTaskbarIntegrationLayoutRefresh(snapshot) || this.NeedsCurrentDpiLayout())
                {
                    this.ApplyDpiLayout(this.currentDpi, false);
                }

                bool shouldYieldToFullscreen = this.ShouldYieldToFullscreenForegroundWindow(snapshot.ScreenBounds);
                if (snapshot.IsHidden || shouldYieldToFullscreen)
                {
                    if (!shouldYieldToFullscreen &&
                        this.TryPreserveTaskbarPlacement(activateWindow, !showNoSpaceMessage))
                    {
                        this.UpdateTaskbarMonitorState();
                        return;
                    }

                    this.HideForTaskbarIntegrationCondition();
                    this.UpdateTaskbarMonitorState();
                    return;
                }

                Rectangle placementBounds;
                if (!this.TryGetTaskbarPlacementBoundsWithCompactFallback(snapshot, out placementBounds))
                {
                    if (this.TryPreserveRightAnchoredTaskbarPlacement(snapshot, activateWindow))
                    {
                        this.UpdateTaskbarMonitorState();
                        return;
                    }

                    this.HideForTaskbarIntegrationCondition();
                    this.ShowNoSpaceMessageIfNeeded(showNoSpaceMessage);
                    this.UpdateTaskbarMonitorState();
                    return;
                }

                this.taskbarNoSpaceMessageShown = false;
                this.lastSuccessfulTaskbarPlacementUtc = DateTime.UtcNow;
                this.lastSuccessfulTaskbarPlacementBounds = placementBounds;
                Rectangle currentBounds = this.GetCurrentPopupScreenBounds();
                if (!activateWindow &&
                    this.Visible &&
                    this.WindowState == FormWindowState.Normal &&
                    currentBounds == placementBounds &&
                    this.IsDesktopShellForegroundWindow())
                {
                    this.lastDesktopShellForegroundUtc = DateTime.UtcNow;
                    if (this.taskbarLocalZOrderRepairPending)
                    {
                        this.EnsurePassiveTaskbarPresence();
                    }

                    this.UpdateTaskbarMonitorState();
                    return;
                }

                if (this.TryHandleFirstTaskbarRefreshAfterDesktop(placementBounds, activateWindow))
                {
                    this.UpdateTaskbarMonitorState();
                    return;
                }

                this.ShowAtTaskbarPlacement(placementBounds, activateWindow);
                this.UpdateTaskbarMonitorState();
            }
            finally
            {
                this.taskbarIntegrationRefreshInProgress = false;
            }

            if (this.taskbarIntegrationRefreshPending && !this.IsDisposed)
            {
                bool pendingActivateWindow = this.taskbarIntegrationPendingActivateWindow;
                bool pendingShowNoSpaceMessage = this.taskbarIntegrationPendingShowNoSpaceMessage;
                this.taskbarIntegrationRefreshPending = false;
                this.taskbarIntegrationPendingActivateWindow = false;
                this.taskbarIntegrationPendingShowNoSpaceMessage = false;
                this.TryBeginInvokeSafely(new Action(delegate
                {
                    if (!this.IsDisposed)
                    {
                        this.RefreshTaskbarIntegration(pendingActivateWindow, pendingShowNoSpaceMessage);
                    }
                }));
            }
        }

        private bool TryDebounceTaskbarIntegrationRefresh()
        {
            if (this.taskbarRefreshDebounceTimer == null ||
                this.lastTaskbarIntegrationRefreshUtc == DateTime.MinValue)
            {
                return false;
            }

            double elapsedMilliseconds = (DateTime.UtcNow - this.lastTaskbarIntegrationRefreshUtc).TotalMilliseconds;
            if (elapsedMilliseconds >= TaskbarRefreshDebounceMs)
            {
                return false;
            }

            int delayMilliseconds = Math.Max(1, TaskbarRefreshDebounceMs - (int)elapsedMilliseconds);
            this.taskbarIntegrationDebouncedRefreshPending = true;
            this.taskbarRefreshDebounceTimer.Stop();
            this.taskbarRefreshDebounceTimer.Interval = delayMilliseconds;
            this.taskbarRefreshDebounceTimer.Start();
            return true;
        }

        private void TaskbarRefreshDebounceTimer_Tick(object sender, EventArgs e)
        {
            this.taskbarRefreshDebounceTimer.Stop();
            if (!this.taskbarIntegrationDebouncedRefreshPending)
            {
                return;
            }

            this.taskbarIntegrationDebouncedRefreshPending = false;
            if (!this.IsDisposed && this.IsTaskbarIntegratedMode())
            {
                this.RefreshTaskbarIntegration(false, false, true);
            }
        }

        private void ClearTaskbarIntegrationRefreshDebounce()
        {
            this.taskbarIntegrationDebouncedRefreshPending = false;
            this.lastTaskbarIntegrationRefreshUtc = DateTime.MinValue;
            if (this.taskbarRefreshDebounceTimer != null)
            {
                this.taskbarRefreshDebounceTimer.Stop();
            }
        }

        private void SetTaskbarIntegrationForcedRightOnly(bool forceRightOnly)
        {
            if (this.taskbarIntegrationForceRightOnlySection == forceRightOnly)
            {
                return;
            }

            this.taskbarIntegrationForceRightOnlySection = forceRightOnly;
            this.ApplyDpiLayout(this.currentDpi, false);
            this.lastPresentedLocation = new Point(int.MinValue, int.MinValue);
            this.lastPresentedSize = Size.Empty;
            this.staticSurfaceDirty = true;
        }

        private void SetTaskbarIntegrationStickyRightOnly(bool stickyRightOnly)
        {
            if (this.taskbarIntegrationStickyRightOnlySection == stickyRightOnly)
            {
                return;
            }

            this.taskbarIntegrationStickyRightOnlySection = stickyRightOnly;
            this.ApplyDpiLayout(this.currentDpi, false);
            this.lastPresentedLocation = new Point(int.MinValue, int.MinValue);
            this.lastPresentedSize = Size.Empty;
            this.staticSurfaceDirty = true;
        }

        private void TryToggleTaskbarSectionModeFromLeftDrag()
        {
            if (this.settings == null ||
                !this.settings.TaskbarIntegrationEnabled ||
                this.GetConfiguredPopupSectionMode() != PopupSectionMode.Both)
            {
                return;
            }

            TaskbarIntegrationSnapshot snapshot = this.activeTaskbarSnapshot;
            if ((snapshot == null && !this.TryCaptureTaskbarIntegrationSnapshot(out snapshot)) ||
                snapshot == null ||
                snapshot.IsVertical)
            {
                return;
            }

            Point cursorPosition = Cursor.Position;
            int deltaX = cursorPosition.X - this.dragStartCursor.X;
            int deltaY = cursorPosition.Y - this.dragStartCursor.Y;
            int toggleThreshold = this.ScaleValue(TaskbarSectionToggleDragThreshold);
            if (deltaX > -toggleThreshold || Math.Abs(deltaY) > toggleThreshold * 2)
            {
                return;
            }

            Rectangle popupBounds = this.GetCurrentPopupScreenBounds();
            if (!this.ShouldAutoIntegrateWithTaskbar(popupBounds, snapshot))
            {
                return;
            }

            if (this.taskbarIntegrationStickyRightOnlySection)
            {
                this.SetTaskbarIntegrationStickyRightOnly(false);
                this.SetTaskbarIntegrationForcedRightOnly(false);
                this.RefreshTaskbarIntegration(false, false);
                return;
            }

            if (this.GetEffectivePopupSectionMode() != PopupSectionMode.Both)
            {
                return;
            }

            this.SetTaskbarIntegrationForcedRightOnly(false);
            this.SetTaskbarIntegrationStickyRightOnly(true);
            this.RefreshTaskbarIntegration(false, false);
        }

        private void HideForTaskbarIntegrationCondition()
        {
            if (!this.Visible)
            {
                return;
            }

            this.taskbarIntegrationVisibilityChange = true;
            try
            {
                base.Hide();
            }
            finally
            {
                this.taskbarIntegrationVisibilityChange = false;
            }
        }

        private void ShowAtTaskbarPlacement(Rectangle placementBounds, bool activateWindow)
        {
            bool wasVisible = this.Visible;
            bool locationChanged = this.Location != placementBounds.Location;
            bool windowStateChanged = this.WindowState != FormWindowState.Normal;
            bool needsLocalZOrderRepair = this.ShouldUseTaskbarLocalZOrder() &&
                (!wasVisible || windowStateChanged || this.taskbarLocalZOrderRepairPending);

            if (locationChanged)
            {
                this.Location = placementBounds.Location;
            }

            if (!wasVisible)
            {
                this.taskbarIntegrationVisibilityChange = true;
                try
                {
                    this.Show();
                }
                finally
                {
                    this.taskbarIntegrationVisibilityChange = false;
                }
            }

            if (windowStateChanged)
            {
                this.WindowState = FormWindowState.Normal;
            }

            if (this.ShouldUseGlobalTopMost())
            {
                this.EnsureTopMostPlacement(activateWindow);
            }
            else
            {
                this.ApplyWindowZOrderMode();
                this.EnsureTaskbarLocalFrontPlacement(needsLocalZOrderRepair);
            }

            this.RefreshVisualSurface();
        }

        private void TrackTaskbarLocalZOrderAnchor(TaskbarIntegrationSnapshot snapshot)
        {
            if (!this.ShouldUseTaskbarLocalZOrder() ||
                snapshot == null ||
                snapshot.TaskbarZOrderAnchorHandle == IntPtr.Zero)
            {
                return;
            }

            if (this.lastTaskbarLocalZOrderAnchorHandle != IntPtr.Zero &&
                snapshot.TaskbarZOrderAnchorHandle != this.lastTaskbarLocalZOrderAnchorHandle)
            {
                this.taskbarLocalZOrderRepairPending = true;
            }
        }

        private void EnsureTaskbarLocalFrontPlacement(bool forceRepair)
        {
            if (!this.ShouldUseTaskbarLocalZOrder() ||
                !this.IsHandleCreated ||
                !this.Visible ||
                this.IsDisposed)
            {
                return;
            }

            TaskbarIntegrationSnapshot snapshot = this.activeTaskbarSnapshot;
            if (snapshot == null && !this.TryCaptureTaskbarIntegrationSnapshot(out snapshot))
            {
                return;
            }

            this.TrackTaskbarLocalZOrderAnchor(snapshot);
            this.activeTaskbarSnapshot = snapshot;

            IntPtr anchorHandle = snapshot.TaskbarZOrderAnchorHandle != IntPtr.Zero
                ? snapshot.TaskbarZOrderAnchorHandle
                : snapshot.TaskbarHandle;
            if (anchorHandle == IntPtr.Zero || anchorHandle == this.Handle)
            {
                return;
            }

            if (!forceRepair &&
                !this.taskbarLocalZOrderRepairPending &&
                this.lastTaskbarLocalZOrderAnchorHandle == anchorHandle &&
                this.IsWindowAboveZOrderAnchor(anchorHandle))
            {
                return;
            }

            IntPtr insertAfterHandle = this.GetTaskbarLocalInsertAfterHandle(anchorHandle);
            if (insertAfterHandle == IntPtr.Zero || insertAfterHandle == this.Handle)
            {
                return;
            }

            uint flags = SwpNoMove | SwpNoSize | SwpNoOwnerZOrder | SwpNoSendChanging | SwpNoActivate;
            bool repaired = SetWindowPos(this.Handle, insertAfterHandle, this.Left, this.Top, this.Width, this.Height, flags);
            if (!repaired)
            {
                AppLog.WarnOnce(
                    "taskbar-local-zorder-setwindowpos",
                    string.Format(
                        "SetWindowPos failed while repairing the taskbar-local z-order. Win32={0}",
                        Marshal.GetLastWin32Error()));
            }

            if (repaired && this.IsWindowAboveZOrderAnchor(anchorHandle))
            {
                this.lastTaskbarLocalZOrderAnchorHandle = anchorHandle;
                this.taskbarLocalZOrderRepairPending = false;
            }
            else
            {
                this.taskbarLocalZOrderRepairPending = true;
            }
        }

        private IntPtr GetTaskbarLocalInsertAfterHandle(IntPtr anchorHandle)
        {
            IntPtr insertAfterHandle = GetWindow(anchorHandle, GwHwndPrev);
            while (insertAfterHandle == this.Handle)
            {
                insertAfterHandle = GetWindow(insertAfterHandle, GwHwndPrev);
            }

            return insertAfterHandle;
        }

        private bool IsWindowAboveZOrderAnchor(IntPtr anchorHandle)
        {
            return this.IsWindowAboveWindow(this.Handle, anchorHandle);
        }

        private bool IsWindowAboveWindow(IntPtr windowHandle, IntPtr anchorHandle)
        {
            if (windowHandle == IntPtr.Zero || anchorHandle == IntPtr.Zero || windowHandle == anchorHandle)
            {
                return false;
            }

            IntPtr currentHandle = GetWindow(windowHandle, GwHwndNext);
            while (currentHandle != IntPtr.Zero)
            {
                if (currentHandle == anchorHandle)
                {
                    return true;
                }

                currentHandle = GetWindow(currentHandle, GwHwndNext);
            }

            return false;
        }

        private bool TryPreserveTaskbarPlacement(bool activateWindow, bool allowExpiredPlacement)
        {
            if (this.lastSuccessfulTaskbarPlacementBounds.Width <= 0 ||
                this.lastSuccessfulTaskbarPlacementBounds.Height <= 0)
            {
                return false;
            }

            if (!allowExpiredPlacement &&
                (DateTime.UtcNow - this.lastSuccessfulTaskbarPlacementUtc).TotalMilliseconds > TaskbarTransientFailureGraceMs)
            {
                return false;
            }

            Rectangle currentBounds = this.GetCurrentPopupScreenBounds();
            if (!activateWindow &&
                this.Visible &&
                this.WindowState == FormWindowState.Normal &&
                currentBounds == this.lastSuccessfulTaskbarPlacementBounds &&
                this.IsDesktopShellForegroundWindow())
            {
                this.lastDesktopShellForegroundUtc = DateTime.UtcNow;
                if (this.taskbarLocalZOrderRepairPending)
                {
                    this.EnsurePassiveTaskbarPresence();
                }

                return true;
            }

            if (this.TryHandleFirstTaskbarRefreshAfterDesktop(this.lastSuccessfulTaskbarPlacementBounds, activateWindow))
            {
                return true;
            }

            this.ShowAtTaskbarPlacement(this.lastSuccessfulTaskbarPlacementBounds, activateWindow);
            return true;
        }

        private void ShowNoSpaceMessageIfNeeded(bool requestedByUser)
        {
            if (this.taskbarNoSpaceMessageVisible)
            {
                return;
            }

            if (!requestedByUser &&
                this.taskbarNoSpaceMessageShown &&
                (DateTime.UtcNow - this.lastNoSpaceMessageUtc).TotalMilliseconds < NoSpaceMessageCooldownMs)
            {
                return;
            }

            this.taskbarNoSpaceMessageShown = true;
            this.lastNoSpaceMessageUtc = DateTime.UtcNow;
            this.taskbarNoSpaceMessageVisible = true;
            try
            {
                MessageBox.Show(
                    "Kein Platz auf der Taskleiste",
                    "TrafficView",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                EventHandler handler = this.TaskbarIntegrationNoSpaceAcknowledged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
            finally
            {
                this.taskbarNoSpaceMessageVisible = false;
            }
        }

        public new void Hide()
        {
            this.taskbarIntegrationDisplayRequested = false;
            base.Hide();
        }

        private void EnsureTopMostPlacement(bool activateWindow)
        {
            if (!this.ShouldUseGlobalTopMost())
            {
                this.ApplyWindowZOrderMode();
                return;
            }

            if (!this.IsHandleCreated || this.IsTopMostEnforcementPaused)
            {
                return;
            }

            if (!this.TopMost)
            {
                this.TopMost = true;
            }

            uint flags = SwpNoMove | SwpNoSize | SwpNoOwnerZOrder | SwpNoSendChanging;
            if (!this.Visible)
            {
                flags |= SwpShowWindow;
            }
            else
            {
                flags |= SwpNoRedraw;
            }
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

        private Rectangle GetCurrentPopupScreenBounds()
        {
            return new Rectangle(this.Location, this.Size);
        }

        private void ApplyTaskbarHostBinding(IntPtr hostHandle)
        {
            this.taskbarIntegrationHostHandle = hostHandle;
        }

        private bool TryCaptureTaskbarIntegrationSnapshot(out TaskbarIntegrationSnapshot snapshot)
        {
            snapshot = null;

            Rectangle targetScreenBounds;
            IntPtr taskbarHandle;
            if (!this.TryFindRelevantTaskbarWindow(out taskbarHandle, out targetScreenBounds))
            {
                return false;
            }

            if (taskbarHandle == IntPtr.Zero || !IsWindowVisible(taskbarHandle))
            {
                return false;
            }

            NativeRect windowRect;
            if (!GetWindowRect(taskbarHandle, out windowRect))
            {
                return false;
            }

            Rectangle visibleBounds = windowRect.ToRectangle();
            visibleBounds = Rectangle.Intersect(visibleBounds, targetScreenBounds);
            if (visibleBounds.Width <= 0 || visibleBounds.Height <= 0)
            {
                return false;
            }

            AppBarData appBarData = new AppBarData();
            appBarData.CbSize = Marshal.SizeOf(typeof(AppBarData));
            appBarData.HWnd = taskbarHandle;

            AppBarEdge edge = DetermineTaskbarEdge(visibleBounds, targetScreenBounds);
            if (SHAppBarMessage(AbmGetTaskbarPos, ref appBarData) != 0)
            {
                edge = appBarData.UEdge;
            }

            uint state = SHAppBarMessage(AbmGetState, ref appBarData);
            bool autoHide = (state & AbsAutoHide) == AbsAutoHide;
            int visibleThickness = edge == AppBarEdge.Left || edge == AppBarEdge.Right
                ? visibleBounds.Width
                : visibleBounds.Height;
            bool hidden = visibleThickness < Math.Max(1, DpiHelper.Scale(MinimumVisibleTaskbarThickness, this.currentDpi));

            bool usesCustomTaskListHeuristic =
                HasVisibleDescendantWindowClass(taskbarHandle, "SIBTrayButton") ||
                HasVisibleDescendantWindowClass(taskbarHandle, "MSTaskListWClass");

            Rectangle[] occupiedBounds = GetTaskbarOccupiedLeafBounds(
                taskbarHandle,
                visibleBounds,
                usesCustomTaskListHeuristic);
            occupiedBounds = this.GetAugmentedTaskbarOccupiedBounds(
                visibleBounds,
                edge,
                occupiedBounds);

            snapshot = new TaskbarIntegrationSnapshot
            {
                TaskbarHandle = taskbarHandle,
                TaskbarZOrderAnchorHandle = this.ResolveTaskbarLocalZOrderAnchor(
                    taskbarHandle,
                    visibleBounds,
                    usesCustomTaskListHeuristic),
                Edge = edge,
                Bounds = visibleBounds,
                ScreenBounds = targetScreenBounds,
                AutoHide = autoHide,
                IsHidden = hidden,
                UsesCustomTaskListHeuristic = usesCustomTaskListHeuristic,
                OccupiedBounds = occupiedBounds,
                Theme = this.CreateTaskbarVisualTheme(visibleBounds, occupiedBounds)
            };

            return true;
        }

        private IntPtr ResolveTaskbarLocalZOrderAnchor(
            IntPtr taskbarHandle,
            Rectangle taskbarBounds,
            bool usesCustomTaskListHeuristic)
        {
            if (taskbarHandle == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            IntPtr bestHandle = taskbarHandle;
            EnumWindows(delegate(IntPtr windowHandle, IntPtr lParam)
            {
                if (windowHandle == this.Handle ||
                    windowHandle == IntPtr.Zero ||
                    !IsWindowVisible(windowHandle))
                {
                    return true;
                }

                NativeRect candidateRect;
                if (!GetWindowRect(windowHandle, out candidateRect))
                {
                    return true;
                }

                Rectangle intersectionBounds = Rectangle.Intersect(candidateRect.ToRectangle(), taskbarBounds);
                if (intersectionBounds.Width <= 0 || intersectionBounds.Height <= 0)
                {
                    return true;
                }

                string className = GetWindowClassName(windowHandle);
                if (!IsTaskbarLocalZOrderAnchorWindowClass(className, usesCustomTaskListHeuristic))
                {
                    return true;
                }

                if (bestHandle == IntPtr.Zero || this.IsWindowAboveWindow(windowHandle, bestHandle))
                {
                    bestHandle = windowHandle;
                }

                return true;
            }, IntPtr.Zero);

            return bestHandle;
        }

        private bool TryFindRelevantTaskbarWindow(out IntPtr taskbarHandle, out Rectangle targetScreenBounds)
        {
            taskbarHandle = IntPtr.Zero;
            targetScreenBounds = this.GetTaskbarIntegrationTargetScreenBounds();
            IntPtr bestTaskbarHandle = IntPtr.Zero;
            Rectangle searchScreenBounds = targetScreenBounds;
            int bestIntersectionArea = 0;

            EnumWindows(delegate(IntPtr windowHandle, IntPtr lParam)
            {
                if (!IsWindowVisible(windowHandle))
                {
                    return true;
                }

                string className = GetWindowClassName(windowHandle);
                if (!IsTaskbarRootWindowClass(className))
                {
                    return true;
                }

                NativeRect windowRect;
                if (!GetWindowRect(windowHandle, out windowRect))
                {
                    return true;
                }

                Rectangle taskbarBounds = Rectangle.Intersect(windowRect.ToRectangle(), searchScreenBounds);
                int intersectionArea = Math.Max(0, taskbarBounds.Width) * Math.Max(0, taskbarBounds.Height);
                if (intersectionArea <= 0)
                {
                    return true;
                }

                if (intersectionArea > bestIntersectionArea ||
                    (intersectionArea == bestIntersectionArea &&
                    string.Equals(className, "Shell_TrayWnd", StringComparison.Ordinal)))
                {
                    bestTaskbarHandle = windowHandle;
                    bestIntersectionArea = intersectionArea;
                }

                return true;
            }, IntPtr.Zero);

            if (bestTaskbarHandle != IntPtr.Zero)
            {
                taskbarHandle = bestTaskbarHandle;
                return true;
            }

            taskbarHandle = FindWindow("Shell_TrayWnd", null);
            return taskbarHandle != IntPtr.Zero;
        }

        private Rectangle GetTaskbarIntegrationTargetScreenBounds()
        {
            if (this.Visible)
            {
                return Screen.FromPoint(GetRectangleCenter(this.GetCurrentPopupScreenBounds())).Bounds;
            }

            if (this.lastSuccessfulTaskbarPlacementBounds.Width > 0 &&
                this.lastSuccessfulTaskbarPlacementBounds.Height > 0)
            {
                return Screen.FromPoint(GetRectangleCenter(this.lastSuccessfulTaskbarPlacementBounds)).Bounds;
            }

            if (this.taskbarIntegrationPreferredLocation.HasValue)
            {
                return Screen.FromPoint(this.taskbarIntegrationPreferredLocation.Value).Bounds;
            }

            return Screen.FromPoint(Cursor.Position).Bounds;
        }

        private static AppBarEdge DetermineTaskbarEdge(Rectangle bounds, Rectangle screenBounds)
        {
            if (bounds.Left <= screenBounds.Left && bounds.Width <= Math.Max(1, screenBounds.Width / 5))
            {
                return AppBarEdge.Left;
            }

            if (bounds.Right >= screenBounds.Right && bounds.Width <= Math.Max(1, screenBounds.Width / 5))
            {
                return AppBarEdge.Right;
            }

            if (bounds.Top <= screenBounds.Top)
            {
                return AppBarEdge.Top;
            }

            return AppBarEdge.Bottom;
        }

        private Rectangle[] GetTaskbarOccupiedLeafBounds(IntPtr taskbarHandle, Rectangle taskbarBounds, bool usesCustomTaskListHeuristic)
        {
            List<Rectangle> bounds = new List<Rectangle>();
            EnumChildWindows(taskbarHandle, delegate(IntPtr childHandle, IntPtr lParam)
            {
                if (!IsWindowVisible(childHandle))
                {
                    return true;
                }

                NativeRect childRect;
                if (!GetWindowRect(childHandle, out childRect))
                {
                    return true;
                }

                Rectangle childBounds = Rectangle.Intersect(childRect.ToRectangle(), taskbarBounds);
                if (childBounds.Width <= 0 || childBounds.Height <= 0)
                {
                    return true;
                }

                string className = GetWindowClassName(childHandle);
                if (IsProtectedTaskbarRegionWindowClass(className))
                {
                    bounds.Add(childBounds);
                    return true;
                }

                if (IsCustomTaskListPlaceholderWindow(childHandle, taskbarHandle, className, usesCustomTaskListHeuristic))
                {
                    return true;
                }

                if (HasVisibleIntersectingChildWindow(childHandle, taskbarBounds))
                {
                    return true;
                }

                bounds.Add(childBounds);
                return true;
            }, IntPtr.Zero);

            return bounds.ToArray();
        }

        private Rectangle[] GetAugmentedTaskbarOccupiedBounds(
            Rectangle taskbarBounds,
            AppBarEdge edge,
            Rectangle[] structuralBounds)
        {
            Color taskbarBackgroundColor;
            if (!this.TrySampleTaskbarBackgroundColor(taskbarBounds, structuralBounds, out taskbarBackgroundColor))
            {
                return structuralBounds;
            }

            Rectangle[] visualBounds;
            if (!this.TryDetectTaskbarVisualOccupiedBounds(
                taskbarBounds,
                edge,
                structuralBounds,
                taskbarBackgroundColor,
                out visualBounds) ||
                visualBounds.Length <= 0)
            {
                return structuralBounds;
            }

            List<Rectangle> augmentedBounds = new List<Rectangle>(structuralBounds);
            augmentedBounds.AddRange(visualBounds);
            return augmentedBounds.ToArray();
        }

        private bool TryDetectTaskbarVisualOccupiedBounds(
            Rectangle taskbarBounds,
            AppBarEdge edge,
            Rectangle[] structuralBounds,
            Color taskbarBackgroundColor,
            out Rectangle[] visualBounds)
        {
            visualBounds = Array.Empty<Rectangle>();
            Rectangle sampleBounds = Rectangle.Intersect(taskbarBounds, Screen.PrimaryScreen.Bounds);
            if (sampleBounds.Width <= 0 || sampleBounds.Height <= 0)
            {
                return false;
            }

            try
            {
                using (Bitmap bitmap = new Bitmap(sampleBounds.Width, sampleBounds.Height, PixelFormat.Format32bppArgb))
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(sampleBounds.Location, Point.Empty, sampleBounds.Size);
                    visualBounds = DetectTaskbarVisualOccupiedBoundsFromBitmap(
                        bitmap,
                        sampleBounds.Location,
                        edge,
                        structuralBounds,
                        this.Visible ? this.GetCurrentPopupScreenBounds() : Rectangle.Empty,
                        this.ScaleValue(TaskbarOccupiedSafetyPadding),
                        taskbarBackgroundColor);
                    return true;
                }
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce(
                    "taskbar-visual-occupied-detection-failed",
                    "Die visuelle Erkennung belegter Taskleistenbereiche konnte nicht ausgefuehrt werden.",
                    ex);
                return false;
            }
        }

        private static Rectangle[] DetectTaskbarVisualOccupiedBoundsFromBitmap(
            Bitmap bitmap,
            Point screenOrigin,
            AppBarEdge edge,
            Rectangle[] structuralBounds,
            Rectangle ownWindowBounds,
            int safetyPadding,
            Color taskbarBackgroundColor)
        {
            bool isVertical = edge == AppBarEdge.Left || edge == AppBarEdge.Right;
            int mainLength = isVertical ? bitmap.Height : bitmap.Width;
            int crossLength = isVertical ? bitmap.Width : bitmap.Height;
            int crossInset = Math.Max(2, crossLength / 7);
            int crossStart = Math.Min(crossLength - 1, crossInset);
            int crossEnd = Math.Max(crossStart, crossLength - crossInset - 1);
            int requiredHits = Math.Max(3, (crossEnd - crossStart + 1) / 7);
            int allowedGap = Math.Max(2, safetyPadding / 2);
            int minimumVisualRun = Math.Max(8, safetyPadding * 2);
            List<Rectangle> detectedBounds = new List<Rectangle>();
            int runStart = -1;
            int lastHit = -1;

            for (int main = 0; main < mainLength; main++)
            {
                int hitCount = 0;
                for (int cross = crossStart; cross <= crossEnd; cross++)
                {
                    int localX = isVertical ? cross : main;
                    int localY = isVertical ? main : cross;
                    Point screenPoint = new Point(screenOrigin.X + localX, screenOrigin.Y + localY);
                    if ((!ownWindowBounds.IsEmpty && IsPointInPaddedRectangle(ownWindowBounds, screenPoint, safetyPadding)) ||
                        IsPointInAnyPaddedRectangle(structuralBounds, screenPoint, safetyPadding))
                    {
                        continue;
                    }

                    if (IsTaskbarForegroundPixel(bitmap.GetPixel(localX, localY), taskbarBackgroundColor))
                    {
                        hitCount++;
                    }
                }

                if (hitCount >= requiredHits)
                {
                    if (runStart < 0)
                    {
                        runStart = main;
                    }

                    lastHit = main;
                    continue;
                }

                if (runStart >= 0 && main - lastHit > allowedGap)
                {
                    AddDetectedTaskbarVisualRun(
                        detectedBounds,
                        screenOrigin,
                        isVertical,
                        runStart,
                        lastHit,
                        crossLength,
                        safetyPadding,
                        minimumVisualRun);
                    runStart = -1;
                    lastHit = -1;
                }
            }

            if (runStart >= 0)
            {
                AddDetectedTaskbarVisualRun(
                    detectedBounds,
                    screenOrigin,
                    isVertical,
                    runStart,
                    lastHit,
                    crossLength,
                    safetyPadding,
                    minimumVisualRun);
            }

            return detectedBounds.ToArray();
        }

        private static void AddDetectedTaskbarVisualRun(
            List<Rectangle> detectedBounds,
            Point screenOrigin,
            bool isVertical,
            int runStart,
            int runEnd,
            int crossLength,
            int safetyPadding,
            int minimumVisualRun)
        {
            if (runEnd < runStart || runEnd - runStart + 1 < minimumVisualRun)
            {
                return;
            }

            int start = Math.Max(0, runStart - safetyPadding);
            int length = Math.Max(1, runEnd - runStart + 1 + (2 * safetyPadding));
            detectedBounds.Add(isVertical
                ? new Rectangle(screenOrigin.X, screenOrigin.Y + start, crossLength, length)
                : new Rectangle(screenOrigin.X + start, screenOrigin.Y, length, crossLength));
        }

        private static bool IsTaskbarForegroundPixel(Color pixel, Color taskbarBackgroundColor)
        {
            int colorDistance =
                Math.Abs(pixel.R - taskbarBackgroundColor.R) +
                Math.Abs(pixel.G - taskbarBackgroundColor.G) +
                Math.Abs(pixel.B - taskbarBackgroundColor.B);
            double luminanceDelta = Math.Abs(GetRelativeLuminance(pixel) - GetRelativeLuminance(taskbarBackgroundColor));
            return colorDistance >= 72 || luminanceDelta >= 0.20D;
        }

        private static bool HasVisibleDescendantWindowClass(IntPtr rootHandle, string className)
        {
            bool found = false;
            EnumChildWindows(rootHandle, delegate(IntPtr childHandle, IntPtr lParam)
            {
                if (!IsWindowVisible(childHandle))
                {
                    return true;
                }

                if (string.Equals(GetWindowClassName(childHandle), className, StringComparison.Ordinal))
                {
                    found = true;
                    return false;
                }

                return true;
            }, IntPtr.Zero);

            return found;
        }

        private static string GetWindowClassName(IntPtr handle)
        {
            System.Text.StringBuilder className = new System.Text.StringBuilder(256);
            int length = GetClassName(handle, className, className.Capacity);
            return length > 0 ? className.ToString() : string.Empty;
        }

        private bool IsDesktopShellForegroundWindow()
        {
            IntPtr foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero || foregroundWindow == this.Handle)
            {
                return false;
            }

            string className = GetWindowClassName(foregroundWindow);
            return string.Equals(className, "Progman", StringComparison.Ordinal) ||
                string.Equals(className, "WorkerW", StringComparison.Ordinal);
        }

        private bool IsTaskbarForegroundWindow()
        {
            IntPtr foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero || foregroundWindow == this.Handle)
            {
                return false;
            }

            IntPtr taskbarHandle = this.activeTaskbarSnapshot != null
                ? this.activeTaskbarSnapshot.TaskbarHandle
                : IntPtr.Zero;
            if (taskbarHandle == IntPtr.Zero)
            {
                Rectangle targetScreenBounds;
                this.TryFindRelevantTaskbarWindow(out taskbarHandle, out targetScreenBounds);
            }

            if (taskbarHandle == IntPtr.Zero)
            {
                return false;
            }

            IntPtr currentHandle = foregroundWindow;
            while (currentHandle != IntPtr.Zero)
            {
                if (currentHandle == taskbarHandle)
                {
                    return true;
                }

                currentHandle = GetParent(currentHandle);
            }

            return false;
        }

        private bool TryHandleFirstTaskbarRefreshAfterDesktop(Rectangle targetBounds, bool activateWindow)
        {
            if (activateWindow ||
                !this.Visible ||
                this.WindowState != FormWindowState.Normal ||
                this.GetCurrentPopupScreenBounds() != targetBounds)
            {
                return false;
            }

            if (!this.IsTaskbarForegroundWindow())
            {
                return false;
            }

            if ((DateTime.UtcNow - this.lastDesktopShellForegroundUtc).TotalMilliseconds > DesktopToTaskbarBlinkSuppressionMs)
            {
                return false;
            }

            this.lastDesktopShellForegroundUtc = DateTime.MinValue;
            this.EnsurePassiveTaskbarPresence();
            return true;
        }

        private void EnsurePassiveTaskbarPresence()
        {
            if (!this.IsHandleCreated || !this.Visible)
            {
                return;
            }

            this.ApplyWindowZOrderMode();
            this.EnsureTaskbarLocalFrontPlacement(false);
            this.RefreshVisualSurface();
        }

        private bool TryRefreshTaskbarPlacementDuringTaskbarFocus()
        {
            if (this.IsOverlayDragInProgress())
            {
                return true;
            }

            TaskbarIntegrationSnapshot snapshot;
            if (!this.TryCaptureTaskbarIntegrationSnapshot(out snapshot))
            {
                return false;
            }

            if (snapshot.IsHidden)
            {
                this.activeTaskbarSnapshot = snapshot;
                this.HideForTaskbarIntegrationCondition();
                this.UpdateTaskbarMonitorState();
                return false;
            }

            this.TrackTaskbarLocalZOrderAnchor(snapshot);
            this.activeTaskbarSnapshot = snapshot;
            if (this.NeedsCurrentDpiLayout())
            {
                this.ApplyDpiLayout(this.currentDpi, false);
            }

            Rectangle placementBounds;
            if (!this.TryGetTaskbarPlacementBoundsWithCompactFallback(snapshot, out placementBounds))
            {
                if (this.TryPreserveRightAnchoredTaskbarPlacement(snapshot, false))
                {
                    this.UpdateTaskbarMonitorState();
                    return true;
                }

                this.HideForTaskbarIntegrationCondition();
                this.ShowNoSpaceMessageIfNeeded(false);
                this.UpdateTaskbarMonitorState();
                return false;
            }

            this.taskbarNoSpaceMessageShown = false;
            this.lastSuccessfulTaskbarPlacementUtc = DateTime.UtcNow;
            this.lastSuccessfulTaskbarPlacementBounds = placementBounds;
            if (this.GetCurrentPopupScreenBounds() == placementBounds)
            {
                return true;
            }

            this.Location = placementBounds.Location;
            this.OnOverlayLocationCommitted();
            this.RefreshVisualSurface();
            return true;
        }

        private bool IsOverlayDragInProgress()
        {
            return this.leftMousePressed && this.dragControl != null;
        }

        private static bool IsTaskbarShellWindowClass(string className)
        {
            return string.Equals(className, "Progman", StringComparison.Ordinal) ||
                string.Equals(className, "WorkerW", StringComparison.Ordinal) ||
                IsTaskbarRootWindowClass(className);
        }

        private static bool IsTaskbarLocalZOrderAnchorWindowClass(string className, bool usesCustomTaskListHeuristic)
        {
            if (IsTaskbarRootWindowClass(className) ||
                IsProtectedTaskbarRegionWindowClass(className))
            {
                return true;
            }

            if (string.Equals(className, "MSTaskListWClass", StringComparison.Ordinal) ||
                string.Equals(className, "MSTaskSwWClass", StringComparison.Ordinal))
            {
                return true;
            }

            return usesCustomTaskListHeuristic &&
                string.Equals(className, "ToolbarWindow32", StringComparison.Ordinal);
        }

        private static bool IsTaskbarRootWindowClass(string className)
        {
            return string.Equals(className, "Shell_TrayWnd", StringComparison.Ordinal) ||
                string.Equals(className, "Shell_SecondaryTrayWnd", StringComparison.Ordinal);
        }

        private static bool IsProtectedTaskbarRegionWindowClass(string className)
        {
            return string.Equals(className, "TrayNotifyWnd", StringComparison.Ordinal) ||
                string.Equals(className, "TrayClockWClass", StringComparison.Ordinal) ||
                string.Equals(className, "TrayShowDesktopButtonWClass", StringComparison.Ordinal) ||
                string.Equals(className, "SIBTrayButton", StringComparison.Ordinal) ||
                string.Equals(className, "Start", StringComparison.Ordinal);
        }

        private static bool IsCustomTaskListPlaceholderWindow(
            IntPtr windowHandle,
            IntPtr taskbarHandle,
            string className,
            bool usesCustomTaskListHeuristic)
        {
            if (!usesCustomTaskListHeuristic)
            {
                return false;
            }

            if (string.Equals(className, "MSTaskListWClass", StringComparison.Ordinal) ||
                string.Equals(className, "MSTaskSwWClass", StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.Equals(className, "ToolbarWindow32", StringComparison.Ordinal))
            {
                return false;
            }

            return HasAncestorWindowClass(windowHandle, taskbarHandle, "ReBarWindow32");
        }

        private static bool HasAncestorWindowClass(IntPtr windowHandle, IntPtr stopHandle, string className)
        {
            IntPtr currentHandle = GetParent(windowHandle);
            while (currentHandle != IntPtr.Zero)
            {
                if (currentHandle == stopHandle)
                {
                    return false;
                }

                if (string.Equals(GetWindowClassName(currentHandle), className, StringComparison.Ordinal))
                {
                    return true;
                }

                currentHandle = GetParent(currentHandle);
            }

            return false;
        }

        private static bool HasVisibleIntersectingChildWindow(IntPtr parentHandle, Rectangle taskbarBounds)
        {
            bool hasVisibleChild = false;
            EnumChildWindows(parentHandle, delegate(IntPtr childHandle, IntPtr lParam)
            {
                if (!IsWindowVisible(childHandle))
                {
                    return true;
                }

                NativeRect childRect;
                if (!GetWindowRect(childHandle, out childRect))
                {
                    return true;
                }

                Rectangle childBounds = Rectangle.Intersect(childRect.ToRectangle(), taskbarBounds);
                if (childBounds.Width <= 0 || childBounds.Height <= 0)
                {
                    return true;
                }

                hasVisibleChild = true;
                return false;
            }, IntPtr.Zero);
            return hasVisibleChild;
        }

        private bool TryGetTaskbarPlacementBounds(TaskbarIntegrationSnapshot snapshot, out Rectangle placementBounds)
        {
            return this.TryGetTaskbarPlacementBoundsForSize(snapshot, this.ClientSize, out placementBounds);
        }

        private bool TryGetTaskbarPlacementBoundsWithCompactFallback(TaskbarIntegrationSnapshot snapshot, out Rectangle placementBounds)
        {
            placementBounds = Rectangle.Empty;
            if (snapshot == null)
            {
                return false;
            }

            if (this.taskbarIntegrationForceRightOnlySection)
            {
                Size regularSize = this.GetScaledClientSizeForSection(this.GetConfiguredPopupSectionMode());
                if (this.ShouldHoldCompactTaskbarSectionNearProtectedEdge(snapshot, regularSize))
                {
                    Size heldCompactSize = this.GetScaledClientSizeForSection(PopupSectionMode.RightOnly);
                    return this.TryGetTaskbarPlacementBoundsForSize(snapshot, heldCompactSize, out placementBounds);
                }

                Size restoreProbeSize = regularSize;
                int restoreHysteresis = this.ScaleValue(TaskbarCompactRestoreHysteresis);
                if (snapshot.IsVertical)
                {
                    restoreProbeSize.Height += restoreHysteresis;
                }
                else
                {
                    restoreProbeSize.Width += restoreHysteresis;
                }

                Rectangle restoreProbeBounds;
                if (this.TryGetTaskbarPlacementBoundsForSize(snapshot, restoreProbeSize, out restoreProbeBounds) &&
                    this.TryGetTaskbarPlacementBoundsForSize(snapshot, regularSize, out placementBounds))
                {
                    this.SetTaskbarIntegrationForcedRightOnly(false);
                    return true;
                }
            }

            if (this.TryGetTaskbarPlacementBounds(snapshot, out placementBounds))
            {
                return true;
            }

            if (this.GetEffectivePopupSectionMode() == PopupSectionMode.RightOnly)
            {
                return false;
            }

            Size compactSize = this.GetScaledClientSizeForSection(PopupSectionMode.RightOnly);
            if (!this.TryGetTaskbarPlacementBoundsForSize(snapshot, compactSize, out placementBounds))
            {
                return false;
            }

            // The compact fallback is taskbar-local: it preserves the user's saved section mode.
            this.SetTaskbarIntegrationForcedRightOnly(true);
            return true;
        }

        private bool TryGetTaskbarPlacementBoundsAllowingQuickLaunchOverlap(TaskbarIntegrationSnapshot snapshot, out Rectangle placementBounds)
        {
            placementBounds = Rectangle.Empty;
            if (snapshot == null)
            {
                return false;
            }

            if (this.taskbarIntegrationForceRightOnlySection)
            {
                Size forcedCompactSize = this.GetScaledClientSizeForSection(PopupSectionMode.RightOnly);
                return this.TryGetTaskbarRightAnchoredPlacementBoundsForSizeIgnoringQuickLaunch(snapshot, forcedCompactSize, out placementBounds);
            }

            if (this.TryGetTaskbarRightAnchoredPlacementBoundsForSizeIgnoringQuickLaunch(snapshot, this.ClientSize, out placementBounds))
            {
                return true;
            }

            if (this.GetEffectivePopupSectionMode() == PopupSectionMode.RightOnly)
            {
                return false;
            }

            Size compactSize = this.GetScaledClientSizeForSection(PopupSectionMode.RightOnly);
            if (!this.TryGetTaskbarRightAnchoredPlacementBoundsForSizeIgnoringQuickLaunch(snapshot, compactSize, out placementBounds))
            {
                return false;
            }

            this.SetTaskbarIntegrationForcedRightOnly(true);
            return true;
        }

        private bool TryGetTaskbarPlacementBoundsForSize(TaskbarIntegrationSnapshot snapshot, Size popupSize, out Rectangle placementBounds)
        {
            placementBounds = Rectangle.Empty;
            if (snapshot == null)
            {
                return false;
            }

            Rectangle usableBounds = Rectangle.Inflate(snapshot.Bounds, -this.ScaleValue(TaskbarInsetThickness), -this.ScaleValue(TaskbarInsetThickness));
            if (usableBounds.Width <= 0 || usableBounds.Height <= 0)
            {
                return false;
            }

            int popupWidth = popupSize.Width;
            int popupHeight = popupSize.Height;
            if (popupWidth <= 0 || popupHeight <= 0)
            {
                return false;
            }

            // Horizontal taskbars stay strictly right-anchored against the tray edge.
            // Left-side occupancy (for example quick-launch style buttons) is ignored on purpose.
            if (!snapshot.IsVertical)
            {
                return this.TryGetTaskbarRightAnchoredPlacementBoundsForSizeIgnoringQuickLaunch(snapshot, popupSize, out placementBounds);
            }

            int placementMargin = this.ScaleValue(TaskbarPlacementMargin);

            List<Rectangle> freeBands = this.GetFreeTaskbarBands(snapshot, usableBounds);
            if (this.taskbarIntegrationPreferredLocation.HasValue)
            {
                return this.TryGetTaskbarPlacementBoundsFromPreferredLocation(
                    snapshot,
                    freeBands,
                    popupWidth,
                    popupHeight,
                    placementMargin,
                    usableBounds,
                    this.taskbarIntegrationPreferredLocation.Value,
                    out placementBounds);
            }

            Rectangle? bestBand = null;

            for (int i = 0; i < freeBands.Count; i++)
            {
                Rectangle band = freeBands[i];
                bool fits = snapshot.IsVertical
                    ? band.Height >= popupHeight
                    : band.Width >= popupWidth;
                if (!fits)
                {
                    continue;
                }

                if (!bestBand.HasValue)
                {
                    bestBand = band;
                    continue;
                }

                if (snapshot.IsVertical)
                {
                    if (band.Bottom > bestBand.Value.Bottom)
                    {
                        bestBand = band;
                    }
                }
                else if (band.Right > bestBand.Value.Right)
                {
                    bestBand = band;
                }
            }

            if (!bestBand.HasValue)
            {
                return false;
            }

            Rectangle selectedBand = bestBand.Value;
            if (snapshot.IsVertical)
            {
                placementBounds = new Rectangle(
                    selectedBand.Left + Math.Max(0, (selectedBand.Width - popupWidth) / 2),
                    selectedBand.Bottom - popupHeight - placementMargin,
                    popupWidth,
                    popupHeight);
            }
            else
            {
                placementBounds = new Rectangle(
                    selectedBand.Right - popupWidth - placementMargin,
                    selectedBand.Top + Math.Max(0, (selectedBand.Height - popupHeight) / 2),
                    popupWidth,
                    popupHeight);
            }

            return usableBounds.Contains(placementBounds);
        }

        private bool TryGetTaskbarRightAnchoredPlacementBoundsForSizeIgnoringQuickLaunch(
            TaskbarIntegrationSnapshot snapshot,
            Size popupSize,
            out Rectangle placementBounds)
        {
            placementBounds = Rectangle.Empty;
            if (snapshot == null)
            {
                return false;
            }

            Rectangle usableBounds = Rectangle.Inflate(snapshot.Bounds, -this.ScaleValue(TaskbarInsetThickness), -this.ScaleValue(TaskbarInsetThickness));
            if (usableBounds.Width <= 0 || usableBounds.Height <= 0)
            {
                return false;
            }

            int popupWidth = popupSize.Width;
            int popupHeight = popupSize.Height;
            if (popupWidth <= 0 || popupHeight <= 0)
            {
                return false;
            }

            int placementMargin = this.ScaleValue(TaskbarPlacementMargin);
            Rectangle protectedEdgeBounds;
            this.TryGetTaskbarProtectedEdgeBounds(snapshot, usableBounds, out protectedEdgeBounds);

            if (snapshot.IsVertical)
            {
                int bottomLimit = protectedEdgeBounds.IsEmpty
                    ? usableBounds.Bottom
                    : Math.Min(usableBounds.Bottom, protectedEdgeBounds.Top);
                int placementY = bottomLimit - popupHeight - placementMargin;
                if (placementY < usableBounds.Top + placementMargin)
                {
                    return false;
                }

                placementBounds = new Rectangle(
                    usableBounds.Left + Math.Max(0, (usableBounds.Width - popupWidth) / 2),
                    placementY,
                    popupWidth,
                    popupHeight);
            }
            else
            {
                int rightLimit = protectedEdgeBounds.IsEmpty
                    ? usableBounds.Right
                    : Math.Min(usableBounds.Right, protectedEdgeBounds.Left);
                int placementX = rightLimit - popupWidth - placementMargin;
                if (placementX < usableBounds.Left + placementMargin)
                {
                    return false;
                }

                placementBounds = new Rectangle(
                    placementX,
                    usableBounds.Top + Math.Max(0, (usableBounds.Height - popupHeight) / 2),
                    popupWidth,
                    popupHeight);
            }

            return usableBounds.Contains(placementBounds);
        }

        private bool ShouldHoldCompactTaskbarSectionNearProtectedEdge(
            TaskbarIntegrationSnapshot snapshot,
            Size regularSize)
        {
            if (snapshot == null ||
                regularSize.Width <= 0 ||
                regularSize.Height <= 0)
            {
                return false;
            }

            Rectangle usableBounds = Rectangle.Inflate(snapshot.Bounds, -this.ScaleValue(TaskbarInsetThickness), -this.ScaleValue(TaskbarInsetThickness));
            if (usableBounds.Width <= 0 || usableBounds.Height <= 0)
            {
                return false;
            }

            Rectangle protectedEdgeBounds;
            if (!this.TryGetTaskbarProtectedEdgeBounds(snapshot, usableBounds, out protectedEdgeBounds) ||
                protectedEdgeBounds.IsEmpty)
            {
                return false;
            }

            int placementMargin = this.ScaleValue(TaskbarPlacementMargin);
            int restoreHysteresis = this.ScaleValue(TaskbarCompactRestoreHysteresis);

            Rectangle anchorBounds = Rectangle.Empty;
            if (this.taskbarIntegrationPreferredLocation.HasValue)
            {
                anchorBounds = new Rectangle(this.taskbarIntegrationPreferredLocation.Value, regularSize);
            }
            else if (this.lastSuccessfulTaskbarPlacementBounds.Width > 0 &&
                this.lastSuccessfulTaskbarPlacementBounds.Height > 0)
            {
                anchorBounds = this.lastSuccessfulTaskbarPlacementBounds;
            }
            else
            {
                anchorBounds = this.GetCurrentPopupScreenBounds();
            }

            if (anchorBounds.Width <= 0 || anchorBounds.Height <= 0)
            {
                return false;
            }

            if (snapshot.IsVertical)
            {
                int anchorBottom = anchorBounds.Bottom + placementMargin;
                return anchorBottom >= protectedEdgeBounds.Top - restoreHysteresis;
            }

            int anchorRight = anchorBounds.Right + placementMargin;
            return anchorRight >= protectedEdgeBounds.Left - restoreHysteresis;
        }

        private bool TryGetTaskbarProtectedEdgeBounds(
            TaskbarIntegrationSnapshot snapshot,
            Rectangle usableBounds,
            out Rectangle protectedEdgeBounds)
        {
            protectedEdgeBounds = Rectangle.Empty;
            if (snapshot == null || snapshot.OccupiedBounds == null || snapshot.OccupiedBounds.Length <= 0)
            {
                return false;
            }

            int probeDistance = this.ScaleValue(TaskbarProtectedEdgeProbe);
            bool hasProtectedBounds = false;
            for (int i = 0; i < snapshot.OccupiedBounds.Length; i++)
            {
                Rectangle occupied = Rectangle.Intersect(snapshot.OccupiedBounds[i], usableBounds);
                if (occupied.Width <= 0 || occupied.Height <= 0)
                {
                    continue;
                }

                bool nearProtectedEdge = snapshot.IsVertical
                    ? occupied.Bottom >= usableBounds.Bottom - probeDistance
                    : occupied.Right >= usableBounds.Right - probeDistance;
                if (!nearProtectedEdge)
                {
                    continue;
                }

                protectedEdgeBounds = hasProtectedBounds
                    ? Rectangle.Union(protectedEdgeBounds, occupied)
                    : occupied;
                hasProtectedBounds = true;
            }

            return hasProtectedBounds;
        }

        private bool TryPreserveRightAnchoredTaskbarPlacement(TaskbarIntegrationSnapshot snapshot, bool activateWindow)
        {
            if (snapshot == null ||
                this.lastSuccessfulTaskbarPlacementBounds.Width <= 0 ||
                this.lastSuccessfulTaskbarPlacementBounds.Height <= 0)
            {
                return false;
            }

            Rectangle usableBounds = Rectangle.Inflate(snapshot.Bounds, -this.ScaleValue(TaskbarInsetThickness), -this.ScaleValue(TaskbarInsetThickness));
            if (!usableBounds.Contains(this.lastSuccessfulTaskbarPlacementBounds))
            {
                return false;
            }

            Rectangle protectedEdgeBounds;
            if (this.TryGetTaskbarProtectedEdgeBounds(snapshot, usableBounds, out protectedEdgeBounds) &&
                !protectedEdgeBounds.IsEmpty)
            {
                bool intrudesProtectedEdge = snapshot.IsVertical
                    ? this.lastSuccessfulTaskbarPlacementBounds.Bottom > protectedEdgeBounds.Top
                    : this.lastSuccessfulTaskbarPlacementBounds.Right > protectedEdgeBounds.Left;
                if (intrudesProtectedEdge)
                {
                    return false;
                }
            }

            this.ShowAtTaskbarPlacement(this.lastSuccessfulTaskbarPlacementBounds, activateWindow);
            return true;
        }

        private bool TryGetTaskbarPlacementBoundsFromPreferredLocation(
            TaskbarIntegrationSnapshot snapshot,
            List<Rectangle> freeBands,
            int popupWidth,
            int popupHeight,
            int placementMargin,
            Rectangle usableBounds,
            Point preferredLocation,
            out Rectangle placementBounds)
        {
            placementBounds = Rectangle.Empty;
            Rectangle? bestPlacement = null;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < freeBands.Count; i++)
            {
                Rectangle band = freeBands[i];
                int candidateX;
                int candidateY;

                if (snapshot.IsVertical)
                {
                    int minimumY = band.Top + placementMargin;
                    int maximumY = band.Bottom - popupHeight - placementMargin;
                    if (maximumY < minimumY || band.Width < popupWidth)
                    {
                        continue;
                    }

                    candidateY = Clamp(preferredLocation.Y, minimumY, maximumY);
                    candidateX = band.Left + Math.Max(0, (band.Width - popupWidth) / 2);
                }
                else
                {
                    int minimumX = band.Left + placementMargin;
                    int maximumX = band.Right - popupWidth - placementMargin;
                    if (maximumX < minimumX || band.Height < popupHeight)
                    {
                        continue;
                    }

                    candidateX = Clamp(preferredLocation.X, minimumX, maximumX);
                    candidateY = band.Top + Math.Max(0, (band.Height - popupHeight) / 2);
                }

                Rectangle candidatePlacement = new Rectangle(candidateX, candidateY, popupWidth, popupHeight);
                if (!usableBounds.Contains(candidatePlacement))
                {
                    continue;
                }

                int distance = snapshot.IsVertical
                    ? Math.Abs(preferredLocation.Y - candidateY)
                    : Math.Abs(preferredLocation.X - candidateX);
                bool preferCandidate = !bestPlacement.HasValue || distance < bestDistance;
                if (!preferCandidate && bestPlacement.HasValue && distance == bestDistance)
                {
                    // Keep the taskbar behavior biased toward the later free band
                    // (rightmost on horizontal taskbars, lowermost on vertical ones)
                    // instead of snapping left just because bands are iterated left-to-right.
                    preferCandidate = snapshot.IsVertical
                        ? candidatePlacement.Bottom > bestPlacement.Value.Bottom
                        : candidatePlacement.Right > bestPlacement.Value.Right;
                }

                if (preferCandidate)
                {
                    bestPlacement = candidatePlacement;
                    bestDistance = distance;
                }
            }

            if (!bestPlacement.HasValue)
            {
                return false;
            }

            placementBounds = bestPlacement.Value;
            return true;
        }

        private List<Rectangle> GetFreeTaskbarBands(TaskbarIntegrationSnapshot snapshot, Rectangle usableBounds)
        {
            List<Tuple<int, int>> occupiedIntervals = new List<Tuple<int, int>>();
            int occupiedSafetyPadding = this.ScaleValue(TaskbarOccupiedSafetyPadding);
            for (int i = 0; i < snapshot.OccupiedBounds.Length; i++)
            {
                Rectangle paddedOccupied = snapshot.OccupiedBounds[i];
                paddedOccupied.Inflate(occupiedSafetyPadding, occupiedSafetyPadding);
                Rectangle occupied = Rectangle.Intersect(paddedOccupied, usableBounds);
                if (occupied.Width <= 0 || occupied.Height <= 0)
                {
                    continue;
                }

                int start = snapshot.IsVertical ? occupied.Top : occupied.Left;
                int end = snapshot.IsVertical ? occupied.Bottom : occupied.Right;
                occupiedIntervals.Add(Tuple.Create(start, end));
            }

            occupiedIntervals.Sort(delegate(Tuple<int, int> a, Tuple<int, int> b)
            {
                return a.Item1.CompareTo(b.Item1);
            });

            List<Rectangle> freeBands = new List<Rectangle>();
            int cursor = snapshot.IsVertical ? usableBounds.Top : usableBounds.Left;
            int maximum = snapshot.IsVertical ? usableBounds.Bottom : usableBounds.Right;

            for (int i = 0; i < occupiedIntervals.Count; i++)
            {
                int intervalStart = Math.Max(cursor, occupiedIntervals[i].Item1);
                int intervalEnd = Math.Min(maximum, occupiedIntervals[i].Item2);
                if (intervalStart > cursor)
                {
                    freeBands.Add(CreateBandRectangle(snapshot, usableBounds, cursor, intervalStart));
                }

                if (intervalEnd > cursor)
                {
                    cursor = intervalEnd;
                }
            }

            if (cursor < maximum)
            {
                freeBands.Add(CreateBandRectangle(snapshot, usableBounds, cursor, maximum));
            }

            return freeBands;
        }

        private static Rectangle CreateBandRectangle(TaskbarIntegrationSnapshot snapshot, Rectangle usableBounds, int start, int end)
        {
            if (snapshot.IsVertical)
            {
                return new Rectangle(
                    usableBounds.Left,
                    start,
                    usableBounds.Width,
                    Math.Max(0, end - start));
            }

            return new Rectangle(
                start,
                usableBounds.Top,
                Math.Max(0, end - start),
                usableBounds.Height);
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum)
            {
                return minimum;
            }

            if (value > maximum)
            {
                return maximum;
            }

            return value;
        }

        private bool ShouldYieldToFullscreenForegroundWindow(Rectangle screenBounds)
        {
            IntPtr foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero || foregroundWindow == this.Handle)
            {
                return false;
            }

            string foregroundClassName = GetWindowClassName(foregroundWindow);
            if (IsTaskbarShellWindowClass(foregroundClassName))
            {
                return false;
            }

            NativeRect foregroundRect;
            if (!GetWindowRect(foregroundWindow, out foregroundRect))
            {
                return false;
            }

            Rectangle visibleForeground = Rectangle.Intersect(foregroundRect.ToRectangle(), screenBounds);
            if (visibleForeground.Width <= 0 || visibleForeground.Height <= 0)
            {
                return false;
            }

            int widthTolerance = Math.Max(2, this.ScaleValue(2));
            int heightTolerance = Math.Max(2, this.ScaleValue(2));
            return visibleForeground.Width >= screenBounds.Width - widthTolerance &&
                visibleForeground.Height >= screenBounds.Height - heightTolerance;
        }

        private TaskbarVisualTheme CreateTaskbarVisualTheme(Rectangle taskbarBounds, Rectangle[] occupiedBounds)
        {
            Color sampledTaskbarColor;
            if (this.TrySampleTaskbarBackgroundColor(taskbarBounds, occupiedBounds, out sampledTaskbarColor))
            {
                return CreateTaskbarVisualThemeFromColor(sampledTaskbarColor, TaskbarIntegratedPanelTintAlpha);
            }

            return this.CreateFallbackTaskbarVisualTheme();
        }

        private bool TrySampleTaskbarBackgroundColor(
            Rectangle taskbarBounds,
            Rectangle[] occupiedBounds,
            out Color sampledTaskbarColor)
        {
            sampledTaskbarColor = Color.Empty;

            Rectangle sampleBounds = Rectangle.Intersect(taskbarBounds, Screen.PrimaryScreen.Bounds);
            if (sampleBounds.Width <= 0 || sampleBounds.Height <= 0)
            {
                return false;
            }

            try
            {
                using (Bitmap bitmap = new Bitmap(sampleBounds.Width, sampleBounds.Height, PixelFormat.Format32bppArgb))
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(sampleBounds.Location, Point.Empty, sampleBounds.Size);

                    return TryGetSampledTaskbarColorFromBitmap(
                        bitmap,
                        sampleBounds.Location,
                        occupiedBounds,
                        this.Visible ? this.GetCurrentPopupScreenBounds() : Rectangle.Empty,
                        this.ScaleValue(TaskbarPlacementMargin),
                        out sampledTaskbarColor);
                }
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce(
                    "taskbar-background-sampling-failed",
                    "Die Taskleistenfarbe konnte nicht direkt abgetastet werden. DWM-Akzentfarbe wird als stabile Naeherung verwendet.",
                    ex);
                return false;
            }
        }

        private static bool TryGetSampledTaskbarColorFromBitmap(
            Bitmap bitmap,
            Point screenOrigin,
            Rectangle[] occupiedBounds,
            Rectangle ownWindowBounds,
            int exclusionPadding,
            out Color sampledTaskbarColor)
        {
            sampledTaskbarColor = Color.Empty;

            List<int> redSamples = new List<int>();
            List<int> greenSamples = new List<int>();
            List<int> blueSamples = new List<int>();
            int stepX = Math.Max(1, bitmap.Width / 96);
            int stepY = Math.Max(1, bitmap.Height / 24);

            CollectTaskbarColorSamples(
                bitmap,
                screenOrigin,
                occupiedBounds,
                ownWindowBounds,
                exclusionPadding,
                true,
                redSamples,
                greenSamples,
                blueSamples,
                stepX,
                stepY);

            if (redSamples.Count < 12)
            {
                CollectTaskbarColorSamples(
                    bitmap,
                    screenOrigin,
                    occupiedBounds,
                    ownWindowBounds,
                    exclusionPadding,
                    false,
                    redSamples,
                    greenSamples,
                    blueSamples,
                    stepX,
                    stepY);
            }

            if (redSamples.Count <= 0)
            {
                return false;
            }

            redSamples.Sort();
            greenSamples.Sort();
            blueSamples.Sort();
            int medianIndex = redSamples.Count / 2;
            sampledTaskbarColor = Color.FromArgb(
                255,
                redSamples[medianIndex],
                greenSamples[medianIndex],
                blueSamples[medianIndex]);
            return true;
        }

        private static void CollectTaskbarColorSamples(
            Bitmap bitmap,
            Point screenOrigin,
            Rectangle[] occupiedBounds,
            Rectangle ownWindowBounds,
            int exclusionPadding,
            bool excludeOccupiedAreas,
            List<int> redSamples,
            List<int> greenSamples,
            List<int> blueSamples,
            int stepX,
            int stepY)
        {
            for (int y = stepY / 2; y < bitmap.Height; y += stepY)
            {
                for (int x = stepX / 2; x < bitmap.Width; x += stepX)
                {
                    Point screenPoint = new Point(screenOrigin.X + x, screenOrigin.Y + y);
                    if (!ownWindowBounds.IsEmpty && IsPointInPaddedRectangle(ownWindowBounds, screenPoint, exclusionPadding))
                    {
                        continue;
                    }

                    if (excludeOccupiedAreas &&
                        IsPointInAnyPaddedRectangle(occupiedBounds, screenPoint, exclusionPadding))
                    {
                        continue;
                    }

                    Color pixel = bitmap.GetPixel(x, y);
                    if (pixel.A <= 0)
                    {
                        continue;
                    }

                    redSamples.Add(pixel.R);
                    greenSamples.Add(pixel.G);
                    blueSamples.Add(pixel.B);
                }
            }
        }

        private static bool IsPointInAnyPaddedRectangle(Rectangle[] bounds, Point point, int padding)
        {
            if (bounds == null)
            {
                return false;
            }

            for (int i = 0; i < bounds.Length; i++)
            {
                if (IsPointInPaddedRectangle(bounds[i], point, padding))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPointInPaddedRectangle(Rectangle bounds, Point point, int padding)
        {
            if (bounds.IsEmpty)
            {
                return false;
            }

            Rectangle paddedBounds = bounds;
            paddedBounds.Inflate(Math.Max(0, padding), Math.Max(0, padding));
            return paddedBounds.Contains(point);
        }

        private static TaskbarVisualTheme CreateTaskbarVisualThemeFromColor(Color taskbarColor, byte overlayAlpha)
        {
            Color normalizedTaskbarColor = Color.FromArgb(255, taskbarColor.R, taskbarColor.G, taskbarColor.B);
            double luminance = GetRelativeLuminance(normalizedTaskbarColor);
            Color edgeReferenceColor = luminance < 0.42D
                ? Color.FromArgb(126, 164, 210)
                : Color.FromArgb(20, 32, 58);

            return new TaskbarVisualTheme
            {
                TaskbarColor = normalizedTaskbarColor,
                BaseColor = GetInterpolatedColor(normalizedTaskbarColor, BackgroundBlue, 0.12D),
                BorderColor = GetInterpolatedColor(normalizedTaskbarColor, edgeReferenceColor, 0.24D),
                DividerColor = GetInterpolatedColor(normalizedTaskbarColor, edgeReferenceColor, 0.18D),
                OverlayAlpha = overlayAlpha
            };
        }

        private TaskbarVisualTheme CreateFallbackTaskbarVisualTheme()
        {
            uint colorizationColor;
            bool opaqueBlend;
            Color accentColor = BackgroundBlue;
            if (DwmGetColorizationColor(out colorizationColor, out opaqueBlend) == 0)
            {
                accentColor = Color.FromArgb(
                    255,
                    (byte)((colorizationColor >> 16) & 0xFF),
                    (byte)((colorizationColor >> 8) & 0xFF),
                    (byte)(colorizationColor & 0xFF));
            }

            return new TaskbarVisualTheme
            {
                TaskbarColor = accentColor,
                BaseColor = GetInterpolatedColor(BackgroundBlue, accentColor, 0.34D),
                BorderColor = GetInterpolatedColor(BorderColor, accentColor, 0.24D),
                DividerColor = GetInterpolatedColor(DividerColor, accentColor, 0.22D),
                OverlayAlpha = TaskbarIntegratedPanelTintAlpha
            };
        }

        private static double GetRelativeLuminance(Color color)
        {
            return ((color.R * 0.2126D) + (color.G * 0.7152D) + (color.B * 0.0722D)) / 255D;
        }

        private void TryActivatePopupWindow()
        {
            if (!this.ShouldUseGlobalTopMost())
            {
                return;
            }

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
                this.OnOverlayLocationCommitted();
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

        private Point GetVisiblePopupLocationForManualDrag(Point preferredLocation, Point anchorPoint)
        {
            bool snappedToTaskbar;
            TaskbarIntegrationSnapshot snapshot;
            if (this.TryCaptureTaskbarIntegrationSnapshot(out snapshot))
            {
                return this.GetVisiblePopupLocationForManualDrag(
                    preferredLocation,
                    anchorPoint,
                    snapshot,
                    out snappedToTaskbar);
            }

            Rectangle screenBounds = Screen.FromPoint(anchorPoint).Bounds;
            return ClampLocationToWorkingArea(preferredLocation, this.Size, screenBounds);
        }

        private Point GetVisiblePopupLocationForManualDrag(
            Point preferredLocation,
            Point anchorPoint,
            TaskbarIntegrationSnapshot snapshot,
            out bool snappedToTaskbar)
        {
            snappedToTaskbar = false;
            Rectangle screenBounds = Screen.FromPoint(anchorPoint).Bounds;
            Point adjustedLocation = ClampLocationToWorkingArea(preferredLocation, this.Size, screenBounds);

            if (snapshot == null ||
                snapshot.IsHidden ||
                snapshot.ScreenBounds != screenBounds)
            {
                return adjustedLocation;
            }

            Rectangle popupBounds = new Rectangle(adjustedLocation, this.Size);
            bool overlapsTaskbarSpan = snapshot.IsVertical
                ? popupBounds.Bottom > snapshot.Bounds.Top && popupBounds.Top < snapshot.Bounds.Bottom
                : popupBounds.Right > snapshot.Bounds.Left && popupBounds.Left < snapshot.Bounds.Right;
            if (!overlapsTaskbarSpan)
            {
                return adjustedLocation;
            }

            int snapDistance = this.ScaleValue(TaskbarDragSnapDistance);
            int breakThroughDepth = this.ScaleValue(TaskbarDragBreakThroughDepth);
            switch (snapshot.Edge)
            {
                case AppBarEdge.Bottom:
                    adjustedLocation.Y = this.ApplyTaskbarDragBarrierAxis(
                        adjustedLocation.Y,
                        popupBounds.Bottom,
                        snapshot.Bounds.Top,
                        this.Height,
                        snapDistance,
                        breakThroughDepth,
                        true,
                        out snappedToTaskbar);
                    break;

                case AppBarEdge.Top:
                    adjustedLocation.Y = this.ApplyTaskbarDragBarrierAxis(
                        adjustedLocation.Y,
                        snapshot.Bounds.Bottom,
                        popupBounds.Top,
                        this.Height,
                        snapDistance,
                        breakThroughDepth,
                        false,
                        out snappedToTaskbar);
                    break;

                case AppBarEdge.Left:
                    adjustedLocation.X = this.ApplyTaskbarDragBarrierAxis(
                        adjustedLocation.X,
                        snapshot.Bounds.Right,
                        popupBounds.Left,
                        this.Width,
                        snapDistance,
                        breakThroughDepth,
                        false,
                        out snappedToTaskbar);
                    break;

                case AppBarEdge.Right:
                    adjustedLocation.X = this.ApplyTaskbarDragBarrierAxis(
                        adjustedLocation.X,
                        popupBounds.Right,
                        snapshot.Bounds.Left,
                        this.Width,
                        snapDistance,
                        breakThroughDepth,
                        true,
                        out snappedToTaskbar);
                    break;
            }

            return adjustedLocation;
        }

        private int ApplyTaskbarDragBarrierAxis(
            int currentLocation,
            int outerPopupEdge,
            int taskbarEdge,
            int popupSize,
            int snapDistance,
            int breakThroughDepth,
            bool snapOutsideBeforeTaskbar,
            out bool snappedToBarrier)
        {
            int barrierLocation = snapOutsideBeforeTaskbar
                ? taskbarEdge - popupSize
                : taskbarEdge;
            int direction = snapOutsideBeforeTaskbar ? 1 : -1;
            int signedOffset = (currentLocation - barrierLocation) * direction;

            if (signedOffset < -snapDistance || signedOffset > breakThroughDepth)
            {
                snappedToBarrier = false;
                return currentLocation;
            }

            if (signedOffset <= 0)
            {
                // Outside the taskbar: keep a short magnetic snap zone directly
                // before the edge so the popup can be aligned flush above/beside
                // the taskbar more easily, then fade back into the softer pull.
                int outsideHoldDistance = Math.Min(snapDistance, this.ScaleValue(TaskbarDesktopSnapHoldDistance));
                if (Math.Abs(signedOffset) <= outsideHoldDistance)
                {
                    snappedToBarrier = false;
                    return barrierLocation;
                }

                int distance = Math.Abs(signedOffset);
                int easedOffset = (int)Math.Round((distance * distance) / (double)Math.Max(1, snapDistance));
                snappedToBarrier = false;
                return barrierLocation - (direction * easedOffset);
            }

            // Inside the taskbar: keep the popup taskbar-locked until the user
            // pushes far enough through the magnetic threshold.
            snappedToBarrier = true;
            int easedInsideOffset = (int)Math.Round((signedOffset * signedOffset) / (double)Math.Max(1, breakThroughDepth));
            return barrierLocation + (direction * easedInsideOffset);
        }

        private bool ShouldAutoIntegrateWithTaskbar(Rectangle popupBounds, TaskbarIntegrationSnapshot snapshot)
        {
            Rectangle overlap = Rectangle.Intersect(popupBounds, snapshot.Bounds);
            if (overlap.Width <= 0 || overlap.Height <= 0)
            {
                return false;
            }

            int overlapDepth = snapshot.IsVertical ? overlap.Width : overlap.Height;
            int taskbarThickness = snapshot.IsVertical ? snapshot.Bounds.Width : snapshot.Bounds.Height;
            int requiredDepth = Math.Max(this.ScaleValue(6), Math.Min(taskbarThickness, this.ScaleValue(10)));
            return overlapDepth >= requiredDepth;
        }

        private Rectangle GetBestWorkingArea(Point preferredLocation, Point? anchorPoint)
        {
            if (anchorPoint.HasValue)
            {
                return Screen.FromPoint(anchorPoint.Value).WorkingArea;
            }

            Point safePreferredLocation = NormalizeRectangleOrigin(preferredLocation, this.Size);
            Rectangle preferredBounds = new Rectangle(safePreferredLocation, this.Size);
            Point preferredCenter = GetRectangleCenter(preferredBounds);
            Rectangle bestWorkingArea = Screen.FromPoint(safePreferredLocation).WorkingArea;
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

        private static Point NormalizeRectangleOrigin(Point location, Size size)
        {
            int safeWidth = Math.Max(0, size.Width);
            int safeHeight = Math.Max(0, size.Height);
            long maxX = (long)int.MaxValue - safeWidth;
            long maxY = (long)int.MaxValue - safeHeight;

            return new Point(
                ClampToInt32(Math.Min(location.X, maxX)),
                ClampToInt32(Math.Min(location.Y, maxY)));
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
            long centerX = (long)bounds.Left + (bounds.Width / 2L);
            long centerY = (long)bounds.Top + (bounds.Height / 2L);
            return new Point(
                ClampToInt32(centerX),
                ClampToInt32(centerY));
        }

        private static long GetDistanceSquaredToRectangle(Rectangle rectangle, Point point)
        {
            long dx = 0L;
            if (point.X < rectangle.Left)
            {
                dx = (long)rectangle.Left - point.X;
            }
            else if (point.X >= rectangle.Right)
            {
                dx = (long)point.X - rectangle.Right;
            }

            long dy = 0L;
            if (point.Y < rectangle.Top)
            {
                dy = (long)rectangle.Top - point.Y;
            }
            else if (point.Y >= rectangle.Bottom)
            {
                dy = (long)point.Y - rectangle.Bottom;
            }

            return (dx * dx) + (dy * dy);
        }

        private static int ClampToInt32(long value)
        {
            if (value < int.MinValue)
            {
                return int.MinValue;
            }

            if (value > int.MaxValue)
            {
                return int.MaxValue;
            }

            return (int)value;
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

            this.AdvanceVisualAnimations();
            this.RefreshVisualSurface();
        }

        private void TopMostGuardTimer_Tick(object sender, EventArgs e)
        {
            if (!this.Visible ||
                this.IsDisposed ||
                this.IsTopMostEnforcementPaused ||
                !this.ShouldUseGlobalTopMost())
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
                this.displayedDownloadBytesPerSecond = 0D;
                this.displayedUploadBytesPerSecond = 0D;
                this.ringDisplayDownloadBytesPerSecond = 0D;
                this.ringDisplayUploadBytesPerSecond = 0D;
                this.peakHoldDownloadBytesPerSecond = 0D;
                this.peakHoldUploadBytesPerSecond = 0D;
                this.peakHoldDownloadCapturedUtc = DateTime.MinValue;
                this.peakHoldUploadCapturedUtc = DateTime.MinValue;
                this.lastAnimationAdvanceUtc = DateTime.MinValue;
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
            long measuredDownloadBytes = 0L;
            long measuredUploadBytes = 0L;

            if (this.lastSampleUtc != DateTime.MinValue)
            {
                long receivedDiff = snapshot.BytesReceived - this.lastReceivedBytes;
                long sentDiff = snapshot.BytesSent - this.lastSentBytes;
                measuredDownloadBytes = Math.Max(0L, receivedDiff);
                measuredUploadBytes = Math.Max(0L, sentDiff);
                double elapsedSeconds = (nowUtc - this.lastSampleUtc).TotalSeconds;
                if (elapsedSeconds > 0.1D)
                {
                    downloadBytesPerSecond = measuredDownloadBytes / elapsedSeconds;
                    uploadBytesPerSecond = measuredUploadBytes / elapsedSeconds;
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
                this.settings.GetDownloadVisualizationPeak(),
                true);
            this.visualUploadPeakBytesPerSecond = this.GetVisualizationPeak(
                uploadBytesPerSecond,
                this.visualUploadPeakBytesPerSecond,
                this.settings.GetUploadVisualizationPeak(),
                false);

            AddDisplaySample(this.recentDownloadSamples, downloadBytesPerSecond);
            AddDisplaySample(this.recentUploadSamples, uploadBytesPerSecond);
            double smoothedDownloadBytesPerSecond = GetSmoothedDisplayRate(this.recentDownloadSamples);
            double smoothedUploadBytesPerSecond = GetSmoothedDisplayRate(this.recentUploadSamples);

            this.displayedDownloadBytesPerSecond = smoothedDownloadBytesPerSecond;
            this.displayedUploadBytesPerSecond = smoothedUploadBytesPerSecond;
            this.UpdatePeakHoldRates(smoothedDownloadBytesPerSecond, smoothedUploadBytesPerSecond, nowUtc);
            this.downloadValueLabel.Text = FormatSpeed(smoothedDownloadBytesPerSecond);
            this.uploadValueLabel.Text = FormatSpeed(smoothedUploadBytesPerSecond);
            this.AddTrafficHistorySample(smoothedDownloadBytesPerSecond, smoothedUploadBytesPerSecond);
            this.UpdateAnimationTimerState();
            if (this.Visible)
            {
                this.RefreshVisualSurface();
            }

            this.OnRatesUpdated(smoothedDownloadBytesPerSecond, smoothedUploadBytesPerSecond);
            this.OnTrafficUsageMeasured(measuredDownloadBytes, measuredUploadBytes);
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

        private bool IsMiniGraphDisplayMode()
        {
            return this.settings != null && this.settings.PopupDisplayMode == PopupDisplayMode.MiniGraph;
        }

        private bool IsMiniSoftDisplayMode()
        {
            return this.settings != null && this.settings.PopupDisplayMode == PopupDisplayMode.MiniSoft;
        }

        private bool IsAlternativeDisplayMode()
        {
            return this.IsMiniGraphDisplayMode() || this.IsMiniSoftDisplayMode();
        }

        private double GetRingDisplayNoiseFloorBytesPerSecond()
        {
            return MiniGraphRingDisplayNoiseFloorBytesPerSecond;
        }

        private int GetCurrentOverlaySparklinePointCount()
        {
            return this.IsAlternativeDisplayMode()
                ? MiniGraphOverlaySparklinePointCount
                : OverlaySparklinePointCount;
        }

        private int GetCurrentRingSegmentCount()
        {
            return this.IsAlternativeDisplayMode()
                ? MiniGraphRingSegmentCount
                : RingSegmentCount;
        }

        private float GetCurrentRingSegmentGapDegrees()
        {
            return this.IsAlternativeDisplayMode()
                ? MiniGraphRingSegmentGapDegrees
                : RingSegmentGapDegrees;
        }

        private double GetMinimumVisualizationPeakBytesPerSecond(bool useDownload)
        {
            return useDownload
                ? MiniGraphDownloadMinimumVisualizationPeakBytesPerSecond
                : MiniGraphUploadMinimumVisualizationPeakBytesPerSecond;
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

        private double UpdateRingDisplayRate(double currentBytesPerSecond, double targetBytesPerSecond)
        {
            double current = Math.Max(0D, currentBytesPerSecond);
            double target = Math.Max(0D, targetBytesPerSecond);
            double noiseFloorBytesPerSecond = this.GetRingDisplayNoiseFloorBytesPerSecond();

            if (current <= noiseFloorBytesPerSecond)
            {
                current = 0D;
            }

            if (target <= noiseFloorBytesPerSecond)
            {
                target = 0D;
            }

            double smoothingFactor = target >= current
                ? RingDisplayRiseSmoothingFactor
                : RingDisplayFallSmoothingFactor;
            double next = current + ((target - current) * smoothingFactor);

            if (Math.Abs(next - target) <= (noiseFloorBytesPerSecond * 0.25D))
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

        private void OnTrafficUsageMeasured(long downloadBytes, long uploadBytes)
        {
            EventHandler<TrafficUsageMeasuredEventArgs> handler = this.TrafficUsageMeasured;
            if (handler != null && (downloadBytes > 0L || uploadBytes > 0L))
            {
                handler(this, new TrafficUsageMeasuredEventArgs(downloadBytes, uploadBytes));
            }
        }

        private double GetVisualizationPeak(
            double currentTrafficBytesPerSecond,
            double previousPeakBytesPerSecond,
            double configuredPeakBytesPerSecond,
            bool useDownload)
        {
            if (configuredPeakBytesPerSecond > 0D)
            {
                return configuredPeakBytesPerSecond;
            }

            double minimumPeak = this.GetMinimumVisualizationPeakBytesPerSecond(useDownload);
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

        private double GetVisualizedFillRatio(double fillRatio, bool useDownload)
        {
            double clamped = Math.Max(0D, Math.Min(1D, fillRatio));

            if (clamped <= 0D || clamped >= 1D)
            {
                return clamped;
            }

            // A gentle gamma curve makes low traffic more visible without
            // overdriving medium and high values too aggressively.
            return Math.Pow(
                clamped,
                useDownload
                    ? MiniGraphDownloadLowTrafficVisualizationExponent
                    : MiniGraphUploadLowTrafficVisualizationExponent);
        }

        private Rectangle GetDownloadMeterBounds()
        {
            PanelSkinDefinition definition = this.GetCurrentPanelSkinDefinition();
            if (definition != null &&
                definition.MeterBounds.HasValue &&
                this.IsBothSectionsVisible())
            {
                return this.ScaleSkinRectangle(definition.MeterBounds.Value);
            }

            bool miniGraphDisplayMode = this.IsAlternativeDisplayMode();
            int baseDiameter = miniGraphDisplayMode ? MiniGraphMeterDiameter : BaseMeterDiameter;
            int diameter = this.ScaleValue(this.IsReadableInfoPanelSkinEnabled()
                ? baseDiameter - 3
                : baseDiameter);
            int x;
            if (this.GetEffectivePopupSectionMode() == PopupSectionMode.RightOnly)
            {
                x = Math.Max(0, (this.ClientSize.Width - diameter) / 2);
            }
            else
            {
                int rightInset = this.ScaleValue(miniGraphDisplayMode ? MiniGraphMeterRightInset : BaseMeterRightInset);
                x = this.ClientSize.Width - diameter - rightInset;
            }
            int y = miniGraphDisplayMode
                ? Math.Max(0, (this.ClientSize.Height - diameter) / 2)
                : Math.Max(this.ScaleValue(6), (this.ClientSize.Height - diameter) / 2);
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
            if (this.IsMiniSoftDisplayMode())
            {
                int segmentCount = RingSegmentCount;
                float slotSweep = 360F / segmentCount;
                float downloadSegmentSweep = Math.Max(
                    MinimumVisibleSegmentSweepDegrees,
                    slotSweep - Math.Min(slotSweep * 0.42F, MiniGraphDownloadRingSegmentGapDegrees));
                float uploadSegmentSweep = Math.Max(
                    MinimumVisibleSegmentSweepDegrees,
                    slotSweep - Math.Min(slotSweep * 0.42F, MiniGraphUploadRingSegmentGapDegrees));
                float gapBetweenRings = Math.Max(this.ScaleFloat(1.0F), strokeWidth * MiniSoftDualRingInnerGapFactor);
                float downloadWeight = this.GetChannelRingWeight(true);
                float uploadWeight = this.GetChannelRingWeight(false);
                float totalWeight = downloadWeight + uploadWeight;
                float usableBand = Math.Max(2.4F, strokeWidth - gapBetweenRings);
                float weightUnit = usableBand / Math.Max(0.1F, totalWeight);
                float downloadStrokeWidth = Math.Max(4.2F, weightUnit * downloadWeight * 2.52F) + this.ScaleFloat(2F);
                float uploadOffsetDownloadStrokeWidth = downloadStrokeWidth;
                float downloadOutwardExpansion = this.IsTaskbarIntegrationActive()
                    ? DpiHelper.Scale(TaskbarIntegratedDownloadRingOutwardExpansion, this.currentDpi)
                    : 0F;
                downloadStrokeWidth += downloadOutwardExpansion;
                float uploadStrokeWidth = Math.Max(2.4F, weightUnit * uploadWeight * 1.18F);

                RectangleF stableBounds = GetStableArcBounds(bounds);
                RectangleF downloadBounds = GetStableArcBounds(
                    InflateRectangle(stableBounds, -Math.Max(0F, (downloadStrokeWidth / 2F) - this.ScaleFloat(1F) - downloadOutwardExpansion)));
                RectangleF uploadBounds = GetStableArcBounds(
                    InflateRectangle(
                        stableBounds,
                        -((uploadOffsetDownloadStrokeWidth / 2F) + gapBetweenRings + (uploadStrokeWidth / 2F))));

                this.DrawSegmentedProgressSet(
                    graphics,
                    downloadBounds,
                    downloadStrokeWidth,
                    segmentCount,
                    slotSweep,
                    0F,
                    downloadSegmentSweep,
                    downloadFillRatio,
                    downloadTrackColor,
                    downloadStartColor,
                    downloadEndColor,
                    drawTracks);
                this.DrawSegmentedProgressSet(
                    graphics,
                    uploadBounds,
                    uploadStrokeWidth,
                    segmentCount,
                    slotSweep,
                    slotSweep * 0.5F,
                    uploadSegmentSweep,
                    uploadFillRatio,
                    uploadTrackColor,
                    uploadStartColor,
                    uploadEndColor,
                    drawTracks);

                if (!drawTracks)
                {
                    this.DrawPeakHoldRingMarker(
                        graphics,
                        downloadBounds,
                        downloadStrokeWidth,
                        this.GetPeakHoldFillRatio(true),
                        downloadEndColor,
                        true);
                    this.DrawPeakHoldRingMarker(
                        graphics,
                        uploadBounds,
                        uploadStrokeWidth,
                        this.GetPeakHoldFillRatio(false),
                        uploadEndColor,
                        false);
                }

                return;
            }

            if (!this.IsMiniGraphDisplayMode())
            {
                int segmentCount = this.GetCurrentRingSegmentCount();
                float slotSweep = 360F / segmentCount;
                float gapAngle = Math.Min(slotSweep * 0.48F, this.GetCurrentRingSegmentGapDegrees());
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
                return;
            }

            int miniGraphSegmentCount = this.GetCurrentRingSegmentCount();
            float miniGraphSlotSweep = 360F / miniGraphSegmentCount;
            float miniGraphSegmentSweep = Math.Max(
                MinimumVisibleSegmentSweepDegrees,
                miniGraphSlotSweep - Math.Min(miniGraphSlotSweep * 0.42F, this.GetCurrentRingSegmentGapDegrees()));
            float classicGapBetweenRings = Math.Max(this.ScaleFloat(1.0F), strokeWidth * MiniGraphDualRingInnerGapFactor);
            float classicTotalWeight = MiniGraphDownloadRingWeight + MiniGraphUploadRingWeight;
            float classicUsableBand = Math.Max(2.4F, strokeWidth - classicGapBetweenRings);
            float classicWeightUnit = classicUsableBand / Math.Max(0.1F, classicTotalWeight);
            float classicDownloadStrokeWidth = Math.Max(3.6F, classicWeightUnit * MiniGraphDownloadRingWeight * 2.08F);
            float classicUploadStrokeWidth = Math.Max(1.9F, classicWeightUnit * MiniGraphUploadRingWeight * 0.92F);

            RectangleF classicStableBounds = GetStableArcBounds(bounds);
            RectangleF classicDownloadBounds = GetStableArcBounds(
                InflateRectangle(classicStableBounds, -(classicDownloadStrokeWidth / 2F)));
            RectangleF classicUploadBounds = GetStableArcBounds(
                InflateRectangle(
                    classicStableBounds,
                    -((classicDownloadStrokeWidth / 2F) + classicGapBetweenRings + (classicUploadStrokeWidth / 2F))));

            this.DrawSegmentedProgressSet(
                graphics,
                classicDownloadBounds,
                classicDownloadStrokeWidth,
                miniGraphSegmentCount,
                miniGraphSlotSweep,
                0F,
                miniGraphSegmentSweep,
                downloadFillRatio,
                downloadTrackColor,
                downloadStartColor,
                downloadEndColor,
                drawTracks);
            this.DrawSegmentedProgressSet(
                graphics,
                classicUploadBounds,
                classicUploadStrokeWidth,
                miniGraphSegmentCount,
                miniGraphSlotSweep,
                miniGraphSlotSweep * 0.5F,
                miniGraphSegmentSweep,
                uploadFillRatio,
                uploadTrackColor,
                uploadStartColor,
                uploadEndColor,
                drawTracks);

            if (!drawTracks)
            {
                this.DrawPeakHoldRingMarker(
                    graphics,
                    classicDownloadBounds,
                    classicDownloadStrokeWidth,
                    this.GetPeakHoldFillRatio(true),
                    downloadEndColor,
                    true);
                this.DrawPeakHoldRingMarker(
                    graphics,
                    classicUploadBounds,
                    classicUploadStrokeWidth,
                    this.GetPeakHoldFillRatio(false),
                    uploadEndColor,
                    false);
            }
        }

        private double GetPeakHoldFillRatio(bool useDownload)
        {
            double peakHoldBytesPerSecond = useDownload
                ? this.peakHoldDownloadBytesPerSecond
                : this.peakHoldUploadBytesPerSecond;
            double configuredPeakBytesPerSecond = useDownload
                ? this.settings.GetDownloadVisualizationPeak()
                : this.settings.GetUploadVisualizationPeak();
            double visualPeakBytesPerSecond = useDownload
                ? this.visualDownloadPeakBytesPerSecond
                : this.visualUploadPeakBytesPerSecond;
            return this.GetTrafficFillRatio(
                peakHoldBytesPerSecond,
                configuredPeakBytesPerSecond,
                visualPeakBytesPerSecond);
        }

        private void DrawPeakHoldRingMarker(
            Graphics graphics,
            RectangleF bounds,
            float strokeWidth,
            double fillRatio,
            Color color,
            bool useDownload)
        {
            double clampedRatio = Clamp01(fillRatio);
            if (clampedRatio <= 0.001D)
            {
                return;
            }

            float markerSweep = PeakHoldMarkerSweepDegrees;
            float centerAngle = -90F + (float)(360D * clampedRatio);
            float startAngle = centerAngle - (markerSweep / 2F);
            int alpha = this.GetChannelPeakMarkerAlpha(useDownload);

            this.DrawRingSegment(
                graphics,
                bounds,
                Math.Max(1.0F, strokeWidth * 0.32F),
                startAngle,
                markerSweep,
                Color.FromArgb(alpha, color),
                Math.Max(this.ScaleFloat(0.8F), strokeWidth * 0.20F));
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
            bool renderFullScaleRing = this.IsMiniSoftDisplayMode() && !drawTracks && clampedRatio >= 0.995D;

            if (renderFullScaleRing)
            {
                this.DrawFullScaleGradientRing(
                    graphics,
                    bounds,
                    strokeWidth,
                    -90F + segmentOffset,
                    startColor,
                    activeEndColor,
                    this.ScaleFloat(1.8F));
                return;
            }

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

        private void DrawFullScaleGradientRing(
            Graphics graphics,
            RectangleF bounds,
            float strokeWidth,
            float startAngle,
            Color startColor,
            Color endColor,
            float glowWidth)
        {
            const int gradientStepCount = 48;
            float stepSweep = 360F / gradientStepCount;

            for (int stepIndex = 0; stepIndex < gradientStepCount; stepIndex++)
            {
                double colorRatio = gradientStepCount <= 1
                    ? 1D
                    : (double)(stepIndex + 1) / gradientStepCount;
                Color stepColor = GetInterpolatedColor(
                    startColor,
                    endColor,
                    SmoothStep(Math.Max(0D, Math.Min(1D, colorRatio))));

                this.DrawRingSegment(
                    graphics,
                    bounds,
                    strokeWidth,
                    startAngle + (stepIndex * stepSweep),
                    stepSweep + 0.18F,
                    stepColor,
                    glowWidth);
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
            int transparencyPercent = this.settings != null ? this.settings.TransparencyPercent : 0;
            bool ultraTransparent = transparencyPercent >= 100;
            GraphicsState state = graphics.Save();

            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;

                if (glowWidth > 0.05F)
                {
                    using (Pen glowPen = new Pen(
                        Color.FromArgb(ultraTransparent ? 210 : 120, color),
                        NormalizeStrokeWidth(stableStrokeWidth + glowWidth + (ultraTransparent ? this.ScaleFloat(1.1F) : 0F))))
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

                if (ultraTransparent)
                {
                    using (Pen corePen = new Pen(
                        Color.FromArgb(236, GetInterpolatedColor(color, Color.White, 0.34D)),
                        NormalizeStrokeWidth(Math.Max(1F, stableStrokeWidth * 0.28F))))
                    {
                        corePen.Alignment = PenAlignment.Center;
                        corePen.LineJoin = LineJoin.Round;
                        corePen.StartCap = LineCap.Round;
                        corePen.EndCap = LineCap.Round;
                        graphics.DrawArc(corePen, accentBounds, stableStartAngle, stableSweepAngle);
                    }
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
            float bobAmplitude = Math.Max(0.4F, this.ScaleFloat(0.55F));
            float horizontalOffset = iconBounds.Width * 0.19F;
            float centerX = centerBounds.Left + (centerBounds.Width / 2F);
            float centerY = centerBounds.Top + (centerBounds.Height / 2F);
            float rawDownloadPulse = 0.5F + (0.5F * (float)Math.Sin(animationSeconds * 3.6D));
            float rawUploadPulse = 0.5F + (0.5F * (float)Math.Sin((animationSeconds * 3.6D) + Math.PI));
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
            int transparencyPercent = this.settings != null ? this.settings.TransparencyPercent : 0;
            bool ultraTransparent = transparencyPercent >= 100;
            float glowWidth = glowBaseWidth
                + (pulse * this.ScaleFloat(0.85F))
                + ((float)intensity * this.ScaleFloat(0.75F))
                + (ultraTransparent ? this.ScaleFloat(0.95F) : 0F);
            int glowAlpha = 96 + (int)Math.Round((pulse * 36F) + (float)intensity * 54F, MidpointRounding.AwayFromZero);
            if (ultraTransparent)
            {
                glowAlpha = Math.Min(232, glowAlpha + 62);
            }
            Color glowColor = Color.FromArgb(
                Math.Max(0, Math.Min(220, glowAlpha)),
                bodyColor.R,
                bodyColor.G,
                bodyColor.B);
            Color outlineColor = GetInterpolatedColor(
                bodyColor,
                Color.FromArgb(255, 250, 248, 236),
                (0.22D + (0.10D * pulse)) + (ultraTransparent ? 0.12D : 0D));
            GraphicsState state = graphics.Save();

            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;

            using (GraphicsPath arrowPath = CreateArrowPath(stableCenter, scaledWidth, scaledHeight, scaledShaftWidth, pointsUp))
            using (Pen glowPen = new Pen(glowColor, glowWidth))
            using (SolidBrush arrowBrush = new SolidBrush(bodyColor))
            using (Pen outlinePen = new Pen(
                outlineColor,
                Math.Max(0.8F, this.ScaleFloat(0.8F)) + (ultraTransparent ? this.ScaleFloat(0.25F) : 0F)))
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
                Color basePanelColor = this.GetPanelBackgroundBaseColor();
                Color convexCenterColor = ApplyAlpha(
                    GetInterpolatedColor(basePanelColor, Color.FromArgb(104, 136, 194), 0.07D),
                    backgroundAlpha);
                Color convexEdgeColor = Color.FromArgb(0, basePanelColor.R, basePanelColor.G, basePanelColor.B);
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

        private void DrawPanelGlassSurface(
            Graphics graphics,
            RectangleF innerBounds,
            float innerCornerRadius,
            byte backgroundAlpha)
        {
            GraphicsState state = graphics.Save();

            try
            {
                using (GraphicsPath clipPath = CreateRoundedPath(innerBounds, innerCornerRadius))
                {
                    graphics.SetClip(clipPath, CombineMode.Intersect);

                    float inset = Math.Max(1F, this.ScaleFloat(1.1F));
                    RectangleF surfaceBounds = InflateRectangle(innerBounds, -inset);

                    using (LinearGradientBrush surfaceBrush = new LinearGradientBrush(
                        new PointF(surfaceBounds.Left, surfaceBounds.Top),
                        new PointF(surfaceBounds.Left, surfaceBounds.Bottom),
                        ApplyAlpha(Color.FromArgb(22, 236, 242, 250), backgroundAlpha),
                        Color.FromArgb(0, 236, 242, 250)))
                    {
                        ColorBlend blend = new ColorBlend();
                        blend.Positions = new float[] { 0F, 0.26F, 0.72F, 1F };
                        blend.Colors = new Color[]
                        {
                            ApplyAlpha(Color.FromArgb(24, 240, 246, 252), backgroundAlpha),
                            ApplyAlpha(Color.FromArgb(12, 224, 232, 244), backgroundAlpha),
                            Color.FromArgb(0, 224, 232, 244),
                            ApplyAlpha(Color.FromArgb(8, 10, 18, 28), backgroundAlpha)
                        };
                        surfaceBrush.InterpolationColors = blend;
                        graphics.FillRectangle(surfaceBrush, surfaceBounds);
                    }
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private void DrawTaskbarIntegratedPanelEdgeGradient(
            Graphics graphics,
            RectangleF outerBounds,
            float cornerRadius,
            byte backgroundAlpha)
        {
            if (this.activeTaskbarSnapshot == null || backgroundAlpha == 0)
            {
                return;
            }

            float maximumGradientWidth = Math.Min(outerBounds.Width, outerBounds.Height) / 2F;
            float gradientWidth = Math.Min(
                Math.Max(2F, this.ScaleFloat(TaskbarIntegratedEdgeGradientWidth)),
                Math.Max(1F, maximumGradientWidth - 1F));
            if (gradientWidth <= 1F)
            {
                return;
            }

            Color taskbarColor = this.activeTaskbarSnapshot.Theme.TaskbarColor;
            int outerAlpha = Math.Min(
                (int)TaskbarIntegratedEdgeGradientOuterAlpha,
                Math.Max(72, (int)backgroundAlpha + 142));
            int lipAlpha = Math.Min(
                (int)TaskbarIntegratedEdgeGradientLipAlpha,
                Math.Max(96, (int)backgroundAlpha + 160));
            int steps = Math.Max(12, (int)Math.Ceiling(gradientWidth));

            GraphicsState state = graphics.Save();

            try
            {
                using (GraphicsPath clipPath = CreateRoundedPath(outerBounds, cornerRadius))
                {
                    graphics.SetClip(clipPath, CombineMode.Intersect);

                    float lipWidth = Math.Min(
                        gradientWidth,
                        Math.Max(1F, this.ScaleFloat(TaskbarIntegratedEdgeGradientLipWidth)));
                    RectangleF lipInnerBounds = InflateRectangle(outerBounds, -lipWidth);
                    using (GraphicsPath lipOuterPath = CreateRoundedPath(outerBounds, cornerRadius))
                    using (GraphicsPath lipInnerPath = CreateRoundedPath(
                        lipInnerBounds,
                        Math.Max(2F, cornerRadius - lipWidth)))
                    using (Region lipRegion = new Region(lipOuterPath))
                    using (SolidBrush lipBrush = new SolidBrush(ApplyAlpha(taskbarColor, (byte)lipAlpha)))
                    {
                        lipRegion.Exclude(lipInnerPath);
                        graphics.FillRegion(lipBrush, lipRegion);
                    }

                    for (int step = 0; step < steps; step++)
                    {
                        float outerProgress = step / (float)steps;
                        float innerProgress = (step + 1) / (float)steps;
                        float outerInset = outerProgress * gradientWidth;
                        float innerInset = innerProgress * gradientWidth;
                        double blend = 1D - SmoothStep((outerProgress + innerProgress) * 0.5F);
                        int alpha = (int)Math.Round(outerAlpha * blend);
                        if (alpha <= 1)
                        {
                            continue;
                        }

                        RectangleF bandOuterBounds = InflateRectangle(outerBounds, -outerInset);
                        RectangleF bandInnerBounds = InflateRectangle(outerBounds, -innerInset);
                        if (bandOuterBounds.Width <= 1F || bandOuterBounds.Height <= 1F)
                        {
                            break;
                        }

                        using (GraphicsPath bandOuterPath = CreateRoundedPath(
                            bandOuterBounds,
                            Math.Max(2F, cornerRadius - outerInset)))
                        using (GraphicsPath bandInnerPath = CreateRoundedPath(
                            bandInnerBounds,
                            Math.Max(2F, cornerRadius - innerInset)))
                        using (Region bandRegion = new Region(bandOuterPath))
                        using (SolidBrush bandBrush = new SolidBrush(ApplyAlpha(taskbarColor, (byte)Math.Min(255, alpha))))
                        {
                            bandRegion.Exclude(bandInnerPath);
                            graphics.FillRegion(bandBrush, bandRegion);
                        }
                    }
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private void DrawTaskbarIntegratedInnerOpacityBoost(
            Graphics graphics,
            RectangleF innerBounds,
            float innerCornerRadius,
            bool drawRightSection)
        {
            if (this.activeTaskbarSnapshot == null)
            {
                return;
            }

            GraphicsState state = graphics.Save();

            try
            {
                RectangleF focusBounds = drawRightSection
                    ? new RectangleF(
                        innerBounds.Left + (innerBounds.Width * 0.05F),
                        innerBounds.Top + (innerBounds.Height * 0.11F),
                        innerBounds.Width * 0.58F,
                        innerBounds.Height * 0.74F)
                    : new RectangleF(
                        innerBounds.Left + (innerBounds.Width * 0.12F),
                        innerBounds.Top + (innerBounds.Height * 0.11F),
                        innerBounds.Width * 0.76F,
                        innerBounds.Height * 0.74F);
                float focusCornerRadius = Math.Max(4F, Math.Min(focusBounds.Width, focusBounds.Height) * 0.18F);

                using (GraphicsPath clipPath = CreateRoundedPath(innerBounds, innerCornerRadius))
                using (GraphicsPath focusPath = CreateRoundedPath(focusBounds, focusCornerRadius))
                using (PathGradientBrush opacityBrush = new PathGradientBrush(focusPath))
                {
                    Color panelBackgroundColor = this.GetPanelBackgroundBaseColor();
                    Color[] surroundColors = new Color[Math.Max(1, focusPath.PointCount)];
                    for (int i = 0; i < surroundColors.Length; i++)
                    {
                        surroundColors[i] = Color.FromArgb(0, panelBackgroundColor.R, panelBackgroundColor.G, panelBackgroundColor.B);
                    }

                    graphics.SetClip(clipPath, CombineMode.Intersect);
                    opacityBrush.CenterPoint = new PointF(
                        focusBounds.Left + (focusBounds.Width * (drawRightSection ? 0.42F : 0.50F)),
                        focusBounds.Top + (focusBounds.Height * 0.50F));
                    opacityBrush.FocusScales = drawRightSection
                        ? new PointF(0.80F, 0.78F)
                        : new PointF(0.84F, 0.78F);
                    opacityBrush.CenterColor = ApplyAlpha(panelBackgroundColor, TaskbarIntegratedPanelInnerOpacityBoostAlpha);
                    opacityBrush.SurroundColors = surroundColors;
                    opacityBrush.WrapMode = WrapMode.Clamp;
                    graphics.FillPath(opacityBrush, focusPath);
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private void DrawTaskbarIntegratedInfoOpacityPlate(Graphics graphics, Rectangle meterBounds)
        {
            if (this.activeTaskbarSnapshot == null || !this.IsLeftSectionVisible())
            {
                return;
            }

            int left = this.ScaleValue(4);
            int right = this.GetLeftSectionRightBoundary(meterBounds, left + this.ScaleValue(44)) - this.ScaleValue(2);
            int top = this.ScaleValue(4);
            int bottom = Math.Min(
                this.ClientSize.Height - this.ScaleValue(6),
                this.uploadValueLabel.Bounds.Bottom + this.ScaleValue(4));
            if (right - left < this.ScaleValue(18) || bottom - top < this.ScaleValue(18))
            {
                return;
            }

            RectangleF plateBounds = new RectangleF(
                AlignToHalfPixel(left),
                AlignToHalfPixel(top),
                Math.Max(1F, right - left),
                Math.Max(1F, bottom - top));
            float radius = Math.Max(this.ScaleFloat(6F), Math.Min(plateBounds.Width, plateBounds.Height) * 0.16F);
            Color panelBackgroundColor = this.GetPanelBackgroundBaseColor();

            GraphicsState state = graphics.Save();
            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;

                using (GraphicsPath platePath = CreateRoundedPath(plateBounds, radius))
                using (LinearGradientBrush fillBrush = new LinearGradientBrush(
                    new PointF(plateBounds.Left, plateBounds.Top),
                    new PointF(plateBounds.Right, plateBounds.Bottom),
                    ApplyAlpha(GetInterpolatedColor(panelBackgroundColor, Color.FromArgb(6, 14, 34), 0.42D), TaskbarIntegratedInfoPlateFillAlpha),
                    ApplyAlpha(GetInterpolatedColor(panelBackgroundColor, Color.FromArgb(10, 20, 40), 0.30D), (byte)Math.Max(0, TaskbarIntegratedInfoPlateFillAlpha - 16))))
                using (LinearGradientBrush highlightBrush = new LinearGradientBrush(
                    new PointF(plateBounds.Left, plateBounds.Top),
                    new PointF(plateBounds.Left, plateBounds.Bottom),
                    Color.Transparent,
                    Color.Transparent))
                {
                    ColorBlend blend = new ColorBlend();
                    blend.Positions = new float[] { 0F, 0.16F, 0.48F, 1F };
                    blend.Colors = new Color[]
                    {
                        ApplyAlpha(Color.FromArgb(218, 234, 255), TaskbarIntegratedInfoPlateHighlightAlpha),
                        ApplyAlpha(Color.FromArgb(168, 202, 240), (byte)Math.Max(0, TaskbarIntegratedInfoPlateHighlightAlpha - 8)),
                        Color.FromArgb(0, 140, 176, 220),
                        Color.FromArgb(0, 24, 34, 48)
                    };
                    highlightBrush.InterpolationColors = blend;

                    graphics.FillPath(fillBrush, platePath);
                    graphics.FillPath(highlightBrush, platePath);
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private static Bitmap GetPanelBackgroundAsset(string panelSkinId, Size targetSize)
        {
            PanelSkinDefinition definition = PanelSkinCatalog.GetSkinById(panelSkinId);
            if (definition == null || string.IsNullOrWhiteSpace(definition.DirectoryPath))
            {
                return null;
            }

            return GetPanelBackgroundAssetFromDirectory(definition.DirectoryPath, targetSize);
        }

        private static Bitmap GetPanelBackgroundAssetFromDirectory(string assetDirectoryPath, Size targetSize)
        {
            string normalizedDirectoryPath = string.IsNullOrWhiteSpace(assetDirectoryPath)
                ? AppStorage.BaseDirectory
                : assetDirectoryPath;

            lock (PanelBackgroundAssetSync)
            {
                bool loadAttempted;
                if (!PanelBackgroundAssetLoadAttemptedByDirectory.TryGetValue(normalizedDirectoryPath, out loadAttempted) || !loadAttempted)
                {
                    Dictionary<string, Bitmap> assets = LoadPanelBackgroundAssets(normalizedDirectoryPath);
                    CachedPanelBackgroundAssetsByDirectory[normalizedDirectoryPath] = assets;
                    PanelBackgroundAssetLoadAttemptedByDirectory[normalizedDirectoryPath] = true;
                }

                Dictionary<string, Bitmap> cachedAssets;
                if (!CachedPanelBackgroundAssetsByDirectory.TryGetValue(normalizedDirectoryPath, out cachedAssets) ||
                    cachedAssets == null ||
                    cachedAssets.Count == 0)
                {
                    return null;
                }

                return SelectBestPanelBackgroundAsset(cachedAssets, targetSize);
            }
        }

        internal static void ReleasePanelBackgroundAssetCache(string assetDirectoryPath)
        {
            string normalizedDirectoryPath = string.IsNullOrWhiteSpace(assetDirectoryPath)
                ? AppStorage.BaseDirectory
                : assetDirectoryPath;

            lock (PanelBackgroundAssetSync)
            {
                Dictionary<string, Bitmap> cachedAssets;
                if (CachedPanelBackgroundAssetsByDirectory.TryGetValue(normalizedDirectoryPath, out cachedAssets) &&
                    cachedAssets != null)
                {
                    foreach (KeyValuePair<string, Bitmap> asset in cachedAssets)
                    {
                        if (asset.Value != null)
                        {
                            asset.Value.Dispose();
                        }
                    }
                }

                CachedPanelBackgroundAssetsByDirectory.Remove(normalizedDirectoryPath);
                PanelBackgroundAssetLoadAttemptedByDirectory.Remove(normalizedDirectoryPath);
            }
        }

        private static Dictionary<string, Bitmap> LoadPanelBackgroundAssets(string assetDirectoryPath)
        {
            Dictionary<string, Bitmap> loadedAssets = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
            string[] assetPaths = GetPanelBackgroundAssetPaths(assetDirectoryPath);

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
                        loadedAssets[assetPath] = new Bitmap(image);
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

            if (loadedAssets.Count == 0)
            {
                AppLog.WarnOnce(
                    "panel-background-asset-missing-" + assetDirectoryPath,
                    string.Format(
                        "No panel background assets were found in '{0}'. Procedural panel rendering will be used.",
                        assetDirectoryPath));
            }

            return loadedAssets;
        }

        private static string[] GetPanelBackgroundAssetPaths(string assetDirectoryPath)
        {
            string normalizedDirectoryPath = string.IsNullOrWhiteSpace(assetDirectoryPath)
                ? AppStorage.BaseDirectory
                : assetDirectoryPath;
            List<string> assetPaths = new List<string>();

            for (int i = 0; i < PanelBackgroundPreparedScalePercents.Length; i++)
            {
                int scalePercent = PanelBackgroundPreparedScalePercents[i];
                string fileName = scalePercent == 100
                    ? PanelBackgroundAssetFileName
                    : string.Format(PanelBackgroundScaledAssetFileNameFormat, scalePercent);
                assetPaths.Add(Path.Combine(normalizedDirectoryPath, fileName));
            }

            return assetPaths.ToArray();
        }

        private static Bitmap SelectBestPanelBackgroundAsset(Dictionary<string, Bitmap> assets, Size targetSize)
        {
            if (assets == null || assets.Count == 0)
            {
                return null;
            }

            Bitmap bestAsset = null;
            long bestScore = long.MaxValue;

            foreach (KeyValuePair<string, Bitmap> pair in assets)
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

        private static Bitmap GetCachedMeterCenterAsset(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || !File.Exists(assetPath))
            {
                return null;
            }

            lock (MeterCenterAssetSync)
            {
                if (string.Equals(cachedMeterCenterAssetPath, assetPath, StringComparison.OrdinalIgnoreCase) &&
                    cachedMeterCenterAsset != null)
                {
                    return cachedMeterCenterAsset;
                }

                if (cachedMeterCenterAsset != null)
                {
                    cachedMeterCenterAsset.Dispose();
                    cachedMeterCenterAsset = null;
                }

                using (Image meterCenterImage = Image.FromFile(assetPath))
                {
                    cachedMeterCenterAsset = new Bitmap(meterCenterImage);
                }

                cachedMeterCenterAssetPath = assetPath;
                return cachedMeterCenterAsset;
            }
        }

        private static void ReleaseCachedMeterCenterAsset()
        {
            lock (MeterCenterAssetSync)
            {
                if (cachedMeterCenterAsset != null)
                {
                    cachedMeterCenterAsset.Dispose();
                    cachedMeterCenterAsset = null;
                }

                cachedMeterCenterAssetPath = string.Empty;
            }
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

                    string meterCenterAssetPath = this.GetCurrentMeterCenterAssetPath();
                    if (!string.IsNullOrWhiteSpace(meterCenterAssetPath))
                    {
                        try
                        {
                            GraphicsState imageState = graphics.Save();
                            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            graphics.CompositingQuality = CompositingQuality.HighQuality;

                            Bitmap meterCenterImage = GetCachedMeterCenterAsset(meterCenterAssetPath);
                            if (meterCenterImage != null)
                            {
                                graphics.DrawImage(meterCenterImage, centerBounds);
                                graphics.Restore(imageState);
                                return;
                            }
                            graphics.Restore(imageState);
                        }
                        catch (Exception ex)
                        {
                            AppLog.WarnOnce(
                                "meter-center-asset-load-failed-" + meterCenterAssetPath,
                                string.Format(
                                    "The meter center asset could not be loaded from '{0}'. Procedural rendering will be used instead.",
                                    meterCenterAssetPath),
                                ex);
                        }
                    }

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

                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private void DrawRotatingMeterGloss(
            Graphics graphics,
            RectangleF centerBounds,
            double visualDownloadFillRatio,
            double visualUploadFillRatio)
        {
            if (!this.settings.RotatingMeterGlossEnabled ||
                Math.Max(visualDownloadFillRatio, visualUploadFillRatio) <= MeterGlossAnimationThresholdRatio)
            {
                return;
            }

            GraphicsState state = graphics.Save();

            try
            {
                using (GraphicsPath centerPath = new GraphicsPath())
                {
                    centerPath.AddEllipse(centerBounds);
                    graphics.SetClip(centerPath, CombineMode.Intersect);
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;

                    float centerX = centerBounds.Left + (centerBounds.Width / 2F);
                    float centerY = centerBounds.Top + (centerBounds.Height / 2F);
                    float size = Math.Min(centerBounds.Width, centerBounds.Height);

                    graphics.TranslateTransform(centerX, centerY);
                    graphics.RotateTransform((float)this.meterGlossRotationDegrees);
                    graphics.TranslateTransform(-centerX, -centerY);

                    RectangleF upperSheenBounds = new RectangleF(
                        centerX - (size * 0.44F),
                        centerY - (size * 0.47F),
                        size * 0.88F,
                        size * 0.30F);
                    using (GraphicsPath upperSheenPath = new GraphicsPath())
                    {
                        upperSheenPath.AddEllipse(upperSheenBounds);
                        using (PathGradientBrush upperSheenBrush = new PathGradientBrush(upperSheenPath))
                        {
                            upperSheenBrush.CenterPoint = new PointF(
                                upperSheenBounds.Left + (upperSheenBounds.Width * 0.64F),
                                upperSheenBounds.Top + (upperSheenBounds.Height * 0.18F));
                            upperSheenBrush.CenterColor = Color.FromArgb(110, 255, 255, 255);
                            upperSheenBrush.SurroundColors = new Color[] { Color.Transparent };
                            graphics.FillEllipse(upperSheenBrush, upperSheenBounds);
                        }
                    }

                    RectangleF topRightHighlightBounds = new RectangleF(
                        centerX + (size * 0.14F),
                        centerY - (size * 0.28F),
                        size * 0.22F,
                        size * 0.22F);
                    using (GraphicsPath topRightHighlightPath = new GraphicsPath())
                    {
                        topRightHighlightPath.AddEllipse(topRightHighlightBounds);
                        using (PathGradientBrush topRightHighlightBrush = new PathGradientBrush(topRightHighlightPath))
                        {
                            topRightHighlightBrush.CenterPoint = new PointF(
                                topRightHighlightBounds.Left + (topRightHighlightBounds.Width * 0.60F),
                                topRightHighlightBounds.Top + (topRightHighlightBounds.Height * 0.44F));
                            topRightHighlightBrush.CenterColor = Color.FromArgb(122, 255, 255, 255);
                            topRightHighlightBrush.SurroundColors = new Color[] { Color.Transparent };
                            graphics.FillEllipse(topRightHighlightBrush, topRightHighlightBounds);
                        }
                    }

                    RectangleF lowerBlueGlowBounds = new RectangleF(
                        centerX - (size * 0.37F),
                        centerY + (size * 0.16F),
                        size * 0.48F,
                        size * 0.25F);
                    using (GraphicsPath lowerBlueGlowPath = new GraphicsPath())
                    {
                        lowerBlueGlowPath.AddEllipse(lowerBlueGlowBounds);
                        using (PathGradientBrush lowerBlueGlowBrush = new PathGradientBrush(lowerBlueGlowPath))
                        {
                            lowerBlueGlowBrush.CenterPoint = new PointF(
                                lowerBlueGlowBounds.Left + (lowerBlueGlowBounds.Width * 0.38F),
                                lowerBlueGlowBounds.Top + (lowerBlueGlowBounds.Height * 0.58F));
                            lowerBlueGlowBrush.CenterColor = Color.FromArgb(112, 78, 176, 255);
                            lowerBlueGlowBrush.SurroundColors = new Color[] { Color.Transparent };
                            graphics.FillEllipse(lowerBlueGlowBrush, lowerBlueGlowBounds);
                        }
                    }

                    RectangleF counterGlossBounds = new RectangleF(
                        centerX - (size * 0.34F),
                        centerY - (size * 0.09F),
                        size * 0.15F,
                        size * 0.15F);
                    using (GraphicsPath counterGlossPath = new GraphicsPath())
                    {
                        counterGlossPath.AddEllipse(counterGlossBounds);
                        using (PathGradientBrush counterGlossBrush = new PathGradientBrush(counterGlossPath))
                        {
                            counterGlossBrush.CenterPoint = new PointF(
                                counterGlossBounds.Left + (counterGlossBounds.Width * 0.46F),
                                counterGlossBounds.Top + (counterGlossBounds.Height * 0.46F));
                            counterGlossBrush.CenterColor = Color.FromArgb(68, 220, 242, 255);
                            counterGlossBrush.SurroundColors = new Color[] { Color.Transparent };
                            graphics.FillEllipse(counterGlossBrush, counterGlossBounds);
                        }
                    }
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private void DrawActivityBorderLights(
            Graphics graphics,
            double visualDownloadFillRatio,
            double visualUploadFillRatio)
        {
            if (!this.ShouldUseActivityBorderGlow())
            {
                return;
            }

            double intensity = Math.Max(visualDownloadFillRatio, visualUploadFillRatio);
            double fadeRatio = GetActivityBorderFadeRatio(intensity);
            if (fadeRatio <= 0D ||
                this.Width < this.ScaleValue(24) ||
                this.Height < this.ScaleValue(18))
            {
                return;
            }

            float inset = Math.Max(2.5F, this.ScaleFloat(3.2F));
            RectangleF borderBounds = new RectangleF(
                inset,
                inset,
                Math.Max(1F, this.Width - (inset * 2F)),
                Math.Max(1F, this.Height - (inset * 2F)));
            float cornerRadius = Math.Min(
                this.ScaleFloat(BaseWindowCornerRadius),
                Math.Min(borderBounds.Width, borderBounds.Height) / 2F);
            float perimeter = GetRoundedRectanglePerimeter(borderBounds, cornerRadius);
            if (perimeter <= 1F)
            {
                return;
            }

            double direction = this.GetActivityBorderDirection();
            Color glowColor = direction >= 0D
                ? DownloadArrowBaseColor
                : UploadArrowBaseColor;
            Color coreColor = direction >= 0D
                ? DownloadArrowHighColor
                : UploadArrowHighColor;
            int lightCount = Math.Max(16, Math.Min(34, (int)Math.Round(perimeter / Math.Max(7.5F, this.ScaleFloat(8.8F)))));
            double phase = this.activityBorderRotationDegrees / 360D;
            double smoothedIntensity = SmoothStep(intensity) * fadeRatio;

            GraphicsState state = graphics.Save();
            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.CompositingQuality = CompositingQuality.HighQuality;

                for (int i = 0; i < lightCount; i++)
                {
                    double unit = i / (double)lightCount;
                    double travelAccent = GetActivityBorderTravelAccent(unit, phase, direction);
                    double wave = 0.24D + (0.76D * travelAccent);
                    double localIntensity = smoothedIntensity * wave;
                    PointF center = GetRoundedRectanglePoint(borderBounds, cornerRadius, unit);
                    float coreRadius = this.ScaleFloat(0.55F) + ((float)localIntensity * this.ScaleFloat(1.85F));
                    float glowRadius = coreRadius + this.ScaleFloat(2.8F) + ((float)localIntensity * this.ScaleFloat(4.4F));
                    int glowAlpha = Math.Min(240, (int)Math.Round(localIntensity * 212D));
                    int coreAlpha = Math.Min(255, (int)Math.Round(localIntensity * 255D));

                    this.DrawActivityBorderLight(
                        graphics,
                        center,
                        coreRadius,
                        glowRadius,
                        glowColor,
                        coreColor,
                        glowAlpha,
                        coreAlpha);
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private void DrawActivityBorderLight(
            Graphics graphics,
            PointF center,
            float coreRadius,
            float glowRadius,
            Color glowColor,
            Color coreColor,
            int glowAlpha,
            int coreAlpha)
        {
            RectangleF glowBounds = new RectangleF(
                center.X - glowRadius,
                center.Y - glowRadius,
                glowRadius * 2F,
                glowRadius * 2F);
            using (GraphicsPath glowPath = new GraphicsPath())
            {
                glowPath.AddEllipse(glowBounds);
                using (PathGradientBrush glowBrush = new PathGradientBrush(glowPath))
                {
                    glowBrush.CenterPoint = center;
                    glowBrush.CenterColor = Color.FromArgb(Math.Max(0, Math.Min(255, glowAlpha)), glowColor);
                    glowBrush.SurroundColors = new Color[] { Color.Transparent };
                    graphics.FillEllipse(glowBrush, glowBounds);
                }
            }

            RectangleF coreBounds = new RectangleF(
                center.X - coreRadius,
                center.Y - coreRadius,
                coreRadius * 2F,
                coreRadius * 2F);
            using (SolidBrush coreBrush = new SolidBrush(Color.FromArgb(Math.Max(0, Math.Min(255, coreAlpha)), coreColor)))
            using (SolidBrush highlightBrush = new SolidBrush(Color.FromArgb(Math.Max(0, Math.Min(255, coreAlpha)), Color.White)))
            {
                graphics.FillEllipse(coreBrush, coreBounds);
                graphics.FillEllipse(
                    highlightBrush,
                    coreBounds.Left + (coreRadius * 0.42F),
                    coreBounds.Top + (coreRadius * 0.30F),
                    Math.Max(0.6F, coreRadius * 0.72F),
                    Math.Max(0.6F, coreRadius * 0.72F));
            }
        }

        private double GetActivityBorderIntensity()
        {
            if (!this.ShouldUseActivityBorderGlow())
            {
                return 0D;
            }

            return Math.Max(
                this.GetVisualizedFillRatioForCurrentDownload(),
                this.GetVisualizedFillRatioForCurrentUpload());
        }

        private bool ShouldUseActivityBorderGlow()
        {
            return this.settings != null &&
                this.settings.ActivityBorderGlowEnabled &&
                !this.IsTaskbarIntegratedMode();
        }

        private double GetActivityBorderDirection()
        {
            double downloadInfluence = this.GetVisualizedFillRatioForCurrentDownload();
            double uploadInfluence = this.GetVisualizedFillRatioForCurrentUpload();
            return uploadInfluence > downloadInfluence + 0.015D ? -1D : 1D;
        }

        private static double GetActivityBorderFadeRatio(double intensity)
        {
            if (intensity <= ActivityBorderFadeInStartRatio)
            {
                return 0D;
            }

            double normalized = (intensity - ActivityBorderFadeInStartRatio) /
                Math.Max(0.0001D, ActivityBorderFadeInFullRatio - ActivityBorderFadeInStartRatio);
            return SmoothStep(Math.Max(0D, Math.Min(1D, normalized)));
        }

        private static double GetActivityBorderTravelAccent(double unitPosition, double phase, double direction)
        {
            double forwardDistance = direction >= 0D
                ? GetCircularForwardDistance(phase, unitPosition)
                : GetCircularForwardDistance(unitPosition, phase);
            double head = 1D - Math.Min(1D, forwardDistance / ActivityBorderTravelPulseWidth);
            double tail = 1D - Math.Min(1D, forwardDistance / ActivityBorderTravelTailWidth);
            double secondaryPhase = phase + 0.50D;
            secondaryPhase -= Math.Floor(secondaryPhase);
            double secondaryDistance = direction >= 0D
                ? GetCircularForwardDistance(secondaryPhase, unitPosition)
                : GetCircularForwardDistance(unitPosition, secondaryPhase);
            double secondaryHead = 1D - Math.Min(1D, secondaryDistance / (ActivityBorderTravelPulseWidth * 0.86D));

            return Math.Max(
                SmoothStep(head),
                Math.Max(SmoothStep(tail) * 0.54D, SmoothStep(secondaryHead) * 0.58D));
        }

        private static double GetCircularForwardDistance(double fromUnitPosition, double toUnitPosition)
        {
            double distance = toUnitPosition - fromUnitPosition;
            distance -= Math.Floor(distance);
            return distance;
        }

        private static float GetRoundedRectanglePerimeter(RectangleF bounds, float radius)
        {
            float safeRadius = Math.Max(0F, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2F));
            float horizontal = Math.Max(0F, bounds.Width - (2F * safeRadius));
            float vertical = Math.Max(0F, bounds.Height - (2F * safeRadius));
            return (2F * horizontal) + (2F * vertical) + ((float)(Math.PI * 2D) * safeRadius);
        }

        private static PointF GetRoundedRectanglePoint(RectangleF bounds, float radius, double unitPosition)
        {
            float safeRadius = Math.Max(0F, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2F));
            float horizontal = Math.Max(0F, bounds.Width - (2F * safeRadius));
            float vertical = Math.Max(0F, bounds.Height - (2F * safeRadius));
            float arcLength = (float)(Math.PI * safeRadius / 2D);
            float perimeter = (2F * horizontal) + (2F * vertical) + (4F * arcLength);
            if (perimeter <= 0F)
            {
                return new PointF(bounds.Left + (bounds.Width / 2F), bounds.Top + (bounds.Height / 2F));
            }

            double normalizedUnit = unitPosition - Math.Floor(unitPosition);
            float distance = (float)(normalizedUnit * perimeter);

            if (distance <= horizontal)
            {
                return new PointF(bounds.Left + safeRadius + distance, bounds.Top);
            }

            distance -= horizontal;
            if (distance <= arcLength)
            {
                return GetPointOnCircle(
                    bounds.Right - safeRadius,
                    bounds.Top + safeRadius,
                    safeRadius,
                    -90D + ((distance / Math.Max(0.0001F, arcLength)) * 90D));
            }

            distance -= arcLength;
            if (distance <= vertical)
            {
                return new PointF(bounds.Right, bounds.Top + safeRadius + distance);
            }

            distance -= vertical;
            if (distance <= arcLength)
            {
                return GetPointOnCircle(
                    bounds.Right - safeRadius,
                    bounds.Bottom - safeRadius,
                    safeRadius,
                    (distance / Math.Max(0.0001F, arcLength)) * 90D);
            }

            distance -= arcLength;
            if (distance <= horizontal)
            {
                return new PointF(bounds.Right - safeRadius - distance, bounds.Bottom);
            }

            distance -= horizontal;
            if (distance <= arcLength)
            {
                return GetPointOnCircle(
                    bounds.Left + safeRadius,
                    bounds.Bottom - safeRadius,
                    safeRadius,
                    90D + ((distance / Math.Max(0.0001F, arcLength)) * 90D));
            }

            distance -= arcLength;
            if (distance <= vertical)
            {
                return new PointF(bounds.Left, bounds.Bottom - safeRadius - distance);
            }

            distance -= vertical;
            return GetPointOnCircle(
                bounds.Left + safeRadius,
                bounds.Top + safeRadius,
                safeRadius,
                180D + ((distance / Math.Max(0.0001F, arcLength)) * 90D));
        }

        private static PointF GetPointOnCircle(float centerX, float centerY, float radius, double angleDegrees)
        {
            double angleRadians = angleDegrees * Math.PI / 180D;
            return new PointF(
                centerX + ((float)Math.Cos(angleRadians) * radius),
                centerY + ((float)Math.Sin(angleRadians) * radius));
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
            Color panelBackgroundColor = this.GetPanelBackgroundBaseColor();
            Color panelDividerColor = this.GetPanelDividerBaseColor();

            using (Pen transitionPen = new Pen(
                ApplyAlpha(GetInterpolatedColor(panelBackgroundColor, panelDividerColor, 0.20D), Math.Min(backgroundAlpha, (byte)52)),
                accentWidth))
            using (Pen separatorPen = new Pen(
                ApplyAlpha(GetInterpolatedColor(panelDividerColor, panelBackgroundColor, 0.14D), Math.Min(backgroundAlpha, (byte)88)),
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

        private static Bitmap CreateSelectiveTransparencyBitmap(Bitmap sourceBitmap, byte baseAlpha)
        {
            if (sourceBitmap == null)
            {
                return null;
            }

            Bitmap adjustedBitmap = new Bitmap(sourceBitmap.Width, sourceBitmap.Height, PixelFormat.Format32bppArgb);
            if (baseAlpha >= 255)
            {
                using (Graphics graphics = Graphics.FromImage(adjustedBitmap))
                {
                    graphics.DrawImageUnscaled(sourceBitmap, 0, 0);
                }

                return adjustedBitmap;
            }

            double baseAlphaRatio = baseAlpha / 255D;

            for (int y = 0; y < sourceBitmap.Height; y++)
            {
                for (int x = 0; x < sourceBitmap.Width; x++)
                {
                    Color pixel = sourceBitmap.GetPixel(x, y);
                    if (pixel.A <= 0)
                    {
                        adjustedBitmap.SetPixel(x, y, Color.Transparent);
                        continue;
                    }

                    double luminance = ((pixel.R * 0.2126D) + (pixel.G * 0.7152D) + (pixel.B * 0.0722D)) / 255D;
                    int maxChannel = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
                    int minChannel = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
                    double saturation = maxChannel <= 0
                        ? 0D
                        : (maxChannel - minChannel) / (double)maxChannel;

                    double highlightWeight = Clamp01((luminance - 0.22D) / 0.78D);
                    double accentWeight = Clamp01((saturation - 0.45D) / 0.55D) * 0.45D;
                    double preserveWeight = Clamp01(Math.Max(highlightWeight, accentWeight));
                    double effectiveAlphaRatio = baseAlphaRatio + ((1D - baseAlphaRatio) * preserveWeight);
                    int effectiveAlpha = (int)Math.Round(pixel.A * effectiveAlphaRatio);

                    adjustedBitmap.SetPixel(
                        x,
                        y,
                        Color.FromArgb(
                            Math.Max(0, Math.Min(255, effectiveAlpha)),
                            pixel.R,
                            pixel.G,
                            pixel.B));
                }
            }

            return adjustedBitmap;
        }

        private static double Clamp01(double value)
        {
            if (value <= 0D)
            {
                return 0D;
            }

            if (value >= 1D)
            {
                return 1D;
            }

            return value;
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
            if (this.IsReadableInfoPanelSkinEnabled())
            {
                Color downloadCaptionColor = GetInterpolatedColor(
                    DownloadCaptionColor,
                    Color.FromArgb(255, 244, 208, 136),
                    0.28D);
                Color downloadValueColor = GetInterpolatedColor(
                    DownloadValueColor,
                    Color.FromArgb(255, 255, 214, 96),
                    0.22D);
                Color uploadCaptionColor = GetInterpolatedColor(
                    UploadCaptionColor,
                    Color.FromArgb(255, 198, 255, 194),
                    0.24D);
                Color uploadValueColor = GetInterpolatedColor(
                    UploadValueColor,
                    Color.FromArgb(255, 132, 255, 148),
                    0.18D);

                DrawReadableTrafficText(
                    graphics,
                    this.downloadCaptionLabel.Text,
                    this.captionFont,
                    downloadCaptionColor,
                    this.downloadCaptionLabel.Bounds,
                    false,
                    false);
                DrawReadableTrafficText(
                    graphics,
                    this.downloadValueLabel.Text,
                    this.valueFont,
                    downloadValueColor,
                    this.downloadValueLabel.Bounds,
                    true,
                    true);
                DrawReadableTrafficText(
                    graphics,
                    this.uploadCaptionLabel.Text,
                    this.captionFont,
                    uploadCaptionColor,
                    this.uploadCaptionLabel.Bounds,
                    false,
                    false);
                DrawReadableTrafficText(
                    graphics,
                    this.uploadValueLabel.Text,
                    this.valueFont,
                    uploadValueColor,
                    this.uploadValueLabel.Bounds,
                    true,
                    true);
                return;
            }

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

        private void DrawReadableTrafficInfoPanel(Graphics graphics, Rectangle meterBounds)
        {
            int left = this.ScaleValue(3);
            int right = this.GetLeftSectionRightBoundary(meterBounds, left + this.ScaleValue(44));
            int top = this.ScaleValue(4);
            int bottom = Math.Min(
                this.ClientSize.Height - this.ScaleValue(7),
                this.uploadValueLabel.Bounds.Bottom + this.ScaleValue(5));
            if (right - left < this.ScaleValue(16) || bottom - top < this.ScaleValue(18))
            {
                return;
            }

            RectangleF bounds = new RectangleF(
                AlignToHalfPixel(left),
                AlignToHalfPixel(top),
                Math.Max(1F, right - left),
                Math.Max(1F, bottom - top));
            float radius = Math.Max(this.ScaleFloat(6F), Math.Min(bounds.Width, bounds.Height) * 0.18F);
            GraphicsState state = graphics.Save();

            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;

                int transparencyPercent = this.settings != null ? this.settings.TransparencyPercent : 0;
                int fillAlpha = Math.Min(188, 118 + (int)Math.Round(transparencyPercent * 0.55D));
                int borderAlpha = Math.Min(96, 48 + (int)Math.Round(transparencyPercent * 0.30D));
                int innerAlpha = Math.Min(64, 28 + (int)Math.Round(transparencyPercent * 0.18D));
                int highlightStrongAlpha = Math.Min(92, 66 + (int)Math.Round(transparencyPercent * 0.20D));
                int highlightSoftAlpha = Math.Min(44, 28 + (int)Math.Round(transparencyPercent * 0.14D));
                Color panelBackgroundColor = this.GetPanelBackgroundBaseColor();
                Color panelBorderColor = this.GetPanelBorderBaseColor();

                using (GraphicsPath platePath = CreateRoundedPath(bounds, radius))
                using (GraphicsPath innerPath = CreateRoundedPath(
                    InflateRectangle(bounds, -this.ScaleFloat(1.5F)),
                    Math.Max(2F, radius - this.ScaleFloat(1.5F))))
                using (SolidBrush fillBrush = new SolidBrush(ApplyAlpha(GetInterpolatedColor(panelBackgroundColor, Color.FromArgb(5, 14, 34), 0.40D), (byte)fillAlpha)))
                using (Pen borderPen = new Pen(ApplyAlpha(GetInterpolatedColor(panelBorderColor, Color.FromArgb(164, 215, 255), 0.32D), (byte)borderAlpha), Math.Max(0.8F, this.ScaleFloat(0.8F))))
                using (Pen innerPen = new Pen(Color.FromArgb(innerAlpha, 255, 255, 255), Math.Max(0.6F, this.ScaleFloat(0.6F))))
                using (LinearGradientBrush highlightBrush = new LinearGradientBrush(
                    new PointF(bounds.Left, bounds.Top),
                    new PointF(bounds.Left, bounds.Bottom),
                    Color.Transparent,
                    Color.Transparent))
                {
                    ColorBlend blend = new ColorBlend();
                    blend.Positions = new float[] { 0F, 0.12F, 0.34F, 1F };
                    blend.Colors = new Color[]
                    {
                        Color.FromArgb(highlightStrongAlpha, 220, 238, 255),
                        Color.FromArgb(highlightSoftAlpha, 188, 220, 255),
                        Color.FromArgb(10, 96, 140, 196),
                        Color.Transparent
                    };
                    highlightBrush.InterpolationColors = blend;

                    graphics.FillPath(fillBrush, platePath);
                    graphics.FillPath(highlightBrush, platePath);
                    graphics.DrawPath(borderPen, platePath);
                    graphics.DrawPath(innerPen, innerPath);
                }

                float separatorLeft = bounds.Left + this.ScaleFloat(10F);
                float separatorRight = bounds.Right - this.ScaleFloat(12F);
                float separatorTop = this.downloadValueLabel.Bounds.Bottom + this.ScaleFloat(2F);
                float separatorBottom = this.uploadCaptionLabel.Bounds.Top - this.ScaleFloat(1F);
                float separatorY = (separatorTop + separatorBottom) / 2F;
                using (Pen separatorPen = new Pen(ApplyAlpha(GetInterpolatedColor(this.GetPanelDividerBaseColor(), Color.FromArgb(150, 196, 255), 0.24D), 28), Math.Max(0.8F, this.ScaleFloat(0.8F))))
                {
                    graphics.DrawLine(
                        separatorPen,
                        AlignToHalfPixel(separatorLeft),
                        AlignToHalfPixel(separatorY),
                        AlignToHalfPixel(separatorRight),
                        AlignToHalfPixel(separatorY));
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private void DrawTransparencyAwareInfoPanel(Graphics graphics, Rectangle meterBounds)
        {
            int transparencyPercent = this.settings != null ? this.settings.TransparencyPercent : 0;
            if (transparencyPercent <= 0)
            {
                return;
            }

            int left = this.ScaleValue(3);
            int right = this.GetLeftSectionRightBoundary(meterBounds, left + this.ScaleValue(44));
            int top = Math.Max(0, this.downloadCaptionLabel.Bounds.Top - this.ScaleValue(3));
            int bottom = Math.Min(this.ClientSize.Height, this.uploadValueLabel.Bounds.Bottom + this.ScaleValue(4));
            if (right - left < this.ScaleValue(16) || bottom - top < this.ScaleValue(16))
            {
                return;
            }

            RectangleF bounds = new RectangleF(
                AlignToHalfPixel(left),
                AlignToHalfPixel(top),
                Math.Max(1F, right - left),
                Math.Max(1F, bottom - top));
            float radius = Math.Max(this.ScaleFloat(5.5F), Math.Min(bounds.Width, bounds.Height) * 0.16F);
            int fillAlpha = Math.Min(176, 42 + (int)Math.Round(transparencyPercent * 0.90D));
            int borderAlpha = Math.Min(82, 18 + (int)Math.Round(transparencyPercent * 0.34D));
            int highlightAlpha = Math.Min(62, 14 + (int)Math.Round(transparencyPercent * 0.26D));
            Color panelBackgroundColor = this.GetPanelBackgroundBaseColor();
            Color panelBorderColor = this.GetPanelBorderBaseColor();

            GraphicsState state = graphics.Save();
            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;

                using (GraphicsPath platePath = CreateRoundedPath(bounds, radius))
                using (LinearGradientBrush fillBrush = new LinearGradientBrush(
                    new PointF(bounds.Left, bounds.Top),
                    new PointF(bounds.Right, bounds.Bottom),
                    ApplyAlpha(GetInterpolatedColor(panelBackgroundColor, Color.FromArgb(4, 10, 24), 0.42D), (byte)fillAlpha),
                    ApplyAlpha(GetInterpolatedColor(panelBackgroundColor, Color.FromArgb(8, 18, 36), 0.34D), (byte)Math.Max(0, fillAlpha - 18))))
                using (LinearGradientBrush highlightBrush = new LinearGradientBrush(
                    new PointF(bounds.Left, bounds.Top),
                    new PointF(bounds.Left, bounds.Bottom),
                    Color.Transparent,
                    Color.Transparent))
                using (Pen borderPen = new Pen(ApplyAlpha(GetInterpolatedColor(panelBorderColor, Color.FromArgb(132, 192, 235), 0.28D), (byte)borderAlpha), Math.Max(0.8F, this.ScaleFloat(0.8F))))
                {
                    ColorBlend blend = new ColorBlend();
                    blend.Positions = new float[] { 0F, 0.16F, 0.40F, 1F };
                    blend.Colors = new Color[]
                    {
                        Color.FromArgb(highlightAlpha, 210, 232, 255),
                        Color.FromArgb(Math.Max(0, highlightAlpha - 20), 168, 206, 244),
                        Color.FromArgb(10, 90, 132, 176),
                        Color.Transparent
                    };
                    highlightBrush.InterpolationColors = blend;

                    graphics.FillPath(fillBrush, platePath);
                    graphics.FillPath(highlightBrush, platePath);
                    graphics.DrawPath(borderPen, platePath);
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private void DrawReadableTrafficText(
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
            StringFormat format = allowEllipsis
                ? TrafficEllipsisTextFormat
                : TrafficTextStringFormat;
            int transparencyPercent = this.settings != null ? this.settings.TransparencyPercent : 0;
            bool ultraTransparent = transparencyPercent >= 100;
            bool taskbarIntegrated = this.IsTaskbarIntegrationActive();
            Color textColor = taskbarIntegrated && isPrimaryValue
                ? GetCrispTaskbarIntegratedValueColor(color)
                : color;
            int glowAlpha = taskbarIntegrated
                ? (isPrimaryValue ? 42 : 34)
                : ultraTransparent
                ? (isPrimaryValue ? 68 : 42)
                : Math.Min(isPrimaryValue ? 48 : 28, (isPrimaryValue ? 28 : 16) + (int)Math.Round(transparencyPercent * 0.22D));
            int shadowAlpha = taskbarIntegrated
                ? (isPrimaryValue ? 214 : 176)
                : ultraTransparent
                ? (isPrimaryValue ? 212 : 176)
                : Math.Min(isPrimaryValue ? 164 : 132, (isPrimaryValue ? 112 : 92) + (int)Math.Round(transparencyPercent * 0.52D));
            int outlineAlpha = taskbarIntegrated
                ? (isPrimaryValue ? 206 : 172)
                : ultraTransparent
                ? (isPrimaryValue ? 198 : 166)
                : Math.Min(isPrimaryValue ? 148 : 118, (isPrimaryValue ? 92 : 72) + (int)Math.Round(transparencyPercent * 0.56D));

            using (SolidBrush glowBrush = new SolidBrush(Color.FromArgb(glowAlpha, textColor)))
            using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(shadowAlpha, 4, 10, 24)))
            using (SolidBrush outlineBrush = new SolidBrush(Color.FromArgb(outlineAlpha, 6, 14, 30)))
            using (SolidBrush textBrush = new SolidBrush(textColor))
            {
                graphics.PixelOffsetMode = PixelOffsetMode.Half;
                graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                graphics.DrawString(text, font, glowBrush, OffsetRectangle(stableBounds, 0F, 0.8F), format);
                graphics.DrawString(text, font, outlineBrush, OffsetRectangle(stableBounds, -0.8F, 0F), format);
                graphics.DrawString(text, font, outlineBrush, OffsetRectangle(stableBounds, 0.8F, 0F), format);
                graphics.DrawString(text, font, outlineBrush, OffsetRectangle(stableBounds, 0F, -0.8F), format);
                graphics.DrawString(text, font, outlineBrush, OffsetRectangle(stableBounds, 0F, 0.8F), format);
                graphics.DrawString(text, font, shadowBrush, OffsetRectangle(stableBounds, 0.35F, 1.15F), format);
                graphics.DrawString(text, font, textBrush, stableBounds, format);
            }

            graphics.Restore(state);
        }

        private void DrawMeterValueBalanceSupport(Graphics graphics, Rectangle meterBounds)
        {
            int valueRight = Math.Max(this.downloadValueLabel.Bounds.Right, this.uploadValueLabel.Bounds.Right);
            int supportLeft = Math.Max(this.ScaleValue(40), valueRight - this.ScaleValue(8));
            int supportRight = this.IsRightSectionVisible()
                ? meterBounds.Left + this.ScaleValue(2)
                : this.ClientSize.Width - this.ScaleValue(4);
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

                Color supportColor = GetInterpolatedColor(this.GetPanelBackgroundBaseColor(), MeterTrackInnerColor, 0.14D);
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

        private float GetChannelRingWeight(bool useDownload)
        {
            return useDownload ? 1.12F : 1.00F;
        }

        private float GetChannelSparklineWidthScale(bool useDownload)
        {
            return useDownload ? 1.08F : 1.00F;
        }

        private int GetChannelSparklineAreaAlpha(bool useDownload)
        {
            return useDownload ? 38 : 30;
        }

        private int GetChannelPeakMarkerAlpha(bool useDownload)
        {
            return useDownload ? 188 : 176;
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
                int transparencyPercent = this.settings != null ? this.settings.TransparencyPercent : 0;
                bool ultraTransparent = transparencyPercent >= 100;

                if (!ultraTransparent)
                {
                    using (Pen guidePen = new Pen(Color.FromArgb(90, SparklineGuideColor), Math.Max(1F, this.ScaleFloat(1F))))
                    {
                        graphics.DrawLine(guidePen, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
                        graphics.DrawLine(guidePen, bounds.Left, bounds.Top + 1, bounds.Right, bounds.Top + 1);
                    }
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
                if (!this.IsMiniGraphDisplayMode())
                {
                    PointF[] downloadPoints = CreateSparklinePoints(samples, bounds, peak, true, false);
                    PointF[] uploadPoints = CreateSparklinePoints(samples, bounds, peak, false, false);

                    float lineWidth = Math.Max(
                        ultraTransparent ? this.ScaleFloat(1.65F) : this.ScaleFloat(1.15F),
                        ultraTransparent ? 1.65F : 1.15F);
                    int lineAlpha = ultraTransparent ? 255 : 220;
                    int glowAlpha = ultraTransparent ? 132 : 92;
                    float glowWidth = lineWidth + Math.Max(this.ScaleFloat(0.9F), ultraTransparent ? 0.9F : 0.6F);

                    using (Pen downloadGlowPen = new Pen(Color.FromArgb(glowAlpha, SparklineDownloadColor), glowWidth))
                    using (Pen uploadGlowPen = new Pen(Color.FromArgb(glowAlpha, SparklineUploadColor), glowWidth))
                    using (Pen downloadPen = new Pen(Color.FromArgb(lineAlpha, SparklineDownloadColor), lineWidth))
                    using (Pen uploadPen = new Pen(Color.FromArgb(lineAlpha, SparklineUploadColor), lineWidth))
                    {
                        downloadGlowPen.LineJoin = LineJoin.Round;
                        downloadGlowPen.StartCap = LineCap.Round;
                        downloadGlowPen.EndCap = LineCap.Round;
                        uploadGlowPen.LineJoin = LineJoin.Round;
                        uploadGlowPen.StartCap = LineCap.Round;
                        uploadGlowPen.EndCap = LineCap.Round;
                        downloadPen.LineJoin = LineJoin.Round;
                        downloadPen.StartCap = LineCap.Round;
                        downloadPen.EndCap = LineCap.Round;
                        uploadPen.LineJoin = LineJoin.Round;
                        uploadPen.StartCap = LineCap.Round;
                        uploadPen.EndCap = LineCap.Round;

                        if (downloadPoints.Length >= 2)
                        {
                            graphics.DrawLines(downloadGlowPen, downloadPoints);
                            graphics.DrawLines(downloadPen, downloadPoints);
                        }

                        if (uploadPoints.Length >= 2)
                        {
                            graphics.DrawLines(uploadGlowPen, uploadPoints);
                            graphics.DrawLines(uploadPen, uploadPoints);
                        }
                    }

                    return;
                }

                PointF[] miniGraphDownloadPoints = CreateSparklinePoints(samples, bounds, peak, true, false);
                PointF[] miniGraphUploadPoints = CreateSparklinePoints(samples, bounds, peak, false, false);

                float miniGraphLineWidth = Math.Max(
                    ultraTransparent ? this.ScaleFloat(1.65F) : this.ScaleFloat(1.15F),
                    ultraTransparent ? 1.65F : 1.15F);
                int miniGraphLineAlpha = ultraTransparent ? 255 : 220;
                int miniGraphGlowAlpha = ultraTransparent ? 132 : 92;
                float miniGraphGlowWidth = miniGraphLineWidth + Math.Max(this.ScaleFloat(0.9F), ultraTransparent ? 0.9F : 0.6F);

                using (Pen downloadGlowPen = new Pen(Color.FromArgb(miniGraphGlowAlpha, SparklineDownloadColor), miniGraphGlowWidth))
                using (Pen uploadGlowPen = new Pen(Color.FromArgb(miniGraphGlowAlpha, SparklineUploadColor), miniGraphGlowWidth))
                using (Pen downloadPen = new Pen(Color.FromArgb(miniGraphLineAlpha, SparklineDownloadColor), miniGraphLineWidth))
                using (Pen uploadPen = new Pen(Color.FromArgb(miniGraphLineAlpha, SparklineUploadColor), miniGraphLineWidth))
                {
                    downloadGlowPen.LineJoin = LineJoin.Round;
                    downloadGlowPen.StartCap = LineCap.Round;
                    downloadGlowPen.EndCap = LineCap.Round;
                    uploadGlowPen.LineJoin = LineJoin.Round;
                    uploadGlowPen.StartCap = LineCap.Round;
                    uploadGlowPen.EndCap = LineCap.Round;
                    downloadPen.LineJoin = LineJoin.Round;
                    downloadPen.StartCap = LineCap.Round;
                    downloadPen.EndCap = LineCap.Round;
                    uploadPen.LineJoin = LineJoin.Round;
                    uploadPen.StartCap = LineCap.Round;
                    uploadPen.EndCap = LineCap.Round;

                    if (miniGraphDownloadPoints.Length >= 2)
                    {
                        graphics.DrawLines(downloadGlowPen, miniGraphDownloadPoints);
                        graphics.DrawLines(downloadPen, miniGraphDownloadPoints);
                    }

                    if (miniGraphUploadPoints.Length >= 2)
                    {
                        graphics.DrawLines(uploadGlowPen, miniGraphUploadPoints);
                        graphics.DrawLines(uploadPen, miniGraphUploadPoints);
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
            PanelSkinDefinition definition = this.GetCurrentPanelSkinDefinition();
            if (definition != null &&
                definition.SparklineBounds.HasValue &&
                this.IsBothSectionsVisible())
            {
                return this.ScaleSkinRectangle(definition.SparklineBounds.Value);
            }

            int left = this.ScaleValue(this.IsMiniGraphDisplayMode() ? (int)MiniGraphSparklineLeft : 8);
            int top = this.ScaleValue(this.IsMiniGraphDisplayMode() ? (int)MiniGraphSparklineTop : 46);
            if (this.IsTaskbarIntegrationActive())
            {
                top = Math.Max(0, top - this.ScaleValue(4));
            }

            int width = this.IsRightSectionVisible()
                ? Math.Max(12, meterBounds.Left - left - this.ScaleValue(4))
                : Math.Max(18, this.ClientSize.Width - left - this.ScaleValue(6));
            int height = Math.Max(
                this.IsMiniGraphDisplayMode() ? 8 : 4,
                this.ScaleValue(this.IsMiniGraphDisplayMode() ? (int)MiniGraphSparklineHeight : 7));
            return new Rectangle(left, top, width, height);
        }

        private int GetLeftSectionRightBoundary(Rectangle meterBounds, int minimumRight)
        {
            int candidateRight = this.IsRightSectionVisible()
                ? meterBounds.Left - this.ScaleValue(5)
                : this.ClientSize.Width - this.ScaleValue(4);
            return Math.Max(minimumRight, candidateRight);
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
            else if (history.Length <= this.GetCurrentOverlaySparklinePointCount())
            {
                snapshot = history;
            }
            else
            {
                int overlaySparklinePointCount = this.GetCurrentOverlaySparklinePointCount();
                snapshot = new TrafficHistorySample[overlaySparklinePointCount];
                Array.Copy(
                    history,
                    history.Length - overlaySparklinePointCount,
                    snapshot,
                    0,
                    overlaySparklinePointCount);
            }

            this.cachedOverlaySparklineHistoryVersion = this.trafficHistoryVersion;
            this.cachedOverlaySparklineSamples = snapshot;
            return snapshot;
        }

        private static PointF[] CreateSparklinePoints(
            TrafficHistorySample[] samples,
            Rectangle bounds,
            double peakBytesPerSecond,
            bool useDownload,
            bool useMiniGraphLayout)
        {
            if (samples == null || samples.Length == 0)
            {
                return Array.Empty<PointF>();
            }

            PointF[] points = new PointF[samples.Length];
            float leftInset = useMiniGraphLayout
                ? Math.Min(Math.Max(0F, MiniGraphSparklineContentLeftInset), Math.Max(0F, bounds.Width - 2F))
                : 0F;
            float width = Math.Max(1F, bounds.Width - 1F - leftInset);
            float height = Math.Max(1F, bounds.Height - 1F);
            double peak = Math.Max(1D, peakBytesPerSecond);

            for (int i = 0; i < samples.Length; i++)
            {
                double value = useDownload
                    ? samples[i].DownloadBytesPerSecond
                    : samples[i].UploadBytesPerSecond;
                double ratio = Math.Max(0D, Math.Min(1D, value / peak));
                float x = bounds.Left + leftInset + ((samples.Length == 1)
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

        private static GraphicsPath CreateSparklineFillPath(PointF[] points, Rectangle bounds, bool smoothLeadingEdge = false)
        {
            if (points == null || points.Length < 2)
            {
                return null;
            }

            GraphicsPath path = new GraphicsPath();
            List<PointF> polygonPoints = new List<PointF>(points.Length + 2);
            float baselineY = AlignToHalfPixel(bounds.Bottom - 1F);
            if (smoothLeadingEdge)
            {
                float firstX = points[0].X;
                float secondX = points.Length >= 2 ? points[1].X : firstX;
                float shoulderOffset = Math.Max(1F, Math.Min(3F, (secondX - firstX) * 0.5F));
                float startBaseX = AlignToHalfPixel(Math.Max(bounds.Left, firstX - shoulderOffset));
                polygonPoints.Add(new PointF(startBaseX, baselineY));
            }

            polygonPoints.AddRange(points);
            polygonPoints.Add(new PointF(points[points.Length - 1].X, baselineY));
            polygonPoints.Add(new PointF(smoothLeadingEdge ? polygonPoints[0].X : points[0].X, baselineY));
            path.AddPolygon(polygonPoints.ToArray());
            path.CloseFigure();
            return path;
        }

        private void DrawSparklinePeakMarker(
            Graphics graphics,
            Rectangle bounds,
            double peakBytesPerSecond,
            double peakHoldBytesPerSecond,
            Color color,
            bool useDownload)
        {
            if (peakBytesPerSecond <= 0D || peakHoldBytesPerSecond <= 0D)
            {
                return;
            }

            float y = GetSparklineValueY(bounds, peakHoldBytesPerSecond, peakBytesPerSecond);
            float right = bounds.Right - this.ScaleFloat(1.5F);
            float width = Math.Max(this.ScaleFloat(useDownload ? 9F : 7F), useDownload ? 9F : 7F);
            float left = right - width;
            float lineWidth = Math.Max(this.ScaleFloat(useDownload ? 1.6F : 1.2F), useDownload ? 1.6F : 1.2F);
            int alpha = this.GetChannelPeakMarkerAlpha(useDownload);

            using (Pen markerPen = new Pen(Color.FromArgb(alpha, color), lineWidth))
            using (Pen glowPen = new Pen(
                Color.FromArgb(Math.Max(72, alpha / 2), color),
                lineWidth + this.ScaleFloat(0.8F)))
            {
                markerPen.StartCap = LineCap.Round;
                markerPen.EndCap = LineCap.Round;
                glowPen.StartCap = LineCap.Round;
                glowPen.EndCap = LineCap.Round;
                graphics.DrawLine(glowPen, AlignToHalfPixel(left), AlignToHalfPixel(y), AlignToHalfPixel(right), AlignToHalfPixel(y));
                graphics.DrawLine(markerPen, AlignToHalfPixel(left), AlignToHalfPixel(y), AlignToHalfPixel(right), AlignToHalfPixel(y));
            }
        }

        private static float GetSparklineValueY(Rectangle bounds, double bytesPerSecond, double peakBytesPerSecond)
        {
            double peak = Math.Max(1D, peakBytesPerSecond);
            double ratio = Math.Max(0D, Math.Min(1D, bytesPerSecond / peak));
            float height = Math.Max(1F, bounds.Height - 1F);
            float y = bounds.Bottom - 1 - (float)(ratio * height);
            return AlignToHalfPixel(y);
        }

        private void DrawTrafficText(
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
            int transparencyPercent = this.settings != null ? this.settings.TransparencyPercent : 0;
            bool ultraTransparent = transparencyPercent >= 100;
            bool taskbarIntegrated = this.IsTaskbarIntegrationActive();
            Color textColor = taskbarIntegrated && isPrimaryValue
                ? GetCrispTaskbarIntegratedValueColor(color)
                : color;
            int contrastAlpha = taskbarIntegrated
                ? (isPrimaryValue ? 174 : 138)
                : ultraTransparent
                ? (isPrimaryValue ? 164 : 132)
                : Math.Min(
                    isPrimaryValue ? 112 : 86,
                    (isPrimaryValue ? 32 : 20) + (int)Math.Round(transparencyPercent * (isPrimaryValue ? 0.80D : 0.66D)));

            using (SolidBrush contrastBrush = new SolidBrush(Color.FromArgb(
                contrastAlpha,
                BackgroundBlue.R,
                BackgroundBlue.G,
                BackgroundBlue.B)))
            using (SolidBrush textBrush = new SolidBrush(textColor))
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

        private static Color GetCrispTaskbarIntegratedValueColor(Color color)
        {
            double whiteBoost = color.ToArgb() == DownloadValueColor.ToArgb()
                ? 0.44D
                : 0.28D;
            return GetInterpolatedColor(color, Color.White, whiteBoost);
        }

        private double GetVisualizedFillRatioForCurrentDownload()
        {
            return this.GetVisualizedFillRatio(this.GetCurrentDownloadFillRatio(), true);
        }

        private double GetVisualizedFillRatioForCurrentUpload()
        {
            return this.GetVisualizedFillRatio(this.GetCurrentUploadFillRatio(), false);
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

        private bool ShouldAnimateVisualEffects()
        {
            double ringNoiseFloorBytesPerSecond = this.GetRingDisplayNoiseFloorBytesPerSecond();
            double ringMotionThreshold = ringNoiseFloorBytesPerSecond * 0.25D;
            double holdMotionThreshold = ringNoiseFloorBytesPerSecond * 0.20D;

            return Math.Abs(this.ringDisplayDownloadBytesPerSecond - this.latestDownloadBytesPerSecond) > ringMotionThreshold ||
                Math.Abs(this.ringDisplayUploadBytesPerSecond - this.latestUploadBytesPerSecond) > ringMotionThreshold ||
                this.peakHoldDownloadBytesPerSecond > this.displayedDownloadBytesPerSecond + holdMotionThreshold ||
                this.peakHoldUploadBytesPerSecond > this.displayedUploadBytesPerSecond + holdMotionThreshold ||
                this.ShouldAnimateActivityBorder() ||
                this.ShouldAnimateMeterGloss();
        }

        private bool ShouldAnimateActivityBorder()
        {
            return this.GetActivityBorderIntensity() > ActivityBorderAnimationThresholdRatio;
        }

        private bool ShouldAnimateMeterGloss()
        {
            return this.settings.RotatingMeterGlossEnabled &&
                (this.GetCurrentDownloadFillRatio() > MeterGlossAnimationThresholdRatio ||
                 this.GetCurrentUploadFillRatio() > MeterGlossAnimationThresholdRatio);
        }

        private void UpdatePeakHoldRates(double downloadBytesPerSecond, double uploadBytesPerSecond, DateTime nowUtc)
        {
            double safeDownloadBytesPerSecond = Math.Max(0D, downloadBytesPerSecond);
            double safeUploadBytesPerSecond = Math.Max(0D, uploadBytesPerSecond);

            if (safeDownloadBytesPerSecond >= this.peakHoldDownloadBytesPerSecond)
            {
                this.peakHoldDownloadBytesPerSecond = safeDownloadBytesPerSecond;
                this.peakHoldDownloadCapturedUtc = nowUtc;
            }

            if (safeUploadBytesPerSecond >= this.peakHoldUploadBytesPerSecond)
            {
                this.peakHoldUploadBytesPerSecond = safeUploadBytesPerSecond;
                this.peakHoldUploadCapturedUtc = nowUtc;
            }
        }

        private void AdvanceVisualAnimations()
        {
            DateTime nowUtc = DateTime.UtcNow;
            double elapsedSeconds = this.lastAnimationAdvanceUtc == DateTime.MinValue
                ? Math.Max(0.001D, this.animationTimer.Interval / 1000D)
                : Math.Max(0.001D, (nowUtc - this.lastAnimationAdvanceUtc).TotalSeconds);
            this.lastAnimationAdvanceUtc = nowUtc;

            this.UpdateRingDisplayRates(this.latestDownloadBytesPerSecond, this.latestUploadBytesPerSecond);
            this.DecayPeakHoldRates(nowUtc, elapsedSeconds);
            this.AdvanceMeterGlossRotation(elapsedSeconds);
            this.AdvanceActivityBorderRotation(elapsedSeconds);
            this.UpdateAnimationTimerState();
        }

        private void AdvanceActivityBorderRotation(double elapsedSeconds)
        {
            double intensity = this.GetActivityBorderIntensity();
            double fadeRatio = GetActivityBorderFadeRatio(intensity);
            if (fadeRatio <= 0D)
            {
                return;
            }

            double direction = this.GetActivityBorderDirection();
            double degreesPerSecond = (ActivityBorderBaseRotationDegreesPerSecond +
                (SmoothStep(intensity) * (ActivityBorderMaxRotationDegreesPerSecond - ActivityBorderBaseRotationDegreesPerSecond))) *
                fadeRatio;
            this.activityBorderRotationDegrees += direction * degreesPerSecond * elapsedSeconds;
            while (this.activityBorderRotationDegrees >= 360D)
            {
                this.activityBorderRotationDegrees -= 360D;
            }

            while (this.activityBorderRotationDegrees < 0D)
            {
                this.activityBorderRotationDegrees += 360D;
            }
        }

        private void AdvanceMeterGlossRotation(double elapsedSeconds)
        {
            if (!this.settings.RotatingMeterGlossEnabled)
            {
                return;
            }

            double downloadInfluence = this.GetVisualizedFillRatioForCurrentDownload();
            double uploadInfluence = this.GetVisualizedFillRatioForCurrentUpload();
            double netInfluence = downloadInfluence - uploadInfluence;
            if (Math.Abs(netInfluence) <= 0.0001D)
            {
                return;
            }

            double degreesPerSecond = netInfluence >= 0D
                ? MeterGlossClockwiseMaxRotationDegreesPerSecond
                : MeterGlossCounterClockwiseMaxRotationDegreesPerSecond;
            this.meterGlossRotationDegrees += netInfluence * degreesPerSecond * elapsedSeconds;
            while (this.meterGlossRotationDegrees >= 360D)
            {
                this.meterGlossRotationDegrees -= 360D;
            }

            while (this.meterGlossRotationDegrees < 0D)
            {
                this.meterGlossRotationDegrees += 360D;
            }
        }

        private void DecayPeakHoldRates(DateTime nowUtc, double elapsedSeconds)
        {
            this.peakHoldDownloadBytesPerSecond = this.DecayPeakHoldRate(
                this.peakHoldDownloadBytesPerSecond,
                this.displayedDownloadBytesPerSecond,
                this.peakHoldDownloadCapturedUtc,
                nowUtc,
                elapsedSeconds);
            this.peakHoldUploadBytesPerSecond = this.DecayPeakHoldRate(
                this.peakHoldUploadBytesPerSecond,
                this.displayedUploadBytesPerSecond,
                this.peakHoldUploadCapturedUtc,
                nowUtc,
                elapsedSeconds);
        }

        private double DecayPeakHoldRate(
            double currentPeakHoldBytesPerSecond,
            double baselineBytesPerSecond,
            DateTime capturedUtc,
            DateTime nowUtc,
            double elapsedSeconds)
        {
            double safePeakHoldBytesPerSecond = Math.Max(0D, currentPeakHoldBytesPerSecond);
            double safeBaselineBytesPerSecond = Math.Max(0D, baselineBytesPerSecond);
            if (safePeakHoldBytesPerSecond <= safeBaselineBytesPerSecond)
            {
                return safeBaselineBytesPerSecond;
            }

            if (capturedUtc != DateTime.MinValue &&
                (nowUtc - capturedUtc).TotalSeconds <= PeakHoldReleaseDelaySeconds)
            {
                return safePeakHoldBytesPerSecond;
            }

            double decayAmount = Math.Max(
                this.GetRingDisplayNoiseFloorBytesPerSecond(),
                safePeakHoldBytesPerSecond * PeakHoldDecayPerSecond * elapsedSeconds);
            return Math.Max(safeBaselineBytesPerSecond, safePeakHoldBytesPerSecond - decayAmount);
        }

        private void UpdateAnimationTimerState()
        {
            if (this.animationTimer == null)
            {
                return;
            }

            bool shouldAnimate = this.Visible &&
                (this.ShouldAnimateCenterArrows() || this.ShouldAnimateVisualEffects());
            if (this.animationTimer.Enabled == shouldAnimate)
            {
                return;
            }

            this.animationTimer.Enabled = shouldAnimate;
            this.lastRenderedAnimationFrame = -1;
        }

        private int GetAnimationFrameIndex()
        {
            if (!this.ShouldAnimateCenterArrows() && !this.ShouldAnimateVisualEffects())
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
            double visualDownloadFillRatio = this.GetVisualizedFillRatio(downloadFillRatio, true);
            double visualUploadFillRatio = this.GetVisualizedFillRatio(uploadFillRatio, false);
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
            this.ApplyDpiLayout(dpi, true);
        }

        private void ApplyDpiLayout(int dpi, bool refreshIfVisible)
        {
            dpi = DpiHelper.NormalizeDpi(dpi);
            this.currentDpi = dpi;
            PanelSkinDefinition definition = this.GetCurrentPanelSkinDefinition();

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

            Size baseClientSize = definition != null && definition.ClientSize.HasValue
                ? definition.ClientSize.Value
                : new Size(BaseClientWidth, BaseClientHeight);
            baseClientSize = this.GetBaseClientSizeForSection(
                this.GetDisplayModeAdjustedBaseClientSize(baseClientSize),
                this.GetEffectivePopupSectionMode());
            Size clientSize = this.ScaleSkinSize(baseClientSize);
            this.ClientSize = clientSize;
            this.MinimumSize = clientSize;
            this.MaximumSize = clientSize;
            this.Font = this.formFont;

            Rectangle defaultDownloadCaptionBounds = new Rectangle(BaseCaptionX, BaseDownloadCaptionY, BaseCaptionWidth, BaseCaptionHeight);
            Rectangle defaultDownloadValueBounds = new Rectangle(BaseDownloadValueX, BaseDownloadValueY, BaseValueWidth, BaseValueHeight);
            Rectangle defaultUploadCaptionBounds = new Rectangle(BaseCaptionX, BaseUploadCaptionY, BaseCaptionWidth, BaseCaptionHeight);
            Rectangle defaultUploadValueBounds = new Rectangle(BaseUploadValueX, BaseUploadValueY, BaseValueWidth, BaseValueHeight);

            Rectangle scaledDownloadCaptionBounds = this.GetCaptionBoundsForCurrentSection(
                defaultDownloadCaptionBounds,
                definition != null ? definition.DownloadCaptionBounds : (Rectangle?)null);
            Rectangle scaledDownloadValueBounds = this.GetValueBoundsForCurrentSection(
                defaultDownloadValueBounds,
                definition != null ? definition.DownloadValueBounds : (Rectangle?)null);
            Rectangle scaledUploadCaptionBounds = this.GetCaptionBoundsForCurrentSection(
                defaultUploadCaptionBounds,
                definition != null ? definition.UploadCaptionBounds : (Rectangle?)null);
            Rectangle scaledUploadValueBounds = this.GetValueBoundsForCurrentSection(
                defaultUploadValueBounds,
                definition != null ? definition.UploadValueBounds : (Rectangle?)null);

            if (this.IsTaskbarIntegrationActive() && !scaledUploadValueBounds.IsEmpty)
            {
                scaledUploadValueBounds.Offset(0, -this.ScaleValue(2));
            }

            this.downloadCaptionLabel.Font = this.captionFont;
            this.downloadCaptionLabel.Location = scaledDownloadCaptionBounds.Location;
            this.downloadCaptionLabel.Size = scaledDownloadCaptionBounds.Size;

            this.downloadValueLabel.Font = this.valueFont;
            this.downloadValueLabel.Location = scaledDownloadValueBounds.Location;
            this.downloadValueLabel.Size = scaledDownloadValueBounds.Size;

            this.uploadCaptionLabel.Font = this.captionFont;
            this.uploadCaptionLabel.Location = scaledUploadCaptionBounds.Location;
            this.uploadCaptionLabel.Size = scaledUploadCaptionBounds.Size;

            this.uploadValueLabel.Font = this.valueFont;
            this.uploadValueLabel.Location = scaledUploadValueBounds.Location;
            this.uploadValueLabel.Size = scaledUploadValueBounds.Size;

            this.ResumeLayout(false);
            this.UpdateWindowRegion();
            this.staticSurfaceDirty = true;

            if (refreshIfVisible && this.Visible)
            {
                this.RefreshVisualSurface();
            }

            DisposeFont(previousFormFont);
            DisposeFont(previousCaptionFont);
            DisposeFont(previousValueFont);
        }

        private Size GetBaseClientSizeForCurrentSection(Size baseClientSize)
        {
            return this.GetBaseClientSizeForSection(
                this.GetDisplayModeAdjustedBaseClientSize(baseClientSize),
                this.GetEffectivePopupSectionMode());
        }

        private Size GetBaseClientSizeForSection(Size baseClientSize, PopupSectionMode popupSectionMode)
        {
            switch (popupSectionMode)
            {
                case PopupSectionMode.LeftOnly:
                    return new Size(
                        Math.Min(baseClientSize.Width, BaseLeftOnlyClientWidth),
                        baseClientSize.Height);
                case PopupSectionMode.RightOnly:
                    return new Size(
                        Math.Min(baseClientSize.Width, BaseRightOnlyClientWidth),
                        baseClientSize.Height);
                default:
                    return baseClientSize;
            }
        }

        private Size GetDisplayModeAdjustedBaseClientSize(Size baseClientSize)
        {
            return baseClientSize;
        }

        private int GetPanelOuterInset()
        {
            if (this.IsMiniSoftDisplayMode())
            {
                return Math.Max(1, BaseOuterInset - 1);
            }

            return BaseOuterInset;
        }

        private Size GetScaledClientSizeForSection(PopupSectionMode popupSectionMode)
        {
            PanelSkinDefinition definition = this.GetCurrentPanelSkinDefinition();
            Size baseClientSize = definition != null && definition.ClientSize.HasValue
                ? definition.ClientSize.Value
                : new Size(BaseClientWidth, BaseClientHeight);
            return this.ScaleSkinSize(
                this.GetBaseClientSizeForSection(
                    this.GetDisplayModeAdjustedBaseClientSize(baseClientSize),
                    popupSectionMode));
        }

        private bool NeedsCurrentDpiLayout()
        {
            PanelSkinDefinition definition = this.GetCurrentPanelSkinDefinition();
            Size baseClientSize = definition != null && definition.ClientSize.HasValue
                ? definition.ClientSize.Value
                : new Size(BaseClientWidth, BaseClientHeight);
            baseClientSize = this.GetBaseClientSizeForCurrentSection(baseClientSize);
            Size desiredClientSize = this.ScaleSkinSize(baseClientSize);
            return this.ClientSize != desiredClientSize ||
                this.MinimumSize != desiredClientSize ||
                this.MaximumSize != desiredClientSize;
        }

        private bool NeedsTaskbarIntegrationLayoutRefresh(TaskbarIntegrationSnapshot snapshot)
        {
            if (snapshot == null || snapshot.IsHidden)
            {
                if (this.lastAppliedTaskbarThickness != -1)
                {
                    this.lastAppliedTaskbarThickness = -1;
                }

                return false;
            }

            int currentTaskbarThickness = snapshot.IsVertical
                ? snapshot.Bounds.Width
                : snapshot.Bounds.Height;
            if (this.lastAppliedTaskbarThickness == currentTaskbarThickness)
            {
                return false;
            }

            this.lastAppliedTaskbarThickness = currentTaskbarThickness;
            return true;
        }

        private Rectangle GetCaptionBoundsForCurrentSection(Rectangle defaultBounds, Rectangle? overrideBounds)
        {
            PopupSectionMode popupSectionMode = this.GetEffectivePopupSectionMode();
            if (popupSectionMode == PopupSectionMode.RightOnly)
            {
                return Rectangle.Empty;
            }

            if (popupSectionMode == PopupSectionMode.Both)
            {
                return this.GetScaledSkinBounds(defaultBounds, overrideBounds);
            }

            Rectangle scaledBounds = this.ScaleSkinRectangle(defaultBounds);
            scaledBounds.Width = Math.Max(scaledBounds.Width, this.ScaleValue(BaseCaptionWidth));
            return scaledBounds;
        }

        private Rectangle GetValueBoundsForCurrentSection(Rectangle defaultBounds, Rectangle? overrideBounds)
        {
            PopupSectionMode popupSectionMode = this.GetEffectivePopupSectionMode();
            if (popupSectionMode == PopupSectionMode.RightOnly)
            {
                return Rectangle.Empty;
            }

            if (popupSectionMode == PopupSectionMode.Both)
            {
                return this.GetScaledSkinBounds(defaultBounds, overrideBounds);
            }

            Rectangle scaledBounds = this.ScaleSkinRectangle(defaultBounds);
            int rightPadding = this.ScaleValue(6);
            int width = Math.Max(
                this.ScaleValue(22),
                this.ClientSize.Width - scaledBounds.Left - rightPadding);
            return new Rectangle(scaledBounds.Left, scaledBounds.Top, width, scaledBounds.Height);
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
            double popupScaleFactor = Math.Max(0.5D, this.settings.PopupScalePercent / 100D);
            if (!this.IsTaskbarIntegrationActive())
            {
                return popupScaleFactor;
            }

            return popupScaleFactor * this.GetTaskbarIntegrationScaleFactor();
        }

        private double GetTaskbarIntegrationScaleFactor()
        {
            if (this.activeTaskbarSnapshot == null)
            {
                return 1D;
            }

            int taskbarThickness = this.activeTaskbarSnapshot.IsVertical
                ? this.activeTaskbarSnapshot.Bounds.Width
                : this.activeTaskbarSnapshot.Bounds.Height;
            int scaledTaskbarInsetThickness = Math.Max(1, DpiHelper.Scale(TaskbarInsetThickness, this.currentDpi));
            int usableTaskbarThickness = Math.Max(
                1,
                taskbarThickness - (2 * scaledTaskbarInsetThickness));
            int baseThickness = DpiHelper.Scale(
                this.activeTaskbarSnapshot.IsVertical ? BaseClientWidth : BaseClientHeight,
                this.currentDpi);
            if (baseThickness <= 0)
            {
                return 1D;
            }

            double scaleFactor = Math.Max(0.42D, Math.Min(1.85D, usableTaskbarThickness / (double)baseThickness));
            return scaleFactor;
        }

        private bool IsTaskbarIntegrationActive()
        {
            return this.settings != null &&
                this.settings.TaskbarIntegrationEnabled &&
                this.activeTaskbarSnapshot != null &&
                !this.activeTaskbarSnapshot.IsHidden;
        }

        private Point GetPopupScaleAdjustedLocation(Rectangle previousBounds)
        {
            if (previousBounds.Width <= 0 || previousBounds.Height <= 0)
            {
                return this.Location;
            }

            Point previousCenter = GetRectangleCenter(previousBounds);
            long adjustedX = (long)previousCenter.X - (this.Width / 2L);
            long adjustedY = (long)previousCenter.Y - (this.Height / 2L);
            return new Point(
                ClampToInt32(adjustedX),
                ClampToInt32(adjustedY));
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
            if (this.settings.TaskbarIntegrationEnabled)
            {
                TaskbarIntegrationSnapshot snapshot = this.activeTaskbarSnapshot;
                if (snapshot == null && !this.TryCaptureTaskbarIntegrationSnapshot(out snapshot))
                {
                    this.taskbarIntegrationPreferredLocation = preferredLocation;
                    this.Location = this.GetVisiblePopupLocationForManualDrag(preferredLocation, cursorPosition);
                    return;
                }

                bool snappedToTaskbar;
                Point dragLocation = this.GetVisiblePopupLocationForManualDrag(
                    preferredLocation,
                    cursorPosition,
                    snapshot,
                    out snappedToTaskbar);
                this.taskbarIntegrationPreferredLocation = preferredLocation;

                if (snappedToTaskbar)
                {
                    Rectangle placementBounds;
                    if (this.TryGetTaskbarPlacementBounds(snapshot, out placementBounds))
                    {
                        this.Location = placementBounds.Location;
                        return;
                    }
                }

                this.Location = dragLocation;
                return;
            }

            this.Location = this.GetVisiblePopupLocationForManualDrag(preferredLocation, cursorPosition);
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
                if (shouldCommitLocation)
                {
                    this.TryToggleTaskbarSectionModeFromLeftDrag();
                }

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
        private bool allowClose;
        private bool calibrationDialogSizeLocked;

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
                this.currentSettings.ActivityBorderGlowEnabled);

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
                    "Bereit für die Kalibration. Bitte mit 'Starten' beginnen und später mit 'Speichern' bestätigen oder mit 'Abbrechen' schließen.");
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
                "Kalibration läuft... {0} / 30 s | DL {1} | UL {2}",
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
                this.currentSettings.ActivityBorderGlowEnabled);
            this.statusLabel.Text = UiLanguage.Format(
                "Calibration.CompletedStatus",
                "Kalibration abgeschlossen. DL {0} | UL {1}. Mit 'Speichern' bestätigen.",
                TrafficPopupForm.FormatSpeed(storedDownloadPeak),
                TrafficPopupForm.FormatSpeed(storedUploadPeak));
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
                this.currentSettings.ActivityBorderGlowEnabled);
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

    internal sealed class TrafficUsageMeasuredEventArgs : EventArgs
    {
        public TrafficUsageMeasuredEventArgs(long downloadBytes, long uploadBytes)
        {
            this.DownloadBytes = Math.Max(0L, downloadBytes);
            this.UploadBytes = Math.Max(0L, uploadBytes);
        }

        public long DownloadBytes { get; private set; }

        public long UploadBytes { get; private set; }
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
            int compactSpacing = Math.Max(8, this.Font.Height / 2);
            int sectionSpacing = Math.Max(12, this.Font.Height - 1);
            int contentTop = Math.Max(14, this.Font.Height);
            int horizontalPadding = Math.Max(16, this.Font.Height + 4);
            int bottomPadding = Math.Max(18, compactSpacing + 10);
            int infoMinimumHeight = Math.Max(48, (this.Font.Height * 2) + (compactSpacing * 2));
            int languageLabelTextHorizontalPadding = Math.Max(8, compactSpacing + 2);
            int languageLabelTextVerticalPadding = Math.Max(4, (compactSpacing / 2) + 1);
            int languageLabelMinimumHeight = Math.Max(24, this.Font.Height + languageLabelTextVerticalPadding + 4);
            int languageLabelMinimumWidth = Math.Max(120, (this.Font.Height * 6) + languageLabelTextHorizontalPadding + 18);
            int comboBoxMinimumHeight = Math.Max(30, this.languageComboBox != null ? this.languageComboBox.PreferredHeight + 6 : this.Font.Height + 14);
            int saveButtonHorizontalPadding = Math.Max(10, compactSpacing + 4);
            int saveButtonVerticalPadding = Math.Max(4, (compactSpacing / 2) + 1);
            int saveButtonTextHorizontalPadding = Math.Max(24, (saveButtonHorizontalPadding * 2) + 4);
            int saveButtonTextVerticalPadding = Math.Max(12, (saveButtonVerticalPadding * 2) + 4);
            int saveButtonMinimumWidth = Math.Max(112, (this.Font.Height * 5) + saveButtonTextHorizontalPadding);
            int saveButtonMinimumHeight = Math.Max(40, this.Font.Height + saveButtonTextVerticalPadding);
            int baseContentWidth = Math.Max(Math.Max(388, (this.Font.Height * 18) + 64), languageLabelMinimumWidth + 200);
            int dialogMinimumWidth = Math.Max(420, (horizontalPadding * 2) + baseContentWidth);
            int baseContentHeight =
                contentTop +
                infoMinimumHeight +
                sectionSpacing +
                languageLabelMinimumHeight +
                compactSpacing +
                comboBoxMinimumHeight +
                sectionSpacing +
                saveButtonMinimumHeight +
                bottomPadding;
            int dialogMinimumHeight = Math.Max(224, baseContentHeight);
            this.ClientSize = new Size(dialogMinimumWidth, dialogMinimumHeight);
            int contentLeft = horizontalPadding;
            int contentRight = this.ClientSize.Width - horizontalPadding;
            int contentWidth = contentRight - contentLeft;

            Label infoLabel = new Label();
            infoLabel.AutoSize = false;
            infoLabel.Text = UiLanguage.Get(
                "StartupLanguage.Info",
                "Bitte wähle zuerst die Programmsprache aus. Nach dem Speichern wird das Fenster geschlossen und das Programm startet weiter.");
            Size infoTextSize = TextRenderer.MeasureText(
                infoLabel.Text,
                infoLabel.Font,
                new Size(contentWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            int infoHeight = Math.Max(infoMinimumHeight, infoTextSize.Height);
            infoLabel.SetBounds(contentLeft, contentTop, contentWidth, infoHeight);

            Label languageLabel = new Label();
            languageLabel.AutoSize = false;
            languageLabel.Text = UiLanguage.Get("StartupLanguage.Label", "Sprache");
            Size languageLabelTextSize = TextRenderer.MeasureText(
                languageLabel.Text,
                languageLabel.Font,
                new Size(contentWidth, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            int languageLabelTop = infoLabel.Bottom + sectionSpacing;
            int languageLabelHeight = Math.Max(languageLabelMinimumHeight, languageLabelTextSize.Height + languageLabelTextVerticalPadding);
            int languageLabelWidth = Math.Max(languageLabelMinimumWidth, languageLabelTextSize.Width + languageLabelTextHorizontalPadding);
            languageLabel.SetBounds(contentLeft, languageLabelTop, languageLabelWidth, languageLabelHeight);

            this.languageComboBox = new ComboBox();
            this.languageComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            int comboBoxTop = languageLabel.Bottom + compactSpacing;
            int comboBoxHeight = Math.Max(comboBoxMinimumHeight, this.languageComboBox.PreferredHeight + 6);
            this.languageComboBox.SetBounds(contentLeft, comboBoxTop, contentWidth, comboBoxHeight);

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
            this.saveButton.Padding = new Padding(
                saveButtonHorizontalPadding,
                saveButtonVerticalPadding,
                saveButtonHorizontalPadding,
                saveButtonVerticalPadding);
            this.saveButton.MinimumSize = new Size(saveButtonMinimumWidth, saveButtonMinimumHeight);
            this.saveButton.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            Size saveButtonSize = this.saveButton.GetPreferredSize(Size.Empty);
            int saveButtonWidth = Math.Max(this.saveButton.MinimumSize.Width, saveButtonSize.Width);
            int saveButtonHeight = Math.Max(this.saveButton.MinimumSize.Height, saveButtonSize.Height);
            int buttonTop = this.languageComboBox.Bottom + sectionSpacing;
            this.ClientSize = new Size(
                this.ClientSize.Width,
                Math.Max(this.ClientSize.Height, buttonTop + saveButtonHeight + bottomPadding));
            this.saveButton.SetBounds(
                contentRight - saveButtonWidth,
                buttonTop,
                saveButtonWidth,
                saveButtonHeight);
            int requiredClientWidthFromControls = Math.Max(
                Math.Max(
                    languageLabel.Right + horizontalPadding,
                    this.languageComboBox.Right + horizontalPadding),
                this.saveButton.Right + horizontalPadding);
            if (requiredClientWidthFromControls > this.ClientSize.Width)
            {
                this.ClientSize = new Size(requiredClientWidthFromControls, this.ClientSize.Height);
                contentRight = this.ClientSize.Width - horizontalPadding;
                contentWidth = contentRight - contentLeft;
                infoLabel.SetBounds(contentLeft, infoLabel.Top, contentWidth, infoLabel.Height);
                this.languageComboBox.SetBounds(contentLeft, this.languageComboBox.Top, contentWidth, this.languageComboBox.Height);
                this.saveButton.SetBounds(
                    contentRight - saveButtonWidth,
                    this.saveButton.Top,
                    saveButtonWidth,
                    saveButtonHeight);
            }
            int requiredClientHeightFromVisibleControls = Math.Max(
                Math.Max(
                    infoLabel.Bottom,
                    languageLabel.Bottom),
                Math.Max(
                    this.languageComboBox.Bottom,
                    this.saveButton.Bottom)) + bottomPadding;
            if (requiredClientHeightFromVisibleControls != this.ClientSize.Height)
            {
                this.ClientSize = new Size(this.ClientSize.Width, requiredClientHeightFromVisibleControls);
            }
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
            this.Text = UiLanguage.Get("Transparency.Title", "Transparenzeinstellung");
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.Font = SystemFonts.MessageBoxFont;
            this.TopMost = true;
            int compactSpacing = Math.Max(4, this.Font.Height / 3);
            int sectionSpacing = Math.Max(8, compactSpacing * 2);
            int horizontalPadding = Math.Max(16, this.Font.Height + 4);
            int buttonSpacing = Math.Max(8, compactSpacing * 2);
            int baseContentWidth = Math.Max(240, this.Font.Height * 15);
            int dialogMinimumWidth = Math.Max(320, (horizontalPadding * 2) + baseContentWidth);
            int buttonWidthReserve = Math.Max(28, compactSpacing * 7);
            int buttonHeightReserve = Math.Max(12, compactSpacing * 3);
            int buttonHorizontalPadding = Math.Max(10, compactSpacing + 4);
            int buttonVerticalPadding = Math.Max(4, (compactSpacing / 2) + 1);
            int buttonTextHorizontalPadding = Math.Max(buttonWidthReserve, (buttonHorizontalPadding * 2) + 4);
            int buttonTextVerticalPadding = Math.Max(buttonHeightReserve, (buttonVerticalPadding * 2) + 4);
            int valueLabelWidthReserve = Math.Max(12, compactSpacing * 3);
            int valueLabelHeightReserve = Math.Max(8, compactSpacing * 2);
            int trackBarHeightReserve = Math.Max(10, compactSpacing * 3);
            int minimumButtonWidth = Math.Max(78, (this.Font.Height * 4) + buttonTextHorizontalPadding);
            int minimumButtonHeight = Math.Max(28, this.Font.Height + buttonTextVerticalPadding);
            int valueLabelMinimumWidth = Math.Max(90, (this.Font.Height * 5) + valueLabelWidthReserve);
            int valueLabelMinimumHeight = Math.Max(24, this.Font.Height + valueLabelHeightReserve);
            int trackBarMinimumHeight = Math.Max(40, this.Font.Height + trackBarHeightReserve);
            int valueLabelTextHorizontalPadding = valueLabelWidthReserve;
            int infoTextVerticalPadding = Math.Max(4, compactSpacing + 1);
            int valueLabelTextVerticalPadding = Math.Max(4, compactSpacing + 1);
            int contentTop = Math.Max(sectionSpacing, this.Font.Height);
            int infoMinimumHeight = Math.Max(this.Font.Height + compactSpacing + infoTextVerticalPadding, sectionSpacing + compactSpacing + 2);
            int contentSectionSpacing = Math.Max(compactSpacing, this.Font.Height / 3);
            int buttonAreaTopSpacing = Math.Max(sectionSpacing, contentSectionSpacing + compactSpacing);
            int bottomPadding = Math.Max(12, buttonAreaTopSpacing);
            int baseContentHeight =
                contentTop +
                infoMinimumHeight +
                contentSectionSpacing +
                valueLabelMinimumHeight +
                contentSectionSpacing +
                trackBarMinimumHeight +
                buttonAreaTopSpacing +
                minimumButtonHeight +
                bottomPadding;
            int dialogMinimumHeight = Math.Max(176, baseContentHeight);
            this.ClientSize = new Size(dialogMinimumWidth, dialogMinimumHeight);
            int contentLeft = horizontalPadding;
            int contentRight = this.ClientSize.Width - horizontalPadding;
            int contentWidth = contentRight - contentLeft;

            Label infoLabel = new Label();
            infoLabel.AutoSize = false;
            infoLabel.Text = UiLanguage.Get(
                "Transparency.Info",
                "Transparenz");
            Size infoTextSize = TextRenderer.MeasureText(
                infoLabel.Text,
                infoLabel.Font,
                new Size(contentWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            int infoHeight = Math.Max(infoMinimumHeight, infoTextSize.Height);
            infoLabel.SetBounds(contentLeft, contentTop, contentWidth, infoHeight);

            this.valueLabel = new Label();
            this.valueLabel.AutoSize = false;
            this.valueLabel.TextAlign = ContentAlignment.MiddleRight;
            Size valueLabelTextSize = TextRenderer.MeasureText(
                "100 %",
                this.valueLabel.Font ?? this.Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            int valueLabelHeight = Math.Max(valueLabelMinimumHeight, valueLabelTextSize.Height + valueLabelTextVerticalPadding);
            int valueLabelWidth = Math.Max(valueLabelMinimumWidth, valueLabelTextSize.Width + valueLabelTextHorizontalPadding);
            int valueLabelTop = infoLabel.Bottom + contentSectionSpacing;
            this.valueLabel.SetBounds(contentRight - valueLabelWidth, valueLabelTop, valueLabelWidth, valueLabelHeight);

            this.transparencyTrackBar = new TrackBar();
            this.transparencyTrackBar.Minimum = 0;
            this.transparencyTrackBar.Maximum = 100;
            this.transparencyTrackBar.TickFrequency = 10;
            this.transparencyTrackBar.SmallChange = 1;
            this.transparencyTrackBar.LargeChange = 10;
            this.transparencyTrackBar.Value = Math.Max(0, Math.Min(100, transparencyPercent));
            int trackBarHeight = Math.Max(trackBarMinimumHeight, this.transparencyTrackBar.PreferredSize.Height);
            int trackBarTop = this.valueLabel.Bottom + contentSectionSpacing;
            this.transparencyTrackBar.SetBounds(contentLeft, trackBarTop, contentWidth, trackBarHeight);
            this.transparencyTrackBar.ValueChanged += this.TransparencyTrackBar_ValueChanged;

            Button saveButton = new Button();
            saveButton.Text = UiLanguage.Get("Transparency.Save", "Speichern");
            saveButton.DialogResult = DialogResult.OK;
            saveButton.Padding = new Padding(buttonHorizontalPadding, buttonVerticalPadding, buttonHorizontalPadding, buttonVerticalPadding);
            saveButton.MinimumSize = new Size(minimumButtonWidth, minimumButtonHeight);
            Size saveButtonTextSize = TextRenderer.MeasureText(
                saveButton.Text,
                saveButton.Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            int saveButtonWidth = Math.Max(saveButton.MinimumSize.Width, saveButtonTextSize.Width + buttonTextHorizontalPadding);
            saveButton.Click += delegate
            {
                if (this.previewTransparency != null)
                {
                    this.previewTransparency(this.transparencyTrackBar.Value);
                }
            };

            Button cancelButton = new Button();
            cancelButton.Text = UiLanguage.Get("Transparency.Close", "Schließen");
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Padding = new Padding(buttonHorizontalPadding, buttonVerticalPadding, buttonHorizontalPadding, buttonVerticalPadding);
            cancelButton.MinimumSize = new Size(minimumButtonWidth, minimumButtonHeight);
            Size cancelButtonTextSize = TextRenderer.MeasureText(
                cancelButton.Text,
                cancelButton.Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            int cancelButtonWidth = Math.Max(cancelButton.MinimumSize.Width, cancelButtonTextSize.Width + buttonTextHorizontalPadding);
            Size saveButtonPreferredSize = saveButton.GetPreferredSize(Size.Empty);
            Size cancelButtonPreferredSize = cancelButton.GetPreferredSize(Size.Empty);
            int buttonHeight = Math.Max(
                Math.Max(saveButton.MinimumSize.Height, cancelButton.MinimumSize.Height),
                Math.Max(saveButtonPreferredSize.Height, cancelButtonPreferredSize.Height));
            int minimumClientWidth = Math.Max(
                dialogMinimumWidth,
                contentLeft + saveButtonWidth + buttonSpacing + cancelButtonWidth + horizontalPadding);
            int minimumClientHeight = Math.Max(
                dialogMinimumHeight,
                this.transparencyTrackBar.Bottom + buttonAreaTopSpacing + buttonHeight + bottomPadding);
            this.ClientSize = new Size(minimumClientWidth, minimumClientHeight);
            contentRight = this.ClientSize.Width - horizontalPadding;
            contentWidth = contentRight - contentLeft;
            infoLabel.SetBounds(contentLeft, infoLabel.Top, contentWidth, infoLabel.Height);
            this.valueLabel.SetBounds(contentRight - valueLabelWidth, this.valueLabel.Top, valueLabelWidth, valueLabelHeight);
            this.transparencyTrackBar.SetBounds(contentLeft, this.transparencyTrackBar.Top, contentWidth, this.transparencyTrackBar.Height);
            int buttonTop = this.transparencyTrackBar.Bottom + buttonAreaTopSpacing;
            int cancelButtonLeft = contentRight - cancelButtonWidth;
            int saveButtonLeft = cancelButtonLeft - buttonSpacing - saveButtonWidth;
            saveButton.SetBounds(saveButtonLeft, buttonTop, saveButtonWidth, buttonHeight);
            cancelButton.SetBounds(cancelButtonLeft, buttonTop, cancelButtonWidth, buttonHeight);
            int requiredClientWidthFromControls = Math.Max(
                Math.Max(
                    Math.Max(
                        this.valueLabel.Right + horizontalPadding,
                        this.transparencyTrackBar.Right + horizontalPadding),
                    saveButton.Right + horizontalPadding),
                cancelButton.Right + horizontalPadding);
            if (requiredClientWidthFromControls > this.ClientSize.Width)
            {
                this.ClientSize = new Size(requiredClientWidthFromControls, this.ClientSize.Height);
                contentRight = this.ClientSize.Width - horizontalPadding;
                contentWidth = contentRight - contentLeft;
                infoLabel.SetBounds(contentLeft, infoLabel.Top, contentWidth, infoLabel.Height);
                this.valueLabel.SetBounds(contentRight - valueLabelWidth, this.valueLabel.Top, valueLabelWidth, valueLabelHeight);
                this.transparencyTrackBar.SetBounds(contentLeft, this.transparencyTrackBar.Top, contentWidth, this.transparencyTrackBar.Height);
                cancelButtonLeft = contentRight - cancelButtonWidth;
                saveButtonLeft = cancelButtonLeft - buttonSpacing - saveButtonWidth;
                saveButton.SetBounds(saveButtonLeft, buttonTop, saveButtonWidth, buttonHeight);
                cancelButton.SetBounds(cancelButtonLeft, buttonTop, cancelButtonWidth, buttonHeight);
            }
            int requiredClientHeightFromVisibleControls = Math.Max(
                Math.Max(
                    infoLabel.Bottom,
                    this.valueLabel.Bottom),
                Math.Max(
                    this.transparencyTrackBar.Bottom,
                    cancelButton.Bottom)) + bottomPadding;
            if (requiredClientHeightFromVisibleControls != this.ClientSize.Height)
            {
                this.ClientSize = new Size(this.ClientSize.Width, requiredClientHeightFromVisibleControls);
            }

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
        public AdapterListItem(string id, string name, string displayText, bool isAvailable = true)
        {
            this.Id = id ?? string.Empty;
            this.Name = name ?? string.Empty;
            this.DisplayText = displayText ?? string.Empty;
            this.IsAvailable = isAvailable;
        }

        public string Id { get; private set; }

        public string Name { get; private set; }

        public string DisplayText { get; private set; }

        public bool IsAvailable { get; private set; }

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
                if (!IsSelectableInSetup(networkInterface))
                {
                    continue;
                }

                bool isAvailable = IsCapturable(networkInterface);
                OperationalStatus operationalStatus;
                string stateText = TryGetOperationalStatus(networkInterface, out operationalStatus) &&
                    operationalStatus == OperationalStatus.Up
                    ? UiLanguage.Get("Calibration.AdapterStateActive", "aktiv")
                    : UiLanguage.Get("Calibration.AdapterStateInactive", "inaktiv");
                string displayText = string.Format("{0} ({1})", networkInterface.Name, stateText);
                items.Add(new AdapterListItem(
                    networkInterface.Id,
                    networkInterface.Name,
                    displayText,
                    isAvailable));
            }

            items.Sort(
                delegate(AdapterListItem left, AdapterListItem right)
                {
                    int availabilityComparison = right.IsAvailable.CompareTo(left.IsAvailable);
                    if (availabilityComparison != 0)
                    {
                        return availabilityComparison;
                    }

                    return string.Compare(left.DisplayText, right.DisplayText, StringComparison.CurrentCultureIgnoreCase);
                });

            return items;
        }

        public static NetworkSnapshot Capture(MonitorSettings settings)
        {
            if (settings == null || string.IsNullOrWhiteSpace(settings.AdapterId))
            {
                return new NetworkSnapshot(0L, 0L, 0, string.Empty);
            }

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

            if (settings.UsesAutomaticAdapterSelection())
            {
                return CaptureAutomatic(interfaces);
            }

            return CaptureSelectedAdapter(interfaces, settings);
        }

        public static AdapterAvailabilityState GetAdapterAvailabilityState(MonitorSettings settings)
        {
            if (settings == null || string.IsNullOrEmpty(settings.AdapterId))
            {
                return AdapterAvailabilityState.Missing;
            }

            NetworkInterface[] interfaces;

            try
            {
                interfaces = NetworkInterface.GetAllNetworkInterfaces();
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce("network-getallnetworkinterfaces-availability", "Failed to enumerate network interfaces for availability check.", ex);
                return AdapterAvailabilityState.Missing;
            }

            if (settings.UsesAutomaticAdapterSelection())
            {
                return SelectAutomaticAdapter(interfaces) != null
                    ? AdapterAvailabilityState.Available
                    : AdapterAvailabilityState.Missing;
            }

            foreach (NetworkInterface networkInterface in interfaces)
            {
                if (!IsSelectableInSetup(networkInterface))
                {
                    continue;
                }

                if (!string.Equals(networkInterface.Id, settings.AdapterId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return IsCapturable(networkInterface)
                    ? AdapterAvailabilityState.Available
                    : AdapterAvailabilityState.Inactive;
            }

            return AdapterAvailabilityState.Missing;
        }

        public static string ResolveAdapterKey(MonitorSettings settings)
        {
            if (settings != null && !string.IsNullOrWhiteSpace(settings.AdapterId))
            {
                if (!settings.UsesAutomaticAdapterSelection())
                {
                    return settings.AdapterId.Trim();
                }

                NetworkInterface[] interfaces;

                try
                {
                    interfaces = NetworkInterface.GetAllNetworkInterfaces();
                }
                catch (Exception ex)
                {
                    AppLog.WarnOnce("network-getallnetworkinterfaces-adapterkey", "Failed to enumerate network interfaces for adapter key resolution.", ex);
                    return MonitorSettings.AutomaticAdapterId;
                }

                NetworkInterface primaryAdapter = SelectAutomaticAdapter(interfaces);
                if (primaryAdapter == null || string.IsNullOrWhiteSpace(primaryAdapter.Id))
                {
                    return MonitorSettings.AutomaticAdapterId;
                }

                return primaryAdapter.Id.Trim();
            }

            return string.Empty;
        }

        private static NetworkSnapshot CaptureAutomatic(NetworkInterface[] interfaces)
        {
            NetworkInterface primaryAdapter = SelectAutomaticAdapter(interfaces);
            if (primaryAdapter == null)
            {
                return new NetworkSnapshot(0L, 0L, 0, string.Empty);
            }

            long bytesReceived;
            long bytesSent;
            if (!TryReadStatistics(primaryAdapter, out bytesReceived, out bytesSent))
            {
                return new NetworkSnapshot(0L, 0L, 0, primaryAdapter.Name);
            }

            return new NetworkSnapshot(bytesReceived, bytesSent, 1, primaryAdapter.Name);
        }

        private static NetworkSnapshot CaptureSelectedAdapter(NetworkInterface[] interfaces, MonitorSettings settings)
        {
            foreach (NetworkInterface networkInterface in interfaces)
            {
                if (!IsSelectableInSetup(networkInterface))
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

            if (LooksLikeAuxiliaryVirtualAdapter(networkInterface))
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

        private static bool IsSelectableInSetup(NetworkInterface networkInterface)
        {
            return IsSelectable(networkInterface) &&
                !LooksLikeVirtualAdapterForSetup(networkInterface);
        }

        private static bool LooksLikeAuxiliaryVirtualAdapter(NetworkInterface networkInterface)
        {
            string name = SafeInterfaceText(networkInterface != null ? networkInterface.Name : null);
            string description = SafeInterfaceText(networkInterface != null ? networkInterface.Description : null);

            return ContainsAuxiliaryInterfaceMarker(name) ||
                ContainsAuxiliaryInterfaceMarker(description);
        }

        private static bool LooksLikeVirtualAdapterForSetup(NetworkInterface networkInterface)
        {
            string name = SafeInterfaceText(networkInterface != null ? networkInterface.Name : null);
            string description = SafeInterfaceText(networkInterface != null ? networkInterface.Description : null);

            return ContainsVirtualAdapterMarker(name) ||
                ContainsVirtualAdapterMarker(description);
        }

        private static string SafeInterfaceText(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }

        private static bool ContainsAuxiliaryInterfaceMarker(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            return value.Contains("ndis 6 filter") ||
                value.Contains("lightweight filter") ||
                value.Contains("filter driver") ||
                value.Contains("qos packet scheduler") ||
                value.Contains("kerneldebugger") ||
                value.Contains("kernel debugger") ||
                value.Contains("pseudo-interface") ||
                value.Contains("wi-fi direct") ||
                value.Contains("wifi direct") ||
                value.Contains("virtual wifi");
        }

        private static bool ContainsVirtualAdapterMarker(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            return value.Contains("virtual") ||
                value.Contains("vpn") ||
                value.Contains("tunnel") ||
                value.Contains("tap-") ||
                value.Contains("tap ") ||
                value.Contains("tap-windows") ||
                value.Contains("wireguard") ||
                value.Contains("mullvad") ||
                value.Contains("openvpn") ||
                value.Contains("hyper-v") ||
                value.Contains("vethernet") ||
                value.Contains("wi-fi direct") ||
                value.Contains("wifi direct") ||
                value.Contains("pseudo-interface") ||
                value.Contains("pseudo interface");
        }

        private static bool IsCapturable(NetworkInterface networkInterface)
        {
            OperationalStatus operationalStatus;
            return IsSelectable(networkInterface) &&
                TryGetOperationalStatus(networkInterface, out operationalStatus) &&
                operationalStatus == OperationalStatus.Up &&
                HasUsableUnicastAddress(networkInterface);
        }

        private static NetworkInterface SelectAutomaticAdapter(NetworkInterface[] interfaces)
        {
            if (interfaces == null || interfaces.Length == 0)
            {
                return null;
            }

            NetworkInterface bestAdapter = null;
            int bestTypePriority = int.MinValue;
            long bestSpeed = long.MinValue;

            foreach (NetworkInterface networkInterface in interfaces)
            {
                if (!IsSelectableInSetup(networkInterface) || !IsCapturable(networkInterface))
                {
                    continue;
                }

                int typePriority = GetAutomaticAdapterTypePriority(networkInterface);
                long speed;
                if (!TryGetInterfaceSpeed(networkInterface, out speed))
                {
                    speed = 0L;
                }

                bool isBetterCandidate = bestAdapter == null;
                if (!isBetterCandidate && typePriority != bestTypePriority)
                {
                    isBetterCandidate = typePriority > bestTypePriority;
                }

                if (!isBetterCandidate && speed != bestSpeed)
                {
                    isBetterCandidate = speed > bestSpeed;
                }

                if (isBetterCandidate)
                {
                    bestAdapter = networkInterface;
                    bestTypePriority = typePriority;
                    bestSpeed = speed;
                }
            }

            return bestAdapter;
        }

        private static int GetAutomaticAdapterTypePriority(NetworkInterface networkInterface)
        {
            if (networkInterface == null)
            {
                return 0;
            }

            NetworkInterfaceType type;
            try
            {
                type = networkInterface.NetworkInterfaceType;
            }
            catch (NetworkInformationException)
            {
                return 0;
            }

            switch (type)
            {
                case NetworkInterfaceType.Ethernet:
                case NetworkInterfaceType.GigabitEthernet:
                case NetworkInterfaceType.FastEthernetFx:
                case NetworkInterfaceType.FastEthernetT:
                    return 300;
                case NetworkInterfaceType.Wireless80211:
                    return 250;
                case NetworkInterfaceType.Ppp:
                    return 200;
                default:
                    return 100;
            }
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

