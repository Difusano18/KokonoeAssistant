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
        // MCP ENHANCED CATALOG
        // ------------------------------------------------------------

        private void McpSyncNotes_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var recent = _obsidian.GetNotesModifiedToday();
                McpOutput.Text = recent.Count == 0
                    ? "No notes modified today."
                    : $"Modified today ({recent.Count}):\n" + string.Join("\n", recent);
            }
            catch (Exception ex) { McpOutput.Text = $"Error: {ex.Message}"; }
        }

        private void RightVaultDoctor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var report = _obsidian.RunVaultDoctor(repair: true);
                var linkProblems = report.FolderWikiLinkCount + report.SuppressedActorLinkCount;
                _rightOpsVaultLine =
                    $"vault doctor {report.HealthScore}/100 · empty {report.EmptyMarkdownFiles.Count} · links {linkProblems} · " +
                    $"fm {report.FrontmatterIssues.Count} · moj {report.MojibakeSuspects.Count} · miss {report.MissingWikiTargets.Count}";
                _rightOpsVaultScanAt = DateTime.Now;
                RightVaultDoctorText.Text = _rightOpsVaultLine;
                McpOutput.Text =
                    $"Vault Doctor ({report.HealthScore}/100)\n" +
                    $"empty: {report.EmptyMarkdownFiles.Count}\n" +
                    $"folder links: {report.FolderWikiLinkCount}\n" +
                    $"Kokonoe links: {report.SuppressedActorLinkCount}\n" +
                    $"frontmatter: {report.FrontmatterIssues.Count}\n" +
                    $"mojibake: {report.MojibakeSuspects.Count}\n" +
                    $"missing targets: {report.MissingWikiTargets.Count}\n" +
                    $"repaired: {report.RepairedFiles.Count}\n" +
                    $"deleted: {report.DeletedEmptyFiles.Count}";
            }
            catch (Exception ex) { McpOutput.Text = $"Vault doctor error: {ex.Message}"; }
        }

        private void RightInspector_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var brain = ServiceContainer.BrainEngine;
                brain.ExportInspectorToVault();
                McpOutput.Text = brain.BuildInspectorMarkdown();
            }
            catch (Exception ex) { McpOutput.Text = $"Inspector error: {ex.Message}"; }
        }

        private void RightGenesis_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RefreshGenesisFabric();
                McpOutput.Text = ServiceContainer.AgentFactory.RenderConsole();
            }
            catch (Exception ex) { McpOutput.Text = $"Genesis error: {ex.Message}"; }
        }

        private async void RightOverlord_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                McpOutput.Text = "System Overlord scanning configured roots...";
                var snap = await ServiceContainer.SystemOverlord.ScanAsync(maxFiles: AppSettings.Load().SystemOverlordMaxFiles);
                McpOutput.Text = ServiceContainer.SystemOverlord.RenderConsole();
                if (snap.Proposals.Count == 0)
                    McpOutput.Text += "\n\nNo maintenance proposals. Either clean system, or insufficient evidence. Shocking restraint.";
            }
            catch (Exception ex) { McpOutput.Text = $"Overlord error: {ex.Message}"; }
        }

        private void McpRecentNotes_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var notes = _obsidian.ListNotes();
                var recent = notes.Take(20).ToList();
                McpOutput.Text = $"Recent notes ({recent.Count}):\n" + string.Join("\n", recent);
            }
            catch (Exception ex) { McpOutput.Text = $"Error: {ex.Message}"; }
        }

        private void McpBacklinks_Click(object sender, RoutedEventArgs e)
        {
            var path = Microsoft.VisualBasic.Interaction.InputBox("Note path for backlinks:", "Backlinks", "");
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                var links = _obsidian.GetBacklinks(path);
                McpOutput.Text = links.Count == 0
                    ? "No backlinks found."
                    : $"Backlinks ({links.Count}):\n" + string.Join("\n", links);
            }
            catch (Exception ex) { McpOutput.Text = $"Error: {ex.Message}"; }
        }

        private void McpIsolated_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var isolated = _obsidian.GetIsolatedNotes();
                McpOutput.Text = isolated.Count == 0
                    ? "No isolated notes."
                    : $"Isolated ({isolated.Count}):\n" + string.Join("\n", isolated.Take(30));
            }
            catch (Exception ex) { McpOutput.Text = $"Error: {ex.Message}"; }
        }

        // ------------------------------------------------------------
        // VAULT SIDEBAR
        // ------------------------------------------------------------

        private void LoadVaultSidebar(string? filter = null)
        {
            try
            {
                var vault = AppSettings.Load().VaultPath;
                VaultTree.Items.Clear();

                var root = new DirectoryInfo(vault);
                if (!root.Exists) return;

                var hasFilter = !string.IsNullOrWhiteSpace(filter);

                foreach (var dir in root.GetDirectories().Where(d => !d.Name.StartsWith(".")))
                {
                    var files = dir.GetFiles("*.md")
                        .Where(f => !hasFilter || f.Name.Contains(filter!, StringComparison.OrdinalIgnoreCase))
                        .Take(20)
                        .ToList();
                    if (hasFilter && files.Count == 0 && !dir.Name.Contains(filter!, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var node = new TreeViewItem
                    {
                        Header = $"📁 {dir.Name}",
                        Tag = dir.FullName
                    };
                    foreach (var file in files)
                    {
                        node.Items.Add(new TreeViewItem
                        {
                            Header = $"  {file.Name[..^3]}",
                            Tag    = file.FullName
                        });
                    }
                    VaultTree.Items.Add(node);
                }

                foreach (var file in root.GetFiles("*.md")
                    .Where(f => !hasFilter || f.Name.Contains(filter!, StringComparison.OrdinalIgnoreCase))
                    .Take(20))
                {
                    VaultTree.Items.Add(new TreeViewItem
                    {
                        Header = $"📄 {file.Name[..^3]}",
                        Tag    = file.FullName
                    });
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("UI-CATCH", "LoadVaultSidebar failed near source line 6048: " + ex); }
        }

        private void VaultTree_Selected(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem item && item.Tag is string path && File.Exists(path))
            {
                _currentNotePath = path;
                if (_activeTab == "Vault")
                {
                    NoteEditor.Text = File.ReadAllText(path);
                    NotePathLabel.Text = Path.GetFileName(path);
                    UpdateNoteStats();
                }
            }
        }

        private void VaultSearch_Changed(object sender, TextChangedEventArgs e)
        {
            var q = VaultSearchBox.Text.Trim();
            LoadVaultSidebar(q.Length < 2 ? null : q);
        }

        // ------------------------------------------------------------
        // VAULT TAB
        // ------------------------------------------------------------

        private void RefreshNotesList()
        {
            try
            {
                var notes = _obsidian.ListNotes()
                    .Select(p => new NoteVm
                    {
                        Path  = p,
                        Title = Path.GetFileNameWithoutExtension(p)
                    }).ToList();
                NotesList.ItemsSource = notes;
            }
            catch (Exception ex)
            {
                KokoSystemLog.Write("VAULT-UI", "refresh notes failed: " + ex);
            }
        }

        private void NotesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NotesList.SelectedItem is not NoteVm vm) return;
            _currentNotePath = vm.Path;
            NoteEditor.Text  = _obsidian.ReadNote(vm.Path) ?? "";
            NotePathLabel.Text = vm.Path;
            UpdateNoteStats();
        }

        private void UpdateNoteStats()
        {
            var t = NoteEditor.Text;
            NoteStatsLabel.Text = $"{t.Length} chars · {t.Split('\n').Length} lines";
        }

        private void SaveNote_Click(object sender, RoutedEventArgs e)
        {
            if (_currentNotePath == null) return;
            _obsidian.WriteNote(_currentNotePath, NoteEditor.Text);
            NoteStatsLabel.Text = $"Збережено {DateTime.Now:HH:mm} · " + NoteStatsLabel.Text;
        }

        private void DeleteNote_Click(object sender, RoutedEventArgs e)
        {
            if (_currentNotePath == null) return;
            if (WMsgBox.Show($"Видалити '{_currentNotePath}'?", "Підтвердження",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            _obsidian.DeleteNote(_currentNotePath);
            NoteEditor.Clear();
            NotePathLabel.Text = "Виберіть нотатку";
            _currentNotePath = null;
            RefreshNotesList();
        }

        private void NewNote_Click(object sender, RoutedEventArgs e)
        {
            var title = Microsoft.VisualBasic.Interaction.InputBox(
                "Назва нотатки:", "Нова нотатка", "");
            if (string.IsNullOrWhiteSpace(title)) return;
            var path = _obsidian.CreateNote(title);
            RefreshNotesList();
            NotePathLabel.Text = title + ".md";
            NoteEditor.Text    = File.ReadAllText(path);
            _currentNotePath   = title + ".md";
        }

        private void RefreshVault_Click(object sender, RoutedEventArgs e)
        {
            RefreshNotesList();
            LoadVaultSidebar();
            UpdateMemoryOpsPanel();
        }

        private void UpdateMemoryOpsPanel()
        {
            try
            {
                var quality = _obsidian.AnalyzeMemoryQuality();
                var queue = _obsidian.BuildTaskQueue();
                var review = _obsidian.BuildMemoryReview(quality, queue);
                MemoryOpsStatusLabel.Text =
                    $"items {quality.NormalizedItems.Count} | exact dup {quality.DuplicateGroups.Count} | similar {quality.SimilarGroups.Count} | tasks {queue.OpenTasks.Count} | review {review.Actions.Count}";

                var state = ServiceContainer.BrainEngine?.State;
                var detail = state == null
                    ? "brain state unavailable"
                    : $"pending batch {state.PendingVaultExchangeCount}/5";

                if (state?.LastAutoVaultSyncAt > DateTime.MinValue)
                    detail += $" | last sync {state.LastAutoVaultSyncAt:dd.MM HH:mm}";

                MemoryOpsDetailLabel.Text = detail;
            }
            catch (Exception ex)
            {
                MemoryOpsStatusLabel.Text = "memory ops unavailable";
                MemoryOpsDetailLabel.Text = ex.Message;
            }
        }

        private void MemoryOpsRefresh_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = _obsidian.MaintainKokonoeVaultArchitecture("gui-memory-ops");
                RefreshNotesList();
                LoadVaultSidebar();
                UpdateMemoryOpsPanel();
                WMsgBox.Show(result.ToString(), "Memory Ops");
            }
            catch (Exception ex)
            {
                WMsgBox.Show(ex.Message, "Memory Ops", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MemoryOpsPreviewCleanup_Click(object sender, RoutedEventArgs e)
        {
            RunMemoryCleanup(dryRun: true);
        }

        private void MemoryOpsApplyCleanup_Click(object sender, RoutedEventArgs e)
        {
            if (WMsgBox.Show(
                    "Apply duplicate memory cleanup? Preview first if you want to inspect what will be removed.",
                    "Memory Cleanup",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            RunMemoryCleanup(dryRun: false);
        }

        private void RunMemoryCleanup(bool dryRun)
        {
            try
            {
                var result = _obsidian.CleanupDuplicateMemoryItems(dryRun);
                RefreshNotesList();
                LoadVaultSidebar();
                UpdateMemoryOpsPanel();
                OpenManagedVaultNote("Kokonoe/Memory/Cleanup.md");
                WMsgBox.Show(result.ToString(), "Memory Cleanup");
            }
            catch (Exception ex)
            {
                WMsgBox.Show(ex.Message, "Memory Cleanup", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenMemoryQuality_Click(object sender, RoutedEventArgs e)
        {
            OpenManagedVaultNote("Kokonoe/Memory/Quality.md");
        }

        private void OpenMemoryReview_Click(object sender, RoutedEventArgs e)
        {
            OpenManagedVaultNote("Kokonoe/Memory/Review.md");
        }

        private void OpenMemoryCleanup_Click(object sender, RoutedEventArgs e)
        {
            OpenManagedVaultNote("Kokonoe/Memory/Cleanup.md");
        }

        private void OpenTasksQueue_Click(object sender, RoutedEventArgs e)
        {
            OpenManagedVaultNote("Kokonoe/Tasks Queue.md");
        }

        private void OpenVaultArchitecture_Click(object sender, RoutedEventArgs e)
        {
            OpenManagedVaultNote("Kokonoe/Vault Index.md");
        }

        private void OpenManagedVaultNote(string path)
        {
            try
            {
                var content = _obsidian.ReadNote(path);
                if (content == null)
                {
                    _obsidian.MaintainKokonoeVaultArchitecture("gui-open-managed-note");
                    content = _obsidian.ReadNote(path);
                }

                if (content == null)
                {
                    WMsgBox.Show($"Note not found: {path}", "Vault", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _currentNotePath = path;
                NoteEditor.Text = content;
                NotePathLabel.Text = path;
                UpdateNoteStats();
            }
            catch (Exception ex)
            {
                WMsgBox.Show(ex.Message, "Vault", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // MCP Tools
        private void McpListNotes_Click(object sender, RoutedEventArgs e)
        {
            var notes = _obsidian.ListNotes();
            McpOutput.Text = notes.Count == 0
                ? "Нотаток не знайдено."
                : string.Join("\n", notes.Take(30));
        }

        private void McpSearch_Click(object sender, RoutedEventArgs e)
        {
            var q = Microsoft.VisualBasic.Interaction.InputBox("Пошуковий запит:", "Search Vault", "");
            if (string.IsNullOrWhiteSpace(q)) return;
            var results = _obsidian.SearchNotes(q, 15);
            McpOutput.Text = results.Count == 0
                ? "Нічого не знайдено."
                : string.Join("\n", results.Select(r => $"[{r.Score}] {r.Path}\n  {r.Preview.Replace('\n', ' ')}"));
        }

        private void McpDailyNote_Click(object sender, RoutedEventArgs e)
        {
            var content = _obsidian.GetOrCreateDailyNote();
            McpOutput.Text = content.Length > 400
                ? content.Substring(0, 400) + "\n..."
                : content;
        }

        private void McpNewNote_Click(object sender, RoutedEventArgs e)
        {
            var title = Microsoft.VisualBasic.Interaction.InputBox("Назва нотатки:", "New Note", "");
            if (string.IsNullOrWhiteSpace(title)) return;
            var path = _obsidian.CreateNote(title);
            McpOutput.Text = $"Створено: {path}";
            LoadVaultSidebar();
        }

        private void McpBuildGraph_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var graph = ServiceContainer.KnowledgeGraph;
                graph.Save();
                McpOutput.Text = $"Graph: {graph.Nodes.Count} nodes, {graph.Edges.Count} edges";
            }
            catch (Exception ex) { McpOutput.Text = ex.Message; }
        }

        private void OpenKnowledgeGraph_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var graph = ServiceContainer.KnowledgeGraph;
                var vault = ServiceContainer.ObsidianMcp?.VaultPath ?? AppSettings.Load().VaultPath;
                var window = new GraphVisualizationWindow(graph, vault);
                window.Show();
            }
            catch (Exception ex) { McpOutput.Text = $"Error opening graph: {ex.Message}"; }
        }
    }
}
