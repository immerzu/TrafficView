using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private readonly Timer refreshTimer;
        private readonly Timer animationTimer;
        private readonly Timer topMostGuardTimer;
        private readonly Timer taskbarMonitorTimer;
        private readonly Timer taskbarRefreshDebounceTimer;
        private readonly Timer manualDragMoveTimer;
        private readonly Label downloadCaptionLabel;
        private readonly Label uploadCaptionLabel;
        private readonly Label downloadValueLabel;
        private readonly Label uploadValueLabel;
        private MonitorSettings settings;
        private long lastReceivedBytes;
        private long lastSentBytes;
        private DateTime lastSampleUtc;
        private int currentDpi;
        private Font captionFont;
        private Font valueFont;
        private Font formFont;
        private double latestDownloadBytesPerSecond;
        private double latestUploadBytesPerSecond;
        private double displayedDownloadBytesPerSecond;
        private double displayedUploadBytesPerSecond;
        private double ringDisplayDownloadBytesPerSecond;
        private double ringDisplayUploadBytesPerSecond;
        private double peakHoldDownloadBytesPerSecond;
        private double peakHoldUploadBytesPerSecond;
        private double visualDownloadPeakBytesPerSecond;
        private double visualUploadPeakBytesPerSecond;
        private DateTime peakHoldDownloadCapturedUtc = DateTime.MinValue;
        private DateTime peakHoldUploadCapturedUtc = DateTime.MinValue;
        private DateTime lastAnimationAdvanceUtc = DateTime.MinValue;
        private double meterGlossRotationDegrees;
        private double activityBorderRotationDegrees;
        private double simpleActivityBorderDirection = 1D;
        private readonly Queue<double> recentDownloadSamples;
        private readonly Queue<double> recentUploadSamples;
        private readonly Queue<TrafficHistorySample> trafficHistory;
        private Control dragControl;
        private Point dragStartCursor;
        private Point dragStartLocation;
        private bool leftMousePressed;
        private bool dragMoved;
        private bool manualDragMoveApplied;
        private Point? pendingManualDragLocation;
        private Bitmap staticSurfaceBitmap;
        private Bitmap composedSurfaceBitmap;
        private bool staticSurfaceDirty = true;
        private int lastRenderedAnimationFrame = -1;
        private string lastRenderedDownloadText = string.Empty;
        private string lastRenderedUploadText = string.Empty;
        private double lastRenderedDownloadFillRatio = double.NaN;
        private double lastRenderedUploadFillRatio = double.NaN;
        private int lastRenderedTrafficHistoryVersion = -1;
        private Point lastPresentedLocation = new Point(int.MinValue, int.MinValue);
        private Size lastPresentedSize = Size.Empty;
        private int topMostPauseDepth;
        private int trafficHistoryVersion;
        private int cachedOverlaySparklineHistoryVersion = -1;
        private TrafficHistorySample[] cachedOverlaySparklineSamples = Array.Empty<TrafficHistorySample>();
        private DateTime suppressMenuUntilUtc = DateTime.MinValue;
        private bool taskbarIntegrationDisplayRequested;
        private bool taskbarIntegrationVisibilityChange;
        private bool taskbarNoSpaceMessageShown;
        private bool taskbarNoSpaceMessageVisible;
        private bool taskbarIntegrationRefreshInProgress;
        private bool taskbarIntegrationRefreshPending;
        private bool taskbarIntegrationDebouncedRefreshPending;
        private bool taskbarIntegrationPendingActivateWindow;
        private bool taskbarIntegrationPendingShowNoSpaceMessage;
        private bool taskbarLocalZOrderRepairPending = true;
        private bool taskbarIntegrationForceRightOnlySection;
        private bool taskbarIntegrationStickyRightOnlySection;
        private Point? taskbarIntegrationPreferredLocation;
        private DateTime lastNoSpaceMessageUtc = DateTime.MinValue;
        private DateTime lastDesktopShellForegroundUtc = DateTime.MinValue;
        private DateTime lastTaskbarIntegrationRefreshUtc = DateTime.MinValue;
        private DateTime lastSuccessfulTaskbarPlacementUtc = DateTime.MinValue;
        private Rectangle lastSuccessfulTaskbarPlacementBounds = Rectangle.Empty;
        private int lastAppliedTaskbarThickness = -1;
        private TaskbarIntegrationSnapshot activeTaskbarSnapshot;
        private TaskbarIntegrationSnapshot dragTaskbarSnapshot;
        private Rectangle dragTaskbarSnapshotScreenBounds = Rectangle.Empty;
        private DateTime dragTaskbarSnapshotCapturedUtc = DateTime.MinValue;
        private IntPtr lastTaskbarLocalZOrderAnchorHandle = IntPtr.Zero;
        private IntPtr taskbarIntegrationHostHandle = IntPtr.Zero;

        public event EventHandler OverlayMenuRequested;
        public event EventHandler OverlayLocationCommitted;
        public event EventHandler TaskbarIntegrationNoSpaceAcknowledged;
        public event EventHandler<TaskbarSectionModeChangeRequestedEventArgs> TaskbarSectionModeChangeRequested;
        public event EventHandler<TrafficUsageMeasuredEventArgs> TrafficUsageMeasured;

        public bool HasDeferredVisibilityRequest
        {
            get { return this.taskbarIntegrationDisplayRequested; }
        }

        public event EventHandler<RatesUpdatedEventArgs> RatesUpdated;
    }
}
