using System;

namespace TrafficView
{
    internal static class TrafficUsageFormatter
    {
        public static string FormatAmount(long bytes)
        {
            string[] units = new string[] { "B", "KB", "MB", "GB", "TB" };
            double value = Math.Max(0L, bytes);
            int unitIndex = 0;

            while (value >= 1024D && unitIndex < units.Length - 1)
            {
                value /= 1024D;
                unitIndex++;
            }

            if (unitIndex == 0)
            {
                return string.Format("{0:0} {1}", value, units[unitIndex]);
            }

            string format = value >= 100D ? "0" : (value >= 10D ? "0.#" : "0.0");
            return string.Format("{0:" + format + "} {1}", value, units[unitIndex]);
        }

        public static string FormatPeriodStart(DateTime exportTimestampLocal, TrafficUsagePeriod period)
        {
            DateTime periodStartLocal;
            switch (period)
            {
                case TrafficUsagePeriod.Daily:
                    periodStartLocal = exportTimestampLocal.Date;
                    break;
                case TrafficUsagePeriod.Weekly:
                    periodStartLocal = GetStartOfWeek(exportTimestampLocal);
                    break;
                case TrafficUsagePeriod.Monthly:
                    periodStartLocal = new DateTime(exportTimestampLocal.Year, exportTimestampLocal.Month, 1);
                    break;
                default:
                    periodStartLocal = exportTimestampLocal;
                    break;
            }

            return periodStartLocal.ToString("dd.MM.yyyy HH:mm:ss");
        }

        public static string FormatPeriodEnd(DateTime exportTimestampLocal)
        {
            return exportTimestampLocal.ToString("dd.MM.yyyy HH:mm:ss");
        }

        public static string EscapeCsvValue(string value)
        {
            string safeValue = value ?? string.Empty;
            if (safeValue.IndexOfAny(new char[] { ';', '"', '\r', '\n' }) < 0)
            {
                return safeValue;
            }

            return "\"" + safeValue.Replace("\"", "\"\"") + "\"";
        }

        private static DateTime GetStartOfWeek(DateTime dateTime)
        {
            DayOfWeek firstDayOfWeek = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
            int offset = (7 + (dateTime.DayOfWeek - firstDayOfWeek)) % 7;
            return dateTime.Date.AddDays(-offset);
        }
    }
}
