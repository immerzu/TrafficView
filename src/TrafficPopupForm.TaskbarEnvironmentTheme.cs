using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private TaskbarVisualTheme CreateTaskbarVisualTheme(Rectangle taskbarBounds, Rectangle[] occupiedBounds)
        {
            Color sampledTaskbarColor;
            if (this.TrySampleTaskbarBackgroundColor(taskbarBounds, occupiedBounds, out sampledTaskbarColor))
            {
                return CreateTaskbarVisualThemeFromColor(sampledTaskbarColor, TaskbarIntegratedPanelTintAlpha);
            }

            return this.CreateFallbackTaskbarVisualTheme();
        }

        private bool TrySampleTaskbarBackgroundColor(
            Rectangle taskbarBounds,
            Rectangle[] occupiedBounds,
            out Color sampledTaskbarColor)
        {
            sampledTaskbarColor = Color.Empty;

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

                    return TryGetSampledTaskbarColorFromBitmap(
                        bitmap,
                        sampleBounds.Location,
                        occupiedBounds,
                        this.Visible ? this.GetCurrentPopupScreenBounds() : Rectangle.Empty,
                        this.ScaleValue(TaskbarPlacementMargin),
                        out sampledTaskbarColor);
                }
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce(
                    "taskbar-background-sampling-failed",
                    "Die Taskleistenfarbe konnte nicht direkt abgetastet werden. DWM-Akzentfarbe wird als stabile Naeherung verwendet.",
                    ex);
                return false;
            }
        }

        private static bool TryGetSampledTaskbarColorFromBitmap(
            Bitmap bitmap,
            Point screenOrigin,
            Rectangle[] occupiedBounds,
            Rectangle ownWindowBounds,
            int exclusionPadding,
            out Color sampledTaskbarColor)
        {
            sampledTaskbarColor = Color.Empty;

            List<int> redSamples = new List<int>();
            List<int> greenSamples = new List<int>();
            List<int> blueSamples = new List<int>();
            int stepX = Math.Max(1, bitmap.Width / 96);
            int stepY = Math.Max(1, bitmap.Height / 24);

            CollectTaskbarColorSamples(
                bitmap,
                screenOrigin,
                occupiedBounds,
                ownWindowBounds,
                exclusionPadding,
                true,
                redSamples,
                greenSamples,
                blueSamples,
                stepX,
                stepY);

            if (redSamples.Count < 12)
            {
                CollectTaskbarColorSamples(
                    bitmap,
                    screenOrigin,
                    occupiedBounds,
                    ownWindowBounds,
                    exclusionPadding,
                    false,
                    redSamples,
                    greenSamples,
                    blueSamples,
                    stepX,
                    stepY);
            }

            if (redSamples.Count <= 0)
            {
                return false;
            }

            redSamples.Sort();
            greenSamples.Sort();
            blueSamples.Sort();
            int medianIndex = redSamples.Count / 2;
            sampledTaskbarColor = Color.FromArgb(
                255,
                redSamples[medianIndex],
                greenSamples[medianIndex],
                blueSamples[medianIndex]);
            return true;
        }

        private static void CollectTaskbarColorSamples(
            Bitmap bitmap,
            Point screenOrigin,
            Rectangle[] occupiedBounds,
            Rectangle ownWindowBounds,
            int exclusionPadding,
            bool excludeOccupiedAreas,
            List<int> redSamples,
            List<int> greenSamples,
            List<int> blueSamples,
            int stepX,
            int stepY)
        {
            for (int y = stepY / 2; y < bitmap.Height; y += stepY)
            {
                for (int x = stepX / 2; x < bitmap.Width; x += stepX)
                {
                    Point screenPoint = new Point(screenOrigin.X + x, screenOrigin.Y + y);
                    if (!ownWindowBounds.IsEmpty && IsPointInPaddedRectangle(ownWindowBounds, screenPoint, exclusionPadding))
                    {
                        continue;
                    }

                    if (excludeOccupiedAreas &&
                        IsPointInAnyPaddedRectangle(occupiedBounds, screenPoint, exclusionPadding))
                    {
                        continue;
                    }

                    Color pixel = bitmap.GetPixel(x, y);
                    if (pixel.A <= 0)
                    {
                        continue;
                    }

                    redSamples.Add(pixel.R);
                    greenSamples.Add(pixel.G);
                    blueSamples.Add(pixel.B);
                }
            }
        }

        private static bool IsPointInAnyPaddedRectangle(Rectangle[] bounds, Point point, int padding)
        {
            if (bounds == null)
            {
                return false;
            }

            for (int i = 0; i < bounds.Length; i++)
            {
                if (IsPointInPaddedRectangle(bounds[i], point, padding))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPointInPaddedRectangle(Rectangle bounds, Point point, int padding)
        {
            if (bounds.IsEmpty)
            {
                return false;
            }

            Rectangle paddedBounds = bounds;
            paddedBounds.Inflate(Math.Max(0, padding), Math.Max(0, padding));
            return paddedBounds.Contains(point);
        }

        private static TaskbarVisualTheme CreateTaskbarVisualThemeFromColor(Color taskbarColor, byte overlayAlpha)
        {
            Color normalizedTaskbarColor = Color.FromArgb(255, taskbarColor.R, taskbarColor.G, taskbarColor.B);
            double luminance = GetRelativeLuminance(normalizedTaskbarColor);
            Color edgeReferenceColor = luminance < 0.42D
                ? Color.FromArgb(126, 164, 210)
                : Color.FromArgb(20, 32, 58);

            return new TaskbarVisualTheme
            {
                TaskbarColor = normalizedTaskbarColor,
                BaseColor = GetInterpolatedColor(normalizedTaskbarColor, BackgroundBlue, 0.12D),
                BorderColor = GetInterpolatedColor(normalizedTaskbarColor, edgeReferenceColor, 0.24D),
                DividerColor = GetInterpolatedColor(normalizedTaskbarColor, edgeReferenceColor, 0.18D),
                OverlayAlpha = overlayAlpha
            };
        }

        private TaskbarVisualTheme CreateFallbackTaskbarVisualTheme()
        {
            uint colorizationColor;
            bool opaqueBlend;
            Color accentColor = BackgroundBlue;
            if (DwmGetColorizationColor(out colorizationColor, out opaqueBlend) == 0)
            {
                accentColor = Color.FromArgb(
                    255,
                    (byte)((colorizationColor >> 16) & 0xFF),
                    (byte)((colorizationColor >> 8) & 0xFF),
                    (byte)(colorizationColor & 0xFF));
            }

            return new TaskbarVisualTheme
            {
                TaskbarColor = accentColor,
                BaseColor = GetInterpolatedColor(BackgroundBlue, accentColor, 0.34D),
                BorderColor = GetInterpolatedColor(BorderColor, accentColor, 0.24D),
                DividerColor = GetInterpolatedColor(DividerColor, accentColor, 0.22D),
                OverlayAlpha = TaskbarIntegratedPanelTintAlpha
            };
        }

        private static double GetRelativeLuminance(Color color)
        {
            return ((color.R * 0.2126D) + (color.G * 0.7152D) + (color.B * 0.0722D)) / 255D;
        }
    }
}
