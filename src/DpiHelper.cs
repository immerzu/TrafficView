using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace TrafficView
{
    internal static class DpiHelper
    {
        private const int BaseDpi = 96;
        private static readonly IntPtr DpiAwarenessContextPerMonitorAwareV2 = new IntPtr(-4);

        private enum ProcessDpiAwareness
        {
            ProcessDpiUnaware = 0,
            ProcessSystemDpiAware = 1,
            ProcessPerMonitorDpiAware = 2
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiAwarenessContext);

        [DllImport("shcore.dll", SetLastError = true)]
        private static extern int SetProcessDpiAwareness(ProcessDpiAwareness awareness);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

        public static void EnableHighDpiSupport()
        {
            try
            {
                if (Environment.OSVersion.Version.Major >= 10 &&
                    SetProcessDpiAwarenessContext(DpiAwarenessContextPerMonitorAwareV2))
                {
                    return;
                }
            }
            catch (EntryPointNotFoundException)
            {
                AppLog.WarnOnce(
                    "dpi-awareness-context-entrypoint",
                    "Per-monitor V2 DPI API is not available; falling back to older DPI handling.");
            }

            try
            {
                int result = SetProcessDpiAwareness(ProcessDpiAwareness.ProcessPerMonitorDpiAware);
                if (result == 0)
                {
                    return;
                }

                AppLog.WarnOnce(
                    "dpi-awareness-shcore-result",
                    string.Format(
                        "SetProcessDpiAwareness failed with result {0}; falling back to legacy DPI handling.",
                        result));
            }
            catch (DllNotFoundException)
            {
                AppLog.WarnOnce(
                    "dpi-awareness-shcore-missing",
                    "shcore.dll is not available; falling back to legacy DPI handling.");
            }
            catch (EntryPointNotFoundException)
            {
                AppLog.WarnOnce(
                    "dpi-awareness-shcore-entrypoint",
                    "SetProcessDpiAwareness is not available; falling back to legacy DPI handling.");
            }

            try
            {
                if (!SetProcessDPIAware())
                {
                    AppLog.WarnOnce(
                        "dpi-awareness-legacy-failed",
                        "Legacy SetProcessDPIAware call did not succeed.");
                }
            }
            catch (EntryPointNotFoundException)
            {
                AppLog.WarnOnce(
                    "dpi-awareness-legacy-entrypoint",
                    "Legacy DPI API is not available.");
            }
        }

        public static int GetDesktopDpi()
        {
            using (Graphics graphics = Graphics.FromHwnd(IntPtr.Zero))
            {
                return NormalizeDpi((int)Math.Round(graphics.DpiX));
            }
        }

        public static int GetWindowDpi(IntPtr handle)
        {
            if (handle != IntPtr.Zero)
            {
                try
                {
                    uint dpi = GetDpiForWindow(handle);
                    if (dpi > 0U)
                    {
                        return NormalizeDpi((int)dpi);
                    }
                }
                catch (EntryPointNotFoundException)
                {
                    AppLog.WarnOnce(
                        "dpi-get-window-fallback",
                        "GetDpiForWindow is not available; using desktop DPI fallback.");
                }
            }

            return GetDesktopDpi();
        }

        public static int NormalizeDpi(int dpi)
        {
            return Math.Max(BaseDpi, dpi);
        }

        public static int Scale(int value, int dpi)
        {
            return Math.Max(
                1,
                (int)Math.Round(
                    (value * NormalizeDpi(dpi)) / (double)BaseDpi,
                    MidpointRounding.AwayFromZero));
        }

        public static float Scale(float value, int dpi)
        {
            return (float)(value * NormalizeDpi(dpi) / (double)BaseDpi);
        }

        public static int HiWord(IntPtr value)
        {
            long raw = value.ToInt64();
            return (int)((raw >> 16) & 0xFFFF);
        }
    }
}
