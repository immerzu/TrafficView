using System;
using System.Drawing;
using System.Windows.Forms;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private bool ShouldYieldToFullscreenForegroundWindow(Rectangle screenBounds)
        {
            IntPtr foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero || foregroundWindow == this.Handle)
            {
                return false;
            }

            string foregroundClassName = GetWindowClassName(foregroundWindow);
            if (IsTaskbarShellWindowClass(foregroundClassName))
            {
                return false;
            }

            NativeRect foregroundRect;
            if (!GetWindowRect(foregroundWindow, out foregroundRect))
            {
                return false;
            }

            Rectangle visibleForeground = Rectangle.Intersect(foregroundRect.ToRectangle(), screenBounds);
            if (visibleForeground.Width <= 0 || visibleForeground.Height <= 0)
            {
                return false;
            }

            int widthTolerance = Math.Max(2, this.ScaleValue(2));
            int heightTolerance = Math.Max(2, this.ScaleValue(2));
            return visibleForeground.Width >= screenBounds.Width - widthTolerance &&
                visibleForeground.Height >= screenBounds.Height - heightTolerance;
        }

        private void TryActivatePopupWindow()
        {
            if (!this.ShouldUseGlobalTopMost())
            {
                return;
            }

            IntPtr foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == this.Handle)
            {
                return;
            }

            this.BringToFront();

            if (Form.ActiveForm != this)
            {
                try
                {
                    this.Activate();
                }
                catch (InvalidOperationException ex)
                {
                    AppLog.WarnOnce(
                        "popup-activate-invalid-operation",
                        "Popup-Aktivierung konnte nicht ausgefuehrt werden.",
                        ex);
                }
            }

            if (GetForegroundWindow() == this.Handle)
            {
                return;
            }

            if (!SetForegroundWindow(this.Handle))
            {
                AppLog.WarnOnce(
                    "popup-setforegroundwindow-failed",
                    "SetForegroundWindow konnte das Popup nicht in den Vordergrund holen; TopMost bleibt aktiv.");
            }
        }
    }
}
