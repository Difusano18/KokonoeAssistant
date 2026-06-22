using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Telegram.Bot;

namespace KokonoeAssistant.Services
{
    public partial class KokoBrainEngine
    {

        private async Task TryDailySelfReviewAsync(string source)
        {
            try
            {
                var now = DateTime.Now;
                if (_state.LastDailySelfReviewAt.Date == now.Date || now.Hour < 4)
                    return;

                var autonomyTail = string.Join("\n", _state.AutonomyDecisionLog.TakeLast(12).Select(x => "- " + x));
                var observations = string.Join("\n", _state.Observations.TakeLast(16).Select(x => "- " + x));
                var stabilityDelta = _state.VisionFailureCount >= 3 ? -0.03f : 0.01f;
                _state.MoodFactors["self_review_stability"] = Math.Clamp(
                    (_state.MoodFactors.TryGetValue("self_review_stability", out var old) ? old : 0f) + stabilityDelta,
                    -0.25f,
                    0.25f);

                var summary = $"vision_failures={_state.VisionFailureCount}; resource={_state.LastResourceGuardianSummary}; wearable={ServiceContainer.WearableTelemetry.State.Summary}";
                var path = $"Kokonoe/Daily Reviews/{now:yyyy-MM-dd} - autonomous self review.md";
                var body = $"""
---
type: daily-self-review
tags: [kokonoe, self-review, autonomy]
created: {now:O}
source: {source}
---

# Autonomous Self Review

Summary: {summary}

## Autonomy Decisions

{autonomyTail}

## Observations

{observations}

## Adjustment

- mood factor `self_review_stability`: {_state.MoodFactors["self_review_stability"]:F2}
- vision failure count considered: {_state.VisionFailureCount}
""";
                await Task.Run(() => _obsidian.WriteNote(path, body)).ConfigureAwait(false);
                lock (_lock)
                {
                    _state.LastDailySelfReviewAt = now;
                    _state.LastDailySelfReviewSummary = summary;
                    SaveState();
                }
                _blackboard.Publish("brain-agent", "daily_self_review", summary, 0.72);
                ServiceContainer.Heartbeat.Update("SELF_REVIEW", "written", path);
                Log($"Daily self-review wrote {path}");
            }
            catch (Exception ex)
            {
                Log($"TryDailySelfReviewAsync: {ex.Message}");
            }
        }

        // ------------------------------------------------------------
        // АНАЛІЗ ЕМОЦІЙ + РЕАКТИВНІ ТРИГЕРИ
        // ------------------------------------------------------------

        private async Task AnalyzeRecentEmotionsAsync()
        {
            try
            {
                var recentUser = _chatRepo.GetMessages(10)
                    .Where(m => m.Role == "user")
                    .OrderByDescending(m => m.Timestamp)
                    .Take(5)
                    .ToList();

                if (!recentUser.Any()) return;

                var lastMsg  = recentUser.First();
                var lastText = lastMsg.Content;

                // Позначаємо кінець розмови якщо остання активність > 10 хв тому
                if ((DateTime.Now - lastMsg.Timestamp).TotalMinutes > 10 &&
                    lastMsg.Timestamp > _state.LastConversationEndAt)
                {
                    _state.LastConversationEndAt = lastMsg.Timestamp;
                }

                // Простий prompt для аналізу тону
                var snippets = string.Join("\n", recentUser.Select(m =>
                    $"- {m.Content[..Math.Min(120, m.Content.Length)]}"));

                var prompt = $@"Проаналізуй емоційний тон цих повідомлень (від нього до Kokonoe):
{snippets}

Відповідь СТРОГО одним словом з набору: anxious / stressed / sad / tired / neutral / calm / happy / excited
Тільки одне слово, нічого більше.";

                var tone = await _llm.SendSystemQueryAsync(prompt, useTools: true);
                if (string.IsNullOrWhiteSpace(tone)) return;

                tone = tone.Trim().ToLower().Split(' ', '\n')[0];
                var validTones = new[] { "anxious", "stressed", "sad", "tired", "neutral", "calm", "happy", "excited" };
                if (!validTones.Contains(tone)) tone = "neutral";

                var prevTone = _state.LastUserEmotionalTone;
                _state.LastUserEmotionalTone = tone;

                // ── Оновити KokoEmotionEngine ─────────────────────────
                Emotion.UpdateFromUserTone(tone);
                Patterns.RecordActivity(wasActive: true, tone: tone, messageCount: recentUser.Count);

                // ── Когнітивний цикл (Working Memory + User Model + Salience) ──
                try
                {
                    var lastUserMsg = recentUser.LastOrDefault()?.Content ?? "";
                    Cognition.ProcessUserMessage(lastUserMsg, tone, Emotion.GetStatusLine());
                }
                catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "AnalyzeRecentEmotionsAsync failed near source line 3021: " + ex); }

