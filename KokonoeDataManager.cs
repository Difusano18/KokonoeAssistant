using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using KokonoeAssistant.Services;
using Newtonsoft.Json;

namespace KokonoeAssistant
{
    /// <summary>
    /// Data Management System - обробка великих обсягів даних з оптимізацією
    /// Все датується, всё архівується, всі матеріали індексуються хронологічно
    /// </summary>
    public class KokonoeDataManager
    {
        public class TimestampedEntry
        {
            public string Id { get; set; } = Guid.NewGuid().ToString();
            public DateTime Timestamp { get; set; } = DateTime.Now;
            public string Type { get; set; } = null!; // "message", "fact", "observation", "memory_update"
            public string Content { get; set; } = null!;
            public string Category { get; set; } = null!;
            public Dictionary<string, string> Metadata { get; set; } = new();
        }

        public class ChronologicalIndex
        {
            public List<(DateTime timestamp, string entryId, string type)> Timeline { get; set; } = new();
            public Dictionary<string, List<string>> ByDate { get; set; } = new();
            public Dictionary<string, List<string>> ByCategory { get; set; } = new();
        }

        private readonly string _vaultPath;
        private readonly string _dataPath;
        private readonly string _archivePath;
        private readonly List<TimestampedEntry> _memoryBuffer = new();
        private readonly ChronologicalIndex _index = new();
        
        private const int BUFFER_SIZE = 500;
        private const int MAX_MEMORY_ENTRIES = 10000;
        private DateTime _lastFlush = DateTime.Now;
        private const int FLUSH_INTERVAL_MINS = 30;

        public IReadOnlyList<TimestampedEntry> MemoryBuffer => _memoryBuffer.AsReadOnly();
        public ChronologicalIndex Index => _index;

        public KokonoeDataManager(string vaultPath)
        {
            _vaultPath = vaultPath;
            _dataPath = Path.Combine(vaultPath, "kokonoe-data");
            _archivePath = Path.Combine(_dataPath, "archives");
            
            Directory.CreateDirectory(_dataPath);
            Directory.CreateDirectory(_archivePath);
            
            LoadIndex();
        }

        public void RecordEntry(string type, string content, string category, Dictionary<string, string>? metadata = null)
        {
            var entry = new TimestampedEntry
            {
                Type = type,
                Content = content,
                Category = category,
                Metadata = metadata ?? new()
            };

            _memoryBuffer.Add(entry);
            AddToIndex(entry);

            // Auto-flush якщо буфер переповнений
            if (_memoryBuffer.Count >= BUFFER_SIZE || ShouldFlush())
                FlushBuffer();
        }

        private void AddToIndex(TimestampedEntry entry)
        {
            var dateKey = entry.Timestamp.ToString("yyyy-MM-dd");
            
            _index.Timeline.Add((entry.Timestamp, entry.Id, entry.Type));
            
            if (!_index.ByDate.ContainsKey(dateKey))
                _index.ByDate[dateKey] = new();
            _index.ByDate[dateKey].Add(entry.Id);
            
            if (!_index.ByCategory.ContainsKey(entry.Category))
                _index.ByCategory[entry.Category] = new();
            _index.ByCategory[entry.Category].Add(entry.Id);
        }

        private bool ShouldFlush() => (DateTime.Now - _lastFlush).TotalMinutes >= FLUSH_INTERVAL_MINS;

        public void FlushBuffer()
        {
            if (_memoryBuffer.Count == 0) return;

            var batch = _memoryBuffer.Take(Math.Min(BUFFER_SIZE, _memoryBuffer.Count)).ToList();
            var archiveFile = Path.Combine(_archivePath, $"batch_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json");
            
            try
            {
                File.WriteAllText(archiveFile, JsonConvert.SerializeObject(batch, Formatting.Indented));
                _memoryBuffer.RemoveRange(0, batch.Count);
                _lastFlush = DateTime.Now;
            }
            catch (Exception suppressedEx107) { KokoSystemLog.Write("NOEDATAMANAGER-CATCH", "FlushBuffer failed near source line 107: " + suppressedEx107); }
        }

        public List<TimestampedEntry> GetEntriesByDateRange(DateTime from, DateTime to)
        {
            var results = new List<TimestampedEntry>();
            
            for (var date = from.Date; date <= to.Date; date = date.AddDays(1))
            {
                var dateKey = date.ToString("yyyy-MM-dd");
                if (_index.ByDate.TryGetValue(dateKey, out var ids))
                {
                    foreach (var id in ids)
                    {
                        var entry = _memoryBuffer.FirstOrDefault(e => e.Id == id);
                        if (entry != null)
                            results.Add(entry);
                    }
                }
            }

            return results.OrderBy(e => e.Timestamp).ToList();
        }

