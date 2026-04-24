using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private TrafficHistorySample[] GetOverlaySparklineSamples()
        {
            if (this.cachedOverlaySparklineHistoryVersion == this.trafficHistoryVersion &&
                this.cachedOverlaySparklineSamples != null)
            {
                return this.cachedOverlaySparklineSamples;
            }

            TrafficHistorySample[] history = this.GetTrafficHistorySnapshot();
            TrafficHistorySample[] snapshot;
            if (history.Length <= 0)
            {
                snapshot = Array.Empty<TrafficHistorySample>();
            }
            else if (history.Length <= this.GetCurrentOverlaySparklinePointCount())
            {
                snapshot = history;
            }
            else
            {
                int overlaySparklinePointCount = this.GetCurrentOverlaySparklinePointCount();
                snapshot = new TrafficHistorySample[overlaySparklinePointCount];
                Array.Copy(
                    history,
                    history.Length - overlaySparklinePointCount,
                    snapshot,
                    0,
                    overlaySparklinePointCount);
            }

            this.cachedOverlaySparklineHistoryVersion = this.trafficHistoryVersion;
            this.cachedOverlaySparklineSamples = snapshot;
            return snapshot;
        }

        private static PointF[] CreateSparklinePoints(
            TrafficHistorySample[] samples,
            Rectangle bounds,
            double peakBytesPerSecond,
            bool useDownload,
            bool useMiniGraphLayout)
        {
            if (samples == null || samples.Length == 0)
            {
                return Array.Empty<PointF>();
            }

            PointF[] points = new PointF[samples.Length];
            float leftInset = useMiniGraphLayout
                ? Math.Min(Math.Max(0F, MiniGraphSparklineContentLeftInset), Math.Max(0F, bounds.Width - 2F))
                : 0F;
            float width = Math.Max(1F, bounds.Width - 1F - leftInset);
            float height = Math.Max(1F, bounds.Height - 1F);
            double peak = Math.Max(1D, peakBytesPerSecond);

            for (int i = 0; i < samples.Length; i++)
            {
                double value = useDownload
                    ? samples[i].DownloadBytesPerSecond
                    : samples[i].UploadBytesPerSecond;
                double ratio = Math.Max(0D, Math.Min(1D, value / peak));
                float x = bounds.Left + leftInset + ((samples.Length == 1)
                    ? 0F
                    : (width * i) / Math.Max(1, samples.Length - 1));
                float y = bounds.Bottom - 1 - (float)(ratio * height);
                points[i] = new PointF(
                    AlignToHalfPixel(x),
                    AlignToHalfPixel(y));
            }

            return RemoveConsecutiveDuplicatePoints(points);
        }

        private static PointF[] RemoveConsecutiveDuplicatePoints(PointF[] points)
        {
            if (points == null || points.Length <= 1)
            {
                return points ?? Array.Empty<PointF>();
            }

            List<PointF> filteredPoints = new List<PointF>(points.Length);
            PointF previousPoint = points[0];
            filteredPoints.Add(previousPoint);

            for (int index = 1; index < points.Length; index++)
            {
                PointF currentPoint = points[index];
                if (currentPoint != previousPoint)
                {
                    filteredPoints.Add(currentPoint);
                    previousPoint = currentPoint;
                }
            }

            return filteredPoints.Count == points.Length
                ? points
                : filteredPoints.ToArray();
        }

        private static GraphicsPath CreateSparklineFillPath(PointF[] points, Rectangle bounds, bool smoothLeadingEdge = false)
        {
            if (points == null || points.Length < 2)
            {
                return null;
            }

            GraphicsPath path = new GraphicsPath();
            List<PointF> polygonPoints = new List<PointF>(points.Length + 2);
            float baselineY = AlignToHalfPixel(bounds.Bottom - 1F);
            if (smoothLeadingEdge)
            {
                float firstX = points[0].X;
                float secondX = points.Length >= 2 ? points[1].X : firstX;
                float shoulderOffset = Math.Max(1F, Math.Min(3F, (secondX - firstX) * 0.5F));
                float startBaseX = AlignToHalfPixel(Math.Max(bounds.Left, firstX - shoulderOffset));
                polygonPoints.Add(new PointF(startBaseX, baselineY));
            }

            polygonPoints.AddRange(points);
            polygonPoints.Add(new PointF(points[points.Length - 1].X, baselineY));
            polygonPoints.Add(new PointF(smoothLeadingEdge ? polygonPoints[0].X : points[0].X, baselineY));
            path.AddPolygon(polygonPoints.ToArray());
            path.CloseFigure();
            return path;
        }

        private void DrawSparklinePeakMarker(
            Graphics graphics,
            Rectangle bounds,
            double peakBytesPerSecond,
            double peakHoldBytesPerSecond,
            Color color,
            bool useDownload)
        {
            if (peakBytesPerSecond <= 0D || peakHoldBytesPerSecond <= 0D)
            {
                return;
            }

            float y = GetSparklineValueY(bounds, peakHoldBytesPerSecond, peakBytesPerSecond);
            float right = bounds.Right - this.ScaleFloat(1.5F);
            float width = Math.Max(this.ScaleFloat(useDownload ? 9F : 7F), useDownload ? 9F : 7F);
            float left = right - width;
            float lineWidth = Math.Max(this.ScaleFloat(useDownload ? 1.6F : 1.2F), useDownload ? 1.6F : 1.2F);
            int alpha = this.GetChannelPeakMarkerAlpha(useDownload);

            using (Pen markerPen = new Pen(Color.FromArgb(alpha, color), lineWidth))
            using (Pen glowPen = new Pen(
                Color.FromArgb(Math.Max(72, alpha / 2), color),
                lineWidth + this.ScaleFloat(0.8F)))
            {
                markerPen.StartCap = LineCap.Round;
                markerPen.EndCap = LineCap.Round;
                glowPen.StartCap = LineCap.Round;
                glowPen.EndCap = LineCap.Round;
                graphics.DrawLine(glowPen, AlignToHalfPixel(left), AlignToHalfPixel(y), AlignToHalfPixel(right), AlignToHalfPixel(y));
                graphics.DrawLine(markerPen, AlignToHalfPixel(left), AlignToHalfPixel(y), AlignToHalfPixel(right), AlignToHalfPixel(y));
            }
        }

        private static float GetSparklineValueY(Rectangle bounds, double bytesPerSecond, double peakBytesPerSecond)
        {
            double peak = Math.Max(1D, peakBytesPerSecond);
            double ratio = Math.Max(0D, Math.Min(1D, bytesPerSecond / peak));
            float height = Math.Max(1F, bounds.Height - 1F);
            float y = bounds.Bottom - 1 - (float)(ratio * height);
            return AlignToHalfPixel(y);
        }
    }
}
