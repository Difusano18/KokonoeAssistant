using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace KokonoeAssistant.Services
{
    public sealed class KokoAnthropicStreamTextResult
    {
        public string Text { get; init; } = "";
        public bool ToolCallsDetected { get; init; }
    }

    public static class KokoAnthropicStreamParser
    {
        public static async Task<KokoAnthropicStreamTextResult> ReadTextAsync(
            Stream stream,
            Action<string>? onChunk = null,
            CancellationToken ct = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            var text = new StringBuilder();
            var toolCallsDetected = false;
            using var reader = new StreamReader(stream, leaveOpen: true);

            while (!reader.EndOfStream)
            {
                ct.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (line == null || !line.StartsWith("data: ", StringComparison.Ordinal))
                    continue;
                var data = line[6..].Trim();
                if (data.Length == 0)
                    continue;

                JObject obj;
                try
                {
                    obj = JObject.Parse(data);
                }
                catch (Exception ex)
                {
                    KokoSystemLog.Write("LLM-STREAM", "ignored malformed Anthropic SSE frame: " + ex.Message);
                    continue;
                }

                var type = obj["type"]?.ToString();
                if (type == "content_block_start" && obj["content_block"]?["type"]?.ToString() == "tool_use")
                {
                    // A tool_use block means the model wants to call a tool — the caller falls
                    // back to the full SendAsync tool-calling loop, same as the OpenAI path.
                    toolCallsDetected = true;
                    continue;
                }

                if (type == "content_block_delta" && obj["delta"]?["type"]?.ToString() == "text_delta")
                {
                    var chunk = obj["delta"]?["text"]?.ToString();
                    if (!string.IsNullOrEmpty(chunk))
                    {
                        text.Append(chunk);
                        onChunk?.Invoke(chunk);
                    }
                    continue;
                }

                if (type == "error")
                {
                    var message = obj["error"]?["message"]?.ToString() ?? "unknown Anthropic stream error";
                    throw new InvalidOperationException("Anthropic stream error: " + message);
                }
            }

            return new KokoAnthropicStreamTextResult
            {
                Text = text.ToString(),
                ToolCallsDetected = toolCallsDetected
            };
        }
    }
}
