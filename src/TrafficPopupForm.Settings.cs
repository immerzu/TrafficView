using System;
using System.Drawing;
using System.Windows.Forms;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
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
            bool sectionModeChanged = this.settings.PopupSectionMode != newSettings.PopupSectionMode ||
                this.settings.TaskbarPopupSectionMode != newSettings.TaskbarPopupSectionMode;
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
    }
}
