using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private void DrawCenterTrafficArrows(
            Graphics graphics,
            RectangleF centerBounds,
            RectangleF iconBounds,
            double downloadFillRatio,
            double uploadFillRatio,
            double visualDownloadFillRatio,
            double visualUploadFillRatio)
        {
            double animationSeconds = DateTime.UtcNow.TimeOfDay.TotalSeconds;
            float arrowWidth = Math.Max(2.2F, (iconBounds.Width * 0.24F) - this.ScaleFloat(1.2F));
            float arrowHeight = Math.Max(5.8F, (iconBounds.Height * 0.58F) - this.ScaleFloat(1F));
            float shaftWidth = Math.Max(1.2F, arrowWidth * 0.34F);
            float glowBaseWidth = Math.Max(1.6F, this.ScaleFloat(1.8F));
            float bobAmplitude = Math.Max(0.4F, this.ScaleFloat(0.55F));
            float horizontalOffset = iconBounds.Width * 0.19F;
            float centerX = centerBounds.Left + (centerBounds.Width / 2F);
            float centerY = centerBounds.Top + (centerBounds.Height / 2F);
            float rawDownloadPulse = 0.5F + (0.5F * (float)Math.Sin(animationSeconds * 3.6D));
            float rawUploadPulse = 0.5F + (0.5F * (float)Math.Sin((animationSeconds * 3.6D) + Math.PI));
            float downloadMotionRatio = (float)GetArrowMotionRatio(downloadFillRatio);
            float uploadMotionRatio = (float)GetArrowMotionRatio(uploadFillRatio);
            float downloadPulse = 0.5F + ((rawDownloadPulse - 0.5F) * downloadMotionRatio);
            float uploadPulse = 0.5F + ((rawUploadPulse - 0.5F) * uploadMotionRatio);
            float downloadYOffset = ((downloadPulse - 0.5F) * bobAmplitude * 1.7F) + this.ScaleFloat(0.45F);
            float uploadYOffset = -((uploadPulse - 0.5F) * bobAmplitude * 1.7F) - this.ScaleFloat(0.45F);
            Color downloadColor = GetInterpolatedColor(
                this.GetDownloadArrowBaseColor(),
                this.GetDownloadArrowHighBaseColor(),
                SmoothStep(visualDownloadFillRatio));
            Color uploadColor = GetInterpolatedColor(
                this.GetUploadArrowBaseColor(),
                this.GetUploadArrowHighBaseColor(),
                SmoothStep(visualUploadFillRatio));

            using (GraphicsPath clipPath = new GraphicsPath())
            {
                clipPath.AddEllipse(centerBounds);
                GraphicsState state = graphics.Save();
                graphics.SetClip(clipPath, CombineMode.Intersect);

                this.DrawAnimatedArrow(
                    graphics,
                    new PointF(centerX - horizontalOffset, centerY + downloadYOffset),
                    arrowWidth,
                    arrowHeight,
                    shaftWidth,
                    false,
                    downloadColor,
                    downloadPulse,
                    glowBaseWidth,
                    visualDownloadFillRatio);
                this.DrawAnimatedArrow(
                    graphics,
                    new PointF(centerX + horizontalOffset, centerY + uploadYOffset),
                    arrowWidth,
                    arrowHeight,
                    shaftWidth,
                    true,
                    uploadColor,
                    uploadPulse,
                    glowBaseWidth,
                    visualUploadFillRatio);

                graphics.Restore(state);
            }
        }

        private void DrawAnimatedArrow(
            Graphics graphics,
            PointF center,
            float width,
            float height,
            float shaftWidth,
            bool pointsUp,
            Color bodyColor,
            float pulse,
            float glowBaseWidth,
            double intensity)
        {
            float scale = 0.92F + ((float)intensity * 0.10F) + (pulse * 0.04F);
            PointF stableCenter = GetStableArrowCenter(center);
            float scaledWidth = NormalizeArrowDimension(width * scale);
            float scaledHeight = NormalizeArrowDimension(height * scale);
            float scaledShaftWidth = NormalizeArrowDimension(shaftWidth * Math.Max(0.92F, scale));
            int transparencyPercent = this.settings != null ? this.settings.TransparencyPercent : 0;
            bool ultraTransparent = transparencyPercent >= 100;
            float glowWidth = glowBaseWidth
                + (pulse * this.ScaleFloat(0.85F))
                + ((float)intensity * this.ScaleFloat(0.75F))
                + (ultraTransparent ? this.ScaleFloat(0.95F) : 0F);
            int glowAlpha = 96 + (int)Math.Round((pulse * 36F) + (float)intensity * 54F, MidpointRounding.AwayFromZero);
            if (ultraTransparent)
            {
                glowAlpha = Math.Min(232, glowAlpha + 62);
            }
            Color glowColor = Color.FromArgb(
                Math.Max(0, Math.Min(220, glowAlpha)),
                bodyColor.R,
                bodyColor.G,
                bodyColor.B);
            Color outlineColor = GetInterpolatedColor(
                bodyColor,
                Color.FromArgb(255, 250, 248, 236),
                (0.22D + (0.10D * pulse)) + (ultraTransparent ? 0.12D : 0D));
            GraphicsState state = graphics.Save();

            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;

            using (GraphicsPath arrowPath = CreateArrowPath(stableCenter, scaledWidth, scaledHeight, scaledShaftWidth, pointsUp))
            using (Pen glowPen = new Pen(glowColor, glowWidth))
            using (SolidBrush arrowBrush = new SolidBrush(bodyColor))
            using (Pen outlinePen = new Pen(
                outlineColor,
                Math.Max(0.8F, this.ScaleFloat(0.8F)) + (ultraTransparent ? this.ScaleFloat(0.25F) : 0F)))
            {
                glowPen.LineJoin = LineJoin.Round;
                glowPen.Alignment = PenAlignment.Center;
                outlinePen.LineJoin = LineJoin.Round;
                outlinePen.Alignment = PenAlignment.Center;
                graphics.DrawPath(glowPen, arrowPath);
                graphics.FillPath(arrowBrush, arrowPath);
                graphics.DrawPath(outlinePen, arrowPath);
            }

            graphics.Restore(state);
        }

        private static GraphicsPath CreateArrowPath(
            PointF center,
            float width,
            float height,
            float shaftWidth,
            bool pointsUp)
        {
            float halfWidth = width / 2F;
            float halfHeight = height / 2F;
            float halfShaftWidth = shaftWidth / 2F;
            float headHeight = height * 0.42F;
            float top = center.Y - halfHeight;
            float bottom = center.Y + halfHeight;
            float headBaseY = pointsUp ? (top + headHeight) : (bottom - headHeight);
            PointF[] points;

            if (pointsUp)
            {
                points = new PointF[]
                {
                    CreateAlignedPoint(center.X, top),
                    CreateAlignedPoint(center.X + halfWidth, headBaseY),
                    CreateAlignedPoint(center.X + halfShaftWidth, headBaseY),
                    CreateAlignedPoint(center.X + halfShaftWidth, bottom),
                    CreateAlignedPoint(center.X - halfShaftWidth, bottom),
                    CreateAlignedPoint(center.X - halfShaftWidth, headBaseY),
                    CreateAlignedPoint(center.X - halfWidth, headBaseY)
                };
            }
            else
            {
                points = new PointF[]
                {
                    CreateAlignedPoint(center.X + halfShaftWidth, top),
                    CreateAlignedPoint(center.X + halfShaftWidth, headBaseY),
                    CreateAlignedPoint(center.X + halfWidth, headBaseY),
                    CreateAlignedPoint(center.X, bottom),
                    CreateAlignedPoint(center.X - halfWidth, headBaseY),
                    CreateAlignedPoint(center.X - halfShaftWidth, headBaseY),
                    CreateAlignedPoint(center.X - halfShaftWidth, top)
                };
            }

            GraphicsPath path = new GraphicsPath();
            path.AddPolygon(points);
            path.CloseFigure();
            return path;
        }
    }
}
