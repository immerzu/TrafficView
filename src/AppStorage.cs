using System;
using System.IO;

namespace TrafficView
{
    internal static class AppStorage
    {
        public const string PortableMarkerFileName = "TrafficView.portable";
        private const string SettingsDirectoryName = "TrafficView";
        private static readonly string[] CloudPathMarkers = new string[]
        {
            "OneDrive",
            "Dropbox",
            "Google Drive",
            "iCloudDrive"
        };

        private static bool? isPortableMode;

        public static bool IsPortableMode
        {
            get
            {
                if (!isPortableMode.HasValue)
                {
                    string markerPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PortableMarkerFileName);
                    isPortableMode = File.Exists(markerPath);
                }

                return isPortableMode.Value;
            }
        }

        public static string BaseDirectory
        {
            get { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        public static string GetSkinsDirectoryPath()
        {
            return Path.Combine(BaseDirectory, "Skins");
        }

        public static string GetSettingsDirectoryPath()
        {
            if (IsPortableMode)
            {
                return BaseDirectory;
            }

            string localApplicationDataPath;

            try
            {
                localApplicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }
            catch (System.Security.SecurityException)
            {
                AppLog.WarnOnce(
                    "settings-directory-security",
                    "LocalAppData is not accessible for settings storage; falling back to application directory.");
                localApplicationDataPath = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(localApplicationDataPath))
            {
                return BaseDirectory;
            }

            return Path.Combine(localApplicationDataPath, SettingsDirectoryName);
        }

        public static bool TryGetStorageWarningMessage(out string message)
        {
            message = string.Empty;

            string settingsDirectoryPath = GetSettingsDirectoryPath();
            string writeError;
            if (!IsDirectoryWritable(settingsDirectoryPath, out writeError))
            {
                message = string.Format(
                    "TrafficView kann im aktuellen Datenordner nicht schreiben.\r\n\r\nOrdner: {0}\r\nFehler: {1}\r\n\r\nEinstellungen, Verbrauchsdaten und Logs koennen dadurch verloren gehen.",
                    settingsDirectoryPath,
                    writeError);
                return true;
            }

            if (IsPortableMode)
            {
                string portableWarning = GetPortableLocationWarning(BaseDirectory);
                if (!string.IsNullOrWhiteSpace(portableWarning))
                {
                    AppLog.WarnOnce(
                        "portable-storage-location-warning-" + BaseDirectory,
                        portableWarning);
                }
            }

            return false;
        }

        public static string CreateStorageDiagnosticsText()
        {
            string settingsDirectoryPath = GetSettingsDirectoryPath();
            string settingsWriteError;
            bool settingsWritable = IsDirectoryWritable(settingsDirectoryPath, out settingsWriteError);
            string baseWriteError;
            bool baseWritable = IsDirectoryWritable(BaseDirectory, out baseWriteError);
            string portableWarning = IsPortableMode ? GetPortableLocationWarning(BaseDirectory) : string.Empty;

            return string.Format(
                "Storage mode: {0}\r\nBase path writable: {1}{2}\r\nSettings path writable: {3}{4}\r\nPortable location warning: {5}",
                IsPortableMode ? "portable" : "local-app-data",
                baseWritable ? "yes" : "no",
                baseWritable ? string.Empty : " (" + baseWriteError + ")",
                settingsWritable ? "yes" : "no",
                settingsWritable ? string.Empty : " (" + settingsWriteError + ")",
                string.IsNullOrWhiteSpace(portableWarning) ? "none" : portableWarning);
        }

        public static bool IsPathWithinBaseDirectory(string path)
        {
            string normalizedPath = NormalizePath(path);
            string normalizedBaseDirectory = NormalizePath(BaseDirectory);

            if (string.IsNullOrWhiteSpace(normalizedPath) || string.IsNullOrWhiteSpace(normalizedBaseDirectory))
            {
                return false;
            }

            if (string.Equals(normalizedPath, normalizedBaseDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return normalizedPath.StartsWith(
                normalizedBaseDirectory + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDirectoryWritable(string directoryPath, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                errorMessage = "Der Pfad ist leer.";
                return false;
            }

            string tempPath = string.Empty;
            try
            {
                Directory.CreateDirectory(directoryPath);
                tempPath = Path.Combine(
                    directoryPath,
                    ".trafficview-write-test-" + Guid.NewGuid().ToString("N") + ".tmp");
                File.WriteAllText(tempPath, "test");
                File.Delete(tempPath);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            finally
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(tempPath) && File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch
                {
                    System.Diagnostics.Trace.WriteLine("[TrafficView] AppStorage.IsDirectoryWritable Temp-Datei konnte nicht geloescht werden.");
                }
            }
        }

        private static string GetPortableLocationWarning(string directoryPath)
        {
            string normalizedPath = NormalizePath(directoryPath);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return string.Empty;
            }

            string programFiles = NormalizePath(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
            string programFilesX86 = NormalizePath(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
            string windows = NormalizePath(Environment.GetFolderPath(Environment.SpecialFolder.Windows));

            if (IsPathWithinDirectory(normalizedPath, programFiles) ||
                IsPathWithinDirectory(normalizedPath, programFilesX86) ||
                IsPathWithinDirectory(normalizedPath, windows))
            {
                return "Portable mode is running from a protected system directory; write access can fail after updates or restarts.";
            }

            for (int i = 0; i < CloudPathMarkers.Length; i++)
            {
                if (normalizedPath.IndexOf(CloudPathMarkers[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "Portable mode is running from a cloud-synchronized directory; delayed sync or file locking can affect settings and usage data.";
                }
            }

            return string.Empty;
        }

        private static bool IsPathWithinDirectory(string path, string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(directoryPath))
            {
                return false;
            }

            if (string.Equals(path, directoryPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return path.StartsWith(
                directoryPath + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                System.Diagnostics.Trace.WriteLine("[TrafficView] AppStorage.NormalizePath schlug fehl.");
                return string.Empty;
            }
        }
    }
}
