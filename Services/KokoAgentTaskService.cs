using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KokonoeAssistant.Services
{
    public enum KokoAgentTaskStatus
    {
        Pending,
        Running,
        Completed,
        Failed,
        Canceled
    }

    public enum KokoAgentStepKind
    {
        Analyze,
        Plan,
        Vault,
        Vision,
        Sandbox,
        SystemControl,
        Observation,
        InsightExtraction,
        Implement,
        Respond,
        Verify,
        SelfReview,
        HardReset,
        Report
    }

    public sealed class KokoAgentStep
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
        public int Order { get; set; }
        public string Title { get; set; } = "";
        public KokoAgentStepKind Kind { get; set; } = KokoAgentStepKind.Analyze;
        public KokoAgentTaskStatus Status { get; set; } = KokoAgentTaskStatus.Pending;
        public string Result { get; set; } = "";
        public string Error { get; set; } = "";
        public DateTime? StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }
    }

    public sealed class KokoAgentTask
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
        public string Objective { get; set; } = "";
        public KokoAgentTaskStatus Status { get; set; } = KokoAgentTaskStatus.Pending;
        public int Priority { get; set; } = 5;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public string CompletionNotice { get; set; } = "";
        public string NextQuestion { get; set; } = "";
        public List<KokoAgentStep> Steps { get; set; } = new();
    }

    public sealed class KokoAgentActivitySnapshot
    {
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public string Phase { get; set; } = "idle";
        public string Tool { get; set; } = "none";
        public string Focus { get; set; } = "No active task.";
        public string Thought { get; set; } = "Waiting. Suspiciously peaceful.";
        public string TaskId { get; set; } = "";
        public string StepId { get; set; } = "";
    }

    public sealed class KokoAgentTaskSnapshot
    {
        public DateTime TakenAt { get; set; } = DateTime.Now;
        public int MaxParallel { get; set; }
        public int RunningSteps { get; set; }
        public KokoAgentActivitySnapshot Activity { get; set; } = new();
        public List<KokoAgentTask> Tasks { get; set; } = new();
    }

    public sealed class KokoAgentTaskService
    {
        private sealed class StepExecutionOutcome
        {
            public bool Success { get; init; }
            public string Result { get; init; } = "";
            public string? Error { get; init; }

            public static StepExecutionOutcome Succeeded(string result) => new()
            {
                Success = true,
                Result = result
            };

            public static StepExecutionOutcome Failed(string result, string error) => new()
            {
                Success = false,
                Result = result,
                Error = error
            };
        }

        private readonly object _lock = new();
        private readonly string _path;
        private readonly LlmService? _llm;
        private readonly ObsidianMcpService? _obsidian;
        private readonly KokoSandboxExecutor _sandbox;
        private readonly KokoObservationService _observation;
        private readonly List<KokoAgentTask> _tasks = new();
        private KokoAgentActivitySnapshot _activity = new();
        private CancellationTokenSource? _runnerCts;
        private int _runningSteps;
        private int _maxParallel = 4;

        public KokoAgentTaskService(string dataDir, LlmService? llm = null, ObsidianMcpService? obsidian = null)
        {
            Directory.CreateDirectory(dataDir);
            _path = Path.Combine(dataDir, "agent-tasks.json");
            _llm = llm;
            _obsidian = obsidian;
            _sandbox = new KokoSandboxExecutor(Path.Combine(dataDir, "agent-sandbox"));
            _observation = new KokoObservationService(Path.Combine(dataDir, "agent-observations"), new PcControlService(), llm);
            Load();
        }

        public int MaxParallel
        {
            get => _maxParallel;
            set => _maxParallel = Math.Clamp(value, 1, 10);
        }
        public bool AutoStartOnAdd { get; set; } = true;
        public event Action<KokoAgentActivitySnapshot>? ActivityChanged;
        public event Action<KokoAgentTask, KokoAgentCompletionNotice>? TaskCompleted;

        public KokoAgentTask AddTask(string objective, int priority = 5)
        {
            if (string.IsNullOrWhiteSpace(objective))
                throw new ArgumentException("Objective is empty.", nameof(objective));

            objective = objective.Trim();

            lock (_lock)
            {
                var existing = _tasks.FirstOrDefault(t =>
                    t.Status is KokoAgentTaskStatus.Pending or KokoAgentTaskStatus.Running &&
                    string.Equals(t.Objective, objective, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                    return Clone(existing);
            }

            var task = new KokoAgentTask
            {
                Objective = objective,
                Priority = Math.Clamp(priority, 1, 10),
                Steps = BuildPlan(objective).ToList()
            };

            lock (_lock)
            {
                _tasks.Insert(0, task);
                SaveLocked();
            }

            EmitActivity("plan", "KokoAgentTaskService", task.Objective, $"Created plan with {task.Steps.Count} steps.", task.Id);
            if (AutoStartOnAdd)
                Start();
            return Clone(task);
        }

        public int SyncFromObsidianBacklog(int max = 5)
        {
            if (_obsidian == null) return 0;
            var queue = _obsidian.BuildTaskQueue();
            var added = 0;
            foreach (var item in queue.OpenTasks.Take(Math.Max(0, max)))
            {
                var objective = $"Obsidian task from {item.Path}: {item.Text}";
                var before = GetSnapshot().Tasks.Count;
                AddTask(objective, priority: 6);
                if (GetSnapshot().Tasks.Count > before)
                    added++;
            }
            if (added > 0)
                EmitActivity("plan", "ObsidianBacklog", $"Imported {added} vault tasks.", "Vault backlog synchronized into agent board.");
            return added;
        }

        public KokoAgentTaskSnapshot GetSnapshot()
        {
            lock (_lock)
            {
                return new KokoAgentTaskSnapshot
                {
                    MaxParallel = MaxParallel,
                    RunningSteps = _runningSteps,
                    Activity = CloneActivity(_activity),
                    Tasks = _tasks.Select(Clone).ToList()
                };
            }
        }

        public void Start()
        {
            lock (_lock)
            {
                if (_runnerCts != null) return;
                _runnerCts = new CancellationTokenSource();
                EmitActivity("observe", "runner", "Agent runner active.", "Loop online: analyze, plan, execute, observe.");
                _ = Task.Run(() => RunnerLoopAsync(_runnerCts.Token));
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                _runnerCts?.Cancel();
                _runnerCts = null;
                foreach (var task in _tasks.Where(t => t.Status == KokoAgentTaskStatus.Running))
                {
                    task.Status = KokoAgentTaskStatus.Pending;
                    task.UpdatedAt = DateTime.Now;
                }
                foreach (var step in _tasks.SelectMany(t => t.Steps).Where(s => s.Status == KokoAgentTaskStatus.Running))
                    step.Status = KokoAgentTaskStatus.Pending;
                EmitActivity("idle", "runner", "Runner stopped.", "Paused. No autonomous steps are executing.");
                SaveLocked();
            }
        }

        public bool CancelTask(string id)
        {
            lock (_lock)
            {
                var task = _tasks.FirstOrDefault(t => t.Id == id);
                if (task == null) return false;
                task.Status = KokoAgentTaskStatus.Canceled;
                task.UpdatedAt = DateTime.Now;
                foreach (var step in task.Steps.Where(s => s.Status is KokoAgentTaskStatus.Pending or KokoAgentTaskStatus.Running))
                    step.Status = KokoAgentTaskStatus.Canceled;
                EmitActivity("observe", "runner", task.Objective, "Task canceled.", task.Id);
                SaveLocked();
                return true;
            }
        }

        public static IReadOnlyList<KokoAgentStep> BuildPlan(string objective)
        {
            var planned = KokoResponsePlannerEngine.BuildAgentStepsForObjective(objective);
            return planned.Select(CloneStep).ToList();
        }

        public string RenderBoard()
        {
            var snap = GetSnapshot();
            var sb = new StringBuilder();
            sb.AppendLine($"Agent Board | tasks {snap.Tasks.Count} | running {snap.RunningSteps}/{snap.MaxParallel}");
            sb.AppendLine($"Focus: {snap.Activity.Phase} via {snap.Activity.Tool} :: {snap.Activity.Thought}");
            if (snap.Tasks.Count == 0)
            {
                sb.AppendLine("No tasks. Peaceful. Suspicious.");
                return sb.ToString();
            }

            foreach (var task in snap.Tasks.Take(8))
            {
                sb.AppendLine();
                sb.AppendLine($"[{task.Status}] {task.Id} p{task.Priority} :: {task.Objective}");
                foreach (var step in task.Steps.OrderBy(s => s.Order))
                    sb.AppendLine($"  {step.Order}. [{step.Status}] {step.Kind}: {step.Title}");
            }
            return sb.ToString();
        }

        private async Task RunnerLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var launches = new List<(string TaskId, string StepId)>();
                lock (_lock)
                {
                    while (_runningSteps < MaxParallel)
                    {
                        var (task, step) = FindNextStepLocked();
                        if (task != null && step != null)
                        {
                            task.Status = KokoAgentTaskStatus.Running;
                            task.UpdatedAt = DateTime.Now;
                            step.Status = KokoAgentTaskStatus.Running;
                            step.StartedAt = DateTime.Now;
                            _runningSteps++;
                            launches.Add((task.Id, step.Id));
                            continue;
                        }

                        break;
                    }

                    if (launches.Count > 0)
                        SaveLocked();
                }

                foreach (var launch in launches)
                    _ = Task.Run(() => ExecuteStepAsync(launch.TaskId, launch.StepId, ct), ct);

                await Task.Delay(650, ct).ConfigureAwait(false);
            }
        }

        private (KokoAgentTask? Task, KokoAgentStep? Step) FindNextStepLocked()
        {
            foreach (var task in _tasks
                         .Where(t => t.Status is KokoAgentTaskStatus.Pending or KokoAgentTaskStatus.Running)
                         .OrderByDescending(t => t.Priority)
                         .ThenBy(t => t.CreatedAt))
            {
                var step = task.Steps.OrderBy(s => s.Order).FirstOrDefault(s =>
                    s.Status == KokoAgentTaskStatus.Pending &&
                    task.Steps.Where(prev => prev.Order < s.Order).All(prev => prev.Status == KokoAgentTaskStatus.Completed));
                if (step != null) return (task, step);
            }
            return (null, null);
        }

        private async Task ExecuteStepAsync(string taskId, string stepId, CancellationToken ct)
        {
            try
            {
                KokoAgentTask? task;
                KokoAgentStep? step;
                lock (_lock)
                {
                    task = _tasks.FirstOrDefault(t => t.Id == taskId);
                    step = task?.Steps.FirstOrDefault(s => s.Id == stepId);
                }

                if (task == null || step == null) return;
                EmitActivity("execute", ToolNameFor(step.Kind), task.Objective, $"Running step {step.Order}: {step.Title}", task.Id, step.Id);
                var outcome = await ExecuteStepCoreAsync(task, step, ct).ConfigureAwait(false);

                lock (_lock)
                {
                    task = _tasks.FirstOrDefault(t => t.Id == taskId);
                    step = task?.Steps.FirstOrDefault(s => s.Id == stepId);
                    if (task == null || step == null || step.Status != KokoAgentTaskStatus.Running || task.Status == KokoAgentTaskStatus.Canceled)
                        return;

                    step.Result = outcome.Result;
                    step.Error = outcome.Error ?? "";
                    if (outcome.Success)
                    {
                        step.Status = KokoAgentTaskStatus.Completed;
                    }
                    else
                    {
                        step.Status = KokoAgentTaskStatus.Failed;
                        task.Status = KokoAgentTaskStatus.Failed;
                    }
                    step.FinishedAt = DateTime.Now;
                    task.UpdatedAt = DateTime.Now;
                    if (outcome.Success &&
                        step.Kind == KokoAgentStepKind.SelfReview &&
                        TryExtractSelfReviewScore(step.Result, out var score) &&
                        score < 7 &&
                        InsertCorrectionStepLocked(task, step, score))
                    {
                        task.UpdatedAt = DateTime.Now;
                        EmitActivity("plan", "SelfReview", task.Objective, $"Score {score}/10; inserted correction step.", task.Id, step.Id);
                    }
                    if (!outcome.Success || task.Steps.All(s => s.Status == KokoAgentTaskStatus.Completed))
                    {
                        if (outcome.Success)
                            task.Status = KokoAgentTaskStatus.Completed;
                        var notice = KokoAgentCompletionPolicy.Build(task);
                        task.CompletionNotice = notice.Notice;
                        task.NextQuestion = notice.NextQuestion;
                        PersistCompletionNotice(task, notice);
                        try { TaskCompleted?.Invoke(Clone(task), notice); } catch (Exception suppressedEx351) { KokoSystemLog.Write("AGENTTASKSERVICE-CATCH", "ExecuteStepAsync failed near source line 351: " + suppressedEx351); }
                    }
                    SaveLocked();
                }
                var terminal = (task.Status is KokoAgentTaskStatus.Completed or KokoAgentTaskStatus.Failed) &&
                               !string.IsNullOrWhiteSpace(task.CompletionNotice);
                EmitActivity(terminal ? "report" : "observe",
                    terminal ? "CompletionPolicy" : ToolNameFor(step.Kind),
                    terminal ? task.CompletionNotice : step.Result,
                    terminal ? task.NextQuestion : $"Step {step.Order} completed.",
                    task.Id,
                    step.Id);
            }
            catch (OperationCanceledException)
            {
                EmitActivity("observe", "runner", "Canceled by user or shutdown.", "Step canceled.");
                MarkStepFinished(taskId, stepId, KokoAgentTaskStatus.Canceled, "Canceled.");
            }
            catch (Exception ex)
            {
                EmitActivity("observe", "error", ex.Message, "Step failed. Not ideal. Obviously.");
                MarkStepFinished(taskId, stepId, KokoAgentTaskStatus.Failed, ex.Message);
            }
            finally
            {
                lock (_lock) { _runningSteps = Math.Max(0, _runningSteps - 1); }
            }
        }

        private async Task<StepExecutionOutcome> ExecuteStepCoreAsync(KokoAgentTask task, KokoAgentStep step, CancellationToken ct)
        {
            var result = await ExecuteStepResultAsync(task, step, ct).ConfigureAwait(false);
            return ClassifyStepOutcome(step.Kind, result);
        }

        private async Task<string> ExecuteStepResultAsync(KokoAgentTask task, KokoAgentStep step, CancellationToken ct)
        {
            if (step.Kind == KokoAgentStepKind.InsightExtraction)
            {
                EmitActivity("execute", "InsightEngine", task.Objective, "Scanning recent vault notes and extracting actual findings.", task.Id, step.Id);
                return await BuildInsightExtractionReportAsync(task, ct).ConfigureAwait(false);
            }

            if (step.Kind == KokoAgentStepKind.SystemControl)
            {
                EmitActivity("execute", "PcControlService", task.Objective, "Routing a safe OS-control step through the local PC controller.", task.Id, step.Id);
                return await ExecuteSystemControlAsync(task, ct).ConfigureAwait(false);
            }

            if (step.Kind == KokoAgentStepKind.Vision)
            {
                EmitActivity("execute", "VisionModel", task.Objective, "Capturing screen and running concrete visual analysis.", task.Id, step.Id);
                return await ExecuteVisionSnapshotAsync(task, ct).ConfigureAwait(false);
            }

            if (step.Kind == KokoAgentStepKind.Observation)
            {
                EmitActivity("observe", "KokoObservationService", task.Objective, "Starting long-term desktop observation over the full virtual screen.", task.Id, step.Id);
                return await ExecuteObservationAsync(task, step, ct).ConfigureAwait(false);
            }

            if (step.Kind == KokoAgentStepKind.Sandbox)
            {
                EmitActivity("execute", "PythonSandbox", task.Objective, "Running safe sandbox health probe.", task.Id, step.Id);
                return await _sandbox.ExecutePythonAsync("print('sandbox ready')", ct: ct).ConfigureAwait(false);
            }

            if (step.Kind == KokoAgentStepKind.SelfReview)
            {
                EmitActivity("observe", "SelfReview", task.Objective, "Reviewing task outputs before report. Yes, checking the work before bragging.", task.Id, step.Id);
                return BuildSelfReviewReport(task);
            }

            if (step.Kind == KokoAgentStepKind.HardReset)
            {
                EmitActivity("execute", "HardReset", task.Objective, "Refusal detected; forcing tool execution before persona.", task.Id, step.Id);
                return await ExecuteHardResetAsync(task, ct).ConfigureAwait(false);
            }

            if (step.Kind == KokoAgentStepKind.Report && task.Steps.Any(s => s.Kind == KokoAgentStepKind.Observation))
                return BuildObservationReport(task);

            if (_llm == null)
                return $"Simulated {step.Kind}: {step.Title}";

            var directVaultContext = "";
            if (step.Kind == KokoAgentStepKind.Vault && _obsidian != null)
            {
                directVaultContext = new ObsidianPreflightContextService(_obsidian)
                    .Build(task.Objective, maxChars: 2400) ?? "";
                if (!string.IsNullOrWhiteSpace(directVaultContext))
                    EmitActivity("execute", "ObsidianPreflight", task.Objective, "Vault context loaded before the model gets to improvise.", task.Id, step.Id);
            }

            var vaultHint = step.Kind == KokoAgentStepKind.Vault && _obsidian != null
                ? "\nVault tools are allowed. Read before writing."
                : "";
            var toolCatalog = step.Kind is KokoAgentStepKind.Plan or KokoAgentStepKind.Implement or KokoAgentStepKind.Vault
                ? "\nAvailable model tools:\n" + _llm.DescribeAvailableTools()
                    + "\n\nVerified gateway tools:\n"
                    + ServiceContainer.ToolGateway.BuildToolPromptBlock(ToolCapabilitiesFor(step.Kind))
                : "";
            EmitActivity("analyze", ToolNameFor(step.Kind), task.Objective, $"Asking model for {step.Kind} result.", task.Id, step.Id);
            var prompt = $"""
            You are Kokonoe's local task executor.
            Objective: {task.Objective}
            Step {step.Order}: {step.Title}
            Step kind: {step.Kind}
            Direct vault context:
            {directVaultContext}

            Return a concise result, risks, and next action. Do not claim external work unless actually done.
            {vaultHint}
            {toolCatalog}
            """;
            var result = await _llm.SendSystemQueryAsync(
                prompt,
                useTools: step.Kind == KokoAgentStepKind.Vault,
                ct,
                agentId: AgentIdFor(step.Kind)).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(result) ? "(empty result)" : result.Trim();
        }

        private static StepExecutionOutcome ClassifyStepOutcome(KokoAgentStepKind kind, string? result)
        {
            var text = result?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(text) || text.Equals("(empty result)", StringComparison.OrdinalIgnoreCase))
                return StepExecutionOutcome.Failed(text, $"{kind} executor returned an empty result.");

            var operationalFailure = kind switch
            {
                KokoAgentStepKind.Vision => StartsWithAny(text,
                    "Vision failed",
                    "Vision skipped because no LLM vision route is attached"),
                KokoAgentStepKind.SystemControl => StartsWithAny(text,
                    "SystemControl skipped",
                    "SystemControl blocked"),
                KokoAgentStepKind.InsightExtraction => StartsWithAny(text,
                    "InsightExtraction: Obsidian service is unavailable"),
                KokoAgentStepKind.Observation or KokoAgentStepKind.Report => StartsWithAny(text,
                    "Observation report: no observation log was produced"),
                _ => false
            };

            return operationalFailure
                ? StepExecutionOutcome.Failed(text, Trim(FirstUsefulLine(text), 320))
                : StepExecutionOutcome.Succeeded(text);
        }

        private static bool StartsWithAny(string text, params string[] prefixes)
            => prefixes.Any(prefix => text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        private static string BuildSelfReviewReport(KokoAgentTask task)
        {
            var findings = new List<string>();
            var completed = task.Steps
                .Where(s => s.Status == KokoAgentTaskStatus.Completed && s.Kind != KokoAgentStepKind.SelfReview)
                .OrderBy(s => s.Order)
                .ToList();
            var score = 10;

            var failed = task.Steps.Count(s => s.Status == KokoAgentTaskStatus.Failed);
            if (failed > 0)
            {
                score -= Math.Min(4, failed * 2);
                findings.Add($"{failed} failed step(s) remain.");
            }

            var empty = completed.Count(s => string.IsNullOrWhiteSpace(s.Result));
            if (empty > 0)
            {
                score -= Math.Min(3, empty);
                findings.Add($"{empty} completed step(s) have no result.");
            }

            var resultBlob = string.Join("\n", completed.Select(s => s.Result ?? ""));
            var objective = task.Objective ?? "";
            var lowerObjective = objective.ToLowerInvariant();
            var lowerBlob = resultBlob.ToLowerInvariant();

            if (ContainsAny(lowerObjective, "background vault", "фоновий", "obsidian", "vault") &&
                !ContainsAny(lowerBlob, "insightextraction завершено", "vault_cluster.py", "інсайти", "тематичні кластери"))
            {
                score -= 3;
                findings.Add("Vault/background task has no concrete insight or cluster output.");
            }

            if (ContainsAny(lowerObjective, "systemcontrol", "system control", "ps:", "powershell", "процеси", "ram") &&
                !ContainsAny(lowerBlob, "systemcontrol завершено", "powershell:", "koko-system-ok", "топ процесів"))
            {
                score -= 2;
                findings.Add("System-control task has no local execution output.");
            }

            if (ContainsAny(lowerBlob, "чекаю наступного запиту", "просто дай", "я не можу", "не маю доступу", "simulated implement"))
            {
                score -= 2;
                findings.Add("Result contains a weak fallback, denial, or simulated implementation.");
            }

            if (LooksLikeExecutionRefusalOrLazy(resultBlob))
            {
                score -= 4;
                findings.Add("Refusal/lazy response detected: execution was requested but the result asked the user to provide obvious tool input or said it cannot/will not act.");
            }

            if (task.Steps.Any(s => s.Kind == KokoAgentStepKind.Observation) &&
                !ContainsAny(lowerBlob, "observation", "capture ", "зібрано кадр", "зiбрано кадр"))
            {
                score -= 3;
                findings.Add("Observation task has no captured-frame log or final report.");
            }

            score = Math.Clamp(score, 1, 10);
            if (findings.Count == 0)
                findings.Add("No blocking defects found.");

            var verdict = score >= 7 ? "pass" : "needs_correction";
            return $"""
            SelfReview score: {score}/10
            Verdict: {verdict}
            Findings:
            - {string.Join("\n- ", findings.Select(f => Trim(f, 180)))}
            """.Trim();
        }

        private static bool TryExtractSelfReviewScore(string? text, out int score)
        {
            score = 10;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            var match = System.Text.RegularExpressions.Regex.Match(text, @"score\s*:\s*(\d{1,2})\s*/\s*10", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out score))
                return false;
            score = Math.Clamp(score, 1, 10);
            return true;
        }

        private static bool InsertCorrectionStepLocked(KokoAgentTask task, KokoAgentStep selfReview, int score)
        {
            var marker = $"self-review:{selfReview.Id}";
            if (task.Steps.Any(s => s.Title.Contains(marker, StringComparison.OrdinalIgnoreCase)))
                return false;

            var order = selfReview.Order + 1;
            foreach (var step in task.Steps.Where(s => s.Order >= order))
                step.Order++;

            var hardReset = LooksLikeExecutionRefusalOrLazy(selfReview.Result);
            task.Steps.Add(new KokoAgentStep
            {
                Order = order,
                Title = hardReset
                    ? $"HardReset executor from {marker} (score {score}/10)"
                    : $"Correct findings from {marker} (score {score}/10)",
                Kind = hardReset ? KokoAgentStepKind.HardReset : KokoAgentStepKind.Implement,
                Status = KokoAgentTaskStatus.Pending
            });
            task.Steps.Sort((a, b) => a.Order.CompareTo(b.Order));
            return true;
        }

        private static async Task<string> ExecuteSystemControlAsync(KokoAgentTask task, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var objective = task.Objective ?? "";
            var pcPlan = PcIntentRouter.TryBuildActionPlan(objective);
            if (pcPlan == null &&
                (KokoScreenIntent.IsManualScreenScan(objective) ||
                 KokoResponsePlannerEngine.LooksLikeFullContextScanObjective(objective)))
            {
                pcPlan = PcActionPlan.Single("SystemControl full context scan", "getContextV2", "Normal", PcActionRiskTier.Observe);
            }

            if (pcPlan == null)
            {
                var plannedShell = BuildSafeSystemControlCommand(objective);
                if (!string.IsNullOrWhiteSpace(plannedShell))
                {
                    pcPlan = PcActionPlan.Single("SystemControl shell probe", "shell", plannedShell, PcActionRiskTier.RiskyLocal);
                    pcPlan.Actions[0].Arguments["command"] = plannedShell;
                }
            }

            if (pcPlan == null)
                return "SystemControl skipped: objective did not contain a plannable PC action.";

            var gatewayResult = await ServiceContainer.ToolGateway.ExecuteAsync(new KokoToolCall
            {
                Name = "pc_action",
                Payload = pcPlan
            }, ct).ConfigureAwait(false);
            var pcExecution = gatewayResult.RawResult as PcActionExecutionResult;
            return $"""
            SystemControl routed through IKokoToolGateway.
            - Action: {pcPlan.Actions.FirstOrDefault()?.ActionType}
            - Decision: {pcExecution?.Decision.Kind}
            - Result:
            {TrimBlock(pcExecution != null ? PcIntentRouter.FormatExecutionResult(pcExecution) : gatewayResult.ToLlmText(), 2200)}
            """.Trim();

#if false
            if (KokoScreenIntent.IsManualScreenScan(objective) ||
                KokoResponsePlannerEngine.LooksLikeFullContextScanObjective(objective))
            {
                var context = ServiceContainer.PcControl.GetContextV2(PcObservationMode.Normal);
                return $"""
                SystemControl completed through full context scan.
                {TrimBlock(PcIntentRouter.FormatAllContext(context), 2200)}
                """.Trim();
            }

            var routed = await PcIntentRouter.TryExecuteAsync(objective, ServiceContainer.PcControl, ct).ConfigureAwait(false);
            if (routed.Handled)
            {
                return $"""
                SystemControl завершено через PcIntentRouter.
                - Action: {routed.Action}
                - Result:
                {TrimBlock(routed.Reply, 1800)}
                """.Trim();
            }

            var command = BuildSafeSystemControlCommand(objective);
            if (string.IsNullOrWhiteSpace(command))
                return "SystemControl пропущено: objective не містить безпечної системної дії.";

            if (PcCommandSafety.IsBlocked(command, out var reason))
                return "SystemControl заблоковано: " + reason;

            var output = await ServiceContainer.PcControl
                .RunCommandAsync(command, timeoutMs: 8_000, enforceSafety: true)
                .ConfigureAwait(false);
            return $"""
            SystemControl завершено через PowerShell.
            - Command: {Trim(command, 260)}
            - Output:
            {TrimBlock(output, 1800)}
            """.Trim();
#endif
        }

        private static string BuildSafeSystemControlCommand(string objective)
        {
            var text = objective ?? "";
            var lower = text.ToLowerInvariant();
            var ps = ExtractPowerShellPayload(text);
            if (!string.IsNullOrWhiteSpace(ps))
                return ps;

            if (ContainsAny(lower, "process", "процес", "tasklist", "ram", "пам'ять", "память", "memory", "що жере"))
            {
                return "Get-Process | Sort-Object WorkingSet -Descending | Select-Object -First 12 ProcessName,Id,@{Name='MB';Expression={[math]::Round($_.WorkingSet/1MB,1)}} | Format-Table -AutoSize";
            }

            if (ContainsAny(lower, "temp", "temporary", "cleanup", "clean up", "очист", "сміт", "мусор"))
            {
                return "$temp=[System.IO.Path]::GetTempPath(); $m=Get-ChildItem -LiteralPath $temp -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum; [pscustomobject]@{TempPath=$temp; Files=$m.Count; MB=[math]::Round(($m.Sum/1MB),1)} | Format-List";
            }

            if (ContainsAny(lower, "disk", "диск", "місце", "место", "storage"))
            {
                return "Get-PSDrive -PSProvider FileSystem | Select-Object Name,@{Name='FreeGB';Expression={[math]::Round($_.Free/1GB,1)}},@{Name='UsedGB';Expression={[math]::Round($_.Used/1GB,1)}} | Format-Table -AutoSize";
            }

            if (ContainsAny(lower, "sysinfo", "system info", "статус пк", "стан пк", "систем"))
            {
                return "$os=Get-CimInstance Win32_OperatingSystem; [pscustomobject]@{Caption=$os.Caption; Version=$os.Version; FreeMemoryMB=[math]::Round($os.FreePhysicalMemory/1024,1); TotalMemoryMB=[math]::Round($os.TotalVisibleMemorySize/1024,1); Uptime=((Get-Date)-$os.LastBootUpTime).ToString()} | Format-List";
            }

            return "";
        }

        private async Task<string> ExecuteVisionSnapshotAsync(KokoAgentTask task, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var pc = ServiceContainer.PcControl;
            var context = pc.GetAllContext();
            byte[] screenshot;
            try
            {
                screenshot = pc.TakeScreenshot(minimizeSelf: true, restoreSelf: true, settleMs: 240);
            }
            catch (Exception ex)
            {
                return "Vision failed at local screenshot capture: " + ex.Message + "\n" + TrimBlock(context.ToString(), 1800);
            }

            if (_llm == null)
            {
                return $"""
                Vision skipped because no LLM vision route is attached.
                Local context:
                {TrimBlock(context.ToString(), 1800)}
                """.Trim();
            }

            var prompt = $"""
            You are Kokonoe's vision executor. Execution precedes persona.
            Objective: {task.Objective}

            Local PC context already collected:
            {TrimBlock(context.ToString(), 2200)}

            Inspect the screenshot. List the visible app/window/tab evidence first, then add one concise useful judgement.
            Do not say you cannot see unless the image is truly blank. Do not ask for a screenshot: you already have one.
            Never copy private tokens or secrets; name them as secrets.
            Ukrainian, concise, competent/sarcastic.
            """;

            string? reply = null;
            using (var visionCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                visionCts.CancelAfter(TimeSpan.FromSeconds(90));
                reply = await _llm.SendSystemVisionQueryAsync(
                    prompt,
                    screenshot,
                    "image/jpeg",
                    visionCts.Token,
                    maxTokensOverride: 2048).ConfigureAwait(false);
            }

            if (VisionResponseQuality.LooksUnusable(reply) || VisionResponseQuality.LooksGeneric(reply))
            {
                try
                {
                    var enhanced = ImageProcessingService.EnhanceForVision(screenshot);
                    var retryPrompt = VisionResponseQuality.BuildRetryPrompt(prompt, context.Foreground.ToString());
                    using var retryCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    retryCts.CancelAfter(TimeSpan.FromSeconds(120));
                    var repaired = await _llm.SendSystemVisionQueryAsync(
                        retryPrompt,
                        enhanced.Length > 0 ? enhanced : screenshot,
                        "image/jpeg",
                        retryCts.Token,
                        maxTokensOverride: 3072).ConfigureAwait(false);
                    if (!VisionResponseQuality.LooksUnusable(repaired) && !VisionResponseQuality.LooksGeneric(repaired))
                        reply = repaired;
                }
                catch (Exception suppressedEx752) { KokoSystemLog.Write("AGENTTASKSERVICE-CATCH", "ExecuteVisionSnapshotAsync failed near source line 752: " + suppressedEx752); }
            }

            if (string.IsNullOrWhiteSpace(reply) ||
                VisionResponseQuality.LooksUnusable(reply) ||
                KokoScreenIntent.LooksLikeScreenCapabilityDenial(reply))
            {
                return $"""
                Vision returned no usable visual text, but local execution did not fail.
                Context fallback:
                {TrimBlock(context.ToString(), 2200)}
                """.Trim();
            }

            return $"""
            Vision completed.
            Context:
            {TrimBlock(PcIntentRouter.FormatAllContext(context), 1600)}

            Vision:
            {TrimBlock(reply.Trim(), 2200)}
            """.Trim();
        }

        private async Task<string> ExecuteHardResetAsync(KokoAgentTask task, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var objective = task.Objective ?? "";
            var lower = objective.ToLowerInvariant();

            if (KokoScreenIntent.IsManualScreenScan(objective) ||
                KokoResponsePlannerEngine.LooksLikeFullContextScanObjective(lower) ||
                ContainsAny(lower, "screen", "desktop", "window", "tabs", "вікн", "вклад"))
            {
                var context = ServiceContainer.PcControl.GetAllContext();
                return $"""
                HardReset executed: local full-context scan completed after refusal.
                {TrimBlock(context.ToString(), 2400)}
                """.Trim();
            }

            if (ContainsAny(lower, "systemcontrol", "system control", "pc control", "os control", "powershell", "ps:", "sysinfo", "process", "ram"))
                return await ExecuteSystemControlAsync(task, ct).ConfigureAwait(false);

            if ((ContainsAny(lower, "obsidian", "vault", "нотат", "пам'ят", "памят") || task.Steps.Any(s => s.Kind == KokoAgentStepKind.Vault)) &&
                _obsidian != null)
            {
                var context = new ObsidianPreflightContextService(_obsidian).Build(objective, maxChars: 2600) ?? "";
                if (string.IsNullOrWhiteSpace(context))
                    context = _obsidian.GetVaultStatus().ToString();
                return $"""
                HardReset executed: vault context loaded after refusal.
                {TrimBlock(context, 2600)}
                """.Trim();
            }

            if (_llm == null)
                return "HardReset executed: no specialized tool matched, and no LLM route is attached.";

            var prompt = $"""
            HARD RESET EXECUTOR.
            You are Kokonoe's local executor mode: pragmatic, terse, artifact-driven.
            Objective: {objective}
            Refusal/lazy/theatrical responses are invalid. Give the concrete next action/result, changed path, status, or failure cause.
            Do not promise background work unless a real task/status artifact already exists.
            Ukrainian, concise.
            """;

            var result = await _llm.SendSystemQueryAsync(prompt, useTools: true, ct: ct, agentId: "system").ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(result) ? "HardReset executed but model returned empty text." : result.Trim();
        }

        private static string ExtractPowerShellPayload(string text)
        {
            foreach (var marker in new[] { "ps:", "powershell:", "pwsh:", "shell:", "команда:" })
            {
                var index = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (index >= 0)
                    return text[(index + marker.Length)..].Trim();
            }
            return "";
        }

        private async Task<string> ExecuteObservationAsync(KokoAgentTask task, KokoAgentStep step, CancellationToken ct)
        {
            var options = KokoObservationService.BuildOptions(task.Objective, task.Id);
            var result = await _observation.RunAsync(
                options,
                progress =>
                {
                    EmitActivity(
                        "observe",
                        "KokoObservationService",
                        task.Objective,
                        $"capture {progress.Index}/{progress.Total}: {progress.Summary}",
                        task.Id,
                        step.Id);
                    return Task.CompletedTask;
                },
                ct).ConfigureAwait(false);

            return $"""
            Observation завершено.
            {result.Summary}
            """.Trim();
        }

        private static string BuildObservationReport(KokoAgentTask task)
        {
            var observation = task.Steps
                .Where(s => s.Kind == KokoAgentStepKind.Observation && !string.IsNullOrWhiteSpace(s.Result))
                .OrderByDescending(s => s.FinishedAt ?? DateTime.MinValue)
                .Select(s => s.Result)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(observation))
                return "Observation report: no observation log was produced.";

            return $"""
            Observation report:
            {TrimBlock(observation, 2200)}
            """.Trim();
        }

        private void PersistCompletionNotice(KokoAgentTask task, KokoAgentCompletionNotice notice)
        {
            if (_obsidian == null) return;
            try
            {
                var result = task.Steps
                    .Where(s => !string.IsNullOrWhiteSpace(s.Result))
                    .OrderByDescending(s => s.FinishedAt ?? DateTime.MinValue)
                    .Select(s => Trim(s.Result, 1200))
                    .FirstOrDefault() ?? "";
                var line = $"""

## {DateTime.Now:yyyy-MM-dd HH:mm} · {task.Id}
- objective: {task.Objective}
- status: {task.Status}
- notice: {notice.Notice}
{(string.IsNullOrWhiteSpace(result) ? "" : "- result: " + result.Replace("\n", "\n  "))}
""";
                _obsidian.AppendToNote("Kokonoe/Agent/Completions.md", line);
            }
            catch (Exception suppressedEx896) { KokoSystemLog.Write("AGENTTASKSERVICE-CATCH", "PersistCompletionNotice failed near source line 896: " + suppressedEx896); }
        }

        private async Task<string> BuildInsightExtractionReportAsync(KokoAgentTask task, CancellationToken ct)
        {
            if (_obsidian == null)
                return "InsightExtraction: Obsidian service is unavailable; no vault scan was executed.";

            ct.ThrowIfCancellationRequested();
            var status = _obsidian.GetVaultStatus();
            EmitActivity("execute", "ObsidianMcpService", task.Objective,
                $"Vault status: {status.TotalNotes} notes, {status.EmptyNotes.Count} empty, {status.OrphanNotes.Count} orphan.",
                task.Id);

            var notes = _obsidian.ListNotes()
                .Where(p => p.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                .Where(p => !p.Contains("kokonoe-data", StringComparison.OrdinalIgnoreCase))
                .Where(p => !p.StartsWith("Kokonoe/Agent/", StringComparison.OrdinalIgnoreCase))
                .Select(p => new
                {
                    Path = p,
                    Modified = GetNoteModifiedAt(p)
                })
                .OrderByDescending(x => x.Modified)
                .Take(10)
                .ToList();

            EmitActivity("execute", "ObsidianMcpService", task.Objective,
                $"Reading {notes.Count} recently modified notes. Yes, actual files, not vibes.",
                task.Id);

            var findings = notes
                .Select(n => BuildFinding(n.Path, n.Modified))
                .Where(f => f.Score > 0)
                .OrderByDescending(f => f.Score)
                .ThenByDescending(f => f.ModifiedAt)
                .Take(5)
                .ToList();

            var clusterReport = await BuildVaultClusterReportAsync(task, ct).ConfigureAwait(false);

            var sb = new StringBuilder();
            sb.AppendLine("InsightExtraction завершено.");
            sb.AppendLine($"- Перевірено: {notes.Count} останніх markdown-нотаток.");
            sb.AppendLine($"- Vault: {status.TotalNotes} нотаток, {status.FilledNotes} заповнених, {status.EmptyNotes.Count} порожніх, {status.OrphanNotes.Count} осиротілих.");

            if (findings.Count == 0)
            {
                sb.AppendLine("- Інсайт: у останніх нотатках не знайшла достатньо сильного сигналу. Це результат, не зависання.");
            }
            else
            {
                sb.AppendLine("- Інсайти:");
                foreach (var finding in findings)
                    sb.AppendLine($"  - `{finding.Path}`: {finding.Preview}");
            }

            if (!string.IsNullOrWhiteSpace(clusterReport))
            {
                sb.AppendLine("- Тематичні кластери:");
                foreach (var line in clusterReport.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(8))
                    sb.AppendLine("  - " + line.Trim());
            }

            var report = sb.ToString().Trim();
            try
            {
                _obsidian.AppendToNote("Kokonoe/Agent/Insights.md", $"""

## {DateTime.Now:yyyy-MM-dd HH:mm} · {task.Id}
Objective: {task.Objective}

{report}
""");
            }
            catch (Exception suppressedEx971) { KokoSystemLog.Write("AGENTTASKSERVICE-CATCH", "BuildInsightExtractionReportAsync failed near source line 971: " + suppressedEx971); }

            return report;
        }

        private async Task<string> BuildVaultClusterReportAsync(KokoAgentTask task, CancellationToken ct)
        {
            if (_obsidian == null)
                return "";

            try
            {
                var scriptPath = ResolveVaultClusterScriptPath();
                if (string.IsNullOrWhiteSpace(scriptPath) || !File.Exists(scriptPath))
                    return "";

                ct.ThrowIfCancellationRequested();
                EmitActivity("execute", "ThematicClustering", task.Objective, "Running vault_cluster.py over the vault. Actual clustering, not mystic noise.", task.Id);
                var script = File.ReadAllText(scriptPath, Encoding.UTF8);
                var code = "VAULT_PATH = " + JsonConvert.SerializeObject(_obsidian.VaultPath) + "\n" +
                           "LIMIT = 120\n" +
                           script;
                var raw = await _sandbox.ExecutePythonAsync(code, timeoutMs: 12_000, ct: ct, stdoutLimit: 12_000).ConfigureAwait(false);
                var stdout = ExtractSandboxStdout(raw);
                if (string.IsNullOrWhiteSpace(stdout))
                    return "clustering skipped: " + Trim(raw, 260);

                var json = JObject.Parse(stdout);
                if (!string.IsNullOrWhiteSpace((string?)json["error"]))
                    return "clustering error: " + Trim((string?)json["error"] ?? "", 220);

                var scanned = (int?)json["scanned"] ?? 0;
                var clusters = json["clusters"] as JArray;
                if (clusters == null || clusters.Count == 0)
                    return $"vault_cluster.py: scanned {scanned} notes; stable clusters not found.";

                var sb = new StringBuilder();
                sb.AppendLine($"vault_cluster.py: {clusters.Count} themes from {scanned} scanned notes.");
                foreach (var cluster in clusters.Take(4))
                {
                    var theme = (string?)cluster["theme"] ?? "unknown";
                    var files = cluster["files"]?.Select(v => v?.ToString() ?? "").Where(v => v.Length > 0).Take(3).ToList() ?? new List<string>();
                    var keywords = cluster["keywords"]?.Select(v => v?.ToString() ?? "").Where(v => v.Length > 0).Take(5).ToList() ?? new List<string>();
                    var observation = (string?)cluster["observation"] ?? "";
                    sb.AppendLine($"{theme}: {observation} files=[{string.Join(", ", files)}] keywords=[{string.Join(", ", keywords)}]");
                }
                var consolidation = BuildClusterConsolidationPlan(clusters);
                if (!string.IsNullOrWhiteSpace(consolidation))
                    sb.AppendLine(consolidation);
                return sb.ToString().Trim();
            }
            catch (Exception ex)
            {
                return "clustering failed: " + Trim(ex.Message, 220);
            }
        }

        private string BuildClusterConsolidationPlan(JArray clusters)
        {
            if (_obsidian == null || clusters.Count == 0)
                return "";

            var autonomy = Math.Clamp(AppSettings.Load().ProactiveAutonomyLevel, 0, 3);
            var sb = new StringBuilder();
            foreach (var cluster in clusters.Take(5))
            {
                var theme = ((string?)cluster["theme"] ?? "unknown").Trim();
                var files = cluster["files"]?
                    .Select(v => v?.ToString() ?? "")
                    .Where(v => v.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(5)
                    .ToArray() ?? Array.Empty<string>();
                var size = (int?)cluster["size"] ?? files.Length;
                var folders = files
                    .Select(f => f.Contains('/') ? f[..f.IndexOf('/')] : "")
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();
                var messy = size >= 3 && files.Length >= 3 && folders >= 2;
                if (!messy)
                    continue;

                if (autonomy >= 3)
                {
                    var target = $"Kokonoe/Agent/Consolidated/{DateTime.Now:yyyyMMdd-HHmm}-{SanitizePathPart(theme)}.md";
                    try
                    {
                        var written = _obsidian.ConsolidateNotes(files, target);
                        sb.AppendLine($"consolidation executed: {theme} -> `{written}` from [{string.Join(", ", files.Take(4))}]");
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"consolidation failed for {theme}: {Trim(ex.Message, 180)}");
                    }
                }
                else
                {
                    sb.AppendLine($"messy cluster proposal: consolidate {theme} into Kokonoe/Agent/Consolidated/{SanitizePathPart(theme)}.md from [{string.Join(", ", files.Take(4))}]");
                }
            }
            return sb.ToString().Trim();
        }

        private static string SanitizePathPart(string value)
        {
            value = string.IsNullOrWhiteSpace(value) ? "cluster" : value.Trim().ToLowerInvariant();
            foreach (var ch in Path.GetInvalidFileNameChars())
                value = value.Replace(ch, '-');
            value = new string(value.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray());
            while (value.Contains("--", StringComparison.Ordinal))
                value = value.Replace("--", "-", StringComparison.Ordinal);
            value = value.Trim('-');
            return string.IsNullOrWhiteSpace(value) ? "cluster" : value[..Math.Min(value.Length, 48)];
        }

        private static string ResolveVaultClusterScriptPath()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null)
            {
                var candidate = Path.Combine(current.FullName, "Services", "agent-sandbox", "vault_cluster.py");
                if (File.Exists(candidate))
                    return candidate;
                current = current.Parent;
            }
            return "";
        }

        private static string ExtractSandboxStdout(string sandboxOutput)
        {
            if (string.IsNullOrWhiteSpace(sandboxOutput))
                return "";
            var stdoutMarker = "stdout:";
            var stdoutIndex = sandboxOutput.IndexOf(stdoutMarker, StringComparison.OrdinalIgnoreCase);
            if (stdoutIndex < 0)
                return "";
            var text = sandboxOutput[(stdoutIndex + stdoutMarker.Length)..].Trim();
            var stderrIndex = text.IndexOf("\nstderr:", StringComparison.OrdinalIgnoreCase);
            if (stderrIndex >= 0)
                text = text[..stderrIndex].Trim();
            return text;
        }

        private DateTime GetNoteModifiedAt(string path)
        {
            try
            {
                var full = Path.Combine(_obsidian!.VaultPath, path.Replace('/', Path.DirectorySeparatorChar));
                return File.Exists(full) ? File.GetLastWriteTime(full) : DateTime.MinValue;
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        private InsightFinding BuildFinding(string path, DateTime modifiedAt)
        {
            try
            {
                var content = _obsidian?.ReadNote(path) ?? "";
                if (string.IsNullOrWhiteSpace(content))
                    return new InsightFinding { Path = path, ModifiedAt = modifiedAt };
                if (content.Contains("managed-by: Kokonoe", StringComparison.OrdinalIgnoreCase))
                    return new InsightFinding { Path = path, ModifiedAt = modifiedAt };

                var clean = CleanMarkdown(content);
                var lower = (path + "\n" + clean).ToLowerInvariant();
                var score = 0;
                score += CountAny(lower, "ідея", "idea", "інсайт", "insight", "спостереж", "патерн", "pattern", "ризик", "план", "todo", "task", "помилка", "баг", "архітектур", "мультизадач");
                if (modifiedAt > DateTime.Now.AddHours(-24)) score += 4;
                if (modifiedAt > DateTime.Now.AddDays(-7)) score += 2;
                if (clean.Length > 300) score += 1;

                return new InsightFinding
                {
                    Path = path,
                    ModifiedAt = modifiedAt,
                    Score = score,
                    Preview = Trim(FirstUsefulLine(clean), 260)
                };
            }
            catch
            {
                return new InsightFinding { Path = path, ModifiedAt = modifiedAt };
            }
        }

        private static string CleanMarkdown(string content)
        {
            var lines = content
                .Replace("\r", "\n")
                .Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .Where(l => !l.StartsWith("---"))
                .Where(l => !l.StartsWith("tags:", StringComparison.OrdinalIgnoreCase))
                .Where(l => !l.StartsWith("created:", StringComparison.OrdinalIgnoreCase));
            return string.Join(" ", lines).Trim();
        }

        private static string FirstUsefulLine(string text)
        {
            return text
                .Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim().Trim('-', '*', '#', ' '))
                .FirstOrDefault(t => t.Length >= 24)
                ?? text.Trim();
        }

        private static int CountAny(string text, params string[] terms)
        {
            var score = 0;
            foreach (var term in terms)
                if (text.Contains(term, StringComparison.OrdinalIgnoreCase))
                    score++;
            return score;
        }

        private void MarkStepFinished(string taskId, string stepId, KokoAgentTaskStatus status, string error)
        {
            lock (_lock)
            {
                var task = _tasks.FirstOrDefault(t => t.Id == taskId);
                var step = task?.Steps.FirstOrDefault(s => s.Id == stepId);
                if (task == null || step == null) return;
                step.Status = status;
                step.Error = error;
                step.FinishedAt = DateTime.Now;
                task.Status = status;
                task.UpdatedAt = DateTime.Now;
                if (status == KokoAgentTaskStatus.Failed)
                {
                    var notice = KokoAgentCompletionPolicy.Build(task);
                    task.CompletionNotice = notice.Notice;
                    task.NextQuestion = notice.NextQuestion;
                    PersistCompletionNotice(task, notice);
                    try { TaskCompleted?.Invoke(Clone(task), notice); } catch (Exception completionEx) { KokoSystemLog.Write("AGENTTASKSERVICE-CATCH", "Failed-task completion notification failed: " + completionEx); }
                }
                SaveLocked();
            }
        }

        private static KokoAgentStep Step(int order, string title, KokoAgentStepKind kind) => new()
        {
            Order = order,
            Title = title,
            Kind = kind
        };

        private static KokoAgentStep CloneStep(KokoAgentStep step) => new()
        {
            Id = step.Id,
            Order = step.Order,
            Title = step.Title,
            Kind = step.Kind,
            Status = step.Status,
            Result = step.Result,
            Error = step.Error,
            StartedAt = step.StartedAt,
            FinishedAt = step.FinishedAt
        };

        private void Load()
        {
            try
            {
                if (!File.Exists(_path)) return;
                var loaded = JsonConvert.DeserializeObject<List<KokoAgentTask>>(File.ReadAllText(_path));
                if (loaded == null) return;
                _tasks.Clear();
                _tasks.AddRange(loaded);
            }
            catch (Exception suppressedEx1236) { KokoSystemLog.Write("AGENTTASKSERVICE-CATCH", "Load failed near source line 1236: " + suppressedEx1236); }
        }

        private void SaveLocked()
        {
            File.WriteAllText(_path, JsonConvert.SerializeObject(_tasks, Formatting.Indented));
        }

        private void EmitActivity(string phase, string tool, string focus, string thought, string taskId = "", string stepId = "")
        {
            KokoAgentActivitySnapshot snapshot;
            lock (_lock)
            {
                _activity = new KokoAgentActivitySnapshot
                {
                    UpdatedAt = DateTime.Now,
                    Phase = phase,
                    Tool = tool,
                    Focus = Trim(focus, 420),
                    Thought = Trim(thought, 220),
                    TaskId = taskId,
                    StepId = stepId
                };
                snapshot = CloneActivity(_activity);
            }

            try { ActivityChanged?.Invoke(snapshot); } catch (Exception suppressedEx1262) { KokoSystemLog.Write("AGENTTASKSERVICE-CATCH", "EmitActivity failed near source line 1262: " + suppressedEx1262); }
        }

        private static string ToolNameFor(KokoAgentStepKind kind) => kind switch
        {
            KokoAgentStepKind.Analyze => "KokoBrainEngine",
            KokoAgentStepKind.Plan => "KokoResponsePlanner",
            KokoAgentStepKind.Vault => "ObsidianMcpService",
            KokoAgentStepKind.Vision => "VisionModel",
            KokoAgentStepKind.Sandbox => "PythonSandbox",
            KokoAgentStepKind.SystemControl => "PcControlService",
            KokoAgentStepKind.Observation => "KokoObservationService",
            KokoAgentStepKind.InsightExtraction => "InsightEngine",
            KokoAgentStepKind.Implement => "LlmService",
            KokoAgentStepKind.Respond => "LlmService",
            KokoAgentStepKind.Verify => "Verifier",
            KokoAgentStepKind.SelfReview => "SelfReview",
            KokoAgentStepKind.HardReset => "HardReset",
            KokoAgentStepKind.Report => "Reporter",
            _ => "unknown"
        };

        private static IReadOnlyCollection<string> ToolCapabilitiesFor(KokoAgentStepKind kind) => kind switch
        {
            KokoAgentStepKind.Vault => new[] { "vault-read", "vault-write" },
            KokoAgentStepKind.Implement => new[] { "code", "sandbox", "files" },
            KokoAgentStepKind.Plan => new[] { "routing" },
            _ => Array.Empty<string>()
        };

        private static string AgentIdFor(KokoAgentStepKind kind) => kind switch
        {
            KokoAgentStepKind.Vault => "obsidian",
            KokoAgentStepKind.Sandbox => "coder",
            KokoAgentStepKind.SystemControl => "system-overlord",
            KokoAgentStepKind.Observation => "vision-observer",
            KokoAgentStepKind.InsightExtraction => "research",
            KokoAgentStepKind.Implement => "coder",
            KokoAgentStepKind.Verify => "system",
            KokoAgentStepKind.SelfReview => "research",
            KokoAgentStepKind.HardReset => "system",
            KokoAgentStepKind.Plan => "research",
            _ => "system"
        };

        private static KokoAgentTask Clone(KokoAgentTask task)
        {
            return JsonConvert.DeserializeObject<KokoAgentTask>(JsonConvert.SerializeObject(task)) ?? task;
        }

        private static KokoAgentActivitySnapshot CloneActivity(KokoAgentActivitySnapshot activity)
        {
            return JsonConvert.DeserializeObject<KokoAgentActivitySnapshot>(JsonConvert.SerializeObject(activity)) ?? activity;
        }

        private static bool ContainsAny(string text, params string[] needles)
            => needles.Any(n => text.Contains(n, StringComparison.OrdinalIgnoreCase));

        private static bool LooksLikeExecutionRefusalOrLazy(string? text)
        {
            var lower = (text ?? "").ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(lower))
                return false;

            return ContainsAny(lower,
                "i can't see", "i cannot see", "cannot see the screen", "give me a screenshot", "send me a screenshot",
                "i won't", "i will not", "refusal/lazy response",
                "не бачу", "не можу бачити", "не маю доступу", "немає доступу",
                "зроби скріншот", "надішли скрін", "скинь скрін", "завантаж скрін",
                "не буду", "не можу виконати", "не можу зробити", "просто дай");
        }

        private static string Trim(string text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= max ? text : text[..max] + "...";
        }

        private static string TrimBlock(string? text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Trim();
            return text.Length <= max ? text : text[..max] + "...";
        }

        private sealed class InsightFinding
        {
            public string Path { get; set; } = "";
            public string Preview { get; set; } = "";
            public DateTime ModifiedAt { get; set; }
            public int Score { get; set; }
        }
    }
}
