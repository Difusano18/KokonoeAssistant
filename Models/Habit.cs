using System;
using System.Collections.Generic;

namespace KokonoeAssistant.Models
{
    /// <summary>
    /// Звичка для повсякденного трекінгу
    /// </summary>
    public class Habit
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = null!;
        public string Description { get; set; } = "";
        public string Category { get; set; } = "general"; // "health", "productivity", "learning", "wellness", "social"
        
        // Статус
        public string Status { get; set; } = "active"; // "active", "paused", "abandoned"
        public DateTime Created { get; set; } = DateTime.Now;
        public DateTime? LastCheckedIn { get; set; } = null;
        
        // Цільова частота
        public int TargetDaysPerWeek { get; set; } = 7; // 1-7
        public string Frequency { get; set; } = "daily"; // "daily", "few_times_week", "weekly"
        
        // Метрики
        public int CurrentStreak { get; set; } = 0; // Кількість днів поспіль
        public int BestStreak { get; set; } = 0;
        public int TotalDaysCompleted { get; set; } = 0;
        public int TotalCheckIns { get; set; } = 0;
        
        // Дані
        public List<HabitCheckIn> CheckIns { get; set; } = new();
        
        // Контекст
        public List<string> Tags { get; set; } = new();
        public string? Goal { get; set; } = null; // Related goal
        public string? Motivation { get; set; } = null; // Why track this?
        public int Priority { get; set; } = 5; // 1-10
    }

    public class HabitCheckIn
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string HabitId { get; set; } = null!;
        public DateTime Date { get; set; } = DateTime.Now;
        public bool Completed { get; set; } = true;
        public int? Value { get; set; } = null; // Опціональне значення (кількість, час, тощо)
        public string? Note { get; set; } = null; // Опціональна нотатка
        public int? Mood { get; set; } = null; // 1-5 настрій під час перевірки
        
        // Синхронізація
        public string? Source { get; set; } = "app"; // "app", "telegram", "auto"
    }

    public class HabitStats
    {
        public string HabitId { get; set; } = null!;
        public DateTime CalculatedAt { get; set; } = DateTime.Now;
        
        // Загальна статистика
        public int DaysTracked { get; set; } = 0;
        public int DaysCompleted { get; set; } = 0;
        public double CompletionRate { get; set; } = 0.0; // %
        
        // Поточні дані
        public int CurrentStreak { get; set; } = 0;
        public int BestStreak { get; set; } = 0;
        public int DaysSinceStarted { get; set; } = 0;
        
        // Тренди
        public int LastWeekCompletions { get; set; } = 0;
        public int LastMonthCompletions { get; set; } = 0;
        public int LastYearCompletions { get; set; } = 0;
        
        // За періоди
        public Dictionary<string, int> ByMonth { get; set; } = new(); // "2026-04" -> count
        public Dictionary<string, int> ByWeekday { get; set; } = new(); // "Monday" -> count
        
        // Середні значення
        public double AverageMood { get; set; } = 0.0; // If tracked
        public double CompletionTrend { get; set; } = 0.0; // Позитивна чи негативна
    }

    public class HabitCalendar
    {
        public string HabitId { get; set; } = null!;
        public int Year { get; set; }
        public int Month { get; set; }
        public Dictionary<int, bool> Days { get; set; } = new(); // Day (1-31) -> completed
        
        public HabitCalendar(string habitId, int year, int month)
        {
            HabitId = habitId;
            Year = year;
            Month = month;
        }
    }

    public class HabitsSummary
    {
        public int TotalHabits { get; set; } = 0;
        public int ActiveHabits { get; set; } = 0;
        public int PausedHabits { get; set; } = 0;
        public int AbandonedHabits { get; set; } = 0;
        
        public double AverageCompletionRate { get; set; } = 0.0;
        public int LongestCurrentStreak { get; set; } = 0;
        
        public Dictionary<string, int> ByCategory { get; set; } = new();
        public List<(string habitName, int completions)> TopHabits { get; set; } = new();
        
        public DateTime CalculatedAt { get; set; } = DateTime.Now;
    }
}
