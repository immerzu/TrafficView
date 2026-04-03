using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace TrafficView
{
    internal sealed class PanelSkinDefinition
    {
        public PanelSkinDefinition(
            string id,
            string displayNameKey,
            string displayNameFallback,
            string surfaceEffect,
            string directoryPath)
        {
            this.Id = string.IsNullOrWhiteSpace(id) ? "08" : id.Trim();
            this.DisplayNameKey = string.IsNullOrWhiteSpace(displayNameKey) ? string.Empty : displayNameKey.Trim();
            this.DisplayNameFallback = string.IsNullOrWhiteSpace(displayNameFallback) ? this.Id : displayNameFallback.Trim();
            this.SurfaceEffect = string.IsNullOrWhiteSpace(surfaceEffect) ? "none" : surfaceEffect.Trim();
            this.DirectoryPath = string.IsNullOrWhiteSpace(directoryPath) ? string.Empty : directoryPath.Trim();
        }

        public string Id { get; private set; }

        public string DisplayNameKey { get; private set; }

        public string DisplayNameFallback { get; private set; }

        public string SurfaceEffect { get; private set; }

        public string DirectoryPath { get; private set; }

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

    internal static class PanelSkinCatalog
    {
        public const string DefaultSkinId = "08";
        private const string SkinSettingsFileName = "skin.ini";
        private const string DeleteStagingDirectoryName = ".delete";
        private static readonly object SyncRoot = new object();
        private static readonly KeyValuePair<string, Size>[] RequiredAssetDefinitions = new KeyValuePair<string, Size>[]
        {
            new KeyValuePair<string, Size>("TrafficView.panel.90.png", new Size(92, 50)),
            new KeyValuePair<string, Size>("TrafficView.panel.png", new Size(102, 56)),
            new KeyValuePair<string, Size>("TrafficView.panel.110.png", new Size(112, 62)),
            new KeyValuePair<string, Size>("TrafficView.panel.125.png", new Size(128, 70)),
            new KeyValuePair<string, Size>("TrafficView.panel.150.png", new Size(153, 84))
        };
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
                return definitions.Length > 0 ? definitions[0].Id : "08";
            }

            for (int i = 0; i < definitions.Length; i++)
            {
                if (string.Equals(definitions[i].Id, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return definitions[i].Id;
                }
            }

            return definitions.Length > 0 ? definitions[0].Id : "08";
        }

        public static string GetDefaultOrFirstSkinId()
        {
            PanelSkinDefinition[] definitions = GetAvailableSkins();

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

            string skinsDirectoryPath = GetSkinsDirectoryPath();
            string fullSkinsDirectoryPath = string.Empty;
            string fullSkinDirectoryPath = string.Empty;
            string deleteStagingDirectoryPath = string.Empty;
            string stagedSkinDirectoryPath = string.Empty;

            try
            {
                fullSkinsDirectoryPath = Path.GetFullPath(skinsDirectoryPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                fullSkinDirectoryPath = Path.GetFullPath(definition.DirectoryPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                deleteStagingDirectoryPath = Path.Combine(fullSkinsDirectoryPath, ".delete");
                stagedSkinDirectoryPath = Path.Combine(
                    deleteStagingDirectoryPath,
                    string.Format(
                        "{0}-{1}",
                        normalizedId,
                        Guid.NewGuid().ToString("N")));
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce(
                    "skin-delete-path-normalize-failed-" + normalizedId,
                    string.Format("Skin-Pfad konnte nicht normalisiert werden: '{0}'.", definition.DirectoryPath),
                    ex);
                errorMessage = "Der Skin-Pfad konnte nicht verarbeitet werden.";
                return false;
            }

            string comparisonPrefix = fullSkinsDirectoryPath + Path.DirectorySeparatorChar;
            if (!fullSkinDirectoryPath.StartsWith(comparisonPrefix, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "Der Skin liegt ausserhalb des erwarteten Skin-Ordners.";
                return false;
            }

            try
            {
                if (!Directory.Exists(fullSkinDirectoryPath))
                {
                    errorMessage = "Der Skin-Ordner ist nicht mehr vorhanden.";
                    return false;
                }

                Directory.CreateDirectory(deleteStagingDirectoryPath);
                Directory.Move(fullSkinDirectoryPath, stagedSkinDirectoryPath);
                Directory.Delete(stagedSkinDirectoryPath, true);
                Reload();
                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    if (!Directory.Exists(fullSkinDirectoryPath) &&
                        Directory.Exists(stagedSkinDirectoryPath))
                    {
                        Directory.Move(stagedSkinDirectoryPath, fullSkinDirectoryPath);
                    }
                }
                catch (Exception rollbackEx)
                {
                    AppLog.WarnOnce(
                        "skin-delete-rollback-failed-" + normalizedId,
                        string.Format("Rollback fuer Skin '{0}' ist fehlgeschlagen.", normalizedId),
                        rollbackEx);
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

            for (int i = 0; i < RequiredAssetDefinitions.Length; i++)
            {
                string fileName = RequiredAssetDefinitions[i].Key;
                Size expectedSize = RequiredAssetDefinitions[i].Value;
                string assetPath = Path.Combine(skinDirectoryPath, fileName);

                if (!File.Exists(assetPath))
                {
                    errorMessage = string.Format("Die Skin-Datei '{0}' fehlt.", fileName);
                    return false;
                }

                try
                {
                    using (Bitmap bitmap = new Bitmap(assetPath))
                    {
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

        private static PanelSkinDefinition[] LoadDefinitions()
        {
            string skinsDirectoryPath = GetSkinsDirectoryPath();
            List<PanelSkinDefinition> definitions = new List<PanelSkinDefinition>();

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
            string parseErrorMessage;
            if (!TryParseSkinDefinition(
                skinDirectoryPath,
                settingsPath,
                out id,
                out displayNameKey,
                out displayNameFallback,
                out surfaceEffect,
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
                skinDirectoryPath);
        }

        private static bool TryParseSkinDefinition(
            string skinDirectoryPath,
            string settingsPath,
            out string id,
            out string displayNameKey,
            out string displayNameFallback,
            out string surfaceEffect,
            out string errorMessage)
        {
            string directoryName = Path.GetFileName(
                skinDirectoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            id = directoryName;
            displayNameKey = string.Empty;
            displayNameFallback = directoryName;
            surfaceEffect = "none";
            errorMessage = string.Empty;

            try
            {
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

            if (!string.Equals(id, directoryName, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = string.Format(
                    "Die Skin-ID '{0}' stimmt nicht mit dem Ordnernamen '{1}' ueberein.",
                    id,
                    directoryName);
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
    }
}
