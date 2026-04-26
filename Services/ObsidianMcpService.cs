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
            _vault = vaultPath;
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
tags: [{tagsLine}]
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

        // ── DELETE ────────────────────────────────────────────────

        public string DeleteNote(string path)
        {
            var full = Resolve(path);
            if (!File.Exists(full)) return "Не знайдено";
            File.Delete(full);
            return "Видалено: " + path;
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
                TotalNotes   = allFiles.Count,
                EmptyNotes   = emptyNotes,
                OrphanNotes  = orphanNotes,
                FilledNotes  = filledNotes.Count
            };
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
                .Where(t => t.Length > 3)
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
                    var thisTitle = Path.GetFileNameWithoutExtension(file);
                    var fileAdded = 0;

                    // Split into [plain, [[link]], plain, [[link]], ...] segments
                    var segments  = linkSplitter.Split(raw);    // plain text parts
                    var links     = linkSplitter.Matches(raw);  // [[link]] parts

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
                    var modified = sb.ToString();

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

            var queryWords = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2).ToHashSet();
            if (queryWords.Count == 0) return results;

            var files = SafeGetFiles(_vault);
            foreach (var file in files)
            {
                try
                {
                    var content = File.ReadAllText(file);
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
                    var links = new List<string>();
                    foreach (System.Text.RegularExpressions.Match m in linkRegex.Matches(File.ReadAllText(file)))
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
                    var tagsLine = $"tags: [{string.Join(", ", tags)}]";
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
                var front = $"---\ntags: [{string.Join(", ", tags)}]\n---\n\n";
                content = front + content;
            }

            File.WriteAllText(full, content);
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

        private string Resolve(string rel)
        {
            if (!rel.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                rel += ".md";
            return Path.Combine(_vault, rel.Replace('/', Path.DirectorySeparatorChar));
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
}
