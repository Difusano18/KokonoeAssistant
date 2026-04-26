using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace KokonoeAssistant.Services
{
    public class SummarizerService
    {
        private static readonly HttpClient _httpClient = new(new SocketsHttpHandler { AutomaticDecompression = System.Net.DecompressionMethods.All })
        { 
            Timeout = TimeSpan.FromSeconds(120) 
        };
        private readonly string _lmUrl;
        private readonly string _model;

        public class SummaryResult
        {
            public string Summary { get; set; } = "";
            public List<string> KeyPoints { get; set; } = new();
            public List<string> ActionItems { get; set; } = new();
            public int MessageCount { get; set; }
            public DateTime GeneratedAt { get; set; }
        }

        public SummarizerService(string? lmUrl = null, string? model = null)
        {
            var settings = AppSettings.Load();
            _lmUrl = lmUrl ?? settings.LmUrl;
            _model = model ?? settings.Model;
            System.Diagnostics.Debug.WriteLine($"[SummarizerService] Initialized: {_lmUrl}");
        }

        public async Task<SummaryResult?> SummarizeChatAsync(List<ChatRepository.ChatMessage> messages, int maxSummaryLength = 500)
        {
            try
            {
                if (messages.Count == 0) return null;

                var conversationText = new StringBuilder();
                foreach (var msg in messages)
                {
                    conversationText.AppendLine($"{msg.Role.ToUpper()}: {msg.Content}");
                }

                var prompt = $@"Summarize this conversation concisely (max {maxSummaryLength} chars). Extract 3-5 key points and action items.

CONVERSATION:
{conversationText}

Respond in JSON format:
{{""summary"": ""..."", ""keyPoints"": [""...""], ""actionItems"": [...]}}";

                var requestBody = new
                {
                    model = _model,
                    messages = new[] { new { role = "user", content = prompt } },
                    temperature = 0.7,
                    max_tokens = 1000
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(requestBody),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.PostAsync(_lmUrl, content);
                response.EnsureSuccessStatusCode();

                var responseText = await response.Content.ReadAsStringAsync();
                dynamic? responseJson = JsonConvert.DeserializeObject(responseText);
                var llmResponse = responseJson?.choices?[0]?.message?.content?.ToString();

                if (llmResponse == null) return null;

                // Extract JSON from LLM response - improved parsing to handle nested objects
                string? jsonString = null;
                var firstBrace = llmResponse.IndexOf('{');
                if (firstBrace >= 0)
                {
                    int braceCount = 0;
                    int lastBrace = -1;
                    for (int i = firstBrace; i < llmResponse.Length; i++)
                    {
                        if (llmResponse[i] == '{') braceCount++;
                        else if (llmResponse[i] == '}') 
                        {
                            braceCount--;
                            if (braceCount == 0)
                            {
                                lastBrace = i;
                                break;
                            }
                        }
                    }
                    
                    if (lastBrace > firstBrace)
                        jsonString = llmResponse.Substring(firstBrace, lastBrace - firstBrace + 1);
                }

                if (jsonString == null) return null;

                dynamic? summaryJson = null;
                try
                {
                    summaryJson = JsonConvert.DeserializeObject(jsonString);
                }
                catch (JsonException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SummarizerService] Failed to parse JSON: {ex.Message}");
                    return null;
                }

                var result = new SummaryResult
                {
                    Summary = summaryJson?.summary?.ToString() ?? "",
                    KeyPoints = (summaryJson?.keyPoints as Newtonsoft.Json.Linq.JArray)?.ToObject<List<string>>() ?? new(),
                    ActionItems = (summaryJson?.actionItems as Newtonsoft.Json.Linq.JArray)?.ToObject<List<string>>() ?? new(),
                    MessageCount = messages.Count,
                    GeneratedAt = DateTime.Now
                };

                System.Diagnostics.Debug.WriteLine($"[SummarizerService] Generated summary");
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SummarizerService] Error: {ex.Message}");
                return null;
            }
        }

        public async Task<string?> GenerateQuickTldrAsync(List<ChatRepository.ChatMessage> messages)
        {
            try
            {
                var summary = await SummarizeChatAsync(messages, 200);
                return summary?.Summary;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SummarizerService] GenerateQuickTldr error: {ex.Message}");
                return null;
            }
        }

        public string FormatSummaryAsMarkdown(SummaryResult summary)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Chat Summary");
            sb.AppendLine($"*Generated: {summary.GeneratedAt:yyyy-MM-dd HH:mm:ss}*");
            sb.AppendLine();
            sb.AppendLine("## Summary");
            sb.AppendLine(summary.Summary);
            sb.AppendLine();

            if (summary.KeyPoints.Any())
            {
                sb.AppendLine("## Key Points");
                foreach (var point in summary.KeyPoints)
                    sb.AppendLine($"- {point}");
                sb.AppendLine();
            }

            if (summary.ActionItems.Any())
            {
                sb.AppendLine("## Action Items");
                foreach (var item in summary.ActionItems)
                    sb.AppendLine($"- [ ] {item}");
                sb.AppendLine();
            }

            sb.AppendLine($"*Based on {summary.MessageCount} messages*");
            return sb.ToString();
        }
    }
}
