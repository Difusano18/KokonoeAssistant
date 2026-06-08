using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using KokonoeAssistant.Services;
using static KokonoeAssistant.Services.ChatRepository;
using MessageBox = System.Windows.MessageBox;

namespace KokonoeAssistant.Windows
{
    public partial class ChatWindow : Window
    {
        private ObservableCollection<ChatMessage> _messages = new();
        private ObservableCollection<ChatMessage> _pinnedMessages = new();
        private string _vaultPath;

        // Parameterless constructor for XAML instantiation
        public ChatWindow() : this(null!) { }

        public ChatWindow(string? vaultPath = null)
        {
            InitializeComponent();
            _vaultPath = vaultPath ?? AppSettings.Load().VaultPath;
            ServiceContainer.Initialize(_vaultPath);
            LoadMessages();
            SubscribeToEvents();
        }

        private void LoadMessages()
        {
            try
            {
                _messages.Clear();
                var repo = ServiceContainer.ChatRepository;
                var messages = repo.GetMessages(100);
                
                foreach (var msg in messages.OrderBy(m => m.Timestamp))
                {
                    _messages.Add(msg);
                }

                LoadPinnedMessages();
                MessageList.ItemsSource = _messages;
                if (_messages.Count > 0)
                    MessageList.ScrollIntoView(_messages.Last());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatWindow] Error loading messages: {ex.Message}");
            }
        }

