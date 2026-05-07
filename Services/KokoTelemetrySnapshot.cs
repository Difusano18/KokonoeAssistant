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
        public string AutonomyDebug { get; set; } = "";
        public string Rhythm { get; set; } = "";
        public string Timeline { get; set; } = "";
        public string TimelineState { get; set; } = "";
        public string StateFreshness { get; set; } = "";
        public string Relationship { get; set; } = "";
        public string Attachment { get; set; } = "";
        public string SelfReview { get; set; } = "";
        public string PostReplyGuard { get; set; } = "";
        public string LlmStatus { get; set; } = "";
        public string LlmProvider { get; set; } = "";
        public string LlmModel { get; set; } = "";
        public string LlmLastError { get; set; } = "";
        public string LlmLastFallback { get; set; } = "";
        public DateTime LlmLastRequestAt { get; set; } = DateTime.MinValue;
        public DateTime LlmLastSuccessAt { get; set; } = DateTime.MinValue;
        public DateTime LlmLastErrorAt { get; set; } = DateTime.MinValue;
        public long LlmLastLatencyMs { get; set; }
        public int LlmConsecutiveFailures { get; set; }
        public string ScenarioHealth { get; set; } = "";
        public int PendingVaultExchangeCount { get; set; }
        public DateTime LastVaultSyncAt { get; set; } = DateTime.MinValue;
        public int ActiveIntentCount { get; set; }
        public string[] ActiveIntents { get; set; } = Array.Empty<string>();
        public string[] AutonomyLog { get; set; } = Array.Empty<string>();
        public string[] PresenceTrace { get; set; } = Array.Empty<string>();
        public string[] InternalDayTrace { get; set; } = Array.Empty<string>();
        public string[] RelationshipEvents { get; set; } = Array.Empty<string>();
        public string[] ScenarioFindings { get; set; } = Array.Empty<string>();
    }
}
