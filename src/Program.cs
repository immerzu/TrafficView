using System;
using System.Windows.Forms;

namespace TrafficView
{
    internal static class Program
    {
        private const string SingleInstanceMutexName = @"Local\TrafficView.SingleInstance";

        [STAThread]
        private static void Main()
        {
            RuntimeDiagnostics.MarkProcessStarted();

            System.Threading.Mutex singleInstanceMutex = null;
            bool ownsSingleInstanceMutex = false;

            try
            {
                singleInstanceMutex = new System.Threading.Mutex(false, SingleInstanceMutexName);

                try
                {
                    ownsSingleInstanceMutex = singleInstanceMutex.WaitOne(0, false);
                }
                catch (System.Threading.AbandonedMutexException)
                {
                    ownsSingleInstanceMutex = true;
                }

                if (!ownsSingleInstanceMutex)
                {
                    AppLog.WarnOnce(
                        "single-instance-already-running",
                        "Ein zweiter Start von TrafficView wurde blockiert, weil bereits eine Instanz aktiv ist.");
                    MessageBox.Show(
                        "TrafficView ist bereits gestartet.\r\n\r\nBitte verwende die bereits laufende Instanz im Infobereich von Windows.",
                        "TrafficView",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                RegisterUnhandledExceptionLogging();
                AppLog.Info(string.Format(
                    "Session started. Version={0}; OS={1}; 64BitOS={2}; Machine={3}; {4}",
                    typeof(Program).Assembly.GetName().Version,
                    Environment.OSVersion.VersionString,
                    Environment.Is64BitOperatingSystem ? "yes" : "no",
                    Environment.MachineName,
                    RuntimeDiagnostics.CreateDiagnosticsText(string.Empty).Replace("\r\n", "; ")));
                DpiHelper.EnableHighDpiSupport();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrafficViewContext());
            }
            catch (Exception ex)
            {
                AppLog.Error("Application terminated during startup/bootstrap.", ex);
                throw;
            }
            finally
            {
                if (singleInstanceMutex != null)
                {
                    if (ownsSingleInstanceMutex)
                    {
                        try
                        {
                            singleInstanceMutex.ReleaseMutex();
                        }
                        catch
                        {
                        }
                    }

                    singleInstanceMutex.Dispose();
                }
            }
        }

        private static void RegisterUnhandledExceptionLogging()
        {
            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
            {
                AppLog.Error("Unhandled UI thread exception.", e.Exception);
            };

            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                AppLog.Error(
                    string.Format("Unhandled AppDomain exception. IsTerminating={0}", e.IsTerminating ? "yes" : "no"),
                    e.ExceptionObject as Exception);
            };
        }
    }

}

