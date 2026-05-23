using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace KokonoeAssistant.Services
{
    public sealed class KokoObsidianExplorationService
    {
        private static readonly string[] InterestTerms =
        {
            "ідея", "idea", "insight", "спостереж", "observation", "патерн", "pattern",
            "цікав", "interesting", "важлив", "ризик", "план", "goal", "memory",
            "емоці", "думк", "гіпотез", "todo", "task", "беклог"
        };

        public static bool LooksLikeInterestingVaultDive(string? text)
        {
            var lower = (text ?? "").ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(lower)) return false;

            var mentionsVault = ContainsAny(lower,
                "obsidian", "vault", "обсидіан", "обсідіан", "обсидиан", "обсидиане", "нотат", "заміт", "замет");
            if (!mentionsVault) return false;

            return ContainsAny(lower,
                "порий", "порій", "порой", "пошукай", "пошукайся", "поищи", "розкоп", "покоп",
                "проскан", "скан", "переглянь", "подивись", "прочеш", "знайди", "найди",
                "щось цікаве", "что-то интерес", "цікав", "интерес", "interesting");
        }

        public static bool LooksLikeObsidianWorkRequest(string? text)
            => LooksLikeInterestingVaultDive(text)
               || LooksLikeVaultAccessCheck(text)
               || LooksLikeExplorationFollowup(text);

        public static bool LooksLikeVaultAccessCheck(string? text)
        {
            var lower = (text ?? "").ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(lower)) return false;

            var mentionsVault = ContainsAny(lower,
                "obsidian", "vault", "обсидіан", "обсідіан", "обсидиан", "обсидиане", "нотат", "заміт", "замет");
            if (!mentionsVault) return false;

            return ContainsAny(lower,
                "добрати", "добрaти", "дістати", "достук", "зайди", "зайти", "заліз", "залез",
                "доступ", "підключ", "подключ", "перевір", "провір", "check", "спробуй",
                "пробуй", "скан", "проскан", "працює", "работает", "відкрий", "открой");
        }

        public static bool LooksLikeExplorationFollowup(string? text)
        {
            var lower = (text ?? "").ToLowerInvariant().Trim();
            if (string.IsNullOrWhiteSpace(lower)) return false;

            return ContainsAny(lower,
                "і що там", "и что там", "шо там", "що там", "ну що", "ну шо", "ну і",
                "результат", "що знайш", "шо знайш", "що найш", "що нарила", "що накопала",
                "знайшла", "знайшов", "далі", "там є щось", "є щось");
        }

        public string BuildAccessReport(ObsidianMcpService obsidian, string? userText, int sampleItems = 5)
        {
            if (obsidian == null) throw new ArgumentNullException(nameof(obsidian));
            sampleItems = Math.Clamp(sampleItems, 1, 10);

            try
            {
                var status = obsidian.GetVaultStatus();
                var notes = obsidian.ListNotes()
                    .Where(p => p.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                    .Where(p => !p.Contains("kokonoe-data", StringComparison.OrdinalIgnoreCase))
                    .Take(sampleItems)
                    .ToList();

                var interesting = BuildCandidates(obsidian, userText)
                    .OrderByDescending(c => c.Score)
                    .ThenByDescending(c => c.ModifiedAt)
                    .Take(2)
                    .ToList();

                var lines = new List<string>
                {
                    "Obsidian-task завершено. Не «чекаю», не «сканую вічно» - виконала перевірку.",
                    $"- Дії: vault_status -> list_notes -> read/sample scan.",
                    $"- Доступ: є.",
                    $"- Стан: {status.TotalNotes} нотаток, {status.FilledNotes} заповнених, {status.EmptyNotes.Count} порожніх, {status.OrphanNotes.Count} осиротілих."
                };

                if (notes.Count > 0)
                    lines.Add("- Видимі файли: " + string.Join(", ", notes.Select(p => $"`{p}`")));
                else
                    lines.Add("- Видимі файли: markdown-нотаток не знайшла.");

                if (interesting.Count > 0)
                {
                    lines.Add("- Швидкі зачіпки:");
                    lines.AddRange(interesting.Select(c => $"  - `{c.Path}`: {c.Preview}"));
                }

                lines.Add("Висновок: маршрут до Obsidian працює. Обіцянки без результату тепер вважаються сміттєвим fallback, не дією.");
                return string.Join("\n", lines);
            }
            catch (Exception ex)
            {
                return "Obsidian-task завершено зі збоєм.\n" +
                       "- Дії: vault_status -> list_notes.\n" +
                       "- Доступ: не підтверджений через помилку читання.\n" +
                       $"- Помилка: {Truncate(ex.Message, 220)}";
            }
        }

        public string BuildInterestingFinds(ObsidianMcpService obsidian, string? userText, int maxItems = 3)
        {
            if (obsidian == null) throw new ArgumentNullException(nameof(obsidian));
            maxItems = Math.Clamp(maxItems, 1, 6);

            var candidates = BuildCandidates(obsidian, userText)
                .OrderByDescending(c => c.Score)
                .ThenByDescending(c => c.ModifiedAt)
                .Take(maxItems)
                .ToList();

            if (candidates.Count == 0)
                return "Obsidian-скан завершено.\n" +
                       "- Дії: list_notes -> semantic_search -> read_notes.\n" +
                       "- Доступ: є.\n" +
                       "- Результат: живих нотаток для нормальної знахідки не знайшла. Це не «немає доступу», це просто порожня полиця.";

            var lines = candidates
                .Select(c => $"- `{c.Path}`: {c.Preview}")
                .ToList();

            var strongest = candidates[0];
            return "Obsidian-скан завершено.\n" +
                   "- Дії: list_notes -> semantic_search -> read_notes.\n" +
                   "- Доступ: є.\n" +
                   "Порилась у vault, не вдавала телепатію. Зачіпки:\n" +
                   string.Join("\n", lines) +
                   $"\n\nНайцікавіше зараз: `{strongest.Path}`. Там є матеріал, який варто розгорнути, а не ховати під пилом.";
        }

        private static IEnumerable<Candidate> BuildCandidates(ObsidianMcpService obsidian, string? userText)
        {
            var semanticBoosts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(userText))
            {
                foreach (var hit in obsidian.SearchSemantic(userText, 12))
                    semanticBoosts[hit.Path] = Math.Max(semanticBoosts.GetValueOrDefault(hit.Path), hit.Score + 4);
            }

            foreach (var path in obsidian.ListNotes())
            {
                if (!path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (path.Contains("kokonoe-data", StringComparison.OrdinalIgnoreCase))
                    continue;

                var content = obsidian.ReadNote(path);
                if (string.IsNullOrWhiteSpace(content))
                    continue;
                if (content.Contains("managed-by: Kokonoe", StringComparison.OrdinalIgnoreCase))
                    continue;

                var clean = CleanMarkdown(content);
                if (clean.Length < 40)
                    continue;

                var fullPath = Path.Combine(obsidian.VaultPath, path.Replace('/', Path.DirectorySeparatorChar));
                var modified = File.Exists(fullPath) ? File.GetLastWriteTime(fullPath) : DateTime.MinValue;
                var score = Score(path, clean, modified);
                if (semanticBoosts.TryGetValue(path, out var boost))
                    score += boost;

                yield return new Candidate
                {
                    Path = path,
                    Preview = BuildPreview(clean),
                    ModifiedAt = modified,
                    Score = score
                };
            }
        }

        private static int Score(string path, string content, DateTime modified)
        {
            var lower = (path + "\n" + content).ToLowerInvariant();
            var score = 0;
            score += Math.Min(content.Length / 280, 14);
            score += InterestTerms.Sum(t => Count(lower, t));
            if (modified.Date == DateTime.Today) score += 8;
            else if (modified > DateTime.Now.AddDays(-7)) score += 5;
            else if (modified > DateTime.Now.AddDays(-30)) score += 2;
            if (ContainsAny(path.ToLowerInvariant(), "journal", "daily", "creator", "analysis", "observ", "idea", "project"))
                score += 3;
            return score;
        }

        private static string BuildPreview(string clean)
        {
            var lines = clean
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim().Trim('-', '*', ' ', '#'))
                .Where(l => l.Length >= 24)
                .ToList();

            var chosen = lines.FirstOrDefault(l => InterestTerms.Any(t => l.Contains(t, StringComparison.OrdinalIgnoreCase)))
                         ?? lines.FirstOrDefault()
                         ?? clean.Trim();

            return Truncate(chosen, 220);
        }

        private static string CleanMarkdown(string content)
        {
            content = Regex.Replace(content, @"\A---[\s\S]*?---", "");
            content = Regex.Replace(content, @"<\|[^>]*\|?>", "");
            content = Regex.Replace(content, @"```[\s\S]*?```", "");
            content = Regex.Replace(content, @"\s+", " ");
            return content.Trim();
        }

        private static string Truncate(string text, int max)
        {
            if (text.Length <= max) return text;
            var cut = text.LastIndexOf(' ', Math.Min(max, text.Length - 1));
            if (cut < max / 2) cut = max;
            return text[..cut].TrimEnd() + "...";
        }

        private static int Count(string text, string term)
        {
            var count = 0;
            var index = 0;
            while ((index = text.IndexOf(term, index, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                count++;
                index += Math.Max(term.Length, 1);
            }
            return count;
        }

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));

        private sealed class Candidate
        {
            public string Path { get; set; } = "";
            public string Preview { get; set; } = "";
            public DateTime ModifiedAt { get; set; }
            public int Score { get; set; }
        }
    }
}
