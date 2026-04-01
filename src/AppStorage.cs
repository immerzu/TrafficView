using System;
using System.IO;

namespace TrafficView
{
    internal static class AppStorage
    {
        public const string PortableMarkerFileName = "TrafficView.portable";
        private const string SettingsDirectoryName = "TrafficView";
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
    }
}
