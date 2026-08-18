using System;
using System.IO;
using System.Threading;

namespace TrafficView
{
    internal static class FileRetry
    {
        private static readonly int[] DelaysMilliseconds = new int[]
        {
            50,
            150,
            300
        };

        public static void Execute(string operationName, Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException("action");
            }

            Exception lastException = null;

            for (int attempt = 0; attempt <= DelaysMilliseconds.Length; attempt++)
            {
                try
                {
                    action();
                    return;
                }
                catch (Exception ex)
                {
                    if (!IsTransientFileException(ex) || attempt >= DelaysMilliseconds.Length)
                    {
                        throw;
                    }

                    lastException = ex;

                    try
                    {
                        AppLog.WarnOnce(
                            "file-retry-" + SanitizeKey(operationName) + "-" + attempt.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            string.Format(
                                "Dateioperation '{0}' fehlgeschlagen, Wiederholung {1}/{2}.",
                                operationName ?? "unknown",
                                attempt + 1,
                                DelaysMilliseconds.Length),
                            ex);
                    }
                    catch
                    {
                        System.Diagnostics.Trace.WriteLine("[TrafficView] FileRetry logging failed.");
                    }

                    Thread.Sleep(DelaysMilliseconds[attempt]);
                }
            }

            if (lastException != null)
            {
                throw lastException;
            }
        }

        public static T Execute<T>(string operationName, Func<T> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException("action");
            }

            T result = default(T);
            Execute(operationName, delegate
            {
                result = action();
            });
            return result;
        }

        private static bool IsTransientFileException(Exception exception)
        {
            if (exception == null)
            {
                return false;
            }

            if (exception is IOException)
            {
                return true;
            }

            if (exception is UnauthorizedAccessException)
            {
                return true;
            }

            return false;
        }

        private static string SanitizeKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unknown";
            }

            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
                {
                    chars[i] = '-';
                }
            }

            return new string(chars);
        }
    }
}
