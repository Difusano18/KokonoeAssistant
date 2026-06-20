using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using KokonoeAssistant.Services;
using Microsoft.Win32;
using Newtonsoft.Json;
using SkiaSharp;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using TgUpdate   = Telegram.Bot.Types.Update;
using WMsgBox    = System.Windows.MessageBox;
using WButton    = System.Windows.Controls.Button;
using WKeyArgs   = System.Windows.Input.KeyEventArgs;
using WDragArgs  = System.Windows.DragEventArgs;
using WClipboard = System.Windows.Clipboard;
using WDataFmts  = System.Windows.DataFormats;
using WTextBox   = System.Windows.Controls.TextBox;
using MediaBrush   = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor   = System.Windows.Media.Color;
using WpfRect      = System.Windows.Shapes.Rectangle;
using WpfFF        = System.Windows.Media.FontFamily;
using WpfSz        = System.Windows.Size;
using WpfOri       = System.Windows.Controls.Orientation;
using WinForms     = System.Windows.Forms;

namespace KokonoeAssistant
{
    public partial class MainWindow
    {
        // ------------------------------------------------------------
        // SANDBOX TAB
        // ------------------------------------------------------------

        private async void SandboxRun_Click(object sender, RoutedEventArgs e)
        {
            var prompt = SandboxInput.Text.Trim();
            if (string.IsNullOrEmpty(prompt)) return;
            SandboxOutput.Text = "// running...";
            try
            {
                var result = await _llm.SendAsync(prompt, ct: CancellationToken.None);
                SandboxOutput.Text = result ?? "(empty response)";
            }
            catch (Exception ex) { SandboxOutput.Text = $"// error: {ex.Message}"; }
        }


        private void SandboxClear_Click(object sender, RoutedEventArgs e)
        {
            SandboxInput.Clear();
            SandboxOutput.Text = "?";
        }

        private void AgentCreateTask_Click(object sender, RoutedEventArgs e)
        {
            HookAgentTaskEvents();
            var objective = SandboxInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(objective))
            {
                AgentTaskStatusText.Text = "Put an objective in the prompt box first. Incredible concept, I know.";
                return;
            }

            try
            {
                var task = ServiceContainer.AgentTasks.AddTask(objective);
                AgentTaskStatusText.Text = $"created {task.Id} with {task.Steps.Count} steps";
                RefreshAgentTaskBoard();
            }
            catch (Exception ex)
            {
                AgentTaskStatusText.Text = $"create failed: {ex.Message}";
            }
        }

        private void AgentStart_Click(object sender, RoutedEventArgs e)
        {
            HookAgentTaskEvents();
            try
            {
                ServiceContainer.AgentTasks.Start();
                AgentTaskStatusText.Text = "runner started";
                RefreshAgentTaskBoard();
            }
            catch (Exception ex)
            {
                AgentTaskStatusText.Text = $"start failed: {ex.Message}";
            }
        }

