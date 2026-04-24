using System;
using System.Collections.Generic;
using System.Drawing;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private bool TryGetTaskbarPlacementBounds(TaskbarIntegrationSnapshot snapshot, out Rectangle placementBounds)
        {
            return this.TryGetTaskbarPlacementBoundsForSize(snapshot, this.ClientSize, out placementBounds);
        }

        private bool TryGetTaskbarPlacementBoundsWithCompactFallback(TaskbarIntegrationSnapshot snapshot, out Rectangle placementBounds)
        {
            placementBounds = Rectangle.Empty;
            if (snapshot == null)
            {
                return false;
            }

            if (this.taskbarIntegrationForceRightOnlySection)
            {
                Size regularSize = this.GetScaledClientSizeForSection(this.GetConfiguredPopupSectionMode());
                if (this.ShouldHoldCompactTaskbarSectionNearProtectedEdge(snapshot, regularSize))
                {
                    Size heldCompactSize = this.GetScaledClientSizeForSection(PopupSectionMode.RightOnly);
                    return this.TryGetTaskbarPlacementBoundsForSize(snapshot, heldCompactSize, out placementBounds);
                }

                Size restoreProbeSize = regularSize;
                int restoreHysteresis = this.ScaleValue(TaskbarCompactRestoreHysteresis);
                if (snapshot.IsVertical)
                {
                    restoreProbeSize.Height += restoreHysteresis;
                }
                else
                {
                    restoreProbeSize.Width += restoreHysteresis;
                }

                Rectangle restoreProbeBounds;
                if (this.TryGetTaskbarPlacementBoundsForSize(snapshot, restoreProbeSize, out restoreProbeBounds) &&
                    this.TryGetTaskbarPlacementBoundsForSize(snapshot, regularSize, out placementBounds))
                {
                    this.SetTaskbarIntegrationForcedRightOnly(false);
                    return true;
                }
            }

            if (this.TryGetTaskbarPlacementBounds(snapshot, out placementBounds))
            {
                return true;
            }

            if (this.GetEffectivePopupSectionMode() == PopupSectionMode.RightOnly)
            {
                return false;
            }

            Size compactSize = this.GetScaledClientSizeForSection(PopupSectionMode.RightOnly);
            if (!this.TryGetTaskbarPlacementBoundsForSize(snapshot, compactSize, out placementBounds))
            {
                return false;
            }

            // The compact fallback is taskbar-local: it preserves the user's saved section mode.
            this.SetTaskbarIntegrationForcedRightOnly(true);
            return true;
        }

        private bool TryGetTaskbarPlacementBoundsAllowingQuickLaunchOverlap(TaskbarIntegrationSnapshot snapshot, out Rectangle placementBounds)
        {
            placementBounds = Rectangle.Empty;
            if (snapshot == null)
            {
                return false;
            }

            if (this.taskbarIntegrationForceRightOnlySection)
            {
                Size forcedCompactSize = this.GetScaledClientSizeForSection(PopupSectionMode.RightOnly);
                return this.TryGetTaskbarRightAnchoredPlacementBoundsForSizeIgnoringQuickLaunch(snapshot, forcedCompactSize, out placementBounds);
            }

            if (this.TryGetTaskbarRightAnchoredPlacementBoundsForSizeIgnoringQuickLaunch(snapshot, this.ClientSize, out placementBounds))
            {
                return true;
            }

            if (this.GetEffectivePopupSectionMode() == PopupSectionMode.RightOnly)
            {
                return false;
            }

            Size compactSize = this.GetScaledClientSizeForSection(PopupSectionMode.RightOnly);
            if (!this.TryGetTaskbarRightAnchoredPlacementBoundsForSizeIgnoringQuickLaunch(snapshot, compactSize, out placementBounds))
            {
                return false;
            }

            this.SetTaskbarIntegrationForcedRightOnly(true);
            return true;
        }

        private bool TryGetTaskbarPlacementBoundsForSize(TaskbarIntegrationSnapshot snapshot, Size popupSize, out Rectangle placementBounds)
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

            // Horizontal taskbars stay strictly right-anchored against the tray edge.
            // Left-side occupancy (for example quick-launch style buttons) is ignored on purpose.
            if (!snapshot.IsVertical)
            {
                return this.TryGetTaskbarRightAnchoredPlacementBoundsForSizeIgnoringQuickLaunch(snapshot, popupSize, out placementBounds);
            }

            int placementMargin = this.ScaleValue(TaskbarPlacementMargin);

            List<Rectangle> freeBands = this.GetFreeTaskbarBands(snapshot, usableBounds);
            if (this.taskbarIntegrationPreferredLocation.HasValue)
            {
                return this.TryGetTaskbarPlacementBoundsFromPreferredLocation(
                    snapshot,
                    freeBands,
                    popupWidth,
                    popupHeight,
                    placementMargin,
                    usableBounds,
                    this.taskbarIntegrationPreferredLocation.Value,
                    out placementBounds);
            }

            Rectangle? bestBand = null;

            for (int i = 0; i < freeBands.Count; i++)
            {
                Rectangle band = freeBands[i];
                bool fits = snapshot.IsVertical
                    ? band.Height >= popupHeight
                    : band.Width >= popupWidth;
                if (!fits)
                {
                    continue;
                }

                if (!bestBand.HasValue)
                {
                    bestBand = band;
                    continue;
                }

                if (snapshot.IsVertical)
                {
                    if (band.Bottom > bestBand.Value.Bottom)
                    {
                        bestBand = band;
                    }
                }
                else if (band.Right > bestBand.Value.Right)
                {
                    bestBand = band;
                }
            }

            if (!bestBand.HasValue)
            {
                return false;
            }

            Rectangle selectedBand = bestBand.Value;
            if (snapshot.IsVertical)
            {
                placementBounds = new Rectangle(
                    selectedBand.Left + Math.Max(0, (selectedBand.Width - popupWidth) / 2),
                    selectedBand.Bottom - popupHeight - placementMargin,
                    popupWidth,
                    popupHeight);
            }
            else
            {
                placementBounds = new Rectangle(
                    selectedBand.Right - popupWidth - placementMargin,
                    selectedBand.Top + Math.Max(0, (selectedBand.Height - popupHeight) / 2),
                    popupWidth,
                    popupHeight);
            }

            return usableBounds.Contains(placementBounds);
        }

    }
}
