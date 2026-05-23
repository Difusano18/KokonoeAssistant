using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace KokonoeAssistant.Services
{
    public enum PcIntentAction
    {
        None,
        SystemInfo,
        Processes,
        VolumeUp,
        VolumeDown,
        VolumeMute,
        VolumeSet,
        OpenApp,
        KillProcess,
        LockScreen,
        Sleep,
        MonitorOff,
        Shutdown,
        Restart,
        AbortShutdown,
        RunPowerShell,
        RunShellChain,
        WorkspaceScenario,
        FocusWindow,
        ArrangeWindows
    }

    public sealed class PcIntentParseResult
    {
        public bool Handled { get; init; }
        public PcIntentAction Action { get; init; }
        public string Argument { get; init; } = "";
        public int? Number { get; init; }
        public bool RequiresConfirmation { get; init; }
        public string ConfirmationText { get; init; } = "";
    }

    public sealed class PcIntentExecutionResult
    {
        public bool Handled { get; init; }
        public string Reply { get; init; } = "";
        public PcIntentAction Action { get; init; }
        public bool RequiresConfirmation { get; init; }
    }

    public static class PcIntentRouter
    {
        private static readonly Regex ShellPrefixRegex = new(
            @"^\s*(?:ps|powershell|pwsh|shell|команда|команду)\s*[:\-]\s*(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ShellVerbRegex = new(
            @"(?:виконай|запусти|run|execute)\s+(?:ps|powershell|pwsh|shell|команду|команда)\s*[:\-]\s*(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ShellChainRegex = new(
            @"^\s*(?:chain|pipeline|commands?|команди|ланцюжок)\s*[:\-]\s*(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex FocusWindowRegex = new(
            @"(?:focus|switch to|перемкни(?:сь)? на|фокус(?:уй)?|активуй вікно)\s+(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ArrangeWindowsRegex = new(
            @"(?:arrange|tile|розстав|упорядкуй|наведи лад).*(?:windows|вікна|окна)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex VolumeNumberRegex = new(
            @"(?:гучн(?:ість|ость)|volume|звук)\D{0,12}(\d{1,3})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex KillRegex = new(
            @"(?:kill|прибий|заверши|закрий|вбий)\s+(?:процес\s+)?(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex OpenRegex = new(
            @"(?:відкрий|відкрити|запусти|запустить|open|start)\s+(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly string[] AppAliases =
        {
            "chrome", "браузер", "firefox", "code", "vscode", "код",
            "explorer", "проводник", "провідник", "telegram", "spotify",
            "спотіфай", "notepad", "нотатник", "calc", "калькулятор",
            "terminal", "термінал", "powershell", "pwsh"
        };

        public static PcIntentParseResult Parse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return NoMatch();

            var original = text.Trim();
            var lower = original.ToLowerInvariant();

            if (KokoScreenIntent.IsManualScreenScan(original))
                return NoMatch();

            var chain = ShellChainRegex.Match(original);
            if (chain.Success)
            {
                return new PcIntentParseResult
                {
                    Handled = true,
                    Action = PcIntentAction.RunShellChain,
                    Argument = chain.Groups[1].Value.Trim()
                };
            }

            if (LooksLikeWorkspaceScenario(lower))
            {
                return new PcIntentParseResult
                {
                    Handled = true,
                    Action = PcIntentAction.WorkspaceScenario,
                    Argument = original
                };
            }

            var focus = FocusWindowRegex.Match(original);
            if (focus.Success)
            {
                return new PcIntentParseResult
                {
                    Handled = true,
                    Action = PcIntentAction.FocusWindow,
                    Argument = CleanTarget(focus.Groups[1].Value)
                };
            }

            if (ArrangeWindowsRegex.IsMatch(original))
            {
                return new PcIntentParseResult
                {
                    Handled = true,
                    Action = PcIntentAction.ArrangeWindows,
                    Argument = original
                };
            }

            var shell = TryExtractShellCommand(original);
            if (!string.IsNullOrWhiteSpace(shell))
            {
                return new PcIntentParseResult
                {
                    Handled = true,
                    Action = PcIntentAction.RunPowerShell,
                    Argument = shell.Trim()
                };
            }

            if (ContainsAny(lower, "скасуй вимкнення", "abort shutdown", "shutdown /a"))
                return Match(PcIntentAction.AbortShutdown);

            if (WantsSystemInfo(lower))
                return Match(PcIntentAction.SystemInfo);

            if (WantsProcesses(lower))
                return Match(PcIntentAction.Processes);

            var volumeNumber = VolumeNumberRegex.Match(original);
            if (volumeNumber.Success && int.TryParse(volumeNumber.Groups[1].Value, out var volume))
                return new PcIntentParseResult { Handled = true, Action = PcIntentAction.VolumeSet, Number = Math.Clamp(volume, 0, 100) };

            if (ContainsAny(lower, "mute", "без звуку", "зам'ють", "замють", "вимкни звук", "звук 0"))
                return Match(PcIntentAction.VolumeMute);
            if (ContainsAny(lower, "голосніше", "додай гучність", "збільш гучність", "volume up", "sound up"))
                return Match(PcIntentAction.VolumeUp);
            if (ContainsAny(lower, "тихіше", "зменш гучність", "приглуш", "volume down", "sound down"))
                return Match(PcIntentAction.VolumeDown);

            if (ContainsAny(lower, "заблокуй пк", "заблокуй комп", "lock pc", "lock workstation", "lock windows"))
                return Match(PcIntentAction.LockScreen);

            if (ContainsAny(lower, "вимкни монітор", "вимкни монитор", "turn off monitor", "monitor off", "погаси екран", "погаси экран"))
                return Match(PcIntentAction.MonitorOff);

            if (WantsSleep(lower))
                return Match(PcIntentAction.Sleep);

            if (WantsShutdown(lower))
            {
                return new PcIntentParseResult
                {
                    Handled = true,
                    Action = PcIntentAction.Shutdown,
                    RequiresConfirmation = true,
                    ConfirmationText = "Вимкнення ПК потребує окремого підтвердження через /pc. Shell-route навмисно блокує shutdown, бо втрачати сесію через випадкову фразу - тупий спорт."
                };
            }

            if (WantsRestart(lower))
            {
                return new PcIntentParseResult
                {
                    Handled = true,
                    Action = PcIntentAction.Restart,
                    RequiresConfirmation = true,
                    ConfirmationText = "Рестарт потребує окремого підтвердження через /pc. Shell-route навмисно блокує reboot/shutdown, щоб не вбити роботу випадковою реплікою."
                };
            }

            var kill = KillRegex.Match(original);
            if (kill.Success)
            {
                var target = CleanTarget(kill.Groups[1].Value);
                if (!string.IsNullOrWhiteSpace(target) && !LooksLikeDocumentRequest(target))
                    return new PcIntentParseResult
                    {
                        Handled = true,
                        Action = PcIntentAction.KillProcess,
                        Argument = target,
                        RequiresConfirmation = true,
                        ConfirmationText = $"Process stop requires direct confirmation. Target: {target}. I will not close apps from a loose phrase."
                    };
            }

            var open = OpenRegex.Match(original);
            if (open.Success)
            {
                var target = CleanTarget(open.Groups[1].Value);
                if (LooksLikeOpenableTarget(target))
                    return new PcIntentParseResult { Handled = true, Action = PcIntentAction.OpenApp, Argument = target };
            }

            return NoMatch();
        }

        public static async Task<PcIntentExecutionResult> TryExecuteAsync(
            string? text,
            PcControlService pc,
            CancellationToken ct = default)
        {
            var parsed = Parse(text);
            if (!parsed.Handled)
                return new PcIntentExecutionResult { Handled = false };

            if (parsed.RequiresConfirmation)
            {
                return new PcIntentExecutionResult
                {
                    Handled = true,
                    Action = parsed.Action,
                    RequiresConfirmation = true,
                    Reply = parsed.ConfirmationText
                };
            }

            try
            {
                ct.ThrowIfCancellationRequested();
                switch (parsed.Action)
                {
                    case PcIntentAction.SystemInfo:
                        return Done(parsed.Action, FormatSystemInfo(pc.GetSystemInfo()));

                    case PcIntentAction.Processes:
                        return Done(parsed.Action, "Топ процесів по RAM:\n" + pc.GetTopProcesses());

                    case PcIntentAction.VolumeUp:
                        pc.VolumeUp();
                        return Done(parsed.Action, $"Гучність: {pc.GetVolume()}%.");

                    case PcIntentAction.VolumeDown:
                        pc.VolumeDown();
                        return Done(parsed.Action, $"Гучність: {pc.GetVolume()}%.");

                    case PcIntentAction.VolumeMute:
                        pc.VolumeMute();
                        return Done(parsed.Action, "Звук вимкнула.");

                    case PcIntentAction.VolumeSet:
                        pc.SetVolume(parsed.Number ?? 0);
                        return Done(parsed.Action, $"Гучність: {pc.GetVolume()}%.");

                    case PcIntentAction.OpenApp:
                    {
                        var (ok, msg) = pc.OpenApp(parsed.Argument);
                        return Done(parsed.Action, ok ? msg : "Не відкрила: " + msg);
                    }

                    case PcIntentAction.KillProcess:
                    {
                        var (ok, msg) = pc.KillProcess(parsed.Argument);
                        return Done(parsed.Action, ok ? msg : "Не завершила: " + msg);
                    }

                    case PcIntentAction.LockScreen:
                        pc.LockScreen();
                        return Done(parsed.Action, "ПК заблоковано.");

                    case PcIntentAction.Sleep:
                        pc.Sleep();
                        return Done(parsed.Action, "Відправила ПК у сон.");

                    case PcIntentAction.MonitorOff:
                        pc.TurnOffMonitor();
                        return Done(parsed.Action, "Монітор вимкнула.");

                    case PcIntentAction.AbortShutdown:
                        pc.AbortShutdown();
                        return Done(parsed.Action, "Скасувала заплановане вимкнення/рестарт.");

                    case PcIntentAction.RunPowerShell:
                        if (PcCommandSafety.IsBlocked(parsed.Argument, out var reason))
                            return Done(parsed.Action, "Команду заблоковано: " + reason);
                        var output = await pc.RunCommandAsync(parsed.Argument, enforceSafety: true);
                        return Done(parsed.Action, "PowerShell:\n" + output);

                    case PcIntentAction.RunShellChain:
                    {
                        var chain = await pc.RunCommandChainAsync(parsed.Argument, ct: ct);
                        return Done(parsed.Action, FormatShellChain(chain));
                    }

                    case PcIntentAction.WorkspaceScenario:
                    {
                        var scenario = pc.RunWorkspaceScenario(parsed.Argument);
                        return Done(parsed.Action, scenario.ToString());
                    }

                    case PcIntentAction.FocusWindow:
                    {
                        var focused = pc.FocusWindow(parsed.Argument);
                        return Done(parsed.Action, focused.Message);
                    }

                    case PcIntentAction.ArrangeWindows:
                    {
                        var arranged = pc.ArrangeWorkspaceWindows(parsed.Argument);
                        return Done(parsed.Action, arranged.Message);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return Done(parsed.Action, "Команду скасовано.");
            }
            catch (Exception ex)
            {
                return Done(parsed.Action, "PC-команда впала: " + ex.Message);
            }

            return new PcIntentExecutionResult { Handled = false };
        }

        private static PcIntentExecutionResult Done(PcIntentAction action, string reply)
            => new()
            {
                Handled = true,
                Action = action,
                Reply = reply
            };

        public static string FormatSystemInfo(SystemInfo info)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Системна інформація:");
            sb.AppendLine($"Машина: {info.MachineName} ({info.UserName})");
            sb.AppendLine($"ОС: {info.OsVersion}");
            sb.AppendLine($"Аптайм: {info.Uptime.Days}д {info.Uptime.Hours}г {info.Uptime.Minutes}хв");
            sb.AppendLine($"RAM: {info.RamUsedGb:F1} / {info.RamTotalGb:F1} GB");
            sb.AppendLine($"CPU: {info.CpuPercent:F1}%");
            if (info.TopProcesses.Count > 0)
                sb.AppendLine("Top: " + string.Join(", ", info.TopProcesses.Take(3).Select(p => $"{p.ProcessName} {p.MemoryMb:F0}MB/{p.CpuPercent:F1}%")));
            sb.AppendLine($"Гучність: {info.VolumePercent}%");
            sb.AppendLine("Диски:");
            foreach (var d in info.Drives)
            {
                var used = d.TotalGb - d.FreeGb;
                sb.AppendLine($"- {d.Name} {used:F0}/{d.TotalGb:F0} GB");
            }
            return sb.ToString().Trim();
        }

        private static string FormatShellChain(ShellCommandChainResult chain)
        {
            var sb = new StringBuilder();
            sb.AppendLine(chain.Summary);
            foreach (var step in chain.Steps)
            {
                var status = step.Succeeded ? "ok" : step.Blocked ? "blocked" : step.TimedOut ? "timeout" : $"exit {step.ExitCode}";
                sb.AppendLine($"{step.Order}. {status}: {step.Command}");
                var tail = string.IsNullOrWhiteSpace(step.Error) ? step.Output : step.Error;
                if (!string.IsNullOrWhiteSpace(tail))
                    sb.AppendLine("   " + TrimLine(tail, 260));
            }
            return sb.ToString().Trim();
        }

        private static bool LooksLikeWorkspaceScenario(string lower)
            => ContainsAny(lower,
                "підготуй все для кодингу", "підготуй для кодингу", "для кодингу",
                "prepare coding", "coding workspace", "workspace for coding",
                "підготуй робоче місце", "підготуй workspace", "режим кодингу",
                "gaming workspace", "режим гри", "режим ігри");

        private static string TrimLine(string? text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            var clean = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return clean.Length <= max ? clean : clean[..Math.Max(0, max - 3)] + "...";
        }

        private static PcIntentParseResult Match(PcIntentAction action)
            => new() { Handled = true, Action = action };

        private static PcIntentParseResult NoMatch()
            => new() { Handled = false, Action = PcIntentAction.None };

        private static string TryExtractShellCommand(string text)
        {
            var direct = ShellPrefixRegex.Match(text);
            if (direct.Success) return direct.Groups[1].Value;

            var verbal = ShellVerbRegex.Match(text);
            if (verbal.Success) return verbal.Groups[1].Value;

            return "";
        }

        private static bool WantsSystemInfo(string lower)
            => ContainsAny(lower,
                "статус пк", "стан пк", "системна інф", "системная инф", "sysinfo",
                "system info", "інфа про пк", "инфа про пк", "покажи ram", "скільки ram",
                "диски", "місце на диску", "место на диске");

        private static bool WantsProcesses(string lower)
            => ContainsAny(lower,
                "процеси", "процессы", "top processes", "tasklist", "що жере ram",
                "що жере пам", "кто жрет ram", "диспетчер задач", "список процес");

        private static bool WantsSleep(string lower)
            => ContainsAny(lower, "приспи пк", "приспи комп", "sleep pc", "sleep computer", "сон пк", "сон комп");

        private static bool WantsShutdown(string lower)
            => ContainsAny(lower, "вимкни пк", "вимкни комп", "shutdown pc", "shutdown computer", "выключи пк", "выключи комп");

        private static bool WantsRestart(string lower)
            => ContainsAny(lower, "перезавантаж пк", "перезагрузи пк", "restart pc", "reboot pc", "рестарт пк", "рестарт комп");

        private static string CleanTarget(string value)
        {
            var target = value.Trim().Trim('"', '\'', '`', '.', '!', '?', ':', ';');
            target = Regex.Replace(target, @"\s+", " ");
            return target;
        }

        private static bool LooksLikeOpenableTarget(string target)
        {
            if (string.IsNullOrWhiteSpace(target) || LooksLikeDocumentRequest(target))
                return false;

            var lower = target.ToLowerInvariant();
            if (AppAliases.Any(alias => lower.Equals(alias, StringComparison.OrdinalIgnoreCase) || lower.Contains(alias, StringComparison.OrdinalIgnoreCase)))
                return true;

            return Regex.IsMatch(target, @"^[a-zA-Z]:\\") ||
                   target.StartsWith(@"\\", StringComparison.Ordinal) ||
                   Uri.TryCreate(target, UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeFile);
        }

        private static bool LooksLikeDocumentRequest(string text)
            => ContainsAny(text.ToLowerInvariant(),
                "obsidian", "vault", "нотат", "замітк", "заметк", "папк", "файл у vault",
                "daily", "щоденник", "журнал");

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));
    }

    public static class PcCommandSafety
    {
        private static readonly string[] BlockedFragments =
        {
            "remove-item", " rm ", " rmdir", " del ", "erase ", "format ",
            "shutdown ", "restart-computer", "stop-computer", "bcdedit",
            "diskpart", "clear-disk", "set-executionpolicy", "reg delete",
            "cipher /w", "takeown ", "icacls ", "new-itemproperty",
            "set-itemproperty", "remove-itemproperty"
        };

        public static bool IsBlocked(string? command, out string reason)
        {
            reason = "";
            if (string.IsNullOrWhiteSpace(command))
            {
                reason = "порожня команда.";
                return true;
            }

            var normalized = " " + command.Trim().ToLowerInvariant() + " ";
            foreach (var fragment in BlockedFragments)
            {
                if (normalized.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                {
                    reason = $"потенційно руйнівний фрагмент `{fragment.Trim()}`. Для такого є окремі підтверджувані кнопки/команди.";
                    return true;
                }
            }

            return false;
        }
    }
}
