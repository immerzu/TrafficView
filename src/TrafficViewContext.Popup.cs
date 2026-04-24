using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace TrafficView
{
    internal sealed partial class TrafficViewContext
    {
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
                TrafficRateFormatter.FormatSpeed(e.DownloadBytesPerSecond),
                TrafficRateFormatter.FormatSpeed(e.UploadBytesPerSecond));

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
                pair.Value.Checked = pair.Key == this.GetCurrentPopupSectionModeSetting();
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
    }
}
