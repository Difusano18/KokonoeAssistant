using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace KokonoeAssistant.Services
{
    public sealed class KokoResearchService : IDisposable
    {
        private static readonly HttpClient Http = new()
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        private readonly string _dataDir;
        private readonly SearchService _localSearch;
        private readonly ChatRepository _chat;
        private readonly ObsidianMcpService _obsidian;
        private readonly KokoInternalBlackboardService _blackboard;
        private readonly KokoServiceHeartbeatService _heartbeat;
        private readonly Func<KokoInternalState?> _stateFactory;
        private readonly System.Threading.Timer _timer;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly string _statePath;
        private ResearchState _state;
        private bool _started;

        public KokoResearchService(
            string dataDir,
            SearchService localSearch,
            ChatRepository chat,
            ObsidianMcpService obsidian,
            KokoInternalBlackboardService blackboard,
            KokoServiceHeartbeatService heartbeat,
            Func<KokoInternalState?> stateFactory)
        {
            _dataDir = dataDir;
            Directory.CreateDirectory(_dataDir);
            _localSearch = localSearch;
            _chat = chat;
            _obsidian = obsidian;
            _blackboard = blackboard;
            _heartbeat = heartbeat;
            _stateFactory = stateFactory;
            _statePath = Path.Combine(_dataDir, "research-state.json");
            _state = LoadState();
            _timer = new System.Threading.Timer(_ => _ = RunOnceAsync("timer"), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        public void Start()
        {
            if (_started) return;
            _started = true;
            _timer.Change(TimeSpan.FromMinutes(3), TimeSpan.FromHours(4));
            _heartbeat.Update("RESEARCH", "armed", "topic tracker online");
            KokoSystemLog.Write("RESEARCH", "started");
        }

        public async Task<ResearchRunResult> RunOnceAsync(string reason, bool force = false, CancellationToken ct = default)
        {
            if (!await _gate.WaitAsync(0, ct).ConfigureAwait(false))
                return new ResearchRunResult { Skipped = true, Summary = "research already running" };

            try
            {
                var now = DateTime.UtcNow;
                var topics = CollectTopicCandidates(_stateFactory()?.TopicFrequency, _obsidian.VaultPath, _chat.GetMessages(120))
                    .Where(t => force || !_state.TopicLastCheckedUtc.TryGetValue(t, out var last) || now - last > TimeSpan.FromHours(18))
                    .Take(4)
                    .ToList();

                if (topics.Count == 0)
                {
                    _heartbeat.Update("RESEARCH", "idle", "no due topics");
                    return new ResearchRunResult { Skipped = true, Summary = "no due topics" };
                }

                var findings = new List<ResearchFinding>();
                foreach (var topic in topics)
                {
                    ct.ThrowIfCancellationRequested();
                    var topicFindings = await ResearchTopicAsync(topic, ct).ConfigureAwait(false);
                    findings.AddRange(topicFindings);
                    _state.TopicLastCheckedUtc[topic] = now;
                }

                _state.LastRunUtc = now;
                _state.RunCount++;
                WriteResearchNote(reason, topics, findings);
                MaybeWriteMorningBriefing(now, topics, findings);
                SaveState();

                var summary = $"researched {topics.Count} topics; findings={findings.Count}; reason={reason}";
                AppendActionMessage($"[ACTION:research] {summary}");
                _blackboard.Publish("research-agent", "topic_scan", summary, findings.Count > 0 ? 0.78 : 0.35);
                _heartbeat.Update("RESEARCH", "updated", summary);
                KokoSystemLog.Write("RESEARCH", summary);
                return new ResearchRunResult { Summary = summary, Topics = topics, Findings = findings };
            }
            catch (Exception ex)
            {
                _heartbeat.Update("RESEARCH", "error", ex.Message);
                KokoSystemLog.Write("RESEARCH", "run failed: " + ex.Message);
                return new ResearchRunResult { Skipped = true, Summary = ex.Message };
            }
            finally
            {
                _gate.Release();
            }
        }

        public static IReadOnlyList<string> CollectTopicCandidates(
            IReadOnlyDictionary<string, int>? topicFrequency,
            string? vaultPath,
            IReadOnlyList<ChatRepository.ChatMessage>? recentMessages)
        {
            var scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            if (topicFrequency != null)
            {
                foreach (var pair in topicFrequency.OrderByDescending(p => p.Value).Take(20))
                    AddTopic(scores, pair.Key, Math.Clamp(pair.Value, 1, 20));
            }

            if (!string.IsNullOrWhiteSpace(vaultPath) && Directory.Exists(vaultPath))
            {
                foreach (var file in Directory.EnumerateFiles(vaultPath, "*.md", SearchOption.AllDirectories).Take(250))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (LooksResearchableTopic(name))
                        AddTopic(scores, name, 3);

                    string text;
                    try { text = File.ReadAllText(file); }
                    catch { continue; }

                    foreach (Match m in Regex.Matches(text, @"(?im)^\s*[-*]\s*(?:interest|topic|watch|research|цікав|інтерес)\s*:\s*(.+)$"))
                        AddTopic(scores, CleanupTopic(m.Groups[1].Value), 8);

                    foreach (Match m in Regex.Matches(text, @"(?im)^#{1,3}\s*(?:Interests|Інтереси|Research|Дослідження)\s*$([\s\S]{0,900})"))
                    {
                        foreach (Match item in Regex.Matches(m.Groups[1].Value, @"(?m)^\s*[-*]\s+(.+)$"))
                            AddTopic(scores, CleanupTopic(item.Groups[1].Value), 6);
                    }
                }
            }

            if (recentMessages != null)
            {
                foreach (var msg in recentMessages.TakeLast(60))
                {
                    foreach (Match m in Regex.Matches(msg.Content ?? "", @"\b([A-Z][A-Za-z0-9+#.\-]{2,}(?:\s+[A-Z][A-Za-z0-9+#.\-]{2,}){0,2})\b"))
                        AddTopic(scores, CleanupTopic(m.Groups[1].Value), 1);
                }
            }

            return scores
                .Where(p => LooksResearchableTopic(p.Key))
                .OrderByDescending(p => p.Value)
                .ThenBy(p => p.Key)
                .Select(p => p.Key)
                .Take(12)
                .ToList();
        }

        private async Task<List<ResearchFinding>> ResearchTopicAsync(string topic, CancellationToken ct)
        {
            var findings = new List<ResearchFinding>();
            var local = _localSearch.Search(topic, limit: 3)
                .Select(r => new ResearchFinding
                {
                    Topic = topic,
                    Title = r.Title,
                    Url = r.SourcePath,
                    Snippet = r.Preview,
                    Source = "local"
                });
            findings.AddRange(local);

            foreach (var web in await SearchDuckDuckGoAsync(topic, ct).ConfigureAwait(false))
                findings.Add(web);

            return findings
                .GroupBy(f => $"{f.Source}|{f.Title}|{f.Url}", StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .Take(8)
                .ToList();
        }

        private static async Task<List<ResearchFinding>> SearchDuckDuckGoAsync(string topic, CancellationToken ct)
        {
            var results = new List<ResearchFinding>();
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, "https://duckduckgo.com/html/?q=" + Uri.EscapeDataString(topic + " news updates"));
                req.Headers.UserAgent.ParseAdd("KokonoeAssistant/1.0");
                var html = await Http.SendAsync(req, ct).ConfigureAwait(false);
                var body = await html.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                if (!html.IsSuccessStatusCode || string.IsNullOrWhiteSpace(body))
                    return results;

                foreach (Match m in Regex.Matches(body, "<a[^>]+class=\"result__a\"[^>]+href=\"(?<url>[^\"]+)\"[^>]*>(?<title>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline).Take(5))
                {
                    var title = WebUtility.HtmlDecode(StripTags(m.Groups["title"].Value)).Trim();
                    var url = WebUtility.HtmlDecode(m.Groups["url"].Value).Trim();
                    if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url))
                        continue;
                    results.Add(new ResearchFinding
                    {
                        Topic = topic,
                        Title = title,
                        Url = url,
                        Snippet = "",
                        Source = "web"
                    });
                }
            }
            catch (Exception ex)
            {
                KokoSystemLog.Write("RESEARCH", $"web search failed for '{topic}': {ex.Message}");
            }

            return results;
        }

        private void WriteResearchNote(string reason, IReadOnlyList<string> topics, IReadOnlyList<ResearchFinding> findings)
        {
            var sb = new StringBuilder();
            sb.AppendLine("---");
            sb.AppendLine("type: autonomous-research");
            sb.AppendLine($"updated: {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine("managed-by: KokoResearchService");
            sb.AppendLine("tags: [kokonoe, research, automation]");
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("# Autonomous Research");
            sb.AppendLine();
            sb.AppendLine("- Reason: " + reason);
            sb.AppendLine("- Topics: " + string.Join(", ", topics));
            sb.AppendLine();
            foreach (var group in findings.GroupBy(f => f.Topic))
            {
                sb.AppendLine("## " + group.Key);
                foreach (var f in group)
                    sb.AppendLine($"- [{f.Source}] {f.Title} — {Trim(f.Snippet, 160)} {f.Url}".Trim());
                sb.AppendLine();
            }

            try { _obsidian.WriteNote("Kokonoe/Research/Autonomous Research.md", sb.ToString()); }
            catch (Exception ex) { KokoSystemLog.Write("RESEARCH", "write note failed: " + ex.Message); }
        }

        private void MaybeWriteMorningBriefing(DateTime nowUtc, IReadOnlyList<string> topics, IReadOnlyList<ResearchFinding> findings)
        {
            var local = nowUtc.ToLocalTime();
            if (local.Hour is < 6 or > 11)
                return;
            if (_state.LastMorningBriefingLocalDate == local.Date)
                return;

            _state.LastMorningBriefingLocalDate = local.Date;
            var sb = new StringBuilder();
            sb.AppendLine("# Morning Briefing");
            sb.AppendLine();
            sb.AppendLine($"Generated: {local:yyyy-MM-dd HH:mm}");
            sb.AppendLine();
            sb.AppendLine("## Watched Topics");
            foreach (var topic in topics)
                sb.AppendLine("- " + topic);
            sb.AppendLine();
            sb.AppendLine("## Findings");
            foreach (var f in findings.Take(12))
                sb.AppendLine($"- {f.Topic}: [{f.Source}] {f.Title} {f.Url}".Trim());

            try { _obsidian.WriteNote($"Kokonoe/Research/Morning Briefing {local:yyyy-MM-dd}.md", sb.ToString()); }
            catch (Exception ex) { KokoSystemLog.Write("RESEARCH", "morning briefing failed: " + ex.Message); }
        }

        private void AppendActionMessage(string message)
        {
            try
            {
                _chat.InsertMessage(new ChatRepository.ChatMessage
                {
                    Role = "assistant",
                    Author = "Kokonoe",
                    Content = message,
                    Timestamp = DateTime.Now
                });
                EventBus.PublishChatMessage(message, "assistant");
            }
            catch { }
        }

        private ResearchState LoadState()
        {
            try
            {
                return File.Exists(_statePath)
                    ? JsonConvert.DeserializeObject<ResearchState>(File.ReadAllText(_statePath)) ?? new ResearchState()
                    : new ResearchState();
            }
            catch { return new ResearchState(); }
        }

        private void SaveState()
        {
            try { File.WriteAllText(_statePath, JsonConvert.SerializeObject(_state, Formatting.Indented)); }
            catch { }
        }

        private static void AddTopic(Dictionary<string, int> scores, string? raw, int score)
        {
            var topic = CleanupTopic(raw);
            if (!LooksResearchableTopic(topic))
                return;
            scores[topic] = scores.TryGetValue(topic, out var old) ? old + score : score;
        }

        private static string CleanupTopic(string? raw)
        {
            var text = Regex.Replace(raw ?? "", @"[`*_#>\[\](){}:;]+", " ");
            text = Regex.Replace(text, @"\s+", " ").Trim();
            if (text.Length > 80) text = text[..80].TrimEnd();
            return text;
        }

        private static bool LooksResearchableTopic(string? topic)
        {
            var value = (topic ?? "").Trim();
            if (value.Length < 3 || value.Length > 80)
                return false;
            var lower = value.ToLowerInvariant();
            if (new[] { "the", "and", "user", "kokonoe", "today", "привіт", "дякую" }.Contains(lower))
                return false;
            return Regex.IsMatch(value, @"[\p{L}\p{N}]");
        }

        private static string StripTags(string html)
            => Regex.Replace(html ?? "", "<.*?>", " ");

        private static string Trim(string? text, int max)
        {
            text = (text ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= max ? text : text[..max].TrimEnd() + "...";
        }

        public void Dispose()
        {
            _timer.Dispose();
            _gate.Dispose();
        }

        private sealed class ResearchState
        {
            public DateTime LastRunUtc { get; set; } = DateTime.MinValue;
            public DateTime LastMorningBriefingLocalDate { get; set; } = DateTime.MinValue;
            public int RunCount { get; set; }
            public Dictionary<string, DateTime> TopicLastCheckedUtc { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }
    }

    public sealed class ResearchRunResult
    {
        public bool Skipped { get; set; }
        public string Summary { get; set; } = "";
        public List<string> Topics { get; set; } = new();
        public List<ResearchFinding> Findings { get; set; } = new();
    }

    public sealed class ResearchFinding
    {
        public string Topic { get; set; } = "";
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public string Snippet { get; set; } = "";
        public string Source { get; set; } = "";
    }
}
