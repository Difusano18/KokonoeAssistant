using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using TgUpdate = Telegram.Bot.Types.Update;

namespace KokonoeAssistant.Services
{
    /// <summary>
    /// MainWindow.TelegramBot.cs's InitTelegram()/HandleTgUpdate is the only place that ever
    /// called TelegramBotClient.StartReceiving - and it only runs when UiShell=legacy-wpf.
    /// The default UiShell is "web" (ShellWindow), which never constructs MainWindow at all,
    /// so in that mode the bot has never actually received or answered a Telegram message;
    /// KokoWebTelegramBridgeService only ever reported status, it never started anything.
    ///
    /// This is a minimal, UI-independent core: receive a text message from the authorized
    /// chat, answer through the same BuildUnifiedExternalContext/LlmService.SendAsync pipeline
    /// web chat uses (so every persona/context fix this session applies here too), send the
    /// reply back. It intentionally does not replicate every special-case branch the WPF
    /// handler had (image messages, observation/overlord/control-command routing, inline
    /// keyboards) - those are real features but a much larger port; this restores basic chat.
    /// </summary>
    public sealed class KokoTelegramReceiverService : IDisposable
    {
        private readonly LlmService _llm;
        private readonly KokoTelegramRuntimeStatusService _status;
        private CancellationTokenSource? _cts;
        private long _chatId;
        private bool _disposed;
        private bool _started;

        public KokoTelegramReceiverService(LlmService llm, KokoTelegramRuntimeStatusService status)
        {
            _llm = llm ?? throw new ArgumentNullException(nameof(llm));
            _status = status ?? throw new ArgumentNullException(nameof(status));
        }

        public void Start()
        {
            if (_started) return;

            var settings = AppSettings.Load();
            _status.RefreshConfiguration(settings);

            if (!settings.TelegramEnabled)
            {
                _status.MarkBotState("disabled");
                return;
            }
            if (string.IsNullOrWhiteSpace(settings.TelegramToken) || settings.TelegramChatId <= 0)
            {
                _status.MarkBotState("not_configured");
                return;
            }

            try
            {
                _chatId = settings.TelegramChatId;
                _cts = new CancellationTokenSource();
                var bot = new TelegramBotClient(settings.TelegramToken);
                bot.StartReceiving(
                    HandleUpdate,
                    HandleError,
                    new ReceiverOptions { AllowedUpdates = new[] { UpdateType.Message } },
                    _cts.Token);
                _started = true;
                _status.MarkBotState("listening");
                KokoSystemLog.Write("TG", "KokoTelegramReceiverService started receiving (web shell path)");
            }
            catch (Exception ex)
            {
                _status.RecordBotError(ex.Message);
                KokoSystemLog.Write("TG-CATCH", "KokoTelegramReceiverService start failed: " + ex.Message);
            }
        }

        private async void HandleUpdate(ITelegramBotClient bot, TgUpdate update, CancellationToken ct)
        {
            try
            {
                var msg = update.Message;
                var text = msg?.Text;
                if (msg == null || string.IsNullOrWhiteSpace(text) || msg.Chat.Id != _chatId)
                    return;

                var from = msg.From?.Username ?? msg.From?.FirstName ?? "Yasu";
                _status.RecordBotActivity("incoming");
                _status.RecordBotMessage(from, text);

                try { ServiceContainer.BrainEngine?.ProcessUserMessage(text); }
                catch (Exception ex) { KokoSystemLog.Write("TG-CATCH", "ProcessUserMessage failed: " + ex.Message); }

                var context = await Task.Run(
                    () => ServiceContainer.BrainEngine?.BuildUnifiedExternalContext("telegram", text),
                    ct).ConfigureAwait(false);

                var prompt =
                    $"Telegram direct message from {from}:\n{text}\n\n" +
                    "Answer only to this latest message. Ukrainian only, concise, natural.";
                var reply = await _llm.SendAsync(prompt, extraContext: context, ct: ct, agentId: "chat").ConfigureAwait(false);

                try { await bot.SendMessage(msg.Chat.Id, reply, cancellationToken: ct).ConfigureAwait(false); }
                catch (Exception ex) { KokoSystemLog.Write("TG-CATCH", "SendMessage failed: " + ex.Message); }
                _status.RecordBotActivity("sent");

                try
                {
                    ServiceContainer.ChatRepository.InsertMessage(new ChatRepository.ChatMessage
                    { Content = text, Role = "user", Author = "Telegram:" + from, Timestamp = DateTime.Now });
                    ServiceContainer.ChatRepository.InsertMessage(new ChatRepository.ChatMessage
                    { Content = reply, Role = "assistant", Author = "Kokonoe", Timestamp = DateTime.Now });
                    _ = Task.Run(() => ServiceContainer.ChatLogger.LogExchange("tg", text, reply));
                }
                catch (Exception ex) { KokoSystemLog.Write("TG-CATCH", "chat persistence failed: " + ex.Message); }

                _status.MarkBotState("listening");
            }
            catch (Exception ex)
            {
                KokoSystemLog.Write("TG-CATCH", "HandleUpdate failed: " + ex);
            }
        }

        private void HandleError(ITelegramBotClient bot, Exception ex, CancellationToken ct)
        {
            _status.RecordBotError(ex.Message);
            KokoSystemLog.Write("TG-CATCH", "polling error: " + ex.Message);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
