using System;
using System.Drawing;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private void DrawTrafficRing(
            Graphics graphics,
            RectangleF bounds,
            float strokeWidth,
            double fillRatio,
            Color trackColor,
            Color progressColor)
        {
            double clampedRatio = Math.Max(0D, Math.Min(1D, fillRatio));

            using (Pen trackPen = CreateRingPen(trackColor, strokeWidth, false))
            using (Pen glowPen = CreateRingPen(Color.FromArgb(96, progressColor), strokeWidth + this.ScaleFloat(1.5F), true))
            using (Pen progressPen = CreateRingPen(progressColor, strokeWidth, true))
            {
                graphics.DrawEllipse(trackPen, bounds);

                if (clampedRatio <= 0.001D)
                {
                    return;
                }

                float sweepAngle = this.GetDisplayedSweepAngle(clampedRatio);
                graphics.DrawArc(glowPen, bounds, -90F, sweepAngle);
                graphics.DrawArc(progressPen, bounds, -90F, sweepAngle);
            }
        }

        private void DrawInterleavedTrafficRing(
            Graphics graphics,
            RectangleF bounds,
            float strokeWidth,
            double downloadFillRatio,
            double uploadFillRatio,
            Color downloadTrackColor,
            Color uploadTrackColor,
            Color downloadStartColor,
            Color downloadEndColor,
            Color uploadStartColor,
            Color uploadEndColor,
            bool drawTracks)
        {
            if (this.IsMiniSoftLikeDisplayMode())
            {
                int segmentCount = RingSegmentCount;
                float slotSweep = 360F / segmentCount;
                float downloadSegmentSweep = Math.Max(
                    MinimumVisibleSegmentSweepDegrees,
                    slotSweep - Math.Min(slotSweep * 0.42F, MiniGraphDownloadRingSegmentGapDegrees));
                float uploadSegmentSweep = Math.Max(
                    MinimumVisibleSegmentSweepDegrees,
                    slotSweep - Math.Min(slotSweep * 0.42F, MiniGraphUploadRingSegmentGapDegrees));
                float gapBetweenRings = Math.Max(this.ScaleFloat(1.0F), strokeWidth * MiniSoftDualRingInnerGapFactor);
                float downloadWeight = this.GetChannelRingWeight(true);
                float uploadWeight = this.GetChannelRingWeight(false);
                float totalWeight = downloadWeight + uploadWeight;
                float usableBand = Math.Max(2.4F, strokeWidth - gapBetweenRings);
                float weightUnit = usableBand / Math.Max(0.1F, totalWeight);
                float downloadStrokeWidth = Math.Max(4.2F, weightUnit * downloadWeight * 2.52F) + this.ScaleFloat(2F);
                float uploadOffsetDownloadStrokeWidth = downloadStrokeWidth;
                float downloadOutwardExpansion = this.IsTaskbarIntegrationActive()
                    ? DpiHelper.Scale(TaskbarIntegratedDownloadRingOutwardExpansion, this.currentDpi)
                    : 0F;
                downloadStrokeWidth += downloadOutwardExpansion;
                float uploadStrokeWidth = Math.Max(2.4F, weightUnit * uploadWeight * 1.18F);

                RectangleF stableBounds = GetStableArcBounds(bounds);
                RectangleF downloadBounds = GetStableArcBounds(
                    InflateRectangle(stableBounds, -Math.Max(0F, (downloadStrokeWidth / 2F) - this.ScaleFloat(1F) - downloadOutwardExpansion)));
                RectangleF uploadBounds = GetStableArcBounds(
                    InflateRectangle(
                        stableBounds,
                        -((uploadOffsetDownloadStrokeWidth / 2F) + gapBetweenRings + (uploadStrokeWidth / 2F))));

                this.DrawSegmentedProgressSet(
                    graphics,
                    downloadBounds,
                    downloadStrokeWidth,
                    segmentCount,
                    slotSweep,
                    0F,
                    downloadSegmentSweep,
                    downloadFillRatio,
                    downloadTrackColor,
                    downloadStartColor,
                    downloadEndColor,
                    drawTracks);
                this.DrawSegmentedProgressSet(
                    graphics,
                    uploadBounds,
                    uploadStrokeWidth,
                    segmentCount,
                    slotSweep,
                    slotSweep * 0.5F,
                    uploadSegmentSweep,
                    uploadFillRatio,
                    uploadTrackColor,
                    uploadStartColor,
                    uploadEndColor,
                    drawTracks);

                if (!drawTracks)
                {
                    this.DrawPeakHoldRingMarker(
                        graphics,
                        downloadBounds,
                        downloadStrokeWidth,
                        this.GetPeakHoldFillRatio(true),
                        downloadEndColor,
                        true);
                    this.DrawPeakHoldRingMarker(
                        graphics,
                        uploadBounds,
                        uploadStrokeWidth,
                        this.GetPeakHoldFillRatio(false),
                        uploadEndColor,
                        false);
                }

                return;
            }

            if (!this.IsMiniGraphDisplayMode())
            {
                int segmentCount = this.GetCurrentRingSegmentCount();
                float slotSweep = 360F / segmentCount;
                float gapAngle = Math.Min(slotSweep * 0.48F, this.GetCurrentRingSegmentGapDegrees());
                float downloadSweep = Math.Max(
                    MinimumVisibleSegmentSweepDegrees,
                    slotSweep - gapAngle);
                float uploadSweep = Math.Max(
                    MinimumVisibleSegmentSweepDegrees,
                    gapAngle * 0.82F);
                float uploadOffset = downloadSweep + ((gapAngle - uploadSweep) / 2F);

                this.DrawSegmentedProgressSet(
                    graphics,
                    bounds,
                    strokeWidth,
                    segmentCount,
                    slotSweep,
                    0F,
                    downloadSweep,
                    downloadFillRatio,
                    downloadTrackColor,
                    downloadStartColor,
                    downloadEndColor,
                    drawTracks);
                this.DrawSegmentedProgressSet(
                    graphics,
                    bounds,
                    strokeWidth,
                    segmentCount,
                    slotSweep,
                    uploadOffset,
                    uploadSweep,
                    uploadFillRatio,
                    uploadTrackColor,
                    uploadStartColor,
                    uploadEndColor,
                    drawTracks);
                return;
            }

            int miniGraphSegmentCount = this.GetCurrentRingSegmentCount();
            float miniGraphSlotSweep = 360F / miniGraphSegmentCount;
            float miniGraphSegmentSweep = Math.Max(
                MinimumVisibleSegmentSweepDegrees,
                miniGraphSlotSweep - Math.Min(miniGraphSlotSweep * 0.42F, this.GetCurrentRingSegmentGapDegrees()));
            float classicGapBetweenRings = Math.Max(this.ScaleFloat(1.0F), strokeWidth * MiniGraphDualRingInnerGapFactor);
            float classicTotalWeight = MiniGraphDownloadRingWeight + MiniGraphUploadRingWeight;
            float classicUsableBand = Math.Max(2.4F, strokeWidth - classicGapBetweenRings);
            float classicWeightUnit = classicUsableBand / Math.Max(0.1F, classicTotalWeight);
            float classicDownloadStrokeWidth = Math.Max(3.6F, classicWeightUnit * MiniGraphDownloadRingWeight * 2.08F);
            float classicUploadStrokeWidth = Math.Max(1.9F, classicWeightUnit * MiniGraphUploadRingWeight * 0.92F);

            RectangleF classicStableBounds = GetStableArcBounds(bounds);
            RectangleF classicDownloadBounds = GetStableArcBounds(
                InflateRectangle(classicStableBounds, -(classicDownloadStrokeWidth / 2F)));
            RectangleF classicUploadBounds = GetStableArcBounds(
                InflateRectangle(
                    classicStableBounds,
                    -((classicDownloadStrokeWidth / 2F) + classicGapBetweenRings + (classicUploadStrokeWidth / 2F))));

            this.DrawSegmentedProgressSet(
                graphics,
                classicDownloadBounds,
                classicDownloadStrokeWidth,
                miniGraphSegmentCount,
                miniGraphSlotSweep,
                0F,
                miniGraphSegmentSweep,
                downloadFillRatio,
                downloadTrackColor,
                downloadStartColor,
                downloadEndColor,
                drawTracks);
            this.DrawSegmentedProgressSet(
                graphics,
                classicUploadBounds,
                classicUploadStrokeWidth,
                miniGraphSegmentCount,
                miniGraphSlotSweep,
                miniGraphSlotSweep * 0.5F,
                miniGraphSegmentSweep,
                uploadFillRatio,
                uploadTrackColor,
                uploadStartColor,
                uploadEndColor,
                drawTracks);

            if (!drawTracks)
            {
                this.DrawPeakHoldRingMarker(
                    graphics,
                    classicDownloadBounds,
                    classicDownloadStrokeWidth,
                    this.GetPeakHoldFillRatio(true),
                    downloadEndColor,
                    true);
                this.DrawPeakHoldRingMarker(
                    graphics,
                    classicUploadBounds,
                    classicUploadStrokeWidth,
                    this.GetPeakHoldFillRatio(false),
                    uploadEndColor,
                    false);
            }
        }

    }
}
