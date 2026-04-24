using System;
using System.Drawing;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private bool IsGlassPanelSkinEnabled()
        {
            PanelSkinDefinition definition = PanelSkinCatalog.GetSkinById(this.settings.PanelSkinId);
            return definition != null && definition.HasGlassSurfaceEffect;
        }

        private bool IsReadableInfoPanelSkinEnabled()
        {
            PanelSkinDefinition definition = PanelSkinCatalog.GetSkinById(this.settings.PanelSkinId);
            return definition != null && definition.HasReadableInfoPlateEffect;
        }

        private PanelSkinDefinition GetCurrentPanelSkinDefinition()
        {
            return PanelSkinCatalog.GetSkinById(this.settings.PanelSkinId);
        }

        private bool IsHudOnlyTransparencyMode()
        {
            return this.settings != null && this.settings.TransparencyPercent >= 100;
        }

        private byte GetStaticPanelBackgroundAlpha()
        {
            byte configuredAlpha = MonitorSettings.ToOpacityByte(this.settings.TransparencyPercent);
            return this.IsTaskbarIntegrationActive()
                ? Math.Min(configuredAlpha, TaskbarIntegratedPanelMaxBackgroundAlpha)
                : configuredAlpha;
        }

        private bool ShouldDrawStaticBackgroundLayer()
        {
            return !this.IsHudOnlyTransparencyMode()
                && this.GetStaticPanelBackgroundAlpha() > 0;
        }

        private bool IsLeftSectionVisible()
        {
            PopupSectionMode popupSectionMode = this.GetEffectivePopupSectionMode();
            return popupSectionMode == PopupSectionMode.Both ||
                popupSectionMode == PopupSectionMode.LeftOnly;
        }

        private bool IsRightSectionVisible()
        {
            PopupSectionMode popupSectionMode = this.GetEffectivePopupSectionMode();
            return popupSectionMode == PopupSectionMode.Both ||
                popupSectionMode == PopupSectionMode.RightOnly;
        }

        private bool IsBothSectionsVisible()
        {
            return this.GetEffectivePopupSectionMode() == PopupSectionMode.Both;
        }

        private PopupSectionMode GetConfiguredPopupSectionMode()
        {
            if (this.settings == null)
            {
                return PopupSectionMode.Both;
            }

            return this.settings.TaskbarIntegrationEnabled
                ? this.settings.TaskbarPopupSectionMode
                : this.settings.PopupSectionMode;
        }

        private PopupSectionMode GetEffectivePopupSectionMode()
        {
            if ((this.taskbarIntegrationForceRightOnlySection ||
                this.taskbarIntegrationStickyRightOnlySection) &&
                this.settings != null &&
                this.settings.TaskbarIntegrationEnabled)
            {
                return PopupSectionMode.RightOnly;
            }

            return this.GetConfiguredPopupSectionMode();
        }

        private bool ShouldDrawDynamicRing()
        {
            if (this.IsSimpleDisplayMode())
            {
                return this.IsRightSectionVisible();
            }

            PanelSkinDefinition definition = this.GetCurrentPanelSkinDefinition();
            return this.IsRightSectionVisible() &&
                (definition == null || definition.DrawDynamicRing);
        }

        private bool ShouldDrawCenterTrafficArrows()
        {
            if (this.IsSimpleDisplayMode())
            {
                return this.IsRightSectionVisible();
            }

            PanelSkinDefinition definition = this.GetCurrentPanelSkinDefinition();
            return this.IsRightSectionVisible() &&
                (definition == null || definition.DrawCenterArrows);
        }

        private bool ShouldDrawSparkline()
        {
            if (this.IsSimpleDisplayMode())
            {
                return this.IsLeftSectionVisible();
            }

            PanelSkinDefinition definition = this.GetCurrentPanelSkinDefinition();
            return this.IsLeftSectionVisible() &&
                (definition == null || definition.DrawSparkline);
        }

        private bool ShouldDrawMeterValueSupport()
        {
            if (this.IsSimpleDisplayMode())
            {
                return false;
            }

            PanelSkinDefinition definition = this.GetCurrentPanelSkinDefinition();
            return this.IsLeftSectionVisible() &&
                (definition == null || definition.DrawMeterValueSupport);
        }

        private Rectangle ScaleSkinRectangle(Rectangle bounds)
        {
            return new Rectangle(
                this.ScaleValue(bounds.X),
                this.ScaleValue(bounds.Y),
                this.ScaleValue(bounds.Width),
                this.ScaleValue(bounds.Height));
        }

        private Size ScaleSkinSize(Size size)
        {
            return new Size(
                this.ScaleValue(size.Width),
                this.ScaleValue(size.Height));
        }

        private Rectangle GetScaledSkinBounds(Rectangle defaultBounds, Rectangle? overrideBounds)
        {
            return this.ScaleSkinRectangle(overrideBounds ?? defaultBounds);
        }

        private void ApplyWindowTransparency()
        {
            this.Opacity = 1D;
            this.BackColor = this.GetPanelBackgroundBaseColor();
            this.downloadCaptionLabel.ForeColor = this.GetDownloadCaptionBaseColor();
            this.downloadValueLabel.ForeColor = this.GetDownloadValueBaseColor();
            this.uploadCaptionLabel.ForeColor = this.GetUploadCaptionBaseColor();
            this.uploadValueLabel.ForeColor = this.GetUploadValueBaseColor();
            this.staticSurfaceDirty = true;

            if (!this.IsHandleCreated)
            {
                return;
            }

            if (this.Visible)
            {
                this.RefreshVisualSurface();
            }
        }

        private Color GetPanelBackgroundBaseColor()
        {
            return this.activeTaskbarSnapshot != null
                ? this.activeTaskbarSnapshot.Theme.BaseColor
                : this.IsSimpleDisplayMode()
                ? SimplePanelBackgroundColor
                : BackgroundBlue;
        }

        private Color GetPanelBorderBaseColor()
        {
            return this.activeTaskbarSnapshot != null
                ? this.activeTaskbarSnapshot.Theme.BorderColor
                : this.IsSimpleDisplayMode()
                ? SimpleBorderColor
                : BorderColor;
        }

        private Color GetPanelDividerBaseColor()
        {
            return this.activeTaskbarSnapshot != null
                ? this.activeTaskbarSnapshot.Theme.DividerColor
                : this.IsSimpleDisplayMode()
                ? SimpleDividerColor
                : DividerColor;
        }

        private byte GetTaskbarBackgroundOverlayAlpha()
        {
            return this.activeTaskbarSnapshot != null
                ? this.activeTaskbarSnapshot.Theme.OverlayAlpha
                : (byte)0;
        }

        private Rectangle GetDownloadMeterBounds()
        {
            PanelSkinDefinition definition = this.GetCurrentPanelSkinDefinition();
            if (definition != null &&
                definition.MeterBounds.HasValue &&
                this.IsBothSectionsVisible())
            {
                return this.ScaleSkinRectangle(definition.MeterBounds.Value);
            }

            bool miniGraphDisplayMode = this.IsAlternativeDisplayMode();
            int baseDiameter = this.IsSimpleDisplayMode()
                ? SimpleMeterDiameter
                : miniGraphDisplayMode
                ? MiniGraphMeterDiameter
                : BaseMeterDiameter;
            int diameter = this.ScaleValue(this.IsReadableInfoPanelSkinEnabled()
                ? baseDiameter - 3
                : baseDiameter);
            int x;
            if (this.GetEffectivePopupSectionMode() == PopupSectionMode.RightOnly)
            {
                x = Math.Max(0, (this.ClientSize.Width - diameter) / 2);
            }
            else
            {
                int rightInset = this.ScaleValue(this.IsSimpleDisplayMode()
                    ? SimpleMeterRightInset
                    : miniGraphDisplayMode
                    ? MiniGraphMeterRightInset
                    : BaseMeterRightInset);
                x = this.ClientSize.Width - diameter - rightInset;
                if (this.settings != null && this.settings.PopupDisplayMode == PopupDisplayMode.SimpleBlue)
                {
                    x -= this.ScaleValue(2);
                }
            }
            int y = miniGraphDisplayMode
                ? Math.Max(0, (this.ClientSize.Height - diameter) / 2)
                : Math.Max(this.ScaleValue(6), (this.ClientSize.Height - diameter) / 2);
            if (this.IsSimpleDisplayMode())
            {
                y = Math.Max(0, y - this.ScaleValue(1));
            }
            return new Rectangle(x, y, diameter, diameter);
        }

        private RectangleF CreateInsetBounds(Rectangle outerBounds, float inset)
        {
            float width = Math.Max(2F, outerBounds.Width - (inset * 2F));
            float height = Math.Max(2F, outerBounds.Height - (inset * 2F));
            return new RectangleF(
                outerBounds.Left + inset,
                outerBounds.Top + inset,
                width,
                height);
        }
    }
}
