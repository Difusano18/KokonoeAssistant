using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KokonoeAssistant.Models;
using Newtonsoft.Json.Linq;

namespace KokonoeAssistant.Services
{
    public sealed class KokoWebAgentPoolBridgeService
    {
        private readonly KokoAgentPoolService _pool;

        public KokoWebAgentPoolBridgeService(KokoWebBridgeService bridge, KokoAgentPoolService pool)
        {
            if (bridge == null) throw new ArgumentNullException(nameof(bridge));
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            bridge.Register("agents.list", HandleListAsync);
            bridge.Register("agents.save", HandleSaveAsync);
            bridge.Register("agents.delete", HandleDeleteAsync);
            bridge.Register("agents.test", HandleTestAsync);
        }

        private async Task<object?> HandleListAsync(JToken? payload, CancellationToken ct)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            return AppSettings.Load().AgentPool.Select(Project).ToList();
        }

        private async Task<object?> HandleSaveAsync(JToken? payload, CancellationToken ct)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();

            var settings = AppSettings.Load();
            var id = payload?["id"]?.ToString();
            var agent = !string.IsNullOrWhiteSpace(id)
                ? settings.AgentPool.FirstOrDefault(a => a.Id == id)
                : null;

            if (agent == null)
            {
                agent = new AgentDefinition();
                settings.AgentPool.Add(agent);
            }

            if (payload?["name"] is { } name) agent.Name = name.ToString();
            if (payload?["description"] is { } description) agent.Description = description.ToString();
            if (payload?["model"] is { } model) agent.Model = model.ToString();
            if (payload?["baseUrl"] is { } baseUrl) agent.BaseUrl = baseUrl.ToString();
            if (payload?["maxTokens"] is { } maxTokens) agent.MaxTokens = (int)maxTokens;
            if (payload?["enabled"] is { } enabled) agent.Enabled = (bool)enabled;

            // ApiKey: only overwrite when a non-empty value was sent — the UI
            // always leaves this field blank when editing an existing agent,
            // same masked-credential convention as ollamaApiKey/claudeApiKey.
            var key = payload?["apiKey"]?.ToString();
            if (!string.IsNullOrWhiteSpace(key))
                agent.ApiKey = key;

            settings.Save();
            KokoSystemLog.Write("AGENT-POOL", $"Agent '{agent.Name}' saved.");
            return new { ok = true, id = agent.Id };
        }

        private async Task<object?> HandleDeleteAsync(JToken? payload, CancellationToken ct)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();

            var settings = AppSettings.Load();
            var id = payload?["id"]?.ToString() ?? "";
            var removed = settings.AgentPool.RemoveAll(a => a.Id == id) > 0;
            if (removed)
                settings.Save();
            return new { ok = removed };
        }

        private async Task<object?> HandleTestAsync(JToken? payload, CancellationToken ct)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();

            var id = payload?["id"]?.ToString() ?? "";
            try
            {
                var result = await _pool.InvokeAsync(id, "You are a helpful assistant.", "Reply with exactly: OK", ct)
                    .ConfigureAwait(false);
                return new { ok = result.Contains("OK"), response = result.Trim() };
            }
            catch (Exception ex)
            {
                return new { ok = false, response = ex.Message };
            }
        }

        private static object Project(AgentDefinition a) => new
        {
            id = a.Id,
            name = a.Name,
            description = a.Description,
            model = a.Model,
            baseUrl = a.BaseUrl,
            enabled = a.Enabled,
            maxTokens = a.MaxTokens,
            totalCalls = a.TotalCalls,
            lastUsed = a.LastUsedAt?.ToString("HH:mm") ?? "—",
            hasKey = !string.IsNullOrWhiteSpace(a.ApiKey)
        };
    }
}
