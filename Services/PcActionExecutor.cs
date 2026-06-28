using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KokonoeAssistant.Services
{
    public sealed class PcActionExecutor
    {
        private readonly PcControlService _pc;
        private readonly PcActionPolicyEngine _policy;
        private readonly PcActionJournal _journal;
        private readonly PcPendingActionStore _pending;

        public PcActionExecutor(
            PcControlService? pc = null,
            PcActionPolicyEngine? policy = null,
            PcActionJournal? journal = null,
            PcPendingActionStore? pending = null)
        {
            _pc = pc ?? new PcControlService();
            _policy = policy ?? new PcActionPolicyEngine();
            _journal = journal ?? new PcActionJournal();
            _pending = pending ?? new PcPendingActionStore();
        }

        public async Task<PcActionExecutionResult> ExecuteAsync(
            PcActionPlan plan,
            PcContextSnapshotV2? context = null,
            CancellationToken ct = default)
        {
            context ??= _pc.GetContextV2(PcObservationMode.Light);
            var decision = _policy.Evaluate(plan, context);
            var result = new PcActionExecutionResult
            {
                ActionId = plan.Id,
                Decision = decision,
                RequiresConfirmation = decision.Kind == PcPolicyDecisionKind.NeedsConfirmation,
                Blocked = decision.Kind == PcPolicyDecisionKind.Blocked
            };

            if (decision.Kind == PcPolicyDecisionKind.Blocked)
            {
                result.Message = decision.Reason;
                _journal.AppendDecision(plan, decision, error: decision.Reason, rollbackAvailable: false);
                return result;
            }

            if (decision.Kind == PcPolicyDecisionKind.NeedsConfirmation)
            {
                var pending = _pending.Save(plan, decision);
                result.PendingActionId = pending.ActionId;
                result.PlanHash = pending.PlanHash;
                result.ExpiresAt = pending.ExpiresAt;
                result.Message = BuildConfirmationMessage(plan, decision, pending);
                _journal.AppendDecision(plan, decision, resultSummary: "confirmation required", rollbackAvailable: false);
                _journal.AppendStatus(plan, "Pending", resultSummary: pending.UserFacingSummary, confirmationRequired: true, rollbackAvailable: false);
                return result;
            }

            try
            {
                foreach (var step in plan.Actions.OrderBy(s => s.Order))
                {
                    ct.ThrowIfCancellationRequested();
                    var stepResult = ExecuteAllowedStep(step);
                    result.StepResults.Add(stepResult);
                    if (!stepResult.Succeeded)
                        break;
                }

                result.Succeeded = result.StepResults.Count > 0 && result.StepResults.All(s => s.Succeeded);
                result.Message = string.Join("\n", result.StepResults.Select(s => s.Message)).Trim();
                _journal.AppendDecision(
                    plan,
                    decision,
                    resultSummary: string.IsNullOrWhiteSpace(result.Message) ? "executed" : result.Message,
                    error: result.Succeeded ? "" : "one or more safe steps failed",
                    rollbackAvailable: plan.RollbackAvailable);
                await Task.CompletedTask.ConfigureAwait(false);
                return result;
            }
            catch (OperationCanceledException)
            {
                result.Message = "PC action cancelled.";
                _journal.AppendDecision(plan, decision, error: result.Message, rollbackAvailable: false);
                return result;
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
                KokoSystemLog.Write("PC-ACTION", $"execute failed plan={plan.Id}: {ex}");
                _journal.AppendDecision(plan, decision, error: ex.Message, rollbackAvailable: false);
                return result;
            }
        }

        public async Task<PcActionExecutionResult> ConfirmAndExecuteAsync(
            string? actionId,
            string? confirmationText,
            PcContextSnapshotV2? context = null,
            CancellationToken ct = default)
        {
            var lookup = _pending.Get(actionId);
            if (!lookup.Exists || lookup.Record == null)
            {
                return new PcActionExecutionResult
                {
                    ActionId = actionId ?? "",
                    Message = "No pending PC action for this id.",
                    Blocked = true,
                    Decision = new PcPolicyDecision
                    {
                        Kind = PcPolicyDecisionKind.Blocked,
                        Reason = "pending action not found"
                    }
                };
            }

            var record = lookup.Record;
            var plan = record.OriginalPlan;
            var currentHash = PcPendingActionStore.ComputePlanHash(plan);
            var result = new PcActionExecutionResult
            {
                ActionId = record.ActionId,
                PendingActionId = record.ActionId,
                PlanHash = record.PlanHash,
                ExpiresAt = record.ExpiresAt
            };

            if (lookup.Expired)
            {
                result.Blocked = true;
                result.Message = "Pending PC action expired. Re-issue the command if you still want it.";
                _journal.AppendStatus(plan, "Expired", error: result.Message, confirmationRequired: true, rollbackAvailable: false);
                return result;
            }

            if (!string.Equals(currentHash, record.PlanHash, StringComparison.OrdinalIgnoreCase))
            {
                result.Blocked = true;
                result.Message = "Pending PC action hash mismatch. I will not execute a mutated plan.";
                _pending.MarkRejected(record, result.Message, confirmationText ?? "");
                _journal.AppendStatus(plan, "Rejected", error: result.Message, confirmationRequired: true, rollbackAvailable: false);
                return result;
            }

            if (!ConfirmationMatches(record, confirmationText))
            {
                result.Blocked = true;
                result.Message = "Confirmation rejected. Use the exact action id and an explicit execute/confirm phrase.";
                _pending.MarkRejected(record, result.Message, confirmationText ?? "");
                _journal.AppendStatus(plan, "Rejected", error: result.Message, confirmationRequired: true, rollbackAvailable: false);
                return result;
            }

            context ??= _pc.GetContextV2(PcObservationMode.Light);
            var decision = _policy.Evaluate(plan, context);
            result.Decision = decision;
            if (decision.Kind == PcPolicyDecisionKind.Blocked)
            {
                result.Blocked = true;
                result.Message = decision.Reason;
                _pending.MarkRejected(record, decision.Reason, confirmationText ?? "");
                _journal.AppendDecision(plan, decision, error: decision.Reason, rollbackAvailable: false);
                _journal.AppendStatus(plan, "Rejected", error: decision.Reason, confirmationRequired: true, rollbackAvailable: false);
                return result;
            }

            _pending.MarkConfirmed(record, confirmationText ?? "", "confirmation accepted");
            _journal.AppendStatus(plan, "Confirmed", resultSummary: "confirmation accepted", confirmationRequired: false, rollbackAvailable: plan.RollbackAvailable);

            try
            {
                foreach (var step in plan.Actions.OrderBy(s => s.Order))
                {
                    ct.ThrowIfCancellationRequested();
                    var stepResult = await ExecuteConfirmedStepAsync(step, ct).ConfigureAwait(false);
                    result.StepResults.Add(stepResult);
                    if (!stepResult.Succeeded)
                        break;
                }

                result.Succeeded = result.StepResults.Count > 0 && result.StepResults.All(s => s.Succeeded);
                result.RequiresConfirmation = false;
                result.Blocked = false;
                result.Message = string.Join("\n", result.StepResults.Select(s => s.Message)).Trim();
                _journal.AppendDecision(
                    plan,
                    decision,
                    resultSummary: string.IsNullOrWhiteSpace(result.Message) ? "confirmed action executed" : result.Message,
                    error: result.Succeeded ? "" : "one or more confirmed steps failed",
                    rollbackAvailable: plan.RollbackAvailable);
                return result;
            }
            catch (OperationCanceledException)
            {
                result.Message = "PC action cancelled.";
                _journal.AppendDecision(plan, decision, error: result.Message, rollbackAvailable: false);
                return result;
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
                KokoSystemLog.Write("PC-ACTION", $"confirmed execute failed action={record.ActionId}: {ex}");
                _journal.AppendDecision(plan, decision, error: ex.Message, rollbackAvailable: false);
                return result;
            }
        }

        public Task<PcActionExecutionResult> CancelPendingActionAsync(string? actionId, string? reason = null)
        {
            if (_pending.Cancel(actionId, string.IsNullOrWhiteSpace(reason) ? "cancelled by user" : reason!, out var record) && record != null)
            {
                _journal.AppendStatus(record.OriginalPlan, "Cancelled", resultSummary: "pending PC action cancelled", confirmationRequired: false, rollbackAvailable: false);
                return Task.FromResult(new PcActionExecutionResult
                {
                    ActionId = record.ActionId,
                    PendingActionId = record.ActionId,
                    PlanHash = record.PlanHash,
                    ExpiresAt = record.ExpiresAt,
                    Succeeded = true,
                    Message = "Pending PC action cancelled."
                });
            }

            return Task.FromResult(new PcActionExecutionResult
            {
                ActionId = actionId ?? "",
                Blocked = true,
                Message = "No pending PC action for this id."
            });
        }

        private PcActionStepResult ExecuteAllowedStep(PcActionStep step)
        {
            var action = NormalizeAction(step.ActionType);
            var dryRun = GetBool(step, "dryRun", defaultValue: false);

            switch (action)
            {
                case "getcontext":
                case "getcontextv2":
                case "systeminfo":
                case "foreground":
                case "processes":
                case "listwindows":
                    return Ok(step, _pc.GetContextV2(PcObservationMode.Normal).ToString());

                case "openapp":
                    if (dryRun)
                        return Ok(step, "dry-run open app: " + step.Target);
                    {
                        var opened = _pc.OpenApp(step.Target);
                        return opened.ok ? Ok(step, opened.msg) : Fail(step, opened.msg);
                    }

                case "openobsidiannote":
                    if (dryRun)
                        return Ok(step, "dry-run open Obsidian note: " + step.Target);
                    {
                        var opened = _pc.OpenObsidianNote(step.Target);
                        return opened.ok ? Ok(step, opened.msg) : Fail(step, opened.msg);
                    }

                case "focuswindow":
                    if (dryRun)
                        return Ok(step, "dry-run focus window: " + step.Target);
                    {
                        var focused = _pc.FocusWindow(step.Target);
                        return focused.Succeeded ? Ok(step, focused.Message) : Fail(step, focused.Message);
                    }

                case "arrangeworkspacewindows":
                case "arrangewindows":
                    if (dryRun)
                        return Ok(step, "dry-run arrange windows: " + step.Target);
                    {
                        var arranged = _pc.ArrangeWorkspaceWindows(step.Target);
                        return arranged.Succeeded ? Ok(step, arranged.Message) : Fail(step, arranged.Message);
                    }

                case "runworkspacescenario":
                    {
                        var scenario = _pc.RunWorkspaceScenario(step.Target, dryRun: GetBool(step, "dryRun", defaultValue: true));
                        return scenario.Succeeded ? Ok(step, scenario.ToString()) : Fail(step, scenario.ToString());
                    }

                case "volumeup":
                    if (dryRun)
                        return Ok(step, "dry-run volume up");
                    _pc.VolumeUp();
                    return Ok(step, $"Volume: {_pc.GetVolume()}%.");

                case "volumedown":
                    if (dryRun)
                        return Ok(step, "dry-run volume down");
                    _pc.VolumeDown();
                    return Ok(step, $"Volume: {_pc.GetVolume()}%.");

                case "volumemute":
                    if (dryRun)
                        return Ok(step, "dry-run volume mute");
                    _pc.VolumeMute();
                    return Ok(step, "Volume: 0%.");

                case "volumeset":
                case "setvolume":
                    if (dryRun)
                        return Ok(step, "dry-run volume set: " + step.Target);
                    if (!int.TryParse(step.Target, out var volume))
                        return Fail(step, "invalid volume target: " + step.Target);
                    _pc.SetVolume(Math.Clamp(volume, 0, 100));
                    return Ok(step, $"Volume: {_pc.GetVolume()}%.");
            }

            return Fail(step, "unsupported allowed PC action: " + step.ActionType);
        }

        private async Task<PcActionStepResult> ExecuteConfirmedStepAsync(PcActionStep step, CancellationToken ct)
        {
            var action = NormalizeAction(step.ActionType);
            var dryRun = GetBool(step, "dryRun", defaultValue: false);

            if (IsSafeAfterConfirmation(action))
                return ExecuteAllowedStep(step);

            switch (action)
            {
                case "killprocess":
                case "closeprocess":
                    if (dryRun)
                        return Ok(step, "dry-run kill process: " + step.Target);
                    {
                        var killed = _pc.KillProcess(step.Target);
                        return killed.ok ? Ok(step, killed.msg) : Fail(step, killed.msg);
                    }

                case "lockscreen":
                    if (dryRun)
                        return Ok(step, "dry-run lock screen");
                    _pc.LockScreen();
                    return Ok(step, "PC locked.");

                case "sleep":
                    if (dryRun)
                        return Ok(step, "dry-run sleep");
                    _pc.Sleep();
                    return Ok(step, "PC sleep requested.");

                case "monitoroff":
                    if (dryRun)
                        return Ok(step, "dry-run monitor off");
                    _pc.TurnOffMonitor();
                    return Ok(step, "Monitor off requested.");

                case "abortshutdown":
                    if (dryRun)
                        return Ok(step, "dry-run abort shutdown");
                    _pc.AbortShutdown();
                    return Ok(step, "Scheduled shutdown/restart aborted.");

                case "shutdown":
                    if (dryRun)
                        return Ok(step, "dry-run shutdown");
                    _pc.Shutdown(10);
                    return Ok(step, "Shutdown scheduled in 10 seconds.");

                case "restart":
                    if (dryRun)
                        return Ok(step, "dry-run restart");
                    _pc.Restart(10);
                    return Ok(step, "Restart scheduled in 10 seconds.");

                case "shell":
                case "runpowershell":
                    if (dryRun)
                        return Ok(step, "dry-run shell: " + ExtractCommand(step));
                    {
                        var output = await _pc.RunCommandAsync(ExtractCommand(step), enforceSafety: true).ConfigureAwait(false);
                        return Ok(step, "PowerShell:\n" + Trim(output, 1400));
                    }

                case "runshellchain":
                    if (dryRun)
                        return Ok(step, "dry-run shell chain: " + ExtractCommand(step));
                    {
                        var chain = await _pc.RunCommandChainAsync(ExtractCommand(step), ct: ct).ConfigureAwait(false);
                        var rendered = chain.ToString() ?? "";
                        return chain.Succeeded ? Ok(step, rendered) : Fail(step, rendered);
                    }
            }

            return Fail(step, "unsupported confirmed PC action: " + step.ActionType);
        }

        private static bool IsSafeAfterConfirmation(string action)
            => action is "getcontext" or "getcontextv2" or "systeminfo" or "foreground" or "processes" or "listwindows"
                or "openapp" or "openobsidiannote" or "focuswindow" or "arrangeworkspacewindows" or "arrangewindows"
                or "runworkspacescenario" or "volumeup" or "volumedown" or "volumemute" or "volumeset" or "setvolume";

        private static string BuildConfirmationMessage(PcActionPlan plan, PcPolicyDecision decision, PcPendingActionRecord pending)
        {
            var why = decision.RequiredConfirmations.Count == 0
                ? decision.Reason
                : string.Join("; ", decision.RequiredConfirmations);
            var summary = string.IsNullOrWhiteSpace(pending.UserFacingSummary) ? plan.Intent : pending.UserFacingSummary;
            return $"PC action needs explicit confirmation.\n" +
                   $"Action: pc:{pending.ActionId}\n" +
                   $"Summary: {summary}\n" +
                   $"Reason: {why}\n" +
                   $"Plan hash: {pending.PlanHash[..Math.Min(12, pending.PlanHash.Length)]}\n" +
                   $"Expires: {pending.ExpiresAt:HH:mm:ss}\n" +
                   $"To run it, write: confirm pc:{pending.ActionId} {GetActionHint(plan)}";
        }

        // Used to require the literal action id PLUS the action-type/target keywords restated
        // in confirmationText, and explicitly rejected a plain "так"/"yes" as "generic". That
        // was meant to make the model prove it understood what it was confirming, but in
        // practice: action types with no entry in ConfirmationMentionsAction's hardcoded list
        // (CreateDirectory, OpenApp, ...) had no natural-language phrase that could ever match,
        // making them effectively unconfirmable, and getting the 12-char hex id exactly right
        // by hand was the actual friction the user hit. By the time this runs, PcPendingAction
        // Store.Get already resolved a specific, non-expired, hash-matching record (by id or,
        // for the single-pending-action case, by inference) and the original proposal was
        // already shown in full before the user/model replied - re-deriving "do they truly
        // understand" from keyword soup added brittleness, not real safety. The policy engine
        // (_policy.Evaluate, right after this call) still re-checks risk tier independent of
        // this text.
        private static bool ConfirmationMatches(PcPendingActionRecord record, string? confirmationText)
            => !string.IsNullOrWhiteSpace(confirmationText);

        private static string GetActionHint(PcActionPlan plan)
        {
            var first = plan.Actions.OrderBy(a => a.Order).FirstOrDefault();
            if (first == null)
                return "action";
            var action = NormalizeAction(first.ActionType);
            var target = string.IsNullOrWhiteSpace(first.Target) ? "" : " " + first.Target.Trim();
            return (action + target).Trim();
        }

        private static string ExtractCommand(PcActionStep step)
        {
            if (step.Arguments.TryGetValue("command", out var command) && !string.IsNullOrWhiteSpace(command))
                return command;
            return step.Target ?? "";
        }

        private static string Trim(string text, int max)
        {
            text = (text ?? "").Trim();
            return text.Length <= max ? text : text[..max].TrimEnd() + "...";
        }

        private static PcActionStepResult Ok(PcActionStep step, string message)
            => new()
            {
                Order = step.Order,
                ActionType = step.ActionType,
                Target = step.Target,
                Succeeded = true,
                Message = message
            };

        private static PcActionStepResult Fail(PcActionStep step, string message)
            => new()
            {
                Order = step.Order,
                ActionType = step.ActionType,
                Target = step.Target,
                Succeeded = false,
                Message = message
            };

        private static bool GetBool(PcActionStep step, string key, bool defaultValue)
        {
            if (!step.Arguments.TryGetValue(key, out var value))
                return defaultValue;
            return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
        }

        private static string NormalizeAction(string? action)
            => new string((action ?? "").Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    public sealed class PcActionExecutionResult
    {
        public string ActionId { get; set; } = "";
        public PcPolicyDecision Decision { get; set; } = new();
        public bool Succeeded { get; set; }
        public bool RequiresConfirmation { get; set; }
        public bool Blocked { get; set; }
        public string Message { get; set; } = "";
        public string PendingActionId { get; set; } = "";
        public string PlanHash { get; set; } = "";
        public DateTime? ExpiresAt { get; set; }
        public List<PcActionStepResult> StepResults { get; set; } = new();
    }

    public sealed class PcActionStepResult
    {
        public int Order { get; set; }
        public string ActionType { get; set; } = "";
        public string Target { get; set; } = "";
        public bool Succeeded { get; set; }
        public string Message { get; set; } = "";
    }
}
