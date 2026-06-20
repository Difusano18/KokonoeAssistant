using System;
using System.Linq;

namespace KokonoeAssistant.Services
{
    public enum KokoActionDirectiveRoute
    {
        None,
        LocalArtifact,
        AgentTask
    }

    public sealed class KokoActionDirective
    {
        public bool IsAction { get; set; }
        public KokoActionDirectiveRoute Route { get; set; } = KokoActionDirectiveRoute.None;
        public string Reason { get; set; } = "";
        public int Confidence { get; set; }
    }

    public static class KokoActionDirectiveRouter
    {
        public static KokoActionDirective Analyze(string? text)
        {
            var lower = Normalize(text);
            if (string.IsNullOrWhiteSpace(lower) || LooksLikePureConversation(lower))
                return None("conversation");

            var hasActionVerb = ContainsAny(lower, ActionVerbs);
            var hasCreationVerb = ContainsAny(lower, CreationVerbs);
            var hasSearchVerb = ContainsAny(lower, SearchVerbs);
            var hasFixVerb = ContainsAny(lower, FixVerbs);
            var localTarget = ContainsAny(lower, LocalTargets);
            var artifactTarget = ContainsAny(lower, ArtifactTargets);
            var projectTarget = ContainsAny(lower, ProjectTargets);
            var autonomyCue = ContainsAny(lower, AutonomyCues);
            var riskyDestructive = ContainsAny(lower, RiskyDestructiveCues);

            if (riskyDestructive)
                return None("risky destructive action must use explicit PC policy route");

            var isAction = hasActionVerb || hasCreationVerb || hasSearchVerb || hasFixVerb;
            if (!isAction)
                return None("no executable directive");

            if ((hasCreationVerb || hasSearchVerb || autonomyCue || (hasActionVerb && artifactTarget && !projectTarget)) &&
                (artifactTarget || localTarget))
            {
                return new KokoActionDirective
                {
                    IsAction = true,
                    Route = KokoActionDirectiveRoute.LocalArtifact,
                    Confidence = 85,
                    Reason = "local artifact/search directive"
                };
            }

            if ((hasFixVerb || hasActionVerb || autonomyCue) && (projectTarget || localTarget || artifactTarget))
            {
                return new KokoActionDirective
                {
                    IsAction = true,
                    Route = KokoActionDirectiveRoute.AgentTask,
                    Confidence = 70,
                    Reason = "multi-step action directive"
                };
            }

            return new KokoActionDirective
            {
                IsAction = true,
                Route = KokoActionDirectiveRoute.AgentTask,
                Confidence = 55,
                Reason = "generic executable directive"
            };
        }

        public static bool ShouldCreateLocalArtifact(string? text)
            => Analyze(text).Route == KokoActionDirectiveRoute.LocalArtifact;

        public static bool ShouldStartAgentTask(string? text)
        {
            var plan = Analyze(text);
            return plan.Route == KokoActionDirectiveRoute.AgentTask && plan.Confidence >= 60;
        }

        private static KokoActionDirective None(string reason) => new()
        {
            IsAction = false,
            Route = KokoActionDirectiveRoute.None,
            Reason = reason
        };

