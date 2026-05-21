using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

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
        Implement,
        Respond,
        Verify,
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
        private readonly object _lock = new();
        private readonly string _path;
        private readonly LlmService? _llm;
        private readonly ObsidianMcpService? _obsidian;
        private readonly KokoSandboxExecutor _sandbox;
        private readonly List<KokoAgentTask> _tasks = new();
        private KokoAgentActivitySnapshot _activity = new();
        private CancellationTokenSource? _runnerCts;
        private int _runningSteps;
        private int _maxParallel = 10;

        public KokoAgentTaskService(string dataDir, LlmService? llm = null, ObsidianMcpService? obsidian = null)
        {
            Directory.CreateDirectory(dataDir);
            _path = Path.Combine(dataDir, "agent-tasks.json");
            _llm = llm;
            _obsidian = obsidian;
            _sandbox = new KokoSandboxExecutor(Path.Combine(dataDir, "agent-sandbox"));
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
                step.Result = await ExecuteStepCoreAsync(task, step, ct).ConfigureAwait(false);

                lock (_lock)
                {
                    task = _tasks.FirstOrDefault(t => t.Id == taskId);
                    step = task?.Steps.FirstOrDefault(s => s.Id == stepId);
                    if (task == null || step == null || step.Status != KokoAgentTaskStatus.Running || task.Status == KokoAgentTaskStatus.Canceled)
                        return;

                    step.Status = KokoAgentTaskStatus.Completed;
                    step.FinishedAt = DateTime.Now;
                    task.UpdatedAt = DateTime.Now;
                    if (task.Steps.All(s => s.Status == KokoAgentTaskStatus.Completed))
                    {
                        task.Status = KokoAgentTaskStatus.Completed;
                        var notice = KokoAgentCompletionPolicy.Build(task);
                        task.CompletionNotice = notice.Notice;
                        task.NextQuestion = notice.NextQuestion;
                        PersistCompletionNotice(task, notice);
                        try { TaskCompleted?.Invoke(Clone(task), notice); } catch { }
                    }
                    SaveLocked();
                }
                var done = task.Status == KokoAgentTaskStatus.Completed && !string.IsNullOrWhiteSpace(task.CompletionNotice);
                EmitActivity(done ? "report" : "observe",
                    done ? "CompletionPolicy" : ToolNameFor(step.Kind),
                    done ? task.CompletionNotice : step.Result,
                    done ? task.NextQuestion : $"Step {step.Order} completed.",
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

        private async Task<string> ExecuteStepCoreAsync(KokoAgentTask task, KokoAgentStep step, CancellationToken ct)
        {
            if (step.Kind == KokoAgentStepKind.Sandbox)
            {
                EmitActivity("execute", "PythonSandbox", task.Objective, "Running safe sandbox health probe.", task.Id, step.Id);
                return await _sandbox.ExecutePythonAsync("print('sandbox ready')", ct: ct).ConfigureAwait(false);
            }

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
                ? "\nAvailable tools:\n" + _llm.DescribeAvailableTools()
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

        private void PersistCompletionNotice(KokoAgentTask task, KokoAgentCompletionNotice notice)
        {
            if (_obsidian == null) return;
            try
            {
                var line = $"""

## {DateTime.Now:yyyy-MM-dd HH:mm} · {task.Id}
- objective: {task.Objective}
- status: {task.Status}
- notice: {notice.Notice}
""";
                _obsidian.AppendToNote("Kokonoe/Agent/Completions.md", line);
            }
            catch { }
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
            catch { }
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

            try { ActivityChanged?.Invoke(snapshot); } catch { }
        }

        private static string ToolNameFor(KokoAgentStepKind kind) => kind switch
        {
            KokoAgentStepKind.Analyze => "KokoBrainEngine",
            KokoAgentStepKind.Plan => "KokoResponsePlanner",
            KokoAgentStepKind.Vault => "ObsidianMcpService",
            KokoAgentStepKind.Vision => "VisionModel",
            KokoAgentStepKind.Sandbox => "PythonSandbox",
            KokoAgentStepKind.Implement => "LlmService",
            KokoAgentStepKind.Respond => "LlmService",
            KokoAgentStepKind.Verify => "Verifier",
            KokoAgentStepKind.Report => "Reporter",
            _ => "unknown"
        };

        private static string AgentIdFor(KokoAgentStepKind kind) => kind switch
        {
            KokoAgentStepKind.Vault => "obsidian",
            KokoAgentStepKind.Sandbox => "coder",
            KokoAgentStepKind.Implement => "coder",
            KokoAgentStepKind.Verify => "system",
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

        private static string Trim(string text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= max ? text : text[..max] + "...";
        }
    }
}
