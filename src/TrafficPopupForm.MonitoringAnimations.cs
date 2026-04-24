using System;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private static double GetArrowMotionRatio(double fillRatio)
        {
            double clamped = Math.Max(0D, Math.Min(1D, fillRatio));

            if (clamped <= ArrowMotionDeadZoneRatio)
            {
                return 0D;
            }

            double normalized = (clamped - ArrowMotionDeadZoneRatio) /
                Math.Max(0.0001D, ArrowMotionFullRatio - ArrowMotionDeadZoneRatio);
            return SmoothStep(Math.Max(0D, Math.Min(1D, normalized)));
        }

        private bool ShouldAnimateCenterArrows()
        {
            return GetArrowMotionRatio(this.GetCurrentDownloadFillRatio()) > 0.001D ||
                GetArrowMotionRatio(this.GetCurrentUploadFillRatio()) > 0.001D;
        }

        private bool ShouldAnimateVisualEffects()
        {
            double ringNoiseFloorBytesPerSecond = this.GetRingDisplayNoiseFloorBytesPerSecond();
            double ringMotionThreshold = ringNoiseFloorBytesPerSecond * 0.25D;
            double holdMotionThreshold = ringNoiseFloorBytesPerSecond * 0.20D;

            return Math.Abs(this.ringDisplayDownloadBytesPerSecond - this.latestDownloadBytesPerSecond) > ringMotionThreshold ||
                Math.Abs(this.ringDisplayUploadBytesPerSecond - this.latestUploadBytesPerSecond) > ringMotionThreshold ||
                this.peakHoldDownloadBytesPerSecond > this.displayedDownloadBytesPerSecond + holdMotionThreshold ||
                this.peakHoldUploadBytesPerSecond > this.displayedUploadBytesPerSecond + holdMotionThreshold ||
                this.ShouldAnimateActivityBorder() ||
                this.ShouldAnimateMeterGloss();
        }

        private bool ShouldAnimateActivityBorder()
        {
            return this.GetActivityBorderIntensity() > ActivityBorderAnimationThresholdRatio;
        }

        private bool ShouldAnimateMeterGloss()
        {
            return this.settings.RotatingMeterGlossEnabled &&
                (this.GetCurrentDownloadFillRatio() > MeterGlossAnimationThresholdRatio ||
                 this.GetCurrentUploadFillRatio() > MeterGlossAnimationThresholdRatio);
        }

        private void UpdatePeakHoldRates(double downloadBytesPerSecond, double uploadBytesPerSecond, DateTime nowUtc)
        {
            double safeDownloadBytesPerSecond = Math.Max(0D, downloadBytesPerSecond);
            double safeUploadBytesPerSecond = Math.Max(0D, uploadBytesPerSecond);

            if (safeDownloadBytesPerSecond >= this.peakHoldDownloadBytesPerSecond)
            {
                this.peakHoldDownloadBytesPerSecond = safeDownloadBytesPerSecond;
                this.peakHoldDownloadCapturedUtc = nowUtc;
            }

            if (safeUploadBytesPerSecond >= this.peakHoldUploadBytesPerSecond)
            {
                this.peakHoldUploadBytesPerSecond = safeUploadBytesPerSecond;
                this.peakHoldUploadCapturedUtc = nowUtc;
            }
        }

        private void AdvanceVisualAnimations()
        {
            DateTime nowUtc = DateTime.UtcNow;
            double elapsedSeconds = this.lastAnimationAdvanceUtc == DateTime.MinValue
                ? Math.Max(0.001D, this.animationTimer.Interval / 1000D)
                : Math.Max(0.001D, (nowUtc - this.lastAnimationAdvanceUtc).TotalSeconds);
            this.lastAnimationAdvanceUtc = nowUtc;

            this.UpdateRingDisplayRates(this.latestDownloadBytesPerSecond, this.latestUploadBytesPerSecond);
            this.DecayPeakHoldRates(nowUtc, elapsedSeconds);
            this.AdvanceMeterGlossRotation(elapsedSeconds);
            this.AdvanceActivityBorderRotation(elapsedSeconds);
            this.UpdateAnimationTimerState();
        }

        private void AdvanceActivityBorderRotation(double elapsedSeconds)
        {
            double intensity = this.GetActivityBorderIntensity();
            double fadeRatio = GetActivityBorderFadeRatio(intensity);
            if (fadeRatio <= 0D)
            {
                return;
            }

            double direction = this.GetActivityBorderDirection();
            double degreesPerSecond = (ActivityBorderBaseRotationDegreesPerSecond +
                (SmoothStep(intensity) * (ActivityBorderMaxRotationDegreesPerSecond - ActivityBorderBaseRotationDegreesPerSecond))) *
                fadeRatio;
            this.activityBorderRotationDegrees += direction * degreesPerSecond * elapsedSeconds;
            while (this.activityBorderRotationDegrees >= 360D)
            {
                this.activityBorderRotationDegrees -= 360D;
            }

            while (this.activityBorderRotationDegrees < 0D)
            {
                this.activityBorderRotationDegrees += 360D;
            }
        }

        private void AdvanceMeterGlossRotation(double elapsedSeconds)
        {
            if (!this.settings.RotatingMeterGlossEnabled)
            {
                return;
            }

            double downloadInfluence = this.GetVisualizedFillRatioForCurrentDownload();
            double uploadInfluence = this.GetVisualizedFillRatioForCurrentUpload();
            double netInfluence = downloadInfluence - uploadInfluence;
            if (Math.Abs(netInfluence) <= 0.0001D)
            {
                return;
            }

            double degreesPerSecond = netInfluence >= 0D
                ? MeterGlossClockwiseMaxRotationDegreesPerSecond
                : MeterGlossCounterClockwiseMaxRotationDegreesPerSecond;
            this.meterGlossRotationDegrees += netInfluence * degreesPerSecond * elapsedSeconds;
            while (this.meterGlossRotationDegrees >= 360D)
            {
                this.meterGlossRotationDegrees -= 360D;
            }

            while (this.meterGlossRotationDegrees < 0D)
            {
                this.meterGlossRotationDegrees += 360D;
            }
        }

        private void DecayPeakHoldRates(DateTime nowUtc, double elapsedSeconds)
        {
            this.peakHoldDownloadBytesPerSecond = this.DecayPeakHoldRate(
                this.peakHoldDownloadBytesPerSecond,
                this.displayedDownloadBytesPerSecond,
                this.peakHoldDownloadCapturedUtc,
                nowUtc,
                elapsedSeconds);
            this.peakHoldUploadBytesPerSecond = this.DecayPeakHoldRate(
                this.peakHoldUploadBytesPerSecond,
                this.displayedUploadBytesPerSecond,
                this.peakHoldUploadCapturedUtc,
                nowUtc,
                elapsedSeconds);
        }

        private double DecayPeakHoldRate(
            double currentPeakHoldBytesPerSecond,
            double baselineBytesPerSecond,
            DateTime capturedUtc,
            DateTime nowUtc,
            double elapsedSeconds)
        {
            double safePeakHoldBytesPerSecond = Math.Max(0D, currentPeakHoldBytesPerSecond);
            double safeBaselineBytesPerSecond = Math.Max(0D, baselineBytesPerSecond);
            if (safePeakHoldBytesPerSecond <= safeBaselineBytesPerSecond)
            {
                return safeBaselineBytesPerSecond;
            }

            if (capturedUtc != DateTime.MinValue &&
                (nowUtc - capturedUtc).TotalSeconds <= PeakHoldReleaseDelaySeconds)
            {
                return safePeakHoldBytesPerSecond;
            }

            double decayAmount = Math.Max(
                this.GetRingDisplayNoiseFloorBytesPerSecond(),
                safePeakHoldBytesPerSecond * PeakHoldDecayPerSecond * elapsedSeconds);
            return Math.Max(safeBaselineBytesPerSecond, safePeakHoldBytesPerSecond - decayAmount);
        }

        private void UpdateAnimationTimerState()
        {
            if (this.animationTimer == null)
            {
                return;
            }

            bool shouldAnimate = this.Visible &&
                (this.ShouldAnimateCenterArrows() || this.ShouldAnimateVisualEffects());
            if (this.animationTimer.Enabled == shouldAnimate)
            {
                return;
            }

            this.animationTimer.Enabled = shouldAnimate;
            this.lastRenderedAnimationFrame = -1;
        }

        private int GetAnimationFrameIndex()
        {
            if (!this.ShouldAnimateCenterArrows() && !this.ShouldAnimateVisualEffects())
            {
                return 0;
            }

            int interval = Math.Max(1, this.animationTimer != null ? this.animationTimer.Interval : 180);
            double milliseconds = DateTime.UtcNow.TimeOfDay.TotalMilliseconds;
            return (int)Math.Floor(milliseconds / interval);
        }

        private static bool AreNearlyEqual(double left, double right)
        {
            if (double.IsNaN(left) && double.IsNaN(right))
            {
                return true;
            }

            return Math.Abs(left - right) < 0.0001D;
        }
    }
}
