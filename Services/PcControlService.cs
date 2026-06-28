using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
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

        [DllImport("user32.dll", EntryPoint = "GetForegroundWindow")]
        private static extern IntPtr GetForegroundWindowHandle();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int width, int height, bool repaint);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("winmm.dll")]
        private static extern int waveOutSetVolume(IntPtr hwo, uint dwVolume);

        [DllImport("winmm.dll")]
        private static extern int waveOutGetVolume(IntPtr hwo, out uint dwVolume);

        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_MONITORPOWER = 0xF170;
        private const int SW_RESTORE = 9;
        private const int SW_MINIMIZE = 6;

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
            ["калькулятор"] = "calc",
            ["calc"]      = "calc",
            ["powershell"] = "powershell",
            ["pwsh"]      = "pwsh",
            ["obsidian"]  = "obsidian",
            ["обсидіан"]  = "obsidian",
            ["обсидиан"]  = "obsidian",
        };

        // ── Screenshot ───────────────────────────────────────────────

        /// <summary>Знімає скріншот всіх екранів і повертає JPEG bytes.</summary>
        public byte[] TakeScreenshot(bool minimizeSelf = false, bool restoreSelf = false, int settleMs = 220)
        {
            var selfHwnd = IntPtr.Zero;
            if (minimizeSelf)
            {
                try
                {
                    selfHwnd = Process.GetCurrentProcess().MainWindowHandle;
                    if (selfHwnd != IntPtr.Zero)
                    {
                        ShowWindow(selfHwnd, SW_MINIMIZE);
                        Thread.Sleep(Math.Clamp(settleMs, 0, 1200));
                    }
                }
                catch (Exception suppressedEx123) { KokoSystemLog.Write("PCCONTROLSERVICE-CATCH", "TakeScreenshot failed near source line 123: " + suppressedEx123); }
            }

            try
            {
                return CaptureVirtualScreenJpeg();
            }
            finally
            {
                if (restoreSelf && selfHwnd != IntPtr.Zero)
                {
                    try { ShowWindow(selfHwnd, SW_RESTORE); } catch (Exception suppressedEx134) { KokoSystemLog.Write("PCCONTROLSERVICE-CATCH", "TakeScreenshot failed near source line 134: " + suppressedEx134); }
                }
            }
        }

        private static byte[] CaptureVirtualScreenJpeg()
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
            info.IdleTime = GetIdleTime();

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
            try
            {
                info.TopProcesses = CaptureTopProcessResources(10, sampleMs: 220);
                info.CpuPercent = Math.Clamp(info.TopProcesses.Sum(p => Math.Max(0, p.CpuPercent)), 0.0, 100.0);
            }
            catch (Exception suppressedEx204) { KokoSystemLog.Write("PCCONTROLSERVICE-CATCH", "GetSystemInfo failed near source line 204: " + suppressedEx204); }

            return info;
        }

        // ── Volume ───────────────────────────────────────────────────

        public int GetVolume()
        {
            waveOutGetVolume(IntPtr.Zero, out uint vol);
            var left = (vol & 0xFFFF);
            return (int)(left * 100 / 0xFFFF);
        }

        /// <summary>Повертає час бездіяльності користувача (idle time).</summary>
        public TimeSpan GetIdleTime()
        {
            var lii = new LASTINPUTINFO();
            lii.cbSize = (uint)Marshal.SizeOf(lii);
            if (GetLastInputInfo(ref lii))
            {
                // Використовуємо (uint)Environment.TickCount для коректної обробки переповнення (wrap-around) кожні 49.7 днів
                uint currentTick = (uint)Environment.TickCount;
                uint idleTicks = currentTick - lii.dwTime;
                return TimeSpan.FromMilliseconds(idleTicks);
            }
            return TimeSpan.Zero;
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
            var hwnd = GetForegroundWindowHandle();
            SendMessage(hwnd, WM_SYSCOMMAND, SC_MONITORPOWER, 2);
        }

        public ForegroundWindowInfo GetForegroundWindow()
        {
            var info = new ForegroundWindowInfo();
            try
            {
                var hwnd = GetForegroundWindowHandle();
                info.Handle = hwnd.ToInt64();
                if (hwnd == IntPtr.Zero)
                    return info;

                var title = new StringBuilder(512);
                if (GetWindowText(hwnd, title, title.Capacity) > 0)
                    info.Title = title.ToString();

                var cls = new StringBuilder(256);
                if (GetClassName(hwnd, cls, cls.Capacity) > 0)
                    info.ClassName = cls.ToString();

                GetWindowThreadProcessId(hwnd, out var pid);
                info.ProcessId = (int)pid;
                if (pid > 0)
                {
                    try
                    {
                        using var proc = Process.GetProcessById((int)pid);
                        info.ProcessName = proc.ProcessName;
                    }
                    catch (Exception suppressedEx294) { KokoSystemLog.Write("PCCONTROLSERVICE-CATCH", "GetForegroundWindow failed near source line 294: " + suppressedEx294); }
                }
            }
            catch (Exception ex)
            {
                info.Error = ex.Message;
            }

            return info;
        }

        public PcContextSnapshot GetAllContext(int maxWindows = 40)
        {
            var snapshot = new PcContextSnapshot
            {
                TakenAt = DateTime.Now
            };

            try { snapshot.Foreground = GetForegroundWindow(); }
            catch (Exception ex) { snapshot.Errors.Add("foreground: " + ex.Message); }

            try { snapshot.System = GetSystemInfo(); }
            catch (Exception ex) { snapshot.Errors.Add("system: " + ex.Message); }

            try
            {
                snapshot.VisibleWindows = ListVisibleWindows(Math.Clamp(maxWindows, 5, 100));
                snapshot.BrowserWindows = snapshot.VisibleWindows
                    .Where(IsBrowserWindow)
                    .Take(30)
                    .ToList();
            }
            catch (Exception ex)
            {
                snapshot.Errors.Add("windows: " + ex.Message);
            }

            return snapshot;
        }

        public PcContextSnapshotV2 GetContextV2(PcObservationMode mode, int maxWindows = 40)
        {
            var snapshot = new PcContextSnapshotV2
            {
                TakenAt = DateTime.Now,
                ObservationMode = mode
            };

            SystemInfo? system = null;
            try
            {
                system = GetSystemInfo();
                snapshot.Identity = new PcIdentityContext
                {
                    MachineName = system.MachineName,
                    UserName = PcContextRedactor.RedactText(system.UserName),
                    OsVersion = system.OsVersion,
                    Uptime = system.Uptime
                };
                snapshot.Presence = new PcPresenceContext
                {
                    IdleTime = system.IdleTime
                };
                snapshot.Resources = new PcResourceContext
                {
                    CpuPercent = system.CpuPercent,
                    RamTotalGb = system.RamTotalGb,
                    RamUsedGb = system.RamUsedGb,
                    VolumePercent = system.VolumePercent,
                    Drives = system.Drives
                        .Take(12)
                        .Select(d => new DriveInfo_ { Name = PcContextRedactor.RedactText(d.Name), FreeGb = d.FreeGb, TotalGb = d.TotalGb })
                        .ToList(),
                    TopProcesses = system.TopProcesses
                        .Take(8)
                        .Select(p => new ProcessResourceInfo
                        {
                            ProcessName = PcContextRedactor.RedactText(p.ProcessName),
                            ProcessId = p.ProcessId,
                            MemoryMb = p.MemoryMb,
                            CpuPercent = p.CpuPercent
                        })
                        .ToList()
                };
            }
            catch (Exception ex)
            {
                snapshot.Errors.Add("system: " + ex.Message);
            }

            var rawForeground = new ForegroundWindowInfo();
            try
            {
                rawForeground = GetForegroundWindow();
                snapshot.Foreground = PcContextRedactor.RedactForeground(rawForeground);
            }
            catch (Exception ex)
            {
                snapshot.Errors.Add("foreground: " + ex.Message);
            }

            var rawWindows = new List<WindowSummary>();
            if (mode >= PcObservationMode.Normal)
            {
                try
                {
                    rawWindows = ListVisibleWindows(Math.Clamp(maxWindows, 5, 100));
                    snapshot.VisibleWindows = rawWindows
                        .Select(PcContextRedactor.RedactWindow)
                        .Take(Math.Clamp(maxWindows, 5, 100))
                        .ToList();
                    snapshot.BrowserWindows = rawWindows
                        .Where(IsBrowserWindow)
                        .Take(30)
                        .Select(PcContextRedactor.RedactWindow)
                        .ToList();
                }
                catch (Exception ex)
                {
                    snapshot.Errors.Add("windows: " + ex.Message);
                }
            }

            snapshot.Privacy = PcContextRedactor.AssessPrivacy(rawForeground, rawWindows);
            snapshot.Privacy.ScreenObservationAllowed = mode == PcObservationMode.Deep && !snapshot.Privacy.IsSensitive;
            if (mode == PcObservationMode.Deep && !snapshot.Privacy.ScreenObservationAllowed && string.IsNullOrWhiteSpace(snapshot.Privacy.SensitivityReason))
                snapshot.Privacy.SensitivityReason = "deep screen awareness blocked by privacy policy";

            snapshot.Workspace = PcContextRedactor.ClassifyWorkspace(snapshot.Foreground, snapshot.VisibleWindows);
            return snapshot;
        }

        private static List<WindowSummary> ListVisibleWindows(int maxWindows)
        {
            var windows = new List<WindowSummary>();
            EnumWindows((hWnd, _) =>
            {
                if (windows.Count >= maxWindows)
                    return false;

                try
                {
                    if (hWnd == IntPtr.Zero || !IsWindowVisible(hWnd))
                        return true;

                    var titleBuilder = new StringBuilder(512);
                    GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
                    var title = titleBuilder.ToString().Trim();
                    if (string.IsNullOrWhiteSpace(title))
                        return true;

                    var classBuilder = new StringBuilder(256);
                    GetClassName(hWnd, classBuilder, classBuilder.Capacity);

                    GetWindowThreadProcessId(hWnd, out var pid);
                    var processName = "";
                    if (pid > 0)
                    {
                        try
                        {
                            using var proc = Process.GetProcessById((int)pid);
                            processName = proc.ProcessName;
                        }
                        catch (Exception suppressedEx457) { KokoSystemLog.Write("PCCONTROLSERVICE-CATCH", "ListVisibleWindows failed near source line 457: " + suppressedEx457); }
                    }

                    windows.Add(new WindowSummary
                    {
                        Handle = hWnd.ToInt64(),
                        ProcessId = (int)pid,
                        ProcessName = processName,
                        Title = title,
                        ClassName = classBuilder.ToString().Trim()
                    });
                }
                catch (Exception suppressedEx469) { KokoSystemLog.Write("PCCONTROLSERVICE-CATCH", "ListVisibleWindows failed near source line 469: " + suppressedEx469); }

                return true;
            }, IntPtr.Zero);

            return windows
                .OrderByDescending(w => IsBrowserWindow(w))
                .ThenBy(w => w.ProcessName)
                .ThenBy(w => w.Title)
                .Take(maxWindows)
                .ToList();
        }

        private static bool IsBrowserWindow(WindowSummary window)
            => ContainsAny(window.ProcessName,
                "chrome", "msedge", "firefox", "brave", "opera", "vivaldi", "browser")
               || ContainsAny(window.ClassName, "Chrome_WidgetWin", "MozillaWindowClass");

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
                // Launching "chrome" with no arguments and multiple profiles on the machine
                // makes Chrome show its own profile picker instead of opening anything - the
                // user has a Chrome profile literally named "Kokonoe" for this. Resolve its
                // internal directory (Chrome profiles are stored as "Profile 1", "Profile 7",
                // etc. - the display name only lives inside Local State) and launch straight
                // into it; falls back to the unchanged bare launch if that profile isn't found.
                if (target.Equals("chrome", StringComparison.OrdinalIgnoreCase))
                {
                    var profileDir = ResolveChromeProfileDirectory("Kokonoe");
                    if (!string.IsNullOrEmpty(profileDir))
                        psi.Arguments = $"--profile-directory=\"{profileDir}\"";
                }
                Process.Start(psi);
                return (true, $"Відкрила: {target}");
            }
            catch (Exception ex)
            {
                return (false, $"Не змогла відкрити '{target}': {ex.Message}");
            }
        }

        private static string? ResolveChromeProfileDirectory(string profileDisplayName)
        {
            try
            {
                var localStatePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Google", "Chrome", "User Data", "Local State");
                if (!File.Exists(localStatePath))
                    return null;

                var json = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(localStatePath));
                if (json["profile"]?["info_cache"] is not Newtonsoft.Json.Linq.JObject infoCache)
                    return null;

                foreach (var entry in infoCache.Properties())
                {
                    var name = entry.Value["name"]?.ToString();
                    if (string.Equals(name, profileDisplayName, StringComparison.OrdinalIgnoreCase))
                        return entry.Name;
                }
            }
            catch (Exception ex)
            {
                KokoSystemLog.Write("PCCONTROLSERVICE-CATCH", "ResolveChromeProfileDirectory failed: " + ex.Message);
            }
            return null;
        }

        // ── Shell commands ───────────────────────────────────────────

        /// <summary>
        /// Виконує команду у PowerShell і повертає stdout+stderr.
        /// УВАГА: викликати тільки для команд що затверджені/введені власником ПК.
        /// </summary>
        public WorkspaceScenarioResult RunWorkspaceScenario(string request, bool dryRun = false)
        {
            var scenario = ResolveWorkspaceScenario(request);
            var result = new WorkspaceScenarioResult
            {
                Scenario = scenario.Name,
                WorkMode = scenario.WorkMode,
                RequestedAt = DateTime.Now
            };

            foreach (var app in scenario.Apps)
            {
                var opened = dryRun ? (ok: true, msg: "dry-run open app") : OpenApp(app);
                result.Actions.Add(new WorkspaceScenarioAction
                {
                    Kind = "open-app",
                    Target = app,
                    Succeeded = opened.ok,
                    Message = opened.msg
                });
                Thread.Sleep(120);
            }

            foreach (var note in scenario.Notes)
            {
                var opened = dryRun ? (ok: true, msg: "dry-run open note") : OpenObsidianNote(note);
                result.Actions.Add(new WorkspaceScenarioAction
                {
                    Kind = "open-note",
                    Target = note,
                    Succeeded = opened.ok,
                    Message = opened.msg
                });
            }

            if (scenario.Arrange)
            {
                var arranged = dryRun
                    ? new WindowActionResult { Succeeded = true, Message = "dry-run arrange windows" }
                    : ArrangeWorkspaceWindows(scenario.WorkMode);
                result.Actions.Add(new WorkspaceScenarioAction
                {
                    Kind = "arrange-windows",
                    Target = scenario.WorkMode,
                    Succeeded = arranged.Succeeded,
                    Message = arranged.Message
                });
            }

            return result;
        }

        public (bool ok, string msg) OpenObsidianNote(string path)
        {
            try
            {
                var clean = string.IsNullOrWhiteSpace(path)
                    ? "Kokonoe/Agent/Insights.md"
                    : path.Trim().Trim('"');
                var uri = "obsidian://open?path=" + Uri.EscapeDataString(clean.Replace('\\', '/'));
                Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
                return (true, $"Opened Obsidian note: {clean}");
            }
            catch (Exception ex)
            {
                var app = OpenApp("obsidian");
                return app.ok
                    ? (true, "Opened Obsidian; note URI failed: " + ex.Message)
                    : (false, "Obsidian note open failed: " + ex.Message);
            }
        }

        private static WorkspaceScenario ResolveWorkspaceScenario(string? request)
        {
            var lower = (request ?? "").ToLowerInvariant();
            if (ContainsAny(lower, "game", "gaming", "гра", "ігри", "игры", "пограти"))
            {
                return new WorkspaceScenario
                {
                    Name = "gaming",
                    WorkMode = "Gaming",
                    Apps = new[] { "spotify", "telegram" },
                    Notes = new[] { "Kokonoe/Memory/Screen Patterns.md" },
                    Arrange = false
                };
            }

            if (ContainsAny(lower, "obsidian", "vault", "notes", "нотат", "замет"))
            {
                return new WorkspaceScenario
                {
                    Name = "vault-review",
                    WorkMode = "Vault",
                    Apps = new[] { "obsidian" },
                    Notes = new[] { "Kokonoe/Agent/Insights.md", "Kokonoe/Memory/Screen Patterns.md" },
                    Arrange = true
                };
            }

            return new WorkspaceScenario
            {
                Name = "coding",
                WorkMode = "Coding",
                Apps = new[] { "code", "wt", "obsidian" },
                Notes = new[] { "Architecture/MANUS_ARCHITECT_RULES.md", "ProjectMemory/CURRENT_STATE.md" },
                Arrange = true
            };
        }

        public async Task<string> RunCommandAsync(string command, int timeoutMs = 10_000, bool enforceSafety = false)
        {
            try
            {
                var detailed = await RunCommandDetailedAsync(command, timeoutMs, enforceSafety, CancellationToken.None)
                    .ConfigureAwait(false);
                if (detailed.Blocked)
                    return "Command blocked: " + detailed.Error;
                if (detailed.TimedOut)
                    return "Timeout: command exceeded the time limit.";
                var combined = (detailed.Output + "\n" + detailed.Error).Trim();
                if (string.IsNullOrWhiteSpace(combined))
                    combined = "(no output)";
                if (detailed.ExitCode != 0)
                    combined = $"exit={detailed.ExitCode}\n{combined}".Trim();
                return combined[..Math.Min(3000, combined.Length)];
            }
            catch (Exception ex)
            {
                return $"Execution error: {ex.Message}";
            }
        }

        private static string EscapePs(string cmd) =>
            cmd.Replace("\"", "\\\"").Replace("`", "``");

        // ── Processes ────────────────────────────────────────────────

        public async Task<ShellCommandChainResult> RunCommandChainAsync(
            string chain,
            int timeoutPerStepMs = 120_000,
            CancellationToken ct = default)
        {
            var commands = SplitCommandChain(chain);
            var result = new ShellCommandChainResult { RequestedAt = DateTime.Now };

            if (commands.Count == 0)
            {
                result.Summary = "No commands found in chain.";
                return result;
            }

            for (var i = 0; i < commands.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var step = await RunCommandDetailedAsync(commands[i], timeoutPerStepMs, enforceSafety: true, ct)
                    .ConfigureAwait(false);
                step.Order = i + 1;
                result.Steps.Add(step);
                if (!step.Succeeded)
                    break;
            }

            result.Summary = BuildChainSummary(result);
            return result;
        }

        private async Task<ShellCommandStepResult> RunCommandDetailedAsync(
            string command,
            int timeoutMs,
            bool enforceSafety,
            CancellationToken ct)
        {
            var step = new ShellCommandStepResult { Command = command };
            if (enforceSafety && PcCommandSafety.IsBlocked(command, out var reason))
            {
                step.Blocked = true;
                step.Error = reason;
                return step;
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(Math.Max(1000, timeoutMs));

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -Command \"{EscapePs(command)}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = new Process { StartInfo = psi };
            proc.Start();

            var outTask = proc.StandardOutput.ReadToEndAsync();
            var errTask = proc.StandardError.ReadToEndAsync();

            try
            {
                await proc.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                step.TimedOut = true;
                try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch (Exception suppressedEx720) { KokoSystemLog.Write("PCCONTROLSERVICE-CATCH", "RunCommandDetailedAsync failed near source line 720: " + suppressedEx720); }
            }

            step.Output = (await outTask.ConfigureAwait(false)).Trim();
            step.Error = (await errTask.ConfigureAwait(false)).Trim();
            step.ExitCode = step.TimedOut ? -1 : proc.ExitCode;
            return step;
        }

        private static List<string> SplitCommandChain(string? chain)
        {
            if (string.IsNullOrWhiteSpace(chain))
                return new List<string>();
            var text = Regex.Replace(
                chain.Trim(),
                @"^\s*(?:chain|pipeline|commands?|команди|ланцюжок)\s*[:\-]\s*",
                "",
                RegexOptions.IgnoreCase);
            return Regex.Split(text, @"\s*(?:->|&&|\r?\n)\s*")
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }

        private static string BuildChainSummary(ShellCommandChainResult result)
        {
            if (result.Steps.Count == 0)
                return "No commands executed.";

            var failed = result.Steps.FirstOrDefault(s => !s.Succeeded);
            if (failed == null)
                return $"Shell chain completed: {result.Steps.Count}/{result.Steps.Count} steps passed.";

            var reason = failed.Blocked
                ? "blocked by PcCommandSafety"
                : failed.TimedOut
                    ? "timed out"
                    : $"exit code {failed.ExitCode}";
            var tail = string.IsNullOrWhiteSpace(failed.Error) ? failed.Output : failed.Error;
            return $"Shell chain stopped at step {failed.Order}: {reason}. {TrimLine(tail, 260)}";
        }

        public WindowActionResult FocusWindow(string query)
        {
            var window = FindWindow(query);
            if (window.Handle == IntPtr.Zero)
            {
                return new WindowActionResult
                {
                    Action = "focus",
                    Query = query,
                    Succeeded = false,
                    Message = $"No matching window for '{query}'."
                };
            }

            try
            {
                ShowWindow(window.Handle, SW_RESTORE);
                var ok = SetForegroundWindow(window.Handle);
                return new WindowActionResult
                {
                    Action = "focus",
                    Query = query,
                    Succeeded = ok,
                    Message = ok
                        ? $"Focused {window.ProcessName}: {window.Title}"
                        : $"Window found but focus was refused by Windows: {window.Title}"
                };
            }
            catch (Exception ex)
            {
                return new WindowActionResult { Action = "focus", Query = query, Succeeded = false, Message = ex.Message };
            }
        }

        public WindowActionResult ArrangeWorkspaceWindows(string layoutOrMode)
        {
            try
            {
                var windows = FindCandidateWindows(layoutOrMode).Take(4).ToList();
                if (windows.Count == 0)
                {
                    return new WindowActionResult
                    {
                        Action = "arrange",
                        Query = layoutOrMode,
                        Succeeded = false,
                        Message = "No matching windows to arrange."
                    };
                }

                var area = Screen.PrimaryScreen?.WorkingArea ?? SystemInformation.VirtualScreen;
                if (windows.Count == 1)
                {
                    MoveWindow(windows[0].Handle, area.Left, area.Top, area.Width, area.Height, true);
                }
                else if (windows.Count == 2)
                {
                    MoveWindow(windows[0].Handle, area.Left, area.Top, area.Width / 2, area.Height, true);
                    MoveWindow(windows[1].Handle, area.Left + area.Width / 2, area.Top, area.Width / 2, area.Height, true);
                }
                else
                {
                    var leftWidth = (int)(area.Width * 0.58);
                    MoveWindow(windows[0].Handle, area.Left, area.Top, leftWidth, area.Height, true);
                    var rightX = area.Left + leftWidth;
                    var rightW = area.Width - leftWidth;
                    var rowH = area.Height / Math.Max(1, windows.Count - 1);
                    for (var i = 1; i < windows.Count; i++)
                        MoveWindow(windows[i].Handle, rightX, area.Top + rowH * (i - 1), rightW, rowH, true);
                }

                return new WindowActionResult
                {
                    Action = "arrange",
                    Query = layoutOrMode,
                    Succeeded = true,
                    Message = $"Arranged {windows.Count} window(s) for {layoutOrMode}."
                };
            }
            catch (Exception ex)
            {
                return new WindowActionResult { Action = "arrange", Query = layoutOrMode, Succeeded = false, Message = ex.Message };
            }
        }

        private static WindowMatch FindWindow(string? query)
        {
            var q = (query ?? "").Trim().ToLowerInvariant();
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.MainWindowHandle == IntPtr.Zero)
                        continue;
                    var title = p.MainWindowTitle ?? "";
                    if (string.IsNullOrWhiteSpace(q) ||
                        p.ProcessName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                        title.Contains(q, StringComparison.OrdinalIgnoreCase))
                        return new WindowMatch(p.MainWindowHandle, p.ProcessName, title);
                }
                catch (Exception suppressedEx862) { KokoSystemLog.Write("PCCONTROLSERVICE-CATCH", "FindWindow failed near source line 862: " + suppressedEx862); }
                finally { try { p.Dispose(); } catch (Exception suppressedEx863) { KokoSystemLog.Write("PCCONTROLSERVICE-CATCH", "FindWindow failed near source line 863: " + suppressedEx863); } }
            }
            return default;
        }

        private static List<WindowMatch> FindCandidateWindows(string? layoutOrMode)
        {
            var q = (layoutOrMode ?? "").ToLowerInvariant();
            var desired = ContainsAny(q, "gaming", "game", "гра")
                ? new[] { "steam", "discord", "spotify", "telegram", "chrome", "msedge", "firefox" }
                : ContainsAny(q, "vault", "obsidian", "notes")
                    ? new[] { "obsidian", "code", "wt", "windowsterminal", "powershell" }
                    : new[] { "code", "devenv", "rider", "wt", "windowsterminal", "powershell", "obsidian" };

            var matches = new List<WindowMatch>();
            foreach (var name in desired)
            {
                var match = FindWindow(name);
                if (match.Handle != IntPtr.Zero && matches.All(m => m.Handle != match.Handle))
                    matches.Add(match);
            }
            return matches;
        }

        public string GetTopProcesses()
        {
            var resources = CaptureTopProcessResources(15, sampleMs: 0);
            if (resources.Count > 0)
                return string.Join(Environment.NewLine, resources.Select(p => $"{p.ProcessName} ({p.MemoryMb:F0} MB, cpu {p.CpuPercent:F1}%)"));

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
                catch (Exception suppressedEx906) { KokoSystemLog.Write("PCCONTROLSERVICE-CATCH", "GetTopProcesses failed near source line 906: " + suppressedEx906); }
            }

            int count = 0;
            foreach (var kv in sorted)
            {
                sb.AppendLine(kv.Value);
                if (++count >= 15) break;
            }
            return sb.ToString().Trim();
        }

        private static List<ProcessResourceInfo> CaptureTopProcessResources(int count, int sampleMs)
        {
            var first = new Dictionary<int, TimeSpan>();
            var sw = Stopwatch.StartNew();
            foreach (var p in Process.GetProcesses())
            {
                // Protected/system processes (Registry, Secure System, csrss, ...) always deny
                // TotalProcessorTime without elevated privileges — that's expected on every
                // call, not a failure. Logging it here used to write a full stack trace per
                // process per call, from a method invoked by several background timers, which
                // ran the log file to multiple gigabytes and serialized every other thread
                // behind KokoSystemLog's lock.
                try { first[p.Id] = p.TotalProcessorTime; }
                catch (System.ComponentModel.Win32Exception) { }
                catch (InvalidOperationException) { }
                catch (Exception suppressedEx925) { KokoSystemLog.Write("PCCONTROLSERVICE-CATCH", "CaptureTopProcessResources failed near source line 925: " + suppressedEx925); }
                finally { try { p.Dispose(); } catch (Exception suppressedEx926) { KokoSystemLog.Write("PCCONTROLSERVICE-CATCH", "CaptureTopProcessResources failed near source line 926: " + suppressedEx926); } }
            }

            if (sampleMs > 0)
                Thread.Sleep(sampleMs);
            sw.Stop();
            var elapsedMs = Math.Max(1, sw.Elapsed.TotalMilliseconds);
            var cpuDenominator = elapsedMs * Math.Max(1, Environment.ProcessorCount);

            var list = new List<ProcessResourceInfo>();
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    var cpu = 0.0;
                    if (first.TryGetValue(p.Id, out var before))
                    {
                        var delta = (p.TotalProcessorTime - before).TotalMilliseconds;
                        cpu = Math.Clamp(delta / cpuDenominator * 100.0, 0.0, 100.0);
                    }

                    list.Add(new ProcessResourceInfo
                    {
                        ProcessName = p.ProcessName,
                        ProcessId = p.Id,
                        MemoryMb = p.WorkingSet64 / 1024.0 / 1024.0,
                        CpuPercent = cpu
                    });
                }
                catch (System.ComponentModel.Win32Exception) { }
                catch (InvalidOperationException) { }
                catch (Exception suppressedEx955) { KokoSystemLog.Write("PCCONTROLSERVICE-CATCH", "CaptureTopProcessResources failed near source line 955: " + suppressedEx955); }
                finally { try { p.Dispose(); } catch (Exception suppressedEx956) { KokoSystemLog.Write("PCCONTROLSERVICE-CATCH", "CaptureTopProcessResources failed near source line 956: " + suppressedEx956); } }
            }

            return list
                .OrderByDescending(p => p.CpuPercent)
                .ThenByDescending(p => p.MemoryMb)
                .Take(Math.Max(1, count))
                .ToList();
        }

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));

        private static string TrimLine(string? text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            var clean = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return clean.Length <= max ? clean : clean[..Math.Max(0, max - 3)] + "...";
        }

        private readonly record struct WindowMatch(IntPtr Handle, string ProcessName, string Title);

        private sealed class WorkspaceScenario
        {
            public string Name { get; init; } = "coding";
            public string WorkMode { get; init; } = "Coding";
            public IReadOnlyList<string> Apps { get; init; } = Array.Empty<string>();
            public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
            public bool Arrange { get; init; }
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
                    try { p.Kill(); killed++; } catch (Exception suppressedEx1003) { KokoSystemLog.Write("PCCONTROLSERVICE-CATCH", "KillProcess failed near source line 1003: " + suppressedEx1003); }
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
        public TimeSpan IdleTime      { get; set; }
        public double   RamTotalGb    { get; set; }
        public double   RamUsedGb     { get; set; }
        public double   CpuPercent    { get; set; }
        public int      VolumePercent { get; set; }
        public string   UserName      { get; set; } = "";
        public string   MachineName   { get; set; } = "";
        public string   OsVersion     { get; set; } = "";
        public List<DriveInfo_> Drives { get; set; } = new();
        public List<ProcessResourceInfo> TopProcesses { get; set; } = new();
    }

    public class DriveInfo_
    {
        public string Name    { get; set; } = "";
        public double FreeGb  { get; set; }
        public double TotalGb { get; set; }
    }

    public class ForegroundWindowInfo
    {
        public long Handle { get; set; }
        public string Title { get; set; } = "";
        public string ClassName { get; set; } = "";
        public string ProcessName { get; set; } = "";
        public int ProcessId { get; set; }
        public string Error { get; set; } = "";

        public bool HasWindow => Handle != 0;

        public override string ToString()
        {
            if (!string.IsNullOrWhiteSpace(Error))
                return "foreground error: " + Error;
            var process = string.IsNullOrWhiteSpace(ProcessName) ? "unknown" : ProcessName;
            var title = string.IsNullOrWhiteSpace(Title) ? "untitled" : Title;
            var cls = string.IsNullOrWhiteSpace(ClassName) ? "unknown-class" : ClassName;
            return $"{process}#{ProcessId} | {cls} | {title}";
        }
    }

    public class ProcessResourceInfo
    {
        public string ProcessName { get; set; } = "";
        public int ProcessId { get; set; }
        public double MemoryMb { get; set; }
        public double CpuPercent { get; set; }
    }

    public class WindowSummary
    {
        public long Handle { get; set; }
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = "";
        public string Title { get; set; } = "";
        public string ClassName { get; set; } = "";

        public override string ToString()
        {
            var proc = string.IsNullOrWhiteSpace(ProcessName) ? "unknown" : ProcessName;
            var title = string.IsNullOrWhiteSpace(Title) ? "untitled" : Title;
            return $"{proc}#{ProcessId}: {title}";
        }
    }

    public class PcContextSnapshot
    {
        public DateTime TakenAt { get; set; } = DateTime.Now;
        public ForegroundWindowInfo Foreground { get; set; } = new();
        public SystemInfo System { get; set; } = new();
        public List<WindowSummary> BrowserWindows { get; set; } = new();
        public List<WindowSummary> VisibleWindows { get; set; } = new();
        public List<string> Errors { get; set; } = new();

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"PC context snapshot: {TakenAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("Active window: " + Foreground);
            sb.AppendLine($"System: CPU {System.CpuPercent:F1}% | RAM {System.RamUsedGb:F1}/{System.RamTotalGb:F1} GB | volume {System.VolumePercent}% | idle {System.IdleTime.TotalMinutes:F1}m");
            if (System.TopProcesses.Count > 0)
                sb.AppendLine("Top processes: " + string.Join(", ", System.TopProcesses.Take(5).Select(p => $"{p.ProcessName} {p.MemoryMb:F0}MB/{p.CpuPercent:F1}%")));

            sb.AppendLine("Browser windows/tabs:");
            if (BrowserWindows.Count == 0)
                sb.AppendLine("- none detected");
            else
                foreach (var window in BrowserWindows.Take(12))
                    sb.AppendLine("- " + window);

            sb.AppendLine("Visible windows:");
            foreach (var window in VisibleWindows.Take(12))
                sb.AppendLine("- " + window);

            if (Errors.Count > 0)
                sb.AppendLine("Context errors: " + string.Join("; ", Errors));

            return sb.ToString().Trim();
        }
    }

    public class ShellCommandStepResult
    {
        public int Order { get; set; }
        public string Command { get; set; } = "";
        public int ExitCode { get; set; }
        public bool Blocked { get; set; }
        public bool TimedOut { get; set; }
        public string Output { get; set; } = "";
        public string Error { get; set; } = "";
        public bool Succeeded => !Blocked && !TimedOut && ExitCode == 0;
    }

    public class ShellCommandChainResult
    {
        public DateTime RequestedAt { get; set; }
        public List<ShellCommandStepResult> Steps { get; set; } = new();
        public string Summary { get; set; } = "";
        public bool Succeeded => Steps.Count > 0 && Steps.All(s => s.Succeeded);
    }

    public class WorkspaceScenarioAction
    {
        public string Kind { get; set; } = "";
        public string Target { get; set; } = "";
        public bool Succeeded { get; set; }
        public string Message { get; set; } = "";
    }

    public class WorkspaceScenarioResult
    {
        public string Scenario { get; set; } = "";
        public string WorkMode { get; set; } = "";
        public DateTime RequestedAt { get; set; }
        public List<WorkspaceScenarioAction> Actions { get; set; } = new();
        public bool Succeeded => Actions.Count > 0 && Actions.Any(a => a.Succeeded);

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Scenario {Scenario} ({WorkMode})");
            foreach (var action in Actions)
                sb.AppendLine($"- {action.Kind}: {action.Target} => {(action.Succeeded ? "ok" : "fail")} | {action.Message}");
            return sb.ToString().Trim();
        }
    }

    public class WindowActionResult
    {
        public string Action { get; set; } = "";
        public string Query { get; set; } = "";
        public bool Succeeded { get; set; }
        public string Message { get; set; } = "";
    }
}
