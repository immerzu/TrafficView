using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private void DrawMiniTrafficSparkline(Graphics graphics, Rectangle meterBounds)
        {
            Rectangle bounds = this.GetSparklineBounds(meterBounds);
            if (bounds.Width < 12 || bounds.Height < 4)
            {
                return;
            }

            GraphicsState state = graphics.Save();
            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;
                int transparencyPercent = this.settings != null ? this.settings.TransparencyPercent : 0;
                bool ultraTransparent = transparencyPercent >= 100;

                if (!ultraTransparent)
                {
                    using (Pen guidePen = new Pen(Color.FromArgb(90, this.GetSparklineGuideBaseColor()), Math.Max(1F, this.ScaleFloat(1F))))
                    {
                        graphics.DrawLine(guidePen, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
                        graphics.DrawLine(guidePen, bounds.Left, bounds.Top + 1, bounds.Right, bounds.Top + 1);
                    }
                }

                TrafficHistorySample[] samples = this.GetOverlaySparklineSamples();
                if (samples.Length < 2)
                {
                    return;
                }

                double peak = 1D;
                for (int i = 0; i < samples.Length; i++)
                {
                    peak = Math.Max(peak, samples[i].DownloadBytesPerSecond);
                    peak = Math.Max(peak, samples[i].UploadBytesPerSecond);
                }

                if (!this.IsMiniGraphDisplayMode())
                {
                    PointF[] downloadPoints = CreateSparklinePoints(samples, bounds, peak, true, false);
                    PointF[] uploadPoints = CreateSparklinePoints(samples, bounds, peak, false, false);

                    float lineWidth = Math.Max(
                        ultraTransparent ? this.ScaleFloat(1.65F) : this.ScaleFloat(1.15F),
                        ultraTransparent ? 1.65F : 1.15F);
                    int lineAlpha = ultraTransparent ? 255 : 220;
                    int glowAlpha = ultraTransparent ? 132 : 92;
                    float glowWidth = lineWidth + Math.Max(this.ScaleFloat(0.9F), ultraTransparent ? 0.9F : 0.6F);

                    using (Pen downloadGlowPen = new Pen(Color.FromArgb(glowAlpha, this.GetSparklineDownloadBaseColor()), glowWidth))
                    using (Pen uploadGlowPen = new Pen(Color.FromArgb(glowAlpha, this.GetSparklineUploadBaseColor()), glowWidth))
                    using (Pen downloadPen = new Pen(Color.FromArgb(lineAlpha, this.GetSparklineDownloadBaseColor()), lineWidth))
                    using (Pen uploadPen = new Pen(Color.FromArgb(lineAlpha, this.GetSparklineUploadBaseColor()), lineWidth))
                    {
                        downloadGlowPen.LineJoin = LineJoin.Round;
                        downloadGlowPen.StartCap = LineCap.Round;
                        downloadGlowPen.EndCap = LineCap.Round;
                        uploadGlowPen.LineJoin = LineJoin.Round;
                        uploadGlowPen.StartCap = LineCap.Round;
                        uploadGlowPen.EndCap = LineCap.Round;
                        downloadPen.LineJoin = LineJoin.Round;
                        downloadPen.StartCap = LineCap.Round;
                        downloadPen.EndCap = LineCap.Round;
                        uploadPen.LineJoin = LineJoin.Round;
                        uploadPen.StartCap = LineCap.Round;
                        uploadPen.EndCap = LineCap.Round;

                        if (downloadPoints.Length >= 2)
                        {
                            graphics.DrawLines(downloadGlowPen, downloadPoints);
                            graphics.DrawLines(downloadPen, downloadPoints);
                        }

                        if (uploadPoints.Length >= 2)
                        {
                            graphics.DrawLines(uploadGlowPen, uploadPoints);
                            graphics.DrawLines(uploadPen, uploadPoints);
                        }
                    }

                    return;
                }

                PointF[] miniGraphDownloadPoints = CreateSparklinePoints(samples, bounds, peak, true, false);
                PointF[] miniGraphUploadPoints = CreateSparklinePoints(samples, bounds, peak, false, false);

                float miniGraphLineWidth = Math.Max(
                    ultraTransparent ? this.ScaleFloat(1.65F) : this.ScaleFloat(1.15F),
                    ultraTransparent ? 1.65F : 1.15F);
                int miniGraphLineAlpha = ultraTransparent ? 255 : 220;
                int miniGraphGlowAlpha = ultraTransparent ? 132 : 92;
                float miniGraphGlowWidth = miniGraphLineWidth + Math.Max(this.ScaleFloat(0.9F), ultraTransparent ? 0.9F : 0.6F);

                using (Pen downloadGlowPen = new Pen(Color.FromArgb(miniGraphGlowAlpha, this.GetSparklineDownloadBaseColor()), miniGraphGlowWidth))
                using (Pen uploadGlowPen = new Pen(Color.FromArgb(miniGraphGlowAlpha, this.GetSparklineUploadBaseColor()), miniGraphGlowWidth))
                using (Pen downloadPen = new Pen(Color.FromArgb(miniGraphLineAlpha, this.GetSparklineDownloadBaseColor()), miniGraphLineWidth))
                using (Pen uploadPen = new Pen(Color.FromArgb(miniGraphLineAlpha, this.GetSparklineUploadBaseColor()), miniGraphLineWidth))
                {
                    downloadGlowPen.LineJoin = LineJoin.Round;
                    downloadGlowPen.StartCap = LineCap.Round;
                    downloadGlowPen.EndCap = LineCap.Round;
                    uploadGlowPen.LineJoin = LineJoin.Round;
                    uploadGlowPen.StartCap = LineCap.Round;
                    uploadGlowPen.EndCap = LineCap.Round;
                    downloadPen.LineJoin = LineJoin.Round;
                    downloadPen.StartCap = LineCap.Round;
                    downloadPen.EndCap = LineCap.Round;
                    uploadPen.LineJoin = LineJoin.Round;
                    uploadPen.StartCap = LineCap.Round;
                    uploadPen.EndCap = LineCap.Round;

                    if (miniGraphDownloadPoints.Length >= 2)
                    {
                        graphics.DrawLines(downloadGlowPen, miniGraphDownloadPoints);
                        graphics.DrawLines(downloadPen, miniGraphDownloadPoints);
                    }

                    if (miniGraphUploadPoints.Length >= 2)
                    {
                        graphics.DrawLines(uploadGlowPen, miniGraphUploadPoints);
                        graphics.DrawLines(uploadPen, miniGraphUploadPoints);
                    }
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private Rectangle GetSparklineBounds(Rectangle meterBounds)
        {
            PanelSkinDefinition definition = this.GetCurrentPanelSkinDefinition();
            if (definition != null &&
                definition.SparklineBounds.HasValue &&
                this.IsBothSectionsVisible())
            {
                return this.ScaleSkinRectangle(definition.SparklineBounds.Value);
            }

            int left = this.ScaleValue(this.IsSimpleDisplayMode()
                ? SimpleSparklineLeft
                : this.IsMiniGraphDisplayMode()
                ? (int)MiniGraphSparklineLeft
                : 8);
            int top = this.ScaleValue(this.IsSimpleDisplayMode()
                ? SimpleSparklineTop
                : this.IsMiniGraphDisplayMode()
                ? (int)MiniGraphSparklineTop
                : 46);
            if (this.IsTaskbarIntegrationActive())
            {
                top = Math.Max(0, top - this.ScaleValue(4));
            }

            int width = this.IsRightSectionVisible()
                ? Math.Max(12, meterBounds.Left - left - this.ScaleValue(this.IsSimpleDisplayMode() ? 3 : 4))
                : Math.Max(18, this.ClientSize.Width - left - this.ScaleValue(6));
            int height = Math.Max(
                this.IsMiniGraphDisplayMode() ? 8 : 4,
                this.ScaleValue(this.IsSimpleDisplayMode()
                    ? SimpleSparklineHeight
                    : this.IsMiniGraphDisplayMode()
                    ? (int)MiniGraphSparklineHeight
                    : 7));
            return new Rectangle(left, top, width, height);
        }

    }
}
