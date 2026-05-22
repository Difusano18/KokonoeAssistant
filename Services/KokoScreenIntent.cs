using System;
using System.Linq;

namespace KokonoeAssistant.Services
{
    public static class KokoScreenIntent
    {
        public static bool IsManualScreenScan(string? text)
        {
            var lower = Normalize(text);
            if (string.IsNullOrWhiteSpace(lower)) return false;
            if (ContainsAny(lower,
                    "не дивись", "не підглядуй", "не скануй", "не проскан", "не треба скан",
                    "не смотри", "не сканируй"))
                return false;

            var targetScreen = MentionsScreenTarget(lower);
            var wantsScan = ContainsAny(lower,
                "проскан", "скануй", "сканувати", "сканируй", "сканировать",
                "подив", "поглянь", "глянь", "посмотр", "провір", "перевір",
                "проаналіз", "проанализ", "зроби скрін", "зроби скрин",
                "зроби знім", "зніми", "зняти", "сфот", "зфот", "фотк",
                "сфоткай", "сфоткати", "сфотограф", "фото екра", "фото мого екра",
                "сделай скрин", "сними скрин", "сними экран", "сфоткай экран", "сфотографируй экран",
                "screenshot", "take screenshot", "take a screenshot", "photo of screen",
                "screen capture", "capture screen");
            if (wantsScan && targetScreen) return true;

            var compact = Compact(lower);
            var compactWantsScreen = ContainsAny(compact,
                "зробискрін", "зробитискрін", "зробискрин", "зробитискрин",
                "знімиекран", "знятиекран", "сфоткайекран", "сфоткатиекран",
                "сфотографуйекран", "сфотографуватиекран", "фотоекрана",
                "фотомогоекрана", "скрінмогоекрана", "скринмогоекрана",
                "сделайскрин", "снимиэкран", "сфоткайэкран", "фотоэкрана",
                "takescreenshot", "screencapture", "capturescreen");
            if (compactWantsScreen && targetScreen) return true;

            var asksVisibleState = ContainsAny(lower,
                "що в мене на", "що у мене на", "шо в мене на", "шо у мене на",
                "що на", "шо на", "что на", "what is on", "what's on",
                "що видно", "шо видно", "что видно", "що відкрит", "шо відкрит",
                "що запущ", "що бачиш", "шо бачиш", "что видишь");
            if (asksVisibleState && targetScreen) return true;

            var asksTabsOrApps = ContainsAny(lower, "вкладк", "таб", "програм", "вікн", "окн", "window");
            return asksTabsOrApps && targetScreen && ContainsAny(lower, "які", "какие", "що", "шо", "what");
        }

        public static bool IsRetryLastScreenScan(string? text, string? lastRequest, DateTime lastAt, DateTime now)
        {
            var lower = Normalize(text);
            if (string.IsNullOrWhiteSpace(lower)) return false;
            if (string.IsNullOrWhiteSpace(lastRequest)) return false;
            if (now - lastAt > TimeSpan.FromMinutes(20)) return false;

            var wantsRetry = ContainsAny(lower,
                "ще раз", "спробуй ще", "спробуй знов", "попробуй ще", "попробуй знов",
                "повтори", "повторно", "перепробуй", "давай знов", "давай еще",
                "retry", "try again", "again");
            if (!wantsRetry) return false;

            return lower.Length <= 72 || MentionsScreenTarget(lower) ||
                   ContainsAny(lower, "це", "так", "знімок", "снімок", "аналіз", "картин");
        }

        public static bool LooksLikeScreenCapabilityDenial(string? reply)
        {
            var lower = Normalize(reply);
            if (string.IsNullOrWhiteSpace(lower)) return false;

            return ContainsAny(lower,
                "не маю прямого виду", "не маю прямого доступу", "не маю доступу до твого екра",
                "не бачу твій екран", "не бачу твого екра", "не можу бачити твій екран",
                "не можу бачити екран", "немає доступу до твого екра", "магічним чином без вхідних даних",
                "завантаж скрін", "завантаж скрин", "завантаж зображення", "зроби скриншот і завантаж",
                "зроби скріншот і завантаж", "я не вірус", "не шпигунське пз", "не шпигунське по",
                "не можу сфоткати", "не можу сфотографувати", "не можу зняти твій екран",
                "не можу зробити знімок", "не можу зробити скрін", "не можу зробити скрин",
                "драйверів камери", "інструментів захоплення екрана",
                "без вхідних даних", "без твоїх дій");
        }

        private static bool MentionsScreenTarget(string lower)
            => ContainsAny(lower,
                "екран", "екрані", "екране", "скрін", "скрин", "скріншот", "скриншот",
                "screen", "монітор", "монитор", "дисплей", "desktop", "робочий стіл",
                "рабочий стол");

        private static string Normalize(string? text)
            => (text ?? "").Replace('\u2019', '\'').Replace('\u02bc', '\'').Trim().ToLowerInvariant();

        private static string Compact(string text)
            => new(text.Where(char.IsLetterOrDigit).ToArray());

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));
    }
}
