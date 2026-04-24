using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private void DrawReadableTrafficInfoPanel(Graphics graphics, Rectangle meterBounds)
        {
            int left = this.ScaleValue(3);
            int right = this.GetLeftSectionRightBoundary(meterBounds, left + this.ScaleValue(44));
            int top = this.ScaleValue(4);
            int bottom = Math.Min(
                this.ClientSize.Height - this.ScaleValue(7),
                this.uploadValueLabel.Bounds.Bottom + this.ScaleValue(5));
            if (right - left < this.ScaleValue(16) || bottom - top < this.ScaleValue(18))
            {
                return;
            }

            RectangleF bounds = new RectangleF(
                AlignToHalfPixel(left),
                AlignToHalfPixel(top),
                Math.Max(1F, right - left),
                Math.Max(1F, bottom - top));
            float radius = Math.Max(this.ScaleFloat(6F), Math.Min(bounds.Width, bounds.Height) * 0.18F);
            GraphicsState state = graphics.Save();

            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;

                int transparencyPercent = this.settings != null ? this.settings.TransparencyPercent : 0;
                int fillAlpha = Math.Min(188, 118 + (int)Math.Round(transparencyPercent * 0.55D));
                int borderAlpha = Math.Min(96, 48 + (int)Math.Round(transparencyPercent * 0.30D));
                int innerAlpha = Math.Min(64, 28 + (int)Math.Round(transparencyPercent * 0.18D));
                int highlightStrongAlpha = Math.Min(92, 66 + (int)Math.Round(transparencyPercent * 0.20D));
                int highlightSoftAlpha = Math.Min(44, 28 + (int)Math.Round(transparencyPercent * 0.14D));
                Color panelBackgroundColor = this.GetPanelBackgroundBaseColor();
                Color panelBorderColor = this.GetPanelBorderBaseColor();

                using (GraphicsPath platePath = CreateRoundedPath(bounds, radius))
                using (GraphicsPath innerPath = CreateRoundedPath(
                    InflateRectangle(bounds, -this.ScaleFloat(1.5F)),
                    Math.Max(2F, radius - this.ScaleFloat(1.5F))))
                using (SolidBrush fillBrush = new SolidBrush(ApplyAlpha(GetInterpolatedColor(panelBackgroundColor, Color.FromArgb(5, 14, 34), 0.40D), (byte)fillAlpha)))
                using (Pen borderPen = new Pen(ApplyAlpha(GetInterpolatedColor(panelBorderColor, Color.FromArgb(164, 215, 255), 0.32D), (byte)borderAlpha), Math.Max(0.8F, this.ScaleFloat(0.8F))))
                using (Pen innerPen = new Pen(Color.FromArgb(innerAlpha, 255, 255, 255), Math.Max(0.6F, this.ScaleFloat(0.6F))))
                using (LinearGradientBrush highlightBrush = new LinearGradientBrush(
                    new PointF(bounds.Left, bounds.Top),
                    new PointF(bounds.Left, bounds.Bottom),
                    Color.Transparent,
                    Color.Transparent))
                {
                    ColorBlend blend = new ColorBlend();
                    blend.Positions = new float[] { 0F, 0.12F, 0.34F, 1F };
                    blend.Colors = new Color[]
                    {
                        Color.FromArgb(highlightStrongAlpha, 220, 238, 255),
                        Color.FromArgb(highlightSoftAlpha, 188, 220, 255),
                        Color.FromArgb(10, 96, 140, 196),
                        Color.Transparent
                    };
                    highlightBrush.InterpolationColors = blend;

                    graphics.FillPath(fillBrush, platePath);
                    graphics.FillPath(highlightBrush, platePath);
                    graphics.DrawPath(borderPen, platePath);
                    graphics.DrawPath(innerPen, innerPath);
                }

                float separatorLeft = bounds.Left + this.ScaleFloat(10F);
                float separatorRight = bounds.Right - this.ScaleFloat(12F);
                float separatorTop = this.downloadValueLabel.Bounds.Bottom + this.ScaleFloat(2F);
                float separatorBottom = this.uploadCaptionLabel.Bounds.Top - this.ScaleFloat(1F);
                float separatorY = (separatorTop + separatorBottom) / 2F;
                using (Pen separatorPen = new Pen(ApplyAlpha(GetInterpolatedColor(this.GetPanelDividerBaseColor(), Color.FromArgb(150, 196, 255), 0.24D), 28), Math.Max(0.8F, this.ScaleFloat(0.8F))))
                {
                    graphics.DrawLine(
                        separatorPen,
                        AlignToHalfPixel(separatorLeft),
                        AlignToHalfPixel(separatorY),
                        AlignToHalfPixel(separatorRight),
                        AlignToHalfPixel(separatorY));
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private void DrawTransparencyAwareInfoPanel(Graphics graphics, Rectangle meterBounds)
        {
            int transparencyPercent = this.settings != null ? this.settings.TransparencyPercent : 0;
            if (transparencyPercent <= 0)
            {
                return;
            }

            int left = this.ScaleValue(3);
            int right = this.GetLeftSectionRightBoundary(meterBounds, left + this.ScaleValue(44));
            int top = Math.Max(0, this.downloadCaptionLabel.Bounds.Top - this.ScaleValue(3));
            int bottom = Math.Min(this.ClientSize.Height, this.uploadValueLabel.Bounds.Bottom + this.ScaleValue(4));
            if (right - left < this.ScaleValue(16) || bottom - top < this.ScaleValue(16))
            {
                return;
            }

            RectangleF bounds = new RectangleF(
                AlignToHalfPixel(left),
                AlignToHalfPixel(top),
                Math.Max(1F, right - left),
                Math.Max(1F, bottom - top));
            float radius = Math.Max(this.ScaleFloat(5.5F), Math.Min(bounds.Width, bounds.Height) * 0.16F);
            int fillAlpha = Math.Min(176, 42 + (int)Math.Round(transparencyPercent * 0.90D));
            int borderAlpha = Math.Min(82, 18 + (int)Math.Round(transparencyPercent * 0.34D));
            int highlightAlpha = Math.Min(62, 14 + (int)Math.Round(transparencyPercent * 0.26D));
            Color panelBackgroundColor = this.GetPanelBackgroundBaseColor();
            Color panelBorderColor = this.GetPanelBorderBaseColor();

            GraphicsState state = graphics.Save();
            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;

                using (GraphicsPath platePath = CreateRoundedPath(bounds, radius))
                using (LinearGradientBrush fillBrush = new LinearGradientBrush(
                    new PointF(bounds.Left, bounds.Top),
                    new PointF(bounds.Right, bounds.Bottom),
                    ApplyAlpha(GetInterpolatedColor(panelBackgroundColor, Color.FromArgb(4, 10, 24), 0.42D), (byte)fillAlpha),
                    ApplyAlpha(GetInterpolatedColor(panelBackgroundColor, Color.FromArgb(8, 18, 36), 0.34D), (byte)Math.Max(0, fillAlpha - 18))))
                using (LinearGradientBrush highlightBrush = new LinearGradientBrush(
                    new PointF(bounds.Left, bounds.Top),
                    new PointF(bounds.Left, bounds.Bottom),
                    Color.Transparent,
                    Color.Transparent))
                using (Pen borderPen = new Pen(ApplyAlpha(GetInterpolatedColor(panelBorderColor, Color.FromArgb(132, 192, 235), 0.28D), (byte)borderAlpha), Math.Max(0.8F, this.ScaleFloat(0.8F))))
                {
                    ColorBlend blend = new ColorBlend();
                    blend.Positions = new float[] { 0F, 0.16F, 0.40F, 1F };
                    blend.Colors = new Color[]
                    {
                        Color.FromArgb(highlightAlpha, 210, 232, 255),
                        Color.FromArgb(Math.Max(0, highlightAlpha - 20), 168, 206, 244),
                        Color.FromArgb(10, 90, 132, 176),
                        Color.Transparent
                    };
                    highlightBrush.InterpolationColors = blend;

                    graphics.FillPath(fillBrush, platePath);
                    graphics.FillPath(highlightBrush, platePath);
                    graphics.DrawPath(borderPen, platePath);
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private void DrawMeterValueBalanceSupport(Graphics graphics, Rectangle meterBounds)
        {
            int valueRight = Math.Max(this.downloadValueLabel.Bounds.Right, this.uploadValueLabel.Bounds.Right);
            int supportLeft = Math.Max(this.ScaleValue(40), valueRight - this.ScaleValue(8));
            int supportRight = this.IsRightSectionVisible()
                ? meterBounds.Left + this.ScaleValue(2)
                : this.ClientSize.Width - this.ScaleValue(4);
            int supportTop = Math.Max(0, this.downloadCaptionLabel.Bounds.Top - this.ScaleValue(2));
            int supportBottom = Math.Min(this.ClientSize.Height, this.uploadValueLabel.Bounds.Bottom + this.ScaleValue(2));

            if (supportRight - supportLeft < this.ScaleValue(6) || supportBottom - supportTop < this.ScaleValue(10))
            {
                return;
            }

            RectangleF supportBounds = new RectangleF(
                AlignToHalfPixel(supportLeft),
                AlignToHalfPixel(supportTop),
                Math.Max(1F, supportRight - supportLeft),
                Math.Max(1F, supportBottom - supportTop));
            GraphicsState state = graphics.Save();

            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;

                Color supportColor = GetInterpolatedColor(this.GetPanelBackgroundBaseColor(), this.GetMeterTrackInnerBaseColor(), 0.14D);
                using (LinearGradientBrush supportBrush = new LinearGradientBrush(
                    new PointF(supportBounds.Left, supportBounds.Top),
                    new PointF(supportBounds.Right, supportBounds.Top),
                    Color.Transparent,
                    Color.Transparent))
                {
                    ColorBlend blend = new ColorBlend();
                    blend.Positions = new float[] { 0F, 0.30F, 0.74F, 1F };
                    blend.Colors = new Color[]
                    {
                        Color.Transparent,
                        ApplyAlpha(supportColor, 10),
                        ApplyAlpha(supportColor, 22),
                        ApplyAlpha(supportColor, 8)
                    };
                    supportBrush.InterpolationColors = blend;
                    graphics.FillRectangle(supportBrush, supportBounds);
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private int GetLeftSectionRightBoundary(Rectangle meterBounds, int minimumRight)
        {
            int candidateRight = this.IsRightSectionVisible()
                ? meterBounds.Left - this.ScaleValue(5)
                : this.ClientSize.Width - this.ScaleValue(4);
            return Math.Max(minimumRight, candidateRight);
        }
    }
}
