using System;

namespace TrafficView
{
    internal static class SkinPathPolicy
    {
        public static void EnsurePortableSkinPathAllowed(string path, string pathLabel)
        {
            if (!AppStorage.IsPortableMode)
            {
                return;
            }

            if (AppStorage.IsPathWithinBaseDirectory(path))
            {
                return;
            }

            throw new InvalidOperationException(
                string.Format(
                    "Der Pfad fuer {0} liegt ausserhalb des Portable-Verzeichnisses: '{1}'.",
                    string.IsNullOrWhiteSpace(pathLabel) ? "Skin-Dateien" : pathLabel,
                    path ?? string.Empty));
        }
    }
}
