using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace KokonoeAssistant.Services
{
    // ══════════════════════════════════════════════════════════════════
    // KOKO PATTERN ENGINE
    // Детектує повторювані паттерни поведінки творця.
    // Аномалії (відхилення від норми), прогноз настрою.
    // Живиться з ChatRepository + KokoInternalState.
    // ══════════════════════════════════════════════════════════════════

    public class KokoPatternEngine
    {
        // ── Моделі ────────────────────────────────────────────────────

        public class ActivitySample
        {
            public DateTime When        { get; set; }
            public int      Hour        { get; set; }
            public int      DayOfWeek   { get; set; } // 0=Sunday
            public bool     WasActive   { get; set; }
            public string   Tone        { get; set; } = "neutral";
            public int      MessageCount { get; set; }
        }

        public class DetectedPattern
        {
            public string  Id          { get; set; } = Guid.NewGuid().ToString("N")[..8];
            public string  Type        { get; set; } = ""; // time_pattern / mood_pattern / behavior / anomaly
            public string  Description { get; set; } = "";
            public float   Confidence  { get; set; } = 0.5f; // 0..1
            public int     Occurrences { get; set; } = 1;
            public DateTime FirstSeen  { get; set; } = DateTime.Now;
            public DateTime LastSeen   { get; set; } = DateTime.Now;
            public bool     IsActive   { get; set; } = true;
        }

        public class PatternData
        {
            public List<ActivitySample>   Samples  { get; set; } = new();
            public List<DetectedPattern>  Patterns { get; set; } = new();
            public Dictionary<int, float> HourlyActivity  { get; set; } = new(); // year_hour -> avg activity
            public Dictionary<int, float> DailyMoodByDow  { get; set; } = new(); // DayOfWeek -> avg mood score
            public DateTime LastAnalyzed { get; set; } = DateTime.MinValue;
        }

        // ── State ─────────────────────────────────────────────────────

        private PatternData  _data;
        private readonly string _path;
        private readonly object _lock = new();

        public IReadOnlyList<DetectedPattern> Patterns => _data.Patterns.AsReadOnly();

        /// <summary>Годинна активність для візуалізації (0-23)</summary>
        public int[] GetHourlyActivity()
        {
            lock (_lock)
            {
                var byHour = new int[24];
                if (_data?.Samples != null)
                {
                    foreach (var s in _data.Samples)
                        byHour[s.Hour] += s.MessageCount;
                }
                return byHour;
            }
        }

        // ── Ініціалізація ─────────────────────────────────────────────

        public KokoPatternEngine(string dataDir)
        {
            _path = Path.Combine(dataDir, "koko-patterns.json");
            _data = Load();
        }

        // ── ЗАПИС АКТИВНОСТІ ──────────────────────────────────────────

        /// <summary>Записати зразок активності (виклик після кожного повідомлення)</summary>
        public void RecordActivity(bool wasActive, string tone = "neutral", int messageCount = 1)
        {
            lock (_lock)
            {
                var now = DateTime.Now;
                _data.Samples.Add(new ActivitySample
                {
                    When         = now,
                    Hour         = now.Hour,
                    DayOfWeek    = (int)now.DayOfWeek,
                    WasActive    = wasActive,
                    Tone         = tone,
                    MessageCount = messageCount
                });

                // Не більше 2000 зразків
                if (_data.Samples.Count > 2000)
                    _data.Samples.RemoveRange(0, _data.Samples.Count - 2000);

                Save();
            }
        }

        // ── АНАЛІЗ ────────────────────────────────────────────────────

        /// <summary>Повний аналіз паттернів. Виклик раз на день.</summary>
        public List<DetectedPattern> Analyze()
        {
            lock (_lock)
            {
                var newPatterns = new List<DetectedPattern>();

                if (_data.Samples.Count < 20) return newPatterns; // мало даних

                AnalyzeTimePatterns(newPatterns);
                AnalyzeMoodPatterns(newPatterns);
                AnalyzeBehaviorPatterns(newPatterns);

                // Зберегти нові паттерни (якщо ще немає схожих)
                foreach (var p in newPatterns)
                {
                    var existing = _data.Patterns.FirstOrDefault(x =>
                        x.Type == p.Type &&
                        Similarity(x.Description, p.Description) > 0.6f);

                    if (existing != null)
                    {
                        existing.Occurrences++;
                        existing.LastSeen  = DateTime.Now;
                        existing.Confidence = Math.Min(1f, existing.Confidence + 0.05f);
                    }
                    else
                    {
                        _data.Patterns.Add(p);
                    }
                }

                // Прибрати старі неактивні паттерни
                _data.Patterns.RemoveAll(p =>
                    p.Occurrences == 1 &&
                    (DateTime.Now - p.FirstSeen).TotalDays > 14);

                _data.LastAnalyzed = DateTime.Now;
                Save();
                return newPatterns;
            }
        }

        /// <summary>Перевірити аномалії — відхилення від очікуваної поведінки</summary>
        public string? DetectAnomaly()
        {
            lock (_lock)
            {
                var now = DateTime.Now;
                var hour = now.Hour;
                var dow  = (int)now.DayOfWeek;

                // Зазвичай активний о цій годині?
                var usuallyActiveThisHour = _data.Samples
                    .Where(s => s.Hour == hour && s.DayOfWeek == dow)
                    .Count(s => s.WasActive);

                var totalThisSlot = _data.Samples.Count(s => s.Hour == hour && s.DayOfWeek == dow);

                if (totalThisSlot < 5) return null; // мало даних для цього слоту

                var expectedRate = (float)usuallyActiveThisHour / totalThisSlot;

                // Якщо зазвичай активний (>70%) але мовчить більше 3 годин
                if (expectedRate > 0.7f)
                {
                    var lastActivity = _data.Samples
                        .Where(s => s.WasActive)
                        .OrderByDescending(s => s.When)
                        .FirstOrDefault();

                    if (lastActivity != null && (now - lastActivity.When).TotalHours > 3)
                        return $"Він зазвичай активний о {hour}:00 — але мовчить вже {(now - lastActivity.When).TotalHours:F0} год.";
                }

                // Перевірити настрій: зазвичай в хорошому стані в цей час?
                var usualMoods = _data.Samples
                    .Where(s => s.DayOfWeek == dow)
                    .Select(s => s.Tone)
                    .ToList();

                var dominantMood = usualMoods
                    .GroupBy(t => t)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault()?.Key ?? "neutral";

                var recentMood = _data.Samples.LastOrDefault()?.Tone ?? "neutral";

                if (dominantMood is "happy" or "calm" or "neutral" &&
                    recentMood  is "sad" or "anxious" or "stressed")
                    return $"Він зазвичай нормально тримається у {DayName(dow)} — але сьогодні {recentMood}.";

                return null;
            }
        }

        // ── КОНТЕКСТ ─────────────────────────────────────────────────

        /// <summary>Рядок паттернів для BuildContext</summary>
        public string BuildPatternContext(int max = 5)
        {
            lock (_lock)
            {
                var active = _data.Patterns
                    .Where(p => p.IsActive && p.Confidence > 0.5f)
                    .OrderByDescending(p => p.Confidence * p.Occurrences)
                    .Take(max)
                    .ToList();

                if (active.Count == 0) return "";

                var sb = new StringBuilder("=== ПАТТЕРНИ ===\n");
                foreach (var p in active)
                    sb.AppendLine($"• [{p.Type}] {p.Description} (×{p.Occurrences}, впевненість {p.Confidence:P0})");
                return sb.ToString();
            }
        }

        /// <summary>Прогноз настрою на сьогодні на основі паттернів</summary>
        public string? PredictTodayMood()
        {
            lock (_lock)
            {
                var dow = (int)DateTime.Now.DayOfWeek;
                if (!_data.DailyMoodByDow.TryGetValue(dow, out var avgMood)) return null;

                return avgMood < 0.35f
                    ? $"Статистично {DayName(dow)} для нього важкий день."
                    : avgMood > 0.65f
                    ? $"Зазвичай {DayName(dow)} у нього непоганий."
                    : null;
            }
        }

        // ── PERSISTENCE ───────────────────────────────────────────────

        private PatternData Load()
        {
            try
            {
                if (File.Exists(_path))
                    return JsonConvert.DeserializeObject<PatternData>(File.ReadAllText(_path)) ?? new();
            }
            catch { }
            return new();
        }

        private void Save()
        {
            try { File.WriteAllText(_path, JsonConvert.SerializeObject(_data, Formatting.Indented)); }
            catch { }
        }

        // ── АНАЛІЗ (внутрішній) ───────────────────────────────────────

        private void AnalyzeTimePatterns(List<DetectedPattern> result)
        {
            // Знайти години з найвищою активністю
            var byHour = _data.Samples
                .GroupBy(s => s.Hour)
                .Select(g => new { Hour = g.Key, Rate = (float)g.Count(s => s.WasActive) / g.Count() })
                .Where(x => x.Rate > 0.7f)
                .OrderByDescending(x => x.Rate)
                .Take(3)
                .ToList();

            foreach (var h in byHour)
            {
                result.Add(new DetectedPattern
                {
                    Type        = "time_pattern",
                    Description = $"Найчастіше активний о {h.Hour}:00 ({h.Rate:P0})",
                    Confidence  = h.Rate
                });
            }

            // Знайти «мертві» години
            var deadHours = _data.Samples
                .GroupBy(s => s.Hour)
                .Where(g => g.Count() > 5 && g.All(s => !s.WasActive))
                .Select(g => g.Key)
                .ToList();

            if (deadHours.Count > 0)
            {
                result.Add(new DetectedPattern
                {
                    Type        = "time_pattern",
                    Description = $"Зазвичай не активний о {string.Join(", ", deadHours.OrderBy(h => h).Select(h => h + ":00"))}",
                    Confidence  = 0.7f
                });
            }
        }

        private void AnalyzeMoodPatterns(List<DetectedPattern> result)
        {
            // Настрій по днях тижня
            var byDow = _data.Samples
                .GroupBy(s => s.DayOfWeek)
                .ToDictionary(
                    g => g.Key,
                    g => (float)g.Count(s => s.Tone is "happy" or "excited" or "neutral") / g.Count());

            foreach (var (dow, rate) in byDow)
            {
                _data.DailyMoodByDow[dow] = rate;

                if (rate < 0.3f)
                {
                    result.Add(new DetectedPattern
                    {
                        Type        = "mood_pattern",
                        Description = $"У {DayName(dow)} настрій зазвичай поганий",
                        Confidence  = 1f - rate
                    });
                }
            }
        }

        private void AnalyzeBehaviorPatterns(List<DetectedPattern> result)
        {
            // Чи є паттерн зникнення після важких розмов?
            var heavyTones = new[] { "sad", "anxious", "stressed" };
            var afterHeavy = new List<bool>();

            for (int i = 0; i < _data.Samples.Count - 2; i++)
            {
                if (heavyTones.Contains(_data.Samples[i].Tone))
                {
                    var nextDay = _data.Samples
                        .Skip(i + 1)
                        .TakeWhile(s => (s.When - _data.Samples[i].When).TotalHours < 24)
                        .ToList();
                    afterHeavy.Add(nextDay.Count < 3); // зник = менше 3 активних зразків
                }
            }

            if (afterHeavy.Count > 3 && afterHeavy.Count(x => x) > afterHeavy.Count * 0.6f)
            {
                result.Add(new DetectedPattern
                {
                    Type        = "behavior",
                    Description = "Після важких розмов часто зникає на наступний день",
                    Confidence  = (float)afterHeavy.Count(x => x) / afterHeavy.Count
                });
            }

            // Паттерн частоти повідомлень
            var recentDays = _data.Samples
                .Where(s => s.When > DateTime.Now.AddDays(-7))
                .GroupBy(s => s.When.Date)
                .Select(g => g.Sum(s => s.MessageCount))
                .ToList();

            if (recentDays.Count > 3)
            {
                var avg = recentDays.Average();
                if (avg > 20)
                {
                    result.Add(new DetectedPattern
                    {
                        Type        = "behavior",
                        Description = $"Активно спілкується — в середньому {avg:F0} повідомлень на день",
                        Confidence  = 0.8f
                    });
                }
                else if (avg < 5)
                {
                    result.Add(new DetectedPattern
                    {
                        Type        = "behavior",
                        Description = $"Останнім часом мало пише — менше {avg:F0} повідомлень на день",
                        Confidence  = 0.7f
                    });
                }
            }
        }

        // ── ІНСАЙТИ І КОРЕЛЯЦІЇ ───────────────────────────────────────

        public class PatternInsight
        {
            public string Description { get; set; } = ""; // "Після поганого сну настрій гірший"
            public float  Confidence  { get; set; } = 0.5f;
            public string Actionable  { get; set; } = ""; // "Краще не чіпати важкі теми вранці"
        }

        /// <summary>Знайти кореляції між паттернами (sleep→mood, topic→disappear)</summary>
        public List<PatternInsight> AnalyzeCorrelations()
        {
            lock (_lock)
            {
                var insights = new List<PatternInsight>();
                var samples  = _data.Samples;
                if (samples.Count < 30) return insights;

                // Кореляція: активна година → тон
                var activeHours = samples
                    .Where(s => s.WasActive && s.Hour >= 8 && s.Hour <= 23)
                    .GroupBy(s => s.Hour)
                    .Where(g => g.Count() >= 5)
                    .Select(g => new
                    {
                        Hour         = g.Key,
                        PositiveRate = (float)g.Count(s => s.Tone is "happy" or "excited") / g.Count(),
                        NegativeRate = (float)g.Count(s => s.Tone is "sad" or "anxious" or "stressed") / g.Count(),
                        Count        = g.Count()
                    });

                foreach (var h in activeHours)
                {
                    if (h.NegativeRate > 0.5f)
                        insights.Add(new PatternInsight
                        {
                            Description = $"О {h.Hour}:00 частіше негативний тон ({h.NegativeRate:P0})",
                            Confidence  = Math.Min(1f, h.Count / 20f),
                            Actionable  = $"Краще уникати важких тем о {h.Hour}:00"
                        });
                    else if (h.PositiveRate > 0.6f)
                        insights.Add(new PatternInsight
                        {
                            Description = $"О {h.Hour}:00 частіше гарний настрій ({h.PositiveRate:P0})",
                            Confidence  = Math.Min(1f, h.Count / 20f),
                            Actionable  = $"Найкращий час для важливих розмов: {h.Hour}:00"
                        });
                }

                // Кореляція: день тижня → активність
                var dowActivity = samples
                    .GroupBy(s => s.DayOfWeek)
                    .Where(g => g.Count() >= 4)
                    .Select(g => new
                    {
                        Dow        = g.Key,
                        ActiveRate = (float)g.Count(s => s.WasActive) / g.Count(),
                        AvgMsgs    = g.Average(s => s.MessageCount)
                    });

                foreach (var d in dowActivity)
                {
                    if (d.ActiveRate < 0.3f)
                        insights.Add(new PatternInsight
                        {
                            Description = $"По {DayName(d.Dow)} зазвичай мовчить ({d.ActiveRate:P0} активності)",
                            Confidence  = 0.6f,
                            Actionable  = $"По {DayName(d.Dow)} краще не очікувати відповіді"
                        });
                }

                // Топ-3 за confidence
                return insights.OrderByDescending(i => i.Confidence).Take(5).ToList();
            }
        }

        /// <summary>Порівняти поточний тиждень з попереднім</summary>
        public string GetWeeklyTrend()
        {
            lock (_lock)
            {
                var now       = DateTime.Now;
                var thisWeek  = _data.Samples.Where(s => (now - s.When).TotalDays <= 7).ToList();
                var lastWeek  = _data.Samples.Where(s => (now - s.When).TotalDays is > 7 and <= 14).ToList();

                if (thisWeek.Count < 3 || lastWeek.Count < 3) return "";

                var thisActive  = (float)thisWeek.Count(s => s.WasActive) / thisWeek.Count;
                var lastActive  = (float)lastWeek.Count(s => s.WasActive) / lastWeek.Count;
                var thisPositive = (float)thisWeek.Count(s => s.Tone is "happy" or "excited") / thisWeek.Count;
                var lastPositive = (float)lastWeek.Count(s => s.Tone is "happy" or "excited") / lastWeek.Count;

                var actDiff  = thisActive - lastActive;
                var moodDiff = thisPositive - lastPositive;

                var parts = new List<string>();
                if (Math.Abs(actDiff) > 0.1f)
                    parts.Add(actDiff > 0 ? "активніший ніж минулого тижня" : "менш активний ніж минулого тижня");
                if (Math.Abs(moodDiff) > 0.1f)
                    parts.Add(moodDiff > 0 ? "настрій кращий" : "настрій гірший");

                return parts.Any() ? $"Тренд тижня: {string.Join(", ", parts)}" : "Тиждень як зазвичай";
            }
        }

        /// <summary>Найкращий час для написати (найвища активність + позитивний тон)</summary>
        public string GetBestTimeToReach()
        {
            lock (_lock)
            {
                var best = _data.Samples
                    .Where(s => s.WasActive && s.Hour >= 8)
                    .GroupBy(s => s.Hour)
                    .Where(g => g.Count() >= 3)
                    .Select(g => new
                    {
                        Hour  = g.Key,
                        Score = (float)g.Count(s => s.WasActive) / g.Count()
                              + (float)g.Count(s => s.Tone is "happy" or "excited" or "neutral") / g.Count() * 0.5f
                    })
                    .OrderByDescending(x => x.Score)
                    .FirstOrDefault();

                return best != null ? $"Найкращий час: ~{best.Hour}:00" : "";
            }
        }

        /// <summary>Топ N actionable інсайтів для LLM</summary>
        public string GetPatternInsights(int n = 3)
        {
            var insights = AnalyzeCorrelations();
            if (!insights.Any()) return "";
            var lines = insights.Take(n).Select(i => $"• {i.Description} → {i.Actionable}");
            return "=== ІНСАЙТИ ===\n" + string.Join("\n", lines);
        }

        // ── УТИЛІТИ ───────────────────────────────────────────────────

        private static string DayName(int dow) => dow switch
        {
            0 => "неділю",   1 => "понеділок", 2 => "вівторок",
            3 => "середу",   4 => "четвер",    5 => "п'ятницю",
            6 => "суботу",   _ => "цей день"
        };

        private static float Similarity(string a, string b)
        {
            var wa = new HashSet<string>(a.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries));
            var wb = new HashSet<string>(b.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries));
            var union = wa.Union(wb).Count();
            return union == 0 ? 0 : (float)wa.Intersect(wb).Count() / union;
        }
    }
}
