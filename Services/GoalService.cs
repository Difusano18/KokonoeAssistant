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
    /// Сервіс для управління цілями з персистенцією
    /// </summary>
    public class GoalService
    {
        private readonly string _goalsFilePath;
        private readonly string _progressLogPath;
        private List<Goal> _goals = new();
        private readonly KokonoeDataManager _dataManager;
        private readonly object _goalsLock = new(); // Thread-safe access to goals

        public GoalService(string vaultPath, KokonoeDataManager dataManager)
        {
            _goalsFilePath = Path.Combine(vaultPath, "kokonoe-goals.json");
            _progressLogPath = Path.Combine(vaultPath, "kokonoe-goals-progress");
            _dataManager = dataManager;

            // Ініціалізація
            if (!Directory.Exists(_progressLogPath))
                Directory.CreateDirectory(_progressLogPath);
        }

        /// <summary>
        /// Завантажити всі цілі з файлу
        /// </summary>
        public async Task LoadGoalsAsync()
        {
            lock (_goalsLock)
            {
                try
                {
                    if (!File.Exists(_goalsFilePath))
                    {
                        _goals = new();
                        // Note: SaveGoalsAsync will also lock, don't call it here to avoid deadlock
                        // Will need to save after first load if needed
                        return;
                    }

                    var json = File.ReadAllText(_goalsFilePath);
                    _goals = JsonSerializer.Deserialize<List<Goal>>(json) ?? new();
                }
                catch (Exception ex)
                {
                    _dataManager.RecordEntry("error", $"LoadGoalsAsync: {ex.Message}", "GoalService");
                    _goals = new();
                }
            }
        }

        /// <summary>
        /// Зберегти всі цілі у файл
        /// </summary>
        public Task SaveGoalsAsync()
        {
            lock (_goalsLock)
            {
                try
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    var json = JsonSerializer.Serialize(_goals, options);
                    File.WriteAllText(_goalsFilePath, json);
                }
                catch (Exception ex)
                {
                    _dataManager.RecordEntry("error", $"SaveGoalsAsync: {ex.Message}", "GoalService");
                }
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Додати нову ціль
        /// </summary>
        public async Task<Goal> AddGoalAsync(string title, string description, string category, int priority = 5, int? daysToComplete = null)
        {
            lock (_goalsLock)
            {
                var goal = new Goal
                {
                    Title = title,
                    Description = description,
                    Category = category,
                    Priority = priority,
                    Status = "active",
                    Created = DateTime.Now,
                    Due = daysToComplete.HasValue ? DateTime.Now.AddDays(daysToComplete.Value) : null
                };

                _goals.Add(goal);

                // Save to file
                try
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    var json = JsonSerializer.Serialize(_goals, options);
                    File.WriteAllText(_goalsFilePath, json);
                }
                catch (Exception ex)
                {
                    _dataManager.RecordEntry("error", $"SaveGoals in AddGoal: {ex.Message}", "GoalService");
                }

                // Record to data manager for chronological tracking
                _dataManager.RecordEntry("goal_created", $"Goal: {title}", "goals", new()
                {
                    { "goalId", goal.Id },
                    { "category", category },
                    { "priority", priority.ToString() }
                });

                return goal;
            }
        }

        /// <summary>
        /// Отримати ціль за ID
        /// </summary>
        public Goal? GetGoal(string goalId)
        {
            return _goals.FirstOrDefault(g => g.Id == goalId);
        }

        /// <summary>
        /// Отримати всі цілі
        /// </summary>
        public List<Goal> GetAllGoals()
        {
            return new List<Goal>(_goals);
        }

        /// <summary>
        /// Отримати активні цілі
        /// </summary>
        public List<Goal> GetActiveGoals()
        {
            return _goals.Where(g => g.Status == "active").OrderByDescending(g => g.Priority).ToList();
        }

        /// <summary>
        /// Отримати завершені цілі
        /// </summary>
        public List<Goal> GetCompletedGoals()
        {
            return _goals.Where(g => g.Status == "completed").ToList();
        }

        /// <summary>
        /// Отримати просрочені цілі
        /// </summary>
        public List<Goal> GetOverdueGoals()
        {
            return _goals
                .Where(g => g.Status == "active" && g.Due.HasValue && g.Due.Value < DateTime.Now)
                .OrderBy(g => g.Due)
                .ToList();
        }

        /// <summary>
        /// Видалити ціль
        /// </summary>
        public async Task<bool> RemoveGoalAsync(string goalId)
        {
            bool removed;
            lock (_goalsLock)
            {
                var goal = _goals.FirstOrDefault(g => g.Id == goalId);
                if (goal == null) return false;
                _goals.Remove(goal);
                removed = true;

                _dataManager.RecordEntry("goal_removed", goal.Title, "goals", new()
                {
                    { "goalId", goalId }
                });
            }
            await SaveGoalsAsync();
            return removed;
        }

        /// <summary>
        /// Оновити ціль
        /// </summary>
        public async Task<bool> UpdateGoalAsync(Goal goal)
        {
            var existing = _goals.FirstOrDefault(g => g.Id == goal.Id);
            if (existing == null) return false;

            var index = _goals.IndexOf(existing);
            _goals[index] = goal;
            await SaveGoalsAsync();

            _dataManager.RecordEntry("goal_updated", goal.Title, "goals", new()
            {
                { "goalId", goal.Id },
                { "progress", goal.Progress.ToString() }
            });

            return true;
        }

        /// <summary>
        /// Встановити прогрес цілі
        /// </summary>
        public async Task<bool> SetProgressAsync(string goalId, double progress, string? note = null)
        {
            var goal = GetGoal(goalId);
            if (goal == null) return false;

            progress = Math.Max(0, Math.Min(100, progress)); // Затиснути 0-100
            var oldProgress = goal.Progress;
            
            goal.Progress = progress;
            goal.LastUpdated = DateTime.Now;
            
            // Логувати прогрес
            var progressLog = new GoalProgress
            {
                GoalId = goalId,
                Date = DateTime.Now,
                ProgressValue = progress,
                Note = note
            };
            goal.ProgressLog.Add(progressLog);

            // Якщо завершено
            if (progress >= 100 && goal.Status != "completed")
            {
                goal.Status = "completed";
                goal.Completed = DateTime.Now;
            }

            await UpdateGoalAsync(goal);

            _dataManager.RecordEntry("goal_progress_updated", goalId, "goals", new()
            {
                { "oldProgress", oldProgress.ToString() },
                { "newProgress", progress.ToString() }
            });

            return true;
        }

        /// <summary>
        /// Додати крок до цілі
        /// </summary>
        public async Task<GoalStep> AddStepAsync(string goalId, string title, int? estimatedDaysFromNow = null)
        {
            var goal = GetGoal(goalId);
            if (goal == null) throw new ArgumentException($"Ціль {goalId} не знайдена");

            var step = new GoalStep
            {
                Title = title,
                Order = goal.Steps.Count + 1,
                EstimatedDue = estimatedDaysFromNow.HasValue ? DateTime.Now.AddDays(estimatedDaysFromNow.Value) : null
            };

            goal.Steps.Add(step);
            await UpdateGoalAsync(goal);

            _dataManager.RecordEntry("goal_step_added", step.Title, "goals", new()
            {
                { "goalId", goalId },
                { "stepId", step.Id }
            });

            return step;
        }

        /// <summary>
        /// Виконати крок
        /// </summary>
        public async Task<bool> CompleteStepAsync(string goalId, string stepId)
        {
            var goal = GetGoal(goalId);
            if (goal == null) return false;

            var step = goal.Steps.FirstOrDefault(s => s.Id == stepId);
            if (step == null) return false;

            step.Completed = true;
            step.CompletedAt = DateTime.Now;

            // Автоматично оновити прогрес цілі
            var completedSteps = goal.Steps.Count(s => s.Completed);
            var newProgress = (completedSteps * 100.0) / goal.Steps.Count;
            await SetProgressAsync(goalId, newProgress, $"Крок завершено: {step.Title}");

            return true;
        }

        /// <summary>
        /// Видалити крок
        /// </summary>
        public async Task<bool> RemoveStepAsync(string goalId, string stepId)
        {
            var goal = GetGoal(goalId);
            if (goal == null) return false;

            var step = goal.Steps.FirstOrDefault(s => s.Id == stepId);
            if (step == null) return false;

            goal.Steps.Remove(step);
            
            // Переупорядкувати порядок
            for (int i = 0; i < goal.Steps.Count; i++)
                goal.Steps[i].Order = i + 1;

            await UpdateGoalAsync(goal);
            return true;
        }

        /// <summary>
        /// Отримати статистику цілі
        /// </summary>
        public GoalAnalytics GetGoalStats(string? goalId = null)
        {
            var analytics = new GoalAnalytics
            {
                CalculatedAt = DateTime.Now,
                TotalGoals = _goals.Count,
                TotalCompleted = _goals.Count(g => g.Status == "completed"),
                TotalActive = _goals.Count(g => g.Status == "active"),
                TotalAbandoned = _goals.Count(g => g.Status == "abandoned")
            };

            // За категоріями
            foreach (var category in _goals.Select(g => g.Category).Distinct())
            {
                var count = _goals.Count(g => g.Category == category);
                if (!analytics.ByCategory.ContainsKey(category))
                    analytics.ByCategory[category] = 0;
                analytics.ByCategory[category] += count;
            }

            // Із просрочками
            analytics.OverdueCount = _goals
                .Count(g => g.Status == "active" && g.Due.HasValue && g.Due.Value < DateTime.Now);

            // Середній прогрес
            if (_goals.Count > 0)
                analytics.AverageProgress = _goals.Average(g => g.Progress);

            // Якщо специфічна ціль
            if (!string.IsNullOrEmpty(goalId))
            {
                var goal = GetGoal(goalId);
                if (goal != null)
                {
                    analytics.GoalSpecific = new()
                    {
                        { "progress", goal.Progress },
                        { "stepsTotal", goal.Steps.Count },
                        { "stepsCompleted", goal.Steps.Count(s => s.Completed) },
                        { "daysActive", (int)(DateTime.Now - goal.Created).TotalDays },
                        { "priority", goal.Priority }
                    };
                }
            }

            return analytics;
        }

        /// <summary>
        /// Отримати цілі за категорією
        /// </summary>
        public List<Goal> GetGoalsByCategory(string category)
        {
            return _goals.Where(g => g.Category == category && g.Status == "active").ToList();
        }

        /// <summary>
        /// Отримати цілі за пріоритетом
        /// </summary>
        public List<Goal> GetGoalsByPriority(int priority)
        {
            return _goals.Where(g => g.Priority == priority && g.Status == "active").ToList();
        }

        /// <summary>
        /// Пауза цілі
        /// </summary>
        public async Task<bool> PauseGoalAsync(string goalId)
        {
            var goal = GetGoal(goalId);
            if (goal == null) return false;

            goal.Status = "paused";
            await UpdateGoalAsync(goal);

            _dataManager.RecordEntry("goal_paused", goalId, "goals");

            return true;
        }

        /// <summary>
        /// Відновити цільь
        /// </summary>
        public async Task<bool> ResumeGoalAsync(string goalId)
        {
            var goal = GetGoal(goalId);
            if (goal == null) return false;

            goal.Status = "active";
            goal.LastUpdated = DateTime.Now;
            await UpdateGoalAsync(goal);

            _dataManager.RecordEntry("goal_resumed", goalId, "goals");

            return true;
        }

        /// <summary>
        /// Позначити як абандонену
        /// </summary>
        public async Task<bool> AbandonGoalAsync(string goalId, string? reason = null)
        {
            var goal = GetGoal(goalId);
            if (goal == null) return false;

            goal.Status = "abandoned";
            goal.LastUpdated = DateTime.Now;
            await UpdateGoalAsync(goal);

            _dataManager.RecordEntry("goal_abandoned", reason ?? "No reason provided", "goals", new()
            {
                { "goalId", goalId }
            });

            return true;
        }

        /// <summary>
        /// Отримати прогрес за період
        /// </summary>
        public List<GoalProgress> GetProgressHistory(string goalId, int lastDays = 30)
        {
            var goal = GetGoal(goalId);
            if (goal == null) return new();

            var startDate = DateTime.Now.AddDays(-lastDays);
            return goal.ProgressLog
                .Where(p => p.Date >= startDate)
                .OrderByDescending(p => p.Date)
                .ToList();
        }

        /// <summary>
        /// Отримати статус дашборду
        /// </summary>
        public Dictionary<string, object?> GetDashboardStatus()
        {
            var active = GetActiveGoals();
            var overdue = GetOverdueGoals();
            var completed = GetCompletedGoals();

            return new Dictionary<string, object?>
            {
                { "totalGoals", _goals.Count },
                { "activeGoals", active.Count },
                { "overdueGoals", overdue.Count },
                { "completedToday", completed.Count(g => g.Completed?.Date == DateTime.Now.Date) },
                { "averageProgress", _goals.Count > 0 ? _goals.Average(g => g.Progress) : 0 },
                { "topPriority", active.FirstOrDefault()?.Title ?? "Немає активних цілей" },
                { "nextDue", active.Where(g => g.Due.HasValue).OrderBy(g => g.Due).FirstOrDefault()?.Due ?? null }
            };
        }
    }
}
