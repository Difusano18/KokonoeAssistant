using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KokonoeAssistant.Models;
using KokonoeAssistant.Services;

namespace KokonoeAssistant.TelegramBot.Commands
{
    /// <summary>
    /// Telegram команди для управління звичками
    /// </summary>
    public class HabitCommands
    {
        private readonly HabitService _habitService;

        public HabitCommands(HabitService habitService)
        {
            _habitService = habitService;
        }

        /// <summary>
        /// /habit_add - Додати нову звичку
        /// Usage: /habit_add Назва звички | категорія | дні_на_тиждень (опц)
        /// </summary>
        public async Task<string> AddHabit(string input)
        {
            try
            {
                var parts = input.Split('|');
                if (parts.Length < 2)
                    return "❌ Формат: /habit_add Назва | категорія | дні_на_тиждень (1-7)\n" +
                           "Категорії: health, productivity, learning, wellness, social, general";

                var name = parts[0].Trim();
                var category = parts[1].Trim().ToLower();
                var daysPerWeek = parts.Length > 2 ? int.Parse(parts[2].Trim()) : 7;

                daysPerWeek = Math.Max(1, Math.Min(7, daysPerWeek));

                var habit = await _habitService.AddHabitAsync(name, "", category, daysPerWeek, "daily");

                return $"✅ Звичка додана!\n" +
                       $"📌 {habit.Name}\n" +
                       $"📂 {category}\n" +
                       $"📅 Мета: {daysPerWeek} днів/тиждень\n" +
                       $"📋 ID: `{habit.Id}`";
            }
            catch (Exception ex)
            {
                return $"❌ Помилка: {ex.Message}";
            }
        }

        /// <summary>
        /// /habit_list - Список всіх звичок
        /// </summary>
        public string ListHabits()
        {
            try
            {
                var habits = _habitService.GetActiveHabits();
                if (habits.Count == 0)
                    return "📭 Немає активних звичок";

                var sb = new StringBuilder("📌 *Активні звички:*\n\n");

                for (int i = 0; i < Math.Min(15, habits.Count); i++)
                {
                    var habit = habits[i];
                    var stats = _habitService.GetHabitStats(habit.Id, 30);
                    
                    var emoji = habit.LastCheckedIn?.Date == DateTime.Now.Date ? "✅" : "⭕";
                    sb.AppendLine($"{emoji} *{habit.Name}*");
                    
                    sb.AppendLine($"   📊 Виконання: {stats.CompletionRate:F0}% | Стрік: {stats.CurrentStreak}дн");
                    sb.AppendLine($"   📂 {habit.Category} | 📅 {habit.TargetDaysPerWeek}д/тиж");
                    sb.AppendLine();
                }

                if (habits.Count > 15)
                    sb.AppendLine($"... та ще {habits.Count - 15} звичок");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"❌ Помилка: {ex.Message}";
            }
        }

        /// <summary>
        /// /habit_checkin - Перевірити звичку
        /// Usage: /habit_checkin habit_id | value (опц) | note (опц)
        /// </summary>
        public async Task<string> CheckIn(string input)
        {
            try
            {
                var parts = input.Split('|');
                if (parts.Length < 1)
                    return "❌ Формат: /habit_checkin habit_id | значення (опц) | нотатка (опц)";

                var habitId = parts[0].Trim();
                var value = parts.Length > 1 ? int.TryParse(parts[1].Trim(), out var v) ? v : (int?)null : null;
                var note = parts.Length > 2 ? parts[2].Trim() : null;

                var habit = _habitService.GetHabit(habitId);
                if (habit == null)
                    return "❌ Звичка не знайдена";

                var checkIn = await _habitService.RecordCheckInAsync(habitId, true, value, note);

                var stats = _habitService.GetHabitStats(habitId, 30);

                return $"✅ Звичка *{habit.Name}* відзначена!\n" +
                       $"🔥 Поточний стрік: {habit.CurrentStreak} днів\n" +
                       $"📊 Цього місяця: {stats.LastMonthCompletions} днів\n" +
                       $"🎯 Виконання: {stats.CompletionRate:F0}%";
            }
            catch (Exception ex)
            {
                return $"❌ Помилка: {ex.Message}";
            }
        }

