using System;
using System.Collections.Generic;
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
                return string.Equals(this.SurfaceEffect, "glass", StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    internal static class PanelSkinCatalog
    {
        private const string SkinSettingsFileName = "skin.ini";
        private static readonly object SyncRoot = new object();
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

        private static PanelSkinDefinition[] LoadDefinitions()
        {
            string skinsDirectoryPath = GetSkinsDirectoryPath();
            List<PanelSkinDefinition> definitions = new List<PanelSkinDefinition>();

            if (Directory.Exists(skinsDirectoryPath))
            {
                string[] skinDirectoryPaths = Directory.GetDirectories(skinsDirectoryPath);
                Array.Sort(skinDirectoryPaths, StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < skinDirectoryPaths.Length; i++)
                {
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

        private static PanelSkinDefinition TryLoadDefinition(string skinDirectoryPath)
        {
            if (string.IsNullOrWhiteSpace(skinDirectoryPath))
            {
                return null;
            }

            string settingsPath = Path.Combine(skinDirectoryPath, SkinSettingsFileName);
            if (!File.Exists(settingsPath))
            {
                return null;
            }

            string id = Path.GetFileName(skinDirectoryPath);
            string displayNameKey = string.Empty;
            string displayNameFallback = id;
            string surfaceEffect = "none";

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
                    "skin-definition-load-failed-" + skinDirectoryPath,
                    string.Format("Skin-Definition konnte nicht aus '{0}' geladen werden.", settingsPath),
                    ex);
                return null;
            }

            return new PanelSkinDefinition(
                id,
                displayNameKey,
                displayNameFallback,
                surfaceEffect,
                skinDirectoryPath);
        }
    }
}
