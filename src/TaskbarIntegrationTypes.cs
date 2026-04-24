using System;
using System.Drawing;

namespace TrafficView
{
    internal struct TaskbarVisualTheme
    {
        public Color TaskbarColor;
        public Color BaseColor;
        public Color BorderColor;
        public Color DividerColor;
        public byte OverlayAlpha;
    }

    internal sealed class TaskbarIntegrationSnapshot
    {
        public IntPtr TaskbarHandle { get; set; }

        public IntPtr TaskbarZOrderAnchorHandle { get; set; }

        public AppBarEdge Edge { get; set; }

        public Rectangle Bounds { get; set; }

        public Rectangle ScreenBounds { get; set; }

        public Rectangle[] OccupiedBounds { get; set; }

        public bool AutoHide { get; set; }

        public bool IsHidden { get; set; }

        public bool UsesCustomTaskListHeuristic { get; set; }

        public TaskbarVisualTheme Theme { get; set; }

        public bool IsVertical
        {
            get { return this.Edge == AppBarEdge.Left || this.Edge == AppBarEdge.Right; }
        }
    }
}
