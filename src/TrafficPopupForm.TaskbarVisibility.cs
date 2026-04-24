using System;
using System.Drawing;
using System.Windows.Forms;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private void EnsureVisiblePopupLocation(string logKey, string logMessage)
        {
            Point adjustedLocation = this.GetVisiblePopupLocation(this.Location, null, logKey, logMessage);
            if (adjustedLocation != this.Location)
            {
                this.Location = adjustedLocation;
                this.OnOverlayLocationCommitted();
            }
        }

        private Point GetVisiblePopupLocation(Point preferredLocation, Point? anchorPoint, string logKey, string logMessage)
        {
            Rectangle workingArea = this.GetBestWorkingArea(preferredLocation, anchorPoint);
            Point adjustedLocation = ClampLocationToWorkingArea(preferredLocation, this.Size, workingArea);

            if (!string.IsNullOrWhiteSpace(logKey) &&
                adjustedLocation != preferredLocation)
            {
                AppLog.WarnOnce(
                    logKey,
                    string.Format(
                        "{0} Angefordert=({1},{2}), angewendet=({3},{4}), Arbeitsbereich=({5},{6},{7},{8}).",
                        logMessage,
                        preferredLocation.X,
                        preferredLocation.Y,
                        adjustedLocation.X,
                        adjustedLocation.Y,
                        workingArea.Left,
                        workingArea.Top,
                        workingArea.Width,
                        workingArea.Height));
            }

            return adjustedLocation;
        }

        private Point GetVisiblePopupLocationForManualDrag(Point preferredLocation, Point anchorPoint)
        {
            bool snappedToTaskbar;
            TaskbarIntegrationSnapshot snapshot;
            if (this.TryGetTaskbarIntegrationSnapshotForManualDrag(anchorPoint, out snapshot))
            {
                return this.GetVisiblePopupLocationForManualDrag(
                    preferredLocation,
                    anchorPoint,
                    snapshot,
                    out snappedToTaskbar);
            }

            Rectangle screenBounds = Screen.FromPoint(anchorPoint).Bounds;
            return ClampLocationToWorkingArea(preferredLocation, this.Size, screenBounds);
        }

        private bool TryGetTaskbarIntegrationSnapshotForManualDrag(Point anchorPoint, out TaskbarIntegrationSnapshot snapshot)
        {
            snapshot = null;

            if (!this.IsOverlayDragInProgress())
            {
                return this.TryCaptureTaskbarIntegrationSnapshot(out snapshot);
            }

            Rectangle screenBounds = Screen.FromPoint(anchorPoint).Bounds;
            bool canReuseSnapshot =
                this.dragTaskbarSnapshot != null &&
                this.dragTaskbarSnapshotScreenBounds == screenBounds &&
                this.dragTaskbarSnapshotCapturedUtc != DateTime.MinValue &&
                (DateTime.UtcNow - this.dragTaskbarSnapshotCapturedUtc).TotalMilliseconds < TaskbarDragSnapshotRefreshMs;
            if (canReuseSnapshot)
            {
                snapshot = this.dragTaskbarSnapshot;
                return true;
            }

            if (!this.TryCaptureTaskbarIntegrationSnapshot(out snapshot))
            {
                this.dragTaskbarSnapshot = null;
                this.dragTaskbarSnapshotScreenBounds = Rectangle.Empty;
                this.dragTaskbarSnapshotCapturedUtc = DateTime.MinValue;
                return false;
            }

            this.dragTaskbarSnapshot = snapshot;
            this.dragTaskbarSnapshotScreenBounds = snapshot.ScreenBounds;
            this.dragTaskbarSnapshotCapturedUtc = DateTime.UtcNow;
            return true;
        }

        private Point GetVisiblePopupLocationForManualDrag(
            Point preferredLocation,
            Point anchorPoint,
            TaskbarIntegrationSnapshot snapshot,
            out bool snappedToTaskbar)
        {
            snappedToTaskbar = false;
            Rectangle screenBounds = Screen.FromPoint(anchorPoint).Bounds;
            Point adjustedLocation = ClampLocationToWorkingArea(preferredLocation, this.Size, screenBounds);

            if (snapshot == null ||
                snapshot.IsHidden ||
                snapshot.ScreenBounds != screenBounds)
            {
                return adjustedLocation;
            }

            Rectangle popupBounds = new Rectangle(adjustedLocation, this.Size);
            bool overlapsTaskbarSpan = snapshot.IsVertical
                ? popupBounds.Bottom > snapshot.Bounds.Top && popupBounds.Top < snapshot.Bounds.Bottom
                : popupBounds.Right > snapshot.Bounds.Left && popupBounds.Left < snapshot.Bounds.Right;
            if (!overlapsTaskbarSpan)
            {
                return adjustedLocation;
            }

            int snapDistance = this.ScaleValue(TaskbarDragSnapDistance);
            int breakThroughDepth = this.ScaleValue(TaskbarDragBreakThroughDepth);
            switch (snapshot.Edge)
            {
                case AppBarEdge.Bottom:
                    adjustedLocation.Y = this.ApplyTaskbarDragBarrierAxis(
                        adjustedLocation.Y,
                        popupBounds.Bottom,
                        snapshot.Bounds.Top,
                        this.Height,
                        snapDistance,
                        breakThroughDepth,
                        true,
                        out snappedToTaskbar);
                    break;

                case AppBarEdge.Top:
                    adjustedLocation.Y = this.ApplyTaskbarDragBarrierAxis(
                        adjustedLocation.Y,
                        snapshot.Bounds.Bottom,
                        popupBounds.Top,
                        this.Height,
                        snapDistance,
                        breakThroughDepth,
                        false,
                        out snappedToTaskbar);
                    break;

                case AppBarEdge.Left:
                    adjustedLocation.X = this.ApplyTaskbarDragBarrierAxis(
                        adjustedLocation.X,
                        snapshot.Bounds.Right,
                        popupBounds.Left,
                        this.Width,
                        snapDistance,
                        breakThroughDepth,
                        false,
                        out snappedToTaskbar);
                    break;

                case AppBarEdge.Right:
                    adjustedLocation.X = this.ApplyTaskbarDragBarrierAxis(
                        adjustedLocation.X,
                        popupBounds.Right,
                        snapshot.Bounds.Left,
                        this.Width,
                        snapDistance,
                        breakThroughDepth,
                        true,
                        out snappedToTaskbar);
                    break;
            }

            return adjustedLocation;
        }

        private int ApplyTaskbarDragBarrierAxis(
            int currentLocation,
            int outerPopupEdge,
            int taskbarEdge,
            int popupSize,
            int snapDistance,
            int breakThroughDepth,
            bool snapOutsideBeforeTaskbar,
            out bool snappedToBarrier)
        {
            int barrierLocation = snapOutsideBeforeTaskbar
                ? taskbarEdge - popupSize
                : taskbarEdge;
            int direction = snapOutsideBeforeTaskbar ? 1 : -1;
            int signedOffset = (currentLocation - barrierLocation) * direction;

            if (signedOffset < -snapDistance || signedOffset > breakThroughDepth)
            {
                snappedToBarrier = false;
                return currentLocation;
            }

            if (signedOffset <= 0)
            {
                // Outside the taskbar: keep a short magnetic snap zone directly
                // before the edge so the popup can be aligned flush above/beside
                // the taskbar more easily, then fade back into the softer pull.
                int outsideHoldDistance = Math.Min(snapDistance, this.ScaleValue(TaskbarDesktopSnapHoldDistance));
                if (Math.Abs(signedOffset) <= outsideHoldDistance)
                {
                    snappedToBarrier = false;
                    return barrierLocation;
                }

                int distance = Math.Abs(signedOffset);
                int easedOffset = (int)Math.Round((distance * distance) / (double)Math.Max(1, snapDistance));
                snappedToBarrier = false;
                return barrierLocation - (direction * easedOffset);
            }

            // Inside the taskbar: keep the popup taskbar-locked until the user
            // pushes far enough through the magnetic threshold.
            snappedToBarrier = true;
            int easedInsideOffset = (int)Math.Round((signedOffset * signedOffset) / (double)Math.Max(1, breakThroughDepth));
            return barrierLocation + (direction * easedInsideOffset);
        }

        private bool ShouldAutoIntegrateWithTaskbar(Rectangle popupBounds, TaskbarIntegrationSnapshot snapshot)
        {
            Rectangle overlap = Rectangle.Intersect(popupBounds, snapshot.Bounds);
            if (overlap.Width <= 0 || overlap.Height <= 0)
            {
                return false;
            }

            int overlapDepth = snapshot.IsVertical ? overlap.Width : overlap.Height;
            int taskbarThickness = snapshot.IsVertical ? snapshot.Bounds.Width : snapshot.Bounds.Height;
            int requiredDepth = Math.Max(this.ScaleValue(6), Math.Min(taskbarThickness, this.ScaleValue(10)));
            return overlapDepth >= requiredDepth;
        }

    }
}
