using System;
using System.Drawing;
using System.Windows.Forms;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private bool TryFindRelevantTaskbarWindow(out IntPtr taskbarHandle, out Rectangle targetScreenBounds)
        {
            taskbarHandle = IntPtr.Zero;
            targetScreenBounds = this.GetTaskbarIntegrationTargetScreenBounds();
            IntPtr bestTaskbarHandle = IntPtr.Zero;
            Rectangle searchScreenBounds = targetScreenBounds;
            int bestIntersectionArea = 0;

            EnumWindows(delegate(IntPtr windowHandle, IntPtr lParam)
            {
                if (!IsWindowVisible(windowHandle))
                {
                    return true;
                }

                string className = GetWindowClassName(windowHandle);
                if (!IsTaskbarRootWindowClass(className))
                {
                    return true;
                }

                NativeRect windowRect;
                if (!GetWindowRect(windowHandle, out windowRect))
                {
                    return true;
                }

                Rectangle taskbarBounds = Rectangle.Intersect(windowRect.ToRectangle(), searchScreenBounds);
                int intersectionArea = Math.Max(0, taskbarBounds.Width) * Math.Max(0, taskbarBounds.Height);
                if (intersectionArea <= 0)
                {
                    return true;
                }

                if (intersectionArea > bestIntersectionArea ||
                    (intersectionArea == bestIntersectionArea &&
                    string.Equals(className, "Shell_TrayWnd", StringComparison.Ordinal)))
                {
                    bestTaskbarHandle = windowHandle;
                    bestIntersectionArea = intersectionArea;
                }

                return true;
            }, IntPtr.Zero);

            if (bestTaskbarHandle != IntPtr.Zero)
            {
                taskbarHandle = bestTaskbarHandle;
                return true;
            }

            taskbarHandle = FindWindow("Shell_TrayWnd", null);
            return taskbarHandle != IntPtr.Zero;
        }

        private Rectangle GetTaskbarIntegrationTargetScreenBounds()
        {
            if (this.Visible)
            {
                return Screen.FromPoint(GetRectangleCenter(this.GetCurrentPopupScreenBounds())).Bounds;
            }

            if (this.lastSuccessfulTaskbarPlacementBounds.Width > 0 &&
                this.lastSuccessfulTaskbarPlacementBounds.Height > 0)
            {
                return Screen.FromPoint(GetRectangleCenter(this.lastSuccessfulTaskbarPlacementBounds)).Bounds;
            }

            if (this.taskbarIntegrationPreferredLocation.HasValue)
            {
                return Screen.FromPoint(this.taskbarIntegrationPreferredLocation.Value).Bounds;
            }

            return Screen.FromPoint(Cursor.Position).Bounds;
        }

        private static AppBarEdge DetermineTaskbarEdge(Rectangle bounds, Rectangle screenBounds)
        {
            if (bounds.Left <= screenBounds.Left && bounds.Width <= Math.Max(1, screenBounds.Width / 5))
            {
                return AppBarEdge.Left;
            }

            if (bounds.Right >= screenBounds.Right && bounds.Width <= Math.Max(1, screenBounds.Width / 5))
            {
                return AppBarEdge.Right;
            }

            if (bounds.Top <= screenBounds.Top)
            {
                return AppBarEdge.Top;
            }

            return AppBarEdge.Bottom;
        }
    }
}
