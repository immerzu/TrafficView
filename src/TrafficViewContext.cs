using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TrafficView
{
    internal sealed partial class TrafficViewContext : ApplicationContext
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
        private readonly ToolStripMenuItem aboutItem;
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
            this.popupForm.TaskbarSectionModeChangeRequested += this.PopupForm_TaskbarSectionModeChangeRequested;
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
            this.aboutItem = new ToolStripMenuItem(string.Empty, null, this.AboutItem_Click);
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
                PopupDisplayMode.MiniSoft,
                PopupDisplayMode.Simple,
                PopupDisplayMode.SimpleBlue
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
            this.sharedMenu.Items.Add(this.aboutItem);
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

        private static AdapterAvailabilityState GetAdapterAvailabilityState(MonitorSettings settings)
        {
            if (settings == null || !settings.HasAdapterSelection())
            {
                return AdapterAvailabilityState.Missing;
            }

            return NetworkSnapshot.GetAdapterAvailabilityState(settings);
        }
    }

}