                // Якщо розмова хороша — підвищити connection
                if (tone is "happy" or "excited" or "neutral" && recentUser.Count >= 3)
                    Emotion.OnGoodConversation();

                // ── Реактивний тригер: якщо тривожний/сумний → через 2 год перевірити
                if ((tone == "anxious" || tone == "stressed" || tone == "sad") &&
                    prevTone != tone && // тільки якщо тон змінився
                    !_state.PendingTriggers.Any(t => t.Type == "anxious_followup" && t.FireAt > DateTime.Now))
                {
                    _state.PendingTriggers.Add(new ReactiveTrigger
                    {
                        Type    = "anxious_followup",
                        Context = $"Він писав з тоном '{tone}': «{lastText[..Math.Min(100, lastText.Length)]}»",
                        FireAt  = DateTime.Now.AddHours(2)
                    });
                    Log($"Reactive trigger set: anxious_followup in 2h (tone={tone})");
                }

                // Реактивний тригер: якщо згадав тему/проект → наступного дня нагадати
                if (tone == "happy" || tone == "excited")
                {
                    // Зберегти топік для можливого follow-up
                    if (!_state.PendingTriggers.Any(t => t.Type == "topic_followup" && t.FireAt > DateTime.Now))
                    {
                        _state.PendingTriggers.Add(new ReactiveTrigger
                        {
                            Type    = "topic_followup",
                            Context = lastText[..Math.Min(150, lastText.Length)],
                            FireAt  = DateTime.Now.AddHours(20 + Random.Shared.Next(4))
                        });
                    }
                }

