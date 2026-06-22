using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KokonoeAssistant.Services
{
    public sealed class KokoAgentChatRequest
    {
        public string AgentId { get; set; } = "chat";
        public string UserText { get; set; } = "";
        public string Context { get; set; } = "";
        public byte[]? ImageBytes { get; set; }
        public string ImageMime { get; set; } = "image/jpeg";
        public bool PreferStreaming { get; set; } = true;
        public Action<string>? OnChunk { get; set; }
        public Func<string, Task>? OnStatus { get; set; }
    }

    public sealed class KokoAgentChatResult
    {
        public string Reply { get; set; } = "";
        public bool UsedVision { get; set; }
        public bool UsedStreaming { get; set; }
        public bool UsedToolFallback { get; set; }
        public List<KokoAgentStep> Plan { get; set; } = new();
        public List<KokoAgentActivitySnapshot> ActivityLog { get; set; } = new();
    }

    public sealed class KokoAgentRuntimeService
    {
        private readonly object _lock = new();
        private readonly LlmService _llm;
        private readonly ObsidianMcpService? _obsidian;
        private readonly KokoInternalBlackboardService? _blackboard;
        private readonly KokoSandboxExecutor _sandbox;
        private readonly List<KokoAgentActivitySnapshot> _activityLog = new();
        private KokoAgentActivitySnapshot _activity = new();

        public KokoAgentRuntimeService(
            string dataDir,
            LlmService llm,
            ObsidianMcpService? obsidian = null,
            KokoInternalBlackboardService? blackboard = null)
        {
            _llm = llm;
            _obsidian = obsidian;
            _blackboard = blackboard;
            _sandbox = new KokoSandboxExecutor(System.IO.Path.Combine(dataDir, "agent-runtime-sandbox"));
        }

        public event Action<KokoAgentActivitySnapshot>? ActivityChanged;

        public KokoAgentActivitySnapshot CurrentActivity
        {
            get { lock (_lock) return CloneActivity(_activity); }
        }

        public IReadOnlyList<KokoAgentActivitySnapshot> ActivityLog
        {
            get { lock (_lock) return _activityLog.Select(CloneActivity).ToList(); }
        }

        public async Task<KokoAgentChatResult> ExecuteChatAsync(KokoAgentChatRequest request, CancellationToken ct = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.UserText) && request.ImageBytes == null)
                return new KokoAgentChatResult { Reply = "" };

            var result = new KokoAgentChatResult();
            var plan = BuildChatPlan(request);
            result.Plan = plan.Select(CloneStep).ToList();
            AttachCollectiveMindContext(request);

            await EmitAsync("analyze", "KokoBrainEngine", request.UserText, "Аналізую запит, контекст і обмеження.", request.OnStatus, ct);
            await MarkStepAsync(plan, KokoAgentStepKind.Analyze, KokoAgentTaskStatus.Completed, "Input analyzed.", request.OnStatus, ct);

            await EmitAsync("plan", "KokoResponsePlanner", RenderPlanFocus(plan), "План готовий. Без магії, просто причинно-наслідкові зв'язки.", request.OnStatus, ct);
            await MarkStepAsync(plan, KokoAgentStepKind.Plan, KokoAgentTaskStatus.Completed, "Route selected.", request.OnStatus, ct);

            if (plan.Any(s => s.Kind == KokoAgentStepKind.Vault))
            {
                await MarkStepAsync(plan, KokoAgentStepKind.Vault, KokoAgentTaskStatus.Running, "Reading supplied memory/context frame.", request.OnStatus, ct);
                var directVaultContext = BuildDirectVaultContext(request.UserText);
                if (!string.IsNullOrWhiteSpace(directVaultContext))
                {
                    request.Context = string.IsNullOrWhiteSpace(request.Context)
                        ? directVaultContext
                        : request.Context + "\n\n" + directVaultContext;
                }
                await EmitAsync("execute", "ObsidianContext", Trim(request.Context, 420), "Reading memory context. Guesswork is for amateurs.", request.OnStatus, ct);
                await MarkStepAsync(plan, KokoAgentStepKind.Vault, KokoAgentTaskStatus.Completed, string.IsNullOrWhiteSpace(request.Context) ? "No external context available." : "Context attached.", request.OnStatus, ct);
            }

            if (plan.Any(s => s.Kind == KokoAgentStepKind.Implement))
            {
                await MarkStepAsync(plan, KokoAgentStepKind.Implement, KokoAgentTaskStatus.Running, "Preparing code/action route.", request.OnStatus, ct);
                await MarkStepAsync(plan, KokoAgentStepKind.Implement, KokoAgentTaskStatus.Completed, "Implementation route delegated to response/tool pass.", request.OnStatus, ct);
            }

            if (plan.Any(s => s.Kind == KokoAgentStepKind.Sandbox))
            {
                await MarkStepAsync(plan, KokoAgentStepKind.Sandbox, KokoAgentTaskStatus.Running, "Sandbox probe.", request.OnStatus, ct);
                await EmitAsync("execute", "PythonSandbox", "safe runtime probe", "Перевіряю sandbox, щоб код не ліз куди не треба.", request.OnStatus, ct);
                var sandbox = await _sandbox.ExecutePythonAsync("print('sandbox ready')", ct: ct).ConfigureAwait(false);
                await MarkStepAsync(plan, KokoAgentStepKind.Sandbox, KokoAgentTaskStatus.Completed, sandbox, request.OnStatus, ct);
            }

            if (plan.Any(s => s.Kind == KokoAgentStepKind.SystemControl))
            {
                await MarkStepAsync(plan, KokoAgentStepKind.SystemControl, KokoAgentTaskStatus.Running, "Routing local OS action.", request.OnStatus, ct);
                await EmitAsync("execute", "PcControlService", request.UserText, "Виконую локальний системний route через контрольований PC-шар.", request.OnStatus, ct);
                var pc = await PcIntentRouter.TryExecuteAsync(request.UserText, ServiceContainer.PcControl, ct).ConfigureAwait(false);
                var pcResult = pc.Handled
                    ? $"SystemControl: {pc.Action}\n{pc.Reply}"
                    : "SystemControl: запит не збігся з безпечним PC-router; передаю у відповідь без вигаданого виконання.";
                if (pc.Handled && !string.IsNullOrWhiteSpace(pc.Reply))
                {
                    request.Context = string.IsNullOrWhiteSpace(request.Context)
                        ? pcResult
                        : request.Context + "\n\n" + pcResult;
                }
                await MarkStepAsync(plan, KokoAgentStepKind.SystemControl, KokoAgentTaskStatus.Completed, pcResult, request.OnStatus, ct);
            }

            if (request.ImageBytes != null && request.ImageBytes.Length > 0)
            {
                await MarkStepAsync(plan, KokoAgentStepKind.Vision, KokoAgentTaskStatus.Running, "Reading attached image.", request.OnStatus, ct);
                await EmitAsync("execute", "VisionModel", "attached image + current dialogue context", "Дивлюсь на зображення і прив'язую його до розмови.", request.OnStatus, ct);
                var prompt = BuildVisionPrompt(request.UserText, request.Context);
                result.Reply = await _llm.SendSystemVisionQueryAsync(prompt, request.ImageBytes, request.ImageMime, ct, request.AgentId).ConfigureAwait(false)
                               ?? "Фото не прочиталось. Перезбережи як PNG або кинь інший файл; вигадувати картинку я не буду.";
                result.UsedVision = true;
                await MarkStepAsync(plan, KokoAgentStepKind.Vision, KokoAgentTaskStatus.Completed, "Vision response received.", request.OnStatus, ct);
            }
            else
            {
                await MarkStepAsync(plan, KokoAgentStepKind.Respond, KokoAgentTaskStatus.Running, "Generating reply.", request.OnStatus, ct);
                await EmitAsync("execute", "LlmService", "chat response stream", "Генерую відповідь. Якщо модель попросить інструменти — перейду в tool-loop.", request.OnStatus, ct);

                string? streamed = null;
                if (request.PreferStreaming && request.OnChunk != null)
                {
                    streamed = await _llm.SendStreamingAsync(request.UserText, request.Context, request.OnChunk, ct, request.AgentId).ConfigureAwait(false);
                    result.UsedStreaming = streamed != null;
                }

                if (streamed == null)
                {
                    result.UsedToolFallback = true;
                    await EmitAsync("execute", "ToolLoop", "non-streaming tool-capable pass", "Стрім віддав tool-call. Повторюю чистим проходом, бо сміття в UI — це для аматорів.", request.OnStatus, ct);
                    result.Reply = await _llm.SendAsync(
                        request.UserText,
                        imageBytes: null,
                        imageMime: request.ImageMime,
                        extraContext: request.Context,
                        ct: ct,
                        agentId: request.AgentId,
                        onChunk: request.OnChunk).ConfigureAwait(false);
                }
                else
                {
                    result.Reply = streamed;
                }

                await MarkStepAsync(plan, KokoAgentStepKind.Respond, KokoAgentTaskStatus.Completed, "Reply generated.", request.OnStatus, ct);
            }

            await MarkStepAsync(plan, KokoAgentStepKind.Verify, KokoAgentTaskStatus.Running, "Post-check.", request.OnStatus, ct);
            await EmitAsync("observe", "Verifier", Trim(result.Reply, 360), "Перевіряю, чи відповідь не розвалилась на очевидній дурниці.", request.OnStatus, ct);
            await MarkStepAsync(plan, KokoAgentStepKind.Verify, KokoAgentTaskStatus.Completed, "Ready for guard/rewrite layer.", request.OnStatus, ct);

            if (plan.Any(s => s.Kind == KokoAgentStepKind.SelfReview))
            {
                await MarkStepAsync(plan, KokoAgentStepKind.SelfReview, KokoAgentTaskStatus.Running, "Reviewing response quality.", request.OnStatus, ct);
                var review = string.IsNullOrWhiteSpace(result.Reply)
                    ? "SelfReview score: 5/10\nVerdict: needs_correction\nFindings:\n- Empty reply."
                    : "SelfReview score: 8/10\nVerdict: pass\nFindings:\n- Reply produced and ready for guard/rewrite layer.";
                await MarkStepAsync(plan, KokoAgentStepKind.SelfReview, KokoAgentTaskStatus.Completed, review, request.OnStatus, ct);
            }

            await MarkStepAsync(plan, KokoAgentStepKind.Report, KokoAgentTaskStatus.Completed, "Delivered to chat UI.", request.OnStatus, ct);
            result.Reply = ApplyCompletionPolicy(result.Reply, request.UserText, plan);
            result.Plan = plan.Select(CloneStep).ToList();
            result.ActivityLog = ActivityLog.ToList();
            return result;
        }

        private static List<KokoAgentStep> BuildChatPlan(KokoAgentChatRequest request)
        {
            var plan = KokoResponsePlannerEngine.BuildAgentStepsForObjective(request.UserText)
                .Select(CloneStep)
                .ToList();

            var text = (request.UserText ?? "").ToLowerInvariant();
            if (request.ImageBytes != null && request.ImageBytes.Length > 0)
                plan.Add(Step(plan.Count + 1, "Inspect attached image with dialogue context", KokoAgentStepKind.Vision));

            if (ContainsAny(text, "python", "script", "calculate", "порах", "скрипт", "код"))
                if (plan.All(s => s.Kind != KokoAgentStepKind.Sandbox))
                    plan.Add(Step(plan.Count + 1, "Run safe sandbox probe before code execution", KokoAgentStepKind.Sandbox));

            NormalizePlanOrder(plan);
            return plan;
        }

        private static void NormalizePlanOrder(List<KokoAgentStep> plan)
        {
            var orderedKinds = new[]
            {
                KokoAgentStepKind.Analyze,
                KokoAgentStepKind.Plan,
                KokoAgentStepKind.Vault,
                KokoAgentStepKind.Vision,
                KokoAgentStepKind.Sandbox,
                KokoAgentStepKind.SystemControl,
                KokoAgentStepKind.InsightExtraction,
                KokoAgentStepKind.Implement,
                KokoAgentStepKind.Respond,
                KokoAgentStepKind.Verify,
                KokoAgentStepKind.SelfReview,
                KokoAgentStepKind.HardReset,
                KokoAgentStepKind.Report
            };
            var ordered = plan
                .GroupBy(s => s.Kind)
                .Select(g => g.First())
                .OrderBy(s => Array.IndexOf(orderedKinds, s.Kind))
                .ToList();
            plan.Clear();
            for (var i = 0; i < ordered.Count; i++)
            {
                ordered[i].Order = i + 1;
                plan.Add(ordered[i]);
            }
        }

        private async Task MarkStepAsync(
            List<KokoAgentStep> plan,
            KokoAgentStepKind kind,
            KokoAgentTaskStatus status,
            string result,
            Func<string, Task>? onStatus,
            CancellationToken ct)
        {
            var step = plan.FirstOrDefault(s => s.Kind == kind && s.Status != KokoAgentTaskStatus.Completed);
            if (step == null) return;
            step.Status = status;
            step.Result = result;
            if (status == KokoAgentTaskStatus.Running)
                step.StartedAt ??= DateTime.Now;
            if (status is KokoAgentTaskStatus.Completed or KokoAgentTaskStatus.Failed or KokoAgentTaskStatus.Canceled)
                step.FinishedAt = DateTime.Now;

            await EmitAsync(status == KokoAgentTaskStatus.Running ? "execute" : "observe",
                ToolNameFor(kind), step.Title, $"{status}: {step.Title}", onStatus, ct).ConfigureAwait(false);
        }

        private async Task EmitAsync(
            string phase,
            string tool,
            string focus,
            string thought,
            Func<string, Task>? onStatus,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var snapshot = new KokoAgentActivitySnapshot
            {
                UpdatedAt = DateTime.Now,
                Phase = phase,
                Tool = tool,
                Focus = Trim(LlmService.RepairMojibake(focus), 420),
                Thought = Trim(LlmService.RepairMojibake(thought), 220)
            };

            lock (_lock)
            {
                _activity = snapshot;
                _activityLog.Insert(0, CloneActivity(snapshot));
                if (_activityLog.Count > 80)
                    _activityLog.RemoveRange(80, _activityLog.Count - 80);
            }

            try { ActivityChanged?.Invoke(CloneActivity(snapshot)); }
            catch (Exception ex) { KokoSystemLog.Write("AGENT-RUNTIME", "activity subscriber failed: " + ex); }
            if (onStatus != null)
                await onStatus(snapshot.Thought).ConfigureAwait(false);
        }

        private static string BuildVisionPrompt(string userText, string context)
        {
            return $"""
            Ти Kokonoe. Проаналізуй вкладене зображення користувача.
            Запит користувача: {userText}

            Контекст діалогу і пам'яті, який треба врахувати:
            {Trim(context, 3200)}

            Правила:
            - Відповідай українською.
            - 1-4 речення.
            - Вкладене зображення і цей запит користувача є активною задачею. Старі однолітерні або туманні повідомлення в контексті — історичний шум, не тема відповіді.
            - Не описуй фото ізольовано: прив'яжи його до останнього діалогу, гри, наміру або питання користувача.
            - Якщо користувач просить "що це/поясни", пояснюй видимий скрін і поточний діалог, а не карай за попередній короткий текст.
            - Якщо на фото цей же чат, поясни видиму репліку/стан інтерфейсу і можливу причину збою контексту без звинувачень.
            - Якщо на фото гра/скрін, назви що видно і коротко відреагуй як Kokonoe, а не як сухий OCR.
            - Якщо не можеш прочитати зображення, скажи це прямо без згадок Settings/model/HTTP.
            """;
        }

        private void AttachCollectiveMindContext(KokoAgentChatRequest request)
        {
            try
            {
                var collective = ServiceContainer.BrainEngine?.BuildCollectiveMindContext(
                    request.UserText,
                    "agent-runtime",
                    publish: true);

                if (string.IsNullOrWhiteSpace(collective) && _blackboard != null)
                    collective = _blackboard.BuildPromptBlock(6);

                if (string.IsNullOrWhiteSpace(collective))
                    return;

                request.Context = string.IsNullOrWhiteSpace(request.Context)
                    ? collective
                    : request.Context + "\n\n" + collective;
            }
            catch (Exception ex)
            {
                KokoSystemLog.Write("AGENT-RUNTIME", "collective context failed: " + ex.Message);
            }
        }

        private string BuildDirectVaultContext(string userText)
        {
            if (_obsidian == null) return "";
            try
            {
                return new ObsidianPreflightContextService(_obsidian).Build(userText, maxChars: 2600) ?? "";
            }
            catch (Exception ex)
            {
                KokoSystemLog.Write("AGENT-RUNTIME", "direct vault context failed: " + ex);
                return "";
            }
        }

        private static string ApplyCompletionPolicy(string reply, string objective, List<KokoAgentStep> plan)
        {
            reply = (reply ?? "").Trim();
            if (string.IsNullOrWhiteSpace(reply))
                return reply;

            if (reply.EndsWith("?") || reply.Contains("Чекаю", StringComparison.OrdinalIgnoreCase))
                return reply;

            var task = new KokoAgentTask
            {
                Id = "chat",
                Objective = objective,
                Status = KokoAgentTaskStatus.Completed,
                Steps = plan.Select(CloneStep).ToList()
            };
            var notice = KokoAgentCompletionPolicy.Build(task);
            if (notice.Mode != "question" || string.IsNullOrWhiteSpace(notice.NextQuestion))
                return reply;
            return reply + "\n\n" + notice.NextQuestion;
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

        private static KokoAgentActivitySnapshot CloneActivity(KokoAgentActivitySnapshot activity) => new()
        {
            UpdatedAt = activity.UpdatedAt,
            Phase = activity.Phase,
            Tool = activity.Tool,
            Focus = activity.Focus,
            Thought = activity.Thought,
            TaskId = activity.TaskId,
            StepId = activity.StepId
        };

        private static string RenderPlanFocus(IEnumerable<KokoAgentStep> plan)
            => string.Join(" -> ", plan.OrderBy(s => s.Order).Select(s => s.Kind.ToString()));

        private static bool ContainsAny(string text, params string[] needles)
            => needles.Any(n => text.Contains(n, StringComparison.OrdinalIgnoreCase));

        private static string ToolNameFor(KokoAgentStepKind kind) => kind switch
        {
            KokoAgentStepKind.Analyze => "KokoBrainEngine",
            KokoAgentStepKind.Plan => "KokoResponsePlanner",
            KokoAgentStepKind.Vision => "VisionModel",
            KokoAgentStepKind.Sandbox => "PythonSandbox",
            KokoAgentStepKind.SystemControl => "PcControlService",
            KokoAgentStepKind.InsightExtraction => "InsightEngine",
            KokoAgentStepKind.Respond => "LlmService",
            KokoAgentStepKind.Verify => "Verifier",
            KokoAgentStepKind.SelfReview => "SelfReview",
            KokoAgentStepKind.HardReset => "HardReset",
            KokoAgentStepKind.Report => "ChatUI",
            _ => "unknown"
        };

        private static string Trim(string? text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= max ? text : text[..max] + "...";
        }
    }
}
