using System;
using System.Collections.Generic;

namespace KokonoeAssistant.Services
{
    public enum KokoUiMode
    {
        Web,
        LegacyWpf
    }

    public sealed record KokoUiStartupDecision(KokoUiMode Mode, string Source);

    public static class KokoUiStartupPolicy
    {
        public const string EnvironmentVariable = "KOKONOE_UI";

        public static KokoUiStartupDecision Resolve(
            IEnumerable<string>? args,
            string? environmentMode,
            string? configuredMode)
        {
            foreach (var arg in args ?? Array.Empty<string>())
            {
                if (arg.Equals("--legacy-wpf", StringComparison.OrdinalIgnoreCase))
                    return new KokoUiStartupDecision(KokoUiMode.LegacyWpf, "argument:--legacy-wpf");
                if (arg.Equals("--web-shell", StringComparison.OrdinalIgnoreCase))
                    return new KokoUiStartupDecision(KokoUiMode.Web, "argument:--web-shell");
            }

            if (TryParse(environmentMode, out var environmentDecision))
                return new KokoUiStartupDecision(environmentDecision, $"environment:{EnvironmentVariable}");

            return new KokoUiStartupDecision(
                ParseOrWeb(configuredMode),
                "settings:UiShell");
        }

        public static string NormalizeConfiguredMode(string? value)
            => ParseOrWeb(value) == KokoUiMode.LegacyWpf ? "legacy" : "web";

        private static KokoUiMode ParseOrWeb(string? value)
            => TryParse(value, out var mode) ? mode : KokoUiMode.Web;

        private static bool TryParse(string? value, out KokoUiMode mode)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "legacy":
                case "wpf":
                case "legacy-wpf":
                    mode = KokoUiMode.LegacyWpf;
                    return true;
                case "web":
                case "webview":
                case "webview2":
                    mode = KokoUiMode.Web;
                    return true;
                default:
                    mode = KokoUiMode.Web;
                    return false;
            }
        }
    }
}
