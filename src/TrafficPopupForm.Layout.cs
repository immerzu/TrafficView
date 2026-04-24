using System;
using System.Drawing;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private void ApplyDpiLayout(int dpi)
        {
            this.ApplyDpiLayout(dpi, true);
        }

        private void ApplyDpiLayout(int dpi, bool refreshIfVisible)
        {
            dpi = DpiHelper.NormalizeDpi(dpi);
            this.currentDpi = dpi;
            PanelSkinDefinition definition = this.GetCurrentPanelSkinDefinition();

            Font newFormFont = new Font("Segoe UI", this.ScaleFloat(BaseFormFontSize), FontStyle.Regular, GraphicsUnit.Pixel);
            Font newCaptionFont = new Font("Segoe UI", this.ScaleFloat(BaseCaptionFontSize), FontStyle.Bold, GraphicsUnit.Pixel);
            float valueFontSize = this.IsSimpleDisplayMode()
                ? SimpleValueFontSize
                : BaseValueFontSize;
            Font newValueFont = new Font("Segoe UI Semibold", this.ScaleFloat(valueFontSize), FontStyle.Bold, GraphicsUnit.Pixel);

            Font previousFormFont = this.formFont;
            Font previousCaptionFont = this.captionFont;
            Font previousValueFont = this.valueFont;

            this.formFont = newFormFont;
            this.captionFont = newCaptionFont;
            this.valueFont = newValueFont;

            this.SuspendLayout();

            Size baseClientSize = definition != null && definition.ClientSize.HasValue
                ? definition.ClientSize.Value
                : new Size(BaseClientWidth, BaseClientHeight);
            baseClientSize = this.GetBaseClientSizeForSection(
                this.GetDisplayModeAdjustedBaseClientSize(baseClientSize),
                this.GetEffectivePopupSectionMode());
            Size clientSize = this.ScaleSkinSize(baseClientSize);
            this.ClientSize = clientSize;
            this.MinimumSize = clientSize;
            this.MaximumSize = clientSize;
            this.Font = this.formFont;

            Rectangle defaultDownloadCaptionBounds = new Rectangle(BaseCaptionX, BaseDownloadCaptionY, BaseCaptionWidth, BaseCaptionHeight);
            Rectangle defaultDownloadValueBounds = new Rectangle(BaseDownloadValueX, BaseDownloadValueY, BaseValueWidth, BaseValueHeight);
            Rectangle defaultUploadCaptionBounds = new Rectangle(BaseCaptionX, BaseUploadCaptionY, BaseCaptionWidth, BaseCaptionHeight);
            Rectangle defaultUploadValueBounds = new Rectangle(BaseUploadValueX, BaseUploadValueY, BaseValueWidth, BaseValueHeight);

            Rectangle scaledDownloadCaptionBounds = this.GetCaptionBoundsForCurrentSection(
                defaultDownloadCaptionBounds,
                definition != null ? definition.DownloadCaptionBounds : (Rectangle?)null);
            Rectangle scaledDownloadValueBounds = this.GetValueBoundsForCurrentSection(
                defaultDownloadValueBounds,
                definition != null ? definition.DownloadValueBounds : (Rectangle?)null);
            Rectangle scaledUploadCaptionBounds = this.GetCaptionBoundsForCurrentSection(
                defaultUploadCaptionBounds,
                definition != null ? definition.UploadCaptionBounds : (Rectangle?)null);
            Rectangle scaledUploadValueBounds = this.GetValueBoundsForCurrentSection(
                defaultUploadValueBounds,
                definition != null ? definition.UploadValueBounds : (Rectangle?)null);

            if (this.IsSimpleDisplayMode() && this.IsBothSectionsVisible())
            {
                Rectangle simpleMeterBounds = this.GetDownloadMeterBounds();
                int maxCaptionRight = Math.Max(
                    this.ScaleValue(42),
                    simpleMeterBounds.Left - this.ScaleValue(12));
                int maxValueRight = Math.Max(
                    this.ScaleValue(48),
                    simpleMeterBounds.Left - this.ScaleValue(3));

                scaledDownloadCaptionBounds = new Rectangle(
                    scaledDownloadCaptionBounds.X + this.ScaleValue(2),
                    scaledDownloadCaptionBounds.Y + this.ScaleValue(2),
                    Math.Max(this.ScaleValue(12), Math.Min(scaledDownloadCaptionBounds.Width, maxCaptionRight - scaledDownloadCaptionBounds.Left)),
                    scaledDownloadCaptionBounds.Height);

                scaledUploadCaptionBounds = new Rectangle(
                    scaledUploadCaptionBounds.X + this.ScaleValue(2),
                    Math.Max(0, scaledUploadCaptionBounds.Y - this.ScaleValue(1)),
                    Math.Max(this.ScaleValue(12), Math.Min(scaledUploadCaptionBounds.Width, maxCaptionRight - scaledUploadCaptionBounds.Left)),
                    scaledUploadCaptionBounds.Height);

                scaledDownloadValueBounds = new Rectangle(
                    scaledDownloadValueBounds.X + this.ScaleValue(2),
                    scaledDownloadValueBounds.Y + this.ScaleValue(2),
                    Math.Max(this.ScaleValue(28), maxValueRight - (scaledDownloadValueBounds.X + this.ScaleValue(2))),
                    scaledDownloadValueBounds.Height);

                scaledUploadValueBounds = new Rectangle(
                    scaledUploadValueBounds.X + this.ScaleValue(1),
                    Math.Max(0, scaledUploadValueBounds.Y - this.ScaleValue(4)),
                    Math.Max(this.ScaleValue(28), maxValueRight - (scaledUploadValueBounds.X + this.ScaleValue(1))),
                    scaledUploadValueBounds.Height);
            }

            if (this.IsTaskbarIntegrationActive() && !scaledUploadValueBounds.IsEmpty)
            {
                scaledUploadValueBounds.Offset(0, -this.ScaleValue(2));
            }

            this.downloadCaptionLabel.Font = this.captionFont;
            this.downloadCaptionLabel.Location = scaledDownloadCaptionBounds.Location;
            this.downloadCaptionLabel.Size = scaledDownloadCaptionBounds.Size;

            this.downloadValueLabel.Font = this.valueFont;
            this.downloadValueLabel.Location = scaledDownloadValueBounds.Location;
            this.downloadValueLabel.Size = scaledDownloadValueBounds.Size;

            this.uploadCaptionLabel.Font = this.captionFont;
            this.uploadCaptionLabel.Location = scaledUploadCaptionBounds.Location;
            this.uploadCaptionLabel.Size = scaledUploadCaptionBounds.Size;

            this.uploadValueLabel.Font = this.valueFont;
            this.uploadValueLabel.Location = scaledUploadValueBounds.Location;
            this.uploadValueLabel.Size = scaledUploadValueBounds.Size;

            this.ResumeLayout(false);
            this.UpdateWindowRegion();
            this.staticSurfaceDirty = true;

            if (refreshIfVisible && this.Visible)
            {
                this.RefreshVisualSurface();
            }

            DisposeFont(previousFormFont);
            DisposeFont(previousCaptionFont);
            DisposeFont(previousValueFont);
        }

        private Size GetBaseClientSizeForCurrentSection(Size baseClientSize)
        {
            return this.GetBaseClientSizeForSection(
                this.GetDisplayModeAdjustedBaseClientSize(baseClientSize),
                this.GetEffectivePopupSectionMode());
        }

        private Size GetBaseClientSizeForSection(Size baseClientSize, PopupSectionMode popupSectionMode)
        {
            switch (popupSectionMode)
            {
                case PopupSectionMode.LeftOnly:
                    return new Size(
                        Math.Min(baseClientSize.Width, BaseLeftOnlyClientWidth),
                        baseClientSize.Height);
                case PopupSectionMode.RightOnly:
                    return new Size(
                        Math.Min(baseClientSize.Width, BaseRightOnlyClientWidth),
                        baseClientSize.Height);
                default:
                    return baseClientSize;
            }
        }

        private Size GetDisplayModeAdjustedBaseClientSize(Size baseClientSize)
        {
            return baseClientSize;
        }

        private int GetPanelOuterInset()
        {
            if (this.IsMiniSoftLikeDisplayMode())
            {
                return Math.Max(1, BaseOuterInset - 1);
            }

            return BaseOuterInset;
        }

        private Size GetScaledClientSizeForSection(PopupSectionMode popupSectionMode)
        {
            PanelSkinDefinition definition = this.GetCurrentPanelSkinDefinition();
            Size baseClientSize = definition != null && definition.ClientSize.HasValue
                ? definition.ClientSize.Value
                : new Size(BaseClientWidth, BaseClientHeight);
            return this.ScaleSkinSize(
                this.GetBaseClientSizeForSection(
                    this.GetDisplayModeAdjustedBaseClientSize(baseClientSize),
                    popupSectionMode));
        }

        private bool NeedsCurrentDpiLayout()
        {
            PanelSkinDefinition definition = this.GetCurrentPanelSkinDefinition();
            Size baseClientSize = definition != null && definition.ClientSize.HasValue
                ? definition.ClientSize.Value
                : new Size(BaseClientWidth, BaseClientHeight);
            baseClientSize = this.GetBaseClientSizeForCurrentSection(baseClientSize);
            Size desiredClientSize = this.ScaleSkinSize(baseClientSize);
            return this.ClientSize != desiredClientSize ||
                this.MinimumSize != desiredClientSize ||
                this.MaximumSize != desiredClientSize;
        }

        private bool NeedsTaskbarIntegrationLayoutRefresh(TaskbarIntegrationSnapshot snapshot)
        {
            if (snapshot == null || snapshot.IsHidden)
            {
                if (this.lastAppliedTaskbarThickness != -1)
                {
                    this.lastAppliedTaskbarThickness = -1;
                }

                return false;
            }

            int currentTaskbarThickness = snapshot.IsVertical
                ? snapshot.Bounds.Width
                : snapshot.Bounds.Height;
            if (this.lastAppliedTaskbarThickness == currentTaskbarThickness)
            {
                return false;
            }

            this.lastAppliedTaskbarThickness = currentTaskbarThickness;
            return true;
        }

        private Rectangle GetCaptionBoundsForCurrentSection(Rectangle defaultBounds, Rectangle? overrideBounds)
        {
            PopupSectionMode popupSectionMode = this.GetEffectivePopupSectionMode();
            if (popupSectionMode == PopupSectionMode.RightOnly)
            {
                return Rectangle.Empty;
            }

            if (popupSectionMode == PopupSectionMode.Both)
            {
                return this.GetScaledSkinBounds(defaultBounds, overrideBounds);
            }

            Rectangle scaledBounds = this.ScaleSkinRectangle(defaultBounds);
            scaledBounds.Width = Math.Max(scaledBounds.Width, this.ScaleValue(BaseCaptionWidth));
            return scaledBounds;
        }

        private Rectangle GetValueBoundsForCurrentSection(Rectangle defaultBounds, Rectangle? overrideBounds)
        {
            PopupSectionMode popupSectionMode = this.GetEffectivePopupSectionMode();
            if (popupSectionMode == PopupSectionMode.RightOnly)
            {
                return Rectangle.Empty;
            }

            if (popupSectionMode == PopupSectionMode.Both)
            {
                return this.GetScaledSkinBounds(defaultBounds, overrideBounds);
            }

            Rectangle scaledBounds = this.ScaleSkinRectangle(defaultBounds);
            int rightPadding = this.ScaleValue(6);
            int width = Math.Max(
                this.ScaleValue(22),
                this.ClientSize.Width - scaledBounds.Left - rightPadding);
            return new Rectangle(scaledBounds.Left, scaledBounds.Top, width, scaledBounds.Height);
        }

    }
}
