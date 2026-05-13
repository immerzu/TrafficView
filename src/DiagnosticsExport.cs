using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace TrafficView
{
    internal static class DiagnosticsExport
    {
        public static void WriteZip(string targetPath, string diagnosticsText)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                throw new ArgumentException("Diagnose-Zielpfad ist leer.", "targetPath");
            }

            string tempPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                string directoryPath = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create))
                {
                    List<string> warnings = new List<string>();
                    List<string> manifestEntries = new List<string>();
                    HashSet<string> usedEntryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    string diagnosticsName = ResolveEntryName("diagnostics.txt", usedEntryNames);
                    WriteTextEntry(archive, diagnosticsName, diagnosticsText ?? string.Empty);
                    manifestEntries.Add(diagnosticsName);
                    usedEntryNames.Add(diagnosticsName);

                    AddLogFiles(archive, warnings, manifestEntries, usedEntryNames);

                    if (warnings.Count > 0)
                    {
                        string warningsName = ResolveEntryName("diagnostics-export-warnings.txt", usedEntryNames);
                        WriteTextEntry(
                            archive,
                            warningsName,
                            string.Join("\r\n", warnings.ToArray()));
                        manifestEntries.Add(warningsName);
                        usedEntryNames.Add(warningsName);
                    }

                    string manifestName = ResolveEntryName("diagnostics-manifest.txt", usedEntryNames);
                    WriteTextEntry(archive, manifestName, CreateDiagnosticsManifest(manifestEntries));
                }

                if (File.Exists(targetPath))
                {
                    File.Replace(tempPath, targetPath, null, true);
                }
                else
                {
                    File.Move(tempPath, targetPath);
                }
            }
            finally
            {
                TryDeleteFile(tempPath);
            }
        }

        private static void AddLogFiles(ZipArchive archive, List<string> warnings, List<string> manifestEntries, HashSet<string> usedEntryNames)
        {
            string[] logPaths = AppLog.GetLogFilePathsForDiagnostics();
            for (int i = 0; i < logPaths.Length; i++)
            {
                string logPath = logPaths[i];
                if (string.IsNullOrWhiteSpace(logPath) || !File.Exists(logPath))
                {
                    continue;
                }

                string desiredName = Path.GetFileName(logPath);
                if (string.IsNullOrWhiteSpace(desiredName))
                {
                    continue;
                }

                string entryName = ResolveEntryName(desiredName, usedEntryNames);

                try
                {
                    using (FileStream logStream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    {
                        ZipArchiveEntry logEntry = archive.CreateEntry(entryName);
                        using (Stream entryStream = logEntry.Open())
                        {
                            logStream.CopyTo(entryStream);
                        }

                        manifestEntries.Add(entryName);
                        usedEntryNames.Add(entryName);
                    }
                }
                catch (Exception ex)
                {
                    if (warnings != null)
                    {
                        warnings.Add(string.Format(
                            "Log file could not be added: {0} ({1}: {2})",
                            entryName,
                            ex.GetType().Name,
                            ex.Message));
                    }
                }
            }
        }

        private static string CreateDiagnosticsManifest(List<string> entryNames)
        {
            StringBuilder manifest = new StringBuilder();
            manifest.AppendLine("TrafficView Diagnostics Export");
            manifest.AppendLine("Created: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC");
            manifest.AppendLine();

            foreach (string entryName in entryNames)
            {
                manifest.AppendLine(entryName);
            }

            manifest.AppendLine();
            manifest.AppendLine(entryNames.Count + " entries");
            return manifest.ToString();
        }

        private static string ResolveEntryName(string desiredName, HashSet<string> usedNames)
        {
            if (!usedNames.Contains(desiredName))
            {
                return desiredName;
            }

            string nameWithoutExtension = Path.GetFileNameWithoutExtension(desiredName);
            string extension = Path.GetExtension(desiredName);
            int counter = 2;
            string candidate;
            do
            {
                candidate = nameWithoutExtension + "-" + counter.ToString(System.Globalization.CultureInfo.InvariantCulture) + extension;
                counter++;
            }
            while (usedNames.Contains(candidate));

            return candidate;
        }

        private static void WriteTextEntry(ZipArchive archive, string entryName, string text)
        {
            ZipArchiveEntry diagnosticsEntry = archive.CreateEntry(entryName);
            using (Stream entryStream = diagnosticsEntry.Open())
            using (StreamWriter writer = new StreamWriter(entryStream, new UTF8Encoding(true)))
            {
                writer.Write(text ?? string.Empty);
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                System.Diagnostics.Trace.WriteLine("[TrafficView] DiagnosticsExport.TryDeleteFile schlug fehl.");
            }
        }
    }
}
