using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace KokonoeAssistant.Services
{
    // ══════════════════════════════════════════════════════════════════
    // KOKO SCHEDULER ENGINE
    // Черга відкладених повідомлень з пріоритетами.
    // Замінює примітивний PendingThoughts[].
    // Підтримує: одноразові, повторювані, контекстні (умовні) записи.
    // ══════════════════════════════════════════════════════════════════

    public class KokoSchedulerEngine
    {
        // ── Типи ──────────────────────────────────────────────────────

        public enum EntryType
        {
            Once        = 0,  // відправити один раз
            Recurring   = 1,  // повторювати
            Conditional = 2,  // відправити якщо умова виконана
        }

        public enum Priority
        {
            Low    = 0,
            Normal = 1,
            High   = 2,
            Urgent = 3
        }

        public class ScheduledEntry
        {
            public string    Id          { get; set; } = Guid.NewGuid().ToString("N")[..10];
            public EntryType Type        { get; set; } = EntryType.Once;
            public Priority  Priority    { get; set; } = Priority.Normal;
            public DateTime  FireAt      { get; set; }
            public string    Prompt      { get; set; } = "";        // підказка що сказати
            public string    Category    { get; set; } = "general"; // morning / followup / reminder / custom
            public string?   Condition   { get; set; }              // null = завжди; або "tone==anxious" тощо
            public bool      Sent        { get; set; } = false;
            public DateTime? SentAt      { get; set; }
            public DateTime? CreatedAt   { get; set; } = DateTime.Now;

            // Для Recurring
            public TimeSpan? RecurInterval { get; set; }
            public int       MaxRecurs    { get; set; } = -1; // -1 = infinite
            public int       RecurCount   { get; set; } = 0;
        }

        // ── State ─────────────────────────────────────────────────────

        private List<ScheduledEntry> _entries;
        private readonly string _path;
        private readonly object _lock = new();
        private static readonly TimeSpan MaxGeneralOnceLateness = TimeSpan.FromDays(2);
        private static readonly TimeSpan MaxUserReminderLateness = TimeSpan.FromHours(12);
        private static readonly TimeSpan MaxWakeReminderLateness = TimeSpan.FromMinutes(45);

        public KokoSchedulerEngine(string dataDir)
        {
            _path    = Path.Combine(dataDir, "koko-scheduler.json");
            _entries = Load();
        }

        // ── ДОДАВАННЯ ─────────────────────────────────────────────────

        /// <summary>Запланувати повідомлення на конкретний час</summary>
        public ScheduledEntry Schedule(string prompt, DateTime fireAt, Priority priority = Priority.Normal,
            string category = "general")
        {
            lock (_lock)
            {
                var entry = new ScheduledEntry
                {
                    Type     = EntryType.Once,
                    Priority = priority,
                    FireAt   = fireAt,
                    Prompt   = prompt,
                    Category = category,
                };
                _entries.Add(entry);
                Save();
                return entry;
            }
        }

        /// <summary>Запланувати повторюване повідомлення</summary>
        public ScheduledEntry ScheduleRecurring(string prompt, DateTime firstFire, TimeSpan interval,
            int maxTimes = -1, string category = "recurring")
        {
            lock (_lock)
            {
                var entry = new ScheduledEntry
                {
                    Type        = EntryType.Recurring,
                    Priority    = Priority.Normal,
                    FireAt      = firstFire,
                    Prompt      = prompt,
                    Category    = category,
                    RecurInterval = interval,
                    MaxRecurs   = maxTimes,
                };
                _entries.Add(entry);
                Save();
                return entry;
            }
        }

        /// <summary>Умовне — відправити якщо умова виконана в момент перевірки</summary>
        public ScheduledEntry ScheduleConditional(string prompt, DateTime fireAt, string condition,
            Priority priority = Priority.Normal)
        {
            lock (_lock)
            {
                var entry = new ScheduledEntry
                {
                    Type      = EntryType.Conditional,
                    Priority  = priority,
                    FireAt    = fireAt,
                    Prompt    = prompt,
                    Category  = "conditional",
                    Condition = condition,
                };
                _entries.Add(entry);
                Save();
                return entry;
            }
        }

        // ── ОТРИМАННЯ ─────────────────────────────────────────────────

        /// <summary>Отримати всі записи готові до відправки прямо зараз</summary>
        public List<ScheduledEntry> GetDue(string? currentTone = null)
        {
            lock (_lock)
            {
                var now = DateTime.Now;
                ExpireStaleUnsentOnceEntries(now);
                return _entries
                    .Where(e => !e.Sent && e.FireAt <= now)
                    .Where(e => EvaluateCondition(e.Condition, currentTone))
                    .OrderByDescending(e => (int)e.Priority)
                    .ThenBy(e => e.FireAt)
                    .ToList();
            }
        }

        /// <summary>Наступний запис (без видалення)</summary>
        public ScheduledEntry? PeekNext() =>
            _entries.Where(e => !e.Sent).OrderBy(e => e.FireAt).FirstOrDefault();

        /// <summary>Всі активні записи</summary>
        public List<ScheduledEntry> GetAll() => _entries.Where(e => !e.Sent).ToList();

        // ── ПОЗНАЧЕННЯ ────────────────────────────────────────────────

        /// <summary>Позначити запис як відправлений + обробити recurring</summary>
        public void MarkSent(string entryId)
        {
            lock (_lock)
            {
                var entry = _entries.FirstOrDefault(e => e.Id == entryId);
                if (entry == null) return;

                if (entry.Type == EntryType.Recurring &&
                    (entry.MaxRecurs == -1 || entry.RecurCount < entry.MaxRecurs - 1))
                {
                    // Не прибираємо — переплануємо
                    entry.RecurCount++;
                    entry.SentAt  = DateTime.Now;
                    entry.FireAt  = DateTime.Now.Add(entry.RecurInterval!.Value);
                    // Не встановлюємо Sent = true — запис залишається активним
                }
                else
                {
                    entry.Sent  = true;
                    entry.SentAt = DateTime.Now;
                }

                Save();
            }
        }

        /// <summary>Скасувати запис</summary>
        public bool Cancel(string entryId)
        {
            lock (_lock)
            {
                var entry = _entries.FirstOrDefault(e => e.Id == entryId);
                if (entry == null) return false;
                _entries.Remove(entry);
                Save();
                return true;
            }
        }

        // ── ОБСЛУГОВУВАННЯ ────────────────────────────────────────────

        /// <summary>Видалити старі відправлені записи (> 7 днів)</summary>
        public int Cleanup()
        {
            lock (_lock)
            {
                var before = _entries.Count;
                _entries.RemoveAll(e =>
                    e.Sent && e.SentAt.HasValue &&
                    (DateTime.Now - e.SentAt.Value).TotalDays > 7);
                ExpireStaleUnsentOnceEntries(DateTime.Now);
                Save();
                return before - _entries.Count;
            }
        }

        /// <summary>Рядок стану для контексту</summary>
        public string GetStatusLine()
        {
            var due     = _entries.Count(e => !e.Sent && e.FireAt <= DateTime.Now);
            var pending = _entries.Count(e => !e.Sent && e.FireAt > DateTime.Now);
            var next    = PeekNext();
            return $"scheduler: {due} due, {pending} pending" +
                   (next != null ? $", next at {next.FireAt:HH:mm}" : "");
        }

        // ── PERSISTENCE ───────────────────────────────────────────────

        private List<ScheduledEntry> Load()
        {
            try
            {
                if (File.Exists(_path))
                    return JsonConvert.DeserializeObject<List<ScheduledEntry>>(File.ReadAllText(_path)) ?? new();
            }
            catch { }
            return new();
        }

        private void Save()
        {
            try
            {
                var tmp = _path + ".tmp";
                File.WriteAllText(tmp, JsonConvert.SerializeObject(_entries, Formatting.Indented));
                if (File.Exists(_path)) File.Replace(tmp, _path, null);
                else                    File.Move(tmp, _path);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Scheduler] Save failed: {ex}"); }
        }

        private void ExpireStaleUnsentOnceEntries(DateTime now)
        {
            var removed = _entries.RemoveAll(e => IsStaleUnsentOnce(e, now));
            if (removed > 0)
                Save();
        }

        private static bool IsStaleUnsentOnce(ScheduledEntry entry, DateTime now)
        {
            if (entry.Sent || entry.Type != EntryType.Once) return false;
            return now - entry.FireAt > MaxLatenessFor(entry);
        }

        private static TimeSpan MaxLatenessFor(ScheduledEntry entry)
        {
            var category = entry.Category ?? "";
            var prompt = entry.Prompt ?? "";
            if (category.Contains("wake", StringComparison.OrdinalIgnoreCase) ||
                prompt.Contains("розбуд", StringComparison.OrdinalIgnoreCase) ||
                prompt.Contains("будиль", StringComparison.OrdinalIgnoreCase) ||
                prompt.Contains("wake", StringComparison.OrdinalIgnoreCase))
                return MaxWakeReminderLateness;

            if (category.Contains("user_reminder", StringComparison.OrdinalIgnoreCase) ||
                prompt.StartsWith("Нагадай користувачу:", StringComparison.OrdinalIgnoreCase))
                return MaxUserReminderLateness;

            return MaxGeneralOnceLateness;
        }

        // ── УМОВИ ─────────────────────────────────────────────────────

        // Fail-safe: if condition references state we don't have, return false (skip)
        // rather than fire blindly. Better a missed reminder than a wrong one.
        private static bool EvaluateCondition(string? condition, string? currentTone)
        {
            if (string.IsNullOrEmpty(condition)) return true;
            if (condition == "always") return true;

            if (condition.StartsWith("tone=="))
                return currentTone != null && currentTone == condition[6..];

            if (condition.StartsWith("tone!="))
                return currentTone != null && currentTone != condition[6..];

            System.Diagnostics.Debug.WriteLine($"[Scheduler] Unknown condition '{condition}' — skipping entry");
            return false;
        }
    }
}
