using System;
using System.Drawing;

namespace TrafficView
{
    internal sealed class PanelSkinDefinition
    {
        public PanelSkinDefinition(
            string id,
            string displayNameKey,
            string displayNameFallback,
            string surfaceEffect,
            string directoryPath,
            Size? clientSize,
            Rectangle? downloadCaptionBounds,
            Rectangle? downloadValueBounds,
            Rectangle? uploadCaptionBounds,
            Rectangle? uploadValueBounds,
            Rectangle? meterBounds,
            Rectangle? sparklineBounds,
            bool drawDynamicRing,
            bool drawCenterArrows,
            bool drawSparkline,
            bool drawMeterValueSupport)
        {
            this.Id = string.IsNullOrWhiteSpace(id) ? "08" : id.Trim();
            this.DisplayNameKey = string.IsNullOrWhiteSpace(displayNameKey) ? string.Empty : displayNameKey.Trim();
            this.DisplayNameFallback = string.IsNullOrWhiteSpace(displayNameFallback) ? this.Id : displayNameFallback.Trim();
            this.SurfaceEffect = string.IsNullOrWhiteSpace(surfaceEffect) ? "none" : surfaceEffect.Trim();
            this.DirectoryPath = string.IsNullOrWhiteSpace(directoryPath) ? string.Empty : directoryPath.Trim();
            this.ClientSize = clientSize;
            this.DownloadCaptionBounds = downloadCaptionBounds;
            this.DownloadValueBounds = downloadValueBounds;
            this.UploadCaptionBounds = uploadCaptionBounds;
            this.UploadValueBounds = uploadValueBounds;
            this.MeterBounds = meterBounds;
            this.SparklineBounds = sparklineBounds;
            this.DrawDynamicRing = drawDynamicRing;
            this.DrawCenterArrows = drawCenterArrows;
            this.DrawSparkline = drawSparkline;
            this.DrawMeterValueSupport = drawMeterValueSupport;
        }

        public string Id { get; private set; }

        public string DisplayNameKey { get; private set; }

        public string DisplayNameFallback { get; private set; }

        public string SurfaceEffect { get; private set; }

        public string DirectoryPath { get; private set; }

        public Size? ClientSize { get; private set; }

        public Rectangle? DownloadCaptionBounds { get; private set; }

        public Rectangle? DownloadValueBounds { get; private set; }

        public Rectangle? UploadCaptionBounds { get; private set; }

        public Rectangle? UploadValueBounds { get; private set; }

        public Rectangle? MeterBounds { get; private set; }

        public Rectangle? SparklineBounds { get; private set; }

        public bool DrawDynamicRing { get; private set; }

        public bool DrawCenterArrows { get; private set; }

        public bool DrawSparkline { get; private set; }

        public bool DrawMeterValueSupport { get; private set; }

        public bool HasGlassSurfaceEffect
        {
            get
            {
                return string.Equals(this.SurfaceEffect, "glass", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(this.SurfaceEffect, "glass-readable", StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool HasReadableInfoPlateEffect
        {
            get
            {
                return string.Equals(this.SurfaceEffect, "glass-readable", StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
