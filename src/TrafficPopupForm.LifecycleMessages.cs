using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmNcHitTest && this.ShouldPassMouseToUnderlyingDesktop())
            {
                m.Result = new IntPtr(HtTransparent);
                return;
            }

            if (m.Msg == WmContextMenu)
            {
                m.Result = IntPtr.Zero;
                return;
            }

            if (m.Msg == WmDpiChanged)
            {
                int newDpi = DpiHelper.HiWord(m.WParam);
                Point newLocation = this.Location;

                if (m.LParam != IntPtr.Zero)
                {
                    NativeRect suggestedRect = (NativeRect)Marshal.PtrToStructure(m.LParam, typeof(NativeRect));
                    newLocation = new Point(suggestedRect.Left, suggestedRect.Top);
                }

                this.ApplyDpiLayout(newDpi);
                this.Location = this.GetVisiblePopupLocation(
                    newLocation,
                    null,
                    "popup-location-dpi-clamped",
                    "Popup-Position wurde nach einer DPI-Aenderung in einen sichtbaren Arbeitsbereich verschoben.");
                this.OnOverlayLocationCommitted();
                return;
            }

            if (m.Msg == WmDisplayChange || m.Msg == WmSettingChange)
            {
                base.WndProc(ref m);

                if (this.IsHandleCreated &&
                    (this.Visible || this.taskbarIntegrationDisplayRequested))
                {
                    this.TryBeginInvokeSafely(new Action(delegate
                    {
                        if (this.IsDisposed)
                        {
                            return;
                        }

                        if (this.settings.TaskbarIntegrationEnabled)
                        {
                            this.RefreshTaskbarIntegration(false, false);
                        }
                        else if (this.Visible)
                        {
                            this.EnsureVisiblePopupLocation(
                                "popup-location-workarea-clamped",
                                "Popup-Position wurde nach einer Monitor- oder Arbeitsbereichsaenderung in einen sichtbaren Bereich verschoben.");
                        }
                    }));
                }

                return;
            }

            base.WndProc(ref m);
        }

        private bool TryBeginInvokeSafely(Action action)
        {
            if (action == null || this.IsDisposed || !this.IsHandleCreated)
            {
                return false;
            }

            try
            {
                this.BeginInvoke(action);
                return true;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}