                // Чистимо старі тригери
                _state.PendingTriggers.RemoveAll(t => t.FireAt < DateTime.Now.AddDays(-2));
            }
            catch (Exception ex) { Log($"AnalyzeRecentEmotions: {ex.Message}"); }
        }

        // ==============================================================
        // ???????????? ???? ??????
        // ==============================================================

        private async Task ReflectAfterConversationAsync()
        {
            _state.LastReflectionAt = DateTime.Now;
            SaveState();

            try
            {
                var msgs = _chatRepo.GetMessages(20)
                    .Where(m => m.Timestamp >= _state.LastConversationEndAt.AddHours(-2))
                    .OrderBy(m => m.Timestamp)
                    .ToList();

                if (msgs.Count < 2) return;

                if (await ReflectAfterConversationAdvancedAsync(msgs))
                    return;

                var dialog = string.Join("\n", msgs.Select(m =>
                    $"{(m.Role == "user" ? "Він" : "Я")}: {m.Content[..Math.Min(150, m.Content.Length)]}"));

                var prompt = $@"Ти — Kokonoe. Розмова щойно закінчилась. Ось що було:
{dialog}

Твій внутрішній монолог після цього (ніхто не читає):
1. Що нового ти дізналась про нього?
2. Що ти сказала добре, а що варто було б сказати інакше?
3. Є щось що ти хочеш запам'ятати?

Відповідь у JSON (поля українською):
{{
  ""learned"": ""що нового дізналась або null"",
  ""reflection"": ""що думаєш про цю розмову"",
  ""remember"": ""що хочеш запам'ятати або null""
}}";

                var result = await _llm.SendSystemQueryAsync(prompt, useTools: true);
                if (string.IsNullOrWhiteSpace(result)) return;

                var jsonStr = ExtractJson(result);
                if (jsonStr == null) return;

                var obj      = Newtonsoft.Json.Linq.JObject.Parse(jsonStr);
                var learned  = obj["learned"]?.ToString();
                var reflect  = obj["reflection"]?.ToString();
                var remember = obj["remember"]?.ToString();

                // Зберегти в vault
                var reflectNote = "Kokonoe/Рефлексія.md";
                var entry = new StringBuilder();
                entry.AppendLine($"\n## {DateTime.Now:dd.MM.yyyy HH:mm}");
                if (!string.IsNullOrEmpty(reflect))  entry.AppendLine($"**Думка:** {reflect}");
                if (!string.IsNullOrEmpty(learned))  entry.AppendLine($"**Дізналась:** {learned}");
                if (!string.IsNullOrEmpty(remember)) entry.AppendLine($"**Запам'ятати:** {remember}");

                try { _obsidian.AppendToNote(reflectNote, entry.ToString()); }
                catch
                {
                    try
                    {
                        _obsidian.WriteNote(reflectNote,
                            $"---\ntype: reflection\ntags: [kokonoe, reflection]\n---\n\n# Рефлексія\n\nМої думки після розмов.{entry}");
                    }
                    catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "ReflectAfterConversationAsync failed near source line 4450: " + ex); }
                }

                // Якщо дізналась щось важливе — записати в спостереження
                if (!string.IsNullOrEmpty(learned) && learned != "null")
                {
                    _state.Observations.Add($"[{DateTime.Now:HH:mm}] {learned}");
                    if (_state.Observations.Count > 50) _state.Observations.RemoveAt(0);
                }

                Log($"Reflection saved: {reflect?[..Math.Min(60, reflect?.Length ?? 0)] ?? ""}");
            }
            catch (Exception ex) { Log($"ReflectAfterConversation: {ex.Message}"); }
        }

        private async Task<bool> ReflectAfterConversationAdvancedAsync(List<ChatRepository.ChatMessage> msgs)
        {
            try
            {
                var dialog = string.Join("\n", msgs.Select(m =>
                    $"{(m.Role == "user" ? "Він" : "Я")}: {m.Content[..Math.Min(220, m.Content.Length)]}"));

                var prompt = $$"""
Ти — Kokonoe. Розмова щойно закінчилась. Проаналізуй її як внутрішній механізм пам'яті й стосунку.

Діалог:
{{dialog}}

Поточний стан:
{{RuntimeState.BuildPromptBlock(_state, Emotion, _health, _chatRepo)}}
{{Relationship.BuildPromptBlock()}}

Поверни лише валідний JSON:
{
  "learned": "що нового дізналась про нього або null",
  "reflection": "короткий внутрішній висновок Kokonoe",
  "remember": "що варто зберегти в довгу пам'ять або null",
  "userTone": "neutral|positive|vulnerable|angry|seeking|crisis",
  "aftertaste": "короткий стан після розмови",
  "followUpQuestion": "конкретне питання на потім або null",
  "importance": 0.0,
  "trustDelta": 0.0,
  "intimacyDelta": 0.0,
  "frictionDelta": 0.0,
  "protectivenessDelta": 0.0,
  "curiosityDelta": 0.0,
  "stabilityDelta": 0.0
}
""";

                var result = await _llm.SendSystemQueryAsync(prompt, useTools: true);
                if (string.IsNullOrWhiteSpace(result)) return false;

                var jsonStr = ExtractJson(result);
                if (jsonStr == null) return false;

                var obj = Newtonsoft.Json.Linq.JObject.Parse(jsonStr);
                var reflection = new KokoConversationReflection
                {
                    Learned = CleanJsonString(obj["learned"]?.ToString()),
                    Reflection = CleanJsonString(obj["reflection"]?.ToString()),
                    Remember = CleanJsonString(obj["remember"]?.ToString()),
                    UserTone = CleanJsonString(obj["userTone"]?.ToString(), "neutral"),
                    Aftertaste = CleanJsonString(obj["aftertaste"]?.ToString(), "neutral"),
                    FollowUpQuestion = CleanJsonString(obj["followUpQuestion"]?.ToString()),
                    Importance = ReadFloat(obj, "importance", 0.5f),
                    TrustDelta = ReadFloat(obj, "trustDelta", 0f),
                    IntimacyDelta = ReadFloat(obj, "intimacyDelta", 0f),
                    FrictionDelta = ReadFloat(obj, "frictionDelta", 0f),
                    ProtectivenessDelta = ReadFloat(obj, "protectivenessDelta", 0f),
                    CuriosityDelta = ReadFloat(obj, "curiosityDelta", 0f),
                    StabilityDelta = ReadFloat(obj, "stabilityDelta", 0f)
                };

                ApplyConversationReflection(reflection);
                SaveAdvancedReflection(reflection);
                Log($"Advanced reflection saved: {reflection.Aftertaste} / {reflection.Importance:F2}");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Advanced reflection failed: {ex.Message}");
                return false;
            }
        }

        private void ApplyConversationReflection(KokoConversationReflection reflection)
        {
            Relationship.ApplyReflection(reflection);

            if (!string.IsNullOrEmpty(reflection.UserTone))
                _state.LastUserEmotionalTone = reflection.UserTone;

            if (!string.IsNullOrEmpty(reflection.Reflection))
            {
                _state.InnerMonologues.Add(reflection.Reflection);
                if (_state.InnerMonologues.Count > 80) _state.InnerMonologues.RemoveAt(0);
            }

            if (!string.IsNullOrEmpty(reflection.Learned))
            {
                _state.Observations.Add($"[{DateTime.Now:HH:mm}] {reflection.Learned}");
                if (_state.Observations.Count > 50) _state.Observations.RemoveAt(0);
            }

            if (!string.IsNullOrEmpty(reflection.FollowUpQuestion) &&
                !_state.CuriosityQueue.Contains(reflection.FollowUpQuestion))
            {
                _state.CuriosityQueue.Add(reflection.FollowUpQuestion);
                if (_state.CuriosityQueue.Count > 20) _state.CuriosityQueue.RemoveAt(0);
            }

            var memoryText = !string.IsNullOrEmpty(reflection.Remember) ? reflection.Remember : reflection.Learned;
            if (!string.IsNullOrEmpty(memoryText))
            {
                var importance = Math.Clamp(reflection.Importance, 0.1f, 1f);
                Memory.LearnFactBlocking(memoryText, "relationship_reflection", importance, new[] { "reflection", reflection.UserTone });
                Memory.RecordEpisodeBlocking(memoryText, reflection.UserTone, importance,
                    new[] { "reflection", "relationship", reflection.Aftertaste });
            }

            SaveState();
        }

        private void SaveAdvancedReflection(KokoConversationReflection reflection)
        {
            var reflectNote = "Kokonoe/Рефлексія.md";
            var entry = new StringBuilder();
            entry.AppendLine($"\n## {DateTime.Now:dd.MM.yyyy HH:mm}");
            if (!string.IsNullOrEmpty(reflection.Reflection)) entry.AppendLine($"**Думка:** {reflection.Reflection}");
            if (!string.IsNullOrEmpty(reflection.Learned)) entry.AppendLine($"**Дізналась:** {reflection.Learned}");
            if (!string.IsNullOrEmpty(reflection.Remember)) entry.AppendLine($"**Запам'ятати:** {reflection.Remember}");
            if (!string.IsNullOrEmpty(reflection.FollowUpQuestion)) entry.AppendLine($"**Питання на потім:** {reflection.FollowUpQuestion}");
            entry.AppendLine($"**Тон:** {reflection.UserTone}");
            entry.AppendLine($"**Aftertaste:** {reflection.Aftertaste}");
            entry.AppendLine($"**Importance:** {reflection.Importance:F2}");

            try { _obsidian.AppendToNote(reflectNote, entry.ToString()); }
            catch
            {
                try
                {
                    _obsidian.WriteNote(reflectNote,
                        $"---\ntype: reflection\ntags: [kokonoe, reflection]\n---\n\n# Рефлексія\n{entry}");
                }
                catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "SaveAdvancedReflection failed near source line 4595: " + ex); }
            }
        }

        private static string CleanJsonString(string? value, string fallback = "")
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            var cleaned = value.Trim();
            return cleaned.Equals("null", StringComparison.OrdinalIgnoreCase) ? fallback : cleaned;
        }

        private static float ReadFloat(Newtonsoft.Json.Linq.JObject obj, string name, float fallback)
        {
            try
            {
                var token = obj[name];
                return token == null ? fallback : Math.Clamp(token.Value<float>(), -1f, 1f);
            }
            catch { return fallback; }
        }
    }
}
