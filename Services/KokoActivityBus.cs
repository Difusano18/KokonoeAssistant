using System;

namespace KokonoeAssistant.Services
{
    public sealed class KokoActivity
    {
        public string Kind   { get; set; } = "";        // tool | context | thinking | source
        public string Label  { get; set; } = "";        // human-readable
        public string Detail { get; set; } = "";
        public string Status { get; set; } = "running";  // running | done | failed
        public DateTime At   { get; set; } = DateTime.UtcNow;
    }

    // Single static bus rather than per-instance plumbing: there is one KokoToolGateway
    // singleton and (normally) one active chat UI subscriber, so this avoids threading an
    // activity callback through every tool handler and context-build call site.
    public static class KokoActivityBus
    {
        public static event Action<KokoActivity>? OnActivity;

        public static void Emit(KokoActivity activity)
        {
            try { OnActivity?.Invoke(activity); }
            catch (Exception ex) { KokoSystemLog.Write("ACTIVITY-BUS", "subscriber threw: " + ex.Message); }
        }
    }
}
