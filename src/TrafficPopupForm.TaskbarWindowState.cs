using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
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
    }
}
