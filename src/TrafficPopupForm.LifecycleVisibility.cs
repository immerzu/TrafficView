using System;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
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
                this.lastPresentedLocation = new System.Drawing.Point(int.MinValue, int.MinValue);

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
    }
}
