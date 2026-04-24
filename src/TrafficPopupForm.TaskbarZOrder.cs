using System;
using System.Runtime.InteropServices;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private void TrackTaskbarLocalZOrderAnchor(TaskbarIntegrationSnapshot snapshot)
        {
            if (!this.ShouldUseTaskbarLocalZOrder() ||
                snapshot == null ||
                snapshot.TaskbarZOrderAnchorHandle == IntPtr.Zero)
            {
                return;
            }

            if (this.lastTaskbarLocalZOrderAnchorHandle != IntPtr.Zero &&
                snapshot.TaskbarZOrderAnchorHandle != this.lastTaskbarLocalZOrderAnchorHandle)
            {
                this.taskbarLocalZOrderRepairPending = true;
            }
        }

        private void EnsureTaskbarLocalFrontPlacement(bool forceRepair)
        {
            if (!this.ShouldUseTaskbarLocalZOrder() ||
                !this.IsHandleCreated ||
                !this.Visible ||
                this.IsDisposed)
            {
                return;
            }

            TaskbarIntegrationSnapshot snapshot = this.activeTaskbarSnapshot;
            if (snapshot == null && !this.TryCaptureTaskbarIntegrationSnapshot(out snapshot))
            {
                return;
            }

            this.TrackTaskbarLocalZOrderAnchor(snapshot);
            this.activeTaskbarSnapshot = snapshot;

            IntPtr anchorHandle = snapshot.TaskbarZOrderAnchorHandle != IntPtr.Zero
                ? snapshot.TaskbarZOrderAnchorHandle
                : snapshot.TaskbarHandle;
            if (anchorHandle == IntPtr.Zero || anchorHandle == this.Handle)
            {
                return;
            }

            if (!forceRepair &&
                !this.taskbarLocalZOrderRepairPending &&
                this.lastTaskbarLocalZOrderAnchorHandle == anchorHandle &&
                this.IsWindowAboveZOrderAnchor(anchorHandle))
            {
                return;
            }

            IntPtr insertAfterHandle = this.GetTaskbarLocalInsertAfterHandle(anchorHandle);
            if (insertAfterHandle == IntPtr.Zero || insertAfterHandle == this.Handle)
            {
                return;
            }

            uint flags = SwpNoMove | SwpNoSize | SwpNoOwnerZOrder | SwpNoSendChanging | SwpNoActivate;
            bool repaired = SetWindowPos(this.Handle, insertAfterHandle, this.Left, this.Top, this.Width, this.Height, flags);
            if (!repaired)
            {
                AppLog.WarnOnce(
                    "taskbar-local-zorder-setwindowpos",
                    string.Format(
                        "SetWindowPos failed while repairing the taskbar-local z-order. Win32={0}",
                        Marshal.GetLastWin32Error()));
            }

            if (repaired && this.IsWindowAboveZOrderAnchor(anchorHandle))
            {
                this.lastTaskbarLocalZOrderAnchorHandle = anchorHandle;
                this.taskbarLocalZOrderRepairPending = false;
            }
            else
            {
                this.taskbarLocalZOrderRepairPending = true;
            }
        }

        private IntPtr GetTaskbarLocalInsertAfterHandle(IntPtr anchorHandle)
        {
            IntPtr insertAfterHandle = GetWindow(anchorHandle, GwHwndPrev);
            while (insertAfterHandle == this.Handle)
            {
                insertAfterHandle = GetWindow(insertAfterHandle, GwHwndPrev);
            }

            return insertAfterHandle;
        }

        private bool IsWindowAboveZOrderAnchor(IntPtr anchorHandle)
        {
            return this.IsWindowAboveWindow(this.Handle, anchorHandle);
        }

        private bool IsWindowAboveWindow(IntPtr windowHandle, IntPtr anchorHandle)
        {
            if (windowHandle == IntPtr.Zero || anchorHandle == IntPtr.Zero || windowHandle == anchorHandle)
            {
                return false;
            }

            IntPtr currentHandle = GetWindow(windowHandle, GwHwndNext);
            while (currentHandle != IntPtr.Zero)
            {
                if (currentHandle == anchorHandle)
                {
                    return true;
                }

                currentHandle = GetWindow(currentHandle, GwHwndNext);
            }

            return false;
        }
    }
}
