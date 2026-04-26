using System;
using System.Collections.Generic;

namespace KokonoeAssistant.Services
{
    /// <summary>
    /// EventBus - Simple pub/sub for inter-window communication
    /// </summary>
    public static class EventBus
    {
        public class InsertTextEventArgs : EventArgs
        {
            public string Text { get; set; } = "";
            public string? Source { get; set; }
        }

        public class NoteSelectedEventArgs : EventArgs
        {
            public string FilePath { get; set; } = "";
            public string? Content { get; set; }
            public string Title { get; set; } = "";
        }

        public class ChatMessageEventArgs : EventArgs
        {
            public string Content { get; set; } = "";
            public string Role { get; set; } = "user";
            public DateTime Timestamp { get; set; } = DateTime.Now;
        }

        public class ErrorEventArgs : EventArgs
        {
            public string Message { get; set; } = "";
            public Exception? Exception { get; set; }
            public string Source { get; set; } = "";
        }

        // Event definitions
        public static event EventHandler<InsertTextEventArgs>? InsertTextRequested;
        public static event EventHandler<NoteSelectedEventArgs>? NoteSelected;
        public static event EventHandler<ChatMessageEventArgs>? ChatMessageReceived;
        public static event EventHandler<ErrorEventArgs>? ErrorOccurred;

        // Publish methods
        public static void PublishInsertText(string text, string? source = null)
        {
            InsertTextRequested?.Invoke(null, new InsertTextEventArgs { Text = text, Source = source });
        }

        public static void PublishNoteSelected(string filePath, string? content = null, string title = "")
        {
            NoteSelected?.Invoke(null, new NoteSelectedEventArgs { FilePath = filePath, Content = content, Title = title });
        }

        public static void PublishChatMessage(string content, string role = "user")
        {
            ChatMessageReceived?.Invoke(null, new ChatMessageEventArgs { Content = content, Role = role, Timestamp = DateTime.Now });
        }

        public static void PublishError(string message, Exception? ex = null, string source = "")
        {
            ErrorOccurred?.Invoke(null, new ErrorEventArgs { Message = message, Exception = ex, Source = source });
            System.Diagnostics.Debug.WriteLine($"[EventBus] Error from {source}: {message}");
        }

        // Unsubscribe methods to prevent memory leaks
        public static void UnsubscribeInsertText(EventHandler<InsertTextEventArgs> handler)
        {
            InsertTextRequested -= handler;
        }

        public static void UnsubscribeNoteSelected(EventHandler<NoteSelectedEventArgs> handler)
        {
            NoteSelected -= handler;
        }

        public static void UnsubscribeChatMessage(EventHandler<ChatMessageEventArgs> handler)
        {
            ChatMessageReceived -= handler;
        }

        public static void UnsubscribeError(EventHandler<ErrorEventArgs> handler)
        {
            ErrorOccurred -= handler;
        }

        // Helper for subscribing in UI code (returns unsubscribe action)
        public static Action SubscribeChatMessage(EventHandler<ChatMessageEventArgs> handler)
        {
            ChatMessageReceived += handler;
            return () => ChatMessageReceived -= handler;
        }

        public static Action SubscribeInsertText(EventHandler<InsertTextEventArgs> handler)
        {
            InsertTextRequested += handler;
            return () => InsertTextRequested -= handler;
        }
    }
}
