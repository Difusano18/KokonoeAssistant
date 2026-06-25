using System;

namespace KokonoeAssistant.Models
{
    public sealed class AgentDefinition
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string Name { get; set; } = "Agent";
        public string Description { get; set; } = "";
        public string BaseUrl { get; set; } = "";
        public string ApiKey { get; set; } = "";
        public string Model { get; set; } = "";
        public int MaxTokens { get; set; } = 4096;
        public bool Enabled { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastUsedAt { get; set; }
        public long TotalCalls { get; set; } = 0;
    }
}
