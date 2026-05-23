using System;
using System.Linq;

namespace KokonoeAssistant.Services
{
    public static class KokoConversationBoundary
    {
        public static bool LooksLikeClosedUntilMorning(string? text)
        {
            var lower = (text ?? "").ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(lower)) return false;

            var closesConversation = ContainsAny(lower,
                "розмову завершено",
                "розмова завершена",
                "разговор завершен",
                "розмову закрито",
                "закриваю розмову",
                "до ранку",
                "до утра",
                "до завтра",
                "пиши зранку",
                "не пиши до ранку",
                "не турбуй до ранку");
            if (!closesConversation) return false;

            return ContainsAny(lower,
                "пішов", "піду", "йду", "іду", "буду", "грати", "спати", "розмов", "до ранку", "до утра", "до завтра");
        }

        public static bool LooksLikeShortApology(string? text)
        {
            var lower = (text ?? "").ToLowerInvariant().Trim();
            if (string.IsNullOrWhiteSpace(lower) || lower.Length > 48) return false;

            var normalized = new string(lower.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray());
            return ContainsAny(normalized,
                "вибач", "вибачаюсь", "сорі", "сори", "sorry", "извини", "извиняюсь", "прости");
        }

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));
    }
}
