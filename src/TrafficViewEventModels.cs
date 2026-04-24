using System;

namespace TrafficView
{
    internal sealed class RatesUpdatedEventArgs : EventArgs
    {
        public RatesUpdatedEventArgs(double downloadBytesPerSecond, double uploadBytesPerSecond)
        {
            this.DownloadBytesPerSecond = downloadBytesPerSecond;
            this.UploadBytesPerSecond = uploadBytesPerSecond;
        }

        public double DownloadBytesPerSecond { get; private set; }

        public double UploadBytesPerSecond { get; private set; }
    }

    internal sealed class TrafficUsageMeasuredEventArgs : EventArgs
    {
        public TrafficUsageMeasuredEventArgs(long downloadBytes, long uploadBytes)
        {
            this.DownloadBytes = Math.Max(0L, downloadBytes);
            this.UploadBytes = Math.Max(0L, uploadBytes);
        }

        public long DownloadBytes { get; private set; }

        public long UploadBytes { get; private set; }
    }

    internal sealed class TaskbarSectionModeChangeRequestedEventArgs : EventArgs
    {
        public TaskbarSectionModeChangeRequestedEventArgs(PopupSectionMode popupSectionMode)
        {
            this.PopupSectionMode = popupSectionMode;
        }

        public PopupSectionMode PopupSectionMode { get; private set; }
    }

    internal struct TrafficHistorySample
    {
        public readonly DateTime TimestampUtc;
        public readonly double DownloadBytesPerSecond;
        public readonly double UploadBytesPerSecond;

        public TrafficHistorySample(DateTime timestampUtc, double downloadBytesPerSecond, double uploadBytesPerSecond)
        {
            this.TimestampUtc = timestampUtc;
            this.DownloadBytesPerSecond = downloadBytesPerSecond;
            this.UploadBytesPerSecond = uploadBytesPerSecond;
        }
    }
}
