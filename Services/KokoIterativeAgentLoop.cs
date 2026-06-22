using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KokonoeAssistant.Services
{
    public sealed class KokoIterativeAgentLoop
    {
        private readonly IKokoToolGateway _tools;
        private readonly KokoAgentRunStore _store;
        private readonly KokoInternalBlackboardService? _blackboard;

        public KokoIterativeAgentLoop(
            string dataDir,
            IKokoToolGateway tools,
            KokoInternalBlackboardService? blackboard = null)
        {
            _tools = tools ?? throw new ArgumentNullException(nameof(tools));
            _store = new KokoAgentRunStore(dataDir);
            _blackboard = blackboard;
        }

        public event Action<KokoAgentRunState>? StateChanged;

        public async Task<KokoAgentRunState> RunAsync(
            string objective,
            IKokoAgent agent,
            string? runId = null,
            int maxIterations = 12,
            CancellationToken ct = default)
        {
            if (agent == null) throw new ArgumentNullException(nameof(agent));
            if (string.IsNullOrWhiteSpace(objective))
                throw new ArgumentException("Agent objective is empty.", nameof(objective));

            runId = string.IsNullOrWhiteSpace(runId) ? Guid.NewGuid().ToString("N")[..12] : runId.Trim();
            var state = _store.Load(runId) ?? new KokoAgentRunState
            {
                RunId = runId,
                AgentId = agent.Id,
                Objective = objective.Trim(),
                MaxIterations = Math.Clamp(maxIterations, 1, 50)
            };

            if (!string.Equals(state.Objective, objective.Trim(), StringComparison.Ordinal))
                throw new InvalidOperationException("A persisted run cannot be resumed with another objective.");
            if (!string.IsNullOrWhiteSpace(state.AgentId) &&
                !string.Equals(state.AgentId, agent.Id, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("A persisted run cannot be resumed by another agent.");
            if (state.Status == KokoAgentRunStatus.Completed)
                return state;
            if (state.Status == KokoAgentRunStatus.AwaitingConfirmation)
                return state;

            state.AgentId = agent.Id;
            state.MaxIterations = Math.Clamp(maxIterations, 1, 50);
            state.Status = KokoAgentRunStatus.Running;
            SaveAndPublish(state, "resume", $"run {state.RunId} started at iteration {state.Iteration}");

            try
            {
                while (state.Iteration < state.MaxIterations)
                {
                    ct.ThrowIfCancellationRequested();
                    var context = new KokoAgentTurnContext
                    {
                        RunId = state.RunId,
                        Objective = state.Objective,
                        Iteration = state.Iteration + 1,
                        Observations = state.Observations.ToList(),
                        AvailableTools = _tools.ToolNames,
                        Blackboard = _blackboard?.Recent(8) ?? Array.Empty<BlackboardEvent>()
                    };

                    var decision = await agent.DecideAsync(context, ct).ConfigureAwait(false)
                        ?? throw new InvalidOperationException($"Agent {agent.Id} returned no decision.");
                    state.Iteration++;
                    state.LastAnalysis = decision.Analysis?.Trim() ?? "";
                    state.CurrentPlan = decision.Plan?.Trim() ?? "";
                    SaveAndPublish(state, "plan", Trim(state.CurrentPlan, 240));

                    if (decision.IsComplete)
                    {
                        state.FinalOutput = decision.FinalOutput?.Trim() ?? "";
                        state.Status = KokoAgentRunStatus.Completed;
                        SaveAndPublish(state, "complete", Trim(state.FinalOutput, 240));
                        return state;
                    }

                    if (decision.Action == null)
                        throw new InvalidOperationException($"Agent {agent.Id} produced neither completion nor an action.");

                    var action = decision.Action;
                    SaveAndPublish(state, "execute", $"{action.Name}#{action.Id}");
                    var result = await _tools.ExecuteAsync(action, ct).ConfigureAwait(false);
                    state.Observations.Add(new KokoAgentObservation
                    {
                        Iteration = state.Iteration,
                        ToolName = result.ToolName,
                        CallId = result.CallId,
                        Success = result.Success,
                        Verified = result.Verified,
                        RequiresConfirmation = result.RequiresConfirmation,
                        PendingActionId = result.PendingActionId,
                        Reason = result.Reason,
                        Output = result.Output,
                        DurationMs = result.DurationMs
                    });
                    SaveAndPublish(state, "observe", Trim(result.ToLlmText(), 300));

                    if (result.RequiresConfirmation)
                    {
                        state.Status = KokoAgentRunStatus.AwaitingConfirmation;
                        SaveAndPublish(state, "awaiting_confirmation", result.PendingActionId);
                        return state;
                    }
                }

                state.Status = KokoAgentRunStatus.IterationLimit;
                state.Error = $"Iteration limit reached ({state.MaxIterations}).";
                SaveAndPublish(state, "limit", state.Error);
                return state;
            }
            catch (OperationCanceledException)
            {
                state.Status = KokoAgentRunStatus.Canceled;
                state.Error = "Canceled.";
                SaveAndPublish(state, "canceled", state.Error);
                throw;
            }
            catch (Exception ex)
            {
                state.Status = KokoAgentRunStatus.Failed;
                state.Error = ex.Message;
                SaveAndPublish(state, "failed", ex.Message);
                return state;
            }
        }

        public KokoAgentRunState? Load(string runId) => _store.Load(runId);

        private void SaveAndPublish(KokoAgentRunState state, string kind, string summary)
        {
            _store.Save(state);
            try { StateChanged?.Invoke(state); }
            catch (Exception ex) { KokoSystemLog.Write("AGENT-LOOP", "state subscriber failed: " + ex.Message); }
            _blackboard?.Publish(state.AgentId, "agent_" + kind, summary, Priority(kind), new
            {
                state.RunId,
                state.Iteration,
                status = state.Status.ToString()
            }, state.Status.ToString().ToLowerInvariant());
            KokoSystemLog.Write("AGENT-LOOP", $"run={state.RunId} agent={state.AgentId} iteration={state.Iteration} phase={kind} status={state.Status}: {summary}");
        }

        private static double Priority(string kind) => kind switch
        {
            "failed" => 0.95,
            "awaiting_confirmation" => 0.90,
            "complete" => 0.80,
            "execute" => 0.70,
            _ => 0.55
        };

        private static string Trim(string? value, int max)
        {
            value = (value ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            return value.Length <= max ? value : value[..Math.Max(0, max - 1)].TrimEnd() + "...";
        }
    }
}
