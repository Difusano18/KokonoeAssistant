using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace KokonoeAssistant.Services
{
    public sealed class KokoOverlordFileFact
    {
        public string Path { get; set; } = "";
        public string Name { get; set; } = "";
        public string Extension { get; set; } = "";
        public long SizeBytes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public string Bucket { get; set; } = "";
        public string Signal { get; set; } = "";
    }

    public sealed class KokoOverlordProcessFact
    {
        public string ProcessName { get; set; } = "";
        public int ProcessId { get; set; }
        public double WorkingSetMb { get; set; }
        public string WindowTitle { get; set; } = "";
    }

    public sealed class KokoOverlordProposal
    {
        public string Kind { get; set; } = "";
        public string Title { get; set; } = "";
        public string Reason { get; set; } = "";
        public PcActionRiskTier RiskTier { get; set; } = PcActionRiskTier.Observe;
        public PcPolicyDecisionKind Decision { get; set; } = PcPolicyDecisionKind.Allowed;
        public string PendingActionId { get; set; } = "";
        public List<string> Targets { get; set; } = new();
    }

    public sealed class KokoOverlordSnapshot
    {
        public DateTime TakenAt { get; set; } = DateTime.Now;
        public List<string> Roots { get; set; } = new();
        public int ScannedFiles { get; set; }
        public long TotalBytes { get; set; }
        public List<KokoOverlordFileFact> Files { get; set; } = new();
        public List<KokoOverlordProcessFact> Processes { get; set; } = new();
        public List<KokoOverlordProposal> Proposals { get; set; } = new();
        public string Status { get; set; } = "idle";
        public string Error { get; set; } = "";
    }

    public sealed class KokoOverlordSurpriseResult
    {
        public bool Success { get; set; }
        public string FilePath { get; set; } = "";
        public int ScannedFiles { get; set; }
        public int SignalCount { get; set; }
        public string Error { get; set; } = "";
        public List<string> Highlights { get; set; } = new();

        public string ToUserReply()
        {
            if (!Success)
                return "Не зробила. Причина: " + (string.IsNullOrWhiteSpace(Error) ? "невідома помилка" : Error);

            var shortSignal = Highlights.Count == 0
                ? "нічого драматичного, але індекс оновлений"
                : string.Join("; ", Highlights.Take(2));
            return $"Зроблено. Файл: `{FilePath}`. Скан: {ScannedFiles} файлів, сигналів {SignalCount}. {shortSignal}";
        }
    }

    public sealed class KokoSystemOverlordService
    {
        private readonly object _lock = new();
        private readonly string _dataDir;
        private readonly string _indexPath;
        private readonly KokoInternalBlackboardService? _blackboard;
        private readonly KokoServiceHeartbeatService? _heartbeat;
        private readonly PcActionPolicyEngine _policy = new();
        private readonly PcPendingActionStore _pending;
        private KokoOverlordSnapshot _last = new();

        public KokoSystemOverlordService(
            string dataDir,
            KokoInternalBlackboardService? blackboard = null,
            KokoServiceHeartbeatService? heartbeat = null)
        {
            _dataDir = dataDir;
            Directory.CreateDirectory(_dataDir);
            _indexPath = Path.Combine(_dataDir, "system-overlord-index.json");
            _blackboard = blackboard;
            _heartbeat = heartbeat;
            _pending = new PcPendingActionStore(Path.Combine(_dataDir, "system-overlord-pending-actions.jsonl"));
            LoadLast();
        }

        public KokoOverlordSnapshot LastSnapshot
        {
            get { lock (_lock) return Clone(_last); }
        }

        public async Task<KokoOverlordSnapshot> ScanAsync(
            IEnumerable<string>? roots = null,
            int? maxFiles = null,
            CancellationToken ct = default)
        {
            var settings = AppSettings.Load();
            var resolvedRoots = ResolveRoots(roots ?? SplitRoots(settings.SystemOverlordRoots));
            var limit = Math.Clamp(maxFiles ?? settings.SystemOverlordMaxFiles, 25, 5000);
            var snapshot = new KokoOverlordSnapshot
            {
                TakenAt = DateTime.Now,
                Roots = resolvedRoots,
                Status = "scanning"
            };

            try
            {
                foreach (var root in resolvedRoots)
                {
                    ct.ThrowIfCancellationRequested();
                    if (!Directory.Exists(root))
                        continue;

                    foreach (var file in EnumerateFilesSafe(root, limit - snapshot.ScannedFiles))
                    {
                        ct.ThrowIfCancellationRequested();
                        var fact = BuildFileFact(file);
                        if (fact == null)
                            continue;
                        snapshot.Files.Add(fact);
                        snapshot.ScannedFiles++;
                        snapshot.TotalBytes += fact.SizeBytes;
                        if (snapshot.ScannedFiles >= limit)
                            break;
                    }

                    if (snapshot.ScannedFiles >= limit)
                        break;
                }

                snapshot.Processes = GetProcessFacts(12).ToList();
                snapshot.Proposals = BuildProposals(snapshot);
                snapshot.Status = "ready";
                SaveSnapshot(snapshot);
                PublishSnapshot(snapshot);
            }
            catch (Exception ex)
            {
                snapshot.Status = "error";
                snapshot.Error = ex.Message;
                SaveSnapshot(snapshot);
                KokoSystemLog.Write("OVERLORD", "scan failed: " + ex.Message);
            }

            await Task.CompletedTask.ConfigureAwait(false);
            return Clone(snapshot);
        }

        public string RenderConsole()
        {
            var snap = LastSnapshot;
            var sb = new StringBuilder();
            sb.AppendLine($"SYSTEM OVERLORD | {snap.Status} | files {snap.ScannedFiles} | size {FormatBytes(snap.TotalBytes)} | pending {_pending.Count}");
            if (snap.Roots.Count > 0)
                sb.AppendLine("roots: " + string.Join("; ", snap.Roots.Take(4)));
            if (!string.IsNullOrWhiteSpace(snap.Error))
                sb.AppendLine("error: " + snap.Error);

            var buckets = snap.Files
                .GroupBy(f => f.Bucket)
                .OrderByDescending(g => g.Sum(f => f.SizeBytes))
                .Take(6)
                .Select(g => $"{g.Key}:{g.Count()}({FormatBytes(g.Sum(f => f.SizeBytes))})");
            sb.AppendLine("buckets: " + string.Join(" | ", buckets));

            if (snap.Proposals.Count > 0)
            {
                sb.AppendLine("proposals:");
                foreach (var p in snap.Proposals.Take(6))
                {
                    var pending = string.IsNullOrWhiteSpace(p.PendingActionId) ? "" : $" pending={p.PendingActionId}";
                    sb.AppendLine($"- [{p.Decision}/{p.RiskTier}] {p.Title}{pending} :: {p.Reason}");
                }
            }

            if (snap.Processes.Count > 0)
            {
                sb.AppendLine("top processes:");
                foreach (var p in snap.Processes.Take(5))
                    sb.AppendLine($"- {p.ProcessName}#{p.ProcessId} {p.WorkingSetMb:0}MB {Trim(p.WindowTitle, 70)}");
            }

            return sb.ToString().Trim();
        }

        public KokoOverlordProposal PrepareCleanupPermission(IEnumerable<string> targets, string reason)
        {
            var cleanTargets = targets
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => Path.GetFullPath(t.Trim()))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(50)
                .ToList();

            if (cleanTargets.Count == 0)
            {
                return new KokoOverlordProposal
                {
                    Kind = "cleanup",
                    Title = "No cleanup targets",
                    Reason = "Target list is empty. Stunningly efficient: nothing to do.",
                    Decision = PcPolicyDecisionKind.Blocked
                };
            }

            var plan = new PcActionPlan
            {
                Intent = "system_overlord_cleanup",
                RiskTier = PcActionRiskTier.RiskyLocal,
                AffectedPaths = cleanTargets,
                RollbackAvailable = false,
                UserFacingSummaryUk = $"Коконое знайшла {cleanTargets.Count} файл(ів) для cleanup. Причина: {reason}"
            };
            var order = 1;
            foreach (var target in cleanTargets)
                plan.Actions.Add(new PcActionStep { Order = order++, ActionType = "deleteFile", Target = target });

            var decision = _policy.Evaluate(plan, new PcContextSnapshotV2());
            var proposal = new KokoOverlordProposal
            {
                Kind = "cleanup",
                Title = $"Cleanup proposal: {cleanTargets.Count} target(s)",
                Reason = decision.Reason,
                RiskTier = decision.RiskTier,
                Decision = decision.Kind,
                Targets = cleanTargets
            };

            if (decision.ConfirmationRequired)
            {
                var pending = _pending.Save(plan, decision);
                proposal.PendingActionId = pending.ActionId;
                proposal.Reason = $"Requires explicit confirmation. Action id: {pending.ActionId}";
            }

            _blackboard?.Publish("system-overlord", "permission_request", proposal.Reason, 0.78, proposal, "pending");
            KokoSystemLog.Write("OVERLORD", $"permission request {proposal.Kind}: {proposal.Reason}");
            return proposal;
        }

        public static bool LooksLikeSystemOverlordDirective(string? text)
        {
            return KokoActionDirectiveRouter.ShouldCreateLocalArtifact(text);
        }

        public async Task<KokoOverlordSurpriseResult> CreateSurpriseNoteAsync(
            string userRequest,
            CancellationToken ct = default,
            IEnumerable<string>? rootsOverride = null,
            string? desktopOverride = null)
        {
            var result = new KokoOverlordSurpriseResult();
            try
            {
                var desktop = ResolveDesktop(desktopOverride);
                Directory.CreateDirectory(desktop);

                var roots = ResolveSurpriseRoots(rootsOverride, desktop);
                var settings = AppSettings.Load();
                var limit = Math.Clamp(Math.Max(settings.SystemOverlordMaxFiles, 450), 100, 1200);
                var snapshot = await ScanAsync(roots, maxFiles: limit, ct).ConfigureAwait(false);

                var highlights = BuildSurpriseHighlights(snapshot);
                var filePath = Path.Combine(desktop, $"Kokonoe_Gift_{DateTime.Now:yyyyMMdd-HHmmss}.txt");
                var content = BuildSurpriseNote(userRequest, snapshot, highlights);
                await File.WriteAllTextAsync(filePath, content, Encoding.UTF8, ct).ConfigureAwait(false);

                result.Success = true;
                result.FilePath = filePath;
                result.ScannedFiles = snapshot.ScannedFiles;
                result.SignalCount = snapshot.Files.Count(f => !string.Equals(f.Signal, "indexed", StringComparison.OrdinalIgnoreCase));
                result.Highlights = highlights.Take(4).ToList();

                var summary = $"created surprise note {filePath}; scanned={result.ScannedFiles}; signals={result.SignalCount}";
                _blackboard?.Publish("system-overlord", "surprise_note", summary, 0.82, result, "done");
                _heartbeat?.Update("SYSTEM_OVERLORD", "surprise-note", summary);
                KokoSystemLog.Write("OVERLORD", summary);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                KokoSystemLog.Write("OVERLORD", "surprise note failed: " + ex.Message);
            }

            return result;
        }

        private List<KokoOverlordProposal> BuildProposals(KokoOverlordSnapshot snapshot)
        {
            var proposals = new List<KokoOverlordProposal>();
            var downloadsOld = snapshot.Files
                .Where(f => f.Signal == "stale_download" || f.Signal == "large_archive")
                .OrderByDescending(f => f.SizeBytes)
                .Take(20)
                .ToList();

            if (downloadsOld.Count > 0)
            {
                proposals.Add(new KokoOverlordProposal
                {
                    Kind = "cleanup",
                    Title = $"Review {downloadsOld.Count} stale download/archive files",
                    Reason = $"Potential cleanup: {FormatBytes(downloadsOld.Sum(f => f.SizeBytes))}. Requires explicit confirmation before any delete/move.",
                    RiskTier = PcActionRiskTier.RiskyLocal,
                    Decision = PcPolicyDecisionKind.NeedsConfirmation,
                    Targets = downloadsOld.Select(f => f.Path).ToList()
                });
            }

            var duplicateNames = snapshot.Files
                .GroupBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() >= 2)
                .OrderByDescending(g => g.Sum(f => f.SizeBytes))
                .Take(5)
                .ToList();
            foreach (var group in duplicateNames)
            {
                proposals.Add(new KokoOverlordProposal
                {
                    Kind = "duplicate_review",
                    Title = $"Duplicate name cluster: {group.Key}",
                    Reason = $"{group.Count()} files share a name; review before cleanup. Yes, guessing deletes is how amateurs lose work.",
                    RiskTier = PcActionRiskTier.Prepare,
                    Decision = PcPolicyDecisionKind.Allowed,
                    Targets = group.Select(f => f.Path).Take(8).ToList()
                });
            }

            var heavy = snapshot.Processes.FirstOrDefault(p => p.WorkingSetMb >= 1200);
            if (heavy != null)
            {
                proposals.Add(new KokoOverlordProposal
                {
                    Kind = "process_review",
                    Title = $"Heavy process: {heavy.ProcessName}",
                    Reason = $"{heavy.WorkingSetMb:0} MB working set. Review before killing anything.",
                    RiskTier = PcActionRiskTier.Prepare,
                    Decision = PcPolicyDecisionKind.Allowed,
                    Targets = { heavy.ProcessName }
                });
            }

            return proposals;
        }

        private static List<string> ResolveSurpriseRoots(IEnumerable<string>? rootsOverride, string desktop)
        {
            var roots = ResolveRoots(rootsOverride ?? SplitRoots(AppSettings.Load().SystemOverlordRoots));
            AddRoot(roots, desktop);
            if (rootsOverride != null)
            {
                return roots
                    .Where(Directory.Exists)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(8)
                    .ToList();
            }

            AddRoot(roots, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
            AddRoot(roots, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            AddRoot(roots, Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
            return roots
                .Where(Directory.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToList();
        }

        private static void AddRoot(List<string> roots, string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;
            try
            {
                var full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
                if (!roots.Contains(full, StringComparer.OrdinalIgnoreCase))
                    roots.Insert(0, full);
            }
            catch (Exception suppressedEx397) { KokoSystemLog.Write("SYSTEMOVERLORDSERVICE-CATCH", "AddRoot failed near source line 397: " + suppressedEx397); }
        }

        private static string ResolveDesktop(string? desktopOverride)
        {
            if (!string.IsNullOrWhiteSpace(desktopOverride))
                return Path.GetFullPath(desktopOverride);

            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (!string.IsNullOrWhiteSpace(desktop))
                return desktop;

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop");
        }

        private static List<string> BuildSurpriseHighlights(KokoOverlordSnapshot snapshot)
        {
            var lines = new List<string>();

            foreach (var file in snapshot.Files
                         .Where(f => !string.Equals(f.Signal, "indexed", StringComparison.OrdinalIgnoreCase))
                         .OrderByDescending(f => f.SizeBytes)
                         .Take(5))
            {
                lines.Add($"{file.Signal}: {file.Name} ({FormatBytes(file.SizeBytes)})");
            }

            foreach (var file in snapshot.Files
                         .OrderByDescending(f => f.ModifiedAt)
                         .Take(5))
            {
                if (lines.Any(l => l.Contains(file.Name, StringComparison.OrdinalIgnoreCase)))
                    continue;
                lines.Add($"recent: {file.Name} ({file.Bucket}, {file.ModifiedAt:yyyy-MM-dd HH:mm})");
            }

            foreach (var proc in snapshot.Processes
                         .Where(p => p.WorkingSetMb >= 500 || !string.IsNullOrWhiteSpace(p.WindowTitle))
                         .Take(4))
            {
                var title = string.IsNullOrWhiteSpace(proc.WindowTitle) ? "" : " / " + Trim(proc.WindowTitle, 55);
                lines.Add($"process: {proc.ProcessName} {proc.WorkingSetMb:0}MB{title}");
            }

            return lines.Distinct(StringComparer.OrdinalIgnoreCase).Take(10).ToList();
        }

        private static string BuildSurpriseNote(
            string userRequest,
            KokoOverlordSnapshot snapshot,
            IReadOnlyList<string> highlights)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Kokonoe local scan artifact");
            sb.AppendLine("Created: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("Request: " + Trim(userRequest, 240));
            sb.AppendLine();
            sb.AppendLine($"Scanned files: {snapshot.ScannedFiles}");
            sb.AppendLine($"Roots: {string.Join("; ", snapshot.Roots.Take(8))}");
            sb.AppendLine($"Signals: {snapshot.Files.Count(f => !string.Equals(f.Signal, "indexed", StringComparison.OrdinalIgnoreCase))}");
            sb.AppendLine();
            sb.AppendLine("Highlights:");
            if (highlights.Count == 0)
            {
                sb.AppendLine("- No dramatic cleanup target found. Boring, but at least real.");
            }
            else
            {
                foreach (var item in highlights)
                    sb.AppendLine("- " + item);
            }

            sb.AppendLine();
            sb.AppendLine("Concrete paths sampled:");
            foreach (var file in snapshot.Files
                         .OrderByDescending(f => f.ModifiedAt)
                         .Take(12))
            {
                sb.AppendLine($"- [{file.Bucket}/{file.Signal}] {file.Path}");
            }

            if (snapshot.Proposals.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Non-destructive proposals:");
                foreach (var proposal in snapshot.Proposals.Take(5))
                    sb.AppendLine($"- {proposal.Title}: {proposal.Reason}");
            }

            sb.AppendLine();
            sb.AppendLine("No delete/move was executed. I wrote only this note, because losing files for drama is idiot behavior.");
            return sb.ToString();
        }

        private static KokoOverlordFileFact? BuildFileFact(string path)
        {
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists)
                    return null;

                var ext = info.Extension.ToLowerInvariant();
                var bucket = ext switch
                {
                    ".png" or ".jpg" or ".jpeg" or ".webp" or ".gif" => "image",
                    ".mp4" or ".mkv" or ".mov" or ".webm" => "video",
                    ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "archive",
                    ".pdf" or ".docx" or ".doc" or ".txt" or ".md" => "document",
                    ".exe" or ".msi" or ".apk" => "installer",
                    ".cs" or ".kt" or ".xaml" or ".json" or ".ps1" => "code",
                    _ => string.IsNullOrWhiteSpace(ext) ? "unknown" : ext.TrimStart('.')
                };

                var age = DateTime.Now - info.LastWriteTime;
                var signal =
                    IsDownloadsPath(path) && age > TimeSpan.FromDays(30) ? "stale_download" :
                    bucket == "archive" && info.Length > 200L * 1024 * 1024 ? "large_archive" :
                    bucket == "installer" && age > TimeSpan.FromDays(14) ? "old_installer" :
                    info.Length > 1_000L * 1024 * 1024 ? "very_large" :
                    "indexed";

                return new KokoOverlordFileFact
                {
                    Path = info.FullName,
                    Name = info.Name,
                    Extension = ext,
                    SizeBytes = info.Length,
                    CreatedAt = info.CreationTime,
                    ModifiedAt = info.LastWriteTime,
                    Bucket = bucket,
                    Signal = signal
                };
            }
            catch
            {
                return null;
            }
        }

        private static IEnumerable<string> EnumerateFilesSafe(string root, int max)
        {
            var queue = new Queue<string>();
            queue.Enqueue(root);
            var count = 0;
            while (queue.Count > 0 && count < max)
            {
                var dir = queue.Dequeue();
                string[] files;
                try { files = Directory.GetFiles(dir); }
                catch { continue; }

                foreach (var file in files)
                {
                    yield return file;
                    if (++count >= max)
                        yield break;
                }

                string[] dirs;
                try { dirs = Directory.GetDirectories(dir); }
                catch { continue; }
                foreach (var child in dirs)
                {
                    var name = Path.GetFileName(child);
                    if (name is ".git" or "node_modules" or "bin" or "obj" or ".vs")
                        continue;
                    queue.Enqueue(child);
                }
            }
        }

        private static IEnumerable<KokoOverlordProcessFact> GetProcessFacts(int count)
        {
            foreach (var proc in Process.GetProcesses().OrderByDescending(p => SafeWorkingSet(p)).Take(count))
            {
                KokoOverlordProcessFact? fact = null;
                try
                {
                    fact = new KokoOverlordProcessFact
                    {
                        ProcessName = proc.ProcessName,
                        ProcessId = proc.Id,
                        WorkingSetMb = proc.WorkingSet64 / 1024.0 / 1024.0,
                        WindowTitle = proc.MainWindowTitle ?? ""
                    };
                }
                catch (Exception suppressedEx584) { KokoSystemLog.Write("SYSTEMOVERLORDSERVICE-CATCH", "GetProcessFacts failed near source line 584: " + suppressedEx584); }
                finally { try { proc.Dispose(); } catch (Exception suppressedEx585) { KokoSystemLog.Write("SYSTEMOVERLORDSERVICE-CATCH", "GetProcessFacts failed near source line 585: " + suppressedEx585); } }
                if (fact != null)
                    yield return fact;
            }
        }

        private void SaveSnapshot(KokoOverlordSnapshot snapshot)
        {
            lock (_lock)
            {
                _last = Clone(snapshot);
                File.WriteAllText(_indexPath, JsonConvert.SerializeObject(_last, Formatting.Indented), Encoding.UTF8);
            }
        }

        private void LoadLast()
        {
            try
            {
                if (!File.Exists(_indexPath))
                    return;
                var loaded = JsonConvert.DeserializeObject<KokoOverlordSnapshot>(File.ReadAllText(_indexPath, Encoding.UTF8));
                if (loaded != null)
                    _last = loaded;
            }
            catch (Exception suppressedEx610) { KokoSystemLog.Write("SYSTEMOVERLORDSERVICE-CATCH", "LoadLast failed near source line 610: " + suppressedEx610); }
        }

        private void PublishSnapshot(KokoOverlordSnapshot snapshot)
        {
            var summary = $"indexed {snapshot.ScannedFiles} files across {snapshot.Roots.Count} root(s); proposals={snapshot.Proposals.Count}; bytes={FormatBytes(snapshot.TotalBytes)}";
            _blackboard?.Publish("system-overlord", "index", summary, 0.64, new
            {
                snapshot.ScannedFiles,
                snapshot.TotalBytes,
                Proposals = snapshot.Proposals.Count
            });
            _heartbeat?.Update("SYSTEM_OVERLORD", snapshot.Status, summary);
            KokoSystemLog.Write("OVERLORD", summary);
        }

        private static List<string> ResolveRoots(IEnumerable<string> roots)
        {
            var list = roots
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => Environment.ExpandEnvironmentVariables(r.Trim()))
                .Select(r =>
                {
                    try { return Path.GetFullPath(r); }
                    catch { return ""; }
                })
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToList();

            if (list.Count > 0)
                return list;

            var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            return new[] { downloads, pictures }
                .Where(Directory.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static IEnumerable<string> SplitRoots(string? roots)
            => (roots ?? "")
                .Split(new[] { '\r', '\n', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        private static bool IsDownloadsPath(string path)
            => path.Contains($"{Path.DirectorySeparatorChar}Downloads{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
               path.Contains($"{Path.AltDirectorySeparatorChar}Downloads{Path.AltDirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));

        private static long SafeWorkingSet(Process p)
        {
            try { return p.WorkingSet64; }
            catch { return 0; }
        }

        private static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = bytes;
            var unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }
            return $"{value:0.#}{units[unit]}";
        }

        private static string Trim(string? text, int max)
        {
            text = (text ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= max ? text : text[..max].TrimEnd() + "...";
        }

        private static KokoOverlordSnapshot Clone(KokoOverlordSnapshot item)
            => JsonConvert.DeserializeObject<KokoOverlordSnapshot>(JsonConvert.SerializeObject(item)) ?? item;
    }
}
