using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private static Bitmap GetPanelBackgroundAsset(string panelSkinId, Size targetSize)
        {
            PanelSkinDefinition definition = PanelSkinCatalog.GetSkinById(panelSkinId);
            if (definition == null || string.IsNullOrWhiteSpace(definition.DirectoryPath))
            {
                return null;
            }

            return GetPanelBackgroundAssetFromDirectory(definition.DirectoryPath, targetSize);
        }

        private static Bitmap GetPanelBackgroundAssetFromDirectory(string assetDirectoryPath, Size targetSize)
        {
            string normalizedDirectoryPath = string.IsNullOrWhiteSpace(assetDirectoryPath)
                ? AppStorage.BaseDirectory
                : assetDirectoryPath;

            lock (PanelBackgroundAssetSync)
            {
                bool loadAttempted;
                if (!PanelBackgroundAssetLoadAttemptedByDirectory.TryGetValue(normalizedDirectoryPath, out loadAttempted) || !loadAttempted)
                {
                    Dictionary<string, Bitmap> assets = LoadPanelBackgroundAssets(normalizedDirectoryPath);
                    CachedPanelBackgroundAssetsByDirectory[normalizedDirectoryPath] = assets;
                    PanelBackgroundAssetLoadAttemptedByDirectory[normalizedDirectoryPath] = true;
                }

                Dictionary<string, Bitmap> cachedAssets;
                if (!CachedPanelBackgroundAssetsByDirectory.TryGetValue(normalizedDirectoryPath, out cachedAssets) ||
                    cachedAssets == null ||
                    cachedAssets.Count == 0)
                {
                    return null;
                }

                return SelectBestPanelBackgroundAsset(cachedAssets, targetSize);
            }
        }

        internal static void ReleasePanelBackgroundAssetCache(string assetDirectoryPath)
        {
            string normalizedDirectoryPath = string.IsNullOrWhiteSpace(assetDirectoryPath)
                ? AppStorage.BaseDirectory
                : assetDirectoryPath;

            lock (PanelBackgroundAssetSync)
            {
                Dictionary<string, Bitmap> cachedAssets;
                if (CachedPanelBackgroundAssetsByDirectory.TryGetValue(normalizedDirectoryPath, out cachedAssets) &&
                    cachedAssets != null)
                {
                    foreach (KeyValuePair<string, Bitmap> asset in cachedAssets)
                    {
                        if (asset.Value != null)
                        {
                            asset.Value.Dispose();
                        }
                    }
                }

                CachedPanelBackgroundAssetsByDirectory.Remove(normalizedDirectoryPath);
                PanelBackgroundAssetLoadAttemptedByDirectory.Remove(normalizedDirectoryPath);
            }
        }

        private static Dictionary<string, Bitmap> LoadPanelBackgroundAssets(string assetDirectoryPath)
        {
            Dictionary<string, Bitmap> loadedAssets = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
            string[] assetPaths = GetPanelBackgroundAssetPaths(assetDirectoryPath);

            for (int i = 0; i < assetPaths.Length; i++)
            {
                string assetPath = assetPaths[i];
                if (!File.Exists(assetPath))
                {
                    continue;
                }

                try
                {
                    using (Image image = Image.FromFile(assetPath))
                    {
                        loadedAssets[assetPath] = new Bitmap(image);
                    }
                }
                catch (Exception ex)
                {
                    AppLog.WarnOnce(
                        "panel-background-asset-load-failed-" + assetPath,
                        string.Format(
                            "The panel background asset could not be loaded from '{0}'. Procedural panel rendering will be used for missing sizes.",
                            assetPath),
                        ex);
                }
            }

            if (loadedAssets.Count == 0)
            {
                AppLog.WarnOnce(
                    "panel-background-asset-missing-" + assetDirectoryPath,
                    string.Format(
                        "No panel background assets were found in '{0}'. Procedural panel rendering will be used.",
                        assetDirectoryPath));
            }

            return loadedAssets;
        }

        private static string[] GetPanelBackgroundAssetPaths(string assetDirectoryPath)
        {
            string normalizedDirectoryPath = string.IsNullOrWhiteSpace(assetDirectoryPath)
                ? AppStorage.BaseDirectory
                : assetDirectoryPath;
            List<string> assetPaths = new List<string>();

            for (int i = 0; i < PanelBackgroundPreparedScalePercents.Length; i++)
            {
                int scalePercent = PanelBackgroundPreparedScalePercents[i];
                string fileName = scalePercent == 100
                    ? PanelBackgroundAssetFileName
                    : string.Format(PanelBackgroundScaledAssetFileNameFormat, scalePercent);
                assetPaths.Add(Path.Combine(normalizedDirectoryPath, fileName));
            }

            return assetPaths.ToArray();
        }

        private static Bitmap SelectBestPanelBackgroundAsset(Dictionary<string, Bitmap> assets, Size targetSize)
        {
            if (assets == null || assets.Count == 0)
            {
                return null;
            }

            Bitmap bestAsset = null;
            long bestScore = long.MaxValue;

            foreach (KeyValuePair<string, Bitmap> pair in assets)
            {
                Bitmap candidate = pair.Value;
                long score = GetPanelBackgroundAssetMatchScore(candidate.Size, targetSize);
                if (score < bestScore)
                {
                    bestAsset = candidate;
                    bestScore = score;
                }
            }

            return bestAsset;
        }

        private static long GetPanelBackgroundAssetMatchScore(Size assetSize, Size targetSize)
        {
            long widthDelta = Math.Abs(assetSize.Width - targetSize.Width);
            long heightDelta = Math.Abs(assetSize.Height - targetSize.Height);
            long areaDelta = Math.Abs((assetSize.Width * assetSize.Height) - (targetSize.Width * targetSize.Height));
            return (widthDelta * widthDelta) + (heightDelta * heightDelta) + areaDelta;
        }
    }
}
