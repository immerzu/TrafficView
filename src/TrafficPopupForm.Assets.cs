using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private string GetCurrentMeterCenterAssetPath()
        {
            string assetDirectoryPath = this.GetActivePanelAssetDirectoryPath();
            if (string.IsNullOrWhiteSpace(assetDirectoryPath))
            {
                return null;
            }

            string assetPath = Path.Combine(assetDirectoryPath, "TrafficView.center_core.png");
            return File.Exists(assetPath) ? assetPath : null;
        }

        private bool TryDrawPanelBackgroundAsset(Graphics graphics, byte backgroundAlpha)
        {
            Bitmap panelBackgroundAsset = GetPanelBackgroundAssetFromDirectory(
                this.GetActivePanelAssetDirectoryPath(),
                this.ClientSize);
            if (panelBackgroundAsset == null)
            {
                return false;
            }

            GraphicsState state = graphics.Save();

            try
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;
                using (Bitmap adjustedAsset = CreateSelectiveTransparencyBitmap(panelBackgroundAsset, backgroundAlpha))
                {
                    if (adjustedAsset.Width == this.ClientSize.Width &&
                        adjustedAsset.Height == this.ClientSize.Height)
                    {
                        graphics.DrawImage(
                            adjustedAsset,
                            new Rectangle(0, 0, adjustedAsset.Width, adjustedAsset.Height),
                            0,
                            0,
                            adjustedAsset.Width,
                            adjustedAsset.Height,
                            GraphicsUnit.Pixel);
                    }
                    else
                    {
                        graphics.DrawImage(
                            adjustedAsset,
                            new Rectangle(0, 0, this.ClientSize.Width, this.ClientSize.Height),
                            0,
                            0,
                            adjustedAsset.Width,
                            adjustedAsset.Height,
                            GraphicsUnit.Pixel);
                    }
                }

                return true;
            }
            catch (ExternalException ex)
            {
                AppLog.WarnOnce(
                    "panel-background-asset-draw-failed",
                    "The panel background asset could not be drawn. Falling back to procedural panel rendering.",
                    ex);
                return false;
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private static Bitmap GetCachedMeterCenterAsset(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || !File.Exists(assetPath))
            {
                return null;
            }

            lock (MeterCenterAssetSync)
            {
                if (string.Equals(cachedMeterCenterAssetPath, assetPath, StringComparison.OrdinalIgnoreCase) &&
                    cachedMeterCenterAsset != null)
                {
                    return cachedMeterCenterAsset;
                }

                if (cachedMeterCenterAsset != null)
                {
                    cachedMeterCenterAsset.Dispose();
                    cachedMeterCenterAsset = null;
                }

                using (Image meterCenterImage = Image.FromFile(assetPath))
                {
                    cachedMeterCenterAsset = new Bitmap(meterCenterImage);
                }

                cachedMeterCenterAssetPath = assetPath;
                return cachedMeterCenterAsset;
            }
        }

        private static void ReleaseCachedMeterCenterAsset()
        {
            lock (MeterCenterAssetSync)
            {
                if (cachedMeterCenterAsset != null)
                {
                    cachedMeterCenterAsset.Dispose();
                    cachedMeterCenterAsset = null;
                }

                cachedMeterCenterAssetPath = string.Empty;
            }
        }
    }
}
