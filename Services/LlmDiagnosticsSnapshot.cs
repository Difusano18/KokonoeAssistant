using System;

namespace KokonoeAssistant.Services
{
    public sealed class LlmDiagnosticsSnapshot
    {
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string Status { get; set; } = "idle";
        public string Provider { get; set; } = "";
        public string Model { get; set; } = "";
        public string Channel { get; set; } = "";
        public int? LastStatusCode { get; set; }
        public string LastError { get; set; } = "";
        public string LastFallback { get; set; } = "";
        public DateTime LastRequestAt { get; set; } = DateTime.MinValue;
        public DateTime LastSuccessAt { get; set; } = DateTime.MinValue;
        public DateTime LastErrorAt { get; set; } = DateTime.MinValue;
        public long LastLatencyMs { get; set; }
        public int InFlight { get; set; }
        public int ConsecutiveFailures { get; set; }
        public long TotalRequests { get; set; }
        public long TotalFailures { get; set; }
    }
}
