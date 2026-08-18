using System;

namespace TrafficView
{
    internal static class TrafficSnapshotBaselinePolicy
    {
        public static bool ShouldResetBaseline(
            long previousBytesReceived,
            long previousBytesSent,
            long previousTotalBytes,
            string previousAdapterKey,
            long currentBytesReceived,
            long currentBytesSent,
            long currentTotalBytes,
            string currentAdapterKey,
            out string reason)
        {
            reason = string.Empty;

            bool countersReset =
                currentBytesReceived < previousBytesReceived ||
                currentBytesSent < previousBytesSent ||
                currentTotalBytes < previousTotalBytes;

            if (countersReset)
            {
                reason = "counter-reset";
                return true;
            }

            if (!string.IsNullOrWhiteSpace(previousAdapterKey) &&
                !string.IsNullOrWhiteSpace(currentAdapterKey) &&
                !string.Equals(previousAdapterKey, currentAdapterKey, StringComparison.OrdinalIgnoreCase))
            {
                reason = "adapter-change";
                return true;
            }

            return false;
        }

        internal static TrafficSnapshotBaselinePolicySelfTestResult RunSelfTest()
        {
            string reason;

            bool counterReset = ShouldResetBaseline(
                1000L,
                1000L,
                2000L,
                "A",
                900L,
                1000L,
                1900L,
                "A",
                out reason);

            bool totalReset = ShouldResetBaseline(
                1000L,
                1000L,
                2000L,
                "A",
                1100L,
                1100L,
                1500L,
                "A",
                out reason);

            bool adapterChange = ShouldResetBaseline(
                1000L,
                1000L,
                2000L,
                "A",
                1100L,
                1100L,
                2200L,
                "B",
                out reason);

            bool normal = ShouldResetBaseline(
                1000L,
                1000L,
                2000L,
                "A",
                1100L,
                1200L,
                2300L,
                "A",
                out reason);

            return new TrafficSnapshotBaselinePolicySelfTestResult(
                counterReset,
                totalReset,
                adapterChange,
                !normal);
        }

        internal static string CreateSelfTestReport()
        {
            TrafficSnapshotBaselinePolicySelfTestResult result = RunSelfTest();

            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "TrafficSnapshotBaselinePolicy self-test: counter-reset={0}, total-reset={1}, adapter-change={2}, normal={3}",
                result.CounterResetPassed ? "pass" : "fail",
                result.TotalResetPassed ? "pass" : "fail",
                result.AdapterChangePassed ? "pass" : "fail",
                result.NormalPassed ? "pass" : "fail");
        }
    }

    internal sealed class TrafficSnapshotBaselinePolicySelfTestResult
    {
        public TrafficSnapshotBaselinePolicySelfTestResult(
            bool counterResetPassed,
            bool totalResetPassed,
            bool adapterChangePassed,
            bool normalPassed)
        {
            this.CounterResetPassed = counterResetPassed;
            this.TotalResetPassed = totalResetPassed;
            this.AdapterChangePassed = adapterChangePassed;
            this.NormalPassed = normalPassed;
        }

        public bool CounterResetPassed { get; private set; }
        public bool TotalResetPassed { get; private set; }
        public bool AdapterChangePassed { get; private set; }
        public bool NormalPassed { get; private set; }

        public bool AllPassed
        {
            get
            {
                return this.CounterResetPassed &&
                       this.TotalResetPassed &&
                       this.AdapterChangePassed &&
                       this.NormalPassed;
            }
        }
    }
}
