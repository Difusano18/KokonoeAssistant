using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace KokonoeAssistant
{
    public class ChatMessageVm : INotifyPropertyChanged
    {
        private string _content = "";

        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Role { get; set; } = "user";
        public DateTime Time { get; set; } = DateTime.Now;
        public string TimeStr => Time.ToString("HH:mm");
        public BitmapImage? ImageThumb { get; set; }
        public bool HasImage => ImageThumb != null;

        public string Content
        {
            get => _content;
            set { _content = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public class NoteVm
    {
        public string Path { get; set; } = "";
        public string Title { get; set; } = "";
    }

    public class HealthHistoryVm
    {
        public string DateStr { get; set; } = "";
        public string Summary { get; set; } = "";
    }

    public class HabitVm : INotifyPropertyChanged
    {
        private bool _done;

        public string Name { get; set; } = "";

        public bool Done
        {
            get => _done;
            set { _done = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public class GoalVm
    {
        public string Title { get; set; } = "";
    }

    public class DashThoughtVm
    {
        public string Time { get; set; } = "";
        public string Thought { get; set; } = "";
        public string MoodTag { get; set; } = "";
    }
}
