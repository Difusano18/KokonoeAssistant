using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace KokonoeAssistant.Services
{
    public sealed class KokoWarmRestartWatchdogService : IDisposable
    {
        private readonly KokoServiceHeartbeatService _heartbeat;
        private readonly System.Threading.Timer _timer;
        private readonly long _maxWorkingSetBytes;
        private int _badMemoryTicks;
        private int _badUiTicks;
        private DateTime _lastRestartAttemptAt = DateTime.MinValue;

        public KokoWarmRestartWatchdogService(KokoServiceHeartbeatService heartbeat, long maxWorkingSetBytes = 2L * 1024 * 1024 * 1024)
        {
            _heartbeat = heartbeat;
            _maxWorkingSetBytes = maxWorkingSetBytes;
            _timer = new System.Threading.Timer(_ => Tick(), null, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(30));
            _heartbeat.Update("PROCESS_WATCHDOG", "armed", "warm restart threshold 2GB");
        }

        private void Tick()
        {
            try
            {
                using var proc = Process.GetCurrentProcess();
                proc.Refresh();
                var ramGb = proc.WorkingSet64 / 1024.0 / 1024.0 / 1024.0;
                var responding = proc.MainWindowHandle == IntPtr.Zero || proc.Responding;
                _heartbeat.Update("PROCESS", "ok", $"ram={ramGb:F2}GB; responding={responding}");

                _badMemoryTicks = proc.WorkingSet64 > _maxWorkingSetBytes ? _badMemoryTicks + 1 : 0;
                _badUiTicks = responding ? 0 : _badUiTicks + 1;

                if (_badMemoryTicks >= 2)
                    WarmRestart("memory over 2GB for consecutive probes");
                else if (_badUiTicks >= 3)
                    WarmRestart("UI non-responsive for consecutive probes");
            }
            catch (Exception ex)
            {
                _heartbeat.Update("PROCESS_WATCHDOG", "error", ex.Message);
                KokoSystemLog.Write("PROCESS_WATCHDOG", "tick failed: " + ex.Message);
            }
        }

        private void WarmRestart(string reason)
        {
            if (DateTime.Now - _lastRestartAttemptAt < TimeSpan.FromMinutes(10))
                return;
            _lastRestartAttemptAt = DateTime.Now;

            KokoSystemLog.Write("PROCESS_WATCHDOG", "Graceful warm restart requested: " + reason);
            _heartbeat.Update("PROCESS_WATCHDOG", "warm_restart", reason);

            try
            {
                var exe = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
                    return;
                Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(exe) ?? AppDomain.CurrentDomain.BaseDirectory
                });
                Environment.Exit(210);
            }
            catch (Exception ex)
            {
                KokoSystemLog.Write("PROCESS_WATCHDOG", "warm restart failed: " + ex.Message);
            }
        }

        public void Dispose() => _timer.Dispose();
    }
}
