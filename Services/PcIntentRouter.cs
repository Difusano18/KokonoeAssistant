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
        ArrangeWindows,
        FullContextScan,
        ConfirmPendingAction,
        CancelPendingAction
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

        private static readonly Regex PendingActionIdRegex = new(
            @"\bpc:([a-zA-Z0-9_-]{6,64})\b",
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

            var pending = TryParsePendingActionCommand(original);
            if (pending.Handled)
                return pending;

            if (KokoScreenIntent.IsManualScreenScan(original))
                return NoMatch();

            if (LooksLikeFullContextScan(lower))
                return Match(PcIntentAction.FullContextScan);

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

        public static PcActionPlan? TryBuildActionPlan(string? text)
        {
            return TryBuildActionPlan(text, out _);
        }

        private static PcActionPlan? TryBuildActionPlan(string? text, out PcIntentParseResult parsed)
        {
            var normalized = NormalizeSystemControlText(text);
            parsed = Parse(normalized);
            if (!parsed.Handled)
                return null;

            var plan = BuildActionPlan(parsed, normalized);
            if (plan != null)
            {
                plan.Intent = string.IsNullOrWhiteSpace(normalized) ? parsed.Action.ToString() : normalized;
                plan.UserFacingSummaryUk = BuildPlanSummary(parsed, plan);
            }

            return plan;
        }

        private static PcActionPlan? BuildActionPlan(PcIntentParseResult parsed, string originalText)
        {
            PcActionPlan Plan(string actionType, string target, PcActionRiskTier risk)
            {
                var plan = PcActionPlan.Single(parsed.Action.ToString(), actionType, target, risk);
                plan.RequiresConfirmation = risk >= PcActionRiskTier.RiskyLocal || parsed.RequiresConfirmation;
                return plan;
            }

            switch (parsed.Action)
            {
                case PcIntentAction.SystemInfo:
                    return Plan("systemInfo", "", PcActionRiskTier.Observe);
                case PcIntentAction.FullContextScan:
                    return Plan("getContextV2", "Normal", PcActionRiskTier.Observe);
                case PcIntentAction.ConfirmPendingAction:
                case PcIntentAction.CancelPendingAction:
                    return null;
                case PcIntentAction.Processes:
                    return Plan("processes", "", PcActionRiskTier.Observe);
                case PcIntentAction.VolumeUp:
                    return Plan("volumeUp", "", PcActionRiskTier.SafeLocal);
                case PcIntentAction.VolumeDown:
                    return Plan("volumeDown", "", PcActionRiskTier.SafeLocal);
                case PcIntentAction.VolumeMute:
                    return Plan("volumeMute", "", PcActionRiskTier.SafeLocal);
                case PcIntentAction.VolumeSet:
                    return Plan("volumeSet", (parsed.Number ?? 0).ToString(), PcActionRiskTier.SafeLocal);
                case PcIntentAction.OpenApp:
                {
                    var target = IsDryRunRequested(originalText) ? StripDryRunMarker(parsed.Argument) : parsed.Argument;
                    var plan = Plan("openApp", target, PcActionRiskTier.SafeLocal);
                    if (IsDryRunRequested(originalText))
                        plan.Actions[0].Arguments["dryRun"] = "true";
                    return plan;
                }
                case PcIntentAction.WorkspaceScenario:
                {
                    var plan = Plan("runWorkspaceScenario", parsed.Argument, PcActionRiskTier.SafeLocal);
                    plan.Actions[0].Arguments["dryRun"] = ShouldExecuteWorkspaceScenario(originalText) ? "false" : "true";
                    return plan;
                }
                case PcIntentAction.FocusWindow:
                    return Plan("focusWindow", parsed.Argument, PcActionRiskTier.SafeLocal);
                case PcIntentAction.ArrangeWindows:
                    return Plan("arrangeWindows", parsed.Argument, PcActionRiskTier.SafeLocal);
                case PcIntentAction.KillProcess:
                {
                    var target = IsDryRunRequested(originalText) ? StripDryRunMarker(parsed.Argument) : parsed.Argument;
                    var plan = Plan("killProcess", target, PcActionRiskTier.RiskyLocal);
                    if (IsDryRunRequested(originalText))
                        plan.Actions[0].Arguments["dryRun"] = "true";
                    if (!string.IsNullOrWhiteSpace(target))
                        plan.AffectedProcesses.Add(target);
                    return plan;
                }
                case PcIntentAction.LockScreen:
                    return Plan("lockScreen", "", PcActionRiskTier.RiskyLocal);
                case PcIntentAction.Sleep:
                    return Plan("sleep", "", PcActionRiskTier.RiskyLocal);
                case PcIntentAction.MonitorOff:
                    return Plan("monitorOff", "", PcActionRiskTier.RiskyLocal);
                case PcIntentAction.AbortShutdown:
                    return Plan("abortShutdown", "", PcActionRiskTier.RiskyLocal);
                case PcIntentAction.Shutdown:
                    return Plan("shutdown", "", PcActionRiskTier.ExternalOrIrreversible);
                case PcIntentAction.Restart:
                    return Plan("restart", "", PcActionRiskTier.ExternalOrIrreversible);
                case PcIntentAction.RunPowerShell:
                {
                    var plan = Plan("shell", parsed.Argument, PcActionRiskTier.RiskyLocal);
                    plan.Actions[0].Arguments["command"] = parsed.Argument;
                    return plan;
                }
                case PcIntentAction.RunShellChain:
                {
                    var plan = Plan("runShellChain", parsed.Argument, PcActionRiskTier.RiskyLocal);
                    plan.Actions[0].Arguments["command"] = parsed.Argument;
                    return plan;
                }
            }

            return null;
        }

        public static async Task<PcIntentExecutionResult> TryExecuteAsync(
            string? text,
            PcControlService pc,
            CancellationToken ct = default,
            PcActionExecutor? executor = null)
        {
            var normalized = NormalizeSystemControlText(text);
            var pendingCommand = TryParsePendingActionCommand(normalized);
            if (pendingCommand.Handled)
            {
                try
                {
                    ct.ThrowIfCancellationRequested();
                    executor ??= new PcActionExecutor(pc: pc);
                    var pendingResult = pendingCommand.Action == PcIntentAction.CancelPendingAction
                        ? await executor.CancelPendingActionAsync(pendingCommand.Argument, "cancelled from live route").ConfigureAwait(false)
                        : await executor.ConfirmAndExecuteAsync(
                            pendingCommand.Argument,
                            normalized,
                            pc.GetContextV2(PcObservationMode.Light),
                            ct).ConfigureAwait(false);

                    return new PcIntentExecutionResult
                    {
                        Handled = true,
                        Action = pendingCommand.Action,
                        RequiresConfirmation = false,
                        Reply = FormatExecutionResult(pendingResult)
                    };
                }
                catch (OperationCanceledException)
                {
                    return Done(pendingCommand.Action, "PC action cancelled.");
                }
                catch (Exception ex)
                {
                    return Done(pendingCommand.Action, "PC confirmation failed: " + ex.Message);
                }
            }

            var routedPlan = TryBuildActionPlan(text, out var routedParsed);
            if (routedPlan == null || !routedParsed.Handled)
                return new PcIntentExecutionResult { Handled = false };

            try
            {
                ct.ThrowIfCancellationRequested();
                executor ??= new PcActionExecutor(pc: pc);
                var routedResult = await executor.ExecuteAsync(routedPlan, pc.GetContextV2(PcObservationMode.Light), ct)
                    .ConfigureAwait(false);
                return new PcIntentExecutionResult
                {
                    Handled = true,
                    Action = routedParsed.Action,
                    RequiresConfirmation = routedResult.RequiresConfirmation,
                    Reply = FormatExecutionResult(routedResult)
                };
            }
            catch (OperationCanceledException)
            {
                return Done(routedParsed.Action, "PC action cancelled.");
            }
            catch (Exception ex)
            {
                return Done(routedParsed.Action, "PC action failed: " + ex.Message);
            }

        }

        private static PcIntentExecutionResult Done(PcIntentAction action, string reply)
            => new()
            {
                Handled = true,
                Action = action,
                Reply = reply
            };

        public static string FormatExecutionResult(PcActionExecutionResult result)
        {
            if (result.Blocked)
                return "PC action blocked by policy: " + result.Decision.Reason;

            if (result.RequiresConfirmation)
            {
                var confirmations = result.Decision.RequiredConfirmations.Count == 0
                    ? result.Decision.Reason
                    : string.Join("; ", result.Decision.RequiredConfirmations);
                var id = string.IsNullOrWhiteSpace(result.PendingActionId) ? result.ActionId : result.PendingActionId;
                var suffix = string.IsNullOrWhiteSpace(id) ? "" : $" Action id: pc:{id}.";
                return string.IsNullOrWhiteSpace(result.Message)
                    ? "PC action requires confirmation: " + confirmations + suffix
                    : result.Message;
            }

            if (!string.IsNullOrWhiteSpace(result.Message))
                return result.Message;

            return result.Succeeded ? "PC action completed." : "PC action did not complete.";
        }

        private static string BuildPlanSummary(PcIntentParseResult parsed, PcActionPlan plan)
        {
            var step = plan.Actions.FirstOrDefault();
            return $"{parsed.Action}: {step?.ActionType} {step?.Target}".Trim();
        }

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

        public static string FormatAllContext(PcContextSnapshot context)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Повний PC-контекст зібрано.");
            sb.AppendLine($"Активне вікно: {context.Foreground}");
            sb.AppendLine($"Система: CPU {context.System.CpuPercent:F1}% | RAM {context.System.RamUsedGb:F1}/{context.System.RamTotalGb:F1} GB | звук {context.System.VolumePercent}%");

            if (context.BrowserWindows.Count > 0)
            {
                sb.AppendLine("Браузерні вікна/заголовки:");
                foreach (var window in context.BrowserWindows.Take(10))
                    sb.AppendLine("- " + TrimLine(window.ToString(), 180));
            }
            else
            {
                sb.AppendLine("Браузерні вікна: не виявлено відкритих top-level browser windows.");
            }

            if (context.VisibleWindows.Count > 0)
            {
                sb.AppendLine("Видимі вікна:");
                foreach (var window in context.VisibleWindows.Take(10))
                    sb.AppendLine("- " + TrimLine(window.ToString(), 180));
            }

            if (context.System.TopProcesses.Count > 0)
                sb.AppendLine("Навантаження: " + string.Join(", ", context.System.TopProcesses.Take(5).Select(p => $"{p.ProcessName} {p.MemoryMb:F0}MB/{p.CpuPercent:F1}%")));

            if (context.Errors.Count > 0)
                sb.AppendLine("Попередження збору: " + string.Join("; ", context.Errors));

            sb.AppendLine("Висновок: локальний context-scan виконано; якщо треба буквальний OCR/картинка екрана, наступний крок - screenshot+vision.");
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

        private static PcIntentParseResult TryParsePendingActionCommand(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return NoMatch();

            var match = PendingActionIdRegex.Match(text);
            if (!match.Success)
                return NoMatch();

            var lower = text.ToLowerInvariant();
            var id = match.Groups[1].Value.Trim();
            if (ContainsAny(lower, "скасуй", "відміни", "cancel", "abort"))
            {
                return new PcIntentParseResult
                {
                    Handled = true,
                    Action = PcIntentAction.CancelPendingAction,
                    Argument = id,
                    ConfirmationText = text
                };
            }

            if (ContainsAny(lower, "підтверджую", "підтверд", "так, виконай", "виконай", "запусти", "confirm", "execute", "run", "do it"))
            {
                return new PcIntentParseResult
                {
                    Handled = true,
                    Action = PcIntentAction.ConfirmPendingAction,
                    Argument = id,
                    ConfirmationText = text
                };
            }

            return NoMatch();
        }

        private static bool LooksLikeWorkspaceScenario(string lower)
            => ContainsAny(lower,
                "підготуй все для кодингу", "підготуй для кодингу", "для кодингу",
                "prepare coding", "coding workspace", "workspace for coding",
                "підготуй робоче місце", "підготуй workspace", "режим кодингу",
                "gaming workspace", "режим гри", "режим ігри");

        private static bool LooksLikeFullContextScan(string lower)
        {
            if (ContainsAny(lower, "obsidian", "vault", "обсидіан", "обсидиан", "нотат", "замет"))
                return false;

            return ContainsAny(lower,
                "scan everything", "full context", "context scan", "pc context", "what is new", "what do you see", "what can you see",
                "\u043f\u0440\u043e\u0441\u043a\u0430\u043d\u0443\u0439 \u0432\u0441\u0435", "\u043f\u0440\u043e\u0441\u043a\u0430\u043d\u0443\u0439 \u0441\u0438\u0441\u0442\u0435\u043c\u0443", "\u043f\u0440\u043e\u0441\u043a\u0430\u043d\u0443\u0439 \u043f\u043a", "\u043f\u0440\u043e\u0441\u043a\u0430\u043d\u0443\u0439 \u043a\u043e\u043c\u043f",
                "\u043f\u0440\u043e\u0441\u043a\u0430\u043d\u0438\u0440\u0443\u0439 \u0432\u0441\u0435", "\u043f\u0440\u043e\u0441\u043a\u0430\u043d\u0438\u0440\u0443\u0439 \u0441\u0438\u0441\u0442\u0435\u043c\u0443", "\u043f\u0440\u043e\u0441\u043a\u0430\u043d\u0438\u0440\u0443\u0439 \u043f\u043a", "\u043f\u0440\u043e\u0441\u043a\u0430\u043d\u0438\u0440\u0443\u0439 \u043a\u043e\u043c\u043f",
                "\u0449\u043e \u0431\u0430\u0447\u0438\u0448", "\u0448\u043e \u0431\u0430\u0447\u0438\u0448", "\u0447\u0442\u043e \u0432\u0438\u0434\u0438\u0448\u044c",
                "\u0449\u043e \u043d\u043e\u0432\u043e\u0433\u043e", "\u0448\u043e \u043d\u043e\u0432\u043e\u0433\u043e", "\u0447\u0442\u043e \u043d\u043e\u0432\u043e\u0433\u043e",
                "\u043f\u043e\u0434\u0438\u0432\u0438\u0441\u044c \u0449\u043e \u0432\u0456\u0434\u043a\u0440\u0438\u0442\u043e", "\u0433\u043b\u044f\u043d\u044c \u0449\u043e \u0432\u0456\u0434\u043a\u0440\u0438\u0442\u043e", "\u044f\u043a\u0456 \u0432\u043a\u043b\u0430\u0434\u043a\u0438", "\u044f\u043a\u0456 \u0432\u0456\u043a\u043d\u0430",
                "\u043a\u0430\u043a\u0438\u0435 \u0432\u043a\u043b\u0430\u0434\u043a\u0438", "\u043a\u0430\u043a\u0438\u0435 \u043e\u043a\u043d\u0430",
                "проскануй все", "проскануй систему", "проскануй пк", "проскануй комп",
                "просканируй все", "просканируй систему", "просканируй пк", "просканируй комп",
                "що бачиш", "шо бачиш", "что видишь",
                "що нового", "шо нового", "что нового",
                "подивись що відкрито", "глянь що відкрито", "які вкладки", "які вікна",
                "какие вкладки", "какие окна", "open tabs", "browser tabs");
        }

        private static string TrimLine(string? text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            var clean = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return clean.Length <= max ? clean : clean[..Math.Max(0, max - 3)] + "...";
        }

        private static string NormalizeSystemControlText(string? text)
        {
            var clean = (text ?? "").Trim();
            clean = Regex.Replace(clean, @"^\s*(?:system\s*control|systemcontrol|pc\s*control|os\s*control)\s*[:\-]\s*", "", RegexOptions.IgnoreCase);
            return clean.Trim();
        }

        private static bool IsDryRunRequested(string text)
            => ContainsAny(text.ToLowerInvariant(), "dry-run", "dry run", "preview", "simulate", "without executing", "без виконання", "не запускай", "тільки план");

        private static string StripDryRunMarker(string text)
        {
            var cleaned = Regex.Replace(
                text ?? "",
                @"\b(?:dry-run|dry\s+run|preview|simulate|without\s+executing|без\s+виконання|не\s+запускай|тільки\s+план)\b",
                "",
                RegexOptions.IgnoreCase);
            return CleanTarget(cleaned);
        }

        private static bool ShouldExecuteWorkspaceScenario(string text)
        {
            var lower = text.ToLowerInvariant();
            if (IsDryRunRequested(text))
                return false;
            return ContainsAny(lower, "execute", "run it", "actually", "for real", "виконай", "запусти реально", "реально запусти", "без dry-run");
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
