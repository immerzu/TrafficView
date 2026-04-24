using System;
using System.Drawing;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private double GetPeakHoldFillRatio(bool useDownload)
        {
            double peakHoldBytesPerSecond = useDownload
                ? this.peakHoldDownloadBytesPerSecond
                : this.peakHoldUploadBytesPerSecond;
            double configuredPeakBytesPerSecond = useDownload
                ? this.settings.GetDownloadVisualizationPeak()
                : this.settings.GetUploadVisualizationPeak();
            double visualPeakBytesPerSecond = useDownload
                ? this.visualDownloadPeakBytesPerSecond
                : this.visualUploadPeakBytesPerSecond;
            return this.GetTrafficFillRatio(
                peakHoldBytesPerSecond,
                configuredPeakBytesPerSecond,
                visualPeakBytesPerSecond);
        }

        private void DrawPeakHoldRingMarker(
            Graphics graphics,
            RectangleF bounds,
            float strokeWidth,
            double fillRatio,
            Color color,
            bool useDownload)
        {
            double clampedRatio = Clamp01(fillRatio);
            if (clampedRatio <= 0.001D)
            {
                return;
            }

            float markerSweep = PeakHoldMarkerSweepDegrees;
            float centerAngle = -90F + (float)(360D * clampedRatio);
            float startAngle = centerAngle - (markerSweep / 2F);
            int alpha = this.GetChannelPeakMarkerAlpha(useDownload);

            this.DrawRingSegment(
                graphics,
                bounds,
                Math.Max(1.0F, strokeWidth * 0.32F),
                startAngle,
                markerSweep,
                Color.FromArgb(alpha, color),
                Math.Max(this.ScaleFloat(0.8F), strokeWidth * 0.20F));
        }

        private void DrawSegmentedProgressSet(
            Graphics graphics,
            RectangleF bounds,
            float strokeWidth,
            int segmentCount,
            float slotSweep,
            float segmentOffset,
            float segmentSweep,
            double fillRatio,
            Color trackColor,
            Color startColor,
            Color endColor,
            bool drawTracks)
        {
            double clampedRatio = Math.Max(0D, Math.Min(1D, fillRatio));
            double activeSegments = clampedRatio * segmentCount;
            int fullyLitSegments = (int)Math.Floor(activeSegments);
            double partialSegmentRatio = activeSegments - fullyLitSegments;
            Color activeEndColor = GetInterpolatedColor(
                startColor,
                endColor,
                SmoothStep(clampedRatio));
            bool renderFullScaleRing = this.IsMiniSoftLikeDisplayMode() && !drawTracks && clampedRatio >= 0.995D;

            if (renderFullScaleRing)
            {
                this.DrawFullScaleGradientRing(
                    graphics,
                    bounds,
                    strokeWidth,
                    -90F + segmentOffset,
                    startColor,
                    activeEndColor,
                    this.ScaleFloat(1.8F));
                return;
            }

            for (int index = 0; index < segmentCount; index++)
            {
                float segmentStartAngle = -90F + segmentOffset + (index * slotSweep);
                if (drawTracks)
                {
                    this.DrawRingSegment(
                        graphics,
                        bounds,
                        strokeWidth,
                        segmentStartAngle,
                        segmentSweep,
                        trackColor,
                        0F);
                }

                if (index < fullyLitSegments)
                {
                    double colorRatio = (index + 1D) / Math.Max(1D, activeSegments);
                    Color activeColor = GetInterpolatedColor(
                        startColor,
                        activeEndColor,
                        SmoothStep(Math.Max(0D, Math.Min(1D, colorRatio))));
                    this.DrawRingSegment(
                        graphics,
                        bounds,
                        strokeWidth,
                        segmentStartAngle,
                        segmentSweep,
                        activeColor,
                        this.ScaleFloat(1.1F));
                    continue;
                }

                if (index == fullyLitSegments && partialSegmentRatio > 0.02D)
                {
                    float partialSweep = Math.Max(
                        MinimumVisibleSegmentSweepDegrees,
                        segmentSweep * (float)partialSegmentRatio);
                    Color partialColor = GetInterpolatedColor(
                        startColor,
                        activeEndColor,
                        SmoothStep(clampedRatio));
                    this.DrawRingSegment(
                        graphics,
                        bounds,
                        strokeWidth,
                        segmentStartAngle,
                        partialSweep,
                        partialColor,
                        this.ScaleFloat(0.8F));
                }
            }
        }

    }
}
