using System;
using System.IO;

namespace KokonoeAssistant
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            try
            {
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
            catch { }
        }
    }
}
