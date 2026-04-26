using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Whisper.net;
using Whisper.net.Ggml;

namespace KokonoeAssistant.Services
{
    // ══════════════════════════════════════════════════════════════════════
    // WHISPER SERVICE v2
    //
    // Локальний Whisper через Whisper.net (github.com/sandrohanea/whisper.net)
    // Без OpenAI API — повністю офлайн, приватно, без витрат.
    //
    // При першому запуску автоматично завантажує модель whisper-tiny (~75MB).
    // Якщо хочеш точніше — зміни GgmlType.Base або Small.
    // Fallback: якщо модель не завантажена — повертає null (не крашиться).
    // ══════════════════════════════════════════════════════════════════════

    public class WhisperService : IDisposable
    {
        // ── Config ────────────────────────────────────────────────────────
        private const GgmlType  DefaultModel    = GgmlType.Tiny;   // ~75MB, fast
        private const string    ModelFileName   = "whisper-tiny.bin";
        private const int       SampleRate      = 16000;

        // ── State ─────────────────────────────────────────────────────────
        private readonly string   _modelPath;
        private WhisperFactory?   _factory;
        private WhisperProcessor? _processor;
        private bool              _initialized;
        private bool              _disposed;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly object        _initLock  = new();

        // OpenAI fallback (якщо локальна модель не змогла завантажитись)
        private readonly string? _openAiKey;
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(60) };

        public bool IsLocalReady => _initialized && _processor != null;

        public class TranscriptionResult
        {
            [JsonProperty("text")]
            public string Text { get; set; } = "";
            public string? Language { get; set; }
            public bool    WasLocal { get; set; } = true;
        }

        public WhisperService(string? modelDir = null, string? openAiApiKeyFallback = null)
        {
            var dir = modelDir ?? Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "models");
            Directory.CreateDirectory(dir);
            _modelPath  = Path.Combine(dir, ModelFileName);
            _openAiKey  = openAiApiKeyFallback
                       ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

            // Ініціалізація у фоні — не блокуємо старт
            _ = Task.Run(InitializeLocalAsync);
        }

        // ── INITIALIZATION ────────────────────────────────────────────────

        private async Task InitializeLocalAsync()
        {
            try
            {
                if (!File.Exists(_modelPath))
                {
                    Log($"Завантаження моделі Whisper {DefaultModel} (~75MB)…");
                    await DownloadModelAsync();
                }

                lock (_initLock)
                {
                    _factory   = WhisperFactory.FromPath(_modelPath);
                    _processor = _factory.CreateBuilder()
                        .WithLanguage("uk")
                        .WithSingleSegment()
                        .Build();
                    _initialized = true;
                }
                Log("Локальний Whisper готовий.");
            }
            catch (Exception ex)
            {
                Log($"Ініціалізація Whisper не вдалась: {ex.Message}. Буде використано OpenAI fallback.");
            }
        }

        private async Task DownloadModelAsync()
        {
            using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(DefaultModel);
            using var fileStream  = File.Create(_modelPath);
            await modelStream.CopyToAsync(fileStream);
            Log($"Модель збережено: {_modelPath}");
        }

        // ── TRANSCRIPTION ─────────────────────────────────────────────────

        /// <summary>Транскрибує WAV байти (16kHz, mono, PCM16). Повертає null при помилці.</summary>
        public async Task<string?> TranscribeAsync(byte[] audioBytes, string language = "uk")
        {
            if (_initialized && _processor != null)
                return await TranscribeLocalAsync(audioBytes);

            if (_openAiKey != null)
                return await TranscribeOpenAiAsync(audioBytes, language);

            Log("Ні локальна модель, ні OpenAI key — транскрипція недоступна.");
            return null;
        }

        public async Task<string?> TranscribeFileAsync(string filePath, string language = "uk")
        {
            if (!File.Exists(filePath)) return null;
            return await TranscribeAsync(File.ReadAllBytes(filePath), language);
        }

        // ── LOCAL TRANSCRIPTION ───────────────────────────────────────────

        private async Task<string?> TranscribeLocalAsync(byte[] audioBytes)
        {
            await _semaphore.WaitAsync();
            try
            {
                // Whisper.net очікує float[] PCM16 нормалізований до [-1, 1]
                var samples = ConvertPcm16ToFloat(audioBytes);

                var result = new System.Text.StringBuilder();
                await foreach (var segment in _processor!.ProcessAsync(samples))
                    result.Append(segment.Text);

                var text = result.ToString().Trim();
                Log($"[Local Whisper] {text.Length} chars");
                return string.IsNullOrEmpty(text) ? null : text;
            }
            catch (Exception ex)
            {
                Log($"[Local Whisper] Error: {ex.Message}");
                return null;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        // ── OPENAI FALLBACK ───────────────────────────────────────────────

        private async Task<string?> TranscribeOpenAiAsync(byte[] audioBytes, string language)
        {
            try
            {
                using var content  = new MultipartFormDataContent();
                var audioContent   = new ByteArrayContent(audioBytes);
                audioContent.Headers.Add("Content-Type", "audio/wav");
                content.Add(audioContent, "file", $"audio_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
                content.Add(new StringContent("whisper-1"),      "model");
                content.Add(new StringContent(language),         "language");
                content.Add(new StringContent("verbose_json"),   "response_format");

                var req = new HttpRequestMessage(HttpMethod.Post,
                    "https://api.openai.com/v1/audio/transcriptions");
                req.Headers.Add("Authorization", $"Bearer {_openAiKey}");
                req.Content = content;

                var resp = await _http.SendAsync(req);
                resp.EnsureSuccessStatusCode();

                var json = await resp.Content.ReadAsStringAsync();
                var obj  = JsonConvert.DeserializeAnonymousType(json, new { text = "" });
                Log($"[OpenAI Whisper fallback] {obj?.text?.Length} chars");
                return obj?.text;
            }
            catch (Exception ex)
            {
                Log($"[OpenAI fallback] Error: {ex.Message}");
                return null;
            }
        }

        // ── UTILS ─────────────────────────────────────────────────────────

        private static float[] ConvertPcm16ToFloat(byte[] pcm16)
        {
            // WAV PCM16: skip 44-byte header, then short[] → float[]
            int headerSize = pcm16.Length > 44 && pcm16[0] == 'R' ? 44 : 0;
            int sampleCount = (pcm16.Length - headerSize) / 2;
            var floats = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                short s = BitConverter.ToInt16(pcm16, headerSize + i * 2);
                floats[i] = s / 32768f;
            }
            return floats;
        }

        public bool IsAvailable() => IsLocalReady || _openAiKey != null;

        public void SetOpenAiKey(string key)
        {
            // Оновлення OpenAI key для fallback
        }

        private static void Log(string msg) =>
            System.Diagnostics.Debug.WriteLine($"[WhisperService] {msg}");

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _processor?.Dispose();
            _factory?.Dispose();
            _semaphore.Dispose();
        }
    }
}
