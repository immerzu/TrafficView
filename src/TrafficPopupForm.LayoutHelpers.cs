using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private int ScaleValue(int value)
        {
            return Math.Max(
                1,
                (int)Math.Round(
                    DpiHelper.Scale(value, this.currentDpi) * this.GetPopupScaleFactor(),
                    MidpointRounding.AwayFromZero));
        }

        private float ScaleFloat(float value)
        {
            return DpiHelper.Scale(value, this.currentDpi) * (float)this.GetPopupScaleFactor();
        }

        private double GetPopupScaleFactor()
        {
            double popupScaleFactor = Math.Max(0.5D, this.settings.PopupScalePercent / 100D);
            if (!this.IsTaskbarIntegrationActive())
            {
                return popupScaleFactor;
            }

            return popupScaleFactor * this.GetTaskbarIntegrationScaleFactor();
        }

        private double GetTaskbarIntegrationScaleFactor()
        {
            if (this.activeTaskbarSnapshot == null)
            {
                return 1D;
            }

            int taskbarThickness = this.activeTaskbarSnapshot.IsVertical
                ? this.activeTaskbarSnapshot.Bounds.Width
                : this.activeTaskbarSnapshot.Bounds.Height;
            int scaledTaskbarInsetThickness = Math.Max(1, DpiHelper.Scale(TaskbarInsetThickness, this.currentDpi));
            int usableTaskbarThickness = Math.Max(
                1,
                taskbarThickness - (2 * scaledTaskbarInsetThickness));
            int baseThickness = DpiHelper.Scale(
                this.activeTaskbarSnapshot.IsVertical ? BaseClientWidth : BaseClientHeight,
                this.currentDpi);
            if (baseThickness <= 0)
            {
                return 1D;
            }

            double scaleFactor = Math.Max(0.42D, Math.Min(1.85D, usableTaskbarThickness / (double)baseThickness));
            return scaleFactor;
        }

        private bool IsTaskbarIntegrationActive()
        {
            return this.settings != null &&
                this.settings.TaskbarIntegrationEnabled &&
                this.activeTaskbarSnapshot != null &&
                !this.activeTaskbarSnapshot.IsHidden;
        }

        private Point GetPopupScaleAdjustedLocation(Rectangle previousBounds)
        {
            if (previousBounds.Width <= 0 || previousBounds.Height <= 0)
            {
                return this.Location;
            }

            Point previousCenter = GetRectangleCenter(previousBounds);
            long adjustedX = (long)previousCenter.X - (this.Width / 2L);
            long adjustedY = (long)previousCenter.Y - (this.Height / 2L);
            return new Point(
                ClampToInt32(adjustedX),
                ClampToInt32(adjustedY));
        }

        private void UpdateWindowRegion()
        {
            if (this.Width <= 0 || this.Height <= 0)
            {
                return;
            }

            Region previousRegion = this.Region;
            using (GraphicsPath path = CreateRoundedPath(
                new Rectangle(0, 0, this.Width, this.Height),
                this.ScaleValue(BaseWindowCornerRadius)))
            {
                this.Region = new Region(path);
            }

            this.staticSurfaceDirty = true;
            this.lastPresentedLocation = new Point(int.MinValue, int.MinValue);
            this.lastPresentedSize = Size.Empty;

            if (previousRegion != null)
            {
                previousRegion.Dispose();
            }
        }

        private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = Math.Max(2, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
            Rectangle arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

            path.AddArc(arc, 180F, 90F);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270F, 90F);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0F, 90F);
            arc.X = bounds.Left;
            path.AddArc(arc, 90F, 90F);
            path.CloseFigure();
            return path;
        }

        private static GraphicsPath CreateRoundedPath(RectangleF bounds, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float diameter = Math.Max(2F, Math.Min(radius * 2F, Math.Min(bounds.Width, bounds.Height)));
            RectangleF arc = new RectangleF(bounds.Location, new SizeF(diameter, diameter));

            path.AddArc(arc, 180F, 90F);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270F, 90F);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0F, 90F);
            arc.X = bounds.Left;
            path.AddArc(arc, 90F, 90F);
            path.CloseFigure();
            return path;
        }

        private static void DisposeFont(Font font)
        {
            if (font != null)
            {
                font.Dispose();
            }
        }
    }
}
