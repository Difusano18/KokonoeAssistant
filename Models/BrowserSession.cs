using System;
using System.Collections.Generic;

namespace KokonoeAssistant.Models
{
    public enum BrowserSessionStatus
    {
        Idle,
        Navigating,
        Acting,
        Extracting,
        WaitingPermission,
        Error,
        Closed
    }

    public sealed class BrowserSession
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string CurrentUrl { get; set; } = "";
        public string PageTitle { get; set; } = "";
        public BrowserSessionStatus Status { get; set; } = BrowserSessionStatus.Idle;
        public string? LastError { get; set; }
        public string? LastScreenshotPath { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastActionAt { get; set; } = DateTime.UtcNow;
        public List<BrowserAction> History { get; set; } = new();
    }

    public sealed class BrowserAction
    {
        public string Kind { get; set; } = "";
        public string Target { get; set; } = "";
        public string? Value { get; set; }
        public string? Result { get; set; }
        public bool Success { get; set; }
        public DateTime At { get; set; } = DateTime.UtcNow;
    }
}
