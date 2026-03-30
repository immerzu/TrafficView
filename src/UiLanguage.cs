using System;
using System.Collections.Generic;
using System.IO;

namespace TrafficView
{
    internal sealed class LanguageOption
    {
        public LanguageOption(string code, string displayName)
        {
            this.Code = code;
            this.DisplayName = displayName;
        }

        public string Code { get; private set; }

        public string DisplayName { get; private set; }

        public override string ToString()
        {
            return this.DisplayName;
        }
    }

    internal static class UiLanguage
    {
        private static readonly LanguageOption[] SupportedLanguages = new LanguageOption[]
        {
            new LanguageOption("de", "Deutsch"),
            new LanguageOption("en", "English"),
            new LanguageOption("ru", "Русский"),
            new LanguageOption("zh-Hans", "中文(简体)")
        };

        private static readonly Dictionary<string, Dictionary<string, string>> Translations =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        private static bool loaded;
        private static string currentCode = "de";

        public static string CurrentCode
        {
            get { return currentCode; }
        }

        public static LanguageOption[] GetSupportedLanguages()
        {
            return SupportedLanguages;
        }

        public static void Initialize(string languageCode)
        {
            if (!loaded)
            {
                LoadTranslations();
                loaded = true;
            }

            SetLanguage(languageCode);
        }

        public static void SetLanguage(string languageCode)
        {
            currentCode = NormalizeLanguageCode(languageCode);
        }

        public static string NormalizeLanguageCode(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
            {
                return "de";
            }

            string normalized = languageCode.Trim();
            if (string.Equals(normalized, "zh", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "zh-cn", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "zh-hans", StringComparison.OrdinalIgnoreCase))
            {
                return "zh-Hans";
            }

            for (int i = 0; i < SupportedLanguages.Length; i++)
            {
                if (string.Equals(SupportedLanguages[i].Code, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return SupportedLanguages[i].Code;
                }
            }

            return "de";
        }

        public static string Get(string key, string fallback)
        {
            Dictionary<string, string> languageEntries;
            string value;

            if (Translations.TryGetValue(currentCode, out languageEntries) &&
                languageEntries.TryGetValue(key, out value) &&
                !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            if (!string.Equals(currentCode, "de", StringComparison.OrdinalIgnoreCase) &&
                Translations.TryGetValue("de", out languageEntries) &&
                languageEntries.TryGetValue(key, out value) &&
                !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            return fallback;
        }

        public static string Format(string key, string fallback, params object[] args)
        {
            string format = Get(key, fallback);

            try
            {
                return string.Format(format, args);
            }
            catch (FormatException)
            {
                AppLog.WarnOnce(
                    "translation-format-" + CurrentCode + "-" + (key ?? string.Empty),
                    string.Format(
                        "Translation format fallback used for key '{0}' in language '{1}'.",
                        key ?? string.Empty,
                        CurrentCode),
                    null);

                try
                {
                    return string.Format(fallback, args);
                }
                catch (FormatException)
                {
                    AppLog.WarnOnce(
                        "translation-format-fallback-" + (key ?? string.Empty),
                        string.Format(
                            "Fallback translation format is also invalid for key '{0}'. Returning raw format string.",
                            key ?? string.Empty),
                        null);
                    return format;
                }
            }
        }

        private static void LoadTranslations()
        {
            string languageFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TrafficView.languages.ini");
            string[] lines;

            try
            {
                if (!File.Exists(languageFilePath))
                {
                    return;
                }

                lines = File.ReadAllLines(languageFilePath);
            }
            catch (IOException)
            {
                AppLog.WarnOnce(
                    "translations-load-io",
                    string.Format("Translation file could not be read: {0}", languageFilePath));
                return;
            }
            catch (UnauthorizedAccessException)
            {
                AppLog.WarnOnce(
                    "translations-load-access",
                    string.Format("Translation file is not accessible: {0}", languageFilePath));
                return;
            }
            catch (System.Security.SecurityException)
            {
                AppLog.WarnOnce(
                    "translations-load-security",
                    string.Format("Translation file access was denied by security policy: {0}", languageFilePath));
                return;
            }
            catch (NotSupportedException)
            {
                AppLog.WarnOnce(
                    "translations-load-path",
                    string.Format("Translation file path is not supported: {0}", languageFilePath));
                return;
            }

            Dictionary<string, Dictionary<string, string>> parsedTranslations =
                new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            string currentSection = string.Empty;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith(";", StringComparison.Ordinal) || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
                {
                    currentSection = NormalizeLanguageCode(line.Substring(1, line.Length - 2));
                    if (!parsedTranslations.ContainsKey(currentSection))
                    {
                        parsedTranslations[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    }

                    continue;
                }

                int splitIndex = line.IndexOf('=');
                if (splitIndex <= 0 || string.IsNullOrEmpty(currentSection))
                {
                    continue;
                }

                string key = line.Substring(0, splitIndex).Trim();
                string value = line.Substring(splitIndex + 1);

                if (!parsedTranslations.ContainsKey(currentSection))
                {
                    parsedTranslations[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                parsedTranslations[currentSection][key] = value;
            }

            Translations.Clear();
            foreach (KeyValuePair<string, Dictionary<string, string>> entry in parsedTranslations)
            {
                Translations[entry.Key] = entry.Value;
            }
        }
    }
}
