using System;
using System.Linq;

namespace KokonoeAssistant.Services
{
    public sealed class KokoAgentCompletionNotice
    {
        public string Mode { get; set; } = "wait";
        public string Summary { get; set; } = "";
        public string NextQuestion { get; set; } = "";
        public string Notice { get; set; } = "";
    }

    public static class KokoAgentCompletionPolicy
    {
        public static KokoAgentCompletionNotice Build(KokoAgentTask task, DateTime? now = null)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));

            var failed = task.Steps.Where(s => s.Status == KokoAgentTaskStatus.Failed).ToList();
            var completed = task.Steps.Count(s => s.Status == KokoAgentTaskStatus.Completed);
            var total = Math.Max(1, task.Steps.Count);
            var objective = task.Objective.Trim();

            var notice = new KokoAgentCompletionNotice
            {
                Summary = failed.Count > 0
                    ? $"Задача {task.Id}: виконано {completed}/{total}, впало {failed.Count}."
                    : $"Задача {task.Id}: виконано {completed}/{total}.",
            };

            var lower = objective.ToLowerInvariant();
            if (failed.Count > 0)
            {
                notice.Mode = "question";
                notice.NextQuestion = "Зупинитися й розібрати першу помилку, чи пустити наступну спробу з іншим маршрутом?";
            }
            else if (LooksLikeImplementation(lower))
            {
                notice.Mode = "question";
                notice.NextQuestion = "Після цього прогнати ширшу перевірку або лишити як стабільний прототип?";
            }
            else if (LooksLikeResearch(lower) || task.Steps.Any(s => s.Kind == KokoAgentStepKind.Vault))
            {
                notice.Mode = "question";
                notice.NextQuestion = "Копати глибше по vault чи чекати твого наступного запиту?";
            }
            else
            {
                notice.Mode = "wait";
                notice.NextQuestion = "Чекаю наступного запиту.";
            }

            notice.Notice = notice.Mode == "question"
                ? $"{notice.Summary} {notice.NextQuestion}"
                : $"{notice.Summary} {notice.NextQuestion}";
            return notice;
        }

        private static bool LooksLikeImplementation(string lower)
            => ContainsAny(lower, "fix", "bug", "code", "build", "test", "пофікс", "виправ", "код", "збір", "тест", "реаліз");

        private static bool LooksLikeResearch(string lower)
            => ContainsAny(lower, "аналіз", "дослід", "порівн", "знайди", "перевір", "obsidian", "vault", "пам'ят");

        private static bool ContainsAny(string text, params string[] needles)
            => needles.Any(n => text.Contains(n, StringComparison.OrdinalIgnoreCase));
    }
}
