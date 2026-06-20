using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace KokonoeAssistant
{
    internal sealed class MatrixColumn
    {
        public double X;
        public double Y;
        public double Speed;
        public List<TextBlock> Cells = new();
        public int Length;
    }

    internal sealed class MemoryCortexNodeVm
    {
        public string Id { get; init; } = "";
        public string Text { get; init; } = "";
        public string Category { get; init; } = "general";
        public float Importance { get; init; }
        public int ConfirmCount { get; init; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Radius { get; set; }
        public SKColor Color { get; init; } = SKColors.White;
    }

    internal sealed class AgentChatUiResult
    {
        public string Reply { get; set; } = "";
        public TextBlock? FinalTextBlock { get; set; }
    }

    internal sealed class ChatMessageVm
    {
        public string Role { get; set; } = "";
        public string Content { get; set; } = "";
        public System.DateTime Time { get; set; } = System.DateTime.Now;
        public string TimeStr => Time.ToString("HH:mm");
        public BitmapImage? ImageThumb { get; set; }
    }

    internal sealed class DashThoughtVm
    {
        public string Time { get; set; } = "";
        public string Thought { get; set; } = "";
        public string MoodTag { get; set; } = "";
    }

    internal sealed class CalEventVm
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string TimeStr { get; set; } = "";
        public string DateStr { get; set; } = "";
    }

    internal sealed class NoteVm
    {
        public string Path { get; set; } = "";
        public string Title { get; set; } = "";
    }

    public sealed class OllamaKeyRowVM : INotifyPropertyChanged
    {
        private string _name = "";
        private string _key = "";
        private string _statusText = "-";
        private System.Windows.Media.Brush _statusBrush = System.Windows.Media.Brushes.Gray;
        private System.Windows.Media.Brush _activeDotBrush = System.Windows.Media.Brushes.Transparent;

        public string Name { get => _name; set { _name = value; OnPropertyChanged(nameof(Name)); } }
        public string Key { get => _key; set { _key = value; OnPropertyChanged(nameof(Key)); } }
        public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(nameof(StatusText)); } }
        public System.Windows.Media.Brush StatusBrush { get => _statusBrush; set { _statusBrush = value; OnPropertyChanged(nameof(StatusBrush)); } }
        public System.Windows.Media.Brush ActiveDotBrush { get => _activeDotBrush; set { _activeDotBrush = value; OnPropertyChanged(nameof(ActiveDotBrush)); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