        /// <summary>
        /// /habit_calendar - Календар звички за місяць
        /// Usage: /habit_calendar habit_id | год (опц) | місяць (опц)
        /// </summary>
        public string GetCalendar(string input)
        {
            try
            {
                var parts = input.Split('|');
                var habitId = parts[0].Trim();
                
                int year = parts.Length > 1 && int.TryParse(parts[1].Trim(), out var y) ? y : DateTime.Now.Year;
                int month = parts.Length > 2 && int.TryParse(parts[2].Trim(), out var m) ? m : DateTime.Now.Month;

                var habit = _habitService.GetHabit(habitId);
                if (habit == null)
                    return "❌ Звичка не знайдена";

                var calendar = _habitService.GetMonthlyCalendar(habitId, year, month);

                var sb = new StringBuilder($"📅 *{habit.Name} - {year}-{month:D2}*\n\n");

                var week = new List<string>();
                var dayOfWeek = new DateTime(year, month, 1).DayOfWeek;
                var daysInMonth = DateTime.DaysInMonth(year, month);

                // Додати пусті дні на початку
                for (int i = 0; i < (int)dayOfWeek; i++)
                    week.Add("  ");

                // Додати дні місяця
                for (int day = 1; day <= daysInMonth; day++)
                {
                    var emoji = calendar.Days.ContainsKey(day) && calendar.Days[day] ? "✅" : "⬜";
                    week.Add($"{emoji}{day:D2}");

                    if (week.Count == 7)
                    {
                        sb.AppendLine(string.Join(" ", week));
                        week.Clear();
                    }
                }

                // Завершити останній тиждень
                if (week.Count > 0)
                {
                    while (week.Count < 7)
                        week.Add("  ");
                    sb.AppendLine(string.Join(" ", week));
                }

                var stats = _habitService.GetHabitStats(habitId, 30);
                sb.AppendLine();
                sb.AppendLine($"📊 Виконання: {stats.CompletionRate:F0}%");
                sb.AppendLine($"🔥 Поточний стрік: {stats.CurrentStreak} днів");
                sb.AppendLine($"📈 Найкращий стрік: {stats.BestStreak} днів");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"❌ Помилка: {ex.Message}";
            }
        }

        /// <summary>
        /// /habit_stats - Статистика звичок
        /// </summary>
        public string GetStats(string input = "")
        {
            try
            {
                // Якщо передано ID конкретної звички
                if (!string.IsNullOrWhiteSpace(input) && !input.Contains('|'))
                {
                    var habit = _habitService.GetHabit(input.Trim());
                    if (habit == null)
                        return "❌ Звичка не знайдена";

                    var stats = _habitService.GetHabitStats(habit.Id, 
                        input.Contains('|') ? int.Parse(input.Split('|')[1].Trim()) : 30);

                    var sb = new StringBuilder($"📊 *{habit.Name}:*\n\n");
                    sb.AppendLine($"📌 {habit.Category} | {habit.TargetDaysPerWeek}x в тиждень");
                    sb.AppendLine($"📊 Виконання: {stats.CompletionRate:F0}%");
                    sb.AppendLine($"🔥 Поточний стрік: {stats.CurrentStreak} днів");
                    sb.AppendLine($"📈 Найкращий: {stats.BestStreak} днів");
                    sb.AppendLine($"📈 За місяць: {stats.LastMonthCompletions} днів");
                    return sb.ToString();
                }

                // Загальна статистика
                var summary = _habitService.GetAllHabitsStats();

                var result = new StringBuilder("📊 *Статистика звичок:*\n\n");
                result.AppendLine($"📌 Всього звичок: {summary.TotalHabits}");
                result.AppendLine($"🔄 Активних: {summary.ActiveHabits}");
                result.AppendLine($"⏸️ Призупинених: {summary.PausedHabits}");
                result.AppendLine($"❌ Абандонено: {summary.AbandonedHabits}");
                result.AppendLine();
                result.AppendLine($"📈 Середнe виконання: {summary.AverageCompletionRate:F0}%");
                result.AppendLine($"🔥 Найдовший стрік: {summary.LongestCurrentStreak} днів");
                result.AppendLine();

                if (summary.ByCategory.Count > 0)
                {
                    result.AppendLine("📂 *По категоріям:*");
                    foreach (var kvp in summary.ByCategory.OrderByDescending(x => x.Value))
                    {
                        result.AppendLine($"  • {kvp.Key}: {kvp.Value}");
                    }
                    result.AppendLine();
                }

                if (summary.TopHabits.Count > 0)
                {
                    result.AppendLine("🏆 *ТОП звички:*");
                    for (int i = 0; i < Math.Min(5, summary.TopHabits.Count); i++)
                    {
                        result.AppendLine($"  {i + 1}. {summary.TopHabits[i].habitName}: {summary.TopHabits[i].completions} днів");
                    }
                }

                return result.ToString();
            }
            catch (Exception ex)
            {
                return $"❌ Помилка: {ex.Message}";
            }
        }

        /// <summary>
        /// /habit_today - Звички які потребують сьогоднішньої перевірки
        /// </summary>
        public string GetDueToday()
        {
            try
            {
                var due = _habitService.GetDueTodayHabits();
                
                if (due.Count == 0)
                    return "✅ *Усі звички перевірені сьогодні!*";

                var sb = new StringBuilder($"📌 *Сьогодні ({due.Count} звичок):*\n\n");

                for (int i = 0; i < Math.Min(10, due.Count); i++)
                {
                    var habit = due[i];
                    var stats = _habitService.GetHabitStats(habit.Id, 30);
                    
                    sb.AppendLine($"⭕ *{habit.Name}*");
                    sb.AppendLine($"   🔥 Стрік: {stats.CurrentStreak} днів");
                    sb.AppendLine($"   📊 Виконання: {stats.CompletionRate:F0}%");
                    sb.AppendLine();
                }

                if (due.Count > 10)
                    sb.AppendLine($"... та ще {due.Count - 10} звичок");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"❌ Помилка: {ex.Message}";
            }
        }

