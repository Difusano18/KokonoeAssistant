using System;

namespace KokonoeAssistant.Models
{
    public enum ArtifactKind
    {
        Markdown,
        Html,
        Pdf,
        Csv,
        Json,
        Image,
        PlainText,
        Patch,
        Note
    }

    public sealed class KokoArtifact
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N")[..12];
        public string Title { get; set; } = "Artifact";
        public ArtifactKind Kind { get; set; } = ArtifactKind.PlainText;
        public string FilePath { get; set; } = "";
        public string? PreviewText { get; set; }
        public string? SourceUrl { get; set; }
        public string? MissionId { get; set; }
        public long SizeBytes { get; set; }
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

        public string SizeLabel => SizeBytes switch
        {
            < 1024 => $"{SizeBytes} B",
            < 1024 * 1024 => $"{SizeBytes / 1024} KB",
            _ => $"{SizeBytes / 1024 / 1024} MB"
        };
    }
}
