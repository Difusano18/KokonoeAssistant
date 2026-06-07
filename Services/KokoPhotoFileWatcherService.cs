using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Text.RegularExpressions;

namespace KokonoeAssistant.Services
{
    public sealed class KokoPhotoFileWatcherService : IDisposable
    {
        private static readonly string[] ImageExtensions =
        {
            ".jpg", ".jpeg", ".png", ".webp", ".bmp"
        };

        private static readonly string[] DocumentExtensions =
        {
            ".txt", ".md", ".markdown", ".csv", ".json", ".log", ".pdf"
        };

        private readonly string _dataDir;
        private readonly Func<LlmService> _llmFactory;
        private readonly ChatRepository _chatRepository;
        private readonly Func<ChatLogger?> _chatLoggerFactory;
        private readonly Func<KokoMemoryEngine?> _memoryFactory;
        private readonly Func<ObsidianMcpService?> _obsidianFactory;
        private readonly List<FileSystemWatcher> _watchers = new();
        private readonly ConcurrentDictionary<string, DateTime> _seen = new(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _visionLock = new(1, 1);
        private bool _started;

        public KokoPhotoFileWatcherService(
            string dataDir,
            Func<LlmService> llmFactory,
            ChatRepository chatRepository,
            Func<ChatLogger?> chatLoggerFactory,
            Func<KokoMemoryEngine?>? memoryFactory = null,
            Func<ObsidianMcpService?>? obsidianFactory = null)
        {
            _dataDir = dataDir;
            _llmFactory = llmFactory;
            _chatRepository = chatRepository;
            _chatLoggerFactory = chatLoggerFactory;
            _memoryFactory = memoryFactory ?? (() => null);
            _obsidianFactory = obsidianFactory ?? (() => null);
            Directory.CreateDirectory(_dataDir);
        }

        public void Start()
        {
            if (_started) return;
            _started = true;

            foreach (var dir in GetDefaultImageFolders().Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    if (!Directory.Exists(dir))
                        continue;

                    var watcher = new FileSystemWatcher(dir)
                    {
                        IncludeSubdirectories = false,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                        EnableRaisingEvents = true
                    };
                    watcher.Created += (_, e) => Queue(e.FullPath, "created");
                    watcher.Changed += (_, e) => Queue(e.FullPath, "changed");
                    watcher.Renamed += (_, e) => Queue(e.FullPath, "renamed");
                    _watchers.Add(watcher);
                    Log($"watching {dir}");
                }
                catch (Exception ex)
                {
                    Log($"watch failed for {dir}: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            foreach (var watcher in _watchers)
            {
                try { watcher.Dispose(); } catch { }
            }
            _watchers.Clear();
            _visionLock.Dispose();
        }

        private void Queue(string path, string reason)
        {
            if (!IsImage(path) && !IsDocument(path))
                return;

            var now = DateTime.UtcNow;
            if (_seen.TryGetValue(path, out var last) && now - last < TimeSpan.FromSeconds(30))
                return;

            _seen[path] = now;
            Log($"queued {reason}: {path}");
            _ = Task.Run(() => IsImage(path)
                ? AnalyzeImageAsync(path)
                : AnalyzeDocumentAsync(path));
        }

        private async Task AnalyzeImageAsync(string path)
        {
            await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

            if (!await WaitForStableFileAsync(path).ConfigureAwait(false))
            {
                Log($"skipped unstable or missing image: {path}");
                return;
            }

            await _visionLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var file = new FileInfo(path);
                if (!file.Exists)
                    return;
                if (file.Length <= 0 || file.Length > 15 * 1024 * 1024)
                {
                    Log($"skipped image size {file.Length}: {path}");
                    return;
                }

                var bytes = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
                var prompt = BuildPrompt(path);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
                var raw = await _llmFactory().SendSystemVisionQueryAsync(
                    prompt,
                    bytes,
                    MimeFromPath(path),
                    cts.Token,
                    maxTokensOverride: 1024).ConfigureAwait(false);

                var comment = ExtractComment(raw);
                if (string.IsNullOrWhiteSpace(comment))
                {
                    Log($"vision no-comment: {path}");
                    return;
                }

                _chatRepository.InsertMessage(new ChatRepository.ChatMessage
                {
                    Role = "assistant",
                    Author = "Kokonoe",
                    Content = comment,
                    Timestamp = DateTime.Now
                });

                try { _chatLoggerFactory()?.LogOutgoing("file_watcher", comment, "photo_vision"); } catch { }
                Log($"commented on image: {path}");
            }
            catch (OperationCanceledException)
            {
                Log($"vision timeout: {path}");
            }
            catch (Exception ex)
            {
                Log($"vision failed for {path}: {ex.Message}");
            }
            finally
            {
                _visionLock.Release();
            }
        }

        private static async Task<bool> WaitForStableFileAsync(string path)
        {
            long lastSize = -1;
            for (var i = 0; i < 8; i++)
            {
                try
                {
                    var info = new FileInfo(path);
                    if (!info.Exists)
                        return false;

                    if (info.Length > 0 && info.Length == lastSize)
                        return true;

                    lastSize = info.Length;
                }
                catch { }

                await Task.Delay(500).ConfigureAwait(false);
            }
            return false;
        }

        private static IEnumerable<string> GetDefaultImageFolders()
        {
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (!string.IsNullOrWhiteSpace(pictures))
                yield return pictures;
            if (!string.IsNullOrWhiteSpace(profile))
                yield return Path.Combine(profile, "Downloads");
        }

        private static bool IsImage(string path)
            => ImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

        private static bool IsDocument(string path)
            => DocumentExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

        private static string MimeFromPath(string path) => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "image/jpeg"
        };

        private static string BuildPrompt(string path)
            => $"""
You are a proactive desktop photo watcher.

A new local image appeared at: {Path.GetFileName(path)}

Inspect the image. If it is private, low-signal, a plain screenshot with no useful event, or not relevant enough to comment on, answer exactly:
NO_COMMENT

Otherwise answer with one concise Ukrainian Kokonoe-style observation that can be inserted into chat. Do not mention file paths. Do not expose private identifiers. Be specific about what changed or why it matters.
""";

        private async Task AnalyzeDocumentAsync(string path)
        {
            await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            if (!await WaitForStableFileAsync(path).ConfigureAwait(false))
            {
                Log($"skipped unstable or missing document: {path}");
                return;
            }

            await _visionLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var file = new FileInfo(path);
                if (!file.Exists)
                    return;
                if (file.Length <= 0 || file.Length > 25 * 1024 * 1024)
                {
                    Log($"skipped document size {file.Length}: {path}");
                    return;
                }

                var text = await ExtractDocumentTextAsync(path).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(text))
                {
                    Log($"document had no extractable text: {path}");
                    return;
                }

                var prompt = BuildDocumentPrompt(path, text);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
                var raw = await _llmFactory().SendSystemQueryAsync(prompt, ct: cts.Token).ConfigureAwait(false);
                var summary = ExtractDocumentSummary(raw);
                if (string.IsNullOrWhiteSpace(summary))
                {
                    Log($"document summary empty: {path}");
                    return;
                }

                _memoryFactory()?.RecordEpisodeBlocking(
                    $"Document intake: {Path.GetFileName(path)} - {summary}",
                    "focused",
                    0.58f,
                    new[] { "document", "file-watcher", Path.GetExtension(path).Trim('.').ToLowerInvariant() });

                try
                {
                    var notePath = $"Kokonoe/File Intake/{DateTime.Now:yyyy-MM-dd HHmm} - {SanitizeTitle(Path.GetFileNameWithoutExtension(path))}.md";
                    _obsidianFactory()?.WriteNote(notePath, BuildDocumentNote(path, summary));
                }
                catch (Exception ex)
                {
                    Log($"document note write failed: {ex.Message}");
                }

                try { _chatLoggerFactory()?.LogOutgoing("file_watcher", $"Document summarized: {Path.GetFileName(path)}", "document_intake"); } catch { }
                Log($"document summarized and stored: {path}");
            }
            catch (OperationCanceledException)
            {
                Log($"document summary timeout: {path}");
            }
            catch (Exception ex)
            {
                Log($"document summary failed for {path}: {ex.Message}");
            }
            finally
            {
                _visionLock.Release();
            }
        }

