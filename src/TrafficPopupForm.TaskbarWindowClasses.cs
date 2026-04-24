using System;
using System.Drawing;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private static bool IsTaskbarShellWindowClass(string className)
        {
            return string.Equals(className, "Progman", StringComparison.Ordinal) ||
                string.Equals(className, "WorkerW", StringComparison.Ordinal) ||
                IsTaskbarRootWindowClass(className);
        }

        private static bool IsTaskbarLocalZOrderAnchorWindowClass(string className, bool usesCustomTaskListHeuristic)
        {
            if (IsTaskbarRootWindowClass(className) ||
                IsProtectedTaskbarRegionWindowClass(className))
            {
                return true;
            }

            if (string.Equals(className, "MSTaskListWClass", StringComparison.Ordinal) ||
                string.Equals(className, "MSTaskSwWClass", StringComparison.Ordinal))
            {
                return true;
            }

            return usesCustomTaskListHeuristic &&
                string.Equals(className, "ToolbarWindow32", StringComparison.Ordinal);
        }

        private static bool IsTaskbarRootWindowClass(string className)
        {
            return string.Equals(className, "Shell_TrayWnd", StringComparison.Ordinal) ||
                string.Equals(className, "Shell_SecondaryTrayWnd", StringComparison.Ordinal);
        }

        private static bool IsProtectedTaskbarRegionWindowClass(string className)
        {
            return string.Equals(className, "TrayNotifyWnd", StringComparison.Ordinal) ||
                string.Equals(className, "TrayClockWClass", StringComparison.Ordinal) ||
                string.Equals(className, "TrayShowDesktopButtonWClass", StringComparison.Ordinal) ||
                string.Equals(className, "SIBTrayButton", StringComparison.Ordinal) ||
                string.Equals(className, "Start", StringComparison.Ordinal);
        }

        private static bool IsCustomTaskListPlaceholderWindow(
            IntPtr windowHandle,
            IntPtr taskbarHandle,
            string className,
            bool usesCustomTaskListHeuristic)
        {
            if (!usesCustomTaskListHeuristic)
            {
                return false;
            }

            if (string.Equals(className, "MSTaskListWClass", StringComparison.Ordinal) ||
                string.Equals(className, "MSTaskSwWClass", StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.Equals(className, "ToolbarWindow32", StringComparison.Ordinal))
            {
                return false;
            }

            return HasAncestorWindowClass(windowHandle, taskbarHandle, "ReBarWindow32");
        }

        private static bool HasAncestorWindowClass(IntPtr windowHandle, IntPtr stopHandle, string className)
        {
            IntPtr currentHandle = GetParent(windowHandle);
            while (currentHandle != IntPtr.Zero)
            {
                if (currentHandle == stopHandle)
                {
                    return false;
                }

                if (string.Equals(GetWindowClassName(currentHandle), className, StringComparison.Ordinal))
                {
                    return true;
                }

                currentHandle = GetParent(currentHandle);
            }

            return false;
        }

        private static bool HasVisibleIntersectingChildWindow(IntPtr parentHandle, Rectangle taskbarBounds)
        {
            bool hasVisibleChild = false;
            EnumChildWindows(parentHandle, delegate(IntPtr childHandle, IntPtr lParam)
            {
                if (!IsWindowVisible(childHandle))
                {
                    return true;
                }

                NativeRect childRect;
                if (!GetWindowRect(childHandle, out childRect))
                {
                    return true;
                }

                Rectangle childBounds = Rectangle.Intersect(childRect.ToRectangle(), taskbarBounds);
                if (childBounds.Width <= 0 || childBounds.Height <= 0)
                {
                    return true;
                }

                hasVisibleChild = true;
                return false;
            }, IntPtr.Zero);
            return hasVisibleChild;
        }

        private static bool HasVisibleDescendantWindowClass(IntPtr rootHandle, string className)
        {
            bool found = false;
            EnumChildWindows(rootHandle, delegate(IntPtr childHandle, IntPtr lParam)
            {
                if (!IsWindowVisible(childHandle))
                {
                    return true;
                }

                if (string.Equals(GetWindowClassName(childHandle), className, StringComparison.Ordinal))
                {
                    found = true;
                    return false;
                }

                return true;
            }, IntPtr.Zero);

            return found;
        }

        private static string GetWindowClassName(IntPtr handle)
        {
            System.Text.StringBuilder className = new System.Text.StringBuilder(256);
            int length = GetClassName(handle, className, className.Capacity);
            return length > 0 ? className.ToString() : string.Empty;
        }
    }
}
