using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace KokonoeAssistant.Services
{
    public sealed class ObsidianPreflightContextService
    {
        private readonly ObsidianMcpService _obsidian;

        public ObsidianPreflightContextService(ObsidianMcpService obsidian)
        {
            _obsidian = obsidian;
        }

        public string? Build(string? userText, DateTime? now = null, int maxChars = 2600)
        {
            try
            {
                var at = now ?? DateTime.Now;
                var sb = new StringBuilder();
                sb.AppendLine("=== OBSIDIAN PREFLIGHT ===");
                sb.AppendLine($"checked_at: {at:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine("rule: this vault context was checked before answering; prefer it over stale chat guesses.");

                try
                {
                    var status = _obsidian.GetVaultStatus();
                    sb.AppendLine($"vault: notes={status.TotalNotes}, filled={status.FilledNotes}, empty={status.EmptyNotes.Count}, orphan={status.OrphanNotes.Count}");
                }
                catch { }

                var noteSnippets = new List<string>();
                AddNote(noteSnippets, "Creator/Profile.md", "Creator profile", 1000);
                AddNote(noteSnippets, "Kokonoe/Досьє.md", "Kokonoe dossier", 650);
                AddNote(noteSnippets, "Kokonoe/Memory/Facts.md", "Facts", 700);
                AddNote(noteSnippets, "Kokonoe/Preferences.md", "Preferences", 500);
                AddNote(noteSnippets, "Kokonoe/Tasks.md", "Tasks", 500);
                AddNote(noteSnippets, "Kokonoe/Tasks Queue.md", "Task queue", 600);
                AddNote(noteSnippets, $"Daily/{at:yyyy-MM-dd}.md", "Today", 650);
                AddNote(noteSnippets, "Kokonoe/Logs/Somatic Events.md", "Recent somatic events", 650, tail: true);
                AddNote(noteSnippets, "Kokonoe/Logs/Live Core.md", "Recent live core", 600, tail: true);

                if (noteSnippets.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("## Always-loaded notes");
                    foreach (var snippet in noteSnippets)
                        sb.AppendLine(snippet);
                }

                AddRelevantRecall(sb, userText);

                var result = SanitizeForLlm(sb.ToString().Trim());
                return result.Length > 120 ? TruncateAtWordBoundary(result, maxChars) : null;
            }
            catch
            {
                return null;
            }
        }

        private void AddRelevantRecall(StringBuilder sb, string? userText)
        {
            if (string.IsNullOrWhiteSpace(userText))
                return;

            var relevant = _obsidian.SearchSemantic(userText, 6)
                .Where(r => !string.IsNullOrWhiteSpace(r.Preview))
                .Take(6)
                .ToList();
            if (relevant.Count == 0)
                return;

            sb.AppendLine();
            sb.AppendLine("## Query-relevant vault recall");
            foreach (var r in relevant)
            {
                var preview = SanitizeForLlm(r.Preview).Replace("\r", " ").Replace("\n", " ");
                sb.AppendLine($"- {r.Path}: {TruncateAtWordBoundary(preview, 260)}");
            }
        }

        private void AddNote(List<string> output, string path, string label, int maxChars, bool tail = false)
        {
            var content = _obsidian.ReadNote(path);
            if (string.IsNullOrWhiteSpace(content))
                return;

            content = SanitizeForLlm(content.Trim());
            if (tail && content.Length > maxChars)
                content = "...\n" + content[^maxChars..];
            else
                content = TruncateAtWordBoundary(content, maxChars);

            output.Add($"### {label} ({path})\n{content}");
        }

        public static string SanitizeForLlm(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            text = Regex.Replace(text, @"<\|[^>]*\|?>", "");
            text = Regex.Replace(text, @"<(start|end)_of_(turn|text|image)>", "");
            text = Regex.Replace(text, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", "");
            return text;
        }

        public static string TruncateAtWordBoundary(string text, int limit)
        {
            if (text.Length <= limit) return text;

            var cutPoint = text.LastIndexOfAny(new[] { ' ', '\n', '\r' }, limit);
            if (cutPoint < limit / 2)
                cutPoint = limit;

            return text[..cutPoint].TrimEnd() + "\n...";
        }
    }
}
