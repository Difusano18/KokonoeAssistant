using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using Newtonsoft.Json;
using Whisper.net;
using Whisper.net.Ggml;

namespace KokonoeAssistant.Services
{
    public class WhisperService : IDisposable
    {
        private const GgmlType DefaultModel = GgmlType.Tiny;
        private const string ModelFileName = "whisper-tiny.bin";
        private const int SampleRate = 16000;
        private const long MinimumModelBytes = 20L * 1024 * 1024;

        private readonly string _modelPath;
        private WhisperFactory? _factory;
        private WhisperProcessor? _processor;
        private bool _initialized;
        private bool _disposed;
        private readonly Task _initTask;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly object _initLock = new();
        private string _lastTranscriptionError = "";

        private readonly string? _openAiKey;
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(60) };

        public bool IsLocalReady => _initialized && _processor != null;
        public string LastTranscriptionError => _lastTranscriptionError;

        public class TranscriptionResult
        {
            [JsonProperty("text")]
            public string Text { get; set; } = "";
            public string? Language { get; set; }
            public bool WasLocal { get; set; } = true;
        }

        public WhisperService(string? modelDir = null, string? openAiApiKeyFallback = null)
        {
            var dir = modelDir ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models");
            Directory.CreateDirectory(dir);
            _modelPath = Path.Combine(dir, ModelFileName);
            _openAiKey = openAiApiKeyFallback ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

            _initTask = Task.Run(InitializeLocalAsync);
        }

        private async Task InitializeLocalAsync()
        {
            try
            {
                if (!LooksLikeValidModel(_modelPath))
                {
                    if (File.Exists(_modelPath))
                    {
                        var bytes = new FileInfo(_modelPath).Length;
                        Log($"model rejected: {_modelPath}; bytes={bytes}; expected>={MinimumModelBytes}");
                        TryDelete(_modelPath);
                    }

                    Log($"downloading Whisper model {DefaultModel} to {_modelPath}");
                    await DownloadModelAsync();
                }

                var modelBytes = new FileInfo(_modelPath).Length;
                lock (_initLock)
                {
                    _factory = WhisperFactory.FromPath(_modelPath);
                    _processor = _factory.CreateBuilder()
                        .WithLanguage("uk")
                        .WithSingleSegment()
                        .Build();
                    _initialized = true;
                }

                Log($"local Whisper ready; modelBytes={modelBytes}; model={_modelPath}");
            }
            catch (Exception ex)
            {
                _lastTranscriptionError = $"local Whisper initialization failed: {DescribeException(ex)}";
                Log($"{_lastTranscriptionError}; OpenAI fallback available={_openAiKey != null}");
            }
        }

        private async Task DownloadModelAsync()
        {
            using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(DefaultModel);
            using (var fileStream = File.Create(_modelPath))
                await modelStream.CopyToAsync(fileStream);

            if (!LooksLikeValidModel(_modelPath))
            {
                var bytes = File.Exists(_modelPath) ? new FileInfo(_modelPath).Length : 0;
                throw new InvalidDataException($"Whisper model is missing or too small: {_modelPath}; bytes={bytes}");
            }

            Log($"model saved: {_modelPath}; bytes={new FileInfo(_modelPath).Length}");
        }

        public async Task<string?> TranscribeAsync(byte[] audioBytes, string language = "uk")
        {
            _lastTranscriptionError = "";

            if (_initialized && _processor != null)
                return await TranscribeLocalAsync(audioBytes);

            if (!_initTask.IsCompleted)
            {
                Log("local model not ready; waiting for initialization before fallback");
                try
                {
                    await _initTask;
                }
                catch (Exception ex)
                {
                    _lastTranscriptionError = "Whisper initialization failed: " + DescribeException(ex);
                    Log(_lastTranscriptionError);
                }

                if (_initialized && _processor != null)
                    return await TranscribeLocalAsync(audioBytes);
            }

            if (_openAiKey != null)
                return await TranscribeOpenAiAsync(audioBytes, language);

            _lastTranscriptionError = "transcription unavailable: no local model and no OpenAI key";
            Log(_lastTranscriptionError);
            return null;
        }

        public async Task<string?> TranscribeFileAsync(string filePath, string language = "uk")
        {
            if (!File.Exists(filePath))
            {
                Log($"transcribe file missing: {filePath}");
                return null;
            }

            return await TranscribeAsync(File.ReadAllBytes(filePath), language);
        }

