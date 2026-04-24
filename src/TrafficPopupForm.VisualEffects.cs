using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private void DrawPanelDepthSurface(
            Graphics graphics,
            RectangleF innerBounds,
            float innerCornerRadius,
            byte backgroundAlpha)
        {
            float shadingInset = Math.Max(2.4F, this.ScaleFloat(2.8F));
            RectangleF shadingBounds = InflateRectangle(innerBounds, -shadingInset);
            float shadingCornerRadius = Math.Max(2F, innerCornerRadius - shadingInset + this.ScaleFloat(0.5F));

            using (GraphicsPath shadingPath = CreateRoundedPath(shadingBounds, shadingCornerRadius))
            using (PathGradientBrush shadingBrush = new PathGradientBrush(shadingPath))
            {
                Color basePanelColor = this.GetPanelBackgroundBaseColor();
                Color convexCenterColor = ApplyAlpha(
                    GetInterpolatedColor(basePanelColor, Color.FromArgb(104, 136, 194), 0.07D),
                    backgroundAlpha);
                Color convexEdgeColor = Color.FromArgb(0, basePanelColor.R, basePanelColor.G, basePanelColor.B);
                Color[] surroundColors = new Color[Math.Max(1, shadingPath.PointCount)];

                for (int i = 0; i < surroundColors.Length; i++)
                {
                    surroundColors[i] = convexEdgeColor;
                }

                shadingBrush.CenterPoint = new PointF(
                    shadingBounds.Left + (shadingBounds.Width * 0.50F),
                    shadingBounds.Top + (shadingBounds.Height * 0.46F));
                shadingBrush.FocusScales = new PointF(0.91F, 0.86F);
                shadingBrush.CenterColor = convexCenterColor;
                shadingBrush.SurroundColors = surroundColors;
                shadingBrush.WrapMode = WrapMode.Clamp;
                graphics.FillPath(shadingBrush, shadingPath);
            }
        }

        private void DrawPanelGlassSurface(
            Graphics graphics,
            RectangleF innerBounds,
            float innerCornerRadius,
            byte backgroundAlpha)
        {
            GraphicsState state = graphics.Save();

            try
            {
                using (GraphicsPath clipPath = CreateRoundedPath(innerBounds, innerCornerRadius))
                {
                    graphics.SetClip(clipPath, CombineMode.Intersect);

                    float inset = Math.Max(1F, this.ScaleFloat(1.1F));
                    RectangleF surfaceBounds = InflateRectangle(innerBounds, -inset);

                    using (LinearGradientBrush surfaceBrush = new LinearGradientBrush(
                        new PointF(surfaceBounds.Left, surfaceBounds.Top),
                        new PointF(surfaceBounds.Left, surfaceBounds.Bottom),
                        ApplyAlpha(Color.FromArgb(22, 236, 242, 250), backgroundAlpha),
                        Color.FromArgb(0, 236, 242, 250)))
                    {
                        ColorBlend blend = new ColorBlend();
                        blend.Positions = new float[] { 0F, 0.26F, 0.72F, 1F };
                        blend.Colors = new Color[]
                        {
                            ApplyAlpha(Color.FromArgb(24, 240, 246, 252), backgroundAlpha),
                            ApplyAlpha(Color.FromArgb(12, 224, 232, 244), backgroundAlpha),
                            Color.FromArgb(0, 224, 232, 244),
                            ApplyAlpha(Color.FromArgb(8, 10, 18, 28), backgroundAlpha)
                        };
                        surfaceBrush.InterpolationColors = blend;
                        graphics.FillRectangle(surfaceBrush, surfaceBounds);
                    }
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private void DrawMeterCenterDepth(Graphics graphics, RectangleF centerBounds)
        {
            GraphicsState state = graphics.Save();

            try
            {
                using (GraphicsPath centerPath = new GraphicsPath())
                {
                    centerPath.AddEllipse(centerBounds);
                    graphics.SetClip(centerPath, CombineMode.Intersect);

                    string meterCenterAssetPath = this.GetCurrentMeterCenterAssetPath();
                    if (!string.IsNullOrWhiteSpace(meterCenterAssetPath))
                    {
                        try
                        {
                            GraphicsState imageState = graphics.Save();
                            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            graphics.CompositingQuality = CompositingQuality.HighQuality;

                            Bitmap meterCenterImage = GetCachedMeterCenterAsset(meterCenterAssetPath);
                            if (meterCenterImage != null)
                            {
                                graphics.DrawImage(meterCenterImage, centerBounds);
                                graphics.Restore(imageState);
                                return;
                            }

                            graphics.Restore(imageState);
                        }
                        catch (Exception ex)
                        {
                            AppLog.WarnOnce(
                                "meter-center-asset-load-failed-" + meterCenterAssetPath,
                                string.Format(
                                    "The meter center asset could not be loaded from '{0}'. Procedural rendering will be used instead.",
                                    meterCenterAssetPath),
                                ex);
                        }
                    }

                    using (LinearGradientBrush centerBrush = new LinearGradientBrush(
                        new PointF(centerBounds.Left, centerBounds.Top),
                        new PointF(centerBounds.Right, centerBounds.Bottom),
                        GetInterpolatedColor(this.GetMeterCenterBaseColor(), Color.FromArgb(48, 78, 132), 0.32D),
                        GetInterpolatedColor(this.GetMeterCenterBaseColor(), Color.FromArgb(4, 10, 24), 0.42D)))
                    {
                        ColorBlend blend = new ColorBlend();
                        blend.Positions = new float[] { 0F, 0.32F, 0.68F, 1F };
                        blend.Colors = new Color[]
                        {
                            GetInterpolatedColor(this.GetMeterCenterBaseColor(), Color.FromArgb(66, 96, 154), 0.36D),
                            GetInterpolatedColor(this.GetMeterCenterBaseColor(), Color.FromArgb(30, 52, 98), 0.20D),
                            this.GetMeterCenterBaseColor(),
                            GetInterpolatedColor(this.GetMeterCenterBaseColor(), Color.FromArgb(2, 8, 18), 0.50D)
                        };
                        centerBrush.InterpolationColors = blend;
                        graphics.FillEllipse(centerBrush, centerBounds);
                    }
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private void DrawRotatingMeterGloss(
            Graphics graphics,
            RectangleF centerBounds,
            double visualDownloadFillRatio,
            double visualUploadFillRatio)
        {
            if (!this.settings.RotatingMeterGlossEnabled ||
                Math.Max(visualDownloadFillRatio, visualUploadFillRatio) <= MeterGlossAnimationThresholdRatio)
            {
                return;
            }

            GraphicsState state = graphics.Save();

            try
            {
                using (GraphicsPath centerPath = new GraphicsPath())
                {
                    centerPath.AddEllipse(centerBounds);
                    graphics.SetClip(centerPath, CombineMode.Intersect);
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;

                    float centerX = centerBounds.Left + (centerBounds.Width / 2F);
                    float centerY = centerBounds.Top + (centerBounds.Height / 2F);
                    float size = Math.Min(centerBounds.Width, centerBounds.Height);

                    graphics.TranslateTransform(centerX, centerY);
                    graphics.RotateTransform((float)this.meterGlossRotationDegrees);
                    graphics.TranslateTransform(-centerX, -centerY);

                    RectangleF upperSheenBounds = new RectangleF(
                        centerX - (size * 0.44F),
                        centerY - (size * 0.47F),
                        size * 0.88F,
                        size * 0.30F);
                    using (GraphicsPath upperSheenPath = new GraphicsPath())
                    {
                        upperSheenPath.AddEllipse(upperSheenBounds);
                        using (PathGradientBrush upperSheenBrush = new PathGradientBrush(upperSheenPath))
                        {
                            upperSheenBrush.CenterPoint = new PointF(
                                upperSheenBounds.Left + (upperSheenBounds.Width * 0.64F),
                                upperSheenBounds.Top + (upperSheenBounds.Height * 0.18F));
                            upperSheenBrush.CenterColor = Color.FromArgb(110, 255, 255, 255);
                            upperSheenBrush.SurroundColors = new Color[] { Color.Transparent };
                            graphics.FillEllipse(upperSheenBrush, upperSheenBounds);
                        }
                    }

                    RectangleF topRightHighlightBounds = new RectangleF(
                        centerX + (size * 0.14F),
                        centerY - (size * 0.28F),
                        size * 0.22F,
                        size * 0.22F);
                    using (GraphicsPath topRightHighlightPath = new GraphicsPath())
                    {
                        topRightHighlightPath.AddEllipse(topRightHighlightBounds);
                        using (PathGradientBrush topRightHighlightBrush = new PathGradientBrush(topRightHighlightPath))
                        {
                            topRightHighlightBrush.CenterPoint = new PointF(
                                topRightHighlightBounds.Left + (topRightHighlightBounds.Width * 0.60F),
                                topRightHighlightBounds.Top + (topRightHighlightBounds.Height * 0.44F));
                            topRightHighlightBrush.CenterColor = Color.FromArgb(122, 255, 255, 255);
                            topRightHighlightBrush.SurroundColors = new Color[] { Color.Transparent };
                            graphics.FillEllipse(topRightHighlightBrush, topRightHighlightBounds);
                        }
                    }

                    RectangleF lowerBlueGlowBounds = new RectangleF(
                        centerX - (size * 0.37F),
                        centerY + (size * 0.16F),
                        size * 0.48F,
                        size * 0.25F);
                    using (GraphicsPath lowerBlueGlowPath = new GraphicsPath())
                    {
                        lowerBlueGlowPath.AddEllipse(lowerBlueGlowBounds);
                        using (PathGradientBrush lowerBlueGlowBrush = new PathGradientBrush(lowerBlueGlowPath))
                        {
                            lowerBlueGlowBrush.CenterPoint = new PointF(
                                lowerBlueGlowBounds.Left + (lowerBlueGlowBounds.Width * 0.38F),
                                lowerBlueGlowBounds.Top + (lowerBlueGlowBounds.Height * 0.58F));
                            lowerBlueGlowBrush.CenterColor = Color.FromArgb(112, 78, 176, 255);
                            lowerBlueGlowBrush.SurroundColors = new Color[] { Color.Transparent };
                            graphics.FillEllipse(lowerBlueGlowBrush, lowerBlueGlowBounds);
                        }
                    }

                    RectangleF counterGlossBounds = new RectangleF(
                        centerX - (size * 0.34F),
                        centerY - (size * 0.09F),
                        size * 0.15F,
                        size * 0.15F);
                    using (GraphicsPath counterGlossPath = new GraphicsPath())
                    {
                        counterGlossPath.AddEllipse(counterGlossBounds);
                        using (PathGradientBrush counterGlossBrush = new PathGradientBrush(counterGlossPath))
                        {
                            counterGlossBrush.CenterPoint = new PointF(
                                counterGlossBounds.Left + (counterGlossBounds.Width * 0.46F),
                                counterGlossBounds.Top + (counterGlossBounds.Height * 0.46F));
                            counterGlossBrush.CenterColor = Color.FromArgb(68, 220, 242, 255);
                            counterGlossBrush.SurroundColors = new Color[] { Color.Transparent };
                            graphics.FillEllipse(counterGlossBrush, counterGlossBounds);
                        }
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
