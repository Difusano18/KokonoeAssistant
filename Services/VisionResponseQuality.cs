using System;

namespace KokonoeAssistant.Services
{
    public static class VisionResponseQuality
    {
        public static bool LooksUnusable(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return true;
            var lower = text.ToLowerInvariant();
            return lower.Contains("i cannot see") ||
                   lower.Contains("can't see") ||
                   lower.Contains("cannot view") ||
                   lower.Contains("no image") ||
                   lower.Contains("image not provided") ||
                   lower.Contains("vision server") ||
                   lower.Contains("vision-сервер") ||
                   lower.Contains("vision model") ||
                   lower.Contains("помилка llm") ||
                   lower.Contains("не бачу") ||
                   lower.Contains("не можу бачити") ||
                   lower.Contains("немає доступу до екрана") ||
                   lower.Contains("скріншот не надіслано") ||
                   lower.Contains("error") ||
                   lower.Contains("http 500") ||
                   lower.Contains("500");
        }

        public static bool LooksGeneric(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return true;
            var lower = text.ToLowerInvariant();
            if (lower.Length < 35) return true;
            return lower.Contains("looks like a screenshot") ||
                   lower.Contains("there is an image") ||
                   lower.Contains("на зображенні щось видно") ||
                   lower.Contains("це схоже на скріншот") ||
                   lower.Contains("скріншот зроблено") ||
                   lower.Contains("не можу визначити деталі");
        }

        public static string BuildRetryPrompt(string originalPrompt, string foregroundSummary)
            => originalPrompt + "\n\nVISION AUTO-REPAIR RETRY\n" +
               "The first vision answer was empty, generic, or claimed it could not see the image. " +
               "The image has been contrast/sharpness enhanced. Use the pixels plus this foreground context, then give concrete visible details. " +
               "Do not say you cannot see the screen unless the enhanced image is truly blank.\n" +
               "Foreground window: " + (string.IsNullOrWhiteSpace(foregroundSummary) ? "-" : foregroundSummary);
    }
}