        private void LoadPinnedMessages()
        {
            try
            {
                _pinnedMessages.Clear();
                var repo = ServiceContainer.ChatRepository;
                var pinned = repo.GetPinnedMessages(20);
                
                foreach (var msg in pinned)
                {
                    _pinnedMessages.Add(msg);
                }
                
                PinnedList.ItemsSource = _pinnedMessages;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatWindow] Error loading pinned: {ex.Message}");
            }
        }

        private void SubscribeToEvents()
        {
            EventBus.SubscribeChatMessage((s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    var msg = new ChatMessage
                    {
                        Content = e.Content,
                        Role = e.Role,
                        Timestamp = e.Timestamp,
                        Author = "EventBus"
                    };
                    _messages.Add(msg);
                    MessageList.ScrollIntoView(msg);
                });
            });
        }

        private void SendMessage_Click(object sender, RoutedEventArgs e)
        {
            SendChatMessage();
        }

        private void SendChatMessage()
        {
            if (string.IsNullOrWhiteSpace(InputTextBox.Text))
                return;

            try
            {
                var repo = ServiceContainer.ChatRepository;
                var message = new ChatMessage
                {
                    Content = InputTextBox.Text,
                    Role = "user",
                    Timestamp = DateTime.Now,
                    Author = Environment.UserName,
                    FormattingTags = new Dictionary<string, string>()
                };

                repo.InsertMessage(message);
                _messages.Add(message);
                EventBus.PublishChatMessage(message.Content, message.Role);
                
                InputTextBox.Clear();
                MessageList.ScrollIntoView(message);

                Debug.WriteLine($"[ChatWindow] Message sent: {message.Content.Substring(0, Math.Min(30, message.Content.Length))}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending message: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            var query = InputTextBox.Text;
            if (string.IsNullOrWhiteSpace(query))
            {
                MessageBox.Show("Enter search query", "Search", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var search = ServiceContainer.SearchService;
                var results = search.Search(query, 50);
                
                _messages.Clear();
                foreach (var result in results.OrderByDescending(r => r.Relevance))
                {
                    _messages.Add(new ChatMessage
                    {
                        Content = $"[{result.Type}] {result.Preview}",
                        Role = "search-result",
                        Timestamp = result.Timestamp,
                        Author = result.Title
                    });
                }

                MessageList.ScrollIntoView(_messages.FirstOrDefault());

                Debug.WriteLine($"[ChatWindow] Search: {query} → {results.Count} results");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Search error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FormatBold_Click(object sender, RoutedEventArgs e)
        {
            WrapSelection("**", "**");
        }

        private void FormatItalic_Click(object sender, RoutedEventArgs e)
        {
            WrapSelection("*", "*");
        }

        private void FormatCode_Click(object sender, RoutedEventArgs e)
        {
            WrapSelection("`", "`");
        }

        private void FormatQuote_Click(object sender, RoutedEventArgs e)
        {
            WrapSelection("> ", "");
        }

        private void WrapSelection(string before, string after)
        {
            var text = InputTextBox.Text;
            var selStart = InputTextBox.SelectionStart;
            var selLength = InputTextBox.SelectionLength;

            if (selLength == 0)
                return;

            var selected = text.Substring(selStart, selLength);
            InputTextBox.Text = text.Remove(selStart, selLength).Insert(selStart, before + selected + after);
            InputTextBox.Focus();
        }

        private async void RecordVoice_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var audio = ServiceContainer.AudioRecordService;
                
                if (audio.IsRecording)
                {
                    await audio.StopRecordingAsync();
                    RecordButton.Content = "🔄 Processing...";
                    RecordButton.IsEnabled = false;

                    var audioBytes = await audio.GetRecordingBytesAsync();
                    if (audioBytes != null && audioBytes.Length > 0)
                    {
                        var whisper = ServiceContainer.WhisperService;
                        var text = await whisper.TranscribeAsync(audioBytes, "uk");
                        
                        if (!string.IsNullOrEmpty(text))
                        {
                            InputTextBox.Text += text + " ";
                            Debug.WriteLine($"[ChatWindow] Transcribed: {text}");
                        }
                        else
                        {
                            MessageBox.Show("Audio was captured, but transcription returned empty text. Run test_mic in the main chat or check microphone level.", "Voice recorder", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }

                    RecordButton.Content = "🎤 Record";
                    RecordButton.IsEnabled = true;
                }
                else
                {
                    var started = await audio.StartRecordingAsync();
                    if (!started)
                    {
                        MessageBox.Show(audio.LastError ?? "Microphone failed to start", "Voice recorder", MessageBoxButton.OK, MessageBoxImage.Error);
                        RecordButton.Content = "🎤 Record";
                        RecordButton.IsEnabled = true;
                        return;
                    }

                    RecordButton.Content = "⏹ Stop";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Recording error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                RecordButton.Content = "🎤 Record";
                RecordButton.IsEnabled = true;
            }
        }

        private void PinMessage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selected = MessageList.SelectedItem as ChatMessage;
                if (selected == null)
                {
                    MessageBox.Show("Select a message to pin", "Pin", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var repo = ServiceContainer.ChatRepository;
                repo.TogglePinMessage(selected.Id, !selected.IsPinned);
                selected.IsPinned = !selected.IsPinned;
                
                LoadPinnedMessages();
                Debug.WriteLine($"[ChatWindow] Pinned: {selected.Content.Substring(0, Math.Min(30, selected.Content.Length))}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Pin error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportChat_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var repo = ServiceContainer.ChatRepository;
                var exportPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    $"chat-export-{DateTime.Now:yyyy-MM-dd-HHmmss}.txt"
                );

                var content = string.Join("\n", _messages.Select(m => $"[{m.Author}] {m.Timestamp:HH:mm}: {m.Content}"));
                File.WriteAllText(exportPath, content);

                MessageBox.Show($"Exported to:\n{exportPath}", "Export Success", MessageBoxButton.OK, MessageBoxImage.Information);
                Debug.WriteLine($"[ChatWindow] Exported: {exportPath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void SummarizeChat_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SummarizeButton.IsEnabled = false;
                SummarizeButton.Content = "⏳ Summarizing...";

                var summarizer = ServiceContainer.SummarizerService;
                var summary = await summarizer.SummarizeChatAsync(_messages.ToList(), 300);

                if (summary != null)
                {
                    MessageBox.Show($"Summary:\n\n{summary.Summary}", "Chat Summary", MessageBoxButton.OK, MessageBoxImage.Information);
                    Debug.WriteLine($"[ChatWindow] Summary: {summary.Summary.Substring(0, Math.Min(100, summary.Summary.Length))}");
                }
                else
                {
                    MessageBox.Show("Failed to generate summary", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                SummarizeButton.Content = "📊 Summary";
                SummarizeButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Summarize error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                SummarizeButton.Content = "📊 Summary";
                SummarizeButton.IsEnabled = true;
            }
        }

        // ==================== MENU HANDLERS ====================

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Debug.WriteLine("[ChatWindow] Closed");
        }
    }
}
