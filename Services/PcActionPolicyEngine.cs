using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace KokonoeAssistant.Services
{
    public enum PcActionRiskTier
    {
        Observe = 0,
        Prepare = 1,
        SafeLocal = 2,
        RiskyLocal = 3,
        ExternalOrIrreversible = 4
    }

    public enum PcPolicyDecisionKind
    {
        Allowed,
        NeedsConfirmation,
        Blocked
    }

    public sealed class PcActionPlan
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string RequestedBy { get; set; } = "kokonoe";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string Intent { get; set; } = "";
        public PcActionRiskTier RiskTier { get; set; } = PcActionRiskTier.Observe;
        public List<PcActionStep> Actions { get; set; } = new();
        public List<string> AffectedPaths { get; set; } = new();
        public List<string> AffectedProcesses { get; set; } = new();
        public bool RequiresConfirmation { get; set; }
        public bool RollbackAvailable { get; set; }
        public string ExpectedResult { get; set; } = "";
        public List<string> VerificationSteps { get; set; } = new();
        public string UserFacingSummaryUk { get; set; } = "";

        public static PcActionPlan Single(string intent, string actionType, string target = "", PcActionRiskTier riskTier = PcActionRiskTier.Observe)
            => new()
            {
                Intent = intent,
                RiskTier = riskTier,
                Actions =
                {
                    new PcActionStep { Order = 1, ActionType = actionType, Target = target }
                }
            };
    }

    public sealed class PcActionStep
    {
        public int Order { get; set; }
        public string ActionType { get; set; } = "";
        public string Target { get; set; } = "";
        public string Description { get; set; } = "";
        public Dictionary<string, string> Arguments { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class PcPolicyDecision
    {
        public PcPolicyDecisionKind Kind { get; set; }
        public PcActionRiskTier RiskTier { get; set; }
        public bool ConfirmationRequired => Kind == PcPolicyDecisionKind.NeedsConfirmation;
        public bool CanExecute => Kind == PcPolicyDecisionKind.Allowed;
        public string Reason { get; set; } = "";
        public List<string> Violations { get; set; } = new();
        public List<string> RequiredConfirmations { get; set; } = new();
        public string UserFacingSummaryUk { get; set; } = "";
    }

    public sealed class PcActionPolicyEngine
    {
        private static readonly HashSet<string> SafeLocalActions = new(StringComparer.OrdinalIgnoreCase)
        {
            "getcontext",
            "getcontextv2",
            "systeminfo",
            "processes",
            "foreground",
            "listwindows",
            "local search",
            "localsearch",
            "createbackup",
            "draftfile",
            "openapp",
            "openobsidiannote",
            "focuswindow",
            "arrangeworkspacewindows",
            "arrangewindows",
            "runworkspacescenario",
            "takescreenshot",
            "screenshot",
            "deepobservation",
            "volumeup",
            "volumedown",
            "volumemute",
            "volumeset",
            "setvolume"
        };

        private static readonly HashSet<string> ObserveActions = new(StringComparer.OrdinalIgnoreCase)
        {
            "observe",
            "getcontext",
            "getcontextv2",
            "systeminfo",
            "processes",
            "foreground",
            "listwindows"
        };

        private static readonly HashSet<string> PrepareActions = new(StringComparer.OrdinalIgnoreCase)
        {
            "prepare",
            "draft",
            "draftfile",
            "dryrun",
            "createbackup",
            "localsearch",
            "local search"
        };

        private static readonly HashSet<string> RiskyActions = new(StringComparer.OrdinalIgnoreCase)
        {
            "killprocess",
            "closeprocess",
            "shutdown",
            "restart",
            "sleep",
            "lockscreen",
            "monitoroff",
            "runpowershell",
            "runshellchain",
            "shell",
            "deletefile",
            "movefile",
            "writeconfig",
            "changesettings"
        };

        private static readonly HashSet<string> ExternalActions = new(StringComparer.OrdinalIgnoreCase)
        {
            "sendmessage",
            "sendtelegram",
            "publish",
            "post",
            "purchase",
            "payment",
            "accountchange",
            "externalpost",
            "email"
        };

        private static readonly Regex SevereShellRegex = new(
            @"(?i)\b(format|diskpart|bcdedit|reg\s+(?:delete|add)|Set-ExecutionPolicy|Invoke-WebRequest|curl|wget|scp|ftp|Remove-Item\s+.*(?:-Recurse|-Force)|rm\s+-rf|shutdown|restart-computer)\b",
            RegexOptions.Compiled);

        public PcPolicyDecision Evaluate(PcActionPlan plan, PcContextSnapshotV2 context)
        {
            if (plan == null)
                return Block(PcActionRiskTier.RiskyLocal, "missing action plan", "План дії порожній.");

            var actions = plan.Actions.OrderBy(a => a.Order).ToList();
            if (actions.Count == 0)
                return Block(plan.RiskTier, "action plan has no steps", "У плані немає кроків.");

            var risk = actions
                .Select(InferRisk)
                .Append(plan.RiskTier)
                .Max();

            var violations = new List<string>();
            var confirmations = new List<string>();
            var sensitive = context?.Privacy?.IsSensitive == true;

            foreach (var step in actions)
            {
                var action = NormalizeAction(step.ActionType);
                var stepRisk = InferRisk(step);

                if (sensitive && IsScreenObservation(action))
                    violations.Add($"Sensitive context blocks screen observation action '{step.ActionType}'.");

                if (sensitive && stepRisk == PcActionRiskTier.ExternalOrIrreversible)
                    violations.Add($"Sensitive context blocks external action '{step.ActionType}'.");

                if (IsShellAction(action))
                {
                    var command = ExtractCommand(step);
                    var severeShell = SevereShellRegex.IsMatch(command);
                    var blockedBySafety = PcCommandSafety.IsBlocked(command, out var reason);
                    if (severeShell || blockedBySafety)
                        violations.Add(severeShell
                            ? $"Shell command blocked: severe command pattern in '{Trim(command, 120)}'."
                            : $"Shell command blocked: {reason}".Trim());
                    else
                        confirmations.Add($"Shell command requires confirmation: {Trim(command, 120)}");
                }

                if (stepRisk == PcActionRiskTier.SafeLocal && !SafeLocalActions.Contains(action))
                    violations.Add($"SafeLocal action '{step.ActionType}' is not allowlisted.");

                if (stepRisk == PcActionRiskTier.RiskyLocal)
                    confirmations.Add($"Risky local action requires confirmation: {step.ActionType} {step.Target}".Trim());

                if (stepRisk == PcActionRiskTier.ExternalOrIrreversible)
                    confirmations.Add($"External/irreversible action requires confirmation: {step.ActionType} {step.Target}".Trim());
            }

            if (violations.Count > 0)
            {
                return new PcPolicyDecision
                {
                    Kind = PcPolicyDecisionKind.Blocked,
                    RiskTier = risk,
                    Reason = string.Join(" ", violations),
                    Violations = violations,
                    UserFacingSummaryUk = "Дію заблоковано політикою безпеки."
                };
            }

            if (risk is PcActionRiskTier.Observe or PcActionRiskTier.Prepare)
                return Allow(risk, "observe/prepare action is safe to run without confirmation");

            if (risk == PcActionRiskTier.SafeLocal)
                return Allow(risk, "safe local action is allowlisted");

            return new PcPolicyDecision
            {
                Kind = PcPolicyDecisionKind.NeedsConfirmation,
                RiskTier = risk,
                Reason = risk == PcActionRiskTier.ExternalOrIrreversible
                    ? "external or irreversible action requires explicit confirmation"
                    : "risky local action requires explicit confirmation",
                RequiredConfirmations = confirmations.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                UserFacingSummaryUk = "Потрібне явне підтвердження перед виконанням."
            };
        }

        private static PcPolicyDecision Allow(PcActionRiskTier risk, string reason)
            => new()
            {
                Kind = PcPolicyDecisionKind.Allowed,
                RiskTier = risk,
                Reason = reason,
                UserFacingSummaryUk = "Дію дозволено політикою."
            };

        private static PcPolicyDecision Block(PcActionRiskTier risk, string reason, string summaryUk)
            => new()
            {
                Kind = PcPolicyDecisionKind.Blocked,
                RiskTier = risk,
                Reason = reason,
                Violations = { reason },
                UserFacingSummaryUk = summaryUk
            };

        private static PcActionRiskTier InferRisk(PcActionStep step)
        {
            var action = NormalizeAction(step.ActionType);
            if (ObserveActions.Contains(action))
                return PcActionRiskTier.Observe;
            if (PrepareActions.Contains(action))
                return PcActionRiskTier.Prepare;
            if (ExternalActions.Contains(action))
                return PcActionRiskTier.ExternalOrIrreversible;
            if (RiskyActions.Contains(action))
                return PcActionRiskTier.RiskyLocal;
            if (SafeLocalActions.Contains(action))
                return PcActionRiskTier.SafeLocal;
            return PcActionRiskTier.RiskyLocal;
        }

        private static bool IsShellAction(string action)
            => action is "runpowershell" or "runshellchain" or "shell";

        private static bool IsScreenObservation(string action)
            => action is "takescreenshot" or "screenshot" or "deepobservation";

        private static string ExtractCommand(PcActionStep step)
        {
            if (step.Arguments.TryGetValue("command", out var command) && !string.IsNullOrWhiteSpace(command))
                return command;
            return step.Target ?? "";
        }

        private static string NormalizeAction(string? action)
            => Regex.Replace(action ?? "", @"[^a-zA-Z0-9]+", "").ToLowerInvariant();

        private static string Trim(string text, int max)
        {
            text = (text ?? "").Trim();
            return text.Length <= max ? text : text[..max].TrimEnd() + "...";
        }
    }
}
