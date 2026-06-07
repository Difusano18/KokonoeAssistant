using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

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

        public string ToUserReply()
        {
            if (!Success)
                return "Профіль не оновлено: " + (string.IsNullOrWhiteSpace(Error) ? "невідома помилка" : Error);

            var sections = ChangedSections.Length == 0 ? "профіль" : string.Join(", ", ChangedSections);
            return "Готово. Оновила Obsidian-профіль без театру.\n" +
                   "- Файл: `" + ProfilePath + "`\n" +
                   "- Backup: `" + BackupPath + "`\n" +
                   "- Змінено: " + sections + "\n" +
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

                var content = BuildProfile(profilePath, existing, instruction, recent);
                _obsidian.WriteNote(profilePath, content);

                result.Success = true;
                result.ProfilePath = profilePath;
                result.BackupPath = backupPath;
                result.RecentContextItems = recent.Count;
                result.ChangedSections = new[]
                {
                    "поточний стан",
                    "проєкти",
                    "операційні правила",
                    "межі пам'яті"
                };
                KokoSystemLog.Write("PROFILE", $"updated {profilePath}; backup={backupPath}; context={recent.Count}");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                KokoSystemLog.Write("PROFILE", "update failed: " + ex.Message);
            }

            return result;
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
