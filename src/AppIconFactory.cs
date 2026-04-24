using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace TrafficView
{
    internal static class AppIconFactory
    {
        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);

        public static Icon CreateAppIcon()
        {
            Bitmap bitmap = new Bitmap(64, 64);

            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (SolidBrush panelBrush = new SolidBrush(Color.FromArgb(18, 34, 82)))
            using (Pen panelPen = new Pen(Color.FromArgb(240, 246, 252), 3F))
            using (SolidBrush orangeBrush = new SolidBrush(Color.FromArgb(255, 155, 0)))
            using (SolidBrush greenBrush = new SolidBrush(Color.FromArgb(64, 255, 96)))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);

                Rectangle panelBounds = new Rectangle(6, 6, 52, 52);
                using (GraphicsPath path = CreateRoundedPath(panelBounds, 14))
                {
                    graphics.FillPath(panelBrush, path);
                    graphics.DrawPath(panelPen, path);
                }

                FillArrow(graphics, new Rectangle(14, 16, 14, 28), orangeBrush, false);
                FillArrow(graphics, new Rectangle(36, 16, 14, 28), greenBrush, true);
            }

            IntPtr handle = bitmap.GetHicon();

            try
            {
                using (Icon temporary = Icon.FromHandle(handle))
                {
                    return (Icon)temporary.Clone();
                }
            }
            finally
            {
                DestroyIcon(handle);
                bitmap.Dispose();
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

        private static void FillArrow(Graphics graphics, Rectangle bounds, Brush brush, bool upward)
        {
            Point[] points;

            if (upward)
            {
                points = new Point[]
                {
                    new Point(bounds.Left + (bounds.Width / 2), bounds.Top),
                    new Point(bounds.Right, bounds.Top + 10),
                    new Point(bounds.Left + 9, bounds.Top + 10),
                    new Point(bounds.Left + 9, bounds.Bottom),
                    new Point(bounds.Left + 5, bounds.Bottom),
                    new Point(bounds.Left + 5, bounds.Top + 10),
                    new Point(bounds.Left, bounds.Top + 10)
                };
            }
            else
            {
                points = new Point[]
                {
                    new Point(bounds.Left + 5, bounds.Top),
                    new Point(bounds.Left + 9, bounds.Top),
                    new Point(bounds.Left + 9, bounds.Bottom - 10),
                    new Point(bounds.Right, bounds.Bottom - 10),
                    new Point(bounds.Left + (bounds.Width / 2), bounds.Bottom),
                    new Point(bounds.Left, bounds.Bottom - 10),
                    new Point(bounds.Left + 5, bounds.Bottom - 10)
                };
            }

            graphics.FillPolygon(brush, points);
        }
    }
}
