using System;
using System.Drawing;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private bool TryGetTaskbarRightAnchoredPlacementBoundsForSizeIgnoringQuickLaunch(
            TaskbarIntegrationSnapshot snapshot,
            Size popupSize,
            out Rectangle placementBounds)
        {
            placementBounds = Rectangle.Empty;
            if (snapshot == null)
            {
                return false;
            }

            Rectangle usableBounds = Rectangle.Inflate(snapshot.Bounds, -this.ScaleValue(TaskbarInsetThickness), -this.ScaleValue(TaskbarInsetThickness));
            if (usableBounds.Width <= 0 || usableBounds.Height <= 0)
            {
                return false;
            }

            int popupWidth = popupSize.Width;
            int popupHeight = popupSize.Height;
            if (popupWidth <= 0 || popupHeight <= 0)
            {
                return false;
            }

            int placementMargin = this.ScaleValue(TaskbarPlacementMargin);
            Rectangle protectedEdgeBounds;
            this.TryGetTaskbarProtectedEdgeBounds(snapshot, usableBounds, out protectedEdgeBounds);

            if (snapshot.IsVertical)
            {
                int bottomLimit = protectedEdgeBounds.IsEmpty
                    ? usableBounds.Bottom
                    : Math.Min(usableBounds.Bottom, protectedEdgeBounds.Top);
                int placementY = bottomLimit - popupHeight - placementMargin;
                if (placementY < usableBounds.Top + placementMargin)
                {
                    return false;
                }

                placementBounds = new Rectangle(
                    usableBounds.Left + Math.Max(0, (usableBounds.Width - popupWidth) / 2),
                    placementY,
                    popupWidth,
                    popupHeight);
            }
            else
            {
                int rightLimit = protectedEdgeBounds.IsEmpty
                    ? usableBounds.Right
                    : Math.Min(usableBounds.Right, protectedEdgeBounds.Left);
                int placementX = rightLimit - popupWidth - placementMargin;
                if (placementX < usableBounds.Left + placementMargin)
                {
                    return false;
                }

                placementBounds = new Rectangle(
                    placementX,
                    usableBounds.Top + Math.Max(0, (usableBounds.Height - popupHeight) / 2),
                    popupWidth,
                    popupHeight);
            }

            return usableBounds.Contains(placementBounds);
        }

        private bool ShouldHoldCompactTaskbarSectionNearProtectedEdge(
            TaskbarIntegrationSnapshot snapshot,
            Size regularSize)
        {
            if (snapshot == null ||
                regularSize.Width <= 0 ||
                regularSize.Height <= 0)
            {
                return false;
            }

            Rectangle usableBounds = Rectangle.Inflate(snapshot.Bounds, -this.ScaleValue(TaskbarInsetThickness), -this.ScaleValue(TaskbarInsetThickness));
            if (usableBounds.Width <= 0 || usableBounds.Height <= 0)
            {
                return false;
            }

            Rectangle protectedEdgeBounds;
            if (!this.TryGetTaskbarProtectedEdgeBounds(snapshot, usableBounds, out protectedEdgeBounds) ||
                protectedEdgeBounds.IsEmpty)
            {
                return false;
            }

            int placementMargin = this.ScaleValue(TaskbarPlacementMargin);
            int restoreHysteresis = this.ScaleValue(TaskbarCompactRestoreHysteresis);

            Rectangle anchorBounds = Rectangle.Empty;
            if (this.taskbarIntegrationPreferredLocation.HasValue)
            {
                anchorBounds = new Rectangle(this.taskbarIntegrationPreferredLocation.Value, regularSize);
            }
            else if (this.lastSuccessfulTaskbarPlacementBounds.Width > 0 &&
                this.lastSuccessfulTaskbarPlacementBounds.Height > 0)
            {
                anchorBounds = this.lastSuccessfulTaskbarPlacementBounds;
            }
            else
            {
                anchorBounds = this.GetCurrentPopupScreenBounds();
            }

            if (anchorBounds.Width <= 0 || anchorBounds.Height <= 0)
            {
                return false;
            }

            if (snapshot.IsVertical)
            {
                int anchorBottom = anchorBounds.Bottom + placementMargin;
                return anchorBottom >= protectedEdgeBounds.Top - restoreHysteresis;
            }

            int anchorRight = anchorBounds.Right + placementMargin;
            return anchorRight >= protectedEdgeBounds.Left - restoreHysteresis;
        }

        private bool TryGetTaskbarProtectedEdgeBounds(
            TaskbarIntegrationSnapshot snapshot,
            Rectangle usableBounds,
            out Rectangle protectedEdgeBounds)
        {
            protectedEdgeBounds = Rectangle.Empty;
            if (snapshot == null || snapshot.OccupiedBounds == null || snapshot.OccupiedBounds.Length <= 0)
            {
                return false;
            }

            int probeDistance = this.ScaleValue(TaskbarProtectedEdgeProbe);
            bool hasProtectedBounds = false;
            for (int i = 0; i < snapshot.OccupiedBounds.Length; i++)
            {
                Rectangle occupied = Rectangle.Intersect(snapshot.OccupiedBounds[i], usableBounds);
                if (occupied.Width <= 0 || occupied.Height <= 0)
                {
                    continue;
                }

                bool nearProtectedEdge = snapshot.IsVertical
                    ? occupied.Bottom >= usableBounds.Bottom - probeDistance
                    : occupied.Right >= usableBounds.Right - probeDistance;
                if (!nearProtectedEdge)
                {
                    continue;
                }

                protectedEdgeBounds = hasProtectedBounds
                    ? Rectangle.Union(protectedEdgeBounds, occupied)
                    : occupied;
                hasProtectedBounds = true;
            }

            return hasProtectedBounds;
        }

        private bool TryPreserveRightAnchoredTaskbarPlacement(TaskbarIntegrationSnapshot snapshot, bool activateWindow)
        {
            if (snapshot == null ||
                this.lastSuccessfulTaskbarPlacementBounds.Width <= 0 ||
                this.lastSuccessfulTaskbarPlacementBounds.Height <= 0)
            {
                return false;
            }

            Rectangle usableBounds = Rectangle.Inflate(snapshot.Bounds, -this.ScaleValue(TaskbarInsetThickness), -this.ScaleValue(TaskbarInsetThickness));
            if (!usableBounds.Contains(this.lastSuccessfulTaskbarPlacementBounds))
            {
                return false;
            }

            Rectangle protectedEdgeBounds;
            if (this.TryGetTaskbarProtectedEdgeBounds(snapshot, usableBounds, out protectedEdgeBounds) &&
                !protectedEdgeBounds.IsEmpty)
            {
                bool intrudesProtectedEdge = snapshot.IsVertical
                    ? this.lastSuccessfulTaskbarPlacementBounds.Bottom > protectedEdgeBounds.Top
                    : this.lastSuccessfulTaskbarPlacementBounds.Right > protectedEdgeBounds.Left;
                if (intrudesProtectedEdge)
                {
                    return false;
                }
            }

            this.ShowAtTaskbarPlacement(this.lastSuccessfulTaskbarPlacementBounds, activateWindow);
            return true;
        }
    }
}
