using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KokonoeAssistant.Models;

namespace KokonoeAssistant.Services
{
    /// <summary>
    /// Сервіс для управління звичками з персистенцією
    /// </summary>
    public class HabitService
    {
        private readonly string _habitsFilePath;
        private readonly string _checkInsLogPath;
        private List<Habit> _habits = new();
        private readonly KokonoeDataManager _dataManager;
        private readonly object _habitsLock = new(); // Thread-safe access to habits

        public HabitService(string vaultPath, KokonoeDataManager dataManager)
        {
            _habitsFilePath = Path.Combine(vaultPath, "kokonoe-habits.json");
            _checkInsLogPath = Path.Combine(vaultPath, "kokonoe-habits-log");
            _dataManager = dataManager;
            
            // Ініціалізація
            if (!Directory.Exists(_checkInsLogPath))
                Directory.CreateDirectory(_checkInsLogPath);
        }

        /// <summary>
        /// Завантажити всі звички з файлу
        /// </summary>
        public async Task LoadHabitsAsync()
        {
            lock (_habitsLock)
            {
                try
                {
                    if (!File.Exists(_habitsFilePath))
                    {
                        _habits = new();
                        // Note: SaveHabitsAsync will also lock, don't call it here to avoid deadlock
                        return;
                    }

                    var json = File.ReadAllText(_habitsFilePath);
                    _habits = JsonSerializer.Deserialize<List<Habit>>(json) ?? new();
                }
                catch (Exception ex)
                {
                    _dataManager.RecordEntry("error", $"LoadHabitsAsync: {ex.Message}", "HabitService");
                    _habits = new();
                }
            }
        }

        /// <summary>
        /// Зберегти все звички у файл
        /// </summary>
        public Task SaveHabitsAsync()
        {
            lock (_habitsLock)
            {
                try
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    var json = JsonSerializer.Serialize(_habits, options);
                    File.WriteAllText(_habitsFilePath, json);
                }
                catch (Exception ex)
                {
                    _dataManager.RecordEntry("error", $"SaveHabitsAsync: {ex.Message}", "HabitService");
                }
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Додати нову звичку
        /// </summary>
        public async Task<Habit> AddHabitAsync(string name, string description, string category, int targetDaysPerWeek = 7, string frequency = "daily")
        {
            lock (_habitsLock)
            {
                var habit = new Habit
                {
                    Name = name,
                    Description = description,
                    Category = category,
                    TargetDaysPerWeek = targetDaysPerWeek,
                    Frequency = frequency
                };

                _habits.Add(habit);

                // Save to file
                try
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    var json = JsonSerializer.Serialize(_habits, options);
                    File.WriteAllText(_habitsFilePath, json);
                }
                catch (Exception ex)
                {
                    _dataManager.RecordEntry("error", $"SaveHabits in AddHabit: {ex.Message}", "HabitService");
                }

                _dataManager.RecordEntry("habit_created", habit.Name, "habits", new()
                {
                    { "habitId", habit.Id },
                    { "category", category }
                });

                return habit;
            }
        }

        /// <summary>
        /// Отримати звичку за ID
        /// </summary>
        public Habit? GetHabit(string habitId)
        {
            return _habits.FirstOrDefault(h => h.Id == habitId);
        }

        /// <summary>
        /// Отримати всі звички
        /// </summary>
        public List<Habit> GetAllHabits()
        {
            return new List<Habit>(_habits);
        }

        /// <summary>
        /// Отримати активні звички
        /// </summary>
        public List<Habit> GetActiveHabits()
        {
            return _habits.Where(h => h.Status == "active").ToList();
        }

        /// <summary>
        /// Видалити звичку
        /// </summary>
        public async Task<bool> RemoveHabitAsync(string habitId)
        {
            bool removed;
            lock (_habitsLock)
            {
                var habit = _habits.FirstOrDefault(h => h.Id == habitId);
                if (habit == null) return false;
                _habits.Remove(habit);
                removed = true;

                _dataManager.RecordEntry("habit_removed", habit.Name, "habits", new()
                {
                    { "habitId", habitId }
                });
            }
            await SaveHabitsAsync();
            return removed;
        }

