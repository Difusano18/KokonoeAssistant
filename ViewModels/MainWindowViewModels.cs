using System.Windows.Media.Imaging;

namespace KokonoeAssistant
{
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

    internal sealed class NoteVm
    {
        public string Path { get; set; } = "";
        public string Title { get; set; } = "";
    }

}
