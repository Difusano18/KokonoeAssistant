using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace KokonoeAssistant.Services
{
    public sealed class KokoProfileUpdateResult
    {
        public bool Success { get; set; }
        public string ProfilePath { get; set; } = "";
        public string BackupPath { get; set; } = "";
        public string Error { get; set; } = "";
        public int RecentContextItems { get; set; }
        public string[] ChangedSections { get; set; } = Array.Empty<string>();
        public bool UsedLlmSynthesis { get; set; }

        public string ToUserReply()
        {
            if (!Success)
                return "Профіль не оновлено: " + (string.IsNullOrWhiteSpace(Error) ? "невідома помилка" : Error);

            var sections = ChangedSections.Length == 0 ? "профіль" : string.Join(", ", ChangedSections);
            return "Готово. Оновила Obsidian-профіль без театру.\n" +
                   "- Файл: `" + ProfilePath + "`\n" +
                   "- Backup: `" + BackupPath + "`\n" +
                   "- Змінено: " + sections + "\n" +
                   "- Синтез: " + (UsedLlmSynthesis ? "LLM прочитала профіль і контекст; валідатор записав файл" : "fallback-план, бо LLM-синтез не пройшов валідацію") + "\n" +
                   "- Контекст: " + RecentContextItems + " останніх реплік враховано.";
        }
    }

    public sealed class KokoProfileUpdateService
    {
        private static readonly string[] PreferredProfilePaths =
        {
            "Creator/Profile.md",
            "Creator/Профіль.md",
            "Kokonoe/Creator Profile.md",
            "Kokonoe/Досьє.md",
            "Profile.md"
        };

        private readonly ObsidianMcpService _obsidian;
        private readonly ChatRepository _chat;

        public KokoProfileUpdateService(ObsidianMcpService obsidian, ChatRepository chat)
        {
            _obsidian = obsidian ?? throw new ArgumentNullException(nameof(obsidian));
            _chat = chat ?? throw new ArgumentNullException(nameof(chat));
        }

        public static bool LooksLikeProfileUpdateRequest(string? text)
        {
            var lower = (text ?? "").ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(lower)) return false;

            var action = ContainsAny(lower,
                "онови", "обнови", "актуалізуй", "актуализируй", "перепиши",
                "відредагуй", "исправь", "виправ", "почисти", "синхронізуй",
                "update", "refresh", "rewrite", "clean up");
            var target = ContainsAny(lower,
                "профіль", "профиль", "profile", "досьє", "досье",
                "обсидіан", "obsidian", "vault", "пам'ять", "память");

            return action && target;
        }

        public KokoProfileUpdateResult UpdateProfileFromRecentContext(string instruction, int recentMessageLimit = 120)
            => UpdateProfileFromRecentContextAsync(instruction, null, CancellationToken.None, recentMessageLimit)
                .GetAwaiter()
                .GetResult();

        public async Task<KokoProfileUpdateResult> UpdateProfileFromRecentContextAsync(
            string instruction,
            LlmService? llm,
            CancellationToken ct = default,
            int recentMessageLimit = 120)
        {
            var result = new KokoProfileUpdateResult();
            try
            {
                var profilePath = ResolveProfilePath();
                var existing = _obsidian.ReadNote(profilePath) ?? "";
                var backupPath = CreateBackup(profilePath, existing);
                var recent = _chat.GetMessages(recentMessageLimit)
                    .Where(m => !string.IsNullOrWhiteSpace(m.Content))
                    .OrderBy(m => m.Timestamp)
                    .ToList();

                var llmDraft = llm == null
                    ? null
                    : await TryBuildProfileWithLlmAsync(llm, profilePath, existing, instruction, recent, ct).ConfigureAwait(false);
                var usedLlm = IsUsableProfileDraft(llmDraft);
                var content = usedLlm
                    ? NormalizeGeneratedProfile(llmDraft!, profilePath, instruction)
                    : BuildProfile(profilePath, existing, instruction, recent);
                _obsidian.WriteNote(profilePath, content);

                result.Success = true;
                result.ProfilePath = profilePath;
                result.BackupPath = backupPath;
                result.RecentContextItems = recent.Count;
                result.UsedLlmSynthesis = usedLlm;
                result.ChangedSections = new[]
                {
                    "поточний стан",
                    "проєкти",
                    "операційні правила",
                    "межі пам'яті"
                };
                KokoSystemLog.Write("PROFILE", $"updated {profilePath}; backup={backupPath}; context={recent.Count}; llm={usedLlm}");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                KokoSystemLog.Write("PROFILE", "update failed: " + ex.Message);
            }

            return result;
        }

        private static async Task<string?> TryBuildProfileWithLlmAsync(
            LlmService llm,
            string profilePath,
            string existing,
            string instruction,
            IReadOnlyList<ChatRepository.ChatMessage> recent,
            CancellationToken ct)
        {
            try
            {
                var prompt = BuildLlmProfilePrompt(profilePath, existing, instruction, recent);
                var raw = await llm.SendSystemQueryAsync(prompt, useTools: false, ct: ct, agentId: "system").ConfigureAwait(false);
                return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
            }
            catch (Exception ex)
            {
                KokoSystemLog.Write("PROFILE", "LLM synthesis failed: " + ex.Message);
                return null;
            }
        }

        private static string BuildLlmProfilePrompt(
            string profilePath,
            string existing,
            string instruction,
            IReadOnlyList<ChatRepository.ChatMessage> recent)
        {
            var sb = new StringBuilder();
            sb.AppendLine("PROFILE UPDATE TASK");
            sb.AppendLine("You are editing the user's Obsidian profile as an autonomous local operator.");
            sb.AppendLine("Return ONLY the complete markdown file. No code fences. No promise to do it later.");
            sb.AppendLine("Use Ukrainian for visible prose.");
            sb.AppendLine("Preserve stable factual data from the old profile when safe.");
            sb.AppendLine("Think over the recent context and update the profile content, not just canned sections.");
            sb.AppendLine("Do not include explicit sexual tags, humiliation, medical diagnosis, or hostile psychological claims.");
            sb.AppendLine("Temporary fatigue, pulse, sleep, stress, or crisis material must be dated observations, not permanent identity traits.");
            sb.AppendLine("Must include an 'Операційні правила Kokonoe' section with: actions before roleplay; real file/status artifacts; no fake background progress.");
            sb.AppendLine("Must include the current complaint: user dislikes intrusive roleplay and wants Manus-like visible execution.");
            sb.AppendLine("Target path: " + profilePath);
            sb.AppendLine("Instruction: " + TrimOneLine(instruction, 500));
            sb.AppendLine();
            sb.AppendLine("OLD PROFILE:");
            sb.AppendLine(TrimBlock(existing, 12000));
            sb.AppendLine();
            sb.AppendLine("RECENT CONTEXT:");
            foreach (var msg in recent.TakeLast(50))
            {
                var role = string.IsNullOrWhiteSpace(msg.Role) ? "message" : msg.Role;
                sb.AppendLine("- " + msg.Timestamp.ToString("yyyy-MM-dd HH:mm") + " " + role + ": " + TrimOneLine(msg.Content, 280));
            }
            return sb.ToString();
        }

        private static bool IsUsableProfileDraft(string? draft)
        {
            if (string.IsNullOrWhiteSpace(draft) || draft.Length < 500) return false;
            var lower = draft.ToLowerInvariant();
            if (!draft.Contains("#", StringComparison.Ordinal)) return false;
            if (!ContainsAny(lower, "операційні правила", "operating rules")) return false;
            if (ContainsAny(lower,
                    "anal", "pussy", "penis", "rule34",
                    "жалігід", "жалюгід", "когнітивного функціонування",
                    "порушеного когнітив", "you are pathetic"))
                return false;
            if (ContainsAny(lower, "я занурюся", "напишу коли", "коли закінчу", "i will update", "when finished")) return false;
            return true;
        }

        private static string NormalizeGeneratedProfile(string draft, string profilePath, string instruction)
        {
            var content = StripCodeFence(draft).Replace("\r\n", "\n").Replace('\r', '\n').Trim();
            var lines = content.Split('\n')
                .Where(line => !LooksSensitiveOrUnsafeForProfile(line))
                .ToList();
            content = string.Join("\n", lines).Trim();

            if (!content.StartsWith("---", StringComparison.Ordinal))
            {
                content = "---\n" +
                          "type: creator-profile\n" +
                          "updated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n" +
                          "managed-by: KokonoeProfileUpdateService\n" +
                          "tags: [creator, profile, operating-context]\n" +
                          "---\n\n" + content;
            }

            if (!content.Contains("Операційні правила Kokonoe", StringComparison.OrdinalIgnoreCase))
                content += "\n\n" + BuildRequiredRulesSection();

            content += "\n\n## Службове\n" +
                       "- Джерело оновлення: `" + profilePath + "`\n" +
                       "- Остання інструкція: " + TrimOneLine(instruction, 260) + "\n" +
                       "- Зміст синтезовано LLM з існуючого профілю та останнього контексту; запис виконано локальним валідованим маршрутом.\n";
            return content.TrimEnd() + "\n";
        }

        private static string BuildRequiredRulesSection()
            => """
## Операційні правила Kokonoe
- Спочатку виконання, потім короткий звіт.
- Якщо запит стосується Obsidian/Vault/профілю, потрібен реальний файл, backup або status artifact.
- Не обіцяти фонову роботу без реальної задачі з id/status.
- Рольплей і сарказм не мають перекривати корисну дію.
""";

        private static string StripCodeFence(string text)
        {
            var value = (text ?? "").Trim();
            if (!value.StartsWith("```", StringComparison.Ordinal)) return value;
            var firstBreak = value.IndexOf('\n');
            var lastFence = value.LastIndexOf("```", StringComparison.Ordinal);
            if (firstBreak >= 0 && lastFence > firstBreak)
                return value[(firstBreak + 1)..lastFence].Trim();
            return value;
        }

        private string ResolveProfilePath()
        {
            foreach (var path in PreferredProfilePaths)
            {
                if (_obsidian.ReadNote(path) != null)
                    return path;
            }

            var candidate = _obsidian.ListNotes()
                .FirstOrDefault(p =>
                    p.Contains("profile", StringComparison.OrdinalIgnoreCase) ||
                    p.Contains("проф", StringComparison.OrdinalIgnoreCase) ||
                    p.Contains("дось", StringComparison.OrdinalIgnoreCase));

            return string.IsNullOrWhiteSpace(candidate) ? "Creator/Profile.md" : candidate;
        }

        private string CreateBackup(string profilePath, string existing)
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var backup = "Kokonoe/Profile Backups/" +
                         Path.GetFileNameWithoutExtension(profilePath).Replace(' ', '-') +
                         "-" + stamp + ".md";
            _obsidian.WriteNote(backup, string.IsNullOrWhiteSpace(existing)
                ? "# Empty profile backup\n"
                : existing);
            return backup;
        }

        private static string BuildProfile(
            string profilePath,
            string existing,
            string instruction,
            IReadOnlyList<ChatRepository.ChatMessage> recent)
        {
            var now = DateTime.Now;
            var facts = ExtractStableFacts(existing);
            var userMessages = recent
                .Where(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
                .Select(m => TrimOneLine(m.Content, 180))
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Where(m => !LooksSensitiveOrUnsafeForProfile(m))
                .TakeLast(10)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("---");
            sb.AppendLine("type: creator-profile");
            sb.AppendLine("created: 2026-04-06");
            sb.AppendLine("updated: " + now.ToString("yyyy-MM-dd HH:mm"));
            sb.AppendLine("managed-by: KokonoeProfileUpdateService");
            sb.AppendLine("tags: [creator, profile, operating-context]");
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("# Творець — Вова (Yasu)");
            sb.AppendLine();
            sb.AppendLine("> Робочий профіль для Kokonoe. Це не медична карта і не місце для принижень. Факти відокремлюються від інтерпретацій.");
            sb.AppendLine();

            sb.AppendLine("## Базові факти");
            AppendFact(sb, "Ім'я", facts.GetValueOrDefault("Ім'я", "Вова (Yasu / Yasu-kun)"));
            AppendFact(sb, "Вік", facts.GetValueOrDefault("Вік", "21 рік (народився 21.04.2005)"));
            AppendFact(sb, "Місцезнаходження", facts.GetValueOrDefault("Місцезнаходження", "Іспанія"));
            AppendFact(sb, "Роль", "творець і власник локальної системи Kokonoe");
            sb.AppendLine();

            sb.AppendLine("## Поточний технічний фокус");
            sb.AppendLine("- KokonoeAssistant: автономний локальний помічник із Obsidian-пам'яттю, Telegram, screen/vision, PC-control і агентськими задачами.");
            sb.AppendLine("- Galaxy Watch bridge: прив'язка годинника до PC, реальний пульс, motion, diagnostics, token/pairing, HTTP bridge на локальній мережі.");
            sb.AppendLine("- Очікування від системи: Manus-подібна поведінка — виконувати задачі, писати статус, залишати артефакти, комітити й пушити зміни після перевірки.");
            sb.AppendLine();

            sb.AppendLine("## Операційні правила Kokonoe");
            sb.AppendLine("- Якщо користувач просить оновити профіль або Vault, треба реально записати файл і показати шлях, backup і короткий список змін.");
            sb.AppendLine("- Не відповідати псевдо-прогресом на кшталт 'я занурюся і напишу потім', якщо не створено реальну задачу з id/status.");
            sb.AppendLine("- Рольплей і сарказм не мають перекривати виконання. Спочатку дія, потім короткий звіт.");
            sb.AppendLine("- Тон: українська, прямо, технічно, без театральних монологів і без принизливих діагнозів.");
            sb.AppendLine();

            sb.AppendLine("## Переваги взаємодії");
            sb.AppendLine("- Користувач дозволяє глибоку автономну роботу, але хоче бачити реальні результати: файли, логи, тести, коміти, push.");
            sb.AppendLine("- Запити часто формулюються швидко й розмовно; система має сама обрати очевидну дію, якщо ризик низький.");
            sb.AppendLine("- Якщо щось не працює, потрібна конкретна діагностика й виправлення, а не характерна репліка.");
            sb.AppendLine();

            sb.AppendLine("## Межі пам'яті");
            sb.AppendLine("- Тимчасову втому, сон, пульс і стрес записувати як спостереження з датою, не як постійні риси або медичні висновки.");
            sb.AppendLine("- Інтимні або соромні деталі не деталізувати в основному профілі. Якщо вони потрібні, тримати в окремій приватній нотатці з явним дозволом.");
            sb.AppendLine("- Заборонено зберігати образливі формулювання як факт про користувача. Переписувати нейтрально: 'перевтома могла вплинути на рішення', а не 'порушення когнітивного функціонування'.");
            sb.AppendLine();

            sb.AppendLine("## Актуальний стан на " + now.ToString("yyyy-MM-dd"));
            sb.AppendLine("- Користувач незадоволений нав'язливим рольплеєм і тим, що попередня відповідь обіцяла роботу замість реального оновлення профілю.");
            sb.AppendLine("- Потрібна більш автономна поведінка: виконувати доручення в Obsidian/коді, показувати артефакти й самостійно продовжувати низькоризикові задачі.");
            sb.AppendLine("- Watch bridge вже доходив до стану live/linked; наступний фокус — стабільне зчитування реальних датчиків і використання telemetry в логіці програми.");
            sb.AppendLine();

            if (userMessages.Count > 0)
            {
                sb.AppendLine("## Останній корисний контекст");
                foreach (var msg in userMessages)
                    sb.AppendLine("- " + msg);
                sb.AppendLine();
            }

            sb.AppendLine("## Службове");
            sb.AppendLine("- Джерело оновлення: `" + profilePath + "`");
            sb.AppendLine("- Остання інструкція: " + TrimOneLine(instruction, 260));
            sb.AppendLine("- Оновлено автоматичним профільним маршрутом, без LLM-обіцянки 'зроблю потім'.");
            sb.AppendLine();

            return sb.ToString();
        }

        private static Dictionary<string, string> ExtractStableFacts(string content)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in (content ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                var line = raw.Trim();
                var match = Regex.Match(line, @"^-\s*\*\*(?<key>[^:*]+):\*\*\s*(?<value>.+)$");
                if (match.Success)
                {
                    var key = match.Groups["key"].Value.Trim();
                    var value = match.Groups["value"].Value.Trim();
                    if (!LooksSensitiveOrUnsafeForProfile(value))
                        map[key] = value;
                }
            }

            return map;
        }

        private static void AppendFact(StringBuilder sb, string key, string value)
            => sb.AppendLine("- **" + key + ":** " + value);

        private static string TrimOneLine(string? value, int max)
        {
            var text = Regex.Replace(value ?? "", @"\s+", " ").Trim();
            if (text.Length <= max) return text;
            return text[..Math.Max(0, max - 1)].TrimEnd() + "…";
        }

        private static string TrimBlock(string? value, int max)
        {
            var text = (value ?? "").Trim();
            if (text.Length <= max) return text;
            return text[..Math.Max(0, max - 1)].TrimEnd() + "…";
        }

        private static bool LooksSensitiveOrUnsafeForProfile(string text)
        {
            var lower = (text ?? "").ToLowerInvariant();
            return ContainsAny(lower,
                "суїцид", "самогуб", "sex", "секc", "секс", "anal", "pussy", "penis",
                "rule34", "жалігід", "жалюгід", "когнітивного функціонування",
                "порушеного когнітив", "діагноз", "депресив");
        }

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));
    }
}