        public List<TimestampedEntry> GetEntriesByCategory(string category, int limit = 100)
        {
            if (!_index.ByCategory.TryGetValue(category, out var ids))
                return new();

            return _memoryBuffer
                .Where(e => ids.Contains(e.Id))
                .OrderByDescending(e => e.Timestamp)
                .Take(limit)
                .ToList();
        }

        public List<TimestampedEntry> GetRecentEntries(int days = 7, int limit = 500)
        {
            var from = DateTime.Now.AddDays(-days);
            return _memoryBuffer
                .Where(e => e.Timestamp >= from)
                .OrderByDescending(e => e.Timestamp)
                .Take(limit)
                .ToList();
        }

        public string GetChronologicalContext(int days = 30, int maxEntries = 200)
        {
            var from = DateTime.Now.AddDays(-days);
            var entries = _memoryBuffer
                .Where(e => e.Timestamp >= from)
                .OrderByDescending(e => e.Timestamp)
                .Take(maxEntries)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("=== CHRONOLOGICAL CONTEXT ===\n");

            var grouped = entries.GroupBy(e => e.Timestamp.ToString("yyyy-MM-dd")).OrderByDescending(g => g.Key);
            
            foreach (var dayGroup in grouped.Take(10))
            {
                sb.AppendLine($"## {dayGroup.Key}");
                foreach (var entry in dayGroup.OrderByDescending(e => e.Timestamp))
                {
                    sb.AppendLine($"- [{entry.Timestamp:HH:mm}] {entry.Type}: {entry.Category}");
                    if (entry.Content.Length > 100)
                        sb.AppendLine($"  > {entry.Content.Substring(0, 100)}...");
                    else
                        sb.AppendLine($"  > {entry.Content}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        public Dictionary<string, int> GetActivityStats(int days = 30)
        {
            var from = DateTime.Now.AddDays(-days);
            var stats = new Dictionary<string, int>();

            foreach (var entry in _memoryBuffer.Where(e => e.Timestamp >= from))
            {
                var key = entry.Type;
                if (!stats.ContainsKey(key))
                    stats[key] = 0;
                stats[key]++;
            }

            return stats;
        }

        public void ClearOldData(int daysToKeep = 90)
        {
            var cutoff = DateTime.Now.AddDays(-daysToKeep);
            var old = _memoryBuffer.Where(e => e.Timestamp < cutoff).ToList();
            
            foreach (var entry in old)
                _memoryBuffer.Remove(entry);
        }

        public string ExportChronology(int days = 365)
        {
            var from = DateTime.Now.AddDays(-days);
            var entries = _memoryBuffer
                .Where(e => e.Timestamp >= from)
                .OrderByDescending(e => e.Timestamp)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("# Kokonoe's Chronological Record\n");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}\n");
            sb.AppendLine($"Total entries: {entries.Count}\n");

            var byMonth = entries.GroupBy(e => e.Timestamp.ToString("yyyy-MM")).OrderByDescending(g => g.Key);
            
            foreach (var monthGroup in byMonth.Take(12))
            {
                sb.AppendLine($"## {monthGroup.Key}\n");
                
                var countByType = monthGroup.GroupBy(e => e.Type).Select(g => new { Type = g.Key, Count = g.Count() });
                foreach (var count in countByType)
                {
                    sb.AppendLine($"- {count.Type}: {count.Count} entries");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private void LoadIndex()
        {
            try
            {
                _index.Timeline.Clear();
                _index.ByDate.Clear();
                _index.ByCategory.Clear();

                foreach (var entry in _memoryBuffer)
                    AddToIndex(entry);
            }
            catch (Exception suppressedEx250) { KokoSystemLog.Write("NOEDATAMANAGER-CATCH", "LoadIndex failed near source line 250: " + suppressedEx250); }
        }

        public void Save()
        {
            FlushBuffer();
            SaveIndex();
        }

        private void SaveIndex()
        {
            try
            {
                var indexFile = Path.Combine(_dataPath, "chronological_index.json");
                File.WriteAllText(indexFile, JsonConvert.SerializeObject(_index, Formatting.Indented));
            }
            catch (Exception suppressedEx266) { KokoSystemLog.Write("NOEDATAMANAGER-CATCH", "SaveIndex failed near source line 266: " + suppressedEx266); }
        }
    }
}
