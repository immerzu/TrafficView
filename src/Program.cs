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
[assembly: AssemblyVersion("1.4.20.0")]
[assembly: AssemblyFileVersion("1.4.20.0")]

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
        private readonly ToolStripMenuItem rotatingGlossItem;
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
        private readonly Dictionary<string, ToolStripMenuItem> panelSkinMenuItems;
        private SharedMenuOpenSource sharedMenuOpenSource;
        private MonitorSettings settings;
        private readonly TrafficUsageLog trafficUsageLog;
        private DateTime lastTrafficUsageFlushUtc = DateTime.MinValue;

        public TrafficViewContext()
        {
            this.settings = MonitorSettings.Load();
            this.trafficUsageLog = new TrafficUsageLog();
            UiLanguage.Initialize(this.settings.LanguageCode);
            this.popupForm = new TrafficPopupForm(this.settings);
            this.popupForm.RatesUpdated += this.PopupForm_RatesUpdated;
            this.popupForm.TrafficUsageMeasured += this.PopupForm_TrafficUsageMeasured;
            this.popupForm.OverlayMenuRequested += this.PopupForm_OverlayMenuRequested;
            this.popupForm.OverlayLocationCommitted += this.PopupForm_OverlayLocationCommitted;
            this.languageMenuItems = new Dictionary<string, ToolStripMenuItem>(StringComparer.OrdinalIgnoreCase);
            this.popupScaleMenuItems = new Dictionary<int, ToolStripMenuItem>();
            this.displayModeMenuItems = new Dictionary<PopupDisplayMode, ToolStripMenuItem>();
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
            this.rotatingGlossItem = new ToolStripMenuItem(string.Empty, null, this.RotatingGlossItem_Click);
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

        private void RotatingGlossItem_Click(object sender, EventArgs e)
        {
            bool nextValue = !this.settings.RotatingMeterGlossEnabled;
            this.settings = this.settings.WithRotatingMeterGlossEnabled(nextValue);
            this.settings.Save();
            this.popupForm.ApplySettings(this.settings);
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
            if (!effectiveLocation.HasValue &&
                this.settings.HasSavedPopupLocation)
            {
                effectiveLocation = this.settings.PopupLocation;
            }

            if (effectiveLocation.HasValue)
            {
                this.popupForm.ShowAtLocation(effectiveLocation.Value, activateWindow);
                if (this.settings.HasSavedPopupLocation &&
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
            this.toggleItem.Text = this.popupForm.Visible
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

            this.rotatingGlossItem.Text = UiLanguage.Get(
                "Menu.RotatingGloss",
                "Rotierender Kernschimmer");
            this.rotatingGlossItem.Checked = this.settings.RotatingMeterGlossEnabled;

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
        public event EventHandler<TrafficUsageMeasuredEventArgs> TrafficUsageMeasured;

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
            this.animationTimer.Interval = 140;
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
                this.TryBeginInvokeSafely(new Action(delegate
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
                this.OnOverlayLocationCommitted();
                return;
            }

            if (m.Msg == WmDisplayChange || m.Msg == WmSettingChange)
            {
                base.WndProc(ref m);

                if (this.Visible && this.IsHandleCreated)
                {
                    this.TryBeginInvokeSafely(new Action(delegate
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

            int outerInset = this.ScaleValue(BaseOuterInset);
            int separatorY = this.ScaleValue(BaseSeparatorY);
            int separatorInset = this.ScaleValue(BaseSeparatorInset);
            int cornerRadius = this.ScaleValue(BaseWindowCornerRadius);
            float strokeWidth = Math.Max(1F, this.ScaleFloat(1F));
            float sharedRingWidth = Math.Max(2F, this.ScaleFloat(6.2F));
            float centerInset = Math.Max(1F, this.ScaleFloat(this.IsMiniSoftDisplayMode() ? 4.6F : 2.3F));
            byte backgroundAlpha = MonitorSettings.ToOpacityByte(this.settings.TransparencyPercent);

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

                if (this.IsGlassPanelSkinEnabled())
                {
                    this.DrawPanelGlassSurface(
                        graphics,
                        innerBounds,
                        Math.Max(2F, cornerRadius - outerInset),
                        backgroundAlpha);
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

        private bool ShouldDrawStaticBackgroundLayer()
        {
            return !this.IsHudOnlyTransparencyMode()
                && MonitorSettings.ToOpacityByte(this.settings.TransparencyPercent) > 0;
        }

        private bool ShouldDrawDynamicRing()
        {
            PanelSkinDefinition definition = this.GetCurrentPanelSkinDefinition();
            return definition == null || definition.DrawDynamicRing;
        }

        private bool ShouldDrawCenterTrafficArrows()
        {
            PanelSkinDefinition definition = this.GetCurrentPanelSkinDefinition();
            return definition == null || definition.DrawCenterArrows;
        }

        private bool ShouldDrawSparkline()
        {
            PanelSkinDefinition definition = this.GetCurrentPanelSkinDefinition();
            return definition == null || definition.DrawSparkline;
        }

        private bool ShouldDrawMeterValueSupport()
        {
            PanelSkinDefinition definition = this.GetCurrentPanelSkinDefinition();
            return definition == null || definition.DrawMeterValueSupport;
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

            if (this.IsReadableInfoPanelSkinEnabled() && !this.IsHudOnlyTransparencyMode())
            {
                this.DrawReadableTrafficInfoPanel(graphics, meterBounds);
            }
            else if (!this.IsHudOnlyTransparencyMode())
            {
                this.DrawTransparencyAwareInfoPanel(graphics, meterBounds);
            }

            if (!this.IsHudOnlyTransparencyMode() && this.ShouldDrawMeterValueSupport())
            {
                this.DrawMeterValueBalanceSupport(graphics, meterBounds);
            }

            this.DrawTrafficTexts(graphics);
            if (this.ShouldDrawSparkline())
            {
                this.DrawMiniTrafficSparkline(graphics, meterBounds);
            }

            if (this.IsHudOnlyTransparencyMode())
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
            Rectangle previousBounds = new Rectangle(this.Location, this.Size);
            this.settings = newSettings.Clone();

            if (popupScaleChanged)
            {
                this.ApplyDpiLayout(this.currentDpi, false);

                if (this.Visible)
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
            if (definition != null && definition.MeterBounds.HasValue)
            {
                return this.ScaleSkinRectangle(definition.MeterBounds.Value);
            }

            bool miniGraphDisplayMode = this.IsAlternativeDisplayMode();
            int baseDiameter = miniGraphDisplayMode ? MiniGraphMeterDiameter : BaseMeterDiameter;
            int diameter = this.ScaleValue(this.IsReadableInfoPanelSkinEnabled()
                ? baseDiameter - 3
                : baseDiameter);
            int rightInset = this.ScaleValue(miniGraphDisplayMode ? MiniGraphMeterRightInset : BaseMeterRightInset);
            int x = this.ClientSize.Width - diameter - rightInset;
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
                float uploadStrokeWidth = Math.Max(2.4F, weightUnit * uploadWeight * 1.18F);

                RectangleF stableBounds = GetStableArcBounds(bounds);
                RectangleF downloadBounds = GetStableArcBounds(
                    InflateRectangle(stableBounds, -Math.Max(0F, (downloadStrokeWidth / 2F) - this.ScaleFloat(1F))));
                RectangleF uploadBounds = GetStableArcBounds(
                    InflateRectangle(
                        stableBounds,
                        -((downloadStrokeWidth / 2F) + gapBetweenRings + (uploadStrokeWidth / 2F))));

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

                            using (Image meterCenterImage = Image.FromFile(meterCenterAssetPath))
                            {
                                graphics.DrawImage(meterCenterImage, centerBounds);
                            }

                            graphics.Restore(imageState);
                            return;
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
            int right = Math.Max(left + this.ScaleValue(44), meterBounds.Left - this.ScaleValue(5));
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

                using (GraphicsPath platePath = CreateRoundedPath(bounds, radius))
                using (GraphicsPath innerPath = CreateRoundedPath(
                    InflateRectangle(bounds, -this.ScaleFloat(1.5F)),
                    Math.Max(2F, radius - this.ScaleFloat(1.5F))))
                using (SolidBrush fillBrush = new SolidBrush(Color.FromArgb(fillAlpha, 5, 14, 34)))
                using (Pen borderPen = new Pen(Color.FromArgb(borderAlpha, 164, 215, 255), Math.Max(0.8F, this.ScaleFloat(0.8F))))
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
                using (Pen separatorPen = new Pen(Color.FromArgb(28, 150, 196, 255), Math.Max(0.8F, this.ScaleFloat(0.8F))))
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
            int right = Math.Max(left + this.ScaleValue(44), meterBounds.Left - this.ScaleValue(5));
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

            GraphicsState state = graphics.Save();
            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;

                using (GraphicsPath platePath = CreateRoundedPath(bounds, radius))
                using (LinearGradientBrush fillBrush = new LinearGradientBrush(
                    new PointF(bounds.Left, bounds.Top),
                    new PointF(bounds.Right, bounds.Bottom),
                    Color.FromArgb(fillAlpha, 4, 10, 24),
                    Color.FromArgb(Math.Max(0, fillAlpha - 18), 8, 18, 36)))
                using (LinearGradientBrush highlightBrush = new LinearGradientBrush(
                    new PointF(bounds.Left, bounds.Top),
                    new PointF(bounds.Left, bounds.Bottom),
                    Color.Transparent,
                    Color.Transparent))
                using (Pen borderPen = new Pen(Color.FromArgb(borderAlpha, 132, 192, 235), Math.Max(0.8F, this.ScaleFloat(0.8F))))
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
            int glowAlpha = ultraTransparent
                ? (isPrimaryValue ? 68 : 42)
                : Math.Min(isPrimaryValue ? 48 : 28, (isPrimaryValue ? 28 : 16) + (int)Math.Round(transparencyPercent * 0.22D));
            int shadowAlpha = ultraTransparent
                ? (isPrimaryValue ? 212 : 176)
                : Math.Min(isPrimaryValue ? 164 : 132, (isPrimaryValue ? 112 : 92) + (int)Math.Round(transparencyPercent * 0.52D));
            int outlineAlpha = ultraTransparent
                ? (isPrimaryValue ? 198 : 166)
                : Math.Min(isPrimaryValue ? 148 : 118, (isPrimaryValue ? 92 : 72) + (int)Math.Round(transparencyPercent * 0.56D));

            using (SolidBrush glowBrush = new SolidBrush(Color.FromArgb(glowAlpha, color)))
            using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(shadowAlpha, 4, 10, 24)))
            using (SolidBrush outlineBrush = new SolidBrush(Color.FromArgb(outlineAlpha, 6, 14, 30)))
            using (SolidBrush textBrush = new SolidBrush(color))
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
            if (definition != null && definition.SparklineBounds.HasValue)
            {
                return this.ScaleSkinRectangle(definition.SparklineBounds.Value);
            }

            int left = this.ScaleValue(this.IsMiniGraphDisplayMode() ? (int)MiniGraphSparklineLeft : 8);
            int top = this.ScaleValue(this.IsMiniGraphDisplayMode() ? (int)MiniGraphSparklineTop : 46);
            int width = Math.Max(12, meterBounds.Left - left - this.ScaleValue(4));
            int height = Math.Max(
                this.IsMiniGraphDisplayMode() ? 8 : 4,
                this.ScaleValue(this.IsMiniGraphDisplayMode() ? (int)MiniGraphSparklineHeight : 7));
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
            int contrastAlpha = ultraTransparent
                ? (isPrimaryValue ? 164 : 132)
                : Math.Min(
                    isPrimaryValue ? 112 : 86,
                    (isPrimaryValue ? 32 : 20) + (int)Math.Round(transparencyPercent * (isPrimaryValue ? 0.80D : 0.66D)));

            using (SolidBrush contrastBrush = new SolidBrush(Color.FromArgb(
                contrastAlpha,
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
                this.ShouldAnimateMeterGloss();
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
            this.UpdateAnimationTimerState();
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
            Size clientSize = this.ScaleSkinSize(baseClientSize);
            this.ClientSize = clientSize;
            this.MinimumSize = clientSize;
            this.MaximumSize = clientSize;
            this.Font = this.formFont;

            Rectangle defaultDownloadCaptionBounds = new Rectangle(BaseCaptionX, BaseDownloadCaptionY, BaseCaptionWidth, BaseCaptionHeight);
            Rectangle defaultDownloadValueBounds = new Rectangle(BaseDownloadValueX, BaseDownloadValueY, BaseValueWidth, BaseValueHeight);
            Rectangle defaultUploadCaptionBounds = new Rectangle(BaseCaptionX, BaseUploadCaptionY, BaseCaptionWidth, BaseCaptionHeight);
            Rectangle defaultUploadValueBounds = new Rectangle(BaseUploadValueX, BaseUploadValueY, BaseValueWidth, BaseValueHeight);

            Rectangle scaledDownloadCaptionBounds = this.GetScaledSkinBounds(
                defaultDownloadCaptionBounds,
                definition != null ? definition.DownloadCaptionBounds : (Rectangle?)null);
            Rectangle scaledDownloadValueBounds = this.GetScaledSkinBounds(
                defaultDownloadValueBounds,
                definition != null ? definition.DownloadValueBounds : (Rectangle?)null);
            Rectangle scaledUploadCaptionBounds = this.GetScaledSkinBounds(
                defaultUploadCaptionBounds,
                definition != null ? definition.UploadCaptionBounds : (Rectangle?)null);
            Rectangle scaledUploadValueBounds = this.GetScaledSkinBounds(
                defaultUploadValueBounds,
                definition != null ? definition.UploadValueBounds : (Rectangle?)null);

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
                this.currentSettings.PanelSkinId);

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
                this.currentSettings.PanelSkinId);
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
                this.currentSettings.PanelSkinId);
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

