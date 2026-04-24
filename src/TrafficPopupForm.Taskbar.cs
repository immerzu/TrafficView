using System;
using System.Drawing;
using System.Windows.Forms;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
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

    }
}
