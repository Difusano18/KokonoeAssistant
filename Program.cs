using System;
using System.IO;
using KokonoeAssistant.Services;

namespace KokonoeAssistant
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            try
            {
                TrySetConsoleUtf8();
                
                BootstrapLog("starting");
                var app = new App();
                app.InitializeComponent();
                var code = app.Run();
                BootstrapLog($"exited code={code}");
                Environment.ExitCode = code;
            }
            catch (Exception ex)
            {
                BootstrapLog(ex.ToString());
                throw;
            }
        }

        private static void BootstrapLog(string message)
        {
            try
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log");
                File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch (Exception suppressedEx36) { KokoSystemLog.Write("PROGRAM-CATCH", "BootstrapLog failed near source line 36: " + suppressedEx36); }
        }

        private static void TrySetConsoleUtf8()
        {
            try
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
            }
            catch (IOException ex)
            {
                // WPF can start without an attached console; UI startup must not die over console encoding.
                KokoSystemLog.Write("PROGRAM-CATCH", "Console UTF-8 setup unavailable: " + ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                KokoSystemLog.Write("PROGRAM-CATCH", "Console UTF-8 setup denied: " + ex);
            }
        }
    }
}
