using System;
using System.Drawing;
using System.Windows.Forms;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
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
            if (this.settings.TaskbarIntegrationEnabled)
            {
                TaskbarIntegrationSnapshot snapshot = this.activeTaskbarSnapshot;
                if (snapshot == null && !this.TryCaptureTaskbarIntegrationSnapshot(out snapshot))
                {
                    this.taskbarIntegrationPreferredLocation = preferredLocation;
                    this.MoveOverlayDuringManualDrag(this.GetVisiblePopupLocationForManualDrag(preferredLocation, cursorPosition));
                    return;
                }

                bool snappedToTaskbar;
                Point dragLocation = this.GetVisiblePopupLocationForManualDrag(
                    preferredLocation,
                    cursorPosition,
                    snapshot,
                    out snappedToTaskbar);
                this.taskbarIntegrationPreferredLocation = preferredLocation;
                if (snappedToTaskbar)
                {
                    this.activeTaskbarSnapshot = snapshot;
                }

                this.MoveOverlayDuringManualDrag(dragLocation);
                return;
            }

            this.MoveOverlayDuringManualDrag(this.GetVisiblePopupLocationForManualDrag(preferredLocation, cursorPosition));
        }

        private void MoveOverlayDuringManualDrag(Point location)
        {
            this.manualDragMoveApplied = true;
            if (this.Location == location && !this.pendingManualDragLocation.HasValue)
            {
                return;
            }

            this.pendingManualDragLocation = location;
            this.manualDragMoveTimer.Stop();
            this.ApplyPendingManualDragMove();
        }

        private void ManualDragMoveTimer_Tick(object sender, EventArgs e)
        {
            if (!this.pendingManualDragLocation.HasValue)
            {
                this.manualDragMoveTimer.Stop();
                return;
            }

            this.ApplyPendingManualDragMove();
        }

        private void ApplyPendingManualDragMove()
        {
            if (!this.pendingManualDragLocation.HasValue)
            {
                return;
            }

            Point location = this.pendingManualDragLocation.Value;
            this.pendingManualDragLocation = null;

            if (this.Location == location)
            {
                return;
            }

            if (!this.IsHandleCreated || this.IsDisposed)
            {
                this.Location = location;
                return;
            }

            uint flags = SwpNoSize | SwpNoZOrder | SwpNoOwnerZOrder | SwpNoActivate;
            if (!SetWindowPos(this.Handle, IntPtr.Zero, location.X, location.Y, this.Width, this.Height, flags))
            {
                this.Location = location;
            }
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
                if (shouldCommitLocation)
                {
                    this.ApplyPendingManualDragMove();
                }

                if (shouldCommitLocation)
                {
                    this.TryToggleTaskbarSectionModeFromLeftDrag();
                }

                this.ResetOverlayDragState();
                if (this.Visible && !this.IsDisposed)
                {
                    this.RefreshVisualSurface();
                }

                if (shouldCommitLocation)
                {
                    this.OnOverlayLocationCommitted();
                }
            }
        }

        private void ResetOverlayDragState()
        {
            this.leftMousePressed = false;
            this.dragMoved = false;
            this.manualDragMoveApplied = false;
            this.pendingManualDragLocation = null;
            this.manualDragMoveTimer.Stop();
            this.dragTaskbarSnapshot = null;
            this.dragTaskbarSnapshotScreenBounds = Rectangle.Empty;
            this.dragTaskbarSnapshotCapturedUtc = DateTime.MinValue;

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
    }
}
