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
            catch { }
        }

        private static void TrySetConsoleUtf8()
        {
            try
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
            }
            catch (IOException)
            {
                // WPF can start without an attached console; UI startup must not die over console encoding.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
