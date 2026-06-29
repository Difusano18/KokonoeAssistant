using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace KokonoeAssistant.Services
{
    public sealed class KokoWebAgentBridgeService : IDisposable
    {
        private readonly KokoWebBridgeService _bridge;
        private readonly KokoAgentTaskService _tasks;
        private bool _disposed;

        public KokoWebAgentBridgeService(KokoWebBridgeService bridge, KokoAgentTaskService tasks)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
            _bridge.Register("agent.snapshot", HandleSnapshotAsync);
            _bridge.Register("get_agent_tasks", HandleSnapshotAsync);
            _bridge.Register("agent.start", HandleStartAsync);
            _bridge.Register("start_agent_task", HandleStartAsync);
            _bridge.Register("agent.start_many", HandleStartManyAsync);
            _bridge.Register("agent.cancel", HandleCancelAsync);
            _bridge.Register("cancel_agent_task", HandleCancelAsync);
            _bridge.Register("agent.retry", HandleRetryAsync);
            _bridge.Register("agent.runner.status", HandleRunnerStatusAsync);
            _bridge.Register("agent.runner.start", HandleRunnerStartAsync);
            _bridge.Register("agent.runner.stop", HandleRunnerStopAsync);
            _bridge.Register("agent.runner.configure", HandleRunnerConfigureAsync);
            _bridge.Register("agent.clear_completed", HandleClearCompletedAsync);
            _tasks.ActivityChanged += OnActivityChanged;
            _tasks.TaskCompleted += OnTaskCompleted;
        }

        private async Task<object?> HandleSnapshotAsync(JToken? payload, CancellationToken ct)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            return BuildSnapshotPayload();
        }

        private async Task<object?> HandleStartAsync(JToken? payload, CancellationToken ct)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            if (_disposed)
                throw new ObjectDisposedException(nameof(KokoWebAgentBridgeService));
            var objective = payload?["objective"]?.ToString()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(objective))
                throw new InvalidOperationException("Agent objective is empty.");
            if (objective.Length > 16_000)
                throw new InvalidOperationException("Agent objective exceeds 16000 characters.");

            var priority = Math.Clamp(payload?["priority"]?.Value<int?>() ?? 5, 1, 10);
            var start = payload?["start"]?.Value<bool?>() ?? true;
            var autoBatch = payload?["autoBatch"]?.Value<bool?>() ?? false;
            if (autoBatch)
            {
                var tasks = _tasks.AddBatch(objective, priority);
                if (start)
                    _tasks.Start();
                return new
                {
                    taskId = tasks.FirstOrDefault()?.Id ?? "",
                    taskIds = tasks.Select(t => t.Id).ToArray(),
                    count = tasks.Count,
                    started = start,
                    snapshot = BuildSnapshotPayload()
                };
            }

            var task = _tasks.AddTask(objective, priority);
            if (start)
                _tasks.Start();
            return new
            {
                taskId = task.Id,
                started = start,
                snapshot = BuildSnapshotPayload()
            };
        }

        private async Task<object?> HandleStartManyAsync(JToken? payload, CancellationToken ct)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            if (_disposed)
                throw new ObjectDisposedException(nameof(KokoWebAgentBridgeService));

            var objective = payload?["objective"]?.ToString()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(objective))
                throw new InvalidOperationException("Agent objective is empty.");
            if (objective.Length > 32_000)
                throw new InvalidOperationException("Agent objective batch exceeds 32000 characters.");

            var priority = Math.Clamp(payload?["priority"]?.Value<int?>() ?? 5, 1, 10);
            var start = payload?["start"]?.Value<bool?>() ?? true;
            var tasks = _tasks.AddBatch(objective, priority);
            if (start)
                _tasks.Start();

            return new
            {
                taskIds = tasks.Select(t => t.Id).ToArray(),
                count = tasks.Count,
                started = start,
                snapshot = BuildSnapshotPayload()
            };
        }

        private async Task<object?> HandleCancelAsync(JToken? payload, CancellationToken ct)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            if (_disposed)
                throw new ObjectDisposedException(nameof(KokoWebAgentBridgeService));

            var taskId = payload?["taskId"]?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(taskId))
                taskId = payload?["id"]?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(taskId))
                throw new InvalidOperationException("Agent task id is empty.");

            var canceled = _tasks.CancelTask(taskId);
            if (!canceled)
                throw new InvalidOperationException("Agent task was not found: " + taskId);

            var snapshot = BuildSnapshotPayload();
            _bridge.Publish("agent.activity", new
            {
                activity = new
                {
                    updatedAt = DateTime.Now,
                    phase = "observe",
                    tool = "runner",
                    focus = taskId,
                    thought = "Task canceled from WebView.",
                    taskId,
                    stepId = ""
                },
                snapshot
            });
            return new
            {
                taskId,
                canceled,
                snapshot
            };
        }

        private async Task<object?> HandleRetryAsync(JToken? payload, CancellationToken ct)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            if (_disposed)
                throw new ObjectDisposedException(nameof(KokoWebAgentBridgeService));

            var taskId = payload?["taskId"]?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(taskId))
                taskId = payload?["id"]?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(taskId))
                throw new InvalidOperationException("Agent task id is empty.");

            var task = _tasks.RetryTask(taskId);
            if (task == null)
                throw new InvalidOperationException("Agent task was not found: " + taskId);

            var start = payload?["start"]?.Value<bool?>() ?? true;
            if (start)
                _tasks.Start();

            return new
            {
                taskId = task.Id,
                started = start,
                snapshot = BuildSnapshotPayload()
            };
        }

        private async Task<object?> HandleClearCompletedAsync(JToken? payload, CancellationToken ct)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            if (_disposed)
                throw new ObjectDisposedException(nameof(KokoWebAgentBridgeService));
            var removed = _tasks.ClearCompletedTasks(TimeSpan.FromHours(1));
            KokoSystemLog.Write("AGENT", $"Cleared {removed} old tasks");
            var snapshot = BuildSnapshotPayload();
            if (removed > 0)
                _bridge.Publish("agent.activity", new { activity = ProjectActivity(_tasks.GetSnapshot().Activity), snapshot });
            return new { removed, snapshot };
        }

        private async Task<object?> HandleRunnerStatusAsync(JToken? payload, CancellationToken ct)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            return new
            {
                active = _tasks.IsRunnerActive,
                snapshot = BuildSnapshotPayload()
            };
        }

        private async Task<object?> HandleRunnerStartAsync(JToken? payload, CancellationToken ct)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            if (_disposed)
                throw new ObjectDisposedException(nameof(KokoWebAgentBridgeService));
            _tasks.Start();
            return new
            {
                active = _tasks.IsRunnerActive,
                snapshot = BuildSnapshotPayload()
            };
        }

        private async Task<object?> HandleRunnerStopAsync(JToken? payload, CancellationToken ct)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            if (_disposed)
                throw new ObjectDisposedException(nameof(KokoWebAgentBridgeService));
            _tasks.Stop();
            return new
            {
                active = _tasks.IsRunnerActive,
                snapshot = BuildSnapshotPayload()
            };
        }

        private async Task<object?> HandleRunnerConfigureAsync(JToken? payload, CancellationToken ct)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            if (_disposed)
                throw new ObjectDisposedException(nameof(KokoWebAgentBridgeService));

            var requested = payload?["maxParallel"]?.Value<int?>() ?? payload?["value"]?.Value<int?>() ?? _tasks.MaxParallel;
            var configured = _tasks.ConfigureMaxParallel(requested);
            var settings = AppSettings.Load();
            if (settings.AgentMaxParallel != configured)
            {
                settings.AgentMaxParallel = configured;
                settings.Save();
            }
            return new
            {
                maxParallel = configured,
                active = _tasks.IsRunnerActive,
                snapshot = BuildSnapshotPayload()
            };
        }

        private void OnActivityChanged(KokoAgentActivitySnapshot activity)
        {
            if (_disposed)
                return;
            _bridge.Publish("agent.activity", new
            {
                activity = ProjectActivity(activity),
                snapshot = BuildSnapshotPayload()
            });
        }

        private void OnTaskCompleted(KokoAgentTask task, KokoAgentCompletionNotice notice)
        {
            if (_disposed)
                return;
            _bridge.Publish("agent.completed", new
            {
                task = ProjectTask(task),
                notice = new
                {
                    mode = notice.Mode,
                    summary = notice.Summary,
                    nextQuestion = notice.NextQuestion,
                    text = notice.Notice
                },
                snapshot = BuildSnapshotPayload()
            });
        }

        private object BuildSnapshotPayload()
        {
            var snapshot = _tasks.GetSnapshot();
            return new
            {
                takenAt = snapshot.TakenAt,
                maxParallel = snapshot.MaxParallel,
                runningSteps = snapshot.RunningSteps,
                pendingTasks = snapshot.PendingTasks,
                completedTasks = snapshot.CompletedTasks,
                failedTasks = snapshot.FailedTasks,
                canceledTasks = snapshot.CanceledTasks,
                runnerActive = _tasks.IsRunnerActive,
                activity = ProjectActivity(snapshot.Activity),
                activeLanes = snapshot.ActiveLanes.Select(ProjectLane).ToArray(),
                taskCount = snapshot.Tasks.Count,
                tasks = snapshot.Tasks.OrderByDescending(t => t.UpdatedAt).Take(50).Select(ProjectTask).ToArray()
            };
        }

        private static object ProjectActivity(KokoAgentActivitySnapshot activity) => new
        {
            updatedAt = activity.UpdatedAt,
            phase = activity.Phase,
            tool = activity.Tool,
            focus = activity.Focus,
            thought = activity.Thought,
            taskId = activity.TaskId,
            stepId = activity.StepId
        };

        private static object ProjectLane(KokoAgentActiveLane lane) => new
        {
            slot = lane.Slot,
            taskId = lane.TaskId,
            stepId = lane.StepId,
            objective = lane.Objective,
            stepTitle = lane.StepTitle,
            kind = lane.Kind.ToString(),
            startedAt = lane.StartedAt,
            elapsedSeconds = lane.ElapsedSeconds
        };

        private static object ProjectTask(KokoAgentTask task) => new
        {
            id = task.Id,
            objective = task.Objective,
            status = task.Status.ToString(),
            priority = task.Priority,
            createdAt = task.CreatedAt,
            updatedAt = task.UpdatedAt,
            completionNotice = task.CompletionNotice,
            nextQuestion = task.NextQuestion,
            steps = task.Steps.OrderBy(step => step.Order).Select(step => new
            {
                id = step.Id,
                order = step.Order,
                title = step.Title,
                kind = step.Kind.ToString(),
                status = step.Status.ToString(),
                startedAt = step.StartedAt,
                finishedAt = step.FinishedAt,
                result = step.Result,
                error = step.Error
            }).ToArray()
        };

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _tasks.ActivityChanged -= OnActivityChanged;
            _tasks.TaskCompleted -= OnTaskCompleted;
        }
    }
}
