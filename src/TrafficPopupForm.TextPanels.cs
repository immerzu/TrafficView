using System.Drawing;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private void DrawTrafficTexts(Graphics graphics)
        {
            Rectangle downloadValueDrawBounds = this.GetPrimaryValueDrawBounds(this.downloadValueLabel.Bounds);
            Rectangle uploadValueDrawBounds = this.GetPrimaryValueDrawBounds(this.uploadValueLabel.Bounds);
            bool allowDownloadValueEllipsis = this.ShouldAllowPrimaryValueEllipsis(
                graphics,
                this.downloadValueLabel.Text,
                this.valueFont,
                downloadValueDrawBounds);
            bool allowUploadValueEllipsis = this.ShouldAllowPrimaryValueEllipsis(
                graphics,
                this.uploadValueLabel.Text,
                this.valueFont,
                uploadValueDrawBounds);

            if (this.IsReadableInfoPanelSkinEnabled())
            {
                Color downloadCaptionColor = GetInterpolatedColor(
                    this.GetDownloadCaptionBaseColor(),
                    Color.FromArgb(255, 244, 208, 136),
                    0.28D);
                Color downloadValueColor = GetInterpolatedColor(
                    this.GetDownloadValueBaseColor(),
                    Color.FromArgb(255, 255, 214, 96),
                    0.22D);
                Color uploadCaptionColor = GetInterpolatedColor(
                    this.GetUploadCaptionBaseColor(),
                    Color.FromArgb(255, 198, 255, 194),
                    0.24D);
                Color uploadValueColor = GetInterpolatedColor(
                    this.GetUploadValueBaseColor(),
                    Color.FromArgb(255, 132, 255, 148),
                    0.18D);

                DrawReadableTrafficText(
                    graphics,
                    this.downloadCaptionLabel.Text,
                    this.captionFont,
                    downloadCaptionColor,
                    this.downloadCaptionLabel.Bounds,
                    false,
                    false);
                DrawReadableTrafficText(
                    graphics,
                    this.downloadValueLabel.Text,
                    this.valueFont,
                    downloadValueColor,
                    downloadValueDrawBounds,
                    allowDownloadValueEllipsis,
                    true);
                DrawReadableTrafficText(
                    graphics,
                    this.uploadCaptionLabel.Text,
                    this.captionFont,
                    uploadCaptionColor,
                    this.uploadCaptionLabel.Bounds,
                    false,
                    false);
                DrawReadableTrafficText(
                    graphics,
                    this.uploadValueLabel.Text,
                    this.valueFont,
                    uploadValueColor,
                    uploadValueDrawBounds,
                    allowUploadValueEllipsis,
                    true);
                return;
            }

            DrawTrafficText(
                graphics,
                this.downloadCaptionLabel.Text,
                this.captionFont,
                this.GetDownloadCaptionBaseColor(),
                this.downloadCaptionLabel.Bounds,
                false,
                false);
            DrawTrafficText(
                graphics,
                this.downloadValueLabel.Text,
                this.valueFont,
                this.GetDownloadValueBaseColor(),
                downloadValueDrawBounds,
                allowDownloadValueEllipsis,
                true);
            DrawTrafficText(
                graphics,
                this.uploadCaptionLabel.Text,
                this.captionFont,
                this.GetUploadCaptionBaseColor(),
                this.uploadCaptionLabel.Bounds,
                false,
                false);
            DrawTrafficText(
                graphics,
                this.uploadValueLabel.Text,
                this.valueFont,
                this.GetUploadValueBaseColor(),
                uploadValueDrawBounds,
                allowUploadValueEllipsis,
                true);
        }

    }
}
