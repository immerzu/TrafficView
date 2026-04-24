using System;
using System.Drawing;
using System.Windows.Forms;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
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
    }
}
