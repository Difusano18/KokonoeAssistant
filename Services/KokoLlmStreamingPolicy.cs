namespace KokonoeAssistant.Services
{
    public static class KokoLlmStreamingPolicy
    {
        public static bool ShouldStreamFinalAfterTools(
            bool hasChunkSink,
            int completedRounds,
            bool isImageRequest,
            bool isClaude)
            => hasChunkSink && completedRounds > 0 && !isImageRequest && !isClaude;
    }
}
