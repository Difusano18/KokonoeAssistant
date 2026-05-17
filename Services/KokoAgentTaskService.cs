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
        Vault,
        Implement,
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
        public List<KokoAgentStep> Steps { get; set; } = new();
    }

    public sealed class KokoAgentTaskSnapshot
    {
        public DateTime TakenAt { get; set; } = DateTime.Now;
        public int MaxParallel { get; set; }
        public int RunningSteps { get; set; }
        public List<KokoAgentTask> Tasks { get; set; } = new();
    }

    public sealed class KokoAgentTaskService
    {
        private readonly object _lock = new();
        private readonly string _path;
        private readonly LlmService? _llm;
        private readonly ObsidianMcpService? _obsidian;
        private readonly List<KokoAgentTask> _tasks = new();
        private CancellationTokenSource? _runnerCts;
        private int _runningSteps;

        public KokoAgentTaskService(string dataDir, LlmService? llm = null, ObsidianMcpService? obsidian = null)
        {
            Directory.CreateDirectory(dataDir);
            _path = Path.Combine(dataDir, "agent-tasks.json");
            _llm = llm;
            _obsidian = obsidian;
            Load();
        }

        public int MaxParallel { get; set; } = 2;

        public KokoAgentTask AddTask(string objective, int priority = 5)
        {
            if (string.IsNullOrWhiteSpace(objective))
                throw new ArgumentException("Objective is empty.", nameof(objective));

            var task = new KokoAgentTask
            {
                Objective = objective.Trim(),
                Priority = Math.Clamp(priority, 1, 10),
                Steps = BuildPlan(objective).ToList()
            };

            lock (_lock)
            {
                _tasks.Insert(0, task);
                SaveLocked();
            }

            return Clone(task);
        }

        public KokoAgentTaskSnapshot GetSnapshot()
        {
            lock (_lock)
            {
                return new KokoAgentTaskSnapshot
                {
                    MaxParallel = MaxParallel,
                    RunningSteps = _runningSteps,
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
                SaveLocked();
                return true;
            }
        }

        public static IReadOnlyList<KokoAgentStep> BuildPlan(string objective)
        {
            var text = objective.ToLowerInvariant();
            var steps = new List<KokoAgentStep>
            {
                Step(1, "Understand objective and constraints", KokoAgentStepKind.Analyze)
            };

            if (text.Contains("obsidian") || text.Contains("vault") || text.Contains("пам") || text.Contains("нотат"))
                steps.Add(Step(steps.Count + 1, "Inspect vault and related memory notes", KokoAgentStepKind.Vault));

            if (text.Contains("код") || text.Contains("fix") || text.Contains("ui") || text.Contains("gui") || text.Contains("зроб") || text.Contains("реаліз"))
                steps.Add(Step(steps.Count + 1, "Implement the required change", KokoAgentStepKind.Implement));

            steps.Add(Step(steps.Count + 1, "Verify result and detect obvious regressions", KokoAgentStepKind.Verify));
            steps.Add(Step(steps.Count + 1, "Prepare concise handoff report", KokoAgentStepKind.Report));
            return steps;
        }

        public string RenderBoard()
        {
            var snap = GetSnapshot();
            var sb = new StringBuilder();
            sb.AppendLine($"Agent Board | tasks {snap.Tasks.Count} | running {snap.RunningSteps}/{snap.MaxParallel}");
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
                KokoAgentTask? task;
                KokoAgentStep? step;
                lock (_lock)
                {
                    if (_runningSteps >= MaxParallel)
                    {
                        task = null;
                        step = null;
                    }
                    else
                    {
                        (task, step) = FindNextStepLocked();
                        if (task != null && step != null)
                        {
                            task.Status = KokoAgentTaskStatus.Running;
                            task.UpdatedAt = DateTime.Now;
                            step.Status = KokoAgentTaskStatus.Running;
                            step.StartedAt = DateTime.Now;
                            _runningSteps++;
                            SaveLocked();
                        }
                    }
                }

                if (task != null && step != null)
                    _ = Task.Run(() => ExecuteStepAsync(task.Id, step.Id, ct), ct);

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
                var step = task.Steps.OrderBy(s => s.Order).FirstOrDefault(s => s.Status == KokoAgentTaskStatus.Pending);
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
                step.Result = await ExecuteStepCoreAsync(task, step, ct).ConfigureAwait(false);

                lock (_lock)
                {
                    step.Status = KokoAgentTaskStatus.Completed;
                    step.FinishedAt = DateTime.Now;
                    task.UpdatedAt = DateTime.Now;
                    if (task.Steps.All(s => s.Status == KokoAgentTaskStatus.Completed))
                        task.Status = KokoAgentTaskStatus.Completed;
                    SaveLocked();
                }
            }
            catch (OperationCanceledException)
            {
                MarkStepFinished(taskId, stepId, KokoAgentTaskStatus.Canceled, "Canceled.");
            }
            catch (Exception ex)
            {
                MarkStepFinished(taskId, stepId, KokoAgentTaskStatus.Failed, ex.Message);
            }
            finally
            {
                lock (_lock) { _runningSteps = Math.Max(0, _runningSteps - 1); }
            }
        }

        private async Task<string> ExecuteStepCoreAsync(KokoAgentTask task, KokoAgentStep step, CancellationToken ct)
        {
            if (_llm == null)
                return $"Simulated {step.Kind}: {step.Title}";

            var vaultHint = step.Kind == KokoAgentStepKind.Vault && _obsidian != null
                ? "\nVault tools are allowed. Read before writing."
                : "";
            var prompt = $"""
            You are Kokonoe's local task executor.
            Objective: {task.Objective}
            Step {step.Order}: {step.Title}
            Step kind: {step.Kind}
            Return a concise result, risks, and next action. Do not claim external work unless actually done.
            {vaultHint}
            """;
            var result = await _llm.SendSystemQueryAsync(prompt, useTools: step.Kind == KokoAgentStepKind.Vault, ct).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(result) ? "(empty result)" : result.Trim();
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

        private static KokoAgentTask Clone(KokoAgentTask task)
        {
            return JsonConvert.DeserializeObject<KokoAgentTask>(JsonConvert.SerializeObject(task)) ?? task;
        }
    }
}
