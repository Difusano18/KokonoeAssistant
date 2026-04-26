using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;

namespace KokonoeAssistant.Services
{
    public class HealthEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Date { get; set; } = DateTime.Today;
        public double? SleepHours { get; set; }
        public int? Mood { get; set; }        // 1-10
        public int? Energy { get; set; }      // 1-10
        public int? Stress { get; set; }      // 1-10
        public int? WaterMl { get; set; }
        public int? ExerciseMin { get; set; }
        public double? WeightKg { get; set; }
        public int? StepsCount { get; set; }
        public string? Notes { get; set; }
    }

    public class HealthService : IDisposable
    {
        private readonly string _dbPath;
        private readonly object _lock = new();

        public HealthService(string vaultPath)
        {
            var dir = Path.Combine(vaultPath, "kokonoe-data");
            Directory.CreateDirectory(dir);
            _dbPath = Path.Combine(dir, "kokonoe-health.db");
            InitDb();
        }

        private void InitDb()
        {
            lock (_lock)
            {
                using var conn = Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS health (
                        Id TEXT PRIMARY KEY,
                        Date TEXT NOT NULL UNIQUE,
                        SleepHours REAL,
                        Mood INTEGER,
                        Energy INTEGER,
                        Stress INTEGER,
                        WaterMl INTEGER,
                        ExerciseMin INTEGER,
                        WeightKg REAL,
                        StepsCount INTEGER,
                        Notes TEXT
                    );
                    CREATE INDEX IF NOT EXISTS idx_date ON health(Date DESC);
                ";
                cmd.ExecuteNonQuery();
            }
        }

        private SQLiteConnection Open()
        {
            var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
            c.Open();
            return c;
        }

        public void Save(HealthEntry e)
        {
            lock (_lock)
            {
                using var conn = Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO health
                    (Id, Date, SleepHours, Mood, Energy, Stress, WaterMl, ExerciseMin, WeightKg, StepsCount, Notes)
                    VALUES (@id,@d,@sl,@mo,@en,@st,@wa,@ex,@wt,@sp,@no)";
                cmd.Parameters.AddWithValue("@id", e.Id);
                cmd.Parameters.AddWithValue("@d", e.Date.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@sl", (object?)e.SleepHours ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@mo", (object?)e.Mood ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@en", (object?)e.Energy ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@st", (object?)e.Stress ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@wa", (object?)e.WaterMl ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ex", (object?)e.ExerciseMin ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@wt", (object?)e.WeightKg ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@sp", (object?)e.StepsCount ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@no", (object?)e.Notes ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        public HealthEntry? GetToday() => GetByDate(DateTime.Today);

        public HealthEntry? GetByDate(DateTime date)
        {
            lock (_lock)
            {
                using var conn = Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT * FROM health WHERE Date = @d LIMIT 1";
                cmd.Parameters.AddWithValue("@d", date.ToString("yyyy-MM-dd"));
                using var r = cmd.ExecuteReader();
                return r.Read() ? Read(r) : null;
            }
        }

        public List<HealthEntry> GetRecent(int days = 30)
        {
            lock (_lock)
            {
                var list = new List<HealthEntry>();
                using var conn = Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT * FROM health ORDER BY Date DESC LIMIT @n";
                cmd.Parameters.AddWithValue("@n", days);
                using var r = cmd.ExecuteReader();
                while (r.Read()) list.Add(Read(r));
                return list;
            }
        }

        private static HealthEntry Read(SQLiteDataReader r) => new()
        {
            Id = r["Id"]?.ToString() ?? Guid.NewGuid().ToString(),
            Date = DateTime.Parse(r["Date"]?.ToString() ?? DateTime.Today.ToString("yyyy-MM-dd")),
            SleepHours = r["SleepHours"] is DBNull ? null : Convert.ToDouble(r["SleepHours"]),
            Mood = r["Mood"] is DBNull ? null : Convert.ToInt32(r["Mood"]),
            Energy = r["Energy"] is DBNull ? null : Convert.ToInt32(r["Energy"]),
            Stress = r["Stress"] is DBNull ? null : Convert.ToInt32(r["Stress"]),
            WaterMl = r["WaterMl"] is DBNull ? null : Convert.ToInt32(r["WaterMl"]),
            ExerciseMin = r["ExerciseMin"] is DBNull ? null : Convert.ToInt32(r["ExerciseMin"]),
            WeightKg = r["WeightKg"] is DBNull ? null : Convert.ToDouble(r["WeightKg"]),
            StepsCount = r["StepsCount"] is DBNull ? null : Convert.ToInt32(r["StepsCount"]),
            Notes = r["Notes"] as string
        };

        /// <summary>Зберегти дані здоров'я отримані з розмови (лише передані поля)</summary>
        public void SaveFromConversation(int? mood = null, int? energy = null, float? sleepHours = null, string? note = null)
        {
            lock (_lock)
            {
                var today = GetToday() ?? new HealthEntry { Date = DateTime.Today };
                if (mood.HasValue)        today.Mood       = Math.Clamp(mood.Value, 1, 10);
                if (energy.HasValue)      today.Energy     = Math.Clamp(energy.Value, 1, 10);
                if (sleepHours.HasValue)  today.SleepHours = Math.Clamp(sleepHours.Value, 0, 24);
                if (!string.IsNullOrEmpty(note))
                    today.Notes = string.IsNullOrEmpty(today.Notes) ? note : today.Notes + " | " + note;
                Save(today);
            }
        }

        public string GetHealthContext()
        {
            var recent = GetRecent(7);
            if (!recent.Any()) return "No health data yet.";

            var sb = new StringBuilder();
            sb.AppendLine("=== HEALTH (7 days) ===");

            double avgMood = recent.Where(e => e.Mood.HasValue).Select(e => (double)e.Mood!.Value).DefaultIfEmpty(0).Average();
            double avgEnergy = recent.Where(e => e.Energy.HasValue).Select(e => (double)e.Energy!.Value).DefaultIfEmpty(0).Average();
            double avgSleep = recent.Where(e => e.SleepHours.HasValue).Select(e => e.SleepHours!.Value).DefaultIfEmpty(0).Average();
            int totalEx = recent.Where(e => e.ExerciseMin.HasValue).Sum(e => e.ExerciseMin!.Value);

            sb.AppendLine($"Avg mood: {avgMood:F1}/10 | Avg energy: {avgEnergy:F1}/10 | Avg sleep: {avgSleep:F1}h");
            sb.AppendLine($"Exercise this week: {totalEx} min");

            var today = GetToday();
            if (today != null)
            {
                sb.AppendLine($"Today: mood={today.Mood?.ToString() ?? "?"}, energy={today.Energy?.ToString() ?? "?"}, " +
                              $"sleep={today.SleepHours?.ToString("F1") ?? "?"}h, water={today.WaterMl?.ToString() ?? "?"}ml");
            }
            return sb.ToString();
        }

        public void Dispose() { }
    }
}
