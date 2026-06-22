using System;
using System.Threading;
using System.Threading.Tasks;

namespace KokonoeAssistant.Services
{
    public interface IKokoEmotionEngine
    {
        string GetPromptHint();
    }

    public interface IKokoMemoryEngine
    {
        Task<string> BuildMemoryContextAsync(int maxFacts = 12, int maxEpisodes = 5, string? query = null);
    }

    public interface IKokoInternalBlackboardService
    {
        void Publish(string agent, string kind, string summary, double priority = 0.5, object? payload = null, string status = "published");
        string BuildPromptBlock(int count = 8);
    }

    public interface IKokoProfileUpdateService
    {
        Task<KokoProfileUpdateResult> UpdateProfileFromRecentContextAsync(
            string instruction,
            LlmService? llm,
            CancellationToken ct = default,
            int recentMessageLimit = 120);
    }

    public interface ILlmService
    {
        Task<string> SendAsync(
            string userText,
            byte[]? imageBytes = null,
            string imageMime = "image/jpeg",
            string? extraContext = null,
            CancellationToken ct = default,
            string? agentId = null,
            int? maxTokensOverride = null,
            Action<string>? onChunk = null);

        Task<string?> SendStreamingAsync(
            string userText,
            string? extraContext,
            Action<string> onChunk,
            CancellationToken ct = default,
            string? agentId = null,
            int? maxTokensOverride = null);
    }
}
