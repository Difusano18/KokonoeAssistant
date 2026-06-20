using System.ComponentModel;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;

namespace KokonoeAssistant
{
    public sealed class OllamaKeyRowVM : INotifyPropertyChanged
    {
        private string _name = "";
        private string _key = "";
        private string _statusText = "-";
        private MediaBrush _statusBrush = MediaBrushes.Gray;
        private MediaBrush _activeDotBrush = MediaBrushes.Transparent;

        public string Name { get => _name; set { _name = value; OnPropertyChanged(nameof(Name)); } }
        public string Key { get => _key; set { _key = value; OnPropertyChanged(nameof(Key)); } }
        public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(nameof(StatusText)); } }
        public MediaBrush StatusBrush { get => _statusBrush; set { _statusBrush = value; OnPropertyChanged(nameof(StatusBrush)); } }
        public MediaBrush ActiveDotBrush { get => _activeDotBrush; set { _activeDotBrush = value; OnPropertyChanged(nameof(ActiveDotBrush)); } }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
