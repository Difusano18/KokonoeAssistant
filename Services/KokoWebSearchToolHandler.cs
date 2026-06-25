using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace KokonoeAssistant.Services
{
    public sealed class KokoWebSearchToolHandler : IKokoToolHandler
    {
        private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

        public string Name => "web_search";

        public async Task<KokoToolResult> ExecuteAsync(KokoToolCall call, CancellationToken ct)
        {
            var query = Arg(call, "query");
            if (string.IsNullOrWhiteSpace(query))
                return Failure(call, "query is required.");
            var maxResults = int.TryParse(Arg(call, "maxResults"), out var parsed) ? Math.Clamp(parsed, 1, 10) : 5;

            var apiKey = AppSettings.Load().TavilyApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
                return Failure(call, "Tavily API key not configured. Add it in Settings -> Web search.");

            var payload = JObject.FromObject(new { api_key = apiKey, query, max_results = maxResults });
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.tavily.com/search")
            {
                Content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json")
            };

            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return Failure(call, $"Tavily search failed: {resp.StatusCode}");

            var json = JObject.Parse(body);
            var results = (json["results"] as JArray ?? new JArray())
                .Select(r => new
                {
                    title = r["title"]?.ToString() ?? "",
                    url = r["url"]?.ToString() ?? "",
                    snippet = Truncate(r["content"]?.ToString() ?? "", 300)
                })
                .ToList();

            return new KokoToolResult
            {
                CallId = call.Id,
                ToolName = Name,
                Success = true,
                Verified = true,
                Reason = $"Found {results.Count} result(s).",
                Output = JArray.FromObject(results).ToString(Newtonsoft.Json.Formatting.None)
            };
        }

        private static string Truncate(string text, int max)
            => text.Length <= max ? text : text[..max] + "...";

        private static KokoToolResult Failure(KokoToolCall call, string reason) => new()
        {
            CallId = call.Id,
            ToolName = "web_search",
            Success = false,
            Reason = reason
        };

        private static string Arg(KokoToolCall call, string key)
            => call.Arguments.TryGetValue(key, out var value) ? value ?? "" : "";
    }
}
