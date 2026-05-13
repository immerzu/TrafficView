using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace TrafficView
{
    internal static class PanelSkinCatalog
    {
        public const string DefaultSkinId = "08";
        private const string SkinSettingsFileName = "skin.ini";
        private const string DeleteStagingDirectoryName = ".delete";
        private static readonly Size DefaultClientSize = new Size(102, 56);
        private static readonly object SyncRoot = new object();
        private static readonly string[] SupportedSurfaceEffects = new string[]
        {
            "none",
            "glass",
            "glass-readable"
        };
        private static PanelSkinDefinition[] cachedDefinitions;

        public static string GetSkinsDirectoryPath()
        {
            return AppStorage.GetSkinsDirectoryPath();
        }

        public static PanelSkinDefinition[] GetAvailableSkins()
        {
            lock (SyncRoot)
            {
                if (cachedDefinitions == null)
                {
                    cachedDefinitions = LoadDefinitions();
                }

                PanelSkinDefinition[] clone = new PanelSkinDefinition[cachedDefinitions.Length];
                Array.Copy(cachedDefinitions, clone, cachedDefinitions.Length);
                return clone;
            }
        }

        public static void Reload()
        {
            lock (SyncRoot)
            {
                cachedDefinitions = null;
            }
        }

        public static string[] GetSupportedSkinIds()
        {
            PanelSkinDefinition[] definitions = GetAvailableSkins();
            string[] ids = new string[definitions.Length];

            for (int i = 0; i < definitions.Length; i++)
            {
                ids[i] = definitions[i].Id;
            }

            return ids;
        }

        public static PanelSkinDefinition GetSkinById(string panelSkinId)
        {
            PanelSkinDefinition[] definitions = GetAvailableSkins();
            string normalizedId = NormalizeSkinId(panelSkinId);

            for (int i = 0; i < definitions.Length; i++)
            {
                if (string.Equals(definitions[i].Id, normalizedId, StringComparison.OrdinalIgnoreCase))
                {
                    return definitions[i];
                }
            }

            return definitions.Length > 0 ? definitions[0] : null;
        }

        public static string NormalizeSkinId(string panelSkinId)
        {
            string candidate = (panelSkinId ?? string.Empty).Trim();
            PanelSkinDefinition[] definitions = GetAvailableSkins();

            if (string.IsNullOrEmpty(candidate))
            {
                return GetDefaultOrFirstSkinId(definitions);
            }

            for (int i = 0; i < definitions.Length; i++)
            {
                if (string.Equals(definitions[i].Id, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return definitions[i].Id;
                }
            }

            return GetDefaultOrFirstSkinId(definitions);
        }

        public static string GetDefaultOrFirstSkinId()
        {
            return GetDefaultOrFirstSkinId(GetAvailableSkins());
        }

        private static string GetDefaultOrFirstSkinId(PanelSkinDefinition[] definitions)
        {
            for (int i = 0; i < definitions.Length; i++)
            {
                if (string.Equals(definitions[i].Id, DefaultSkinId, StringComparison.OrdinalIgnoreCase))
                {
                    return definitions[i].Id;
                }
            }

            return definitions.Length > 0 ? definitions[0].Id : DefaultSkinId;
        }

        public static bool IsProtectedSkinId(string panelSkinId)
        {
            return string.Equals(
                NormalizeSkinId(panelSkinId),
                GetDefaultOrFirstSkinId(),
                StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryDeleteSkin(string panelSkinId, out string errorMessage)
        {
            errorMessage = string.Empty;
            string normalizedId = NormalizeSkinId(panelSkinId);
            PanelSkinDefinition definition = GetSkinById(normalizedId);

            if (definition == null || string.IsNullOrWhiteSpace(definition.DirectoryPath))
            {
                errorMessage = "Der ausgewaehlte Skin konnte nicht gefunden werden.";
                return false;
            }

            PanelSkinDefinition[] definitions = GetAvailableSkins();
            if (definitions.Length <= 1)
            {
                errorMessage = "Mindestens ein Skin muss erhalten bleiben.";
                return false;
            }

            if (IsProtectedSkinId(normalizedId))
            {
                errorMessage = "Der Standardskin kann nicht geloescht werden.";
                return false;
            }

            try
            {
                List<SkinDeleteTarget> deleteTargets = GetDeleteTargets(normalizedId, definition);
                if (deleteTargets == null)
                {
                    errorMessage = "Der Skin-Pfad konnte nicht aufgeloest werden.";
                    return false;
                }

                if (deleteTargets.Count == 0)
                {
                    errorMessage = "Der Skin-Ordner ist nicht mehr vorhanden.";
                    return false;
                }

                List<SkinDeleteTarget> movedTargets = new List<SkinDeleteTarget>();

                try
                {
                    for (int i = 0; i < deleteTargets.Count; i++)
                    {
                        SkinDeleteTarget target = deleteTargets[i];
                        Directory.CreateDirectory(target.DeleteStagingDirectoryPath);
                        Directory.Move(target.FullSkinDirectoryPath, target.StagedSkinDirectoryPath);
                        movedTargets.Add(target);
                    }

                    for (int i = 0; i < movedTargets.Count; i++)
                    {
                        Directory.Delete(movedTargets[i].StagedSkinDirectoryPath, true);
                    }
                }
                catch
                {
                    for (int i = movedTargets.Count - 1; i >= 0; i--)
                    {
                        SkinDeleteTarget target = movedTargets[i];

                        try
                        {
                            if (!Directory.Exists(target.FullSkinDirectoryPath) &&
                                Directory.Exists(target.StagedSkinDirectoryPath))
                            {
                                Directory.Move(target.StagedSkinDirectoryPath, target.FullSkinDirectoryPath);
                            }
                        }
                        catch (Exception rollbackEx)
                        {
                            AppLog.WarnOnce(
                                "skin-delete-rollback-failed-" + normalizedId + "-" + i.ToString(),
                                string.Format("Rollback fuer Skin '{0}' ist fehlgeschlagen.", normalizedId),
                                rollbackEx);
                        }
                    }

                    throw;
                }

                Reload();
                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    Reload();
                }
                catch (Exception reloadEx)
                {
                    AppLog.WarnOnce(
                        "skin-delete-reload-failed-" + normalizedId,
                        string.Format("Skin-Katalog konnte nach fehlgeschlagenem Loeschen von Skin '{0}' nicht neu geladen werden.", normalizedId),
                        reloadEx);
                }

                AppLog.WarnOnce(
                    "skin-delete-failed-" + normalizedId,
                    string.Format("Skin '{0}' konnte nicht geloescht werden.", normalizedId),
                    ex);
                errorMessage = "Der Skin konnte nicht vollstaendig geloescht werden.";
                return false;
            }
        }


        public static bool TryValidateSkinDirectory(string skinDirectoryPath, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(skinDirectoryPath))
            {
                errorMessage = "Der Skin-Ordner ist leer oder ungueltig.";
                return false;
            }

            try
            {
                SkinPathPolicy.EnsurePortableSkinPathAllowed(skinDirectoryPath, "Skin-Verzeichnis");
            }
            catch (InvalidOperationException ex)
            {
                errorMessage = ex.Message;
                return false;
            }

            if (!Directory.Exists(skinDirectoryPath))
            {
                errorMessage = "Der Skin-Ordner wurde nicht gefunden.";
                return false;
            }

            string settingsPath = Path.Combine(skinDirectoryPath, SkinSettingsFileName);
            if (!File.Exists(settingsPath))
            {
                errorMessage = "Die skin.ini fehlt.";
                return false;
            }

            Size? clientSizeOverride;
            string clientSizeErrorMessage;
            if (!TryReadClientSizeOverride(settingsPath, out clientSizeOverride, out clientSizeErrorMessage))
            {
                errorMessage = clientSizeErrorMessage;
                return false;
            }

            KeyValuePair<string, Size>[] expectedAssetDefinitions = GetExpectedAssetDefinitions(clientSizeOverride);

            for (int i = 0; i < expectedAssetDefinitions.Length; i++)
            {
                string fileName = expectedAssetDefinitions[i].Key;
                Size expectedSize = expectedAssetDefinitions[i].Value;
                string assetPath = Path.Combine(skinDirectoryPath, fileName);

                if (!File.Exists(assetPath))
                {
                    errorMessage = string.Format("Die Skin-Datei '{0}' fehlt.", fileName);
                    return false;
                }

                const long MaxPngFileBytes = 2L * 1024L * 1024L;
                const int MaxBitmapDimension = 4096;

                FileInfo pngFileInfo = new FileInfo(assetPath);
                if (pngFileInfo.Length > MaxPngFileBytes)
                {
                    errorMessage = string.Format(
                        "Die Skin-Datei '{0}' ist zu gross ({1:N0} Bytes, Maximum {2:N0} Bytes).",
                        fileName,
                        pngFileInfo.Length,
                        MaxPngFileBytes);
                    return false;
                }

                try
                {
                    using (Bitmap bitmap = new Bitmap(assetPath))
                    {
                        if (bitmap.Width > MaxBitmapDimension || bitmap.Height > MaxBitmapDimension)
                        {
                            errorMessage = string.Format(
                                "Die Skin-Datei '{0}' hat zu grosse Abmessungen ({1}x{2}, Maximum {3}x{3}).",
                                fileName,
                                bitmap.Width,
                                bitmap.Height,
                                MaxBitmapDimension);
                            return false;
                        }

                        if (bitmap.Width != expectedSize.Width || bitmap.Height != expectedSize.Height)
                        {
                            errorMessage = string.Format(
                                "Die Skin-Datei '{0}' hat die falsche Groesse ({1}x{2} statt {3}x{4}).",
                                fileName,
                                bitmap.Width,
                                bitmap.Height,
                                expectedSize.Width,
                                expectedSize.Height);
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppLog.WarnOnce(
                        "skin-asset-validate-failed-" + assetPath,
                        string.Format("Skin-Datei '{0}' konnte nicht gelesen werden.", assetPath),
                        ex);
                    errorMessage = string.Format("Die Skin-Datei '{0}' ist beschaedigt oder nicht lesbar.", fileName);
                    return false;
                }
            }

            return true;
        }

        private static KeyValuePair<string, Size>[] GetExpectedAssetDefinitions(Size? clientSizeOverride)
        {
            Size baseClientSize = clientSizeOverride ?? DefaultClientSize;
            return new KeyValuePair<string, Size>[]
            {
                new KeyValuePair<string, Size>("TrafficView.panel.90.png", ScaleAssetSize(baseClientSize, 90)),
                new KeyValuePair<string, Size>("TrafficView.panel.png", baseClientSize),
                new KeyValuePair<string, Size>("TrafficView.panel.110.png", ScaleAssetSize(baseClientSize, 110)),
                new KeyValuePair<string, Size>("TrafficView.panel.125.png", ScaleAssetSize(baseClientSize, 125)),
                new KeyValuePair<string, Size>("TrafficView.panel.150.png", ScaleAssetSize(baseClientSize, 150))
            };
        }

        private static Size ScaleAssetSize(Size baseClientSize, int percentage)
        {
            return new Size(
                ScaleDimension(baseClientSize.Width, percentage),
                ScaleDimension(baseClientSize.Height, percentage));
        }

        private static int ScaleDimension(int value, int percentage)
        {
            return (int)Math.Floor(((value * percentage) / 100.0) + 0.5);
        }

        private static bool TryReadClientSizeOverride(string settingsPath, out Size? clientSize, out string errorMessage)
        {
            clientSize = null;
            errorMessage = string.Empty;

            const long MaxSkinIniBytes = 64L * 1024L;

            try
            {
                FileInfo iniFileInfo = new FileInfo(settingsPath);
                if (iniFileInfo.Length > MaxSkinIniBytes)
                {
                    errorMessage = string.Format(
                        "Die skin.ini ist zu gross ({0:N0} Bytes, Maximum {1:N0} Bytes).",
                        iniFileInfo.Length,
                        MaxSkinIniBytes);
                    return false;
                }

                string[] lines = File.ReadAllLines(settingsPath);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    string trimmedLine = line.Trim();
                    if (trimmedLine.StartsWith("#", StringComparison.Ordinal) ||
                        trimmedLine.StartsWith(";", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    int equalsIndex = trimmedLine.IndexOf('=');
                    if (equalsIndex <= 0)
                    {
                        continue;
                    }

                    string key = trimmedLine.Substring(0, equalsIndex).Trim();
                    string value = trimmedLine.Substring(equalsIndex + 1).Trim();
                    if (!string.Equals(key, "ClientSize", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!TryParseSize(value, out clientSize))
                    {
                        errorMessage = "ClientSize ist ungueltig.";
                        return false;
                    }

                    return true;
                }

                return true;
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce(
                    "skin-clientsize-read-failed-" + settingsPath,
                    string.Format("ClientSize konnte nicht aus '{0}' gelesen werden.", settingsPath),
                    ex);
                errorMessage = "skin.ini konnte nicht gelesen werden.";
                return false;
            }
        }

        private static PanelSkinDefinition[] LoadDefinitions()
        {
            string skinsDirectoryPath = GetSkinsDirectoryPath();
            List<PanelSkinDefinition> definitions = new List<PanelSkinDefinition>();

            SkinPathPolicy.EnsurePortableSkinPathAllowed(skinsDirectoryPath, "Skin-Basisverzeichnis");

            if (Directory.Exists(skinsDirectoryPath))
            {
                CleanupDeleteStagingDirectory(skinsDirectoryPath);
                string[] skinDirectoryPaths = Directory.GetDirectories(skinsDirectoryPath);
                Array.Sort(skinDirectoryPaths, StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < skinDirectoryPaths.Length; i++)
                {
                    if (ShouldIgnoreSkinDirectory(skinDirectoryPaths[i]))
                    {
                        continue;
                    }

                    PanelSkinDefinition definition = TryLoadDefinition(skinDirectoryPaths[i]);
                    if (definition == null)
                    {
                        continue;
                    }

                    bool alreadyAdded = false;
                    for (int j = 0; j < definitions.Count; j++)
                    {
                        if (string.Equals(definitions[j].Id, definition.Id, StringComparison.OrdinalIgnoreCase))
                        {
                            AppLog.WarnOnce(
                                "skin-definition-duplicate-id-" + definition.Id,
                                string.Format(
                                    "Skin-Ordner '{0}' wurde uebersprungen, weil die Skin-ID '{1}' bereits durch '{2}' belegt ist.",
                                    skinDirectoryPaths[i],
                                    definition.Id,
                                    definitions[j].DirectoryPath));
                            alreadyAdded = true;
                            break;
                        }
                    }

                    if (!alreadyAdded)
                    {
                        definitions.Add(definition);
                    }
                }
            }
            definitions.Sort(
                delegate(PanelSkinDefinition left, PanelSkinDefinition right)
                {
                    return string.Compare(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);
                });

            return definitions.ToArray();
        }

        private static bool ShouldIgnoreSkinDirectory(string skinDirectoryPath)
        {
            if (string.IsNullOrWhiteSpace(skinDirectoryPath))
            {
                return true;
            }

            string directoryName = Path.GetFileName(
                skinDirectoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return string.Equals(
                directoryName,
                DeleteStagingDirectoryName,
                StringComparison.OrdinalIgnoreCase);
        }

        private static void CleanupDeleteStagingDirectory(string skinsDirectoryPath)
        {
            if (string.IsNullOrWhiteSpace(skinsDirectoryPath))
            {
                return;
            }

            SkinPathPolicy.EnsurePortableSkinPathAllowed(skinsDirectoryPath, "Skin-Verzeichnis");

            string deleteStagingDirectoryPath = Path.Combine(skinsDirectoryPath, DeleteStagingDirectoryName);
            if (!Directory.Exists(deleteStagingDirectoryPath))
            {
                return;
            }

            string[] stagedDirectoryPaths;
            try
            {
                stagedDirectoryPaths = Directory.GetDirectories(deleteStagingDirectoryPath);
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce(
                    "skin-delete-staging-enumeration-failed",
                    string.Format(
                        "Temporäre Skin-Löschreste unter '{0}' konnten nicht geprüft werden.",
                        deleteStagingDirectoryPath),
                    ex);
                return;
            }

            for (int i = 0; i < stagedDirectoryPaths.Length; i++)
            {
                string stagedDirectoryPath = stagedDirectoryPaths[i];

                try
                {
                    SkinPathPolicy.EnsurePortableSkinPathAllowed(stagedDirectoryPath, "temporärer Skin-Löschrest");

                    if (Directory.Exists(stagedDirectoryPath))
                    {
                        Directory.Delete(stagedDirectoryPath, true);
                    }
                }
                catch (Exception ex)
                {
                    AppLog.WarnOnce(
                        "skin-delete-staging-cleanup-failed-" + stagedDirectoryPath,
                        string.Format(
                            "Ein temporärer Skin-Löschrest unter '{0}' konnte nicht entfernt werden.",
                            stagedDirectoryPath),
                        ex);
                }
            }

            try
            {
                if (Directory.Exists(deleteStagingDirectoryPath) &&
                    Directory.GetFileSystemEntries(deleteStagingDirectoryPath).Length == 0)
                {
                    Directory.Delete(deleteStagingDirectoryPath, false);
                }
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce(
                    "skin-delete-staging-root-cleanup-failed",
                    string.Format(
                        "Das temporäre Skin-Löschverzeichnis '{0}' konnte nicht bereinigt werden.",
                        deleteStagingDirectoryPath),
                    ex);
            }
        }

        private static PanelSkinDefinition TryLoadDefinition(string skinDirectoryPath)
        {
            if (string.IsNullOrWhiteSpace(skinDirectoryPath))
            {
                return null;
            }

            SkinPathPolicy.EnsurePortableSkinPathAllowed(skinDirectoryPath, "Skin-Verzeichnis");

            string validationError;
            if (!TryValidateSkinDirectory(skinDirectoryPath, out validationError))
            {
                AppLog.WarnOnce(
                    "skin-definition-validation-failed-" + skinDirectoryPath,
                    string.Format(
                        "Skin-Ordner '{0}' wurde uebersprungen: {1}",
                        skinDirectoryPath,
                        string.IsNullOrWhiteSpace(validationError) ? "ungueltige Skin-Dateien" : validationError));
                return null;
            }

            string settingsPath = Path.Combine(skinDirectoryPath, SkinSettingsFileName);
            string id;
            string displayNameKey;
            string displayNameFallback;
            string surfaceEffect;
            Size? clientSize;
            Rectangle? downloadCaptionBounds;
            Rectangle? downloadValueBounds;
            Rectangle? uploadCaptionBounds;
            Rectangle? uploadValueBounds;
            Rectangle? meterBounds;
            Rectangle? sparklineBounds;
            bool drawDynamicRing;
            bool drawCenterArrows;
            bool drawSparkline;
            bool drawMeterValueSupport;
            string parseErrorMessage;
            if (!TryParseSkinDefinition(
                skinDirectoryPath,
                settingsPath,
                out id,
                out displayNameKey,
                out displayNameFallback,
                out surfaceEffect,
                out clientSize,
                out downloadCaptionBounds,
                out downloadValueBounds,
                out uploadCaptionBounds,
                out uploadValueBounds,
                out meterBounds,
                out sparklineBounds,
                out drawDynamicRing,
                out drawCenterArrows,
                out drawSparkline,
                out drawMeterValueSupport,
                out parseErrorMessage))
            {
                AppLog.WarnOnce(
                    "skin-definition-load-failed-" + skinDirectoryPath,
                    string.Format(
                        "Skin-Definition konnte nicht aus '{0}' geladen werden: {1}",
                        settingsPath,
                        string.IsNullOrWhiteSpace(parseErrorMessage) ? "ungueltige skin.ini" : parseErrorMessage));
                return null;
            }

            return new PanelSkinDefinition(
                id,
                displayNameKey,
                displayNameFallback,
                surfaceEffect,
                skinDirectoryPath,
                clientSize,
                downloadCaptionBounds,
                downloadValueBounds,
                uploadCaptionBounds,
                uploadValueBounds,
                meterBounds,
                sparklineBounds,
                drawDynamicRing,
                drawCenterArrows,
                drawSparkline,
                drawMeterValueSupport);
        }

        private static bool TryParseSkinDefinition(
            string skinDirectoryPath,
            string settingsPath,
            out string id,
            out string displayNameKey,
            out string displayNameFallback,
            out string surfaceEffect,
            out Size? clientSize,
            out Rectangle? downloadCaptionBounds,
            out Rectangle? downloadValueBounds,
            out Rectangle? uploadCaptionBounds,
            out Rectangle? uploadValueBounds,
            out Rectangle? meterBounds,
            out Rectangle? sparklineBounds,
            out bool drawDynamicRing,
            out bool drawCenterArrows,
            out bool drawSparkline,
            out bool drawMeterValueSupport,
            out string errorMessage)
        {
            string directoryName = Path.GetFileName(
                skinDirectoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            id = directoryName;
            displayNameKey = string.Empty;
            displayNameFallback = directoryName;
            surfaceEffect = "none";
            clientSize = null;
            downloadCaptionBounds = null;
            downloadValueBounds = null;
            uploadCaptionBounds = null;
            uploadValueBounds = null;
            meterBounds = null;
            sparklineBounds = null;
            drawDynamicRing = true;
            drawCenterArrows = true;
            drawSparkline = true;
            drawMeterValueSupport = true;
            errorMessage = string.Empty;

            const long MaxSkinIniBytes = 64L * 1024L;

            try
            {
                FileInfo iniFileInfo = new FileInfo(settingsPath);
                if (iniFileInfo.Length > MaxSkinIniBytes)
                {
                    errorMessage = string.Format(
                        "Die skin.ini ist zu gross ({0:N0} Bytes, Maximum {1:N0} Bytes).",
                        iniFileInfo.Length,
                        MaxSkinIniBytes);
                    return false;
                }

                string[] lines = File.ReadAllLines(settingsPath);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    string trimmedLine = line.Trim();
                    if (trimmedLine.StartsWith("#", StringComparison.Ordinal) ||
                        trimmedLine.StartsWith(";", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    int equalsIndex = trimmedLine.IndexOf('=');
                    if (equalsIndex <= 0)
                    {
                        continue;
                    }

                    string key = trimmedLine.Substring(0, equalsIndex).Trim();
                    string value = trimmedLine.Substring(equalsIndex + 1).Trim();

                    if (string.Equals(key, "Id", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            id = value.Trim();
                        }

                        continue;
                    }

                    if (string.Equals(key, "DisplayNameKey", StringComparison.OrdinalIgnoreCase))
                    {
                        displayNameKey = value;
                        continue;
                    }

                    if (string.Equals(key, "DisplayNameFallback", StringComparison.OrdinalIgnoreCase))
                    {
                        displayNameFallback = value;
                        continue;
                    }

                    if (string.Equals(key, "SurfaceEffect", StringComparison.OrdinalIgnoreCase))
                    {
                        surfaceEffect = value;
                        continue;
                    }

                    if (string.Equals(key, "ClientSize", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryParseSize(value, out clientSize))
                        {
                            errorMessage = "ClientSize ist ungueltig.";
                            return false;
                        }

                        continue;
                    }

                    if (string.Equals(key, "DownloadCaptionBounds", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryParseRectangle(value, out downloadCaptionBounds))
                        {
                            errorMessage = "DownloadCaptionBounds ist ungueltig.";
                            return false;
                        }

                        continue;
                    }

                    if (string.Equals(key, "DownloadValueBounds", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryParseRectangle(value, out downloadValueBounds))
                        {
                            errorMessage = "DownloadValueBounds ist ungueltig.";
                            return false;
                        }

                        continue;
                    }

                    if (string.Equals(key, "UploadCaptionBounds", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryParseRectangle(value, out uploadCaptionBounds))
                        {
                            errorMessage = "UploadCaptionBounds ist ungueltig.";
                            return false;
                        }

                        continue;
                    }

                    if (string.Equals(key, "UploadValueBounds", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryParseRectangle(value, out uploadValueBounds))
                        {
                            errorMessage = "UploadValueBounds ist ungueltig.";
                            return false;
                        }

                        continue;
                    }

                    if (string.Equals(key, "MeterBounds", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryParseRectangle(value, out meterBounds))
                        {
                            errorMessage = "MeterBounds ist ungueltig.";
                            return false;
                        }

                        continue;
                    }

                    if (string.Equals(key, "SparklineBounds", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryParseRectangle(value, out sparklineBounds))
                        {
                            errorMessage = "SparklineBounds ist ungueltig.";
                            return false;
                        }

                        continue;
                    }

                    if (string.Equals(key, "DrawDynamicRing", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryParseBoolean(value, out drawDynamicRing))
                        {
                            errorMessage = "DrawDynamicRing ist ungueltig.";
                            return false;
                        }

                        continue;
                    }

                    if (string.Equals(key, "DrawCenterArrows", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryParseBoolean(value, out drawCenterArrows))
                        {
                            errorMessage = "DrawCenterArrows ist ungueltig.";
                            return false;
                        }

                        continue;
                    }

                    if (string.Equals(key, "DrawSparkline", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryParseBoolean(value, out drawSparkline))
                        {
                            errorMessage = "DrawSparkline ist ungueltig.";
                            return false;
                        }

                        continue;
                    }

                    if (string.Equals(key, "DrawMeterValueSupport", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryParseBoolean(value, out drawMeterValueSupport))
                        {
                            errorMessage = "DrawMeterValueSupport ist ungueltig.";
                            return false;
                        }

                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce(
                    "skin-definition-read-failed-" + skinDirectoryPath,
                    string.Format("Skin-Definition konnte nicht aus '{0}' gelesen werden.", settingsPath),
                    ex);
                errorMessage = "skin.ini konnte nicht gelesen werden.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                errorMessage = "Die Skin-ID in der skin.ini ist leer.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(displayNameFallback))
            {
                displayNameFallback = id;
            }

            if (!string.Equals(displayNameFallback, directoryName, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = string.Format(
                    "Der Skin-Ordnername '{0}' stimmt nicht mit DisplayNameFallback '{1}' ueberein.",
                    directoryName,
                    displayNameFallback);
                return false;
            }

            if (!IsSupportedSurfaceEffect(surfaceEffect))
            {
                errorMessage = string.Format(
                    "Der SurfaceEffect '{0}' wird nicht unterstuetzt.",
                    surfaceEffect);
                return false;
            }

            return true;
        }

        private static bool TryParseRectangle(string value, out Rectangle? rectangle)
        {
            rectangle = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            string[] parts = value.Split(',');
            if (parts.Length != 4)
            {
                return false;
            }

            int x;
            int y;
            int width;
            int height;
            if (!int.TryParse(parts[0].Trim(), out x) ||
                !int.TryParse(parts[1].Trim(), out y) ||
                !int.TryParse(parts[2].Trim(), out width) ||
                !int.TryParse(parts[3].Trim(), out height))
            {
                return false;
            }

            if (width <= 0 || height <= 0)
            {
                return false;
            }

            rectangle = new Rectangle(x, y, width, height);
            return true;
        }

        private static bool TryParseSize(string value, out Size? size)
        {
            size = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string[] parts = value.Split(',');
            if (parts.Length != 2)
            {
                return false;
            }

            int width;
            int height;
            if (!int.TryParse(parts[0].Trim(), out width) ||
                !int.TryParse(parts[1].Trim(), out height) ||
                width <= 0 ||
                height <= 0)
            {
                return false;
            }

            size = new Size(width, height);
            return true;
        }

        private static bool TryParseBoolean(string value, out bool result)
        {
            result = false;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = value.Trim();
            if (string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "yes", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "1", StringComparison.OrdinalIgnoreCase))
            {
                result = true;
                return true;
            }

            if (string.Equals(normalized, "false", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "no", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "0", StringComparison.OrdinalIgnoreCase))
            {
                result = false;
                return true;
            }

            return false;
        }

        private static bool IsSupportedSurfaceEffect(string surfaceEffect)
        {
            string normalizedSurfaceEffect = string.IsNullOrWhiteSpace(surfaceEffect)
                ? "none"
                : surfaceEffect.Trim();

            for (int i = 0; i < SupportedSurfaceEffects.Length; i++)
            {
                if (string.Equals(
                    SupportedSurfaceEffects[i],
                    normalizedSurfaceEffect,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<SkinDeleteTarget> GetDeleteTargets(string normalizedId, PanelSkinDefinition definition)
        {
            List<SkinDeleteTarget> targets = new List<SkinDeleteTarget>();

            try
            {
                string runtimeSkinsDirectoryPath = Path.GetFullPath(GetSkinsDirectoryPath())
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string runtimeSkinDirectoryPath = Path.GetFullPath(definition.DirectoryPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                if (Directory.Exists(runtimeSkinDirectoryPath))
                {
                    targets.Add(CreateDeleteTarget(runtimeSkinsDirectoryPath, runtimeSkinDirectoryPath, normalizedId));
                }
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce(
                    "skin-delete-path-normalize-failed-" + normalizedId,
                    string.Format("Skin-Pfad konnte nicht normalisiert werden: '{0}'.", definition.DirectoryPath),
                    ex);
                return null;
            }

            return targets;
        }

        private static SkinDeleteTarget CreateDeleteTarget(string skinsDirectoryPath, string skinDirectoryPath, string normalizedId)
        {
            string fullSkinsDirectoryPath = Path.GetFullPath(skinsDirectoryPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullSkinDirectoryPath = Path.GetFullPath(skinDirectoryPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string comparisonPrefix = fullSkinsDirectoryPath + Path.DirectorySeparatorChar;

            SkinPathPolicy.EnsurePortableSkinPathAllowed(fullSkinsDirectoryPath, "Skin-Basisverzeichnis");
            SkinPathPolicy.EnsurePortableSkinPathAllowed(fullSkinDirectoryPath, "Skin-Verzeichnis");

            if (!fullSkinDirectoryPath.StartsWith(comparisonPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Der Skin liegt ausserhalb des erwarteten Skin-Ordners.");
            }

            string deleteStagingDirectoryPath = Path.Combine(fullSkinsDirectoryPath, DeleteStagingDirectoryName);
            string stagedSkinDirectoryPath = Path.Combine(
                deleteStagingDirectoryPath,
                string.Format(
                    "{0}-{1}",
                    normalizedId,
                    Guid.NewGuid().ToString("N")));

            SkinPathPolicy.EnsurePortableSkinPathAllowed(deleteStagingDirectoryPath, "Skin-Staging-Verzeichnis");
            SkinPathPolicy.EnsurePortableSkinPathAllowed(stagedSkinDirectoryPath, "temporäres Skin-Staging-Verzeichnis");

            return new SkinDeleteTarget(
                fullSkinsDirectoryPath,
                fullSkinDirectoryPath,
                deleteStagingDirectoryPath,
                stagedSkinDirectoryPath);
        }

        private struct SkinDeleteTarget
        {
            public SkinDeleteTarget(
                string fullSkinsDirectoryPath,
                string fullSkinDirectoryPath,
                string deleteStagingDirectoryPath,
                string stagedSkinDirectoryPath)
            {
                this.FullSkinsDirectoryPath = fullSkinsDirectoryPath;
                this.FullSkinDirectoryPath = fullSkinDirectoryPath;
                this.DeleteStagingDirectoryPath = deleteStagingDirectoryPath;
                this.StagedSkinDirectoryPath = stagedSkinDirectoryPath;
            }

            public readonly string FullSkinsDirectoryPath;
            public readonly string FullSkinDirectoryPath;
            public readonly string DeleteStagingDirectoryPath;
            public readonly string StagedSkinDirectoryPath;
        }
    }
}
