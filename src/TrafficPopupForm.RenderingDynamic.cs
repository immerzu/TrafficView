using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private void RenderDynamicPopupSurface(
            Graphics graphics,
            double downloadFillRatio,
            double uploadFillRatio,
            double visualDownloadFillRatio,
            double visualUploadFillRatio)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.CompositingQuality = CompositingQuality.HighQuality;

            float sharedRingWidth = Math.Max(2F, this.ScaleFloat(6.2F));
            float centerInset = Math.Max(1F, this.ScaleFloat(this.IsMiniSoftLikeDisplayMode() ? 4.6F : 2.3F));
            float iconInset = Math.Max(1F, this.ScaleFloat(this.IsMiniSoftLikeDisplayMode() ? 4.6F : 2.3F));

            bool drawLeftSection = this.IsLeftSectionVisible();
            bool drawRightSection = this.IsRightSectionVisible();
            Rectangle meterBounds = drawRightSection
                ? this.GetDownloadMeterBounds()
                : Rectangle.Empty;
            RectangleF sharedRingBounds = drawRightSection
                ? this.CreateInsetBounds(meterBounds, sharedRingWidth / 2F)
                : RectangleF.Empty;
            RectangleF centerBounds = drawRightSection
                ? this.CreateInsetBounds(meterBounds, sharedRingWidth + centerInset)
                : RectangleF.Empty;
            RectangleF iconBounds = drawRightSection
                ? this.CreateInsetBounds(meterBounds, sharedRingWidth + iconInset)
                : RectangleF.Empty;

            Color downloadRingEndColor = GetInterpolatedColor(
                this.GetDownloadRingLowBaseColor(),
                this.GetDownloadRingHighBaseColor(),
                SmoothStep(visualDownloadFillRatio));
            Color uploadRingEndColor = GetInterpolatedColor(
                this.GetUploadRingLowBaseColor(),
                this.GetUploadRingHighBaseColor(),
                SmoothStep(visualUploadFillRatio));

            if (drawLeftSection &&
                this.IsReadableInfoPanelSkinEnabled() &&
                !this.IsHudOnlyTransparencyMode())
            {
                this.DrawReadableTrafficInfoPanel(graphics, meterBounds);
            }
            else if (drawLeftSection && !this.IsHudOnlyTransparencyMode())
            {
                this.DrawTransparencyAwareInfoPanel(graphics, meterBounds);
            }

            if (drawLeftSection && !this.IsHudOnlyTransparencyMode() && this.ShouldDrawMeterValueSupport())
            {
                this.DrawMeterValueBalanceSupport(graphics, meterBounds);
            }

            if (drawLeftSection)
            {
                this.DrawTrafficTexts(graphics);
            }

            if (this.ShouldDrawSparkline())
            {
                this.DrawMiniTrafficSparkline(graphics, meterBounds);
            }

            if (drawRightSection && this.IsHudOnlyTransparencyMode())
            {
                this.DrawHudOnlyMeterGuideCircles(graphics, sharedRingBounds, sharedRingWidth);
            }

            if (this.ShouldDrawDynamicRing())
            {
                this.DrawInterleavedTrafficRing(
                    graphics,
                    sharedRingBounds,
                    sharedRingWidth,
                    visualDownloadFillRatio,
                    visualUploadFillRatio,
                    this.GetMeterTrackBaseColor(),
                    this.GetMeterTrackInnerBaseColor(),
                    this.GetDownloadRingLowBaseColor(),
                    downloadRingEndColor,
                    this.GetUploadRingLowBaseColor(),
                    uploadRingEndColor,
                    false);
            }

            if (drawRightSection)
            {
                RectangleF glossBounds = centerBounds;
                if (!this.IsMiniSoftLikeDisplayMode())
                {
                    float glossInset = this.ScaleFloat(StandardMeterGlossExtraInset);
                    glossBounds = new RectangleF(
                        centerBounds.Left + glossInset,
                        centerBounds.Top + glossInset,
                        Math.Max(2F, centerBounds.Width - (glossInset * 2F)),
                        Math.Max(2F, centerBounds.Height - (glossInset * 2F)));
                }

                this.DrawRotatingMeterGloss(
                    graphics,
                    glossBounds,
                    visualDownloadFillRatio,
                    visualUploadFillRatio);
            }

            if (this.ShouldDrawCenterTrafficArrows())
            {
                this.DrawCenterTrafficArrows(
                    graphics,
                    centerBounds,
                    iconBounds,
                    downloadFillRatio,
                    uploadFillRatio,
                    visualDownloadFillRatio,
                    visualUploadFillRatio);
            }

            this.DrawActivityBorderLights(
                graphics,
                visualDownloadFillRatio,
                visualUploadFillRatio);
        }

        private void DrawHudOnlyMeterGuideCircles(
            Graphics graphics,
            RectangleF sharedRingBounds,
            float sharedRingWidth)
        {
            GraphicsState state = graphics.Save();

            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;

                float outlineWidth = 1F;
                float stableRingWidth = NormalizeStrokeWidth(sharedRingWidth);
                float edgeOffset = Math.Max(0F, (stableRingWidth - outlineWidth) / 2F);
                RectangleF baseBounds = GetStableArcBounds(sharedRingBounds);
                RectangleF outerBounds = GetStableArcBounds(
                    InflateRectangle(baseBounds, edgeOffset + 2F));
                RectangleF innerBounds = GetStableArcBounds(
                    InflateRectangle(baseBounds, -(edgeOffset + 2F)));

                Color outerColor = Color.FromArgb(220, 164, 228, 255);
                Color innerColor = Color.FromArgb(212, 132, 210, 255);

                using (Pen outerPen = new Pen(outerColor, outlineWidth))
                using (Pen innerPen = new Pen(innerColor, outlineWidth))
                {
                    outerPen.Alignment = PenAlignment.Center;
                    innerPen.Alignment = PenAlignment.Center;
                    graphics.DrawEllipse(outerPen, outerBounds);
                    graphics.DrawEllipse(innerPen, innerBounds);
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }
    }
}
