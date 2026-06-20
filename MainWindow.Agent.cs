using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KokonoeAssistant.Services;

namespace KokonoeAssistant
{
    public partial class MainWindow
    {
        private async Task<PcIntentExecutionResult> TryHandleDirectControlCommandAsync(
            string userText,
            CancellationToken ct)
        {
            try
            {
                if (TryScheduleWakeOrReminder(userText, out var reply))
                    return new PcIntentExecutionResult { Handled = true, Reply = reply };

                var pc = await PcIntentRouter.TryExecuteAsync(
                    userText,
                    ServiceContainer.PcControl,
                    ct,
                    gateway: ServiceContainer.ToolGateway);
                if (pc.Handled)
                    return pc;

                var brain = ServiceContainer.BrainEngine;
                if (brain != null && brain.TryApplyUserControlCommand(userText, out reply))
                    return new PcIntentExecutionResult { Handled = true, Reply = reply };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                KokoSystemLog.Write("PC-CONTROL", "Direct control routing failed: " + ex);
            }

            return new PcIntentExecutionResult { Handled = false };
        }

        private async Task<(bool Handled, string Reply)> TryHandleSystemOverlordDirectiveAsync(
            string userText,
            CancellationToken ct,
            bool allowAgentTask = false)
        {
            var directive = KokoActionDirectiveRouter.Analyze(userText);
            if (directive.Route == KokoActionDirectiveRoute.None)
                return (false, "");

            try
            {
                if (directive.Route == KokoActionDirectiveRoute.LocalArtifact)
                {
                    await ShowKokoActivityAsync("Overlord: scanning local files and creating an artifact");
                    var result = await ServiceContainer.SystemOverlord.CreateSurpriseNoteAsync(userText, ct);
                    return (true, result.ToUserReply());
                }

                if (directive.Route == KokoActionDirectiveRoute.GeneratedContentArtifact)
                {
                    await ShowKokoActivityAsync("Generating requested content and writing a verified artifact");
                    return (true, await CreateGeneratedContentArtifactAsync(userText, ct));
                }

                if (allowAgentTask && directive.Route == KokoActionDirectiveRoute.AgentTask)
                {
                    HookAgentTaskEvents();
                    var task = ServiceContainer.AgentTasks.AddTask(
                        userText,
                        priority: Math.Clamp(directive.Confidence / 10, 5, 9));
                    ServiceContainer.AgentTasks.Start();
                    RefreshAgentTaskBoard();
                    KokoSystemLog.Write(
                        "ACTION-DIRECTIVE",
                        $"agent task {task.Id}: {directive.Reason}; confidence={directive.Confidence}; objective={userText}");
                    return (true, $"Agent task `{task.Id}` started. Route: {directive.Reason}.");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                KokoSystemLog.Write("ACTION-DIRECTIVE", "UI directive failed: " + ex);
                return (true, "Action failed: " + ex.Message);
            }

            return (false, "");
        }

        private static async Task<string> CreateGeneratedContentArtifactAsync(string userText, CancellationToken ct)
        {
            var prompt = $"""
            Generate the actual body of the file requested below.
            Request: {userText}

            Return only finished file content. Do not discuss the task, promise future work, invent a path,
            or wrap the content in markdown fences. Match the user's language and requested format.
            """;
            var content = (await ServiceContainer.LlmService
                .SendSystemQueryAsync(prompt, useTools: false, ct: ct, agentId: "writer")
                .ConfigureAwait(false))?.Trim();
            if (string.IsNullOrWhiteSpace(content))
                return "Content generation failed: the model returned an empty result. No file was created.";

            var extension = userText.Contains(".txt", StringComparison.OrdinalIgnoreCase) ||
                            userText.Contains(" txt", StringComparison.OrdinalIgnoreCase)
                ? ".txt"
                : ".md";
            var relativePath = Path.Combine(
                "generated-content",
                $"Kokonoe_Content_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..8]}{extension}");
            var result = await ServiceContainer.ToolGateway.ExecuteAsync(new KokoToolCall
            {
                Name = "fs_write_text",
                Confirmed = true,
                Arguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["path"] = relativePath,
                    ["content"] = content
                }
            }, ct).ConfigureAwait(false);

            if (!result.Success || !result.Verified)
                return "Content was generated, but the file write failed verification: " + result.Reason;

            var fullPath = ServiceContainer.FileTools.ResolvePath(relativePath);
            KokoSystemLog.Write("GENERATED-CONTENT", $"verified artifact path={fullPath} chars={content.Length}");
            return $"Готово. Створено й перевірено файл: `{fullPath}` ({content.Length} символів).";
        }

        private static async Task<string> ExecuteLivePcActionPlanAsync(PcActionPlan plan, CancellationToken ct)
        {
            var result = await ServiceContainer.ToolGateway.ExecuteAsync(new KokoToolCall
            {
                Name = "pc_action",
                Payload = plan
            }, ct).ConfigureAwait(false);

            return result.RawResult is PcActionExecutionResult pcResult
                ? PcIntentRouter.FormatExecutionResult(pcResult)
                : result.ToLlmText();
        }

        private bool TryStartObservationAgentTask(string userText, out string reply)
        {
            reply = "";
            try
            {
                if (!ServiceContainer.IsInitialized ||
                    !KokoResponsePlannerEngine.LooksLikeLongObservationObjective(userText))
                    return false;

                HookAgentTaskEvents();
                var objective = "Observation: " + userText.Trim();
                var task = ServiceContainer.AgentTasks.AddTask(objective, priority: 8);
                ServiceContainer.AgentTasks.Start();
                var options = KokoObservationService.BuildOptions(objective, task.Id);
                reply = $"Observation task `{task.Id}` started: {KokoObservationService.DescribePlan(options)}";
                RefreshAgentTaskBoard();
                return true;
            }
            catch (Exception ex)
            {
                KokoSystemLog.Write("OBSERVATION", "Observation task failed to start: " + ex);
                reply = "Observation task failed to start: " + ex.Message;
                return true;
            }
        }
    }
}
