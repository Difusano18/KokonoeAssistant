using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace KokonoeAssistant.Services
{
    /// <summary>
    /// Obsidian MCP-style tools — повний набір для роботи з vault
    /// </summary>
    public class ObsidianMcpService
    {
        private readonly string _vault;
        public string VaultPath => _vault;

        public ObsidianMcpService(string vaultPath)
        {
            _vault = Path.GetFullPath(vaultPath);
        }

        // ── LIST ─────────────────────────────────────────────────

        public List<string> ListNotes(string? subfolder = null)
        {
            var root = subfolder != null ? Path.Combine(_vault, subfolder) : _vault;
            if (!Directory.Exists(root)) return new();
            return SafeGetFiles(root)
                .Select(f => Path.GetRelativePath(_vault, f).Replace('\\', '/'))
                .OrderBy(f => f)
                .ToList();
        }

        public List<string> ListFolders()
        {
            return SafeGetDirectories(_vault)
                .Select(d => Path.GetRelativePath(_vault, d).Replace('\\', '/'))
                .Where(d => !d.StartsWith(".") && !d.Contains("kokonoe-data"))
                .OrderBy(d => d)
                .ToList();
        }

        // ── READ ─────────────────────────────────────────────────

        public string? ReadNote(string path)
        {
            var full = Resolve(path);
            return File.Exists(full) ? File.ReadAllText(full, Encoding.UTF8) : null;
        }

        // ── WRITE / CREATE ────────────────────────────────────────

        public string WriteNote(string path, string content)
        {
            var full = Resolve(path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            content = NormalizeFrontmatter(content);
            File.WriteAllText(full, content, Encoding.UTF8);
            return full;
        }

        public string CreateNote(string title, string content = "", string? folder = null, string[]? tags = null)
        {
            var safe = string.Concat(title.Split(Path.GetInvalidFileNameChars())).Trim();
            var rel = folder != null ? $"{folder}/{safe}.md" : $"{safe}.md";

            var tagsLine = tags?.Length > 0
                ? string.Join(", ", tags.Select(t => t.StartsWith('#') ? t : '#' + t))
                : "";

            var body = $"""
---
date: {DateTime.Now:yyyy-MM-dd}
created: {DateTime.Now:yyyy-MM-dd HH:mm}
tags: [{SanitizeTagsLine(tagsLine)}]
---

# {title}

{content}
""";
            return WriteNote(rel, body);
        }

        public string AppendToNote(string path, string content)
        {
            var full = Resolve(path);
            if (!File.Exists(full)) return "Нотатка не знайдена: " + path;
            File.AppendAllText(full, "\n" + content, Encoding.UTF8);
            return full;
        }

        public static string NormalizeFrontmatter(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return content;

            if (!content.StartsWith("---", StringComparison.Ordinal))
            {
                var firstLineEnd = content.IndexOf('\n');
                var firstLine = firstLineEnd >= 0 ? content[..firstLineEnd].TrimEnd('\r') : content.TrimEnd('\r');
                if (firstLine.EndsWith("---", StringComparison.Ordinal) && firstLine.Length > 3)
                {
                    content = "---" + (firstLineEnd >= 0 ? content[firstLineEnd..] : "");
                }
            }

            if (!content.StartsWith("---", StringComparison.Ordinal)) return content;

            var end = content.IndexOf("---", 3, StringComparison.Ordinal);
            if (end <= 0) return content;

            var front = content[3..end];
            var body = content[(end + 3)..];
            var lines = front.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
            var normalized = new List<string>();

            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (line.StartsWith("tags:", StringComparison.OrdinalIgnoreCase))
                {
                    var value = line[(line.IndexOf(':') + 1)..].Trim();
                    normalized.Add($"tags: [{SanitizeTagsLine(value)}]");
                    continue;
                }

                if (line.StartsWith("date:", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("created:", StringComparison.OrdinalIgnoreCase))
                {
                    var colon = line.IndexOf(':');
                    var key = line[..colon].Trim();
                    var value = NormalizeYamlDateLike(line[(colon + 1)..].Trim());
                    normalized.Add($"{key}: {value}");
                    continue;
                }

                normalized.Add(NormalizeYamlInlineWikiLinks(line));
            }

            return "---\n" + string.Join("\n", normalized) + "\n---" + body;
        }

        private static string SanitizeTagsLine(string value)
        {
            value = (value ?? "")
                .Replace("[[", "", StringComparison.Ordinal)
                .Replace("]]", "", StringComparison.Ordinal)
                .Replace("[", "", StringComparison.Ordinal)
                .Replace("]", "", StringComparison.Ordinal)
                .Replace("\"", "", StringComparison.Ordinal);

            var tags = value
                .Split(new[] { ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim().TrimStart('#').Trim())
                .Select(t => System.Text.RegularExpressions.Regex.Replace(t, @"[^\p{L}\p{N}_/-]", ""))
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return string.Join(", ", tags);
        }

        private static string NormalizeYamlDateLike(string value)
        {
            value = NormalizeYamlInlineWikiLinks(value);
            var match = System.Text.RegularExpressions.Regex.Match(
                value,
                @"^(?<date>\d{4}-\d{2}-\d{2})(?<time>\s+\d{1,2}:\d{2})?$");
            if (!match.Success) return value;
            return match.Groups["date"].Value + (match.Groups["time"].Success ? match.Groups["time"].Value : "");
        }

        private static string NormalizeYamlInlineWikiLinks(string value)
            => System.Text.RegularExpressions.Regex.Replace(value ?? "", @"\[\[([^\]]+)\]\]", "$1").Trim();

        public int AppendUniqueItemsToNote(string path, string header, IEnumerable<string> items, string label, double duplicateThreshold = 0.82)
        {
            var cleanItems = items
                .Select(i => i.Trim())
                .Where(i => !string.IsNullOrWhiteSpace(i))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (cleanItems.Count == 0) return 0;

            var full = Resolve(path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);

            var existing = File.Exists(full) ? File.ReadAllText(full, Encoding.UTF8) : "";
            var existingItems = ExtractMarkdownListItems(existing)
                .Select(NormalizeMemoryText)
                .Where(i => i.Length > 0)
                .ToList();

            var accepted = new List<string>();
            foreach (var item in cleanItems)
            {
                var normalized = NormalizeMemoryText(item);
                if (normalized.Length == 0) continue;

                var duplicate = existingItems.Any(e => e == normalized || TextSimilarity(e, normalized) >= duplicateThreshold) ||
                                accepted.Select(NormalizeMemoryText).Any(e => e == normalized || TextSimilarity(e, normalized) >= duplicateThreshold);
                if (!duplicate)
                    accepted.Add(item);
            }

            if (accepted.Count == 0)
                return 0;

            var sb = new StringBuilder();
            if (!File.Exists(full))
                sb.AppendLine(header.TrimEnd());
            sb.AppendLine($"\n## {DateTime.Now:yyyy-MM-dd HH:mm}");
            foreach (var item in accepted)
                sb.AppendLine($"- [{label}] {item}");

            File.AppendAllText(full, sb.ToString(), Encoding.UTF8);
            return accepted.Count;
        }

        // ── DELETE ────────────────────────────────────────────────

        public string DeleteNote(string path)
        {
            var full = Resolve(path);
            if (!File.Exists(full)) return "Не знайдено";
            File.Delete(full);
            return "Видалено: " + path;
        }

        // ── MOVE / RENAME / FOLDER ────────────────────────────────

        public string MoveNote(string oldPath, string newPath)
        {
            var oldFull = Resolve(oldPath);
            if (!File.Exists(oldFull)) return $"Не знайдено: {oldPath}";
            var newFull = Resolve(newPath);
            Directory.CreateDirectory(Path.GetDirectoryName(newFull)!);
            File.Move(oldFull, newFull, overwrite: false);
            return $"Переміщено: {oldPath} → {newPath}";
        }

        public string CreateFolder(string folderPath)
        {
            var full = Path.Combine(_vault, folderPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(full);
            return $"Папка створена: {folderPath}";
        }

        public string GetVaultTree(int maxDepth = 3)
        {
            var sb = new System.Text.StringBuilder();
            BuildTree(sb, _vault, _vault, 0, maxDepth);
            return sb.ToString();
        }

        private void BuildTree(System.Text.StringBuilder sb, string root, string current, int depth, int maxDepth)
        {
            if (depth > maxDepth) return;
            var indent = new string(' ', depth * 2);
            var name = Path.GetRelativePath(root, current);
            if (name == ".") name = Path.GetFileName(root);

            // skip hidden / data dirs
            var dirName = Path.GetFileName(current);
            if (dirName.StartsWith('.') || dirName == "kokonoe-data") return;

            if (depth > 0) sb.AppendLine($"{indent}📁 {dirName}/");

            foreach (var f in Directory.GetFiles(current, "*.md").OrderBy(x => x))
                sb.AppendLine($"{indent}  📄 {Path.GetFileNameWithoutExtension(f)}");

            foreach (var d in Directory.GetDirectories(current).OrderBy(x => x))
                BuildTree(sb, root, d, depth + 1, maxDepth);
        }

        // ── SEARCH ────────────────────────────────────────────────

        public List<SearchResult> SearchNotes(string query, int max = 10)
        {
            var results = new List<SearchResult>();
            var words = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            foreach (var file in SafeGetFiles(_vault))
            {
                try
                {
                    var raw = File.ReadAllText(file, Encoding.UTF8);
                    var lower = raw.ToLower();
                    var score = words.Sum(w => CountOccurrences(lower, w));
                    if (score == 0) continue;

                    var idx = lower.IndexOf(words[0]);
                    var preview = idx >= 0
                        ? raw.Substring(Math.Max(0, idx - 20), Math.Min(150, raw.Length - Math.Max(0, idx - 20)))
                        : raw.Substring(0, Math.Min(100, raw.Length));

                    results.Add(new SearchResult
                    {
                        Path = Path.GetRelativePath(_vault, file).Replace('\\', '/'),
                        Title = Path.GetFileNameWithoutExtension(file),
                        Preview = preview.Trim().Replace('\n', ' '),
                        Score = score
                    });
                }
                catch (Exception ex) { Debug.WriteLine($"[ObsidianMcp] SearchNotes failed for {file}: {ex.Message}"); }
            }

            return results.OrderByDescending(r => r.Score).Take(max).ToList();
        }

        private static int CountOccurrences(string text, string word)
        {
            int count = 0, idx = 0;
            while ((idx = text.IndexOf(word, idx)) >= 0) { count++; idx += word.Length; }
            return count;
        }

        // ── DAILY NOTE ────────────────────────────────────────────

        public string GetOrCreateDailyNote()
        {
            var today = DateTime.Today.ToString("yyyy-MM-dd");
            var rel = $"Daily/{today}.md";
            var full = Resolve(rel);

            if (!File.Exists(full))
                CreateNote(today, "", "Daily", new[] { "daily" });

            return ReadNote(rel) ?? "";
        }

        public string AppendToDailyNote(string content)
        {
            var today = DateTime.Today.ToString("yyyy-MM-dd");
            var rel = $"Daily/{today}.md";
            var full = Resolve(rel);

            if (!File.Exists(full))
                CreateNote(today, "", "Daily", new[] { "daily" });

            return AppendToNote(rel, content);
        }

        // ── VAULT STATUS / HEALTH ─────────────────────────────────

        /// <summary>
        /// Повертає стан vault: порожні нотатки, осиротілі (без посилань), загальна статистика.
        /// </summary>
        public VaultStatus GetVaultStatus()
        {
            var allFiles = SafeGetFiles(_vault)
                .Where(f => !f.Contains("kokonoe-data"))
                .ToList();

            var index = GetNoteIndex();
            var emptyNotes   = new List<string>();
            var orphanNotes  = new List<string>();
            var filledNotes  = new List<string>();

            foreach (var file in allFiles)
            {
                var rel     = Path.GetRelativePath(_vault, file).Replace('\\', '/');
                var content = File.ReadAllText(file, Encoding.UTF8).Trim();
                if (IsKokonoeManagedNote(content))
                    continue;

                // "Порожня" = тільки frontmatter або взагалі нічого
                var bodyOnly = System.Text.RegularExpressions.Regex.Replace(
                    content, @"^---[\s\S]*?---\s*", "").Trim();
                var hasHeader = System.Text.RegularExpressions.Regex.IsMatch(bodyOnly, @"^#+\s+\S");
                var hasContent = bodyOnly.Length > 5 &&
                    !(hasHeader && System.Text.RegularExpressions.Regex.Replace(bodyOnly, @"^#+.+", "").Trim().Length < 3);

                if (!hasContent)
                    emptyNotes.Add(rel);
                else
                    filledNotes.Add(rel);
            }

            // Orphan = нотатка без жодного incoming або outgoing [[link]]
            foreach (var rel in filledNotes)
            {
                var outgoing  = GetOutgoingLinks(rel);
                var backlinks = GetBacklinks(rel);
                if (outgoing.Count == 0 && backlinks.Count == 0)
                    orphanNotes.Add(rel);
            }

            return new VaultStatus
            {
                TotalNotes   = filledNotes.Count + emptyNotes.Count,
                EmptyNotes   = emptyNotes,
                OrphanNotes  = orphanNotes,
                FilledNotes  = filledNotes.Count
            };
        }

        public VaultDoctorReport RunVaultDoctor(bool repair = false)
        {
            var report = new VaultDoctorReport { RanAt = DateTime.Now, RepairApplied = repair };
            var files = SafeGetFiles(_vault).Where(f => !f.Contains("kokonoe-data")).ToList();
            var noteTargets = BuildWikiTargetIndex(files);
            var utf8 = new UTF8Encoding(false);

            foreach (var file in files)
            {
                var rel = Path.GetRelativePath(_vault, file).Replace('\\', '/');
                string raw;
                try { raw = File.ReadAllText(file, Encoding.UTF8); }
                catch { continue; }

                var bodyOnly = System.Text.RegularExpressions.Regex.Replace(
                    raw.Trim(), @"^---[\s\S]*?---\s*", "").Trim();
                if (string.IsNullOrWhiteSpace(bodyOnly))
                    report.EmptyMarkdownFiles.Add(rel);

                var folderLinks = System.Text.RegularExpressions.Regex.Matches(raw, @"\[\[([^\]|#]+/)\]\]").Count;
                if (folderLinks > 0)
                    report.FolderWikiLinks[rel] = folderLinks;

                var actorLinks = System.Text.RegularExpressions.Regex.Matches(
                    raw, @"\[\[Kokonoe(?:\|[^\]]+)?\]\]", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
                if (actorLinks > 0)
                    report.SuppressedActorLinks[rel] = actorLinks;

                if (LooksLikeMojibake(raw))
                    report.MojibakeSuspects.Add(rel);

                var normalized = NormalizeFrontmatter(raw);
                if (normalized != raw && HasFrontmatterShape(raw))
                    report.FrontmatterIssues.Add(rel);

                foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(raw, @"\[\[([^\]|#]+)"))
                {
                    var target = match.Groups[1].Value.Trim();
                    if (string.IsNullOrWhiteSpace(target) || target.EndsWith("/", StringComparison.Ordinal))
                        continue;
                    if (!noteTargets.Contains(NormalizeWikiTarget(target)))
                        report.MissingWikiTargets[target] = report.MissingWikiTargets.TryGetValue(target, out var c) ? c + 1 : 1;
                }

                if (!repair)
                    continue;

                var repaired = normalized;
                repaired = System.Text.RegularExpressions.Regex.Replace(
                    repaired,
                    @"\[\[Kokonoe(?:\|([^\]]+))?\]\]",
                    m => m.Groups[1].Success ? m.Groups[1].Value : "Kokonoe",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                repaired = System.Text.RegularExpressions.Regex.Replace(
                    repaired,
                    @"\[\[([^\]|#]+/)\]\]",
                    m => $"`{m.Groups[1].Value}`");

                if (repaired != raw)
                {
                    File.WriteAllText(file, repaired, utf8);
                    report.RepairedFiles.Add(rel);
                }
            }

            if (repair)
                RepairEmptyMarkdownFiles(report, utf8);

            return report;
        }

        /// <summary>
        /// Видаляє порожні нотатки (без реального контенту), повертає список видалених.
        /// Захищає нотатки автоматично за метаданими — без hardcoded назв:
        /// - frontmatter type: brain-core або type: index → захищено
        /// - 3+ backlinks → важливий вузол, захищено
        /// - Daily/ папка → захищено
        /// </summary>
        public List<string> CleanupEmptyNotes(bool dryRun = false)
        {
            var status  = GetVaultStatus();
            var deleted = new List<string>();

            foreach (var rel in status.EmptyNotes)
            {
                // 1. Daily/ завжди захищена
                if (rel.StartsWith("Daily/", StringComparison.OrdinalIgnoreCase))
                    continue;

                // 2. Нотатки з type: brain-core або type: index — захищені
                try
                {
                    var content = ReadNote(rel) ?? "";
                    if (System.Text.RegularExpressions.Regex.IsMatch(
                            content, @"type:\s*(brain-core|index)",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        continue;
                }
                catch (Exception ex) { Debug.WriteLine($"[ObsidianMcp] CleanupEmptyNotes read failed: {ex.Message}"); continue; }

                // 3. Нотатки з 3+ backlinks — важливий вузол, не чіпати
                try
                {
                    if (GetBacklinks(rel).Count >= 3)
                        continue;
                }
                catch (Exception ex) { Debug.WriteLine($"[ObsidianMcp] GetBacklinks failed for {rel}: {ex.Message}"); }

                if (!dryRun)
                {
                    try { File.Delete(Resolve(rel)); } catch (Exception ex) { Debug.WriteLine($"[ObsidianMcp] File.Delete failed for {rel}: {ex.Message}"); }
                }
                deleted.Add(rel);
            }

            return deleted;
        }

        // ── BRAIN VAULT INITIALIZATION ────────────────────────────

        /// <summary>
        /// Перевіряє стан vault і повертає статус ініціалізації.
        /// НЕ створює hardcoded нотатки — LLM сама вирішує що і як назвати.
        /// </summary>
        private HashSet<string> BuildWikiTargetIndex(List<string> files)
        {
            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                var rel = Path.GetRelativePath(_vault, file).Replace('\\', '/');
                var withoutExt = Path.ChangeExtension(rel, null)?.Replace('\\', '/') ?? rel;
                var title = Path.GetFileNameWithoutExtension(file);
                targets.Add(NormalizeWikiTarget(withoutExt));
                targets.Add(NormalizeWikiTarget(title));
            }
            return targets;
        }

        private static string NormalizeWikiTarget(string target)
        {
            target = target.Trim().Replace('\\', '/');
            if (target.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                target = target[..^3];
            return target.TrimEnd('/').ToLowerInvariant();
        }

        private static bool HasFrontmatterShape(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return false;
            if (raw.StartsWith("---", StringComparison.Ordinal)) return true;
            var idx = raw.IndexOf("---", StringComparison.Ordinal);
            return idx > 0 && idx < 8;
        }

        private static bool LooksLikeMojibake(string raw)
            => System.Text.RegularExpressions.Regex.IsMatch(raw, @"(Рџ|РЅ|Р°|Рё|СЏ|СЊ|С–|Сѓ|С‚|СЃ|вЂ|Â|Ð|Ñ|�)");

        private static string SanitizeTagValue(string value)
        {
            value = (value ?? "").Trim().ToLowerInvariant();
            value = System.Text.RegularExpressions.Regex.Replace(value, @"[^\p{L}\p{Nd}/_-]+", "-");
            value = value.Trim('-', '/', '_');
            return string.IsNullOrWhiteSpace(value) ? "index" : value;
        }

        private void RepairEmptyMarkdownFiles(VaultDoctorReport report, Encoding encoding)
        {
            foreach (var rel in report.EmptyMarkdownFiles.ToList())
            {
                var full = Resolve(rel);
                if (!File.Exists(full)) continue;
                try
                {
                    if (new FileInfo(full).Length > 0)
                        continue;

                    if (string.Equals(rel, "Kokonoe.md", StringComparison.OrdinalIgnoreCase))
                    {
                        File.Delete(full);
                        report.DeletedEmptyFiles.Add(rel);
                        continue;
                    }

                    var title = Path.GetFileNameWithoutExtension(rel);
                    var tag = SanitizeTagValue(title).ToLowerInvariant();
                    var content = $"""
---
type: index
tags: [{tag}, index]
created: {DateTime.Now:yyyy-MM-dd}
---

# {title}

Індекс-нотатка створена автоматично, бо на цей вузол уже посилався vault. Порожні вузли на графі залишимо для людей, які люблять дивитись у чорну діру.
""";
                    File.WriteAllText(full, content, encoding);
                    report.RepairedFiles.Add(rel);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ObsidianMcp] RepairEmptyMarkdownFiles failed for {rel}: {ex.Message}");
                }
            }
        }

        public VaultInitStatus GetVaultInitStatus()
        {
            var allNotes = ListNotes();
            var noteCount = allNotes.Count;

            // Шукаємо нотатки з type: brain-core в frontmatter
            var hasCoreNote = false;
            var coreNotePath = "";
            foreach (var rel in allNotes)
            {
                try
                {
                    var content = ReadNote(rel) ?? "";
                    if (System.Text.RegularExpressions.Regex.IsMatch(
                            content, @"type:\s*brain-core",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    {
                        hasCoreNote   = true;
                        coreNotePath  = rel;
                        break;
                    }
                }
                catch (Exception ex) { Debug.WriteLine($"[ObsidianMcp] Brain-core check failed for {rel}: {ex.Message}"); }
            }

            // Рахуємо загальну кількість [[links]] в vault
            var totalLinks = 0;
            foreach (var rel in allNotes)
            {
                try { totalLinks += GetOutgoingLinks(rel).Count; }
                catch (Exception ex) { Debug.WriteLine($"[ObsidianMcp] GetOutgoingLinks failed for {rel}: {ex.Message}"); }
            }

            var isEmpty = noteCount < 3;
            var isSparslyLinked = noteCount > 0 && totalLinks < noteCount;

            string suggestedAction;
            if (isEmpty)
                suggestedAction = "Vault порожній. Створи свої ключові нотатки: центральна нотатка мозку (type: brain-core), профіль творця, щоденник. Назви на свій розсуд.";
            else if (!hasCoreNote)
                suggestedAction = "Немає центральної нотатки мозку. Створи одну з frontmatter: type: brain-core — це буде твій хаб.";
            else if (isSparslyLinked)
                suggestedAction = $"Мало зв'язків ({totalLinks} на {noteCount} нотаток). Виклич rebuild_links або додай [[посилання]] вручну.";
            else
                suggestedAction = "Vault здоровий.";

            return new VaultInitStatus
            {
                NoteCount    = noteCount,
                TotalLinks   = totalLinks,
                IsEmpty      = isEmpty,
                HasCoreNote  = hasCoreNote,
                CoreNotePath = coreNotePath,
                SuggestedAction = suggestedAction
            };
        }

        // Зворотна сумісність — тепер просто повертає статус у вигляді рядка
        public List<string> InitBrainVault()
        {
            var status = GetVaultInitStatus();
            var result = new List<string> { status.ToString() };

            // Якщо vault зовсім порожній — тільки rebuild_links після першого запуску
            if (!status.IsEmpty && status.TotalLinks < status.NoteCount)
                RebuildLinks();

            return result;
        }

        // ── TOOLS FOR LLM ─────────────────────────────────────────

        public VaultMaintenanceResult MaintainKokonoeVaultArchitecture(string reason = "manual")
        {
            var result = new VaultMaintenanceResult { Reason = reason, RanAt = DateTime.Now };

            foreach (var folder in new[]
            {
                "Kokonoe",
                "Kokonoe/Architecture",
                "Kokonoe/Memory",
                "Kokonoe/Automation",
                "Kokonoe/Reviews",
                "Kokonoe/Logs",
                "Daily",
                "Chats"
            })
            {
                var full = Path.Combine(_vault, folder.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(full))
                {
                    Directory.CreateDirectory(full);
                    result.CreatedFolders.Add(folder);
                }
            }

            var notesBefore = ListNotes();
            var doctor = RunVaultDoctor(repair: true);
            var status = GetVaultStatus();
            var init = GetVaultInitStatus();
            var modifiedToday = GetNotesModifiedToday()
                .Where(n => !IsKokonoeManagedPath(n))
                .Take(20)
                .ToList();
            var folders = ListFolders();
            var graph = GetNoteGraph();
            var isolated = GetIsolatedNotes().Take(30).ToList();
            var memoryQuality = AnalyzeMemoryQuality();
            var taskQueue = BuildTaskQueue();
            var memoryReview = BuildMemoryReview(memoryQuality, taskQueue);

            UpsertManagedNote("Kokonoe/Vault Index.md", BuildVaultIndex(status, init, folders, modifiedToday), result);
            UpsertManagedNote("Kokonoe/Architecture/Manifest.md", BuildVaultManifest(notesBefore, folders), result);
            UpsertManagedNote("Kokonoe/Architecture/Map.md", BuildVaultMap(graph), result);
            UpsertManagedNote("Kokonoe/Architecture/Health.md", BuildVaultHealth(status, init, isolated, doctor), result);
            UpsertManagedNote("Kokonoe/Architecture/Backlog.md", BuildVaultBacklog(status, init, isolated), result);
            UpsertManagedNote("Kokonoe/Architecture/Language Policy.md", BuildVaultLanguagePolicy(), result);
            UpsertManagedNote("Kokonoe/Memory/Quality.md", BuildMemoryQualityNote(memoryQuality), result);
            UpsertManagedNote("Kokonoe/Memory/Review.md", BuildMemoryReviewNote(memoryReview), result);
            UpsertManagedNote("Kokonoe/Tasks Queue.md", BuildTaskQueueNote(taskQueue), result);
            UpsertManagedNote("Kokonoe/Automation/Obsidian Sync.md", BuildVaultAutomationNote(), result);

            var linkResult = RebuildLinks();
            result.LinkTouchedNotes = linkResult.changed;
            result.LinksAdded = linkResult.linksAdded;
            result.MemoryDuplicateGroups = memoryQuality.DuplicateGroups.Count;
            result.OpenTaskCount = taskQueue.OpenTasks.Count;
            result.MemoryReviewActionCount = memoryReview.Actions.Count;
            AppendMaintenanceLog(result, status, init);
            return result;
        }

        private void UpsertManagedNote(string path, string content, VaultMaintenanceResult result)
        {
            var full = Resolve(path);
            var existed = File.Exists(full);
            var current = existed ? File.ReadAllText(full, Encoding.UTF8) : null;
            if (current == content || (current != null && NormalizeManagedContent(current) == NormalizeManagedContent(content)))
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content, Encoding.UTF8);

            if (existed)
                result.UpdatedNotes.Add(path);
            else
                result.CreatedNotes.Add(path);
        }

        private static string NormalizeManagedContent(string content)
        {
            return System.Text.RegularExpressions.Regex.Replace(
                content,
                @"^updated:\s*.*$",
                "updated: <normalized>",
                System.Text.RegularExpressions.RegexOptions.Multiline);
        }

        private static bool IsKokonoeManagedNote(string content)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(
                content,
                @"^managed-by:\s*Kokonoe\s*$",
                System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        private static bool IsKokonoeManagedPath(string path)
        {
            return path.StartsWith("Kokonoe/Architecture/", StringComparison.OrdinalIgnoreCase) ||
                   path.Equals("Kokonoe/Vault Index.md", StringComparison.OrdinalIgnoreCase) ||
                   path.Equals("Kokonoe/Tasks Queue.md", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("Kokonoe/Memory/Quality", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("Kokonoe/Memory/Cleanup", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("Kokonoe/Memory/Review", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("Kokonoe/Automation/", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildManagedFrontmatter(string type)
        {
            return $"""
---
type: {type}
managed-by: Kokonoe
updated: {DateTime.Now:yyyy-MM-dd HH:mm}
tags: [kokonoe, vault, architecture]
---

""";
        }

        private string BuildVaultIndex(VaultStatus status, VaultInitStatus init, List<string> folders, List<string> modifiedToday)
        {
            var sb = new StringBuilder();
            sb.Append(BuildManagedFrontmatter("vault-index"));
            sb.AppendLine("# Індекс vault Коконое");
            sb.AppendLine();
            sb.AppendLine("## Ядро");
            sb.AppendLine("- [[Kokonoe/Architecture/Manifest|Маніфест]]");
            sb.AppendLine("- [[Kokonoe/Architecture/Map|Карта]]");
            sb.AppendLine("- [[Kokonoe/Architecture/Health|Стан]]");
            sb.AppendLine("- [[Kokonoe/Architecture/Backlog|Беклог]]");
            sb.AppendLine("- [[Kokonoe/Automation/Obsidian Sync|Синхронізація Obsidian]]");
            sb.AppendLine("- [[Kokonoe/AutoMemory|Автопам'ять]]");
            sb.AppendLine("- [[Kokonoe/Project Log|Журнал проекту]]");
            sb.AppendLine("- [[Kokonoe/Memory/Facts|Факти]]");
            sb.AppendLine("- [[Kokonoe/Memory/Quality|Якість пам'яті]]");
            sb.AppendLine("- [[Kokonoe/Memory/Cleanup|Очищення пам'яті]]");
            sb.AppendLine("- [[Kokonoe/Memory/Review|Огляд пам'яті]]");
            sb.AppendLine("- [[Kokonoe/Tasks Queue|Черга задач]]");
            sb.AppendLine();
            sb.AppendLine("## Стан");
            sb.AppendLine($"- Нотаток: {status.TotalNotes}");
            sb.AppendLine($"- Заповнених нотаток: {status.FilledNotes}");
            sb.AppendLine($"- Порожніх нотаток: {status.EmptyNotes.Count}");
            sb.AppendLine($"- Осиротілих нотаток: {status.OrphanNotes.Count}");
            sb.AppendLine($"- Ядро мозку: {(init.HasCoreNote ? init.CoreNotePath : "відсутнє")}");
            sb.AppendLine();
            sb.AppendLine("## Основні папки");
            foreach (var folder in folders.Take(40))
                sb.AppendLine($"- `{folder}/`");
            sb.AppendLine();
            sb.AppendLine("## Змінено сьогодні");
            foreach (var note in modifiedToday)
                sb.AppendLine($"- [[{Path.GetFileNameWithoutExtension(note)}]] ({note})");
            return sb.ToString();
        }

        private string BuildVaultManifest(List<string> notes, List<string> folders)
        {
            var sb = new StringBuilder();
            sb.Append(BuildManagedFrontmatter("vault-manifest"));
            sb.AppendLine("# Маніфест vault");
            sb.AppendLine();
            sb.AppendLine("## Керовані області");
            sb.AppendLine("- Kokonoe/: оперативна пам'ять, експорт стану, знання проекту.");
            sb.AppendLine("- Kokonoe/Architecture/: згенеровані карти, звіти стану, беклог.");
            sb.AppendLine("- Kokonoe/Memory/: стабільні факти та епізоди.");
            sb.AppendLine("- Kokonoe/Automation/: правила синхронізації та автоматизація.");
            sb.AppendLine("- Daily/: щоденні робочі нотатки.");
            sb.AppendLine("- Chats/: сирі логи чатів.");
            sb.AppendLine();
            sb.AppendLine("## Інвентар папок");
            foreach (var folder in folders.Take(80))
                sb.AppendLine($"- {folder}");
            sb.AppendLine();
            sb.AppendLine("## Інвентар нотаток");
            foreach (var note in notes.Take(200))
                sb.AppendLine($"- [[{Path.GetFileNameWithoutExtension(note)}]] ({note})");
            if (notes.Count > 200)
                sb.AppendLine($"- ... ще {notes.Count - 200} нотаток");
            return sb.ToString();
        }

        private string BuildVaultMap(Dictionary<string, List<string>> graph)
        {
            var sb = new StringBuilder();
            sb.Append(BuildManagedFrontmatter("vault-map"));
            sb.AppendLine("# Карта vault");
            sb.AppendLine();
            sb.AppendLine("## Вузли посилань");
            foreach (var node in graph.OrderByDescending(x => x.Value.Count).Take(40))
                sb.AppendLine($"- {node.Key}: вихідних посилань {node.Value.Count}");
            sb.AppendLine();
            sb.AppendLine("## Зв'язки");
            foreach (var node in graph.OrderBy(x => x.Key).Take(120))
            {
                if (node.Value.Count == 0) continue;
                sb.AppendLine($"### {node.Key}");
                foreach (var link in node.Value.Take(20))
                    sb.AppendLine($"- [[{link}]]");
            }
            return sb.ToString();
        }

        private string BuildVaultHealth(VaultStatus status, VaultInitStatus init, List<string> isolated, VaultDoctorReport? doctor = null)
        {
            var sb = new StringBuilder();
            sb.Append(BuildManagedFrontmatter("vault-health"));
            sb.AppendLine("# Стан vault");
            sb.AppendLine();
            sb.AppendLine("## Сигнали");
            sb.AppendLine($"- Усього нотаток: {status.TotalNotes}");
            sb.AppendLine($"- Заповнених нотаток: {status.FilledNotes}");
            sb.AppendLine($"- Порожніх нотаток: {status.EmptyNotes.Count}");
            sb.AppendLine($"- Осиротілих нотаток: {status.OrphanNotes.Count}");
            sb.AppendLine($"- Ізольованих нотаток: {isolated.Count}");
            sb.AppendLine($"- Ядро мозку: {(init.HasCoreNote ? init.CoreNotePath : "відсутнє")}");
            if (doctor != null)
            {
                sb.AppendLine($"- Vault doctor: {(doctor.HasProblems ? "issues found" : "clean")} (score {doctor.HealthScore}/100)");
                sb.AppendLine($"- Doctor repairs: {doctor.RepairedFiles.Count + doctor.DeletedEmptyFiles.Count}");
                sb.AppendLine();
                sb.AppendLine("## Vault doctor");
                sb.AppendLine($"- Empty markdown files: {doctor.EmptyMarkdownFiles.Count}");
                sb.AppendLine($"- Folder wiki-links: {doctor.FolderWikiLinkCount}");
                sb.AppendLine($"- Suppressed actor links: {doctor.SuppressedActorLinkCount}");
                sb.AppendLine($"- Frontmatter issues: {doctor.FrontmatterIssues.Count}");
                sb.AppendLine($"- Mojibake suspects: {doctor.MojibakeSuspects.Count}");
                sb.AppendLine($"- Missing wiki targets: {doctor.MissingWikiTargets.Count}");
                if (doctor.RepairedFiles.Count > 0)
                    sb.AppendLine($"- Repaired: {string.Join(", ", doctor.RepairedFiles.Take(12))}");
                if (doctor.DeletedEmptyFiles.Count > 0)
                    sb.AppendLine($"- Deleted empty: {string.Join(", ", doctor.DeletedEmptyFiles.Take(12))}");
            }
            sb.AppendLine();
            sb.AppendLine("## Порожні нотатки");
            foreach (var note in status.EmptyNotes.Take(50))
                sb.AppendLine($"- [[{Path.GetFileNameWithoutExtension(note)}]] ({note})");
            sb.AppendLine();
            sb.AppendLine("## Осиротілі нотатки");
            foreach (var note in status.OrphanNotes.Take(50))
                sb.AppendLine($"- [[{Path.GetFileNameWithoutExtension(note)}]] ({note})");
            sb.AppendLine();
            sb.AppendLine("## Ізольовані нотатки");
            foreach (var note in isolated.Take(50))
                sb.AppendLine($"- [[{Path.GetFileNameWithoutExtension(note)}]] ({note})");
            return sb.ToString();
        }

        private string BuildVaultBacklog(VaultStatus status, VaultInitStatus init, List<string> isolated)
        {
            var sb = new StringBuilder();
            sb.Append(BuildManagedFrontmatter("vault-backlog"));
            sb.AppendLine("# Беклог vault");
            sb.AppendLine();
            if (!init.HasCoreNote)
                sb.AppendLine("- [ ] Створити або позначити центральну нотатку з `type: brain-core`.");
            if (status.EmptyNotes.Count > 0)
                sb.AppendLine($"- [ ] Переглянути порожні нотатки: {status.EmptyNotes.Count}.");
            if (status.OrphanNotes.Count > 0)
                sb.AppendLine($"- [ ] Під'єднати осиротілі нотатки до графа: {status.OrphanNotes.Count}.");
            if (isolated.Count > 0)
                sb.AppendLine($"- [ ] Визначити місце для ізольованих нотаток: {isolated.Count}.");
            if (status.EmptyNotes.Count == 0 && status.OrphanNotes.Count == 0 && init.HasCoreNote)
                sb.AppendLine("- [x] Очевидного структурного боргу не знайдено.");
            sb.AppendLine();
            sb.AppendLine("## Кандидати");
            foreach (var note in status.OrphanNotes.Concat(isolated).Distinct().Take(80))
                sb.AppendLine($"- [ ] [[{Path.GetFileNameWithoutExtension(note)}]] ({note})");
            return sb.ToString();
        }

        private string BuildVaultLanguagePolicy()
        {
            var sb = new StringBuilder();
            sb.Append(BuildManagedFrontmatter("vault-language-policy"));
            sb.AppendLine("# Мовна політика vault");
            sb.AppendLine();
            sb.AppendLine("## Правило");
            sb.AppendLine("- Людські записи, підсумки, журнали, пам'ять, задачі та архітектурні нотатки ведуться українською.");
            sb.AppendLine("- Англійська дозволена тільки для технічних ключів, назв API, назв моделей, команд, шляхів, frontmatter, JSON-полів, тегів, коду та сталих термінів на кшталт health, mood, bpm, vault.");
            sb.AppendLine("- Якщо службова нотатка генерується автоматично, її видимий текст має бути українським.");
            sb.AppendLine("- Сирі історичні чати не переписуються агресивно, щоб не пошкодити архів розмов.");
            sb.AppendLine();
            sb.AppendLine("## Контроль");
            sb.AppendLine("- Після змін у генераторах треба запускати build і тести.");
            sb.AppendLine("- Після масової чистки vault треба повторно сканувати Kokonoe/Daily на старі англійські заголовки.");
            return sb.ToString();
        }

        public MemoryQualityReport AnalyzeMemoryQuality()
        {
            var report = new MemoryQualityReport { RanAt = DateTime.Now };
            foreach (var path in new[]
            {
                "Kokonoe/Memory/Facts.md",
                "Kokonoe/Project Log.md",
                "Kokonoe/Preferences.md",
                "Kokonoe/Tasks.md",
                "Kokonoe/Relationship Notes.md",
                "Kokonoe/AutoMemory.md"
            })
            {
                var content = ReadNote(path);
                if (string.IsNullOrWhiteSpace(content)) continue;

                var items = ExtractMarkdownListItems(content);
                report.NoteItemCounts[path] = items.Count;
                foreach (var item in items)
                {
                    var normalized = NormalizeMemoryText(item);
                    if (normalized.Length > 0)
                        report.NormalizedItems.Add(new MemoryQualityItem(path, item, normalized));
                }
            }

            report.DuplicateGroups = report.NormalizedItems
                .GroupBy(i => i.Normalized)
                .Where(g => g.Count() > 1)
                .Select(g => g.ToList())
                .OrderByDescending(g => g.Count)
                .Take(30)
                .ToList();

            report.SimilarGroups = FindSimilarMemoryGroups(report.NormalizedItems, 0.78, 20);
            return report;
        }

        public MemoryCleanupResult CleanupDuplicateMemoryItems(bool dryRun = true)
        {
            var result = new MemoryCleanupResult { DryRun = dryRun, RanAt = DateTime.Now };
            foreach (var path in new[]
            {
                "Kokonoe/Memory/Facts.md",
                "Kokonoe/Project Log.md",
                "Kokonoe/Preferences.md",
                "Kokonoe/Tasks.md",
                "Kokonoe/Relationship Notes.md"
            })
            {
                var full = Resolve(path);
                if (!File.Exists(full)) continue;

                var lines = File.ReadAllLines(full, Encoding.UTF8).ToList();
                var seen = new HashSet<string>();
                var output = new List<string>();
                var removed = new List<string>();

                foreach (var line in lines)
                {
                    if (TryNormalizeMemoryListLine(line, out var normalized))
                    {
                        if (seen.Contains(normalized))
                        {
                            removed.Add(line.Trim());
                            continue;
                        }
                        seen.Add(normalized);
                    }
                    output.Add(line);
                }

                if (removed.Count == 0) continue;
                result.RemovedByPath[path] = removed;
                if (!dryRun)
                    File.WriteAllLines(full, output, Encoding.UTF8);
            }

            WriteMemoryCleanupReport(result);
            return result;
        }

        private static List<List<MemoryQualityItem>> FindSimilarMemoryGroups(List<MemoryQualityItem> items, double threshold, int maxGroups)
        {
            var groups = new List<List<MemoryQualityItem>>();
            var used = new HashSet<int>();
            for (int i = 0; i < items.Count; i++)
            {
                if (used.Contains(i)) continue;
                var group = new List<MemoryQualityItem> { items[i] };
                for (int j = i + 1; j < items.Count; j++)
                {
                    if (used.Contains(j)) continue;
                    if (items[i].Normalized != items[j].Normalized &&
                        TextSimilarity(items[i].Normalized, items[j].Normalized) >= threshold)
                    {
                        group.Add(items[j]);
                        used.Add(j);
                    }
                }

                if (group.Count > 1)
                {
                    used.Add(i);
                    groups.Add(group);
                    if (groups.Count >= maxGroups) break;
                }
            }
            return groups;
        }

        private string BuildMemoryQualityNote(MemoryQualityReport report)
        {
            var sb = new StringBuilder();
            sb.Append(BuildManagedFrontmatter("memory-quality"));
            sb.AppendLine("# Якість пам'яті");
            sb.AppendLine();
            sb.AppendLine("## Підсумок");
            sb.AppendLine($"- Перевірено елементів пам'яті: {report.NormalizedItems.Count}");
            sb.AppendLine($"- Груп точних дублікатів: {report.DuplicateGroups.Count}");
            sb.AppendLine($"- Груп схожих дублікатів: {report.SimilarGroups.Count}");
            sb.AppendLine();
            sb.AppendLine("## Розмір нотаток");
            foreach (var pair in report.NoteItemCounts.OrderByDescending(p => p.Value))
                sb.AppendLine($"- {pair.Key}: елементів {pair.Value}");
            sb.AppendLine();
            sb.AppendLine("## Точні дублікати");
            AppendMemoryGroups(sb, report.DuplicateGroups);
            sb.AppendLine();
            sb.AppendLine("## Схожі дублікати");
            AppendMemoryGroups(sb, report.SimilarGroups);
            sb.AppendLine();
            sb.AppendLine("## Очищення");
            sb.AppendLine("- Використай `cleanup_memory_duplicates` з `dry_run: true`, щоб переглянути точні дублікати перед видаленням.");
            sb.AppendLine("- `dry_run: false` використовуй тільки після перегляду. Схожі дублікати лише показуються, автоматично не видаляються.");
            return sb.ToString();
        }

        private void WriteMemoryCleanupReport(MemoryCleanupResult result)
        {
            var sb = new StringBuilder();
            sb.Append(BuildManagedFrontmatter("memory-cleanup"));
            sb.AppendLine("# Очищення пам'яті");
            sb.AppendLine();
            sb.AppendLine("## Підсумок");
            sb.AppendLine($"- Пробний режим: {result.DryRun}");
            sb.AppendLine($"- Знайдено рядків-дублікатів: {result.TotalRemoved}");
            sb.AppendLine();
            sb.AppendLine("## Кандидати на видалення");
            if (result.TotalRemoved == 0)
                sb.AppendLine("- немає");
            foreach (var pair in result.RemovedByPath)
            {
                sb.AppendLine($"### {pair.Key}");
                foreach (var line in pair.Value.Take(80))
                    sb.AppendLine($"- `{line.Replace("`", "'")}`");
            }

            UpsertManagedNote("Kokonoe/Memory/Cleanup.md", sb.ToString(), new VaultMaintenanceResult());
        }

        private static void AppendMemoryGroups(StringBuilder sb, List<List<MemoryQualityItem>> groups)
        {
            if (groups.Count == 0)
            {
                sb.AppendLine("- немає");
                return;
            }

            foreach (var group in groups.Take(20))
            {
                sb.AppendLine("- група:");
                foreach (var item in group.Take(6))
                    sb.AppendLine($"  - {item.Path}: {item.Text}");
            }
        }

        public MemoryReviewSnapshot BuildMemoryReview(MemoryQualityReport? quality = null, TaskQueueSnapshot? queue = null)
        {
            quality ??= AnalyzeMemoryQuality();
            queue ??= BuildTaskQueue();

            var review = new MemoryReviewSnapshot { RanAt = DateTime.Now };
            foreach (var group in quality.DuplicateGroups.Take(20))
            {
                review.Actions.Add(new MemoryReviewAction
                {
                    Action = "merge",
                    Reason = "точні дублікати в пам'яті",
                    SourcePath = group.First().Path,
                    TargetPath = group.First().Path,
                    Confidence = 1.0,
                    Items = group.Select(i => i.Text).Distinct().Take(8).ToList()
                });
            }

            foreach (var group in quality.SimilarGroups.Take(20))
            {
                review.Actions.Add(new MemoryReviewAction
                {
                    Action = "confirm",
                    Reason = "схожі елементи пам'яті потребують підтвердження перед об'єднанням",
                    SourcePath = group.First().Path,
                    TargetPath = group.First().Path,
                    Confidence = 0.72,
                    Items = group.Select(i => i.Text).Distinct().Take(8).ToList()
                });
            }

            foreach (var task in queue.OpenTasks.Take(20))
            {
                review.Actions.Add(new MemoryReviewAction
                {
                    Action = "keep",
                    Reason = "відкрита задача ще потребує відстеження",
                    SourcePath = task.Path,
                    TargetPath = "Kokonoe/Tasks Queue.md",
                    Confidence = 0.85,
                    Items = new List<string> { task.Text }
                });
            }

            foreach (var item in quality.NormalizedItems
                .Where(i => i.Path.Contains("Preferences", StringComparison.OrdinalIgnoreCase) ||
                            i.Text.Contains("like", StringComparison.OrdinalIgnoreCase) ||
                            i.Text.Contains("prefer", StringComparison.OrdinalIgnoreCase) ||
                            i.Text.Contains("подоба", StringComparison.OrdinalIgnoreCase))
                .Take(20))
            {
                review.Actions.Add(new MemoryReviewAction
                {
                    Action = "promote_to_preference",
                    Reason = "пам'ять схожа на вподобання, її треба легко знаходити",
                    SourcePath = item.Path,
                    TargetPath = "Kokonoe/Preferences.md",
                    Confidence = item.Path.Contains("Preferences", StringComparison.OrdinalIgnoreCase) ? 0.95 : 0.65,
                    Items = new List<string> { item.Text }
                });
            }

            review.Actions = review.Actions
                .GroupBy(a => $"{a.Action}|{a.TargetPath}|{string.Join(";", a.Items.Select(NormalizeMemoryText))}")
                .Select(g => g.First())
                .OrderByDescending(a => a.Confidence)
                .ThenBy(a => a.Action)
                .Take(120)
                .ToList();
            return review;
        }

        private string BuildMemoryReviewNote(MemoryReviewSnapshot review)
        {
            var sb = new StringBuilder();
            sb.Append(BuildManagedFrontmatter("memory-review"));
            sb.AppendLine("# Огляд пам'яті");
            sb.AppendLine();
            sb.AppendLine("## Підсумок");
            sb.AppendLine($"- Запропоновано дій: {review.Actions.Count}");
            sb.AppendLine("- Це рекомендації. Нотатки пам'яті самі по собі не змінюються.");
            sb.AppendLine();
            foreach (var actionGroup in review.Actions.GroupBy(a => a.Action).OrderBy(g => g.Key))
            {
                sb.AppendLine($"## {MemoryReviewActionLabel(actionGroup.Key)}");
                foreach (var action in actionGroup.Take(40))
                {
                    sb.AppendLine($"- впевненість {action.Confidence:0.00}: {action.Reason}");
                    sb.AppendLine($"  - джерело: {action.SourcePath}");
                    sb.AppendLine($"  - ціль: {action.TargetPath}");
                    foreach (var item in action.Items.Take(5))
                        sb.AppendLine($"  - елемент: {item}");
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static string MemoryReviewActionLabel(string action) => action switch
        {
            "merge" => "Об'єднати",
            "confirm" => "Підтвердити",
            "keep" => "Залишити",
            "promote_to_preference" => "Перенести у вподобання",
            _ => action
        };

        public TaskQueueSnapshot BuildTaskQueue()
        {
            var snapshot = new TaskQueueSnapshot { RanAt = DateTime.Now };
            foreach (var path in new[] { "Kokonoe/Tasks.md", "Kokonoe/Project Log.md", "Kokonoe/Architecture/Backlog.md" })
            {
                var content = ReadNote(path);
                if (string.IsNullOrWhiteSpace(content)) continue;
                foreach (var item in ExtractMarkdownListItems(content))
                {
                    if (IsDoneTask(item))
                        snapshot.DoneTasks.Add(new TaskQueueItem(path, item));
                    else if (LooksLikeTask(item))
                        snapshot.OpenTasks.Add(new TaskQueueItem(path, item));
                }
            }

            snapshot.OpenTasks = snapshot.OpenTasks
                .GroupBy(t => NormalizeMemoryText(t.Text))
                .Select(g => g.First())
                .OrderBy(t => t.Path)
                .ThenBy(t => t.Text)
                .Take(120)
                .ToList();
            return snapshot;
        }

        private string BuildTaskQueueNote(TaskQueueSnapshot snapshot)
        {
            var sb = new StringBuilder();
            sb.Append(BuildManagedFrontmatter("task-queue"));
            sb.AppendLine("# Черга задач");
            sb.AppendLine();
            sb.AppendLine("## Підсумок");
            sb.AppendLine($"- Відкритих задач: {snapshot.OpenTasks.Count}");
            sb.AppendLine($"- Завершених задач знайдено: {snapshot.DoneTasks.Count}");
            sb.AppendLine();
            sb.AppendLine("## Відкриті");
            if (snapshot.OpenTasks.Count == 0)
                sb.AppendLine("- [x] Відкритих задач не знайдено.");
            foreach (var task in snapshot.OpenTasks)
                sb.AppendLine($"- [ ] {task.Text} ({task.Path})");
            return sb.ToString();
        }

        private static bool LooksLikeTask(string item)
        {
            var text = NormalizeMemoryText(item);
            if (text.Length < 4) return false;
            return item.Contains("[task]", StringComparison.OrdinalIgnoreCase) ||
                   item.Contains("[ ]", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("todo") ||
                   text.Contains("task") ||
                   text.Contains("add") ||
                   text.Contains("implement") ||
                   text.Contains("create") ||
                   text.Contains("зроб") ||
                   text.Contains("реаліз") ||
                   text.Contains("дод") ||
                   text.Contains("fix") ||
                   text.Contains("bug");
        }

        private static bool IsDoneTask(string item)
        {
            var text = NormalizeMemoryText(item);
            return item.Contains("[x]", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("done") ||
                   text.Contains("completed") ||
                   text.Contains("готово") ||
                   text.Contains("зроблено");
        }

        private static string BuildVaultAutomationNote()
        {
            var sb = new StringBuilder();
            sb.Append(BuildManagedFrontmatter("vault-automation"));
            sb.AppendLine("# Синхронізація Obsidian");
            sb.AppendLine();
            sb.AppendLine("## Правила");
            sb.AppendLine("- Кожні 5 обмінів у чаті збираються в нотатки пам'яті Коконое.");
            sb.AppendLine("- Архітектурні нотатки vault оновлюються після кожної пакетної синхронізації.");
            sb.AppendLine("- Керовані нотатки перезаписуються, бо це згенеровані знімки стану.");
            sb.AppendLine("- Нотатки, написані людиною, тільки доповнюються або зв'язуються звичайною синхронізацією пам'яті.");
            sb.AppendLine();
            sb.AppendLine("## Керовані нотатки");
            sb.AppendLine("- [[Kokonoe/Vault Index]]");
            sb.AppendLine("- [[Kokonoe/Architecture/Manifest]]");
            sb.AppendLine("- [[Kokonoe/Architecture/Map]]");
            sb.AppendLine("- [[Kokonoe/Architecture/Health]]");
            sb.AppendLine("- [[Kokonoe/Architecture/Backlog]]");
            sb.AppendLine("- [[Kokonoe/Memory/Quality]]");
            sb.AppendLine("- [[Kokonoe/Memory/Cleanup]]");
            sb.AppendLine("- [[Kokonoe/Memory/Review]]");
            sb.AppendLine("- [[Kokonoe/Tasks Queue]]");
            return sb.ToString();
        }

        private void AppendMaintenanceLog(VaultMaintenanceResult result, VaultStatus status, VaultInitStatus init)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"\n## {result.RanAt:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"- Причина: {result.Reason}");
            sb.AppendLine($"- Створено папок: {result.CreatedFolders.Count}");
            sb.AppendLine($"- Створено нотаток: {result.CreatedNotes.Count}");
            sb.AppendLine($"- Оновлено нотаток: {result.UpdatedNotes.Count}");
            sb.AppendLine($"- Нотаток із зміненими посиланнями: {result.LinkTouchedNotes}");
            sb.AppendLine($"- Додано посилань: {result.LinksAdded}");
            sb.AppendLine($"- Груп дублікатів пам'яті: {result.MemoryDuplicateGroups}");
            sb.AppendLine($"- Відкритих задач: {result.OpenTaskCount}");
            sb.AppendLine($"- Дій огляду пам'яті: {result.MemoryReviewActionCount}");
            sb.AppendLine($"- Нотаток: {status.TotalNotes}, осиротілих: {status.OrphanNotes.Count}, порожніх: {status.EmptyNotes.Count}");
            sb.AppendLine($"- Ядро мозку: {(init.HasCoreNote ? init.CoreNotePath : "відсутнє")}");

            var full = Resolve("Kokonoe/Architecture/Change Log.md");
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            if (!File.Exists(full))
                File.WriteAllText(full, BuildManagedFrontmatter("vault-change-log") + "# Журнал архітектурних змін\n", Encoding.UTF8);
            File.AppendAllText(full, sb.ToString(), Encoding.UTF8);
        }

        public string GetToolsDescription() => """
=== OBSIDIAN TOOLS ===
list_notes [folder] — список нотаток
read_note <path> — читати нотатку
write_note <path> <content> — перезаписати нотатку
create_note <title> [folder] [tags] [content] — нова нотатка
append_note <path> <content> — дописати в кінець
search_notes <query> — пошук по тексту
daily_note — сьогоднішня щоденна нотатка
append_daily <content> — дописати в щоденну нотатку
delete_note <path> — видалити нотатку
vault_status — стан vault (порожні, осиротілі нотатки)
cleanup_empty — видалити порожні нотатки
""";

        // ── GRAPH / LINKS ─────────────────────────────────────────

        /// <summary>
        /// Повертає словник: назва нотатки → відносний шлях.
        /// Використовується для пошуку згадок і проставлення [[links]].
        /// </summary>
        public Dictionary<string, string> GetNoteIndex()
        {
            var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in SafeGetFiles(_vault))
            {
                var rel   = Path.GetRelativePath(_vault, file).Replace('\\', '/');
                var title = Path.GetFileNameWithoutExtension(file);
                try
                {
                    if (IsKokonoeManagedNote(File.ReadAllText(file, Encoding.UTF8)))
                        continue;
                }
                catch { continue; }
                if (!index.ContainsKey(title))
                    index[title] = rel;
            }
            return index;
        }

        /// <summary>
        /// Сканує всі нотатки vault. Де знаходить назву іншої нотатки як звичайний текст
        /// (не всередині [[...]]) — загортає її в [[посилання]].
        /// Використовує split-and-replace: розбиває текст на [[link]] і plain-text сегменти,
        /// замінює тільки в plain-text — гарантовано без дублікатів і вкладень.
        /// </summary>
        public (int changed, int linksAdded) RebuildLinks()
        {
            var index   = GetNoteIndex();
            var changed = 0;
            var total   = 0;

            // Titles sorted longest first — щоб "Python Tips" не перекрилось "Python"
            var titles = index.Keys
                .Where(t => t.Length > 3 && !IsSuppressedAutoLinkTitle(t))
                .OrderByDescending(t => t.Length)
                .ToList();

            // Pre-compile regexes for performance
            var titleRegexes = titles.ToDictionary(
                t => t,
                t => new System.Text.RegularExpressions.Regex(
                    $@"\b{System.Text.RegularExpressions.Regex.Escape(t)}\b",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                    System.Text.RegularExpressions.RegexOptions.Compiled));

            // Splitter: breaks text into [[link]] tokens and plain-text tokens
            var linkSplitter = new System.Text.RegularExpressions.Regex(@"\[\[[^\]]*\]\]");

            foreach (var file in SafeGetFiles(_vault))
            {
                try
                {
                    var raw       = File.ReadAllText(file, Encoding.UTF8);
                    if (IsKokonoeManagedNote(raw))
                        continue;
                    var thisTitle = Path.GetFileNameWithoutExtension(file);
                    var fileAdded = 0;

                    var frontmatter = "";
                    var body = raw;
                    if (raw.StartsWith("---", StringComparison.Ordinal))
                    {
                        var end = raw.IndexOf("---", 3, StringComparison.Ordinal);
                        if (end > 0)
                        {
                            frontmatter = raw[..(end + 3)];
                            body = raw[(end + 3)..];
                        }
                    }

                    // Split into [plain, [[link]], plain, [[link]], ...] segments.
                    // Frontmatter is metadata, not prose; never inject wiki links into tags/date/type.
                    var segments  = linkSplitter.Split(body);    // plain text parts
                    var links     = linkSplitter.Matches(body);  // [[link]] parts

                    var newSegments = new string[segments.Length];
                    for (int i = 0; i < segments.Length; i++)
                    {
                        var seg = segments[i];
                        foreach (var title in titles)
                        {
                            if (string.Equals(title, thisTitle, StringComparison.OrdinalIgnoreCase))
                                continue; // не посилатись на себе

                            var rx = titleRegexes[title];
                            var before = seg;
                            seg = rx.Replace(seg, m => $"[[{m.Value}]]");
                            if (seg != before)
                                fileAdded += System.Text.RegularExpressions.Regex
                                    .Matches(before, $@"\b{System.Text.RegularExpressions.Regex.Escape(title)}\b",
                                        System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
                        }
                        newSegments[i] = seg;
                    }

                    // Reassemble: plain[0] + link[0] + plain[1] + link[1] + ...
                    var sb = new StringBuilder();
                    for (int i = 0; i < newSegments.Length; i++)
                    {
                        sb.Append(newSegments[i]);
                        if (i < links.Count)
                            sb.Append(links[i].Value);
                    }
                    var modified = frontmatter + sb;

                    if (modified != raw)
                    {
                        File.WriteAllText(file, modified, Encoding.UTF8);
                        changed++;
                        total += fileAdded;
                    }
                }
                catch (Exception ex) { Debug.WriteLine($"[ObsidianMcp] RebuildLinks failed for {file}: {ex.Message}"); }
            }

            return (changed, total);
        }

        private static bool IsSuppressedAutoLinkTitle(string title)
        {
            var normalized = title.Trim();
            return string.Equals(normalized, "Kokonoe", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Повертає список нотаток, на які посилається дана нотатка (вихідні посилання).
        /// </summary>
        public List<string> GetOutgoingLinks(string path)
        {
            var content = ReadNote(path);
            if (content == null) return new();

            var matches = System.Text.RegularExpressions.Regex.Matches(content, @"\[\[([^\]|#]+)");
            return matches.Select(m => m.Groups[1].Value.Trim()).Distinct().ToList();
        }

        /// <summary>
        /// Повертає список нотаток, які посилаються на дану (вхідні посилання / backlinks).
        /// </summary>
        public List<string> GetBacklinks(string path)
        {
            var targetTitle = Path.GetFileNameWithoutExtension(path);
            var backlinks   = new List<string>();

            foreach (var file in SafeGetFiles(_vault))
            {
                try
                {
                    var rel = Path.GetRelativePath(_vault, file).Replace('\\', '/');
                    if (rel == path) continue;

                    var content = File.ReadAllText(file, Encoding.UTF8);
                    if (IsKokonoeManagedNote(content))
                        continue;
                    if (System.Text.RegularExpressions.Regex.IsMatch(
                            content, $@"\[\[{System.Text.RegularExpressions.Regex.Escape(targetTitle)}",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        backlinks.Add(rel);
                }
                catch (Exception ex) { Debug.WriteLine($"[ObsidianMcp] GetBacklinks scan failed for {file}: {ex.Message}"); }
            }

            return backlinks;
        }

        // ── HELPERS ───────────────────────────────────────────────

        // ── SEMANTIC SEARCH ───────────────────────────────────────────

        /// <summary>
        /// Пошук нотаток за змістом з TF-IDF ранжуванням.
        /// Повертає топ результати з score та preview.
        /// </summary>
        public List<SearchResult> SearchSemantic(string query, int max = 8)
        {
            var results = new List<SearchResult>();
            if (!Directory.Exists(_vault)) return results;

            var queryWords = System.Text.RegularExpressions.Regex
                .Split(query.ToLowerInvariant(), @"[^\p{L}\p{N}]+")
                .Where(w => w.Length > 2)
                .ToHashSet();
            if (queryWords.Count == 0) return results;

            var files = SafeGetFiles(_vault);
            foreach (var file in files)
            {
                try
                {
                    var content = File.ReadAllText(file, Encoding.UTF8);
                    if (IsKokonoeManagedNote(content))
                        continue;
                    var lower   = content.ToLower();
                    // TF: кількість співпадінь
                    var tf = queryWords.Sum(w => CountOccurrences(lower, w));
                    if (tf == 0) continue;

                    // Бонус якщо слово є в заголовку
                    var title = Path.GetFileNameWithoutExtension(file).ToLower();
                    var titleBonus = queryWords.Count(w => title.Contains(w)) * 3;

                    var score = tf + titleBonus;
                    var preview = ExtractPreview(content, queryWords.First());

                    results.Add(new SearchResult
                    {
                        Path    = Path.GetRelativePath(_vault, file).Replace('\\', '/'),
                        Title   = Path.GetFileNameWithoutExtension(file),
                        Preview = preview,
                        Score   = score
                    });
                }
                catch (Exception ex) { Debug.WriteLine($"[ObsidianMcp] SearchSemantic failed for {file}: {ex.Message}"); }
            }

            return results.OrderByDescending(r => r.Score).Take(max).ToList();
        }

        private static string ExtractPreview(string content, string keyword, int maxLen = 200)
        {
            var idx = content.ToLower().IndexOf(keyword, StringComparison.Ordinal);
            if (idx < 0) return content[..Math.Min(maxLen, content.Length)];
            var start = Math.Max(0, idx - 60);
            var end   = Math.Min(content.Length, start + maxLen);
            return (start > 0 ? "…" : "") + content[start..end].Trim() + (end < content.Length ? "…" : "");
        }

        // ── NOTE GRAPH ────────────────────────────────────────────────

        /// <summary>Граф посилань: словник {нотатка → список куди веде}</summary>
        public Dictionary<string, List<string>> GetNoteGraph()
        {
            var graph = new Dictionary<string, List<string>>();
            if (!Directory.Exists(_vault)) return graph;

            var linkRegex = new System.Text.RegularExpressions.Regex(@"\[\[([^\]|#]+)(?:[|#][^\]]*)?\]\]");
            foreach (var file in SafeGetFiles(_vault))
            {
                try
                {
                    var key   = Path.GetRelativePath(_vault, file).Replace('\\', '/');
                    var raw = File.ReadAllText(file, Encoding.UTF8);
                    if (IsKokonoeManagedNote(raw))
                        continue;
                    var links = new List<string>();
                    foreach (System.Text.RegularExpressions.Match m in linkRegex.Matches(raw))
                    {
                        var target = m.Groups[1].Value.Trim();
                        if (!string.IsNullOrEmpty(target)) links.Add(target);
                    }
                    graph[key] = links.Distinct().ToList();
                }
                catch (Exception ex) { Debug.WriteLine($"[ObsidianMcp] GetNoteGraph failed for {file}: {ex.Message}"); }
            }
            return graph;
        }

        // ── MERGE NOTES ───────────────────────────────────────────────

        /// <summary>Злити кілька нотаток в одну (перша — destination, решта — source)</summary>
        public string MergeNotes(string[] paths, string separator = "\n\n---\n\n")
        {
            if (paths.Length < 2) throw new ArgumentException("Потрібно хоча б 2 нотатки");

            var destPath = Resolve(paths[0]);
            var sb = new StringBuilder();

            foreach (var rel in paths)
            {
                var full = Resolve(rel);
                if (!File.Exists(full)) continue;
                sb.Append(File.ReadAllText(full));
                sb.Append(separator);
            }

            var merged = sb.ToString().TrimEnd();
            File.WriteAllText(destPath, merged);

            // Видалити джерела (крім першої)
            foreach (var rel in paths.Skip(1))
            {
                try { var f = Resolve(rel); if (File.Exists(f)) File.Delete(f); } catch (Exception ex) { Debug.WriteLine($"[ObsidianMcp] MergeNotes delete failed for {rel}: {ex.Message}"); }
            }

            return paths[0];
        }

        // ── MODIFIED TODAY ────────────────────────────────────────────

        /// <summary>Нотатки змінені сьогодні</summary>
        public List<string> GetNotesModifiedToday()
        {
            if (!Directory.Exists(_vault)) return new();
            return SafeGetFiles(_vault)
                .Where(f => File.GetLastWriteTime(f).Date == DateTime.Today)
                .Select(f => Path.GetRelativePath(_vault, f).Replace('\\', '/'))
                .OrderByDescending(f => File.GetLastWriteTime(Path.Combine(_vault, f.Replace('/', Path.DirectorySeparatorChar))))
                .ToList();
        }

        // ── AUTO TAG ──────────────────────────────────────────────────

        /// <summary>Додати теги в frontmatter нотатки на основі її змісту</summary>
        public string AutoTagNote(string relPath, string[] tags)
        {
            var full = Resolve(relPath);
            if (!File.Exists(full)) throw new FileNotFoundException($"Нотатка не знайдена: {relPath}");

            var content = File.ReadAllText(full);

            if (content.StartsWith("---"))
            {
                // Вже є frontmatter — оновити теги
                var end = content.IndexOf("---", 3);
                if (end > 0)
                {
                    var front = content[3..end];
                    var tagsLine = $"tags: [{SanitizeTagsLine(string.Join(", ", tags))}]";
                    if (front.Contains("tags:"))
                        front = System.Text.RegularExpressions.Regex.Replace(front, @"tags:.*", tagsLine);
                    else
                        front += $"\n{tagsLine}\n";
                    content = "---" + front + "---" + content[(end + 3)..];
                }
            }
            else
            {
                // Немає frontmatter — додати
                var front = $"---\ntags: [{SanitizeTagsLine(string.Join(", ", tags))}]\n---\n\n";
                content = front + content;
            }

            File.WriteAllText(full, NormalizeFrontmatter(content), Encoding.UTF8);
            return relPath;
        }

        // ── CLUSTER ORPHANS ───────────────────────────────────────────

        /// <summary>Знайти нотатки без жодних зв'язків (ні вхідних ні вихідних)</summary>
        public List<string> GetIsolatedNotes()
        {
            var graph     = GetNoteGraph();
            var allNotes  = graph.Keys.ToHashSet();
            var hasIncoming = new HashSet<string>();

            foreach (var links in graph.Values)
                foreach (var l in links)
                    hasIncoming.Add(l + ".md");

            return allNotes
                .Where(n => graph[n].Count == 0 && !hasIncoming.Contains(n))
                .ToList();
        }

        private static List<string> ExtractMarkdownListItems(string content)
        {
            var items = new List<string>();
            using var reader = new StringReader(content ?? "");
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("- ")) continue;
                var item = trimmed[2..].Trim();
                item = System.Text.RegularExpressions.Regex.Replace(item, @"^\[[^\]]+\]\s*", "").Trim();
                if (item.Length > 0) items.Add(item);
            }
            return items;
        }

        private static bool TryNormalizeMemoryListLine(string line, out string normalized)
        {
            normalized = "";
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("- ")) return false;
            var item = trimmed[2..].Trim();
            if (item.StartsWith("[x]", StringComparison.OrdinalIgnoreCase))
                return false;
            item = System.Text.RegularExpressions.Regex.Replace(item, @"^\[[^\]]+\]\s*", "").Trim();
            normalized = NormalizeMemoryText(item);
            return normalized.Length > 0;
        }

        private static string NormalizeMemoryText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            var normalized = text.ToLowerInvariant();
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\[[^\]]+\]", " ");
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[^\p{L}\p{N}\s]+", " ");
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ").Trim();
            return normalized;
        }

        private static double TextSimilarity(string a, string b)
        {
            var aWords = NormalizeMemoryText(a).Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 2).ToHashSet();
            var bWords = NormalizeMemoryText(b).Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 2).ToHashSet();
            if (aWords.Count == 0 || bWords.Count == 0) return 0;
            var intersection = aWords.Intersect(bWords).Count();
            var union = aWords.Union(bWords).Count();
            return union == 0 ? 0 : (double)intersection / union;
        }

        private string Resolve(string rel)
        {
            if (string.IsNullOrWhiteSpace(rel))
                throw new ArgumentException("Vault path is empty.", nameof(rel));

            if (!rel.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                rel += ".md";

            var normalizedRel = rel
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var full = Path.GetFullPath(Path.Combine(_vault, normalizedRel));
            var vaultRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_vault)) + Path.DirectorySeparatorChar;

            if (!full.StartsWith(vaultRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Vault path escapes vault root: {rel}");

            return full;
        }

        /// <summary>
        /// Безпечний аналог Directory.GetDirectories з AllDirectories.
        /// </summary>
        private static IEnumerable<string> SafeGetDirectories(string root)
        {
            var queue = new Queue<string>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                var dir = queue.Dequeue();

                string[] subdirs;
                try { subdirs = Directory.GetDirectories(dir); }
                catch (Exception ex) { Debug.WriteLine($"[ObsidianMcp] SafeGetDirectories skip {dir}: {ex.Message}"); continue; }

                foreach (var sub in subdirs)
                {
                    yield return sub;
                    queue.Enqueue(sub);
                }
            }
        }

        /// <summary>
        /// Безпечний аналог Directory.GetFiles з AllDirectories.
        /// Скіпає папки з UnauthorizedAccessException (System Volume Information тощо).
        /// </summary>
        private static IEnumerable<string> SafeGetFiles(string root, string pattern = "*.md")
        {
            var queue = new Queue<string>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                var dir = queue.Dequeue();

                string[] files;
                try { files = Directory.GetFiles(dir, pattern); }
                catch (Exception ex) { Debug.WriteLine($"[ObsidianMcp] SafeGetFiles skip {dir}: {ex.Message}"); continue; }

                foreach (var f in files) yield return f;

                string[] subdirs;
                try { subdirs = Directory.GetDirectories(dir); }
                catch (Exception ex) { Debug.WriteLine($"[ObsidianMcp] SafeGetFiles skip dirs {dir}: {ex.Message}"); continue; }

                foreach (var sub in subdirs) queue.Enqueue(sub);
            }
        }
    }

    public class SearchResult
    {
        public string Path { get; set; } = "";
        public string Title { get; set; } = "";
        public string Preview { get; set; } = "";
        public int Score { get; set; }
    }

    public class VaultInitStatus
    {
        public int    NoteCount       { get; set; }
        public int    TotalLinks      { get; set; }
        public bool   IsEmpty         { get; set; }
        public bool   HasCoreNote     { get; set; }
        public string CoreNotePath    { get; set; } = "";
        public string SuggestedAction { get; set; } = "";

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Vault: {NoteCount} нотаток, {TotalLinks} [[посилань]]");
            sb.AppendLine($"Центральна нотатка (brain-core): {(HasCoreNote ? CoreNotePath : "відсутня")}");
            sb.AppendLine($"Дія: {SuggestedAction}");
            return sb.ToString().Trim();
        }
    }

    public class VaultStatus
    {
        public int TotalNotes  { get; set; }
        public int FilledNotes { get; set; }
        public List<string> EmptyNotes  { get; set; } = new();
        public List<string> OrphanNotes { get; set; } = new();

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Всього нотаток: {TotalNotes} (заповнених: {FilledNotes})");
            if (EmptyNotes.Count > 0)
                sb.AppendLine($"Порожніх ({EmptyNotes.Count}): {string.Join(", ", EmptyNotes.Take(10))}");
            if (OrphanNotes.Count > 0)
                sb.AppendLine($"Осиротілих без [[links]] ({OrphanNotes.Count}): {string.Join(", ", OrphanNotes.Take(10))}");
            return sb.ToString().Trim();
        }
    }

    public class VaultDoctorReport
    {
        public DateTime RanAt { get; set; }
        public bool RepairApplied { get; set; }
        public List<string> EmptyMarkdownFiles { get; set; } = new();
        public Dictionary<string, int> FolderWikiLinks { get; set; } = new();
        public Dictionary<string, int> SuppressedActorLinks { get; set; } = new();
        public List<string> FrontmatterIssues { get; set; } = new();
        public List<string> MojibakeSuspects { get; set; } = new();
        public Dictionary<string, int> MissingWikiTargets { get; set; } = new();
        public List<string> RepairedFiles { get; set; } = new();
        public List<string> DeletedEmptyFiles { get; set; } = new();
        public int FolderWikiLinkCount => FolderWikiLinks.Values.Sum();
        public int SuppressedActorLinkCount => SuppressedActorLinks.Values.Sum();
        public bool HasProblems =>
            EmptyMarkdownFiles.Count > 0 ||
            FolderWikiLinkCount > 0 ||
            SuppressedActorLinkCount > 0 ||
            FrontmatterIssues.Count > 0 ||
            MojibakeSuspects.Count > 0 ||
            MissingWikiTargets.Count > 0;
        public int HealthScore => Math.Clamp(
            100
            - EmptyMarkdownFiles.Count * 8
            - FolderWikiLinkCount * 3
            - SuppressedActorLinkCount * 2
            - FrontmatterIssues.Count * 5
            - MojibakeSuspects.Count * 4
            - MissingWikiTargets.Count,
            0,
            100);
    }

    public class VaultMaintenanceResult
    {
        public string Reason { get; set; } = "";
        public DateTime RanAt { get; set; }
        public List<string> CreatedFolders { get; set; } = new();
        public List<string> CreatedNotes { get; set; } = new();
        public List<string> UpdatedNotes { get; set; } = new();
        public int LinkTouchedNotes { get; set; }
        public int LinksAdded { get; set; }
        public int MemoryDuplicateGroups { get; set; }
        public int OpenTaskCount { get; set; }
        public int MemoryReviewActionCount { get; set; }

        public override string ToString()
        {
            return $"Vault maintenance: {CreatedFolders.Count} folders, {CreatedNotes.Count} created notes, {UpdatedNotes.Count} updated notes, {LinksAdded} links, {MemoryDuplicateGroups} duplicate groups, {OpenTaskCount} open tasks, {MemoryReviewActionCount} review actions.";
        }
    }

    public class MemoryQualityReport
    {
        public DateTime RanAt { get; set; }
        public Dictionary<string, int> NoteItemCounts { get; set; } = new();
        public List<MemoryQualityItem> NormalizedItems { get; set; } = new();
        public List<List<MemoryQualityItem>> DuplicateGroups { get; set; } = new();
        public List<List<MemoryQualityItem>> SimilarGroups { get; set; } = new();
    }

    public record MemoryQualityItem(string Path, string Text, string Normalized);

    public class MemoryReviewSnapshot
    {
        public DateTime RanAt { get; set; }
        public List<MemoryReviewAction> Actions { get; set; } = new();
    }

    public class MemoryReviewAction
    {
        public string Action { get; set; } = "";
        public string Reason { get; set; } = "";
        public string SourcePath { get; set; } = "";
        public string TargetPath { get; set; } = "";
        public double Confidence { get; set; }
        public List<string> Items { get; set; } = new();
    }

    public class MemoryCleanupResult
    {
        public DateTime RanAt { get; set; }
        public bool DryRun { get; set; }
        public Dictionary<string, List<string>> RemovedByPath { get; set; } = new();
        public int TotalRemoved => RemovedByPath.Values.Sum(v => v.Count);

        public override string ToString()
        {
            return $"Memory cleanup: {(DryRun ? "dry run" : "applied")}, {TotalRemoved} duplicate lines.";
        }
    }

    public class TaskQueueSnapshot
    {
        public DateTime RanAt { get; set; }
        public List<TaskQueueItem> OpenTasks { get; set; } = new();
        public List<TaskQueueItem> DoneTasks { get; set; } = new();
    }

    public record TaskQueueItem(string Path, string Text);
}
