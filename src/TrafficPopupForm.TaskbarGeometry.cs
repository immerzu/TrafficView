using System;
using System.Drawing;
using System.Windows.Forms;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private Rectangle GetBestWorkingArea(Point preferredLocation, Point? anchorPoint)
        {
            if (anchorPoint.HasValue)
            {
                return Screen.FromPoint(anchorPoint.Value).WorkingArea;
            }

            Point safePreferredLocation = NormalizeRectangleOrigin(preferredLocation, this.Size);
            Rectangle preferredBounds = new Rectangle(safePreferredLocation, this.Size);
            Point preferredCenter = GetRectangleCenter(preferredBounds);
            Rectangle bestWorkingArea = Screen.FromPoint(safePreferredLocation).WorkingArea;
            long bestDistance = GetDistanceSquaredToRectangle(bestWorkingArea, preferredCenter);
            int bestIntersectionArea = GetIntersectionArea(preferredBounds, bestWorkingArea);

            foreach (Screen screen in Screen.AllScreens)
            {
                Rectangle candidateArea = screen.WorkingArea;
                int intersectionArea = GetIntersectionArea(preferredBounds, candidateArea);

                if (intersectionArea > bestIntersectionArea)
                {
                    bestWorkingArea = candidateArea;
                    bestIntersectionArea = intersectionArea;
                    bestDistance = GetDistanceSquaredToRectangle(candidateArea, preferredCenter);
                    continue;
                }

                if (intersectionArea == bestIntersectionArea)
                {
                    long candidateDistance = GetDistanceSquaredToRectangle(candidateArea, preferredCenter);
                    if (candidateDistance < bestDistance)
                    {
                        bestWorkingArea = candidateArea;
                        bestDistance = candidateDistance;
                    }
                }
            }

            return bestWorkingArea;
        }

        private static Point NormalizeRectangleOrigin(Point location, Size size)
        {
            int safeWidth = Math.Max(0, size.Width);
            int safeHeight = Math.Max(0, size.Height);
            long maxX = (long)int.MaxValue - safeWidth;
            long maxY = (long)int.MaxValue - safeHeight;

            return new Point(
                ClampToInt32(Math.Min(location.X, maxX)),
                ClampToInt32(Math.Min(location.Y, maxY)));
        }

        private Point ClampLocationToWorkingArea(Point preferredLocation, Size windowSize, Rectangle workingArea)
        {
            int minimumVisibleMargin = Math.Max(1, this.ScaleValue(BasePopupVisibleMargin));
            int x = ClampAxisToRange(
                preferredLocation.X,
                windowSize.Width,
                workingArea.Left,
                workingArea.Right,
                minimumVisibleMargin);
            int y = ClampAxisToRange(
                preferredLocation.Y,
                windowSize.Height,
                workingArea.Top,
                workingArea.Bottom,
                minimumVisibleMargin);
            return new Point(x, y);
        }

        private static int ClampAxisToRange(
            int preferredStart,
            int elementSize,
            int rangeStart,
            int rangeEndExclusive,
            int minimumVisibleMargin)
        {
            elementSize = Math.Max(1, elementSize);
            minimumVisibleMargin = Math.Max(1, minimumVisibleMargin);

            int availableSize = Math.Max(0, rangeEndExclusive - rangeStart);
            int minimumStart = rangeStart;
            int maximumStart = rangeEndExclusive - elementSize;

            if (elementSize > availableSize)
            {
                minimumStart = rangeStart - Math.Max(0, elementSize - minimumVisibleMargin);
                maximumStart = rangeEndExclusive - minimumVisibleMargin;
            }

            if (maximumStart < minimumStart)
            {
                maximumStart = minimumStart;
            }

            if (preferredStart < minimumStart)
            {
                return minimumStart;
            }

            if (preferredStart > maximumStart)
            {
                return maximumStart;
            }

            return preferredStart;
        }

        private static int GetIntersectionArea(Rectangle first, Rectangle second)
        {
            Rectangle intersection = Rectangle.Intersect(first, second);
            if (intersection.IsEmpty)
            {
                return 0;
            }

            return intersection.Width * intersection.Height;
        }

        private static Point GetRectangleCenter(Rectangle bounds)
        {
            long centerX = (long)bounds.Left + (bounds.Width / 2L);
            long centerY = (long)bounds.Top + (bounds.Height / 2L);
            return new Point(
                ClampToInt32(centerX),
                ClampToInt32(centerY));
        }

        private static long GetDistanceSquaredToRectangle(Rectangle rectangle, Point point)
        {
            long dx = 0L;
            if (point.X < rectangle.Left)
            {
                dx = (long)rectangle.Left - point.X;
            }
            else if (point.X >= rectangle.Right)
            {
                dx = (long)point.X - rectangle.Right;
            }

            long dy = 0L;
            if (point.Y < rectangle.Top)
            {
                dy = (long)rectangle.Top - point.Y;
            }
            else if (point.Y >= rectangle.Bottom)
            {
                dy = (long)point.Y - rectangle.Bottom;
            }

            return (dx * dx) + (dy * dy);
        }

        private static int ClampToInt32(long value)
        {
            if (value < int.MinValue)
            {
                return int.MinValue;
            }

            if (value > int.MaxValue)
            {
                return int.MaxValue;
            }

            return (int)value;
        }
    }
}
