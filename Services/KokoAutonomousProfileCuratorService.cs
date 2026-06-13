using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KokonoeAssistant.Services
{
    public sealed class KokoAutonomousProfileCuratorService : IDisposable
    {
        private readonly string _dataDir;
        private readonly ChatRepository _chat;
        private readonly ObsidianMcpService _obsidian;
        private readonly KokoProfileUpdateService _profileUpdater;
        private readonly Func<LlmService?> _llmFactory;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly System.Threading.Timer _timer;
        private readonly string _statePath;
        private bool _started;
        private CuratorState _state;

        public KokoAutonomousProfileCuratorService(
            string dataDir,
            ChatRepository chat,
            ObsidianMcpService obsidian,
            KokoProfileUpdateService profileUpdater,
            Func<LlmService?> llmFactory)
        {
            _dataDir = dataDir;
            Directory.CreateDirectory(_dataDir);
            _chat = chat;
            _obsidian = obsidian;
            _profileUpdater = profileUpdater;
            _llmFactory = llmFactory;
            _statePath = Path.Combine(_dataDir, "autonomous-profile-curator.json");
            _state = LoadState();
            _timer = new System.Threading.Timer(_ => _ = RunOnceAsync("periodic-sweep"), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        public void Start()
        {
            if (_started) return;
            _started = true;
            _timer.Change(TimeSpan.FromSeconds(90), TimeSpan.FromMinutes(12));
            KokoSystemLog.Write("PROFILE_CURATOR", "started");
        }

        public Task ObserveExchangeAsync(string userText, string assistantText, string source = "app", CancellationToken ct = default)
        {
            if (!ShouldTriggerFromExchange(userText, assistantText))
                return Task.CompletedTask;

            return RunOnceAsync("exchange:" + source + ":" + BuildTriggerSummary(userText, assistantText), force: false, ct);
        }

        public async Task<KokoProfileUpdateResult?> RunOnceAsync(string reason, bool force = false, CancellationToken ct = default)
        {
            if (!await _gate.WaitAsync(0, ct).ConfigureAwait(false))
                return null;

            try
            {
                var now = DateTime.UtcNow;
                if (!force && _state.LastRunUtc.HasValue && now - _state.LastRunUtc.Value < TimeSpan.FromMinutes(5))
                {
                    KokoSystemLog.Write("PROFILE_CURATOR", "skip throttle; reason=" + reason);
                    return null;
                }

                _state.LastRunUtc = now;
                var recent = _chat.GetMessages(80)
                    .Where(m => !string.IsNullOrWhiteSpace(m.Content))
                    .OrderBy(m => m.Timestamp)
                    .ToList();
                if (recent.Count == 0)
                {
                    SaveState();
                    return null;
                }

                var lastMessageUtc = recent.Max(m => m.Timestamp.ToUniversalTime());
                if (!force &&
                    _state.LastSeenMessageUtc.HasValue &&
                    lastMessageUtc <= _state.LastSeenMessageUtc.Value &&
                    (!_state.LastUpdateUtc.HasValue || now - _state.LastUpdateUtc.Value < TimeSpan.FromHours(6)))
                {
                    SaveState();
                    KokoSystemLog.Write("PROFILE_CURATOR", "skip no new messages; reason=" + reason);
                    return null;
                }

                _state.LastSeenMessageUtc = lastMessageUtc;
                var llm = _llmFactory();
                var decision = llm == null
                    ? CuratorDecision.FromHeuristic(recent.Any(m => ShouldTriggerFromExchange(m.Content, "")), reason)
                    : await DecideWithLlmAsync(llm, recent, reason, ct).ConfigureAwait(false);

                if (!force && !decision.ShouldUpdate)
                {
                    WriteCuratorStatus("Skipped", decision.Reason, recent.Count);
                    SaveState();
                    KokoSystemLog.Write("PROFILE_CURATOR", "skip decision=false; " + decision.Reason);
                    return null;
                }

                var instruction = "[auto-profile-curator] " + decision.Instruction;
                var result = await _profileUpdater.UpdateProfileFromRecentContextAsync(instruction, llm, ct).ConfigureAwait(false);
                if (result.Success)
                {
                    _state.LastUpdateUtc = now;
                    _state.UpdateCount++;
                }
                WriteCuratorStatus(result.Success ? "Updated" : "Failed", decision.Reason + " / " + result.Error, recent.Count);
                SaveState();
                KokoSystemLog.Write("PROFILE_CURATOR", $"update success={result.Success}; llm={result.UsedLlmSynthesis}; reason={decision.Reason}");
                return result;
            }
            catch (Exception ex)
            {
                KokoSystemLog.Write("PROFILE_CURATOR", "run failed: " + ex.Message);
                return null;
            }
            finally
            {
                _gate.Release();
            }
        }

        public static bool ShouldTriggerFromExchange(string? userText, string? assistantText)
        {
            var text = ((userText ?? "") + "\n" + (assistantText ?? "")).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(text)) return false;

            if (ContainsAny(text,
                "promise", "promised", "ledger", "obligation", "autonomy", "roleplay", "scripted",
                "обіц", "обещ", "викона", "зроблю", "зроби все", "сама контрол", "оновлюй обсидіан", "онови профіль"))
                return true;

            return ContainsAny(text,
                "запам", "запиши", "пам'ят", "память", "факт",
                "профіль", "профиль", "obsidian", "vault",
                "мені не подоба", "я хочу", "я не хочу", "моє правило", "налаштування",
                "проєкт", "проект", "план", "пріоритет", "обов'яз", "дедлайн",
                "watch", "годинник", "пульс", "датчик", "bridge", "манус", "manus",
                "рольплей", "ролплей", "автоном", "сама дум", "самостійно");
        }

        public static string BuildTriggerSummary(string? userText, string? assistantText)
        {
            var text = RegexOneLine((userText ?? "") + " " + (assistantText ?? ""));
            return text.Length <= 180 ? text : text[..179] + "…";
        }

        private async Task<CuratorDecision> DecideWithLlmAsync(
            LlmService llm,
            System.Collections.Generic.IReadOnlyList<ChatRepository.ChatMessage> recent,
            string reason,
            CancellationToken ct)
        {
            var prompt = BuildDecisionPrompt(recent, reason);
            try
            {
                var raw = await llm.SendSystemQueryAsync(prompt, useTools: false, ct: ct, agentId: "system").ConfigureAwait(false);
                var json = ExtractJson(raw);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var obj = JObject.Parse(json);
                    var should = obj["shouldUpdate"]?.Value<bool>() == true;
                    var why = obj["reason"]?.ToString() ?? reason;
                    var instruction = obj["instruction"]?.ToString();
                    if (string.IsNullOrWhiteSpace(instruction))
                        instruction = "Онови профіль на основі нових стабільних фактів і правил з останнього контексту.";
                    return new CuratorDecision(should, why, instruction);
                }
            }
            catch (Exception ex)
            {
                KokoSystemLog.Write("PROFILE_CURATOR", "LLM decision failed: " + ex.Message);
            }

            var heuristic = recent.Any(m => ShouldTriggerFromExchange(m.Content, ""));
            return CuratorDecision.FromHeuristic(heuristic, reason);
        }

        private static string BuildDecisionPrompt(System.Collections.Generic.IReadOnlyList<ChatRepository.ChatMessage> recent, string reason)
        {
            var sb = new StringBuilder();
            sb.AppendLine("AUTONOMOUS OBSIDIAN PROFILE CURATOR");
            sb.AppendLine("Decide whether the user's Obsidian profile should be updated now.");
            sb.AppendLine("Return ONLY JSON: {\"shouldUpdate\":true|false,\"reason\":\"...\",\"instruction\":\"...\"}");
            sb.AppendLine("Update only for durable facts, explicit preferences, project priorities, operating rules, active obligations, or important wearable/system integration changes.");
            sb.AppendLine("Active obligations include promises Kokonoe made to actually edit files, update Obsidian, run tests, commit, push, or report artifacts.");
            sb.AppendLine("Prefer synthesized facts and operating rules. Never store persona theater, dominance/flirting filler, insults, or raw chat as profile facts.");
            sb.AppendLine("Do not update for greetings, transient mood, raw sexual/private lines, or noise.");
            sb.AppendLine("If updating, instruction must say what durable synthesis to write, not raw chat lines.");
            sb.AppendLine("Trigger reason: " + reason);
            sb.AppendLine("Recent messages:");
            foreach (var msg in recent.TakeLast(35))
                sb.AppendLine("- " + msg.Timestamp.ToString("yyyy-MM-dd HH:mm") + " " + msg.Role + ": " + RegexOneLine(msg.Content, 260));
            return sb.ToString();
        }

        private void WriteCuratorStatus(string status, string reason, int contextCount)
        {
            var content = $"""
---
type: automation-status
updated: {DateTime.Now:yyyy-MM-dd HH:mm}
managed-by: KokoAutonomousProfileCuratorService
tags: [kokonoe, automation, profile-curator]
---

# Автокуратор профілю

- Статус: {status}
- Причина: {reason}
- Контекстних повідомлень: {contextCount}
- Останній запуск UTC: {_state.LastRunUtc:yyyy-MM-dd HH:mm:ss}
- Останнє оновлення UTC: {_state.LastUpdateUtc:yyyy-MM-dd HH:mm:ss}
- Успішних оновлень: {_state.UpdateCount}

Ця нотатка службова. Людський профіль не повинен містити сирий чат або службовий changelog.
""";
            try { _obsidian.WriteNote("Kokonoe/Automation/Profile Curator.md", content); } catch { }
        }

        private CuratorState LoadState()
        {
            try
            {
                if (!File.Exists(_statePath)) return new CuratorState();
                return JsonConvert.DeserializeObject<CuratorState>(File.ReadAllText(_statePath)) ?? new CuratorState();
            }
            catch { return new CuratorState(); }
        }

        private void SaveState()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);
                File.WriteAllText(_statePath, JsonConvert.SerializeObject(_state, Formatting.Indented));
            }
            catch { }
        }

        private static string? ExtractJson(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var text = raw.Trim();
            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            return start >= 0 && end > start ? text[start..(end + 1)] : null;
        }

        private static string RegexOneLine(string? text, int max = 220)
        {
            var value = System.Text.RegularExpressions.Regex.Replace(text ?? "", @"\s+", " ").Trim();
            return value.Length <= max ? value : value[..Math.Max(0, max - 1)].TrimEnd() + "…";
        }

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));

        public void Dispose()
        {
            _timer.Dispose();
            _gate.Dispose();
        }

        private sealed class CuratorState
        {
            public DateTime? LastRunUtc { get; set; }
            public DateTime? LastSeenMessageUtc { get; set; }
            public DateTime? LastUpdateUtc { get; set; }
            public int UpdateCount { get; set; }
        }

        private readonly record struct CuratorDecision(bool ShouldUpdate, string Reason, string Instruction)
        {
            public static CuratorDecision FromHeuristic(bool shouldUpdate, string reason) => new(
                shouldUpdate,
                shouldUpdate ? "heuristic profile-relevant signal: " + reason : "no durable profile signal",
                "Онови профіль на основі нових стабільних фактів, правил і технічних пріоритетів з останнього контексту; не копіюй сирий чат.");
        }
    }
}
