using System;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private bool ShouldDeferVisualSurfaceRefreshDuringManualDrag()
        {
            return this.IsOverlayDragInProgress() &&
                this.Visible &&
                !this.IsDisposed;
        }

        private void ResetDisplayedRateSmoothing()
        {
            this.recentDownloadSamples.Clear();
            this.recentUploadSamples.Clear();
        }

        private double GetRingDisplayNoiseFloorBytesPerSecond()
        {
            return MiniGraphRingDisplayNoiseFloorBytesPerSecond;
        }

        private double GetMinimumVisualizationPeakBytesPerSecond(bool useDownload)
        {
            return useDownload
                ? MiniGraphDownloadMinimumVisualizationPeakBytesPerSecond
                : MiniGraphUploadMinimumVisualizationPeakBytesPerSecond;
        }

        private void UpdateRingDisplayRates(double downloadBytesPerSecond, double uploadBytesPerSecond)
        {
            this.ringDisplayDownloadBytesPerSecond = UpdateRingDisplayRate(
                this.ringDisplayDownloadBytesPerSecond,
                downloadBytesPerSecond);
            this.ringDisplayUploadBytesPerSecond = UpdateRingDisplayRate(
                this.ringDisplayUploadBytesPerSecond,
                uploadBytesPerSecond);
        }

        private double UpdateRingDisplayRate(double currentBytesPerSecond, double targetBytesPerSecond)
        {
            double current = Math.Max(0D, currentBytesPerSecond);
            double target = Math.Max(0D, targetBytesPerSecond);
            double noiseFloorBytesPerSecond = this.GetRingDisplayNoiseFloorBytesPerSecond();

            if (current <= noiseFloorBytesPerSecond)
            {
                current = 0D;
            }

            if (target <= noiseFloorBytesPerSecond)
            {
                target = 0D;
            }

            double smoothingFactor = target >= current
                ? RingDisplayRiseSmoothingFactor
                : RingDisplayFallSmoothingFactor;
            double next = current + ((target - current) * smoothingFactor);

            if (Math.Abs(next - target) <= (noiseFloorBytesPerSecond * 0.25D))
            {
                return target;
            }

            return Math.Max(0D, next);
        }

        private void AddTrafficHistorySample(double downloadBytesPerSecond, double uploadBytesPerSecond)
        {
            while (this.trafficHistory.Count >= TrafficHistorySampleCount)
            {
                this.trafficHistory.Dequeue();
            }

            this.trafficHistory.Enqueue(new TrafficHistorySample(
                DateTime.UtcNow,
                Math.Max(0D, downloadBytesPerSecond),
                Math.Max(0D, uploadBytesPerSecond)));
            this.trafficHistoryVersion++;
        }

        private TrafficHistorySample[] GetTrafficHistorySnapshot()
        {
            return this.trafficHistory.ToArray();
        }

        private void OnRatesUpdated(double downloadBytesPerSecond, double uploadBytesPerSecond)
        {
            EventHandler<RatesUpdatedEventArgs> handler = this.RatesUpdated;
            if (handler != null)
            {
                handler(this, new RatesUpdatedEventArgs(downloadBytesPerSecond, uploadBytesPerSecond));
            }
        }

        private void OnTrafficUsageMeasured(long downloadBytes, long uploadBytes)
        {
            EventHandler<TrafficUsageMeasuredEventArgs> handler = this.TrafficUsageMeasured;
            if (handler != null && (downloadBytes > 0L || uploadBytes > 0L))
            {
                handler(this, new TrafficUsageMeasuredEventArgs(downloadBytes, uploadBytes));
            }
        }

        private double GetVisualizationPeak(
            double currentTrafficBytesPerSecond,
            double previousPeakBytesPerSecond,
            double configuredPeakBytesPerSecond,
            bool useDownload)
        {
            if (configuredPeakBytesPerSecond > 0D)
            {
                return configuredPeakBytesPerSecond;
            }

            double minimumPeak = this.GetMinimumVisualizationPeakBytesPerSecond(useDownload);
            double currentPeak = Math.Max(minimumPeak, currentTrafficBytesPerSecond * 1.15D);
            if (previousPeakBytesPerSecond <= 0D)
            {
                return currentPeak;
            }

            return Math.Max(currentPeak, previousPeakBytesPerSecond * 0.96D);
        }

        private double GetTrafficFillRatio(double bytesPerSecond, double configuredPeakBytesPerSecond, double visualPeakBytesPerSecond)
        {
            double peak = configuredPeakBytesPerSecond > 0D
                ? configuredPeakBytesPerSecond
                : visualPeakBytesPerSecond;

            if (peak <= 0D)
            {
                return 0D;
            }

            return Math.Max(0D, Math.Min(1D, bytesPerSecond / peak));
        }

        private double GetVisualizedFillRatio(double fillRatio, bool useDownload)
        {
            double clamped = Math.Max(0D, Math.Min(1D, fillRatio));

            if (clamped <= 0D || clamped >= 1D)
            {
                return clamped;
            }

            // A gentle gamma curve makes low traffic more visible without
            // overdriving medium and high values too aggressively.
            return Math.Pow(
                clamped,
                useDownload
                    ? MiniGraphDownloadLowTrafficVisualizationExponent
                    : MiniGraphUploadLowTrafficVisualizationExponent);
        }

        private double GetVisualizedFillRatioForCurrentDownload()
        {
            return this.GetVisualizedFillRatio(this.GetCurrentDownloadFillRatio(), true);
        }

        private double GetVisualizedFillRatioForCurrentUpload()
        {
            return this.GetVisualizedFillRatio(this.GetCurrentUploadFillRatio(), false);
        }

        private double GetCurrentDownloadFillRatio()
        {
            return this.GetTrafficFillRatio(
                this.ringDisplayDownloadBytesPerSecond,
                this.settings.GetDownloadVisualizationPeak(),
                this.visualDownloadPeakBytesPerSecond);
        }

        private double GetCurrentUploadFillRatio()
        {
            return this.GetTrafficFillRatio(
                this.ringDisplayUploadBytesPerSecond,
                this.settings.GetUploadVisualizationPeak(),
                this.visualUploadPeakBytesPerSecond);
        }
    }
}
