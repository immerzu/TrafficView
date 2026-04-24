using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private void RefreshVisualSurface()
        {
            if (!this.IsHandleCreated || this.Width <= 0 || this.Height <= 0)
            {
                return;
            }

            try
            {
                this.UpdateLayeredWindowContent();
            }
            catch (ExternalException ex)
            {
                this.HandleOverlayRenderFailure(
                    "overlay-refresh-external-exception",
                    "Overlay refresh failed because of a GDI/native rendering exception.",
                    ex);
            }
            catch (ArgumentException ex)
            {
                this.HandleOverlayRenderFailure(
                    "overlay-refresh-argument-exception",
                    "Overlay refresh failed because of an invalid rendering argument.",
                    ex);
            }
            catch (InvalidOperationException ex)
            {
                this.HandleOverlayRenderFailure(
                    "overlay-refresh-invalid-operation",
                    "Overlay refresh failed because the rendering state was not valid.",
                    ex);
            }
        }

        private void UpdateLayeredWindowContent()
        {
            if (!this.Visible)
            {
                return;
            }

            if (!this.EnsureSurfaceBitmaps())
            {
                return;
            }

            bool staticWasDirty = this.staticSurfaceDirty;
            if (!this.RebuildStaticSurfaceIfNeeded())
            {
                return;
            }

            double downloadFillRatio = this.GetCurrentDownloadFillRatio();
            double uploadFillRatio = this.GetCurrentUploadFillRatio();
            double visualDownloadFillRatio = this.GetVisualizedFillRatio(downloadFillRatio, true);
            double visualUploadFillRatio = this.GetVisualizedFillRatio(uploadFillRatio, false);
            int animationFrame = this.GetAnimationFrameIndex();
            bool needCompose =
                staticWasDirty ||
                this.composedSurfaceBitmap == null ||
                this.lastRenderedAnimationFrame != animationFrame ||
                !string.Equals(this.lastRenderedDownloadText, this.downloadValueLabel.Text, StringComparison.Ordinal) ||
                !string.Equals(this.lastRenderedUploadText, this.uploadValueLabel.Text, StringComparison.Ordinal) ||
                this.lastRenderedTrafficHistoryVersion != this.trafficHistoryVersion ||
                !AreNearlyEqual(this.lastRenderedDownloadFillRatio, visualDownloadFillRatio) ||
                !AreNearlyEqual(this.lastRenderedUploadFillRatio, visualUploadFillRatio);

            if (needCompose)
            {
                try
                {
                    using (Graphics graphics = Graphics.FromImage(this.composedSurfaceBitmap))
                    {
                        graphics.Clear(Color.Transparent);
                        graphics.DrawImageUnscaled(this.staticSurfaceBitmap, 0, 0);
                        this.RenderDynamicPopupSurface(
                            graphics,
                            downloadFillRatio,
                            uploadFillRatio,
                            visualDownloadFillRatio,
                            visualUploadFillRatio);
                        this.TrimTaskbarPresentationFringe(graphics);
                    }
                }
                catch (ExternalException ex)
                {
                    this.HandleOverlayRenderFailure(
                        "overlay-compose-external-exception",
                        "Overlay composition failed because of a GDI/native rendering exception.",
                        ex);
                    return;
                }
                catch (ArgumentException ex)
                {
                    this.HandleOverlayRenderFailure(
                        "overlay-compose-argument-exception",
                        "Overlay composition failed because of an invalid rendering argument.",
                        ex);
                    return;
                }

                this.lastRenderedAnimationFrame = animationFrame;
                this.lastRenderedDownloadText = this.downloadValueLabel.Text;
                this.lastRenderedUploadText = this.uploadValueLabel.Text;
                this.lastRenderedTrafficHistoryVersion = this.trafficHistoryVersion;
                this.lastRenderedDownloadFillRatio = visualDownloadFillRatio;
                this.lastRenderedUploadFillRatio = visualUploadFillRatio;
            }

            if (needCompose ||
                this.lastPresentedLocation != this.Location ||
                this.lastPresentedSize != this.Size)
            {
                if (this.PresentLayeredBitmap(this.composedSurfaceBitmap))
                {
                    this.lastPresentedLocation = this.Location;
                    this.lastPresentedSize = this.Size;
                }
            }
        }

        private void TrimTaskbarPresentationFringe(Graphics graphics)
        {
            if (!this.IsTaskbarIntegrationActive() ||
                graphics == null ||
                this.Width <= 2 ||
                this.Height <= 2)
            {
                return;
            }

            int trimInset = Math.Max(1, this.ScaleValue(1));
            Rectangle innerBounds = new Rectangle(
                trimInset,
                trimInset,
                Math.Max(1, this.Width - (trimInset * 2)),
                Math.Max(1, this.Height - (trimInset * 2)));
            int cornerRadius = Math.Max(2, this.ScaleValue(BaseWindowCornerRadius) - trimInset);

            GraphicsState state = graphics.Save();
            try
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.SmoothingMode = SmoothingMode.None;

                using (Region outerRegion = new Region(new Rectangle(0, 0, this.Width, this.Height)))
                using (GraphicsPath innerPath = CreateRoundedPath(innerBounds, cornerRadius))
                using (SolidBrush clearBrush = new SolidBrush(Color.Transparent))
                {
                    outerRegion.Exclude(innerPath);
                    graphics.FillRegion(clearBrush, outerRegion);
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private bool RebuildStaticSurfaceIfNeeded()
        {
            if (!this.staticSurfaceDirty || this.staticSurfaceBitmap == null)
            {
                return true;
            }

            try
            {
                using (Graphics graphics = Graphics.FromImage(this.staticSurfaceBitmap))
                {
                    graphics.Clear(Color.Transparent);
                    this.RenderStaticPopupSurface(graphics);
                }
            }
            catch (ExternalException ex)
            {
                this.HandleOverlayRenderFailure(
                    "overlay-static-surface-external-exception",
                    "Overlay static surface rebuild failed because of a GDI/native rendering exception.",
                    ex);
                return false;
            }
            catch (ArgumentException ex)
            {
                this.HandleOverlayRenderFailure(
                    "overlay-static-surface-argument-exception",
                    "Overlay static surface rebuild failed because of an invalid rendering argument.",
                    ex);
                return false;
            }

            this.staticSurfaceDirty = false;
            return true;
        }

    }
}
