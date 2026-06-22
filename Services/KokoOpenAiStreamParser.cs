using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace KokonoeAssistant.Services
{
    public sealed class KokoOpenAiStreamTextResult
    {
        public string Text { get; init; } = "";
        public string Reasoning { get; init; } = "";
        public bool ToolCallsDetected { get; init; }
    }

    public static class KokoOpenAiStreamParser
    {
        public static async Task<KokoOpenAiStreamTextResult> ReadTextAsync(
            Stream stream,
            Action<string>? onChunk = null,
            CancellationToken ct = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            var text = new StringBuilder();
            var reasoning = new StringBuilder();
            var toolCallsDetected = false;
            using var reader = new StreamReader(stream, leaveOpen: true);

            while (!reader.EndOfStream)
            {
                ct.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (line == null || !line.StartsWith("data: ", StringComparison.Ordinal))
                    continue;
                var data = line[6..].Trim();
                if (data == "[DONE]")
                    break;
                if (data.Length == 0)
                    continue;

                try
                {
                    var delta = JObject.Parse(data)["choices"]?[0]?["delta"];
                    if (delta?["tool_calls"] != null)
                    {
                        toolCallsDetected = true;
                        continue;
                    }

                    var chunk = delta?["content"]?.ToString();
                    if (!string.IsNullOrEmpty(chunk) && !chunk.Equals("null", StringComparison.OrdinalIgnoreCase))
                    {
                        text.Append(chunk);
                        onChunk?.Invoke(chunk);
                    }
                    var thought = delta?["reasoning_content"]?.ToString();
                    if (!string.IsNullOrEmpty(thought))
                        reasoning.Append(thought);
                }
                catch (Exception ex)
                {
                    KokoSystemLog.Write("LLM-STREAM", "ignored malformed SSE frame: " + ex.Message);
                }
            }

            return new KokoOpenAiStreamTextResult
            {
                Text = text.ToString(),
                Reasoning = reasoning.ToString(),
                ToolCallsDetected = toolCallsDetected
            };
        }
    }
}
