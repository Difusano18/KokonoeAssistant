using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TL;
using WTelegram;

namespace KokonoeAssistant.Services
{
    /// <summary>
    /// Повний доступ до Telegram акаунту через MTProto (WTelegramClient 3.7.x).
    /// Слухає ВСІ апдейти: DM, групи, канали.
    /// </summary>
    public class TelegramUserService : IDisposable
    {
        // ── Events ─────────────────────────────────────────────────
        public event Action<TgIncomingMessage>? OnMessage;
        public event Action<string>?            OnStatusChanged;

        // ── State ──────────────────────────────────────────────────
        public bool   IsConnected { get; private set; }
        public string MySelf      { get; private set; } = "";
        public long   MyUserId    { get; private set; }

        // ── Auth callback — WPF встановлює Func для показу діалогу ─
        public Func<string, Task<string>>? AskForInput;

        // ── Internals ──────────────────────────────────────────────
        private Client? _client;
        private readonly string _sessionPath;
        private readonly int    _apiId;
        private readonly string _apiHash;
        private readonly string _phone;
        private readonly bool   _dmOnly;

        // Кеш імен: peer_id → display name, input peer
        private readonly Dictionary<long, string>    _names = new();
        private readonly Dictionary<long, InputPeer> _peers = new();

        public TelegramUserService(int apiId, string apiHash, string phone, string dataDir, bool dmOnly = false)
        {
            _apiId       = apiId;
            _apiHash     = apiHash;
            _phone       = phone;
            _dmOnly      = dmOnly;
            _sessionPath = Path.Combine(dataDir, "tg_session.dat");
            Directory.CreateDirectory(dataDir);
        }

        // ── Connect ────────────────────────────────────────────────

        public async Task ConnectAsync(CancellationToken ct = default)
        {
            if (_apiId == 0 || string.IsNullOrEmpty(_apiHash))
                throw new InvalidOperationException("Не задані api_id / api_hash. Зайди на my.telegram.org → App API.");

            WTelegram.Helpers.Log = (lvl, msg) =>
                System.Diagnostics.Debug.WriteLine($"[WTG:{lvl}] {msg}");

            _client = new Client(Config);
            _client.OnUpdate += HandleUpdates;       // OnUpdate (без s) — правильна назва в 3.7.x

            OnStatusChanged?.Invoke("підключення...");

            var user = await _client.LoginUserIfNeeded();
            MySelf    = $"{user.first_name} {user.last_name}".Trim();
            MyUserId  = user.id;
            IsConnected = true;

            OnStatusChanged?.Invoke($"✓ {MySelf}");
            System.Diagnostics.Debug.WriteLine($"[TgUser] Logged in as {MySelf} (id={MyUserId})");

            await PreloadDialogsAsync();
        }

        // ── WTelegramClient config delegate ────────────────────────

        private string Config(string what) => what switch
        {
            "api_id"            => _apiId.ToString(),
            "api_hash"          => _apiHash,
            "phone_number"      => _phone,
            "session_pathname"  => _sessionPath,
            "verification_code" => AskSync("Введи код з Telegram:"),
            "password"          => AskSync("Введи пароль 2FA:"),
            "first_name"        => "Kokonoe",
            "last_name"         => "",
            _                   => null!
        };

        private string AskSync(string prompt)
        {
            if (AskForInput == null) throw new InvalidOperationException("AskForInput не встановлено");
            return AskForInput(prompt).GetAwaiter().GetResult();
        }

        // ── Preload dialogs ────────────────────────────────────────

        private async Task PreloadDialogsAsync()
        {
            try
            {
                var result = await _client!.Messages_GetAllDialogs();
                // Messages_Dialogs вже має .users і .chats словники напряму
                CacheNames(result.users, result.chats);

                System.Diagnostics.Debug.WriteLine(
                    $"[TgUser] Preloaded {_names.Count} names from {result.dialogs.Length} dialogs");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TgUser] PreloadDialogs failed: {ex.Message}");
            }
        }

        // ── Updates handler ────────────────────────────────────────

        private async Task HandleUpdates(UpdatesBase updates)
        {
            // UpdatesBase вже має .Users і .Chats в 3.7.x
            CacheNames(updates.Users, updates.Chats);

            foreach (var update in updates.UpdateList)
            {
                TL.Message? msg = null;
                if (update is UpdateNewMessage unm && unm.message is TL.Message m1)
                    msg = m1;
                else if (update is UpdateNewChannelMessage uncm && uncm.message is TL.Message m2)
                    msg = m2;

                if (msg != null)
                    ProcessMessage(msg, updates.Users, updates.Chats);
            }

            await Task.CompletedTask;
        }

