using System;

namespace TrafficView
{
    internal static class TrafficRateFormatter
    {
        public static string FormatSpeed(double bytesPerSecond)
        {
            string[] units = new string[] { "B/s", "KB/s", "MB/s", "GB/s" };
            double value = Math.Max(0D, bytesPerSecond);
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
    }
}
