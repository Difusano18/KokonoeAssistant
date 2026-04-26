using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FuzzySharp;
using FuzzySharp.SimilarityRatio;

namespace KokonoeAssistant.Services
{
    /// <summary>
    /// SearchService - Full-text search across messages, vault files, and facts
    /// </summary>
    public class SearchService
    {
        private readonly ChatRepository _chatRepository;
        private readonly string _vaultPath;

        public class SearchResult
        {
            public string Id { get; set; } = "";
            public string Type { get; set; } = ""; // "message", "note", "fact"
            public string Title { get; set; } = "";
            public string Preview { get; set; } = "";
            public DateTime Timestamp { get; set; }
            public double Relevance { get; set; } // 0-1
            public string SourcePath { get; set; } = "";
        }

        public SearchService(ChatRepository chatRepository, string? vaultPath = null)
        {
            _chatRepository = chatRepository;
            _vaultPath = vaultPath ?? AppSettings.Load().VaultPath;
            System.Diagnostics.Debug.WriteLine("[SearchService] Initialized");
        }

        public List<SearchResult> Search(string query, int limit = 50, bool useRegex = false)
        {
            var results = new List<SearchResult>();

            try
            {
                // Search messages
                var messages = _chatRepository.SearchMessages(query, limit);
                foreach (var msg in messages)
                {
                    var preview = msg.Content.Length > 100 
                        ? msg.Content.Substring(0, 97) + "..." 
                        : msg.Content;
                    
                    results.Add(new SearchResult
                    {
                        Id = msg.Id,
                        Type = "message",
                        Title = $"Message from {msg.Author ?? msg.Role}",
                        Preview = preview,
                        Timestamp = msg.Timestamp,
                        Relevance = CalculateRelevance(msg.Content, query),
                        SourcePath = "Chat History"
                    });
                }

                // Search vault notes
                var noteResults = SearchVaultNotes(query, limit - results.Count, useRegex);
                results.AddRange(noteResults);

                // Sort by relevance & timestamp
                results = results
                    .OrderByDescending(r => r.Relevance)
                    .ThenByDescending(r => r.Timestamp)
                    .Take(limit)
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"[SearchService] Found {results.Count} results for '{query}'");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SearchService] Search error: {ex.Message}");
            }

            return results;
        }

        private List<SearchResult> SearchVaultNotes(string query, int limit = 20, bool useRegex = false)
        {
            var results = new List<SearchResult>();

            try
            {
                var vaultDir = new DirectoryInfo(_vaultPath);
                if (!vaultDir.Exists) return results;

                var notePaths = vaultDir.GetFiles("*.md", SearchOption.AllDirectories);

                foreach (var notePath in notePaths.Take(100)) // Limit to avoid timeout
                {
                    try
                    {
                        var content = File.ReadAllText(notePath.FullName);
                        bool matches = false;
                        
                        if (useRegex)
                        {
                            try
                            {
                                matches = Regex.IsMatch(content, query, RegexOptions.IgnoreCase);
                            }
                            catch (ArgumentException)
                            {
                                // Invalid regex pattern, fallback to literal match
                                System.Diagnostics.Debug.WriteLine($"[SearchService] Invalid regex pattern: {query}");
                                matches = content.Contains(query, StringComparison.OrdinalIgnoreCase);
                            }
                        }
                        else
                        {
                            matches = content.Contains(query, StringComparison.OrdinalIgnoreCase);
                        }

                        if (matches)
                        {
                            var lines = content.Split('\n');
                            var matchLine = lines.FirstOrDefault(l => 
                                l.Contains(query, StringComparison.OrdinalIgnoreCase)) 
                                ?? lines.FirstOrDefault(l => l.Length > 0) 
                                ?? "";

                            results.Add(new SearchResult
                            {
                                Id = notePath.FullName,
                                Type = "note",
                                Title = notePath.Name,
                                Preview = matchLine.Length > 100 
                                    ? matchLine.Substring(0, 97) + "..." 
                                    : matchLine,
                                Timestamp = notePath.LastWriteTime,
                                Relevance = CalculateRelevance(content, query),
                                SourcePath = notePath.FullName
                            });

                            if (results.Count >= limit) break;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SearchService] Note read error: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SearchService] Vault search error: {ex.Message}");
            }

            return results;
        }

        private double CalculateRelevance(string content, string query)
        {
            try
            {
                if (string.IsNullOrEmpty(content)) return 0;

                // ── Exact keyword match (original) ────────────────────────
                var queryWords  = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var matchCount  = 0;
                foreach (var word in queryWords)
                {
                    var occ = Regex.Matches(content, $@"\b{Regex.Escape(word)}\b",
                        RegexOptions.IgnoreCase).Count;
                    matchCount += occ;
                }
                var contentWords   = content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                var baseRelevance  = (double)Math.Min(matchCount, 10) / 10;
                var lengthFactor   = Math.Min((double)contentWords / 1000, 1.0);
                double exactScore  = baseRelevance * (0.7 + lengthFactor * 0.3);

                // ── Fuzzy match (FuzzySharp) ──────────────────────────────
                // Порівнюємо запит з першими 500 символами контенту
                var snippet = content.Length > 500 ? content[..500] : content;
                double fuzzyScore = Fuzz.PartialRatio(query.ToLowerInvariant(),
                    snippet.ToLowerInvariant()) / 100.0;

                // Комбінований скор: 60% exact + 40% fuzzy
                return Math.Min(exactScore * 0.6 + fuzzyScore * 0.4, 1.0);
            }
            catch
            {
                return 0.5;
            }
        }

        /// <summary>Нечіткий пошук по списку рядків — повертає топ N за схожістю</summary>
        public List<(string Item, int Score)> FuzzyFind(string query, IEnumerable<string> items, int topN = 10)
        {
            return Process.ExtractTop(query, items, limit: topN)
                .Where(r => r.Score >= 50)
                .Select(r => (r.Value, r.Score))
                .ToList();
        }

        public List<SearchResult> AdvancedSearch(string query, DateTime? fromDate = null, DateTime? toDate = null, string? type = null)
        {
            var results = Search(query, 100, false);

            // Filter by date
            if (fromDate.HasValue)
                results = results.Where(r => r.Timestamp >= fromDate.Value).ToList();
            
            if (toDate.HasValue)
                results = results.Where(r => r.Timestamp <= toDate.Value).ToList();

            // Filter by type
            if (!string.IsNullOrEmpty(type))
                results = results.Where(r => r.Type == type).ToList();

            return results;
        }
    }
}
