using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private bool TryCaptureTaskbarIntegrationSnapshot(out TaskbarIntegrationSnapshot snapshot)
        {
            snapshot = null;

            Rectangle targetScreenBounds;
            IntPtr taskbarHandle;
            if (!this.TryFindRelevantTaskbarWindow(out taskbarHandle, out targetScreenBounds))
            {
                return false;
            }

            if (taskbarHandle == IntPtr.Zero || !IsWindowVisible(taskbarHandle))
            {
                return false;
            }

            NativeRect windowRect;
            if (!GetWindowRect(taskbarHandle, out windowRect))
            {
                return false;
            }

            Rectangle visibleBounds = windowRect.ToRectangle();
            visibleBounds = Rectangle.Intersect(visibleBounds, targetScreenBounds);
            if (visibleBounds.Width <= 0 || visibleBounds.Height <= 0)
            {
                return false;
            }

            AppBarData appBarData = new AppBarData();
            appBarData.CbSize = Marshal.SizeOf(typeof(AppBarData));
            appBarData.HWnd = taskbarHandle;

            AppBarEdge edge = DetermineTaskbarEdge(visibleBounds, targetScreenBounds);
            if (SHAppBarMessage(AbmGetTaskbarPos, ref appBarData) != 0)
            {
                edge = appBarData.UEdge;
            }

            uint state = SHAppBarMessage(AbmGetState, ref appBarData);
            bool autoHide = (state & AbsAutoHide) == AbsAutoHide;
            int visibleThickness = edge == AppBarEdge.Left || edge == AppBarEdge.Right
                ? visibleBounds.Width
                : visibleBounds.Height;
            bool hidden = visibleThickness < Math.Max(1, DpiHelper.Scale(MinimumVisibleTaskbarThickness, this.currentDpi));

            bool usesCustomTaskListHeuristic =
                HasVisibleDescendantWindowClass(taskbarHandle, "SIBTrayButton") ||
                HasVisibleDescendantWindowClass(taskbarHandle, "MSTaskListWClass");

            Rectangle[] occupiedBounds = GetTaskbarOccupiedLeafBounds(
                taskbarHandle,
                visibleBounds,
                usesCustomTaskListHeuristic);
            occupiedBounds = this.GetAugmentedTaskbarOccupiedBounds(
                visibleBounds,
                edge,
                occupiedBounds);

            snapshot = new TaskbarIntegrationSnapshot
            {
                TaskbarHandle = taskbarHandle,
                TaskbarZOrderAnchorHandle = this.ResolveTaskbarLocalZOrderAnchor(
                    taskbarHandle,
                    visibleBounds,
                    usesCustomTaskListHeuristic),
                Edge = edge,
                Bounds = visibleBounds,
                ScreenBounds = targetScreenBounds,
                AutoHide = autoHide,
                IsHidden = hidden,
                UsesCustomTaskListHeuristic = usesCustomTaskListHeuristic,
                OccupiedBounds = occupiedBounds,
                Theme = this.CreateTaskbarVisualTheme(visibleBounds, occupiedBounds)
            };

            return true;
        }

        private IntPtr ResolveTaskbarLocalZOrderAnchor(
            IntPtr taskbarHandle,
            Rectangle taskbarBounds,
            bool usesCustomTaskListHeuristic)
        {
            if (taskbarHandle == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            IntPtr bestHandle = taskbarHandle;
            EnumWindows(delegate(IntPtr windowHandle, IntPtr lParam)
            {
                if (windowHandle == this.Handle ||
                    windowHandle == IntPtr.Zero ||
                    !IsWindowVisible(windowHandle))
                {
                    return true;
                }

                NativeRect candidateRect;
                if (!GetWindowRect(windowHandle, out candidateRect))
                {
                    return true;
                }

                Rectangle intersectionBounds = Rectangle.Intersect(candidateRect.ToRectangle(), taskbarBounds);
                if (intersectionBounds.Width <= 0 || intersectionBounds.Height <= 0)
                {
                    return true;
                }

                string className = GetWindowClassName(windowHandle);
                if (!IsTaskbarLocalZOrderAnchorWindowClass(className, usesCustomTaskListHeuristic))
                {
                    return true;
                }

                if (bestHandle == IntPtr.Zero || this.IsWindowAboveWindow(windowHandle, bestHandle))
                {
                    bestHandle = windowHandle;
                }

                return true;
            }, IntPtr.Zero);

            return bestHandle;
        }

        private Rectangle[] GetTaskbarOccupiedLeafBounds(IntPtr taskbarHandle, Rectangle taskbarBounds, bool usesCustomTaskListHeuristic)
        {
            List<Rectangle> bounds = new List<Rectangle>();
            EnumChildWindows(taskbarHandle, delegate(IntPtr childHandle, IntPtr lParam)
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

                string className = GetWindowClassName(childHandle);
                if (IsProtectedTaskbarRegionWindowClass(className))
                {
                    bounds.Add(childBounds);
                    return true;
                }

                if (IsCustomTaskListPlaceholderWindow(childHandle, taskbarHandle, className, usesCustomTaskListHeuristic))
                {
                    return true;
                }

                if (HasVisibleIntersectingChildWindow(childHandle, taskbarBounds))
                {
                    return true;
                }

                bounds.Add(childBounds);
                return true;
            }, IntPtr.Zero);

            return bounds.ToArray();
        }
    }
}
