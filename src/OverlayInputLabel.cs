using System;
using System.Windows.Forms;

namespace TrafficView
{
    internal sealed class OverlayInputLabel : Label
    {
        private const int WmContextMenu = 0x007B;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmContextMenu)
            {
                m.Result = IntPtr.Zero;
                return;
            }

            base.WndProc(ref m);
        }
    }
}
