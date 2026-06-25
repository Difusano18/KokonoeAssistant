using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using KokonoeAssistant.Models;

namespace KokonoeAssistant.Services
{
    public sealed class KokoArtifactService
    {
        private readonly List<KokoArtifact> _artifacts = new();
        private readonly string _artifactsDir;

        public event Action<KokoArtifact>? ArtifactAdded;

        public KokoArtifactService(string dataDir)
        {
            _artifactsDir = Path.Combine(dataDir, "artifacts");
            Directory.CreateDirectory(_artifactsDir);
        }

        public KokoArtifact Save(
            string title,
            ArtifactKind kind,
            string content,
            string? sourceUrl = null,
            string? missionId = null)
        {
            var ext = kind switch
            {
                ArtifactKind.Markdown => "md",
                ArtifactKind.Html => "html",
                ArtifactKind.Csv => "csv",
                ArtifactKind.Json => "json",
                ArtifactKind.Patch => "diff",
                ArtifactKind.Note => "md",
                _ => "txt"
            };
            var safeName = SanitizeFileNameSegment(title);
            var path = Path.Combine(_artifactsDir, $"{DateTime.Now:yyyyMMdd_HHmmss}_{safeName}.{ext}");

            File.WriteAllText(path, content, Encoding.UTF8);

            var artifact = new KokoArtifact
            {
                Title = title,
                Kind = kind,
                FilePath = path,
                PreviewText = content.Length > 500 ? content[..500] + "…" : content,
                SourceUrl = sourceUrl,
                MissionId = missionId,
                SizeBytes = new FileInfo(path).Length
            };
            AddAndNotify(artifact);
            KokoSystemLog.Write("ARTIFACT", $"[{artifact.Kind}] {title} -> {path}");
            return artifact;
        }

        /// <summary>Register an already-existing file (e.g. a PDF) as an artifact.</summary>
        public KokoArtifact Register(
            string title,
            ArtifactKind kind,
            string filePath,
            string? sourceUrl = null,
            string? missionId = null)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Artifact source file not found.", filePath);

            var content = kind is ArtifactKind.Pdf or ArtifactKind.Image ? "" : File.ReadAllText(filePath);
            var artifact = new KokoArtifact
            {
                Title = title,
                Kind = kind,
                FilePath = filePath,
                PreviewText = content.Length > 500 ? content[..500] + "…" : content,
                SourceUrl = sourceUrl,
                MissionId = missionId,
                SizeBytes = new FileInfo(filePath).Length
            };
            AddAndNotify(artifact);
            return artifact;
        }

        public IReadOnlyList<KokoArtifact> GetAll()
        {
            lock (_artifacts) { return _artifacts.ToList(); }
        }

        public KokoArtifact? GetById(string id)
        {
            lock (_artifacts) { return _artifacts.FirstOrDefault(a => a.Id == id); }
        }

        public void OpenInExplorer(string id)
        {
            var artifact = GetById(id);
            if (artifact == null || !File.Exists(artifact.FilePath))
                return;
            try
            {
                var psi = new ProcessStartInfo("explorer.exe");
                // Kept as one ArgumentList entry (not two) because explorer.exe
                // expects "/select,<path>" as a single token; ArgumentList still
                // handles the embedded quoting/escaping safely, unlike building
                // this as one manually-quoted string.
                psi.ArgumentList.Add("/select," + artifact.FilePath);
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                KokoSystemLog.Write("ARTIFACT-CATCH", "OpenInExplorer failed: " + ex.Message);
            }
        }

        private void AddAndNotify(KokoArtifact artifact)
        {
            lock (_artifacts) { _artifacts.Add(artifact); }
            try { ArtifactAdded?.Invoke(artifact); }
            catch (Exception ex) { KokoSystemLog.Write("ARTIFACT-CATCH", "ArtifactAdded subscriber failed: " + ex.Message); }
        }

        private static string SanitizeFileNameSegment(string title)
        {
            var trimmed = title.Length > 40 ? title[..40] : title;
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(trimmed.Length);
            foreach (var ch in trimmed)
                sb.Append(ch == ' ' || invalid.Contains(ch) ? '_' : ch);
            var result = sb.ToString().Trim('_');
            return string.IsNullOrWhiteSpace(result) ? "artifact" : result;
        }
    }
}
