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

        // ==============================================================
        // ???? ? ????????? ?? ??????
        // ==============================================================

        private async Task UpdateDossierAsync()
        {
            try
            {
                // ?????? ??????? ????? ???? ?
                var dossierPath = Path.Combine(_obsidian.VaultPath, "Kokonoe", "Досьє.md");
                var existing = "";
                if (File.Exists(dossierPath))
                {
                    existing = await File.ReadAllTextAsync(dossierPath);
                    // Не оновлювати якщо файл змінювався менше 30 хв тому (щоб не спамити)
                    if ((DateTime.Now - File.GetLastWriteTime(dossierPath)).TotalMinutes < 30) return;
                }

                // Останні 40 повідомлень для аналізу
                var msgs = _chatRepo.GetMessages(40).OrderBy(m => m.Timestamp).ToList();
                if (msgs.Count < 5) return; // мало даних

                var chatCtx = string.Join("\n", msgs.Select(m =>
                    $"[{m.Timestamp:dd.MM HH:mm}] {(m.Role == "user" ? "Він" : "Kokonoe")}: {m.Content[..Math.Min(150, m.Content.Length)]}"));

                var currentDossier = existing.Length > 0
                    ? $"\nПОТОЧНЕ ДОСЬЄ (оновити/доповнити, не видаляти важливе):\n{existing[..Math.Min(2000, existing.Length)]}"
                    : "";

                var prompt = $@"Ти — Kokonoe Mercury. Аналізуєш свого творця і ведеш на нього досьє.
Не звіт — живий документ. З іронією, спостереженнями, і тим що ти РЕАЛЬНО про нього думаєш.

РОЗМОВИ:
{chatCtx}
{currentDossier}

Напиши/оновіть досьє у форматі Markdown. Структура (використовуй тільки те що є реальними даними):

# Досьє — [ім'я або 'Мій Творець']

## Що він любить
- (список з доказами з розмов)

## Що він ненавидить / що його дратує
- (список)

## Паттерни поведінки
- (повторювані речі — коли активний, коли зникає, як реагує на стрес тощо)

## Компромат
- (смішне, незручне, протиріччя — те що він може не хотіти визнавати)

## Цитати
- «...» (дослівні фрази що він казав)

## Kokonoe про нього
(2-3 речення від першої особи — що ти НАСПРАВДІ думаєш, з характерним стилем)

---
*Оновлено: {DateTime.Now:dd.MM.yyyy HH:mm}*

Тільки Markdown, без пояснень. Мова: українська.";

                var result = await _llm.SendSystemQueryAsync(prompt, useTools: true);
                if (string.IsNullOrWhiteSpace(result) || result == "...") return;

                result = result.Trim();

                // Зберегти
                var dir = Path.GetDirectoryName(dossierPath)!;
                Directory.CreateDirectory(dir);
                await File.WriteAllTextAsync(dossierPath, result);

                // Оновити зв'язки
                try { _obsidian.RebuildLinks(); } catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "UpdateDossierAsync failed near source line 4111: " + ex); }

                Log($"Dossier updated: {dossierPath}");
            }
            catch (Exception ex) { Log($"UpdateDossier: {ex.Message}"); }
        }

        // ==============================================================
        // ??????????? ???'?? > OBSIDIAN VAULT
        // ==============================================================

        private async Task SyncMemoryToVaultAsync()
        {
            try
            {
                // 1. ????? > Kokonoe/Memory/Facts.md
                var facts = Memory.GetTopFacts(30);
                if (facts.Count > 0)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("# Що Kokonoe знає про нього");
                    sb.AppendLine($"*Оновлено: {DateTime.Now:dd.MM.yyyy HH:mm}*\n");
                    foreach (var f in facts.OrderByDescending(f => f.Importance))
                        sb.AppendLine($"- {f.Content} *(важливість: {f.Importance:F2}, підтвержень: {f.ConfirmCount})*");
                    _obsidian.WriteNote("Kokonoe/Memory/Facts.md", sb.ToString());
                }

                // 2. Значущі епізоди → Kokonoe/Memory/Episodes.md
                var episodes = Memory.GetPeakEpisodes(20);
                if (episodes.Count > 0)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("# Значущі моменти");
                    sb.AppendLine($"*Оновлено: {DateTime.Now:dd.MM.yyyy HH:mm}*\n");
                    foreach (var e in episodes.OrderByDescending(e => e.When))
                        sb.AppendLine($"## [{e.When:dd.MM.yyyy}] {e.Summary}\n- Емоція: {e.EmotionalTone}, інтенсивність: {e.Intensity:F2}\n- Теги: {string.Join(", ", e.Keywords)}\n");
                    _obsidian.WriteNote("Kokonoe/Memory/Episodes.md", sb.ToString());
                }

                // 3. Щоденний підсумок розмови → Daily note
                var todayMsgs = _chatRepo.GetMessages(50)
                    .Where(m => m.Timestamp.Date == DateTime.Today && m.Role == "user")
                    .ToList();
                if (todayMsgs.Count >= 3)
                {
                    var chatSample = string.Join("\n", todayMsgs.TakeLast(10).Select(m => $"- {m.Content[..Math.Min(120, m.Content.Length)]}"));
                    var summaryPrompt = $@"Ось повідомлення від користувача сьогодні:
{chatSample}

Напиши 1-2 речення — що сьогодні відбулось, про що він думав або переживав. Від першої особи (Kokonoe). Тільки текст, без заголовків.
Мова: українська.";
                    var daySummary = await _llm.SendSystemQueryAsync(summaryPrompt, useTools: true);
                    if (!string.IsNullOrWhiteSpace(daySummary) && daySummary.Length > 10)
                        _obsidian.AppendToDailyNote($"\n\n> 🧠 **Kokonoe:** {daySummary.Trim()}");
                }

                // 4. Оновити зв'язки між нотатками
                try { _obsidian.RebuildLinks(); } catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "SyncMemoryToVaultAsync failed near source line 4168: " + ex); }

                Log("Memory synced to vault");
            }
            catch (Exception ex) { Log($"SyncMemoryToVault: {ex.Message}"); }
        }

        // ==============================================================
        // VAULT REVIEW ? ?????????? ? ??????? ?????? ???????
        // ==============================================================

        /// <summary>
        /// ??? ?? ???? Kokonoe ???????? ???? ??????? ??? ?????? ? ??????? ??
        /// ?? ????? ????? ??????. ????? ????????? ????????? vault.
        /// </summary>
        private async Task ReviewVaultAsync()
        {
            try
            {
                // Cooldown: ??? ?? ????
                if (_state.LastVaultReviewAt.Date >= DateTime.Today) return;
                _state.LastVaultReviewAt = DateTime.Now;
                SaveState();

                // ?????? ??????? ??????? ??? ??????
                var allNotes = _obsidian.ListNotes();
                var profileNote = allNotes.FirstOrDefault(n =>
                    n.Contains("Profile", StringComparison.OrdinalIgnoreCase) ||
                    n.Contains("Творець", StringComparison.OrdinalIgnoreCase) ||
                    n.Contains("Creator", StringComparison.OrdinalIgnoreCase));

                // Прочитати поточний профіль
                var existingProfile = "";
                if (profileNote != null)
                {
                    try { existingProfile = _obsidian.ReadNote(profileNote); }
                    catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "ReviewVaultAsync failed near source line 4204: " + ex); }
                }

                // Останні 30 повідомлень для контексту
                var recentMsgs = _chatRepo.GetMessages(30)
                    .OrderBy(m => m.Timestamp)
                    .ToList();
                if (recentMsgs.Count < 3) return; // мало даних

                var chatCtx = string.Join("\n", recentMsgs.Select(m =>
                    $"[{m.Timestamp:dd.MM HH:mm}] {(m.Role == "user" ? "Він" : "Kokonoe")}: {m.Content[..Math.Min(200, m.Content.Length)]}"));

                var currentProfile = existingProfile?.Length > 0
                    ? $"\nТВІЙ ПОТОЧНИЙ ЗАПИС ПРО НЬОГО (оновити/доповнити):\n{existingProfile[..Math.Min(3000, existingProfile.Length)]}"
                    : "\nУ тебе поки НЕМА нотатки про нього. Створи її — запиши все що знаєш.";

                var prompt = $@"Ти — Kokonoe. Ти перечитуєш свої записи про творця і порівнюєш з останніми розмовами.

