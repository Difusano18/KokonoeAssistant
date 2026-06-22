using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KokonoeAssistant.Services
{
    public enum KokoAgentRunStatus
    {
        Pending,
        Running,
        AwaitingConfirmation,
        Completed,
        Failed,
        Canceled,
        IterationLimit
    }

    public sealed class KokoAgentObservation
    {
        public int Iteration { get; set; }
        public DateTime At { get; set; } = DateTime.UtcNow;
        public string ToolName { get; set; } = "";
        public string CallId { get; set; } = "";
        public bool Success { get; set; }
        public bool Verified { get; set; }
        public bool RequiresConfirmation { get; set; }
        public string PendingActionId { get; set; } = "";
        public string Reason { get; set; } = "";
        public string Output { get; set; } = "";
        public long DurationMs { get; set; }
    }

    public sealed class KokoAgentTurnContext
    {
        public string RunId { get; init; } = "";
        public string Objective { get; init; } = "";
        public int Iteration { get; init; }
        public IReadOnlyList<KokoAgentObservation> Observations { get; init; } = Array.Empty<KokoAgentObservation>();
        public IReadOnlyCollection<string> AvailableTools { get; init; } = Array.Empty<string>();
        public IReadOnlyCollection<KokoToolDescriptor> AvailableToolDescriptors { get; init; } = Array.Empty<KokoToolDescriptor>();
        public string ToolPromptBlock { get; init; } = "";
        public IReadOnlyList<BlackboardEvent> Blackboard { get; init; } = Array.Empty<BlackboardEvent>();
    }

    public sealed class KokoAgentDecision
    {
        public string Analysis { get; set; } = "";
        public string Plan { get; set; } = "";
        public KokoToolCall? Action { get; set; }
        public bool IsComplete { get; set; }
        public string FinalOutput { get; set; } = "";

        public static KokoAgentDecision Complete(string output, string analysis = "Objective complete.") => new()
        {
            Analysis = analysis,
            Plan = "No further action.",
            IsComplete = true,
            FinalOutput = output
        };

        public static KokoAgentDecision Execute(KokoToolCall action, string analysis, string plan) => new()
        {
            Action = action ?? throw new ArgumentNullException(nameof(action)),
            Analysis = analysis,
            Plan = plan
        };
    }

    public interface IKokoAgent
    {
        string Id { get; }
        IReadOnlyCollection<string> Capabilities { get; }
        Task<KokoAgentDecision> DecideAsync(KokoAgentTurnContext context, CancellationToken ct = default);
    }

    public sealed class KokoAgentRunState
    {
        public string RunId { get; set; } = Guid.NewGuid().ToString("N")[..12];
        public string AgentId { get; set; } = "";
        public string Objective { get; set; } = "";
        public KokoAgentRunStatus Status { get; set; } = KokoAgentRunStatus.Pending;
        public int Iteration { get; set; }
        public int MaxIterations { get; set; } = 12;
        public string LastAnalysis { get; set; } = "";
        public string CurrentPlan { get; set; } = "";
        public string FinalOutput { get; set; } = "";
        public string Error { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public List<KokoAgentObservation> Observations { get; set; } = new();
    }
}
