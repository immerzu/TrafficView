using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private void DrawFullScaleGradientRing(
            Graphics graphics,
            RectangleF bounds,
            float strokeWidth,
            float startAngle,
            Color startColor,
            Color endColor,
            float glowWidth)
        {
            const int gradientStepCount = 48;
            float stepSweep = 360F / gradientStepCount;

            for (int stepIndex = 0; stepIndex < gradientStepCount; stepIndex++)
            {
                double colorRatio = gradientStepCount <= 1
                    ? 1D
                    : (double)(stepIndex + 1) / gradientStepCount;
                Color stepColor = GetInterpolatedColor(
                    startColor,
                    endColor,
                    SmoothStep(Math.Max(0D, Math.Min(1D, colorRatio))));

                this.DrawRingSegment(
                    graphics,
                    bounds,
                    strokeWidth,
                    startAngle + (stepIndex * stepSweep),
                    stepSweep + 0.18F,
                    stepColor,
                    glowWidth);
            }
        }

        private void DrawRingSegment(
            Graphics graphics,
            RectangleF bounds,
            float strokeWidth,
            float startAngle,
            float sweepAngle,
            Color color,
            float glowWidth)
        {
            if (sweepAngle <= 0.05F)
            {
                return;
            }

            float stableStrokeWidth = NormalizeStrokeWidth(strokeWidth);
            RectangleF stableBounds = GetStableArcBounds(bounds);
            RectangleF accentBounds = GetStableArcBounds(
                InflateRectangle(
                    stableBounds,
                    -Math.Max(0.2F, stableStrokeWidth * 0.03F)));
            float stableStartAngle = NormalizeArcAngle(startAngle);
            float stableSweepAngle = NormalizeArcSweepAngle(sweepAngle);
            int transparencyPercent = this.settings != null ? this.settings.TransparencyPercent : 0;
            bool ultraTransparent = transparencyPercent >= 100;
            GraphicsState state = graphics.Save();

            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;

                if (glowWidth > 0.05F)
                {
                    using (Pen glowPen = new Pen(
                        Color.FromArgb(ultraTransparent ? 210 : 120, color),
                        NormalizeStrokeWidth(stableStrokeWidth + glowWidth + (ultraTransparent ? this.ScaleFloat(1.1F) : 0F))))
                    {
                        glowPen.Alignment = PenAlignment.Center;
                        glowPen.LineJoin = LineJoin.MiterClipped;
                        glowPen.StartCap = LineCap.Flat;
                        glowPen.EndCap = LineCap.Flat;
                        graphics.DrawArc(glowPen, stableBounds, stableStartAngle, stableSweepAngle);
                    }
                }

                using (Pen segmentPen = new Pen(color, stableStrokeWidth))
                {
                    segmentPen.Alignment = PenAlignment.Center;
                    segmentPen.LineJoin = LineJoin.MiterClipped;
                    segmentPen.StartCap = LineCap.Flat;
                    segmentPen.EndCap = LineCap.Flat;
                    graphics.DrawArc(segmentPen, stableBounds, stableStartAngle, stableSweepAngle);
                }

                float accentWidth = NormalizeStrokeWidth(Math.Max(0.6F, stableStrokeWidth * 0.14F));
                float highlightSweep = NormalizeArcSweepAngle(Math.Max(0.1F, stableSweepAngle * 0.42F));
                float shadowStartAngle = NormalizeArcAngle(stableStartAngle + (stableSweepAngle * 0.44F));
                float shadowSweep = NormalizeArcSweepAngle(Math.Max(0.1F, stableSweepAngle * 0.46F));
                using (Pen highlightPen = new Pen(
                    ApplyAlpha(GetInterpolatedColor(color, Color.FromArgb(255, 245, 248, 255), 0.12D), 84),
                    accentWidth))
                using (Pen shadowPen = new Pen(
                    ApplyAlpha(GetInterpolatedColor(color, Color.FromArgb(255, 8, 14, 28), 0.16D), 92),
                    accentWidth))
                {
                    highlightPen.Alignment = PenAlignment.Center;
                    highlightPen.LineJoin = LineJoin.MiterClipped;
                    highlightPen.StartCap = LineCap.Flat;
                    highlightPen.EndCap = LineCap.Flat;
                    shadowPen.Alignment = PenAlignment.Center;
                    shadowPen.LineJoin = LineJoin.MiterClipped;
                    shadowPen.StartCap = LineCap.Flat;
                    shadowPen.EndCap = LineCap.Flat;
                    graphics.DrawArc(
                        highlightPen,
                        accentBounds,
                        stableStartAngle,
                        highlightSweep);
                    graphics.DrawArc(
                        shadowPen,
                        accentBounds,
                        shadowStartAngle,
                        shadowSweep);
                }

                if (ultraTransparent)
                {
                    using (Pen corePen = new Pen(
                        Color.FromArgb(236, GetInterpolatedColor(color, Color.White, 0.34D)),
                        NormalizeStrokeWidth(Math.Max(1F, stableStrokeWidth * 0.28F))))
                    {
                        corePen.Alignment = PenAlignment.Center;
                        corePen.LineJoin = LineJoin.Round;
                        corePen.StartCap = LineCap.Round;
                        corePen.EndCap = LineCap.Round;
                        graphics.DrawArc(corePen, accentBounds, stableStartAngle, stableSweepAngle);
                    }
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }
    }
}
