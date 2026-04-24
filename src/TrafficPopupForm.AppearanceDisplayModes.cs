using System.Drawing;
using System.IO;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private bool IsMiniGraphDisplayMode()
        {
            return this.settings != null && this.settings.PopupDisplayMode == PopupDisplayMode.MiniGraph;
        }

        private bool IsMiniSoftDisplayMode()
        {
            return this.settings != null && this.settings.PopupDisplayMode == PopupDisplayMode.MiniSoft;
        }

        private bool IsSimpleDisplayMode()
        {
            return this.settings != null &&
                (this.settings.PopupDisplayMode == PopupDisplayMode.Simple ||
                 this.settings.PopupDisplayMode == PopupDisplayMode.SimpleBlue);
        }

        private bool IsMiniSoftLikeDisplayMode()
        {
            return this.IsMiniSoftDisplayMode() || this.IsSimpleDisplayMode();
        }

        private bool IsAlternativeDisplayMode()
        {
            return this.IsMiniGraphDisplayMode() || this.IsMiniSoftLikeDisplayMode();
        }

        private string GetActivePanelAssetDirectoryPath()
        {
            if (this.IsSimpleDisplayMode())
            {
                string simpleModeAssetDirectoryPath = Path.Combine(
                    AppStorage.BaseDirectory,
                    "DisplayModeAssets",
                    this.GetSimpleModeAssetDirectoryName());
                if (Directory.Exists(simpleModeAssetDirectoryPath))
                {
                    return simpleModeAssetDirectoryPath;
                }
            }

            PanelSkinDefinition definition = this.GetCurrentPanelSkinDefinition();
            return definition != null ? definition.DirectoryPath : string.Empty;
        }

        private string GetSimpleModeAssetDirectoryName()
        {
            return this.settings != null && this.settings.PopupDisplayMode == PopupDisplayMode.SimpleBlue
                ? SimpleBlueModeAssetDirectoryName
                : SimpleModeAssetDirectoryName;
        }

        private Color GetDownloadCaptionBaseColor()
        {
            return this.IsSimpleDisplayMode() ? SimpleDownloadCaptionColor : DownloadCaptionColor;
        }

        private Color GetDownloadValueBaseColor()
        {
            return this.IsSimpleDisplayMode() ? SimpleDownloadValueColor : DownloadValueColor;
        }

        private Color GetUploadCaptionBaseColor()
        {
            return this.IsSimpleDisplayMode() ? SimpleUploadCaptionColor : UploadCaptionColor;
        }

        private Color GetUploadValueBaseColor()
        {
            return this.IsSimpleDisplayMode() ? SimpleUploadValueColor : UploadValueColor;
        }

        private Color GetDownloadRingLowBaseColor()
        {
            return this.IsSimpleDisplayMode() ? SimpleDownloadRingLowColor : DownloadRingLowColor;
        }

        private Color GetDownloadRingHighBaseColor()
        {
            return this.IsSimpleDisplayMode() ? SimpleDownloadRingHighColor : DownloadRingHighColor;
        }

        private Color GetUploadRingLowBaseColor()
        {
            return this.IsSimpleDisplayMode() ? SimpleUploadRingLowColor : UploadRingLowColor;
        }

        private Color GetUploadRingHighBaseColor()
        {
            return this.IsSimpleDisplayMode() ? SimpleUploadRingHighColor : UploadRingHighColor;
        }

        private Color GetMeterTrackBaseColor()
        {
            return this.IsSimpleDisplayMode() ? SimpleMeterTrackColor : MeterTrackColor;
        }

        private Color GetMeterTrackInnerBaseColor()
        {
            return this.IsSimpleDisplayMode() ? SimpleMeterTrackInnerColor : MeterTrackInnerColor;
        }

        private Color GetMeterCenterBaseColor()
        {
            return this.IsSimpleDisplayMode() ? SimpleMeterCenterColor : MeterCenterColor;
        }

        private Color GetDownloadArrowBaseColor()
        {
            return this.IsSimpleDisplayMode() ? SimpleDownloadArrowBaseColor : DownloadArrowBaseColor;
        }

        private Color GetDownloadArrowHighBaseColor()
        {
            return this.IsSimpleDisplayMode() ? SimpleDownloadArrowHighColor : DownloadArrowHighColor;
        }

        private Color GetUploadArrowBaseColor()
        {
            return this.IsSimpleDisplayMode() ? SimpleUploadArrowBaseColor : UploadArrowBaseColor;
        }

        private Color GetUploadArrowHighBaseColor()
        {
            return this.IsSimpleDisplayMode() ? SimpleUploadArrowHighColor : UploadArrowHighColor;
        }

        private Color GetSparklineGuideBaseColor()
        {
            return this.IsSimpleDisplayMode() ? SimpleSparklineGuideColor : SparklineGuideColor;
        }

        private Color GetSparklineDownloadBaseColor()
        {
            return this.IsSimpleDisplayMode() ? SimpleSparklineDownloadColor : SparklineDownloadColor;
        }

        private Color GetSparklineUploadBaseColor()
        {
            return this.IsSimpleDisplayMode() ? SimpleSparklineUploadColor : SparklineUploadColor;
        }

        private int GetCurrentOverlaySparklinePointCount()
        {
            return this.IsAlternativeDisplayMode()
                ? MiniGraphOverlaySparklinePointCount
                : OverlaySparklinePointCount;
        }

        private int GetCurrentRingSegmentCount()
        {
            return this.IsAlternativeDisplayMode()
                ? MiniGraphRingSegmentCount
                : RingSegmentCount;
        }

        private float GetCurrentRingSegmentGapDegrees()
        {
            return this.IsAlternativeDisplayMode()
                ? MiniGraphRingSegmentGapDegrees
                : RingSegmentGapDegrees;
        }
    }
}
