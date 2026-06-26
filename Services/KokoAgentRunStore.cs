using System;
using System.IO;
using Newtonsoft.Json;

namespace KokonoeAssistant.Services
{
    public sealed class KokoAgentRunStore
    {
        private readonly object _lock = new();
        private readonly string _directory;

        public KokoAgentRunStore(string dataDir)
        {
            if (string.IsNullOrWhiteSpace(dataDir))
                throw new ArgumentException("Agent data directory is empty.", nameof(dataDir));
            _directory = Path.Combine(Path.GetFullPath(dataDir), "agent-runs");
            Directory.CreateDirectory(_directory);
        }

        public KokoAgentRunState? Load(string runId)
        {
            var path = Resolve(runId);
            lock (_lock)
            {
                if (!File.Exists(path))
                    return null;
                try
                {
                    return JsonConvert.DeserializeObject<KokoAgentRunState>(File.ReadAllText(path));
                }
                catch (Exception ex)
                {
                    KokoSystemLog.Write("AGENT-RUN", $"load failed run={runId}: {ex.Message}");
                    return null;
                }
            }
        }

        public void Save(KokoAgentRunState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var path = Resolve(state.RunId);
            var temp = path + ".tmp";
            state.UpdatedAt = DateTime.UtcNow;
            lock (_lock)
            {
                try
                {
                    File.WriteAllText(temp, JsonConvert.SerializeObject(state, Formatting.Indented));
                    File.Move(temp, path, overwrite: true);
                }
                finally
                {
                    try { if (File.Exists(temp)) File.Delete(temp); }
                    catch (Exception ex) { KokoSystemLog.Write("AGENT-STORE", $"temp cleanup failed: {ex.Message}"); }
                }
            }
        }

        private string Resolve(string runId)
        {
            runId = (runId ?? "").Trim();
            if (runId.Length is < 4 or > 64 || runId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                runId.Contains("..", StringComparison.Ordinal))
                throw new ArgumentException("Invalid agent run id.", nameof(runId));
            return Path.Combine(_directory, runId + ".json");
        }
    }
}
