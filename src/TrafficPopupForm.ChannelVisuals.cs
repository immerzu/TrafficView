using System.Drawing;
using System.Windows.Forms;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private static Label CreateCaptionLabel(string text, Color color)
        {
            Label label = new OverlayInputLabel();
            label.AutoSize = false;
            label.BackColor = Color.Transparent;
            label.ForeColor = color;
            label.Text = text;
            label.TextAlign = ContentAlignment.MiddleLeft;
            return label;
        }

        private static Label CreateValueLabel(Color color)
        {
            Label label = new OverlayInputLabel();
            label.AutoSize = false;
            label.AutoEllipsis = true;
            label.BackColor = Color.Transparent;
            label.ForeColor = color;
            label.Text = "0 B/s";
            label.TextAlign = ContentAlignment.MiddleLeft;
            return label;
        }

        private float GetChannelRingWeight(bool useDownload)
        {
            return useDownload ? 1.12F : 1.00F;
        }

        private float GetChannelSparklineWidthScale(bool useDownload)
        {
            return useDownload ? 1.08F : 1.00F;
        }

        private int GetChannelSparklineAreaAlpha(bool useDownload)
        {
            return useDownload ? 38 : 30;
        }

        private int GetChannelPeakMarkerAlpha(bool useDownload)
        {
            return useDownload ? 188 : 176;
        }
    }
}