        private void ProcessMessage(
            TL.Message msg,
            Dictionary<long, User> users,
            Dictionary<long, ChatBase> chats)
        {
            try
            {
                // Ігноруємо вихідні повідомлення (надіслані з цього акаунту)
                // flags.HasFlag(out_) — надійніший спосіб ніж перевірка from_id,
                // бо для вихідних повідомлень from_id часто null
                if ((msg.flags & TL.Message.Flags.out_) != 0) return;

                // Додатковий захист: ігноруємо якщо from_id явно вказує на нас
                if (msg.from_id is PeerUser selfCheck && selfCheck.user_id == MyUserId) return;

                var text = msg.message;
                if (string.IsNullOrWhiteSpace(text)) return;

                var (chatId, chatName, chatType) = ResolvePeer(msg.peer_id, users, chats);

                if (_dmOnly && chatType != TgChatType.Private) return;

                var senderName = ResolveUser(msg.from_id, users);

                var incoming = new TgIncomingMessage
                {
                    ChatId    = chatId,
                    ChatName  = chatName,
                    ChatType  = chatType,
                    Sender    = senderName,
                    Text      = text,
                    MessageId = msg.id,
                    Date      = msg.date.ToLocalTime()
                };

                System.Diagnostics.Debug.WriteLine(
                    $"[TgUser] {chatType} '{chatName}' від '{senderName}': {text[..Math.Min(80, text.Length)]}");

                OnMessage?.Invoke(incoming);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TgUser] ProcessMessage error: {ex.Message}");
            }
        }

        // ── Send ───────────────────────────────────────────────────

        public async Task SendAsync(long chatId, string text, CancellationToken ct = default)
        {
            if (_client == null || !IsConnected) return;
            try
            {
                if (!_peers.TryGetValue(chatId, out var peer))
                {
                    var result = await _client.Messages_GetAllDialogs();
                    CacheNames(result.users, result.chats);

                    if (result.users.TryGetValue(chatId, out var u))
                        { peer = u.ToInputPeer(); _peers[chatId] = peer; }
                    else if (result.chats.TryGetValue(chatId, out var c))
                        { peer = c.ToInputPeer(); _peers[chatId] = peer; }
                }

                if (peer != null)
                    await _client.SendMessageAsync(peer, text);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TgUser] Send failed to {chatId}: {ex.Message}");
            }
        }

        // ── Get dialogs for UI ─────────────────────────────────────

        public async Task<List<TgDialog>> GetDialogsAsync(int limit = 50)
        {
            if (_client == null || !IsConnected) return new();
            try
            {
                var result = await _client.Messages_GetAllDialogs();
                CacheNames(result.users, result.chats);

                var list = new List<TgDialog>();
                foreach (var dialog in result.dialogs.Take(limit))
                {
                    if (dialog is not Dialog d) continue;

                    var (chatId, chatName, chatType) = ResolvePeer(d.peer, result.users, result.chats);
                    if (string.IsNullOrEmpty(chatName)) continue;

                    // Кешуємо input peer для відправки
                    if (result.users.TryGetValue(chatId, out var u))
                        _peers[chatId] = u.ToInputPeer();
                    else if (result.chats.TryGetValue(chatId, out var c))
                        _peers[chatId] = c.ToInputPeer();

                    list.Add(new TgDialog
                    {
                        ChatId      = chatId,
                        Name        = chatName,
                        Type        = chatType,
                        UnreadCount = d.unread_count
                    });
                }
                return list;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TgUser] GetDialogs failed: {ex.Message}");
                return new();
            }
        }

        // ── Helpers ────────────────────────────────────────────────

        private void CacheNames(Dictionary<long, User> users, Dictionary<long, ChatBase> chats)
        {
            foreach (var (id, u) in users)
                _names[id] = $"{u.first_name} {u.last_name}".Trim();
            foreach (var (id, c) in chats)
                _names[id] = c.Title;
        }

        private (long id, string name, TgChatType type) ResolvePeer(
            Peer peer,
            Dictionary<long, User> users,
            Dictionary<long, ChatBase> chats)
        {
            if (peer is PeerUser pu)
            {
                var name = users.TryGetValue(pu.user_id, out var u)
                    ? $"{u.first_name} {u.last_name}".Trim()
                    : _names.GetValueOrDefault(pu.user_id, $"User_{pu.user_id}");
                return (pu.user_id, name, TgChatType.Private);
            }
            if (peer is PeerChat pc)
            {
                var name = chats.TryGetValue(pc.chat_id, out var g)
                    ? g.Title
                    : _names.GetValueOrDefault(pc.chat_id, $"Group_{pc.chat_id}");
                return (pc.chat_id, name, TgChatType.Group);
            }
            if (peer is PeerChannel pch)
            {
                var name = chats.TryGetValue(pch.channel_id, out var c)
                    ? c.Title
                    : _names.GetValueOrDefault(pch.channel_id, $"Channel_{pch.channel_id}");
                return (pch.channel_id, name, TgChatType.Channel);
            }
            return (0, "", TgChatType.Private);
        }

        private string ResolveUser(Peer? from, Dictionary<long, User> users)
        {
            if (from is PeerUser pu && users.TryGetValue(pu.user_id, out var u))
                return $"{u.first_name} {u.last_name}".Trim();
            return "Unknown";
        }

        // ── Dispose ────────────────────────────────────────────────

        public void Dispose()
        {
            try { _client?.Dispose(); } catch { }
            IsConnected = false;
        }
    }

    // ── DTOs ───────────────────────────────────────────────────────

    public enum TgChatType { Private, Group, Channel }

    public class TgIncomingMessage
    {
        public long       ChatId    { get; set; }
        public string     ChatName  { get; set; } = "";
        public TgChatType ChatType  { get; set; }
        public string     Sender    { get; set; } = "";
        public string     Text      { get; set; } = "";
        public int        MessageId { get; set; }
        public DateTime   Date      { get; set; }
    }

    public class TgDialog
    {
        public long       ChatId      { get; set; }
        public string     Name        { get; set; } = "";
        public TgChatType Type        { get; set; }
        public int        UnreadCount { get; set; }

        public string TypeIcon => Type switch
        {
            TgChatType.Channel => "📢",
            TgChatType.Group   => "👥",
            _                  => "👤"
        };
    }
}
