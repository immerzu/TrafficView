using System;
using System.Threading;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private int isCapturingTrafficSnapshot;

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            if (this.isCapturingTrafficSnapshot != 0)
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
                this.lastTrafficSnapshotAdapterKey = string.Empty;
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

            if (this.lastSampleUtc != DateTime.MinValue)
            {
                string currentAdapterKey = CreateTrafficSnapshotAdapterKey(snapshot);
                string baselineResetReason;
                long previousTotalBytes = this.lastReceivedBytes + this.lastSentBytes;

                bool resetBaseline = TrafficSnapshotBaselinePolicy.ShouldResetBaseline(
                    this.lastReceivedBytes,
                    this.lastSentBytes,
                    previousTotalBytes,
                    this.lastTrafficSnapshotAdapterKey,
                    snapshot.BytesReceived,
                    snapshot.BytesSent,
                    snapshot.TotalBytes,
                    currentAdapterKey,
                    out baselineResetReason);

                if (resetBaseline)
                {
                    string reason = string.IsNullOrWhiteSpace(baselineResetReason)
                        ? "unknown"
                        : baselineResetReason;

                    AppLog.WarnOnce(
                        "traffic-snapshot-baseline-reset-" + reason + "-" + SanitizeAdapterKeyForLog(currentAdapterKey),
                        string.Format(
                            "Traffic snapshot baseline reset. Reason={0}. Adapter={1}",
                            reason,
                            snapshot.DisplayName ?? string.Empty));

                    this.lastReceivedBytes = snapshot.BytesReceived;
                    this.lastSentBytes = snapshot.BytesSent;
                    this.lastSampleUtc = nowUtc;
                    this.lastTrafficSnapshotAdapterKey = currentAdapterKey;
                    this.latestDownloadBytesPerSecond = 0D;
                    this.latestUploadBytesPerSecond = 0D;

                    if (this.Visible && !this.ShouldDeferVisualSurfaceRefreshDuringManualDrag())
                    {
                        this.RefreshVisualSurface();
                    }

                    return;
                }
            }

            this.lastTrafficSnapshotAdapterKey = CreateTrafficSnapshotAdapterKey(snapshot);

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

        private static string CreateTrafficSnapshotAdapterKey(NetworkSnapshot snapshot)
        {
            return snapshot.DisplayName ?? string.Empty;
        }

        internal string CreateMonitoringDiagnosticsText()
        {
            string adapterMode;
            if (this.settings == null)
            {
                adapterMode = "unknown";
            }
            else if (this.settings.UsesAutomaticAdapterSelection())
            {
                adapterMode = "automatic";
            }
            else
            {
                adapterMode = "manual";
            }

            string configuredAdapter = this.settings != null
                ? this.settings.GetAdapterDisplayName()
                : "unknown";

            string lastSnapshotAdapter = string.IsNullOrWhiteSpace(this.lastTrafficSnapshotAdapterKey)
                ? "none"
                : this.lastTrafficSnapshotAdapterKey;

            string lastSample = FormatDiagnosticsUtc(this.lastSampleUtc);

            string currentRates = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "DL {0} / UL {1}",
                TrafficRateFormatter.FormatSpeed(this.displayedDownloadBytesPerSecond),
                TrafficRateFormatter.FormatSpeed(this.displayedUploadBytesPerSecond));

            string captureActive = System.Threading.Volatile.Read(ref this.isCapturingTrafficSnapshot) != 0
                ? "yes"
                : "no";

            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "Monitoring diagnostics:\r\nAdapter selection mode: {0}\r\nConfigured adapter: {1}\r\nLast snapshot adapter: {2}\r\nLast successful sample UTC: {3}\r\nCurrent rates: {4}\r\nCapture active: {5}",
                adapterMode,
                configuredAdapter,
                lastSnapshotAdapter,
                lastSample,
                currentRates,
                captureActive);
        }

        private static string FormatDiagnosticsUtc(DateTime value)
        {
            if (value == DateTime.MinValue)
            {
                return "none";
            }

            DateTime utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
            return utc.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + " UTC";
        }

        private static string SanitizeAdapterKeyForLog(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return "unknown";
            }

            char[] chars = key.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
                {
                    chars[i] = '-';
                }
            }

            return new string(chars);
        }

        private async void RefreshTraffic()
        {
            if (Interlocked.CompareExchange(ref this.isCapturingTrafficSnapshot, 1, 0) != 0)
            {
                return;
            }

            try
            {
                await this.RefreshTrafficAsync();
            }
            catch (Exception ex)
            {
                AppLog.Warn("RefreshTraffic abgebrochen.", ex);
            }
        }

        private async System.Threading.Tasks.Task RefreshTrafficAsync()
        {
            CancellationToken cancellationToken = this.trafficSnapshotCancellation.Token;

            try
            {
                if (cancellationToken.IsCancellationRequested || this.IsDisposed || this.Disposing)
                {
                    return;
                }

                MonitorSettings settings = this.settings;
                NetworkSnapshot snapshot;

                try
                {
                    snapshot = await System.Threading.Tasks.Task.Run(
                        () => NetworkSnapshot.Capture(settings),
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine(string.Format("[TrafficView] RefreshTraffic-Capture fehlgeschlagen: {0}", ex.Message));
                    return;
                }

                if (cancellationToken.IsCancellationRequested || this.IsDisposed || this.Disposing)
                {
                    return;
                }

                if (this.IsDisposed || !this.IsHandleCreated)
                {
                    return;
                }

                try
                {
                    this.ProcessTrafficSnapshot(snapshot);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine(string.Format("[TrafficView] ProcessTrafficSnapshot fehlgeschlagen: {0}", ex.Message));
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                Interlocked.Exchange(ref this.isCapturingTrafficSnapshot, 0);
            }
        }
    }
}