        private void AgentStop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ServiceContainer.AgentTasks.Stop();
                AgentTaskStatusText.Text = "runner stopped";
                RefreshAgentTaskBoard();
            }
            catch (Exception ex)
            {
                AgentTaskStatusText.Text = $"stop failed: {ex.Message}";
            }
        }

        private void AgentRefresh_Click(object sender, RoutedEventArgs e) => RefreshAgentTaskBoard();

        private void AgentDetailLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _agentDetailLevel = AgentDetailLevelBox?.SelectedIndex ?? 1;
            RefreshAgentTaskBoard();
        }

        private void GenesisRefresh_Click(object sender, RoutedEventArgs e) => RefreshGenesisFabric();

        private void GenesisRegister_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var role = GenesisRoleBox.SelectedItem as KokoAgentRoleDefinition;
                var roleId = role?.RoleId ?? "analyst";
                var provider = GenesisProviderBox.Text?.Trim();
                var model = GenesisModelBox.Text?.Trim();
                var agent = ServiceContainer.AgentFactory.CreateOrUpdateAgent(
                    GenesisAgentIdBox.Text,
                    roleId,
                    displayName: role?.DisplayName,
                    provider: string.IsNullOrWhiteSpace(provider) ? null : provider,
                    model: string.IsNullOrWhiteSpace(model) ? null : model);

                GenesisStatusText.Text = $"registered {agent.AgentId} as {agent.RoleId}";
                RefreshGenesisFabric(selectAgentId: agent.AgentId);
            }
            catch (Exception ex)
            {
                GenesisStatusText.Text = "register failed: " + ex.Message;
            }
        }

        private void GenesisDisable_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var agentId = GenesisAgentIdBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(agentId))
                {
                    GenesisStatusText.Text = "agent id is empty";
                    return;
                }

                var ok = ServiceContainer.AgentFactory.SetAgentEnabled(agentId, false);
                GenesisStatusText.Text = ok ? $"disabled {agentId}" : $"agent not found: {agentId}";
                RefreshGenesisFabric(selectAgentId: agentId);
            }
            catch (Exception ex)
            {
                GenesisStatusText.Text = "disable failed: " + ex.Message;
            }
        }

        private void GenesisRunTask_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var objective = SandboxInput.Text.Trim();
                if (string.IsNullOrWhiteSpace(objective))
                {
                    GenesisStatusText.Text = "prompt workspace is empty";
                    return;
                }

                HookAgentTaskEvents();
                var agentId = GenesisAgentIdBox.Text.Trim();
                var role = (GenesisRoleBox.SelectedItem as KokoAgentRoleDefinition)?.RoleId ?? "analyst";
                var task = ServiceContainer.AgentTasks.AddTask($"[{agentId}/{role}] {objective}", priority: 7);
                ServiceContainer.Blackboard.Publish(agentId, "task", $"queued task {task.Id}: {objective}", 0.68,
                    new { task.Id, role, objective }, "queued");
                GenesisStatusText.Text = $"queued {task.Id}";
                RefreshAgentTaskBoard();
                RefreshGenesisFabric(selectAgentId: agentId);
            }
            catch (Exception ex)
            {
                GenesisStatusText.Text = "queue failed: " + ex.Message;
            }
        }

        private void RefreshGenesisFabric(string? selectAgentId = null)
        {
            try
            {
                if (!ServiceContainer.IsInitialized)
                    return;

                var factory = ServiceContainer.AgentFactory;
                var roles = factory.Roles.ToList();
                if (GenesisRoleBox.Items.Count == 0)
                {
                    GenesisRoleBox.ItemsSource = roles;
                    GenesisRoleBox.SelectedItem = roles.FirstOrDefault(r => r.RoleId == "analyst") ?? roles.FirstOrDefault();
                }

                var snap = factory.GetSnapshot();
                GenesisConsoleText.Text = factory.RenderConsole();
                GenesisStatusText.Text = $"agents {snap.Agents.Count(a => a.Enabled)}/{snap.Agents.Count} | blackboard {snap.BlackboardRecent.Count}";

                var selected = !string.IsNullOrWhiteSpace(selectAgentId)
                    ? snap.Agents.FirstOrDefault(a => a.AgentId.Equals(selectAgentId.Trim(), StringComparison.OrdinalIgnoreCase))
                    : snap.Agents.FirstOrDefault(a => a.AgentId.Equals(GenesisAgentIdBox.Text.Trim(), StringComparison.OrdinalIgnoreCase));
                if (selected != null)
                {
                    GenesisAgentIdBox.Text = selected.AgentId;
                    GenesisProviderBox.Text = selected.Provider;
                    GenesisModelBox.Text = selected.Model;
                    GenesisRoleBox.SelectedItem = roles.FirstOrDefault(r => r.RoleId.Equals(selected.RoleId, StringComparison.OrdinalIgnoreCase))
                        ?? GenesisRoleBox.SelectedItem;
                }
            }
            catch (Exception ex)
            {
                GenesisStatusText.Text = "fabric offline: " + ex.Message;
            }
        }

        private bool HookAgentTaskEvents()
        {
            if (!ServiceContainer.IsInitialized)
                return false;

            if (!_agentTaskEventsHooked)
            {
                var agentTasks = ServiceContainer.AgentTasks;
                agentTasks.ActivityChanged += activity =>
                {
                    Dispatcher.InvokeAsync(() => UpdateAgentActivityPanel(activity), DispatcherPriority.Background);
                };
                agentTasks.TaskCompleted += (task, notice) =>
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        AgentTaskStatusText.Text = notice.Notice;
                        AppendAgentActivity(new KokoAgentActivitySnapshot
                        {
                            UpdatedAt = DateTime.Now,
                            Phase = "report",
                            Tool = "CompletionPolicy",
                            Focus = task.Objective,
                            Thought = notice.Notice,
                            TaskId = task.Id
                        });
                        var visible = BuildVisibleAgentCompletion(task, notice);
                        if (!string.IsNullOrWhiteSpace(visible))
                        {
                            AddMessageBubble(new ChatMessageVm { Role = "assistant", Content = visible });
                            try
                            {
                                ServiceContainer.ChatRepository.InsertMessage(new ChatRepository.ChatMessage
                                {
                                    Content = visible,
                                    Role = "assistant",
                                    Author = "Kokonoe",
                                    Timestamp = DateTime.Now
                                });
                            }
                            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "HookAgentTaskEvents failed near source line 2603: " + ex); }
                            _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("agent", task.Objective, visible));
                        }
                        RefreshAgentTaskBoard();
                    }, DispatcherPriority.Background);
                };
                _agentTaskEventsHooked = true;
            }

            if (!_agentRuntimeEventsHooked)
            {
                var agentRuntime = ServiceContainer.AgentRuntime;
                agentRuntime.ActivityChanged += activity =>
                {
                    Dispatcher.InvokeAsync(() => UpdateAgentActivityPanel(activity), DispatcherPriority.Background);
                };
                _agentRuntimeEventsHooked = true;
            }

            return true;
        }

        private void RefreshAgentTaskBoard()
        {
            try
            {
                if (!HookAgentTaskEvents())
                {
                    AgentTaskStatusText.Text = "agent services initializing";
                    return;
                }
                var board = ServiceContainer.AgentTasks.RenderBoard();
                var snap = ServiceContainer.AgentTasks.GetSnapshot();
                AgentTaskBoardText.Text = RenderAgentBoard(snap, board);
                AgentTaskStatusText.Text = $"tasks {snap.Tasks.Count} | running {snap.RunningSteps}/{snap.MaxParallel}";
                UpdateAgentActivityPanel(snap.Activity);
            }
            catch (Exception ex)
            {
                AgentTaskStatusText.Text = $"refresh failed: {ex.Message}";
            }
        }

        private void UpdateAgentActivityPanel(KokoAgentActivitySnapshot activity)
        {
            AgentPhaseText.Text = $"phase: {activity.Phase}";
            AgentToolText.Text = $"tool: {activity.Tool}";
            AgentFocusText.Text = $"focus: {activity.Focus}";
            AgentThoughtText.Text = $"thought: {activity.Thought}";
            var workMode = "Unknown";
            try { workMode = ServiceContainer.BrainEngine.GetCurrentWorkModeLabel(); } catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "UpdateAgentActivityPanel failed near source line 2653: " + ex); }
            ThoughtStreamStatusText.Text =
                $"mode {workMode} | agent {activity.Phase} | {activity.Tool} | {TrimLiveCoreLine(activity.Focus, 120)}";
            ThoughtStreamStatusText.ToolTip =
                $"{activity.UpdatedAt:HH:mm:ss} mode={workMode} {activity.Phase}/{activity.Tool}\n{activity.Focus}\n{activity.Thought}";
            UpdateAgentEmotionLine();
            AppendAgentActivity(activity);
        }

        private string RenderAgentBoard(KokoAgentTaskSnapshot snap, string fallback)
        {
            if (_agentDetailLevel <= 0)
                return $"tasks {snap.Tasks.Count} | running {snap.RunningSteps}/{snap.MaxParallel}\n{snap.Activity.Phase} -> {snap.Activity.Tool}\n{snap.Activity.Thought}";

            if (_agentDetailLevel == 1)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Agent Board | tasks {snap.Tasks.Count} | running {snap.RunningSteps}/{snap.MaxParallel}");
                foreach (var task in snap.Tasks.Take(6))
                {
                    var active = task.Steps.OrderBy(s => s.Order)
                        .FirstOrDefault(s => s.Status is KokoAgentTaskStatus.Running or KokoAgentTaskStatus.Pending);
                    var done = task.Steps.Count(s => s.Status == KokoAgentTaskStatus.Completed);
                    sb.AppendLine($"[{task.Status}] {task.Id} p{task.Priority} | {done}/{task.Steps.Count} | {task.Objective}");
                    if (active != null)
                        sb.AppendLine($"  -> {active.Kind}: {active.Title}");
                }
                return sb.ToString();
            }

            return fallback;
        }

        private void UpdateAgentEmotionLine()
        {
            try
            {
                var brain = ServiceContainer.BrainEngine;
                var emotion = DashboardEmotionLabel(brain.Emotion.Current);
                var state = brain.State;
                var detail = _agentDetailLevel switch { 0 => "compact", 2 => "verbose", _ => "normal" };
                AgentEmotionStateText.Text = $"emotion: {emotion} | mood {state.MoodScore:F2} | detail: {detail}";
                AgentEmotionStateText.Foreground = DashMakeBrush(brain.Emotion.Current);
            }
            catch
            {
                AgentEmotionStateText.Text = "emotion: offline | detail: ?";
            }
        }

        private void AppendAgentActivity(KokoAgentActivitySnapshot activity)
        {
            if (string.IsNullOrWhiteSpace(activity.Phase) && string.IsNullOrWhiteSpace(activity.Tool))
                return;

            if (_agentActivityTrace.Count > 0)
            {
                var last = _agentActivityTrace[0];
                if (last.Phase == activity.Phase &&
                    last.Tool == activity.Tool &&
                    last.Focus == activity.Focus &&
                    last.Thought == activity.Thought)
                    return;
            }

            _agentActivityTrace.Insert(0, activity);
            if (_agentActivityTrace.Count > 18)
                _agentActivityTrace.RemoveRange(18, _agentActivityTrace.Count - 18);

            var take = _agentDetailLevel switch { 0 => 4, 2 => 14, _ => 8 };
            AgentActivityLogText.Text = string.Join("\n", _agentActivityTrace.Take(take).Select(a =>
                $"[{a.UpdatedAt:HH:mm:ss}] {a.Phase}/{a.Tool} :: {TrimLiveCoreLine(a.Thought, _agentDetailLevel == 2 ? 160 : 90)}"));
        }
    }
}
