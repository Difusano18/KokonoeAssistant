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
            _tasks.ActivityChanged += OnActivityChanged;
            _tasks.TaskCompleted += OnTaskCompleted;
        }

        private Task<object?> HandleSnapshotAsync(JToken? payload, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<object?>(BuildSnapshotPayload());
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
                activity = ProjectActivity(snapshot.Activity),
                tasks = snapshot.Tasks.Select(ProjectTask).ToArray()
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

        private static object ProjectTask(KokoAgentTask task) => new
        {
            id = task.Id,
            objective = task.Objective,
            status = task.Status.ToString(),
            priority = task.Priority,
            createdAt = task.CreatedAt,
            updatedAt = task.UpdatedAt,
            steps = task.Steps.OrderBy(step => step.Order).Select(step => new
            {
                id = step.Id,
                order = step.Order,
                title = step.Title,
                kind = step.Kind.ToString(),
                status = step.Status.ToString(),
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
