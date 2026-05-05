using System;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private bool isCapturingTrafficSnapshot;

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            if (this.isCapturingTrafficSnapshot)
            {
                return;
            }

            if (this.IsOverlayDragInProgress())
            {
                return;
            }

            this.RefreshTraffic();
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            if (!this.Visible || this.IsDisposed)
            {
                return;
            }

            if (this.IsOverlayDragInProgress())
            {
                return;
            }

            this.AdvanceVisualAnimations();
            if (this.ShouldDeferVisualSurfaceRefreshDuringManualDrag())
            {
                return;
            }

            this.RefreshVisualSurface();
        }

        private void TopMostGuardTimer_Tick(object sender, EventArgs e)
        {
            if (!this.Visible ||
                this.IsDisposed ||
                this.IsTopMostEnforcementPaused ||
                !this.ShouldUseGlobalTopMost())
            {
                return;
            }

            this.EnsureTopMostPlacement(false);
        }

        private bool IsTopMostEnforcementPaused
        {
            get { return this.topMostPauseDepth > 0; }
        }

        private static void ComputeRawTrafficRates(
            NetworkSnapshot snapshot,
            long lastReceivedBytes,
            long lastSentBytes,
            DateTime lastSampleUtc,
            DateTime nowUtc,
            out long measuredDownloadBytes,
            out long measuredUploadBytes,
            out double downloadBytesPerSecond,
            out double uploadBytesPerSecond)
        {
            measuredDownloadBytes = 0L;
            measuredUploadBytes = 0L;
            downloadBytesPerSecond = 0D;
            uploadBytesPerSecond = 0D;

            if (lastSampleUtc != DateTime.MinValue)
            {
                long receivedDiff = snapshot.BytesReceived - lastReceivedBytes;
                long sentDiff = snapshot.BytesSent - lastSentBytes;
                measuredDownloadBytes = Math.Max(0L, receivedDiff);
                measuredUploadBytes = Math.Max(0L, sentDiff);
                double elapsedSeconds = (nowUtc - lastSampleUtc).TotalSeconds;
                if (elapsedSeconds > 0.1D)
                {
                    downloadBytesPerSecond = measuredDownloadBytes / elapsedSeconds;
                    uploadBytesPerSecond = measuredUploadBytes / elapsedSeconds;
                }
            }
        }

        private void UpdateVisualizationPeaks(double rawDownloadBytesPerSecond, double rawUploadBytesPerSecond)
        {
            this.visualDownloadPeakBytesPerSecond = this.GetVisualizationPeak(
                rawDownloadBytesPerSecond,
                this.visualDownloadPeakBytesPerSecond,
                this.settings.GetDownloadVisualizationPeak(),
                true);
            this.visualUploadPeakBytesPerSecond = this.GetVisualizationPeak(
                rawUploadBytesPerSecond,
                this.visualUploadPeakBytesPerSecond,
                this.settings.GetUploadVisualizationPeak(),
                false);
        }

        private void ApplyTrafficRates(DateTime nowUtc, long receivedBytes, long sentBytes, double rawDownloadBytesPerSecond, double rawUploadBytesPerSecond)
        {
            this.lastSampleUtc = nowUtc;
            this.lastReceivedBytes = receivedBytes;
            this.lastSentBytes = sentBytes;
            this.latestDownloadBytesPerSecond = rawDownloadBytesPerSecond;
            this.latestUploadBytesPerSecond = rawUploadBytesPerSecond;
            this.UpdateRingDisplayRates(rawDownloadBytesPerSecond, rawUploadBytesPerSecond);
            this.UpdateVisualizationPeaks(rawDownloadBytesPerSecond, rawUploadBytesPerSecond);

            TrafficRateSmoothing.AddSample(this.recentDownloadSamples, rawDownloadBytesPerSecond, DisplaySmoothingSampleCount);
            TrafficRateSmoothing.AddSample(this.recentUploadSamples, rawUploadBytesPerSecond, DisplaySmoothingSampleCount);
            double smoothedDownloadBytesPerSecond = TrafficRateSmoothing.GetSmoothedRate(this.recentDownloadSamples, DisplaySmoothingWeights);
            double smoothedUploadBytesPerSecond = TrafficRateSmoothing.GetSmoothedRate(this.recentUploadSamples, DisplaySmoothingWeights);

            this.displayedDownloadBytesPerSecond = smoothedDownloadBytesPerSecond;
            this.displayedUploadBytesPerSecond = smoothedUploadBytesPerSecond;
            this.UpdatePeakHoldRates(smoothedDownloadBytesPerSecond, smoothedUploadBytesPerSecond, nowUtc);
            this.AddTrafficHistorySample(smoothedDownloadBytesPerSecond, smoothedUploadBytesPerSecond);
        }

        private void ProcessTrafficSnapshot(NetworkSnapshot snapshot)
        {
            if (!snapshot.HasAdapters)
            {
                this.lastSampleUtc = DateTime.MinValue;
                this.lastReceivedBytes = 0L;
                this.lastSentBytes = 0L;
                this.latestDownloadBytesPerSecond = 0D;
                this.latestUploadBytesPerSecond = 0D;
                this.displayedDownloadBytesPerSecond = 0D;
                this.displayedUploadBytesPerSecond = 0D;
                this.ringDisplayDownloadBytesPerSecond = 0D;
                this.ringDisplayUploadBytesPerSecond = 0D;
                this.peakHoldDownloadBytesPerSecond = 0D;
                this.peakHoldUploadBytesPerSecond = 0D;
                this.peakHoldDownloadCapturedUtc = DateTime.MinValue;
                this.peakHoldUploadCapturedUtc = DateTime.MinValue;
                this.lastAnimationAdvanceUtc = DateTime.MinValue;
                this.ResetDisplayedRateSmoothing();
                this.trafficHistory.Clear();
                this.trafficHistoryVersion++;
                this.downloadValueLabel.Text = "0 B/s";
                this.uploadValueLabel.Text = "0 B/s";
                this.UpdateAnimationTimerState();
                if (this.Visible && !this.ShouldDeferVisualSurfaceRefreshDuringManualDrag())
                {
                    this.RefreshVisualSurface();
                }

                this.OnRatesUpdated(0D, 0D);
                return;
            }

            DateTime nowUtc = DateTime.UtcNow;
            long measuredDownloadBytes;
            long measuredUploadBytes;
            double rawDownloadBytesPerSecond;
            double rawUploadBytesPerSecond;
            ComputeRawTrafficRates(
                snapshot,
                this.lastReceivedBytes,
                this.lastSentBytes,
                this.lastSampleUtc,
                nowUtc,
                out measuredDownloadBytes,
                out measuredUploadBytes,
                out rawDownloadBytesPerSecond,
                out rawUploadBytesPerSecond);

            this.ApplyTrafficRates(nowUtc, snapshot.BytesReceived, snapshot.BytesSent, rawDownloadBytesPerSecond, rawUploadBytesPerSecond);

            this.downloadValueLabel.Text = TrafficRateFormatter.FormatSpeed(this.displayedDownloadBytesPerSecond);
            this.uploadValueLabel.Text = TrafficRateFormatter.FormatSpeed(this.displayedUploadBytesPerSecond);
            this.UpdateAnimationTimerState();
            if (this.Visible && !this.ShouldDeferVisualSurfaceRefreshDuringManualDrag())
            {
                this.RefreshVisualSurface();
            }

            this.OnRatesUpdated(this.displayedDownloadBytesPerSecond, this.displayedUploadBytesPerSecond);
            this.OnTrafficUsageMeasured(measuredDownloadBytes, measuredUploadBytes);
        }

        private async void RefreshTraffic()
        {
            if (this.isCapturingTrafficSnapshot)
            {
                return;
            }

            this.isCapturingTrafficSnapshot = true;

            try
            {
                MonitorSettings settings = this.settings;
                NetworkSnapshot snapshot;

                try
                {
                    snapshot = await System.Threading.Tasks.Task.Run(() => NetworkSnapshot.Capture(settings));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine(string.Format("[TrafficView] RefreshTraffic-Capture fehlgeschlagen: {0}", ex.Message));
                    return;
                }

                if (!this.IsDisposed)
                {
                    this.ProcessTrafficSnapshot(snapshot);
                }
            }
            finally
            {
                this.isCapturingTrafficSnapshot = false;
            }
        }
    }
}
