using System;

namespace KokonoeAssistant.Services
{
    public sealed class KokoTelegramChannelStatus
    {
        public bool Enabled { get; set; }
        public bool Configured { get; set; }
        public string State { get; set; } = "unknown";
        public string Account { get; set; } = "";
        public string LastActivity { get; set; } = "";
        public DateTime? LastActivityAt { get; set; }
        public string LastError { get; set; } = "";
        public DateTime? LastErrorAt { get; set; }
        public string LastMessageFrom { get; set; } = "";
        public string LastMessagePreview { get; set; } = "";
        public DateTime? LastMessageAt { get; set; }
    }

    public sealed class KokoTelegramRuntimeSnapshot
    {
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public KokoTelegramChannelStatus Bot { get; set; } = new();
        public KokoTelegramChannelStatus User { get; set; } = new();
    }

    public sealed class KokoTelegramRuntimeStatusService
    {
        private readonly object _lock = new();
        private KokoTelegramRuntimeSnapshot _state = new();
        public event Action<KokoTelegramRuntimeSnapshot>? Changed;

        public void RefreshConfiguration(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            Mutate(state =>
            {
                state.Bot.Enabled = settings.TelegramEnabled;
                state.Bot.Configured = !string.IsNullOrWhiteSpace(settings.TelegramToken) && settings.TelegramChatId > 0;
                state.User.Enabled = settings.TgUserEnabled;
                state.User.Configured = settings.TgApiId > 0 && !string.IsNullOrWhiteSpace(settings.TgApiHash);
                NormalizeIdleState(state.Bot);
                NormalizeIdleState(state.User);
            });
        }

        public void MarkBotState(string state, string error = "")
            => Mutate(snapshot => ApplyState(snapshot.Bot, state, error, ""));

        public void MarkUserState(string state, string account = "", string error = "")
            => Mutate(snapshot => ApplyState(snapshot.User, state, error, account));

        public void RecordBotActivity(string activity)
            => Mutate(snapshot => ApplyActivity(snapshot.Bot, activity));

        public void RecordUserActivity(string activity)
            => Mutate(snapshot => ApplyActivity(snapshot.User, activity));

        public void RecordBotError(string error)
            => Mutate(snapshot => ApplyError(snapshot.Bot, error));

        public void RecordUserError(string error)
            => Mutate(snapshot => ApplyError(snapshot.User, error));

        public void RecordBotMessage(string from, string text)
            => Mutate(snapshot => ApplyMessage(snapshot.Bot, from, text));

        public void RecordUserMessage(string from, string text)
            => Mutate(snapshot => ApplyMessage(snapshot.User, from, text));

        public KokoTelegramRuntimeSnapshot GetSnapshot()
        {
            lock (_lock)
                return Clone(_state);
        }

        private void Mutate(Action<KokoTelegramRuntimeSnapshot> mutation)
        {
            KokoTelegramRuntimeSnapshot snapshot;
            lock (_lock)
            {
                mutation(_state);
                _state.UpdatedAt = DateTime.UtcNow;
                snapshot = Clone(_state);
            }
            try { Changed?.Invoke(snapshot); }
            catch (Exception ex) { KokoSystemLog.Write("TELEGRAM-STATUS", "subscriber failed: " + ex.Message); }
        }

        private static void NormalizeIdleState(KokoTelegramChannelStatus channel)
        {
            if (!channel.Enabled)
                channel.State = "disabled";
            else if (!channel.Configured)
                channel.State = "not_configured";
            else if (channel.State is "unknown" or "disabled" or "not_configured")
                channel.State = "idle";
        }

        private static void ApplyState(KokoTelegramChannelStatus channel, string state, string error, string account)
        {
            channel.State = Clean(state, 32, "unknown");
            if (!string.IsNullOrWhiteSpace(account))
                channel.Account = Clean(account, 80, "");
            if (!string.IsNullOrWhiteSpace(error))
                ApplyError(channel, error);
            else if (channel.State is "connected" or "listening")
            {
                channel.LastError = "";
                channel.LastErrorAt = null;
            }
        }

        private static void ApplyActivity(KokoTelegramChannelStatus channel, string activity)
        {
            channel.LastActivity = Clean(activity, 32, "activity");
            channel.LastActivityAt = DateTime.UtcNow;
        }

        private static void ApplyError(KokoTelegramChannelStatus channel, string error)
        {
            channel.State = "error";
            channel.LastError = Clean(error, 180, "transport error");
            channel.LastErrorAt = DateTime.UtcNow;
        }

        private static void ApplyMessage(KokoTelegramChannelStatus channel, string from, string text)
        {
            channel.LastMessageFrom = Clean(from, 80, "unknown");
            channel.LastMessagePreview = Clean(text, 200, "");
            channel.LastMessageAt = DateTime.UtcNow;
        }

        private static string Clean(string value, int max, string fallback)
        {
            var clean = (value ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (clean.Length == 0) return fallback;
            return clean.Length <= max ? clean : clean[..max] + "...";
        }

        private static KokoTelegramRuntimeSnapshot Clone(KokoTelegramRuntimeSnapshot source) => new()
        {
            UpdatedAt = source.UpdatedAt,
            Bot = CloneChannel(source.Bot),
            User = CloneChannel(source.User)
        };

        private static KokoTelegramChannelStatus CloneChannel(KokoTelegramChannelStatus source) => new()
        {
            Enabled = source.Enabled,
            Configured = source.Configured,
            State = source.State,
            Account = source.Account,
            LastActivity = source.LastActivity,
            LastActivityAt = source.LastActivityAt,
            LastError = source.LastError,
            LastErrorAt = source.LastErrorAt,
            LastMessageFrom = source.LastMessageFrom,
            LastMessagePreview = source.LastMessagePreview,
            LastMessageAt = source.LastMessageAt
        };
    }
}
