using System;
using System.Threading;
using System.Threading.Tasks;

namespace KokonoeAssistant.Services
{
    public sealed class KokoAgentDelegationToolHandler : IKokoToolHandler
    {
        private readonly KokoAgentPoolService _pool;

        public KokoAgentDelegationToolHandler(KokoAgentPoolService pool)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        }

        public string Name => "delegate_to_agent";

        public async Task<KokoToolResult> ExecuteAsync(KokoToolCall call, CancellationToken ct)
        {
            var agentId = Arg(call, "agentId");
            if (string.IsNullOrWhiteSpace(agentId))
                return Failure(call, "agentId is required.");

            var systemPrompt = Arg(call, "systemPrompt");
            if (string.IsNullOrWhiteSpace(systemPrompt))
                systemPrompt = "You are a helpful specialist assistant.";
            var userMessage = Arg(call, "userMessage");

            try
            {
                var result = await _pool.InvokeAsync(agentId, systemPrompt, userMessage, ct).ConfigureAwait(false);
                return new KokoToolResult
                {
                    CallId = call.Id,
                    ToolName = Name,
                    Success = true,
                    Verified = true,
                    Reason = "Agent responded.",
                    Output = result
                };
            }
            catch (Exception ex)
            {
                return Failure(call, $"Agent '{agentId}' error: {ex.Message}");
            }
        }

        private static KokoToolResult Failure(KokoToolCall call, string reason) => new()
        {
            CallId = call.Id,
            ToolName = "delegate_to_agent",
            Success = false,
            Reason = reason
        };

        private static string Arg(KokoToolCall call, string key)
            => call.Arguments.TryGetValue(key, out var value) ? value ?? "" : "";
    }
}