        /// <summary>
        /// /habit_details - Деталі звички
        /// </summary>
        public string GetDetails(string habitId)
        {
            try
            {
                var habit = _habitService.GetHabit(habitId);
                if (habit == null)
                    return "❌ Звичка не знайдена";

                var stats = _habitService.GetHabitStats(habitId, 30);

                var sb = new StringBuilder($"📌 *{habit.Name}*\n\n");
                sb.AppendLine($"📝 {habit.Description}");
                sb.AppendLine($"📂 Категорія: {habit.Category}");
                sb.AppendLine($"📅 Мета: {habit.TargetDaysPerWeek} днів/тиждень");
                sb.AppendLine($"📋 Статус: {habit.Status}");
                sb.AppendLine();
                
                sb.AppendLine($"📊 *Статистика:*");
                sb.AppendLine($"  • Виконань: {stats.DaysCompleted}/{stats.DaysTracked}");
                sb.AppendLine($"  • Виконання: {stats.CompletionRate:F0}%");
                sb.AppendLine($"  • 🔥 Поточний стрік: {stats.CurrentStreak} днів");
                sb.AppendLine($"  • 📈 Найкращий стрік: {stats.BestStreak} днів");
                sb.AppendLine($"  • ⏱️ Днів від початку: {stats.DaysSinceStarted}");
                sb.AppendLine();

                if (habit.LastCheckedIn.HasValue)
                {
                    var lastCheck = habit.LastCheckedIn.Value;
                    sb.AppendLine($"✅ Остання перевірка: {lastCheck:dd.MM.yyyy HH:mm}");
                }

                sb.AppendLine($"📆 Створена: {habit.Created:dd.MM.yyyy}");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"❌ Помилка: {ex.Message}";
            }
        }

        /// <summary>
        /// /habit_remove - Видалити звичку
        /// </summary>
        public async Task<string> RemoveHabit(string habitId)
        {
            try
            {
                var habit = _habitService.GetHabit(habitId);
                if (habit == null)
                    return "❌ Звичка не знайдена";

                await _habitService.RemoveHabitAsync(habitId);
                return $"🗑️ Звичка *{habit.Name}* видалена";
            }
            catch
            {
                return "❌ Помилка при видаленні звички";
            }
        }

        /// <summary>
        /// /habit_pause - Призупинити звичку
        /// </summary>
        public async Task<string> PauseHabit(string habitId)
        {
            try
            {
                var habit = _habitService.GetHabit(habitId);
                if (habit == null)
                    return "❌ Звичка не знайдена";

                habit.Status = "paused";
                await _habitService.UpdateHabitAsync(habit);
                return $"⏸️ Звичка *{habit.Name}* призупинена";
            }
            catch
            {
                return "❌ Помилка при призупиненні звички";
            }
        }

        /// <summary>
        /// /habit_resume - Відновити звичку
        /// </summary>
        public async Task<string> ResumeHabit(string habitId)
        {
            try
            {
                var habit = _habitService.GetHabit(habitId);
                if (habit == null)
                    return "❌ Звичка не знайдена";

                habit.Status = "active";
                await _habitService.UpdateHabitAsync(habit);
                return $"▶️ Звичка *{habit.Name}* відновлена";
            }
            catch
            {
                return "❌ Помилка при відновленні звички";
            }
        }

        /// <summary>
        /// /habit_dashboard - Дашборд звичок
        /// </summary>
        public string GetDashboard()
        {
            try
            {
                var stats = _habitService.GetAllHabitsStats();
                var today = _habitService.GetDueTodayHabits();

                var sb = new StringBuilder("✅ *ДАШБОРД ЗВИЧОК*\n\n");
                
                sb.AppendLine($"📌 Всього: {stats.TotalHabits}");
                sb.AppendLine($"🔄 Активних: {stats.ActiveHabits}");
                sb.AppendLine($"⏸️ Призупинених: {stats.PausedHabits}");
                sb.AppendLine();
                sb.AppendLine($"📊 Середнe виконання: {stats.AverageCompletionRate:F0}%");
                sb.AppendLine($"🔥 Найдовший стрік: {stats.LongestCurrentStreak} днів");
                sb.AppendLine();
                sb.AppendLine($"⭕ **Сьогодні залишилось: {today.Count}** звичок");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"❌ Помилка: {ex.Message}";
            }
        }
    }
}