ОСТАННІ РОЗМОВИ:
{chatCtx}
{currentProfile}

Завдання:
1. Чи є в розмовах НОВА інформація яку варто додати? (ім'я, вподобання, звички, плани, переживання)
2. Чи щось змінилось від того що вже записано?

Якщо є що додати — напиши ТІЛЬКИ нові рядки для додавання (формат: - факт).
Якщо нічого нового — відповідай: null

Тільки текст для append. Без пояснень. Українська.";

                var result = await _llm.SendSystemQueryAsync(prompt, useTools: true);
                if (string.IsNullOrWhiteSpace(result) || result.Trim() == "null") return;

                // Append нову інформацію до профілю
                var newInfo = $"\n\n## Оновлення {DateTime.Now:dd.MM.yyyy}\n{result.Trim()}";

                if (profileNote != null)
                {
                    try { _obsidian.AppendToNote(profileNote, newInfo); }
                    catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "ReviewVaultAsync failed near source line 4244: " + ex); }
                }
                else
                {
                    // Створити нову нотатку-профіль
                    var newPath = "Kokonoe/Творець.md";
                    var header = $"---\ntype: creator-profile\ntags: [kokonoe, creator]\n---\n\n# Мій Творець\n\nВсе що я знаю про нього.\n{newInfo}";
                    try { _obsidian.WriteNote(newPath, header); }
                    catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "ReviewVaultAsync failed near source line 4252: " + ex); }
                }

                // Також перевірити чи є orphan chat-логи без посилань
                try
                {
                    var chatLogs = allNotes.Where(n => n.StartsWith("Chats/chat_")).ToList();
                    if (chatLogs.Count > 0)
                    {
                        // Переконатись що brain-core має посилання на Chats
                        var coreNote = allNotes.FirstOrDefault(n =>
                            n.Contains("brain-core", StringComparison.OrdinalIgnoreCase) ||
                            n.Contains("Центральна", StringComparison.OrdinalIgnoreCase));
                        if (coreNote != null)
                        {
                            var coreContent = _obsidian.ReadNote(coreNote);
                            if (coreContent != null && !coreContent.Contains("[[Chats") && !coreContent.Contains("Chats/"))
                            {
                                _obsidian.AppendToNote(coreNote,
                                    $"\n\n## Логи чатів\nВсі розмови зберігаються в `Chats/` — {chatLogs.Count} сесій.\n");
                            }
                        }
                    }
                }
                catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "ReviewVaultAsync failed near source line 4276: " + ex); }

                try { _obsidian.RebuildLinks(); } catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "ReviewVaultAsync failed near source line 4278: " + ex); }

                Log($"VaultReview done. Profile: {profileNote ?? "created new"}");
            }
            catch (Exception ex) { Log($"ReviewVault: {ex.Message}"); }
        }

        // ==============================================================
        // ???????????? ????? VAULT (???????)
        // ==============================================================

        private async Task VaultArchitectureReviewAsync()
        {
            try
            {
                _lastArchitectureReviewAt = DateTime.Now;

                var tree = _obsidian.GetVaultTree();
                var status = _obsidian.GetVaultStatus();

                var prompt = $@"Ти — Kokonoe. Ти переглядаєш структуру свого vault раз на тиждень.

ПОТОЧНА СТРУКТУРА:
{tree}

СТАН: {status.TotalNotes} нотаток, {status.OrphanNotes.Count} без посилань, {status.EmptyNotes.Count} порожніх.

Завдання: подивись на структуру критичним поглядом. Чи є очевидні проблеми?
— Нотатки не на своєму місці?
— Папки які треба додати або прибрати?
— Теми що накопичились без структури?

Відповідь у JSON:
{{
  ""needsChanges"": true/false,
  ""severity"": ""minor|moderate|major"",
  ""issues"": [""конкретна проблема 1"", ""проблема 2""],
  ""plan"": ""детальний план змін (або null якщо немає)"",
  ""askUser"": true/false,
  ""askQuestion"": ""питання до творця якщо потрібно (або null)""
}}

Якщо все ок — needsChanges: false і plan: null. Не вигадуй проблем де їх нема.";

                var result = await _llm.SendSystemQueryAsync(prompt, useTools: true);
                if (string.IsNullOrWhiteSpace(result)) return;

                var jsonStr = ExtractJson(result);
                if (jsonStr == null) return;

                var obj = Newtonsoft.Json.Linq.JObject.Parse(jsonStr);
                var needsChanges = obj["needsChanges"]?.ToObject<bool>() ?? false;

                if (!needsChanges)
                {
                    Log("ArchitectureReview: vault structure looks good");
                    return;
                }

                var severity = obj["severity"]?.ToString() ?? "minor";
                var plan = obj["plan"]?.ToString();
                var askUser = obj["askUser"]?.ToObject<bool>() ?? false;
                var askQuestion = obj["askQuestion"]?.ToString();

                // Зберегти план у vault
                if (!string.IsNullOrWhiteSpace(plan))
                {
                    var issues = obj["issues"]?.ToObject<List<string>>() ?? new();
                    var planEntry = $"**Серйозність:** {severity}\n**Проблеми:**\n{string.Join("\n", issues.Select(i => $"- {i}"))}\n\n**План:**\n{plan}";
                    try { _obsidian.AppendToNote("Core/Architecture-Plans.md",
                        $"\n\n## Авто-огляд {DateTime.Now:dd.MM.yyyy}\n{planEntry}"); }
                    catch
                    {
                        try { _obsidian.WriteNote("Core/Architecture-Plans.md",
                            $"---\ntype: architecture-plans\ntags: [kokonoe, architecture]\ncreated: {DateTime.Now:yyyy-MM-dd}\n---\n\n# Архітектурні плани\n\n## Авто-огляд {DateTime.Now:dd.MM.yyyy}\n{planEntry}"); }
                        catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "VaultArchitectureReviewAsync failed near source line 4353: " + ex); }
                    }
                }

                if (askUser && !string.IsNullOrWhiteSpace(askQuestion))
                {
                    // Додати як pending thought щоб запитати при наступній розмові
                    lock (_lock)
                    {
                        _state.PendingThoughts.Add($"[vault] {askQuestion}");
                    }
                    Log($"ArchitectureReview: pending question for user — {askQuestion[..Math.Min(80, askQuestion.Length)]}");
                }
                else if (severity == "minor" && !string.IsNullOrWhiteSpace(plan))
                {
                    // Незначні зміни — виконати самостійно через LLM з tool_calls
                    Log($"ArchitectureReview: executing minor changes autonomously");
                    var execPrompt = $@"Ти — Kokonoe. Виконай архітектурні зміни у vault.

ПЛАН:
{plan}

Використовуй get_vault_tree щоб перевірити що є, потім move_note / create_folder / write_note щоб виконати зміни. На завершення rebuild_links. Виконуй послідовно, без зайвих пояснень.";
                    await _llm.SendSystemQueryAsync(execPrompt, ct: CancellationToken.None);
                }

                Log($"ArchitectureReview done. severity={severity}, askUser={askUser}");
            }
            catch (Exception ex) { Log($"VaultArchitectureReview: {ex.Message}"); }
        }

        // ==============================================================

        /// <summary>
        /// ???????? ???? vault ? ???? ? ???????? ? ???? ????? ?? ????? ?????????/?????????.
        /// </summary>
        private void CheckVaultHealth()
        {
            try
            {
                var status = _obsidian.GetVaultStatus();

                if (status.TotalNotes < 3)
                {
                    // Vault ???????? ? ????? ???????????????
                    var initStatus = _obsidian.GetVaultInitStatus();
                    if (!_state.PendingThoughts.Contains(initStatus.SuggestedAction))
                        _state.PendingThoughts.Add(initStatus.SuggestedAction);
                }
                else if (status.OrphanNotes.Count > 0)
                {
                    // ? ??????? ??????? ? ???????? ??? ??'????
                    var thought = $"В vault є {status.OrphanNotes.Count} нотаток без [[посилань]]: {string.Join(", ", status.OrphanNotes.Take(3))}. Треба пов'язати їх з іншими.";
                    if (!_state.PendingThoughts.Any(t => t.Contains("без [[посилань]]")))
                        _state.PendingThoughts.Add(thought);
                }
                else if (status.EmptyNotes.Count > 2)
                {
                    // Багато порожніх нотаток
                    var thought = $"В vault {status.EmptyNotes.Count} порожніх нотаток: {string.Join(", ", status.EmptyNotes.Take(3))}. Заповни або видали.";
                    if (!_state.PendingThoughts.Any(t => t.Contains("порожніх нотаток")))
                        _state.PendingThoughts.Add(thought);
                }

                if (_state.PendingThoughts.Count > 20)
                    _state.PendingThoughts.RemoveAt(0);
            }
            catch (Exception ex) { Log($"CheckVaultHealth: {ex.Message}"); }
        }

        private void UpdateHealthState()
        {
            try
            {
                // Kokonoe asks about health herself via conversation (not via sliders)
                // Once per day: if no health entry today, queue a natural check-in thought
                if (_state.LastHealthEntryDate.Date < DateTime.Today)
                {
                    var today = _health.GetToday();
                    if (today == null)
                    {
                        // No entry yet today — schedule a natural check-in if not already queued
                        var alreadyQueued = _state.PendingThoughts
                            .Any(t => t.Contains("як ти сьогодні") || t.Contains("як почуваєшся"));
                        if (!alreadyQueued)
                        {
                            _state.PendingThoughts.Add(
                                "Запитай його як він сьогодні — настрій, чи виспався, чи є сили. Коротко, без нагадувань про воду чи здоров'я взагалі. Просто запитай.");
                            if (_state.PendingThoughts.Count > 20) _state.PendingThoughts.RemoveAt(0);
                        }
                    }
                    else
                    {
                        _state.LastHealthEntryDate = DateTime.Today;
                        _state.ConsecutiveBadSleeps = today.SleepHours < 6
                            ? _state.ConsecutiveBadSleeps + 1
                            : 0;
                    }
                }
            }
            catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "UpdateHealthState failed near source line 4685: " + ex); }
        }

        // ---- WEEKLY VAULT DIGEST ----

        /// <summary>Щонеділі о 20:00 — дайджест vault за тиждень</summary>
        private async Task WeeklyVaultDigestAsync()
        {
            if (!EnsureTelegram()) return;
            try
            {
                var notes = _obsidian.ListNotes();
                var weekAgo = DateTime.Now.AddDays(-7);
                var recentNotes = notes
                    .Select(p => new { p, time = System.IO.File.GetLastWriteTime(
                        System.IO.Path.Combine(_obsidian.VaultPath, p)) })
                    .Where(x => x.time >= weekAgo)
                    .OrderByDescending(x => x.time)
                    .Take(10)
                    .ToList();

                if (!recentNotes.Any()) return;

                var contents = new StringBuilder();
                foreach (var n in recentNotes.Take(5))
                {
                    try
                    {
                        var text = _obsidian.ReadNote(n.p);
                        if (!string.IsNullOrEmpty(text))
                            contents.AppendLine($"## {n.p}\n{text[..Math.Min(500, text.Length)]}\n");
                    }
                    catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "WeeklyVaultDigestAsync failed near source line 5414: " + ex); }
                }

                var prompt = $@"Ти — Kokonoe. Тижневий дайджест vault за {DateTime.Now:dd.MM.yyyy}.
