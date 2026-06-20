using System.Windows.Controls;

namespace KokonoeAssistant
{
    internal sealed class AgentChatUiResult
    {
        public string Reply { get; set; } = "";
        public TextBlock? FinalTextBlock { get; set; }
    }
}
