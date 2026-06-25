using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KokonoeAssistant.Models;
using Newtonsoft.Json.Linq;

namespace KokonoeAssistant.Services
{
    public sealed class KokoAgentPoolService
    {
        private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(180) };

        public IReadOnlyList<AgentDefinition> GetAll() => AppSettings.Load().AgentPool;

        public IReadOnlyList<AgentDefinition> GetEnabled() =>
            AppSettings.Load().AgentPool.Where(a => a.Enabled).ToList();

        public AgentDefinition? Get(string id) =>
            AppSettings.Load().AgentPool.FirstOrDefault(a => a.Id == id);

        /// <summary>
        /// Виконати prompt через конкретного агента.
        /// Повертає повну відповідь (не streaming — агенти працюють у фоні).
        /// </summary>
        public async Task<string> InvokeAsync(
            string agentId,
            string systemPrompt,
            string userMessage,
            CancellationToken ct = default)
        {
            var settings = AppSettings.Load();
            var agent = settings.AgentPool.FirstOrDefault(a => a.Id == agentId)
                ?? throw new InvalidOperationException($"Agent '{agentId}' not found.");

            if (!agent.Enabled)
                throw new InvalidOperationException($"Agent '{agent.Name}' is disabled.");
            if (string.IsNullOrWhiteSpace(agent.BaseUrl))
                throw new InvalidOperationException($"Agent '{agent.Name}' has no base URL configured.");

            // Anthropic's API doesn't speak the OpenAI dialect this method otherwise
            // assumes everywhere else: different endpoint path, x-api-key/anthropic-version
            // instead of Authorization: Bearer, and a different request/response shape
            // (system is a top-level field, no per-message system role; reply text is
            // content[0].text, not choices[0].message.content). Detected off BaseUrl since
            // AgentDefinition has no explicit provider field.
            var isClaude = agent.BaseUrl.Contains("anthropic.com", StringComparison.OrdinalIgnoreCase);

            JObject payload = isClaude
                ? JObject.FromObject(new
                {
                    model = agent.Model,
                    max_tokens = agent.MaxTokens,
                    system = systemPrompt,
                    messages = new object[] { new { role = "user", content = userMessage } }
                })
                : JObject.FromObject(new
                {
                    model = agent.Model,
                    stream = false,
                    max_tokens = agent.MaxTokens,
                    messages = new object[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userMessage }
                    }
                });

            var endpoint = isClaude
                ? agent.BaseUrl.TrimEnd('/') + "/v1/messages"
                : agent.BaseUrl.TrimEnd('/') + "/chat/completions";

            using var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json")
            };
            if (isClaude)
            {
                if (!string.IsNullOrWhiteSpace(agent.ApiKey))
                    req.Headers.Add("x-api-key", agent.ApiKey);
                req.Headers.Add("anthropic-version", "2023-06-01");
            }
            else if (!string.IsNullOrWhiteSpace(agent.ApiKey))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", agent.ApiKey);
            }

            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            var json = JObject.Parse(body);
            var text = isClaude
                ? json["content"]?.FirstOrDefault()?["text"]?.ToString() ?? ""
                : json["choices"]?[0]?["message"]?["content"]?.ToString() ?? "";

            agent.LastUsedAt = DateTime.UtcNow;
            agent.TotalCalls++;
            settings.Save();

            KokoSystemLog.Write("AGENT-POOL", $"[{agent.Name}] responded ({text.Length} chars)");
            return text;
        }
    }
}
