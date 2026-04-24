using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private void DrawTaskbarIntegratedPanelEdgeGradient(
            Graphics graphics,
            RectangleF outerBounds,
            float cornerRadius,
            byte backgroundAlpha)
        {
            if (this.activeTaskbarSnapshot == null || backgroundAlpha == 0)
            {
                return;
            }

            float maximumGradientWidth = Math.Min(outerBounds.Width, outerBounds.Height) / 2F;
            float gradientWidth = Math.Min(
                Math.Max(2F, this.ScaleFloat(TaskbarIntegratedEdgeGradientWidth)),
                Math.Max(1F, maximumGradientWidth - 1F));
            if (gradientWidth <= 1F)
            {
                return;
            }

            Color taskbarColor = this.activeTaskbarSnapshot.Theme.TaskbarColor;
            int outerAlpha = Math.Min(
                (int)TaskbarIntegratedEdgeGradientOuterAlpha,
                Math.Max(72, (int)backgroundAlpha + 142));
            int lipAlpha = Math.Min(
                (int)TaskbarIntegratedEdgeGradientLipAlpha,
                Math.Max(96, (int)backgroundAlpha + 160));
            int steps = Math.Max(12, (int)Math.Ceiling(gradientWidth));

            GraphicsState state = graphics.Save();

            try
            {
                using (GraphicsPath clipPath = CreateRoundedPath(outerBounds, cornerRadius))
                {
                    graphics.SetClip(clipPath, CombineMode.Intersect);

                    float lipWidth = Math.Min(
                        gradientWidth,
                        Math.Max(1F, this.ScaleFloat(TaskbarIntegratedEdgeGradientLipWidth)));
                    RectangleF lipInnerBounds = InflateRectangle(outerBounds, -lipWidth);
                    using (GraphicsPath lipOuterPath = CreateRoundedPath(outerBounds, cornerRadius))
                    using (GraphicsPath lipInnerPath = CreateRoundedPath(
                        lipInnerBounds,
                        Math.Max(2F, cornerRadius - lipWidth)))
                    using (Region lipRegion = new Region(lipOuterPath))
                    using (SolidBrush lipBrush = new SolidBrush(ApplyAlpha(taskbarColor, (byte)lipAlpha)))
                    {
                        lipRegion.Exclude(lipInnerPath);
                        graphics.FillRegion(lipBrush, lipRegion);
                    }

                    for (int step = 0; step < steps; step++)
                    {
                        float outerProgress = step / (float)steps;
                        float innerProgress = (step + 1) / (float)steps;
                        float outerInset = outerProgress * gradientWidth;
                        float innerInset = innerProgress * gradientWidth;
                        double blend = 1D - SmoothStep((outerProgress + innerProgress) * 0.5F);
                        int alpha = (int)Math.Round(outerAlpha * blend);
                        if (alpha <= 1)
                        {
                            continue;
                        }

                        RectangleF bandOuterBounds = InflateRectangle(outerBounds, -outerInset);
                        RectangleF bandInnerBounds = InflateRectangle(outerBounds, -innerInset);
                        if (bandOuterBounds.Width <= 1F || bandOuterBounds.Height <= 1F)
                        {
                            break;
                        }

                        using (GraphicsPath bandOuterPath = CreateRoundedPath(
                            bandOuterBounds,
                            Math.Max(2F, cornerRadius - outerInset)))
                        using (GraphicsPath bandInnerPath = CreateRoundedPath(
                            bandInnerBounds,
                            Math.Max(2F, cornerRadius - innerInset)))
                        using (Region bandRegion = new Region(bandOuterPath))
                        using (SolidBrush bandBrush = new SolidBrush(ApplyAlpha(taskbarColor, (byte)Math.Min(255, alpha))))
                        {
                            bandRegion.Exclude(bandInnerPath);
                            graphics.FillRegion(bandBrush, bandRegion);
                        }
                    }
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private void DrawTaskbarIntegratedInnerOpacityBoost(
            Graphics graphics,
            RectangleF innerBounds,
            float innerCornerRadius,
            bool drawRightSection)
        {
            if (this.activeTaskbarSnapshot == null)
            {
                return;
            }

            GraphicsState state = graphics.Save();

            try
            {
                RectangleF focusBounds = drawRightSection
                    ? new RectangleF(
                        innerBounds.Left + (innerBounds.Width * 0.05F),
                        innerBounds.Top + (innerBounds.Height * 0.11F),
                        innerBounds.Width * 0.58F,
                        innerBounds.Height * 0.74F)
                    : new RectangleF(
                        innerBounds.Left + (innerBounds.Width * 0.12F),
                        innerBounds.Top + (innerBounds.Height * 0.11F),
                        innerBounds.Width * 0.76F,
                        innerBounds.Height * 0.74F);
                float focusCornerRadius = Math.Max(4F, Math.Min(focusBounds.Width, focusBounds.Height) * 0.18F);

                using (GraphicsPath clipPath = CreateRoundedPath(innerBounds, innerCornerRadius))
                using (GraphicsPath focusPath = CreateRoundedPath(focusBounds, focusCornerRadius))
                using (PathGradientBrush opacityBrush = new PathGradientBrush(focusPath))
                {
                    Color panelBackgroundColor = this.GetPanelBackgroundBaseColor();
                    Color[] surroundColors = new Color[Math.Max(1, focusPath.PointCount)];
                    for (int i = 0; i < surroundColors.Length; i++)
                    {
                        surroundColors[i] = Color.FromArgb(0, panelBackgroundColor.R, panelBackgroundColor.G, panelBackgroundColor.B);
                    }

                    graphics.SetClip(clipPath, CombineMode.Intersect);
                    opacityBrush.CenterPoint = new PointF(
                        focusBounds.Left + (focusBounds.Width * (drawRightSection ? 0.42F : 0.50F)),
                        focusBounds.Top + (focusBounds.Height * 0.50F));
                    opacityBrush.FocusScales = drawRightSection
                        ? new PointF(0.80F, 0.78F)
                        : new PointF(0.84F, 0.78F);
                    opacityBrush.CenterColor = ApplyAlpha(panelBackgroundColor, TaskbarIntegratedPanelInnerOpacityBoostAlpha);
                    opacityBrush.SurroundColors = surroundColors;
                    opacityBrush.WrapMode = WrapMode.Clamp;
                    graphics.FillPath(opacityBrush, focusPath);
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private void DrawTaskbarIntegratedInfoOpacityPlate(Graphics graphics, Rectangle meterBounds)
        {
            if (this.activeTaskbarSnapshot == null || !this.IsLeftSectionVisible())
            {
                return;
            }

            int left = this.ScaleValue(4);
            int right = this.GetLeftSectionRightBoundary(meterBounds, left + this.ScaleValue(44)) - this.ScaleValue(2);
            int top = this.ScaleValue(4);
            int bottom = Math.Min(
                this.ClientSize.Height - this.ScaleValue(6),
                this.uploadValueLabel.Bounds.Bottom + this.ScaleValue(4));
            if (right - left < this.ScaleValue(18) || bottom - top < this.ScaleValue(18))
            {
                return;
            }

            RectangleF plateBounds = new RectangleF(
                AlignToHalfPixel(left),
                AlignToHalfPixel(top),
                Math.Max(1F, right - left),
                Math.Max(1F, bottom - top));
            float radius = Math.Max(this.ScaleFloat(6F), Math.Min(plateBounds.Width, plateBounds.Height) * 0.16F);
            Color panelBackgroundColor = this.GetPanelBackgroundBaseColor();

            GraphicsState state = graphics.Save();
            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;

                using (GraphicsPath platePath = CreateRoundedPath(plateBounds, radius))
                using (LinearGradientBrush fillBrush = new LinearGradientBrush(
                    new PointF(plateBounds.Left, plateBounds.Top),
                    new PointF(plateBounds.Right, plateBounds.Bottom),
                    ApplyAlpha(GetInterpolatedColor(panelBackgroundColor, Color.FromArgb(6, 14, 34), 0.42D), TaskbarIntegratedInfoPlateFillAlpha),
                    ApplyAlpha(GetInterpolatedColor(panelBackgroundColor, Color.FromArgb(10, 20, 40), 0.30D), (byte)Math.Max(0, TaskbarIntegratedInfoPlateFillAlpha - 16))))
                using (LinearGradientBrush highlightBrush = new LinearGradientBrush(
                    new PointF(plateBounds.Left, plateBounds.Top),
                    new PointF(plateBounds.Left, plateBounds.Bottom),
                    Color.Transparent,
                    Color.Transparent))
                {
                    ColorBlend blend = new ColorBlend();
                    blend.Positions = new float[] { 0F, 0.16F, 0.48F, 1F };
                    blend.Colors = new Color[]
                    {
                        ApplyAlpha(Color.FromArgb(218, 234, 255), TaskbarIntegratedInfoPlateHighlightAlpha),
                        ApplyAlpha(Color.FromArgb(168, 202, 240), (byte)Math.Max(0, TaskbarIntegratedInfoPlateHighlightAlpha - 8)),
                        Color.FromArgb(0, 140, 176, 220),
                        Color.FromArgb(0, 24, 34, 48)
                    };
                    highlightBrush.InterpolationColors = blend;

                    graphics.FillPath(fillBrush, platePath);
                    graphics.FillPath(highlightBrush, platePath);
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }
    }
}
