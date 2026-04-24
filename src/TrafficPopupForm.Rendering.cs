using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            this.RenderPopupSurface(e.Graphics);
        }

        private void RenderPopupSurface(Graphics graphics)
        {
            graphics.Clear(Color.Transparent);
            this.RenderStaticPopupSurface(graphics);
            double downloadFillRatio = this.GetCurrentDownloadFillRatio();
            double uploadFillRatio = this.GetCurrentUploadFillRatio();
            this.RenderDynamicPopupSurface(
                graphics,
                downloadFillRatio,
                uploadFillRatio,
                this.GetVisualizedFillRatio(downloadFillRatio, true),
                this.GetVisualizedFillRatio(uploadFillRatio, false));
        }
    }
}
