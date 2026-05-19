using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace KokonoeAssistant.Services
{
    public record KeyStatus(
        string Name,
        string MaskedKey,
        int    RequestsLastHour,
        int    Limit,
        double UsagePct,
        bool   OnCooldown,
        DateTime? CooldownUntil,
        bool   Active);

    /// <summary>
    /// Пул Ollama Cloud API-ключів з round-robin ротацією.
    /// Гібридна детекція ліміту: проактивний sliding-window лічильник + реактивний 429 fallback.
    /// </summary>
    public class OllamaKeyPoolService
    {
        private readonly object _lock = new();
        private List<OllamaKeyEntry> _keys = new();
        private int    _activeIndex      = 0;
        private int    _maxPerHour       = 20;
        private double _rotateAt         = 0.9;
        private int    _cooldownMinutes  = 60;

        public OllamaKeyPoolService() { ReloadSettings(); }

        public void ReloadSettings()
        {
            lock (_lock)
            {
                var s = AppSettings.Load();

                // Міграція single → pool: якщо пул порожній а legacy single key є — підхопити
                if ((s.OllamaKeys == null || s.OllamaKeys.Count == 0)
                    && !string.IsNullOrWhiteSpace(s.OllamaApiKey))
                {
                    s.OllamaKeys ??= new List<OllamaKeyEntry>();
                    s.OllamaKeys.Add(new OllamaKeyEntry
                    {
                        Name = "Account 1",
                        Key  = s.OllamaApiKey.Trim(),
                        Enabled = true
                    });
                    s.Save();
                }

                _keys            = s.OllamaKeys ?? new List<OllamaKeyEntry>();
                _activeIndex     = (_keys.Count == 0) ? 0 : Math.Clamp(s.OllamaActiveKeyIndex, 0, _keys.Count - 1);
                _maxPerHour      = Math.Max(1, s.OllamaPoolMaxPerHour);
                _rotateAt        = Math.Clamp(s.OllamaPoolRotateAt, 0.1, 1.0);
                _cooldownMinutes = Math.Max(1, s.OllamaPoolCooldownMins);
            }
        }

        /// <summary>Кількість живих (Enabled, не порожніх, не на cooldown) ключів.</summary>
        public int LiveKeyCount
        {
            get { lock (_lock) { CleanupAll(); return _keys.Count(k => IsLive(k)); } }
        }

        /// <summary>Загальна кількість ключів у пулі (живі + мертві).</summary>
        public int TotalKeyCount { get { lock (_lock) { return _keys.Count; } } }

        /// <summary>Повертає поточний активний ключ або null якщо всі заблоковані / порожньо.</summary>
        public string? GetActiveKey()
        {
            lock (_lock)
            {
                if (_keys.Count == 0) return null;
                CleanupAll();

                // Один повний цикл — пробуємо знайти живого
                for (int tries = 0; tries < _keys.Count; tries++)
                {
                    var k = _keys[_activeIndex];
                    if (IsLive(k)) return k.Key;
                    _activeIndex = (_activeIndex + 1) % _keys.Count;
                }
                return null;
            }
        }

        /// <summary>
        /// Якщо активний ключ досяг порогу (rotateAt × maxPerHour) — переключитись на наступний.
        /// Викликати ПЕРЕД кожним запитом. Повертає true якщо було перемикання.
        /// </summary>
        public bool AdvanceIfAtThreshold()
        {
            lock (_lock)
            {
                if (_keys.Count == 0) return false;
                CleanupAll();

                var current = _keys[_activeIndex];
                if (!IsLive(current))
                {
                    AdvanceIndexLocked();
                    PersistIndex();
                    return true;
                }

                int threshold = (int)Math.Ceiling(_maxPerHour * _rotateAt);
                if (current.RecentRequests.Count >= threshold)
                {
                    Debug.WriteLine($"[Pool] key #{_activeIndex} '{current.Name}' at {current.RecentRequests.Count}/{_maxPerHour} (>= {threshold}), advancing");
                    AdvanceIndexLocked();
                    PersistIndex();
                    return true;
                }
                return false;
            }
        }

        public void RecordRequest(string keyUsed)
        {
            if (string.IsNullOrEmpty(keyUsed)) return;
            lock (_lock)
            {
                var entry = _keys.FirstOrDefault(k => k.Key == keyUsed);
                if (entry == null) return;
                entry.RecentRequests.Add(DateTime.UtcNow);
                CleanupOne(entry);
                PersistAll();
            }
        }

        public void MarkRateLimited(string keyUsed) => MarkUnavailable(keyUsed, 429);

        public void MarkUnavailable(string keyUsed, int statusCode)
        {
            if (string.IsNullOrEmpty(keyUsed)) return;
            lock (_lock)
            {
                var entry = _keys.FirstOrDefault(k => k.Key == keyUsed);
                if (entry == null) return;
                entry.CooldownUntil = DateTime.UtcNow.AddMinutes(_cooldownMinutes);
                Debug.WriteLine($"[Pool] key '{entry.Name}' got HTTP {statusCode} → cooldown until {entry.CooldownUntil:HH:mm:ss} UTC");

                // Перейти на наступний негайно
                int idx = _keys.IndexOf(entry);
                if (idx == _activeIndex) AdvanceIndexLocked();
                PersistAll();
            }
        }

        /// <summary>Найменший cooldown серед усіх ключів (для дружньої помилки коли всі заблоковані).</summary>
        public TimeSpan? NearestCooldown()
        {
            lock (_lock)
            {
                CleanupAll();
                var now = DateTime.UtcNow;
                var soonest = _keys
                    .Where(k => k.Enabled && !string.IsNullOrEmpty(k.Key) && k.CooldownUntil.HasValue && k.CooldownUntil > now)
                    .Select(k => k.CooldownUntil!.Value - now)
                    .DefaultIfEmpty()
                    .Min();
                return soonest == default ? (TimeSpan?)null : soonest;
            }
        }

        public IReadOnlyList<KeyStatus> Snapshot()
        {
            lock (_lock)
            {
                CleanupAll();
                var list = new List<KeyStatus>(_keys.Count);
                for (int i = 0; i < _keys.Count; i++)
                {
                    var k = _keys[i];
                    int reqs = k.RecentRequests.Count;
                    list.Add(new KeyStatus(
                        Name: string.IsNullOrEmpty(k.Name) ? $"Key {i + 1}" : k.Name,
                        MaskedKey: Mask(k.Key),
                        RequestsLastHour: reqs,
                        Limit: _maxPerHour,
                        UsagePct: _maxPerHour > 0 ? (double)reqs / _maxPerHour : 0,
                        OnCooldown: k.CooldownUntil.HasValue && k.CooldownUntil > DateTime.UtcNow,
                        CooldownUntil: k.CooldownUntil,
                        Active: i == _activeIndex && IsLive(k)));
                }
                return list;
            }
        }

        // ─────── private ───────

        private bool IsLive(OllamaKeyEntry k)
            => k.Enabled
            && !string.IsNullOrWhiteSpace(k.Key)
            && (!k.CooldownUntil.HasValue || k.CooldownUntil <= DateTime.UtcNow);

        private void AdvanceIndexLocked()
        {
            if (_keys.Count == 0) return;
            _activeIndex = (_activeIndex + 1) % _keys.Count;
        }

        private void CleanupOne(OllamaKeyEntry k)
        {
            var cutoff = DateTime.UtcNow.AddHours(-1);
            k.RecentRequests.RemoveAll(t => t < cutoff);
            if (k.CooldownUntil.HasValue && k.CooldownUntil <= DateTime.UtcNow)
                k.CooldownUntil = null;
        }

        private void CleanupAll()
        {
            foreach (var k in _keys) CleanupOne(k);
        }

        private void PersistIndex()
        {
            try
            {
                var s = AppSettings.Load();
                s.OllamaActiveKeyIndex = _activeIndex;
                s.Save();
            }
            catch (Exception ex) { Debug.WriteLine($"[Pool] PersistIndex failed: {ex.Message}"); }
        }

        private void PersistAll()
        {
            try
            {
                var s = AppSettings.Load();
                s.OllamaKeys = _keys;
                s.OllamaActiveKeyIndex = _activeIndex;
                s.Save();
            }
            catch (Exception ex) { Debug.WriteLine($"[Pool] PersistAll failed: {ex.Message}"); }
        }

        private static string Mask(string key)
        {
            if (string.IsNullOrEmpty(key)) return "(empty)";
            if (key.Length <= 8) return new string('•', key.Length);
            return key[..4] + "…" + key[^4..];
        }
    }
}
