using System;
using System.Windows.Forms;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private void WireSurface(Control control)
        {
            control.MouseDown += this.Control_MouseDown;
            control.MouseMove += this.Control_MouseMove;
            control.MouseUp += this.Control_MouseUp;
        }

        private void SuppressOverlayContextMenus()
        {
            SuppressContextMenu(this);
            SuppressContextMenu(this.downloadCaptionLabel);
            SuppressContextMenu(this.downloadValueLabel);
            SuppressContextMenu(this.uploadCaptionLabel);
            SuppressContextMenu(this.uploadValueLabel);
        }

        private static void SuppressContextMenu(Control control)
        {
            if (control != null)
            {
                control.ContextMenuStrip = null;
            }
        }

        private void OnOverlayMenuRequested()
        {
            EventHandler handler = this.OverlayMenuRequested;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void OnOverlayLocationCommitted()
        {
            EventHandler handler = this.OverlayLocationCommitted;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void OnTaskbarSectionModeChangeRequested(PopupSectionMode popupSectionMode)
        {
            EventHandler<TaskbarSectionModeChangeRequestedEventArgs> handler = this.TaskbarSectionModeChangeRequested;
            if (handler != null)
            {
                handler(this, new TaskbarSectionModeChangeRequestedEventArgs(popupSectionMode));
            }
        }
    }
}
