using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private void RenderStaticPopupSurface(Graphics graphics)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.CompositingQuality = CompositingQuality.HighQuality;

            int outerInset = this.ScaleValue(this.GetPanelOuterInset());
            int separatorY = this.ScaleValue(BaseSeparatorY);
            int separatorInset = this.ScaleValue(BaseSeparatorInset);
            int cornerRadius = this.ScaleValue(BaseWindowCornerRadius);
            float strokeWidth = Math.Max(1F, this.ScaleFloat(1F));
            float sharedRingWidth = Math.Max(2F, this.ScaleFloat(6.2F));
            float centerInset = Math.Max(1F, this.ScaleFloat(this.IsMiniSoftLikeDisplayMode() ? 4.6F : 2.3F));
            byte backgroundAlpha = this.GetStaticPanelBackgroundAlpha();
            Color panelBackgroundColor = this.GetPanelBackgroundBaseColor();
            Color panelBorderColor = this.GetPanelBorderBaseColor();

            if (!this.ShouldDrawStaticBackgroundLayer())
            {
                return;
            }

            float strokeInset = strokeWidth / 2F;
            RectangleF outerBounds = new RectangleF(
                strokeInset,
                strokeInset,
                Math.Max(1F, this.Width - strokeWidth),
                Math.Max(1F, this.Height - strokeWidth));
            RectangleF innerBounds = new RectangleF(
                outerInset + strokeInset,
                outerInset + strokeInset,
                Math.Max(1F, this.Width - (outerInset * 2F) - strokeWidth),
                Math.Max(1F, this.Height - (outerInset * 2F) - strokeWidth));
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

            using (SolidBrush meterCenterBrush = new SolidBrush(this.GetMeterCenterBaseColor()))
            using (Pen meterCenterPen = new Pen(MeterCenterBorderColor, strokeWidth))
            {
                if (!this.TryDrawPanelBackgroundAsset(graphics, backgroundAlpha))
                {
                    using (GraphicsPath outerFillPath = CreateRoundedPath(outerBounds, cornerRadius))
                    using (GraphicsPath innerFillPath = CreateRoundedPath(innerBounds, Math.Max(2F, cornerRadius - outerInset)))
                    using (GraphicsPath outerStrokePath = CreateRoundedPath(outerBounds, cornerRadius))
                    using (GraphicsPath innerStrokePath = CreateRoundedPath(
                        innerBounds,
                        Math.Max(2F, cornerRadius - outerInset)))
                    using (SolidBrush borderBrush = new SolidBrush(ApplyAlpha(panelBorderColor, backgroundAlpha)))
                    using (SolidBrush fillBrush = new SolidBrush(ApplyAlpha(panelBackgroundColor, backgroundAlpha)))
                    using (Pen outerPen = new Pen(ApplyAlpha(EdgeSmoothingColor, backgroundAlpha), strokeWidth))
                    using (Pen innerPen = new Pen(ApplyAlpha(Color.FromArgb(60, 86, 144), backgroundAlpha), strokeWidth))
                    {
                        outerPen.LineJoin = LineJoin.Round;
                        innerPen.LineJoin = LineJoin.Round;
                        graphics.FillPath(borderBrush, outerFillPath);
                        graphics.FillPath(fillBrush, innerFillPath);
                        this.DrawPanelDepthSurface(
                            graphics,
                            innerBounds,
                            Math.Max(2F, cornerRadius - outerInset),
                            backgroundAlpha);
                        graphics.DrawPath(outerPen, outerStrokePath);
                        graphics.DrawPath(innerPen, innerStrokePath);
                    }
                }

                byte overlayAlpha = this.GetTaskbarBackgroundOverlayAlpha();
                if (overlayAlpha > 0)
                {
                    using (GraphicsPath tintPath = CreateRoundedPath(innerBounds, Math.Max(2F, cornerRadius - outerInset)))
                    using (SolidBrush tintBrush = new SolidBrush(ApplyAlpha(panelBackgroundColor, overlayAlpha)))
                    {
                        graphics.FillPath(tintBrush, tintPath);
                    }
                }

                if (this.IsGlassPanelSkinEnabled())
                {
                    this.DrawPanelGlassSurface(
                        graphics,
                        innerBounds,
                        Math.Max(2F, cornerRadius - outerInset),
                        backgroundAlpha);
                }

                if (this.IsTaskbarIntegrationActive())
                {
                    this.DrawTaskbarIntegratedInnerOpacityBoost(
                        graphics,
                        innerBounds,
                        Math.Max(2F, cornerRadius - outerInset),
                        drawRightSection);
                    this.DrawTaskbarIntegratedInfoOpacityPlate(graphics, meterBounds);
                }

                if (this.IsTaskbarIntegrationActive())
                {
                    RectangleF fullPanelBounds = new RectangleF(0F, 0F, this.Width, this.Height);
                    this.DrawTaskbarIntegratedPanelEdgeGradient(
                        graphics,
                        fullPanelBounds,
                        cornerRadius,
                        backgroundAlpha);
                }

                if (this.IsBothSectionsVisible())
                {
                    this.DrawPanelSeparator(
                        graphics,
                        separatorInset,
                        separatorY,
                        Math.Max(separatorInset, meterBounds.Left - this.ScaleValue(5)),
                        backgroundAlpha);
                }

                if (drawRightSection)
                {
                    this.DrawInterleavedTrafficRing(
                        graphics,
                        sharedRingBounds,
                        sharedRingWidth,
                        0D,
                        0D,
                        this.GetMeterTrackBaseColor(),
                        this.GetMeterTrackInnerBaseColor(),
                        this.GetDownloadRingLowBaseColor(),
                        this.GetDownloadRingLowBaseColor(),
                        this.GetUploadRingLowBaseColor(),
                        this.GetUploadRingLowBaseColor(),
                        true);

                    graphics.FillEllipse(meterCenterBrush, centerBounds);
                    this.DrawMeterCenterDepth(graphics, centerBounds);
                    graphics.DrawEllipse(meterCenterPen, centerBounds);
                }
            }
        }
    }
}
