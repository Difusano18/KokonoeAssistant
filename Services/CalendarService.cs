using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace KokonoeAssistant.Services
{
    public class CalendarEvent
    {
        public string   Id          { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string   Title       { get; set; } = "";
        public DateTime EventAt     { get; set; }
        public string   Description { get; set; } = "";
        public bool     Reminded24h { get; set; } = false;
        public bool     RemindedDay { get; set; } = false;
    }

    public class CalendarService
    {
        private readonly string _path;
        private List<CalendarEvent> _events = new();

        public CalendarService(string dataDir)
        {
            _path = Path.Combine(dataDir, "calendar-events.json");
            _events = Load();
            EnsureAnnualEvents();
        }

        private void EnsureAnnualEvents()
        {
            var year = DateTime.Today.Year;
            bool changed = false;

            var annualEvents = new List<(string Title, int Month, int Day, string Desc)>
            {
                ("🎄 Новий Рік", 1, 1, "Початок нового року!"),
                ("💖 День святого Валентина", 2, 14, "День закоханих"),
                ("💻 День створення Коконое", 4, 6, "Річниця запуску Коконое як асистента!"),
                ("🎉 День народження Коконое", 4, 18, "Привітати Коконое з її днем за лором BlazBlue!"),
                ("🎈 День народження Творця", 4, 21, "Головне свято року!"),
                ("🎃 Геловін", 10, 31, "Моторошне свято"),
                ("❄️ Різдво", 12, 25, "Зимові свята")
            };

            foreach (var evt in annualEvents)
            {
                if (!_events.Any(e => e.Title.Contains(evt.Title, StringComparison.OrdinalIgnoreCase) && e.EventAt.Year == year))
                {
                    _events.Add(new CalendarEvent 
                    { 
                        Title = evt.Title, 
                        EventAt = new DateTime(year, evt.Month, evt.Day, 10, 0, 0), 
                        Description = evt.Desc 
                    });
                    changed = true;
                }
            }

            if (changed) Save();
        }

        private List<CalendarEvent> Load()
        {
            try
            {
                if (File.Exists(_path))
                    return JsonConvert.DeserializeObject<List<CalendarEvent>>(
                        File.ReadAllText(_path)) ?? new();
            }
            catch (Exception suppressedEx72) { KokoSystemLog.Write("CALENDARSERVICE-CATCH", "Load failed near source line 72: " + suppressedEx72); }
            return new();
        }

        public void Save()
        {
            try { File.WriteAllText(_path, JsonConvert.SerializeObject(_events, Formatting.Indented)); }
            catch (Exception suppressedEx79) { KokoSystemLog.Write("CALENDARSERVICE-CATCH", "Save failed near source line 79: " + suppressedEx79); }
        }

        public List<CalendarEvent> GetAll() =>
            _events.OrderBy(e => e.EventAt).ToList();

        public List<CalendarEvent> GetForDay(DateTime day) =>
            _events.Where(e => e.EventAt.Date == day.Date)
                   .OrderBy(e => e.EventAt).ToList();

        public List<CalendarEvent> GetUpcoming(int days = 30) =>
            _events.Where(e => e.EventAt.Date >= DateTime.Today &&
                               e.EventAt.Date <= DateTime.Today.AddDays(days))
                   .OrderBy(e => e.EventAt).ToList();

        public bool HasEventsOnDay(DateTime day) =>
            _events.Any(e => e.EventAt.Date == day.Date);

        /// <summary>Події завтра — ще не нагадані (нагадування за 24г)</summary>
        public List<CalendarEvent> GetDue24hReminders() =>
            _events.Where(e => !e.Reminded24h &&
                               e.EventAt.Date == DateTime.Today.AddDays(1).Date)
                   .ToList();

        /// <summary>Події сьогодні — ще не нагадані в день події</summary>
        public List<CalendarEvent> GetDueTodayReminders() =>
            _events.Where(e => !e.RemindedDay &&
                               e.EventAt.Date == DateTime.Today)
                   .ToList();

        public void Add(CalendarEvent ev) { _events.Add(ev); Save(); }

        public void Delete(string id) { _events.RemoveAll(e => e.Id == id); Save(); }

        public void MarkReminded24h(string id)
        {
            var ev = _events.FirstOrDefault(e => e.Id == id);
            if (ev != null) { ev.Reminded24h = true; Save(); }
        }

        public void MarkRemindedDay(string id)
        {
            var ev = _events.FirstOrDefault(e => e.Id == id);
            if (ev != null) { ev.RemindedDay = true; Save(); }
        }
    }
}
