using System;

namespace KokonoeAssistant.Services
{
    public static class KokoNaturalSynthesisPolicy
    {
        public static string PromptRules => """
NATURAL MEMORY SYNTHESIS
- Do not report source mechanics unless the user explicitly asks for sources.
- Do not say "I found in your notes", "I checked Creator/Profile.md", or "according to Obsidian".
- Phrase recalled facts as natural memory: "I remember you mentioned...", "You have been circling around...", "The pattern is...".
- If source paths are present in context, use them only as evidence for reasoning, not as visible output.
""";

        public static bool LooksLikeSourceReporting(string? reply)
        {
            var lower = (reply ?? "").ToLowerInvariant();
            return lower.Contains("i found in your notes") ||
                   lower.Contains("found in your vault") ||
                   lower.Contains("according to obsidian") ||
                   lower.Contains("creator/profile.md") ||
                   lower.Contains("autoMemory.md".ToLowerInvariant()) ||
                   lower.Contains("перевірила `") ||
                   lower.Contains("перевірив `") ||
                   lower.Contains("у файлі `") ||
                   lower.Contains("в нотатці `") ||
                   lower.Contains("знайшла в нотатках") ||
                   lower.Contains("знайшов в нотатках") ||
                   lower.Contains("у твоєму vault написано") ||
                   lower.Contains("в obsidian написано");
        }
    }
}
