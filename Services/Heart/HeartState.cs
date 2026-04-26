using System;

namespace KokonoeAssistant.Services.Heart
{
    public sealed class HeartState
    {
        public double BaselineBpm { get; set; } = 62.0;
        public double LastBpm { get; set; } = 62.0;
        public DateTime LastSavedUtc { get; set; } = DateTime.UtcNow;
        public long TotalBeats { get; set; }
    }
}
