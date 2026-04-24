using System;
using System.Drawing;
using System.Windows.Forms;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
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
                !this.leftMousePressed ||
                !this.dragMoved ||
                !this.manualDragMoveApplied)
            {
                return;
            }

            PopupSectionMode configuredSectionMode = this.GetConfiguredPopupSectionMode();
            if (configuredSectionMode != PopupSectionMode.Both &&
                configuredSectionMode != PopupSectionMode.RightOnly)
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

            if (configuredSectionMode == PopupSectionMode.RightOnly)
            {
                this.SetTaskbarIntegrationStickyRightOnly(false);
                this.SetTaskbarIntegrationForcedRightOnly(false);
                this.OnTaskbarSectionModeChangeRequested(PopupSectionMode.Both);
                this.RefreshTaskbarIntegration(false, false);
                return;
            }

            if (this.taskbarIntegrationStickyRightOnlySection)
            {
                this.SetTaskbarIntegrationStickyRightOnly(false);
                this.SetTaskbarIntegrationForcedRightOnly(false);
                this.OnTaskbarSectionModeChangeRequested(PopupSectionMode.Both);
                this.RefreshTaskbarIntegration(false, false);
                return;
            }

            if (this.GetEffectivePopupSectionMode() != PopupSectionMode.Both)
            {
                return;
            }

            this.SetTaskbarIntegrationForcedRightOnly(false);
            this.SetTaskbarIntegrationStickyRightOnly(true);
            this.OnTaskbarSectionModeChangeRequested(PopupSectionMode.RightOnly);
            this.RefreshTaskbarIntegration(false, false);
        }
    }
}
