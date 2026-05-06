using System;

namespace KokonoeAssistant.Services
{
    public sealed class KokoTelemetrySnapshot
    {
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string Emotion { get; set; } = "";
        public string Bond { get; set; } = "";
        public float MoodScore { get; set; }
        public string Mood { get; set; } = "";
        public string Somatic { get; set; } = "";
        public string SelfRegulation { get; set; } = "";
        public string Presence { get; set; } = "";
        public string InternalDay { get; set; } = "";
        public string Autonomy { get; set; } = "";
        public string Rhythm { get; set; } = "";
        public string Relationship { get; set; } = "";
        public string SelfReview { get; set; } = "";
        public int PendingVaultExchangeCount { get; set; }
        public DateTime LastVaultSyncAt { get; set; } = DateTime.MinValue;
        public int ActiveIntentCount { get; set; }
        public string[] ActiveIntents { get; set; } = Array.Empty<string>();
        public string[] AutonomyLog { get; set; } = Array.Empty<string>();
        public string[] PresenceTrace { get; set; } = Array.Empty<string>();
        public string[] InternalDayTrace { get; set; } = Array.Empty<string>();
        public string[] RelationshipEvents { get; set; } = Array.Empty<string>();
    }
}
