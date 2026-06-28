using System;
using System.Collections.Generic;
using System.Linq;

namespace KokonoeAssistant.Services
{
    public sealed class KokoDevLogEntry
    {
        public string Kind    { get; set; } = "";  // llm_request | llm_response | tool_call
        public string Label   { get; set; } = "";  // short human-readable summary
        public string Content { get; set; } = "";  // raw text/JSON, this is the point of the tab
        public DateTime At    { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Developer-only raw diagnostic feed: full LLM request/response bodies and raw tool
    /// call args/results, not the friendly labels KokoActivityBus shows. Built for live
    /// debugging while fixing real bugs blind (no other way to see exactly what's being
    /// sent/received) - keeps a rolling buffer so the dev tab has something to show on
    /// open instead of only events that happen to fire after it's opened.
    /// Never include API keys/tokens here - request dumps are bodies only, not headers.
    /// </summary>
    public static class KokoDevLogBus
    {
        private const int MaxEntries = 300;
        private const int MaxContentLength = 6000;

        private static readonly object Lock = new();
        private static readonly List<KokoDevLogEntry> Buffer = new();

        public static event Action<KokoDevLogEntry>? OnEntry;

        public static void Emit(string kind, string label, string? content)
        {
            var entry = new KokoDevLogEntry
            {
                Kind = kind,
                Label = label,
                Content = Trim(content),
            };

            lock (Lock)
            {
                Buffer.Add(entry);
                if (Buffer.Count > MaxEntries)
                    Buffer.RemoveAt(0);
            }

            try { OnEntry?.Invoke(entry); }
            catch (Exception ex) { KokoSystemLog.Write("DEVLOG-BUS", "subscriber threw: " + ex.Message); }
        }

        public static List<KokoDevLogEntry> Snapshot()
        {
            lock (Lock) { return Buffer.ToList(); }
        }

        private static string Trim(string? content)
        {
            if (string.IsNullOrEmpty(content)) return "";
            return content.Length > MaxContentLength
                ? content[..MaxContentLength] + $"\n...[truncated, {content.Length - MaxContentLength} more chars]"
                : content;
        }
    }
}