        private async Task<string?> TranscribeLocalAsync(byte[] audioBytes)
        {
            await _semaphore.WaitAsync();
            try
            {
                var samples = ConvertWavOrPcmToFloat16kMono(audioBytes, out var peak, out var rms, out var gain);
                Log($"audio prepared: inputBytes={audioBytes.Length}; samples={samples.Length}; peak={peak:F4}; rms={rms:F4}; gain={gain:F2}");

                if (samples.Length < SampleRate / 2)
                {
                    _lastTranscriptionError = "transcription skipped: audio shorter than 0.5s";
                    Log(_lastTranscriptionError);
                    return null;
                }

                if (peak < 0.001f)
                {
                    _lastTranscriptionError = "transcription skipped: silence/near-silence detected";
                    Log(_lastTranscriptionError);
                    return null;
                }

                var result = new System.Text.StringBuilder();
                await foreach (var segment in _processor!.ProcessAsync(samples))
                    result.Append(segment.Text);

                var text = result.ToString().Trim();
                Log($"local Whisper result chars={text.Length}");
                if (string.IsNullOrWhiteSpace(text))
                    _lastTranscriptionError = $"local Whisper returned empty text; peak={peak:F4}; rms={rms:F4}; gain={gain:F2}";
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }
            catch (Exception ex)
            {
                _lastTranscriptionError = "local Whisper error: " + DescribeException(ex);
                Log(_lastTranscriptionError);
                return null;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task<string?> TranscribeOpenAiAsync(byte[] audioBytes, string language)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                var audioContent = new ByteArrayContent(audioBytes);
                audioContent.Headers.Add("Content-Type", "audio/wav");
                content.Add(audioContent, "file", $"audio_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
                content.Add(new StringContent("whisper-1"), "model");
                content.Add(new StringContent(language), "language");
                content.Add(new StringContent("verbose_json"), "response_format");

                var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/transcriptions");
                req.Headers.Add("Authorization", $"Bearer {_openAiKey}");
                req.Content = content;

                var resp = await _http.SendAsync(req);
                resp.EnsureSuccessStatusCode();

                var json = await resp.Content.ReadAsStringAsync();
                var obj = JsonConvert.DeserializeAnonymousType(json, new { text = "" });
                Log($"OpenAI Whisper fallback result chars={obj?.text?.Length ?? 0}");
                return obj?.text;
            }
            catch (Exception ex)
            {
                _lastTranscriptionError = "OpenAI fallback error: " + DescribeException(ex);
                Log(_lastTranscriptionError);
                return null;
            }
        }

        private static float[] ConvertWavOrPcmToFloat16kMono(byte[] audioBytes, out float peak, out float rms, out float gain)
        {
            if (LooksLikeRiffWave(audioBytes))
            {
                try
                {
                    using var input = new MemoryStream(audioBytes);
                    using var reader = new WaveFileReader(input);
                    Log($"wav input format: {reader.WaveFormat.SampleRate}Hz/{reader.WaveFormat.BitsPerSample}bit/{reader.WaveFormat.Channels}ch/{reader.WaveFormat.Encoding}");

                    if (reader.WaveFormat.SampleRate == SampleRate &&
                        reader.WaveFormat.BitsPerSample == 16 &&
                        reader.WaveFormat.Channels == 1 &&
                        reader.WaveFormat.Encoding == WaveFormatEncoding.Pcm)
                    {
                        var pcm = ReadWaveDataBytes(reader);
                        return ConvertPcm16ToFloat(pcm, 0, out peak, out rms, out gain);
                    }

                    using var resampler = new MediaFoundationResampler(reader, new WaveFormat(SampleRate, 16, 1))
                    {
                        ResamplerQuality = 60
                    };
                    using var converted = new MemoryStream();
                    WaveFileWriter.WriteWavFileToStream(converted, resampler);
                    var wav = converted.ToArray();
                    Log($"wav resampled for Whisper: bytes={wav.Length}; target={SampleRate}Hz/16bit/mono");
                    return ConvertPcm16ToFloat(wav, 44, out peak, out rms, out gain);
                }
                catch (Exception ex)
                {
                    Log($"wav parse/resample failed, raw PCM fallback: {DescribeException(ex)}");
                }
            }

            var headerSize = audioBytes.Length > 44 && audioBytes[0] == 'R' ? 44 : 0;
            return ConvertPcm16ToFloat(audioBytes, headerSize, out peak, out rms, out gain);
        }

        private static byte[] ReadWaveDataBytes(WaveFileReader reader)
        {
            using var output = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
                output.Write(buffer, 0, read);
            return output.ToArray();
        }

        private static float[] ConvertPcm16ToFloat(byte[] pcm16, int headerSize, out float peak, out float rms, out float gain)
        {
            var payloadBytes = Math.Max(0, pcm16.Length - headerSize);
            var sampleCount = payloadBytes / 2;
            var floats = new float[sampleCount];
            peak = 0;
            double sumSquares = 0;

            for (var i = 0; i < sampleCount; i++)
            {
                var offset = headerSize + i * 2;
                if (offset + 1 >= pcm16.Length)
                    break;

                var value = BitConverter.ToInt16(pcm16, offset) / 32768f;
                var abs = Math.Abs(value);
                if (abs > peak)
                    peak = abs;
                sumSquares += value * value;
                floats[i] = value;
            }

            rms = sampleCount > 0 ? (float)Math.Sqrt(sumSquares / sampleCount) : 0;
            gain = peak > 0.001f && peak < 0.12f ? Math.Clamp(0.35f / peak, 1f, 8f) : 1f;

            if (gain > 1.01f)
            {
                for (var i = 0; i < floats.Length; i++)
                    floats[i] = Math.Clamp(floats[i] * gain, -1f, 1f);
            }

            return floats;
        }

        private static bool LooksLikeRiffWave(byte[] bytes) =>
            bytes.Length >= 12 &&
            bytes[0] == 'R' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == 'F' &&
            bytes[8] == 'W' && bytes[9] == 'A' && bytes[10] == 'V' && bytes[11] == 'E';

        private static bool LooksLikeValidModel(string path)
        {
            try
            {
                return File.Exists(path) && new FileInfo(path).Length >= MinimumModelBytes;
            }
            catch
            {
                return false;
            }
        }

        public bool IsAvailable() => IsLocalReady || _openAiKey != null;

        public void SetOpenAiKey(string key)
        {
            Log("SetOpenAiKey called; runtime key replacement is not wired yet");
        }

        private static void TryDelete(string path)
        {
            try { File.Delete(path); }
            catch (Exception ex) { Log($"failed to delete invalid model: {DescribeException(ex)}"); }
        }

        private static string DescribeException(Exception ex)
        {
            var hresult = ex.HResult != 0 ? $" HResult=0x{ex.HResult:X8}" : "";
            return $"{ex.GetType().Name}:{hresult} {ex.Message}";
        }

        private static void Log(string msg)
        {
            System.Diagnostics.Debug.WriteLine($"[WhisperService] {msg}");
            KokoSystemLog.Write("WHISPER", msg);
        }

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
