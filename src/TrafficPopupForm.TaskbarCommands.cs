using System;
using System.Drawing;
using System.Windows.Forms;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
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

        public void ClearTaskbarIntegrationPreferredLocation()
        {
            this.taskbarIntegrationPreferredLocation = null;
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
    }
}
