using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace KokonoeAssistant.Services
{
    /// <summary>
    /// PC Control Service — виконання системних команд через Telegram бота.
    /// Всі команди виконуються тільки з авторизованого chatId.
    /// </summary>
    public class PcControlService
    {
        // ── Win32 ────────────────────────────────────────────────────
        [DllImport("user32.dll")]
        private static extern bool LockWorkStation();

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("winmm.dll")]
        private static extern int waveOutSetVolume(IntPtr hwo, uint dwVolume);

        [DllImport("winmm.dll")]
        private static extern int waveOutGetVolume(IntPtr hwo, out uint dwVolume);

        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_MONITORPOWER = 0xF170;

        // ── Shortcuts: назва → шлях/команда ─────────────────────────
        private readonly Dictionary<string, string> _appShortcuts = new(StringComparer.OrdinalIgnoreCase)
        {
            ["браузер"]   = "chrome",
            ["chrome"]    = "chrome",
            ["firefox"]   = "firefox",
            ["код"]       = "code",
            ["vscode"]    = "code",
            ["проводник"] = "explorer",
            ["explorer"]  = "explorer",
            ["термінал"]  = "wt",
            ["terminal"]  = "wt",
            ["telegram"]  = "telegram",
            ["спотіфай"]  = "spotify",
            ["spotify"]   = "spotify",
            ["нотатник"]  = "notepad",
            ["notepad"]   = "notepad",
        };

        // ── Screenshot ───────────────────────────────────────────────

        /// <summary>Знімає скріншот всіх екранів і повертає JPEG bytes.</summary>
        public byte[] TakeScreenshot()
        {
            var bounds = SystemInformation.VirtualScreen;
            using var bmp = new Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);

            using var ms = new MemoryStream();
            var jpegEncoder = GetEncoder(ImageFormat.Jpeg);
            var encParams = new EncoderParameters(1);
            encParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 75L);
            bmp.Save(ms, jpegEncoder, encParams);
            return ms.ToArray();
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            foreach (var codec in ImageCodecInfo.GetImageEncoders())
                if (codec.FormatID == format.Guid) return codec;
            throw new InvalidOperationException("JPEG encoder not found");
        }

        // ── System info ──────────────────────────────────────────────

        /// <summary>CPU, RAM, диск, аптайм, поточний юзер.</summary>
        public SystemInfo GetSystemInfo()
        {
            var info = new SystemInfo();

            // Uptime
            info.Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);

            // RAM
            var gcInfo = GC.GetGCMemoryInfo();
            var totalRam = gcInfo.TotalAvailableMemoryBytes;
            var usedRam  = Environment.WorkingSet;
            info.RamTotalGb = totalRam / 1024.0 / 1024.0 / 1024.0;
            info.RamUsedGb  = usedRam  / 1024.0 / 1024.0 / 1024.0;

            // Drives
            info.Drives = new List<DriveInfo_>();
            foreach (var d in DriveInfo.GetDrives())
            {
                if (!d.IsReady) continue;
                info.Drives.Add(new DriveInfo_
                {
                    Name      = d.Name,
                    FreeGb    = d.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0,
                    TotalGb   = d.TotalSize          / 1024.0 / 1024.0 / 1024.0,
                });
            }

            // User & machine
            info.UserName    = Environment.UserName;
            info.MachineName = Environment.MachineName;
            info.OsVersion   = Environment.OSVersion.VersionString;

            // Volume
            info.VolumePercent = GetVolume();

            return info;
        }

        // ── Volume ───────────────────────────────────────────────────

        public int GetVolume()
        {
            waveOutGetVolume(IntPtr.Zero, out uint vol);
            var left = (vol & 0xFFFF);
            return (int)(left * 100 / 0xFFFF);
        }

        public void SetVolume(int percent)
        {
            percent = Math.Clamp(percent, 0, 100);
            uint val = (uint)(percent * 0xFFFF / 100);
            uint packed = (val & 0xFFFF) | ((val & 0xFFFF) << 16);
            waveOutSetVolume(IntPtr.Zero, packed);
        }

        public void VolumeUp(int step = 10)   => SetVolume(GetVolume() + step);
        public void VolumeDown(int step = 10) => SetVolume(GetVolume() - step);
        public void VolumeMute()              => SetVolume(0);

        // ── Power / session ──────────────────────────────────────────

        public void LockScreen() => LockWorkStation();

        public void Sleep() =>
            Process.Start("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0");

        public void Shutdown(int delaySeconds = 0) =>
            Process.Start("shutdown", $"/s /t {delaySeconds}");

        public void Restart(int delaySeconds = 0) =>
            Process.Start("shutdown", $"/r /t {delaySeconds}");

        public void AbortShutdown() =>
            Process.Start("shutdown", "/a");

        public void TurnOffMonitor()
        {
            var hwnd = GetForegroundWindow();
            SendMessage(hwnd, WM_SYSCOMMAND, SC_MONITORPOWER, 2);
        }

        // ── Apps ─────────────────────────────────────────────────────

        /// <summary>Відкриває застосунок за ключовим словом або шляхом.</summary>
        public (bool ok, string msg) OpenApp(string nameOrPath)
        {
            var target = _appShortcuts.TryGetValue(nameOrPath.Trim(), out var mapped)
                ? mapped
                : nameOrPath.Trim();

            try
            {
                var psi = new ProcessStartInfo(target) { UseShellExecute = true };
                Process.Start(psi);
                return (true, $"Відкрила: {target}");
            }
            catch (Exception ex)
            {
                return (false, $"Не змогла відкрити '{target}': {ex.Message}");
            }
        }

        // ── Shell commands ───────────────────────────────────────────

        /// <summary>
        /// Виконує команду у PowerShell і повертає stdout+stderr.
        /// УВАГА: викликати тільки для команд що затверджені/введені власником ПК.
        /// </summary>
        public async Task<string> RunCommandAsync(string command, int timeoutMs = 10_000)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = "powershell.exe",
                    Arguments              = $"-NoProfile -NonInteractive -Command \"{EscapePs(command)}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                };

                using var proc = new Process { StartInfo = psi };
                proc.Start();

                var outTask = proc.StandardOutput.ReadToEndAsync();
                var errTask = proc.StandardError.ReadToEndAsync();

                var finished = proc.WaitForExit(timeoutMs);
                var stdout = await outTask;
                var stderr = await errTask;

                if (!finished)
                {
                    try { proc.Kill(); } catch { }
                    return "⏱ Timeout — команда перевищила ліміт часу.";
                }

                var result = (stdout + stderr).Trim();
                return string.IsNullOrEmpty(result) ? "(немає виводу)" : result[..Math.Min(3000, result.Length)];
            }
            catch (Exception ex)
            {
                return $"Помилка виконання: {ex.Message}";
            }
        }

        private static string EscapePs(string cmd) =>
            cmd.Replace("\"", "\\\"").Replace("`", "``");

        // ── Processes ────────────────────────────────────────────────

        public string GetTopProcesses()
        {
            var procs = Process.GetProcesses();
            var sb = new System.Text.StringBuilder();
            // сортуємо по WorkingSet (RAM)
            var sorted = new System.Collections.Generic.SortedList<long, string>();
            foreach (var p in procs)
            {
                try
                {
                    var mem = p.WorkingSet64;
                    // ключ має бути унікальним
                    while (sorted.ContainsKey(-mem)) mem--;
                    sorted[-mem] = $"{p.ProcessName} ({p.WorkingSet64 / 1024 / 1024} MB)";
                }
                catch { }
            }

            int count = 0;
            foreach (var kv in sorted)
            {
                sb.AppendLine(kv.Value);
                if (++count >= 15) break;
            }
            return sb.ToString().Trim();
        }

        public (bool ok, string msg) KillProcess(string nameOrPid)
        {
            if (int.TryParse(nameOrPid, out var pid))
            {
                try
                {
                    Process.GetProcessById(pid).Kill();
                    return (true, $"Процес {pid} завершено.");
                }
                catch (Exception ex) { return (false, ex.Message); }
            }
            else
            {
                var killed = 0;
                foreach (var p in Process.GetProcessesByName(nameOrPid))
                {
                    try { p.Kill(); killed++; } catch { }
                }
                return killed > 0
                    ? (true,  $"Завершено {killed} процес(ів) '{nameOrPid}'.")
                    : (false, $"Процеси '{nameOrPid}' не знайдено.");
            }
        }
    }

    // ── DTOs ─────────────────────────────────────────────────────────

    public class SystemInfo
    {
        public TimeSpan Uptime        { get; set; }
        public double   RamTotalGb    { get; set; }
        public double   RamUsedGb     { get; set; }
        public int      VolumePercent { get; set; }
        public string   UserName      { get; set; } = "";
        public string   MachineName   { get; set; } = "";
        public string   OsVersion     { get; set; } = "";
        public List<DriveInfo_> Drives { get; set; } = new();
    }

    public class DriveInfo_
    {
        public string Name    { get; set; } = "";
        public double FreeGb  { get; set; }
        public double TotalGb { get; set; }
    }
}
