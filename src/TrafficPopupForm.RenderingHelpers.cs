using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private float GetDisplayedSweepAngle(double clampedRatio)
        {
            float sweepAngle = (float)(clampedRatio * 360D);
            return Math.Min(359.5F, Math.Max(sweepAngle, 8F));
        }

        private static Pen CreateRingPen(Color color, float width, bool roundedCaps)
        {
            Pen pen = new Pen(color, width);
            pen.Alignment = PenAlignment.Center;
            pen.LineJoin = LineJoin.Round;
            pen.StartCap = roundedCaps ? LineCap.Round : LineCap.Flat;
            pen.EndCap = roundedCaps ? LineCap.Round : LineCap.Flat;
            return pen;
        }

        private static RectangleF GetStableArcBounds(RectangleF bounds)
        {
            float left = AlignToHalfPixel(bounds.Left);
            float top = AlignToHalfPixel(bounds.Top);
            float right = AlignToHalfPixel(bounds.Right);
            float bottom = AlignToHalfPixel(bounds.Bottom);
            return new RectangleF(
                left,
                top,
                Math.Max(1F, right - left),
                Math.Max(1F, bottom - top));
        }

        private static float NormalizeArcAngle(float angle)
        {
            return (float)(Math.Round(angle * 4F, MidpointRounding.AwayFromZero) / 4D);
        }

        private static float NormalizeArcSweepAngle(float sweepAngle)
        {
            return Math.Max(0.05F, (float)(Math.Round(sweepAngle * 4F, MidpointRounding.AwayFromZero) / 4D));
        }

        private static float AlignToHalfPixel(float value)
        {
            return (float)(Math.Round(value * 2F, MidpointRounding.AwayFromZero) / 2D);
        }

        private static float NormalizeStrokeWidth(float value)
        {
            return Math.Max(0.5F, AlignToHalfPixel(value));
        }

        private static RectangleF GetStableTextBounds(Rectangle bounds)
        {
            return new RectangleF(
                AlignToHalfPixel(bounds.Left),
                AlignToHalfPixel(bounds.Top),
                Math.Max(1F, bounds.Width),
                Math.Max(1F, bounds.Height));
        }

        private static PointF GetStableArrowCenter(PointF center)
        {
            return new PointF(
                AlignToHalfPixel(center.X),
                AlignToHalfPixel(center.Y));
        }

        private static float NormalizeArrowDimension(float value)
        {
            return Math.Max(1F, AlignToHalfPixel(value));
        }

        private static PointF CreateAlignedPoint(float x, float y)
        {
            return new PointF(
                AlignToHalfPixel(x),
                AlignToHalfPixel(y));
        }

        private static RectangleF OffsetRectangle(RectangleF bounds, float offsetX, float offsetY)
        {
            return new RectangleF(
                bounds.Left + offsetX,
                bounds.Top + offsetY,
                bounds.Width,
                bounds.Height);
        }

        private void DrawPanelSeparator(
            Graphics graphics,
            int separatorInset,
            int separatorY,
            int separatorEndX,
            byte backgroundAlpha)
        {
            float startX = AlignToHalfPixel(separatorInset);
            float endX = AlignToHalfPixel(Math.Max(separatorInset, separatorEndX));
            float lineY = AlignToHalfPixel(separatorY);
            float accentWidth = Math.Max(1F, this.ScaleFloat(1F));
            float transitionOffset = Math.Max(1F, this.ScaleFloat(1F));
            Color panelBackgroundColor = this.GetPanelBackgroundBaseColor();
            Color panelDividerColor = this.GetPanelDividerBaseColor();

            using (Pen transitionPen = new Pen(
                ApplyAlpha(GetInterpolatedColor(panelBackgroundColor, panelDividerColor, 0.20D), Math.Min(backgroundAlpha, (byte)52)),
                accentWidth))
            using (Pen separatorPen = new Pen(
                ApplyAlpha(GetInterpolatedColor(panelDividerColor, panelBackgroundColor, 0.14D), Math.Min(backgroundAlpha, (byte)88)),
                accentWidth))
            {
                transitionPen.StartCap = LineCap.Flat;
                transitionPen.EndCap = LineCap.Flat;
                separatorPen.StartCap = LineCap.Flat;
                separatorPen.EndCap = LineCap.Flat;

                graphics.DrawLine(transitionPen, startX, lineY - transitionOffset, endX, lineY - transitionOffset);
                graphics.DrawLine(separatorPen, startX, lineY, endX, lineY);
                graphics.DrawLine(transitionPen, startX, lineY + transitionOffset, endX, lineY + transitionOffset);
            }
        }

        private static double SmoothStep(double value)
        {
            double clamped = Math.Max(0D, Math.Min(1D, value));
            return clamped * clamped * (3D - (2D * clamped));
        }

        private static Color ApplyAlpha(Color color, byte alpha)
        {
            int effectiveAlpha = (color.A * alpha) / 255;
            return Color.FromArgb(effectiveAlpha, color.R, color.G, color.B);
        }

        private static Color GetInterpolatedColor(Color fromColor, Color toColor, double ratio)
        {
            double clamped = Math.Max(0D, Math.Min(1D, ratio));
            int a = InterpolateChannel(fromColor.A, toColor.A, clamped);
            int r = InterpolateChannel(fromColor.R, toColor.R, clamped);
            int g = InterpolateChannel(fromColor.G, toColor.G, clamped);
            int b = InterpolateChannel(fromColor.B, toColor.B, clamped);
            return Color.FromArgb(a, r, g, b);
        }

        private static int InterpolateChannel(int fromValue, int toValue, double ratio)
        {
            return (int)Math.Round(fromValue + ((toValue - fromValue) * ratio), MidpointRounding.AwayFromZero);
        }

        private static RectangleF InflateRectangle(RectangleF bounds, float amount)
        {
            return new RectangleF(
                bounds.Left - amount,
                bounds.Top - amount,
                Math.Max(1F, bounds.Width + (amount * 2F)),
                Math.Max(1F, bounds.Height + (amount * 2F)));
        }

        private static ImageAttributes CreateAlphaImageAttributes(byte alpha)
        {
            ImageAttributes imageAttributes = new ImageAttributes();
            ColorMatrix colorMatrix = new ColorMatrix();
            colorMatrix.Matrix33 = alpha / 255F;
            imageAttributes.SetColorMatrix(colorMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
            return imageAttributes;
        }

        private static Bitmap CreateSelectiveTransparencyBitmap(Bitmap sourceBitmap, byte baseAlpha)
        {
            if (sourceBitmap == null)
            {
                return null;
            }

            Bitmap adjustedBitmap = new Bitmap(sourceBitmap.Width, sourceBitmap.Height, PixelFormat.Format32bppArgb);
            if (baseAlpha >= 255)
            {
                using (Graphics graphics = Graphics.FromImage(adjustedBitmap))
                {
                    graphics.DrawImageUnscaled(sourceBitmap, 0, 0);
                }

                return adjustedBitmap;
            }

            double baseAlphaRatio = baseAlpha / 255D;

            for (int y = 0; y < sourceBitmap.Height; y++)
            {
                for (int x = 0; x < sourceBitmap.Width; x++)
                {
                    Color pixel = sourceBitmap.GetPixel(x, y);
                    if (pixel.A <= 0)
                    {
                        adjustedBitmap.SetPixel(x, y, Color.Transparent);
                        continue;
                    }

                    double luminance = ((pixel.R * 0.2126D) + (pixel.G * 0.7152D) + (pixel.B * 0.0722D)) / 255D;
                    int maxChannel = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
                    int minChannel = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
                    double saturation = maxChannel <= 0
                        ? 0D
                        : (maxChannel - minChannel) / (double)maxChannel;

                    double highlightWeight = Clamp01((luminance - 0.22D) / 0.78D);
                    double accentWeight = Clamp01((saturation - 0.45D) / 0.55D) * 0.45D;
                    double preserveWeight = Clamp01(Math.Max(highlightWeight, accentWeight));
                    double effectiveAlphaRatio = baseAlphaRatio + ((1D - baseAlphaRatio) * preserveWeight);
                    int effectiveAlpha = (int)Math.Round(pixel.A * effectiveAlphaRatio);

                    adjustedBitmap.SetPixel(
                        x,
                        y,
                        Color.FromArgb(
                            Math.Max(0, Math.Min(255, effectiveAlpha)),
                            pixel.R,
                            pixel.G,
                            pixel.B));
                }
            }

            return adjustedBitmap;
        }

        private static double Clamp01(double value)
        {
            if (value <= 0D)
            {
                return 0D;
            }

            if (value >= 1D)
            {
                return 1D;
            }

            return value;
        }
    }
}
