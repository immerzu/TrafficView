using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private Rectangle[] GetAugmentedTaskbarOccupiedBounds(
            Rectangle taskbarBounds,
            AppBarEdge edge,
            Rectangle[] structuralBounds)
        {
            Color taskbarBackgroundColor;
            if (!this.TrySampleTaskbarBackgroundColor(taskbarBounds, structuralBounds, out taskbarBackgroundColor))
            {
                return structuralBounds;
            }

            Rectangle[] visualBounds;
            if (!this.TryDetectTaskbarVisualOccupiedBounds(
                taskbarBounds,
                edge,
                structuralBounds,
                taskbarBackgroundColor,
                out visualBounds) ||
                visualBounds.Length <= 0)
            {
                return structuralBounds;
            }

            List<Rectangle> augmentedBounds = new List<Rectangle>(structuralBounds);
            augmentedBounds.AddRange(visualBounds);
            return augmentedBounds.ToArray();
        }

        private bool TryDetectTaskbarVisualOccupiedBounds(
            Rectangle taskbarBounds,
            AppBarEdge edge,
            Rectangle[] structuralBounds,
            Color taskbarBackgroundColor,
            out Rectangle[] visualBounds)
        {
            visualBounds = Array.Empty<Rectangle>();
            Rectangle sampleBounds = Rectangle.Intersect(taskbarBounds, Screen.PrimaryScreen.Bounds);
            if (sampleBounds.Width <= 0 || sampleBounds.Height <= 0)
            {
                return false;
            }

            try
            {
                using (Bitmap bitmap = new Bitmap(sampleBounds.Width, sampleBounds.Height, PixelFormat.Format32bppArgb))
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(sampleBounds.Location, Point.Empty, sampleBounds.Size);
                    visualBounds = DetectTaskbarVisualOccupiedBoundsFromBitmap(
                        bitmap,
                        sampleBounds.Location,
                        edge,
                        structuralBounds,
                        this.Visible ? this.GetCurrentPopupScreenBounds() : Rectangle.Empty,
                        this.ScaleValue(TaskbarOccupiedSafetyPadding),
                        taskbarBackgroundColor);
                    return true;
                }
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce(
                    "taskbar-visual-occupied-detection-failed",
                    "Die visuelle Erkennung belegter Taskleistenbereiche konnte nicht ausgefuehrt werden.",
                    ex);
                return false;
            }
        }

        private static Rectangle[] DetectTaskbarVisualOccupiedBoundsFromBitmap(
            Bitmap bitmap,
            Point screenOrigin,
            AppBarEdge edge,
            Rectangle[] structuralBounds,
            Rectangle ownWindowBounds,
            int safetyPadding,
            Color taskbarBackgroundColor)
        {
            bool isVertical = edge == AppBarEdge.Left || edge == AppBarEdge.Right;
            int mainLength = isVertical ? bitmap.Height : bitmap.Width;
            int crossLength = isVertical ? bitmap.Width : bitmap.Height;
            int crossInset = Math.Max(2, crossLength / 7);
            int crossStart = Math.Min(crossLength - 1, crossInset);
            int crossEnd = Math.Max(crossStart, crossLength - crossInset - 1);
            int requiredHits = Math.Max(3, (crossEnd - crossStart + 1) / 7);
            int allowedGap = Math.Max(2, safetyPadding / 2);
            int minimumVisualRun = Math.Max(8, safetyPadding * 2);
            List<Rectangle> detectedBounds = new List<Rectangle>();
            int runStart = -1;
            int lastHit = -1;

            for (int main = 0; main < mainLength; main++)
            {
                int hitCount = 0;
                for (int cross = crossStart; cross <= crossEnd; cross++)
                {
                    int localX = isVertical ? cross : main;
                    int localY = isVertical ? main : cross;
                    Point screenPoint = new Point(screenOrigin.X + localX, screenOrigin.Y + localY);
                    if ((!ownWindowBounds.IsEmpty && IsPointInPaddedRectangle(ownWindowBounds, screenPoint, safetyPadding)) ||
                        IsPointInAnyPaddedRectangle(structuralBounds, screenPoint, safetyPadding))
                    {
                        continue;
                    }

                    if (IsTaskbarForegroundPixel(bitmap.GetPixel(localX, localY), taskbarBackgroundColor))
                    {
                        hitCount++;
                    }
                }

                if (hitCount >= requiredHits)
                {
                    if (runStart < 0)
                    {
                        runStart = main;
                    }

                    lastHit = main;
                    continue;
                }

                if (runStart >= 0 && main - lastHit > allowedGap)
                {
                    AddDetectedTaskbarVisualRun(
                        detectedBounds,
                        screenOrigin,
                        isVertical,
                        runStart,
                        lastHit,
                        crossLength,
                        safetyPadding,
                        minimumVisualRun);
                    runStart = -1;
                    lastHit = -1;
                }
            }

            if (runStart >= 0)
            {
                AddDetectedTaskbarVisualRun(
                    detectedBounds,
                    screenOrigin,
                    isVertical,
                    runStart,
                    lastHit,
                    crossLength,
                    safetyPadding,
                    minimumVisualRun);
            }

            return detectedBounds.ToArray();
        }

        private static void AddDetectedTaskbarVisualRun(
            List<Rectangle> detectedBounds,
            Point screenOrigin,
            bool isVertical,
            int runStart,
            int runEnd,
            int crossLength,
            int safetyPadding,
            int minimumVisualRun)
        {
            if (runEnd < runStart || runEnd - runStart + 1 < minimumVisualRun)
            {
                return;
            }

            int start = Math.Max(0, runStart - safetyPadding);
            int length = Math.Max(1, runEnd - runStart + 1 + (2 * safetyPadding));
            detectedBounds.Add(isVertical
                ? new Rectangle(screenOrigin.X, screenOrigin.Y + start, crossLength, length)
                : new Rectangle(screenOrigin.X + start, screenOrigin.Y, length, crossLength));
        }

        private static bool IsTaskbarForegroundPixel(Color pixel, Color taskbarBackgroundColor)
        {
            int colorDistance =
                Math.Abs(pixel.R - taskbarBackgroundColor.R) +
                Math.Abs(pixel.G - taskbarBackgroundColor.G) +
                Math.Abs(pixel.B - taskbarBackgroundColor.B);
            double luminanceDelta = Math.Abs(GetRelativeLuminance(pixel) - GetRelativeLuminance(taskbarBackgroundColor));
            return colorDistance >= 72 || luminanceDelta >= 0.20D;
        }
    }
}
