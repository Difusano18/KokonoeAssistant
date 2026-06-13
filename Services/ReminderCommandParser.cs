using System;
using System.Text.RegularExpressions;

namespace KokonoeAssistant.Services
{
    public sealed class ReminderCommand
    {
        public DateTime FireAt { get; set; }
        public string Prompt { get; set; } = "";
        public bool IsWake { get; set; }
        public bool UsedAssumedLater { get; set; }
    }

    public static class ReminderCommandParser
    {
        public static bool TryParse(string userText, DateTime now, out ReminderCommand command)
        {
            command = new ReminderCommand();
            if (string.IsNullOrWhiteSpace(userText)) return false;

            var lower = userText.ToLowerInvariant();
            var wantsWake = ContainsAny(lower, "розбуд", "будиль", "wake", "збуд", "будил");
            var wantsReminder = ContainsAny(lower, "нагад", "нагадай", "нагадати", "remind", "напиши", "пінгани", "пінг",
                "нагад", "напиши");
            if (!wantsWake && !wantsReminder) return false;
            if (LooksLikeLaterConversationStatus(lower) &&
                !ContainsAny(lower, "\u043d\u0430\u0433\u0430\u0434", "\u043d\u0430\u0433\u0430\u0434\u0430\u0439", "remind", "\u043d\u0430\u043f\u0438\u0448\u0438", "\u043f\u0456\u043d\u0433"))
                return false;

            if (!TryParseFireAt(lower, now, out var fireAt, out var assumedLater))
                return false;

            command = new ReminderCommand
            {
                FireAt = fireAt,
                IsWake = wantsWake,
                UsedAssumedLater = assumedLater,
                Prompt = wantsWake
                    ? "Розбуди користувача. Коротко, різко, українською. Без пояснення системи."
                    : "Нагадай користувачу: " + userText.Trim()
            };
            return true;
        }

        private static bool TryParseFireAt(string lower, DateTime now, out DateTime fireAt, out bool assumedLater)
        {
            assumedLater = false;
            fireAt = default;

            var relative = Regex.Match(lower, @"(?:через|за)\s*(\d{1,3})\s*(хв|хвилин|хвилини|мин|m|min|год|годин|години|h)\b");
            if (relative.Success && int.TryParse(relative.Groups[1].Value, out var amount))
            {
                var unit = relative.Groups[2].Value;
                fireAt = unit.StartsWith("год", StringComparison.Ordinal) || unit == "h"
                    ? now.AddHours(Math.Clamp(amount, 1, 72))
                    : now.AddMinutes(Math.Clamp(amount, 1, 24 * 60));
                return true;
            }

            if (Regex.IsMatch(lower, @"(?:через|за)\s+(?:пів|півгодини|пів\s+години)"))
            {
                fireAt = now.AddMinutes(30);
                return true;
            }

            if (Regex.IsMatch(lower, @"(?:через|за)\s+годин[ауи]?|(?:через|за)\s+1\s*год"))
            {
                fireAt = now.AddHours(1);
                return true;
            }

            var absolute = Regex.Match(lower, @"(?:о|об|в|у|на|at)\s*(\d{1,2})(?::(\d{2}))?");
            if (absolute.Success && int.TryParse(absolute.Groups[1].Value, out var hour))
            {
                var minute = 0;
                if (absolute.Groups[2].Success)
                    int.TryParse(absolute.Groups[2].Value, out minute);
                hour = Math.Clamp(hour, 0, 23);
                minute = Math.Clamp(minute, 0, 59);

                fireAt = now.Date.AddHours(hour).AddMinutes(minute);
                if (fireAt <= now.AddMinutes(1))
                    fireAt = fireAt.AddDays(1);
                return true;
            }

            if (ContainsAny(lower, "пізніше", "попізніше", "потім", "якось потім", "колись потім", "later"))
            {
                fireAt = now.AddMinutes(30);
                assumedLater = true;
                return true;
            }

            return false;
        }

        private static bool LooksLikeLaterConversationStatus(string lower)
        {
            if (string.IsNullOrWhiteSpace(lower)) return false;
            var saysContinueLater = ContainsAny(lower,
                "\u043f\u043e\u0442\u0456\u043c \u043f\u0440\u043e\u0434\u043e\u0432\u0436",
                "\u043f\u0456\u0437\u043d\u0456\u0448\u0435 \u043f\u0440\u043e\u0434\u043e\u0432\u0436",
                "later continue",
                "continue later");
            var saysAway = ContainsAny(lower,
                "\u044f \u0432\u0438\u0439\u0448\u043e\u0432",
                "\u044f \u0432\u0438\u0439\u0448\u043b\u0430",
                "\u0432\u0438\u0439\u0448\u043e\u0432 \u043d\u0430 \u043f\u0440\u043e\u0433\u0443\u043b",
                "\u043f\u0456\u0448\u043e\u0432 \u043d\u0430 \u043f\u0440\u043e\u0433\u0443\u043b",
                "\u0432\u0438\u0439\u0448\u043e\u0432 \u0437 \u0434\u0440\u0443\u0433",
                "\u043f\u0456\u0448\u043e\u0432 \u0437 \u0434\u0440\u0443\u0433",
                "i went out",
                "going for a walk");
            return saysContinueLater || (ContainsAny(lower, "\u043f\u043e\u0442\u0456\u043c", "\u043f\u0456\u0437\u043d\u0456\u0448\u0435", "later") && saysAway);
        }

        private static bool ContainsAny(string text, params string[] values)
        {
            foreach (var value in values)
                if (text.Contains(value, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