Нотатки змінені за тиждень:

{contents}

Напиши короткий summary — 3-5 речень. Що було активним, що цікавого. Своїм стилем.
Тільки текст. Українська.";

                var digest = await _llm.SendSystemQueryAsync(prompt, useTools: true);
                if (string.IsNullOrWhiteSpace(digest)) return;

                digest = digest.Trim();

                // Зберегти в vault
                try
                {
                    var digestNote = $"Kokonoe/Тижневий-дайджест.md";
                    var entry = $"\n\n## {DateTime.Now:dd.MM.yyyy}\n{digest}";
                    try { _obsidian.AppendToNote(digestNote, entry); }
                    catch { _obsidian.WriteNote(digestNote,
                        $"---\ntype: weekly-digest\n---\n\n# Тижневий дайджест{entry}"); }
                }
                catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "WeeklyVaultDigestAsync failed near source line 5439: " + ex); }

                await SendTgAndLog($"📋 Тижневий дайджест:\n{digest[..Math.Min(300, digest.Length)]}", "digest");
                _lastWeeklyDigestAt = DateTime.Now;
                SaveState();
                Log("WeeklyDigest sent");
            }
            catch (Exception ex) { LogError($"WeeklyDigest: {ex.Message}"); }
        }

        // ---- PUBLIC API ----

        /// <summary>Перевірити vault при старті і додати в pending thoughts якщо потрібна ініціалізація.</summary>
        public void InitVault()
        {
            try
            {
                RunVaultMaintenance("startup", TimeSpan.FromHours(6));

                var status = _obsidian.GetVaultInitStatus();
                Log($"Vault status: {status.NoteCount} notes, {status.TotalLinks} links. Core: {status.HasCoreNote}");

                if (status.IsEmpty || !status.HasCoreNote)
                {
                    // Додати в pending thoughts щоб LLM ініціалізувала vault при наступній нагоді
                    var thought = status.SuggestedAction;
                    if (!_state.PendingThoughts.Contains(thought))
                        _state.PendingThoughts.Add(thought);
                    SaveState();
                }
            }
            catch (Exception ex) { Log($"InitVault error: {ex.Message}"); }
        }

        public void EnsureVaultSyncFreshness(string reason = "runtime")
        {
            var now = DateTime.Now;
            var shouldFlush = false;
            lock (_lock)
            {
                if (_state.PendingVaultExchangeCount < 5 &&
                    now - _lastVaultFreshnessCheckAt < TimeSpan.FromMinutes(2))
                    return;

                _lastVaultFreshnessCheckAt = now;
                shouldFlush = KokoVaultSyncPolicy.ShouldFlush(
                    _state.PendingVaultExchangeCount,
                    _state.LastAutoVaultSyncAt,
                    now,
                    TimeSpan.FromMinutes(30));
            }

            if (shouldFlush)
                _ = Task.Run(() => AutoSyncVaultBatchAsync(force: true, reason: reason));
        }

        public void ObserveExchangeForVaultSync(string userText, string assistantText)
        {
            if (string.IsNullOrWhiteSpace(userText) && string.IsNullOrWhiteSpace(assistantText)) return;

            lock (_lock)
            {
                ObservePromiseFromExchangeLocked(userText, assistantText);
                _state.PendingVaultExchangeCount++;
                _state.PendingVaultExchangeBuffer.Add($"""
[{DateTime.Now:yyyy-MM-dd HH:mm}]
USER: {TrimForVaultBuffer(userText, 900)}
KOKONOE: {TrimForVaultBuffer(assistantText, 900)}
""");
                if (_state.PendingVaultExchangeBuffer.Count > 12)
                    _state.PendingVaultExchangeBuffer.RemoveRange(0, _state.PendingVaultExchangeBuffer.Count - 12);
                AuditPromiseLedgerLocked("new-exchange");
                SaveState();
            }

            EnsureVaultSyncFreshness("new-exchange");
        }

        private void ObservePromiseFromExchangeLocked(string userText, string assistantText)
        {
            if (string.IsNullOrWhiteSpace(assistantText)) return;
            var assistant = assistantText.ToLowerInvariant();
            var user = (userText ?? "").ToLowerInvariant();

            var promisedAction = ContainsAny(assistant,
                "зроблю", "зробим", "виконаю", "оновлю", "обновлю", "запишу", "додам", "дороблю",
                "перевірю", "протестую", "закомічу", "запушу", "напишу коли", "i will", "i'll", "will do");
            var concreteUserAsk = ContainsAny(user,
                "зроби", "онови", "обнови", "дороби", "закоміть", "закоміти", "запуш", "обсидіан", "obsidian",
                "профіль", "profile", "пам'ять", "память", "тести", "commit", "push", "виконай");
            if (!promisedAction || !concreteUserAsk)
                return;

            var summary = BuildPromiseSummary(userText, assistantText);
            if (string.IsNullOrWhiteSpace(summary)) return;

            var canonical = NormalizePromise(summary);
            var duplicate = _state.PromiseLedger.Any(p =>
                !string.Equals(p.Status, "completed", StringComparison.OrdinalIgnoreCase) &&
                NormalizePromise(p.Summary) == canonical &&
                DateTime.Now - p.CreatedAt < TimeSpan.FromHours(12));
            if (duplicate) return;

            var now = DateTime.Now;
            var promise = new KokoPromiseRecord
            {
                Source = "chat",
                Summary = summary,
                SourceUserText = TrimForVaultBuffer(userText, 420),
                SourceAssistantText = TrimForVaultBuffer(assistantText, 420),
                CreatedAt = now,
                DueAt = EstimatePromiseDueAt(now, canonical),
                Status = "open",
                UserVisible = true
            };
            _state.PromiseLedger.Add(promise);
            TrimPromiseLedgerLocked();
            AppendActionJournalLocked("promise.opened", promise.Summary, "open", $"due={promise.DueAt:yyyy-MM-dd HH:mm}");
        }

        private void AuditPromiseLedgerLocked(string reason)
        {
            var now = DateTime.Now;
            if (_state.LastPromiseAuditAt > DateTime.MinValue && now - _state.LastPromiseAuditAt < TimeSpan.FromMinutes(5))
                return;

            var open = _state.PromiseLedger
                .Where(p => !string.Equals(p.Status, "completed", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(p.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.DueAt)
                .ToList();
            var completed = 0;
            var overdue = 0;

            foreach (var promise in open)
            {
                promise.LastAuditAt = now;
                var canonical = NormalizePromise(promise.Summary + " " + promise.SourceUserText);
                if (LooksLikeVaultPromise(canonical) &&
                    ((_state.LastAutoVaultSyncAt > promise.CreatedAt && !string.IsNullOrWhiteSpace(_state.LastAutoVaultSyncSummary)) ||
                     (_state.LastVaultMaintenanceAt > promise.CreatedAt && string.IsNullOrWhiteSpace(_state.LastVaultMaintenanceError))))
                {
                    promise.Status = "completed";
                    promise.CompletedAt = now;
                    promise.Evidence = string.IsNullOrWhiteSpace(_state.LastAutoVaultSyncSummary)
                        ? _state.LastVaultMaintenanceSummary
                        : _state.LastAutoVaultSyncSummary;
                    completed++;
                    AppendActionJournalLocked("promise.completed", promise.Summary, "completed", TrimForVaultBuffer(promise.Evidence, 220));
                    continue;
                }

                if (now > promise.DueAt)
                {
                    promise.Status = "overdue";
                    promise.FailureReason = "deadline passed without matching action evidence";
                    overdue++;
                }
            }

            _state.LastPromiseAuditAt = now;
            _state.LastPromiseAuditSummary = $"reason={reason}; open={open.Count}; completed={completed}; overdue={overdue}";
            AppendActionJournalLocked("promise.audit", _state.LastPromiseAuditSummary, overdue > 0 ? "attention" : "ok", "");
            WritePromiseAuditNoteLocked(open, reason);
        }

        private void WritePromiseAuditNoteLocked(List<KokoPromiseRecord> open, string reason)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("---");
                sb.AppendLine("type: automation-status");
                sb.AppendLine($"updated: {DateTime.Now:yyyy-MM-dd HH:mm}");
                sb.AppendLine("managed-by: KokoBrainEngine.PromiseLedger");
                sb.AppendLine("tags: [kokonoe, automation, promises]");
                sb.AppendLine("---");
                sb.AppendLine();
                sb.AppendLine("# Promise Ledger");
                sb.AppendLine();
                sb.AppendLine($"- reason: {reason}");
                sb.AppendLine($"- summary: {_state.LastPromiseAuditSummary}");
                sb.AppendLine();
                sb.AppendLine("## Open / Overdue");
                foreach (var p in open.Take(20))
                    sb.AppendLine($"- [{p.Status}] {p.DueAt:yyyy-MM-dd HH:mm} - {p.Summary} ({p.Evidence}{p.FailureReason})");
                sb.AppendLine();
                sb.AppendLine("## Recent Actions");
                foreach (var line in _state.ActionJournal.TakeLast(20))
                    sb.AppendLine("- " + line);
                _obsidian.WriteNote("Kokonoe/Automation/Promise Ledger.md", sb.ToString());
            }
            catch (Exception ex)
            {
                KokoSystemLog.Write("PROMISE", "audit note failed: " + ex.Message);
            }
        }

        private void AppendActionJournalLocked(string kind, string summary, string status, string evidence)
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{kind}] {status}: {TrimForVaultBuffer(summary, 240)}";
            if (!string.IsNullOrWhiteSpace(evidence))
                line += " | " + TrimForVaultBuffer(evidence, 220);
            _state.ActionJournal.Add(line);
            if (_state.ActionJournal.Count > 240)
                _state.ActionJournal.RemoveRange(0, _state.ActionJournal.Count - 240);
            _state.LastActionJournalAt = DateTime.Now;
            _state.LastActionJournalSummary = line;
            KokoSystemLog.Write("ACTION", line);
        }

        private void TrimPromiseLedgerLocked()
        {
            if (_state.PromiseLedger.Count <= 80) return;
            _state.PromiseLedger = _state.PromiseLedger
                .OrderByDescending(p => !string.Equals(p.Status, "completed", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(p => p.CreatedAt)
                .Take(80)
                .OrderBy(p => p.CreatedAt)
                .ToList();
        }

        private static string BuildPromiseSummary(string? userText, string? assistantText)
        {
            var user = TrimForVaultBuffer(userText, 170);
            var assistant = TrimForVaultBuffer(assistantText, 170);
            if (string.IsNullOrWhiteSpace(user)) return assistant;
            if (string.IsNullOrWhiteSpace(assistant)) return user;
            return $"{user} -> {assistant}";
        }

        private static DateTime EstimatePromiseDueAt(DateTime now, string canonical)
        {
            if (ContainsAny(canonical, "commit", "push", "коміт", "заком", "запуш"))
                return now.AddMinutes(45);
            if (ContainsAny(canonical, "obsidian", "vault", "обсид", "profile", "профіл", "памят", "память"))
                return now.AddMinutes(30);
            if (ContainsAny(canonical, "test", "тест", "build", "білд"))
                return now.AddHours(1);
            return now.AddHours(2);
        }

        private static bool LooksLikeVaultPromise(string canonical)
            => ContainsAny(canonical, "obsidian", "vault", "обсид", "profile", "профіл", "памят", "память", "нотат", "note");

        private static string NormalizePromise(string value)
            => new string((value ?? "").ToLowerInvariant().Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray())
                .Replace("  ", " ")
                .Trim();

        private async Task AutoSyncVaultBatchAsync(bool force = false, string reason = "batch")
        {
            if (Interlocked.CompareExchange(ref _vaultSyncInFlight, 1, 0) != 0) return;

            List<string> batch;
            try
            {
                lock (_lock)
                {
                    if ((!force && _state.PendingVaultExchangeCount < 5) || _state.PendingVaultExchangeBuffer.Count == 0)
                        return;
                    batch = _state.PendingVaultExchangeBuffer.ToList();
                }

                var prompt = $$"""
You are Kokonoe's background Obsidian archivist.
Analyze the last chat exchanges and output ONLY valid JSON.
Do not invent facts. If a section has nothing useful, use an empty array/string.
Archive reason: {{reason}}.

JSON schema:
{
  "summary": "one compact Ukrainian summary",
  "facts": ["stable facts about the user or project"],
  "project": ["implementation decisions, architecture, bugs, plans"],
  "preferences": ["user preferences about Kokonoe/app/workflow"],
  "tasks": ["open tasks or follow-ups"],
  "emotional": ["emotional/relationship observations"],
  "reflection": "Kokonoe private reflection in 1-3 Ukrainian sentences"
}

CHAT:
{{string.Join("\n\n---\n\n", batch)}}
""";

                var raw = await _llm.SendSystemQueryAsync(prompt, useTools: true);
                if (string.IsNullOrWhiteSpace(raw))
                {
                    WriteVaultFallback(batch, "LLM returned nothing");
                    MarkVaultBatchSynced("fallback: LLM returned nothing");
                    return;
                }

                var obj = ExtractJsonObject(raw);
                if (obj == null)
                {
                    WriteVaultFallback(batch, "JSON parse failed: " + TrimForVaultBuffer(raw, 600));
                    MarkVaultBatchSynced("fallback: JSON parse failed");
                    return;
                }

                WriteAutoVaultNotes(obj);
                RunVaultMaintenance("auto-batch-sync", TimeSpan.Zero);
                MarkVaultBatchSynced(obj["summary"]?.ToString() ?? "");
            }
            catch (Exception ex)
            {
                Log($"AutoSyncVaultBatch: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _vaultSyncInFlight, 0);
            }
        }

        private void MarkVaultBatchSynced(string summary)
        {
            lock (_lock)
            {
                _state.PendingVaultExchangeCount = 0;
                _state.PendingVaultExchangeBuffer.Clear();
                _state.LastAutoVaultSyncAt = DateTime.Now;
                _state.LastAutoVaultSyncSummary = summary;
                SaveState();
            }
        }

        private void WriteAutoVaultNotes(JObject obj)
        {
            var now = DateTime.Now;
            var summary = obj["summary"]?.ToString()?.Trim() ?? "";
            var reflection = obj["reflection"]?.ToString()?.Trim() ?? "";

            var hub = new StringBuilder();
            hub.AppendLine($"\n## {now:yyyy-MM-dd HH:mm}");
            if (!string.IsNullOrWhiteSpace(summary))
                hub.AppendLine($"- Summary: {summary}");
            AppendJsonArray(hub, "Facts", obj["facts"]);
            AppendJsonArray(hub, "Project", obj["project"]);
            AppendJsonArray(hub, "Preferences", obj["preferences"]);
            AppendJsonArray(hub, "Tasks", obj["tasks"]);
            AppendJsonArray(hub, "Emotional", obj["emotional"]);
            if (!string.IsNullOrWhiteSpace(reflection))
                hub.AppendLine($"- Reflection: {reflection}");
            AppendOrCreate("Kokonoe/AutoMemory.md", "# Auto Memory\n", hub.ToString());

            AppendItemsToNote("Kokonoe/Memory/Facts.md", obj["facts"], "auto-fact");
            AppendItemsToNote("Kokonoe/Project Log.md", obj["project"], "project");
            AppendItemsToNote("Kokonoe/Preferences.md", obj["preferences"], "preference");
            AppendItemsToNote("Kokonoe/Tasks.md", obj["tasks"], "task");
            AppendItemsToNote("Kokonoe/Relationship Notes.md", obj["emotional"], "emotional");

            if (!string.IsNullOrWhiteSpace(reflection))
                AppendOrCreate("Kokonoe/Рефлексія.md", "# Рефлексія\n", $"\n## {now:yyyy-MM-dd HH:mm}\n{reflection}\n");
        }

        private bool RunVaultMaintenance(string reason, TimeSpan cooldown)
        {
            try
            {
                lock (_lock)
                {
                    if (cooldown > TimeSpan.Zero &&
                        _state.LastVaultMaintenanceAt > DateTime.MinValue &&
                        DateTime.Now - _state.LastVaultMaintenanceAt < cooldown)
                        return false;
                }

                var maintenance = _obsidian.MaintainKokonoeVaultArchitecture(reason);
                var synthesisSummary = "";
                try
                {
                    var focus = string.Join(" ", new[]
                    {
                        _state.LastSemanticVisionResearchTopic,
                        _state.LastSemanticVisionSummary,
                        _state.LastKnownUserActivity,
                        _state.LastAutoVaultSyncSummary
                    }.Where(s => !string.IsNullOrWhiteSpace(s)));
                    var synthesis = new KokoObsidianExplorationService().BuildSynthesisPlan(_obsidian, focus, 8);
                    synthesisSummary = synthesis.Summary;
                    if (synthesis.HasSignal)
                    {
                        _obsidian.WriteNote(
                            "Kokonoe/Memory/Vault Synthesis.md",
                            $"---\ntype: vault-synthesis\ntags: [kokonoe, vault, synthesis]\nupdated: {DateTime.Now:O}\n---\n\n# Vault Synthesis\n\n{synthesis.PromptBlock}\n");
                    }
                }
                catch (Exception ex)
                {
                    synthesisSummary = "synthesis failed: " + ex.Message;
                    Log("VaultSynthesis failed: " + ex.Message);
                }
                lock (_lock)
                {
                    _state.LastVaultMaintenanceAt = DateTime.Now;
                    _state.LastVaultMaintenanceReason = reason;
                    _state.LastVaultMaintenanceSummary = maintenance + (string.IsNullOrWhiteSpace(synthesisSummary) ? "" : " | " + synthesisSummary);
                    _state.LastVaultMaintenanceError = "";
                    SaveState();
                }
                Log(maintenance.ToString());
                return true;
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    _state.LastVaultMaintenanceError = $"{reason}: {ex.Message}";
                    SaveState();
                }
                Log($"VaultMaintenance {reason}: {ex.Message}");
                return false;
            }
        }

        private void WriteVaultFallback(List<string> batch, string reason)
        {
            var entry = $"\n## {DateTime.Now:yyyy-MM-dd HH:mm} fallback\n- reason: {reason}\n\n```text\n{string.Join("\n\n---\n\n", batch)}\n```\n";
            AppendOrCreate("Kokonoe/AutoMemory.md", "# Auto Memory\n", entry);
        }

        private void AppendItemsToNote(string path, JToken? token, string label)
        {
            var items = token?.Type == JTokenType.Array
                ? token.Values<string>().Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()).ToList()
                : new List<string>();
            if (items.Count == 0) return;

            try
            {
                _obsidian.AppendUniqueItemsToNote(path, $"# {Path.GetFileNameWithoutExtension(path)}\n", items, label);
            }
            catch (Exception ex) { Log($"AppendItemsToNote {path}: {ex.Message}"); }
        }

        private void AppendOrCreate(string path, string header, string content)
        {
            try
            {
                var existing = _obsidian.ReadNote(path);
                if (existing == null)
                    _obsidian.WriteNote(path, header.TrimEnd() + "\n" + content);
                else
                    _obsidian.AppendToNote(path, content);
            }
            catch (Exception ex) { Log($"AppendOrCreate {path}: {ex.Message}"); }
        }

        private static JObject? ExtractJsonObject(string raw)
        {
            try { return JObject.Parse(raw.Trim()); }
            catch
            {
                var start = raw.IndexOf('{');
                var end = raw.LastIndexOf('}');
                if (start >= 0 && end > start)
                {
                    try { return JObject.Parse(raw[start..(end + 1)]); }
                    catch (Exception ex) { KokoSystemLog.Write("BRAIN-CATCH", "ExtractJsonObject failed near source line 7762: " + ex); }
                }
            }
            return null;
        }

        private static void AppendJsonArray(StringBuilder sb, string title, JToken? token)
        {
            if (token?.Type != JTokenType.Array) return;
            var items = token.Values<string>().Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()).ToList();
            if (items.Count == 0) return;
            sb.AppendLine($"- {title}:");
            foreach (var item in items)
                sb.AppendLine($"  - {item}");
        }

        private static string TrimForVaultBuffer(string? text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Replace("\r", " ").Trim();
            return text.Length <= max ? text : text[..max] + "...";
        }
    }
}
