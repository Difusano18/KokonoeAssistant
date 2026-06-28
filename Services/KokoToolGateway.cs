using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KokonoeAssistant.Services
{
    public sealed class KokoToolCall
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
        public string Name { get; set; } = "";
        public Dictionary<string, string> Arguments { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public object? Payload { get; set; }
        public bool Confirmed { get; set; }
    }

    public sealed class KokoToolResult
    {
        public string CallId { get; set; } = "";
        public string ToolName { get; set; } = "";
        public bool Success { get; set; }
        public bool Verified { get; set; }
        public bool RequiresConfirmation { get; set; }
        public string PendingActionId { get; set; } = "";
        public string Reason { get; set; } = "";
        public string Output { get; set; } = "";
        public long DurationMs { get; set; }
        public object? RawResult { get; set; }

        public string ToLlmText()
        {
            var state = RequiresConfirmation ? "confirmation_required" : Success ? "success" : "failure";
            var verified = Verified ? "verified" : "unverified";
            var pending = string.IsNullOrWhiteSpace(PendingActionId) ? "" : $" pending_action={PendingActionId}";
            return $"tool_result {ToolName} {state} {verified}{pending}: {Reason}\n{Output}".Trim();
        }
    }

    public interface IKokoToolHandler
    {
        string Name { get; }
        Task<KokoToolResult> ExecuteAsync(KokoToolCall call, CancellationToken ct);
    }

    public interface IKokoToolGateway
    {
        IReadOnlyCollection<string> ToolNames { get; }
        IReadOnlyCollection<KokoToolDescriptor> Tools { get; }
        IReadOnlyCollection<KokoToolDescriptor> GetToolsForCapabilities(IEnumerable<string>? capabilities);
        string BuildToolPromptBlock(IEnumerable<string>? capabilities = null);
        void Register(IKokoToolHandler handler);
        Task<KokoToolResult> ExecuteAsync(KokoToolCall call, CancellationToken ct = default);
        Task<IReadOnlyList<KokoToolResult>> ExecutePlanAsync(IEnumerable<KokoToolCall> calls, CancellationToken ct = default);
    }

    public sealed class KokoToolGateway : IKokoToolGateway
    {
        private readonly KokoToolRegistry _registry = new();

        public KokoToolGateway(IKokoFileSystemToolService fileSystem, PcActionExecutor pcExecutor)
        {
            foreach (var name in fileSystem.GetToolNames())
                Register(new KokoFileToolHandler(name, fileSystem));
            Register(new KokoPcActionToolHandler("pc_action", pcExecutor));
            Register(new KokoPcActionToolHandler("pc_confirm", pcExecutor));
            Register(new KokoPcActionToolHandler("pc_cancel", pcExecutor));
        }

        public IReadOnlyCollection<string> ToolNames
            => _registry.Tools.Select(tool => tool.Name).ToArray();

        public IReadOnlyCollection<KokoToolDescriptor> Tools => _registry.Tools;

        public IReadOnlyCollection<KokoToolDescriptor> GetToolsForCapabilities(IEnumerable<string>? capabilities)
            => _registry.SelectForCapabilities(capabilities);

        public string BuildToolPromptBlock(IEnumerable<string>? capabilities = null)
            => _registry.BuildPromptBlock(capabilities);

        public void Register(IKokoToolHandler handler)
            => _registry.Register(handler);

        public async Task<KokoToolResult> ExecuteAsync(KokoToolCall call, CancellationToken ct = default)
        {
            if (call == null) throw new ArgumentNullException(nameof(call));
            if (string.IsNullOrWhiteSpace(call.Name))
                return Failure(call, "tool name is empty");

            if (!_registry.TryResolve(call.Name, out var handler) || handler == null)
            {
                KokoSystemLog.Write("TOOL-GATEWAY", $"no handler for '{call.Name}' — returning error, not hanging");
                return Failure(call, $"Інструмент '{call.Name}' недоступний. Спробуй інший підхід або відповідай напряму, без цього tool.");
            }

            var watch = Stopwatch.StartNew();
            KokoSystemLog.Write("TOOL-GATEWAY", $"start id={call.Id} tool={call.Name} confirmed={call.Confirmed}");
            var label = HumanLabel(call);
            KokoActivityBus.Emit(new KokoActivity { Kind = "tool", Label = label, Detail = call.Name, Status = "running" });
            try
            {
                var result = await handler.ExecuteAsync(call, ct).ConfigureAwait(false);
                result.CallId = call.Id;
                result.ToolName = call.Name;
                result.DurationMs = watch.ElapsedMilliseconds;
                KokoSystemLog.Write("TOOL-GATEWAY", $"finish id={call.Id} tool={call.Name} success={result.Success} verified={result.Verified} confirmation={result.RequiresConfirmation} ms={result.DurationMs} reason={result.Reason}");
                KokoActivityBus.Emit(new KokoActivity { Kind = "tool", Label = label, Detail = call.Name, Status = result.Success ? "done" : "failed" });
                return result;
            }
            catch (OperationCanceledException)
            {
                KokoSystemLog.Write("TOOL-GATEWAY", $"cancel id={call.Id} tool={call.Name}");
                KokoActivityBus.Emit(new KokoActivity { Kind = "tool", Label = label, Detail = call.Name, Status = "failed" });
                throw;
            }
            catch (Exception ex)
            {
                KokoSystemLog.Write("TOOL-GATEWAY", $"failure id={call.Id} tool={call.Name}: {ex}");
                KokoActivityBus.Emit(new KokoActivity { Kind = "tool", Label = label, Detail = call.Name, Status = "failed" });
                var result = Failure(call, ex.Message);
                result.DurationMs = watch.ElapsedMilliseconds;
                return result;
            }
        }

        // Real tool name strings, confirmed against each handler's own Name property -
        // browser.* and artifact.save use dots, fs_*/pc_*/web_search/delegate_to_agent use
        // underscores. Mixed convention already in this codebase, not a typo here.
        // Includes the actual argument (path/query/selector/...) instead of just the tool
        // name, so the activity feed can show "Читаю файл: C:\...\x.jpg" instead of a generic
        // "Працюю з файлами" - the whole point of surfacing this in the UI is knowing exactly
        // which file/directory/url/query she's touching right now, Manus-style.
        private static string HumanLabel(KokoToolCall call) => call.Name switch
        {
            "browser.navigate"    => $"Відкриваю сторінку: {Arg(call, "url")}",
            "browser.extract"     => "Читаю сторінку",
            "browser.screenshot"  => "Дивлюся на екран браузера",
            "browser.click"       => $"Клікаю: {Arg(call, "selector")}",
            "browser.type"        => $"Вводжу текст: {Arg(call, "selector")}",
            "browser.scroll"      => "Гортаю сторінку",
            "browser.wait_for"    => $"Чекаю: {Arg(call, "selector")}",
            "browser.close"       => "Закриваю браузер",
            "web_search"          => $"Шукаю в інтернеті: {Arg(call, "query")}",
            "delegate_to_agent"   => $"Делегую агенту: {Arg(call, "agentId")}",
            "codeact_python"      => "Виконую код",
            "artifact.save"       => "Зберігаю результат",
            "pc_confirm"          => $"Підтверджую дію: {Arg(call, "actionId")}",
            "pc_cancel"           => $"Скасовую дію: {Arg(call, "actionId")}",
            "pc_action"           => $"PC дія: {Arg(call, "actionType")} {Arg(call, "target")}".TrimEnd(),
            "fs_read_text"        => $"Читаю файл: {Arg(call, "path")}",
            "fs_write_text"       => $"Записую файл: {Arg(call, "path")}",
            "fs_create_directory" => $"Створюю папку: {Arg(call, "path")}",
            "fs_move"             => $"Переміщую: {Arg(call, "path")} -> {Arg(call, "destinationPath")}",
            "fs_delete"           => $"Видаляю: {Arg(call, "path")}",
            "fs_list_directory"   => $"Дивлюся в папку: {Arg(call, "path")}",
            var t when t.StartsWith("fs_", StringComparison.Ordinal) => "Працюю з файлами",
            _ => $"Виконую {call.Name}"
        };

        private static string Arg(KokoToolCall call, string key)
            => call.Arguments.TryGetValue(key, out var value) ? value ?? "" : "";

        public async Task<IReadOnlyList<KokoToolResult>> ExecutePlanAsync(IEnumerable<KokoToolCall> calls, CancellationToken ct = default)
        {
            var plan = (calls ?? Array.Empty<KokoToolCall>()).ToList();
            KokoSystemLog.Write("TOOL-GATEWAY", "plan: " + string.Join(" -> ", plan.Select(c => $"{c.Name}#{c.Id}")));
            var results = new List<KokoToolResult>();
            foreach (var call in plan)
            {
                ct.ThrowIfCancellationRequested();
                var result = await ExecuteAsync(call, ct).ConfigureAwait(false);
                results.Add(result);
                if (!result.Success || result.RequiresConfirmation)
                    break;
            }
            return results;
        }

        private static KokoToolResult Failure(KokoToolCall call, string reason) => new()
        {
            CallId = call.Id,
            ToolName = call.Name,
            Success = false,
            Verified = false,
            Reason = reason
        };
    }

    internal sealed class KokoFileToolHandler : IKokoToolHandler
    {
        private readonly IKokoFileSystemToolService _fileSystem;
        public string Name { get; }

        public KokoFileToolHandler(string name, IKokoFileSystemToolService fileSystem)
        {
            Name = name;
            _fileSystem = fileSystem;
        }

        public async Task<KokoToolResult> ExecuteAsync(KokoToolCall call, CancellationToken ct)
        {
            var kind = Name switch
            {
                "fs_read_text" => KokoFileOperationKind.ReadText,
                "fs_write_text" => KokoFileOperationKind.WriteText,
                "fs_create_directory" => KokoFileOperationKind.CreateDirectory,
                "fs_delete" => KokoFileOperationKind.Delete,
                "fs_move" => KokoFileOperationKind.Move,
                "fs_list_directory" => KokoFileOperationKind.ListDirectory,
                _ => throw new InvalidOperationException("Unsupported file handler: " + Name)
            };
            var request = new KokoFileOperationRequest
            {
                Kind = kind,
                Path = Arg(call, "path"),
                DestinationPath = Arg(call, "destinationPath"),
                Content = Arg(call, "content"),
                Confirmed = call.Confirmed
            };
            var operation = await _fileSystem.ExecuteAsync(request, ct).ConfigureAwait(false);
            if (operation.RequiresConfirmation)
            {
                return new KokoToolResult
                {
                    RequiresConfirmation = true,
                    Reason = operation.Message,
                    Output = operation.Output,
                    RawResult = operation
                };
            }
            if (!operation.Success)
            {
                return new KokoToolResult { Reason = operation.Message, Output = operation.Output, RawResult = operation };
            }

            var verification = await VerifyAsync(request, ct).ConfigureAwait(false);
            return new KokoToolResult
            {
                Success = verification.ok,
                Verified = verification.ok,
                Reason = verification.ok ? operation.Message : "provider claimed success but verification failed: " + verification.reason,
                Output = operation.Output,
                RawResult = operation
            };
        }

        private async Task<(bool ok, string reason)> VerifyAsync(KokoFileOperationRequest request, CancellationToken ct)
        {
            var path = _fileSystem.ResolvePath(request.Path);
            switch (request.Kind)
            {
                case KokoFileOperationKind.ReadText:
                    return (File.Exists(path), File.Exists(path) ? "read source exists" : "read source disappeared");
                case KokoFileOperationKind.WriteText:
                    if (!File.Exists(path)) return (false, "written file does not exist");
                    var actual = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
                    return (string.Equals(actual, request.Content ?? "", StringComparison.Ordinal), "written content mismatch");
                case KokoFileOperationKind.CreateDirectory:
                    return (Directory.Exists(path), "created directory does not exist");
                case KokoFileOperationKind.Delete:
                    return (!File.Exists(path) && !Directory.Exists(path), "deleted path still exists");
                case KokoFileOperationKind.Move:
                    var destination = _fileSystem.ResolvePath(request.DestinationPath);
                    return ((!File.Exists(path) && !Directory.Exists(path)) && (File.Exists(destination) || Directory.Exists(destination)), "move source/destination state mismatch");
                case KokoFileOperationKind.ListDirectory:
                    return (Directory.Exists(path), "listed directory does not exist");
                default:
                    return (false, "unsupported verification");
            }
        }

        private static string Arg(KokoToolCall call, string key)
            => call.Arguments.TryGetValue(key, out var value) ? value ?? "" : "";
    }

    internal sealed class KokoPcActionToolHandler : IKokoToolHandler
    {
        private readonly PcActionExecutor _executor;
        public string Name { get; }

        public KokoPcActionToolHandler(string name, PcActionExecutor executor)
        {
            Name = name;
            _executor = executor;
        }

        public async Task<KokoToolResult> ExecuteAsync(KokoToolCall call, CancellationToken ct)
        {
            PcActionExecutionResult result;
            if (Name == "pc_action")
            {
                if (call.Payload is not PcActionPlan plan)
                    return new KokoToolResult { Reason = "pc_action requires PcActionPlan payload" };
                result = await _executor.ExecuteAsync(plan, ct: ct).ConfigureAwait(false);
            }
            else if (Name == "pc_confirm")
            {
                result = await _executor.ConfirmAndExecuteAsync(Arg(call, "actionId"), Arg(call, "confirmationText"), ct: ct).ConfigureAwait(false);
            }
            else
            {
                result = await _executor.CancelPendingActionAsync(Arg(call, "actionId"), Arg(call, "reason")).ConfigureAwait(false);
            }

            return new KokoToolResult
            {
                Success = result.Succeeded,
                Verified = result.Succeeded && !result.RequiresConfirmation && !result.Blocked,
                RequiresConfirmation = result.RequiresConfirmation,
                PendingActionId = string.IsNullOrWhiteSpace(result.PendingActionId) ? result.ActionId : result.PendingActionId,
                Reason = result.Message,
                Output = result.Message,
                RawResult = result
            };
        }

        private static string Arg(KokoToolCall call, string key)
            => call.Arguments.TryGetValue(key, out var value) ? value ?? "" : "";
    }
}
