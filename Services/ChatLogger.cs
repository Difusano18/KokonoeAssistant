using System;
using System.Text;

namespace KokonoeAssistant.Services
{
    /// <summary>
    /// Централізований логер чатів — записує ВСІ повідомлення в Obsidian vault.
    /// Працює автоматично на рівні інфраструктури — LLM не потрібно нічого робити.
    /// Підтримує: desktop чат, Telegram, спонтанні повідомлення, проактивні перевірки.
    /// </summary>
    public class ChatLogger
    {
        private readonly ObsidianMcpService _obsidian;
        private string? _sessionPath;
        private readonly object _lock = new();

        public ChatLogger(ObsidianMcpService obsidian)
        {
            _obsidian = obsidian;
        }

        /// <summary>
        /// Поточний шлях сесійного лога. Створюється ліниво при першому записі.
        /// </summary>
        public string? CurrentSessionPath => _sessionPath;

        /// <summary>
        /// Логує обмін повідомленнями (user → Kokonoe).
        /// </summary>
        public void LogExchange(string source, string userMsg, string botReply)
        {
            lock (_lock)
            {
                try
                {
                    EnsureSessionFile();

                    var now = DateTime.Now;
                    var sb = new StringBuilder();
                    sb.AppendLine("***");
                    sb.AppendLine($"**[{now:HH:mm}] [{source}] Вова:** {userMsg.Trim()}");
                    sb.AppendLine();
                    sb.AppendLine($"**[{now:HH:mm}] [{source}] Kokonoe:** {botReply.Trim()}");
                    sb.AppendLine();

                    _obsidian.AppendToNote(_sessionPath!, sb.ToString());
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ChatLogger] LogExchange: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Логує одностороннє повідомлення від Kokonoe (спонтанне, нагадування, аналітика).
        /// </summary>
        public void LogOutgoing(string source, string message, string? category = null)
        {
            lock (_lock)
            {
                try
                {
                    EnsureSessionFile();

                    var now = DateTime.Now;
                    var tag = category != null ? $" ({category})" : "";
                    var sb = new StringBuilder();
                    sb.AppendLine("***");
                    sb.AppendLine($"**[{now:HH:mm}] [{source}]{tag} Kokonoe →** {message.Trim()}");
                    sb.AppendLine();

                    _obsidian.AppendToNote(_sessionPath!, sb.ToString());
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ChatLogger] LogOutgoing: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Логує вхідне повідомлення від юзера (без відповіді, наприклад TG з очікуванням).
        /// </summary>
        public void LogIncoming(string source, string message)
        {
            lock (_lock)
            {
                try
                {
                    EnsureSessionFile();

                    var now = DateTime.Now;
                    var sb = new StringBuilder();
                    sb.AppendLine("***");
                    sb.AppendLine($"**[{now:HH:mm}] [{source}] Вова →** {message.Trim()}");
                    sb.AppendLine();

                    _obsidian.AppendToNote(_sessionPath!, sb.ToString());
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ChatLogger] LogIncoming: {ex.Message}");
                }
            }
        }

        private void EnsureSessionFile()
        {
            if (_sessionPath != null) return;

            _sessionPath = $"Chats/chat_{DateTime.Now:yyyy-MM-dd_HH-mm}.md";

            // Посилання на попередній чат
            var prevLink = "";
            try
            {
                var allLogs = _obsidian.ListNotes()
                    .FindAll(p => p.StartsWith("Chats/chat_") && p.EndsWith(".md"));
                allLogs.Sort();
                // Останній лог що не є поточним
                var prev = allLogs.FindLast(p => p != _sessionPath);
                if (prev != null)
                {
                    var name = System.IO.Path.GetFileNameWithoutExtension(prev);
                    prevLink = $"\nПопередня сесія: [[{name}]]\n";
                }
            }
            catch { }

            var header = $"---\ntype: chat-log\ntags: [kokonoe, chat]\ndate: {DateTime.Now:yyyy-MM-dd}\n---\n\n# Чат {DateTime.Now:dd.MM.yyyy HH:mm}{prevLink}\n\n";
            _obsidian.WriteNote(_sessionPath, header);
        }
    }
}