        private static string Normalize(string? text)
        {
            var lower = (text ?? "").ToLowerInvariant();
            lower = lower.Replace('ё', 'е');
            lower = string.Join(" ", lower.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return lower.Trim();
        }

        private static bool LooksLikePureConversation(string lower)
        {
            if (lower.Length <= 32 &&
                (ShortConversationReplies.Contains(lower, StringComparer.OrdinalIgnoreCase) ||
                 ContainsAny(lower, "привіт", "привет", "поговоримо", "просто поговорити", "що думаєш", "як справи")))
                return true;

            return ContainsAny(lower,
                       "скажи щось миле", "побудь", "просто поговор", "флірт", "заігру")
                   && !ContainsAny(lower, ActionVerbs)
                   && !ContainsAny(lower, CreationVerbs);
        }

        private static readonly string[] ShortConversationReplies =
        {
            "хм", "угу", "ок", "окей", "ясно", "дякую", "спс", "добре"
        };

        private static readonly string[] ActionVerbs =
        {
            "зроби", "роби", "виконай", "виконуй", "запусти", "прожени", "перевір", "провір",
            "подивись", "глянь", "проскануй", "скануй", "розбери", "проаналізуй", "налаштуй",
            "підключи", "додай", "онови", "дороби", "покращ", "реалізуй", "постав", "збери",
            "сделай", "выполни", "запусти", "проверь", "посмотри", "сканируй", "настрой",
            "добавь", "обнови", "доработай", "улучши", "реализуй", "собери",
            "do", "run", "execute", "check", "scan", "inspect", "analyze", "configure",
            "add", "update", "implement", "build", "test"
        };

        private static readonly string[] CreationVerbs =
        {
            "створи", "создай", "залиш", "остав", "запиши", "напиши", "збережи", "сохрани",
            "поклади", "кинь", "сформуй", "згенеруй", "створити", "создать",
            "create", "write", "save", "leave", "generate", "drop"
        };

        private static readonly string[] SearchVerbs =
        {
            "пошукай", "знайди", "найди", "пошарь", "пошукай щось", "знайди щось",
            "шукай", "ищи", "search", "find", "look for"
        };

        private static readonly string[] FixVerbs =
        {
            "пофікси", "виправ", "почини", "зремонтуй", "пофикси", "исправь", "почини",
            "fix", "repair", "debug", "resolve"
        };

        private static readonly string[] LocalTargets =
        {
            "пк", "pc", "комп", "компі", "компе", "комп'ютер", "компьютер", "windows",
            "на комп'ютері", "до комп'ютера", "на компьютере", "с компьютера",
            "система", "у системі", "системою", "в системе", "системой",
            "залізо", "на залізі", "железо", "на железе",
            "машина", "на машині", "на машине", "диск", "на диску", "на диске",
            "desktop", "робочий стіл", "робочому столі", "робочого столу", "на столі", "зі столу",
            "рабочий стол", "рабочем столе", "рабочего стола", "на столе", "со стола",
            "downloads", "загрузки", "завантаження", "у завантаженнях", "папка", "папку",
            "у папці", "з папки", "в папке", "из папки", "каталог", "у каталозі", "в каталоге",
            "folder", "directory"
        };

        private static readonly string[] ArtifactTargets =
        {
            "файл", "у файлі", "до файлу", "в файле", "в файл", "file", "txt", "md",
            "нотат", "у нотатці", "до нотатки", "в заметке", "заміт", "note",
            "артефакт", "артефакті", "сюрприз", "сюрпризом", "сюрпризу",
            "подарунок", "подарунку", "подарком", "gift", "звіт", "у звіті", "отчете", "report",
            "лог", "у лозі", "в логе", "log", "документ", "у документі", "в документе"
        };

        private static readonly string[] ProjectTargets =
        {
            "код", "у коді", "над кодом", "в коде", "code",
            "проект", "проєкт", "у проєкті", "над проєктом", "в проекте", "над проектом", "project",
            "repo", "репо", "репозиторій", "у репозиторії", "в репозитории",
            "gui", "інтерфейс", "в інтерфейсі", "в интерфейсе", "interface",
            "тест", "у тестах", "в тестах", "tests", "білд", "у білді", "в билде", "build",
            "obsidian", "обсидіан", "в обсидіані", "vault", "у сховищі",
            "telegram", "телеграм", "у телеграмі", "watch", "годин", "на годиннику", "на часах"
        };

        private static readonly string[] AutonomyCues =
        {
            "сам", "сама", "самостійно", "як краще", "придумай", "будь-як", "що завгодно",
            "на твій вибір", "на твій розсуд", "на власний розсуд", "за власним рішенням",
            "без мого втручання", "без додаткових питань", "повністю автономно",
            "по-своєму", "по своему", "на свое усмотрение", "без моего вмешательства",
            "актуально", "ще раз", "спробуй", "попробуй", "нормально",
            "solid", "manus", "autonomous", "do whatever"
        };

        private static readonly string[] RiskyDestructiveCues =
        {
            "видали все", "удали все", "format", "форматни", "стерти диск", "wipe disk",
            "shutdown", "перезавантаж", "reboot", "kill all"
        };

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));
    }
}
