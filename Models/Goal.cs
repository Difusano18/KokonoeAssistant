using System;
using System.Collections.Generic;

namespace KokonoeAssistant.Models
{
    /// <summary>
    /// Ціль користувача з відстеженням прогресу та DL
    /// </summary>
    public class Goal
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = null!;
        public string Description { get; set; } = "";
        public string Category { get; set; } = "general"; // "work", "personal", "health", "learning", "project"
        
        // Статус
        public string Status { get; set; } = "active"; // "active", "completed", "paused", "abandoned"
        public DateTime Created { get; set; } = DateTime.Now;
        public DateTime? Due { get; set; } = null;
        public DateTime? Completed { get; set; } = null;
        
        // Пріоритет & оцінка
        public int Priority { get; set; } = 3; // 1=highest, 5=lowest
        public double Progress { get; set; } = 0.0; // 0-100%
        public int Importance { get; set; } = 5; // 1-10 scale
        
        // Розбиття на мікро-кроки
        public List<GoalStep> Steps { get; set; } = new();
        
        // Логування прогресу
        public List<GoalProgress> ProgressLog { get; set; } = new();
        
        // Метрики
        public int DaysActive { get; set; } = 0;
        public int TimesRevisited { get; set; } = 0;
        public DateTime? LastUpdated { get; set; } = null;
        
        // Контекст
        public List<string> Tags { get; set; } = new();
        public List<string> RelatedNoteIds { get; set; } = new();
        public List<string> RelatedGoalIds { get; set; } = new(); // Dependencies & related goals
        public string? Context { get; set; } // Why this goal matters
    }

    public class GoalStep
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = null!;
        public string? Description { get; set; } = null;
        public int Order { get; set; } = 0;
        public bool Completed { get; set; } = false;
        public DateTime? CompletedAt { get; set; } = null;
        public DateTime? EstimatedDue { get; set; } = null;
    }

    public class GoalProgress
    {
        public string GoalId { get; set; } = null!;
        public DateTime Date { get; set; } = DateTime.Now;
        public double ProgressValue { get; set; } = 0.0;
        public string? Note { get; set; } = null;
        public int StepsCompleted { get; set; } = 0;
        public int TotalSteps { get; set; } = 0;
    }

    public class GoalAnalytics
    {
        public int TotalGoals { get; set; } = 0;
        public int TotalActive { get; set; } = 0;
        public int TotalCompleted { get; set; } = 0;
        public int TotalAbandoned { get; set; } = 0;
        public double AverageProgress { get; set; } = 0.0;
        public int OverdueCount { get; set; } = 0;
        public Dictionary<string, int> ByCategory { get; set; } = new();
        public Dictionary<string, object> GoalSpecific { get; set; } = new();
        public DateTime CalculatedAt { get; set; } = DateTime.Now;
    }
}
