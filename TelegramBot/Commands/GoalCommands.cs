using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KokonoeAssistant.Services;

namespace KokonoeAssistant.TelegramBot.Commands
{
    /// <summary>
    /// Telegram команди для управління цілями
    /// </summary>
    public class GoalCommands
    {
        private readonly GoalService _goalService;

        public GoalCommands(GoalService goalService)
        {
            _goalService = goalService;
        }

        /// <summary>
        /// /goal_add - Додати нову ціль
        /// Usage: /goal_add Назва цілі | категорія | пріоритет (1-5)
        /// </summary>
        public async Task<string> AddGoal(string input)
        {
            try
            {
                var parts = input.Split('|');
                if (parts.Length < 2)
                    return "❌ Формат: /goal_add Назва | категорія | пріоритет (опц)\n" +
                           "Категорії: work, personal, health, learning, project";

                var title = parts[0].Trim();
                var category = parts[1].Trim().ToLower();
                var priority = parts.Length > 2 ? int.Parse(parts[2].Trim()) : 3;

                priority = Math.Max(1, Math.Min(5, priority));

                var goal = await _goalService.AddGoalAsync(title, "", category, priority);

                return $"✅ Ціль додана!\n" +
                       $"📌 {goal.Title}\n" +
                       $"📂 {category}\n" +
                       $"⚡ Пріоритет: {priority}/5\n" +
                       $"📋 ID: `{goal.Id}`";
            }
            catch (Exception ex)
            {
                return $"❌ Помилка: {ex.Message}";
            }
        }

        /// <summary>
        /// /goal_list - Список активних цілей
        /// </summary>
        public string ListGoals()
        {
            try
            {
                var goals = _goalService.GetActiveGoals();
                if (goals.Count == 0)
                    return "📭 Немає активних цілей";

                var sb = new StringBuilder("📌 *Активні цілі:*\n\n");
                
                for (int i = 0; i < Math.Min(10, goals.Count); i++)
                {
                    var goal = goals[i];
                    var progress = (int)goal.Progress;
                    var progressBar = GenerateProgressBar(progress);
                    
                    sb.AppendLine($"{i + 1}. *{goal.Title}*");
                    sb.AppendLine($"   📊 {progressBar} {progress}%");
                    sb.AppendLine($"   📂 {goal.Category} | ⚡ {goal.Priority}/5");
                    
                    if (goal.Due.HasValue)
                    {
                        var daysLeft = (int)(goal.Due.Value - DateTime.Now).TotalDays;
                        sb.AppendLine($"   📅 {daysLeft} днів до DL");
                    }
                    
                    sb.AppendLine();
                }

                if (goals.Count > 10)
                    sb.AppendLine($"... та ще {goals.Count - 10} цілей");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"❌ Помилка: {ex.Message}";
            }
        }

        /// <summary>
        /// /goal_progress - Встановити прогрес цілі
        /// Usage: /goal_progress goal_id | 75
        /// </summary>
        public async Task<string> SetProgress(string input)
        {
            try
            {
                var parts = input.Split('|');
                if (parts.Length < 2)
                    return "❌ Формат: /goal_progress goal_id | відсоток_прогресу";

                var goalId = parts[0].Trim();
                var progress = double.Parse(parts[1].Trim());

                var goal = _goalService.GetGoal(goalId);
                if (goal == null)
                    return "❌ Ціль не знайдена";

                var oldProgress = goal.Progress;
                await _goalService.SetProgressAsync(goalId, progress);

                var emoji = progress >= 100 ? "🎉" : "📈";
                return $"{emoji} Прогрес оновлено!\n" +
                       $"📌 {goal.Title}\n" +
                       $"📊 {oldProgress:F0}% → {progress:F0}%\n" +
                       (progress >= 100 ? "✅ Ціль *ЗАВЕРШЕНА*!" : "");
            }
            catch (Exception ex)
            {
                return $"❌ Помилка: {ex.Message}";
            }
        }

        /// <summary>
        /// /goal_complete - Позначити ціль як завершену
        /// Usage: /goal_complete goal_id
        /// </summary>
        public async Task<string> CompleteGoal(string goalId)
        {
            try
            {
                var goal = _goalService.GetGoal(goalId);
                if (goal == null)
                    return "❌ Ціль не знайдена";

                await _goalService.SetProgressAsync(goalId, 100);
                return $"🎉 Ціль *{goal.Title}* завершена!\n" +
                       $"⏱️時間виконання: {(int)(DateTime.Now - goal.Created).TotalDays} днів";
            }
            catch (Exception ex)
            {
                return $"❌ Помилка: {ex.Message}";
            }
        }

