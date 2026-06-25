using System;
using System.Threading;
using System.Threading.Tasks;
using KokonoeAssistant.Models;

namespace KokonoeAssistant.Services
{
    public sealed class KokoArtifactSaveToolHandler : IKokoToolHandler
    {
        private readonly KokoArtifactService _artifacts;

        public KokoArtifactSaveToolHandler(KokoArtifactService artifacts)
        {
            _artifacts = artifacts ?? throw new ArgumentNullException(nameof(artifacts));
        }

        public string Name => "artifact.save";

        public async Task<KokoToolResult> ExecuteAsync(KokoToolCall call, CancellationToken ct)
        {
            await Task.Yield();

            var content = Arg(call, "content");
            if (string.IsNullOrWhiteSpace(content))
                return Failure(call, "content is required.");

            var title = Arg(call, "title");
            if (string.IsNullOrWhiteSpace(title))
                title = "Artifact";

            var url = call.Arguments.TryGetValue("sourceUrl", out var u) && !string.IsNullOrWhiteSpace(u) ? u : null;
            var kind = ParseKind(Arg(call, "kind"));

            try
            {
                var artifact = _artifacts.Save(title, kind, content, url);
                return new KokoToolResult
                {
                    CallId = call.Id,
                    ToolName = Name,
                    Success = true,
                    Verified = true,
                    Reason = "Artifact saved.",
                    Output = $"artifact.saved|id={artifact.Id}|path={artifact.FilePath}"
                };
            }
            catch (Exception ex)
            {
                return Failure(call, ex.Message);
            }
        }

        private static ArtifactKind ParseKind(string kindStr) => kindStr.ToLowerInvariant() switch
        {
            "markdown" or "md" => ArtifactKind.Markdown,
            "html" => ArtifactKind.Html,
            "csv" => ArtifactKind.Csv,
            "json" => ArtifactKind.Json,
            "patch" or "diff" => ArtifactKind.Patch,
            "note" => ArtifactKind.Note,
            _ => ArtifactKind.PlainText
        };

        private static KokoToolResult Failure(KokoToolCall call, string reason) => new()
        {
            CallId = call.Id,
            ToolName = call.Name,
            Success = false,
            Reason = reason
        };

        private static string Arg(KokoToolCall call, string key)
            => call.Arguments.TryGetValue(key, out var value) ? value ?? "" : "";
    }
}
