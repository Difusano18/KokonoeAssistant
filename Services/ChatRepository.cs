using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace KokonoeAssistant.Services
{
    /// <summary>
    /// ChatRepository - SQLite-based chat message storage
    /// Supports threading, pinning, formatting, full persistence
    /// </summary>
    public class ChatRepository : IDisposable
    {
        public class ChatMessage
        {
            public string Id { get; set; } = Guid.NewGuid().ToString();
            public DateTime Timestamp { get; set; } = DateTime.Now;
            public string Content { get; set; } = "";
            public string Role { get; set; } = "user"; // "user" or "assistant"
            public string? ThreadId { get; set; } // null = main thread
            public string? ParentMessageId { get; set; } // for replies
            public bool IsPinned { get; set; }
            public Dictionary<string, string> FormattingTags { get; set; } = new(); // {"bold": "0-5", ...}
            public string? Author { get; set; }
            public string? Attachments { get; set; } // JSON array of file paths
            public DateTime? EditedAt { get; set; }
        }

        public class ChatThread
        {
            public string Id { get; set; } = Guid.NewGuid().ToString();
            public string Title { get; set; } = "Untitled";
            public DateTime Created { get; set; } = DateTime.Now;
            public string? ParentMessageId { get; set; }
            public int MessageCount { get; set; }
        }

        private readonly string _dbPath;
        private readonly object _lock = new();

        public ChatRepository(string? vaultPath = null)
        {
            vaultPath ??= AppSettings.Load().VaultPath;
            var dataDir = Path.Combine(vaultPath, "kokonoe-data");
            Directory.CreateDirectory(dataDir);
            _dbPath = Path.Combine(dataDir, "kokonoe-chats.db");
            
            InitializeDatabase();
            System.Diagnostics.Debug.WriteLine($"[ChatRepository] Initialized: {_dbPath}");
        }

        private void InitializeDatabase()
        {
            lock (_lock)
            {
                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();
                    using (var command = connection.CreateCommand())
                    {
                        // Messages table
                        command.CommandText = @"
                            CREATE TABLE IF NOT EXISTS messages (
                                Id TEXT PRIMARY KEY,
                                Timestamp DATETIME NOT NULL,
                                Content TEXT NOT NULL,
                                Role TEXT NOT NULL,
                                ThreadId TEXT,
                                ParentMessageId TEXT,
                                IsPinned BOOLEAN DEFAULT 0,
                                FormattingTags TEXT,
                                Author TEXT,
                                Attachments TEXT,
                                EditedAt DATETIME
                            );
                            CREATE INDEX IF NOT EXISTS idx_timestamp ON messages(Timestamp DESC);
                            CREATE INDEX IF NOT EXISTS idx_thread ON messages(ThreadId);
                            CREATE INDEX IF NOT EXISTS idx_pinned ON messages(IsPinned) WHERE IsPinned = 1;
                        ";
                        command.ExecuteNonQuery();

                        // Threads table
                        command.CommandText = @"
                            CREATE TABLE IF NOT EXISTS threads (
                                Id TEXT PRIMARY KEY,
                                Title TEXT NOT NULL,
                                Created DATETIME NOT NULL,
                                ParentMessageId TEXT
                            );
                        ";
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        public void InsertMessage(ChatMessage message)
        {
            lock (_lock)
            {
                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                            INSERT INTO messages (Id, Timestamp, Content, Role, ThreadId, ParentMessageId, 
                                                  IsPinned, FormattingTags, Author, Attachments, EditedAt)
                            VALUES (@id, @ts, @content, @role, @thread, @parent, @pinned, @tags, @author, @attach, @edited)
                        ";
                        command.Parameters.AddWithValue("@id", message.Id);
                        command.Parameters.AddWithValue("@ts", message.Timestamp);
                        command.Parameters.AddWithValue("@content", message.Content);
                        command.Parameters.AddWithValue("@role", message.Role);
                        command.Parameters.AddWithValue("@thread", (object?)message.ThreadId ?? DBNull.Value);
                        command.Parameters.AddWithValue("@parent", (object?)message.ParentMessageId ?? DBNull.Value);
                        command.Parameters.AddWithValue("@pinned", message.IsPinned ? 1 : 0);
                        command.Parameters.AddWithValue("@tags", message.FormattingTags.Count > 0 ? JsonConvert.SerializeObject(message.FormattingTags) : "{}");
                        command.Parameters.AddWithValue("@author", (object?)message.Author ?? DBNull.Value);
                        command.Parameters.AddWithValue("@attach", (object?)message.Attachments ?? DBNull.Value);
                        command.Parameters.AddWithValue("@edited", (object?)message.EditedAt ?? DBNull.Value);
                        
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        public List<ChatMessage> GetMessages(int limit = 100, string? threadId = null)
        {
            var messages = new List<ChatMessage>();
            lock (_lock)
            {
                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();
                    using (var command = connection.CreateCommand())
                    {
                        if (threadId == null)
                            command.CommandText = "SELECT * FROM messages WHERE ThreadId IS NULL ORDER BY Timestamp DESC LIMIT @limit";
                        else
                            command.CommandText = "SELECT * FROM messages WHERE ThreadId = @thread ORDER BY Timestamp DESC LIMIT @limit";
                        
                        command.Parameters.AddWithValue("@limit", limit);
                        if (threadId != null)
                            command.Parameters.AddWithValue("@thread", threadId);

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                messages.Add(new ChatMessage
                                {
                                    Id = reader["Id"].ToString() ?? "",
                                    Timestamp = DateTime.Parse(reader["Timestamp"].ToString() ?? DateTime.Now.ToString()),
                                    Content = reader["Content"].ToString() ?? "",
                                    Role = reader["Role"].ToString() ?? "user",
                                    ThreadId = reader["ThreadId"] as string,
                                    ParentMessageId = reader["ParentMessageId"] as string,
                                    IsPinned = reader["IsPinned"] is DBNull ? false : Convert.ToInt64(reader["IsPinned"]) != 0,
                                    FormattingTags = JsonConvert.DeserializeObject<Dictionary<string, string>>(reader["FormattingTags"].ToString() ?? "{}") ?? new(),
                                    Author = reader["Author"] as string,
                                    Attachments = reader["Attachments"] as string,
                                    EditedAt = reader["EditedAt"] is DBNull ? null : DateTime.Parse(reader["EditedAt"].ToString() ?? "")
                                });
                            }
                        }
                    }
                }
            }
            messages.Reverse(); // We fetched DESC (newest first), so reverse to get ASC (oldest first) chronological order
            return messages;
        }

        public List<ChatMessage> GetMessagesFromDate(DateTime from, int max = 200)
        {
            var messages = new List<ChatMessage>();
            lock (_lock)
            {
                using var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT * FROM messages WHERE Timestamp >= @from ORDER BY Timestamp ASC LIMIT @max";
                command.Parameters.AddWithValue("@from", from.ToString("yyyy-MM-dd HH:mm:ss"));
                command.Parameters.AddWithValue("@max", max);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    messages.Add(new ChatMessage
                    {
                        Id        = reader["Id"].ToString() ?? "",
                        Timestamp = DateTime.Parse(reader["Timestamp"].ToString() ?? DateTime.Now.ToString()),
                        Content   = reader["Content"].ToString() ?? "",
                        Role      = reader["Role"].ToString() ?? "user",
                        Author    = reader["Author"] as string,
                    });
                }
            }
            return messages;
        }

        public List<ChatMessage> SearchMessages(string query, int limit = 50)
        {
            var messages = new List<ChatMessage>();
            lock (_lock)
            {
                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                            SELECT * FROM messages 
                            WHERE Content LIKE @query 
                            ORDER BY Timestamp DESC 
                            LIMIT @limit
                        ";
                        command.Parameters.AddWithValue("@query", $"%{query}%");
                        command.Parameters.AddWithValue("@limit", limit);

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                messages.Add(new ChatMessage
                                {
                                    Id = reader["Id"].ToString() ?? "",
                                    Timestamp = DateTime.Parse(reader["Timestamp"].ToString() ?? ""),
                                    Content = reader["Content"].ToString() ?? "",
                                    Role = reader["Role"].ToString() ?? "user",
                                    ThreadId = reader["ThreadId"] as string,
                                    IsPinned = reader["IsPinned"] is DBNull ? false : Convert.ToInt64(reader["IsPinned"]) != 0,
                                });
                            }
                        }
                    }
                }
            }
            return messages;
        }

        public List<ChatMessage> GetPinnedMessages(int limit = 20)
        {
            var messages = new List<ChatMessage>();
            lock (_lock)
            {
                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT * FROM messages WHERE IsPinned = 1 ORDER BY Timestamp DESC LIMIT @limit";
                        command.Parameters.AddWithValue("@limit", limit);

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                messages.Add(new ChatMessage
                                {
                                    Id = reader["Id"].ToString() ?? "",
                                    Timestamp = DateTime.Parse(reader["Timestamp"].ToString() ?? ""),
                                    Content = reader["Content"].ToString() ?? "",
                                    Role = reader["Role"].ToString() ?? "user",
                                    IsPinned = true
                                });
                            }
                        }
                    }
                }
            }
            return messages;
        }

        public void TogglePinMessage(string messageId, bool pin)
        {
            lock (_lock)
            {
                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "UPDATE messages SET IsPinned = @pinned WHERE Id = @id";
                        command.Parameters.AddWithValue("@pinned", pin ? 1 : 0);
                        command.Parameters.AddWithValue("@id", messageId);
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        public void DeleteMessage(string messageId)
        {
            lock (_lock)
            {
                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "DELETE FROM messages WHERE Id = @id";
                        command.Parameters.AddWithValue("@id", messageId);
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        public void ClearAll()
        {
            lock (_lock)
            {
                using var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "DELETE FROM messages";
                cmd.ExecuteNonQuery();
            }
        }

        public int GetMessageCount()
        {
            lock (_lock)
            {
                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT COUNT(*) FROM messages";
                        var result = command.ExecuteScalar();
                        return result is long l ? (int)l : result is int i ? i : 0;
                    }
                }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                try
                {
                    // SQLiteConnection is created fresh for each query, so no persistent connection to close
                    // Just log for now - if we implement connection pooling later, close pool here
                    System.Diagnostics.Debug.WriteLine("[ChatRepository] Disposed");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ChatRepository] Dispose error: {ex.Message}");
                }
            }
        }
    }
}