        /// <summary>
        /// /goal_stats - Статистика цілей
        /// </summary>
        public string GetStats()
        {
            try
            {
                var stats = _goalService.GetGoalStats();
                var dashboard = _goalService.GetDashboardStatus();

                var sb = new StringBuilder("📊 *Статистика цілей:*\n\n");
                sb.AppendLine($"📌 Всього цілей: {stats.TotalGoals}");
                sb.AppendLine($"🔄 Активних: {stats.TotalActive}");
                sb.AppendLine($"✅ Завершено: {stats.TotalCompleted}");
                sb.AppendLine($"❌ Абандонено: {stats.TotalAbandoned}");
                sb.AppendLine();
                sb.AppendLine($"📈 Середній прогрес: {stats.AverageProgress:F0}%");
                sb.AppendLine($"⏰ Просрочено: {stats.OverdueCount}");
                sb.AppendLine();

                if (stats.ByCategory.Count > 0)
                {
                    sb.AppendLine("📂 *По категоріям:*");
                    foreach (var kvp in stats.ByCategory.OrderByDescending(x => x.Value))
                    {
                        sb.AppendLine($"  • {kvp.Key}: {kvp.Value}");
                    }
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"❌ Помилка: {ex.Message}";
            }
        }

        /// <summary>
        /// /goal_details goal_id - Деталі конкретної цілі
        /// </summary>
        public string GetDetails(string goalId)
        {
            try
            {
                var goal = _goalService.GetGoal(goalId);
                if (goal == null)
                    return "❌ Ціль не знайдена";

                var sb = new StringBuilder($"📌 *{goal.Title}*\n\n");
                sb.AppendLine($"📝 {goal.Description}");
                sb.AppendLine();
                sb.AppendLine($"📊 Прогрес: {GenerateProgressBar((int)goal.Progress)} {goal.Progress:F0}%");
                sb.AppendLine($"📂 Категорія: {goal.Category}");
                sb.AppendLine($"⚡ Пріоритет: {goal.Priority}/5");
                sb.AppendLine($"📋 Статус: {goal.Status}");
                sb.AppendLine();

                if (goal.Steps.Count > 0)
                {
                    sb.AppendLine($"📋 *Кроки ({goal.Steps.Count(s => s.Completed)}/{goal.Steps.Count}):*");
                    foreach (var step in goal.Steps)
                    {
                        var emoji = step.Completed ? "✅" : "⬜";
                        sb.AppendLine($"{emoji} {step.Title}");
                    }
                    sb.AppendLine();
                }

                if (goal.Due.HasValue)
                {
                    var daysLeft = (int)(goal.Due.Value - DateTime.Now).TotalDays;
                    sb.AppendLine($"📅 Deadline: {goal.Due:dd.MM.yyyy} ({daysLeft} днів)");
                }

                sb.AppendLine($"📆 Створена: {goal.Created:dd.MM.yyyy}");
                sb.AppendLine($"⏱️ Днів активна: {goal.DaysActive}");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"❌ Помилка: {ex.Message}";
            }
        }

        /// <summary>
        /// /goal_pause goal_id - Призупинити ціль
        /// </summary>
        public async Task<string> PauseGoal(string goalId)
        {
            try
            {
                await _goalService.PauseGoalAsync(goalId);
                return "⏸️ Ціль призупинена";
            }
            catch
            {
                return "❌ Помилка: ціль не знайдена";
            }
        }

        /// <summary>
        /// /goal_resume goal_id - Відновити ціль
        /// </summary>
        public async Task<string> ResumeGoal(string goalId)
        {
            try
            {
                await _goalService.ResumeGoalAsync(goalId);
                return "▶️ Ціль відновлена";
            }
            catch
            {
                return "❌ Помилка: ціль не знайдена";
            }
        }

        /// <summary>
        /// /goal_remove goal_id - Видалити ціль
        /// </summary>
        public async Task<string> RemoveGoal(string goalId)
        {
            try
            {
                var goal = _goalService.GetGoal(goalId);
                if (goal == null)
                    return "❌ Ціль не знайдена";

                await _goalService.RemoveGoalAsync(goalId);
                return $"🗑️ Ціль *{goal.Title}* видалена";
            }
            catch
            {
                return "❌ Помилка при видаленні цілі";
            }
        }

        private string GenerateProgressBar(int progress)
        {
            var filled = (progress / 10);
            var empty = 10 - filled;
            return $"[{'█'.ToString().PadRight(filled, '█').PadRight(10, '░')}]";
        }

        /// <summary>
        /// /goal_dashboard - Дашборд цілей
        /// </summary>
        public string GetDashboard()
        {
            try
            {
                var dashboard = _goalService.GetDashboardStatus();
                var sb = new StringBuilder("🎯 *ДАШБОРД ЦІЛЕЙ*\n\n");
                
                sb.AppendLine($"📋 Всього: {dashboard["totalGoals"]}");
                sb.AppendLine($"🔄 Активних: {dashboard["activeGoals"]}");
                sb.AppendLine($"⏰ Просрочено: {dashboard["overdueGoals"]}");
                sb.AppendLine($"✅ Завершено сьогодні: {dashboard["completedToday"]}");
                sb.AppendLine();
                sb.AppendLine($"📈 Середній прогрес: {dashboard["averageProgress"]:F0}%");
                sb.AppendLine();
                sb.AppendLine($"🎯 *Пріоритет:* {dashboard["topPriority"]}");
                
                if (dashboard["nextDue"] != null)
                {
                    sb.AppendLine($"📅 *Наступна:* {dashboard["nextDue"]:dd.MM.yyyy}");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"❌ Помилка: {ex.Message}";
            }
        }
    }
}
