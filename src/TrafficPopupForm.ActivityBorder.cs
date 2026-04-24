using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private void DrawActivityBorderLights(
            Graphics graphics,
            double visualDownloadFillRatio,
            double visualUploadFillRatio)
        {
            if (!this.ShouldUseActivityBorderGlow())
            {
                return;
            }

            double intensity = Math.Max(visualDownloadFillRatio, visualUploadFillRatio);
            double fadeRatio = GetActivityBorderFadeRatio(intensity);
            if (fadeRatio <= 0D ||
                this.Width < this.ScaleValue(24) ||
                this.Height < this.ScaleValue(18))
            {
                return;
            }

            float inset = Math.Max(2.5F, this.ScaleFloat(3.2F));
            RectangleF borderBounds = new RectangleF(
                inset,
                inset,
                Math.Max(1F, this.Width - (inset * 2F)),
                Math.Max(1F, this.Height - (inset * 2F)));
            float cornerRadius = Math.Min(
                this.ScaleFloat(BaseWindowCornerRadius),
                Math.Min(borderBounds.Width, borderBounds.Height) / 2F);
            float perimeter = GetRoundedRectanglePerimeter(borderBounds, cornerRadius);
            if (perimeter <= 1F)
            {
                return;
            }

            double direction = this.GetActivityBorderDirection();
            Color glowColor = direction >= 0D
                ? this.GetDownloadArrowBaseColor()
                : this.GetUploadArrowBaseColor();
            Color coreColor = direction >= 0D
                ? this.GetDownloadArrowHighBaseColor()
                : this.GetUploadArrowHighBaseColor();
            int lightCount = Math.Max(16, Math.Min(34, (int)Math.Round(perimeter / Math.Max(7.5F, this.ScaleFloat(8.8F)))));
            double phase = this.activityBorderRotationDegrees / 360D;
            double smoothedIntensity = SmoothStep(intensity) * fadeRatio;

            GraphicsState state = graphics.Save();
            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.CompositingQuality = CompositingQuality.HighQuality;

                for (int i = 0; i < lightCount; i++)
                {
                    double unit = i / (double)lightCount;
                    double travelAccent = GetActivityBorderTravelAccent(unit, phase, direction);
                    double wave = 0.24D + (0.76D * travelAccent);
                    double localIntensity = smoothedIntensity * wave;
                    PointF center = GetRoundedRectanglePoint(borderBounds, cornerRadius, unit);
                    float coreRadius = this.ScaleFloat(0.55F) + ((float)localIntensity * this.ScaleFloat(1.85F));
                    float glowRadius = coreRadius + this.ScaleFloat(2.8F) + ((float)localIntensity * this.ScaleFloat(4.4F));
                    int glowAlpha = Math.Min(240, (int)Math.Round(localIntensity * 212D));
                    int coreAlpha = Math.Min(255, (int)Math.Round(localIntensity * 255D));

                    this.DrawActivityBorderLight(
                        graphics,
                        center,
                        coreRadius,
                        glowRadius,
                        glowColor,
                        coreColor,
                        glowAlpha,
                        coreAlpha);
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private void DrawActivityBorderLight(
            Graphics graphics,
            PointF center,
            float coreRadius,
            float glowRadius,
            Color glowColor,
            Color coreColor,
            int glowAlpha,
            int coreAlpha)
        {
            RectangleF glowBounds = new RectangleF(
                center.X - glowRadius,
                center.Y - glowRadius,
                glowRadius * 2F,
                glowRadius * 2F);
            using (GraphicsPath glowPath = new GraphicsPath())
            {
                glowPath.AddEllipse(glowBounds);
                using (PathGradientBrush glowBrush = new PathGradientBrush(glowPath))
                {
                    glowBrush.CenterPoint = center;
                    glowBrush.CenterColor = Color.FromArgb(Math.Max(0, Math.Min(255, glowAlpha)), glowColor);
                    glowBrush.SurroundColors = new Color[] { Color.Transparent };
                    graphics.FillEllipse(glowBrush, glowBounds);
                }
            }

            RectangleF coreBounds = new RectangleF(
                center.X - coreRadius,
                center.Y - coreRadius,
                coreRadius * 2F,
                coreRadius * 2F);
            using (SolidBrush coreBrush = new SolidBrush(Color.FromArgb(Math.Max(0, Math.Min(255, coreAlpha)), coreColor)))
            using (SolidBrush highlightBrush = new SolidBrush(Color.FromArgb(Math.Max(0, Math.Min(255, coreAlpha)), Color.White)))
            {
                graphics.FillEllipse(coreBrush, coreBounds);
                graphics.FillEllipse(
                    highlightBrush,
                    coreBounds.Left + (coreRadius * 0.42F),
                    coreBounds.Top + (coreRadius * 0.30F),
                    Math.Max(0.6F, coreRadius * 0.72F),
                    Math.Max(0.6F, coreRadius * 0.72F));
            }
        }

        private double GetActivityBorderIntensity()
        {
            if (!this.ShouldUseActivityBorderGlow())
            {
                return 0D;
            }

            return Math.Max(
                this.GetVisualizedFillRatioForCurrentDownload(),
                this.GetVisualizedFillRatioForCurrentUpload());
        }

        private bool ShouldUseActivityBorderGlow()
        {
            return this.settings != null &&
                this.settings.ActivityBorderGlowEnabled &&
                !this.IsTaskbarIntegratedMode();
        }

        private double GetActivityBorderDirection()
        {
            if (this.IsSimpleDisplayMode())
            {
                double simpleDownloadInfluence = Math.Max(0D, this.displayedDownloadBytesPerSecond);
                double simpleUploadInfluence = Math.Max(0D, this.displayedUploadBytesPerSecond);
                double dominantBaseline = Math.Max(simpleDownloadInfluence, simpleUploadInfluence);
                double hysteresis = Math.Max(24D * 1024D, dominantBaseline * 0.18D);

                if (simpleDownloadInfluence > simpleUploadInfluence + hysteresis)
                {
                    this.simpleActivityBorderDirection = 1D;
                }
                else if (simpleUploadInfluence > simpleDownloadInfluence + hysteresis)
                {
                    this.simpleActivityBorderDirection = -1D;
                }

                return this.simpleActivityBorderDirection;
            }

            double downloadInfluence = this.GetVisualizedFillRatioForCurrentDownload();
            double uploadInfluence = this.GetVisualizedFillRatioForCurrentUpload();
            return uploadInfluence > downloadInfluence + 0.015D ? -1D : 1D;
        }
    }
}
