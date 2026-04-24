using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private void DrawReadableTrafficText(
            Graphics graphics,
            string text,
            Font font,
            Color color,
            Rectangle bounds,
            bool allowEllipsis,
            bool isPrimaryValue)
        {
            GraphicsState state = graphics.Save();
            RectangleF stableBounds = GetStableTextBounds(bounds);
            bool disposeDrawingFont;
            Font drawingFont = this.GetTrafficTextFontForBounds(
                graphics,
                text,
                font,
                stableBounds,
                isPrimaryValue,
                out disposeDrawingFont);
            bool useEllipsis = allowEllipsis && !(this.IsSimpleDisplayMode() && isPrimaryValue);
            StringFormat format = this.GetTrafficTextFormatForBounds(useEllipsis, isPrimaryValue);
            int transparencyPercent = this.settings != null ? this.settings.TransparencyPercent : 0;
            bool ultraTransparent = transparencyPercent >= 100;
            bool taskbarIntegrated = this.IsTaskbarIntegrationActive();
            Color textColor = taskbarIntegrated && isPrimaryValue
                ? GetCrispTaskbarIntegratedValueColor(color)
                : color;
            int glowAlpha = taskbarIntegrated
                ? (isPrimaryValue ? 42 : 34)
                : ultraTransparent
                ? (isPrimaryValue ? 68 : 42)
                : Math.Min(isPrimaryValue ? 48 : 28, (isPrimaryValue ? 28 : 16) + (int)Math.Round(transparencyPercent * 0.22D));
            int shadowAlpha = taskbarIntegrated
                ? (isPrimaryValue ? 214 : 176)
                : ultraTransparent
                ? (isPrimaryValue ? 212 : 176)
                : Math.Min(isPrimaryValue ? 164 : 132, (isPrimaryValue ? 112 : 92) + (int)Math.Round(transparencyPercent * 0.52D));
            int outlineAlpha = taskbarIntegrated
                ? (isPrimaryValue ? 206 : 172)
                : ultraTransparent
                ? (isPrimaryValue ? 198 : 166)
                : Math.Min(isPrimaryValue ? 148 : 118, (isPrimaryValue ? 92 : 72) + (int)Math.Round(transparencyPercent * 0.56D));

            using (SolidBrush glowBrush = new SolidBrush(Color.FromArgb(glowAlpha, textColor)))
            using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(shadowAlpha, 4, 10, 24)))
            using (SolidBrush outlineBrush = new SolidBrush(Color.FromArgb(outlineAlpha, 6, 14, 30)))
            using (SolidBrush textBrush = new SolidBrush(textColor))
            {
                graphics.PixelOffsetMode = PixelOffsetMode.Half;
                graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                graphics.DrawString(text, drawingFont, glowBrush, OffsetRectangle(stableBounds, 0F, 0.8F), format);
                graphics.DrawString(text, drawingFont, outlineBrush, OffsetRectangle(stableBounds, -0.8F, 0F), format);
                graphics.DrawString(text, drawingFont, outlineBrush, OffsetRectangle(stableBounds, 0.8F, 0F), format);
                graphics.DrawString(text, drawingFont, outlineBrush, OffsetRectangle(stableBounds, 0F, -0.8F), format);
                graphics.DrawString(text, drawingFont, outlineBrush, OffsetRectangle(stableBounds, 0F, 0.8F), format);
                graphics.DrawString(text, drawingFont, shadowBrush, OffsetRectangle(stableBounds, 0.35F, 1.15F), format);
                graphics.DrawString(text, drawingFont, textBrush, stableBounds, format);
            }

            if (disposeDrawingFont)
            {
                drawingFont.Dispose();
            }

            graphics.Restore(state);
        }

        private void DrawTrafficText(
            Graphics graphics,
            string text,
            Font font,
            Color color,
            Rectangle bounds,
            bool allowEllipsis,
            bool isPrimaryValue)
        {
            GraphicsState state = graphics.Save();
            RectangleF stableBounds = GetStableTextBounds(bounds);
            bool disposeDrawingFont;
            Font drawingFont = this.GetTrafficTextFontForBounds(
                graphics,
                text,
                font,
                stableBounds,
                isPrimaryValue,
                out disposeDrawingFont);
            bool useEllipsis = allowEllipsis && !(this.IsSimpleDisplayMode() && isPrimaryValue);
            RectangleF contrastBounds = OffsetRectangle(
                stableBounds,
                0F,
                0.5F);
            int transparencyPercent = this.settings != null ? this.settings.TransparencyPercent : 0;
            bool ultraTransparent = transparencyPercent >= 100;
            bool taskbarIntegrated = this.IsTaskbarIntegrationActive();
            Color textColor = taskbarIntegrated && isPrimaryValue
                ? GetCrispTaskbarIntegratedValueColor(color)
                : color;
            int contrastAlpha = taskbarIntegrated
                ? (isPrimaryValue ? 174 : 138)
                : ultraTransparent
                ? (isPrimaryValue ? 164 : 132)
                : Math.Min(
                    isPrimaryValue ? 112 : 86,
                    (isPrimaryValue ? 32 : 20) + (int)Math.Round(transparencyPercent * (isPrimaryValue ? 0.80D : 0.66D)));

            using (SolidBrush contrastBrush = new SolidBrush(Color.FromArgb(
                contrastAlpha,
                BackgroundBlue.R,
                BackgroundBlue.G,
                BackgroundBlue.B)))
            using (SolidBrush textBrush = new SolidBrush(textColor))
            {
                graphics.PixelOffsetMode = PixelOffsetMode.Half;
                graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                graphics.DrawString(
                    text,
                    drawingFont,
                    contrastBrush,
                    contrastBounds,
                    this.GetTrafficTextFormatForBounds(useEllipsis, isPrimaryValue));
                graphics.DrawString(
                    text,
                    drawingFont,
                    textBrush,
                    stableBounds,
                    this.GetTrafficTextFormatForBounds(useEllipsis, isPrimaryValue));
            }

            if (disposeDrawingFont)
            {
                drawingFont.Dispose();
            }

            graphics.Restore(state);
        }

        private static Color GetCrispTaskbarIntegratedValueColor(Color color)
        {
            double whiteBoost = color.ToArgb() == DownloadValueColor.ToArgb() ||
                color.ToArgb() == SimpleDownloadValueColor.ToArgb()
                ? 0.44D
                : 0.28D;
            return GetInterpolatedColor(color, Color.White, whiteBoost);
        }
    }
}
