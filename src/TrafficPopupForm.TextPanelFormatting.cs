using System;
using System.Drawing;
using System.Drawing.Text;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private static StringFormat CreateTrafficTextFormat(bool allowEllipsis)
        {
            return CreateTrafficTextFormat(allowEllipsis, StringAlignment.Near);
        }

        private static StringFormat CreateTrafficTextFormat(bool allowEllipsis, StringAlignment alignment)
        {
            StringFormat stringFormat = new StringFormat(StringFormat.GenericTypographic);
            stringFormat.Alignment = alignment;
            stringFormat.LineAlignment = StringAlignment.Center;
            stringFormat.FormatFlags = StringFormatFlags.NoWrap;
            stringFormat.Trimming = allowEllipsis
                ? StringTrimming.EllipsisCharacter
                : StringTrimming.None;
            return stringFormat;
        }

        private StringFormat GetTrafficTextFormatForBounds(bool useEllipsis, bool isPrimaryValue)
        {
            if (this.IsSimpleDisplayMode() && isPrimaryValue)
            {
                return TrafficRightAlignedTextFormat;
            }

            return useEllipsis ? TrafficEllipsisTextFormat : TrafficTextStringFormat;
        }

        private Rectangle GetPrimaryValueDrawBounds(Rectangle bounds)
        {
            if (!this.IsSimpleDisplayMode() ||
                !this.IsBothSectionsVisible() ||
                !this.IsRightSectionVisible())
            {
                return bounds;
            }

            Rectangle meterBounds = this.GetDownloadMeterBounds();
            int maxRight = Math.Max(
                bounds.Left + this.ScaleValue(16),
                meterBounds.Left - this.ScaleValue(3));
            return new Rectangle(
                bounds.Left,
                bounds.Top,
                Math.Max(1, Math.Min(bounds.Width, maxRight - bounds.Left)),
                bounds.Height);
        }

        private bool ShouldAllowPrimaryValueEllipsis(Graphics graphics, string text, Font font, Rectangle bounds)
        {
            if (!this.IsSimpleDisplayMode() || string.IsNullOrEmpty(text))
            {
                return true;
            }

            SizeF measuredSize = graphics.MeasureString(
                text,
                font,
                new SizeF(1000F, Math.Max(1F, bounds.Height)),
                TrafficTextStringFormat);
            float horizontalPadding = this.ScaleFloat(3.5F);
            return measuredSize.Width + horizontalPadding > bounds.Width;
        }

        private Font GetTrafficTextFontForBounds(
            Graphics graphics,
            string text,
            Font font,
            RectangleF bounds,
            bool isPrimaryValue,
            out bool shouldDispose)
        {
            shouldDispose = false;
            if (!this.IsSimpleDisplayMode() ||
                !isPrimaryValue ||
                string.IsNullOrEmpty(text) ||
                font == null ||
                bounds.Width <= 1F)
            {
                return font;
            }

            float availableWidth = Math.Max(1F, bounds.Width - this.ScaleFloat(0.6F));
            SizeF measuredSize = graphics.MeasureString(
                text,
                font,
                new SizeF(1000F, Math.Max(1F, bounds.Height)),
                TrafficTextStringFormat);
            float minFontSize = Math.Max(this.ScaleFloat(5.2F), 5.2F);
            float maxFontSize = Math.Min(
                Math.Max(font.Size, this.ScaleFloat(10.8F)),
                Math.Max(font.Size, bounds.Height * 0.86F));
            Font bestFont = null;

            if (measuredSize.Width <= availableWidth)
            {
                float candidateSize = font.Size;
                while (candidateSize < maxFontSize)
                {
                    candidateSize = Math.Min(maxFontSize, candidateSize + this.ScaleFloat(0.25F));
                    Font candidateFont = new Font(font.FontFamily, candidateSize, font.Style, font.Unit);
                    measuredSize = graphics.MeasureString(
                        text,
                        candidateFont,
                        new SizeF(1000F, Math.Max(1F, bounds.Height)),
                        TrafficTextStringFormat);
                    if (measuredSize.Width > availableWidth)
                    {
                        candidateFont.Dispose();
                        break;
                    }

                    if (bestFont != null)
                    {
                        bestFont.Dispose();
                    }

                    bestFont = candidateFont;
                }

                if (bestFont != null)
                {
                    shouldDispose = true;
                    return bestFont;
                }

                return font;
            }

            float shrinkingSize = font.Size;
            while (shrinkingSize > minFontSize)
            {
                shrinkingSize = Math.Max(minFontSize, shrinkingSize - this.ScaleFloat(0.25F));
                Font candidateFont = new Font(font.FontFamily, shrinkingSize, font.Style, font.Unit);
                measuredSize = graphics.MeasureString(
                    text,
                    candidateFont,
                    new SizeF(1000F, Math.Max(1F, bounds.Height)),
                    TrafficTextStringFormat);
                if (measuredSize.Width <= availableWidth || Math.Abs(shrinkingSize - minFontSize) < 0.01F)
                {
                    shouldDispose = true;
                    return candidateFont;
                }

                candidateFont.Dispose();
            }

            return font;
        }
    }
}
