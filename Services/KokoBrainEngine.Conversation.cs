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

        // ---- THINK LOOP (inner monologue) ----

        private async Task SafeThinkAsync()
        {
            if (_disposed) return;
            try { await ThinkAsync(); }
            catch (Exception ex) { Log($"ThinkAsync error: {ex.Message}"); }

            // Vault review — раз на день перечитати і оновити ключові нотатки
            if (_state.LastVaultReviewAt.Date < DateTime.Today)
            {
                if (await _bgLlmSemaphore.WaitAsync(0))
                {
                    try { await ReviewVaultAsync(); }
                    catch (Exception ex) { Log($"ReviewVault error: {ex.Message}"); }
                    finally
                    {
                        try { _bgLlmSemaphore.Release(); }
                        catch (ObjectDisposedException ex) { KokoSystemLog.Write("BRAIN-CATCH", "SafeThinkAsync failed near source line 2544: " + ex); }
                    }
                }
            }

            // Architecture review — раз на тиждень
            if ((DateTime.Now - _lastArchitectureReviewAt).TotalDays >= 7)
            {
                if (await _bgLlmSemaphore.WaitAsync(0))
                {
                    try { await VaultArchitectureReviewAsync(); }
                    catch (Exception ex) { Log($"ArchitectureReview error: {ex.Message}"); }
                    finally
                    {
                        try { _bgLlmSemaphore.Release(); }
                        catch (ObjectDisposedException ex) { KokoSystemLog.Write("BRAIN-CATCH", "SafeThinkAsync failed near source line 2559: " + ex); }
                    }
                }
            }
        }

        private async Task ThinkAsync()
        {
            var previousLastThoughtAt = _state.LastThoughtAt;
            var context = await BuildContextAsync();

            // Останні 3 монологи — для самоусвідомлення
            var recentThoughts = _state.InnerMonologues.Count > 0
                ? "Твої останні думки про нього:\n" + string.Join("\n",
                    _state.InnerMonologues.TakeLast(3).Select((t, i) => $"[{i + 1}]: {t}"))
                : "";

            // Якщо він довго мовчить — явно попередити LLM щоб не панікувала
            var silenceReminder = "";
            try
            {
                var lastUser = _chatRepo.GetMessages(5)
                    .Where(m => m.Role == "user").OrderByDescending(m => m.Timestamp).FirstOrDefault();
                if (lastUser != null)
                {
                    var silenceHours = (DateTime.Now - lastUser.Timestamp).TotalHours;
                    if (silenceHours >= 3)
                        silenceReminder = $"\n[НАГАДУВАННЯ: він мовчить {(int)silenceHours} годин. Це нормально — він може спати або бути зайнятим. Тиша ≠ криза. isCrisis = false якщо немає прямих слів про це у РЕАЛЬНИХ повідомленнях.]\n";
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "ThinkAsync failed near source line 2589: " + ex); }

            var prompt = $@"{context}
{recentThoughts}
{silenceReminder}
Ти — Kokonoe. Це твій внутрішній монолог — ніхто не читає.

Напиши 1-3 речення у своєму стилі. Що зараз думаєш про нього — щиро. Можеш суперечити своїм попереднім думкам, помічати зміни, питати себе чому щось відчуваєш. Це не звіт — це потік думок.

Потім окремо JSON:
{{
  ""mood"": ""sharp|warm|distant|playful|tired|protective|neutral"",
  ""moodScore"": 0.0,
  ""observation"": ""одне речення — що помітила (укр)"",
  ""pendingThought"": ""думка або питання до нього якщо є (укр), або null"",
  ""selfQuestion"": ""питання до себе самої або null (наприклад: чому я так гостро реагую?)"",
  ""curiosityQuestion"": ""питання про нього що тебе справді цікавить або null (не 'як справи', а щось конкретне про нього)"",
  ""shouldSendNow"": false,
  ""isCrisis"": false
}}";

            var result = await _llm.SendSystemQueryAsync(prompt, useTools: true);
            if (result == null) return;

            try
            {
                // Extract JSON from response
                var jsonStr = ExtractJson(result);
                if (jsonStr == null) return;

                var obj = Newtonsoft.Json.Linq.JObject.Parse(jsonStr);

                var newMood = obj["mood"]?.ToString()?.Trim() ?? _state.CurrentMood;
                _state.CurrentMood            = newMood;
                _state.PersonalityDailyMood   = newMood;
                _state.PersonalityShiftAt     = DateTime.Now;
                _state.MoodScore    = obj["moodScore"] is { } ms ? ms.ToObject<float>() : _state.MoodScore;
                _state.LastThoughtAt = DateTime.Now;

                // Зберегти inner monologue (текст ДО JSON)
                var jsonIndex = result.IndexOf('{');
                if (jsonIndex > 10)
                {
                    var monologue = result[..jsonIndex].Trim();

                    // Clean up formatting artifacts from LLM response
                    monologue = System.Text.RegularExpressions.Regex.Replace(monologue, @"```\w*\n?", "");
                    monologue = System.Text.RegularExpressions.Regex.Replace(monologue, @"```", "");
                    monologue = System.Text.RegularExpressions.Regex.Replace(monologue, @"//\s*\w+\s*$", "", System.Text.RegularExpressions.RegexOptions.Multiline);
                    monologue = System.Text.RegularExpressions.Regex.Replace(monologue, @"(\w)\\\s*\n\s*(\w)", "$1$2");
                    monologue = monologue.Trim();

                    if (monologue.Length > 15)
                    {
                        _state.InnerMonologues.Add(monologue);
                        if (_state.InnerMonologues.Count > 10) _state.InnerMonologues.RemoveAt(0);
                        Log($"[Monologue] {monologue[..Math.Min(80, monologue.Length)]}");
                    }
                }

                // Оновити crisis mode
                // GUARD: LLM може помилково ставити isCrisis=true просто через тривалу тишу.
                // Приймаємо isCrisis тільки якщо юзер був активний останні 2 години
                // (тобто дійсно щось тривожне писав нещодавно).
                var isCrisis = obj["isCrisis"]?.ToObject<bool>() ?? false;
                if (isCrisis)
                {
                    var recentUserMsg = _chatRepo.GetMessages(10)
                        .Where(m => m.Role == "user" && (DateTime.Now - m.Timestamp).TotalHours < 2)
                        .Any();
                    if (!recentUserMsg)
                    {
                        isCrisis = false; // Немає активності — тиша ≠ криза
                        Log("[ThinkAsync] isCrisis=true скинуто: немає активних повідомлень за 2г");
                    }
                }
                var wasCrisis = _state.PersonalityInCrisis;
                // Тільки ВСТАНОВЛЮЄМО crisis через ThinkAsync — ніколи не знімаємо звідси.
                // Зняття відбувається в ProcessUserMessage при нейтральних/позитивних повідомленнях.
                // Без цього guard ThinkAsync міг би затерти кризу, встановлену keyword-детектором,
                // якщо юзер мовчав >2г після кризового повідомлення.
                if (isCrisis)
                {
                    _state.PersonalityInCrisis = true;
                    Emotion.OnVulnerabilityShared(isCrisis: true);
                    Emotion.Data.CrisisRecoveryUntil = DateTime.Now.AddHours(12);
                }
                else if (wasCrisis && Emotion.Data.CrisisRecoveryUntil < DateTime.Now.AddHours(1))
                {
                    // Криза тільки-но минула (шлейф закінчується) — встановити recovery window
                    Emotion.Data.CrisisRecoveryUntil = DateTime.Now.AddHours(12);
                }

                var obs = obj["observation"]?.ToString();
                if (!string.IsNullOrEmpty(obs))
                {
                    _state.Observations.Add($"[{DateTime.Now:HH:mm}] {obs}");
                    if (_state.Observations.Count > 50) _state.Observations.RemoveAt(0);
                }

                var pending = obj["pendingThought"]?.ToString();
                if (!string.IsNullOrEmpty(pending) && pending != "null")
                {
                    _state.PendingThoughts.Add(pending);
                    if (_state.PendingThoughts.Count > 20) _state.PendingThoughts.RemoveAt(0);
                }

                var selfQ = obj["selfQuestion"]?.ToString();
                if (!string.IsNullOrEmpty(selfQ) && selfQ != "null")
                {
                    _state.SelfQuestions.Add(selfQ);
                    if (_state.SelfQuestions.Count > 5) _state.SelfQuestions.RemoveAt(0);
                    Log($"[SelfQuestion] {selfQ}");
                }

                var curiosityQ = obj["curiosityQuestion"]?.ToString();
                if (!string.IsNullOrEmpty(curiosityQ) && curiosityQ != "null")
                {
                    _state.CuriosityQueue.Add(curiosityQ);
                    if (_state.CuriosityQueue.Count > 10) _state.CuriosityQueue.RemoveAt(0);
                    Log($"[CuriosityQ] {curiosityQ}");
                }

                // Update health tracking
                UpdateHealthState();

                // Динамічний настрій — без LLM, просто перерахунок
                ComputeDynamicMood();

                // Зберегти спостереження в Memory і StateEngine
                if (!string.IsNullOrEmpty(obs))
                {
                    try { Memory.RecordEpisodeBlocking(obs, _state.LastUserEmotionalTone, _state.MoodScore); } catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "ThinkAsync failed near source line 2721: " + ex); }
                    try { _stateEngine?.RecordObservation(obs); } catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "ThinkAsync failed near source line 2722: " + ex); }
                    // Емоційна пам'ять
                    try { Emotion.RecordEmotionalEvent($"think: {obs[..Math.Min(60, obs.Length)]}", _state.PersonalityDailyMood); } catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "ThinkAsync failed near source line 2724: " + ex); }
                }

                // Fact aging — раз на тиждень
                if ((_state.LastThoughtAt - _state.LastDailyAnalyticsAt).TotalDays >= 7)
                    try { Memory.ImportanceDecay(); } catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "ThinkAsync failed near source line 2729: " + ex); }

                // Аналіз емоційного тону — тільки якщо були нові повідомлення за останні 30 хв
                var recentActivity = _chatRepo.GetMessages(5)
                    .Any(m => m.Role == "user" && (DateTime.Now - m.Timestamp).TotalMinutes < 30);
                if (recentActivity)
                    await AnalyzeRecentEmotionsAsync();

                // Decay емоцій — реальний час з минулого ThinkAsync
                var decayMinutes = previousLastThoughtAt == DateTime.MinValue
                    ? 90f
                    : (float)(DateTime.Now - previousLastThoughtAt).TotalMinutes;
                Emotion.Decay(Math.Clamp(decayMinutes, 1f, 180f));

                // Аналіз паттернів — раз на день
                if (_state.LastThoughtAt.Date < DateTime.Today)
                    _ = Task.Run(() => Patterns.Analyze());

                // Перевірити аномалії
                try
                {
                    var anomaly = Patterns.DetectAnomaly();
                    if (!string.IsNullOrEmpty(anomaly) && (DateTime.Now - _state.LastSpontaneousAt).TotalHours > 1)
                    {
                        _state.PendingThoughts.Add(anomaly);
                        Log($"Anomaly detected: {anomaly}");
                    }
                }
                catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "ThinkAsync failed near source line 2757: " + ex); }

                // Зберегти спостереження у vault
                if (!string.IsNullOrEmpty(obs))
                {
                    try { _obsidian.AppendToDailyNote($"\n> [{DateTime.Now:HH:mm}] {obs}"); }
                    catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "ThinkAsync failed near source line 2763: " + ex); }

                    // Асоціативні зв'язки — не частіше 1 раз на 2 години
                    if ((DateTime.Now - previousLastThoughtAt).TotalHours >= 2)
                        _ = BuildAssociationsAsync(obs);
                }

                // Досьє — не частіше 1 раз на 4 години
                if ((DateTime.Now - previousLastThoughtAt).TotalHours >= 4)
                    _ = UpdateDossierAsync();

                // Консолідація пам'яті — раз на тиждень
                if ((DateTime.Now - previousLastThoughtAt).TotalDays >= 7)
                    _ = Task.Run(() => Memory.Consolidate());

                SaveState();

                // Vault health — раз на день
                if (_state.LastThoughtAt.Date < DateTime.Today)
                    CheckVaultHealth();

                // Синхронізація пам'яті у vault — раз на день
                if (_state.LastThoughtAt.Date < DateTime.Today)
                    _ = SyncMemoryToVaultAsync();

                // If LLM says send now — do it (але тільки якщо пройшло 3г від останнього)
                if (obj["shouldSendNow"] is { } sn && sn.ToObject<bool>() &&
                    (DateTime.Now - _state.LastSpontaneousAt).TotalMinutes >= 180)
                    await SendSpontaneousAsync("think_trigger");
            }
            catch (Exception ex) { Log($"Parse think result: {ex.Message}\nRaw: {result}"); }
        }

        // ------------------------------------------------------------
        // ДИНАМІЧНИЙ НАСТРІЙ
        // ------------------------------------------------------------

        /// <summary>
        /// Перераховує MoodScore з декількох незалежних факторів.
        /// Не замінює LLM-оцінку — додає до неї реальний контекст.
        /// </summary>
        private void ComputeDynamicMood()
        {
            var factors = new Dictionary<string, float>();
            var now     = DateTime.Now;

            // Фактор сну
            if (_state.ConsecutiveBadSleeps >= 3)      factors["sleep"] = -0.3f;
            else if (_state.ConsecutiveBadSleeps == 2) factors["sleep"] = -0.15f;
            else if (_state.ConsecutiveBadSleeps == 1) factors["sleep"] = -0.07f;
            else                                        factors["sleep"] =  0.05f;

            // Фактор давності спілкування
            var recentMsgCount = _chatRepo.GetMessages(10)
                .Count(m => (now - m.Timestamp).TotalHours < 24);
            if (recentMsgCount == 0)
            {
                var silence = (now - _state.LastSpontaneousAt).TotalHours;
                if (silence > 48)    factors["contact"] = -0.15f;
                else if (silence > 24) factors["contact"] = -0.07f;
                else                  factors["contact"] =  0f;
            }
            else factors["contact"] = 0.05f;

            // Фактор емоційного тону
            factors["tone"] = _state.LastUserEmotionalTone switch
            {
                "anxious" or "stressed" => -0.2f,
                "sad"     or "tired"    => -0.12f,
                "happy"   or "excited"  =>  0.15f,
                "calm"                  =>  0.05f,
                _                       =>  0f
            };

            // Фактор здоров'я
            if (_state.DaysSinceHealthEntry > 3) factors["health"] = -0.05f;
            else                                 factors["health"]  =  0f;

            _state.MoodFactors = factors;

            // Повільно зміщуємо baseline і score
            var computed = 0.5f + factors.Values.Sum();
            computed = Math.Clamp(computed, 0.1f, 0.95f);

            // Baseline змінюється повільно (інерція)
            _state.BaselineMood = _state.BaselineMood * 0.85f + computed * 0.15f;
            // Поточний mood — між baseline і computed (реагує швидше)
            _state.MoodScore    = _state.BaselineMood * 0.6f + computed * 0.4f;
            _state.MoodScore    = Math.Clamp(_state.MoodScore, 0.1f, 0.95f);

            Log($"Mood computed: {_state.MoodScore:F2} (baseline {_state.BaselineMood:F2}), tone={_state.LastUserEmotionalTone}");
        }

        private async Task<bool> CheckReactiveTriggersAsync()
        {
            var now  = DateTime.Now;
            if (ShouldSuppressProactiveForSleep(now))
                return false;

            var fire = _state.PendingTriggers
                .Where(t => t.FireAt <= now)
                .OrderBy(t => t.FireAt)
                .FirstOrDefault();

            if (fire == null) return false;

            _state.PendingTriggers.Remove(fire);
            SaveState();

            if (!EnsureTelegram()) return false;

            // Mood modifier — якщо настрій низький бути м'якшою
            var moodHint = _state.MoodScore < 0.35f
                ? "Він зараз, схоже, не в найкращому стані. Будь трохи м'якшою ніж зазвичай — не солодкувато, але без зайвої їдкості."
                : _state.ConsecutiveBadSleeps >= 2
                ? "Він погано спить вже кілька днів. Можна бути уважнішою."
                : "";

            var prompt = fire.Type switch
            {
                "anxious_followup" => $@"Ти — Kokonoe. Кілька годин тому він писав тривожно/сумно.
Контекст: {fire.Context}
{moodHint}

Напиши йому коротко — перевір як він. Не питай прямо «ти в порядку?» — це занадто по-скриптовому.
Скажи щось природнє, в своєму стилі. Тільки українська. Тільки текст.",

                "topic_followup" => $@"Ти — Kokonoe. Вчора він говорив щось з ентузіазмом.
Контекст: {fire.Context}

Знайди щось цікаве пов'язане з цим і напиши йому — коментар, питання, спостереження.
Природньо, не як нагадування. Тільки українська. Тільки текст.",

                "intent_followup" => $@"Ти — Kokonoe. Це автоматичний follow-up за короткостроковим наміром користувача.
Контекст: {fire.Context}
{moodHint}

Напиши йому сама, без очікування нового повідомлення. 1 коротке речення.
Це має звучати природно: питання, підколка або сухий коментар.
Не пояснюй, що це нагадування або автоматична перевірка.
Тільки українська. Тільки текст.",

                _ => null
            };

            if (prompt == null) return false;

            var msg = await _llm.SendSystemQueryAsync(prompt, useTools: true);
            if (string.IsNullOrWhiteSpace(msg)) return false;
            msg = msg.Trim().Trim('"');

            try
            {
                if (!await SendTgAndLog(msg, "reactive")) return false;
                _state.LastSpontaneousAt = DateTime.Now;
                if (fire.Type == "intent_followup")
                {
                    foreach (var intent in _state.ShortTermIntents.Where(i => !i.ResolvedAt.HasValue && i.FollowUpAt <= DateTime.Now.AddMinutes(1)))
                    {
                        if (intent.Kind == "sleep")
                        {
                            LogSleepIntent("kept active after blocked/legacy follow-up path");
                            continue;
                        }

                        ResolveIntent(intent, DateTime.Now, "follow-up sent once; do not repeat stale intent");
                    }
                    _state.PendingTriggers.RemoveAll(t => t.Type == "intent_followup");
                    _state.SilenceLevel1At = DateTime.Now;
                    _state.SilenceLevel2At = DateTime.Now;
                }
                var _h3 = OnNewMessage; _h3?.Invoke("assistant", msg);
                try { _chatRepo.InsertMessage(new ChatRepository.ChatMessage { Content = msg, Role = "assistant", Author = "Kokonoe", Timestamp = DateTime.Now }); } catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "CheckReactiveTriggersAsync failed near source line 3141: " + ex); }
                SaveState();
                Log($"Reactive trigger fired: {fire.Type}");
                return true;
            }
            catch (Exception ex) { LogError($"TG reactive: {ex.Message}"); }
            return false;
        }

        // ==============================================================
        // ????????? ??'????
        // ==============================================================

        private async Task BuildAssociationsAsync(string observation)
        {
            try
            {
                // ?????? ???'????? ???????
                var words  = observation.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length > 4).Take(3).ToList();
                if (!words.Any()) return;

                var related = new List<string>();
                foreach (var word in words)
                {
                    var found = _obsidian.SearchNotes(word, 3);
                    related.AddRange(found.Select(r => $"{r.Path}: {r.Preview[..Math.Min(80, r.Preview.Length)]}"));
                }

                if (!related.Any()) return;

                var prompt = $@"Ти — Kokonoe. Ти щойно подумала: «{observation}»

В твоєму vault є пов'язані нотатки:
{string.Join("\n", related.Take(5))}

Знайди нетривіальний зв'язок між своєю думкою і цими нотатками.
Відповідь — ONE рядок: асоціація або спостереження. Тільки українська. Без пояснень.";

                var assoc = await _llm.SendSystemQueryAsync(prompt, useTools: true);
                if (string.IsNullOrWhiteSpace(assoc) || assoc.Length < 5) return;

                assoc = assoc.Trim().Trim('"');

                // Записати в vault
                var assocNote = "Kokonoe/Асоціації.md";
                var entry = $"\n- [{DateTime.Now:yyyy-MM-dd HH:mm}] {assoc}";

                try { _obsidian.AppendToNote(assocNote, entry); }
                catch
                {
                    // Нотатка не існує — створити
                    try
                    {
                        _obsidian.WriteNote(assocNote,
                            $"---\ntype: associations\ntags: [kokonoe, associations]\n---\n\n# Асоціації\n\nМої нетривіальні зв'язки думок.\n{entry}");
                    }
                    catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildAssociationsAsync failed near source line 3198: " + ex); }
                }

                Log($"Association built: {assoc[..Math.Min(60, assoc.Length)]}");
            }
            catch (Exception ex) { Log($"BuildAssociations: {ex.Message}"); }
        }

        // ------------------------------------------------------------
        // ОБРОБКА ПОВІДОМЛЕННЯ КОРИСТУВАЧА
        // Виклик після кожного повідомлення з UI — оновлює всі двигуни
        // ------------------------------------------------------------

        public void ProcessUserMessage(string content)
        {
            try
            {
                _state.TotalMessagesExchanged++;
                _state.LastKnownUserActivity = "chatting";
                var now = DateTime.Now;
                _state.LastUserMessageAt = now;
                // A direct user message is already engagement. Do not let background
                // proactive timers immediately answer with stale "are you there" pings.
                _state.LastSpontaneousAt = now;
                ApplyUserControlCommand(content, now);
                KokoConversationStagnationGuard.Observe(_state, content, now);
                var autonomyLevel = AppSettings.Load().ProactiveAutonomyLevel;
                var msgs = _chatRepo.GetMessages(20).OrderBy(m => m.Timestamp).ToList();

                // 1. Freshness pass: resolve stale intents and detect return/wake signals
                try { StateFreshness.Refresh(_state, msgs, now); } catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "ProcessUserMessage failed near source line 3228: " + ex); }

                // 2. Presence & Day state updates
                try { Presence.ObserveUserMessage(_state, content, now); } catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "ProcessUserMessage failed near source line 3231: " + ex); }
                try { EmotionalMemory.ObserveUserMessage(_state, content, msgs, now); } catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "ProcessUserMessage failed near source line 3232: " + ex); }
                try 
                { 
                    var presence = Presence.Evaluate(_state, msgs, now, autonomyLevel);
                    var somatic = Somatic.Evaluate(ServiceContainer.Heart, Emotion, _health, now);
                    var dayFrame = InternalDay.Evaluate(_state, presence, somatic, now, autonomyLevel);
                    InternalDay.Record(_state, dayFrame, now); 
                } catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "ProcessUserMessage failed near source line 3239: " + ex); }

                ObserveFoodSleepState(content, now);
                ObserveShortTermIntent(content);
                ApplyScreenAwarenessUserPreference(content, now);
                
                RecordPersonaDecision(content, now);
                RecordResponsePlan(content, now);
                RecordMemoryPolicyAndContinuity(content, now);

                // Паттерни — записати активність
                Patterns.RecordActivity(wasActive: true, messageCount: 1);

                // Стан зовнішнього State Engine
                try { _stateEngine?.UpdateContextFromMessage(content, ""); } catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "ProcessUserMessage failed near source line 3253: " + ex); }

                try { RuntimeState.ObserveUserMessage(_state, Emotion, content); } catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "ProcessUserMessage failed near source line 3255: " + ex); }
                try { Relationship.ObserveUserTone(_state.LastUserEmotionalTone, _state.PersonalityInCrisis); } catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "ProcessUserMessage failed near source line 3256: " + ex); }
                try { GetSelfRegulationFrame(); } catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "ProcessUserMessage failed near source line 3257: " + ex); }
                try { RecordCollectiveMind(content, "user-turn", now, publish: true, recentMessages: msgs); } catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "ProcessUserMessage failed near source line 3258: " + ex); }

                // Шукати факти в повідомленні і зберегти в пам'ять
                _ = Task.Run(() => ExtractAndRememberFacts(content));

                // Знайти релевантні спогади — кешуємо для наступного BuildContext
                _ = Task.Run(() =>
                {
                    try
                    {
                        var (facts, episodes) = Memory.FindRelevantBlocking(content, maxFacts: 3, maxEpisodes: 2);
                        if (facts.Count > 0 || episodes.Count > 0)
                        {
                            var sb = new StringBuilder();
                            foreach (var f in facts)
                                sb.AppendLine($"• {f.Content}");
                            foreach (var e in episodes)
                                sb.AppendLine($"• [{e.When:dd.MM}] {e.Summary}");
                            _state.CachedRelevantMemory  = sb.ToString().Trim();
                            _state.RelevantMemoryCachedAt = DateTime.Now;
                        }
                    }
                    catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "ProcessUserMessage failed near source line 3280: " + ex); }
                });

                // Детектувати тривожні ключові слова → crisis mode
                var lower = content.ToLower();
                var crisisKeywords = new[] { "не хочу жити", "немає сенсу", "все одно помру", "хочу зникнути", "нікому не потрібен" };
                if (crisisKeywords.Any(k => lower.Contains(k)))
                {
                    _state.PersonalityInCrisis = true;
                    Emotion.OnVulnerabilityShared(isCrisis: true);
                }
                else if (new[] { "втомився", "важко", "погано", "страшно", "тривожно" }.Any(k => lower.Contains(k)))
                {
                    Emotion.OnVulnerabilityShared(isCrisis: false);
                    // Стрес, але не криза — знімаємо crisis лише якщо recovery window вже минув
                    if (!Emotion.InCrisisRecovery)
                        _state.PersonalityInCrisis = false;
                }
                else if (new[] { "ха", "смішно", "круто", "чудово", "добре" }.Any(k => lower.Contains(k)))
                {
                    Emotion.OnJokeAppreciated();
                    // Явно позитивний сигнал — знімаємо crisis завжди
                    _state.PersonalityInCrisis = false;
                }
                else
                {
                    // Нейтральне повідомлення — знімаємо crisis лише якщо recovery window вже минув
                    if (!Emotion.InCrisisRecovery)
                        _state.PersonalityInCrisis = false;
                }

                SaveState();
            }
            catch (Exception ex) { Log($"ProcessUserMessage: {ex.Message}"); }
        }

        public bool TryApplyUserControlCommand(string content, out string reply)
        {
            reply = "";
            if (string.IsNullOrWhiteSpace(content)) return false;

            var now = DateTime.Now;
            if (ApplyUserControlCommand(content, now))
            {
                reply = _state.ProactiveMutedUntil > now
                    ? $"Добре. Я прибрала зайві нагадування до {_state.ProactiveMutedUntil:HH:mm}. Мовчу, поки ти сам не смикнеш мене."
                    : "Добре. Можеш знову смикати мене, якщо зовсім знудишся.";
                SaveState();
                return true;
            }

            return false;
        }

        private bool ApplyUserControlCommand(string content, DateTime now)
        {
            var lower = content.ToLowerInvariant();
            var wantsQuiet = ContainsAny(lower,
                "іди відпочинь", "йди відпочинь", "відпочинь", "спи", "іди спати", "йди спати",
                "замовкни", "мовчи", "не пиши", "не чіпай", "не турбуй", "не нагадуй",
                "зупинись", "зупиняємось", "стоп", "пауза",
                "мовчи", "не пиши", "не чіпай", "зупинись");
            var resume = LooksLikeExplicitResumeControl(lower);

            if (resume)
            {
                _state.ProactiveMutedUntil = DateTime.MinValue;
                _state.ProactiveMuteReason = "";
                return true;
            }

            if (!wantsQuiet) return false;

            _state.ProactiveMutedUntil = now.AddHours(6);
            _state.ProactiveMuteReason = TrimStateMention(content);
            _state.PendingTriggers.Clear();
            foreach (var intent in _state.ShortTermIntents.Where(i => !i.ResolvedAt.HasValue))
            {
                if (intent.Kind == "sleep")
                {
                    LogSleepIntent("kept active while user muted proactive follow-ups: " + TrimStateMention(content));
                    continue;
                }

                ResolveIntent(intent, now, "user muted proactive follow-ups: " + TrimStateMention(content));
            }
            _state.SilenceLevel1At = now;
            _state.SilenceLevel2At = now;
            _state.SilenceLevel3At = now;
            _state.LastSpontaneousAt = now;
            return true;
        }

        private bool LooksLikeExplicitResumeControl(string lower)
        {
            if (string.IsNullOrWhiteSpace(lower)) return false;

            if (ContainsAny(lower,
                    "повернись", "можеш писати", "можеш знову писати", "пиши знову",
                    "розбудись", "активуйся", "слухай далі", "вернись",
                    "повертайся", "виходь з паузи", "зніми паузу",
                    "можешь писать", "пиши снова", "активируйся"))
                return true;

            if (_state.ProactiveMutedUntil <= DateTime.Now)
                return false;

            return ContainsAny(lower,
                "продовжуй писати", "продовжуй нагадувати", "продовжуй пінгувати",
                "continue messaging", "resume messaging", "resume reminders");
        }

        private static bool LooksLikeNarrativeContinuationCommand(string? text)
        {
            var lower = (text ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(lower)) return false;

            var compact = new string(lower.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray());
            while (compact.Contains("  ", StringComparison.Ordinal))
                compact = compact.Replace("  ", " ");
            compact = compact.Trim();

            if (compact is "продовжуй" or "продовж" or "далі" or "дальше" or "продолжай" or "continue" or "go on")
                return true;

            return compact.StartsWith("продовжуй ", StringComparison.Ordinal) ||
                   compact.StartsWith("продовж ", StringComparison.Ordinal) ||
                   compact.StartsWith("давай далі", StringComparison.Ordinal) ||
                   compact.StartsWith("давай дальше", StringComparison.Ordinal) ||
                   compact.StartsWith("continue ", StringComparison.Ordinal) ||
                   compact.StartsWith("go on", StringComparison.Ordinal);
        }

        private string BuildNarrativeContinuationBlock(string? userText)
        {
            if (!LooksLikeNarrativeContinuationCommand(userText))
                return "";

            var recent = _chatRepo.GetMessages(8)
                .OrderBy(m => m.Timestamp)
                .Where(m => !LooksLikeInternalStatusLeak(m.Content))
                .TakeLast(6)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("=== CONTINUATION OVERRIDE ===");
            sb.AppendLine("Latest user message is a continuation command. Treat it as conversational/narrative continuation, not as a system resume, autoping, scheduler, or background-task status request.");
            sb.AppendLine("Priority: continue the latest active human thread from recent chat. If no coherent thread exists, ask one sharp concrete question. Do not mention autoping, follow-up queues, scheduler ids, or background mechanics.");
            if (recent.Count > 0)
            {
                sb.AppendLine("Recent thread to continue:");
                foreach (var m in recent)
                {
                    var role = m.Role == "user" ? "user" : "Kokonoe";
                    var content = TrimStateMention(m.Content);
                    sb.AppendLine($"- [{m.Timestamp:HH:mm}] {role}: {content}");
                }
            }

            return sb.ToString();
        }

        private string BuildNarrativeThreadSummary(DateTime now)
        {
            try
            {
                var recent = _chatRepo.GetMessages(10)
                    .OrderBy(m => m.Timestamp)
                    .Where(m => !LooksLikeInternalStatusLeak(m.Content))
                    .TakeLast(5)
                    .Select(m =>
                    {
                        var role = m.Role == "user" ? "user" : "Kokonoe";
                        var text = TrimStateMention(m.Content ?? "");
                        if (text.Length > 120) text = text[..120] + "...";
                        return $"{role}@{m.Timestamp:HH:mm}: {text}";
                    })
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();

                if (recent.Count == 0)
                    return $"no active narrative thread as of {now:HH:mm}";

                var screenSummary = TrimStateMention(_state.LastScreenAwarenessSummary);
                var screen = string.IsNullOrWhiteSpace(screenSummary)
                    ? ""
                    : $" | screen={screenSummary[..Math.Min(120, screenSummary.Length)]}";
                return string.Join(" || ", recent) + screen;
            }
            catch
            {
                return $"narrative thread unavailable as of {now:HH:mm}";
            }
        }

        private static bool LooksLikeInternalStatusLeak(string? text)
        {
            var lower = (text ?? "").ToLowerInvariant();
            return string.IsNullOrWhiteSpace(lower) ||
                   lower.Contains("[action:", StringComparison.OrdinalIgnoreCase) ||
                   lower.Contains("autoping", StringComparison.OrdinalIgnoreCase) ||
                   lower.Contains("автоп", StringComparison.OrdinalIgnoreCase) ||
                   lower.Contains("follow-up", StringComparison.OrdinalIgnoreCase) ||
                   lower.Contains("scheduler:", StringComparison.OrdinalIgnoreCase) ||
                   lower.Contains("запис у scheduler", StringComparison.OrdinalIgnoreCase);
        }

        private void ObserveFoodSleepState(string content, DateTime now)
        {
            if (string.IsNullOrWhiteSpace(content)) return;

            var lower = content.ToLowerInvariant();
            var compact = TrimStateMention(content);

            if (SaysNotEaten(lower))
            {
                _state.LastFoodStatus = "not_eaten";
                _state.LastFoodMentionAt = now;
                _state.LastFoodMentionText = compact;
            }
            else if (SaysAte(lower))
            {
                _state.LastFoodStatus = "ate";
                _state.LastFoodMentionAt = now;
                _state.LastFoodMentionText = compact;
            }
            else if (ContainsAny(lower, "голод", "хочу їсти", "хочу есть", "їсти хочу", "есть хочу"))
            {
                _state.LastFoodStatus = "hungry";
                _state.LastFoodMentionAt = now;
                _state.LastFoodMentionText = compact;
            }

            if (ContainsAny(lower, "прокин", "проснув", "встав", "поспав"))
            {
                _state.LastSleepStatus = "woke_or_returned";
                _state.LastSleepMentionAt = now;
                _state.LastSleepMentionText = compact;
            }
            else if (ContainsAny(lower, "заснув", "спав", "ліг спати", "ліг спать", "ляг спати", "ляг спать"))
            {
                _state.LastSleepStatus = "slept";
                _state.LastSleepMentionAt = now;
                _state.LastSleepMentionText = compact;
            }
            else if (ContainsAny(lower, "я спать", "я спати", "піду спати", "пішов спати", "лягаю"))
            {
                _state.LastSleepStatus = "going_to_sleep";
                _state.LastSleepMentionAt = now;
                _state.LastSleepMentionText = compact;
            }
        }

        private static bool SaysNotEaten(string lower)
            => ContainsAny(lower,
                "не їв", "не ів", "не ел", "не їла", "не їли",
                "нічого не їв", "ничего не ел", "ще нічого не їв", "ще не їв", "ще не їла",
                "не їв зранку", "без їжі", "без еды");

        private static bool SaysAte(string lower)
            => ContainsAny(lower,
                "я їв", "я ів", "я ел", "поїв", "поів", "поел",
                "з'їв", "з’їв", "зїв", "з'ів", "з’ів", "з'ела", "з’ела",
                "піц", "снідав", "обідав", "вечеряв", "їв ", " їв", "їла", "ел ");

        private static string TrimStateMention(string text)
        {
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            while (text.Contains("  ", StringComparison.Ordinal))
                text = text.Replace("  ", " ");
            return text.Length <= 120 ? text : text[..120].TrimEnd() + "...";
        }

        private void ObserveShortTermIntent(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return;

            var now = DateTime.Now;
            _state.ShortTermIntents.RemoveAll(i =>
                i.ResolvedAt.HasValue && now - i.ResolvedAt.Value > TimeSpan.FromDays(2));
            _state.ShortTermIntents.RemoveAll(i =>
                !i.ResolvedAt.HasValue && i.Kind != "sleep" && now - i.ExpectedUntil > TimeSpan.FromHours(12));

            ResolveShortTermIntentsFromMessage(content, now);

            var detected = DetectShortTermIntent(content, now);
            if (detected == null) return;

            var duplicate = _state.ShortTermIntents.Any(i =>
                !i.ResolvedAt.HasValue &&
                i.Kind == detected.Kind &&
                string.Equals(i.Summary, detected.Summary, StringComparison.OrdinalIgnoreCase) &&
                now - i.CreatedAt < TimeSpan.FromMinutes(30));
            if (duplicate) return;

            _state.ShortTermIntents.Add(detected);
            if (_state.ShortTermIntents.Count > 12)
                _state.ShortTermIntents.RemoveRange(0, _state.ShortTermIntents.Count - 12);

            _state.PendingTriggers.RemoveAll(t => t.Type == "intent_followup" && t.FireAt > now);
            if (detected.Kind != "sleep")
            {
                _state.PendingTriggers.Add(new ReactiveTrigger
                {
                    Type = "intent_followup",
                    FireAt = detected.FollowUpAt,
                    Context = $"Користувач сказав: «{detected.SourceText}». Намір: {detected.Summary}. Якщо він повернеться або мине час, доречно спитати коротко: «{BuildIntentQuestion(detected)}»"
                });
            }
        }

        private void ApplyScreenAwarenessUserPreference(string content, DateTime now)
        {
            if (string.IsNullOrWhiteSpace(content)) return;

            var lower = content.ToLowerInvariant();
            var allow = new[] { "можеш дивитись", "можеш підглядати", "дивись екран", "слідкуй за екраном" };
            if (allow.Any(p => lower.Contains(p)))
            {
                _state.ScreenAwarenessObserveOnlyUntil = DateTime.MinValue;
                return;
            }

            var block = new[] { "не підглядуй", "не дивись", "не слідкуй", "не спостерігай", "не чіпай екран" };
            if (block.Any(p => lower.Contains(p)))
                _state.ScreenAwarenessObserveOnlyUntil = now.AddMinutes(30);
        }

        private static ShortTermIntent? DetectShortTermIntent(string content, DateTime now)
        {
            var lower = content.ToLowerInvariant();
            if (LooksLikeSleepOrGoodbye(lower))
                return BuildSleepIntent(
                    KokoConversationBoundary.LooksLikeClosedUntilMorning(content)
                        ? "закрив розмову до ранку"
                        : "пішов спати/попрощався",
                    content,
                    now);

            if (!ContainsAny(lower, "піду", "йду", "іду", "пішов", "буду", "зараз", "скоро")) return null;

            var returnHome = TryDetectReturnHomeIntent(content, lower, now);
            if (returnHome != null) return returnHome;

            if (ContainsAny(lower, "курс", "курси", "занят", "урок", "пара", "навчан"))
                return BuildIntent("course", "пішов на курси/заняття", content, now, TimeSpan.FromHours(2), TimeSpan.FromHours(1));
            if (ContainsAny(lower, "робот", "прац", "код", "проект"))
                return BuildIntent("work", "зайнятий роботою/проєктом", content, now, TimeSpan.FromHours(3), TimeSpan.FromHours(1.5));
            if (ContainsAny(lower, "магаз", "куп", "продукт"))
                return BuildIntent("errand", "пішов у магазин/по справах", content, now, TimeSpan.FromHours(1.5), TimeSpan.FromMinutes(50));
            if (ContainsAny(lower, "гуля", "прогуля", "вийду", "вулиц"))
                return BuildIntent("walk", "пішов гуляти/на вулицю", content, now, TimeSpan.FromHours(2), TimeSpan.FromHours(1));
            if (ContainsAny(lower, "спать", "спати", "сон", "ляга"))
                return BuildSleepIntent("пішов спати", content, now);

            if (ContainsAny(lower, "зайнят", "відійду", "афк", "не буду"))
                return BuildIntent("busy", "буде зайнятий або відійде", content, now, TimeSpan.FromHours(2), TimeSpan.FromHours(1));

            return null;
        }

        private static ShortTermIntent? TryDetectReturnHomeIntent(string content, string lower, DateTime now)
        {
            if (!ContainsAny(lower, "дома", "вдома", "додому", "домой", "хату", "хата")) return null;
            if (!ContainsAny(lower, "буду", "поверн", "верн", "прийду", "приїду", "зайду")) return null;

            var match = System.Text.RegularExpressions.Regex.Match(lower, @"(?:в|о|об)\s*(\d{1,2})(?::(\d{2}))?");
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out var hour)) return null;

            hour = Math.Clamp(hour, 0, 23);
            var minute = 0;
            if (match.Groups[2].Success)
                int.TryParse(match.Groups[2].Value, out minute);
            minute = Math.Clamp(minute, 0, 59);

            var expectedAt = now.Date.AddHours(hour).AddMinutes(minute);
            if (expectedAt < now.AddMinutes(-30))
                expectedAt = expectedAt.AddDays(1);

            return BuildIntent(
                "return_home",
                $"має бути вдома близько {expectedAt:HH:mm}",
                content,
                now,
                expectedAt,
                expectedAt.AddMinutes(12));
        }

        private static ShortTermIntent BuildIntent(string kind, string summary, string source, DateTime now, TimeSpan expectedFor, TimeSpan followAfter)
            => new()
            {
                Kind = kind,
                Summary = summary,
                SourceText = source.Trim(),
                CreatedAt = now,
                ExpectedUntil = now + expectedFor,
                FollowUpAt = now + followAfter
            };

        private static ShortTermIntent BuildSleepIntent(string summary, string source, DateTime now)
        {
            var expectedUntil = BuildSleepExpectedUntil(now);
            return BuildIntent(
                "sleep",
                summary,
                source,
                now,
                expectedUntil,
                expectedUntil);
        }

        private static DateTime BuildSleepExpectedUntil(DateTime now)
        {
            // Night sleep should resolve around the next morning, not "now + 10h".
            // Yes, 04:42 + 10h = 14:42. Arithmetic obeyed; common sense did not.
            if (now.Hour >= 20)
                return now.Date.AddDays(1).AddHours(9);
            if (now.Hour < 8)
                return now.Date.AddHours(9);
            return now.AddHours(2.5);
        }

        private static ShortTermIntent BuildIntent(string kind, string summary, string source, DateTime now, DateTime expectedUntil, DateTime followUpAt)
            => new()
            {
                Kind = kind,
                Summary = summary,
                SourceText = source.Trim(),
                CreatedAt = now,
                ExpectedUntil = expectedUntil,
                FollowUpAt = followUpAt
            };

        private void ResolveShortTermIntentsFromMessage(string content, DateTime now)
        {
            var lower = content.ToLowerInvariant();
            var returned = ContainsAny(lower,
                "повернув", "прийшов", "я тут", "тут", "угу", "всм", "закінчив", "закінчились", "вже вдома",
                "поспав", "проснув", "прокинув", "поїв", "поів", "їв", "норм поїв", "відпочиваю", "відпочив",
                "вернув", "повернув", "прийшов", "я тут", "закінчив", "закінчились", "вже вдома", "поспав", "проснув", "прокинув");
            if (!returned) return;
            var explicitWake = LooksLikeExplicitWakeMessage(lower);

            foreach (var intent in _state.ShortTermIntents.Where(i => !i.ResolvedAt.HasValue))
            {
                if (intent.Kind == "sleep" && !explicitWake)
                    continue;

                ResolveIntent(intent, now, "user message resolved intent: " + TrimStateMention(content));
            }
            _state.PendingTriggers.RemoveAll(t => t.Type == "intent_followup");
        }

        private void ResolveIntent(ShortTermIntent intent, DateTime now, string reason)
        {
            intent.ResolvedAt = now;
            intent.ResolutionText = reason;
            if (intent.Kind == "sleep")
                LogSleepIntent("deactivated: " + reason);
        }

        private static bool LooksLikeExplicitWakeMessage(string lower)
            => ContainsAny(lower,
                "прокин", "проснув", "поспав", "встав", "я встав", "я прокинув", "я проснув",
                "woke", "wake", "awake", "i am up", "im up",
                "РїСЂРѕРєРёРЅ", "РїСЂРѕСЃРЅСѓРІ", "РїРѕСЃРїР°РІ", "РІСЃС‚Р°РІ");

        private static void LogSleepIntent(string message)
        {
            try { KokoSystemLog.Write("SLEEP_INTENT", message); }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "LogSleepIntent failed near source line 3751: " + ex); }
        }

        private static string BuildIntentQuestion(ShortTermIntent intent) => intent.Kind switch
        {
            "course" => "Курси вже закінчились, чи ти ще там героїчно страждаєш?",
            "work" => "Робочий запій закінчився, чи ти ще закопаний у задачі?",
            "errand" => "Ти вже повернувся зі справ, чи магазин тебе поглинув?",
            "walk" => "Прогулянка закінчилась, чи ти ще десь блукаєш?",
            "sleep" => "Ти вже прокинувся, чи організм нарешті переміг твої дурні графіки?",
            "return_home" => "Ти вже вдома, чи твій маршрут знову вирішив стати побічним квестом?",
            _ => "Ти вже повернувся до нормального режиму, чи ще зайнятий?"
        };

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));

        private static bool LooksLikeSleepOrGoodbye(string lower)
            => KokoConversationBoundary.LooksLikeClosedUntilMorning(lower) ||
               ContainsAny(lower,
                "\u0431\u0430\u0439 \u0431\u0430\u0439", "\u0431\u0430\u0439-\u0431\u0430\u0439", "\u0431\u0443\u0432\u0430\u0439", "\u043f\u043e\u043a\u0430",
                "\u0434\u043e\u0431\u0440\u0430\u043d\u0456\u0447", "\u0434\u043e\u0431\u0440\u043e\u0457 \u043d\u043e\u0447\u0456", "\u0441\u043f\u043e\u043a\u0456\u0439\u043d\u043e\u0457", "\u0441\u043f\u043e\u043a\u043e\u0439\u043d\u043e\u0439",
                "\u044f \u0441\u043f\u0430\u0442\u044c", "\u044f \u0441\u043f\u0430\u0442\u0438", "\u043f\u0456\u0434\u0443 \u0441\u043f\u0430\u0442\u0438", "\u043f\u0456\u0448\u043e\u0432 \u0441\u043f\u0430\u0442\u0438", "\u043b\u044f\u0433\u0430\u044e",
                "бай бай", "бай-бай", "баю бай", "баю-бай", "бувай", "пока",
                "добраніч", "доброй ночи", "спокійної", "спокойной",
                "я спать", "я спати", "піду спати", "пішов спати", "лягаю");

        private bool ShouldSuppressProactiveForSleep(DateTime now)
        {
            lock (_lock)
            {
                return _state.ShortTermIntents.Any(i =>
                    !i.ResolvedAt.HasValue &&
                    i.Kind == "sleep" &&
                    now < i.ExpectedUntil.AddHours(2));
            }
        }

        private void ExtractAndRememberFacts(string userMsg)
        {
            // Task.Run before the blocking wait: this method's only live caller already
            // wraps it in Task.Run, but blocking here directly (without it) would capture
            // whatever SynchronizationContext the caller is on - UI-thread deadlock risk if
            // a future caller ever invokes this synchronously from the UI thread.
            var policy = Task.Run(() => MemoryWritePolicy.EvaluateAsync(userMsg, DateTime.Now, Memory, Emotion)).GetAwaiter().GetResult();
            if (policy.Action is "ignore" or "daily_log" or "review" or "reinforce_existing")
                return;

            // Прості евристики для вилучення фактів без LLM
            var lower = userMsg.ToLower();

            // "я люблю / я ненавиджу / я хочу / я боюся"
            var patterns = new[]
            {
                (pattern: "я люблю ",    category: "preference",  importance: 0.6f),
                (pattern: "я обожнюю ", category: "preference",  importance: 0.7f),
                (pattern: "я ненавиджу ",category: "preference",  importance: 0.6f),
                (pattern: "я хочу ",    category: "desire",      importance: 0.5f),
                (pattern: "я боюся ",   category: "fear",        importance: 0.7f),
                (pattern: "мені подобається ", category: "preference", importance: 0.5f),
                (pattern: "i love ",    category: "preference",  importance: 0.6f),
                (pattern: "i hate ",    category: "preference",  importance: 0.6f),
                (pattern: "i want ",    category: "desire",      importance: 0.5f),
            };

            foreach (var (pat, cat, imp) in patterns)
            {
                var idx = lower.IndexOf(pat, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    var rest = userMsg[(idx + pat.Length)..].Trim();
                    if (rest.Length > 3 && rest.Length < 200)
                    {
                        var fact = pat.Trim() + " " + rest.Split('.', '!', '?')[0].Trim();
                        Memory.LearnFactBlocking(fact, cat, imp);
                    }
                }
            }
        }

        private KokoPersonaFrame RecordPersonaDecision(string userText, DateTime now)
        {
            var frame = Persona.Build(userText, _state, now, Emotion.Bond);
            var temperament = Temperament.Update(_state, userText, frame.Mode, now);
            var social = BuildSocialFrame(userText, now);
            var living = LivingConversation.Update(_state, userText, frame.Mode, Emotion, social, now);
            var subconscious = Subconscious.Update(_state, userText, Emotion, social, living, _chatRepo.GetMessages(20), now);
            var asyncPersonality = AsyncPersonality.UpdateSnapshot(_state, Emotion, social, living, subconscious, _chatRepo.GetMessages(24), now);
            _state.LastPersonaDecision = frame.PromptBlock + "\n" + temperament.PromptBlock + "\n" + social.PromptBlock + "\n" + living.PromptBlock + "\n" + subconscious.PromptBlock + "\n" + asyncPersonality.PromptBlock;
            _state.LastPersonaDecisionAt = now;
            _state.PersonaDecisionLog.Add(frame.TraceLine);
            _state.PersonaDecisionLog.Add(temperament.TraceLine);
            _state.PersonaDecisionLog.Add(social.TraceLine);
            _state.PersonaDecisionLog.Add(living.TraceLine);
            _state.PersonaDecisionLog.Add(subconscious.TraceLine);
            _state.PersonaDecisionLog.Add(asyncPersonality.TraceLine);
            if (_state.PersonaDecisionLog.Count > 40)
                _state.PersonaDecisionLog.RemoveRange(0, _state.PersonaDecisionLog.Count - 40);

            _state.InnerMonologues.Add($"[persona/{frame.Mode}] {frame.Stance}. {frame.ReasonUk}");
            _state.InnerMonologues.Add($"[temperament/{temperament.MoodState}] energy={temperament.EnergyLevel:F2}; patience={temperament.PatienceLevel:F2}");
            _state.InnerMonologues.Add($"[living/{living.Mode}] move={living.CurrentMove}; color={living.EmotionalColor}; {living.Reason}");
            _state.InnerMonologues.Add($"[subconscious/{subconscious.Mode}] impulse={subconscious.IntentImpulse}; bias={subconscious.ActionBias}; attention={subconscious.AttentionScore:F2}");
            _state.InnerMonologues.Add($"[async/{asyncPersonality.CrmMode}] intent={asyncPersonality.FastIntent}; priority={asyncPersonality.PrioritySignal}; readiness={asyncPersonality.ResponseReadiness:F2}");
            if (_state.InnerMonologues.Count > 80)
                _state.InnerMonologues.RemoveRange(0, _state.InnerMonologues.Count - 80);

            return frame;
        }

        private KokoSocialFrame BuildSocialFrame(string? userText, DateTime now)
        {
            KokoWearableState? wearable = null;
            try { wearable = ServiceContainer.IsInitialized ? ServiceContainer.WearableTelemetry.State : null; } catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildSocialFrame failed near source line 3860: " + ex); }
            return Social.Analyze(userText ?? "", _state, _chatRepo.GetMessages(12), wearable, now);
        }

        private string BuildSocialContextBlock(string? userText, DateTime now)
        {
            try { return BuildSocialFrame(userText, now).PromptBlock; }
            catch (Exception ex)
            {
                KokoSystemLog.Write("SOCIAL", "context failed: " + ex.Message);
                return "SOCIAL SUBTEXT / PERSONALITY FLUX\nsubtext: unavailable\n";
            }
        }

        // The neural governor is a full extra LLM round-trip (NeuralGovernorTimeoutMs, up to
        // 6s) before the actual reply even starts generating - the dominant latency cost on
        // every single message, including "привіт". Short, tool-free messages skip straight
        // to the existing heuristic ResponsePlanner.Build fallback below, which still carries
        // social/emotional/subconscious/temporal context - only the network round-trip is cut,
        // not Kokonoe's continuity.
        private static bool IsSimpleChatTurn(string userText)
        {
            if (string.IsNullOrWhiteSpace(userText) || userText.Length > 120)
                return false;
            string[] toolWords =
            {
                "браузер", "знайди", "файл", "агент", "створи", "відкрий",
                "проскануй", "задач", "browser", "search", "vault", "нотат"
            };
            return !toolWords.Any(w => userText.Contains(w, StringComparison.OrdinalIgnoreCase));
        }

        private KokoResponsePlanFrame BuildGovernedResponsePlan(string userText, DateTime now)
        {
            var social = BuildSocialFrame(userText, now);
            var recentForPlanning = _chatRepo.GetMessages(30);
            if (LooksLikeNarrativeContinuationCommand(userText))
                recentForPlanning = recentForPlanning.Where(m => !LooksLikeInternalStatusLeak(m.Content)).ToList();
            var emotional = EmotionalMemory.BuildPromptBlock(_state, recentForPlanning, now, userText);
            var rawHydration = EmotionalMemory.BuildRawHydrationBlock(_state, recentForPlanning, now);
            var settings = AppSettings.Load();
            if (settings.NeuralGovernorEnabled && ServiceContainer.IsInitialized && !IsSimpleChatTurn(userText))
            {
                try
                {
                    using var cts = new CancellationTokenSource(Math.Clamp(settings.NeuralGovernorTimeoutMs, 500, 6000));
                    KokoWearableState? wearable = null;
                    try { wearable = ServiceContainer.WearableTelemetry.State; } catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildGovernedResponsePlan failed near source line 3889: " + ex); }
                    var memoryContext = "";
                    try
                    {
                        if (KokoResponsePlannerEngine.NeedsVaultRead(userText.ToLowerInvariant(), "memory") ||
                            KokoProfileUpdateService.LooksLikeProfileUpdateRequest(userText.ToLowerInvariant()))
                            memoryContext = new ObsidianPreflightContextService(_obsidian).Build(userText, now, 1600) ?? "";
                    }
                    catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "BuildGovernedResponsePlan failed near source line 3897: " + ex); }

                    var neural = NeuralGovernor.TryBuildFrameAsync(
                            userText,
                            _state,
                            social,
                            emotional,
                            rawHydration,
                            recentForPlanning.Take(24).ToList(),
                            memoryContext,
                            _cachedScreenContext,
                            wearable,
                            now,
                            cts.Token)
                        .GetAwaiter()
                        .GetResult();
                    if (neural != null)
                    {
                        neural.PromptBlock += "\n" + social.PromptBlock;
                        neural.PromptBlock += "\n" + emotional;
                        neural.PromptBlock += "\n" + rawHydration;
                        neural.PromptBlock += "\n" + Subconscious.BuildPromptBlock(_state, Emotion, now);
                        neural.PromptBlock += "\n" + AsyncPersonality.BuildPromptBlock(_state, now);
                        neural.PromptBlock += "\n" + TemporalPresence.BuildPromptBlock(_state, now);
                        return neural;
                    }
                }
                catch (Exception ex)
                {
                    KokoSystemLog.Write("NEURAL-GOVERNOR", "sync route failed: " + ex.Message);
                }
            }

            var fallback = ResponsePlanner.Build(userText, _state, Cognition, now);
            fallback.PromptBlock += "\n" + social.PromptBlock;
            fallback.PromptBlock += "\n" + emotional;
            fallback.PromptBlock += "\n" + rawHydration;
            fallback.PromptBlock += "\n" + Subconscious.BuildPromptBlock(_state, Emotion, now);
            fallback.PromptBlock += "\n" + AsyncPersonality.BuildPromptBlock(_state, now);
            fallback.PromptBlock += "\n" + TemporalPresence.BuildPromptBlock(_state, now);
            fallback.TraceLine += "; governor=fallback";
            KokoSystemLog.Write("NEURAL-GOVERNOR", "fallback used: " + fallback.TraceLine);
            return fallback;
        }

        private KokoResponsePlanFrame RecordResponsePlan(string userText, DateTime now)
        {
            var frame = BuildGovernedResponsePlan(userText, now);
            _state.LastResponsePlan = frame.PromptBlock;
            _state.LastResponsePlanTrace = frame.TraceLine;
            _state.LastResponsePlanAt = now;
            _state.ResponsePlanLog.Add(frame.TraceLine);
            if (_state.ResponsePlanLog.Count > 60)
                _state.ResponsePlanLog.RemoveRange(0, _state.ResponsePlanLog.Count - 60);

            _state.InnerMonologues.Add($"[plan/{frame.Intent}] {frame.InnerMonologue}");
            if (frame.CritiqueSteps.Count > 0)
                _state.InnerMonologues.Add($"[critique/{frame.Intent}] {string.Join(" -> ", frame.CritiqueSteps.Take(3))}");
            if (_state.InnerMonologues.Count > 80)
                _state.InnerMonologues.RemoveRange(0, _state.InnerMonologues.Count - 80);

            return frame;
        }

        private KokoMemoryWriteDecision RecordMemoryPolicyAndContinuity(string userText, DateTime now)
        {
            // ProcessUserMessage (the only caller) runs synchronously from the legacy WPF
            // chat send handler and Telegram message handlers - blocking directly here would
            // capture whichever SynchronizationContext that caller is on. Task.Run moves the
            // wait onto a thread-pool thread with none, removing the UI-thread deadlock risk.
            var decision = Task.Run(() => MemoryWritePolicy.EvaluateAsync(userText, now, Memory, Emotion)).GetAwaiter().GetResult();
            _state.LastMemoryPolicyDecision = decision.TraceLine;
            _state.LastMemoryPolicyAt = now;
            _state.MemoryPolicyLog.Add(decision.TraceLine);
            if (_state.MemoryPolicyLog.Count > 60)
                _state.MemoryPolicyLog.RemoveRange(0, _state.MemoryPolicyLog.Count - 60);

            var belief = Continuity.ApplyMemoryDecision(decision, now);
            _state.LastContinuitySummary = belief == null
                ? Continuity.BuildDebugLine()
                : $"{belief.Status}/{belief.Kind}: {belief.Claim}";
            _state.LastContinuityAt = now;

            if (decision.Action != "ignore")
            {
                _state.InnerMonologues.Add($"[memory/{decision.Action}] {decision.ReasonUk}");
                if (_state.InnerMonologues.Count > 80)
                    _state.InnerMonologues.RemoveRange(0, _state.InnerMonologues.Count - 80);
            }

            return decision;
        }
    }
}