        private static async Task<string> ExtractDocumentTextAsync(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".pdf")
                return ExtractPdfLooseText(await File.ReadAllBytesAsync(path).ConfigureAwait(false));

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var buffer = new char[Math.Min(80_000, Math.Max(4096, (int)Math.Min(stream.Length, 80_000)))];
            var read = await reader.ReadBlockAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            return NormalizeExtractedText(new string(buffer, 0, read));
        }

        private static string ExtractPdfLooseText(byte[] bytes)
        {
            if (bytes.Length == 0)
                return "";
            var raw = Encoding.Latin1.GetString(bytes);
            var matches = Regex.Matches(raw, @"\((?<text>(?:\\.|[^\\)]){3,})\)");
            var parts = matches
                .Cast<Match>()
                .Select(m => m.Groups["text"].Value)
                .Select(UnescapePdfLiteral)
                .Where(t => t.Count(char.IsLetterOrDigit) >= 3)
                .Take(600);
            return NormalizeExtractedText(string.Join(" ", parts));
        }

        private static string UnescapePdfLiteral(string value)
            => value.Replace("\\(", "(")
                .Replace("\\)", ")")
                .Replace("\\\\", "\\")
                .Replace("\\n", " ")
                .Replace("\\r", " ")
                .Replace("\\t", " ");

        private static string NormalizeExtractedText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";
            text = Regex.Replace(text, @"\s+", " ").Trim();
            return text.Length > 30_000 ? text[..30_000] : text;
        }

        private static string BuildDocumentPrompt(string path, string text)
            => $"""
Summarize this newly detected local document for Kokonoe's memory.

File name: {Path.GetFileName(path)}

Rules:
- Do not invent content that is not in the extracted text.
- If the text is too sparse or unreadable, answer exactly: NO_SUMMARY
- Return Ukrainian.
- Include: one-sentence summary, key facts, why it might matter later.
- Keep it under 900 characters.

Extracted text:
{text}
""";

        private static string ExtractDocumentSummary(string? raw)
        {
            var text = (raw ?? "").Trim();
            if (string.IsNullOrWhiteSpace(text) || text.Contains("NO_SUMMARY", StringComparison.OrdinalIgnoreCase))
                return "";
            text = text.Replace("\r", " ").Replace("\n", " ").Trim().Trim('"', '\'', '`');
            while (text.Contains("  ", StringComparison.Ordinal))
                text = text.Replace("  ", " ");
            return text.Length > 1200 ? text[..1200].Trim() : text;
        }

        private static string BuildDocumentNote(string path, string summary)
            => $"""
---
created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
tags: [kokonoe, file-intake, document]
source_file: {Path.GetFileName(path)}
---

# {Path.GetFileName(path)}

{summary}
""";

        private static string SanitizeTitle(string title)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = title.Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray();
            var result = new string(chars).Trim();
            return string.IsNullOrWhiteSpace(result) ? "document" : result[..Math.Min(result.Length, 80)];
        }

        private static string ExtractComment(string? raw)
        {
            var text = (raw ?? "").Trim();
            if (string.IsNullOrWhiteSpace(text))
                return "";
            if (text.Contains("NO_COMMENT", StringComparison.OrdinalIgnoreCase))
                return "";

            text = text.Replace("\r", " ").Replace("\n", " ").Trim().Trim('"', '\'', '`');
            while (text.Contains("  ", StringComparison.Ordinal))
                text = text.Replace("  ", " ");
            return text.Length > 360 ? text[..360].Trim() : text;
        }

        private void Log(string message)
        {
            try
            {
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [PHOTO_WATCHER] {message}{Environment.NewLine}";
                File.AppendAllText(Path.Combine(_dataDir, "photo-watcher.log"), line);
            }
            catch { }
            KokoSystemLog.Write("PHOTO_WATCHER", message);
        }
    }
}