        /// <summary>
        /// Оновити звичку
        /// </summary>
        public async Task<bool> UpdateHabitAsync(Habit habit)
        {
            var existing = _habits.FirstOrDefault(h => h.Id == habit.Id);
            if (existing == null) return false;

            var index = _habits.IndexOf(existing);
            _habits[index] = habit;
            await SaveHabitsAsync();

            _dataManager.RecordEntry("habit_updated", habit.Name, "habits", new()
            {
                { "habitId", habit.Id }
            });

            return true;
        }

        /// <summary>
        /// Перевірити звичку за дату
        /// </summary>
        public async Task<HabitCheckIn> RecordCheckInAsync(string habitId, bool completed, int? value = null, string? note = null, int? mood = null)
        {
            var habit = GetHabit(habitId);
            if (habit == null) throw new ArgumentException($"Звичка {habitId} не знайдена");

            var checkIn = new HabitCheckIn
            {
                HabitId = habitId,
                Date = DateTime.Now,
                Completed = completed,
                Value = value,
                Note = note,
                Mood = mood,
                Source = "app"
            };

            habit.CheckIns.Add(checkIn);
            habit.LastCheckedIn = DateTime.Now;
            habit.TotalCheckIns++;

            // Оновити стрік
            var today = DateTime.Now.Date;
            var yesterday = today.AddDays(-1);
            
            if (completed)
            {
                habit.TotalDaysCompleted++;

                // Перерахувати стрік тільки якщо це перше виконання сьогодні
                // (checkIn вже доданий — шукаємо серед попередніх записів, виключаючи щойно доданий)
                var alreadyCompletedToday = habit.CheckIns
                    .Where(c => c != checkIn && c.Completed && c.Date.Date == today)
                    .Any();

                if (!alreadyCompletedToday)
                {
                    // Рахуємо стрік назад від вчора
                    habit.CurrentStreak = 1;
                    var checkDate = yesterday;

                    while (checkDate >= habit.Created.Date)
                    {
                        var check = habit.CheckIns.FirstOrDefault(c => c.Date.Date == checkDate && c.Completed);
                        if (check != null)
                        {
                            habit.CurrentStreak++;
                            checkDate = checkDate.AddDays(-1);
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (habit.CurrentStreak > habit.BestStreak)
                        habit.BestStreak = habit.CurrentStreak;
                }
            }

            await UpdateHabitAsync(habit);

            _dataManager.RecordEntry("habit_check_in", habitId, "habits", new()
            {
                { "completed", completed.ToString() },
                { "mood", (mood?.ToString() ?? "none") }
            });

            return checkIn;
        }

        /// <summary>
        /// Отримати перевірки за дату
        /// </summary>
        public List<HabitCheckIn> GetCheckInsForDate(string habitId, DateTime date)
        {
            var habit = GetHabit(habitId);
            if (habit == null) return new();

            var dateOnly = date.Date;
            return habit.CheckIns
                .Where(c => c.Date.Date == dateOnly)
                .ToList();
        }

        /// <summary>
        /// Отримати календар звички за місяць
        /// </summary>
        public HabitCalendar GetMonthlyCalendar(string habitId, int year = 0, int month = 0)
        {
            if (year == 0) year = DateTime.Now.Year;
            if (month == 0) month = DateTime.Now.Month;

            var habit = GetHabit(habitId);
            var calendar = new HabitCalendar(habitId, year, month);

            if (habit == null) return calendar;

            var daysInMonth = DateTime.DaysInMonth(year, month);
            
            for (int day = 1; day <= daysInMonth; day++)
            {
                var date = new DateTime(year, month, day).Date;
                var hasCheckIn = habit.CheckIns.Any(c => c.Date.Date == date && c.Completed);
                calendar.Days[day] = hasCheckIn;
            }

            return calendar;
        }

        /// <summary>
        /// Отримати статистику звички
        /// </summary>
        public HabitStats GetHabitStats(string habitId, int days = 30)
        {
            var habit = GetHabit(habitId);
            if (habit == null) throw new ArgumentException($"Звичка {habitId} не знайдена");

            var stats = new HabitStats
            {
                HabitId = habitId,
                CalculatedAt = DateTime.Now,
                DaysSinceStarted = (int)(DateTime.Now.Date - habit.Created.Date).TotalDays,
                CurrentStreak = habit.CurrentStreak,
                BestStreak = habit.BestStreak
            };

            var startDate = DateTime.Now.AddDays(-days).Date;
            var periodCheckIns = habit.CheckIns.Where(c => c.Date.Date >= startDate).ToList();
            var periodDays = (int)((DateTime.Now.Date - startDate).TotalDays) + 1;

            stats.DaysTracked = periodCheckIns.Count;
            stats.DaysCompleted = periodCheckIns.Count(c => c.Completed);
            stats.CompletionRate = periodDays > 0 ? (stats.DaysCompleted * 100.0 / periodDays) : 0;

            // Останні 7, 30 днів
            stats.LastWeekCompletions = habit.CheckIns
                .Count(c => c.Date >= DateTime.Now.AddDays(-7) && c.Completed);
            stats.LastMonthCompletions = habit.CheckIns
                .Count(c => c.Date >= DateTime.Now.AddDays(-30) && c.Completed);
            stats.LastYearCompletions = habit.CheckIns
                .Count(c => c.Date >= DateTime.Now.AddDays(-365) && c.Completed);

            // По місяцях та дням тижня
            foreach (var checkIn in habit.CheckIns)
            {
                if (checkIn.Completed)
                {
                    var monthKey = checkIn.Date.ToString("yyyy-MM");
                    if (stats.ByMonth.ContainsKey(monthKey))
                        stats.ByMonth[monthKey]++;
                    else
                        stats.ByMonth[monthKey] = 1;

                    var weekday = checkIn.Date.DayOfWeek.ToString();
                    if (stats.ByWeekday.ContainsKey(weekday))
                        stats.ByWeekday[weekday]++;
                    else
                        stats.ByWeekday[weekday] = 1;
                }

                // Середня настройвання
                if (checkIn.Mood.HasValue)
                {
                    var moodCheckIns = habit.CheckIns.Where(c => c.Mood.HasValue).ToList();
                    if (moodCheckIns.Count > 0)
                        stats.AverageMood = moodCheckIns.Average(c => c.Mood!.Value);
                }
            }

            return stats;
        }

        /// <summary>
        /// Отримати загальну статистику всіх звичок
        /// </summary>
        public HabitsSummary GetAllHabitsStats()
        {
            var summary = new HabitsSummary
            {
                TotalHabits = _habits.Count,
                ActiveHabits = _habits.Count(h => h.Status == "active"),
                PausedHabits = _habits.Count(h => h.Status == "paused"),
                AbandonedHabits = _habits.Count(h => h.Status == "abandoned"),
                CalculatedAt = DateTime.Now
            };

            // За категоріями
            foreach (var category in _habits.Select(h => h.Category).Distinct())
            {
                summary.ByCategory[category] = _habits.Count(h => h.Category == category);
            }

            // Найкращі звички
            summary.TopHabits = _habits
                .OrderByDescending(h => h.TotalDaysCompleted)
                .Take(5)
                .Select(h => (h.Name, h.TotalDaysCompleted))
                .ToList();

            // Середній показник завершення
            if (_habits.Count > 0)
            {
                summary.AverageCompletionRate = _habits.Average(h => 
                    h.CheckIns.Count > 0 
                        ? (h.CheckIns.Count(c => c.Completed) * 100.0 / h.CheckIns.Count)
                        : 0);
                
                summary.LongestCurrentStreak = _habits.Max(h => h.CurrentStreak);
            }

            return summary;
        }

        /// <summary>
        /// Отримати звички, які потребують сьогоднішної перевірки
        /// </summary>
        public List<Habit> GetDueTodayHabits()
        {
            var today = DateTime.Now.Date;
            return _habits.Where(h => h.Status == "active" && 
                (h.LastCheckedIn == null || h.LastCheckedIn.Value.Date < today))
                .ToList();
        }
    }
}
