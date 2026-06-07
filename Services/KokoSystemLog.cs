using System;
using System.Diagnostics;
using System.IO;

namespace KokonoeAssistant.Services
{
    public static class KokoSystemLog
    {
        private static readonly object LockObj = new();
        private static string _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "kokonoe-data", "logs", "Kokonoe_System.log");

        public static string LogPath
        {
            get
            {
                lock (LockObj) return _logPath;
            }
        }

        public static void Configure(string dataDir)
        {
            if (string.IsNullOrWhiteSpace(dataDir))
                return;

            try
            {
                var logDir = Path.Combine(dataDir, "logs");
                Directory.CreateDirectory(logDir);
                lock (LockObj)
                {
                    _logPath = Path.Combine(logDir, "Kokonoe_System.log");
                }
                Write("SYSTEM", "central log configured");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[KokoSystemLog] configure failed: {ex.Message}");
            }
        }

        public static void Write(string source, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            var safeSource = string.IsNullOrWhiteSpace(source) ? "SYSTEM" : source.Trim();
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{safeSource}] {message.Trim()}{Environment.NewLine}";
            try
            {
                lock (LockObj)
                {
                    var dir = Path.GetDirectoryName(_logPath);
                    if (!string.IsNullOrWhiteSpace(dir))
                        Directory.CreateDirectory(dir);
                    File.AppendAllText(_logPath, line);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[KokoSystemLog] write failed: {ex.Message}");
            }
        }
    }
}
