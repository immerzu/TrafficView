using System;
using System.Collections.Generic;
using System.Drawing;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private bool TryGetTaskbarPlacementBoundsFromPreferredLocation(
            TaskbarIntegrationSnapshot snapshot,
            List<Rectangle> freeBands,
            int popupWidth,
            int popupHeight,
            int placementMargin,
            Rectangle usableBounds,
            Point preferredLocation,
            out Rectangle placementBounds)
        {
            placementBounds = Rectangle.Empty;
            Rectangle? bestPlacement = null;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < freeBands.Count; i++)
            {
                Rectangle band = freeBands[i];
                int candidateX;
                int candidateY;

                if (snapshot.IsVertical)
                {
                    int minimumY = band.Top + placementMargin;
                    int maximumY = band.Bottom - popupHeight - placementMargin;
                    if (maximumY < minimumY || band.Width < popupWidth)
                    {
                        continue;
                    }

                    candidateY = Clamp(preferredLocation.Y, minimumY, maximumY);
                    candidateX = band.Left + Math.Max(0, (band.Width - popupWidth) / 2);
                }
                else
                {
                    int minimumX = band.Left + placementMargin;
                    int maximumX = band.Right - popupWidth - placementMargin;
                    if (maximumX < minimumX || band.Height < popupHeight)
                    {
                        continue;
                    }

                    candidateX = Clamp(preferredLocation.X, minimumX, maximumX);
                    candidateY = band.Top + Math.Max(0, (band.Height - popupHeight) / 2);
                }

                Rectangle candidatePlacement = new Rectangle(candidateX, candidateY, popupWidth, popupHeight);
                if (!usableBounds.Contains(candidatePlacement))
                {
                    continue;
                }

                int distance = snapshot.IsVertical
                    ? Math.Abs(preferredLocation.Y - candidateY)
                    : Math.Abs(preferredLocation.X - candidateX);
                bool preferCandidate = !bestPlacement.HasValue || distance < bestDistance;
                if (!preferCandidate && bestPlacement.HasValue && distance == bestDistance)
                {
                    // Keep the taskbar behavior biased toward the later free band
                    // (rightmost on horizontal taskbars, lowermost on vertical ones)
                    // instead of snapping left just because bands are iterated left-to-right.
                    preferCandidate = snapshot.IsVertical
                        ? candidatePlacement.Bottom > bestPlacement.Value.Bottom
                        : candidatePlacement.Right > bestPlacement.Value.Right;
                }

                if (preferCandidate)
                {
                    bestPlacement = candidatePlacement;
                    bestDistance = distance;
                }
            }

            if (!bestPlacement.HasValue)
            {
                return false;
            }

            placementBounds = bestPlacement.Value;
            return true;
        }

        private List<Rectangle> GetFreeTaskbarBands(TaskbarIntegrationSnapshot snapshot, Rectangle usableBounds)
        {
            List<Tuple<int, int>> occupiedIntervals = new List<Tuple<int, int>>();
            int occupiedSafetyPadding = this.ScaleValue(TaskbarOccupiedSafetyPadding);
            for (int i = 0; i < snapshot.OccupiedBounds.Length; i++)
            {
                Rectangle paddedOccupied = snapshot.OccupiedBounds[i];
                paddedOccupied.Inflate(occupiedSafetyPadding, occupiedSafetyPadding);
                Rectangle occupied = Rectangle.Intersect(paddedOccupied, usableBounds);
                if (occupied.Width <= 0 || occupied.Height <= 0)
                {
                    continue;
                }

                int start = snapshot.IsVertical ? occupied.Top : occupied.Left;
                int end = snapshot.IsVertical ? occupied.Bottom : occupied.Right;
                occupiedIntervals.Add(Tuple.Create(start, end));
            }

            occupiedIntervals.Sort(delegate(Tuple<int, int> a, Tuple<int, int> b)
            {
                return a.Item1.CompareTo(b.Item1);
            });

            List<Rectangle> freeBands = new List<Rectangle>();
            int cursor = snapshot.IsVertical ? usableBounds.Top : usableBounds.Left;
            int maximum = snapshot.IsVertical ? usableBounds.Bottom : usableBounds.Right;

            for (int i = 0; i < occupiedIntervals.Count; i++)
            {
                int intervalStart = Math.Max(cursor, occupiedIntervals[i].Item1);
                int intervalEnd = Math.Min(maximum, occupiedIntervals[i].Item2);
                if (intervalStart > cursor)
                {
                    freeBands.Add(CreateBandRectangle(snapshot, usableBounds, cursor, intervalStart));
                }

                if (intervalEnd > cursor)
                {
                    cursor = intervalEnd;
                }
            }

            if (cursor < maximum)
            {
                freeBands.Add(CreateBandRectangle(snapshot, usableBounds, cursor, maximum));
            }

            return freeBands;
        }

        private static Rectangle CreateBandRectangle(TaskbarIntegrationSnapshot snapshot, Rectangle usableBounds, int start, int end)
        {
            if (snapshot.IsVertical)
            {
                return new Rectangle(
                    usableBounds.Left,
                    start,
                    usableBounds.Width,
                    Math.Max(0, end - start));
            }

            return new Rectangle(
                start,
                usableBounds.Top,
                Math.Max(0, end - start),
                usableBounds.Height);
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum)
            {
                return minimum;
            }

            if (value > maximum)
            {
                return maximum;
            }

            return value;
        }
    }
}
